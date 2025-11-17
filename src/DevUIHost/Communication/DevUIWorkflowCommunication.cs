// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Concurrent;
using Common.WebSocket;
using Microsoft.Extensions.Logging;

namespace DevUIHost.Communication;

/// <summary>
/// DevUI環境でのワークフロー通信実装
/// HTTP/SSE ベースのDevUI環境でHITL承認をサポート
/// </summary>
public class DevUIWorkflowCommunication : IWorkflowCommunication
{
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, HITLApprovalRequest> _pendingApprovals = new();
    
    public DevUIWorkflowCommunication(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 承認待ちリクエストの一覧を取得
    /// </summary>
    public IEnumerable<HITLApprovalRequest> GetPendingApprovals()
    {
        return _pendingApprovals.Values;
    }

    /// <summary>
    /// 承認応答を処理
    /// </summary>
    public bool ProcessApprovalResponse(string requestId, bool approved, string? comment = null)
    {
        if (_pendingApprovals.TryRemove(requestId, out var request))
        {
            _logger?.LogInformation("HITL承認応答を処理: RequestId={RequestId}, Approved={Approved}", 
                requestId, approved);
            request.SetResult(approved, comment);
            return true;
        }
        
        _logger?.LogWarning("HITL承認リクエストが見つかりません: RequestId={RequestId}", requestId);
        return false;
    }

    public Task SendAgentUtteranceAsync(string agentName, string content, string? phase = null, int? riskScore = null)
    {
        // DevUIでは、エージェントの発話はワークフローの出力として自動的に処理される
        _logger?.LogInformation("━━━ {AgentName} {Phase} ━━━", agentName, phase ?? "");
        _logger?.LogInformation("{Content}", content);
        
        if (riskScore.HasValue)
        {
            _logger?.LogInformation("リスクスコア: {RiskScore}/100", riskScore.Value);
        }
        
        return Task.CompletedTask;
    }

    public Task SendFinalResponseAsync(object decision, string summary)
    {
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("🎉 最終決定");
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("{Summary}", summary);
        return Task.CompletedTask;
    }

    public async Task<bool> RequestHITLApprovalAsync(
        string approvalType,
        object contractInfo,
        object riskAssessment,
        string promptMessage)
    {
        var requestId = Guid.NewGuid().ToString();
        var request = new HITLApprovalRequest
        {
            RequestId = requestId,
            ApprovalType = approvalType,
            ContractInfo = contractInfo,
            RiskAssessment = riskAssessment,
            PromptMessage = promptMessage,
            CreatedAt = DateTime.UtcNow
        };

        _pendingApprovals[requestId] = request;

        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("👤 HITL: 人間による承認が必要です");
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("RequestId: {RequestId}", requestId);
        _logger?.LogInformation("承認タイプ: {ApprovalType}", approvalType);
        _logger?.LogInformation("{PromptMessage}", promptMessage);
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("承認を待機中... (DevUI UIまたはAPIで応答してください)");

        try
        {
            // タイムアウト付きで承認を待機 (5分)
            var approved = await request.WaitForResponseAsync(TimeSpan.FromMinutes(5));
            
            _logger?.LogInformation("HITL承認結果: {Result}", approved ? "承認" : "却下");
            return approved;
        }
        catch (TimeoutException)
        {
            _logger?.LogWarning("HITL承認がタイムアウトしました。自動的に却下します。");
            _pendingApprovals.TryRemove(requestId, out _);
            return false;
        }
    }

    public Task SendWorkflowStartAsync(object contractInfo)
    {
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("ワークフロー開始");
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        return Task.CompletedTask;
    }

    public Task SendWorkflowCompleteAsync(object finalDecision)
    {
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("ワークフロー完了");
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        return Task.CompletedTask;
    }

    public Task SendErrorAsync(string error, string? details = null)
    {
        _logger?.LogError("❌ エラー: {Error}", error);
        if (details != null)
        {
            _logger?.LogError("詳細: {Details}", details);
        }
        return Task.CompletedTask;
    }

    public Task<int> RequestContractSelectionAsync(object[] contracts)
    {
        // DevUIではユーザーが入力JSONで契約を指定するため、選択機能は不要
        // デフォルトで最初の契約を返す
        _logger?.LogInformation("契約選択: デフォルトで最初の契約を使用");
        return Task.FromResult(0);
    }
}

/// <summary>
/// HITL承認リクエスト
/// </summary>
public class HITLApprovalRequest
{
    private readonly TaskCompletionSource<bool> _completionSource = new();

    public required string RequestId { get; init; }
    public required string ApprovalType { get; init; }
    public required object ContractInfo { get; init; }
    public required object RiskAssessment { get; init; }
    public required string PromptMessage { get; init; }
    public required DateTime CreatedAt { get; init; }
    public string? Comment { get; private set; }

    /// <summary>
    /// 承認結果を設定
    /// </summary>
    public void SetResult(bool approved, string? comment = null)
    {
        Comment = comment;
        _completionSource.TrySetResult(approved);
    }

    /// <summary>
    /// 承認応答を待機
    /// </summary>
    public async Task<bool> WaitForResponseAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        cts.Token.Register(() => _completionSource.TrySetException(new TimeoutException()));
        
        return await _completionSource.Task;
    }
}
