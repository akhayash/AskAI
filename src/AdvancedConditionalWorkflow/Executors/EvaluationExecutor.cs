// Copyright (c) Microsoft. All rights reserved.

using AdvancedConditionalWorkflow.Models;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// 交渉提案の効果を評価し、継続判定を行う Executor
/// </summary>
public class EvaluationExecutor(ILogger? logger = null, string id = "evaluation_executor")
    : Executor<(NegotiationProposal Proposal, RiskAssessment OriginalRisk), EvaluationResult>(id)
{
    private readonly ILogger? _logger = logger;
    private readonly Random _random = new();

    public override async ValueTask<EvaluationResult> HandleAsync(
        (NegotiationProposal Proposal, RiskAssessment OriginalRisk) input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var (proposal, originalRisk) = input;
        var iteration = proposal.Iteration;

        _logger?.LogInformation("🔍 交渉提案の効果を評価中 (反復 {Iteration}/3)...", iteration);

        // 簡略化: 提案数に応じてリスクスコアを削減 (実際にはAIエージェントで評価)
        var riskReduction = proposal.Proposals.Count * 5; // 1提案あたり5ポイント削減
        var newRiskScore = Math.Max(0, originalRisk.OverallRiskScore - riskReduction);

        // ランダム要素を追加して現実感を出す
        var randomAdjustment = _random.Next(-5, 6);
        newRiskScore = Math.Clamp(newRiskScore + randomAdjustment, 0, 100);

        var isImproved = newRiskScore < originalRisk.OverallRiskScore;
        var targetAchieved = newRiskScore <= proposal.TargetRiskScore;

        // 継続判定: 目標未達成 かつ 最大反復回数未満
        var continueNegotiation = !targetAchieved && iteration < 3;

        var evaluationComment = GenerateEvaluationComment(
            originalRisk.OverallRiskScore,
            newRiskScore,
            targetAchieved,
            iteration);

        var result = new EvaluationResult
        {
            Iteration = iteration,
            IsImproved = isImproved,
            NewRiskScore = newRiskScore,
            EvaluationComment = evaluationComment,
            ContinueNegotiation = continueNegotiation
        };

        _logger?.LogInformation(
            "✓ 評価完了: 改善={IsImproved}, 新スコア={NewScore}, 継続={Continue}",
            isImproved, newRiskScore, continueNegotiation);

        return result;
    }

    private static string GenerateEvaluationComment(
        int originalScore,
        int newScore,
        bool targetAchieved,
        int iteration)
    {
        var improvement = originalScore - newScore;

        if (targetAchieved)
        {
            return $"✅ 目標達成! リスクスコアが {originalScore} → {newScore} に改善しました。" +
                   $"({improvement}ポイント削減、反復回数: {iteration})";
        }

        if (improvement > 0)
        {
            return $"⚠️ リスクスコアは {originalScore} → {newScore} に改善しましたが、" +
                   $"目標(30以下)には到達していません。(反復回数: {iteration}/3)";
        }

        return $"❌ リスクスコアが改善しませんでした。({originalScore} → {newScore}、反復回数: {iteration}/3)";
    }
}
