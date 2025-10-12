# ロギング設定ガイド

このドキュメントは、AskAI プロジェクト全体のロギングとテレメトリの設定について説明します。

## 概要

すべてのワークフロープロジェクトは、OpenTelemetry を使用した構造化ロギングを実装しています。ログは以下のいずれかに出力されます：

1. **Aspire Dashboard** (デフォルト)
2. **Application Insights** (設定されている場合)
3. **コンソール** (開発時)

## 設定方法

### 1. Aspire Dashboard の使用（推奨）

Aspire Dashboard は、.NET Aspire に含まれる軽量なテレメトリダッシュボードです。

#### Aspire Dashboard のインストール

```bash
# .NET Aspire をインストール
dotnet workload install aspire

# または、スタンドアロン版の Aspire Dashboard を Docker で実行
docker run --rm -it -p 18888:18888 -p 4317:18889 --name aspire-dashboard \
    mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

#### デフォルト設定

環境変数を設定しない場合、デフォルトで `http://localhost:4317` に OTLP データを送信します。

#### カスタム OTLP エンドポイントの設定

```bash
# 環境変数で設定
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317"

# または appsettings.Development.json で設定
{
  "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317"
}
```

#### Aspire Dashboard へのアクセス

ブラウザで `http://localhost:18888` を開くと、以下が確認できます：

- **Logs**: 構造化されたログメッセージ
- **Traces**: リクエストの分散トレース
- **Metrics**: パフォーマンスメトリクス

### 2. Application Insights の使用

Azure Application Insights を使用する場合は、接続文字列を設定します。

#### 環境変数で設定

```bash
export APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=...;IngestionEndpoint=..."
```

#### appsettings.Development.json で設定

```json
{
  "APPLICATIONINSIGHTS_CONNECTION_STRING": "InstrumentationKey=...;IngestionEndpoint=..."
}
```

## ログレベル

各プロジェクトは以下のログレベルを使用しています：

- **Information**: 一般的な情報メッセージ（起動、設定、フェーズ遷移など）
- **Warning**: 警告メッセージ（フォールバック、リトライなど）
- **Error**: エラーメッセージ（例外、失敗など）

## コメントとログメッセージの統一

### 統一されたフォーマット

すべてのプロジェクトで以下のフォーマットが統一されています：

```csharp
// フェーズ区切り
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("フェーズ 1: タイトル");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// エラーメッセージ
logger.LogError(ex, "❌ エラー: {ErrorMessage}", ex.Message);

// 警告メッセージ
logger.LogWarning("⚠️ 警告: {WarningMessage}", message);

// 成功メッセージ
logger.LogInformation("✓ 成功: {SuccessMessage}", message);
```

### 構造化ロギング

すべてのログメッセージは構造化されており、プロパティを使用してフィルタリングや検索が可能です：

```csharp
// 良い例: 構造化ロギング
logger.LogInformation("エンドポイント: {Endpoint}", endpoint);
logger.LogInformation("タスク完了: {TaskId}, 担当: {AssignedWorker}", taskId, worker);

// 悪い例: 文字列補間（避ける）
logger.LogInformation($"エンドポイント: {endpoint}");  // ❌
```

## プロジェクト別の実装

### SelectiveGroupChatWorkflow

- サービス名: `SelectiveGroupChatWorkflow`
- ログ出力: Router、Specialist、Moderator の各フェーズ
- 主要メトリクス: 専門家選抜、並列実行時間、統合結果

### HandoffWorkflow

- サービス名: `HandoffWorkflow`
- ログ出力: エージェント間のハンドオフ、メッセージ完了
- 主要メトリクス: ハンドオフ回数、エージェント切り替え回数

### TaskBasedWorkflow

- サービス名: `TaskBasedWorkflow`
- ログ出力: Planner、Worker、結果統合の各フェーズ
- 主要メトリクス: タスク数、完了率、実行時間

### GroupChatWorkflow

- サービス名: `GroupChatWorkflow`
- ログ出力: 各エージェントの発言、ラウンド数
- 主要メトリクス: メッセージ数、議論ラウンド数

## トラブルシューティング

### Aspire Dashboard に接続できない

1. Aspire Dashboard が起動しているか確認
2. ポート 4317 が開放されているか確認
3. OTLP エンドポイントが正しく設定されているか確認

### Application Insights にデータが表示されない

1. 接続文字列が正しいか確認
2. Azure ポータルで Application Insights リソースが有効か確認
3. ネットワーク接続を確認

### ログが出力されない

1. ログレベルが適切に設定されているか確認（デフォルトは `Information`）
2. LoggerFactory が正しく初期化されているか確認

## 参考資料

- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)
- [.NET Aspire Dashboard](https://learn.microsoft.com/ja-jp/dotnet/aspire/fundamentals/dashboard)
- [Azure Monitor OpenTelemetry Exporter](https://learn.microsoft.com/ja-jp/azure/azure-monitor/app/opentelemetry-enable)
- [Microsoft.Extensions.Logging](https://learn.microsoft.com/ja-jp/dotnet/core/extensions/logging)
