// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using AdvancedConditionalWorkflow.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// 専門家レビューを実行する Executor
/// State に ReviewResult を累積させるパターンを使用
/// Structured Output を使用して型安全なJSON出力を実現
/// </summary>
public class SpecialistReviewExecutor : Executor<(ContractInfo Contract, List<ReviewResult> Reviews), (ContractInfo Contract, List<ReviewResult> Reviews)>
{
    private readonly ChatClientAgent _agent;
    private readonly string _specialistName;
    private readonly ILogger? _logger;

    public SpecialistReviewExecutor(
        IChatClient chatClient,
        string specialistType, // "Legal", "Finance", "Procurement"
        string id,
        ILogger? logger = null)
        : base(id)
    {
        _specialistName = specialistType;
        _logger = logger;

        // Structured Output対応: ChatClientAgentOptionsでResponseFormatを指定
        var (instructions, agentId, agentName) = specialistType switch
        {
            "Legal" => (
                "あなたは Legal (法務) 専門家です。法的リスク、コンプライアンス、規制要件、法的義務、知的財産権などの観点から契約を分析してください。簡潔で実用的な回答を心がけてください。必ず日本語で回答してください。",
                "legal_agent",
                "Legal Agent"
            ),
            "Finance" => (
                "あなたは Finance (財務) 専門家です。財務影響、予算管理、ROI分析、キャッシュフロー、財務リスクなどの観点から契約を分析してください。簡潔で実用的な回答を心がけてください。必ず日本語で回答してください。",
                "finance_agent",
                "Finance Agent"
            ),
            "Procurement" => (
                "あなたは Procurement (調達実務) 専門家です。調達プロセス、購買手続き、契約管理、サプライヤー管理、調達戦略などの観点から契約を分析してください。簡潔で実用的な回答を心がけてください。必ず日本語で回答してください。",
                "procurement_agent",
                "Procurement Agent"
            ),
            _ => throw new ArgumentException($"Unknown specialist type: {specialistType}")
        };

        _agent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions(instructions, agentId, agentName)
            {
                ChatOptions = new()
                {
                    ResponseFormat = ChatResponseFormat.ForJsonSchema<ReviewResult>()
                }
            });
    }

    public override async ValueTask<(ContractInfo Contract, List<ReviewResult> Reviews)> HandleAsync(
        (ContractInfo Contract, List<ReviewResult> Reviews) input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation("🔍 {SpecialistName} による契約レビューを開始", _specialistName);

        var contract = input.Contract;
        var penaltyClause = contract.HasPenaltyClause ? "あり" : "なし";
        var autoRenewal = contract.HasAutoRenewal ? "あり" : "なし";
        var description = string.IsNullOrEmpty(contract.Description) ? "" : $"- 説明: {contract.Description}";

        var prompt = $@"以下の契約情報をレビューし、評価を返してください。

【契約情報】
- サプライヤー: {contract.SupplierName}
- 契約金額: ${contract.ContractValue:N0}
- 契約期間: {contract.ContractTermMonths}ヶ月
- 支払条件: {contract.PaymentTerms}
- 納品条件: {contract.DeliveryTerms}
- 保証期間: {contract.WarrantyPeriodMonths}ヶ月
- ペナルティ条項: {penaltyClause}
- 自動更新: {autoRenewal}
{description}

あなたの専門分野から評価してください:
- opinion: あなたの総合的な所見
- risk_score: リスクスコア(0-100の整数、100が最高リスク)
- concerns: 懸念点のリスト
- recommendations: 推奨事項のリスト";

        var messages = new[] { new ChatMessage(ChatRole.User, prompt) };

        // Azure OpenAI 呼び出しと Structured Output による JSON パース
        try
        {
            var response = await _agent.RunAsync(messages, cancellationToken: cancellationToken);
            var responseText = response.Messages?.LastOrDefault()?.Text ?? "";

            _logger?.LogInformation("  AIレスポンス受信: {Length}文字", responseText.Length);

            if (string.IsNullOrWhiteSpace(responseText))
            {
                throw new InvalidOperationException("AIからのレスポンスが空です");
            }

            // Structured Outputなので直接デシリアライズ可能
            var reviewResult = JsonSerializer.Deserialize<ReviewResult>(responseText);

            if (reviewResult == null)
            {
                throw new InvalidOperationException("JSON デシリアライズに失敗しました");
            }

            // ReviewerフィールドをSpecialistNameに更新 (record型のwithを使用)
            var result = reviewResult with { Reviewer = _specialistName };

            _logger?.LogInformation("✓ {SpecialistName} レビュー完了 (リスクスコア: {RiskScore})",
                _specialistName, result.RiskScore);

            // レビュー詳細をログ出力
            _logger?.LogInformation("  所見: {Opinion}", result.Opinion);
            if (result.Concerns != null && result.Concerns.Count > 0)
            {
                _logger?.LogInformation("  懸念事項:");
                foreach (var concern in result.Concerns)
                {
                    _logger?.LogInformation("    - {Concern}", concern);
                }
            }
            if (result.Recommendations != null && result.Recommendations.Count > 0)
            {
                _logger?.LogInformation("  推奨事項:");
                foreach (var recommendation in result.Recommendations)
                {
                    _logger?.LogInformation("    - {Recommendation}", recommendation);
                }
            }

            // 既存のレビューリストに追加
            var updatedReviews = new List<ReviewResult>(input.Reviews) { result };
            return (input.Contract, updatedReviews);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ {SpecialistName} のレビュー処理中にエラー発生: {ErrorMessage}",
                _specialistName, ex.Message);

            // フォールバック: 安全側に高リスクとして返す
            var fallbackResult = new ReviewResult
            {
                Reviewer = _specialistName,
                Opinion = $"エラーが発生しました: {ex.Message}",
                RiskScore = 70, // デフォルトで中リスク
                Concerns = new List<string> { "レビュー処理中にエラーが発生しました" },
                Recommendations = new List<string> { "手動での再レビューを推奨" }
            };

            var updatedReviews = new List<ReviewResult>(input.Reviews) { fallbackResult };
            return (input.Contract, updatedReviews);
        }
    }
}
