# GraphExecutorWorkflow

## 概要

このワークフローは、Microsoft Agent Framework の **WorkflowBuilder** と **条件付きエッジ** を使用した本格的なグラフベースのワークフロー実装です。

HandsOff パターンではなく、`ReflectingExecutor`、`AddFanOutEdge`、カスタムパーティショナーなど、ワークフローの高度な機能を活用しています。

## アーキテクチャ

```
┌─────────────────────────────────────────────────────────────┐
│           WorkflowBuilder-Based Graph Workflow              │
└─────────────────────────────────────────────────────────────┘

   ユーザー質問 (ChatMessage)
        │
        ↓
┌──────────────────┐
│ Router Executor  │  ← ReflectingExecutor<RouterExecutor>
│                  │     IMessageHandler<ChatMessage, RouterDecision>
│ - 質問を分析     │
│ - 専門家を選抜   │
│ - JSON で出力    │
└──────────────────┘
        │
        │ AddFanOutEdge with Custom Partitioner
        │ (動的分岐: 選抜された専門家のみ実行)
        │
        ├─────────┬─────────┬─────────┬─────────┬─────────┐
        ↓         ↓         ↓         ↓         ↓         ↓
    ┌────────┐┌────────┐┌────────┐┌────────┐┌────────┐┌────────┐
    │Contract││ Spend  ││Negotia-││Sourcing││Knowledge││Supplier│
    │Executor││Executor││tion    ││Executor││Executor││Executor│
    │        ││        ││Executor││        ││        ││        │
    └────────┘└────────┘└────────┘└────────┘└────────┘└────────┘
         │        │         │         │         │         │
         └────────┴─────────┴─────────┴─────────┴─────────┘
                            │
                            │ AddEdge (結合)
                            ↓
                   ┌──────────────────┐
                   │ Aggregator       │  ← ReflectingExecutor<AggregatorExecutor>
                   │ Executor         │     IMessageHandler<OpinionData>
                   │                  │
                   │ - 意見を統合     │
                   │ - 構造化出力     │
                   │ - 最終回答       │
                   └──────────────────┘
                            │
                            │ WithOutputFrom
                            ↓
                       最終回答
```

## 主要コンポーネント

### 1. Router Executor (ReflectingExecutor)
```csharp
internal sealed class RouterExecutor : 
    ReflectingExecutor<RouterExecutor>, 
    IMessageHandler<ChatMessage, RouterDecision>
```

- **役割**: ユーザーの質問を分析し、必要な専門家を選抜
- **入力**: `ChatMessage` (ユーザー質問)
- **出力**: `RouterDecision` (選抜された専門家のリスト)
- **特徴**:
  - Structured Output (JSON Schema) を使用
  - 質問を WorkflowState に保存
  - カスタム `RouterEvent` を発行

### 2. Specialist Executors (ReflectingExecutor)
```csharp
internal sealed class SpecialistExecutor : 
    ReflectingExecutor<SpecialistExecutor>, 
    IMessageHandler<RouterDecision, OpinionData>
```

- **役割**: 各専門領域から意見を生成
- **種類**: 
  - Contract Executor: 契約関連の専門家
  - Spend Executor: 支出分析の専門家
  - Negotiation Executor: 交渉戦略の専門家
  - Sourcing Executor: 調達戦略の専門家
  - Knowledge Executor: 知識管理の専門家
  - Supplier Executor: サプライヤー管理の専門家
- **入力**: `RouterDecision`
- **出力**: `OpinionData` (専門家の意見)
- **実行**: パーティショナーによる動的並列実行
- **特徴**:
  - WorkflowState から質問を読み取り
  - 意見を OpinionsState に保存
  - カスタム `SpecialistEvent` を発行

### 3. Aggregator Executor (ReflectingExecutor)
```csharp
internal sealed class AggregatorExecutor : 
    ReflectingExecutor<AggregatorExecutor>, 
    IMessageHandler<OpinionData>
```

- **役割**: 各専門家の意見を統合し、構造化された最終回答を生成
- **入力**: `OpinionData` (最後の専門家意見をトリガーとして使用)
- **出力**: 構造化された最終回答 (YieldOutput)
- **特徴**:
  - 全ての専門家意見を OpinionsState から収集
  - Structured Output で統合回答を生成
  - 構造化フォーマットで最終出力

## ワークフローグラフの構築

このワークフローは `WorkflowBuilder` を使用して構築されます：

```csharp
WorkflowBuilder builder = new(routerExecutor);
builder
    .AddFanOutEdge(
        routerExecutor,
        targets: [contractExecutor, spendExecutor, negotiationExecutor, 
                  sourcingExecutor, knowledgeExecutor, supplierExecutor],
        partitioner: GetSpecialistPartitioner()
    )
    .AddEdge(contractExecutor, aggregatorExecutor)
    .AddEdge(spendExecutor, aggregatorExecutor)
    .AddEdge(negotiationExecutor, aggregatorExecutor)
    .AddEdge(sourcingExecutor, aggregatorExecutor)
    .AddEdge(knowledgeExecutor, aggregatorExecutor)
    .AddEdge(supplierExecutor, aggregatorExecutor)
    .WithOutputFrom(aggregatorExecutor);
```

### エッジの種類

1. **AddFanOutEdge (動的分岐)**
   ```csharp
   .AddFanOutEdge(source, targets, partitioner)
   ```
   - カスタムパーティショナー関数で実行するターゲットを決定
   - `RouterDecision.Selected` に基づいて専門家を選択
   - 選抜されなかった Executor は実行されない（コスト最適化）

2. **AddEdge (単純エッジ)**
   ```csharp
   .AddEdge(specialist, aggregator)
   ```
   - 各専門家 Executor から Aggregator への固定エッジ
   - 実行された専門家全員が完了後、Aggregator が起動

3. **WithOutputFrom (出力指定)**
   ```csharp
   .WithOutputFrom(aggregatorExecutor)
   ```
   - ワークフローの最終出力を生成する Executor を指定

## 特徴

### ✅ WorkflowBuilder パターンの使用
- Microsoft Agent Framework の正式な WorkflowBuilder API を使用
- HandsOff パターンではなく、グラフベースの構築
- 明示的なエッジとパーティショナーの定義

### ✅ ReflectingExecutor の実装
- `ReflectingExecutor<T>` を継承した型安全な Executor
- `IMessageHandler<TInput, TOutput>` インターフェースの実装
- フレームワークによる自動的なメッセージルーティング

### ✅ Structured Output (JSON Schema)
- `ChatResponseFormat.ForJsonSchema<T>()` を使用
- 型安全な Agent 応答
- パース エラーの削減

### ✅ WorkflowState 管理
- `IWorkflowContext` による状態管理
- `QueueStateUpdateAsync` / `ReadStateAsync` でデータ共有
- スコープ分離 (QuestionState / OpinionsState)

### ✅ カスタム WorkflowEvent
- `RouterEvent` / `SpecialistEvent` の独自イベント
- リアルタイムな進行状況の可視化
- `context.AddEventAsync()` による発行

### ✅ 動的並列実行
- FanOutEdge + Partitioner による条件分岐
- 選抜された専門家のみ実行
- コストと時間の最適化

## 使用方法

### 前提条件

1. Azure OpenAI サービスへのアクセス
2. Azure CLI でログイン済み
3. 環境変数の設定:
   - `AZURE_OPENAI_ENDPOINT`
   - `AZURE_OPENAI_DEPLOYMENT_NAME`

または、`appsettings.json` / `appsettings.Development.json` に設定を記載できます。

### 実行

```bash
cd src/GraphExecutorWorkflow
dotnet run
```

### 実行例

```
質問> 新しいサプライヤーとの契約で注意すべき点は？

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
ステップ 1: Router Executor - 専門家を特定
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✓ 選抜された専門家: Contract, Supplier

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
ステップ 2: Specialist Executors - 意見を生成
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[Contract] 契約条件の明確化、リスク条項の確認、法的コンプライアンスの確保が重要です...
[Supplier] サプライヤーの信頼性評価、パフォーマンス指標の設定、長期的な関係構築が必要です...

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
ステップ 3: Aggregator Executor - 意見を集約
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

【最終回答】
## 結論
...

## 根拠
- ...

## 各専門家の所見
...

## 推奨アクション
- ...
```

## 技術詳細

### Executor の実装

各 Executor は `ChatClientAgent` として実装され、以下の要素を持ちます：

```csharp
static ChatClientAgent CreateRouterExecutor(IChatClient chatClient)
{
    var instructions = "..."; // Executor の役割と指示
    return new ChatClientAgent(
        chatClient,
        instructions,
        "router_executor",  // Executor ID
        "Router Executor"); // Executor 名
}
```

### エッジの実装

エッジは非同期関数として実装され、Executor 間のデータフローを制御します：

```csharp
// Router → Specialists エッジ
static async Task<List<string>> ExecuteRouterAsync(...)
{
    // Router を実行し、選抜結果を返す
}

// Specialists → Aggregator エッジ
static async Task<Dictionary<string, string>> ExecuteSpecialistsAsync(...)
{
    // 選抜された Specialist を並列実行
    var tasks = selectedSpecialists.Select(async name => ...);
    return await Task.WhenAll(tasks);
}
```

### OpenTelemetry トレーシング

各 Executor の実行は Activity としてトレースされます：

```csharp
using var activity = activitySource.StartActivity("RouterExecutor", ActivityKind.Internal);
activity?.SetTag("executor.type", "router");
activity?.SetTag("question", question);
```

## 比較: 他のワークフローとの違い

| 特徴 | GraphExecutor | SelectiveGroupChat | Handoff |
|------|---------------|-------------------|---------|
| アーキテクチャ | Executor + Edge | フェーズベース | Handoff ベース |
| エッジ定義 | 明示的 | 暗黙的 | 暗黙的 |
| 動的分岐 | ✅ Yes | ✅ Yes | ❌ No |
| 並列実行 | ✅ Yes | ✅ Yes | ❌ No |
| 責任分離 | ⭐⭐⭐ 高 | ⭐⭐ 中 | ⭐⭐ 中 |
| 拡張性 | ⭐⭐⭐ 高 | ⭐⭐ 中 | ⭐ 低 |

## 今後の拡張

このアーキテクチャは以下の拡張に適しています：

- [ ] 条件分岐エッジの追加
- [ ] ループエッジの実装（再試行ロジック）
- [ ] HITL (Human-In-The-Loop) Executor の統合
- [ ] Quality Gate Executor の追加
- [ ] キャッシュ Executor の実装
- [ ] 永続化された状態管理

## まとめ

GraphExecutorWorkflow は、Agent Framework の Executor と Edge の概念を明示的に実装したワークフローです。各コンポーネントの責任が明確に分離され、拡張性と保守性に優れた設計となっています。
