# エージェント型調達支援システム 要件定義および基本設計

## 1. 背景とゴール

- 調達関連の社内問い合わせに対し、Azure OpenAI の Chat Completions を活用した専門家エージェント群で迅速かつ根拠付きの回答を返す。
- Microsoft Agent Framework を用いて Router → 専門家並列 → Moderator → (HITL) のワークフローを構築し、人手承認を交えた高品質な応答プロセスを提供する。

## 2. システム要件

### 2.1 機能要件

- Router エージェントがユーザー質問を解析し、最大 2〜3 件の関連専門家エージェントを JSON 契約に従って選抜する。
- 選抜された専門家エージェント（Contract / Spend / Negotiation / Sourcing / Knowledge / Supplier）が並列で発言を生成する。
- Moderator エージェントが各専門家の所見を統合し、結論・根拠・各専門家所見・次アクション・品質スコアを含む最終回答を生成する。
- Router あるいは Moderator の判断で Hitl=true の場合、人手承認ノードに回答案を提示し、承認後にユーザーへ提示する。
- Router 出力が期待スキーマを満たさない場合は Knowledge エージェント単独で回答するフォールバックを行う。
- 最終回答の品質スコアが閾値 (既定 80 点) を下回る場合、優先度の高い専門家に差し戻して再統合を 1 回実施する。

### 2.2 非機能要件

- 応答時間: 並列実行を前提に平均 8 秒以内、長文でも 15 秒以内を目標。
- 可用性: Azure OpenAI の冗長化設定を利用し、障害時はリトライとフォールバックで継続運用。
- 観測性: 各 Executor の入力・出力・所要時間をイベントとして記録し、トレースを取得可能とする。
- コンプライアンス: API キーや接続情報は安全なシークレットストア (Key Vault 等) で管理し、ログには機微情報を含めない。
- 保守性: 専門家プロンプトや閾値を設定ファイル／環境変数で切り替え可能とし、新規エージェントの追加を最小改修で行えるアーキテクチャとする。

## 3. システム構成概要

- 言語・ランタイム: .NET 8 以降、C#。
- フレームワーク: Microsoft Agent Framework (Executors / Edges / Workflow Graph)。
- モデル提供: Azure OpenAI Chat Completions API。
- 主なコンポーネント:
  - Router (軽量モデル gpt-4o-mini)。
  - 6 専門家エージェントおよび Moderator (高品質モデル gpt-4o)。
  - Human-In-The-Loop 承認モジュール。
  - Orchestrator (Workflow Runtime) と状態ストア。
- ハイレベル構成図:

```
User --> Router Executor --selects--> [Contract | Spend | Negotiation | Sourcing | Knowledge | Supplier]
                                          \--parallel opinions--> Moderator Executor --> (HITL?) --> Final Reply
```

## 4. 基本設計

### 4.1 コンポーネント設計

- Router Executor:
  - `OpenAI ChatCompletion Agent` を内部に持つ LLM Executor。
  - Agent Framework の Structured Output を利用し、JsonSchema に基づく厳格なレスポンス (`Selected`, `Priority`, `DebateMode`, `EvidenceNeed`, `Hitl`, `Reason`) を生成。
  - Structured Output 生成時はスキーマ検証の失敗を捕捉し、フォールバックや再試行を制御する。
  - 出力はステートに保存し後続ノードが参照する。
- 専門家 Executors (6 種):
  - 共通ラッパー `ChatCompletionAgent` を使用し、専門領域別 System Prompt を設定。
  - 並列に起動され、ユーザー質問と Router 決定を入力として短い所見を返す。
- Moderator Executor:

  - 専門家の所見とメタデータ (priority, evidenceNeed, debateMode) を入力に統合回答を生成。
  - 応答フォーマット (結論/根拠/各専門家/次アクション/QSCORE) を担保する。

- HITL Executor:
  - Agent Framework の HumanApproval ノードとして実装。
  - Router 決定または Moderator 判断で Hitl=true の場合にのみ起動し、承認結果に応じてフローを継続。
- Workflow Orchestrator:
  - Router → dynamic fork (専門家) → join → Moderator → quality gate → (HITL) → Finish のグラフを組み立てる。
  - Quality gate で QSCORE が閾値未満の場合は Moderator へ最大 1 回再試行させるロジックをエッジで表現する。

### 4.2 データフロー

1. ユーザー質問を Router Executor に送付し、意思決定 JSON を取得。
2. `Selected` に含まれる専門家ノードのみ並列起動し、各所見を収集。
3. Moderator Executor が全所見を集約し、最終回答案と `QSCORE` を生成。
4. Quality gate が `QSCORE < 80` の場合、優先度上位の専門家に差し戻し、再度 Moderator で統合。
5. `Hitl=true` の場合、HITL Executor が承認を要求し、承認後に最終回答としてユーザーへ返却。

### 4.3 エラー処理とフォールバック

- Router JSON 解析失敗時: Knowledge エージェント単独で応答し `Reason` にフォールバック理由を記録。
- 専門家呼び出し失敗時: 該当エージェントを除外し、Moderator には失敗情報を注記。必要に応じて Router 再実行を検討。
- Moderator 失敗時: 全体フローを 1 回リトライし、それでも失敗すればエラーメッセージとログを残して運用者に通知。

### 4.4 設定およびデプロイ要件

- デプロイメント名や温度、最大トークンなどの実行パラメータを `appsettings.json` または環境変数で管理。
- 認証は `DefaultAzureCredential` を用い、CLI でのログイン状態を前提としたマネージドな資格情報解決を採用。
- Azure OpenAI エンドポイントおよび API キーは Key Vault 連携を推奨。

- ローカル開発時は `dotnet user-secrets` 等でシークレットを管理。

### 4.5 観測性と拡張性

- 各 Executor の開始・終了イベント、所要時間、トークン使用量をテレメトリに送信し、遅延やコストの傾向を可視化。
- Router の選抜結果と Moderator の品質スコアをダッシュボード化し、プロンプト調整や専門家構成の改善に活用。
- 新たな専門領域を追加する際は、System Prompt と Executor 登録を設定駆動で行えるよう拡張ポイントを提供。

## 5. 実装状況

### 5.1 SelectiveGroupChatWorkflow (実装完了)

動的エージェント選抜とモデレーターによる統合を実装した新しいワークフローです。

**アーキテクチャ:**
```
Phase 1: Router が必要な専門家を選抜
Phase 2: 選抜された専門家が並列で意見を提供
Phase 3: Moderator が専門家の意見を統合して最終回答を生成
```

**主な特徴:**
- Router エージェントが JSON 形式で必要な専門家を動的に選抜
- 選抜された専門家のみが並列実行されることでコストと応答時間を最適化
- Moderator エージェントが各専門家の所見を統合し、構造化された最終回答を生成
- 各フェーズが独立したワークフローとして実行され、明確な責任分離を実現

**フォールバック機能:**
- Router の JSON 出力がパース失敗時は Knowledge 専門家をデフォルトで使用
- 個別の専門家エージェントがエラーの場合でも他の専門家の意見で継続

**プロジェクト構成:**
- `src/SelectiveGroupChatWorkflow/Program.cs`: メインワークフロー実装
- `src/SelectiveGroupChatWorkflow/SelectiveGroupChatWorkflow.csproj`: プロジェクト設定
- Router, Specialist, Moderator の各エージェントを ChatClientAgent として実装
- Microsoft.Agents.AI.Workflows を使用した段階的ワークフロー実行

### 5.2 HandoffWorkflow (既存)

ルーターと専門家グループ間の双方向ハンドオフを実装。
すべての専門家が利用可能で、ルーターが必要に応じてハンドオフを行う。

### 5.3 GroupChatWorkflow (既存)

RoundRobin 方式で全専門家が順番に発言する従来型のグループチャット実装。

## 6. 今後の課題

- Router JSON のスキーマ厳格化とバリデーション、観測データを用いた自動チューニングの検討。
- HITL 前後の承認履歴管理、監査ログ整備。
- Agent Framework Workflow のチェックポイント機能を活かした途中復旧とイベント可視化の実装。
- SelectiveGroupChatWorkflow への HITL 統合と品質スコアベースの再試行機能の追加。
