# AdvancedConditionalWorkflow UI Demo ガイド

## 概要

AdvancedConditionalWorkflow の WebUI デモは、Next.js と WebSocket を使用したリアルタイム可視化インターフェースです。

## アーキテクチャ

### システム構成

```
┌─────────────────────┐         WebSocket (ws://localhost:8080)         ┌─────────────────────┐
│                     │◄──────────────────────────────────────────────►│                     │
│  Next.js UI         │                                                 │  .NET Backend       │
│  (port 3000)        │                                                 │  Workflow Engine    │
│                     │                                                 │                     │
└─────────────────────┘                                                 └─────────────────────┘
        │                                                                           │
        │                                                                           │
        ▼                                                                           ▼
   ユーザーブラウザ                                                            Azure OpenAI
```

### 通信フロー

1. **ワークフロー開始**: Backend → UI (workflow_start)
2. **エージェント発話**: Backend → UI (agent_utterance)
3. **リスク評価**: Backend → UI (agent_utterance with risk score)
4. **HITL要求**: Backend → UI (hitl_request)
5. **ユーザー応答**: UI → Backend (hitl_response)
6. **最終決定**: Backend → UI (final_response)
7. **ワークフロー完了**: Backend → UI (workflow_complete)

## セットアップ

### 前提条件

- .NET 8 SDK
- Node.js 20.x 以降
- npm 10.x 以降
- Azure OpenAI API アクセス

### インストール

#### Backend

```bash
cd src/AdvancedConditionalWorkflow
# appsettings.Development.json を設定
```

#### UI

```bash
cd ui
npm install
```

## 実行方法

### 開発モード

#### ステップ 1: Backend を WebSocket モードで起動

```bash
cd src/AdvancedConditionalWorkflow
dotnet run -- --websocket
```

出力例:
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Advanced Conditional Workflow デモ
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
実行モード: WEBSOCKET
テレメトリ設定: OTLP Endpoint = http://localhost:4317
✓ WebSocketサーバー起動完了 (Port: 8080)
```

#### ステップ 2: UI を起動 (別ターミナル)

```bash
cd ui
npm run dev
```

#### ステップ 3: ブラウザでアクセス

http://localhost:3000 を開く

### 本番モード

#### UI ビルド

```bash
cd ui
npm run build
npm run start
```

## UI 機能

### 1. リアルタイムメッセージ表示

#### エージェント発話

- **アイコン表示**: エージェント名の頭文字
- **Phase タグ**: 現在のフェーズ (Phase 2: Specialist Review など)
- **リスクスコア**: 色分け表示
  - 🟢 Low (≤30): 緑
  - 🟡 Medium (31-70): 黄
  - 🔴 High (>70): 赤
- **タイムスタンプ**: メッセージ受信時刻

#### 最終決定

- 特別なハイライトカード
- JSON 形式の詳細表示
- 意思決定サマリー

### 2. HITL (Human-in-the-Loop) 承認

#### 承認パネル

- **契約情報表示**: JSON 形式
- **リスク評価表示**: スコアと懸念事項
- **承認/却下ボタン**:
  - ✅ Approve (Y): 承認
  - ❌ Reject (N): 却下

#### 動作

1. Backend から `hitl_request` を受信
2. UI に承認パネルが表示 (アニメーション付き)
3. ユーザーがボタンをクリック
4. UI が `hitl_response` を Backend に送信
5. Backend が処理を継続

### 3. ステータスインジケーター

- **接続状態**: 緑 (接続) / 赤 (切断)
- **ワークフロー状態**: 開始/実行中/完了

## メッセージ仕様

### agent_utterance

```json
{
  "type": "agent_utterance",
  "agentName": "Legal Agent",
  "content": "法的リスクの評価結果...",
  "phase": "Phase 2: Specialist Review",
  "riskScore": 45,
  "timestamp": "2025-11-01T00:00:00Z",
  "messageId": "uuid"
}
```

### final_response

```json
{
  "type": "final_response",
  "decision": {
    "Decision": "Approved",
    "FinalRiskScore": 30,
    "DecisionSummary": "...",
    "NextActions": ["..."]
  },
  "summary": "決定: Approved, 最終リスクスコア: 30/100",
  "timestamp": "2025-11-01T00:00:00Z",
  "messageId": "uuid"
}
```

### hitl_request

```json
{
  "type": "hitl_request",
  "approvalType": "final_approval",
  "contractInfo": { ... },
  "riskAssessment": { ... },
  "promptMessage": "交渉により目標リスクレベルに到達しました。\nこの契約を承認しますか?",
  "timestamp": "2025-11-01T00:00:00Z",
  "messageId": "uuid"
}
```

### hitl_response (UI → Backend)

```json
{
  "type": "hitl_response",
  "approved": true,
  "comment": "",
  "timestamp": "2025-11-01T00:00:00Z",
  "messageId": "uuid"
}
```

## トラブルシューティング

### UI が Backend に接続できない

**症状**: 「Disconnected」表示

**原因と対策**:
1. Backend が起動していない → `dotnet run -- --websocket` で起動
2. WebSocket ポートが異なる → Backend が 8080 で起動していることを確認
3. ファイアウォール → localhost:8080 へのアクセスを許可

### HITL 応答がタイムアウトする

**症状**: Backend に「HITL応答のタイムアウト」と表示

**原因と対策**:
1. UI が応答を送信していない → ブラウザのコンソールでエラー確認
2. WebSocket 接続が切れている → ページをリロード

### メッセージが表示されない

**症状**: UI にメッセージが表示されない

**原因と対策**:
1. ブラウザのコンソールでエラー確認
2. Backend のログでメッセージ送信を確認
3. WebSocket 接続状態を確認

## カスタマイズ

### UI のカスタマイズ

#### メッセージカードのスタイル変更

`ui/src/app/page.tsx` の `renderMessage` 関数を編集:

```tsx
case "agent_utterance":
  // スタイルをカスタマイズ
  return (
    <div className="...">
      ...
    </div>
  );
```

#### 色の変更

Tailwind CSS のクラスを変更:

- リスクスコア: `bg-green-100`, `bg-yellow-100`, `bg-red-100`
- ボタン: `bg-green-600`, `bg-red-600`

### Backend のカスタマイズ

#### メッセージ送信タイミング

`src/AdvancedConditionalWorkflow/Executors/*.cs` で `Communication.SendAgentUtteranceAsync()` を呼び出すタイミングを調整:

```csharp
await Program.Communication!.SendAgentUtteranceAsync(
    agentName: "Custom Agent",
    content: "カスタムメッセージ",
    phase: "Custom Phase",
    riskScore: 50);
```

## パフォーマンス

### レイテンシ

- **WebSocket 接続**: < 100ms
- **メッセージ配信**: < 50ms
- **UI 更新**: < 100ms

### スケーラビリティ

現在の実装は 1 対 1 通信を想定:
- 1 Backend プロセス
- 複数 UI クライアント (ブロードキャスト)

## セキュリティ考慮事項

### 本番環境での注意点

1. **認証・認可**: WebSocket 接続に認証を追加
2. **HTTPS/WSS**: TLS 暗号化を使用
3. **入力検証**: HITL 応答の検証を強化
4. **レート制限**: DoS 攻撃対策

## 今後の拡張

- [ ] 複数ワークフロー同時実行対応
- [ ] 契約パターン選択 UI
- [ ] ワークフロー履歴表示
- [ ] エクスポート機能 (PDF, JSON)
- [ ] リアルタイムチャート表示
- [ ] ダークモード対応

## 関連ドキュメント

- [AdvancedConditionalWorkflow README](../../src/AdvancedConditionalWorkflow/README.md)
- [UI README](../../ui/README.md)
- [Common WebSocket Infrastructure](../../src/Common/WebSocket/)
- [Clean Architecture Guide](../architecture/clean-architecture.md)
