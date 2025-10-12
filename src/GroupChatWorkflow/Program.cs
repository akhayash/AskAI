using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using System.Text;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

// コンソールの文字エンコーディングを UTF-8 に設定
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// 設定を読み込む
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
    ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
    ?? "http://localhost:4317";

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService("GroupChatWorkflow"));
        
        options.AddOtlpExporter(exporterOptions =>
        {
            exporterOptions.Endpoint = new Uri(otlpEndpoint);
        });
        
        options.AddConsoleExporter();
    });
    
    builder.SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("=== アプリケーション起動 ===");
logger.LogInformation("テレメトリ設定: OTLP Endpoint = {OtlpEndpoint}", otlpEndpoint);
if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    logger.LogInformation("Application Insights 接続文字列が設定されています");
}

// 環境変数を設定から取得
var endpoint = configuration["environmentVariables:AZURE_OPENAI_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("環境変数 AZURE_OPENAI_ENDPOINT が設定されていません。");

var deployment = configuration["environmentVariables:AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("環境変数 AZURE_OPENAI_DEPLOYMENT_NAME が設定されていません。");

logger.LogInformation("エンドポイント: {Endpoint}", endpoint);
logger.LogInformation("デプロイメント名: {DeploymentName}", deployment);

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

var openAIClient = new AzureOpenAIClient(new Uri(endpoint), credential);
var chatClient = openAIClient.GetChatClient(deployment);
IChatClient extensionsAIChatClient = chatClient.AsIChatClient();

logger.LogInformation("=== Group Chat Workflow デモ ===");
Console.WriteLine("質問を入力してください。");
Console.Write("質問> ");
var question = Console.ReadLine();

if (string.IsNullOrWhiteSpace(question))
{
    logger.LogWarning("質問が空です。");
    return;
}

logger.LogInformation("受信した質問: {Question}", question);

// 専門家エージェントを作成
var contractAgent = CreateSpecialistAgent(extensionsAIChatClient, "Contract", "契約関連の専門家");
var spendAgent = CreateSpecialistAgent(extensionsAIChatClient, "Spend", "支出分析の専門家");
var negotiationAgent = CreateSpecialistAgent(extensionsAIChatClient, "Negotiation", "交渉戦略の専門家");
var sourcingAgent = CreateSpecialistAgent(extensionsAIChatClient, "Sourcing", "調達戦略の専門家");
var knowledgeAgent = CreateSpecialistAgent(extensionsAIChatClient, "Knowledge", "知識管理の専門家");
var supplierAgent = CreateSpecialistAgent(extensionsAIChatClient, "Supplier", "サプライヤー管理の専門家");

// GitHubサンプルに基づく正しい Group Chat 実装
// RoundRobinGroupChatManager を使用して、全員が順番に発言
var workflow = AgentWorkflowBuilder
    .CreateGroupChatBuilderWith(agents => new AgentWorkflowBuilder.RoundRobinGroupChatManager(agents) 
    { 
        MaximumIterationCount = 5  // 最大5ラウンドまで議論
    })
    .AddParticipants([contractAgent, spendAgent, negotiationAgent, sourcingAgent, knowledgeAgent, supplierAgent])
    .Build();

// ワークフローを実行
var messages = new List<ChatMessage>
{
    new ChatMessage(ChatRole.User, question)
};

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("Group Chat ワークフロー実行開始");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));

try
{
    var workflowAgent = await workflow.AsAgentAsync("group_chat", "Group Chat Workflow");
    var thread = workflowAgent.GetNewThread();

    var messageCount = 0;
    var currentAgentName = "";
    var currentMessage = new StringBuilder();
    
    await foreach (var update in workflowAgent.RunStreamingAsync(messages, thread, cancellationToken: cts.Token))
    {
        // エージェントが変わった場合
        if (!string.IsNullOrEmpty(update.AuthorName) && update.AuthorName != currentAgentName)
        {
            // 前のメッセージを出力
            if (currentMessage.Length > 0)
            {
                Console.WriteLine("¥n");
                currentMessage.Clear();
            }
            
            messageCount++;
            currentAgentName = update.AuthorName;
            
            // 新しいエージェントのヘッダー
            logger.LogInformation("[{MessageCount}] {AgentName} の発言", messageCount, currentAgentName);
            Console.WriteLine($"\n┌─ [{messageCount}] {currentAgentName} ─────────────────");
            Console.Write("│ ");
        }
        
        // テキストを蓄積して表示
        if (!string.IsNullOrWhiteSpace(update.Text))
        {
            Console.Write(update.Text);
            currentMessage.Append(update.Text);
        }
    }

    // 最後のメッセージの終了
    if (currentMessage.Length > 0)
    {
        Console.WriteLine("\n└──────────────────────────────────────");
        logger.LogInformation("最後のメッセージ: {Message}", currentMessage.ToString());
    }

    logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    logger.LogInformation("合計メッセージ数: {MessageCount}", messageCount);
    logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
}
catch (OperationCanceledException)
{
    logger.LogWarning("⚠️ タイムアウト: ワークフローが時間内に完了しませんでした。");
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ エラー: {ExceptionType}, メッセージ: {ErrorMessage}", 
        ex.GetType().Name, ex.Message);
    if (ex.InnerException != null)
    {
        logger.LogError("内部エラー: {InnerErrorMessage}", ex.InnerException.Message);
    }
}

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("ワークフロー実行完了");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

Console.WriteLine("\nEnter キーを押して終了してください...");
Console.ReadLine();

logger.LogInformation("=== アプリケーション終了 ===");

static ChatClientAgent CreateSpecialistAgent(IChatClient chatClient, string specialty, string description)
{
    var instructions = $"""
あなたは{description}として、グループチャットに参加しています。
他のエージェントの発言を読み、あなたの専門知識を活用して議論に貢献してください。

役割:
- 専門分野の視点から簡潔に意見を述べる（2-3文程度）
- 他のエージェントの意見を踏まえてコメントする
- 議論を前進させる質問や提案を行う
- 結論が出た場合は、次のエージェントにハンドオフする

重要:
- 簡潔に要点のみを述べてください
- 冗長な説明は避けてください
- 議論が進展しない場合は、適切なエージェントにハンドオフしてください
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        $"{specialty.ToLowerInvariant()}_agent",
        $"{specialty} Agent");
}
