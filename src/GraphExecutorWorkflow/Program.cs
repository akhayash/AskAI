using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
    ?? "http://localhost:4317";

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

        options.AddConsoleExporter(consoleOptions =>
        {
            consoleOptions.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
        });
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

var logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("=== Graph Executor Workflow デモ ===");

// 環境変数を設定から取得
var endpoint = configuration["environmentVariables:AZURE_OPENAI_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("環境変数 AZURE_OPENAI_ENDPOINT が設定されていません。");

var deployment = configuration["environmentVariables:AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("環境変数 AZURE_OPENAI_DEPLOYMENT_NAME が設定されていません。");

logger.LogInformation("エンドポイント: {Endpoint}", endpoint);
logger.LogInformation("デプロイメント名: {DeploymentName}", deployment);

var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    ExcludeEnvironmentCredential = true,
    ExcludeManagedIdentityCredential = true,
    ExcludeSharedTokenCacheCredential = true,
    ExcludeVisualStudioCredential = true,
    ExcludeVisualStudioCodeCredential = true,
    ExcludeAzureCliCredential = false,
    ExcludeAzurePowerShellCredential = true,
    ExcludeAzureDeveloperCliCredential = true,
    ExcludeInteractiveBrowserCredential = true,
    ExcludeWorkloadIdentityCredential = true
});

var openAIClient = new AzureOpenAIClient(new Uri(endpoint), credential);
var chatClient = openAIClient.GetChatClient(deployment);
IChatClient extensionsAIChatClient = chatClient.AsIChatClient();

// ユーザー入力
Console.WriteLine("質問を入力してください。");
Console.Write("質問> ");
var question = Console.ReadLine();

if (string.IsNullOrWhiteSpace(question))
{
    logger.LogWarning("質問が空です。");
    return;
}

logger.LogInformation("受信した質問: {Question}", question);

// ==========================================
// グラフベースのワークフロー実装
// ==========================================
// 
// フロー:
// 1. Router Executor: ユーザー質問から必要な専門家を特定
// 2. Specialist Executors: 各専門家が並列で意見を生成
// 3. Aggregator Executor: 意見を集約し最終出力
//
// エッジ定義:
// - Router → Specialists (動的分岐)
// - Specialists → Aggregator (結合)
// ==========================================

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("ステップ 1: Router Executor - 専門家を特定");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// Router Executor: 専門家を選抜
var routerAgent = CreateRouterExecutor(extensionsAIChatClient);
var selectedSpecialists = await ExecuteRouterAsync(routerAgent, question, logger, activitySource);

logger.LogInformation("✓ 選抜された専門家: {Specialists}", string.Join(", ", selectedSpecialists));

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("ステップ 2: Specialist Executors - 意見を生成");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// Specialist Executors: 選抜された専門家が並列実行
var specialistExecutors = CreateSpecialistExecutors(extensionsAIChatClient);
var opinions = await ExecuteSpecialistsAsync(specialistExecutors, selectedSpecialists, question, logger, activitySource);

foreach (var (specialist, opinion) in opinions)
{
    logger.LogInformation("[{Specialist}] {Opinion}", specialist, opinion.Substring(0, Math.Min(100, opinion.Length)) + "...");
}

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("ステップ 3: Aggregator Executor - 意見を集約");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// Aggregator Executor: 意見を統合
var aggregatorAgent = CreateAggregatorExecutor(extensionsAIChatClient);
var finalOutput = await ExecuteAggregatorAsync(aggregatorAgent, question, opinions, logger, activitySource);

Console.WriteLine("\n" + new string('═', 60));
Console.WriteLine("【最終回答】");
Console.WriteLine(new string('═', 60));
Console.WriteLine(finalOutput);
Console.WriteLine(new string('═', 60) + "\n");

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("ワークフロー完了");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

Console.WriteLine("\nEnter キーを押して終了してください...");
Console.ReadLine();

// ==========================================
// Executor 定義
// ==========================================

static ChatClientAgent CreateRouterExecutor(IChatClient chatClient)
{
    var instructions = """
あなたは Router Executor です。
ユーザーの質問を分析し、必要な専門家を選抜します。

専門家候補:
- Contract: 契約関連の専門家
- Spend: 支出分析の専門家
- Negotiation: 交渉戦略の専門家
- Sourcing: 調達戦略の専門家
- Knowledge: 知識管理の専門家
- Supplier: サプライヤー管理の専門家

以下のJSON形式で回答してください:
{
  "selected": ["専門家名1", "専門家名2"],
  "reason": "選抜理由"
}

注意: 過剰選抜は避け、通常は2-3件以内で十分です。
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        "router_executor",
        "Router Executor");
}

static Dictionary<string, ChatClientAgent> CreateSpecialistExecutors(IChatClient chatClient)
{
    var specialists = new Dictionary<string, ChatClientAgent>();
    
    var specialistDefinitions = new Dictionary<string, string>
    {
        ["Contract"] = "契約条件、リスク条項、法的観点から専門的な意見を提供します。",
        ["Spend"] = "支出分析、コスト削減、予算管理の観点から専門的な意見を提供します。",
        ["Negotiation"] = "交渉戦略、条件交渉、合意形成の観点から専門的な意見を提供します。",
        ["Sourcing"] = "調達戦略、サプライヤー選定、調達プロセスの観点から専門的な意見を提供します。",
        ["Knowledge"] = "業界知識、ベストプラクティス、一般的な情報の観点から専門的な意見を提供します。",
        ["Supplier"] = "サプライヤー管理、関係構築、パフォーマンス評価の観点から専門的な意見を提供します。"
    };

    foreach (var (name, description) in specialistDefinitions)
    {
        var instructions = $"""
あなたは {name} Specialist Executor です。
{description}

質問に対して、あなたの専門領域から見た重要なポイントを2-3文で簡潔に述べてください。
""";

        specialists[name] = new ChatClientAgent(
            chatClient,
            instructions,
            $"{name.ToLower()}_executor",
            $"{name} Executor");
    }

    return specialists;
}

static ChatClientAgent CreateAggregatorExecutor(IChatClient chatClient)
{
    var instructions = """
あなたは Aggregator Executor です。
複数の専門家の意見を統合し、構造化された最終回答を生成します。

以下の形式で回答を生成してください:

## 結論
[各専門家の意見を統合した結論を3-4文で記述]

## 根拠
[結論に至った主要な根拠を箇条書きで記述]
- 根拠1
- 根拠2
- 根拠3

## 各専門家の所見
[各専門家の意見を要約]

## 推奨アクション
[具体的な次のステップを箇条書きで記述]
- アクション1
- アクション2
- アクション3
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        "aggregator_executor",
        "Aggregator Executor");
}

// ==========================================
// Executor 実行関数（エッジ実装）
// ==========================================

static async Task<List<string>> ExecuteRouterAsync(
    ChatClientAgent routerAgent,
    string question,
    ILogger logger,
    ActivitySource activitySource)
{
    using var activity = activitySource.StartActivity("RouterExecutor", ActivityKind.Internal);
    activity?.SetTag("executor.type", "router");
    activity?.SetTag("question", question);

    var messages = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.User, $"質問: {question}")
    };

    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        
        var workflow = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(routerAgent)
            .Build();

        var workflowAgent = await workflow.AsAgentAsync("router", "Router");
        var thread = workflowAgent.GetNewThread();

        var responseBuilder = new StringBuilder();
        await foreach (var update in workflowAgent.RunStreamingAsync(messages, thread, cancellationToken: cts.Token))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                responseBuilder.Append(update.Text);
            }
        }

        var response = responseBuilder.ToString();
        logger.LogInformation("Router Response: {Response}", response);

        // JSONをパース
        var jsonText = ExtractJson(response);
        var routerDecision = JsonSerializer.Deserialize<RouterDecision>(jsonText, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var selected = routerDecision?.Selected ?? new List<string>();
        if (selected.Count == 0)
        {
            logger.LogWarning("専門家が選抜されませんでした。Knowledge をデフォルト使用します。");
            selected = new List<string> { "Knowledge" };
        }

        activity?.SetTag("selected.count", selected.Count);
        activity?.SetTag("selected.specialists", string.Join(", ", selected));

        return selected;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Router Executor エラー: {Message}", ex.Message);
        activity?.SetTag("error", true);
        activity?.SetTag("error.message", ex.Message);
        return new List<string> { "Knowledge" };
    }
}

static async Task<Dictionary<string, string>> ExecuteSpecialistsAsync(
    Dictionary<string, ChatClientAgent> specialists,
    List<string> selectedSpecialists,
    string question,
    ILogger logger,
    ActivitySource activitySource)
{
    using var activity = activitySource.StartActivity("SpecialistExecutors", ActivityKind.Internal);
    activity?.SetTag("executor.type", "specialists");
    activity?.SetTag("specialists.count", selectedSpecialists.Count);

    var tasks = selectedSpecialists
        .Where(name => specialists.ContainsKey(name))
        .Select(async name =>
        {
            using var specialistActivity = activitySource.StartActivity($"SpecialistExecutor.{name}", ActivityKind.Internal);
            specialistActivity?.SetTag("specialist.name", name);

            var agent = specialists[name];
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.User, $"質問: {question}\n\nあなたの専門分野から見た意見を述べてください。")
            };

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                
                var workflow = AgentWorkflowBuilder
                    .CreateHandoffBuilderWith(agent)
                    .Build();

                var workflowAgent = await workflow.AsAgentAsync(name, name);
                var thread = workflowAgent.GetNewThread();

                var opinionBuilder = new StringBuilder();
                await foreach (var update in workflowAgent.RunStreamingAsync(messages, thread, cancellationToken: cts.Token))
                {
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        opinionBuilder.Append(update.Text);
                    }
                }

                var opinion = opinionBuilder.ToString();
                specialistActivity?.SetTag("opinion.length", opinion.Length);
                logger.LogInformation("{Specialist} Executor 完了", name);

                return (name, opinion);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Specialist} Executor エラー: {Message}", name, ex.Message);
                specialistActivity?.SetTag("error", true);
                return (name, $"[エラー: {ex.Message}]");
            }
        });

    var results = await Task.WhenAll(tasks);
    return results.ToDictionary(r => r.Item1, r => r.Item2);
}

static async Task<string> ExecuteAggregatorAsync(
    ChatClientAgent aggregatorAgent,
    string question,
    Dictionary<string, string> opinions,
    ILogger logger,
    ActivitySource activitySource)
{
    using var activity = activitySource.StartActivity("AggregatorExecutor", ActivityKind.Internal);
    activity?.SetTag("executor.type", "aggregator");
    activity?.SetTag("opinions.count", opinions.Count);

    var opinionsSummary = string.Join("\n\n", opinions.Select(kvp =>
        $"【{kvp.Key} の意見】\n{kvp.Value}"));

    var messages = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.User, $"""
質問: {question}

以下は各専門家の意見です:

{opinionsSummary}

これらの意見を統合し、構造化された最終回答を生成してください。
""")
    };

    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        
        var workflow = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(aggregatorAgent)
            .Build();

        var workflowAgent = await workflow.AsAgentAsync("aggregator", "Aggregator");
        var thread = workflowAgent.GetNewThread();

        var finalOutputBuilder = new StringBuilder();
        await foreach (var update in workflowAgent.RunStreamingAsync(messages, thread, cancellationToken: cts.Token))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                finalOutputBuilder.Append(update.Text);
            }
        }

        var finalOutput = finalOutputBuilder.ToString();
        activity?.SetTag("output.length", finalOutput.Length);
        logger.LogInformation("Aggregator Executor 完了");

        return finalOutput;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Aggregator Executor エラー: {Message}", ex.Message);
        activity?.SetTag("error", true);
        return $"エラーが発生しました: {ex.Message}";
    }
}

// ==========================================
// ヘルパー関数
// ==========================================

static string ExtractJson(string text)
{
    if (text.Contains("```json"))
    {
        var start = text.IndexOf("```json") + 7;
        var end = text.IndexOf("```", start);
        return text.Substring(start, end - start).Trim();
    }
    else if (text.Contains("```"))
    {
        var start = text.IndexOf("```") + 3;
        var end = text.IndexOf("```", start);
        return text.Substring(start, end - start).Trim();
    }
    else if (text.Contains("{"))
    {
        var start = text.IndexOf("{");
        var end = text.LastIndexOf("}") + 1;
        return text.Substring(start, end - start).Trim();
    }
    return text;
}

record RouterDecision(List<string> Selected, string Reason);
