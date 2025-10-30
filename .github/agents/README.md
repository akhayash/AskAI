# GitHub Copilot カスタムエージェント

このディレクトリには、AskAIリポジトリ用のGitHub Copilotカスタムエージェント定義が含まれています。

## カスタムエージェント一覧

### 1. agent-framework-expert
**Microsoft Agent Framework専門家**

Microsoft.Agents.AI.Workflowsライブラリを使用したエージェントシステムの設計と実装に特化したエージェント。

**専門分野:**
- Microsoft.Agents.AI.Workflows APIの使用法
- Executor/Edge/Workflowグラフの設計と実装
- ワークフローパターン（GroupChat、Handoff、SelectiveGroupChat、TaskBased、DynamicGroupChat）
- エージェント間通信とハンドオフメカニズム
- HITL (Human-in-the-Loop) の実装
- Structured Outputによる厳格なレスポンス生成

**使用例:**
```
@agent-framework-expert 新しいワークフローパターンを実装したい
@agent-framework-expert エージェント間のハンドオフロジックを改善したい
```

### 2. test-expert
**.NETテスティング専門家**

.NET 8/C#アプリケーションのテスト戦略と実装に特化したエージェント。

**専門分野:**
- xUnit/NUnit/MSTestフレームワーク
- モックとスタブの実装（Moq、NSubstitute）
- 統合テスト戦略
- テストカバレッジの向上
- CI/CDパイプラインでのテスト自動化
- Azure OpenAI APIのモックとテストダブル
- 非同期コードのテスト戦略

**使用例:**
```
@test-expert このワークフローの単体テストを作成したい
@test-expert Azure OpenAI APIのモックを作成したい
```

### 3. documentation-expert
**技術ドキュメント専門家**

日本語・英語の技術ドキュメント作成とメンテナンスに特化したエージェント。

**専門分野:**
- 技術ドキュメントの構造設計と執筆
- README.mdの作成とベストプラクティス
- API仕様書とリファレンスドキュメント
- システム要件定義と基本設計書
- Mermaidダイアグラムによる図表作成
- コードコメントの標準化

**使用例:**
```
@documentation-expert このワークフローのREADMEを更新したい
@documentation-expert システム要件定義書を作成したい
```

### 4. dotnet-architecture-expert
**.NETアーキテクチャ専門家**

.NET 8/C#のアーキテクチャ設計とベストプラクティスに特化したエージェント。

**専門分野:**
- .NET 8の最新機能と最適な使用法
- C#言語機能とモダンな記述パターン
- 依存性注入（DI）とIoCコンテナ
- 構成管理（IConfiguration、appsettings.json）
- ロギングとテレメトリ（ILogger、OpenTelemetry）
- 非同期プログラミング（async/await、Task）
- パフォーマンス最適化とメモリ管理

**使用例:**
```
@dotnet-architecture-expert このコードのパフォーマンスを改善したい
@dotnet-architecture-expert 依存性注入のパターンを見直したい
```

### 5. azure-openai-expert
**Azure OpenAI専門家**

Azure OpenAI ServiceとMicrosoft.Extensions.AIの実装に特化したエージェント。

**専門分野:**
- Azure OpenAI Serviceの設定と使用法
- Microsoft.Extensions.AIライブラリの活用
- ChatCompletionとストリーミングレスポンス
- プロンプトエンジニアリングとシステムプロンプト設計
- トークン管理とコスト最適化
- レート制限とリトライ戦略
- Azure.Identityによる認証（DefaultAzureCredential）
- 関数呼び出し（Function Calling）とツール使用
- OpenTelemetryによるテレメトリ収集

**使用例:**
```
@azure-openai-expert プロンプトを最適化したい
@azure-openai-expert トークン使用量を削減したい
@azure-openai-expert ChatCompletionのエラーハンドリングを改善したい
```

## カスタムエージェントの使い方

### GitHub Copilot Chat（VS Code / Visual Studio）
```
@カスタムエージェント名 質問や依頼内容
```

### GitHub Copilot CLI
```
gh copilot suggest --agent カスタムエージェント名 "質問や依頼内容"
```

### Coding Agent
Coding Agentは、これらのカスタムエージェントを自動的に認識し、適切なタスクに対して最適なエージェントを選択します。

## カスタムエージェントの追加・変更

新しいカスタムエージェントを追加する場合、または既存のエージェントを変更する場合：

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

tools:
  - "*"  # 全てのツールを有効化
```

3. このREADMEを更新してエージェントの説明を追加
4. コミットしてプッシュ

## 参考リンク

- [GitHub Copilot カスタムエージェント公式ドキュメント](https://docs.github.com/en/copilot/reference/custom-agents-configuration)
- [カスタムエージェント作成ガイド](https://github.com/github/docs/blob/main/content/copilot/how-tos/use-copilot-agents/coding-agent/create-custom-agents.md)
- [Microsoft Agent Framework ドキュメント](https://github.com/microsoft/Agents-for-net)
