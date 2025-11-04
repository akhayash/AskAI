# Dynamic Group Chat Workflow - 実装詳細

> **関連**: [プロジェクトREADME](../../src/DynamicGroupChatWorkflow/README.md) | [Clean Architecture](../architecture/clean-architecture.md) | [ログセットアップ](../development/logging-setup.md)

## 概要

Router が会話の流れに応じて専門家を動的に選抜し、必要に応じてユーザーにも意見を求める適応的なワークフローです。

**実装機能**: Dynamic Selection, Handoff, HITL (Human-in-the-Loop), Loop Monitoring, OpenTelemetry 統合

## ワークフロー構造

### 4 フェーズ構成

```text
Phase 1: Router         → 質問分析と動的専門家選抜
Phase 2: Specialists    → 選抜された専門家が順次意見を提供
Phase 3: HITL (オプション) → ユーザーへの追加情報依頼
Phase 4: Moderator      → 全情報の統合と最終回答生成
```

## アーキテクチャ

```text
ユーザー質問
    ↓
Router Agent (動的選抜・調整)
    ↓ Handoff
Specialist 1 → Router → Specialist 2 → Router → ...
    ↓ (必要に応じて)
User Input (HITL) → Router (会話履歴に追加)
    ↓ Handoff
Moderator Agent (最終統合)
    ↓
構造化最終回答
```

## 主要機能

### 1. 動的な専門家選抜

Router が質問内容と会話の進行状況に応じて、必要な専門家を**順次・動的に**選抜します。

```csharp
// Router Agent の指示
var instructions = """
あなたは調達領域のルーターです。

役割:
1. ユーザー質問を分析し、必要な専門家を動的に選抜
2. 専門家にハンドオフして意見を収集
3. 専門家の意見を踏まえ、さらに情報が必要か判断:
   - 他の専門家の意見が必要 → その専門家にハンドオフ
   - ユーザーの追加情報が必要 → ユーザーに質問してください（「？」で終わる質問文）
4. 十分な情報が集まったら、Moderator Agent にハンドオフ
""";
```

**選択可能な専門家**:
- Contract Agent (契約関連)
- Spend Agent (支出分析)
- Negotiation Agent (交渉戦略)
- Sourcing Agent (調達戦略)
- Knowledge Agent (知識管理)
- Supplier Agent (サプライヤー管理)

### 2. Human-in-the-Loop (HITL) with Loop Monitoring

Router が「？」で終わる質問を出力した場合、システムがユーザー入力待ちに移行します。**ループ監視方式**を採用しています。

#### ループ監視方式の実装

```csharp
await foreach (var update in workflowAgent.RunStreamingAsync(messages, thread, cancellationToken: cts.Token))
{
    if (!string.IsNullOrEmpty(update.Text))
    {
        Console.Write(update.Text);
        currentMessage.Append(update.Text);
        
        // Router からの質問を検出（「？」で終わる場合）
        if (currentAgent == "router_agent" && update.Text.Contains("？"))
        {
            waitingForUserInput = true;
        }
    }
}

// ユーザー入力待ちチェック
if (waitingForUserInput)
{
    Console.Write("\n回答> ");
    var userResponse = Console.ReadLine();
    
    if (!string.IsNullOrWhiteSpace(userResponse))
    {
        messages.Add(new ChatMessage(ChatRole.User, userResponse));
        
        // ワークフローを再実行
        thread = workflowAgent.GetNewThread();
        await foreach (var newUpdate in workflowAgent.RunStreamingAsync(messages, thread, cancellationToken: cts.Token))
        {
            // 処理を継続...
        }
    }
}
```

**ループ監視方式の利点**:
- 現在の Agent Framework バージョンとの互換性
- シンプルな実装
- ストリーミング出力との親和性が高い

**動作フロー**:
1. Router が専門家の意見を収集
2. Router が「予算の上限を教えてください。」のような質問を出力
3. システムが「？」を検出し、ユーザー入力待ち状態に移行
4. ユーザーが回答を入力
5. 回答を会話履歴に追加してワークフローを再実行
6. Router が新しい情報を踏まえて次のアクションを決定

### 3. 専門家間の対話

会話履歴が引き継がれるため、専門家が他の意見を参照しながら回答できます。

```csharp
static ChatClientAgent CreateSpecialistAgent(IChatClient chatClient, string specialty, string description)
{
    var instructions = $"""
あなたは {description} として回答します。

役割:
- 専門知識を活用してユーザーの質問に答える
- 会話履歴に他の専門家の意見やユーザーの追加情報が含まれている場合、それらを参考にする
- 回答が完了したら、必ず Router Agent にハンドオフして結果を報告する
""";
    
    return new ChatClientAgent(chatClient, instructions, ...);
}
```

### 4. Moderator による統合

全ての情報（専門家の意見 + ユーザー入力）を統合して、構造化された最終回答を生成します。

```csharp
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
```

## OpenTelemetry 統合

### Activity 構造

```csharp
// ワークフロー全体のトレース
using var workflowActivity = activitySource.StartActivity("Workflow: Dynamic Group Chat", ActivityKind.Internal);
workflowActivity?.SetTag("initial.question", question);

// 各エージェントターンのトレース
var agentActivity = activitySource.StartActivity($"Agent Turn: {agentDisplayName} (#{messageCount})", ActivityKind.Internal);
agentActivity?.SetTag("agent.name", update.AuthorName ?? "不明");
agentActivity?.SetTag("agent.id", update.AgentId);
agentActivity?.SetTag("message.ordinal", messageCount);

// ユーザー入力イベント
workflowActivity?.AddEvent(new ActivityEvent("user-input-received", tags: new ActivityTagsCollection
{
    { "user.input.length", userResponse.Length }
}));
```

### 分散トレーシング

- **ActivitySource**: `DynamicGroupChatWorkflow`
- **Exporter**: OTLP (default: `http://localhost:4317`)
- **可視化**: Aspire Dashboard (`http://localhost:18888`)
- **Agent Framework 内部ログ**: `Microsoft.Agents.AI.Workflows*` ソースを有効化

## データフロー

```text
ユーザー質問
    ↓
ChatMessage[] (会話履歴)
    ↓
Router Agent → 専門家選抜判断
    ↓ Handoff
Specialist Agent → 意見生成 → Router へ報告
    ↓ (会話履歴に追加)
Router Agent → 追加情報が必要? Yes → HITL
                              No → Moderator へハンドオフ
    ↓ (HITL の場合)
ユーザー入力 → 会話履歴に追加 → ワークフロー再実行
    ↓
Moderator Agent → 最終回答生成
```

## エージェント設計

### Handoff Pattern

このワークフローは、Agent Framework の Handoff パターンを使用してエージェント間の連携を実現しています。

```csharp
var workflow = AgentWorkflowBuilder
    .CreateHandoffBuilderWith(routerAgent)
    .WithHandoffs(routerAgent, [contractAgent, spendAgent, negotiationAgent,
                                  sourcingAgent, knowledgeAgent, supplierAgent,
                                  moderatorAgent])
    .WithHandoffs([contractAgent, spendAgent, negotiationAgent,
                   sourcingAgent, knowledgeAgent, supplierAgent], routerAgent)
    .Build();
```

**Handoff の利点**:
- エージェント間の制御フローが明確
- 会話履歴が自動的に引き継がれる
- ストリーミング出力に対応

## カスタマイズポイント

### 1. 専門家の追加

```csharp
// 新しい専門家エージェントを追加
var complianceAgent = CreateSpecialistAgent(extensionsAIChatClient, "Compliance", "コンプライアンスの専門家");

// Handoff 設定を更新
var workflow = AgentWorkflowBuilder
    .CreateHandoffBuilderWith(routerAgent)
    .WithHandoffs(routerAgent, [contractAgent, ..., complianceAgent, moderatorAgent])
    .WithHandoffs([contractAgent, ..., complianceAgent], routerAgent)
    .Build();
```

### 2. HITL の動作調整

```csharp
// Router の指示を変更して、質問のタイミングや形式を調整
var instructions = """
...
ユーザーへの質問は本当に必要な場合のみ（1回まで推奨）
質問は明確で具体的に（例: "予算の上限を教えてください。"）
質問は「？」で終わるようにしてください。
...
""";
```

### 3. マルチターン対応の拡張

現在は1回の追加質問に対応。マルチターンに拡張する場合:

```csharp
// ループ回数の追跡
var hitlRound = 0;
var maxHitlRounds = 3;

while (waitingForUserInput && hitlRound < maxHitlRounds)
{
    hitlRound++;
    // ユーザー入力処理...
    // ワークフロー再実行...
}
```

## トラブルシューティング

### HITL が発動しない

**原因**: Router が「？」付きの質問を出力していない  
**確認**: Router の指示が明確か確認

```csharp
Logger.LogInformation("[Router Agent の発言内容]: {RouterMessage}", currentMessage.ToString());
```

### 専門家が選抜されない

**原因**: Router の判断ロジックが適切でない  
**確認**: Router に専門家の一覧と役割を明確に指示

### ワークフローが終了しない

**原因**: Moderator へのハンドオフが行われていない  
**確認**: Router が「十分な情報が集まったら Moderator へハンドオフ」を理解しているか

## パフォーマンス考慮事項

### 順次実行の特性

- 専門家は順次実行される（並列実行ではない）
- Router が動的に選抜するため、無駄な専門家呼び出しを削減
- 会話履歴が増えるほど、コンテキスト処理時間が増加

### 最適化の方向性

1. **必要最小限の専門家選抜**: Router の指示を洗練
2. **会話履歴の圧縮**: 長い会話を要約する機能の追加
3. **並列実行の検討**: 確実に必要な専門家は並列実行

## 参考資料

- [Microsoft Agent Framework Workflows](https://learn.microsoft.com/dotnet/ai/agents)
- [Handoff Pattern](https://learn.microsoft.com/dotnet/ai/agents/handoffs)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)
- [プロジェクト Clean Architecture](../architecture/clean-architecture.md)
- [ログセットアップガイド](../development/logging-setup.md)
