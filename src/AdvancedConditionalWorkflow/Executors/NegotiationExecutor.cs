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
public class NegotiationExecutor : Executor<(ContractInfo Contract, RiskAssessment Risk, int Iteration), NegotiationProposal>
{
    private readonly ChatClientAgent _agent;
    private readonly ILogger? _logger;

    public NegotiationExecutor(IChatClient chatClient, ILogger? logger = null, string id = "negotiation_executor")
        : base(id)
    {
        _agent = AgentFactory.CreateNegotiationAgent(chatClient);
        _logger = logger;
    }

    public override async ValueTask<NegotiationProposal> HandleAsync(
        (ContractInfo Contract, RiskAssessment Risk, int Iteration) input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        var (contract, risk, iteration) = input;

        _logger?.LogInformation("💼 交渉提案を生成中 (反復 {Iteration}/3)...", iteration);

        var concerns = risk.KeyConcerns != null && risk.KeyConcerns.Count > 0
            ? string.Join("\n", risk.KeyConcerns.Select((c, i) => $"{i + 1}. {c}"))
            : "特になし";

        var prompt = $@"以下の契約について、リスクを軽減するための交渉提案を生成してください。

【現在の契約条件】
- サプライヤー: {contract.SupplierName}
- 契約金額: ${contract.ContractValue:N0}
- 契約期間: {contract.ContractTermMonths}ヶ月
- 支払条件: {contract.PaymentTerms}
- 納品条件: {contract.DeliveryTerms}

【リスク評価】
- 総合リスクスコア: {risk.OverallRiskScore}/100
- リスクレベル: {risk.RiskLevel}

【主要な懸念事項】
{concerns}

【交渉目標】
- 目標リスクスコア: 30以下 (低リスク領域)
- 現在の反復回数: {iteration}/3

【出力形式】
以下のJSON形式で3-5個の具体的な交渉提案を返してください:
{{
  ""proposals"": [
    ""提案1: 具体的な交渉内容"",
    ""提案2: 具体的な交渉内容"",
    ""提案3: 具体的な交渉内容""
  ],
  ""rationale"": ""これらの提案がリスクを軽減する理由""
}}";

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

            var result = new NegotiationProposal
            {
                Iteration = iteration,
                Proposals = proposals,
                TargetRiskScore = 30,
                Rationale = proposalData["rationale"].GetString() ?? "理由なし"
            };

            _logger?.LogInformation("✓ {ProposalCount}件の交渉提案を生成しました", proposals.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ 交渉提案のパースに失敗");

            // フォールバック
            return new NegotiationProposal
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
