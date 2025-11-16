// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using AdvancedConditionalWorkflow.Models;
using Common;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// 交渉提案を生成する Executor
/// </summary>
public class NegotiationExecutor : Executor<NegotiationStateOutput, NegotiationExecutionOutput>
{
    private readonly ChatClientAgent _agent;
    private readonly ILogger? _logger;

    public NegotiationExecutor(IChatClient chatClient, ILogger? logger = null, string id = "negotiation_executor")
        : base(id)
    {
        _agent = AgentFactory.CreateNegotiationAgent(chatClient);
        _logger = logger;
    }

    public override async ValueTask<NegotiationExecutionOutput> HandleAsync(
        NegotiationStateOutput input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        var contract = input.Contract;
        var risk = input.Risk;
        var iteration = input.Iteration;

        using var activity = Common.TelemetryHelper.StartActivity(
            Program.ActivitySource,
            "NegotiationProposalGeneration",
            new Dictionary<string, object>
            {
                ["iteration"] = iteration,
                ["current_risk_score"] = risk.OverallRiskScore,
                ["supplier"] = contract.SupplierName
            });

        _logger?.LogInformation("💼 交渉提案を生成中 (反復 {Iteration}/3)...", iteration);
        _logger?.LogInformation("  現在のリスクスコア: {RiskScore}/100", risk.OverallRiskScore);
        _logger?.LogInformation("  目標リスクスコア: 30以下");

        var concerns = risk.KeyConcerns != null && risk.KeyConcerns.Count > 0
            ? string.Join("\n", risk.KeyConcerns.Select((c, i) => $"{i + 1}. {c}"))
            : "特になし";

        var penaltyClauseText = contract.HasPenaltyClause ? "あり" : "なし";
        var autoRenewalText = contract.HasAutoRenewal ? "あり" : "なし";

        var prompt = $@"以下の契約について、リスクを軽減するための具体的な契約条件変更を提案してください。

【現在の契約条件】
- サプライヤー: {contract.SupplierName}
- 契約金額: ${contract.ContractValue:N0}
- 契約期間: {contract.ContractTermMonths}ヶ月
- 支払条件: {contract.PaymentTerms}
- 納品条件: {contract.DeliveryTerms}
- 保証期間: {contract.WarrantyPeriodMonths}ヶ月
- ペナルティ条項: {penaltyClauseText}
- 自動更新: {autoRenewalText}

【リスク評価】
- 総合リスクスコア: {risk.OverallRiskScore}/100
- リスクレベル: {risk.RiskLevel}

【主要な懸念事項】
{concerns}

【交渉目標】
- 目標リスクスコア: 30以下 (低リスク領域)
- 現在の反復回数: {iteration}/3

【出力形式】
以下のJSON形式で、具体的な契約条件の変更を返してください:
{{
  ""proposals"": [
    ""提案1: 具体的な交渉内容"",
    ""提案2: 具体的な交渉内容""
  ],
  ""rationale"": ""これらの提案がリスクを軽減する理由"",
  ""updated_contract"": {{
    ""warranty_period_months"": 24,
    ""penalty_clause"": true,
    ""auto_renewal"": false
  }}
}}

updated_contractには、変更する契約条件のみを含めてください。以下の項目が変更可能です:
- warranty_period_months: 保証期間（12-36ヶ月）
- penalty_clause: ペナルティ条項（true/false）
- auto_renewal: 自動更新（true/false）
- payment_terms: 支払条件（例: Net 30, Net 45）
- contract_term_months: 契約期間（短縮を推奨）";

        var messages = new[] { new ChatMessage(ChatRole.User, prompt) };
        var response = await _agent.RunAsync(messages, cancellationToken: cancellationToken);
        var responseText = response.Messages?.LastOrDefault()?.Text ?? "";

        try
        {
            var jsonContent = ExtractJsonFromResponse(responseText);
            var proposalData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonContent);

            if (proposalData == null)
            {
                throw new InvalidOperationException("JSON デシリアライズに失敗");
            }

            var proposals = proposalData["proposals"]
                .EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            // 契約条件の更新を適用
            var updatedContract = contract;
            var contractChanges = new Dictionary<string, (object? Before, object? After)>();

            if (proposalData.TryGetValue("updated_contract", out var updatedContractElement))
            {
                _logger?.LogInformation("📝 契約条件を更新中...");

                if (updatedContractElement.TryGetProperty("warranty_period_months", out var warranty))
                {
                    var newWarranty = warranty.GetInt32();
                    contractChanges["warranty_period_months"] = (contract.WarrantyPeriodMonths, newWarranty);
                    updatedContract = updatedContract with { WarrantyPeriodMonths = newWarranty };
                    _logger?.LogInformation("  保証期間: {Old}ヶ月 → {New}ヶ月", contract.WarrantyPeriodMonths, newWarranty);
                }

                if (updatedContractElement.TryGetProperty("penalty_clause", out var penalty))
                {
                    var newPenalty = penalty.GetBoolean();
                    contractChanges["penalty_clause"] = (contract.HasPenaltyClause, newPenalty);
                    updatedContract = updatedContract with { HasPenaltyClause = newPenalty };
                    _logger?.LogInformation("  ペナルティ条項: {Old} → {New}",
                        contract.HasPenaltyClause ? "あり" : "なし",
                        newPenalty ? "あり" : "なし");
                }

                if (updatedContractElement.TryGetProperty("auto_renewal", out var autoRenewal))
                {
                    var newAutoRenewal = autoRenewal.GetBoolean();
                    contractChanges["auto_renewal"] = (contract.HasAutoRenewal, newAutoRenewal);
                    updatedContract = updatedContract with { HasAutoRenewal = newAutoRenewal };
                    _logger?.LogInformation("  自動更新: {Old} → {New}",
                        contract.HasAutoRenewal ? "あり" : "なし",
                        newAutoRenewal ? "あり" : "なし");
                }

                if (updatedContractElement.TryGetProperty("payment_terms", out var paymentTerms))
                {
                    var newPaymentTerms = paymentTerms.GetString() ?? contract.PaymentTerms;
                    contractChanges["payment_terms"] = (contract.PaymentTerms, newPaymentTerms);
                    updatedContract = updatedContract with { PaymentTerms = newPaymentTerms };
                    _logger?.LogInformation("  支払条件: {Old} → {New}", contract.PaymentTerms, newPaymentTerms);
                }

                if (updatedContractElement.TryGetProperty("contract_term_months", out var termMonths))
                {
                    var newTermMonths = termMonths.GetInt32();
                    contractChanges["contract_term_months"] = (contract.ContractTermMonths, newTermMonths);
                    updatedContract = updatedContract with { ContractTermMonths = newTermMonths };
                    _logger?.LogInformation("  契約期間: {Old}ヶ月 → {New}ヶ月", contract.ContractTermMonths, newTermMonths);
                }

                _logger?.LogInformation("✓ {ChangeCount}項目の契約条件を更新しました", contractChanges.Count);
            }

            var result = new NegotiationProposal
            {
                Iteration = iteration,
                Proposals = proposals,
                TargetRiskScore = 30,
                Rationale = proposalData["rationale"].GetString() ?? "理由なし",
                ContractChanges = contractChanges.Count > 0 ? contractChanges : null
            };

            _logger?.LogInformation("✓ {ProposalCount}件の交渉提案を生成しました", proposals.Count);
            _logger?.LogInformation("  提案内容:");
            foreach (var (proposal, index) in proposals.Select((p, i) => (p, i + 1)))
            {
                _logger?.LogInformation("    {Index}. {Proposal}", index, proposal);
            }
            _logger?.LogInformation("  根拠: {Rationale}", result.Rationale);

            activity?.SetTag("proposal_count", proposals.Count);
            activity?.SetTag("rationale", result.Rationale);
            activity?.SetTag("contract_changes", contractChanges.Count);

            // Shared State に交渉履歴を保存
            try
            {
                var history = await context.ReadStateAsync<List<NegotiationProposal>>("negotiation_history",
                    scopeName: SharedStateScopes.NegotiationHistory,
                    cancellationToken: cancellationToken) ?? new List<NegotiationProposal>();

                history.Add(result);

                await context.QueueStateUpdateAsync("negotiation_history", history,
                    scopeName: SharedStateScopes.NegotiationHistory,
                    cancellationToken: cancellationToken);

                _logger?.LogInformation("💾 交渉履歴を {Scope} に保存 (合計 {Count}件)",
                    SharedStateScopes.NegotiationHistory, history.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("⚠️ 交渉履歴の保存に失敗: {Message}", ex.Message);
            }

            // 更新された契約を返す
            return new NegotiationExecutionOutput
            {
                Contract = updatedContract,
                Risk = risk,
                Proposal = result,
                Iteration = iteration
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ 交渉提案のパースに失敗");

            // フォールバック
            var fallbackProposal = new NegotiationProposal
            {
                Iteration = iteration,
                Proposals = new List<string>
                {
                    "契約金額の10%削減を提案",
                    "支払条件を Net 60 に延長",
                    "ペナルティ条項の追加"
                },
                TargetRiskScore = 30,
                Rationale = "標準的なリスク軽減策"
            };

            return new NegotiationExecutionOutput
            {
                Contract = contract,
                Risk = risk,
                Proposal = fallbackProposal,
                Iteration = iteration
            };
        }
    }

    private static string ExtractJsonFromResponse(string response)
    {
        var jsonStart = response.IndexOf('{');
        var jsonEnd = response.LastIndexOf('}');

        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            return response.Substring(jsonStart, jsonEnd - jsonStart + 1);
        }

        return response;
    }
}
