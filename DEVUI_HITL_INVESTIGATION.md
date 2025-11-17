# DevUI HITL 統合可能性の調査結果

## 調査概要

Microsoft Agent Framework の DevUI パッケージ (`Microsoft.Agents.AI.DevUI v1.0.0-preview.251113.1`) に、HITL (Human-in-the-Loop) 承認機能を統合できるかを調査しました。

## DevUI の構造

### 1. パッケージ構成

```
Microsoft.Agents.AI.DevUI/
├── lib/net9.0/
│   └── Microsoft.Agents.AI.DevUI.dll    # .NET ライブラリ
└── staticwebassets/
    ├── index.html                        # React SPA のエントリーポイント
    ├── assets/index.js                   # React アプリケーション (バンドル済み)
    └── assets/index.css                  # スタイルシート
```

### 2. 提供される API

DevUI パッケージが公開している主な API:

#### MapDevUI() メソッド
```csharp
public static IEndpointConventionBuilder MapDevUI(
    this IEndpointRouteBuilder endpoints, 
    string pattern = "/devui")
```

**機能**: 
- 事前ビルドされた React SPA を指定されたパスで配信
- 静的ファイル (HTML/JS/CSS) を提供するだけ
- カスタマイズや拡張のための API は公開されていない

#### 期待されるバックエンドエンドポイント

DevUI フロントエンドが期待するエンドポイント:

1. **GET /meta** - サーバーメタデータ
   - UI モード (developer/user)
   - バージョン情報
   - 機能フラグ (tracing, openai_proxy など)

2. **GET /v1/entities** - エンティティ一覧
   - 登録済みエージェントとワークフローのリスト

3. **GET /v1/entities/{id}/info** - エンティティ詳細情報

4. **OpenAI 互換エンドポイント** (`MapOpenAIResponses()`, `MapOpenAIConversations()`)
   - `/v1/responses` - チャット応答
   - `/v1/conversations` - 会話管理

## HITL 統合の可能性

### ❌ 直接統合は不可能

以下の理由により、公式 DevUI への直接的な HITL UI 統合は**できません**:

1. **フロントエンドがプリビルド済み**
   - React SPA はコンパイル済みの JavaScript バンドルとして配布
   - ソースコードは含まれておらず、カスタマイズ不可
   - UI コンポーネントを追加する API が存在しない

2. **拡張ポイントが未提供**
   - `MapDevUI()` は静的ファイルを配信するだけ
   - カスタム UI コンポーネントを注入する仕組みがない
   - プラグインや拡張機能の仕組みがない

3. **フロントエンドのソースコード非公開**
   - GitHub リポジトリには DevUI のソースコードが含まれていない可能性
   - 独自にビルドして配布することも困難

### ⚠️ 制限付きの統合オプション

以下の方法であれば、限定的な統合が可能です:

#### オプション 1: 既存エンドポイントの拡張

**方法**: DevUI が期待する標準エンドポイントに HITL 情報を追加

```csharp
// /meta エンドポイントで HITL 機能を通知
app.MapGet("/meta", () => new MetaResponse
{
    Capabilities = new Dictionary<string, bool>
    {
        ["tracing"] = true,
        ["openai_proxy"] = true,
        ["hitl_approval"] = true  // カスタム機能フラグ
    }
});
```

**制限**:
- DevUI のフロントエンドがこのフラグを認識しない
- UI には反映されない（バックエンドのみの通知）

#### オプション 2: 外部ウィンドウまたはタブでの実装（現在の実装）

**方法**: 別の HTML ページで HITL UI を提供

```
現在の実装:
- DevUI: http://localhost:5000/devui           (ワークフロー実行)
- HITL UI: http://localhost:5000/ui/hitl-approval.html  (承認処理)
```

**利点**:
- ✅ 完全に機能する HITL UI を提供可能
- ✅ 既存の DevUI に影響を与えない
- ✅ API ベースで統合されているため、拡張性が高い

**欠点**:
- ❌ ユーザーが2つのページを開く必要がある
- ❌ シームレスな UX ではない

#### オプション 3: DevUI の同一ページ内での通知（部分的）

**方法**: ワークフローの出力メッセージに HITL 情報を含める

```csharp
// Executor から DevUI に表示されるメッセージとして出力
await context.YieldOutputAsync(new
{
    type = "hitl_required",
    message = "👤 承認が必要です: http://localhost:5000/ui/hitl-approval.html",
    requestId = requestId,
    approvalUrl = $"http://localhost:5000/ui/hitl-approval.html"
}, cancellationToken);
```

**利点**:
- ✅ DevUI 内でユーザーに通知できる
- ✅ リンクをクリックして HITL ページに移動可能

**欠点**:
- ❌ DevUI 内で直接承認はできない
- ❌ 別タブを開く必要がある

## Agent Framework の HITL サポート

ドキュメントによると:
> "Graph-based Workflows: Connect agents and functions with streaming, checkpointing, and **human-in-the-loop capabilities**"

これは**ワークフローレベルの機能**を指しており:
- ワークフロー実行の一時停止
- 外部からの入力待機
- 条件分岐や承認フロー

**DevUI の UI 機能**ではありません。

## 推奨事項

### ✅ 現在の実装を維持（推奨）

**理由**:
1. **完全に機能する** HITL システムが実装済み
2. API ベースで疎結合、将来の拡張が容易
3. DevUI とは独立して動作するため、安定性が高い

**改善案**:
1. **通知の改善**: ワークフロー実行時に HITL URL を明示的に表示
2. **ドキュメント強化**: ユーザーガイドで2つのページの使用方法を説明
3. **統合リンク**: DevUI から HITL ページへのリンクを README に記載

### 🔍 将来的な可能性

Microsoft が将来的に以下を提供する可能性があります:
1. **DevUI のカスタマイズ API** - プラグインシステムや UI 拡張ポイント
2. **DevUI のソースコード公開** - フォーク＆カスタマイズが可能に
3. **HITL UI コンポーネントの組み込み** - 標準機能として提供

これらが実現するまでは、現在の実装が最適なアプローチです。

## 結論

### 調査結果まとめ

| 項目 | 結果 |
|------|------|
| DevUI への直接統合 | ❌ 不可能 |
| プリビルド UI の変更 | ❌ 不可能 |
| カスタム UI コンポーネント追加 | ❌ API 未提供 |
| 別ページでの HITL UI | ✅ 可能（現在の実装） |
| API 経由での統合 | ✅ 実装済み |
| ワークフロー内での HITL サポート | ✅ Agent Framework がサポート |

### 最終推奨

**現在の実装（別ページでの HITL UI）を継続することを推奨します。**

この実装は:
- ✅ 完全に機能する
- ✅ 技術的に健全
- ✅ 将来の拡張に対応可能
- ✅ DevUI の更新に影響されない

Microsoft が DevUI のカスタマイズ機能を提供するまでは、これが最善のアプローチです。

## 参考情報

- **DevUI パッケージ**: `Microsoft.Agents.AI.DevUI v1.0.0-preview.251113.1`
- **GitHub リポジトリ**: https://github.com/microsoft/agent-framework
- **ドキュメント**: https://learn.microsoft.com/agent-framework/overview/agent-framework-overview

---

**調査日**: 2025-11-17  
**調査者**: GitHub Copilot  
**対象バージョン**: Microsoft.Agents.AI.DevUI 1.0.0-preview.251113.1
