// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using AdvancedConditionalWorkflow.Models;
using Common;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// 交渉提案とリスク評価を結合して評価 Executor に渡す
/// </summary>
public class NegotiationContextExecutor : Executor<(ContractInfo Contract, RiskAssessment Risk, NegotiationProposal Proposal, int Iteration), (ContractInfo Contract, EvaluationResult Evaluation)>
{
    private readonly ILogger? _logger;

    public NegotiationContextExecutor(ILogger? logger = null, string id = "negotiation_context")
        : base(id)
    {
        _logger = logger;
    }

    public override async ValueTask<(ContractInfo Contract, EvaluationResult Evaluation)> HandleAsync(
        (ContractInfo Contract, RiskAssessment Risk, NegotiationProposal Proposal, int Iteration) input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var (contract, originalRisk, proposal, iteration) = input;

        using var activity = TelemetryHelper.StartActivity(
            Program.ActivitySource,
            "NegotiationEvaluation",
            new Dictionary<string, object>
            {
                ["iteration"] = iteration,
                ["proposal_count"] = proposal.Proposals.Count
            });

        _logger?.LogInformation("🔍 交渉提案の効果を評価中 (反復 {Iteration}/3)...", iteration);
        _logger?.LogInformation("  提案数: {ProposalCount}件", proposal.Proposals.Count);
        _logger?.LogInformation("  現在のリスクスコア: {CurrentScore}/100", originalRisk.OverallRiskScore);
        _logger?.LogInformation("  目標リスクスコア: 30以下");

        // 交渉提案の効果を評価
        var riskReduction = proposal.Proposals.Count * 5; // 1提案あたり5ポイント削減
        var newRiskScore = Math.Max(0, originalRisk.OverallRiskScore - riskReduction);

        var isImproved = newRiskScore < originalRisk.OverallRiskScore;
        var targetAchieved = newRiskScore <= proposal.TargetRiskScore;
        var continueNegotiation = !targetAchieved && iteration < 3;

        var evaluationComment = GenerateEvaluationComment(
            originalRisk.OverallRiskScore,
            newRiskScore,
            targetAchieved,
            iteration);

        var evaluation = new EvaluationResult
        {
            Iteration = iteration,
            IsImproved = isImproved,
            NewRiskScore = newRiskScore,
            EvaluationComment = evaluationComment,
            ContinueNegotiation = continueNegotiation
        };

        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("📊 評価結果 (反復 {Iteration}/3)", iteration);
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("  元のリスクスコア: {OriginalScore}/100", originalRisk.OverallRiskScore);
        _logger?.LogInformation("  新しいリスクスコア: {NewScore}/100", newRiskScore);
        _logger?.LogInformation("  改善: {Improvement} ({Status})",
            isImproved ? $"-{originalRisk.OverallRiskScore - newRiskScore}ポイント" : "なし",
            isImproved ? "✅" : "❌");
        _logger?.LogInformation("  目標達成: {TargetAchieved}", targetAchieved ? "✅ はい" : "❌ いいえ");
        _logger?.LogInformation("  交渉継続: {ContinueNegotiation}", continueNegotiation ? "✅ はい" : "❌ いいえ");
        _logger?.LogInformation("  コメント: {Comment}", evaluationComment);

        activity?.SetTag("original_risk_score", originalRisk.OverallRiskScore);
        activity?.SetTag("new_risk_score", newRiskScore);
        activity?.SetTag("is_improved", isImproved);
        activity?.SetTag("target_achieved", targetAchieved);
        activity?.SetTag("continue_negotiation", continueNegotiation);

        _logger?.LogInformation("🔀 条件付きエッジへ出力: ContinueNegotiation={Continue} (ループバック={Loopback}, 終了={Exit})",
            continueNegotiation,
            continueNegotiation ? "✅" : "❌",
            !continueNegotiation ? "✅" : "❌");

        return (contract, evaluation);
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
