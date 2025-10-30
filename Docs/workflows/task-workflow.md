# TaskBasedWorkflow Implementation Summary

## Issue Background

Issue: Taskベースのワークフロー、新規プロジェクト

新規のプロジェクトとして、タスクベースのワークフローを作成しました。

### 要件
- Planner がタスク一覧を作成し、誰がやるかを決める
- Worker (GroupChat で使っている専門家) がタスクを実行する
- 動的プランニングと専門エージェントによる問題解決を試行
- TaskBoard, TaskItem などの domain モデルを含む

## 実装内容

### 新規作成ファイル

1. **src/TaskBasedWorkflow/TaskBasedWorkflow.csproj**
   - .NET 8 コンソールアプリケーション
   - 必要な NuGet パッケージを追加

2. **src/TaskBasedWorkflow/Program.cs** (約370行)
   - Domain モデル (TaskItem, TaskStatus, TaskBoard)
   - Planner Agent の実装
   - Worker Agent の実装 (6つの専門家)
   - 3フェーズのワークフロー:
     - フェーズ 1: Planner がタスク計画を作成
     - フェーズ 2: Worker がタスクを順次実行
     - フェーズ 3: 結果の統合と最終レポート

3. **src/TaskBasedWorkflow/README.md**
   - 詳細な使用方法
   - アーキテクチャ図
   - ドメインモデルのドキュメント
   - 既存ワークフローとの比較

4. **src/TaskBasedWorkflow/appsettings.json**
   - Azure OpenAI の設定

5. **src/TaskBasedWorkflow/appsettings.Development.json**
   - 開発環境用の設定

### 更新ファイル

1. **AgentWorkflows.sln**
   - TaskBasedWorkflow プロジェクトを追加

2. **README.md**
   - TaskBasedWorkflow の説明を追加
   - ワークフロー比較表を更新 (4つのワークフロー対応)
   - 実行方法を追加

## アーキテクチャ

### ワークフローの流れ

```
User (目標入力)
  ↓
Planner Agent (タスク計画作成)
  ↓
TaskBoard (タスク管理)
  ↓
Worker Agents (タスク実行)
  - Contract Agent
  - Spend Agent
  - Negotiation Agent
  - Sourcing Agent
  - Knowledge Agent
  - Supplier Agent
  ↓
Final Report (結果統合)
```

### Domain モデル

#### TaskStatus (enum)
- Queued: 実行待ち
- Doing: 実行中
- Done: 完了
- Blocked: ブロック (エラー)

#### TaskItem (record)
- Id: タスクID
- Description: タスクの説明
- Acceptance: 受け入れ基準
- Status: タスクの状態
- AssignedTo: 担当専門家
- Notes: 備考

#### TaskBoard (class)
- TaskId: ボードID
- Objective: 目標
- Tasks: タスクリスト
- UpdateTaskStatus(): タスク状態を更新
- AssignTask(): タスクを割り当て

## 主な特徴

### 1. 動的タスク計画
Planner が目標を分析し、必要なタスクを自動生成します。各タスクに最適な専門家を割り当てます。

### 2. タスクボード管理
タスクの状態を追跡し、進捗を可視化します。

### 3. 順次実行
タスクは計画された順序で順次実行され、各タスクの結果が記録されます。

### 4. 専門家の再利用
既存の6つの専門家エージェント (Contract, Spend, Negotiation, Sourcing, Knowledge, Supplier) を活用します。

### 5. エラーハンドリング
- Planner エラー時: デフォルトタスクを作成
- Worker エラー時: タスクを Blocked 状態に更新
- タイムアウト保護: 各フェーズで30秒のタイムアウト

## 既存ワークフローとの比較

| 特徴 | TaskBased | SelectiveGroupChat | Handoff | GroupChat |
|-----|-----------|-------------------|---------|-----------|
| 専門家選抜 | ✅ Plannerが割当 | ✅ 動的選抜 | ❌ 全員利用可能 | ❌ 全員参加 |
| タスク管理 | ✅ タスクボード | ❌ なし | ❌ なし | ❌ なし |
| 並列実行 | ❌ 順次実行 | ✅ あり | ❌ なし | ❌ なし |
| 統合機能 | ✅ 最終レポート | ✅ Moderator | Router | なし |
| 計画性 | ⭐⭐⭐ | ⭐ | ⭐⭐ | ⭐ |
| コスト効率 | ⭐⭐ | ⭐⭐⭐ | ⭐⭐ | ⭐ |
| 応答時間 | ⭐⭐ | ⭐⭐⭐ | ⭐⭐ | ⭐ |
| 対話能力 | ⭐⭐ | ⭐⭐ | ⭐⭐⭐ | ⭐⭐ |
| 適用場面 | 複雑な目標の段階的達成 | 効率的な専門家活用 | 専門家間の対話が必要 | 全員の意見が必要 |

## 使用方法

### 前提条件
- .NET 8.0 SDK
- Azure OpenAI アクセス
- 環境変数:
  - `AZURE_OPENAI_ENDPOINT`
  - `AZURE_OPENAI_DEPLOYMENT_NAME`
  - Azure CLI 認証 (`az login`)

### 実行方法

```bash
cd src/TaskBasedWorkflow
dotnet run
```

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
    ...
  ]
}

✓ 3 個のタスクを作成しました

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
フェーズ 2: Worker がタスクを実行
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
...
```

## ビルドステータス

✅ すべてのプロジェクトが正常にビルドされます
✅ コンパイルエラーなし
✅ 警告なし
✅ ソリューション構造が正常

```bash
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.17
```

## 技術スタック

- **.NET 8**: アプリケーションフレームワーク
- **Microsoft.Agents.AI.Workflows 1.0.0-preview.251009.1**: ワークフロー管理
- **Microsoft.Extensions.AI 9.9.1**: AI 統合
- **Azure.AI.OpenAI 2.1.0**: Azure OpenAI クライアント
- **Azure.Identity 1.12.0**: Azure 認証

## 将来の拡張可能性

- [ ] タスクの並列実行サポート
- [ ] タスク間の依存関係管理
- [ ] HITL (Human-In-The-Loop) 統合
- [ ] タスクの優先順位付け
- [ ] チェックポイントと再開機能
- [ ] タスク実行履歴の永続化
- [ ] タスク実行メトリクスの収集
- [ ] 動的な専門家の追加/削除

## まとめ

TaskBasedWorkflow は、以下の特徴を持つ新しいワークフローパターンです:

1. **構造化されたアプローチ**: Planner によるタスク分解と専門家への割り当て
2. **透明性**: タスクボードによる進捗の可視化
3. **柔軟性**: 既存の専門家エージェントを活用
4. **拡張性**: 将来の機能追加に対応可能な設計

複雑な目標を段階的に達成する必要がある場合に最適なワークフローです。
