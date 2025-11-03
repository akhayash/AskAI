// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using AdvancedConditionalWorkflow.Models;
using Common;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// 交渉ループに入る前にShared Stateへ初期状態を保存する Executor
/// 元の契約、元のリスク、交渉履歴、評価履歴を初期化
/// </summary>
public class NegotiationStateInitExecutor : Executor<(ContractInfo Contract, RiskAssessment Risk), (ContractInfo Contract, RiskAssessment Risk, int Iteration)>
{
    private readonly ILogger? _logger;

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
        _logger?.LogInformation("🔄 交渉ループ開始 - Shared State に初期状態を保存");
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("  サプライヤー: {Supplier}", contract.SupplierName);
        _logger?.LogInformation("  初期リスクスコア: {RiskScore}/100 ({RiskLevel})",
            risk.OverallRiskScore, risk.RiskLevel);
        _logger?.LogInformation("  契約金額: ${ContractValue:N0}", contract.ContractValue);

        // Shared State に保存 (scopeName で名前空間を分離)
        _logger?.LogInformation("💾 Shared State 書き込み開始...");

        // 元の契約情報
        await context.QueueStateUpdateAsync("original_contract", contract,
            scopeName: SharedStateScopes.OriginalContract,
            cancellationToken: cancellationToken);
        _logger?.LogInformation("  ✓ 元の契約情報を {Scope} スコープに保存", SharedStateScopes.OriginalContract);

        // 元のリスク評価
        await context.QueueStateUpdateAsync("original_risk", risk,
            scopeName: SharedStateScopes.OriginalRisk,
            cancellationToken: cancellationToken);
        _logger?.LogInformation("  ✓ 元のリスク評価を {Scope} スコープに保存", SharedStateScopes.OriginalRisk);

        // 交渉履歴を空リストで初期化
        var negotiationHistory = new List<NegotiationProposal>();
        await context.QueueStateUpdateAsync("negotiation_history", negotiationHistory,
            scopeName: SharedStateScopes.NegotiationHistory,
            cancellationToken: cancellationToken);
        _logger?.LogInformation("  ✓ 交渉履歴を {Scope} スコープに初期化", SharedStateScopes.NegotiationHistory);

        // 評価履歴を空リストで初期化
        var evaluationHistory = new List<EvaluationResult>();
        await context.QueueStateUpdateAsync("evaluation_history", evaluationHistory,
            scopeName: SharedStateScopes.EvaluationHistory,
            cancellationToken: cancellationToken);
        _logger?.LogInformation("  ✓ 評価履歴を {Scope} スコープに初期化", SharedStateScopes.EvaluationHistory);

        TelemetryHelper.LogWithActivity(_logger, activity, LogLevel.Information,
            "✓ Shared State 初期化完了: リスクスコア={0}, サプライヤー={1}",
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
