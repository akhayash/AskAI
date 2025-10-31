// Copyright (c) Microsoft. All rights reserved.

using AdvancedConditionalWorkflow.Models;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// 交渉ループに入る前にコンテキスト情報を保存する Executor
/// </summary>
public class NegotiationStateInitExecutor : Executor<(ContractInfo Contract, RiskAssessment Risk), (ContractInfo Contract, RiskAssessment Risk, int Iteration)>
{
    private readonly ILogger? _logger;
    private const string OriginalRiskKey = "OriginalRiskAssessment";
    private const string ContractKey = "ContractInfo";
    private const string IterationCountKey = "NegotiationIterationCount";

    public NegotiationStateInitExecutor(ILogger? logger = null, string id = "negotiation_state_init")
        : base(id)
    {
        _logger = logger;
    }

    public override async ValueTask<(ContractInfo Contract, RiskAssessment Risk, int Iteration)> HandleAsync(
        (ContractInfo Contract, RiskAssessment Risk) input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        var (contract, risk) = input;

        _logger?.LogInformation("🔄 交渉ループ開始 - 初期状態を保存");
        _logger?.LogInformation("  初期リスクスコア: {Score}/100", risk.OverallRiskScore);

        // ワークフロー状態に保存
        await context.QueueStateUpdateAsync(OriginalRiskKey, risk, cancellationToken: cancellationToken);
        await context.QueueStateUpdateAsync(ContractKey, contract, cancellationToken: cancellationToken);
        await context.QueueStateUpdateAsync(IterationCountKey, 0, cancellationToken: cancellationToken);

        // 初回反復として開始
        return (contract, risk, 1);
    }
}
