# ロギング設定ガイド

このドキュメントは、AskAI プロジェクト全体のロギングとテレメトリの設定について説明します。

## 概要

すべてのワークフロープロジェクトは、OpenTelemetry を使用した構造化ロギングを実装しています。ログは以下のいずれかに出力されます：

1. **Aspire Dashboard** (デフォルト)
2. **Application Insights** (設定されている場合)
3. **コンソール** (開発時)

## Agent Framework 内部ログの有効化

このプロジェクトでは、Microsoft Agent Framework の内部トレースを有効化しています。これにより、ワークフロー実行中のフレームワーク内部の処理を詳細に追跡できます。

### 設定方法

すべてのワークフローで、TracerProvider に以下のソースを追加しています：

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("YourWorkflowName"))
    .AddSource("Microsoft.Agents.AI.Workflows*")  // Agent Framework 内部ログ
    .AddSource("YourWorkflowName")
    .AddHttpClientInstrumentation()
    .AddOtlpExporter(exporterOptions =>
    {
        exporterOptions.Endpoint = new Uri(otlpEndpoint);
    })
    .AddConsoleExporter()
    .Build();
```

### 内部ログの利点

- **ワークフロー実行の詳細**: Executor の開始・完了、メッセージのルーティング、状態遷移などのフレームワーク内部動作を追跡
- **パフォーマンス分析**: フレームワーク内の処理時間を詳細に測定
- **デバッグの容易化**: 問題発生時にフレームワーク内部の動作を確認可能
- **エンドツーエンドのトレーシング**: アプリケーションコードとフレームワーク内部の処理を統合的に追跡

### 確認方法

Aspire Dashboard でトレースを表示すると、`Microsoft.Agents.AI.Workflows` で始まるスパンが表示されます。これらのスパンは、Agent Framework 内部の処理を表しています。

## 設定方法

### 1. Aspire Dashboard の使用（推奨）

Aspire Dashboard は、.NET Aspire に含まれる軽量なテレメトリダッシュボードです。このプロジェクトでは Docker Compose を使用して管理します。

#### Docker Compose による Aspire Dashboard の起動

プロジェクトルートに `docker-compose.yml` が配置されています。

**docker-compose.yml の内容：**

```yaml
version: "3.8"

services:
  aspire-dashboard:
    image: mcr.microsoft.com/dotnet/aspire-dashboard:8.0
    container_name: aspire-dashboard
    ports:
      - "18888:18888" # Dashboard Web UI
      - "4317:18889" # OTLP gRPC endpoint
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true
    restart: unless-stopped
```

**起動方法：**

```powershell
# Dashboard を起動（バックグラウンド実行）
docker compose up -d

# ログを確認
docker compose logs -f aspire-dashboard

# 起動状態を確認
docker compose ps

# 停止
docker compose down
```

#### アーキテクチャ

```
┌─────────────────────────────────────────────────────────────┐
│  GroupChatWorkflow (.NET アプリ)                             │
│  ┌──────────────────────────────────────┐                   │
│  │ OpenTelemetry Exporter               │                   │
│  │ - OTLP/gRPC プロトコル               │                   │
│  │ - エンドポイント: localhost:4317     │                   │
│  └──────────────────────────────────────┘                   │
└────────────────────┬────────────────────────────────────────┘
                     │ OTLP/gRPC
                     │ (ログ、トレース、メトリクス)
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  Aspire Dashboard (Docker コンテナ)                          │
│  ┌──────────────────────────────────────┐                   │
│  │ OTLP Receiver (ポート 18889)         │                   │
│  │ ↓                                    │                   │
│  │ テレメトリ処理・保存                 │                   │
│  │ ↓                                    │                   │
│  │ Web UI (ポート 18888)                │                   │
│  └──────────────────────────────────────┘                   │
└─────────────────────────────────────────────────────────────┘
                     ▲
                     │ HTTP/ブラウザアクセス
                     │
              http://localhost:18888
```

#### デフォルト設定

アプリケーション（`Program.cs`）は、環境変数を設定しない場合、デフォルトで `http://localhost:4317` に OTLP データを送信します。Docker Compose の設定により、このエンドポイントが Aspire Dashboard の OTLP Receiver（内部ポート 18889）にマッピングされます。

#### カスタム OTLP エンドポイントの設定

異なるエンドポイントを使用する場合：

```powershell
# PowerShell で環境変数を設定
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"

# アプリを起動
dotnet run --project src/GroupChatWorkflow
```

または `appsettings.Development.json` で設定：

```json
{
  "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317"
}
```

#### Aspire Dashboard へのアクセス

1. Docker Compose で Dashboard を起動：

   ```powershell
   docker compose up -d
   ```

2. ブラウザで `http://localhost:18888` を開く

3. 以下のセクションが利用可能：
   - **Resources**: アプリケーションリソースの一覧
   - **Console**: コンソールログ
   - **Structured**: 構造化ログメッセージ（フィルタリング・検索可能）
   - **Traces**: リクエストの分散トレース
   - **Metrics**: パフォーマンスメトリクス

#### 完全なワークフロー

```powershell
# 1. Dashboard を起動
docker compose up -d

# 2. Dashboard の起動を確認（ログに "Now listening on: http://[::]:18888" が表示される）
docker compose logs aspire-dashboard

# 3. 別のターミナルでアプリを起動
cd C:\Repos\AskAI
dotnet run --project src/GroupChatWorkflow

# 4. ブラウザで Dashboard を開く
# http://localhost:18888

# 5. アプリを使用すると、リアルタイムでログ・トレースが Dashboard に表示される

# 6. 終了時は Dashboard を停止
docker compose down
```

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
- ログ出力: Router、Specialist、Moderator の各フェーズ、エージェントの発言内容
- トレーシング: ワークフロー全体と各フェーズ（Router/Specialist/Moderator）の処理時間を Span で追跡
- 主要メトリクス: 専門家選抜、並列実行時間、統合結果

### HandoffWorkflow

- サービス名: `HandoffWorkflow`
- ログ出力: エージェント間のハンドオフ、各エージェントの発言内容
- トレーシング: ワークフロー全体と各エージェントの処理時間を Span で追跡
- 主要メトリクス: ハンドオフ回数、エージェント切り替え回数、メッセージ数

### TaskBasedWorkflow

- サービス名: `TaskBasedWorkflow`
- ログ出力: Planner、Worker、結果統合の各フェーズ、各タスクの実行内容
- トレーシング: ワークフロー全体と各フェーズ（Planner/Worker）の処理時間を Span で追跡
- 主要メトリクス: タスク数、完了率、実行時間、各 Worker の応答時間

### GroupChatWorkflow

- サービス名: `GroupChatWorkflow`
- ログ出力: 各エージェントの発言開始・完了、発言内容全体
- トレーシング: ワークフロー全体と各エージェントの処理時間を Span で追跡
- 主要メトリクス: メッセージ数、議論ラウンド数、各エージェントの応答時間

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
