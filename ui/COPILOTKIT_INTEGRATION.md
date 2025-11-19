# CopilotKit Integration Guide

## 概要

このディレクトリには、CopilotKit を使用した AG-UI 統合のデモが含まれています。

CopilotKit は AG-UI (Agent Graph UI Protocol) 準拠であり、DevUIHost の既存のエンドポイントに直接接続できます。

参考: [CopilotKit と AG-UI の統合](https://zenn.dev/suwash/articles/agui_copilotkit_20250523)

## セットアップ

### 1. 依存関係のインストール

```bash
cd ui
npm install
```

必要なパッケージ:

- `@copilotkit/react-core`: CopilotKit コアライブラリ
- `@copilotkit/react-ui`: CopilotKitUI コンポーネント
- `@copilotkit/runtime`: CopilotKit ランタイム
- `@ag-ui/client`: AG-UIプロトコルクライアント
- `openai`: OpenAI SDK (CopilotKitの要件)

### 2. バックエンドの起動

DevUIHost を起動します:

```bash
cd src/DevUIHost
dotnet run
```

DevUIHost はポート 5000 で起動し、以下の AG-UI エンドポイントを公開します:

- `/agents/contract` - 契約専門家
- `/agents/sourcing` - 調達専門家
- `/agents/spend` - 支出分析専門家
- `/agents/negotiation` - 交渉専門家
- `/agents/knowledge` - 知識管理専門家
- `/agents/supplier` - サプライヤー管理専門家

### 3. フロントエンドの起動

```bash
cd ui
npm run dev
```

ブラウザで `http://localhost:3000` を開きます。

## 利用可能なデモ

### 1. WebSocket Demo (`/`)

既存の WebSocket ベースの UI。AdvancedConditionalWorkflow のリアルタイム表示に対応。

### 2. CopilotKit Demo (`/copilotkit`)

CopilotKit を使用した基本的なエージェントチャット。

**機能:**

- 複数の専門エージェントから選択可能
- AG-UI プロトコル経由の直接接続
- リアルタイムストリーミング応答

**技術詳細:**

```tsx
<CopilotKit runtimeUrl="/api/copilotkit?agent=contract" agent="contract">
  <CopilotChat labels={{ title: "Contract Agent" }} />
</CopilotKit>
```

## アーキテクチャ

```
┌─────────────────┐
│   Browser UI    │
│  (CopilotKit)   │
└────────┬────────┘
         │ AG-UI Protocol
         │ (HTTP/SSE)
         ↓
┌─────────────────┐
│   DevUIHost     │
│  (ASP.NET Core) │
└────────┬────────┘
         │ Microsoft Agent Framework
         ↓
┌─────────────────┐
│  Azure OpenAI   │
└─────────────────┘
```

### ポイント

1. **API Route経由**: `/api/copilotkit` がHttpAgentを使用してAG-UIエンドポイントに接続
2. **バックエンド変更不要**: 既存の `MapAGUI()` エンドポイントがそのまま利用可能
3. **OpenAIAdapter使用**: CopilotKitの要件として必要（実際のLLM呼び出しはAG-UI側で実行）

## トラブルシューティング

### CORS エラーが発生する

DevUIHost の CORS 設定を確認:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

app.UseCors("AllowAll");
```

### ストリーミングが動作しない

1. ブラウザの開発者ツールでネットワークタブを確認
2. SSE (Server-Sent Events) ストリームが正しく開いているか確認
3. DevUIHost のログで `/agents/*` エンドポイントのリクエストを確認

## 参考資料

- [CopilotKit Documentation](https://docs.copilotkit.ai/)
- [AG-UI Protocol Specification](https://github.com/microsoft/autogen)
- [Microsoft Agent Framework](https://learn.microsoft.com/ja-jp/dotnet/ai/quickstarts/quickstart-ai-chat-with-agents)
- [CopilotKit と AG-UI の統合 (Zenn 記事)](https://zenn.dev/suwash/articles/agui_copilotkit_20250523)

## 次のステップ

1. カスタムエージェントの追加
2. マルチエージェント対応 (複数エージェントの同時実行)
3. エージェント状態の永続化 (データベース連携)
