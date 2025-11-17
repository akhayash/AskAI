// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AdvancedConditionalWorkflow.Executors;
using AdvancedConditionalWorkflow.Models;
using Azure.AI.OpenAI;
using Azure.Identity;
using Common.WebSocket;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AdvancedConditionalWorkflow;

/// <summary>
/// Shared State のスコープ定数
/// </summary>
internal static class SharedStateScopes
{
    public const string OriginalContract = "OriginalContract";
    public const string OriginalRisk = "OriginalRisk";
    public const string NegotiationHistory = "NegotiationHistory";
    public const string EvaluationHistory = "EvaluationHistory";
}

/// <summary>
/// Advanced Conditional Workflow:
/// Condition, Loop, HITL, Visualize, Multi-Selection を活用した
/// 契約レビュー→自動交渉→承認プロセスのデモ
/// </summary>
public static class Program
{
    internal static ActivitySource? ActivitySource;
    internal static ILogger? Logger;

    // Communication is initialized in Main() before any workflow execution
    // and accessed only by Executors during workflow execution
    // Made public to allow DevUIHost to inject its own communication implementation
    public static Common.WebSocket.IWorkflowCommunication? Communication;

    private static async Task Main(string[] args)
    {
        // コマンドライン引数でモード判定
        var mode = args.Length > 0 && args[0].Equals("--websocket", StringComparison.OrdinalIgnoreCase)
            ? "websocket"
            : "console";
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        // 設定読み込み
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // OpenTelemetry 設定
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        if (string.IsNullOrEmpty(otlpEndpoint))
        {
            otlpEndpoint = "http://localhost:4317";
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService("AdvancedConditionalWorkflow"));
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;

                options.AddOtlpExporter(exporterOptions =>
                {
                    exporterOptions.Endpoint = new Uri(otlpEndpoint);
                });

                options.AddConsoleExporter();
            });
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            });

            builder.SetMinimumLevel(LogLevel.Information);
        });

        ActivitySource = new ActivitySource("AdvancedConditionalWorkflow");

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("AdvancedConditionalWorkflow"))
            .AddSource("Microsoft.Agents.AI.Workflows*")  // Agent Framework 内部ログ
            .AddSource("AdvancedConditionalWorkflow")
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(exporterOptions =>
            {
                exporterOptions.Endpoint = new Uri(otlpEndpoint);
            })
            .AddConsoleExporter()
            .Build();

        Logger = loggerFactory.CreateLogger("AdvancedConditionalWorkflow");

        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Logger.LogInformation("Advanced Conditional Workflow デモ");
        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Logger.LogInformation("実行モード: {Mode}", mode.ToUpper());
        Logger.LogInformation("テレメトリ設定: OTLP Endpoint = {OtlpEndpoint}", otlpEndpoint);

        // Communication設定 (WebSocketまたはConsole)
        WorkflowWebSocketServer? webSocketServer = null;

        if (mode == "websocket")
        {
            webSocketServer = new WorkflowWebSocketServer(8080, Logger);
            webSocketServer.Start();
            Communication = new WebSocketCommunication(webSocketServer, Logger);
            Logger.LogInformation("✓ WebSocketサーバー起動完了 (Port: 8080)");
        }
        else
        {
            Communication = new ConsoleCommunication(Logger);
            Logger.LogInformation("✓ コンソールモードで実行");
        }

        Console.WriteLine();

        // Azure OpenAI クライアント設定
        var endpoint = configuration["environmentVariables:AZURE_OPENAI_ENDPOINT"]
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
            ?? throw new InvalidOperationException("環境変数 AZURE_OPENAI_ENDPOINT が設定されていません。");

        var deploymentName = configuration["environmentVariables:AZURE_OPENAI_DEPLOYMENT_NAME"]
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
            ?? "gpt-4o";

        Logger.LogInformation("Azure OpenAI エンドポイント: {Endpoint}", endpoint);
        Logger.LogInformation("デプロイメント名: {DeploymentName}", deploymentName);

        var credential = new AzureCliCredential();
        var chatClient = new AzureOpenAIClient(new Uri(endpoint), credential)
            .GetChatClient(deploymentName)
            .AsIChatClient();

        Logger.LogInformation("✓ Azure OpenAI クライアント初期化完了");
        Console.WriteLine();

        // 3パターンの契約データを作成
        var testContracts = new[]
        {
            // パターン1: 低リスク契約 (ペナルティ条項あり、自動更新なし、短期)
            new ContractInfo
            {
                SupplierName = "Reliable Goods Co.",
                ContractValue = 100000m,
                ContractTermMonths = 12,
                PaymentTerms = "Net 30",
                DeliveryTerms = "FOB Destination",
                WarrantyPeriodMonths = 24,
                HasPenaltyClause = true,
                HasAutoRenewal = false,
                Description = "標準的な物品供給契約。ペナルティ条項あり、自動更新なし。"
            },
            // パターン2: 中リスク契約 (標準的な条件)
            new ContractInfo
            {
                SupplierName = "Standard Services Ltd.",
                ContractValue = 300000m,
                ContractTermMonths = 18,
                PaymentTerms = "Net 45",
                DeliveryTerms = "FOB Destination",
                WarrantyPeriodMonths = 12,
                HasPenaltyClause = true,
                HasAutoRenewal = true,
                Description = "サービス提供契約。標準的な条件。"
            },
            // パターン3: 高リスク契約 (ペナルティなし、自動更新あり、長期)
            new ContractInfo
            {
                SupplierName = "Global Tech Solutions Inc.",
                ContractValue = 500000m,
                ContractTermMonths = 24,
                PaymentTerms = "Net 30",
                DeliveryTerms = "FOB Destination",
                WarrantyPeriodMonths = 12,
                HasPenaltyClause = false,
                HasAutoRenewal = true,
                Description = "クラウドインフラサービスの提供契約。24ヶ月の長期契約で自動更新条項あり。"
            }
        };

        // ワークフロー構築
        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Logger.LogInformation("ワークフロー構築中...");
        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        var workflow = BuildWorkflow(chatClient, Logger);

        // Mermaid図をログ出力
        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Logger.LogInformation("ワークフロー構造 (Mermaid図)");
        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        var mermaidDiagram = workflow.ToMermaidString();
        Logger.LogInformation("{MermaidDiagram}", mermaidDiagram);
        Console.WriteLine();

        Logger.LogInformation("✓ ワークフロー構築完了");
        Console.WriteLine();

        // WebSocketモードの場合、クライアント接続を待機
        if (mode == "websocket" && webSocketServer != null)
        {
            Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Logger.LogInformation("WebSocketクライアントの接続を待機中...");
            Logger.LogInformation("ブラウザで http://localhost:3000 を開いてください");
            Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            // クライアントが実際に接続するまで待機
            await webSocketServer.WaitForClientConnectionAsync();

            Logger.LogInformation("✓ クライアント接続を確認しました (接続数: {Count})", webSocketServer.ConnectedClientCount);
            Logger.LogInformation("✓ 契約選択プロセスを開始します。");
            Console.WriteLine();
        }

        // ユーザーに契約パターンを選択させる
        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Logger.LogInformation("契約評価パターンの選択");
        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        // Communication経由で契約選択を要求
        var selectedIndex = await Communication.RequestContractSelectionAsync(testContracts);

        Logger.LogInformation("選択された契約: インデックス {SelectedIndex}", selectedIndex);
        Console.WriteLine();

        // 実行する契約を決定（選択されたもののみ）
        var contractsToRun = new[] { testContracts[selectedIndex] };
        var startIndex = selectedIndex;

        // 選択されたパターンを実行
        for (int i = 0; i < contractsToRun.Length; i++)
        {
            var contract = contractsToRun[i];
            var actualIndex = startIndex;
            var patternLabel = actualIndex switch
            {
                0 => "低リスク",
                1 => "中リスク",
                2 => "高リスク",
                _ => "不明"
            };

            Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Logger.LogInformation("パターン {PatternNumber}: {PatternLabel} 契約の評価", actualIndex + 1, patternLabel);
            Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Logger.LogInformation("サプライヤー: {SupplierName}", contract.SupplierName);
            Logger.LogInformation("契約金額: ${ContractValue:N0}", contract.ContractValue);
            Logger.LogInformation("契約期間: {TermMonths}ヶ月", contract.ContractTermMonths);
            Logger.LogInformation("ペナルティ条項: {HasPenalty}", contract.HasPenaltyClause ? "あり" : "なし");
            Logger.LogInformation("自動更新: {HasAutoRenewal}", contract.HasAutoRenewal ? "あり" : "なし");
            Console.WriteLine();

            Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Logger.LogInformation("ワークフロー実行開始");
            Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            // ワークフロー開始をCommunicationに通知
            await Communication.SendWorkflowStartAsync(contract);

            // ワークフロー全体を包む親Activityを作成
            using var workflowActivity = ActivitySource?.StartActivity("ContractReviewWorkflow");
            workflowActivity?.SetTag("supplier", contract.SupplierName);
            workflowActivity?.SetTag("contract_value", contract.ContractValue);
            workflowActivity?.SetTag("pattern", patternLabel);
            workflowActivity?.SetTag("pattern_index", actualIndex + 1);

            try
            {
                await using var run = await InProcessExecution.StreamAsync(workflow, contract);

                // WorkflowOutputEvent重複チェック用フラグ
                var outputReceived = false;

                await foreach (var evt in run.WatchStreamAsync())
                {
                    // デバッグ用: すべてのイベントをInfoレベルで記録
                    Logger.LogInformation("📍 イベント受信: {EventType}", evt.GetType().Name);

                    switch (evt)
                    {
                        case WorkflowOutputEvent outputEvent:
                            // 重複チェック: 既に出力を受信している場合はスキップ
                            if (outputReceived)
                            {
                                Logger.LogWarning("⚠️ 重複するWorkflowOutputEventを検出しました。スキップします。");
                                break;
                            }

                            outputReceived = true;

                            Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                            Logger.LogInformation("🎉 ワークフロー完了");
                            Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                            if (outputEvent.Data is FinalDecision decision)
                            {
                                // 元の契約情報を追加（交渉前後の差分表示用）
                                var enrichedDecision = decision with
                                {
                                    OriginalContractInfo = contract
                                };

                                workflowActivity?.SetTag("final_decision", enrichedDecision.Decision);
                                workflowActivity?.SetTag("final_risk_score", enrichedDecision.FinalRiskScore);
                                DisplayFinalDecision(enrichedDecision);

                                // ワークフロー完了をCommunicationに通知
                                await Communication.SendWorkflowCompleteAsync(enrichedDecision);
                                await Communication.SendFinalResponseAsync(
                                    enrichedDecision,
                                    $"決定: {enrichedDecision.Decision}, 最終リスクスコア: {enrichedDecision.FinalRiskScore}/100");
                            }
                            else
                            {
                                Logger.LogInformation("出力: {Output}", outputEvent.Data);
                            }
                            break;

                        case SuperStepCompletedEvent superStepEvent:
                            Logger.LogTrace("SuperStep 完了");
                            break;

                        default:
                            // その他のすべてのイベントはTraceレベルで記録
                            Logger.LogTrace("⚪ その他のイベント: {EventType}", evt.GetType().Name);
                            try
                            {
                                var eventJson = JsonSerializer.Serialize(evt, new JsonSerializerOptions
                                {
                                    WriteIndented = false,
                                    IgnoreReadOnlyProperties = false
                                });
                                Logger.LogTrace("   イベント詳細: {EventData}", eventJson);
                            }
                            catch (Exception jsonEx)
                            {
                                // JSON化できない場合は ToString()
                                Logger.LogTrace("   イベント詳細 (ToString): {EventData}", evt.ToString());
                                Logger.LogDebug("   JSON化失敗: {JsonError}", jsonEx.Message);
                            }
                            break;
                    }
                }

                workflowActivity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                workflowActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                Logger.LogError(ex, "❌ ワークフロー実行中にエラーが発生しました: パターン {PatternNumber}", actualIndex + 1);
            }

            // 次のパターンとの間に区切り
            if (i < contractsToRun.Length - 1)
            {
                Console.WriteLine();
                Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine();
                await Task.Delay(1000); // 少し待機
            }
        }

        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Logger.LogInformation("=== 評価完了 ===");
        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        // WebSocketサーバーのクリーンアップ
        if (webSocketServer != null)
        {
            Logger.LogInformation("WebSocketサーバーを停止しています...");
            await webSocketServer.StopAsync();
            webSocketServer.Dispose();
        }
    }

    private static Workflow BuildWorkflow(IChatClient chatClient, ILogger? logger)
    {
        // === Phase 1: 契約分析 ===
        var analysisExecutor = new ContractAnalysisExecutor(logger);

        // === Phase 2: Fan-Out/Fan-In - 並列専門家レビュー ===
        var legalReviewer = new SpecialistReviewExecutor(chatClient, "Legal", "legal_reviewer", logger);
        var financeReviewer = new SpecialistReviewExecutor(chatClient, "Finance", "finance_reviewer", logger);
        var procurementReviewer = new SpecialistReviewExecutor(chatClient, "Procurement", "procurement_reviewer", logger);
        var aggregator = new ParallelReviewAggregator(logger);

        // === Phase 3: Switch - リスクベース分岐 ===
        var lowRiskApproval = new LowRiskApprovalExecutor(logger);

        // === Phase 4: Loop - 交渉反復 (中リスク用) ===
        var negotiationStateInit = new NegotiationStateInitExecutor(logger);
        var negotiationExecutor = new NegotiationExecutor(chatClient, logger);
        var negotiationContext = new NegotiationContextExecutor(logger);
        var negotiationLoopBack = new NegotiationLoopBackExecutor(logger);
        var negotiationResult = new NegotiationResultExecutor(logger);

        // === Phase 5: HITL - 人間による最終判断 ===
        var finalApprovalHITL = new HITLApprovalExecutor("final_approval", logger);
        var escalationHITL = new HITLApprovalExecutor("escalation", logger);
        var rejectionConfirmHITL = new HITLApprovalExecutor("rejection_confirm", logger);

        // === ワークフロー構築 ===
        var builder = new WorkflowBuilder(analysisExecutor);

        // Fan-Out: 契約分析後、3人の専門家に並列配信
        builder.AddFanOutEdge(analysisExecutor, targets: [legalReviewer, financeReviewer, procurementReviewer]);

        // Fan-In: 3人のレビューをAggregatorに集約
        builder.AddFanInEdge(aggregator, sources: [legalReviewer, financeReviewer, procurementReviewer]);

        // Switch: リスクスコアによる3方向分岐
        builder
            // 低リスク (≤30): 即座に承認
            .AddEdge(aggregator, lowRiskApproval,
                condition: (ContractRiskOutput? data) =>
                    data != null && data.Risk.OverallRiskScore <= 30)

            // 中リスク (31-70): 交渉ループへ
            .AddEdge(aggregator, negotiationStateInit,
                condition: (ContractRiskOutput? data) =>
                    data != null &&
                    data.Risk.OverallRiskScore > 30 &&
                    data.Risk.OverallRiskScore <= 70)

            // 高リスク (>70): HITL確認へ
            .AddEdge(aggregator, rejectionConfirmHITL,
                condition: (ContractRiskOutput? data) =>
                    data != null && data.Risk.OverallRiskScore > 70);

        // Loop: 交渉反復フロー
        builder
            // 状態初期化 → 交渉提案生成
            .AddEdge(negotiationStateInit, negotiationExecutor)
            // 交渉提案 → 評価 (状態から契約とリスクを取得)
            .AddEdge(negotiationExecutor, negotiationContext)

            // ループバック: 継続 && 改善余地あり → ループバック処理 → 次の交渉へ
            .AddEdge(negotiationContext, negotiationLoopBack,
                condition: (ContractEvaluationOutput? data) =>
                    data != null && data.Evaluation.ContinueNegotiation)
            .AddEdge(negotiationLoopBack, negotiationExecutor)

            // 評価結果 → リスク評価形式に変換 (ループ終了時のみ)
            .AddEdge(negotiationContext, negotiationResult,
                condition: (ContractEvaluationOutput? data) =>
                    data != null && !data.Evaluation.ContinueNegotiation)

            // ループ終了: 目標達成 → HITL最終承認
            .AddEdge(negotiationResult, finalApprovalHITL,
                condition: (ContractRiskOutput? data) =>
                    data != null && data.Risk.OverallRiskScore <= 30)

            // ループ終了: 目標未達成 → HITLエスカレーション
            .AddEdge(negotiationResult, escalationHITL,
                condition: (ContractRiskOutput? data) =>
                    data != null && data.Risk.OverallRiskScore > 30);

        // 出力設定: 各終端からの出力を許可
        builder
            .WithOutputFrom(lowRiskApproval)
            .WithOutputFrom(finalApprovalHITL)
            .WithOutputFrom(escalationHITL)
            .WithOutputFrom(rejectionConfirmHITL);

        return builder.Build();
    }

    private static void DisplayFinalDecision(FinalDecision decision)
    {
        Logger?.LogInformation("【最終決定】");
        Logger?.LogInformation("決定: {Decision}", decision.Decision);
        Logger?.LogInformation("最終リスクスコア: {RiskScore}/100", decision.FinalRiskScore);
        Logger?.LogInformation("サマリー: {Summary}", decision.DecisionSummary);

        if (decision.NextActions != null && decision.NextActions.Count > 0)
        {
            Logger?.LogInformation("次のアクション:");
            foreach (var action in decision.NextActions)
            {
                Logger?.LogInformation("  - {Action}", action);
            }
        }
    }
}
