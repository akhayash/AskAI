using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
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
            .AddService("DynamicGroupChatWorkflow"));
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

var activitySource = new ActivitySource("DynamicGroupChatWorkflow");

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("DynamicGroupChatWorkflow"))
    .AddSource("DynamicGroupChatWorkflow")
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

logger.LogInformation("=== Dynamic Group Chat Workflow デモ ===");
// Keep user-facing prompt only; logger will output structured logs (also to console via exporter)
Console.WriteLine("Router が動的に専門家を選抜し、必要に応じてユーザーに意見を求めます。");
Console.WriteLine("質問を入力してください。");
Console.Write("質問> ");
var question = Console.ReadLine();

if (string.IsNullOrWhiteSpace(question))
{
    logger.LogWarning("質問が空です。");
    Console.WriteLine("質問が空です。");
    return;
}

logger.LogInformation("受信した質問: {Question}", question);

// エージェント作成
logger.LogInformation("エージェントを作成中...");
var routerAgent = CreateRouterAgent(extensionsAIChatClient);
var contractAgent = CreateSpecialistAgent(extensionsAIChatClient, "Contract", "契約関連の専門家");
var spendAgent = CreateSpecialistAgent(extensionsAIChatClient, "Spend", "支出分析の専門家");
var negotiationAgent = CreateSpecialistAgent(extensionsAIChatClient, "Negotiation", "交渉戦略の専門家");
var sourcingAgent = CreateSpecialistAgent(extensionsAIChatClient, "Sourcing", "調達戦略の専門家");
var knowledgeAgent = CreateSpecialistAgent(extensionsAIChatClient, "Knowledge", "知識管理の専門家");
var supplierAgent = CreateSpecialistAgent(extensionsAIChatClient, "Supplier", "サプライヤー管理の専門家");
var moderatorAgent = CreateModeratorAgent(extensionsAIChatClient);
logger.LogInformation("エージェント作成完了");

// ワークフロー構築
logger.LogInformation("ワークフローを構築中...");
var workflow = AgentWorkflowBuilder
    .CreateHandoffBuilderWith(routerAgent)
    .WithHandoffs(routerAgent, [contractAgent, spendAgent, negotiationAgent,
                                  sourcingAgent, knowledgeAgent, supplierAgent,
                                  moderatorAgent])
    .WithHandoffs([contractAgent, spendAgent, negotiationAgent,
                   sourcingAgent, knowledgeAgent, supplierAgent], routerAgent)
    .Build();
logger.LogInformation("ワークフロー構築完了");

// ワークフロー実行
var messages = new List<ChatMessage>
{
    new ChatMessage(ChatRole.User, question)
};

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("ワークフロー実行開始");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300));

try
{
    using var workflowActivity = activitySource.StartActivity("Workflow: Dynamic Group Chat", ActivityKind.Internal);
    workflowActivity?.SetTag("initial.question", question);

    logger.LogInformation("ワークフローをエージェントに変換中...");
    var workflowAgent = await workflow.AsAgentAsync("workflow", "Dynamic Workflow");
    logger.LogInformation("エージェント変換完了");
    workflowActivity?.SetTag("workflow.agent.id", workflowAgent.Id);

    var thread = workflowAgent.GetNewThread();
    logger.LogInformation("スレッド作成完了");

    logger.LogInformation("メッセージ数: {MessageCount}", messages.Count);
    logger.LogInformation("メッセージ内容: {MessageText}", messages[0].Text);
    logger.LogInformation("ストリーミング開始...");

    var currentAgent = "";
    var messageCount = 0;
    var updateCount = 0;
    var maxMessages = 30;
    var currentMessage = new StringBuilder();
    var pendingQuestion = new StringBuilder();
    var waitingForUserInput = false;
    Activity? agentActivity = null;

    await foreach (var update in workflowAgent.RunStreamingAsync(messages, thread, cancellationToken: cts.Token))
    {
        updateCount++;
        // エージェントが変わったら表示
        if (!string.IsNullOrEmpty(update.AgentId) && update.AgentId != currentAgent)
        {
            // ユーザー入力待ちチェック
            if (waitingForUserInput)
            {
                logger.LogInformation("[ユーザー入力を待機] 質問: {Question}", pendingQuestion.ToString());
                if (agentActivity != null)
                {
                    agentActivity.SetTag("message.length", currentMessage.Length);
                    agentActivity.SetTag("message.content.preview", TruncateForTelemetry(currentMessage.ToString()));
                    agentActivity.SetTag("status", "waiting-for-user");
                    agentActivity.Dispose();
                    agentActivity = null;
                }
                Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine("?? ユーザー入力が必要です");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.Write("\n回答> ");

                var userResponse = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(userResponse))
                {
                    messages.Add(new ChatMessage(ChatRole.User, userResponse));
                    logger.LogInformation("[ユーザー入力受信] 回答: {Response}", userResponse);
                    workflowActivity?.AddEvent(new ActivityEvent("user-input-received", tags: new ActivityTagsCollection
                    {
                        { "user.input.length", userResponse.Length }
                    }));

                    // ワークフローを再実行
                    thread = workflowAgent.GetNewThread();
                    await foreach (var newUpdate in workflowAgent.RunStreamingAsync(messages, thread, cancellationToken: cts.Token))
                    {
                        if (!string.IsNullOrEmpty(newUpdate.Text))
                        {
                            Console.Write(newUpdate.Text);
                        }
                    }
                    waitingForUserInput = false;
                    break;
                }

                waitingForUserInput = false;
                pendingQuestion.Clear();
            }

            // 前のメッセージを出力
            if (currentMessage.Length > 0)
            {
                var messageText = currentMessage.ToString();
                var messagePreview = TruncateForTelemetry(messageText);
                logger.LogInformation("[メッセージ完了 #{MessageCount}] エージェント: {AgentId}, 内容長: {ContentLength}, 内容: {Content}",
                    messageCount, currentAgent, currentMessage.Length, messageText);
                if (agentActivity != null)
                {
                    agentActivity.SetTag("message.length", currentMessage.Length);
                    agentActivity.SetTag("message.content.preview", messagePreview);
                    agentActivity.Dispose();
                    agentActivity = null;
                }
                currentMessage.Clear();
                pendingQuestion.Clear();
            }

            currentAgent = update.AgentId;
            messageCount++;

            logger.LogInformation("[メッセージ開始 #{MessageCount}] エージェント名: {AgentName}, エージェントID: {AgentId}, ロール: {Role}",
                messageCount, update.AuthorName ?? "不明", update.AgentId, update.Role?.ToString() ?? "不明");
            var agentDisplayName = update.AuthorName ?? $"Agent {messageCount}";
            agentActivity = activitySource.StartActivity($"Agent Turn: {agentDisplayName} (#{messageCount})", ActivityKind.Internal);
            agentActivity?.SetTag("agent.name", update.AuthorName ?? "不明");
            agentActivity?.SetTag("agent.id", update.AgentId);
            agentActivity?.SetTag("agent.role", update.Role?.ToString() ?? "不明");
            agentActivity?.SetTag("message.ordinal", messageCount);

            if (messageCount > maxMessages)
            {
                logger.LogWarning("?? 最大メッセージ数 ({MaxMessages}) に達しました。", maxMessages);
                break;
            }

            Console.WriteLine($"\n━━━ {update.AuthorName ?? currentAgent} ━━━");
        }

        // テキストを表示
        if (!string.IsNullOrEmpty(update.Text))
        {
            Console.Write(update.Text);
            currentMessage.Append(update.Text);
            pendingQuestion.Append(update.Text);
            agentActivity?.AddEvent(new ActivityEvent("stream-token", tags: new ActivityTagsCollection
            {
                { "token.length", update.Text.Length }
            }));

            // Router からの質問を検出（「？」で終わる場合）
            if (currentAgent == "router_agent" && update.Text.Contains("？"))
            {
                waitingForUserInput = true;
            }
        }
    }

    // 最後のメッセージを出力
    if (currentMessage.Length > 0)
    {
        var messageText = currentMessage.ToString();
        var messagePreview = TruncateForTelemetry(messageText);
        logger.LogInformation("[メッセージ完了 #{MessageCount}] エージェント: {AgentId}, 完全な内容長: {ContentLength}, 内容: {Content}",
            messageCount, currentAgent, currentMessage.Length, messageText);
        if (agentActivity != null)
        {
            agentActivity.SetTag("message.length", currentMessage.Length);
            agentActivity.SetTag("message.content.preview", messagePreview);
            agentActivity.Dispose();
        }
    }

    workflowActivity?.SetTag("total.messages", messageCount);
    workflowActivity?.SetTag("total.updates", updateCount);
    logger.LogInformation("ストリーミング完了。メッセージ数: {MessageCount}", messageCount);
    workflowActivity?.Stop();
}
catch (OperationCanceledException)
{
    logger.LogWarning("?? タイムアウト: ワークフローが時間内に完了しませんでした。");
}
catch (Exception ex)
{
    logger.LogError(ex, "? エラー: {ExceptionType}, メッセージ: {ErrorMessage}",
        ex.GetType().Name, ex.Message);
    if (ex.InnerException != null)
    {
        logger.LogError("内部エラー: {InnerErrorMessage}", ex.InnerException.Message);
    }
}

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("ワークフロー実行完了");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

Console.WriteLine("Enter キーを押して終了してください...");
Console.ReadLine();

logger.LogInformation("=== アプリケーション終了 ===");

static string TruncateForTelemetry(string content, int maxLength = 512)
{
    if (string.IsNullOrEmpty(content))
    {
        return string.Empty;
    }

    if (content.Length <= maxLength)
    {
        return content;
    }

    return content[..maxLength] + "...";
}

static ChatClientAgent CreateRouterAgent(IChatClient chatClient)
{
    var instructions = """
あなたは調達領域のルーターです。

役割:
1. ユーザー質問を分析し、必要な専門家を動的に選抜
2. 専門家にハンドオフして意見を収集
3. 専門家の意見を踏まえ、さらに情報が必要か判断:
   - 他の専門家の意見が必要 → その専門家にハンドオフ
   - ユーザーの追加情報が必要 → ユーザーに質問してください（「？」で終わる質問文）
4. 十分な情報が集まったら、Moderator Agent にハンドオフ

利用可能なエージェント:
- Contract Agent (契約関連)
- Spend Agent (支出分析)
- Negotiation Agent (交渉戦略)
- Sourcing Agent (調達戦略)
- Knowledge Agent (知識管理)
- Supplier Agent (サプライヤー管理)
- Moderator Agent (最終統合) ← 十分な情報が集まった場合のみ

Human-in-the-Loop のガイドライン:
- 専門家の意見を聞いた結果、ユーザーの追加情報（予算、期限、優先事項など）が必要な場合:
  質問内容を明確に記述してください（例: "予算の上限を教えてください。" "希望する契約期間は何年ですか？"）
  質問は「？」で終わるようにしてください。
- ユーザーからの回答を受け取ったら、それを踏まえて次のアクションを決定

重要:
- 必ず適切なエージェントにハンドオフしてください
- 過剰なハンドオフは避けてください（通常は2-3名の専門家で十分）
- ユーザーへの質問は本当に必要な場合のみ（1回まで推奨）
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

役割:
- 専門知識を活用してユーザーの質問に答える
- 会話履歴に他の専門家の意見やユーザーの追加情報が含まれている場合、それらを参考にする
- 回答が完了したら、必ず Router Agent にハンドオフして結果を報告する

回答のガイドライン:
- 簡潔かつ具体的に回答（2-3文程度）
- 必要に応じて、他の専門家の意見への言及も可能
- 不明な点があれば、Router Agent に追加情報の必要性を伝える
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        $"{specialty.ToLowerInvariant()}_agent",
        $"{specialty} Agent");
}

static ChatClientAgent CreateModeratorAgent(IChatClient chatClient)
{
    var instructions = """
あなたはモデレーターです。

役割:
Router から渡された会話履歴を読み、複数の専門家の意見とユーザーの追加情報を統合して最終回答を生成します。

要求事項:
- 各専門家の所見を尊重しながら、一貫性のある結論を導く
- ユーザーが提供した追加情報を適切に反映する
- 回答は以下の形式で構造化:

## 結論
[統合された結論]

## 根拠
[各専門家の意見とユーザー入力を踏まえた根拠]

## 各専門家の所見
- Contract: [要約]
- Negotiation: [要約]
...

## ユーザーからの追加情報
[ユーザーが提供した情報の要約（該当する場合）]

## 次のアクション
[推奨される次のステップ]
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        "moderator_agent",
        "Moderator Agent");
}
