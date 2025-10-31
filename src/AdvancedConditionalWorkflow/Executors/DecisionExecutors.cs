// Copyright (c) Microsoft. All rights reserved.

using AdvancedConditionalWorkflow.Models;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// 低リスク契約の自動承認を行う Executor
/// </summary>
public class LowRiskApprovalExecutor(ILogger? logger = null, string id = "low_risk_approval")
    : Executor<(ContractInfo Contract, RiskAssessment Risk), FinalDecision>(id)
{
    private readonly ILogger? _logger = logger;

    public override async ValueTask<FinalDecision> HandleAsync(
        (ContractInfo Contract, RiskAssessment Risk) input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var (contract, risk) = input;

        _logger?.LogInformation("✅ 低リスク契約を自動承認");
        _logger?.LogInformation("  契約: {SupplierName}", contract.SupplierName);
        _logger?.LogInformation("  リスクスコア: {RiskScore}/100", risk.OverallRiskScore);

        var decision = new FinalDecision
        {
            Decision = "Approved",
            ContractInfo = contract,
            FinalRiskScore = risk.OverallRiskScore,
            DecisionSummary = $"リスクスコア {risk.OverallRiskScore}/100 (低リスク) のため、自動承認されました。",
            NextActions = new List<string>
            {
                "契約書の最終レビュー",
                "署名プロセスの開始",
                "関係者への通知"
            }
        };

        // 最終出力を発行
        await context.YieldOutputAsync(decision, cancellationToken);

        return decision;
    }
}

/// <summary>
/// 高リスク契約の自動却下を行う Executor
/// </summary>
public class HighRiskRejectionExecutor(ILogger? logger = null, string id = "high_risk_rejection")
    : Executor<(ContractInfo Contract, RiskAssessment Risk), FinalDecision>(id)
{
    private readonly ILogger? _logger = logger;

    public override async ValueTask<FinalDecision> HandleAsync(
        (ContractInfo Contract, RiskAssessment Risk) input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var (contract, risk) = input;

        _logger?.LogInformation("❌ 高リスク契約を自動却下");
        _logger?.LogInformation("  契約: {SupplierName}", contract.SupplierName);
        _logger?.LogInformation("  リスクスコア: {RiskScore}/100", risk.OverallRiskScore);

        if (risk.KeyConcerns != null && risk.KeyConcerns.Count > 0)
        {
            _logger?.LogInformation("  主要な懸念事項:");
            foreach (var concern in risk.KeyConcerns.Take(3))
            {
                _logger?.LogInformation("    - {Concern}", concern);
            }
        }

        var concerns = risk.KeyConcerns != null && risk.KeyConcerns.Count > 0
            ? string.Join(", ", risk.KeyConcerns)
            : "複数の重大なリスク";

        var decision = new FinalDecision
        {
            Decision = "Rejected",
            ContractInfo = contract,
            FinalRiskScore = risk.OverallRiskScore,
            DecisionSummary = $"リスクスコア {risk.OverallRiskScore}/100 (高リスク) のため、却下されました。主な懸念: {concerns}",
            NextActions = new List<string>
            {
                "サプライヤーへの却下通知",
                "代替サプライヤーの検討",
                "要件の見直し"
            }
        };

        // 最終出力を発行
        await context.YieldOutputAsync(decision, cancellationToken);

        return decision;
    }
}

/// <summary>
/// 最終承認結果を確定する Executor
/// </summary>
public class FinalizeDecisionExecutor(ILogger? logger = null, string id = "finalize_decision")
    : Executor<(ContractInfo Contract, RiskAssessment Risk, ApprovalResponse? Approval), FinalDecision>(id)
{
    private readonly ILogger? _logger = logger;

    public override async ValueTask<FinalDecision> HandleAsync(
        (ContractInfo Contract, RiskAssessment Risk, ApprovalResponse? Approval) input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var (contract, risk, approval) = input;

        if (approval == null || !approval.Approved)
        {
            _logger?.LogInformation("❌ 承認が拒否されました");

            return new FinalDecision
            {
                Decision = "Rejected",
                ContractInfo = contract,
                FinalRiskScore = risk.OverallRiskScore,
                DecisionSummary = $"人間による承認が拒否されました。理由: {approval?.ApproverComment ?? "未指定"}",
                NextActions = new List<string>
                {
                    "拒否理由の詳細確認",
                    "契約条件の再交渉",
                    "代替案の検討"
                }
            };
        }

        _logger?.LogInformation("✅ 承認が完了しました");

        return new FinalDecision
        {
            Decision = "Approved",
            ContractInfo = contract,
            FinalRiskScore = risk.OverallRiskScore,
            DecisionSummary = $"リスクスコア {risk.OverallRiskScore}/100 で承認されました。承認者コメント: {approval.ApproverComment ?? "なし"}",
            NextActions = new List<string>
            {
                "契約書の最終化",
                "署名プロセスの開始",
                "関係者への通知",
                "契約管理システムへの登録"
            }
        };
    }
}
