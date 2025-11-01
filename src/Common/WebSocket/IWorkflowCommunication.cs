// Copyright (c) Microsoft. All rights reserved.

namespace Common.WebSocket;

/// <summary>
/// ワークフローの通信インターフェース (コンソール/WebSocket両対応)
/// </summary>
public interface IWorkflowCommunication
{
    /// <summary>
    /// エージェントの発話を送信
    /// </summary>
    Task SendAgentUtteranceAsync(string agentName, string content, string? phase = null, int? riskScore = null);

    /// <summary>
    /// 最終応答を送信
    /// </summary>
    Task SendFinalResponseAsync(object decision, string summary);

    /// <summary>
    /// HITL承認要求を送信し、応答を待機
    /// </summary>
    Task<bool> RequestHITLApprovalAsync(
        string approvalType,
        object contractInfo,
        object riskAssessment,
        string promptMessage);

    /// <summary>
    /// ワークフロー開始を通知
    /// </summary>
    Task SendWorkflowStartAsync(object contractInfo);

    /// <summary>
    /// ワークフロー完了を通知
    /// </summary>
    Task SendWorkflowCompleteAsync(object finalDecision);

    /// <summary>
    /// エラーを送信
    /// </summary>
    Task SendErrorAsync(string error, string? details = null);

    /// <summary>
    /// 契約選択要求を送信し、選択を待機
    /// </summary>
    /// <param name="contracts">契約情報の配列</param>
    /// <returns>選択された契約のインデックス（0-based）</returns>
    Task<int> RequestContractSelectionAsync(object[] contracts);
}
