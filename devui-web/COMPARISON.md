# DevUI 利用方法の比較

DevUIHost は 3 つの異なる方法でエージェントにアクセスできます。

## 1. Microsoft 公式 DevUI（推奨）🆕

### アクセス方法
```
http://localhost:5000/devui
```

### 特徴
- ✅ **Microsoft Agent Framework 標準の UI**
- ✅ すべての登録済みエージェントを表示
- ✅ ワークフローのサポート
- ✅ デバッグツール統合
- ✅ OpenAI API 互換 (`/v1/responses`)
- ✅ Python DevUI との連携が可能
- ✅ エージェントの詳細情報表示
- ✅ 会話履歴の管理

### 適している用途
- 開発中のデバッグ
- ワークフローのテスト
- 複数エージェントの比較
- Python DevUI との連携

### 実装詳細
- パッケージ: `Microsoft.Agents.AI.DevUI`
- 組み込みリソースから提供
- `MapDevUI()` で有効化

---

## 2. カスタム Web UI

### アクセス方法
```
http://localhost:5000/ui/
```

### 特徴
- ✅ **シンプルなチャットインターフェース**
- ✅ エージェント選択機能
- ✅ リアルタイムチャット
- ✅ 会話履歴の保持
- ✅ モダンなデザイン（紫のグラデーション）
- ✅ ローディング/エラー表示
- ✅ レスポンシブデザイン
- ❌ ワークフローサポートなし
- ❌ デバッグツールなし

### 適している用途
- シンプルなエージェントテスト
- デモ/プレゼンテーション
- 1対1の会話
- エンドユーザー向けインターフェース

### 実装詳細
- Pure HTML/CSS/JavaScript
- 静的ファイル提供
- フレームワーク不要

---

## 3. AGUI API エンドポイント

### アクセス方法
```
http://localhost:5000/agents/{agent_name}
```

### 利用可能なエンドポイント
- `/agents/contract` - Contract Agent
- `/agents/spend` - Spend Agent
- `/agents/negotiation` - Negotiation Agent
- `/agents/sourcing` - Sourcing Agent
- `/agents/knowledge` - Knowledge Agent
- `/agents/supplier` - Supplier Agent
- `/agents/legal` - Legal Agent
- `/agents/finance` - Finance Agent
- `/agents/procurement` - Procurement Agent
- `/agents/assistant` - Procurement Assistant

### 特徴
- ✅ **RESTful API**
- ✅ プログラマティックアクセス
- ✅ OpenAI 互換フォーマット
- ✅ Server-Sent Events (SSE) サポート
- ✅ CORS 有効
- ✅ 外部ツールとの統合が容易
- ❌ UI なし

### 適している用途
- 外部アプリケーションとの統合
- カスタム UI の構築
- バッチ処理
- 自動化スクリプト

### 使用例
```bash
curl -X POST http://localhost:5000/agents/contract \
  -H "Content-Type: application/json" \
  -d '{
    "messages": [
      {"role": "user", "content": "新規契約の注意点は？"}
    ]
  }'
```

---

## 比較表

| 機能 | Microsoft DevUI | Custom UI | AGUI API |
|------|----------------|-----------|----------|
| **アクセス** | `/devui` | `/ui/` | `/agents/*` |
| **UI の種類** | 標準 DevUI | カスタムチャット | API |
| **エージェント選択** | ✅ | ✅ | プログラム側で指定 |
| **ワークフロー** | ✅ | ❌ | ✅ |
| **デバッグツール** | ✅ | ❌ | ❌ |
| **OpenAI 互換** | ✅ | ❌ | ✅ |
| **Python DevUI 連携** | ✅ | ❌ | ❌ |
| **カスタマイズ性** | ❌ | ✅ | ✅ |
| **依存関係** | なし | なし | なし |
| **セットアップ** | なし | なし | なし |
| **学習コスト** | 低 | 低 | 中 |
| **適用範囲** | 開発 | デモ/テスト | 統合 |

---

## 推奨される使い分け

### 開発・デバッグ時
→ **Microsoft DevUI** (`/devui`)
- 最も機能が充実
- デバッグツール完備
- ワークフローテストに最適

### デモ・プレゼンテーション時
→ **カスタム Web UI** (`/ui/`)
- シンプルで直感的
- 見た目が綺麗
- エンドユーザー向け

### システム統合時
→ **AGUI API** (`/agents/*`)
- プログラマティックアクセス
- 外部ツールとの連携
- バッチ処理

---

## すべて同時に利用可能

3 つの方法はすべて独立しており、同時に利用できます：

```bash
# サーバー起動
cd src/DevUIHost
dotnet run

# ブラウザで同時に複数開ける
# タブ1: http://localhost:5000/devui
# タブ2: http://localhost:5000/ui/

# コマンドラインからも同時にアクセス可能
curl http://localhost:5000/agents/contract -X POST ...
```

---

## まとめ

- **Microsoft DevUI**: プロフェッショナルな開発用 UI（推奨）🆕
- **カスタム Web UI**: シンプルなチャット UI
- **AGUI API**: プログラマティックアクセス

用途に応じて使い分けることで、最適な開発体験が得られます。
