# DevUI Human In The Loop (HITL) 実装サマリー

## 概要

DevUIHost に Human In The Loop (HITL) 承認機能を実装しました。この機能により、`advanced-contract-review` ワークフロー実行時に、重要な判断ポイントで人間による承認/却下の判断を求めることができます。

## 実装された機能

### 1. DevUI統合型HITL承認システム

#### 主要コンポーネント

```
DevUIHost
├── Communication/
│   └── DevUIWorkflowCommunication.cs     # HITL通信実装
├── Program.cs                             # APIエンドポイントとDI設定
└── HITL_GUIDE.md, TESTING.md             # ドキュメント

devui-web/
└── hitl-approval.html                     # 承認UI

AdvancedConditionalWorkflow/
└── Program.cs                             # Communication公開（public化）
```

#### アーキテクチャ

```
┌─────────────────────────────────────────────────────────────┐
│                        ユーザー                              │
└────────┬──────────────────────────────────┬─────────────────┘
         │                                  │
         │ DevUI                            │ HITL Approval UI
         │ (ワークフロー実行)               │ (承認/却下)
         ↓                                  ↓
┌─────────────────────────────────────────────────────────────┐
│                      DevUIHost Server                        │
│  ┌──────────────────┐         ┌──────────────────────────┐  │
│  │ Workflow Engine  │────────→│ DevUIWorkflow            │  │
│  │ (advanced-       │  HITL   │ Communication            │  │
│  │  contract-review)│  要求   │ (TaskCompletionSource)   │  │
│  └──────────────────┘         └──────────┬───────────────┘  │
│                                           │                   │
│                                           │ 承認待機          │
│  ┌──────────────────────────────────────┴─────────────────┐ │
│  │            HITL API Endpoints                           │ │
│  │  • GET  /hitl/pending  (承認待ちリクエスト取得)         │ │
│  │  • POST /hitl/approve  (承認応答送信)                   │ │
│  └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### 2. 実装の特徴

#### 非同期待機メカニズム

```csharp
// TaskCompletionSourceを使用したワークフロー一時停止
public async Task<bool> RequestHITLApprovalAsync(...)
{
    var request = new HITLApprovalRequest { ... };
    _pendingApprovals[requestId] = request;
    
    // タイムアウト付きで承認を待機（5分）
    var approved = await request.WaitForResponseAsync(TimeSpan.FromMinutes(5));
    return approved;
}
```

#### スレッドセーフな管理

```csharp
// ConcurrentDictionaryで複数の並行承認リクエストを管理
private readonly ConcurrentDictionary<string, HITLApprovalRequest> _pendingApprovals;
```

#### タイムアウト処理

```csharp
// 5分以内に応答がない場合は自動却下
using var cts = new CancellationTokenSource(timeout);
cts.Token.Register(() => _completionSource.TrySetException(new TimeoutException()));
```

### 3. ユーザーインターフェース

#### HITL Approval UI の特徴

- 📊 **リアルタイム更新**: 3秒ごとのポーリングで承認待ちリクエストを表示
- 📋 **詳細情報表示**: 契約情報、リスク評価、プロンプトメッセージ
- 💬 **コメント機能**: 承認/却下時にコメントを追加可能
- 🎨 **モダンUI**: グラデーション背景、カード型レイアウト、アニメーション
- 📱 **レスポンシブ**: モバイルデバイスにも対応

#### 承認タイプの可視化

```javascript
// 承認タイプごとに色分け
const approvalTypeLabel = {
    'final_approval': '最終承認',      // 緑色（承認可能）
    'escalation': 'エスカレーション',   // 赤色（要上位判断）
    'rejection_confirm': '却下確認'     // オレンジ色（要確認）
};
```

## 使用方法

### 基本的なフロー

```bash
# 1. サーバー起動
cd src/DevUIHost
export AZURE_OPENAI_ENDPOINT="https://your-endpoint.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o"
dotnet run

# 2. DevUIでワークフロー実行
# → http://localhost:5000/devui

# 3. HITL Approval UIで承認
# → http://localhost:5000/ui/hitl-approval.html
```

### 契約データ例

#### 低リスク契約（最終承認が必要）

```json
{
  "SupplierName": "Reliable Goods Co.",
  "ContractValue": 100000,
  "ContractTermMonths": 12,
  "PaymentTerms": "Net 30",
  "DeliveryTerms": "FOB Destination",
  "WarrantyPeriodMonths": 24,
  "HasPenaltyClause": true,
  "HasAutoRenewal": false
}
```

#### 中リスク契約（エスカレーションの可能性）

```json
{
  "SupplierName": "Standard Services Ltd.",
  "ContractValue": 300000,
  "ContractTermMonths": 18,
  "PaymentTerms": "Net 45",
  "HasPenaltyClause": true,
  "HasAutoRenewal": true
}
```

#### 高リスク契約（却下確認が必要）

```json
{
  "SupplierName": "Global Tech Solutions Inc.",
  "ContractValue": 500000,
  "ContractTermMonths": 24,
  "HasPenaltyClause": false,
  "HasAutoRenewal": true
}
```

### API経由の操作

```bash
# 承認待ちリクエストの確認
curl http://localhost:5000/hitl/pending

# 承認の送信
curl -X POST http://localhost:5000/hitl/approve \
  -H "Content-Type: application/json" \
  -d '{
    "requestId": "YOUR_REQUEST_ID",
    "approved": true,
    "comment": "契約条件を確認しました。"
  }'
```

## HITL承認が発生するシナリオ

### advanced-contract-review ワークフローの流れ

```
1. 契約情報入力
   ↓
2. 契約分析（ContractAnalysisExecutor）
   ↓
3. 並列専門家レビュー
   ├─ Legal Review
   ├─ Finance Review
   └─ Procurement Review
   ↓
4. リスク評価と分岐
   ├─ リスクスコア ≤ 30  → 6. LowRiskApproval（自動承認）
   ├─ 30 < リスクスコア ≤ 70 → 5. 交渉ループ
   └─ リスクスコア > 70  → 7c. HITL (rejection_confirm)
   ↓
5. 交渉ループ（最大3回）
   ├─ 交渉提案生成
   ├─ 評価
   └─ 継続判定
   ↓
6. 交渉後のリスク評価
   ├─ リスクスコア ≤ 30  → 7a. HITL (final_approval) ✓
   └─ リスクスコア > 30  → 7b. HITL (escalation) ⚠️
   ↓
7. HITL承認 👤
   a. final_approval: 最終承認
   b. escalation: エスカレーション判断
   c. rejection_confirm: 却下確認
   ↓
8. 最終決定
```

## 技術的な詳細

### 依存関係の注入

```csharp
// Program.cs での設定
builder.Services.AddSingleton<DevUIWorkflowCommunication>(sp =>
{
    var logger = sp.GetRequiredService<ILoggerFactory>()
                   .CreateLogger<DevUIWorkflowCommunication>();
    return new DevUIWorkflowCommunication(logger);
});

// ワークフロー登録時に注入
builder.AddWorkflow("advanced-contract-review", (sp, key) =>
{
    var communication = sp.GetRequiredService<DevUIWorkflowCommunication>();
    global::AdvancedConditionalWorkflow.Program.Communication = communication;
    // ... ワークフロー構築
});
```

### OpenTelemetry統合

HITL承認はOpenTelemetryでトレースされます：

```csharp
using var activity = TelemetryHelper.StartActivity(
    Program.ActivitySource,
    $"HITL_{approvalType}",
    new Dictionary<string, object>
    {
        ["approval_type"] = approvalType,
        ["risk_score"] = risk.OverallRiskScore,
        ["supplier"] = contract.SupplierName,
        ["approved"] = approved
    });
```

Aspire Dashboardで確認:
```bash
docker compose up -d
# http://localhost:18888 でトレースを表示
```

### ログ出力

サーバーコンソールで以下のようなログが出力されます：

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
👤 HITL: 人間による承認が必要です
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
RequestId: 12345678-1234-1234-1234-123456789abc
承認タイプ: 最終承認
契約: Reliable Goods Co.
契約金額: $100,000
リスクスコア: 28/100 (Low)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
承認を待機中... (DevUI UIまたはAPIで応答してください)
```

## トラブルシューティング

### よくある問題と解決策

#### 1. ワークフローが進まない

**原因**: HITL承認が送信されていない、またはタイムアウトした

**解決策**:
```bash
# 承認待ちリクエストを確認
curl http://localhost:5000/hitl/pending

# RequestIdを確認して承認を送信
curl -X POST http://localhost:5000/hitl/approve \
  -H "Content-Type: application/json" \
  -d '{"requestId":"YOUR_REQUEST_ID","approved":true}'
```

#### 2. HITL Approval UIに何も表示されない

**原因**: ワークフローがHITL承認ポイントに到達していない

**確認手順**:
1. サーバーログで「👤 HITL: 人間による承認が必要です」を確認
2. `/hitl/pending` エンドポイントに直接アクセス
3. ブラウザのコンソールでJavaScriptエラーを確認

#### 3. タイムアウトエラー

**ログメッセージ**:
```
HITL承認がタイムアウトしました。自動的に却下します。
```

**解決策**:
- 5分以内に承認/却下を送信してください
- タイムアウト時間を延長する場合は `DevUIWorkflowCommunication.cs` を編集:

```csharp
// タイムアウトを10分に変更
var approved = await request.WaitForResponseAsync(TimeSpan.FromMinutes(10));
```

## 次のステップ

### 本番環境への展開前に

1. **認証・認可の追加**
   ```csharp
   app.MapGet("/hitl/pending", ...)
      .RequireAuthorization(); // 認証を要求
   ```

2. **監査ログの実装**
   - 承認/却下の履歴をデータベースに記録
   - ユーザーID、タイムスタンプ、理由を保存

3. **HTTPS の使用**
   - 本番環境では必ずHTTPSを使用
   - `appsettings.Production.json` で設定

4. **タイムアウト設定の調整**
   - 組織のポリシーに合わせて設定
   - 承認フローの複雑さに応じて調整

## 関連ドキュメント

- [HITL_GUIDE.md](src/DevUIHost/HITL_GUIDE.md) - 詳細な使用方法
- [TESTING.md](src/DevUIHost/TESTING.md) - テストシナリオ
- [DevUIHost README](src/DevUIHost/README.md) - プロジェクト概要
- [DEVUI_SETUP.md](DEVUI_SETUP.md) - セットアップガイド

## まとめ

この実装により、DevUI環境でも AdvancedConditionalWorkflow の HITL 承認機能が完全に利用可能になりました。主な利点：

✅ **非ブロッキング**: ワークフローは承認を待つ間、他のリクエストをブロックしません
✅ **スケーラブル**: 複数の並行承認リクエストをサポート
✅ **ユーザーフレンドリー**: ブラウザベースのモダンなUI
✅ **API統合**: プログラマティックアクセスも可能
✅ **タイムアウト制御**: 無限待機を防ぐセーフティネット
✅ **完全なトレーサビリティ**: OpenTelemetryと構造化ログ

これで DevUI のワークフローにおける Human In The Loop 機能の実装が完了しました！
