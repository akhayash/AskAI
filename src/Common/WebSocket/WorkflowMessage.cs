// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Common.WebSocket;

/// <summary>
/// WebSocketメッセージの基本型
/// </summary>
public record WorkflowMessage
{
    /// <summary>
    /// メッセージタイプ
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// タイムスタンプ
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// メッセージID
    /// </summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; init; } = Guid.NewGuid().ToString();
}

/// <summary>
/// エージェントの中間発話メッセージ
/// </summary>
public record AgentUtteranceMessage : WorkflowMessage
{
    public AgentUtteranceMessage() : base()
    {
        Type = "agent_utterance";
    }

    /// <summary>
    /// エージェント名
    /// </summary>
    [JsonPropertyName("agentName")]
    public required string AgentName { get; init; }

    /// <summary>
    /// エージェントの発話内容
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>
    /// フェーズ情報 (オプション)
    /// </summary>
    [JsonPropertyName("phase")]
    public string? Phase { get; init; }

    /// <summary>
    /// リスクスコア (オプション)
    /// </summary>
    [JsonPropertyName("riskScore")]
    public int? RiskScore { get; init; }
}

/// <summary>
/// 最終応答メッセージ
/// </summary>
public record FinalResponseMessage : WorkflowMessage
{
    public FinalResponseMessage() : base()
    {
        Type = "final_response";
    }

    /// <summary>
    /// 最終決定内容 (JSON形式)
    /// </summary>
    [JsonPropertyName("decision")]
    public required object Decision { get; init; }

    /// <summary>
    /// サマリーテキスト
    /// </summary>
    [JsonPropertyName("summary")]
    public required string Summary { get; init; }
}

/// <summary>
/// HITL承認要求メッセージ
/// </summary>
public record HITLRequestMessage : WorkflowMessage
{
    public HITLRequestMessage() : base()
    {
        Type = "hitl_request";
    }

    /// <summary>
    /// 承認タイプ
    /// </summary>
    [JsonPropertyName("approvalType")]
    public required string ApprovalType { get; init; }

    /// <summary>
    /// 契約情報
    /// </summary>
    [JsonPropertyName("contractInfo")]
    public required object ContractInfo { get; init; }

    /// <summary>
    /// リスク評価
    /// </summary>
    [JsonPropertyName("riskAssessment")]
    public required object RiskAssessment { get; init; }

    /// <summary>
    /// プロンプトメッセージ
    /// </summary>
    [JsonPropertyName("promptMessage")]
    public required string PromptMessage { get; init; }
}

/// <summary>
/// HITL承認応答メッセージ
/// </summary>
public record HITLResponseMessage : WorkflowMessage
{
    public HITLResponseMessage() : base()
    {
        Type = "hitl_response";
    }

    /// <summary>
    /// 承認されたかどうか
    /// </summary>
    [JsonPropertyName("approved")]
    public required bool Approved { get; init; }

    /// <summary>
    /// コメント (オプション)
    /// </summary>
    [JsonPropertyName("comment")]
    public string? Comment { get; init; }
}

/// <summary>
/// ワークフロー開始メッセージ
/// </summary>
public record WorkflowStartMessage : WorkflowMessage
{
    public WorkflowStartMessage() : base()
    {
        Type = "workflow_start";
    }

    /// <summary>
    /// 契約情報
    /// </summary>
    [JsonPropertyName("contractInfo")]
    public required object ContractInfo { get; init; }
}

/// <summary>
/// ワークフロー完了メッセージ
/// </summary>
public record WorkflowCompleteMessage : WorkflowMessage
{
    public WorkflowCompleteMessage() : base()
    {
        Type = "workflow_complete";
    }

    /// <summary>
    /// 最終決定
    /// </summary>
    [JsonPropertyName("finalDecision")]
    public required object FinalDecision { get; init; }
}

/// <summary>
/// エラーメッセージ
/// </summary>
public record ErrorMessage : WorkflowMessage
{
    public ErrorMessage() : base()
    {
        Type = "error";
    }

    /// <summary>
    /// エラーメッセージ
    /// </summary>
    [JsonPropertyName("error")]
    public required string Error { get; init; }

    /// <summary>
    /// エラー詳細 (オプション)
    /// </summary>
    [JsonPropertyName("details")]
    public string? Details { get; init; }
}
