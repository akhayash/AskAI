# コメントとログ表示形式の標準化

このドキュメントは、AskAI プロジェクト全体で統一されたコメントとログ表示形式の標準について説明します。

## 概要

すべてのワークフロープロジェクトで、以下の点が統一されています：

1. **日本語コメントの一貫性**: すべてのプロジェクトで UTF-8 エンコーディングと統一された日本語表現を使用
2. **ログメッセージの形式**: 絵文字と記号を使用した視認性の高いログメッセージ
3. **フェーズ区切りの統一**: すべてのワークフローで同じ区切り記号を使用
4. **構造化ロギング**: パラメータを使用したフィルタリング可能なログ

## フェーズ区切りの標準形式

### フェーズの開始と終了

```csharp
// フェーズ開始
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("フェーズ 1: ルーターが必要な専門家を選抜");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// フェーズ終了（オプション）
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
```

### アプリケーションの開始と終了

```csharp
// 起動時
logger.LogInformation("=== アプリケーション起動 ===");

// 終了時
logger.LogInformation("=== アプリケーション終了 ===");
```

## ログメッセージの標準形式

### 成功メッセージ

```csharp
logger.LogInformation("✓ 成功: {SuccessMessage}", message);
logger.LogInformation("✓ {TaskCount} 個のタスクを作成しました", taskCount);
logger.LogInformation("✓ Task {TaskId} 完了", taskId);
```

### 警告メッセージ

```csharp
logger.LogWarning("⚠️ 警告: {WarningMessage}", message);
logger.LogWarning("⚠️ 専門家が選抜されませんでした。Knowledge 専門家をデフォルトで使用します。");
logger.LogWarning("⚠️ 最大メッセージ数 ({MaxMessages}) に達しました。", maxMessages);
```

### エラーメッセージ

```csharp
logger.LogError(ex, "❌ エラー: {ErrorMessage}", ex.Message);
logger.LogError(ex, "❌ {ComponentName} エラー: {ErrorMessage}", componentName, ex.Message);
logger.LogError("❌ {SpecialistName} エージェントのエラー: {ErrorMessage}", specialistName, ex.Message);
```

### 情報メッセージ

```csharp
logger.LogInformation("エンドポイント: {Endpoint}", endpoint);
logger.LogInformation("デプロイメント名: {DeploymentName}", deployment);
logger.LogInformation("受信した質問: {Question}", question);
```

## 構造化ロギングのベストプラクティス

### 推奨される方法

```csharp
// ✅ 良い例: 構造化パラメータを使用
logger.LogInformation("タスク完了: {TaskId}, 担当: {AssignedWorker}", taskId, worker);

// ✅ 良い例: 複数のパラメータを使用
logger.LogInformation("[{MessageCount}] {AgentName} の発言", messageCount, agentName);

// ✅ 良い例: エラーオブジェクトを渡す
logger.LogError(ex, "❌ {ComponentName} エラー: {ErrorMessage}", componentName, ex.Message);
```

### 避けるべき方法

```csharp
// ❌ 悪い例: 文字列補間を使用（構造化されない）
logger.LogInformation($"タスク完了: {taskId}, 担当: {worker}");

// ❌ 悪い例: 連結された文字列
logger.LogInformation("タスク完了: " + taskId + ", 担当: " + worker);

// ❌ 悪い例: Exception.ToString() を使用
logger.LogError($"エラー: {ex.ToString()}");
```

## プロジェクト別の統一例

### SelectiveGroupChatWorkflow

```csharp
// 起動
logger.LogInformation("=== アプリケーション起動 ===");
logger.LogInformation("テレメトリ設定: OTLP Endpoint = {OtlpEndpoint}", otlpEndpoint);

// フェーズ 1
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("フェーズ 1: ルーターが必要な専門家を選抜");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// 結果
logger.LogInformation("✓ 選抜された専門家: {SelectedSpecialists}", string.Join(", ", specialists));
logger.LogInformation("✓ 選抜理由: {SelectionReason}", reason);

// エラー
logger.LogError(ex, "❌ ルーター実行エラー: {ErrorMessage}", ex.Message);
```

### HandoffWorkflow

```csharp
// ワークフロー開始
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("ワークフロー実行開始");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// メッセージ開始
logger.LogInformation("[メッセージ開始 #{MessageCount}] エージェント名: {AgentName}, エージェントID: {AgentId}, ロール: {Role}", 
    messageCount, agentName, agentId, role);

// メッセージ完了
logger.LogInformation("[メッセージ完了 #{MessageCount}] エージェント: {AgentId}, 内容: {Content}", 
    messageCount, agentId, content);
```

### TaskBasedWorkflow

```csharp
// フェーズ 1
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("フェーズ 1: Planner がタスク計画を作成");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// タスク実行
logger.LogInformation("[Task {TaskId}] {TaskDescription}", taskId, description);
logger.LogInformation("担当: {AssignedWorker}", worker);
logger.LogInformation("受け入れ基準: {Acceptance}", acceptance);

// 完了
logger.LogInformation("✓ Task {TaskId} 完了", taskId);
logger.LogInformation("完了率: {CompletedTasks}/{TotalTasks} ({CompletionRate:F1}%)", 
    completed, total, completionRate);
```

### GroupChatWorkflow

```csharp
// グループチャット開始
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("Group Chat ワークフロー実行開始");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// 発言開始
logger.LogInformation("[{MessageCount}] {AgentName} の発言", messageCount, agentName);

// 完了
logger.LogInformation("合計メッセージ数: {MessageCount}", messageCount);
```

## 絵文字の使用ガイドライン

| 用途 | 絵文字 | 使用例 |
|-----|-------|--------|
| 成功 | ✓ または ✅ | `✓ タスク完了` |
| 警告 | ⚠️ | `⚠️ タイムアウト` |
| エラー | ❌ | `❌ エラー発生` |
| 進行中 | 🔄 | `🔄 処理中` |
| 待機中 | ⏳ | `⏳ キュー待ち` |
| ブロック | 🚫 | （現在は使用していない） |

## 区切り記号の使用ガイドライン

| 用途 | 記号 | 使用例 |
|-----|------|--------|
| メインフェーズ | `━` (U+2501) | フェーズ区切り |
| サブセクション | `─` (U+2500) | グループチャットのメッセージ枠 |
| 等号 | `=` | アプリケーション開始/終了 |
| ハイフン | `-` | サブタスクや箇条書き |

## コメントの一貫性

### コード内コメント

```csharp
// 環境変数を設定から取得（appsettings.json → 環境変数の順で優先）
var endpoint = configuration["environmentVariables:AZURE_OPENAI_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("環境変数 AZURE_OPENAI_ENDPOINT が設定されていません。");

// OpenTelemetry とロギングを設定
using var loggerFactory = LoggerFactory.Create(builder =>
{
    // ... 設定
});

// 専門家エージェントを作成
var specialists = new Dictionary<string, ChatClientAgent>
{
    ["Contract"] = CreateSpecialistAgent(extensionsAIChatClient, "Contract", "契約関連の専門家"),
    // ...
};
```

### エージェント説明コメント

```csharp
static ChatClientAgent CreateSpecialistAgent(IChatClient chatClient, string specialty, string description)
{
    var instructions = $"""
あなたは {description} として回答します。
専門知識を活用してユーザーの質問に答えてください。
簡潔で実用的な回答を心がけてください。
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        $"{specialty.ToLower()}_agent",
        $"{specialty} Agent");
}
```

## 文字エンコーディング

すべてのプロジェクトファイルは **UTF-8** エンコーディングを使用しています：

```csharp
// コンソールの文字エンコーディングを UTF-8 に設定
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;
```

## まとめ

これらの標準に従うことで：

1. ✅ すべてのプロジェクトで一貫したログ出力
2. ✅ 視認性の高いログメッセージ
3. ✅ 構造化されたフィルタリング可能なログ
4. ✅ Aspire Dashboard や Application Insights での効果的な分析
5. ✅ 保守性の向上とデバッグの効率化

詳細なロギング設定については [ロギング設定ガイド](logging-setup.md) を参照してください。
