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

    public Task<int> RequestContractSelectionAsync(object[] contracts)
    {
        Console.WriteLine();
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("契約評価パターンの選択");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();
        Console.WriteLine("評価する契約パターンを選択してください:");
        Console.WriteLine();

        for (int i = 0; i < contracts.Length; i++)
        {
            dynamic contract = contracts[i];
            var label = i switch
            {
                0 => "低リスク契約",
                1 => "中リスク契約",
                2 => "高リスク契約",
                _ => $"契約パターン {i + 1}"
            };

            Console.WriteLine($"  [{i + 1}] {label}");
            Console.WriteLine($"      - サプライヤー: {contract.SupplierName}");
            Console.WriteLine($"      - 契約金額: ${contract.ContractValue:N0}");
            Console.WriteLine($"      - ペナルティ条項: {(contract.HasPenaltyClause ? "あり" : "なし")}");
            Console.WriteLine($"      - 自動更新: {(contract.HasAutoRenewal ? "あり" : "なし")}");
            Console.WriteLine();
        }

        Console.Write($"選択 [1-{contracts.Length}]: ");

        var input = Console.ReadLine();
        if (!int.TryParse(input, out var selection) || selection < 1 || selection > contracts.Length)
        {
            _logger?.LogWarning("無効な入力です。最初の契約を選択します。");
            return Task.FromResult(0);
        }

        Console.WriteLine();
        return Task.FromResult(selection - 1); // 0-basedに変換
    }
}
