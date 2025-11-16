# DevUI セットアップガイド

このガイドでは、AskAI プロジェクトで DevUI を使用する方法を説明します。

## 前提条件

- .NET 9 SDK
- Azure OpenAI サービスへのアクセス
- Azure CLI（認証用）
- （オプション）Python 3.8+ と pip

## セットアップ手順

### 1. Azure OpenAI の設定

環境変数を設定するか、`src/DevUIHost/appsettings.Development.json` を編集します：

```bash
export AZURE_OPENAI_ENDPOINT="https://your-endpoint.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o"
```

または：

```json
{
  "environmentVariables": {
    "AZURE_OPENAI_ENDPOINT": "https://your-endpoint.openai.azure.com/",
    "AZURE_OPENAI_DEPLOYMENT_NAME": "gpt-4o"
  }
}
```

### 2. Azure CLI で認証

```bash
az login
```

### 3. DevUIHost サーバーの起動

```bash
cd src/DevUIHost
dotnet run
```

サーバーは `http://localhost:5000` で起動します。

### 4. UI でエージェントと対話

#### オプション 1: Microsoft 公式 DevUI（推奨）🆕

ブラウザで以下の URL を開く：

```
http://localhost:5000/devui
```

**公式 DevUI の機能：**

- ✅ Microsoft Agent Framework 標準の開発用 UI
- ✅ すべての登録済みエージェントの表示
- ✅ ワークフローのサポート
- ✅ デバッグツール統合
- ✅ OpenAI API 互換エンドポイント

#### オプション 2: カスタム Web UI

ブラウザで以下の URL を開く：

```
http://localhost:5000/ui/
```

**カスタム Web UI の機能：**

- ✅ シンプルなチャットインターフェース
- ✅ エージェント一覧の表示と選択
- ✅ チャット形式での対話
- ✅ 会話履歴の保持
- ✅ モダンな UI デザイン

### 5. エージェント一覧の確認（API）

ブラウザまたは curl でアクセス：

```bash
curl http://localhost:5000/
```

レスポンス例：

```json
{
  "message": "AskAI DevUI Server - Agent Framework AGUI Endpoints",
  "version": "1.0.0",
  "framework": "Microsoft Agent Framework",
  "agents": [
    {
      "name": "Contract Agent",
      "endpoint": "/agents/contract",
      "description": "契約関連の専門家"
    },
    // ... 他のエージェント
  ]
}
```

## 利用可能なエージェント

DevUIHost は以下の 10 個のエージェントを AGUI プロトコル経由で公開しています：

| エージェント           | エンドポイント          | 説明                           |
| ---------------------- | ----------------------- | ------------------------------ |
| Contract Agent         | `/agents/contract`      | 契約関連の専門家               |
| Spend Agent            | `/agents/spend`         | 支出分析の専門家               |
| Negotiation Agent      | `/agents/negotiation`   | 交渉戦略の専門家               |
| Sourcing Agent         | `/agents/sourcing`      | 調達戦略の専門家               |
| Knowledge Agent        | `/agents/knowledge`     | 知識管理の専門家               |
| Supplier Agent         | `/agents/supplier`      | サプライヤー管理の専門家       |
| Legal Agent            | `/agents/legal`         | 法務の専門家                   |
| Finance Agent          | `/agents/finance`       | 財務の専門家                   |
| Procurement Agent      | `/agents/procurement`   | 調達実務の専門家               |
| Procurement Assistant  | `/agents/assistant`     | 調達・購買業務の総合アシスタント |

## Python DevUI との連携（オプション）

Microsoft Agent Framework の Python DevUI ツールを使用して、ブラウザベースの UI からエージェントと対話できます。

### DevUI のインストール

```bash
pip install agent-framework-devui --pre
```

### 使用方法

Python DevUI は通常、ローカルの Python エージェントを自動検出しますが、この .NET AGUI サーバーに接続するには、AG-UI クライアントライブラリを使用してカスタムクライアントを作成する必要があります。

詳細は [Microsoft Agent Framework AG-UI ドキュメント](https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/) を参照してください。

## AGUI プロトコルについて

AGUI (Agent Graph UI) は、Microsoft Agent Framework が提供するプロトコルで、以下の機能を提供します：

- **ストリーミング会話**: Server-Sent Events (SSE) を使用したリアルタイム通信
- **マルチターン対話**: 会話コンテキストの保持と継続的な対話
- **エージェント機能**: エージェントが持つツールや機能の実行
- **標準化された API**: OpenAI 互換の API インターフェース

## HTTP API の直接利用

AGUI プロトコルは HTTP/SSE ベースなので、任意のプログラミング言語から利用可能です。

### エージェントとの対話例（curl）

```bash
# エージェントにメッセージを送信
curl -X POST http://localhost:5000/agents/contract \
  -H "Content-Type: application/json" \
  -d '{
    "messages": [
      {
        "role": "user",
        "content": "新規サプライヤーとの契約で注意すべき点は？"
      }
    ]
  }'
```

詳細な API 仕様については、[Microsoft Agent Framework AGUI ドキュメント](https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/) を参照してください。

## トラブルシューティング

### サーバーが起動しない

- Azure OpenAI の環境変数が正しく設定されているか確認
- `az login` で Azure CLI 認証が完了しているか確認
- ポート 5000 が他のプロセスで使用されていないか確認

### エージェントがresponseを返さない

- Azure OpenAI エンドポイントとデプロイ名が正しいか確認
- Azure OpenAI のクォータとレート制限を確認
- アプリケーションログでエラーメッセージを確認

## 既存ワークフローとの共存

DevUIHost は既存のコンソール・WebSocket ベースのワークフローと並行して動作します：

- **コンソールモード**: 従来通り各ワークフロープロジェクトを直接実行
- **WebSocket モード**: AdvancedConditionalWorkflow の UI と連携
- **DevUI/AGUI モード**: DevUIHost サーバーを通じて API アクセス

すべてのモードは独立して動作し、互いに干渉しません。

## 参考リンク

- [DevUIHost README](src/DevUIHost/README.md)
- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)
- [AG-UI Integration Guide](https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/)
- [メインプロジェクト README](README.md)
