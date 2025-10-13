# TaskBasedWorkflow

タスクベースのワークフローシステムです。Planner が目標を分析してタスク計画を作成し、専門家 Worker がタスクを実行し、サマリーエージェントが最終回答をまとめます。

## 概要

TaskBasedWorkflow は、以下の 3 つのフェーズで構成されています:

1. **Planner フェーズ**: 目標を分析し、タスク計画（タスク一覧と担当者）を作成
2. **Worker フェーズ**: 各専門家が割り当てられたタスクを順次実行
3. **Summary フェーズ**: サマリーエージェントがタスク結果を統合し、構造化された最終回答を生成

```
User → Planner (計画作成) → Workers (タスク実行) → Summary Agent (統合サマリー)
```

## 主な特徴

### 1. 動的タスク計画

Planner が目標を分析し、必要なタスクを自動生成します。各タスクには以下が含まれます:

- タスク ID
- タスクの説明
- 受け入れ基準
- 担当専門家

### 2. タスクボード

タスクの状態を管理するタスクボードが実装されています:

- **Queued**: 実行待ち
- **Doing**: 実行中
- **Done**: 完了
- **Blocked**: ブロック（エラー）

### 3. 専門家 Worker

以下の 6 つの専門家が利用可能です:

- **Contract**: 契約関連の専門家
- **Spend**: 支出分析の専門家
- **Negotiation**: 交渉戦略の専門家
- **Sourcing**: 調達戦略の専門家
- **Knowledge**: 知識管理の専門家
- **Supplier**: サプライヤー管理の専門家

### 4. 順次実行

タスクは計画された順序で順次実行され、進捗が可視化されます。

### 5. 構造化サマリー

すべてのタスクが完了すると、サマリーエージェントが以下のテンプレートで統合レポートを生成します。

- `### 最終回答` で質問に対する直接的な回答を箇条書きで提示
- `### 要約`、`### 詳細`、`### 主要リスク`、`### 推奨アクション` で要点を整理
- `### 参考回答` に各タスクの回答全文をコードブロックで添付

### 6. OpenTelemetry 連携

ログとトレースを OpenTelemetry で収集します。OTLP エンドポイント（デフォルト `http://localhost:4317`）とコンソールに送信され、各エージェント実行は `Activity` として記録されます。

## アーキテクチャ

```text
┌─────────────┐
│    User     │
│  (目標入力) │
└──────┬──────┘
       │
       ▼
┌─────────────────┐
│  Planner Agent  │
│ (タスク計画作成) │
└──────┬──────────┘
       │
       ▼
┌─────────────────┐
│   TaskBoard     │
│  (タスク管理)   │
└──────┬──────────┘
       │
       ▼
┌─────────────────────────────┐
│     Worker Agents           │
│ ┌────────┐ ┌────────┐      │
│ │Contract│ │ Spend  │ ...  │
│ └────────┘ └────────┘      │
└──────┬──────────────────────┘
       │
       ▼
┌─────────────────────┐
│   Summary Agent     │
│  (統合サマリー生成)  │
└──────┬────────────┘
       │
       ▼
┌────────────────────────┐
│ Structured Final Report│
│   (最終回答出力)        │
└────────────────────────┘
```

## 使用方法

### 前提条件

- .NET 8.0 SDK
- Azure OpenAI アクセス
- 環境変数の設定:
  - `AZURE_OPENAI_ENDPOINT`
  - `AZURE_OPENAI_DEPLOYMENT_NAME`
  - Azure CLI 認証 (`az login`)

環境変数は `appsettings.json` / `appsettings.Development.json` の `environmentVariables` セクションに記載することもできます。

### 実行

```bash
cd src/TaskBasedWorkflow
dotnet run
```

実行後、目標を入力するプロンプトが表示されます。`exit` を入力すると終了します。

### 実行例（抜粋）

```text
質問を入力してください (終了するには 'exit' と入力):
> 新しいサプライヤーとの契約を締結したい

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
フェーズ 1: Planner がタスク計画を作成
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Planner の計画]
{
  "tasks": [
    {
      "id": "task-1",
      "description": "サプライヤーの評価と選定",
      "acceptance": "評価基準に基づき適切なサプライヤーを選定",
      "assignedTo": "Supplier"
    },
    {
      "id": "task-2",
      "description": "契約条件の策定",
      "acceptance": "契約書のドラフトを作成",
      "assignedTo": "Contract"
    },
    ...
  ]
}

✓ 3 個のタスクを作成しました

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
フェーズ 2: Worker がタスクを実行
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
...

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
フェーズ 3: 結果の統合
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

統合要約（抜粋）:

> ### 最終回答
> - サプライヤーのオンボーディングプロセスは、段階的なフレームワークを整備し、評価基準を設定し、交渉と契約を完了することで確立できます。
> - 各サプライヤーとの契約締結後に必要情報を整理し、オンボーディング実行の準備を整えます。
> - プロセスを継続的にモニタリングして改善し、効率を維持します。
>
> ### 要約
> - ...
> - ...
> - ...
>
> ### 詳細
> - task-1 (担当: Knowledge): ...
> - task-2 (担当: Sourcing): ...
>
> ### 主要リスク
> - ...
>
> ### 推奨アクション
> - ...
>
> ### 参考回答
> - task-1: （実際の出力では回答全文をコードブロックで表示）
> - ...
```

## ドメインモデル

### TaskItem

```csharp
public record TaskItem(
    string Id,
    string Description,
    string Acceptance,
    TaskStatus Status = TaskStatus.Queued,
    string? AssignedTo = null,
    string? Notes = null
);
```

### TaskStatus

```csharp
public enum TaskStatus { Queued, Doing, Done, Blocked }
```

### TaskBoard

```csharp
public class TaskBoard
{
    public string TaskId { get; init; }
    public string Objective { get; set; }
    public List<TaskItem> Tasks { get; set; }

    public void UpdateTaskStatus(string taskId, TaskStatus newStatus, string? notes = null)
    public void AssignTask(string taskId, string worker)
}
```

## 技術詳細

### 使用技術

- **Microsoft.Agents.AI.Workflows**: ワークフローエンジン
- **Microsoft.Extensions.AI**: AI 統合
- **Azure.AI.OpenAI**: Azure OpenAI クライアント
- **Azure.Identity**: Azure 認証

### エラーハンドリング

- Planner のエラー時はデフォルトタスクを作成
- Worker のエラー時はタスクを Blocked 状態に更新
- タイムアウト設定により長時間の実行を防止

### ワークフローの制御

- 各エージェントは `AgentWorkflowBuilder` を用いた独立ワークフローとして実行
- タスクは計画順に順次実行され、状態更新はロガー経由で出力
- Planner／各 Worker／Summary それぞれに `Activity` を割り当て、OTLP へトレース送信

## 既存ワークフローとの比較

| ワークフロー                    | 特徴                                   | 適用場面                 |
| ------------------------------- | -------------------------------------- | ------------------------ |
| **TaskBasedWorkflow**           | 動的計画 + タスク管理 + 構造化サマリー | 複雑な目標を段階的に達成 |
| **SelectiveGroupChatWorkflow**  | 動的選抜 + 並列実行                    | 効率的な専門家活用       |
| **DynamicGroupChatWorkflow** 🆕 | 動的選抜 + HITL                        | ユーザー入力が必要な場合 |
| **HandoffWorkflow**             | ハンドオフ方式                         | 専門家間の対話が必要     |
| **GroupChatWorkflow**           | ラウンドロビン方式                     | 全専門家の意見が必要     |

## 将来の拡張

- [ ] タスクの並列実行サポート
- [ ] タスク間の依存関係管理
- [ ] HITL (Human-In-The-Loop) 統合
- [ ] タスクの優先順位付け
- [ ] チェックポイントと再開機能
- [ ] タスク実行履歴の保存
