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

        // デモ契約情報を作成
        var sampleContract = new ContractInfo
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
        };

        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Logger.LogInformation("デモ契約情報");
        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Logger.LogInformation("サプライヤー: {SupplierName}", sampleContract.SupplierName);
        Logger.LogInformation("契約金額: ${ContractValue:N0}", sampleContract.ContractValue);
        Logger.LogInformation("契約期間: {TermMonths}ヶ月", sampleContract.ContractTermMonths);
        Logger.LogInformation("支払条件: {PaymentTerms}", sampleContract.PaymentTerms);
        Console.WriteLine();

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
        Console.WriteLine(mermaidDiagram);
        Console.WriteLine();

        Logger.LogInformation("✓ ワークフロー構築完了");
        Console.WriteLine();

        // ワークフロー実行
        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Logger.LogInformation("ワークフロー実行開始");
        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        try
        {
            await using var run = await InProcessExecution.StreamAsync(workflow, sampleContract);

            await foreach (var evt in run.WatchStreamAsync())
            {
                switch (evt)
                {
                    case WorkflowOutputEvent outputEvent:
                        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        Logger.LogInformation("🎉 ワークフロー完了");
                        Logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                        if (outputEvent.Data is FinalDecision decision)
                        {
                            DisplayFinalDecision(decision);
                        }
                        else
                        {
                            Logger.LogInformation("出力: {Output}", outputEvent.Data);
                        }
                        break;

                    case SuperStepCompletedEvent superStepEvent:
                        Logger.LogInformation("SuperStep 完了");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ ワークフロー実行中にエラーが発生しました");
        }

        Logger.LogInformation("=== アプリケーション終了 ===");
    }

    private static Workflow BuildWorkflow(IChatClient chatClient, ILogger? logger)
    {
        // Executor群の作成
        var analysisExecutor = new ContractAnalysisExecutor();
        var legalReviewer = new SpecialistReviewExecutor(chatClient, "Legal", "legal_reviewer", logger);
        var financeReviewer = new SpecialistReviewExecutor(chatClient, "Finance", "finance_reviewer", logger);
        var procurementReviewer = new SpecialistReviewExecutor(chatClient, "Procurement", "procurement_reviewer", logger);

        // 注: 完全な実装では、ここで Fan-Out/Fan-In、Loop、HITL、Switchなどを構築します
        // 現在は簡略版として基本的なフローのみ実装

        var aggregator = new ParallelReviewAggregator(logger);
        var lowRiskApproval = new LowRiskApprovalExecutor(logger);
        var highRiskRejection = new HighRiskRejectionExecutor(logger);

        // ワークフローの構築
        var builder = new WorkflowBuilder(analysisExecutor);

        // 並列レビュー (簡略版: 順次実行)
        builder
            .AddEdge(analysisExecutor, legalReviewer)
            .AddEdge(legalReviewer, financeReviewer)
            .AddEdge(financeReviewer, procurementReviewer)
            .AddEdge(procurementReviewer, aggregator);

        // リスク判定による分岐
        builder
            .AddEdge(aggregator, lowRiskApproval,
                condition: ((ContractInfo, RiskAssessment)? data) =>
                    data.HasValue && data.Value.Item2.OverallRiskScore <= 30)
            .AddEdge(aggregator, highRiskRejection,
                condition: ((ContractInfo, RiskAssessment)? data) =>
                    data.HasValue && data.Value.Item2.OverallRiskScore > 30);

        builder.WithOutputFrom(lowRiskApproval);
        builder.WithOutputFrom(highRiskRejection);

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
