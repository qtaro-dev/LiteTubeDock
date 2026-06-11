# 共通前提（必ず遵守）

- AGENT.md が存在する場合、その内容を最優先で遵守させること
- ユーザーはコードを直接編集しない前提である
- 実装・修正はすべて CODEX が行う
- 既存設計を破壊しない
- 既存テーマ・ライブラリ構成を維持する
- ハードコード文字列は禁止
- ICommand構造を崩さない
- 作業対象は `E:\Dev\LiteTubeDock` 配下のみとすること
- `E:\Dev\LiteTubeDock` の外側にあるファイルやフォルダは一切編集しないこと
- `MainWindow.xaml` と `SettingsWindow.xaml` の既存レイアウト、既存 `x:Name` は原則維持すること
- UIを大きく作り直さず、既存UIに処理を接続する形を優先すること

# タスク名

YouTubeログイン状態保持の安定化

# 目的

WebView2のユーザーデータフォルダを明示管理し、YouTubeログイン状態、Cookie、キャッシュなどがアプリ再起動後も安定して保持されるようにする。

アプリ側でGoogle ID、パスワード、認証情報を保存してはいけない。

# 対象ファイル（推定可）

- `MainWindow.xaml.cs`
- `Models/AppSettings.cs`
- `Services/SettingsService.cs`
- `Services/AppPathService.cs`
- `Constants/AppConstants.cs`
- `data/settings.json`
- `README.md`

# 実装内容（具体的変更指示）

## 1. WebView2 UserDataFolder を明示する

WebView2初期化時に、専用のユーザーデータフォルダを使う。

推奨パス:

```text
data/webview2-user-data
```

または設定ファイルで指定できるようにする。

例:

```json
{
  "webView2UserDataFolder": "data/webview2-user-data"
}
```

## 2. 既定フォルダの自動作成

`data/webview2-user-data` が存在しない場合は自動作成する。

## 3. 初期化順序

`PlayerWebView` の通常表示前に、WebView2環境を明示的に作成してから初期URLへ遷移すること。

## 4. ログイン情報の扱い

以下を必ず守る。

- Google IDをアプリ側JSONに保存しない
- パスワードをアプリ側JSONに保存しない
- 認証トークンをアプリ側JSONに保存しない
- Cookieを独自保存しない
- WebView2標準のユーザーデータフォルダに任せる

## 5. README更新

READMEに以下を記載する。

- YouTubeログイン状態はWebView2のユーザーデータフォルダで保持されること
- アプリ側でGoogle IDやパスワードは保存しないこと
- ログイン状態をリセットしたい場合は、アプリ終了後に `data/webview2-user-data` を削除する運用であること

## 6. 注意

このタスクではログイン自動化は実装しない。

ユーザーは通常のYouTube画面から手動でログインする。

# 受け入れ条件（目視確認基準）

- `dotnet build` でビルドエラーがない
- 起動時に `data/webview2-user-data` が作成される
- YouTubeへ通常どおりログインできる
- アプリを閉じて再起動してもYouTubeログイン状態が残る
- `settings.json` にGoogle IDやパスワードが保存されていない
- `bookmarks.json` にGoogle IDやパスワードが保存されていない
- READMEにログイン状態保持とリセット方法が記載されている
