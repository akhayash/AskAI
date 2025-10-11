using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
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

// 環境変数を設定から取得（appsettings.json → 環境変数の順で優先）
var endpoint = configuration["environmentVariables:AZURE_OPENAI_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("環境変数 AZURE_OPENAI_ENDPOINT が設定されていません。");

var deployment = configuration["environmentVariables:AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("環境変数 AZURE_OPENAI_DEPLOYMENT_NAME が設定されていません。");

Console.WriteLine($"エンドポイント: {endpoint}");
Console.WriteLine($"デプロイメント名: {deployment}");

Console.WriteLine("\n認証情報の取得中（Azure CLI のみを使用）...");
// Visual Studio での Managed Identity エラーを回避するため、Azure CLI 認証のみを有効化
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
Console.WriteLine("認証情報取得完了");

var openAIClient = new AzureOpenAIClient(new Uri(endpoint), credential);
var chatClient = openAIClient.GetChatClient(deployment);
IChatClient extensionsAIChatClient = chatClient.AsIChatClient();

Console.WriteLine("Router Workflow デモ - 質問を入力してください。");
Console.Write("質問> ");
var question = Console.ReadLine();

if (string.IsNullOrWhiteSpace(question))
{
    Console.WriteLine("質問が空です。");
    return;
}

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

Console.WriteLine("\n--- ワークフロー実行開始 ---");

// タイムアウト設定（60秒に延長）
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

try
{
    Console.WriteLine("ワークフローをエージェントに変換中...");
    // ワークフローを型付きに変換してAIAgentとしてラップ
    var workflowAgent = await workflow.AsAgentAsync("workflow", "Routing Workflow");
    Console.WriteLine("エージェント変換完了");
    
    var thread = workflowAgent.GetNewThread();
    Console.WriteLine("スレッド作成完了");

    Console.WriteLine($"メッセージ数: {messages.Count}");
    Console.WriteLine($"メッセージ内容: {messages[0].Text}");
    Console.WriteLine("ストリーミング開始...\n");

    var updateCount = 0;
    var messageCount = 0; // 完了したメッセージ数
    var maxMessages = 20; // 最大メッセージ数
    var currentAgentId = "";
    var currentMessage = new System.Text.StringBuilder();
    
    await foreach (var update in workflowAgent.RunStreamingAsync(messages, thread, cancellationToken: cts.Token))
    {
        updateCount++;
        
        // エージェントが変わった場合、新しいメッセージとしてカウント
        if (!string.IsNullOrEmpty(update.AgentId) && update.AgentId != currentAgentId)
        {
            // 前のメッセージを出力
            if (currentMessage.Length > 0)
            {
                Console.WriteLine($"\n\n[メッセージ完了 #{messageCount}]");
                Console.WriteLine($"  エージェント: {currentAgentId}");
                Console.WriteLine($"  内容: {currentMessage}");
                Console.WriteLine("---");
                currentMessage.Clear();
            }
            
            messageCount++;
            currentAgentId = update.AgentId;
            
            Console.WriteLine($"\n\n[メッセージ開始 #{messageCount}]");
            Console.WriteLine($"  エージェント名: {update.AuthorName ?? "不明"}");
            Console.WriteLine($"  エージェントID: {update.AgentId}");
            Console.WriteLine($"  ロール: {update.Role?.ToString() ?? "不明"}");
            Console.WriteLine("  応答: ");
            
            // 最大メッセージ数チェック
            if (messageCount > maxMessages)
            {
                Console.WriteLine($"\n⚠️ 最大メッセージ数 ({maxMessages}) に達しました。");
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

    // 最後のメッセージを出力
    if (currentMessage.Length > 0)
    {
        Console.WriteLine($"\n\n[メッセージ完了 #{messageCount}]");
        Console.WriteLine($"  エージェント: {currentAgentId}");
        Console.WriteLine($"  完全な内容: {currentMessage}");
        Console.WriteLine("---");
    }

    Console.WriteLine($"\n\n合計更新数: {updateCount}, メッセージ数: {messageCount}");
}
catch (OperationCanceledException)
{
    Console.WriteLine("\n⚠️ タイムアウト: ワークフローが時間内に完了しませんでした。");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ エラー: {ex.GetType().Name}");
    Console.WriteLine($"メッセージ: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"内部エラー: {ex.InnerException.Message}");
    }
    Console.WriteLine($"スタックトレース:\n{ex.StackTrace}");
}

Console.WriteLine("\n--- ワークフロー実行完了 ---");

Console.WriteLine("\nEnter キーを押して終了してください...");
Console.ReadLine();

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
