# DevUIHost HITL Testing Guide

このガイドでは、DevUIHost の Human In The Loop (HITL) 機能をテストする方法を説明します。

## 前提条件

- Azure OpenAI エンドポイントとデプロイメント名が設定されている
- Azure CLI で認証済み (`az login`)
- .NET 9 SDK がインストールされている

## テストシナリオ

### シナリオ 1: 低リスク契約（自動承認なし、最終承認が必要）

このシナリオでは、初期リスクが中程度で、交渉後に低リスクになるケースをテストします。

#### 1. サーバー起動

```bash
cd src/DevUIHost
export AZURE_OPENAI_ENDPOINT="https://your-endpoint.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o"
dotnet run
```

#### 2. DevUI にアクセス

ブラウザで http://localhost:5000/devui を開きます。

#### 3. ワークフロー実行

1. ワークフローリストから `advanced-contract-review` を選択
2. 以下の契約情報を入力:

```json
{
  "SupplierName": "Reliable Goods Co.",
  "ContractValue": 100000,
  "ContractTermMonths": 12,
  "PaymentTerms": "Net 30",
  "DeliveryTerms": "FOB Destination",
  "WarrantyPeriodMonths": 24,
  "HasPenaltyClause": true,
  "HasAutoRenewal": false,
  "Description": "標準的な物品供給契約。ペナルティ条項あり、自動更新なし。"
}
```

3. 送信ボタンをクリック

#### 4. ワークフロー進行の観察

サーバーコンソールログで以下のフェーズを確認:

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
フェーズ 1: 契約分析
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
フェーズ 2: 並列専門家レビュー
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
👤 HITL: 人間による承認が必要です
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
RequestId: xxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
承認タイプ: 最終承認
```

#### 5. HITL Approval UI で承認

1. 別のブラウザタブで http://localhost:5000/ui/hitl-approval.html を開く
2. 承認待ちの契約が表示されることを確認
3. 契約情報を確認:
   - サプライヤー: Reliable Goods Co.
   - 契約金額: $100,000
   - リスクスコア: ~25-30/100 (交渉後)
4. コメント欄にオプショナルなメモを追加
5. 「✓ 承認」ボタンをクリック

#### 6. 完了確認

- DevUI でワークフローの最終結果が表示される
- 決定: "Approved"
- サーバーログに承認結果が記録される

### シナリオ 2: 中リスク契約（交渉後エスカレーション）

#### 入力データ

```json
{
  "SupplierName": "Standard Services Ltd.",
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

#### 期待される動作

1. 初期リスク評価: 中リスク（40-60）
2. 交渉フェーズが3回実行される
3. 交渉後もリスクスコア > 30 の場合
4. **HITL承認タイプ: escalation（エスカレーション）**
5. 上位承認者への判断が要求される

### シナリオ 3: 高リスク契約（即座に却下確認）

#### 入力データ

```json
{
  "SupplierName": "Global Tech Solutions Inc.",
  "ContractValue": 500000,
  "ContractTermMonths": 24,
  "PaymentTerms": "Net 30",
  "DeliveryTerms": "FOB Destination",
  "WarrantyPeriodMonths": 12,
  "HasPenaltyClause": false,
  "HasAutoRenewal": true,
  "Description": "クラウドインフラサービスの提供契約。24ヶ月の長期契約で自動更新条項あり。ペナルティ条項なし。"
}
```

#### 期待される動作

1. 初期リスク評価: 高リスク（> 70）
2. 交渉フェーズはスキップされる
3. **HITL承認タイプ: rejection_confirm（却下確認）**
4. 却下の最終確認が要求される

## API 経由のテスト

### 承認待ちリクエストの確認

```bash
curl http://localhost:5000/hitl/pending | jq '.'
```

**期待される出力例:**

```json
{
  "requests": [
    {
      "requestId": "abc-123-def-456",
      "approvalType": "final_approval",
      "promptMessage": "契約: Reliable Goods Co.\n...",
      "contractInfo": {
        "supplierName": "Reliable Goods Co.",
        "contractValue": 100000,
        ...
      },
      "riskAssessment": {
        "overallRiskScore": 28,
        "riskLevel": "Low",
        ...
      },
      "createdAt": "2024-11-17T00:30:00Z"
    }
  ]
}
```

### 承認応答の送信

```bash
# 承認
curl -X POST http://localhost:5000/hitl/approve \
  -H "Content-Type: application/json" \
  -d '{
    "requestId": "abc-123-def-456",
    "approved": true,
    "comment": "契約条件を確認しました。承認します。"
  }'

# 却下
curl -X POST http://localhost:5000/hitl/approve \
  -H "Content-Type: application/json" \
  -d '{
    "requestId": "abc-123-def-456",
    "approved": false,
    "comment": "追加のデューデリジェンスが必要です。"
  }'
```

**期待される出力:**

```json
{
  "message": "承認応答を処理しました",
  "success": true
}
```

## タイムアウトのテスト

### 手順

1. ワークフローを実行して HITL 承認を発生させる
2. 5 分間待つ（承認/却下を送信しない）
3. サーバーログでタイムアウトメッセージを確認:

```
HITL承認がタイムアウトしました。自動的に却下します。
```

4. DevUI でワークフローが "Rejected" で完了することを確認

## 並行実行のテスト

### 手順

1. 複数のブラウザタブで DevUI を開く
2. 同時に複数のワークフローを実行
3. HITL Approval UI で複数の承認待ちリクエストが表示されることを確認
4. それぞれを個別に承認/却下

## トラブルシューティング

### HITL リクエストが表示されない

**確認項目:**
- サーバーログで HITL 要求が発生しているか
- `/hitl/pending` エンドポイントにアクセスできるか
- ブラウザのコンソールでエラーがないか

**デバッグ手順:**
```bash
# サーバーログを確認
# "👤 HITL: 人間による承認が必要です" というメッセージを探す

# APIを直接確認
curl http://localhost:5000/hitl/pending

# CORS エラーがある場合、サーバーの CORS 設定を確認
```

### ワークフローが進まない

**原因:**
- HITL 承認が送信されていない
- タイムアウトが発生した
- リクエスト ID が一致していない

**解決策:**
1. HITL Approval UI で承認を送信
2. API で直接承認を送信:
   ```bash
   curl -X POST http://localhost:5000/hitl/approve \
     -H "Content-Type: application/json" \
     -d '{"requestId":"YOUR_REQUEST_ID","approved":true}'
   ```

### OpenTelemetry トレースの確認

```bash
# Aspire Dashboard を起動（オプション）
docker compose up -d

# ブラウザで http://localhost:18888 にアクセス
# Traces セクションで HITL Activity を確認
```

## テスト結果の検証

### 成功基準

- ✅ ワークフローが HITL ポイントで一時停止する
- ✅ HITL Approval UI に承認待ちリクエストが表示される
- ✅ 承認/却下を送信するとワークフローが再開する
- ✅ 最終決定が DevUI に返される
- ✅ タイムアウト時に自動却下される
- ✅ 複数の並行リクエストが正しく処理される

### ログの確認

**重要なログメッセージ:**
1. Communication 初期化: `✓ DevUIWorkflowCommunication初期化完了 (HITL承認サポート有効)`
2. HITL 要求: `👤 HITL: 人間による承認が必要です`
3. 承認結果: `HITL承認結果: 承認` または `HITL承認結果: 却下`
4. タイムアウト: `HITL承認がタイムアウトしました。自動的に却下します。`

## 次のステップ

テストが成功したら:
1. 実際の契約データでテスト
2. 組織の承認フローに合わせてカスタマイズ
3. 認証・認可の追加を検討
4. 監査ログの実装を検討
