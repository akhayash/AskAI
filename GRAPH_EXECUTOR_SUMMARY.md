# Graph Executor Workflow 実装サマリー

## 実装内容

Issue「graph executor」の要件に対応し、Agent Framework のワークフロー機能を使用した Executor と Edge によるグラフベースのワークフローを実装しました。

## 成果物

### 1. GraphExecutorWorkflow プロジェクト

**場所**: `src/GraphExecutorWorkflow/`

**ファイル構成**:
- `Program.cs`: メイン実装（約500行）
- `README.md`: 詳細ドキュメント
- `GraphExecutorWorkflow.csproj`: プロジェクト設定

**実装内容**:

```
ユーザー質問
    ↓
┌─────────────────┐
│ Router Executor │  ← ステップ 1: 専門家を特定
└─────────────────┘
    ↓ (動的分岐)
┌─────────┐ ┌─────────┐ ┌─────────┐
│Contract │ │  Spend  │ │Supplier │  ← ステップ 2: 意見生成（並列）
│Executor │ │Executor │ │Executor │
└─────────┘ └─────────┘ └─────────┘
    ↓           ↓           ↓
    └───────────┴───────────┘
             ↓ (結合)
    ┌──────────────────┐
    │   Aggregator     │  ← ステップ 3: 意見を集約
    │   Executor       │
    └──────────────────┘
             ↓
         最終回答
```

### 2. ドキュメント

1. **GRAPH_EXECUTOR_INVESTIGATION.md**
   - 詳細な調査結果
   - Executor と Edge の概念説明
   - 実装詳細と技術仕様
   - 他のワークフローとの比較
   - 今後の拡張可能性

2. **src/GraphExecutorWorkflow/README.md**
   - プロジェクトの概要
   - 使用方法
   - アーキテクチャ図
   - 技術詳細

## 要件達成状況

✅ **Router が必要な専門家を特定**
- Router Executor を実装
- JSON 形式で専門家を選抜
- 動的な選抜ロジック

✅ **各専門家からの意見が生成**
- 6種類の Specialist Executor を実装:
  - Contract Executor
  - Spend Executor
  - Negotiation Executor
  - Sourcing Executor
  - Knowledge Executor
  - Supplier Executor
- 並列実行による効率化

✅ **最後に意見を集約し出力**
- Aggregator Executor を実装
- 構造化された最終回答:
  - 結論
  - 根拠
  - 各専門家の所見
  - 推奨アクション

## 技術的特徴

### Executor パターン

各コンポーネントを独立した Executor として実装：

```csharp
// Router Executor
var routerAgent = CreateRouterExecutor(extensionsAIChatClient);
var selectedSpecialists = await ExecuteRouterAsync(routerAgent, question, ...);

// Specialist Executors
var specialistExecutors = CreateSpecialistExecutors(extensionsAIChatClient);
var opinions = await ExecuteSpecialistsAsync(specialistExecutors, selectedSpecialists, ...);

// Aggregator Executor
var aggregatorAgent = CreateAggregatorExecutor(extensionsAIChatClient);
var finalOutput = await ExecuteAggregatorAsync(aggregatorAgent, question, opinions, ...);
```

### Edge 定義

明示的なエッジ（データフロー）を実装：

1. **Router → Specialists**: 動的分岐エッジ
   - Router の出力に基づいて実行する Specialist を決定
   
2. **Specialists → Aggregator**: 結合エッジ
   - 並列実行された結果を待ち合わせて統合

### 観測性

OpenTelemetry による完全なトレーシング：

```csharp
using var activity = activitySource.StartActivity("RouterExecutor", ActivityKind.Internal);
activity?.SetTag("executor.type", "router");
activity?.SetTag("selected.specialists", string.Join(", ", selected));
```

## ビルドと実行

### ビルド

```bash
cd /home/runner/work/AskAI/AskAI
dotnet build
```

**ビルド結果**: ✅ 成功（0 Warning, 0 Error）

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
（構造化された回答が表示される）
```

## 既存ワークフローとの比較

| 特徴 | GraphExecutor | SelectiveGroupChat | Handoff |
|------|---------------|-------------------|---------|
| アーキテクチャ | Executor + Edge | フェーズベース | Handoff ベース |
| エッジ定義 | 明示的 | 暗黙的 | 暗黙的 |
| 動的分岐 | ✅ Yes | ✅ Yes | ❌ No |
| 並列実行 | ✅ Yes | ✅ Yes | ❌ No |
| 責任分離 | ⭐⭐⭐ 高 | ⭐⭐ 中 | ⭐⭐ 中 |
| 拡張性 | ⭐⭐⭐ 高 | ⭐⭐ 中 | ⭐ 低 |
| 理解しやすさ | ⭐⭐⭐ 高 | ⭐⭐ 中 | ⭐⭐ 中 |

## 今後の拡張可能性

この実装は以下の拡張に適しています：

1. **条件分岐エッジ**
   - Quality Score に基づく分岐
   - 動的なルーティング

2. **ループエッジ**
   - リトライロジック
   - 段階的な改善

3. **HITL Executor**
   - 人間の承認プロセス
   - 承認フローの統合

4. **Quality Gate Executor**
   - 品質チェック
   - 閾値ベースの再処理

5. **キャッシュ Executor**
   - 結果のキャッシング
   - パフォーマンス最適化

## まとめ

### 達成事項

✅ Agent Framework のワークフロー機能を調査  
✅ Executor と Edge の概念を明示的に実装  
✅ Router → Specialists → Aggregator のフローを実現  
✅ 並列実行による効率化  
✅ 観測性の向上（OpenTelemetry）  
✅ 拡張性の高い設計  
✅ 包括的なドキュメント作成  

### 技術的価値

1. **明確な責任分離**: 各 Executor が単一の責任を持つ
2. **明示的なフロー**: エッジによる明確なデータフロー
3. **高い保守性**: 独立したコンポーネント
4. **観測可能性**: 完全なトレーシング
5. **拡張性**: 新しい Executor や Edge を容易に追加可能

### 推奨する適用シーン

- マルチエージェントワークフロー
- 複雑な条件分岐を持つフロー
- 動的な実行パスが必要なシステム
- 高い観測性が求められるシステム
- 段階的な拡張が想定されるシステム

---

**実装日**: 2025-10-13  
**ステータス**: ✅ 実装完了・ビルド成功  
**プロジェクト**: src/GraphExecutorWorkflow  
**ドキュメント**: 
- GRAPH_EXECUTOR_INVESTIGATION.md
- src/GraphExecutorWorkflow/README.md
