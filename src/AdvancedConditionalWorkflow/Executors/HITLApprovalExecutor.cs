// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using System.Text;
using AdvancedConditionalWorkflow.Models;
using Common;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// Human-in-the-Loop (HITL) 承認を実行する Executor
/// </summary>
public class HITLApprovalExecutor : Executor<(ContractInfo Contract, RiskAssessment Risk), FinalDecision>
{
    private readonly string _approvalType;
    private readonly ILogger? _logger;

    public HITLApprovalExecutor(string approvalType, ILogger? logger = null, string? id = null)
        : base(id ?? $"hitl_{approvalType}")
    {
        _approvalType = approvalType;
        _logger = logger;
    }

    public override async ValueTask<FinalDecision> HandleAsync(
        (ContractInfo Contract, RiskAssessment Risk) input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        using var activity = TelemetryHelper.StartActivity(
            Program.ActivitySource,
            $"HITL_{_approvalType}",
            new Dictionary<string, object>
            {
                ["approval_type"] = _approvalType,
                ["risk_score"] = input.Risk.OverallRiskScore,
                ["supplier"] = input.Contract.SupplierName,
                ["contract_value"] = input.Contract.ContractValue
            });

        await Task.CompletedTask;

        var (contract, risk) = input;

        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("👤 HITL: 人間による承認が必要です");
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("  承認タイプ: {ApprovalType}", GetApprovalTypeLabel());

        // Communicationインターフェース経由でHITL要求
        var promptMessage = BuildPromptMessage(contract, risk);
        var approved = await Program.Communication!.RequestHITLApprovalAsync(
            _approvalType,
            contract,
            risk,
            promptMessage);

        activity?.SetTag("approved", approved);
        activity?.SetTag("user_response", approved ? "Y" : "N");

        TelemetryHelper.LogWithActivity(_logger, activity, LogLevel.Information,
            "👤 HITL結果: {0} (タイプ: {1})",
            approved ? "承認" : "却下", _approvalType);

        var decision = new FinalDecision
        {
            Decision = GetDecisionLabel(approved),
            ContractInfo = contract,
            FinalRiskScore = risk.OverallRiskScore,
            DecisionSummary = GenerateSummary(approved, contract, risk),
            NextActions = GenerateNextActions(approved, risk)
        };

        // 最終出力を発行
        await context.YieldOutputAsync(decision, cancellationToken);

        return decision;
    }

    private string GetApprovalTypeLabel() => _approvalType switch
    {
        "final_approval" => "最終承認",
        "escalation" => "エスカレーション判断",
        "rejection_confirm" => "拒否確認",
        _ => "承認要求"
    };

    private string GetApprovalPrompt() => _approvalType switch
    {
        "final_approval" => "交渉により目標リスクレベルに到達しました。\nこの契約を承認しますか?",
        "escalation" => "交渉を3回実施しましたが、目標リスクレベルには到達していません。\n上位承認者へエスカレーションしますか?",
        "rejection_confirm" => "この契約は高リスクと判定されています。\n拒否を確定しますか?",
        _ => "この決定を承認しますか?"
    };

    private string GetDecisionLabel(bool approved) => _approvalType switch
    {
        "final_approval" => approved ? "Approved" : "Rejected",
        "escalation" => approved ? "Escalated" : "Rejected",
        "rejection_confirm" => approved ? "Rejected" : "RequiresReview",
        _ => approved ? "Approved" : "Rejected"
    };

    private string GenerateSummary(bool approved, ContractInfo contract, RiskAssessment risk)
    {
        var action = GetApprovalTypeLabel();
        var result = approved ? "承認されました" : "却下されました";

        return $"{action}が{result}。契約: {contract.SupplierName}、" +
               $"最終リスクスコア: {risk.OverallRiskScore}/100 ({risk.RiskLevel})";
    }

    private List<string> GenerateNextActions(bool approved, RiskAssessment risk)
    {
        if (!approved)
        {
            return new List<string>
            {
                "契約条件を再検討",
                "別のサプライヤーを検討",
                "リスク軽減策を再評価"
            };
        }

        return _approvalType switch
        {
            "final_approval" => new List<string>
            {
                "契約書の最終確認と署名",
                "サプライヤーへの正式通知",
                "契約管理システムへの登録"
            },
            "escalation" => new List<string>
            {
                "上位承認者への報告書作成",
                "追加のデューデリジェンス実施",
                "経営会議での審議"
            },
            "rejection_confirm" => new List<string>
            {
                "サプライヤーへの丁寧な拒否通知",
                "拒否理由の文書化",
                "代替サプライヤーの検討開始"
            },
            _ => new List<string> { "次のステップを決定" }
        };
    }

    private string BuildPromptMessage(ContractInfo contract, RiskAssessment risk)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"契約: {contract.SupplierName}");
        sb.AppendLine($"契約金額: ${contract.ContractValue:N0}");
        sb.AppendLine($"リスクスコア: {risk.OverallRiskScore}/100 ({risk.RiskLevel})");
        sb.AppendLine();

        if (risk.KeyConcerns != null && risk.KeyConcerns.Count > 0)
        {
            sb.AppendLine("主要な懸念事項:");
            foreach (var concern in risk.KeyConcerns.Take(3))
            {
                sb.AppendLine($"  • {concern}");
            }
            sb.AppendLine();
        }

        sb.AppendLine(GetApprovalPrompt());

        return sb.ToString();
    }
}
