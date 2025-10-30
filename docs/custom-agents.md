# GitHub Copilot カスタムエージェント詳細ガイド

## 概要

このドキュメントでは、AskAIリポジトリに定義されているGitHub Copilotカスタムエージェントの詳細と使用方法について説明します。

## カスタムエージェントとは

GitHub Copilotカスタムエージェントは、特定のドメインや技術領域に特化したAIアシスタントです。リポジトリの`.github/agents/`ディレクトリにYAML形式で定義することで、Copilotがその専門知識を活用できるようになります。

## 定義されているカスタムエージェント

### 1. agent-framework-expert (Agent Framework専門家)

**ファイル:** `.github/agents/agent-framework-expert.yml`

**目的:**  
Microsoft Agent Frameworkを使用したエージェントシステムの設計・実装を支援します。

**専門分野:**
- Microsoft.Agents.AI.Workflows APIの使用法
- Executor/Edge/Workflowグラフの設計と実装
- ワークフローパターン（GroupChat、Handoff、SelectiveGroupChat、TaskBased、DynamicGroupChat）
- エージェント間通信とハンドオフメカニズム
- HITL (Human-in-the-Loop) の実装
- エージェントのオーケストレーション
- Structured Outputによる厳格なレスポンス生成
- エージェントのエラー処理とフォールバック戦略

**使用シーン:**
- 新しいワークフローパターンの実装
- エージェント設計の改善
- ハンドオフロジックの最適化
- HITLの実装
- エラーハンドリングの強化

### 2. test-expert (テスト専門家)

**ファイル:** `.github/agents/test-expert.yml`

**目的:**  
.NET 8/C#アプリケーションのテスト戦略と実装を支援します。

**専門分野:**
- xUnit/NUnit/MSTestフレームワークの使用法
- モックとスタブの実装（Moq、NSubstitute）
- テストダブルとテストフィクスチャの設計
- 統合テスト戦略（WebApplicationFactory、TestServerなど）
- テストカバレッジの向上
- テストコードの品質とメンテナンス性
- CI/CDパイプラインでのテスト自動化
- パフォーマンステストと負荷テスト
- Azure OpenAI APIのモックとテストダブル
- 非同期コードのテスト戦略

**使用シーン:**
- ワークフローの単体テストの作成
- Azure OpenAI APIのモック実装
- 統合テストの設計
- テストカバレッジの向上
- CI/CDパイプラインの改善

### 3. documentation-expert (ドキュメント専門家)

**ファイル:** `.github/agents/documentation-expert.yml`

**目的:**  
日本語・英語の技術ドキュメントの作成とメンテナンスを支援します。

**専門分野:**
- 技術ドキュメントの構造設計と執筆
- README.mdの作成とベストプラクティス
- API仕様書とリファレンスドキュメント
- システム要件定義と基本設計書
- Mermaidダイアグラムによる図表作成
- コードコメントの標準化とドキュメンテーション
- 日本語技術文書の品質向上
- ユーザーガイドとチュートリアル作成
- ドキュメントの国際化（i18n）とローカライゼーション
- マークダウンのフォーマットとスタイルガイド

**使用シーン:**
- READMEの更新
- 新機能のドキュメント作成
- システム要件定義書の作成
- Mermaid図の作成
- 日本語ドキュメントの品質向上

### 4. dotnet-architecture-expert (.NETアーキテクチャ専門家)

**ファイル:** `.github/agents/dotnet-architecture-expert.yml`

**目的:**  
.NET 8/C#のアーキテクチャ設計とベストプラクティスを支援します。

**専門分野:**
- .NET 8の最新機能と最適な使用法
- C#言語機能とモダンな記述パターン
- 依存性注入（DI）とIoCコンテナ
- 構成管理（IConfiguration、appsettings.json）
- ロギングとテレメトリ（ILogger、OpenTelemetry）
- 非同期プログラミング（async/await、Task）
- エラーハンドリングとレジリエンスパターン
- パフォーマンス最適化とメモリ管理
- NuGetパッケージ管理とバージョニング
- .NETプロジェクトの構造とソリューション設計

**使用シーン:**
- コードのパフォーマンス改善
- 依存性注入パターンの見直し
- 非同期処理の最適化
- エラーハンドリングの強化
- OpenTelemetryの設定

### 5. azure-openai-expert (Azure OpenAI専門家)

**ファイル:** `.github/agents/azure-openai-expert.yml`

**目的:**  
Azure OpenAI ServiceとMicrosoft.Extensions.AIの実装を支援します。

**専門分野:**
- Azure OpenAI Serviceの設定と使用法
- Microsoft.Extensions.AIライブラリの活用
- Azure.AI.OpenAIクライアントの実装
- ChatCompletionとストリーミングレスポンス
- プロンプトエンジニアリングとシステムプロンプト設計
- トークン管理とコスト最適化
- レート制限とリトライ戦略
- Azure.Identityによる認証（DefaultAzureCredential）
- モデル選択とデプロイメント戦略（gpt-4o、gpt-4o-mini）
- 関数呼び出し（Function Calling）とツール使用
- エラーハンドリングとフォールバック戦略
- OpenTelemetryによるテレメトリ収集

**使用シーン:**
- プロンプトの最適化
- トークン使用量の削減
- ChatCompletionのエラーハンドリング
- レート制限対策
- モデル選択の最適化

## 使用方法

### GitHub Copilot Chat（VS Code / Visual Studio）

エディタのCopilot Chatで、`@エージェント名`を使用してエージェントを呼び出します：

```
@agent-framework-expert 新しいワークフローパターンを実装したい
@test-expert このワークフローの単体テストを作成したい
@documentation-expert READMEを更新したい
@dotnet-architecture-expert このコードのパフォーマンスを改善したい
@azure-openai-expert プロンプトを最適化したい
```

### GitHub Copilot Coding Agent

Coding Agentは、これらのカスタムエージェントを自動的に認識し、タスクの内容に応じて最適なエージェントを選択して実行します。特に指定しなくても、適切な専門家が自動的に活用されます。

### GitHub Copilot CLI

コマンドラインから使用する場合：

```bash
gh copilot suggest --agent agent-framework-expert "新しいワークフローパターンを実装したい"
gh copilot suggest --agent test-expert "このワークフローの単体テストを作成したい"
```

## カスタムエージェントの利点

1. **専門的な知識**: 各エージェントはその分野に特化した知識を持っています
2. **コンテキスト認識**: リポジトリ固有の情報とベストプラクティスを理解しています
3. **一貫性**: プロジェクト全体で一貫したアプローチを提供します
4. **効率性**: 適切なエージェントを選択することで、より正確で効率的な支援が得られます

## カスタムエージェントの追加・変更

新しいカスタムエージェントを追加する場合：

1. `.github/agents/` ディレクトリに新しいYAMLファイルを作成
2. 以下の形式でエージェントを定義：

```yaml
name: "エージェント名"
description: |
  エージェントの説明
  専門分野の詳細な説明
  
  専門分野:
  - 項目1
  - 項目2
  - 項目3

tools:
  - "*"  # 全てのツールを有効化
  # または特定のツールのみ指定
  # - read
  # - edit
  # - search
```

3. `.github/agents/README.md` を更新してエージェントの説明を追加
4. このドキュメント（`docs/custom-agents.md`）にも詳細を追加
5. メインの `README.md` に新しいエージェントを追加

## ベストプラクティス

1. **具体的な質問**: エージェントには具体的な質問や依頼をすることで、より良い結果が得られます
2. **適切なエージェント選択**: タスクの内容に最も適したエージェントを選択します
3. **複数エージェントの活用**: 複雑なタスクでは、複数のエージェントを組み合わせて使用することも効果的です
4. **フィードバック**: エージェントの回答が期待と異なる場合は、質問を具体化して再度試します

## トラブルシューティング

### エージェントが認識されない

- `.github/agents/` ディレクトリにYAMLファイルが正しく配置されているか確認
- YAMLファイルの構文が正しいか確認
- `name` と `description` フィールドが必須であることを確認

### エージェントの応答が期待と異なる

- 質問をより具体的にする
- コンテキストを追加で提供する
- 別のエージェントも試してみる

## 参考リンク

- [GitHub Copilot カスタムエージェント公式ドキュメント](https://docs.github.com/en/copilot/reference/custom-agents-configuration)
- [カスタムエージェント作成ガイド](https://github.com/github/docs/blob/main/content/copilot/how-tos/use-copilot-agents/coding-agent/create-custom-agents.md)
- [Microsoft Agent Framework ドキュメント](https://github.com/microsoft/Agents-for-net)
- [Azure OpenAI Service ドキュメント](https://learn.microsoft.com/azure/ai-services/openai/)
- [.NET 8 ドキュメント](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-8)
