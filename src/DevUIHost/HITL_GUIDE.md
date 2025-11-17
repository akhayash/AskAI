# DevUI での Human In The Loop (HITL) 実装ガイド

## 概要

DevUIHost に Human In The Loop (HITL) 承認機能が実装されました。これにより、`advanced-contract-review` ワークフロー実行時に、人間による承認が必要な場合、ワークフローが一時停止し、承認/却下の判断を待ちます。

## アーキテクチャ

### 主要コンポーネント

1. **DevUIWorkflowCommunication**
   - `IWorkflowCommunication` インターフェースの DevUI 実装
   - `TaskCompletionSource` を使用してワークフローを一時停止
   - HTTP API 経由で承認応答を受け取る

2. **HITLApprovalRequest**
   - 承認待ちリクエストを表すモデル
   - タイムアウト付きの非同期待機機能

3. **HTTP エンドポイント**
   - `GET /hitl/pending`: 承認待ちリクエストの一覧を取得
   - `POST /hitl/approve`: 承認/却下の応答を送信

4. **HITL Approval UI**
   - `/ui/hitl-approval.html`: ブラウザベースの承認インターフェース
   - リアルタイムポーリングで承認待ち案件を表示

## 使用方法

### 1. サーバーの起動

```bash
cd src/DevUIHost
dotnet run
```

サーバーが起動すると、以下のエンドポイントが利用可能になります:
- DevUI: http://localhost:5000/devui
- Custom Web UI: http://localhost:5000/ui/
- HITL Approval UI: http://localhost:5000/ui/hitl-approval.html
- HITL API: http://localhost:5000/hitl/pending

### 2. ワークフローの実行

DevUI または AGUI API を使用して `advanced-contract-review` ワークフローを実行します。

#### DevUI から実行

1. http://localhost:5000/devui にアクセス
2. ワークフロー `advanced-contract-review` を選択
3. 契約情報を JSON 形式で入力:

```json
{
  "SupplierName": "Test Supplier Co.",
  "ContractValue": 300000,
  "ContractTermMonths": 18,
  "PaymentTerms": "Net 45",
  "DeliveryTerms": "FOB Destination",
  "WarrantyPeriodMonths": 12,
  "HasPenaltyClause": true,
  "HasAutoRenewal": true,
  "Description": "サービス提供契約。標準的な条件。"
}
```

4. ワークフローが HITL 承認ポイントに到達すると、自動的に一時停止します

### 3. HITL 承認の処理

#### オプション 1: HITL Approval UI を使用

1. 新しいブラウザタブで http://localhost:5000/ui/hitl-approval.html を開く
2. 承認待ちの案件が自動的に表示されます
3. 契約情報とリスク評価を確認
4. コメントを追加（オプション）
5. 「承認」または「却下」ボタンをクリック

#### オプション 2: API を直接使用

```bash
# 承認待ちリクエストの確認
curl http://localhost:5000/hitl/pending

# 承認応答の送信
curl -X POST http://localhost:5000/hitl/approve \
  -H "Content-Type: application/json" \
  -d '{
    "requestId": "YOUR_REQUEST_ID",
    "approved": true,
    "comment": "リスク評価を確認しました。承認します。"
  }'
```

### 4. ワークフローの継続

承認/却下の応答を送信すると:
- ワークフローが自動的に再開します
- 承認結果に基づいて後続の処理が実行されます
- 最終的な決定が DevUI に返されます

## HITL が発生するシナリオ

`advanced-contract-review` ワークフローでは、以下の 3 つのケースで HITL 承認が要求されます:

### 1. 最終承認 (final_approval)
- **条件**: 交渉後のリスクスコアが 30 以下
- **説明**: 交渉により目標リスクレベルに到達しました

### 2. エスカレーション (escalation)
- **条件**: 3 回の交渉後もリスクスコアが 30 を超える
- **説明**: 上位承認者への判断が必要です

### 3. 却下確認 (rejection_confirm)
- **条件**: 初期リスクスコアが 70 を超える
- **説明**: 高リスク契約のため、却下を確認します

## タイムアウト

- HITL 承認リクエストは **5 分間** 有効です
- タイムアウトすると自動的に「却下」として処理されます
- タイムアウト時間は `DevUIWorkflowCommunication.cs` で変更可能です

## デバッグとモニタリング

### ログ出力

DevUIHost のコンソールログで HITL の状態を確認できます:

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
👤 HITL: 人間による承認が必要です
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
RequestId: xxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
承認タイプ: 最終承認
契約: Test Supplier Co.
契約金額: $300,000
リスクスコア: 28/100 (Medium)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
承認を待機中... (DevUI UIまたはAPIで応答してください)
```

### OpenTelemetry トレース

HITL 承認は OpenTelemetry でトレースされます:
- Activity Name: `HITL_{approval_type}`
- Tags: `approval_type`, `risk_score`, `supplier`, `contract_value`
- Events: 承認結果、タイムアウト

## トラブルシューティング

### 承認待ちリクエストが表示されない

1. サーバーログで HITL 要求が発生していることを確認
2. ブラウザのコンソールでエラーがないか確認
3. `/hitl/pending` エンドポイントに直接アクセスして確認

### ワークフローが進まない

1. HITL Approval UI で承認/却下を送信したか確認
2. 5 分のタイムアウトを超過していないか確認
3. サーバーログで承認応答が処理されたか確認

### タイムアウトエラー

```
HITL承認がタイムアウトしました。自動的に却下します。
```

- 5 分以内に承認/却下を送信してください
- 必要に応じて `DevUIWorkflowCommunication.cs` でタイムアウト時間を延長できます:

```csharp
var approved = await request.WaitForResponseAsync(TimeSpan.FromMinutes(10)); // 10分に変更
```

## コード例

### カスタム承認タイプの追加

新しい承認タイプを追加する場合:

1. `HITLApprovalExecutor` に新しい承認タイプを追加:

```csharp
var customApprovalHITL = new HITLApprovalExecutor("custom_approval", logger);
```

2. ワークフローに適切な条件で組み込む:

```csharp
workflowBuilder
    .AddEdge(someExecutor, customApprovalHITL,
        condition: (SomeOutput? data) => /* 条件 */);
```

3. HITL Approval UI で新しい承認タイプのラベルを追加:

```javascript
const approvalTypeLabel = {
    'custom_approval': 'カスタム承認',
    // ... 他の承認タイプ
}[approval.approvalType] || approval.approvalType;
```

## セキュリティ考慮事項

現在の実装は **開発環境向け** です。本番環境では以下を考慮してください:

1. **認証・認可**: HITL エンドポイントにアクセス制御を追加
2. **HTTPS**: 本番環境では必ず HTTPS を使用
3. **監査ログ**: 承認/却下の履歴を記録
4. **タイムアウト設定**: 組織のポリシーに合わせて調整

## 関連ファイル

- `/src/DevUIHost/Communication/DevUIWorkflowCommunication.cs`: HITL 通信実装
- `/src/DevUIHost/Program.cs`: サーバー設定と HITL エンドポイント
- `/src/AdvancedConditionalWorkflow/Executors/HITLApprovalExecutor.cs`: HITL 承認ロジック
- `/devui-web/hitl-approval.html`: HITL 承認 UI

## 参考リンク

- [AdvancedConditionalWorkflow ドキュメント](../../docs/workflows/advanced-conditional-workflow.md)
- [DevUI セットアップガイド](../../DEVUI_SETUP.md)
- [Common WebSocket 通信](../Common/WebSocket/)
