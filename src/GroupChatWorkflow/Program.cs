using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
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

        // メッセージテンプレートを展開して送信（読みやすくするため）
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;

        options.AddOtlpExporter(exporterOptions =>
        {
            exporterOptions.Endpoint = new Uri(otlpEndpoint);
        });

        // コンソールにも構造化ログを出力（値を展開）
        options.AddConsoleExporter(consoleOptions =>
        {
            consoleOptions.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
        });
    });

    // SimpleConsoleFormatter を使用して、構造化ログを読みやすく表示
    builder.AddSimpleConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    });

    builder.SetMinimumLevel(LogLevel.Information);
});

// OpenTelemetry Tracing を設定
var activitySource = new ActivitySource("GroupChatWorkflow");

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("GroupChatWorkflow"))
    .AddSource("GroupChatWorkflow")
    .AddHttpClientInstrumentation()
    .AddOtlpExporter(exporterOptions =>
    {
        exporterOptions.Endpoint = new Uri(otlpEndpoint);
    })
    .AddConsoleExporter()
    .Build();

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
    using var workflowActivity = activitySource.StartActivity("GroupChatWorkflow", ActivityKind.Internal);
    workflowActivity?.SetTag("question", question);

    var workflowAgent = await workflow.AsAgentAsync("group_chat", "Group Chat Workflow");
    var thread = workflowAgent.GetNewThread();

    var messageCount = 0;
    var currentAgentName = "";
    var currentMessage = new StringBuilder();
    Activity? agentActivity = null;

    await foreach (var update in workflowAgent.RunStreamingAsync(messages, thread, cancellationToken: cts.Token))
    {
        // エージェントが変わった場合
        if (!string.IsNullOrEmpty(update.AuthorName) && update.AuthorName != currentAgentName)
        {
            // 前のエージェントの Span を終了
            if (agentActivity != null)
            {
                agentActivity.SetTag("message.length", currentMessage.Length);
                agentActivity.SetTag("message.content", currentMessage.ToString());
                logger.LogInformation("【完了】{AgentName}: {Message}", currentAgentName, currentMessage.ToString());
                agentActivity.Dispose();
            }

            // 前のメッセージを出力
            if (currentMessage.Length > 0)
            {
                Console.WriteLine("\n");
                currentMessage.Clear();
            }

            messageCount++;
            currentAgentName = update.AuthorName;

            // 新しいエージェントの Span を開始
            agentActivity = activitySource.StartActivity($"Agent.{currentAgentName}", ActivityKind.Internal);
            agentActivity?.SetTag("agent.name", currentAgentName);
            agentActivity?.SetTag("message.count", messageCount);

            // 新しいエージェントのヘッダー
            logger.LogInformation("【開始】[{MessageCount}] {AgentName} の発言", messageCount, currentAgentName);
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

    // 最後のエージェントの Span を終了
    if (agentActivity != null)
    {
        agentActivity.SetTag("message.length", currentMessage.Length);
        agentActivity.SetTag("message.content", currentMessage.ToString());
        logger.LogInformation("【完了】{AgentName}: {Message}", currentAgentName, currentMessage.ToString());
        agentActivity.Dispose();
    }

    // 最後のメッセージの終了
    if (currentMessage.Length > 0)
    {
        Console.WriteLine("\n└──────────────────────────────────────");
    }

    workflowActivity?.SetTag("total.messages", messageCount);
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
