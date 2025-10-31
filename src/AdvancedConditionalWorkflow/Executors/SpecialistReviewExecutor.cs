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
/// 専門家レビューを実行する Executor
/// State に ReviewResult を累積させるパターンを使用
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

        _agent = specialistType switch
        {
            "Legal" => AgentFactory.CreateLegalAgent(chatClient),
            "Finance" => AgentFactory.CreateFinanceAgent(chatClient),
            "Procurement" => AgentFactory.CreateProcurementAgent(chatClient),
            _ => throw new ArgumentException($"Unknown specialist type: {specialistType}")
        };
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

        var prompt = $@"以下の契約情報をレビューし、JSON形式で評価を返してください。

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

【出力形式】
以下のJSON形式で出力してください:
{{
  ""opinion"": ""あなたの専門分野からの総合的な所見"",
  ""risk_score"": リスクスコア(0-100の整数、100が最高リスク),
  ""concerns"": [""懸念点1"", ""懸念点2""],
  ""recommendations"": [""推奨事項1"", ""推奨事項2""]
}}";

        var messages = new[] { new ChatMessage(ChatRole.User, prompt) };
        var response = await _agent.RunAsync(messages, cancellationToken: cancellationToken);
        var responseText = response.Messages?.LastOrDefault()?.Text ?? "";

        // JSON パースを試みる
        try
        {
            var jsonContent = ExtractJsonFromResponse(responseText);
            var reviewData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonContent);

            if (reviewData == null)
            {
                throw new InvalidOperationException("JSON デシリアライズに失敗しました");
            }

            var result = new ReviewResult
            {
                Reviewer = _specialistName,
                Opinion = reviewData["opinion"].GetString() ?? "所見なし",
                RiskScore = reviewData["risk_score"].GetInt32(),
                Concerns = reviewData.ContainsKey("concerns")
                    ? reviewData["concerns"].EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                    : null,
                Recommendations = reviewData.ContainsKey("recommendations")
                    ? reviewData["recommendations"].EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                    : null
            };

            _logger?.LogInformation("✓ {SpecialistName} レビュー完了 (リスクスコア: {RiskScore})",
                _specialistName, result.RiskScore);

            // 既存のレビューリストに追加
            var updatedReviews = new List<ReviewResult>(input.Reviews) { result };
            return (input.Contract, updatedReviews);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ {SpecialistName} のレビュー結果のパースに失敗", _specialistName);

            // フォールバック: 安全側に高リスクとして返す
            var fallbackResult = new ReviewResult
            {
                Reviewer = _specialistName,
                Opinion = responseText,
                RiskScore = 70, // デフォルトで中リスク
                Concerns = new List<string> { "レビュー結果のパース失敗" },
                Recommendations = new List<string> { "手動での再レビューを推奨" }
            };

            var updatedReviews = new List<ReviewResult>(input.Reviews) { fallbackResult };
            return (input.Contract, updatedReviews);
        }
    }

    private static string ExtractJsonFromResponse(string response)
    {
        // JSON部分のみを抽出 (```json や ``` で囲まれている場合に対応)
        var jsonStart = response.IndexOf('{');
        var jsonEnd = response.LastIndexOf('}');

        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            return response.Substring(jsonStart, jsonEnd - jsonStart + 1);
        }

        return response;
    }
}
