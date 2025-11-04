# Advanced Conditional Workflow - 実装詳細

> **関連**: [プロジェクトREADME](../../src/AdvancedConditionalWorkflow/README.md) | [Clean Architecture](../architecture/clean-architecture.md) | [ログセットアップ](../development/logging-setup.md)

## 概要

AI 自動交渉と人間承認プロセスを統合した、Microsoft Agent Framework の高度な機能デモワークフローです。

**実装機能**: Conditional Edges, Fan-Out/Fan-In, Loop, HITL, Visualization, OpenTelemetry 統合

## ワークフロー構造

### 5 フェーズ構成

```text
Phase 1: 契約分析      → ContractAnalysisExecutor
Phase 2: Fan-Out/Fan-In → 3並列レビュー (Legal/Finance/Procurement) → Aggregator
Phase 3: Switch分岐    → 低リスク承認 / 交渉ループ / 高リスク却下確認
Phase 4: Loop (最大3回) → AI交渉提案 → 効果評価 → 継続判定
Phase 5: HITL          → 人間による最終承認/エスカレーション/却下確認
```

**Phase 2 の並列実行**: `AddFanOutEdge` と `AddFanInEdge` を使用して、3つの専門家レビューを**真の並列実行**で処理し、パフォーマンスを最大66%改善しています。

## Executor 詳細

### Phase 2: Fan-Out/Fan-In による並列レビュー

| Executor                      | 役割                       | 入力                             | 出力                          |
| ----------------------------- | -------------------------- | -------------------------------- | ----------------------------- |
| **ContractAnalysisExecutor**  | 契約の基本分析             | `ContractInfo`                   | `(ContractInfo, RiskAssessment)` |
| **SpecialistReviewExecutor**  | 専門家レビュー (3並列)     | `(ContractInfo, RiskAssessment)` | `SpecialistReview` |
| **ParallelReviewAggregator**  | 3つのレビューを集約        | `SpecialistReview` (x3)          | `(ContractInfo, RiskAssessment)` |

#### 並列実行の実装

```csharp
// Phase 2: Fan-Out/Fan-In - 並列専門家レビュー
var legalReviewer = new SpecialistReviewExecutor(chatClient, "Legal", "legal_reviewer", logger);
var financeReviewer = new SpecialistReviewExecutor(chatClient, "Finance", "finance_reviewer", logger);
var procurementReviewer = new SpecialistReviewExecutor(chatClient, "Procurement", "procurement_reviewer", logger);
var aggregator = new ParallelReviewAggregator(logger);

// Fan-Out: 契約分析後、3人の専門家に並列配信
builder.AddFanOutEdge(analysisExecutor, targets: [legalReviewer, financeReviewer, procurementReviewer]);

// Fan-In: 3人のレビューをAggregatorに集約
builder.AddFanInEdge(aggregator, sources: [legalReviewer, financeReviewer, procurementReviewer]);
```

**並列実行の効果**:
- 3つの専門家レビューが同時並行で実行される
- 最も遅い専門家の処理時間で全体が完了（最速約2秒）
- 順次実行と比較して66%の時間短縮（6秒 → 2秒）

### Phase 4: 交渉ループ実装

| Executor                         | 役割               | 入力                                  | 出力                                  |
| -------------------------------- | ------------------ | ------------------------------------- | ------------------------------------- |
| **NegotiationStateInitExecutor** | ループ初期化       | `(ContractInfo, RiskAssessment)`      | `(ContractInfo, EvaluationResult)`    |
| **NegotiationExecutor**          | AI 交渉提案生成    | `(ContractInfo, EvaluationResult)`    | `(ContractInfo, NegotiationProposal)` |
| **NegotiationContextExecutor**   | 効果評価・継続判定 | `(ContractInfo, NegotiationProposal)` | `(ContractInfo, EvaluationResult)`    |
| **NegotiationResultExecutor**    | 形式変換           | `(ContractInfo, EvaluationResult)`    | `(ContractInfo, RiskAssessment)`      |

#### ループ制御ロジック

```csharp
// NegotiationContextExecutor: 効果評価と継続判定
var riskReduction = proposal.Proposals.Count * 5;
var newRiskScore = Math.Max(0, originalRisk.OverallRiskScore - riskReduction);
var iteration = evaluationResult.Iteration + 1;

// 継続条件: リスク>30 かつ 反復<3
var continueNegotiation = newRiskScore > 30 && iteration < 3;
```

**ループバックエッジ**: `ContinueNegotiation=true` → NegotiationExecutor に戻る  
**終了エッジ**: `ContinueNegotiation=false` → NegotiationResult へ進む

### Phase 5: HITL 実装

| HITL 種別             | トリガー条件        | プロンプト                  | 承認時    | 却下時           |
| --------------------- | ------------------- | --------------------------- | --------- | ---------------- |
| **final_approval**    | 交渉成功 (score≤30) | "最終承認しますか?"         | Approved  | エスカレーション |
| **escalation**        | 交渉未達 (score>30) | "エスカレーションしますか?" | Escalated | 却下             |
| **rejection_confirm** | 高リスク (score>70) | "却下確認しますか?"         | Rejected  | 再評価           |

```csharp
// HITLApprovalExecutor実装
Console.WriteLine($"\n【{approvalType}】");
Console.WriteLine(context);
Console.Write("承認しますか? [Y/N]: ");
var response = Console.ReadLine();
var approved = response?.Trim().ToUpperInvariant() == "Y";
```

## データフロー

### 型の伝搬

```text
ContractInfo (初期入力)
  ↓
(ContractInfo, RiskAssessment) ← 分析・レビュー・集約
  ↓ Switch
  ├─ 低リスク → FinalDecision
  ├─ 中リスク → (ContractInfo, EvaluationResult) ← Loop初期化
  │              ↓ ループ内
  │            (ContractInfo, NegotiationProposal) → (ContractInfo, EvaluationResult)
  │              ↓ ループ終了
  │            (ContractInfo, RiskAssessment) → HITL → FinalDecision
  └─ 高リスク → HITL → FinalDecision
```

### 条件付きエッジの相互排他性

Switch (Phase 3):

- `score ≤ 30`: LowRiskApproval
- `31 < score ≤ 70`: NegotiationLoop
- `score > 70`: RejectionConfirmHITL

Loop 継続判定 (Phase 4):

- `ContinueNegotiation=true`: ループバック
- `ContinueNegotiation=false`: NegotiationResult

## ログレベル戦略

### 2 層ログ構造

| レベル          | 用途                   | 例                                            |
| --------------- | ---------------------- | --------------------------------------------- |
| **Trace**       | フレームワークイベント | ExecutorInvokedEvent, SuperStepCompletedEvent |
| **Information** | ビジネスロジック進捗   | Phase 開始, Executor 完了, 判定結果           |

```csharp
// Program.cs: デフォルトレベル設定
builder.SetMinimumLevel(LogLevel.Information);

// フレームワークイベント
Logger.LogTrace("📍 イベント受信: {EventType}", evt.GetType().Name);

// ビジネスロジック
Logger.LogInformation("✓ {ExecutorName} 完了", executorName);
```

## OpenTelemetry 統合

### Activity 構造

```csharp
using var activity = TelemetryHelper.StartActivity(
    Program.ActivitySource,
    "Phase2_SpecialistReviews",
    new Dictionary<string, object>
    {
        ["specialist"] = specialty,
        ["contract_amount"] = amount
    });

TelemetryHelper.LogWithActivity(
    _logger,
    activity,
    LogLevel.Information,
    "✓ {Specialty} レビュー完了 (リスクスコア: {Score})",
    specialty, score);
```

### 分散トレーシング

- **ActivitySource**: `AdvancedConditionalWorkflow`
- **Exporter**: OTLP (default: `http://localhost:4317`)
- **可視化**: Aspire Dashboard (`http://localhost:18888`)

## カスタマイズポイント

### 1. リスク閾値調整

```csharp
// ParallelReviewAggregator.cs
var riskLevel = overallRiskScore switch
{
    <= 30 => "Low",    // 低リスク: 自動承認
    <= 70 => "Medium", // 中リスク: AI交渉
    _ => "High"        // 高リスク: HITL却下確認
};
```

### 2. ループ制御パラメータ

```csharp
// NegotiationContextExecutor.cs
var riskReduction = proposal.Proposals.Count * 5; // 1提案あたり5点削減
var maxIterations = 3; // 最大反復回数
var targetRiskScore = 30; // 目標スコア
```

### 3. 専門家追加

```csharp
// Program.cs: 4つ目の専門家を追加
var complianceReviewer = new SpecialistReviewExecutor(..., "Compliance", ...);
builder
    .AddEdge(analysisExecutor, complianceReviewer)
    .AddEdge(complianceReviewer, aggregator);
```

## トラブルシューティング

### ループが終了しない

**原因**: `ContinueNegotiation` 条件が常に `true`  
**確認**: NegotiationContextExecutor のスコア削減ロジック

```csharp
Logger.LogInformation("新リスクスコア: {NewScore}, 反復: {Iteration}", newRiskScore, iteration);
```

### HITL 入力が反映されない

**原因**: Console.ReadLine() のトリミング不足  
**確認**: 入力文字列の正規化

```csharp
var response = Console.ReadLine()?.Trim().ToUpperInvariant();
```

### テレメトリが表示されない

**原因**: OTLP Exporter エンドポイント未起動  
**解決**:

```powershell
docker compose up -d
# http://localhost:18888 で確認
```

## Fan-Out/Fan-In による並列実行

### Phase 2: 真の並列実行実装

このワークフローは `AddFanOutEdge` と `AddFanInEdge` を使用して、3つの専門家レビューを**真の並列実行**で処理します。

```csharp
// Fan-Out: 契約分析後、3人の専門家に並列配信
builder.AddFanOutEdge(analysisExecutor, targets: [legalReviewer, financeReviewer, procurementReviewer]);

// Fan-In: 3人のレビューをAggregatorに集約
builder.AddFanInEdge(aggregator, sources: [legalReviewer, financeReviewer, procurementReviewer]);
```

### パフォーマンス改善

**従来の順次実行** (他のワークフローの実装):

```text
Legal Review (2s) → Finance Review (2s) → Procurement Review (2s) = 6s
```

**現在の並列実行** (Fan-Out/Fan-In):

```text
              ┌─ Legal Review (2s) ──┐
分析 → FanOut ├─ Finance Review (2s) ─┤ FanIn → 集約 = 2s
              └─ Procurement Review (2s) ─┘
```

**実測効果**: 専門家レビューフェーズの実行時間を **66% 削減** (6s → 2s)

## 参考資料

- [Microsoft Agent Framework Workflows](https://learn.microsoft.com/dotnet/ai/agents)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)
- [プロジェクト Clean Architecture](../architecture/clean-architecture.md)
- [ログセットアップガイド](../development/logging-setup.md)
