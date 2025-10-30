# Graph Executor ワークフローの調査結果

## 概要

本ドキュメントは、Agent Framework のワークフロー機能を使用した Executor と Edge による実装の調査結果をまとめたものです。

## 調査目的

ユーザーの発話から以下のフローを実現するワークフローを実装する：

1. **Router が必要な専門家を特定**
2. **各専門家からの意見が生成**（並列実行）
3. **最後に意見を集約し出力**

## 実装結果: GraphExecutorWorkflow

### プロジェクト構成

```
src/GraphExecutorWorkflow/
├── Program.cs              # メイン実装
├── README.md              # 詳細ドキュメント
├── GraphExecutorWorkflow.csproj
├── appsettings.json
└── appsettings.Development.json
```

### アーキテクチャ概要

```
ユーザー質問
    │
    ↓
┌─────────────────┐
│ Router Executor │ ← ステップ 1: 専門家を特定
└─────────────────┘
    │ (動的分岐エッジ)
    ├───────────┬───────────┐
    ↓           ↓           ↓
┌─────────┐ ┌─────────┐ ┌─────────┐
│Specialist│ │Specialist│ │Specialist│ ← ステップ 2: 意見生成（並列）
│Executor 1│ │Executor 2│ │Executor N│
└─────────┘ └─────────┘ └─────────┘
    │           │           │
    └───────────┴───────────┘
             │ (結合エッジ)
             ↓
    ┌──────────────────┐
    │ Aggregator       │ ← ステップ 3: 意見を集約
    │ Executor         │
    └──────────────────┘
             │
             ↓
         最終回答
```

## Executor の概念

### 1. Executor とは

Executor は、ワークフロー内の独立した実行単位です。各 Executor は以下の特徴を持ちます：

- **単一責任**: 1つの明確な役割を持つ
- **独立性**: 他の Executor から独立して実行可能
- **再利用性**: 異なるワークフローで再利用可能
- **観測可能**: トレーシングとロギングのサポート

### 2. 実装された Executor

#### Router Executor
```csharp
static ChatClientAgent CreateRouterExecutor(IChatClient chatClient)
{
    var instructions = """
    あなたは Router Executor です。
    ユーザーの質問を分析し、必要な専門家を選抜します。
    """;
    
    return new ChatClientAgent(
        chatClient,
        instructions,
        "router_executor",
        "Router Executor");
}
```

**役割**: 
- ユーザー質問を分析
- 必要な専門家を選抜
- JSON 形式で結果を出力

**出力例**:
```json
{
  "selected": ["Contract", "Supplier"],
  "reason": "契約とサプライヤー管理の専門知識が必要"
}
```

#### Specialist Executors

6種類の専門家 Executor を実装：

1. **Contract Executor**: 契約関連の専門家
2. **Spend Executor**: 支出分析の専門家
3. **Negotiation Executor**: 交渉戦略の専門家
4. **Sourcing Executor**: 調達戦略の専門家
5. **Knowledge Executor**: 知識管理の専門家
6. **Supplier Executor**: サプライヤー管理の専門家

各 Executor は専門領域からの意見を生成します。

#### Aggregator Executor
```csharp
static ChatClientAgent CreateAggregatorExecutor(IChatClient chatClient)
{
    var instructions = """
    あなたは Aggregator Executor です。
    複数の専門家の意見を統合し、構造化された最終回答を生成します。
    """;
    
    return new ChatClientAgent(
        chatClient,
        instructions,
        "aggregator_executor",
        "Aggregator Executor");
}
```

**役割**:
- 各専門家の意見を統合
- 構造化された最終回答を生成
- 結論、根拠、所見、推奨アクションを含む

## Edge の概念

### 1. Edge とは

Edge は、Executor 間のデータフローと実行順序を定義します。

- **データフロー**: Executor 間でデータを受け渡し
- **制御フロー**: 実行順序と条件分岐を制御
- **変換**: データの変換や加工を実行

### 2. 実装された Edge

#### Edge 1: Router → Specialists（動的分岐）

```csharp
static async Task<List<string>> ExecuteRouterAsync(
    ChatClientAgent routerAgent,
    string question,
    ILogger logger,
    ActivitySource activitySource)
{
    // Router を実行
    // JSON レスポンスをパース
    // 選抜された専門家のリストを返す
    
    return selectedSpecialists;
}
```

**特徴**:
- 動的分岐: Router の出力に基づいて実行する Specialist を決定
- コスト最適化: 不要な Executor は実行されない
- フォールバック: エラー時は Knowledge Executor を使用

#### Edge 2: Specialists → Aggregator（結合）

```csharp
static async Task<Dictionary<string, string>> ExecuteSpecialistsAsync(
    Dictionary<string, ChatClientAgent> specialists,
    List<string> selectedSpecialists,
    string question,
    ILogger logger,
    ActivitySource activitySource)
{
    // 選抜された Specialist を並列実行
    var tasks = selectedSpecialists
        .Where(name => specialists.ContainsKey(name))
        .Select(async name => { /* 実行 */ });
    
    var results = await Task.WhenAll(tasks);
    return results.ToDictionary(...);
}
```

**特徴**:
- 並列実行: 複数の Specialist を同時実行
- 結合: 全ての結果を待ち合わせて統合
- エラーハンドリング: 個別の Executor のエラーを処理

#### Edge 3: Aggregator → Output（最終化）

```csharp
static async Task<string> ExecuteAggregatorAsync(
    ChatClientAgent aggregatorAgent,
    string question,
    Dictionary<string, string> opinions,
    ILogger logger,
    ActivitySource activitySource)
{
    // 各専門家の意見を統合
    // 構造化された最終回答を生成
    
    return finalOutput;
}
```

**特徴**:
- データ統合: 複数の入力を1つの出力に変換
- 構造化: 最終回答を構造化されたフォーマットで生成

## 実装の特徴

### ✅ 明示的なフロー定義

従来のワークフローと異なり、GraphExecutorWorkflow では各ステップとエッジが明示的に定義されています：

```csharp
// ステップ 1: Router Executor
var selectedSpecialists = await ExecuteRouterAsync(...);

// ステップ 2: Specialist Executors（並列）
var opinions = await ExecuteSpecialistsAsync(...);

// ステップ 3: Aggregator Executor
var finalOutput = await ExecuteAggregatorAsync(...);
```

### ✅ 責任の明確な分離

各 Executor は単一の責任を持ち、独立してテスト・保守が可能です。

### ✅ 観測性の向上

OpenTelemetry を使用した完全なトレーシング：

```csharp
using var activity = activitySource.StartActivity("RouterExecutor", ActivityKind.Internal);
activity?.SetTag("executor.type", "router");
activity?.SetTag("question", question);
activity?.SetTag("selected.count", selected.Count);
```

### ✅ 並列実行による効率化

Specialist Executors は並列実行され、応答時間を短縮します。

### ✅ 動的な実行パス

Router の判断に基づいて、実行する Executor を動的に決定します。

## 比較: 他のワークフローとの違い

### SelectiveGroupChatWorkflow との比較

| 項目 | GraphExecutor | SelectiveGroupChat |
|------|---------------|-------------------|
| アーキテクチャ | Executor + Edge | フェーズベース |
| エッジ定義 | 明示的 | 暗黙的 |
| 責任分離 | ⭐⭐⭐ 高 | ⭐⭐ 中 |
| 拡張性 | ⭐⭐⭐ 高 | ⭐⭐ 中 |
| 理解しやすさ | ⭐⭐⭐ 高 | ⭐⭐ 中 |

### HandoffWorkflow との比較

| 項目 | GraphExecutor | Handoff |
|------|---------------|---------|
| アーキテクチャ | Executor + Edge | Handoff ベース |
| 動的分岐 | ✅ Yes | ❌ No |
| 並列実行 | ✅ Yes | ❌ No |
| 制御の明示性 | ⭐⭐⭐ 高 | ⭐ 低 |

## 使用方法

### ビルド

```bash
cd /home/runner/work/AskAI/AskAI
dotnet build
```

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
Contract Executor 完了
Supplier Executor 完了

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
ステップ 3: Aggregator Executor - 意見を集約
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

【最終回答】
## 結論
...

## 根拠
...

## 各専門家の所見
...

## 推奨アクション
...
```

## 今後の拡張可能性

このアーキテクチャは以下の拡張に適しています：

### 1. 条件分岐エッジ

```csharp
// Quality Score に基づく条件分岐
if (qualityScore < 80)
{
    // 再試行エッジ
    await ExecuteRetryAsync(...);
}
else
{
    // 成功エッジ
    await ExecuteFinalizeAsync(...);
}
```

### 2. ループエッジ

```csharp
// 最大3回までリトライ
for (int i = 0; i < 3; i++)
{
    var result = await ExecuteWithRetryAsync(...);
    if (result.IsSuccess) break;
}
```

### 3. HITL Executor の統合

```csharp
// Human-In-The-Loop Executor
if (requiresHumanApproval)
{
    var approval = await ExecuteHITLAsync(...);
    if (!approval) return;
}
```

### 4. Quality Gate Executor

```csharp
// 品質チェック Executor
var qualityResult = await ExecuteQualityGateAsync(...);
if (!qualityResult.PassedThreshold)
{
    // 再処理エッジ
    await ExecuteReprocessAsync(...);
}
```

### 5. キャッシュ Executor

```csharp
// キャッシュチェック
var cachedResult = await ExecuteCacheLookupAsync(...);
if (cachedResult != null)
{
    return cachedResult; // ショートカットエッジ
}
```

## 技術的な詳細

### OpenTelemetry による観測性

各 Executor の実行は Activity として記録され、分散トレーシングが可能です：

```csharp
Activity.Current = activitySource.StartActivity("RouterExecutor");
Activity.Current?.SetTag("executor.type", "router");
Activity.Current?.SetTag("question", question);
// ... 実行 ...
Activity.Current?.SetTag("selected.count", selected.Count);
```

### エラーハンドリング

各 Executor と Edge でエラーハンドリングを実装：

```csharp
try
{
    // Executor を実行
}
catch (Exception ex)
{
    logger.LogError(ex, "Executor エラー");
    activity?.SetTag("error", true);
    activity?.SetTag("error.message", ex.Message);
    // フォールバック処理
}
```

### タイムアウト管理

各 Executor で適切なタイムアウトを設定：

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await executor.RunAsync(..., cancellationToken: cts.Token);
```

## まとめ

GraphExecutorWorkflow は、Agent Framework の Executor と Edge の概念を明示的に実装したワークフローです。

### 主要な成果

1. ✅ **Router → Specialists → Aggregator** のフローを実装
2. ✅ 明示的な **Executor** の定義（Router, Specialists, Aggregator）
3. ✅ 明示的な **Edge** の実装（動的分岐、結合、変換）
4. ✅ **並列実行** による効率化
5. ✅ **観測性** の向上（OpenTelemetry）
6. ✅ **拡張性** の高い設計

### 適用シーン

- マルチエージェントワークフロー
- 複雑な条件分岐を持つフロー
- 動的な実行パスが必要なシステム
- 高い観測性が求められるシステム
- 段階的な拡張が想定されるシステム

### 推奨事項

このアーキテクチャは以下のような要件に最適です：

- ✅ 複数の専門家エージェントの協調
- ✅ 動的な専門家選抜
- ✅ 並列実行による効率化
- ✅ 明確な責任分離
- ✅ 段階的な機能拡張

---

**実装日**: 2025-10-13  
**プロジェクト**: src/GraphExecutorWorkflow  
**ステータス**: 実装完了・ビルド成功
