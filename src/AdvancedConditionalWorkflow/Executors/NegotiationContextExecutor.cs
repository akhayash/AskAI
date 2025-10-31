// Copyright (c) Microsoft. All rights reserved.

using AdvancedConditionalWorkflow.Models;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// 交渉提案とリスク評価を結合して評価 Executor に渡す
/// </summary>
public class NegotiationContextExecutor : Executor<NegotiationProposal, (ContractInfo Contract, EvaluationResult Evaluation)>
{
    private readonly ILogger? _logger;
    private const string OriginalRiskKey = "OriginalRiskAssessment";
    private const string ContractKey = "ContractInfo";

    public NegotiationContextExecutor(ILogger? logger = null, string id = "negotiation_context")
        : base(id)
    {
        _logger = logger;
    }

    public override async ValueTask<(ContractInfo Contract, EvaluationResult Evaluation)> HandleAsync(
        NegotiationProposal proposal,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        // ワークフロー状態から元のリスク評価と契約情報を取得
        var originalRisk = await context.ReadStateAsync<RiskAssessment>(OriginalRiskKey, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Original risk assessment not found in state");

        var contract = await context.ReadStateAsync<ContractInfo>(ContractKey, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Contract info not found in state");

        // 交渉提案の効果を評価
        var riskReduction = proposal.Proposals.Count * 5; // 1提案あたり5ポイント削減
        var newRiskScore = Math.Max(0, originalRisk.OverallRiskScore - riskReduction);

        // ランダム要素を追加
        var random = new Random();
        var randomAdjustment = random.Next(-5, 6);
        newRiskScore = Math.Clamp(newRiskScore + randomAdjustment, 0, 100);

        var isImproved = newRiskScore < originalRisk.OverallRiskScore;
        var targetAchieved = newRiskScore <= proposal.TargetRiskScore;
        var continueNegotiation = !targetAchieved && proposal.Iteration < 3;

        var evaluationComment = GenerateEvaluationComment(
            originalRisk.OverallRiskScore,
            newRiskScore,
            targetAchieved,
            proposal.Iteration);

        var evaluation = new EvaluationResult
        {
            Iteration = proposal.Iteration,
            IsImproved = isImproved,
            NewRiskScore = newRiskScore,
            EvaluationComment = evaluationComment,
            ContinueNegotiation = continueNegotiation
        };

        _logger?.LogInformation(
            "✓ 評価完了: 改善={IsImproved}, 新スコア={NewScore}, 継続={Continue}",
            isImproved, newRiskScore, continueNegotiation);

        // 次の反復のために更新されたリスク評価を保存
        if (continueNegotiation)
        {
            var updatedRisk = originalRisk with { OverallRiskScore = newRiskScore };
            await context.QueueStateUpdateAsync(OriginalRiskKey, updatedRisk, cancellationToken: cancellationToken);
        }

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
