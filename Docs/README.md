# AskAI ドキュメント

このディレクトリには、AskAI プロジェクトに関する包括的なドキュメントが含まれています。

## 📚 ドキュメント構成

### 🤖 Copilot Agent 向け

開発作業を行う Copilot Agent が参照すべき主要なコンテキスト情報です。

- **[COPILOT_CONTEXT.md](COPILOT_CONTEXT.md)**
  - プロジェクト概要
  - クリーンアーキテクチャの概要
  - ロギングとテレメトリ
  - ライブラリとバージョン情報
  - フォルダ構成
  - エージェント実装パターン
  - 設定とデプロイ
  - コーディング規約

### 🏗️ Architecture (アーキテクチャ)

システムの設計と構造に関するドキュメントです。

- **[clean-architecture.md](architecture/clean-architecture.md)**
  - クリーンアーキテクチャの詳細説明
  - レイヤー構成と責任
  - 依存関係の管理
  - テスト戦略
  - エラーハンドリング戦略
  - 拡張性の確保

- **[system-requirements.md](architecture/system-requirements.md)**
  - システム要件定義
  - 機能要件と非機能要件
  - システム構成概要
  - 基本設計
  - 実装状況

### 💻 Development (開発ガイド)

開発者向けの実践的なガイドです。

- **[logging-setup.md](development/logging-setup.md)**
  - OpenTelemetry 統合
  - Aspire Dashboard の使用方法
  - Application Insights の設定
  - ログレベルとログメッセージ
  - プロジェクト別の実装
  - トラブルシューティング

- **[coding-standards.md](development/coding-standards.md)**
  - コメントとログ表示形式の標準化
  - フェーズ区切りの標準形式
  - ログメッセージの標準形式
  - 構造化ロギングのベストプラクティス
  - 絵文字と区切り記号の使用ガイドライン
  - 文字エンコーディング

### 🔄 Workflows (ワークフロー詳細)

各ワークフローの実装詳細と調査結果です。

- **[implementation-summary.md](workflows/implementation-summary.md)**
  - SelectiveGroupChatWorkflow の実装概要
  - Issue の背景と解決策
  - アーキテクチャと実装詳細
  - 既存ワークフローとの比較

- **[task-workflow.md](workflows/task-workflow.md)**
  - TaskBasedWorkflow の実装概要
  - Domain モデル (TaskItem, TaskStatus, TaskBoard)
  - 3フェーズのワークフロー
  - 既存ワークフローとの比較

- **[graph-executor-investigation.md](workflows/graph-executor-investigation.md)**
  - Graph Executor ワークフローの詳細調査
  - Executor と Edge の概念
  - 実装詳細と技術仕様
  - 今後の拡張可能性

- **[graph-executor-summary.md](workflows/graph-executor-summary.md)**
  - Graph Executor の実装サマリー
  - 成果物と要件達成状況
  - 技術的特徴
  - 既存ワークフローとの比較

## 📖 各ワークフローの README

詳細な使用方法とアーキテクチャは、各ワークフローのディレクトリ内の README を参照してください：

- [DynamicGroupChatWorkflow](../src/DynamicGroupChatWorkflow/README.md) - 動的選抜 + HITL
- [TaskBasedWorkflow](../src/TaskBasedWorkflow/README.md) - タスクベースワークフロー
- [SelectiveGroupChatWorkflow](../src/SelectiveGroupChatWorkflow/README.md) - 選択的グループチャット
- [GraphExecutorWorkflow](../src/GraphExecutorWorkflow/README.md) - グラフエグゼキュータ
- [HandoffWorkflow](../src/HandoffWorkflow/) - ハンドオフワークフロー
- [GroupChatWorkflow](../src/GroupChatWorkflow/) - グループチャットワークフロー

## 🚀 クイックスタート

### 新しい開発者向け

1. **[COPILOT_CONTEXT.md](COPILOT_CONTEXT.md)** を読んで、プロジェクト全体の概要を理解する
2. **[clean-architecture.md](architecture/clean-architecture.md)** でアーキテクチャの詳細を学ぶ
3. **[logging-setup.md](development/logging-setup.md)** でローカル開発環境をセットアップする
4. **[coding-standards.md](development/coding-standards.md)** でコーディング規約を確認する
5. 興味のあるワークフローの README を読んで実装を理解する

### Copilot Agent 向け

1. **[COPILOT_CONTEXT.md](COPILOT_CONTEXT.md)** を最初に読む
2. 必要に応じて、architecture/ や development/ 配下の詳細ドキュメントを参照
3. 特定のワークフローに関する作業の場合は、workflows/ 配下のドキュメントを参照

## 📝 ドキュメント更新ガイドライン

ドキュメントを更新する際は、以下のガイドラインに従ってください：

### 更新すべき場合

- 新しいワークフローを追加した場合
- アーキテクチャに重要な変更を加えた場合
- ライブラリのバージョンを更新した場合
- 新しい開発ガイドラインを追加した場合

### ドキュメントの配置

- **Copilot 向けコンテキスト**: `Docs/COPILOT_CONTEXT.md`
- **アーキテクチャ関連**: `Docs/architecture/`
- **開発ガイド**: `Docs/development/`
- **ワークフロー詳細**: `Docs/workflows/`
- **個別ワークフローの使用方法**: `src/{ワークフロー名}/README.md`

### 文書形式

- Markdown 形式を使用
- UTF-8 エンコーディング
- 日本語での記述を推奨（技術用語は英語可）
- 見出しレベルを適切に使用
- コードブロックには言語を指定

## 🔗 関連リンク

### プロジェクトルート
- [README.md](../README.md) - プロジェクトのメインドキュメント

### 外部リソース
- [Microsoft Agent Framework](https://learn.microsoft.com/ja-jp/dotnet/ai/quickstarts/quickstart-ai-chat-with-agents)
- [Azure OpenAI サービス](https://learn.microsoft.com/ja-jp/azure/ai-services/openai/)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)
- [.NET Aspire Dashboard](https://learn.microsoft.com/ja-jp/dotnet/aspire/fundamentals/dashboard)

## 📞 サポート

ドキュメントに関する質問や提案がある場合は、Issues を作成してください。
