# TaskBasedWorkflow

タスクベースのワークフローシステムです。Planner が目標を分析してタスク計画を作成し、専門家 Worker がタスクを実行していきます。

## 概要

TaskBasedWorkflow は、以下の3つのフェーズで構成されています:

1. **Planner フェーズ**: 目標を分析し、タスク計画（タスク一覧と担当者）を作成
2. **Worker フェーズ**: 各専門家が割り当てられたタスクを順次実行
3. **統合フェーズ**: タスクの実行結果を統合して最終報告を表示

```
User → Planner (計画作成) → Workers (タスク実行) → Final Report
```

## 主な特徴

### 1. 動的タスク計画

Planner が目標を分析し、必要なタスクを自動生成します。各タスクには以下が含まれます:
- タスクID
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

以下の6つの専門家が利用可能です:
- **Contract**: 契約関連の専門家
- **Spend**: 支出分析の専門家
- **Negotiation**: 交渉戦略の専門家
- **Sourcing**: 調達戦略の専門家
- **Knowledge**: 知識管理の専門家
- **Supplier**: サプライヤー管理の専門家

### 4. 順次実行

タスクは計画された順序で順次実行され、進捗が可視化されます。

## アーキテクチャ

```
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
┌─────────────────┐
│  Final Report   │
│   (結果統合)    │
└─────────────────┘
```

## 使用方法

### 前提条件

- .NET 8.0 SDK
- Azure OpenAI アクセス
- 環境変数の設定:
  - `AZURE_OPENAI_ENDPOINT`
  - `AZURE_OPENAI_DEPLOYMENT_NAME`
  - Azure CLI 認証 (`az login`)

### 実行

```bash
cd src/TaskBasedWorkflow
dotnet run
```

実行後、目標を入力するプロンプトが表示されます。

### 実行例

```
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

- 各エージェントは独立したワークフローとして実行
- タスクは順次実行され、状態が逐次更新される
- 各フェーズの結果がコンソールに表示される

## 既存ワークフローとの比較

| ワークフロー | 特徴 | 適用場面 |
|-----------|------|---------|
| **TaskBasedWorkflow** | 動的計画 + タスク管理 | 複雑な目標を段階的に達成 |
| **SelectiveGroupChatWorkflow** | 動的選抜 + 並列実行 | 効率的な専門家活用 |
| **HandoffWorkflow** | ハンドオフ方式 | 専門家間の対話が必要 |
| **GroupChatWorkflow** | ラウンドロビン方式 | 全専門家の意見が必要 |

## 将来の拡張

- [ ] タスクの並列実行サポート
- [ ] タスク間の依存関係管理
- [ ] HITL (Human-In-The-Loop) 統合
- [ ] タスクの優先順位付け
- [ ] チェックポイントと再開機能
- [ ] タスク実行履歴の保存
