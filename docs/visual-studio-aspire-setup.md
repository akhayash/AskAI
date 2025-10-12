# Visual Studio で Aspire Dashboard を自動起動する

このガイドでは、Visual Studio でプロジェクトを実行すると自動的に Aspire Dashboard が起動し、ログとテレメトリを確認できるようにする方法を説明します。

## 前提条件

- Visual Studio 2022 (バージョン 17.9 以降)
- .NET 8.0 SDK 以降
- Docker Desktop (Aspire Dashboard のコンテナ実行用)
- .NET Aspire Workload

## セットアップ手順

### 1. Aspire Workload のインストール

Visual Studio で Aspire を使用するには、Aspire workload をインストールする必要があります。

**コマンドラインからのインストール:**
```bash
dotnet workload install aspire
```

**Visual Studio Installer からのインストール:**
1. Visual Studio Installer を開く
2. 「変更」をクリック
3. 「Individual components」タブを選択
4. 「.NET Aspire SDK」を検索してチェック
5. 「変更」をクリックしてインストール

### 2. プロジェクト構成

このリポジトリには、すでに以下の Aspire プロジェクトが含まれています：

- **AskAI.AppHost**: Aspire オーケストレーター（すべてのワークフローを管理）
- **AskAI.ServiceDefaults**: 共通の OpenTelemetry 設定

各ワークフロープロジェクト（SelectiveGroupChatWorkflow、HandoffWorkflow、TaskBasedWorkflow、GroupChatWorkflow）は、ServiceDefaults を参照して OpenTelemetry を自動的に構成します。

### 3. Visual Studio でのプロジェクト実行

#### 方法 1: AppHost を直接実行（推奨）

1. Visual Studio でソリューションを開く
2. ソリューションエクスプローラーで `AskAI.AppHost` を右クリック
3. 「スタートアップ プロジェクトとして設定」を選択
4. F5 キーまたは「開始」ボタンをクリック

これにより：
- Aspire Dashboard が自動的に起動します
- ブラウザが開き、Dashboard（通常 `https://localhost:17xxx/`）が表示されます
- すべてのワークフローアプリが Dashboard に登録されます
- 各アプリのログ、トレース、メトリクスがリアルタイムで確認できます

#### 方法 2: 個別のワークフロー  を実行

個別のワークフローを実行することもできますが、Aspire Dashboard は自動起動しません。ログは引き続きコンソールとOTLPエンドポイントに送信されます。

1. 実行したいワークフロープロジェクト（例: `SelectiveGroupChatWorkflow`）を右クリック
2. 「スタートアップ プロジェクトとして設定」を選択
3. F5 キーで実行

### 4. Aspire Dashboard の機能

Aspire Dashboard では以下を確認できます：

#### リソース（Resources）タブ
- 実行中のすべてのワークフローアプリの状態
- 各アプリの実行ログ（stdout/stderr）
- アプリの起動・停止状態

#### ログ（Logs）タブ
- OpenTelemetry で送信された構造化ログ
- ログレベル（Information、Warning、Error）でフィルタリング
- タイムスタンプ、サービス名、メッセージでの検索
- ログの詳細情報（プロパティ、スタックトレースなど）

#### トレース（Traces）タブ
- 分散トレーシング情報
- リクエストの流れと処理時間
- 各スパンの詳細とタイミング

#### メトリクス（Metrics）タブ
- ランタイムメトリクス（CPU、メモリ、GC）
- カスタムメトリクス
- グラフによる可視化

### 5. プロジェクト固有の設定

#### appsettings.json の設定

各ワークフロープロジェクトの `appsettings.Development.json` で設定をカスタマイズできます：

```json
{
  "environmentVariables": {
    "AZURE_OPENAI_ENDPOINT": "https://your-endpoint.openai.azure.com/",
    "AZURE_OPENAI_DEPLOYMENT_NAME": "gpt-4o"
  },
  "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317"
}
```

AppHost から実行する場合、`OTEL_EXPORTER_OTLP_ENDPOINT` は自動的に設定されます。

#### launchSettings.json の確認

`Properties/launchSettings.json` で起動設定を確認・変更できます：

```json
{
  "profiles": {
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317"
      }
    }
  }
}
```

### 6. トラブルシューティング

#### Aspire Dashboard が起動しない

**原因**: Docker Desktop が起動していない

**解決方法**:
1. Docker Desktop を起動
2. Docker が実行中であることを確認: `docker ps`
3. Visual Studio でプロジェクトを再実行

#### ポートの競合

**原因**: 既に使用されているポートがある

**解決方法**:
1. 使用中のプロセスを停止
2. または `Properties/launchSettings.json` でポートを変更

#### ログが Dashboard に表示されない

**原因**: OTLP エンドポイントの設定が間違っている

**解決方法**:
1. `OTEL_EXPORTER_OTLP_ENDPOINT` 環境変数を確認
2. AppHost の `Program.cs` で設定されているエンドポイントを確認
3. Aspire Dashboard のログで接続エラーを確認

### 7. AppHost のカスタマイズ

`src/AskAI.AppHost/Program.cs` で、実行するワークフローをカスタマイズできます：

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// 特定のワークフローのみを追加
var selectiveGroupChat = builder.AddProject<Projects.SelectiveGroupChatWorkflow>("selectivegroupchat")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");

// 環境変数を追加
selectiveGroupChat.WithEnvironment("AZURE_OPENAI_ENDPOINT", "https://your-endpoint.openai.azure.com/")
    .WithEnvironment("AZURE_OPENAI_DEPLOYMENT_NAME", "gpt-4o");

builder.Build().Run();
```

## まとめ

Visual Studio で Aspire Dashboard を使用することで：

✅ ワンクリックでダッシュボードが起動  
✅ すべてのワークフローのログを一元管理  
✅ リアルタイムでログ・トレース・メトリクスを確認  
✅ フィルタリングと検索で効率的なデバッグ  
✅ 分散トレーシングでパフォーマンス分析  

詳細なドキュメント：
- [.NET Aspire ドキュメント](https://learn.microsoft.com/ja-jp/dotnet/aspire/)
- [Aspire Dashboard](https://learn.microsoft.com/ja-jp/dotnet/aspire/fundamentals/dashboard)
- [ロギング設定ガイド](logging-setup.md)
