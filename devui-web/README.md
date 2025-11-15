# AskAI DevUI Web Interface

このディレクトリには、DevUIHost の AGUI エンドポイントと対話するためのシンプルな Web UI が含まれています。

## 概要

この Web インターフェースを使用すると、ブラウザから直接専門家エージェントと会話できます。API を直接呼び出す必要はありません。

## 特徴

- ✨ モダンな UI デザイン
- 🤖 10 種類の専門家エージェントに対応
- 💬 リアルタイムチャット形式の対話
- 📝 会話履歴の保持
- 🎨 レスポンシブデザイン

## 使用方法

### 1. DevUIHost サーバーを起動

まず、バックエンドの AGUI サーバーを起動します：

```bash
cd ../src/DevUIHost
dotnet run
```

サーバーは `http://localhost:5000` で起動します。

### 2. Web UI を開く

ブラウザで `index.html` を直接開くか、シンプルな HTTP サーバーを使用します：

**オプション A: ブラウザで直接開く**
```bash
# Linux/Mac
open index.html

# Windows
start index.html
```

**オプション B: Python で簡易サーバー起動**
```bash
# Python 3 の場合
python3 -m http.server 8080

# ブラウザで http://localhost:8080 を開く
```

**オプション C: Node.js の http-server を使用**
```bash
# http-server をグローバルインストール（初回のみ）
npm install -g http-server

# サーバー起動
http-server -p 8080

# ブラウザで http://localhost:8080 を開く
```

### 3. エージェントと対話

1. 左側のサイドバーからエージェントを選択
2. テキストエリアにメッセージを入力
3. Enter キーまたは「送信」ボタンをクリック
4. エージェントからの応答を確認

## 利用可能なエージェント

- **Contract Agent**: 契約関連の専門家
- **Spend Agent**: 支出分析の専門家
- **Negotiation Agent**: 交渉戦略の専門家
- **Sourcing Agent**: 調達戦略の専門家
- **Knowledge Agent**: 知識管理の専門家
- **Supplier Agent**: サプライヤー管理の専門家
- **Legal Agent**: 法務の専門家
- **Finance Agent**: 財務の専門家
- **Procurement Agent**: 調達実務の専門家
- **Procurement Assistant**: 調達・購買業務の総合アシスタント

## 技術仕様

- **フロントエンド**: Pure HTML/CSS/JavaScript（フレームワーク不要）
- **API 通信**: Fetch API を使用した RESTful 通信
- **プロトコル**: AGUI (Agent Graph UI) over HTTP
- **依存関係**: なし（ブラウザのみで動作）

## トラブルシューティング

### エージェント一覧が表示されない

- DevUIHost が起動しているか確認
- `http://localhost:5000` にアクセスできるか確認
- ブラウザの開発者ツールでコンソールエラーを確認

### CORS エラーが発生する

DevUIHost では CORS を有効にしていますが、問題が発生する場合：

1. DevUIHost の `Program.cs` で CORS 設定を確認
2. `file://` プロトコルではなく HTTP サーバー経由でアクセス

### メッセージが送信できない

- エージェントが選択されているか確認
- DevUIHost が起動しているか確認
- ネットワーク接続を確認

## カスタマイズ

### API エンドポイントの変更

`index.html` の以下の行を編集：

```javascript
const API_BASE = 'http://localhost:5000';
```

### スタイルのカスタマイズ

`<style>` タグ内の CSS を編集してデザインをカスタマイズできます。

## セキュリティに関する注意

この Web UI は**開発・デモ用**です。本番環境で使用する場合は：

- 適切な認証・認可の実装
- HTTPS の使用
- CORS 設定の厳格化
- 入力バリデーションの強化
- レート制限の実装

を検討してください。

## 関連ドキュメント

- [DevUIHost README](../src/DevUIHost/README.md)
- [DEVUI セットアップガイド](../DEVUI_SETUP.md)
- [メインプロジェクト README](../README.md)
