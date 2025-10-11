using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
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

// 環境変数を設定から取得
var endpoint = configuration["environmentVariables:AZURE_OPENAI_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("環境変数 AZURE_OPENAI_ENDPOINT が設定されていません。");

var deployment = configuration["environmentVariables:AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("環境変数 AZURE_OPENAI_DEPLOYMENT_NAME が設定されていません。");

Console.WriteLine($"エンドポイント: {endpoint}");
Console.WriteLine($"デプロイメント名: {deployment}");

Console.WriteLine("\n認証情報の取得中（Azure CLI のみを使用）...");
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

Console.WriteLine("\n=== Group Chat Workflow デモ ===");
Console.WriteLine("質問を入力してください。");
Console.Write("質問> ");
var question = Console.ReadLine();

if (string.IsNullOrWhiteSpace(question))
{
    Console.WriteLine("質問が空です。");
    return;
}

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

Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Group Chat ワークフロー実行開始");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

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
                Console.WriteLine("\n");
                currentMessage.Clear();
            }
            
            messageCount++;
            currentAgentName = update.AuthorName;
            
            // 新しいエージェントのヘッダー
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
    }

    Console.WriteLine($"\n\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Console.WriteLine($"合計メッセージ数: {messageCount}");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
}
catch (OperationCanceledException)
{
    Console.WriteLine("\n?? タイムアウト: ワークフローが時間内に完了しませんでした。");
}
catch (Exception ex)
{
    Console.WriteLine($"\n? エラー: {ex.GetType().Name}");
    Console.WriteLine($"メッセージ: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"内部エラー: {ex.InnerException.Message}");
    }
}

Console.WriteLine("\nEnter キーを押して終了してください...");
Console.ReadLine();

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
