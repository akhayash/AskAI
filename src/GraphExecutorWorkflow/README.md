# GraphExecutorWorkflow

## 概要

このワークフローは、Agent Framework の Executor と Edge の概念を使用したグラフベースのワークフロー実装です。

## アーキテクチャ

```
┌─────────────────────────────────────────────────────────────┐
│                    Graph-Based Workflow                     │
└─────────────────────────────────────────────────────────────┘

   ユーザー質問
        │
        ↓
┌──────────────────┐
│ Router Executor  │  ← ステップ 1: 専門家を特定
│                  │
│ - 質問を分析     │
│ - 専門家を選抜   │
│ - JSON で出力    │
└──────────────────┘
        │
        ├─────────────────┐
        ↓                 ↓
┌──────────────┐   ┌──────────────┐
│ Specialist   │   │ Specialist   │  ← ステップ 2: 意見生成（並列）
│ Executor 1   │   │ Executor N   │
│              │   │              │
│ - 専門分野の │   │ - 専門分野の │
│   観点から   │   │   観点から   │
│ - 意見を生成 │   │ - 意見を生成 │
└──────────────┘   └──────────────┘
        │                 │
        └────────┬────────┘
                 ↓
        ┌──────────────────┐
        │ Aggregator       │  ← ステップ 3: 意見を集約
        │ Executor         │
        │                  │
        │ - 意見を統合     │
        │ - 構造化出力     │
        │ - 最終回答       │
        └──────────────────┘
                 │
                 ↓
            最終回答
```

## 主要コンポーネント

### 1. Router Executor
- **役割**: ユーザーの質問を分析し、必要な専門家を選抜
- **入力**: ユーザー質問
- **出力**: 選抜された専門家のリスト（JSON形式）
- **エッジ**: Router → Specialists（動的分岐）

### 2. Specialist Executors
- **役割**: 各専門領域から意見を生成
- **種類**: 
  - Contract Executor: 契約関連の専門家
  - Spend Executor: 支出分析の専門家
  - Negotiation Executor: 交渉戦略の専門家
  - Sourcing Executor: 調達戦略の専門家
  - Knowledge Executor: 知識管理の専門家
  - Supplier Executor: サプライヤー管理の専門家
- **入力**: ユーザー質問
- **出力**: 専門領域からの意見
- **実行**: 並列実行（効率化）
- **エッジ**: Specialists → Aggregator（結合）

### 3. Aggregator Executor
- **役割**: 各専門家の意見を統合し、構造化された最終回答を生成
- **入力**: 各専門家の意見
- **出力**: 構造化された最終回答
  - 結論
  - 根拠
  - 各専門家の所見
  - 推奨アクション

## エッジ定義

このワークフローでは、明示的なエッジ（フロー）が定義されています：

1. **Router → Specialists**: 動的分岐エッジ
   - Router の選抜結果に基づいて、実行する Specialist を動的に決定
   - 不要な Executor は実行されない（コスト最適化）

2. **Specialists → Aggregator**: 結合エッジ
   - 複数の Specialist の出力を集約
   - 並列実行された結果を待ち合わせ

## 特徴

### ✅ Executor パターンの実装
- 各コンポーネントを独立した Executor として実装
- 責任の明確な分離
- 再利用可能な設計

### ✅ グラフベースのフロー
- 明示的なエッジ定義
- 動的な実行パス
- 条件分岐のサポート

### ✅ 並列実行
- Specialist Executors の並列実行
- 応答時間の短縮
- リソースの効率的な利用

### ✅ 観測性
- OpenTelemetry による完全なトレーシング
- 各 Executor の実行時間を計測
- エラー追跡とデバッグ支援

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
