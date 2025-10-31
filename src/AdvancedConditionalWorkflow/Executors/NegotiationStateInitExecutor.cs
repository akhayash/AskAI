// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using AdvancedConditionalWorkflow.Models;
using Common;
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
        using var activity = TelemetryHelper.StartActivity(
            Program.ActivitySource,
            "NegotiationStateInit",
            new Dictionary<string, object>
            {
                ["supplier"] = input.Contract.SupplierName,
                ["initial_risk_score"] = input.Risk.OverallRiskScore,
                ["contract_value"] = input.Contract.ContractValue
            });

        var (contract, risk) = input;

        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("🔄 交渉ループ開始 - 初期状態を保存");
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("  サプライヤー: {Supplier}", contract.SupplierName);
        _logger?.LogInformation("  初期リスクスコア: {RiskScore}/100 ({RiskLevel})",
            risk.OverallRiskScore, risk.RiskLevel);
        _logger?.LogInformation("  契約金額: ${ContractValue:N0}", contract.ContractValue);

        // ワークフロー状態に保存
        _logger?.LogInformation("💾 State 書き込み開始...");
        await context.QueueStateUpdateAsync(OriginalRiskKey, risk, cancellationToken: cancellationToken);
        _logger?.LogInformation("  {Key} 書き込み完了", OriginalRiskKey);

        await context.QueueStateUpdateAsync(ContractKey, contract, cancellationToken: cancellationToken);
        _logger?.LogInformation("  {Key} 書き込み完了", ContractKey);

        await context.QueueStateUpdateAsync(IterationCountKey, 0, cancellationToken: cancellationToken);
        _logger?.LogInformation("  {Key} 書き込み完了", IterationCountKey);

        TelemetryHelper.LogWithActivity(_logger, activity, LogLevel.Information,
            "✓ 状態初期化完了: リスクスコア={0}, サプライヤー={1}",
            risk.OverallRiskScore, contract.SupplierName);

        if (risk.KeyConcerns != null && risk.KeyConcerns.Count > 0)
        {
            _logger?.LogInformation("  主要な懸念事項 ({Count}件):", risk.KeyConcerns.Count);
            foreach (var concern in risk.KeyConcerns.Take(3))
            {
                _logger?.LogInformation("    • {Concern}", concern);
            }
        }

        // 初回反復として開始
        return (contract, risk, 1);
    }
}
