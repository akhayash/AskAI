// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AdvancedConditionalWorkflow.Executors;
using AdvancedConditionalWorkflow.Models;
using Azure.AI.OpenAI;
using Azure.Identity;
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
/// Advanced Conditional Workflow:
/// Condition, Loop, HITL, Visualize, Multi-Selection を活用した
/// 契約レビュー→自動交渉→承認プロセスのデモ
/// </summary>
public static class Program
{
    internal static ActivitySource? ActivitySource;
    internal static ILogger? Logger;

    private static async Task Main()
    {
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
        Logger.LogInformation("テレメトリ設定: OTLP Endpoint = {OtlpEndpoint}", otlpEndpoint);

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

        // ユーザーに契約パターンを選択させる
        Console.WriteLine();
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("契約評価パターンの選択");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();
        Console.WriteLine("評価する契約パターンを選択してください:");
        Console.WriteLine();
        Console.WriteLine("  [0] 全パターンを順次実行");
        Console.WriteLine();
        Console.WriteLine("  [1] 低リスク契約");
        Console.WriteLine("      - サプライヤー: Reliable Goods Co.");
        Console.WriteLine("      - 契約金額: $100,000");
        Console.WriteLine("      - ペナルティ条項: あり");
        Console.WriteLine("      - 自動更新: なし");
        Console.WriteLine();
        Console.WriteLine("  [2] 中リスク契約");
        Console.WriteLine("      - サプライヤー: Standard Services Ltd.");
        Console.WriteLine("      - 契約金額: $300,000");
        Console.WriteLine("      - ペナルティ条項: あり");
        Console.WriteLine("      - 自動更新: あり");
        Console.WriteLine();
        Console.WriteLine("  [3] 高リスク契約");
        Console.WriteLine("      - サプライヤー: Global Tech Solutions Inc.");
        Console.WriteLine("      - 契約金額: $500,000");
        Console.WriteLine("      - ペナルティ条項: なし");
        Console.WriteLine("      - 自動更新: あり");
        Console.WriteLine();
        Console.Write("選択 [0-3]: ");

        var input = Console.ReadLine();
        if (!int.TryParse(input, out var selection) || selection < 0 || selection > 3)
        {
            Logger.LogWarning("無効な入力です。全パターンを実行します。");
            selection = 0;
        }

        Console.WriteLine();

        // 実行する契約を決定
        var contractsToRun = selection == 0
            ? testContracts
            : new[] { testContracts[selection - 1] };

        var startIndex = selection == 0 ? 0 : selection - 1;

        // 選択されたパターンを実行
        for (int i = 0; i < contractsToRun.Length; i++)
        {
            var contract = contractsToRun[i];
            var actualIndex = selection == 0 ? i : startIndex;
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

            // ワークフロー全体を包む親Activityを作成
            using var workflowActivity = ActivitySource?.StartActivity("ContractReviewWorkflow");
            workflowActivity?.SetTag("supplier", contract.SupplierName);
            workflowActivity?.SetTag("contract_value", contract.ContractValue);
            workflowActivity?.SetTag("pattern", patternLabel);
            workflowActivity?.SetTag("pattern_index", actualIndex + 1);

            try
            {
                await using var run = await InProcessExecution.StreamAsync(workflow, contract);

                await foreach (var evt in run.WatchStreamAsync())
                {
                    // フレームワークイベントはTraceレベルで記録
                    Logger.LogTrace("📍 イベント受信: {EventType}", evt.GetType().Name);

                    switch (evt)
                    {
                        case WorkflowOutputEvent outputEvent:
                            Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                            Logger.LogInformation("🎉 ワークフロー完了");
                            Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                            if (outputEvent.Data is FinalDecision decision)
                            {
                                workflowActivity?.SetTag("final_decision", decision.Decision);
                                workflowActivity?.SetTag("final_risk_score", decision.FinalRiskScore);
                                DisplayFinalDecision(decision);
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
        Logger.LogInformation(selection == 0 ? "=== 全パターンの評価完了 ===" : "=== 評価完了 ===");
        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
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
        var negotiationResult = new NegotiationResultExecutor(logger);

        // === Phase 5: HITL - 人間による最終判断 ===
        var finalApprovalHITL = new HITLApprovalExecutor("final_approval", logger);
        var escalationHITL = new HITLApprovalExecutor("escalation", logger);
        var rejectionConfirmHITL = new HITLApprovalExecutor("rejection_confirm", logger);

        // === ワークフロー構築 ===
        var builder = new WorkflowBuilder(analysisExecutor);

        // Fan-Out: 契約分析後、3人の専門家に並列に渡す
        // 注: 現在のフレームワークでは順次実行になるが、構造上は並列を意図
        builder
            .AddEdge(analysisExecutor, legalReviewer)
            .AddEdge(analysisExecutor, financeReviewer)
            .AddEdge(analysisExecutor, procurementReviewer);

        // Fan-In: 3人のレビューを集約
        builder
            .AddEdge(legalReviewer, aggregator)
            .AddEdge(financeReviewer, aggregator)
            .AddEdge(procurementReviewer, aggregator);

        // Switch: リスクスコアによる3方向分岐
        builder
            // 低リスク (≤30): 即座に承認
            .AddEdge(aggregator, lowRiskApproval,
                condition: ((ContractInfo, RiskAssessment)? data) =>
                    data.HasValue && data.Value.Item2.OverallRiskScore <= 30)

            // 中リスク (31-70): 交渉ループへ
            .AddEdge(aggregator, negotiationStateInit,
                condition: ((ContractInfo, RiskAssessment)? data) =>
                    data.HasValue &&
                    data.Value.Item2.OverallRiskScore > 30 &&
                    data.Value.Item2.OverallRiskScore <= 70)

            // 高リスク (>70): HITL確認へ
            .AddEdge(aggregator, rejectionConfirmHITL,
                condition: ((ContractInfo, RiskAssessment)? data) =>
                    data.HasValue && data.Value.Item2.OverallRiskScore > 70);

        // Loop: 交渉反復フロー
        builder
            // 状態初期化 → 交渉提案生成
            .AddEdge(negotiationStateInit, negotiationExecutor)
            // 交渉提案 → 評価 (状態から契約とリスクを取得)
            .AddEdge(negotiationExecutor, negotiationContext)

            // ループバック: 継続 && 改善余地あり → 次の交渉へ
            .AddEdge(negotiationContext, negotiationExecutor,
                condition: ((ContractInfo, EvaluationResult)? data) =>
                    data.HasValue && data.Value.Item2.ContinueNegotiation)

            // 評価結果 → リスク評価形式に変換 (ループ終了時のみ)
            .AddEdge(negotiationContext, negotiationResult,
                condition: ((ContractInfo, EvaluationResult)? data) =>
                    data.HasValue && !data.Value.Item2.ContinueNegotiation)

            // ループ終了: 目標達成 → HITL最終承認
            .AddEdge(negotiationResult, finalApprovalHITL,
                condition: ((ContractInfo, RiskAssessment)? data) =>
                    data.HasValue && data.Value.Item2.OverallRiskScore <= 30)

            // ループ終了: 目標未達成 → HITLエスカレーション
            .AddEdge(negotiationResult, escalationHITL,
                condition: ((ContractInfo, RiskAssessment)? data) =>
                    data.HasValue && data.Value.Item2.OverallRiskScore > 30);

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
