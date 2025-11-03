// Copyright (c) Microsoft. All rights reserved.

using AdvancedConditionalWorkflow.Models;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// Fan-In パターンでの専門家レビュー統合 Executor
/// 3つの専門家 (Legal, Finance, Procurement) からの ReviewResult を収集し、
/// すべて揃った時点で RiskAssessment を生成
/// </summary>
public class ParallelReviewAggregator : Executor<ReviewResult, (ContractInfo Contract, RiskAssessment Risk)?>
{
    private readonly ILogger? _logger;
    private readonly List<ReviewResult> _reviews = [];

    // Shared State のスコープ名
    private const string ContractStateScope = "ContractAnalysis";
    private const string ContractStateKey = "current_contract";

    public ParallelReviewAggregator(ILogger? logger = null, string id = "review_aggregator")
        : base(id)
    {
        _logger = logger;
    }

    public override async ValueTask<(ContractInfo Contract, RiskAssessment Risk)?> HandleAsync(
        ReviewResult review,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        // レビュー結果を追加
        _reviews.Add(review);
        _logger?.LogInformation("📊 レビュー受信: {Reviewer} ({CurrentCount}/3)", review.Reviewer, _reviews.Count);

        // Fan-In: 3つすべてのレビューが揃うまで待機
        if (_reviews.Count < 3)
        {
            _logger?.LogInformation("⏳ 残り {RemainingCount} 件のレビューを待機中 (null返却)", 3 - _reviews.Count);
            // 3つ揃うまでは null を返す (条件付きエッジで HasValue = false になる)
            return null;
        }

        _logger?.LogInformation("✓ すべてのレビューが揃いました。統合処理を開始");

        // Shared State から契約情報を取得
        var contract = await context.ReadStateAsync<ContractInfo>(ContractStateKey, scopeName: ContractStateScope, cancellationToken);

        if (contract == null)
        {
            throw new InvalidOperationException("契約情報が Shared State に保存されていません");
        }

        var reviews = _reviews;

        // 平均リスクスコアを計算
        var overallRiskScore = reviews.Count > 0
            ? (int)Math.Round(reviews.Average(r => r.RiskScore))
            : 50; // デフォルト中リスク

        // リスクレベルを判定 (0-30: Low, 31-70: Medium, 71-100: High)
        var riskLevel = overallRiskScore switch
        {
            <= 30 => "Low",
            <= 70 => "Medium",
            _ => "High"
        };

        // すべての懸念事項を集約
        var allConcerns = reviews
            .Where(r => r.Concerns != null)
            .SelectMany(r => r.Concerns!)
            .Distinct()
            .ToList();

        // サマリーを生成
        var summary = GenerateSummary(reviews, overallRiskScore, riskLevel);

        var result = new RiskAssessment
        {
            OverallRiskScore = overallRiskScore,
            RiskLevel = riskLevel,
            Reviews = reviews,
            Summary = summary,
            KeyConcerns = allConcerns.Count > 0 ? allConcerns : null
        };

        _logger?.LogInformation("✓ リスク評価完了: レベル={RiskLevel}, スコア={RiskScore}",
            riskLevel, overallRiskScore);

        // エージェント発話をCommunicationに送信
        await Program.Communication!.SendAgentUtteranceAsync(
            "Risk Aggregator",
            summary,
            "Phase 3: Risk Assessment",
            overallRiskScore);

        // 評価詳細をログ出力
        _logger?.LogInformation("  サマリー:");
        foreach (var line in result.Summary?.Split('\n') ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                _logger?.LogInformation("    {SummaryLine}", line.TrimStart());
            }
        }

        if (result.KeyConcerns != null && result.KeyConcerns.Count > 0)
        {
            _logger?.LogInformation("  主要な懸念事項:");
            foreach (var concern in result.KeyConcerns)
            {
                _logger?.LogInformation("    - {Concern}", concern);
            }
        }

        // タプルを返して条件付きエッジ経由で次のExecutorにルーティング
        _logger?.LogInformation("🔀 条件付きエッジへ出力: Supplier={Supplier}, RiskScore={RiskScore}, RiskLevel={RiskLevel}",
            contract.SupplierName, result.OverallRiskScore, result.RiskLevel);

        // 最終的な統合レポートを return (Nullable型なので non-null を返す)
        return (contract, result);
    }

    private static string GenerateSummary(List<ReviewResult> reviews, int overallScore, string riskLevel)
    {
        var reviewerNames = string.Join(", ", reviews.Select(r => r.Reviewer));
        var summary = $"【総合リスク評価】\n" +
                     $"リスクレベル: {riskLevel} (スコア: {overallScore}/100)\n" +
                     $"レビュー担当: {reviewerNames}\n\n";

        foreach (var review in reviews)
        {
            summary += $"◆ {review.Reviewer} (スコア: {review.RiskScore})\n";
            summary += $"  {review.Opinion}\n\n";
        }

        return summary.TrimEnd();
    }
}
