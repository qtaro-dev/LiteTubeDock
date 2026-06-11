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

お気に入りボタン色設定追加

# 目的

お気に入りボタンごとに背景色と文字色を設定できるようにし、視認性と使いやすさを向上させる。

# 対象ファイル（推定可）

- `MainWindow.xaml`
- `MainWindow.xaml.cs`
- `SettingsWindow.xaml`
- `SettingsWindow.xaml.cs`
- `Models/BookmarkItem.cs`
- `Services/BookmarkService.cs`
- `data/bookmarks.json`
- `README.md`

# 実装内容（具体的変更指示）

## 1. BookmarkItemに色設定を追加

`BookmarkItem` に以下のプロパティを追加する。

- `BackgroundColor`
- `ForegroundColor`

値は `#RRGGBB` 形式を基本とする。

例:

```json
{
  "label": "Home",
  "url": "https://www.youtube.com/",
  "sortOrder": 1,
  "isEnabled": true,
  "backgroundColor": "#F0F0F0",
  "foregroundColor": "#000000"
}
```

## 2. MainWindowのお気に入りボタンへ反映

- `BackgroundColor` が有効な色ならボタン背景色へ反映
- `ForegroundColor` が有効な色ならボタン文字色へ反映
- 不正な色コードの場合は既定色を使う
- 無効ボタンは既存仕様どおり無効状態が分かる表示にする

## 3. SettingsWindowで編集できるようにする

既存の左カテゴリ型UIを維持しつつ、お気に入りボタン設定に以下を追加する。

- 背景色入力欄
- 文字色入力欄

項目数が横に増えすぎる場合は、`SettingsWindow.xaml` の既存構造を大きく壊さない範囲で行の高さや列幅を調整してよい。

## 4. 初期値

既定値は以下とする。

- 背景色: `#F0F0F0`
- 文字色: `#000000`

## 5. README更新

色指定が `#RRGGBB` 形式であることをREADMEに追記する。

# 受け入れ条件（目視確認基準）

- `dotnet build` でビルドエラーがない
- `bookmarks.json` に背景色と文字色を設定できる
- アプリ起動時にボタン色が反映される
- 設定画面で背景色と文字色を編集できる
- 保存後、MainWindowのボタン色が更新される
- 不正な色コードでもクラッシュしない
- 既存のボタン遷移機能が壊れていない
