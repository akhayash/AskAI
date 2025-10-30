# Copilot Agent コンテキスト情報

このドキュメントは、Copilot Agent が AskAI プロジェクトを理解し、効率的に作業するために必要なコンテキスト情報をまとめたものです。

## プロジェクト概要

**AskAI** は、Microsoft Agent Framework を使用した調達領域向けのマルチエージェントワークフローサンプル集です。様々なワークフローパターンを提供し、Azure OpenAI を活用した専門家エージェント群による問い合わせ対応システムを実装しています。

### 主要なワークフロー

1. **DynamicGroupChatWorkflow**: 動的選抜 + HITL (Human-in-the-Loop)
2. **TaskBasedWorkflow**: タスク管理に基づく段階的な目標達成
3. **SelectiveGroupChatWorkflow**: 事前選抜と並列実行による効率的な専門家活用
4. **HandoffWorkflow**: ハンドオフベースの動的な専門家選抜
5. **GroupChatWorkflow**: ラウンドロビン方式の全員参加型グループチャット
6. **GraphExecutorWorkflow**: Executor と Edge による明示的なグラフベースワークフロー

## クリーンアーキテクチャ

### アーキテクチャ原則

このプロジェクトは、以下のクリーンアーキテクチャの原則に従っています：

#### 1. 関心の分離 (Separation of Concerns)

各ワークフローは独立したプロジェクトとして実装され、明確な責任を持っています：

- **Router/Planner**: 専門家の選抜またはタスクの計画
- **Specialist/Worker**: 専門領域の知識を提供
- **Moderator/Aggregator**: 複数の意見を統合
- **HITL**: 人間の承認プロセス

#### 2. 依存関係の原則

```
┌─────────────────────────────────────┐
│  ワークフロー層                      │
│  (Program.cs)                       │
│  - ワークフローのオーケストレーション  │
└──────────┬──────────────────────────┘
           │ 依存
           ↓
┌─────────────────────────────────────┐
│  エージェント層                      │
│  - ChatClientAgent                  │
│  - System Prompt                    │
└──────────┬──────────────────────────┘
           │ 依存
           ↓
┌─────────────────────────────────────┐
│  AI サービス層                       │
│  - IChatClient                      │
│  - Azure OpenAI                     │
└──────────┬──────────────────────────┘
           │ 依存
           ↓
┌─────────────────────────────────────┐
│  インフラストラクチャ層              │
│  - Configuration                    │
│  - Logging                          │
│  - Telemetry                        │
└─────────────────────────────────────┘
```

#### 3. 単一責任の原則

各エージェントは1つの専門領域のみを担当：

- **Contract Agent**: 契約関連の専門家
- **Spend Agent**: 支出分析の専門家
- **Negotiation Agent**: 交渉戦略の専門家
- **Sourcing Agent**: 調達戦略の専門家
- **Knowledge Agent**: 知識管理の専門家
- **Supplier Agent**: サプライヤー管理の専門家

#### 4. 拡張性の原則

- 新しいワークフローの追加が既存コードに影響しない
- 新しい専門家エージェントを容易に追加可能
- プロンプトと設定を外部化して変更を容易に

### Common プロジェクト

共通機能は `src/Common` プロジェクトに集約：

- Configuration 管理
- Logging 設定
- Telemetry 設定
- 共通ユーティリティ

## ロギングとテレメトリ

### OpenTelemetry 統合

すべてのワークフローは OpenTelemetry を使用した観測性を実装しています。

#### ロギング設定

```csharp
// OpenTelemetry とロギングを設定
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.AddOpenTelemetry(options =>
    {
        options.AddOtlpExporter(otlpOptions =>
        {
            otlpOptions.Endpoint = new Uri(otlpEndpoint);
        });
    });
    builder.SetMinimumLevel(LogLevel.Information);
});
```

#### テレメトリエンドポイント

- **デフォルト**: `http://localhost:4317` (Aspire Dashboard)
- **環境変数**: `OTEL_EXPORTER_OTLP_ENDPOINT`
- **Application Insights**: `APPLICATIONINSIGHTS_CONNECTION_STRING`

### ログメッセージの標準化

#### フェーズ区切り

```csharp
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("フェーズ 1: タイトル");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
```

#### ステータスメッセージ

- **成功**: `✓` または `✅`
- **警告**: `⚠️`
- **エラー**: `❌`

#### 構造化ロギング

```csharp
// 推奨
logger.LogInformation("タスク完了: {TaskId}, 担当: {AssignedWorker}", taskId, worker);

// 避けるべき
logger.LogInformation($"タスク完了: {taskId}, 担当: {worker}");
```

詳細は [Docs/development/logging-setup.md](development/logging-setup.md) を参照してください。

## ライブラリとバージョン

### .NET フレームワーク

- **.NET**: 8.0
- **言語**: C# 12
- **SDK**: .NET 8 SDK 以降

### Microsoft Agent Framework

- **Microsoft.Agents.AI.Workflows**: 1.0.0-preview.251009.1
  - エージェントワークフローの構築と実行
  - ChatClientAgent による LLM エージェントの実装
  - Handoff パターンのサポート

### AI と機械学習

- **Microsoft.Extensions.AI**: 9.9.1
  - AI モデルの統合レイヤー
  - IChatClient インターフェース
  
- **Microsoft.Extensions.AI.OpenAI**: 9.9.1-*
  - OpenAI モデルの統合
  - Azure OpenAI との接続

- **Azure.AI.OpenAI**: 2.1.0
  - Azure OpenAI サービスクライアント
  - Chat Completions API

- **Azure.Identity**: 1.12.0
  - Azure 認証
  - DefaultAzureCredential による資格情報管理

### 構成とロギング

- **Microsoft.Extensions.Configuration**: 9.0.0
- **Microsoft.Extensions.Configuration.Json**: 9.0.0
- **Microsoft.Extensions.Configuration.EnvironmentVariables**: 9.0.0
- **Microsoft.Extensions.Logging**: 9.0.0
- **Microsoft.Extensions.Logging.Console**: 9.0.0

### テレメトリ (OpenTelemetry)

- **OpenTelemetry**: 1.10.0
  - 分散トレーシング
  - メトリクス収集

- **OpenTelemetry.Exporter.Console**: 1.10.0
  - コンソール出力エクスポーター

- **OpenTelemetry.Exporter.OpenTelemetryProtocol**: 1.10.0
  - OTLP エクスポーター (Aspire Dashboard 向け)

- **OpenTelemetry.Extensions.Hosting**: 1.10.0
  - ホスティング統合

- **OpenTelemetry.Instrumentation.Http**: 1.9.0
  - HTTP 呼び出しの計測

- **Azure.Monitor.OpenTelemetry.Exporter**: 1.3.0
  - Application Insights エクスポーター

### Azure OpenAI モデル

プロジェクトで使用される主なモデル：

- **gpt-4o**: 高品質な応答が必要な専門家エージェント、Moderator
- **gpt-4o-mini**: 軽量な処理が可能な Router、分類タスク

## フォルダ構成

```
AskAI/
├── README.md                          # プロジェクトのメインドキュメント
├── AgentWorkflows.sln                 # ソリューションファイル
├── docker-compose.yml                 # Aspire Dashboard 用 Docker Compose
├── Docs/                              # ドキュメントルートディレクトリ
│   ├── COPILOT_CONTEXT.md            # このファイル（Copilot Agent 向けコンテキスト）
│   ├── architecture/                  # アーキテクチャドキュメント
│   │   ├── clean-architecture.md     # クリーンアーキテクチャの詳細
│   │   └── system-requirements.md    # システム要件定義
│   ├── development/                   # 開発ガイド
│   │   ├── logging-setup.md          # ロギング設定ガイド
│   │   └── coding-standards.md       # コーディング標準
│   └── workflows/                     # ワークフロー詳細ドキュメント
│       ├── implementation-summary.md  # SelectiveGroupChat 実装概要
│       ├── task-workflow.md          # TaskBasedWorkflow 実装概要
│       └── graph-executor.md         # GraphExecutor 調査結果
├── src/                               # ソースコードディレクトリ
│   ├── Common/                        # 共通ライブラリ
│   │   ├── Common.csproj
│   │   └── (共通コンポーネント)
│   ├── DynamicGroupChatWorkflow/      # 動的グループチャット + HITL
│   │   ├── Program.cs
│   │   ├── README.md
│   │   └── DynamicGroupChatWorkflow.csproj
│   ├── TaskBasedWorkflow/             # タスクベースワークフロー
│   │   ├── Program.cs
│   │   ├── README.md
│   │   └── TaskBasedWorkflow.csproj
│   ├── SelectiveGroupChatWorkflow/    # 選択的グループチャット
│   │   ├── Program.cs
│   │   ├── README.md
│   │   └── SelectiveGroupChatWorkflow.csproj
│   ├── GraphExecutorWorkflow/         # グラフエグゼキュータ
│   │   ├── Program.cs
│   │   ├── README.md
│   │   └── GraphExecutorWorkflow.csproj
│   ├── HandoffWorkflow/               # ハンドオフワークフロー
│   │   ├── Program.cs
│   │   └── HandoffWorkflow.csproj
│   └── GroupChatWorkflow/             # グループチャットワークフロー
│       ├── Program.cs
│       └── GroupChatWorkflow.csproj
└── appsettings.Development.json       # 開発環境設定
```

### プロジェクト構成の原則

1. **ワークフローの独立性**: 各ワークフローは独立したプロジェクトとして実装
2. **共通機能の集約**: 共通コンポーネントは `Common` プロジェクトに配置
3. **ドキュメントの構造化**: Docs フォルダ配下で目的別に整理
4. **README の役割分担**: 
   - トップレベル README: プロジェクト全体の概要
   - 各ワークフロー README: 個別ワークフローの詳細

## エージェントの実装パターン

### ChatClientAgent の作成

```csharp
static ChatClientAgent CreateSpecialistAgent(
    IChatClient chatClient, 
    string specialty, 
    string description)
{
    var instructions = $"""
あなたは {description} として回答します。
専門知識を活用してユーザーの質問に答えてください。
簡潔で実用的な回答を心がけてください。
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        $"{specialty.ToLower()}_agent",  // Agent ID
        $"{specialty} Agent");            // Agent Name
}
```

### Router パターン

```csharp
// JSON 形式で専門家を選抜
var instructions = """
あなたは Router Agent です。
ユーザーの質問を分析し、必要な専門家を選抜してください。

以下の JSON 形式で回答してください：
{
  "selected": ["専門家1", "専門家2"],
  "reason": "選抜理由"
}
""";
```

### Moderator パターン

```csharp
// 複数の意見を統合
var instructions = """
あなたは Moderator Agent です。
複数の専門家の意見を統合し、構造化された最終回答を生成してください。

以下の構造で回答してください：
## 結論
## 根拠
## 各専門家の所見
## 次のアクション
""";
```

## 設定とデプロイ

### 必須環境変数

```bash
# Azure OpenAI 設定
export AZURE_OPENAI_ENDPOINT="https://your-endpoint.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o"

# テレメトリ設定（オプション）
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317"

# Application Insights（オプション）
export APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=...;IngestionEndpoint=..."
```

### appsettings.json の構造

```json
{
  "environmentVariables": {
    "AZURE_OPENAI_ENDPOINT": "https://your-endpoint.openai.azure.com/",
    "AZURE_OPENAI_DEPLOYMENT_NAME": "gpt-4o"
  },
  "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317"
}
```

### 認証

- **Azure CLI**: `az login` によるローカル認証
- **DefaultAzureCredential**: マネージド ID やサービスプリンシパルをサポート

## ビルドとテスト

### ビルドコマンド

```bash
# ソリューション全体をビルド
dotnet build

# 特定のプロジェクトをビルド
dotnet build src/SelectiveGroupChatWorkflow

# リリースビルド
dotnet build -c Release
```

### 実行コマンド

```bash
# 特定のワークフローを実行
cd src/SelectiveGroupChatWorkflow
dotnet run

# または、ソリューションルートから
dotnet run --project src/SelectiveGroupChatWorkflow
```

### Aspire Dashboard の起動

```bash
# Docker Compose で起動
docker compose up -d

# ログ確認
docker compose logs -f aspire-dashboard

# 停止
docker compose down
```

ブラウザで `http://localhost:18888` を開いてダッシュボードにアクセス。

## コーディング規約

### 一般原則

1. **UTF-8 エンコーディング**: すべてのファイルで UTF-8 を使用
2. **日本語コメント**: ドメイン知識を含むコメントは日本語で記述
3. **構造化ロギング**: パラメータを使用した構造化ロギングを使用
4. **非同期処理**: I/O バウンドな処理は async/await を使用

### 命名規則

- **プロジェクト名**: PascalCase (例: `SelectiveGroupChatWorkflow`)
- **Agent ID**: snake_case (例: `router_agent`, `contract_agent`)
- **Agent Name**: 人間が読みやすい形式 (例: `Router Agent`, `Contract Agent`)
- **ファイル名**: PascalCase (例: `Program.cs`, `README.md`)

### エラーハンドリング

```csharp
try
{
    // 処理
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ エラー: {ErrorMessage}", ex.Message);
    // フォールバック処理
}
```

## 重要な参考資料

- [Microsoft Agent Framework ドキュメント](https://learn.microsoft.com/ja-jp/dotnet/ai/quickstarts/quickstart-ai-chat-with-agents)
- [Azure OpenAI サービス](https://learn.microsoft.com/ja-jp/azure/ai-services/openai/)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)
- [.NET Aspire Dashboard](https://learn.microsoft.com/ja-jp/dotnet/aspire/fundamentals/dashboard)

## よくある質問

### Q: 新しいワークフローを追加するには？

1. `src/` 配下に新しいプロジェクトを作成
2. `AgentWorkflows.sln` にプロジェクトを追加
3. 必要な NuGet パッケージを追加
4. `Program.cs` でワークフローを実装
5. README.md でドキュメントを作成
6. トップレベル README.md に追加

### Q: 新しい専門家エージェントを追加するには？

```csharp
var newSpecialist = CreateSpecialistAgent(
    chatClient,
    "NewSpecialty",
    "新しい専門領域の専門家");

specialists.Add("NewSpecialty", newSpecialist);
```

Router の instructions にも新しい専門家を追加することを忘れずに。

### Q: テレメトリの設定を変更するには？

環境変数 `OTEL_EXPORTER_OTLP_ENDPOINT` を設定するか、`appsettings.json` で変更します。

## まとめ

このドキュメントは、Copilot Agent が AskAI プロジェクトで効率的に作業するための包括的なコンテキスト情報を提供します。

**重要なポイント**:
- クリーンアーキテクチャに従った設計
- 構造化されたロギングとテレメトリ
- 明確なライブラリバージョン管理
- 整理されたフォルダ構造
- 一貫したコーディング規約

追加情報が必要な場合は、Docs フォルダ配下の各ドキュメントを参照してください。
