// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using AdvancedConditionalWorkflow.Models;
using Common;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// 契約情報を分析し、Fan-Out へ契約を渡す Executor
/// Fan-Out パターンでは契約情報をそのまま返し、各専門家へ並行配信される
/// </summary>
public class ContractAnalysisExecutor : Executor<ContractInfo, ContractInfo>
{
    private readonly ILogger? _logger;

    // Shared State のスコープ名 (ParallelReviewAggregator と共通)
    private const string ContractStateScope = "ContractAnalysis";
    private const string ContractStateKey = "current_contract";

    public ContractAnalysisExecutor(ILogger? logger = null, string id = "contract_analysis")
        : base(id)
    {
        _logger = logger;
    }

    public override async ValueTask<ContractInfo> HandleAsync(
        ContractInfo input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        using var activity = TelemetryHelper.StartActivity(
            Program.ActivitySource,
            "ContractAnalysis",
            new Dictionary<string, object>
            {
                ["supplier"] = input.SupplierName,
                ["contract_value"] = input.ContractValue,
                ["contract_term_months"] = input.ContractTermMonths
            });

        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("📋 契約分析フェーズ開始");
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("  サプライヤー: {Supplier}", input.SupplierName);
        _logger?.LogInformation("  契約金額: ${Value:N0}", input.ContractValue);
        _logger?.LogInformation("  契約期間: {Term}ヶ月", input.ContractTermMonths);
        _logger?.LogInformation("  ペナルティ条項: {HasPenalty}", input.HasPenaltyClause ? "あり" : "なし");
        _logger?.LogInformation("  自動更新: {HasAutoRenewal}", input.HasAutoRenewal ? "あり" : "なし");

        // Shared State に契約情報を保存 (Aggregatorで参照)
        await context.QueueStateUpdateAsync(ContractStateKey, input, scopeName: ContractStateScope, cancellationToken);
        _logger?.LogInformation("  ✓ 契約情報を Shared State に保存 (scope: {Scope}, key: {Key})",
            ContractStateScope, ContractStateKey);

        TelemetryHelper.LogWithActivity(_logger, activity, LogLevel.Information,
            "✓ 契約分析完了 - 専門家レビューへ並行実行 (Fan-Out)");

        // 契約情報をそのまま返し、Fan-Outで3つの専門家へ並行配信
        return input;
    }
}
