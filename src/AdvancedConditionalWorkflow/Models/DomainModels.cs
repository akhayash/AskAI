// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AdvancedConditionalWorkflow.Models;

/// <summary>
/// 契約情報を表すデータモデル
/// </summary>
public record ContractInfo
{
    [JsonPropertyName("supplier_name")]
    public required string SupplierName { get; init; }

    [JsonPropertyName("contract_value")]
    public required decimal ContractValue { get; init; }

    [JsonPropertyName("contract_term_months")]
    public required int ContractTermMonths { get; init; }

    [JsonPropertyName("payment_terms")]
    public required string PaymentTerms { get; init; }

    [JsonPropertyName("delivery_terms")]
    public required string DeliveryTerms { get; init; }

    [JsonPropertyName("warranty_period_months")]
    public int WarrantyPeriodMonths { get; init; }

    [JsonPropertyName("penalty_clause")]
    public bool HasPenaltyClause { get; init; }

    [JsonPropertyName("auto_renewal")]
    public bool HasAutoRenewal { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// 専門家レビュー結果を表すデータモデル
/// </summary>
public record ReviewResult
{
    [JsonPropertyName("reviewer")]
    public required string Reviewer { get; init; }

    [JsonPropertyName("opinion")]
    public required string Opinion { get; init; }

    [JsonPropertyName("risk_score")]
    public required int RiskScore { get; init; }

    [JsonPropertyName("concerns")]
    public List<string>? Concerns { get; init; }

    [JsonPropertyName("recommendations")]
    public List<string>? Recommendations { get; init; }
}

/// <summary>
/// リスク評価結果を表すデータモデル
/// </summary>
public record RiskAssessment
{
    [JsonPropertyName("overall_risk_score")]
    public required int OverallRiskScore { get; init; }

    [JsonPropertyName("risk_level")]
    public required string RiskLevel { get; init; } // "Low", "Medium", "High"

    [JsonPropertyName("reviews")]
    public required List<ReviewResult> Reviews { get; init; }

    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    [JsonPropertyName("key_concerns")]
    public List<string>? KeyConcerns { get; init; }
}

/// <summary>
/// 交渉提案を表すデータモデル
/// </summary>
public record NegotiationProposal
{
    [JsonPropertyName("iteration")]
    public required int Iteration { get; init; }

    [JsonPropertyName("proposals")]
    public required List<string> Proposals { get; init; }

    [JsonPropertyName("target_risk_score")]
    public required int TargetRiskScore { get; init; }

    [JsonPropertyName("rationale")]
    public required string Rationale { get; init; }

    [JsonPropertyName("contract_changes")]
    public Dictionary<string, (object? Before, object? After)>? ContractChanges { get; init; }
}

/// <summary>
/// 評価結果を表すデータモデル
/// </summary>
public record EvaluationResult
{
    [JsonPropertyName("iteration")]
    public required int Iteration { get; init; }

    [JsonPropertyName("is_improved")]
    public required bool IsImproved { get; init; }

    [JsonPropertyName("new_risk_score")]
    public required int NewRiskScore { get; init; }

    [JsonPropertyName("evaluation_comment")]
    public required string EvaluationComment { get; init; }

    [JsonPropertyName("continue_negotiation")]
    public required bool ContinueNegotiation { get; init; }
}

/// <summary>
/// 承認要求を表すデータモデル
/// </summary>
public record ApprovalRequest
{
    [JsonPropertyName("request_type")]
    public required string RequestType { get; init; } // "NegotiationEscalation", "FinalApproval"

    [JsonPropertyName("contract_info")]
    public required ContractInfo ContractInfo { get; init; }

    [JsonPropertyName("risk_assessment")]
    public required RiskAssessment RiskAssessment { get; init; }

    [JsonPropertyName("negotiation_history")]
    public List<NegotiationProposal>? NegotiationHistory { get; init; }

    [JsonPropertyName("evaluation_history")]
    public List<EvaluationResult>? EvaluationHistory { get; init; }

    [JsonPropertyName("recommendation")]
    public required string Recommendation { get; init; }
}

/// <summary>
/// 承認応答を表すデータモデル
/// </summary>
public record ApprovalResponse
{
    [JsonPropertyName("approved")]
    public required bool Approved { get; init; }

    [JsonPropertyName("approver_comment")]
    public string? ApproverComment { get; init; }
}

/// <summary>
/// 最終決定を表すデータモデル
/// </summary>
public record FinalDecision
{
    [JsonPropertyName("decision")]
    public required string Decision { get; init; } // "Approved", "Rejected", "RequiresModification"

    [JsonPropertyName("contract_info")]
    public required ContractInfo ContractInfo { get; init; }

    [JsonPropertyName("original_contract_info")]
    public ContractInfo? OriginalContractInfo { get; init; }

    [JsonPropertyName("final_risk_score")]
    public required int FinalRiskScore { get; init; }

    [JsonPropertyName("original_risk_score")]
    public int? OriginalRiskScore { get; init; }

    [JsonPropertyName("decision_summary")]
    public required string DecisionSummary { get; init; }

    [JsonPropertyName("next_actions")]
    public List<string>? NextActions { get; init; }

    [JsonPropertyName("negotiation_history")]
    public List<NegotiationProposal>? NegotiationHistory { get; init; }

    [JsonPropertyName("evaluation_history")]
    public List<EvaluationResult>? EvaluationHistory { get; init; }
}
