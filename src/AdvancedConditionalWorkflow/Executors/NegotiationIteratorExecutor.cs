// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using AdvancedConditionalWorkflow.Models;
using Common;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// 交渉ループの反復カウンターを管理する Executor
/// </summary>
public class NegotiationIteratorExecutor : Executor<ContractRiskOutput, NegotiationStateOutput>
{
    private readonly ILogger? _logger;
    private const string IterationCountKey = "NegotiationIterationCount";

    public NegotiationIteratorExecutor(ILogger? logger = null, string id = "negotiation_iterator")
        : base(id)
    {
        _logger = logger;
    }

    public override async ValueTask<NegotiationStateOutput> HandleAsync(
        ContractRiskOutput input,
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

        using var activity = TelemetryHelper.StartActivity(
            Program.ActivitySource,
            "NegotiationIteration",
            new Dictionary<string, object>
            {
                ["iteration"] = iteration,
                ["current_risk_score"] = input.Risk.OverallRiskScore,
                ["supplier"] = input.Contract.SupplierName
            });

        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("🔁 交渉反復 {Iteration}/3", iteration);
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("  現在のリスクスコア: {RiskScore}/100", input.Risk.OverallRiskScore);

        // 反復回数を保存
        await context.QueueStateUpdateAsync(IterationCountKey, iteration, cancellationToken: cancellationToken);

        TelemetryHelper.LogWithActivity(_logger, activity, LogLevel.Information,
            "反復カウンター更新完了: {0}/3",
            iteration);

        return new NegotiationStateOutput
        {
            Contract = input.Contract,
            Risk = input.Risk,
            Iteration = iteration
        };
    }
}
