// Copyright (c) Microsoft. All rights reserved.

using AdvancedConditionalWorkflow.Models;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// 交渉結果を最終決定に変換する Executor
/// </summary>
public class NegotiationResultExecutor : Executor<(ContractInfo Contract, EvaluationResult Evaluation), (ContractInfo Contract, RiskAssessment Risk)>
{
    private readonly ILogger? _logger;

    public NegotiationResultExecutor(ILogger? logger = null, string id = "negotiation_result")
        : base(id)
    {
        _logger = logger;
    }

    public override async ValueTask<(ContractInfo Contract, RiskAssessment Risk)> HandleAsync(
        (ContractInfo Contract, EvaluationResult Evaluation) input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var (contract, evaluation) = input;

        _logger?.LogInformation("📊 交渉結果をリスク評価に変換中...");
        _logger?.LogInformation("  最終リスクスコア: {Score}/100", evaluation.NewRiskScore);
        _logger?.LogInformation("  評価コメント: {Comment}", evaluation.EvaluationComment);

        // 交渉後の更新されたリスク評価を作成
        var updatedRisk = new RiskAssessment
        {
            OverallRiskScore = evaluation.NewRiskScore,
            RiskLevel = evaluation.NewRiskScore <= 30 ? "Low" :
                       evaluation.NewRiskScore <= 70 ? "Medium" : "High",
            Reviews = new List<ReviewResult>(), // 交渉プロセスではレビューは不要
            Summary = evaluation.EvaluationComment,
            KeyConcerns = evaluation.IsImproved
                ? new List<string> { $"交渉により{evaluation.Iteration}回の改善を実施" }
                : new List<string> { "交渉による十分な改善が得られませんでした" }
        };

        return (contract, updatedRisk);
    }
}
