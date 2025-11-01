// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Common.WebSocket;

/// <summary>
/// ワークフローイベントをWebSocketで配信するサーバー
/// </summary>
public class WorkflowWebSocketServer : IDisposable
{
    private readonly HttpListener _httpListener;
    private readonly ConcurrentBag<System.Net.WebSockets.WebSocket> _connectedClients = new();
    private readonly ILogger? _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _listenerTask;
    private readonly SemaphoreSlim _hitlResponseSemaphore = new(0, 1);
    private HITLResponseMessage? _pendingHitlResponse;

    private readonly int _port;

    public WorkflowWebSocketServer(int port = 8080, ILogger? logger = null)
    {
        _port = port;
        _logger = logger;
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://localhost:{port}/");
    }

    /// <summary>
    /// サーバーを起動
    /// </summary>
    public void Start()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _httpListener.Start();
        _listenerTask = Task.Run(() => ListenAsync(_cancellationTokenSource.Token));
        _logger?.LogInformation("WebSocketサーバーを起動しました (Port: {Port})", _port);
    }

    /// <summary>
    /// サーバーを停止
    /// </summary>
    public async Task StopAsync()
    {
        _cancellationTokenSource?.Cancel();

        if (_listenerTask != null)
        {
            await _listenerTask;
        }

        _httpListener.Stop();

        foreach (var client in _connectedClients)
        {
            if (client.State == WebSocketState.Open)
            {
                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server stopping", CancellationToken.None);
            }
            client.Dispose();
        }

        _connectedClients.Clear();
        _logger?.LogInformation("WebSocketサーバーを停止しました");
    }

    /// <summary>
    /// クライアント接続を待機
    /// </summary>
    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await _httpListener.GetContextAsync();

                if (context.Request.IsWebSocketRequest)
                {
                    _ = Task.Run(async () => await HandleWebSocketAsync(context), cancellationToken);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }
        catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException)
        {
            // サーバー停止時の例外は無視
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "WebSocketリスナーでエラーが発生しました");
        }
    }

    /// <summary>
    /// WebSocket接続を処理
    /// </summary>
    private async Task HandleWebSocketAsync(HttpListenerContext context)
    {
        System.Net.WebSockets.WebSocket? webSocket = null;

        try
        {
            var webSocketContext = await context.AcceptWebSocketAsync(null);
            webSocket = webSocketContext.WebSocket;
            _connectedClients.Add(webSocket);
            _logger?.LogInformation("WebSocketクライアントが接続しました");

            var buffer = new byte[1024 * 4];

            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var messageText = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleClientMessage(messageText);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "WebSocket接続処理でエラーが発生しました");
        }
        finally
        {
            if (webSocket != null)
            {
                webSocket.Dispose();
                _logger?.LogInformation("WebSocketクライアントが切断しました");
            }
        }
    }

    /// <summary>
    /// クライアントからのメッセージを処理
    /// </summary>
    private async Task HandleClientMessage(string messageText)
    {
        try
        {
            var message = JsonSerializer.Deserialize<WorkflowMessage>(messageText);

            if (message?.Type == "hitl_response")
            {
                var hitlResponse = JsonSerializer.Deserialize<HITLResponseMessage>(messageText);
                if (hitlResponse != null)
                {
                    _pendingHitlResponse = hitlResponse;
                    _hitlResponseSemaphore.Release();
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "クライアントメッセージの処理でエラーが発生しました");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// HITL応答を待機
    /// </summary>
    public async Task<HITLResponseMessage?> WaitForHITLResponseAsync(TimeSpan timeout)
    {
        var cts = new CancellationTokenSource(timeout);

        try
        {
            await _hitlResponseSemaphore.WaitAsync(cts.Token);
            var response = _pendingHitlResponse;
            _pendingHitlResponse = null;
            return response;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("HITL応答のタイムアウト");
            return null;
        }
    }

    /// <summary>
    /// メッセージをすべてのクライアントにブロードキャスト
    /// </summary>
    public async Task BroadcastAsync(WorkflowMessage message)
    {
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            WriteIndented = false
        });
        var bytes = Encoding.UTF8.GetBytes(json);

        var tasks = new List<Task>();

        foreach (var client in _connectedClients)
        {
            if (client.State == WebSocketState.Open)
            {
                tasks.Add(client.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None));
            }
        }

        await Task.WhenAll(tasks);
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _httpListener.Close();
        _cancellationTokenSource?.Dispose();
        _hitlResponseSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
