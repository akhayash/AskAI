// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using AdvancedConditionalWorkflow.Models;
using Common;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// 交渉ループのループバック時に、EvaluationResult から次の反復のための入力を準備する Executor
/// </summary>
public class NegotiationLoopBackExecutor : Executor<(ContractInfo Contract, EvaluationResult Evaluation), (ContractInfo Contract, RiskAssessment Risk, int Iteration)>
{
    private readonly ILogger? _logger;

    public NegotiationLoopBackExecutor(ILogger? logger = null, string id = "negotiation_loopback")
        : base(id)
    {
        _logger = logger;
    }

    public override async ValueTask<(ContractInfo Contract, RiskAssessment Risk, int Iteration)> HandleAsync(
        (ContractInfo Contract, EvaluationResult Evaluation) input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        var (contract, evaluation) = input;

        using var activity = TelemetryHelper.StartActivity(
            Program.ActivitySource,
            "NegotiationLoopBack",
            new Dictionary<string, object>
            {
                ["current_iteration"] = evaluation.Iteration,
                ["next_iteration"] = evaluation.Iteration + 1,
                ["current_risk_score"] = evaluation.NewRiskScore,
                ["supplier"] = contract.SupplierName
            });

        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("🔁 交渉ループバック: 反復 {CurrentIteration} → {NextIteration}",
            evaluation.Iteration, evaluation.Iteration + 1);
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        // 元のリスク評価を取得
        var originalRisk = await context.ReadStateAsync<RiskAssessment>("original_risk",
            scopeName: SharedStateScopes.OriginalRisk,
            cancellationToken: cancellationToken);

        if (originalRisk == null)
        {
            _logger?.LogWarning("⚠️ 元のリスク評価が見つかりません。フォールバック値を使用します。");
            originalRisk = new RiskAssessment
            {
                OverallRiskScore = evaluation.NewRiskScore,
                RiskLevel = "Medium",
                Reviews = new List<ReviewResult>(),
                Summary = "ループバック時のフォールバック"
            };
        }

        // 評価結果から更新されたリスク評価を作成
        var updatedRisk = originalRisk with
        {
            OverallRiskScore = evaluation.NewRiskScore,
            RiskLevel = evaluation.NewRiskScore switch
            {
                <= 30 => "Low",
                <= 70 => "Medium",
                _ => "High"
            },
            Summary = $"{originalRisk.Summary}\n\n【交渉反復 {evaluation.Iteration} の結果】\n{evaluation.EvaluationComment}"
        };

        var nextIteration = evaluation.Iteration + 1;

        _logger?.LogInformation("  現在の反復: {CurrentIteration}/3", evaluation.Iteration);
        _logger?.LogInformation("  次の反復: {NextIteration}/3", nextIteration);
        _logger?.LogInformation("  更新後のリスクスコア: {RiskScore}/100", updatedRisk.OverallRiskScore);
        _logger?.LogInformation("  リスクレベル: {RiskLevel}", updatedRisk.RiskLevel);

        TelemetryHelper.LogWithActivity(_logger, activity, LogLevel.Information,
            "✓ ループバック準備完了: 反復{0}→{1}, リスク{2}→{3}",
            evaluation.Iteration, nextIteration,
            originalRisk.OverallRiskScore, updatedRisk.OverallRiskScore);

        return (contract, updatedRisk, nextIteration);
    }
}
