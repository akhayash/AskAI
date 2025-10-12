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
using System.Text.Json;
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
    ?? "http://localhost:4317"; // Aspire Dashboard デフォルト

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService("SelectiveGroupChatWorkflow"));

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
var activitySource = new ActivitySource("SelectiveGroupChatWorkflow");

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("SelectiveGroupChatWorkflow"))
    .AddSource("SelectiveGroupChatWorkflow")
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

logger.LogInformation("=== Selective Group Chat Workflow デモ ===");
Console.WriteLine("質問を入力してください。");
Console.Write("質問> ");
var question = Console.ReadLine();

if (string.IsNullOrWhiteSpace(question))
{
    logger.LogWarning("質問が空です。");
    return;
}

logger.LogInformation("受信した質問: {Question}", question);

// ルーターエージェントを作成（専門家を選抜）
var routerAgent = CreateRouterAgent(extensionsAIChatClient);

// 専門家エージェントを作成
var specialists = new Dictionary<string, ChatClientAgent>
{
    ["Contract"] = CreateSpecialistAgent(extensionsAIChatClient, "Contract", "契約関連の専門家"),
    ["Spend"] = CreateSpecialistAgent(extensionsAIChatClient, "Spend", "支出分析の専門家"),
    ["Negotiation"] = CreateSpecialistAgent(extensionsAIChatClient, "Negotiation", "交渉戦略の専門家"),
    ["Sourcing"] = CreateSpecialistAgent(extensionsAIChatClient, "Sourcing", "調達戦略の専門家"),
    ["Knowledge"] = CreateSpecialistAgent(extensionsAIChatClient, "Knowledge", "知識管理の専門家"),
    ["Supplier"] = CreateSpecialistAgent(extensionsAIChatClient, "Supplier", "サプライヤー管理の専門家")
};

// モデレーターエージェントを作成（専門家の回答を統合）
var moderatorAgent = CreateModeratorAgent(extensionsAIChatClient);

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("フェーズ 1: ルーターが必要な専門家を選抜");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// ルーターに専門家を選抜させる
var routerMessages = new List<ChatMessage>
{
    new ChatMessage(ChatRole.User, $$"""
ユーザーの質問: {{question}}

上記の質問に回答するために必要な専門家を選抜してください。
専門家候補: Contract, Spend, Negotiation, Sourcing, Knowledge, Supplier

以下のJSON形式で回答してください:
{
  "selected": ["専門家名1", "専門家名2"],
  "reason": "選抜理由"
}

注意: 過剰選抜は避け、通常は2件以内で十分です。
""")
};

string routerResponse;
try
{
    using var routerCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

    // Create a simple workflow with just the router
    var routerWorkflow = AgentWorkflowBuilder
        .CreateHandoffBuilderWith(routerAgent)
        .Build();

    var routerWorkflowAgent = await routerWorkflow.AsAgentAsync("router", "Router");
    var routerThread = routerWorkflowAgent.GetNewThread();

    var responseBuilder = new StringBuilder();
    await foreach (var update in routerWorkflowAgent.RunStreamingAsync(routerMessages, routerThread, cancellationToken: routerCts.Token))
    {
        if (!string.IsNullOrEmpty(update.Text))
        {
            responseBuilder.Append(update.Text);
        }
    }

    routerResponse = responseBuilder.ToString();
    logger.LogInformation("[Router Agent の判断]\n{RouterResponse}", routerResponse);
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ ルーター実行エラー: {ErrorMessage}", ex.Message);
    return;
}

// JSONをパースして選抜された専門家を取得
List<string> selectedSpecialists;
string selectionReason;
try
{
    // JSON部分を抽出（マークダウンのコードブロックも考慮）
    var jsonText = routerResponse;
    if (jsonText.Contains("```json"))
    {
        var start = jsonText.IndexOf("```json") + 7;
        var end = jsonText.IndexOf("```", start);
        jsonText = jsonText.Substring(start, end - start).Trim();
    }
    else if (jsonText.Contains("```"))
    {
        var start = jsonText.IndexOf("```") + 3;
        var end = jsonText.IndexOf("```", start);
        jsonText = jsonText.Substring(start, end - start).Trim();
    }
    else if (jsonText.Contains("{"))
    {
        var start = jsonText.IndexOf("{");
        var end = jsonText.LastIndexOf("}") + 1;
        jsonText = jsonText.Substring(start, end - start).Trim();
    }

    var routerDecision = JsonSerializer.Deserialize<RouterDecision>(jsonText, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    selectedSpecialists = routerDecision?.Selected ?? new List<string>();
    selectionReason = routerDecision?.Reason ?? "理由不明";

    if (selectedSpecialists.Count == 0)
    {
        logger.LogWarning("⚠️ 専門家が選抜されませんでした。Knowledge 専門家をデフォルトで使用します。");
        selectedSpecialists = new List<string> { "Knowledge" };
        selectionReason = "フォールバック: デフォルト選抜";
    }
}
catch (Exception ex)
{
    logger.LogWarning(ex, "⚠️ ルーター応答のパースエラー: {ErrorMessage}", ex.Message);
    logger.LogInformation("Knowledge 専門家をデフォルトで使用します。");
    selectedSpecialists = new List<string> { "Knowledge" };
    selectionReason = "フォールバック: パースエラー";
}

logger.LogInformation("✓ 選抜された専門家: {SelectedSpecialists}", string.Join(", ", selectedSpecialists));
logger.LogInformation("✓ 選抜理由: {SelectionReason}", selectionReason);

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("フェーズ 2: 選抜された専門家が並列で意見を提供");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// 選抜された専門家からの応答を収集
var specialistResponses = new Dictionary<string, string>();
var specialistTasks = selectedSpecialists
    .Where(s => specialists.ContainsKey(s))
    .Select(async specialistName =>
    {
        var specialist = specialists[specialistName];
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, $$"""
ユーザーの質問: {{question}}

あなたは {{specialistName}} 専門家として、この質問に対する見解を提供してください。
2-3文程度の簡潔な意見で構いません。
""")
        };

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            // Create a simple workflow with just this specialist
            var specialistWorkflow = AgentWorkflowBuilder
                .CreateHandoffBuilderWith(specialist)
                .Build();

            var specialistWorkflowAgent = await specialistWorkflow.AsAgentAsync(specialistName, specialistName);
            var specialistThread = specialistWorkflowAgent.GetNewThread();

            var opinionBuilder = new StringBuilder();
            await foreach (var update in specialistWorkflowAgent.RunStreamingAsync(messages, specialistThread, cancellationToken: cts.Token))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    opinionBuilder.Append(update.Text);
                }
            }

            var opinion = opinionBuilder.ToString();
            logger.LogInformation("[{SpecialistName} Agent の意見]", specialistName);
            logger.LogInformation("{Opinion}", opinion);

            return (specialistName, opinion);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ {SpecialistName} エージェントのエラー: {ErrorMessage}", specialistName, ex.Message);
            return (specialistName, $"エラー: {ex.Message}");
        }
    });

var results = await Task.WhenAll(specialistTasks);
foreach (var result in results)
{
    specialistResponses[result.Item1] = result.Item2;
}

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("フェーズ 3: モデレーターが専門家の意見を統合");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// モデレーターに統合させる
var specialistSummary = string.Join("\n\n", specialistResponses.Select(kvp =>
    $"【{kvp.Key} 専門家の意見】\n{kvp.Value}"));

var specialistsSummaryList = string.Join("\n", selectedSpecialists.Select(s => $"- **{s}**: [要約]"));

var moderatorMessages = new List<ChatMessage>
{
    new ChatMessage(ChatRole.User, $$"""
ユーザーの質問: {{question}}

以下の専門家の意見を統合して、最終的な回答を生成してください:

{{specialistSummary}}

回答形式:
## 結論
[統合された結論]

## 根拠
[各専門家の意見を踏まえた根拠]

## 各専門家の所見
{{specialistsSummaryList}}

## 次のアクション
[推奨される次のステップ]
""")
};

try
{
    using var moderatorCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    logger.LogInformation("[Moderator Agent が統合中...]");

    // Create a simple workflow with just the moderator
    var moderatorWorkflow = AgentWorkflowBuilder
        .CreateHandoffBuilderWith(moderatorAgent)
        .Build();

    var moderatorWorkflowAgent = await moderatorWorkflow.AsAgentAsync("moderator", "Moderator");
    var moderatorThread = moderatorWorkflowAgent.GetNewThread();

    var finalResponse = new StringBuilder();
    await foreach (var update in moderatorWorkflowAgent.RunStreamingAsync(moderatorMessages, moderatorThread, cancellationToken: moderatorCts.Token))
    {
        if (!string.IsNullOrEmpty(update.Text))
        {
            Console.Write(update.Text);
            finalResponse.Append(update.Text);
        }
    }

    Console.WriteLine("\n");
    logger.LogInformation("最終回答: {FinalResponse}", finalResponse.ToString());
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ モデレーターエラー: {ErrorMessage}", ex.Message);
}

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("ワークフロー完了");
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
- 質問の内容を分析し、最も関連性の高い専門家を選抜してください。
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
あなたは {description} です。
専門知識を活用して簡潔に意見を述べてください（2-3文程度）。
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
あなたはモデレーターです。複数の専門家の意見を統合し、まとまった最終回答を生成します。
各専門家の所見を尊重しながら、一貫性のある結論を導き出してください。
回答は構造化され、次のアクションを含む実用的な内容にしてください。
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        "moderator_agent",
        "Moderator Agent");
}

// ルーター決定用のデータクラス
record RouterDecision(List<string> Selected, string Reason);
