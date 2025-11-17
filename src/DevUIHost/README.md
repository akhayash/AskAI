# DevUIHost - AGUI Server for AskAI Workflows

DevUIHost は、Microsoft Agent Framework の AGUI プロトコルを使用して、AskAI プロジェクトの専門家エージェントを Web API として公開するサーバーです。**Microsoft 公式 DevUI** と**カスタム Web UI** の両方が含まれており、ブラウザから直接エージェントと対話できます。

## 🆕 新機能: Human In The Loop (HITL) サポート

DevUIHost は、ワークフロー実行中の人間による承認機能（HITL）をサポートしています。`advanced-contract-review` ワークフローでは、重要な判断ポイントでワークフローが一時停止し、人間の承認を待ちます。

- 📋 **HITL Approval UI**: http://localhost:5000/ui/hitl-approval.html
- 🔌 **HITL API**: `GET /hitl/pending`, `POST /hitl/approve`
- 📖 **詳細ガイド**: [HITL_GUIDE.md](HITL_GUIDE.md)

## 概要

このサーバーは、以下の専門家エージェントを複数の方法で利用可能にします：

1. **Microsoft 公式 DevUI** (`/devui`) - Agent Framework 標準の開発用 UI 🆕
2. **カスタム Web UI** (`/ui/`) - シンプルなチャットインターフェース
3. **AGUI API エンドポイント** (`/agents/*`) - プログラマティックアクセス
4. **HITL Approval UI** (`/ui/hitl-approval.html`) - ワークフロー承認インターフェース 🆕

### 専門家エージェント

このサーバーは、以下の専門家エージェントを提供します：

- **Contract Agent** (`/agents/contract`) - 契約関連の専門家
- **Spend Agent** (`/agents/spend`) - 支出分析の専門家  
- **Negotiation Agent** (`/agents/negotiation`) - 交渉戦略の専門家
- **Sourcing Agent** (`/agents/sourcing`) - 調達戦略の専門家
- **Knowledge Agent** (`/agents/knowledge`) - 知識管理の専門家
- **Supplier Agent** (`/agents/supplier`) - サプライヤー管理の専門家
- **Legal Agent** (`/agents/legal`) - 法務の専門家
- **Finance Agent** (`/agents/finance`) - 財務の専門家
- **Procurement Agent** (`/agents/procurement`) - 調達実務の専門家
- **Procurement Assistant** (`/agents/assistant`) - 調達・購買業務の総合アシスタント

## 前提条件

- .NET 9 SDK
- Azure OpenAI サービスへのアクセス
- Azure CLI（認証用）

## セットアップ

### 1. Azure CLI でログイン

```bash
az login
```

### 2. 環境変数を設定

```bash
export AZURE_OPENAI_ENDPOINT="https://your-endpoint.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o"
```

または、`appsettings.Development.json` に設定を追加：

```json
{
  "environmentVariables": {
    "AZURE_OPENAI_ENDPOINT": "https://your-endpoint.openai.azure.com/",
    "AZURE_OPENAI_DEPLOYMENT_NAME": "gpt-4o"
  }
}
```

## 実行方法

### 開発環境で実行

```bash
cd src/DevUIHost
dotnet run
```

サーバーは `http://localhost:5000` で起動します。

### UI にアクセス

#### オプション 1: Microsoft 公式 DevUI（推奨）🆕

ブラウザで以下の URL を開いてください：

```
http://localhost:5000/devui
```

**公式 DevUI の機能：**
- ✅ Microsoft Agent Framework 標準の UI
- ✅ すべての登録済みエージェントの表示
- ✅ ワークフローのサポート
- ✅ デバッグツール統合
- ✅ OpenAI API 互換エンドポイント

#### オプション 2: カスタム Web UI

ブラウザで以下の URL を開いてください：

```
http://localhost:5000/ui/
```

**カスタム Web UI の機能：**
- ✅ シンプルなチャットインターフェース
- ✅ エージェントの選択
- ✅ チャット形式での対話
- ✅ 会話履歴の保持
- ✅ モダンな UI デザイン

### エージェント一覧の確認

```bash
curl http://localhost:5000/
```

## DevUI（Python）との連携

Microsoft Agent Framework の Python DevUI ツールを使用して、このサーバーに接続できます：

### DevUI のインストール

```bash
pip install agent-framework-devui --pre
```

### DevUI の起動

DevUI は通常、ローカルの Python エージェントを自動検出しますが、この AGUI サーバーに接続するには、クライアント側で AGUI エンドポイントを指定する必要があります。

詳細は [Microsoft Agent Framework AG-UI ドキュメント](https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/) を参照してください。

## AGUI プロトコル

このサーバーは Microsoft Agent Framework の AGUI (Agent Graph UI) プロトコルを実装しており、以下の機能を提供します：

- ✅ エージェントとのストリーミング会話
- ✅ 会話履歴の管理
- ✅ マルチターン対話
- ✅ エージェント機能の実行
- ✅ リアルタイムイベント配信

## ワークフロー

DevUIHost は以下のワークフローを提供します：

### 1. Simple Review Workflow
- **ID**: `simple-review-workflow`
- **説明**: 調達専門家によるレビューと要約の2ステップワークフロー
- **フェーズ**: レビュー → 要約
- **HITL**: なし

### 2. Advanced Contract Review Workflow 🆕
- **ID**: `advanced-contract-review`
- **説明**: 契約レビュー→リスク評価→条件分岐→交渉ループ→HITL承認の高度なワークフロー
- **フェーズ**: 
  1. 契約分析
  2. 並列専門家レビュー（Legal, Finance, Procurement）
  3. リスク評価と分岐
  4. 交渉ループ（必要な場合）
  5. **HITL承認** 👤
- **HITL承認タイプ**:
  - `final_approval`: 最終承認（リスクスコア ≤ 30）
  - `escalation`: エスカレーション（交渉3回後もリスク > 30）
  - `rejection_confirm`: 却下確認（初期リスク > 70）

## アーキテクチャ

```
DevUIHost (ASP.NET Core)
  ├── AGUI Protocol (HTTP/SSE)
  ├── Agent Endpoints (/agents/*)
  ├── Workflow Endpoints (advanced-contract-review, simple-review-workflow)
  ├── HITL API (/hitl/pending, /hitl/approve)
  └── Common Library
       ├── AgentFactory (共通エージェント作成)
       └── DevUIWorkflowCommunication (HITL実装)
```

## 既存ワークフローとの統合

DevUIHost は既存のコンソール・WebSocket ベースのワークフローと並行して動作します：

- **コンソールモード**: 従来通り各ワークフロープロジェクトを直接実行
- **WebSocket モード**: AdvancedConditionalWorkflow の UI と連携
- **DevUI/AGUI モード**: このサーバーを通じて Python DevUI や他のクライアントから接続

## 技術スタック

- **ASP.NET Core 9.0**: Web API フレームワーク
- **Microsoft.Agents.AI.Hosting.AGUI.AspNetCore**: AGUI プロトコル実装
- **Azure.AI.OpenAI**: Azure OpenAI 連携
- **Microsoft.Extensions.AI**: AI 抽象化レイヤー

## 関連ドキュメント

- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)
- [AG-UI Integration Guide](https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/)
- [メインプロジェクト README](../../README.md)
