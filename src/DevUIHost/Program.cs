// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using Common;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Agents.AI.Hosting.OpenAI;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

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

// Configuration
var endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

var deploymentName = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? "gpt-4o";

// Set up the Azure OpenAI client
var chatClient = new AzureOpenAIClient(
    new Uri(endpoint),
    new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();

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

// Register services for OpenAI responses and conversations (required for DevUI)
builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

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
Console.WriteLine($"✓ Agents available: 10");
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
