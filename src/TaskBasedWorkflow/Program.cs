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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            .AddService("TaskBasedWorkflow"));

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
var activitySource = new ActivitySource("TaskBasedWorkflow");

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("TaskBasedWorkflow"))
    .AddSource("Microsoft.Agents.AI.Workflows*")  // Agent Framework 内部ログ
    .AddSource("TaskBasedWorkflow")
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
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set");

var deploymentName = configuration["environmentVariables:AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not set");

logger.LogInformation("エンドポイント: {Endpoint}", endpoint);
logger.LogInformation("デプロイメント名: {DeploymentName}", deploymentName);

logger.LogInformation("認証情報の取得中...");
var credential = new AzureCliCredential();
var openAIClient = new AzureOpenAIClient(new Uri(endpoint), credential);
var chatClient = openAIClient.GetChatClient(deploymentName);
IChatClient extensionsAIChatClient = chatClient.AsIChatClient();
logger.LogInformation("認証情報取得完了");

logger.LogInformation("=== Task-Based Workflow デモ ===");
Console.WriteLine("質問を入力してください (終了するには 'exit' と入力):");
var question = Console.ReadLine();

if (string.IsNullOrWhiteSpace(question) || question.ToLower() == "exit")
{
    logger.LogInformation("終了します。");
    return;
}

logger.LogInformation("受信した質問: {Question}", question);

// タスクボードを初期化
var taskBoard = new TaskBoard { Objective = question };

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("フェーズ 1: Planner がタスク計画を作成");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// Planner Agent を作成
var plannerAgent = CreatePlannerAgent(extensionsAIChatClient);

// Planner にタスク計画を依頼
var plannerMessages = new List<ChatMessage>
{
    new ChatMessage(ChatRole.User, $@"目標: {question}

この目標を達成するためのタスク計画を作成してください。
以下のJSON形式で回答してください:

{{
  ""tasks"": [
    {{
      ""id"": ""task-1"",
      ""description"": ""タスクの説明"",
      ""acceptance"": ""受け入れ基準"",
      ""assignedTo"": ""Contract/Spend/Negotiation/Sourcing/Knowledge/Supplier""
    }}
  ]
}}

利用可能な専門家:
- Contract: 契約関連の専門家
- Spend: 支出分析の専門家
- Negotiation: 交渉戦略の専門家
- Sourcing: 調達戦略の専門家
- Knowledge: 知識管理の専門家
- Supplier: サプライヤー管理の専門家

通常は2-4つのタスクで構成してください。")
};

string plannerResponse;
using var plannerActivity = activitySource.StartActivity("Agent.Planner", ActivityKind.Internal);
plannerActivity?.SetTag("agent.name", "Planner");
plannerActivity?.SetTag("agent.role", "Planner");
plannerActivity?.SetTag("workflow.objective", taskBoard.Objective);

try
{
    using var plannerCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

    var plannerWorkflow = AgentWorkflowBuilder
        .CreateHandoffBuilderWith(plannerAgent)
        .Build();

    var plannerWorkflowAgent = await plannerWorkflow.AsAgentAsync("planner", "Planner");
    var plannerThread = plannerWorkflowAgent.GetNewThread();

    var responseBuilder = new StringBuilder();
    await foreach (var update in plannerWorkflowAgent.RunStreamingAsync(plannerMessages, plannerThread, cancellationToken: plannerCts.Token))
    {
        if (!string.IsNullOrEmpty(update.Text))
        {
            responseBuilder.Append(update.Text);
        }
    }

    plannerResponse = responseBuilder.ToString();
    plannerActivity?.SetTag("agent.response.length", plannerResponse.Length);
    logger.LogInformation("[Planner の計画]\n{PlannerResponse}", plannerResponse);
}
catch (Exception ex)
{
    plannerActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    plannerActivity?.SetTag("agent.error", ex.Message);
    logger.LogError(ex, "❌ Planner エラー: {ErrorMessage}", ex.Message);
    return;
}

// JSON をパースしてタスクを抽出
try
{
    var jsonStart = plannerResponse.IndexOf('{');
    var jsonEnd = plannerResponse.LastIndexOf('}');
    if (jsonStart >= 0 && jsonEnd > jsonStart)
    {
        var jsonText = plannerResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
        var planData = JsonSerializer.Deserialize<JsonElement>(jsonText);

        if (planData.TryGetProperty("tasks", out var tasksArray))
        {
            foreach (var taskElement in tasksArray.EnumerateArray())
            {
                var task = new TaskItem(
                    taskElement.GetProperty("id").GetString() ?? Guid.NewGuid().ToString(),
                    taskElement.GetProperty("description").GetString() ?? "",
                    taskElement.GetProperty("acceptance").GetString() ?? "",
                    TaskStatus.Queued,
                    taskElement.TryGetProperty("assignedTo", out var assignedTo) ? assignedTo.GetString() : null
                );
                taskBoard.Tasks.Add(task);
            }
        }
    }

    logger.LogInformation("✓ {TaskCount} 個のタスクを作成しました", taskBoard.Tasks.Count);
}
catch (Exception ex)
{
    plannerActivity?.SetTag("plan.parseError", ex.Message);
    logger.LogWarning(ex, "⚠️ プラン解析エラー: {ErrorMessage}", ex.Message);
    logger.LogInformation("デフォルトタスクを作成します。");
    taskBoard.Tasks.Add(new TaskItem(
        "task-1",
        question,
        "質問に対する回答を提供する",
        TaskStatus.Queued,
        "Knowledge"
    ));
}

plannerActivity?.SetTag("plan.tasks.count", taskBoard.Tasks.Count);
plannerActivity?.SetStatus(ActivityStatusCode.Ok);

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("フェーズ 2: Worker がタスクを実行");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

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

// タスクを順次実行
var taskResults = new Dictionary<string, string>();

foreach (var task in taskBoard.Tasks.ToList())
{
    var assignedWorker = task.AssignedTo ?? "Knowledge";
    if (!specialists.ContainsKey(assignedWorker))
    {
        assignedWorker = "Knowledge";
    }

    using var workerActivity = activitySource.StartActivity($"Agent.{assignedWorker}", ActivityKind.Internal);
    workerActivity?.SetTag("agent.name", assignedWorker);
    workerActivity?.SetTag("agent.role", "Worker");
    workerActivity?.SetTag("workflow.objective", taskBoard.Objective);
    workerActivity?.SetTag("task.id", task.Id);
    workerActivity?.SetTag("task.description", task.Description);
    workerActivity?.SetTag("task.acceptance", task.Acceptance);

    logger.LogInformation("[Task {TaskId}] {TaskDescription}", task.Id, task.Description);
    logger.LogInformation("担当: {AssignedWorker}", assignedWorker);
    logger.LogInformation("受け入れ基準: {Acceptance}", task.Acceptance);

    // タスクをDoingに更新
    taskBoard.AssignTask(task.Id, assignedWorker);
    workerActivity?.SetTag("task.status", TaskStatus.Doing.ToString());

    var specialist = specialists[assignedWorker];
    var workerMessages = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.User, $@"目標: {taskBoard.Objective}

タスク: {task.Description}
受け入れ基準: {task.Acceptance}

このタスクを完了してください。簡潔に回答してください。")
    };

    try
    {
        using var workerCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var specialistWorkflow = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(specialist)
            .Build();

        var specialistWorkflowAgent = await specialistWorkflow.AsAgentAsync(assignedWorker, assignedWorker);
        var specialistThread = specialistWorkflowAgent.GetNewThread();

        var resultBuilder = new StringBuilder();
        await foreach (var update in specialistWorkflowAgent.RunStreamingAsync(workerMessages, specialistThread, cancellationToken: workerCts.Token))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                resultBuilder.Append(update.Text);
            }
        }

        var result = resultBuilder.ToString();
        taskResults[task.Id] = result;

        logger.LogInformation("[{AssignedWorker} の回答]\n{Result}", assignedWorker, result);

        workerActivity?.SetTag("agent.response.length", result.Length);

        // タスクをDoneに更新
        taskBoard.UpdateTaskStatus(task.Id, TaskStatus.Done, "完了");
        workerActivity?.SetTag("task.status", TaskStatus.Done.ToString());
        workerActivity?.SetStatus(ActivityStatusCode.Ok);
        logger.LogInformation("✓ Task {TaskId} 完了", task.Id);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ {AssignedWorker} のエラー: {ErrorMessage}", assignedWorker, ex.Message);
        taskBoard.UpdateTaskStatus(task.Id, TaskStatus.Blocked, $"エラー: {ex.Message}");
        workerActivity?.SetTag("task.status", TaskStatus.Blocked.ToString());
        workerActivity?.SetTag("agent.error", ex.Message);
        workerActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    }
}

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("フェーズ 3: 結果の統合");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// 最終結果を表示
logger.LogInformation("## 目標: {Objective}", taskBoard.Objective);
logger.LogInformation("## タスク実行結果:");

foreach (var task in taskBoard.Tasks)
{
    var statusEmoji = task.Status switch
    {
        TaskStatus.Done => "✅",
        TaskStatus.Blocked => "❌",
        TaskStatus.Doing => "🔄",
        _ => "⏳"
    };

    logger.LogInformation("{StatusEmoji} [{TaskId}] {TaskDescription}", statusEmoji, task.Id, task.Description);
    logger.LogInformation("   担当: {AssignedTo}", task.AssignedTo ?? "未割当");
    logger.LogInformation("   状態: {Status}", task.Status);

    if (taskResults.TryGetValue(task.Id, out var result))
    {
        logger.LogInformation("   結果概要: {ResultSnippet}", new string(result.Take(80).ToArray()) + (result.Length > 80 ? "..." : string.Empty));
    }

    if (!string.IsNullOrEmpty(task.Notes))
    {
        logger.LogInformation("   備考: {Notes}", task.Notes);
    }
}

var completedTasks = taskBoard.Tasks.Count(t => t.Status == TaskStatus.Done);
var totalTasks = taskBoard.Tasks.Count;
logger.LogInformation("完了率: {CompletedTasks}/{TotalTasks} ({CompletionRate:F1}%)",
    completedTasks, totalTasks, (completedTasks * 100.0 / totalTasks));

if (taskResults.Count > 0)
{
    using var summaryActivity = activitySource.StartActivity("Agent.Summary", ActivityKind.Internal);
    summaryActivity?.SetTag("agent.name", "Summary");
    summaryActivity?.SetTag("agent.role", "Coordinator");
    summaryActivity?.SetTag("workflow.objective", taskBoard.Objective);

    var summaryAgent = CreateSummaryAgent(extensionsAIChatClient);
    var summaryPayload = taskBoard.Tasks.Select(t => new
    {
        t.Id,
        t.Description,
        Acceptance = t.Acceptance,
        AssignedTo = t.AssignedTo ?? "未割当",
        Status = t.Status.ToString(),
        Result = taskResults.TryGetValue(t.Id, out var r) ? r : null
    });

    var summaryJson = JsonSerializer.Serialize(summaryPayload, new JsonSerializerOptions
    {
        WriteIndented = true
    });

    var summaryMessages = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.User, $@"目標: {taskBoard.Objective}

タスク結果一覧:
{summaryJson}

上記の情報を統合し、以下の形式で簡潔にまとめてください:

### 最終回答
- ...
- ...
- ...

### 要約
- ...
- ...
- ...

### 詳細
- {{task-id}} (担当: ...): ...

### 主要リスク
- ...
- ...
- ...

### 推奨アクション
- ...
- ...
- ...

### 参考回答
- {{task-id}}:
    ```
    <回答全文>
    ```

箇条書きは3項目前後を目安にし、重複を避けてください。")
    };

    try
    {
        using var summaryCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var summaryWorkflow = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(summaryAgent)
            .Build();

        var summaryWorkflowAgent = await summaryWorkflow.AsAgentAsync("summary", "Summary");
        var summaryThread = summaryWorkflowAgent.GetNewThread();

        var summaryBuilder = new StringBuilder();
        await foreach (var update in summaryWorkflowAgent.RunStreamingAsync(summaryMessages, summaryThread, cancellationToken: summaryCts.Token))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                summaryBuilder.Append(update.Text);
            }
        }

        var summaryText = summaryBuilder.ToString();
        summaryActivity?.SetTag("agent.response.length", summaryText.Length);
        summaryActivity?.SetStatus(ActivityStatusCode.Ok);
        logger.LogInformation("統合要約:\n{Summary}", summaryText);
    }
    catch (Exception ex)
    {
        summaryActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        summaryActivity?.SetTag("agent.error", ex.Message);
        logger.LogWarning(ex, "⚠️ サマリー統合に失敗しました: {Message}", ex.Message);
    }
}

logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("ワークフロー完了");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

Console.WriteLine("\nEnter キーを押して終了してください...");
Console.ReadLine();

logger.LogInformation("=== アプリケーション終了 ===");

static ChatClientAgent CreatePlannerAgent(IChatClient chatClient)
{
    var instructions = """
あなたはタスク計画の専門家です。
ユーザーの目標を分析し、適切なタスクリストと担当者（専門家）を決定します。

以下の専門家が利用可能です:
- Contract: 契約関連の専門家
- Spend: 支出分析の専門家
- Negotiation: 交渉戦略の専門家
- Sourcing: 調達戦略の専門家
- Knowledge: 知識管理の専門家
- Supplier: サプライヤー管理の専門家

タスクを作成する際の原則:
1. 目標を達成するために必要な具体的なタスクに分解する
2. 各タスクには明確な受け入れ基準を設定する
3. 最も適切な専門家を割り当てる
4. 通常は2-4つのタスクで十分です
5. タスクは実行可能で測定可能なものにする

必ずJSON形式で回答してください。
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        "planner_agent",
        "Planner Agent");
}

static ChatClientAgent CreateSpecialistAgent(IChatClient chatClient, string specialty, string description)
{
    var instructions = $"""
あなたは {description} として回答します。

指示された目標・タスクに対して、あなたの専門知識を活かした回答を提供してください。
簡潔で実用的な回答を心がけてください。
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        $"{specialty.ToLower()}_agent",
        $"{specialty} Agent");
}

static ChatClientAgent CreateSummaryAgent(IChatClient chatClient)
{
    var instructions = """
あなたはタスクベースワークフローの調整役です。
提供された各タスクの結果を読み、重複を取り除きながら全体像を要約します。

必ず以下のテンプレートに厳密に従ってください（見出し名・順序は変更不可）。箇条書きの行頭には必ず "- " を付けます。

### 最終回答
- ...
- ...
- ...

### 要約
- ...
- ...
- ...

### 詳細
- {task-id} (担当: ...): ...
- {task-id} (担当: ...): ...
- {task-id} (担当: ...): ...

### 主要リスク
- ...
- ...
- ...

### 推奨アクション
- ...
- ...
- ...

### 参考回答
- {task-id}:
    ```
    <回答全文>
    ```
- {task-id}:
    ```
    <回答全文>
    ```
- {task-id}:
    ```
    <回答全文>
    ```

- 「要約」では全体像を3項目前後で示してください。3項目未満の場合は "- 該当なし" を使います。
- 「最終回答」では質問に対する直接的かつ実行可能な回答を3項目前後で示し、すべての内容が各エージェントの出力を根拠とすることを明確にしてください。3項目未満の場合は "- 該当なし" を使います。
- 「詳細」ではタスクID順にすべてのタスクを列挙し、担当者と結果の要点を一文でまとめます（必要に応じて行数を増やしてかまいません）。
- 「主要リスク」と「推奨アクション」もそれぞれ3項目前後を目安にし、施策や洞察が重複しないようにしてください。該当項目が少ない場合は "- 該当なし" を使います。
- 「参考回答」では各タスクの回答全文をコードブロック (```) で囲んで順番に掲載します（必要に応じて行数を増やし、回答がない場合はコードブロック内に "該当なし" と記載します）。
- 上記テンプレートの {task-id} は入力で提供された実際のタスクIDに置き換え、テンプレート行は必要なぶんだけ繰り返してください。
- 明確で実行可能な表現を用い、日本語で出力します。
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        "summary_agent",
        "Summary Agent");
}

#region Domain

public enum TaskStatus { Queued, Doing, Done, Blocked }

public record TaskItem(
    string Id,
    string Description,
    string Acceptance,              // 受け入れ基準
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TaskStatus Status = TaskStatus.Queued,
    string? AssignedTo = null,      // 担当専門家
    string? Notes = null
);

public class TaskBoard
{
    public string TaskId { get; init; } = Guid.NewGuid().ToString("N");
    public string Objective { get; set; } = "";
    public List<TaskItem> Tasks { get; set; } = new();

    public void UpdateTaskStatus(string taskId, TaskStatus newStatus, string? notes = null)
    {
        var task = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
        {
            var index = Tasks.IndexOf(task);
            Tasks[index] = task with { Status = newStatus, Notes = notes };
        }
    }

    public void AssignTask(string taskId, string worker)
    {
        var task = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
        {
            var index = Tasks.IndexOf(task);
            Tasks[index] = task with { AssignedTo = worker, Status = TaskStatus.Doing };
        }
    }
}

#endregion
