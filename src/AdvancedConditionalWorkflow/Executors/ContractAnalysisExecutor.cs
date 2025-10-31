// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using AdvancedConditionalWorkflow.Models;
using Common;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// 契約情報を分析し、レビューが必要な専門分野を特定する Executor
/// State に ReviewResult リストを初期化
/// </summary>
public class ContractAnalysisExecutor : Executor<ContractInfo, (ContractInfo Contract, List<ReviewResult> Reviews)>
{
    private readonly ILogger? _logger;

    public ContractAnalysisExecutor(ILogger? logger = null, string id = "contract_analysis")
        : base(id)
    {
        _logger = logger;
    }

    public override async ValueTask<(ContractInfo Contract, List<ReviewResult> Reviews)> HandleAsync(
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

        TelemetryHelper.LogWithActivity(_logger, activity, LogLevel.Information,
            "✓ 契約分析完了 - 専門家レビューへ移行");

        await Task.CompletedTask;

        // 契約情報と空のレビューリストを返す
        return (input, new List<ReviewResult>());
    }
}
