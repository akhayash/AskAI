// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Diagnostics;
using Azure.AI.OpenAI;
using Azure.Identity;
using Common;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Agents.AI.Hosting.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI.Workflows;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using AdvancedConditionalWorkflow.Executors;
using AdvancedConditionalWorkflow.Models;
using DevUIHost.Executors;

var builder = WebApplication.CreateBuilder(args);

// Configure detailed logging
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "HH:mm:ss.fff ";
});
builder.Logging.SetMinimumLevel(LogLevel.Trace);

// OpenTelemetry configuration for DevUI Traces
// DevUI expects OTLP_ENDPOINT or OTEL_EXPORTER_OTLP_ENDPOINT environment variable
var otlpEndpoint = builder.Configuration["OTLP_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("OTLP_ENDPOINT")
    ?? builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
    ?? "http://localhost:4317";

var activitySource = new ActivitySource("DevUIHost");
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("DevUIHost"))
    .WithTracing(t => t
        .AddSource("DevUIHost")
        .AddSource("Microsoft.Extensions.AI")
        .AddSource("Microsoft.Agents.AI.Workflows*")
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint))
        .AddConsoleExporter());

// Add services
builder.Services.AddHttpClient().AddLogging();
builder.Services.AddAGUI();

// CORS設定（開発環境用）
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configuration
var endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

var deploymentName = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? "gpt-4o";

// Set up the Azure OpenAI client with OpenTelemetry
var chatClient = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsBuilder()
    .UseOpenTelemetry(sourceName: "DevUIHost")
    .Build();

// Register the chat client for DI
builder.Services.AddChatClient(chatClient);

// Register specialist agents using the hosting package
builder.AddAIAgent("contract", """
あなたは Contract (契約) 専門家です。
契約条項、契約リスク、法的義務、契約期間、更新条件などの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""");

builder.AddAIAgent("spend", """
あなたは Spend Analysis (支出分析) 専門家です。
コスト構造、支出トレンド、予算管理、コスト削減機会などの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""");

builder.AddAIAgent("negotiation", """
あなたは Negotiation (交渉) 専門家です。
交渉戦略、条件改善提案、価格交渉、契約条件の最適化などの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""");

builder.AddAIAgent("sourcing", """
あなたは Sourcing (調達) 専門家です。
サプライヤー選定、調達戦略、品質管理、納期管理などの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""");

builder.AddAIAgent("knowledge", """
あなたは Knowledge Management (ナレッジ管理) 専門家です。
過去の事例、ベストプラクティス、組織の知見、業界標準などの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""");

builder.AddAIAgent("supplier", """
あなたは Supplier Management (サプライヤー管理) 専門家です。
サプライヤーの信頼性、パフォーマンス評価、リスク評価、関係管理などの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""");

builder.AddAIAgent("legal", """
あなたは Legal (法務) 専門家です。
法的リスク、コンプライアンス、規制要件、法的義務、知的財産権などの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""");

builder.AddAIAgent("finance", """
あなたは Finance (財務) 専門家です。
財務影響、予算管理、ROI分析、キャッシュフロー、財務リスクなどの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""");

builder.AddAIAgent("procurement", """
あなたは Procurement (調達実務) 専門家です。
調達プロセス、購買手続き、契約管理、サプライヤー管理、調達戦略などの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""");

builder.AddAIAgent("assistant", """
あなたは調達・購買業務の専門アシスタントです。
契約、支出分析、交渉、調達戦略、知識管理、サプライヤー管理に関する質問に答えます。
複雑な質問の場合は、専門家エージェントに相談することもできます。
""");

// シンプルなワークフローの登録 (ChatProtocol対応)
builder.AddWorkflow("simple-review-workflow", (sp, key) =>
{
    var chatClientFromDI = sp.GetRequiredService<IChatClient>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("SimpleWorkflow");

    // ChatProtocol entry point: Receives List<ChatMessage> and forwards to first executor
    var chatForwarder = new ChatForwardingExecutor($"{key}_forwarder");

    // Executor 1: 専門家レビュー (入力: List<ChatMessage>)
    var reviewerExecutor = new SimpleReviewerExecutor(chatClientFromDI, $"{key}_reviewer", logger);

    // Executor 2: 要約 (入力: string from ReviewerExecutor)
    var summarizerExecutor = new SimpleSummarizerExecutor(chatClientFromDI, $"{key}_summarizer", logger);

    // Sequential workflow: ChatMessage → Forwarder → Reviewer → Summarizer
    var workflowBuilder = new WorkflowBuilder(chatForwarder);
    workflowBuilder.AddEdge(chatForwarder, reviewerExecutor);
    workflowBuilder.AddEdge(reviewerExecutor, summarizerExecutor);

    // ⚠️ 重要: WithOutputFrom()を設定しないとワークフローの出力が生成されない
    workflowBuilder.WithOutputFrom(summarizerExecutor);

    // ⚠️ 重要: WorkflowにName属性を設定しないとAddWorkflowのバリデーションエラーになる
    workflowBuilder.WithName(key);
    workflowBuilder.WithDescription("調達専門家によるレビューと要約の2ステップワークフロー");

    return workflowBuilder.Build();
}).AddAsAIAgent();

// Advanced Conditional Workflow の登録
builder.AddWorkflow("advanced-contract-review", (sp, key) =>
{
    var chatClientFromDI = sp.GetRequiredService<IChatClient>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("AdvancedContractReview");

    // ChatProtocol entry point
    var chatForwarder = new ChatForwardingExecutor($"{key}_forwarder");

    // ContractInfo変換Executor (ChatMessage → ContractInfo)
    var contractParser = new ChatMessageToContractExecutor($"{key}_parser", logger);

    // === AdvancedConditionalWorkflow の全Executor ===
    // Phase 1: 契約分析
    var analysisExecutor = new ContractAnalysisExecutor(logger);

    // Phase 2: 並列専門家レビュー
    var legalReviewer = new SpecialistReviewExecutor(chatClientFromDI, "Legal", $"{key}_legal", logger);
    var financeReviewer = new SpecialistReviewExecutor(chatClientFromDI, "Finance", $"{key}_finance", logger);
    var procurementReviewer = new SpecialistReviewExecutor(chatClientFromDI, "Procurement", $"{key}_procurement", logger);
    var aggregator = new ParallelReviewAggregator(logger);

    // Phase 3: リスクベース分岐
    var lowRiskApproval = new LowRiskApprovalExecutor(logger);

    // Phase 4: 交渉ループ
    var negotiationStateInit = new NegotiationStateInitExecutor(logger);
    var negotiationExecutor = new NegotiationExecutor(chatClientFromDI, logger);
    var negotiationContext = new NegotiationContextExecutor(logger);
    var negotiationLoopBack = new NegotiationLoopBackExecutor(logger);
    var negotiationResult = new NegotiationResultExecutor(logger);

    // Phase 5: HITL承認
    var finalApprovalHITL = new HITLApprovalExecutor("final_approval", logger);
    var escalationHITL = new HITLApprovalExecutor("escalation", logger);
    var rejectionConfirmHITL = new HITLApprovalExecutor("rejection_confirm", logger);

    // === Workflow構築 ===
    var workflowBuilder = new WorkflowBuilder(chatForwarder);

    // ChatMessage → ContractInfo変換
    workflowBuilder.AddEdge(chatForwarder, contractParser);
    workflowBuilder.AddEdge(contractParser, analysisExecutor);

    // Fan-Out: 並列レビュー
    workflowBuilder.AddFanOutEdge(analysisExecutor,
        targets: [legalReviewer, financeReviewer, procurementReviewer]);

    // Fan-In: レビュー集約 (新しいシグネチャ)
    workflowBuilder.AddFanInEdge([legalReviewer, financeReviewer, procurementReviewer], aggregator);

    // Switch: リスクスコア分岐
    workflowBuilder
        .AddEdge(aggregator, lowRiskApproval,
            condition: (ContractRiskOutput? data) =>
                data != null && data.Risk.OverallRiskScore <= 30)
        .AddEdge(aggregator, negotiationStateInit,
            condition: (ContractRiskOutput? data) =>
                data != null &&
                data.Risk.OverallRiskScore > 30 &&
                data.Risk.OverallRiskScore <= 70)
        .AddEdge(aggregator, rejectionConfirmHITL,
            condition: (ContractRiskOutput? data) =>
                data != null && data.Risk.OverallRiskScore > 70);

    // Loop: 交渉反復
    workflowBuilder
        .AddEdge(negotiationStateInit, negotiationExecutor)
        .AddEdge(negotiationExecutor, negotiationContext)
        .AddEdge(negotiationContext, negotiationLoopBack,
            condition: (ContractEvaluationOutput? data) =>
                data != null && data.Evaluation.ContinueNegotiation)
        .AddEdge(negotiationLoopBack, negotiationExecutor)
        .AddEdge(negotiationContext, negotiationResult,
            condition: (ContractEvaluationOutput? data) =>
                data != null && !data.Evaluation.ContinueNegotiation)
        .AddEdge(negotiationResult, finalApprovalHITL,
            condition: (ContractRiskOutput? data) =>
                data != null && data.Risk.OverallRiskScore <= 30)
        .AddEdge(negotiationResult, escalationHITL,
            condition: (ContractRiskOutput? data) =>
                data != null && data.Risk.OverallRiskScore > 30);

    // 出力設定
    workflowBuilder
        .WithOutputFrom(lowRiskApproval)
        .WithOutputFrom(finalApprovalHITL)
        .WithOutputFrom(escalationHITL)
        .WithOutputFrom(rejectionConfirmHITL)
        .WithName(key)
        .WithDescription("契約レビュー→リスク評価→条件分岐→交渉ループ→HITL承認の高度なワークフロー");

    return workflowBuilder.Build();
}).AddAsAIAgent();

// Register services for OpenAI responses and conversations (required for DevUI)
builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

var app = builder.Build();

// Use CORS
app.UseCors();

// Serve static files from devui-web directory
var devuiWebPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "devui-web");
if (Directory.Exists(devuiWebPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(devuiWebPath),
        RequestPath = "/ui"
    });
}

// Create and map specialist agents for AGUI endpoints (backward compatibility)
var iChatClient = chatClient;
var contractAgent = AgentFactory.CreateContractAgent(iChatClient);
var spendAgent = AgentFactory.CreateSpendAgent(iChatClient);
var negotiationAgent = AgentFactory.CreateNegotiationAgent(iChatClient);
var sourcingAgent = AgentFactory.CreateSourcingAgent(iChatClient);
var knowledgeAgent = AgentFactory.CreateKnowledgeAgent(iChatClient);
var supplierAgent = AgentFactory.CreateSupplierAgent(iChatClient);
var legalAgent = AgentFactory.CreateLegalAgent(iChatClient);
var financeAgent = AgentFactory.CreateFinanceAgent(iChatClient);
var procurementAgent = AgentFactory.CreateProcurementAgent(iChatClient);

// Map agents to AGUI endpoints
app.MapAGUI("/agents/contract", contractAgent);
app.MapAGUI("/agents/spend", spendAgent);
app.MapAGUI("/agents/negotiation", negotiationAgent);
app.MapAGUI("/agents/sourcing", sourcingAgent);
app.MapAGUI("/agents/knowledge", knowledgeAgent);
app.MapAGUI("/agents/supplier", supplierAgent);
app.MapAGUI("/agents/legal", legalAgent);
app.MapAGUI("/agents/finance", financeAgent);
app.MapAGUI("/agents/procurement", procurementAgent);

// Create a general purpose assistant for AGUI
var assistantAgent = iChatClient.CreateAIAgent(
    name: "ProcurementAssistant",
    instructions: """
あなたは調達・購買業務の専門アシスタントです。
契約、支出分析、交渉、調達戦略、知識管理、サプライヤー管理に関する質問に答えます。
複雑な質問の場合は、専門家エージェントに相談することもできます。
"""
);

app.MapAGUI("/agents/assistant", assistantAgent);

// Map endpoints for OpenAI responses and conversations (required for DevUI)
app.MapOpenAIResponses();
app.MapOpenAIConversations();

// Map DevUI endpoint to /devui
if (builder.Environment.IsDevelopment())
{
    app.MapDevUI();
}

// Root endpoint with agent list
app.MapGet("/", () => Results.Json(new
{
    message = "AskAI DevUI Server - Agent Framework AGUI Endpoints",
    version = "1.0.0",
    framework = "Microsoft Agent Framework",
    agents = new[]
    {
        new { name = "Contract Agent", endpoint = "/agents/contract", description = "契約関連の専門家" },
        new { name = "Spend Agent", endpoint = "/agents/spend", description = "支出分析の専門家" },
        new { name = "Negotiation Agent", endpoint = "/agents/negotiation", description = "交渉戦略の専門家" },
        new { name = "Sourcing Agent", endpoint = "/agents/sourcing", description = "調達戦略の専門家" },
        new { name = "Knowledge Agent", endpoint = "/agents/knowledge", description = "知識管理の専門家" },
        new { name = "Supplier Agent", endpoint = "/agents/supplier", description = "サプライヤー管理の専門家" },
        new { name = "Legal Agent", endpoint = "/agents/legal", description = "法務の専門家" },
        new { name = "Finance Agent", endpoint = "/agents/finance", description = "財務の専門家" },
        new { name = "Procurement Agent", endpoint = "/agents/procurement", description = "調達実務の専門家" },
        new { name = "Procurement Assistant", endpoint = "/agents/assistant", description = "調達・購買業務の総合アシスタント" }
    }
}));

var serverUrl = builder.Configuration["urls"] ?? "http://localhost:5000";
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("🚀 AskAI DevUI Server Started");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($"✓ Server URL: {serverUrl}");
Console.WriteLine($"✓ DevUI (Official): {serverUrl}/devui");
Console.WriteLine($"✓ Custom Web UI: {serverUrl}/ui/");
Console.WriteLine($"✓ Agents/Workflows available: 11");
Console.WriteLine($"✓ Agent List: GET /");
Console.WriteLine($"✓ AGUI Endpoints: /agents/*");
Console.WriteLine($"✓ OpenAI API: /v1/responses");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();
Console.WriteLine("💡 使用方法:");
Console.WriteLine($"   1. Microsoft DevUI: {serverUrl}/devui");
Console.WriteLine($"   2. Custom Web UI:   {serverUrl}/ui/");
Console.WriteLine($"   3. AGUI API:        {serverUrl}/agents/contract");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

await app.RunAsync();

// ===== Executor Classes =====

/// <summary>
/// 質問に対して詳細な専門家レビューを生成するExecutor
/// ChatProtocol対応: List<ChatMessage>を入力として受け取る
/// </summary>
public class SimpleReviewerExecutor : Executor<List<ChatMessage>, string>
{
    private readonly ChatClientAgent _agent;
    private readonly ILogger _logger;

    public SimpleReviewerExecutor(IChatClient chatClient, string id, ILogger logger) : base(id)
    {
        _logger = logger;
        var instructions = """
あなたは調達・契約の専門家です。
質問に対して詳細な分析と推奨事項を提供してください。
実用的で具体的な回答を心がけてください。
""";
        // 正しい引数順: chatClient, instructions, name, description
        _agent = new ChatClientAgent(
            chatClient,
            instructions: instructions,
            name: "Reviewer",
            description: "Procurement Expert");
    }

    public override async ValueTask<string> HandleAsync(
        List<ChatMessage> messages,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // 最後のユーザーメッセージから質問を取得
            var question = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "質問が見つかりません";
            _logger.LogInformation("🔍 ReviewerExecutor開始: {Question}", question);
            _logger.LogInformation("📞 Azure OpenAI呼び出し中...");

            var response = await _agent.RunAsync(messages, cancellationToken: cancellationToken);

            var detailedReview = response.Messages?.LastOrDefault()?.Text ?? "回答を生成できませんでした。";
            _logger.LogInformation("✅ ReviewerExecutor完了: {Length}文字", detailedReview.Length);

            // Yield intermediate output to workflow
            await context.YieldOutputAsync(detailedReview, cancellationToken);

            return detailedReview;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ ReviewerExecutor失敗: {Message}", ex.Message);
            throw;
        }
    }
}

/// <summary>
/// 専門家レビューを3つの要点に要約するExecutor
/// 入力: ReviewerExecutorからのstring
/// </summary>
public class SimpleSummarizerExecutor : Executor<string, string>
{
    private readonly ChatClientAgent _agent;
    private readonly ILogger _logger;

    public SimpleSummarizerExecutor(IChatClient chatClient, string id, ILogger logger) : base(id)
    {
        _logger = logger;
        var instructions = """
与えられたレビューテキストを3つの要点にまとめてください。
箇条書きで簡潔に出力してください。
""";
        // 正しい引数順: chatClient, instructions, name, description
        _agent = new ChatClientAgent(
            chatClient,
            instructions: instructions,
            name: "Summarizer",
            description: "Summary Expert");
    }

    public override async ValueTask<string> HandleAsync(
        string reviewText,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("📝 SummarizerExecutor開始: {Length}文字の入力", reviewText?.Length ?? 0);
            var messages = new[] { new ChatMessage(ChatRole.User, reviewText ?? "入力なし") };
            _logger.LogInformation("📞 Azure OpenAI呼び出し中...");

            var response = await _agent.RunAsync(messages, cancellationToken: cancellationToken);

            var summary = response.Messages?.LastOrDefault()?.Text ?? "要約を生成できませんでした。";
            _logger.LogInformation("✅ SummarizerExecutor完了: {Length}文字", summary.Length);

            // Yield final output to workflow
            await context.YieldOutputAsync(summary, cancellationToken);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ SummarizerExecutor失敗: {Message}", ex.Message);
            throw;
        }
    }
}
