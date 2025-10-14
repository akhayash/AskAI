// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace GraphExecutorWorkflowSample;

/// <summary>
/// This sample demonstrates a multi-selection routing workflow for procurement domain specialists.
/// 
/// The workflow implements:
/// 1. Router Executor: Analyzes user questions and selects relevant specialist executors
/// 2. Specialist Executors: Generate opinions in parallel based on their domain expertise
/// 3. Aggregator Executor: Consolidates all opinions into a structured final answer
///
/// Key features:
/// - Dynamic specialist selection based on question analysis
/// - Parallel execution of selected specialists for efficiency
/// - Conditional edges for routing to selected specialists
/// - State management for sharing data between executors
/// </summary>
public static class Program
{
    private static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        // Load configuration from appsettings.json and environment variables
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // OpenTelemetry とロギングを設定
        var appInsightsConnectionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        // 空文字列の場合もデフォルト値を使用
        if (string.IsNullOrEmpty(otlpEndpoint))
        {
            otlpEndpoint = "http://localhost:4317";
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService("GraphExecutorWorkflow"));
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

        var activitySource = new ActivitySource("GraphExecutorWorkflow");

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("GraphExecutorWorkflow"))
            .AddSource("GraphExecutorWorkflow")
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(exporterOptions =>
            {
                exporterOptions.Endpoint = new Uri(otlpEndpoint);
            })
            .AddConsoleExporter()
            .Build();

        var logger = loggerFactory.CreateLogger("GraphExecutorWorkflow");
        logger.LogInformation("=== アプリケーション起動 ===");
        logger.LogInformation("テレメトリ設定: OTLP Endpoint = {OtlpEndpoint}", otlpEndpoint);
        if (!string.IsNullOrEmpty(appInsightsConnectionString))
        {
            logger.LogInformation("Application Insights 接続文字列が設定されています");
        }

        // Get Azure OpenAI settings from configuration or environment variables
        var endpoint = configuration["environmentVariables:AZURE_OPENAI_ENDPOINT"]
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
            ?? throw new InvalidOperationException("環境変数 AZURE_OPENAI_ENDPOINT が設定されていません。");

        var deploymentName = configuration["environmentVariables:AZURE_OPENAI_DEPLOYMENT_NAME"]
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
            ?? "gpt-4o";

        logger.LogInformation("エンドポイント: {Endpoint}", endpoint);
        logger.LogInformation("デプロイメント名: {DeploymentName}", deploymentName);

        logger.LogInformation("認証情報の取得中（Azure CLI のみを使用）...");
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeEnvironmentCredential = true,
            ExcludeManagedIdentityCredential = true,
            ExcludeSharedTokenCacheCredential = true,
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeAzureCliCredential = false,  // Azure CLI のみ有効
            ExcludeAzurePowerShellCredential = true,
            ExcludeAzureDeveloperCliCredential = true,
            ExcludeInteractiveBrowserCredential = true,
            ExcludeWorkloadIdentityCredential = true
        });
        logger.LogInformation("認証情報取得完了");

        var chatClient = new AzureOpenAIClient(new Uri(endpoint), credential)
            .GetChatClient(deploymentName)
            .AsIChatClient();

        logger.LogInformation("=== Graph Executor Workflow デモ ===");
        Console.WriteLine("=== Graph Executor Workflow デモ ===");
        Console.WriteLine();
        Console.WriteLine("このワークフローは以下のフローを実装しています:");
        Console.WriteLine("1. Router Executor: ユーザー質問から必要な専門家を特定");
        Console.WriteLine("2. Specialist Executors: 各専門家が並列で意見を生成");
        Console.WriteLine("3. Aggregator Executor: 意見を集約し最終出力");
        Console.WriteLine();

        // Create agents for each executor
        AIAgent routerAgent = GetRouterAgent(chatClient);
        AIAgent contractAgent = GetSpecialistAgent(chatClient, "Contract", "契約関連");
        AIAgent spendAgent = GetSpecialistAgent(chatClient, "Spend", "支出分析");
        AIAgent negotiationAgent = GetSpecialistAgent(chatClient, "Negotiation", "交渉戦略");
        AIAgent sourcingAgent = GetSpecialistAgent(chatClient, "Sourcing", "調達戦略");
        AIAgent knowledgeAgent = GetSpecialistAgent(chatClient, "Knowledge", "知識管理");
        AIAgent supplierAgent = GetSpecialistAgent(chatClient, "Supplier", "サプライヤー管理");
        AIAgent aggregatorAgent = GetAggregatorAgent(chatClient);

        // Create executors
        var routerExecutor = new RouterExecutor(routerAgent);
        var contractExecutor = new SpecialistExecutor(contractAgent, "Contract");
        var spendExecutor = new SpecialistExecutor(spendAgent, "Spend");
        var negotiationExecutor = new SpecialistExecutor(negotiationAgent, "Negotiation");
        var sourcingExecutor = new SpecialistExecutor(sourcingAgent, "Sourcing");
        var knowledgeExecutor = new SpecialistExecutor(knowledgeAgent, "Knowledge");
        var supplierExecutor = new SpecialistExecutor(supplierAgent, "Supplier");
        var aggregatorExecutor = new AggregatorExecutor(aggregatorAgent);

        // Build the workflow with conditional edges
        WorkflowBuilder builder = new(routerExecutor);
        builder
            .AddFanOutEdge(
                routerExecutor,
                targets: [
                    contractExecutor,
                    spendExecutor,
                    negotiationExecutor,
                    sourcingExecutor,
                    knowledgeExecutor,
                    supplierExecutor
                ],
                partitioner: GetSpecialistPartitioner()
            )
            // All specialists route to the aggregator
            .AddEdge(contractExecutor, aggregatorExecutor)
            .AddEdge(spendExecutor, aggregatorExecutor)
            .AddEdge(negotiationExecutor, aggregatorExecutor)
            .AddEdge(sourcingExecutor, aggregatorExecutor)
            .AddEdge(knowledgeExecutor, aggregatorExecutor)
            .AddEdge(supplierExecutor, aggregatorExecutor)
            .WithOutputFrom(aggregatorExecutor);

        var workflow = builder.Build();

        // Output workflow visualization
        logger.LogInformation("ワークフローグラフの構造:");
        Console.WriteLine();
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("ワークフローグラフ構造:");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("User Question");
        Console.WriteLine("    ↓");
        Console.WriteLine("┌─────────────────┐");
        Console.WriteLine("│ Router Executor │  ← 専門家を選抜");
        Console.WriteLine("└─────────────────┘");
        Console.WriteLine("    ↓ (AddFanOutEdge with partitioner)");
        Console.WriteLine("┌─────────┐ ┌─────────┐ ┌─────────┐");
        Console.WriteLine("│Contract │ │  Spend  │ │Supplier │  ← 並列実行");
        Console.WriteLine("│Executor │ │Executor │ │Executor │");
        Console.WriteLine("└─────────┘ └─────────┘ └─────────┘");
        Console.WriteLine("    ↓           ↓           ↓");
        Console.WriteLine("    └───────────┴───────────┘");
        Console.WriteLine("             ↓ (AddEdge - join)");
        Console.WriteLine("    ┌──────────────────┐");
        Console.WriteLine("    │   Aggregator     │  ← 意見を集約");
        Console.WriteLine("    │   Executor       │");
        Console.WriteLine("    └──────────────────┘");
        Console.WriteLine("             ↓");
        Console.WriteLine("         Final Answer");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();

        // Get user input
        Console.Write("質問> ");
        var question = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(question))
        {
            logger.LogWarning("質問が空です。");
            Console.WriteLine("質問が空です。");
            return;
        }

        logger.LogInformation("受信した質問: {Question}", question);

        Console.WriteLine();
        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        logger.LogInformation("ワークフロー実行中...");
        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("ワークフロー実行中...");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();

        // Execute the workflow
        await using StreamingRun run = await InProcessExecution.StreamAsync(
            workflow, 
            new ChatMessage(ChatRole.User, question)
        );
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        
        await foreach (WorkflowEvent evt in run.WatchStreamAsync().ConfigureAwait(false))
        {
            if (evt is WorkflowOutputEvent outputEvent)
            {
                Console.WriteLine($"{outputEvent}");
            }
            else if (evt is SpecialistEvent specialistEvent)
            {
                Console.WriteLine($"[{specialistEvent.SpecialistName}] {specialistEvent.Message}");
            }
            else if (evt is RouterEvent routerEvent)
            {
                Console.WriteLine($"[Router] {routerEvent.Message}");
            }
        }

        Console.WriteLine();
        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        logger.LogInformation("ワークフロー完了");
        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("ワークフロー完了");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();
        Console.WriteLine("Enter キーを押して終了してください...");
        Console.ReadLine();

        logger.LogInformation("=== アプリケーション終了 ===");
    }

    /// <summary>
    /// Creates a partitioner for routing to specialists based on router decision.
    /// </summary>
    private static Func<RouterDecision?, int, IEnumerable<int>> GetSpecialistPartitioner()
    {
        return (routerDecision, targetCount) =>
        {
            if (routerDecision is not null && routerDecision.Selected.Count > 0)
            {
                var targets = new List<int>();
                var specialistNames = new[] { "Contract", "Spend", "Negotiation", "Sourcing", "Knowledge", "Supplier" };

                foreach (var selected in routerDecision.Selected)
                {
                    var index = Array.IndexOf(specialistNames, selected);
                    if (index >= 0)
                    {
                        targets.Add(index);
                    }
                }

                return targets.Count > 0 ? targets : new[] { 4 }; // Default to Knowledge if no match
            }
            
            return new[] { 4 }; // Default to Knowledge specialist
        };
    }

    /// <summary>
    /// Creates the router agent that selects appropriate specialists.
    /// </summary>
    private static ChatClientAgent GetRouterAgent(IChatClient chatClient) =>
        new(chatClient, new ChatClientAgentOptions(
            instructions: """
            あなたは調達領域のルーターです。ユーザーの質問を分析し、必要な専門家を選抜します。
            
            専門家候補:
            - Contract: 契約条件、契約交渉、契約リスク
            - Spend: コスト分析、予算管理、支出最適化
            - Negotiation: 交渉戦略、価格交渉、条件交渉
            - Sourcing: サプライヤー選定、調達戦略、ソーシング
            - Knowledge: 一般的な調達知識、ベストプラクティス
            - Supplier: サプライヤー管理、関係構築、評価
            
            通常は2-3件の専門家で十分です。質問の内容を慎重に分析してください。
            """)
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<RouterDecision>()
            }
        });

    /// <summary>
    /// Creates a specialist agent for a specific domain.
    /// </summary>
    private static ChatClientAgent GetSpecialistAgent(IChatClient chatClient, string name, string domain) =>
        new(chatClient, new ChatClientAgentOptions(
            instructions: $"""
            あなたは{domain}の専門家です。
            質問に対して、{domain}の観点から重要なポイントを2-3文で簡潔に述べてください。
            専門的な知識と実務経験に基づいた意見を提供してください。
            """)
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<SpecialistOpinion>()
            }
        });

    /// <summary>
    /// Creates the aggregator agent that consolidates specialist opinions.
    /// </summary>
    private static ChatClientAgent GetAggregatorAgent(IChatClient chatClient) =>
        new(chatClient, new ChatClientAgentOptions(
            instructions: """
            あなたは Aggregator です。複数の専門家の意見を統合し、構造化された最終回答を生成します。
            
            各専門家の意見を尊重しながら、一貫性のある結論を導いてください。
            回答は以下の形式で生成してください:
            
            ## 結論
            [統合された結論を3-4文で記述]
            
            ## 根拠
            - [根拠1]
            - [根拠2]
            - [根拠3]
            
            ## 各専門家の所見
            [各専門家の意見を要約]
            
            ## 推奨アクション
            - [アクション1]
            - [アクション2]
            - [アクション3]
            """)
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<AggregatedResponse>()
            }
        });
}

// ==========================================
// State Constants
// ==========================================

internal static class WorkflowStateConstants
{
    public const string QuestionStateScope = "QuestionState";
    public const string OpinionsStateScope = "OpinionsState";
}

// ==========================================
// Data Models
// ==========================================

/// <summary>
/// Represents the router's decision on which specialists to engage.
/// </summary>
public sealed class RouterDecision
{
    [JsonPropertyName("selected")]
    public List<string> Selected { get; set; } = new();

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonIgnore]
    public string QuestionId { get; set; } = string.Empty;
}

/// <summary>
/// Represents a question with metadata.
/// </summary>
internal sealed class Question
{
    [JsonPropertyName("question_id")]
    public string QuestionId { get; set; } = string.Empty;

    [JsonPropertyName("question_text")]
    public string QuestionText { get; set; } = string.Empty;
}

/// <summary>
/// Represents a specialist's opinion.
/// </summary>
public sealed class SpecialistOpinion
{
    [JsonPropertyName("opinion")]
    public string Opinion { get; set; } = string.Empty;

    [JsonPropertyName("key_points")]
    public List<string> KeyPoints { get; set; } = new();
}

/// <summary>
/// Represents an opinion with metadata.
/// </summary>
internal sealed class OpinionData
{
    [JsonPropertyName("specialist_name")]
    public string SpecialistName { get; set; } = string.Empty;

    [JsonPropertyName("opinion")]
    public string Opinion { get; set; } = string.Empty;

    [JsonPropertyName("question_id")]
    public string QuestionId { get; set; } = string.Empty;
}

/// <summary>
/// Represents the aggregated response.
/// </summary>
public sealed class AggregatedResponse
{
    [JsonPropertyName("conclusion")]
    public string Conclusion { get; set; } = string.Empty;

    [JsonPropertyName("rationale")]
    public List<string> Rationale { get; set; } = new();

    [JsonPropertyName("specialist_insights")]
    public string SpecialistInsights { get; set; } = string.Empty;

    [JsonPropertyName("recommended_actions")]
    public List<string> RecommendedActions { get; set; } = new();
}

// ==========================================
// Custom Events
// ==========================================

/// <summary>
/// Event emitted by the router executor.
/// </summary>
internal sealed class RouterEvent : WorkflowEvent
{
    public RouterEvent(string message) : base(message) { }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Event emitted by specialist executors.
/// </summary>
internal sealed class SpecialistEvent : WorkflowEvent
{
    public SpecialistEvent(string specialistName, string message) : base(message)
    {
        SpecialistName = specialistName;
        Message = message;
    }
    
    public string SpecialistName { get; }
    public string Message { get; }
}

// ==========================================
// Executors
// ==========================================

/// <summary>
/// Router executor that analyzes questions and selects appropriate specialists.
/// </summary>
internal sealed class RouterExecutor : ReflectingExecutor<RouterExecutor>, IMessageHandler<ChatMessage, RouterDecision>
{
    private readonly AIAgent _routerAgent;

    public RouterExecutor(AIAgent routerAgent) : base("RouterExecutor")
    {
        _routerAgent = routerAgent;
    }

    public async ValueTask<RouterDecision> HandleAsync(ChatMessage message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // Store the question
        var question = new Question
        {
            QuestionId = Guid.NewGuid().ToString("N"),
            QuestionText = message.Text
        };
        await context.QueueStateUpdateAsync(question.QuestionId, question, scopeName: WorkflowStateConstants.QuestionStateScope, cancellationToken);

        // Invoke the router agent
        var response = await _routerAgent.RunAsync(message, cancellationToken: cancellationToken);
        var routerDecision = JsonSerializer.Deserialize<RouterDecision>(response.Text);

        if (routerDecision is null || routerDecision.Selected.Count == 0)
        {
            routerDecision = new RouterDecision
            {
                Selected = new List<string> { "Knowledge" },
                Reason = "デフォルト選抜: 明確な専門領域が特定できませんでした。"
            };
        }

        // Set the question ID
        routerDecision.QuestionId = question.QuestionId;

        // Emit event
        await context.AddEventAsync(
            new RouterEvent($"選抜された専門家: {string.Join(", ", routerDecision.Selected)} - 理由: {routerDecision.Reason}"),
            cancellationToken
        );

        return routerDecision;
    }
}

/// <summary>
/// Specialist executor that provides domain-specific opinions.
/// </summary>
internal sealed class SpecialistExecutor : ReflectingExecutor<SpecialistExecutor>, IMessageHandler<RouterDecision, OpinionData>
{
    private readonly AIAgent _specialistAgent;
    private readonly string _specialistName;

    public SpecialistExecutor(AIAgent specialistAgent, string specialistName) : base($"{specialistName}Executor")
    {
        _specialistAgent = specialistAgent;
        _specialistName = specialistName;
    }

    public async ValueTask<OpinionData> HandleAsync(RouterDecision message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // Get the question ID from router decision (we'll add it there)
        var questionId = message.QuestionId;
        
        if (string.IsNullOrEmpty(questionId))
        {
            throw new InvalidOperationException("Question ID not found in router decision");
        }
        
        var question = await context.ReadStateAsync<Question>(questionId, scopeName: WorkflowStateConstants.QuestionStateScope, cancellationToken);
        
        if (question is null)
        {
            throw new InvalidOperationException($"Question with ID {questionId} not found");
        }

        // Invoke the specialist agent
        var response = await _specialistAgent.RunAsync(question.QuestionText, cancellationToken: cancellationToken);
        var specialistOpinion = JsonSerializer.Deserialize<SpecialistOpinion>(response.Text);

        var opinionData = new OpinionData
        {
            SpecialistName = _specialistName,
            Opinion = specialistOpinion?.Opinion ?? string.Empty,
            QuestionId = questionId
        };

        // Store the opinion
        var opinionKey = $"{questionId}_{_specialistName}";
        await context.QueueStateUpdateAsync(opinionKey, opinionData, scopeName: WorkflowStateConstants.OpinionsStateScope, cancellationToken);

        // Emit event
        await context.AddEventAsync(
            new SpecialistEvent(_specialistName, $"意見生成完了: {opinionData.Opinion.Substring(0, Math.Min(50, opinionData.Opinion.Length))}..."),
            cancellationToken
        );

        return opinionData;
    }
}

/// <summary>
/// Aggregator executor that consolidates all specialist opinions.
/// </summary>
internal sealed class AggregatorExecutor : ReflectingExecutor<AggregatorExecutor>, IMessageHandler<OpinionData>
{
    private readonly AIAgent _aggregatorAgent;

    public AggregatorExecutor(AIAgent aggregatorAgent) : base("AggregatorExecutor")
    {
        _aggregatorAgent = aggregatorAgent;
    }

    public async ValueTask HandleAsync(OpinionData message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // Get the question
        var question = await context.ReadStateAsync<Question>(message.QuestionId, scopeName: WorkflowStateConstants.QuestionStateScope, cancellationToken);

        // Collect all opinions for this question
        var allOpinions = new List<OpinionData>();
        
        // Try to read all possible specialist opinions
        var specialistNames = new[] { "Contract", "Spend", "Negotiation", "Sourcing", "Knowledge", "Supplier" };
        foreach (var name in specialistNames)
        {
            var opinionKey = $"{message.QuestionId}_{name}";
            var opinion = await context.ReadStateAsync<OpinionData>(opinionKey, scopeName: WorkflowStateConstants.OpinionsStateScope, cancellationToken);
            if (opinion is not null)
            {
                allOpinions.Add(opinion);
            }
        }

        // Build the aggregation prompt
        var opinionsSummary = string.Join("\n\n", allOpinions.Select(o =>
            $"【{o.SpecialistName} の意見】\n{o.Opinion}"));

        var aggregationPrompt = $"""
質問: {question?.QuestionText}

以下は各専門家の意見です:

{opinionsSummary}

これらの意見を統合し、構造化された最終回答を生成してください。
""";

        // Invoke the aggregator agent
        var response = await _aggregatorAgent.RunAsync(aggregationPrompt, cancellationToken: cancellationToken);
        var aggregatedResponse = JsonSerializer.Deserialize<AggregatedResponse>(response.Text);

        if (aggregatedResponse is not null)
        {
            var output = new StringBuilder();
            output.AppendLine();
            output.AppendLine("═══════════════════════════════════════════");
            output.AppendLine("【最終回答】");
            output.AppendLine("═══════════════════════════════════════════");
            output.AppendLine();
            output.AppendLine("## 結論");
            output.AppendLine(aggregatedResponse.Conclusion);
            output.AppendLine();
            output.AppendLine("## 根拠");
            foreach (var rationale in aggregatedResponse.Rationale)
            {
                output.AppendLine($"- {rationale}");
            }
            output.AppendLine();
            output.AppendLine("## 各専門家の所見");
            output.AppendLine(aggregatedResponse.SpecialistInsights);
            output.AppendLine();
            output.AppendLine("## 推奨アクション");
            foreach (var action in aggregatedResponse.RecommendedActions)
            {
                output.AppendLine($"- {action}");
            }
            output.AppendLine();
            output.AppendLine("═══════════════════════════════════════════");

            await context.YieldOutputAsync(output.ToString(), cancellationToken);
        }
    }
}
