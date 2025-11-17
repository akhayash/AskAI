# DevUI HITL アーキテクチャ

## システム構成図

```
┌─────────────────────────────────────────────────────────────────────┐
│                           ユーザー層                                 │
│  ┌──────────────────┐              ┌──────────────────────────┐    │
│  │   DevUI          │              │  HITL Approval UI        │    │
│  │  (ワークフロー    │              │  (承認/却下)              │    │
│  │   実行)          │              │  /ui/hitl-approval.html  │    │
│  └────────┬─────────┘              └──────────┬───────────────┘    │
│           │                                   │                     │
└───────────┼───────────────────────────────────┼─────────────────────┘
            │                                   │
            │ POST /v1/responses                │ GET/POST /hitl/*
            │ (ワークフロー実行)                 │ (承認API)
            ↓                                   ↓
┌─────────────────────────────────────────────────────────────────────┐
│                      DevUIHost (ASP.NET Core)                        │
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                    Workflow Engine                            │  │
│  │  ┌────────────────────────────────────────────────────────┐  │  │
│  │  │  advanced-contract-review ワークフロー                  │  │  │
│  │  │  ┌──────────┐  ┌──────────┐  ┌──────────────────┐     │  │  │
│  │  │  │ 契約分析  │→│ 専門家   │→│ リスク評価・分岐 │     │  │  │
│  │  │  └──────────┘  │ レビュー │  └────────┬─────────┘     │  │  │
│  │  │                └──────────┘           │               │  │  │
│  │  │                                       ↓               │  │  │
│  │  │                              ┌────────────────┐       │  │  │
│  │  │                              │ 交渉ループ     │       │  │  │
│  │  │                              │ (最大3回)      │       │  │  │
│  │  │                              └────────┬───────┘       │  │  │
│  │  │                                       │               │  │  │
│  │  │                                       ↓               │  │  │
│  │  │                    ┌──────────────────────────────┐  │  │  │
│  │  │                    │  HITLApprovalExecutor        │  │  │  │
│  │  │                    │  • final_approval            │  │  │  │
│  │  │                    │  • escalation                │  │  │  │
│  │  │                    │  • rejection_confirm         │  │  │  │
│  │  │                    └────────┬─────────────────────┘  │  │  │
│  │  │                             │                        │  │  │
│  │  │                             │ RequestHITLApproval    │  │  │
│  │  │                             ↓                        │  │  │
│  │  └─────────────────────────────────────────────────────┘  │  │
│  └──────────────────────────────┬─────────────────────────────┘  │
│                                  │                                │
│  ┌───────────────────────────────┴──────────────────────────┐    │
│  │         DevUIWorkflowCommunication (Singleton)           │    │
│  │                                                           │    │
│  │  ┌─────────────────────────────────────────────────┐     │    │
│  │  │  ConcurrentDictionary<string, HITLRequest>      │     │    │
│  │  │  ┌───────────────┐  ┌───────────────┐          │     │    │
│  │  │  │ RequestId-1   │  │ RequestId-2   │  ...     │     │    │
│  │  │  │ TaskCompletion│  │ TaskCompletion│          │     │    │
│  │  │  │ Source (待機) │  │ Source (待機) │          │     │    │
│  │  │  └───────┬───────┘  └───────┬───────┘          │     │    │
│  │  │          │                  │                  │     │    │
│  │  │          │ await (5分タイムアウト)            │     │    │
│  │  │          ↓                  ↓                  │     │    │
│  │  │      [承認待機中]      [承認待機中]            │     │    │
│  │  └─────────────────────────────────────────────────┘     │    │
│  │                                                           │    │
│  │  public methods:                                         │    │
│  │  • GetPendingApprovals()                                │    │
│  │  • ProcessApprovalResponse(requestId, approved)         │    │
│  │  • RequestHITLApprovalAsync(...) → Task<bool>          │    │
│  └───────────────────────────────┬──────────────────────────┘    │
│                                  │                                │
│  ┌───────────────────────────────┴──────────────────────────┐    │
│  │             HTTP API Endpoints                           │    │
│  │  • GET  /hitl/pending                                   │    │
│  │    → GetPendingApprovals()                             │    │
│  │  • POST /hitl/approve                                  │    │
│  │    → ProcessApprovalResponse()                         │    │
│  │       └─> TaskCompletionSource.SetResult(approved)     │    │
│  └──────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
```

## シーケンス図: HITL承認フロー

```
ユーザー          DevUI          WorkflowEngine    HITLExecutor    Communication    HITL UI
   │                │                  │                │                │            │
   │ 1. ワークフロー │                  │                │                │            │
   │    実行要求    │                  │                │                │            │
   ├───────────────>│                  │                │                │            │
   │                │ 2. 開始          │                │                │            │
   │                ├─────────────────>│                │                │            │
   │                │                  │ 3. 各Executor  │                │            │
   │                │                  │    実行        │                │            │
   │                │                  ├───────────────>│                │            │
   │                │                  │                │ 4. HITL要求    │            │
   │                │                  │                ├───────────────>│            │
   │                │                  │                │                │            │
   │                │                  │                │ 5. RequestId   │            │
   │                │                  │                │    生成・保存  │            │
   │                │                  │                │    ↓           │            │
   │                │                  │                │ [TaskCompletion│            │
   │                │                  │                │  Source待機]   │            │
   │                │                  │                │    ↓           │            │
   │                │                  │                │ await (5分)    │            │
   │                │                  │                │                │            │
   │                │                  │                │                │ 6. ポーリング│
   │                │                  │                │                │<───────────┤
   │                │                  │                │                │            │
   │                │                  │                │ 7. GetPending  │            │
   │                │                  │                │                ├───────────>│
   │                │                  │                │                │ 8. 承認待ち │
   │                │                  │                │                │    リスト  │
   │                │                  │                │                │<───────────┤
   │                │                  │                │                │            │
   │                │                  │                │                │ 9. UI表示  │
   │                │                  │                │                │            │
   │                │                  │                │                │ 10. ユーザー│
   │                │                  │                │                │     判断   │
   │                │                  │                │                │            │
   │                │                  │                │                │ 11. 承認/却下│
   │                │                  │                │ 12. Process    │<───────────┤
   │                │                  │                │    Approval    │            │
   │                │                  │                │<───────────────┤            │
   │                │                  │                │                │            │
   │                │                  │                │ 13. SetResult  │            │
   │                │                  │                │    (approved)  │            │
   │                │                  │                │    ↓           │            │
   │                │                  │                │ [await完了]    │            │
   │                │                  │                │    ↓           │            │
   │                │                  │ 14. return     │                │            │
   │                │                  │     approved   │                │            │
   │                │                  │<───────────────┤                │            │
   │                │                  │                │                │            │
   │                │                  │ 15. 後続処理   │                │            │
   │                │                  │                │                │            │
   │                │ 16. 最終結果     │                │                │            │
   │                │<─────────────────┤                │                │            │
   │                │                  │                │                │            │
   │ 17. 結果表示   │                  │                │                │            │
   │<───────────────┤                  │                │                │            │
   │                │                  │                │                │            │
```

## データフロー

### 1. HITL リクエストの作成

```csharp
// HITLApprovalExecutor.cs
var approved = await Program.Communication.RequestHITLApprovalAsync(
    approvalType,        // "final_approval" など
    contractInfo,        // 契約情報オブジェクト
    riskAssessment,      // リスク評価オブジェクト
    promptMessage        // ユーザー向けメッセージ
);
```

### 2. Communication での処理

```csharp
// DevUIWorkflowCommunication.cs
public async Task<bool> RequestHITLApprovalAsync(...)
{
    // 1. リクエストオブジェクト作成
    var requestId = Guid.NewGuid().ToString();
    var request = new HITLApprovalRequest
    {
        RequestId = requestId,
        ApprovalType = approvalType,
        ContractInfo = contractInfo,
        RiskAssessment = riskAssessment,
        PromptMessage = promptMessage,
        CreatedAt = DateTime.UtcNow
    };

    // 2. ConcurrentDictionaryに保存
    _pendingApprovals[requestId] = request;

    // 3. 非同期待機（5分タイムアウト）
    var approved = await request.WaitForResponseAsync(TimeSpan.FromMinutes(5));
    
    // 4. 結果を返す
    return approved;
}
```

### 3. HITL UI からの承認

```javascript
// hitl-approval.html
async function handleApproval(requestId, approved) {
    const response = await fetch(`${API_BASE}/hitl/approve`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            requestId,
            approved,
            comment: comment || null
        })
    });
}
```

### 4. API での処理

```csharp
// Program.cs
app.MapPost("/hitl/approve", (
    DevUIWorkflowCommunication communication,
    HITLApprovalResponse response) =>
{
    // ConcurrentDictionaryからリクエストを取り出し
    // TaskCompletionSource.SetResult()を呼び出し
    var success = communication.ProcessApprovalResponse(
        response.RequestId,
        response.Approved,
        response.Comment);
    
    return success ? Results.Ok(...) : Results.NotFound(...);
});
```

### 5. TaskCompletionSource の完了

```csharp
// HITLApprovalRequest.cs
public void SetResult(bool approved, string? comment = null)
{
    Comment = comment;
    _completionSource.TrySetResult(approved);  // await が完了する
}
```

## 状態遷移図

```
                    ┌─────────────────┐
                    │ ワークフロー開始 │
                    └────────┬────────┘
                             │
                             ↓
                    ┌─────────────────┐
                    │ HITLポイント到達 │
                    └────────┬────────┘
                             │
                             ↓
         ┌───────────────────────────────────────┐
         │  DevUIWorkflowCommunication           │
         │  ┌─────────────────────────────────┐  │
         │  │ RequestHITLApprovalAsync()      │  │
         │  │  1. RequestId生成               │  │
         │  │  2. HITLApprovalRequest作成     │  │
         │  │  3. ConcurrentDictionaryに保存  │  │
         │  │  4. await (5分タイムアウト)     │  │
         │  └─────────────┬───────────────────┘  │
         └────────────────┼───────────────────────┘
                          │
         ┌────────────────┴────────────────┐
         │                                 │
         ↓                                 ↓
┌─────────────────┐             ┌─────────────────┐
│ HITL UI から承認 │             │ タイムアウト(5分)│
│ ProcessApproval  │             │ 自動却下         │
│ Response()       │             └─────────┬───────┘
└────────┬────────┘                       │
         │                                 │
         │ SetResult(approved)             │ SetException()
         │                                 │
         └────────────────┬────────────────┘
                          │
                          ↓
                 ┌─────────────────┐
                 │ await 完了       │
                 │ approved 取得    │
                 └────────┬────────┘
                          │
                          ↓
                 ┌─────────────────┐
                 │ ワークフロー継続 │
                 └─────────────────┘
```

## コンカレンシー制御

### 複数の同時承認リクエスト

```
Time ───────────────────────────────────────────>

Request-1: [────────待機────────][承認]──>
Request-2:      [────────待機────────][却下]──>
Request-3:           [────────待機────────]timeout──>

ConcurrentDictionary:
{
  "req-1": HITLApprovalRequest (TaskCompletionSource)
  "req-2": HITLApprovalRequest (TaskCompletionSource)
  "req-3": HITLApprovalRequest (TaskCompletionSource)
}
```

### スレッドセーフな操作

```csharp
// 追加（スレッドセーフ）
_pendingApprovals[requestId] = request;

// 削除（スレッドセーフ）
if (_pendingApprovals.TryRemove(requestId, out var request))
{
    request.SetResult(approved);
}

// 読み取り（スレッドセーフ）
var pending = _pendingApprovals.Values;
```

## エラーハンドリング

```
┌──────────────────────────────────────────┐
│ RequestHITLApprovalAsync()               │
├──────────────────────────────────────────┤
│                                          │
│  try {                                   │
│    await request.WaitForResponse(5min)   │
│  }                                       │
│  catch (TimeoutException) {              │
│    ログ: "タイムアウト"                   │
│    return false; // 自動却下             │
│  }                                       │
│                                          │
└──────────────────────────────────────────┘

┌──────────────────────────────────────────┐
│ ProcessApprovalResponse()                │
├──────────────────────────────────────────┤
│                                          │
│  if (TryRemove(requestId, out request)) {│
│    request.SetResult(approved);          │
│    return true;                          │
│  } else {                                │
│    ログ: "リクエストが見つかりません"     │
│    return false;                         │
│  }                                       │
│                                          │
└──────────────────────────────────────────┘
```

## まとめ

この HITL アーキテクチャは以下の特徴を持ちます：

1. **非同期・ノンブロッキング**: TaskCompletionSource による効率的な待機
2. **スケーラブル**: ConcurrentDictionary による並行処理のサポート
3. **堅牢**: タイムアウトとエラーハンドリング
4. **分離されたUI**: ワークフローエンジンとUIの疎結合
5. **拡張可能**: 新しい承認タイプの追加が容易

これにより、DevUI 環境でもエンタープライズグレードの Human In The Loop 機能が実現されました。
