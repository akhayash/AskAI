using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Reflection;
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
            .AddService("HandoffWorkflow"));

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
var activitySource = new ActivitySource("HandoffWorkflow");

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("HandoffWorkflow"))
    .AddSource("HandoffWorkflow")
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

// 環境変数を設定から取得（appsettings.json → 環境変数の順で優先）
var endpoint = configuration["environmentVariables:AZURE_OPENAI_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("環境変数 AZURE_OPENAI_ENDPOINT が設定されていません。");

var deployment = configuration["environmentVariables:AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("環境変数 AZURE_OPENAI_DEPLOYMENT_NAME が設定されていません。");

logger.LogInformation("エンドポイント: {Endpoint}", endpoint);
logger.LogInformation("デプロイメント名: {DeploymentName}", deployment);

logger.LogInformation("認証情報の取得中（Azure CLI を使用）...");
var credential = new AzureCliCredential();
logger.LogInformation("認証情報取得完了");

var openAIClient = new AzureOpenAIClient(new Uri(endpoint), credential);
var chatClient = openAIClient.GetChatClient(deployment);
IChatClient extensionsAIChatClient = chatClient.AsIChatClient();

logger.LogInformation("=== Router Workflow デモ ===");
Console.WriteLine("質問を入力してください。");
Console.Write("質問> ");
var question = Console.ReadLine();

if (string.IsNullOrWhiteSpace(question))
{
    logger.LogWarning("質問が空です。");
    return;
}

logger.LogInformation("受信した質問: {Question}", question);

// ルーターエージェントを作成
var routerAgent = CreateRouterAgent(extensionsAIChatClient);

// 専門家エージェントを作成（ダミー実装）
var contractAgent = CreateSpecialistAgent(extensionsAIChatClient, "Contract", "契約関連の専門家");
var spendAgent = CreateSpecialistAgent(extensionsAIChatClient, "Spend", "支出分析の専門家");
var negotiationAgent = CreateSpecialistAgent(extensionsAIChatClient, "Negotiation", "交渉戦略の専門家");
var sourcingAgent = CreateSpecialistAgent(extensionsAIChatClient, "Sourcing", "調達戦略の専門家");
var knowledgeAgent = CreateSpecialistAgent(extensionsAIChatClient, "Knowledge", "知識管理の専門家");
var supplierAgent = CreateSpecialistAgent(extensionsAIChatClient, "Supplier", "サプライヤー管理の専門家");

// ワークフローを構築（ルーター ⇔ 専門家グループ の双方向ハンドオフ）
var workflow = AgentWorkflowBuilder
    .CreateHandoffBuilderWith(routerAgent)
    .WithHandoffs(routerAgent, [contractAgent, spendAgent, negotiationAgent, sourcingAgent, knowledgeAgent, supplierAgent])
    .WithHandoffs([contractAgent, spendAgent, negotiationAgent, sourcingAgent, knowledgeAgent, supplierAgent], routerAgent)
    .Build();

// ワークフローを実行
var messages = new List<ChatMessage>
{
    new ChatMessage(ChatRole.User, question)
};

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("ワークフロー実行開始");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// タイムアウト設定（60秒に延長）
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

try
{
    using var workflowActivity = activitySource.StartActivity("HandoffWorkflow", ActivityKind.Internal);
    workflowActivity?.SetTag("question", question);

    logger.LogInformation("ワークフローをエージェントに変換中...");
    var workflowAgent = await workflow.AsAgentAsync("workflow", "Routing Workflow");
    logger.LogInformation("エージェント変換完了");

    var thread = workflowAgent.GetNewThread();
    logger.LogInformation("スレッド作成完了");

    logger.LogInformation("メッセージ数: {MessageCount}", messages.Count);
    logger.LogInformation("メッセージ内容: {MessageText}", messages[0].Text);
    logger.LogInformation("ストリーミング開始...");

    var updateCount = 0;
    var messageCount = 0; // 完了したメッセージ数
    var maxMessages = 20; // 最大メッセージ数
    var currentAgentId = "";
    var currentAgentName = "";
    var currentMessage = new System.Text.StringBuilder();
    Activity? agentActivity = null;

    await foreach (var update in workflowAgent.RunStreamingAsync(messages, thread, cancellationToken: cts.Token))
    {
        updateCount++;

        // エージェントが変わった場合、新しいメッセージとしてカウント
        if (!string.IsNullOrEmpty(update.AgentId) && update.AgentId != currentAgentId)
        {
            // 前のエージェントの Span を終了
            if (agentActivity != null)
            {
                agentActivity.SetTag("message.length", currentMessage.Length);
                agentActivity.SetTag("message.content", currentMessage.ToString());
                logger.LogInformation("【完了】エージェント: {AgentName} ({AgentId}), 内容: {Content}",
                    currentAgentName, currentAgentId, currentMessage.ToString());
                agentActivity.Dispose();
            }

            // 前のメッセージを出力
            if (currentMessage.Length > 0)
            {
                Console.WriteLine("\n");
                currentMessage.Clear();
            }

            messageCount++;
            currentAgentId = update.AgentId;
            currentAgentName = update.AuthorName ?? "不明";

            // 新しいエージェントの Span を開始
            agentActivity = activitySource.StartActivity($"Agent.{currentAgentName}", ActivityKind.Internal);
            agentActivity?.SetTag("agent.name", currentAgentName);
            agentActivity?.SetTag("agent.id", currentAgentId);
            agentActivity?.SetTag("message.count", messageCount);
            agentActivity?.SetTag("role", update.Role?.ToString() ?? "不明");

            logger.LogInformation("【開始】[#{MessageCount}] エージェント名: {AgentName}, エージェントID: {AgentId}, ロール: {Role}",
                messageCount, currentAgentName, update.AgentId, update.Role?.ToString() ?? "不明");

            Console.WriteLine($"\n┌─ [{messageCount}] {currentAgentName} ({update.Role?.ToString() ?? "不明"}) ─────────────────");
            Console.Write("│ ");

            // 最大メッセージ数チェック
            if (messageCount > maxMessages)
            {
                logger.LogWarning("⚠️ 最大メッセージ数 ({MaxMessages}) に達しました。", maxMessages);
                break;
            }
        }

        // テキストを蓄積
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
        logger.LogInformation("【完了】エージェント: {AgentName} ({AgentId}), 内容: {Content}",
            currentAgentName, currentAgentId, currentMessage.ToString());
        agentActivity.Dispose();
    }

    // 最後のメッセージの終了
    if (currentMessage.Length > 0)
    {
        Console.WriteLine("\n└──────────────────────────────────────");
    }

    workflowActivity?.SetTag("total.messages", messageCount);
    workflowActivity?.SetTag("total.updates", updateCount);

    logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    logger.LogInformation("合計更新数: {UpdateCount}, メッセージ数: {MessageCount}", updateCount, messageCount);
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

static ChatClientAgent CreateRouterAgent(IChatClient chatClient)
{
    var instructions = """
あなたは調達領域のルーターです。ユーザー質問を読み、必要な専門家だけを選びます。
専門家候補: Contract, Spend, Negotiation, Sourcing, Knowledge, Supplier
評価:
- 過剰選抜は避け、通常は 2 件以内で十分です。
- 交渉や社外送信が想定される場合は HITL が必要です。
- 適切な専門家にハンドオフしてください。ALWAYS handoff to another agent.
- 専門家からの回答を受け取った場合は、それを統合して最終回答を提供してください。
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        "router_agent",
        "Router Agent");
}

static ChatClientAgent CreateSpecialistAgent(IChatClient chatClient, string specialty, string description)
{
    var instructions = $"""
あなたは {description} として回答します。
専門知識を活用してユーザーの質問に答えてください。
回答が完了したら、Router Agent にハンドオフして結果を報告してください。
他の専門家の意見が必要な場合は、Router Agent にハンドオフして依頼してください。
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        $"{specialty.ToLowerInvariant()}_agent",
        $"{specialty} Agent");
}
