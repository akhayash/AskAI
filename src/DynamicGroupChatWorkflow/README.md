# DynamicGroupChatWorkflow

Router が会話の流れに応じて専門家を動的に選抜し、必要に応じてユーザーにも意見を求める適応的なワークフローです。

## 概要

このワークフローは、以下のフェーズで動作します:

1. **Router による動的選抜**: 質問内容と会話の進行状況に応じて、必要な専門家を順次選抜
2. **専門家の意見収集**: 選抜された専門家が順次意見を提供し、Router が調整
3. **Human-in-the-Loop (HITL)**: Router が判断した場合、ユーザーに追加情報を求める
4. **Moderator による統合**: 全ての情報を統合して構造化された最終回答を生成

## アーキテクチャ

```
ユーザー質問
    ↓
Router (動的選抜・調整)
    ↓
Specialist 1 → Router → Specialist 2 → Router → ...
    ↓ (必要に応じて)
User Input (HITL) → Router
    ↓
Moderator (最終統合)
    ↓
最終回答
```

## 特徴

### 1. 動的な専門家選抜

Router が質問内容と会話の進行状況に応じて、必要な専門家を順次選抜します。

### 2. Human-in-the-Loop (HITL) with Loop Monitoring

Router が「？」で終わる質問を出力した場合、システムがユーザー入力待ちに移行します。従来の Human Agent パターンではなく、**ループ監視方式**を採用しています。

**ループ監視方式の利点:**

- 現在の Agent Framework バージョンとの互換性
- シンプルな実装
- ストリーミング出力との親和性が高い

**動作フロー:**

1. Router が専門家の意見を収集
2. Router が「予算の上限を教えてください。」のような質問を出力
3. システムが「？」を検出し、ユーザー入力待ち状態に移行
4. ユーザーが回答を入力
5. 回答を会話履歴に追加してワークフローを再実行
6. Router が新しい情報を踏まえて次のアクションを決定

現状は 1 回の追加質問に対応し、ユーザー回答を反映した後にワークフローを再実行して終了します（マルチターン対応は今後の拡張予定）。

### 3. 専門家間の対話

会話履歴が引き継がれるため、専門家が他の意見を参照しながら回答できます。

### 4. Moderator による統合

全ての情報（専門家の意見 + ユーザー入力）を統合して、構造化された最終回答を生成します。

### 5. OpenTelemetry によるテレメトリ

OpenTelemetry を使用して、ワークフローの実行状況をログとトレースで記録します。コンソール出力に加えて、OTLP エンドポイント（デフォルト `http://localhost:4317`）へエクスポートします。

## エージェント

### Router Agent

- 専門家を動的に選抜・調整
- 必要に応じてユーザーに追加情報を依頼（「？」で終わる質問）
- 十分な情報が集まったら Moderator にハンドオフ

### Specialist Agents

- Contract Agent (契約関連)
- Spend Agent (支出分析)
- Negotiation Agent (交渉戦略)
- Sourcing Agent (調達戦略)
- Knowledge Agent (知識管理)
- Supplier Agent (サプライヤー管理)

### Moderator Agent

- 全ての情報を統合して最終回答を生成

## 使用方法

### 前提条件

1. Azure OpenAI サービスへのアクセス
2. Azure CLI でログイン済み（`DefaultAzureCredential` は Azure CLI 認証のみを使用）
3. 環境変数の設定:
   - `AZURE_OPENAI_ENDPOINT`
   - `AZURE_OPENAI_DEPLOYMENT_NAME`
   - (オプション) `APPLICATIONINSIGHTS_CONNECTION_STRING`
   - (オプション) `OTEL_EXPORTER_OTLP_ENDPOINT` (デフォルト: `http://localhost:4317`)

または、`appsettings.json` / `appsettings.Development.json` の `environmentVariables` セクションに設定を記載できます。

### 実行

```bash
cd src/DynamicGroupChatWorkflow
dotnet run
```

### 実行例

```text
質問> 新しいサプライヤーとの契約交渉で注意すべき点は？予算は500万円です。

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
ワークフロー実行開始
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

━━━ Router Agent ━━━
Contract Agent にハンドオフします。

━━━ Contract Agent ━━━
契約条項の明確化が重要です。特に支払条件、納期、違約金について...

━━━ Router Agent ━━━
希望する契約期間を教えてください。

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
👤 ユーザー入力が必要です
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

回答> 3年希望です

✓ 回答を送信しました

━━━ Router Agent ━━━
予算500万円、期間3年の情報を踏まえて、Sourcing Agent にハンドオフします。

━━━ Sourcing Agent ━━━
予算500万円、期間3年であれば、既存サプライヤーベースの拡張が現実的です...

━━━ Router Agent ━━━
十分な情報が集まりました。Moderator Agent にハンドオフします。

━━━ Moderator Agent ━━━

## 結論
予算500万円、契約期間3年の制約下では、契約条項の明確化と既存サプライヤーベースの拡張が最適解です。

## 根拠
- Contract Agent: 契約条項の明確化が必須
- Sourcing Agent: 予算・期限制約下では既存ベース拡張が現実的

## 各専門家の所見
- Contract: 契約条項の明確化が最優先
- Sourcing: 既存サプライヤーベースの拡張を推奨

## ユーザーからの追加情報
- 予算: 500万円
- 希望契約期間: 3年

## 次のアクション
1. 既存サプライヤーの評価
2. 契約条項ドラフトの作成
3. 交渉ポイントの洗い出し

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
ワークフロー完了
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

## OpenTelemetry によるログ / トレース

### 出力先

1. **コンソール**: SimpleConsole フォーマッタと OpenTelemetry コンソールエクスポーターで可読性の高いログを出力
2. **OTLP エンドポイント**: 設定された OTLP エンドポイント（デフォルト: `http://localhost:4317`）にログとトレースを送信

> 補足: `APPLICATIONINSIGHTS_CONNECTION_STRING` は読み込まれますが、既定では Azure Monitor エクスポーターは有効化されていません。必要に応じて `AddAzureMonitorLogExporter` / `AddAzureMonitorTraceExporter` などを追加してください。

### 記録内容

- アプリケーション起動・終了、テレメトリ設定
- 各エージェントターン開始／終了とストリーミング更新数
- ユーザー入力待機イベントと受信した回答
- エラーやタイムアウト、タイムアウト理由

### Jaeger での確認

OTLP エンドポイントとして Jaeger を使用する場合:

```bash
docker run -d --name jaeger \
  -e COLLECTOR_OTLP_ENABLED=true \
  -p 16686:16686 \
  -p 4317:4317 \
  jaegertracing/all-in-one:latest
```

ブラウザで `http://localhost:16686` を開き、トレースを確認できます。

## 既存ワークフローとの比較

| 項目             | SelectiveGroupChat | DynamicGroupChat         |
| ---------------- | ------------------ | ------------------------ |
| **選抜方式**     | 事前選抜 (JSON)    | 動的選抜 (ハンドオフ)    |
| **実行方式**     | 並列実行           | 順次ハンドオフ           |
| **HITL**         | ❌ なし            | ✅ あり (ループ監視)     |
| **専門家間対話** | ❌ 不可            | ✅ 可能                  |
| **可視化**       | ❌ 困難            | ✅ 可能 (Reflection API) |
| **テレメトリ**   | ❌ なし            | ✅ OpenTelemetry         |
| **実行速度**     | ✅ 高速            | ⚠️ やや低速              |
| **柔軟性**       | ⚠️ 固定            | ✅ 動的                  |

## 技術詳細

### HITL の実装方式

当初は Agent Framework の `Agent` 基底クラスを継承した Human Agent パターンを検討しましたが、現在のバージョン (1.0.0-preview.251009.1) では `Agent` クラスが提供されていないため、**ループ監視方式**を採用しました。

**ループ監視方式の仕組み:**

```csharp
// Router からの質問を検出（「？」で終わる場合）
if (currentAgent == "router_agent" && update.Text.Contains("？"))
{
    waitingForUserInput = true;
}

// ユーザー入力待ち状態に移行
if (waitingForUserInput)
{
    Console.Write("回答> ");
    var userResponse = Console.ReadLine();
    messages.Add(new ChatMessage(ChatRole.User, userResponse));

    // ワークフローを再実行
    thread = workflowAgent.GetNewThread();
    await foreach (var newUpdate in workflowAgent.RunStreamingAsync(messages, thread, ...))
    {
        // 処理継続
    }
}
```

### ワークフロー構成

```csharp
var workflow = AgentWorkflowBuilder
    .CreateHandoffBuilderWith(routerAgent)
    .WithHandoffs(routerAgent, [specialists..., moderatorAgent])
    .WithHandoffs([specialists...], routerAgent)
    .Build();
```

### タイムアウト設定

- 全体ワークフロー: 300 秒
- 最大メッセージ数: 30

### OpenTelemetry の設定

```csharp
builder.AddOpenTelemetry(options =>
{
    options.SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("DynamicGroupChatWorkflow"));

    options.AddOtlpExporter(exporterOptions =>
    {
        exporterOptions.Endpoint = new Uri(otlpEndpoint);
    });

    options.AddConsoleExporter();
});
```

## 今後の拡張

- [ ] ユーザー入力の検証機能（タイムアウト、フォーマット検証）
- [ ] 承認フロー統合 (Approve/Reject の選択肢)
- [ ] 会話履歴の永続化と再開機能
- [ ] Mermaid 可視化の自動生成
- [ ] マルチターン対話のサポート（複数回のユーザー入力）
- [ ] 分散トレーシングの強化（Azure Monitor など追加エクスポーターの組み込み）
- [ ] Human Agent パターンへの移行（Agent Framework の将来バージョンで対応予定）

## トラブルシューティング

### OTLP エンドポイントに接続できない

`OTEL_EXPORTER_OTLP_ENDPOINT` の設定を確認してください。ローカルで Jaeger や OpenTelemetry Collector が起動しているか確認してください。

```bash
# Jaeger が起動しているか確認
docker ps | grep jaeger

# ポート 4317 がリッスンしているか確認 (Windows)
netstat -an | findstr :4317
```

### Router がユーザーに質問しない

Router のプロンプトを確認し、「ユーザーの追加情報が必要」と判断されるような質問をしてください。

**例:**

- "予算や期限が不明な契約交渉について教えてください"
- "新しいプロジェクトを開始したいのですが、詳細は未定です"

Router は質問を「？」で終わらせるように指示されているため、質問文が検出されます。

### ユーザー入力が検出されない

Router が「？」で終わる質問を出力しているか確認してください。必要に応じて Router のプロンプトを調整し、質問形式を明示してください。

**Router のプロンプト（重要な部分）:**

```text
- ユーザーの追加情報が必要な場合:
  質問内容を明確に記述してください
  質問は「？」で終わるようにしてください。
```

### タイムアウトエラー

会話が長引く場合、`TimeSpan.FromSeconds(300)` の値を増やしてください:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(600)); // 10分に延長
```

### ワークフローが再実行されない

ユーザー入力後にワークフローを再実行していますが、会話履歴が正しく引き継がれているか確認してください。ログ出力を確認し、`[ユーザー入力受信]` のログが記録されているか確認します。

```bash
# ログをフィルタリング
dotnet run 2>&1 | grep "ユーザー入力"
```

## 設計上の考慮事項

### なぜ Human Agent パターンではないのか？

当初、`Agent` クラスを継承した `HumanAgent` を実装することで、ユーザーをエージェントとして扱う「Human Agent パターン」を目指しました。しかし、現在の Agent Framework バージョン (1.0.0-preview.251009.1) では以下の理由により実装できませんでした:

1. **`Agent` 基底クラスが提供されていない**: `ChatClientAgent` のような具体的なエージェントクラスのみが提供されています
2. **ストリーミング API の制約**: `RunStreamingAsync` の戻り値型 (`IAsyncEnumerable<StreamingChatUpdate>`) をカスタムエージェントで実装するには、内部 API への依存が必要

将来的に Agent Framework が `Agent` 基底クラスや Human Agent のサポートを提供した場合、この実装を Human Agent パターンに移行する予定です。

### ループ監視方式の利点

- **フレームワーク互換性**: 現在のバージョンで動作
- **シンプルな実装**: 追加のクラスが不要
- **柔軟な検出ロジック**: 「？」以外のパターンも容易に追加可能
- **デバッグが容易**: ログ出力で動作を追跡しやすい

### ループ監視方式の制約

- **ワークフローの可視化**: Human Agent がノードとして表示されない
- **再実行のオーバーヘッド**: ユーザー入力後、ワークフローを最初から再実行
- **会話履歴の管理**: 手動で会話履歴を管理する必要がある
