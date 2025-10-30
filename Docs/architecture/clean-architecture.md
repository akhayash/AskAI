# クリーンアーキテクチャ

このドキュメントでは、AskAI プロジェクトにおけるクリーンアーキテクチャの適用について詳細に説明します。

## クリーンアーキテクチャとは

クリーンアーキテクチャは、ソフトウェアを独立したレイヤーに分離し、依存関係を一方向に保つことで、保守性、テスト可能性、拡張性を向上させる設計原則です。

### 基本原則

1. **依存関係の逆転**: 外側のレイヤーは内側のレイヤーに依存するが、その逆はない
2. **関心の分離**: 各レイヤーは明確な責任を持つ
3. **独立性**: ビジネスロジックはフレームワークやデータベースから独立
4. **テスト可能性**: 各レイヤーを独立してテスト可能

## AskAI のレイヤー構成

```
┌────────────────────────────────────────────────────────────┐
│  外部インターフェース層                                      │
│  - ユーザー入力 (Console)                                   │
│  - 設定ファイル (appsettings.json)                         │
└──────────────┬─────────────────────────────────────────────┘
               │
               ↓
┌────────────────────────────────────────────────────────────┐
│  ワークフロー層 (Presentation/Application Layer)           │
│  - Program.cs                                              │
│  - ワークフローオーケストレーション                         │
│  - フェーズ管理                                            │
└──────────────┬─────────────────────────────────────────────┘
               │
               ↓
┌────────────────────────────────────────────────────────────┐
│  エージェント層 (Domain Layer)                              │
│  - ChatClientAgent                                         │
│  - Router / Planner                                        │
│  - Specialist / Worker                                     │
│  - Moderator / Aggregator                                  │
│  - HITL Agent                                              │
└──────────────┬─────────────────────────────────────────────┘
               │
               ↓
┌────────────────────────────────────────────────────────────┐
│  AI サービス層 (Application Service Layer)                 │
│  - IChatClient インターフェース                            │
│  - Microsoft.Extensions.AI                                │
└──────────────┬─────────────────────────────────────────────┘
               │
               ↓
┌────────────────────────────────────────────────────────────┐
│  インフラストラクチャ層 (Infrastructure Layer)             │
│  - Azure OpenAI クライアント                               │
│  - Configuration                                           │
│  - Logging                                                 │
│  - Telemetry (OpenTelemetry)                              │
│  - Authentication (Azure.Identity)                        │
└────────────────────────────────────────────────────────────┘
```

## 各レイヤーの詳細

### 1. 外部インターフェース層

**責任**: ユーザーとの対話、外部設定の読み込み

**コンポーネント**:
- Console I/O (ユーザー入力/出力)
- appsettings.json (設定ファイル)
- 環境変数

**原則**:
- ユーザー体験に関する処理のみ
- ビジネスロジックを含まない
- ワークフロー層への委譲

### 2. ワークフロー層

**責任**: ワークフローの構成とオーケストレーション

**コンポーネント**:
- `Program.cs`: メインエントリーポイント
- フェーズ管理ロジック
- エージェント間の調整

**実装例** (SelectiveGroupChatWorkflow):
```csharp
// フェーズ 1: Router が専門家を選抜
var selectedSpecialists = await ExecuteRouterAsync(routerAgent, question, ...);

// フェーズ 2: 専門家が並列実行
var opinions = await ExecuteSpecialistsAsync(specialists, selectedSpecialists, question, ...);

// フェーズ 3: Moderator が統合
var finalOutput = await ExecuteModeratorAsync(moderatorAgent, question, opinions, ...);
```

**原則**:
- ワークフローの構造を明確に定義
- エージェント層への依存のみ
- エラーハンドリングとフォールバック
- ロギングとテレメトリ

### 3. エージェント層

**責任**: ビジネスロジックとドメインモデル

**コンポーネント**:

#### Router / Planner エージェント
- 質問分析
- 専門家選抜 / タスク計画
- JSON 形式での構造化出力

```csharp
static ChatClientAgent CreateRouterAgent(IChatClient chatClient)
{
    var instructions = """
あなたは Router Agent です。
ユーザーの質問を分析し、必要な専門家を選抜してください。

以下の JSON 形式で回答してください：
{
  "selected": ["専門家1", "専門家2"],
  "reason": "選抜理由"
}
""";
    
    return new ChatClientAgent(
        chatClient,
        instructions,
        "router_agent",
        "Router Agent");
}
```

#### Specialist / Worker エージェント
- 専門領域の知識提供
- 独立した意見生成

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
        $"{specialty.ToLower()}_agent",
        $"{specialty} Agent");
}
```

#### Moderator / Aggregator エージェント
- 複数の意見の統合
- 構造化された最終回答の生成

```csharp
static ChatClientAgent CreateModeratorAgent(IChatClient chatClient)
{
    var instructions = """
あなたは Moderator Agent です。
複数の専門家の意見を統合し、構造化された最終回答を生成してください。

以下の構造で回答してください：
## 結論
## 根拠
## 各専門家の所見
## 次のアクション
""";
    
    return new ChatClientAgent(
        chatClient,
        instructions,
        "moderator_agent",
        "Moderator Agent");
}
```

**原則**:
- 単一責任: 各エージェントは1つの専門領域のみ
- 独立性: エージェント間の直接的な依存なし
- プロンプトエンジニアリング: System Prompt による動作定義
- AI サービス層への依存のみ

### 4. AI サービス層

**責任**: AI モデルとの統合インターフェース

**コンポーネント**:
- `IChatClient`: Microsoft.Extensions.AI の統合インターフェース
- Chat Completions API のラッパー

```csharp
// IChatClient の作成
var extensionsAIChatClient = azureOpenAIClient
    .AsChatClient(deploymentName);
```

**原則**:
- モデルの抽象化
- プロバイダーの切り替えが容易
- エージェント層とインフラストラクチャ層の橋渡し

### 5. インフラストラクチャ層

**責任**: 外部システムとの統合

**コンポーネント**:

#### Azure OpenAI クライアント
```csharp
var credential = new DefaultAzureCredential();
var azureOpenAIClient = new AzureOpenAIClient(
    new Uri(endpoint),
    credential);
```

#### Configuration
```csharp
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();
```

#### Logging
```csharp
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.AddOpenTelemetry(options => { ... });
    builder.SetMinimumLevel(LogLevel.Information);
});
```

#### Telemetry
```csharp
var activitySource = new ActivitySource("SelectiveGroupChatWorkflow");

using var activity = activitySource.StartActivity("Phase1_Router");
activity?.SetTag("question", question);
```

**原則**:
- 技術的な詳細の隠蔽
- 設定の外部化
- 依存性注入の準備

## Common プロジェクト

共通機能を集約したプロジェクト:

```
src/Common/
├── Common.csproj
└── (将来の共通コンポーネント)
```

**目的**:
- 共通設定の一元管理
- 共通ユーティリティの提供
- 横断的関心事の実装 (ロギング、テレメトリなど)

**現在の構成**:
- Configuration パッケージ
- Logging パッケージ
- OpenTelemetry パッケージ

## 依存関係の管理

### NuGet パッケージの分類

#### エージェント層
- Microsoft.Agents.AI.Workflows
- Microsoft.Extensions.AI

#### AI サービス層
- Microsoft.Extensions.AI.OpenAI

#### インフラストラクチャ層
- Azure.AI.OpenAI
- Azure.Identity
- Microsoft.Extensions.Configuration.*
- Microsoft.Extensions.Logging.*
- OpenTelemetry.*

### 依存関係の原則

1. **上位レイヤーは下位レイヤーに依存できる**
2. **下位レイヤーは上位レイヤーに依存してはいけない**
3. **同じレイヤー内の依存は最小限に**
4. **インターフェースによる抽象化を推奨**

## テスト戦略

### 単体テスト

各エージェントを独立してテスト:

```csharp
[Test]
public async Task RouterAgent_ShouldSelectCorrectSpecialists()
{
    // Arrange
    var mockChatClient = CreateMockChatClient();
    var routerAgent = CreateRouterAgent(mockChatClient);
    
    // Act
    var result = await routerAgent.InvokeAsync("契約に関する質問");
    
    // Assert
    Assert.Contains("Contract", result.Selected);
}
```

### 統合テスト

ワークフロー全体をテスト:

```csharp
[Test]
public async Task SelectiveGroupChatWorkflow_EndToEnd()
{
    // Arrange
    var workflow = CreateWorkflow();
    
    // Act
    var result = await workflow.ExecuteAsync("テスト質問");
    
    // Assert
    Assert.IsNotNull(result.FinalOutput);
    Assert.IsTrue(result.QualityScore > 80);
}
```

## エラーハンドリング戦略

### レイヤー別のエラーハンドリング

#### ワークフロー層
- ワークフロー全体のリトライ
- フォールバック処理
- ユーザーへのエラー通知

```csharp
try
{
    var result = await ExecuteRouterAsync(...);
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ Router エラー: {ErrorMessage}", ex.Message);
    // Knowledge エージェントへフォールバック
    return await FallbackToKnowledgeAsync(...);
}
```

#### エージェント層
- エージェント単位のエラー処理
- 部分的な失敗の許容

```csharp
foreach (var specialist in selectedSpecialists)
{
    try
    {
        opinions[specialist] = await ExecuteSpecialistAsync(...);
    }
    catch (Exception ex)
    {
        logger.LogWarning("⚠️ {Specialist} エラー: {Message}", specialist, ex.Message);
        // 他の専門家の結果で継続
    }
}
```

#### インフラストラクチャ層
- 接続エラーのリトライ
- タイムアウト処理

```csharp
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(3, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

await retryPolicy.ExecuteAsync(async () =>
{
    return await azureOpenAIClient.GetChatCompletionAsync(...);
});
```

## 拡張性の確保

### 新しいワークフローの追加

1. `src/` 配下に新しいプロジェクトを作成
2. 既存のエージェント実装を再利用
3. ワークフロー特有のオーケストレーションロジックを実装
4. Common プロジェクトの共通機能を活用

### 新しいエージェントの追加

1. `CreateSpecialistAgent` パターンに従う
2. 専門領域に特化した System Prompt を定義
3. Router/Planner の選抜ロジックに追加

### 設定の外部化

```json
{
  "Specialists": [
    {
      "Name": "Contract",
      "Description": "契約関連の専門家",
      "SystemPrompt": "あなたは契約関連の専門家として..."
    }
  ]
}
```

## パフォーマンスの考慮

### 並列実行

専門家エージェントの並列実行でパフォーマンスを向上:

```csharp
var tasks = selectedSpecialists.Select(async specialist =>
{
    return await ExecuteSpecialistAsync(specialist, question);
});

var results = await Task.WhenAll(tasks);
```

### キャッシング

将来的なキャッシング戦略:
- Router の選抜結果
- 頻繁な質問への回答
- 専門家の応答パターン

## まとめ

AskAI プロジェクトは、クリーンアーキテクチャの原則に従って設計されています：

**主要な利点**:
1. ✅ **保守性**: 各レイヤーが独立して変更可能
2. ✅ **テスト可能性**: 各コンポーネントを独立してテスト可能
3. ✅ **拡張性**: 新しいワークフローやエージェントを容易に追加
4. ✅ **独立性**: ビジネスロジックがインフラストラクチャから独立
5. ✅ **明確性**: 各レイヤーの責任が明確

この設計により、長期的な保守とスケーラビリティが確保されています。
