// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Logging;

namespace Common.WebSocket;

/// <summary>
/// WebSocket経由のワークフロー通信実装
/// </summary>
public class WebSocketCommunication : IWorkflowCommunication
{
    private readonly WorkflowWebSocketServer _server;
    private readonly ILogger? _logger;

    public WebSocketCommunication(WorkflowWebSocketServer server, ILogger? logger = null)
    {
        _server = server;
        _logger = logger;
    }

    public async Task SendAgentUtteranceAsync(string agentName, string content, string? phase = null, int? riskScore = null)
    {
        var message = new AgentUtteranceMessage
        {
            AgentName = agentName,
            Content = content,
            Phase = phase,
            RiskScore = riskScore
        };

        await _server.BroadcastAsync(message);

        // コンソールにも出力
        if (phase != null)
        {
            _logger?.LogInformation("━━━ {AgentName} ({Phase}) ━━━", agentName, phase);
        }
        else
        {
            _logger?.LogInformation("━━━ {AgentName} ━━━", agentName);
        }

        _logger?.LogInformation("{Content}", content);

        if (riskScore.HasValue)
        {
            _logger?.LogInformation("リスクスコア: {RiskScore}/100", riskScore.Value);
        }

        Console.WriteLine();
    }

    public async Task SendFinalResponseAsync(object decision, string summary)
    {
        var message = new FinalResponseMessage
        {
            Decision = decision,
            Summary = summary
        };

        await _server.BroadcastAsync(message);

        // コンソールにも出力
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("🎉 最終決定");
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("{Summary}", summary);
        Console.WriteLine();
    }

    public async Task<bool> RequestHITLApprovalAsync(
        string approvalType,
        object contractInfo,
        object riskAssessment,
        string promptMessage)
    {
        var message = new HITLRequestMessage
        {
            ApprovalType = approvalType,
            ContractInfo = contractInfo,
            RiskAssessment = riskAssessment,
            PromptMessage = promptMessage
        };

        await _server.BroadcastAsync(message);

        _logger?.LogInformation("HITL承認要求を送信しました: {ApprovalType}", approvalType);

        // WebSocketからの応答を待機 (タイムアウト: 5分)
        var response = await _server.WaitForHITLResponseAsync(TimeSpan.FromMinutes(5));

        if (response == null)
        {
            _logger?.LogWarning("HITL応答がタイムアウトしました。自動的に却下します。");
            return false;
        }

        _logger?.LogInformation("HITL応答を受信: {Approved}", response.Approved ? "承認" : "却下");
        return response.Approved;
    }

    public async Task SendWorkflowStartAsync(object contractInfo)
    {
        var message = new WorkflowStartMessage
        {
            ContractInfo = contractInfo
        };

        await _server.BroadcastAsync(message);

        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("ワークフロー開始");
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    }

    public async Task SendWorkflowCompleteAsync(object finalDecision)
    {
        var message = new WorkflowCompleteMessage
        {
            FinalDecision = finalDecision
        };

        await _server.BroadcastAsync(message);

        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("ワークフロー完了");
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();
    }

    public async Task SendErrorAsync(string error, string? details = null)
    {
        var message = new ErrorMessage
        {
            Error = error,
            Details = details
        };

        await _server.BroadcastAsync(message);

        _logger?.LogError("❌ エラー: {Error}", error);
        if (details != null)
        {
            _logger?.LogError("詳細: {Details}", details);
        }
    }
}
