// Copyright (c) Microsoft. All rights reserved.

using AdvancedConditionalWorkflow.Models;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// 交渉ループの反復カウンターを管理する Executor
/// </summary>
public class NegotiationIteratorExecutor : Executor<(ContractInfo Contract, RiskAssessment Risk), (ContractInfo Contract, RiskAssessment Risk, int Iteration)>
{
    private readonly ILogger? _logger;
    private const string IterationCountKey = "NegotiationIterationCount";

    public NegotiationIteratorExecutor(ILogger? logger = null, string id = "negotiation_iterator")
        : base(id)
    {
        _logger = logger;
    }

    public override async ValueTask<(ContractInfo Contract, RiskAssessment Risk, int Iteration)> HandleAsync(
        (ContractInfo Contract, RiskAssessment Risk) input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        // ワークフロー状態から反復回数を取得（初回は0）
        var iteration = await context.ReadStateAsync<int>(IterationCountKey, cancellationToken: cancellationToken);

        if (iteration == 0)
        {
            _logger?.LogInformation("🔄 交渉ループ開始");
        }

        iteration++;

        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("交渉反復 {Iteration}/3", iteration);
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        // 反復回数を保存
        await context.QueueStateUpdateAsync(IterationCountKey, iteration, cancellationToken: cancellationToken);

        return (input.Contract, input.Risk, iteration);
    }
}
