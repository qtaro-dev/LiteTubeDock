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

お気に入りボタン画像アイコン設定追加

# 目的

お気に入りボタンに任意の画像アイコンを設定できるようにし、見た目と識別性を向上させる。

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

## 1. BookmarkItemに画像パスを追加

`BookmarkItem` に以下のプロパティを追加する。

- `IconPath`

例:

```json
{
  "label": "BGM",
  "url": "https://www.youtube.com/",
  "sortOrder": 2,
  "isEnabled": true,
  "iconPath": "data/icons/bgm.png"
}
```

## 2. 画像配置フォルダ

推奨フォルダを追加する。

- `data/icons`

存在しない場合は自動作成してよい。

## 3. ボタン表示

アイコンが設定され、ファイルが存在する場合は、ボタン内に画像とラベルを表示する。

初期表示方針:

- アイコンを上
- ラベルを下

画像が存在しない場合は、従来どおりテキストのみ表示する。

## 4. 設定画面

お気に入りボタン設定に `IconPath` 入力欄を追加する。

推奨名:

- `FavoriteIconPathTextBox01` 〜 `FavoriteIconPathTextBox10`

画像ファイル選択ダイアログは今回必須ではない。まずはパス入力方式でよい。

## 5. 対応形式

まずは以下を対象とする。

- `.png`
- `.jpg`
- `.jpeg`
- `.webp`

不正ファイルや存在しないファイルを指定してもクラッシュしないこと。

# 受け入れ条件（目視確認基準）

- `dotnet build` でビルドエラーがない
- `bookmarks.json` に `iconPath` を設定できる
- 画像ファイルがある場合、ボタンにアイコンが表示される
- 画像ファイルがない場合、テキストボタンとして表示される
- 設定画面で画像パスを編集できる
- 保存後、MainWindowのお気に入りボタン表示に反映される
- 既存のボタン遷移機能が壊れていない
