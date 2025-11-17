# CopilotKit + Microsoft Agent Framework Integration Status

## 実装完了: 2025-01-16

### 参照ドキュメント

- CopilotKit Microsoft Agent Framework Quickstart: https://docs.copilotkit.ai/microsoft-agent-framework/quickstart
- AG-UI Protocol: https://github.com/ag-ui-protocol/ag-ui
- CopilotKit GitHub Repository: https://github.com/CopilotKit/CopilotKit

## アーキテクチャ概要

```
[Next.js UI (Port 3000)]
    ↓ CopilotKit React Components
[/api/copilotkit Route]
    ↓ @ag-ui/client.HttpAgent
[DevUIHost AG-UI Endpoint (Port 5000)]
    ↓ MapAGUI()
[Microsoft Agent Framework Agents]
```

## 実装詳細

### 1. API Route (`/api/copilotkit/route.ts`)

**目的**: CopilotKit Runtime と DevUIHost (AG-UI) を接続

**実装パターン**: Microsoft Agent Framework 公式パターンを使用

- `@copilotkit/runtime` - CopilotRuntime, OpenAIAdapter
- `@ag-ui/client` - HttpAgent for AG-UI protocol communication
- `copilotRuntimeNextJSAppRouterEndpoint` - Next.js App Router 統合

**コード構造**:

```typescript
// 1. OpenAIAdapter を使用（CopilotKitの要件、実際のLLM呼び出しはAG-UI側）
const openai = new OpenAI({
  apiKey: "dummy-key-not-used",
});
const serviceAdapter = new OpenAIAdapter({ openai, model: "gpt-4o" });

// 2. CopilotRuntime + HttpAgent でAG-UIに接続
const runtime = new CopilotRuntime({
  agents: {
    [agentId]: new HttpAgent({ url: aguiEndpoint }),
  },
});

// 3. Next.js App Router統合
const { handleRequest } = copilotRuntimeNextJSAppRouterEndpoint({
  runtime,
  serviceAdapter,
  endpoint: "/api/copilotkit",
});

return handleRequest(req);
```

**重要**: `OpenAIAdapter`はCopilotKitの要件として必要ですが、実際のLLM呼び出しはAG-UIエージェント側で行われます。

**参考**:

- https://docs.copilotkit.ai/microsoft-agent-framework/quickstart

### 2. フロントエンドページ (`/app/copilotkit/page.tsx`)

**目的**: CopilotKit チャット UI で AG-UI エージェントと対話

**実装パターン**: 標準的な CopilotKit 統合

```tsx
<CopilotKit runtimeUrl={`/api/copilotkit?agent=${selectedAgent.id}`}>
  <CopilotChat labels={{ title: agentName, ... }} />
</CopilotKit>
```

**利用可能なエージェント**:

- `contract` - Contract Agent
- `sourcing` - Sourcing Agent
- `spend` - Spend Agent
- `negotiation` - Negotiation Agent
- `knowledge` - Knowledge Agent
- `supplier` - Supplier Agent

### 3. DevUIHost 側の対応

**既存実装**: DevUIHost はすでに AG-UI プロトコルをサポート

- `app.MapAGUI()` でエンドポイントを公開
- `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` パッケージ使用
- HTTP POST + text/event-stream 応答

**エンドポイント例**:

- `POST http://localhost:5000/agents/contract`
- `POST http://localhost:5000/agents/sourcing`

## 使用技術スタック

### フロントエンド

- Next.js 16.0.1 (App Router)
- React 19.2.0
- CopilotKit 1.10.6
  - @copilotkit/react-core
  - @copilotkit/react-ui
  - @copilotkit/runtime
- @ag-ui/client 0.0.41

### バックエンド

- DevUIHost (ASP.NET Core)
- Microsoft.Agents.AI.Hosting.AGUI.AspNetCore
- Microsoft Agent Framework

## テスト手順

1. **DevUIHost 起動**:

   ```powershell
   cd c:\Repos\AskAI\src\DevUIHost
   dotnet run
   ```

   → http://localhost:5000 で起動

2. **Next.js UI 起動**:

   ```powershell
   cd c:\Repos\AskAI\ui
   npm run dev
   ```

   → http://localhost:3000 で起動

3. **CopilotKit ページにアクセス**:

   - http://localhost:3000/copilotkit
   - エージェント選択ドロップダウンでエージェントを選ぶ
   - チャット入力欄に質問を入力

4. **期待される動作**:
   - ✅ CopilotKit チャット UI が表示される
   - ✅ 質問を送信すると、選択したエージェントが応答する
   - ✅ SSE ストリーミングでリアルタイム応答が表示される
   - ✅ ブラウザ DevTools の Network タブで `/api/copilotkit` への POST が成功する

## トラブルシューティング

### 以前のエラー: "[Network] No Content"

**原因**: CopilotKit が直接 AG-UI エンドポイントに接続しようとしていた

```typescript
// ❌ 誤った実装
<CopilotKit runtimeUrl={`${backendUrl}${selectedAgent.endpoint}`}>
```

**解決策**: `HttpAgent`を使用したプロキシ API Route を作成

```typescript
// ✅ 正しい実装
const runtime = new CopilotRuntime({
  agents: {
    [agentId]: new HttpAgent({ url: aguiEndpoint }),
  },
});
```

### CORS 問題

DevUIHost で既に CORS 設定済み:

```csharp
app.UseCors(policy => policy
    .WithOrigins("http://localhost:3000")
    .AllowAnyMethod()
    .AllowAnyHeader());
```

### エラーログの確認

- **Next.js**: ターミナルのサーバーログ
- **ブラウザ**: DevTools → Console/Network
- **DevUIHost**: Visual Studio またはターミナルのログ

## 次のステップ

### 完了済み

- ✅ CopilotKit 基本チャット UI
- ✅ API Route with HttpAgent
- ✅ DevUIHost AG-UI 統合
- ✅ 複数エージェント切り替え

### 今後の実装候補

- ⏳ Generative UI (useCoAgentStateRender)
- ⏳ Shared State (useCoAgent)
- ⏳ Frontend Actions (useCopilotAction)
- ⏳ マルチエージェント対応

## 参考リンク

- [CopilotKit Documentation](https://docs.copilotkit.ai/)
- [Microsoft Agent Framework Quickstart](https://docs.copilotkit.ai/microsoft-agent-framework/quickstart)
- [AG-UI Protocol](https://docs.ag-ui.com/)
- [CopilotKit GitHub](https://github.com/CopilotKit/CopilotKit)
- [AG-UI Client npm](https://www.npmjs.com/package/@ag-ui/client)

---

**最終更新**: 2025-01-18
**ステータス**: 実装完了
