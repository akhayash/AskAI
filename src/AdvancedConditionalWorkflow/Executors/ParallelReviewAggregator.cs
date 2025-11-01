// Copyright (c) Microsoft. All rights reserved.

using AdvancedConditionalWorkflow.Models;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// 複数の専門家レビューを統合してリスク評価を行う
/// </summary>
public class ParallelReviewAggregator(ILogger? logger = null, string id = "review_aggregator")
    : Executor<(ContractInfo Contract, List<ReviewResult> Reviews), (ContractInfo Contract, RiskAssessment Risk)>(id)
{
    private readonly ILogger? _logger = logger;

    public override async ValueTask<(ContractInfo Contract, RiskAssessment Risk)> HandleAsync(
        (ContractInfo Contract, List<ReviewResult> Reviews) input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var reviews = input.Reviews;
        _logger?.LogInformation("📊 {ReviewCount}件のレビュー結果を統合中...", reviews.Count);

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

        return (input.Contract, result);
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
