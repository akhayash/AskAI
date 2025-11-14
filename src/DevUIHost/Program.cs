// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using Common;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
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

// Configuration
var endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

var deploymentName = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? "gpt-4o";

// Create chat client
var chatClient = new AzureOpenAIClient(
    new Uri(endpoint),
    new DefaultAzureCredential())
    .GetChatClient(deploymentName);

// Create and map specialist agents using the Common AgentFactory
var iChatClient = chatClient.AsIChatClient();

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

// Create a general purpose assistant
var assistantAgent = iChatClient.CreateAIAgent(
    name: "ProcurementAssistant",
    instructions: """
あなたは調達・購買業務の専門アシスタントです。
契約、支出分析、交渉、調達戦略、知識管理、サプライヤー管理に関する質問に答えます。
複雑な質問の場合は、専門家エージェントに相談することもできます。
"""
);

app.MapAGUI("/agents/assistant", assistantAgent);

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

Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("🚀 AskAI DevUI Server Started");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($"✓ Server URL: {builder.Configuration["urls"] ?? "http://localhost:5000"}");
Console.WriteLine($"✓ Agents available: 10");
Console.WriteLine($"✓ Agent List: GET /");
Console.WriteLine($"✓ AGUI Protocol: Microsoft Agent Framework");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

await app.RunAsync();
