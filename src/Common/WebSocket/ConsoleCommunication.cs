// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Logging;

namespace Common.WebSocket;

/// <summary>
/// コンソール経由のワークフロー通信実装
/// </summary>
public class ConsoleCommunication : IWorkflowCommunication
{
    private readonly ILogger? _logger;

    public ConsoleCommunication(ILogger? logger = null)
    {
        _logger = logger;
    }

    public Task SendAgentUtteranceAsync(string agentName, string content, string? phase = null, int? riskScore = null)
    {
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
        return Task.CompletedTask;
    }

    public Task SendFinalResponseAsync(object decision, string summary)
    {
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("🎉 最終決定");
        _logger?.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger?.LogInformation("{Summary}", summary);
        Console.WriteLine();
        return Task.CompletedTask;
    }

    public Task<bool> RequestHITLApprovalAsync(
        string approvalType,
        object contractInfo,
        object riskAssessment,
        string promptMessage)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine($"【人間による承認が必要です】");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine(promptMessage);
        Console.WriteLine();
        Console.Write("承認しますか? [Y/N]: ");

        var response = Console.ReadLine()?.Trim().ToUpperInvariant();
        var approved = response == "Y" || response == "YES";

        Console.WriteLine();

        return Task.FromResult(approved);
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
        Console.WriteLine();
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
}
