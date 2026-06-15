# 共通前提（必ず遵守）

- AGENT.md が存在する場合、その内容を最優先で遵守させること
- ユーザーはコードを直接編集しない前提である
- 実装・修正はすべて CODEX が行う
- 既存設計を破壊しない
- 既存テーマ・ライブラリ構成を維持する
- ハードコード文字列は禁止
- ICommand構造を崩さない

# タスク名

LiteTube Dock v0.1 基本機能実装

# 目的

LiteTube Dock を、WPF + .NET 8 + WebView2 ベースの軽量Webプレイヤーとして実用可能な状態にする。

現時点でユーザーが作成済みの `MainWindow.xaml` と `SettingsWindow.xaml` のUI構造を維持し、以下を実装する。

- YouTubeをWebView2で表示する
- YouTubeログイン状態はWebView2標準のCookie/session管理に任せる
- お気に入りボタン10個をJSONから読み込む
- ボタン押下で登録URLへ遷移する
- 設定ウィンドウで基本設定とお気に入りボタン設定を編集できる
- ウィンドウ位置、サイズ、最後に開いたURL、常に最前面状態を保存・復元する
- READMEを追加する

# 対象ファイル（推定可）

- `AGENT.md`
- `LiteTubeDock.csproj`
- `MainWindow.xaml`
- `MainWindow.xaml.cs`
- `SettingsWindow.xaml`
- `SettingsWindow.xaml.cs`
- `App.xaml`
- `App.xaml.cs`
- `Models/AppSettings.cs`
- `Models/BookmarkItem.cs`
- `Models/WindowSettings.cs`
- `Services/SettingsService.cs`
- `Services/BookmarkService.cs`
- `Constants/AppConstants.cs`
- `data/settings.json`
- `data/bookmarks.json`
- `README.md`

必要に応じて、`Models`、`Services`、`Constants`、`data` フォルダを新規作成すること。

# 実装内容（具体的変更指示）

## 1. 作業開始前の確認

- 必ず `AGENT.md` を読むこと。
- 作業対象は `E:\Dev\LiteTubeDock` 配下のみとすること。
- `E:\Dev\LiteTubeDock` の外側にあるファイルやフォルダは一切編集しないこと。
- バックアップフォルダを含め、`E:\Dev\LiteTubeDock` 以外のパスは参照のみでも原則触らないこと。
- `MainWindow.xaml` と `SettingsWindow.xaml` の既存レイアウト、既存 `x:Name` は原則変更しないこと。
- UIを大きく作り直さないこと。
- WinForms、WinUI、MAUI、Avalonia、Electron へ置き換えないこと。

## 2. JSON設定ファイルを実装する

以下の2ファイルを使用する。

- `data/settings.json`
- `data/bookmarks.json`

存在しない場合は、アプリ起動時に自動作成すること。

### settings.json の内容

以下の情報を保存・復元できるようにする。

- ホームURL
- 前回URLを復元するか
- 最後に開いていたURL
- 常に最前面で起動するか
- ウィンドウ位置とサイズを復元するか
- ウィンドウ位置
- ウィンドウサイズ

例:

```json
{
  "homeUrl": "https://www.youtube.com/",
  "lastUrl": "https://www.youtube.com/",
  "restoreLastUrl": true,
  "alwaysOnTop": false,
  "restoreWindowState": true,
  "window": {
    "left": 100,
    "top": 100,
    "width": 800,
    "height": 600
  }
}
```

### bookmarks.json の内容

最大10件のお気に入りボタンを管理する。

例:

```json
[
  {
    "label": "Home",
    "url": "https://www.youtube.com/",
    "sortOrder": 1,
    "isEnabled": true
  },
  {
    "label": "BGM1",
    "url": "https://www.youtube.com/",
    "sortOrder": 2,
    "isEnabled": true
  }
]
```

## 3. モデルを追加する

以下のモデルを追加すること。

- `AppSettings`
- `WindowSettings`
- `BookmarkItem`

必要なプロパティはJSON構造に合わせること。

## 4. サービスを追加する

以下のサービスを追加すること。

### SettingsService

- `settings.json` の読み込み
- 存在しない場合の初期ファイル作成
- 設定保存
- 不正JSON時の安全なフォールバック

### BookmarkService

- `bookmarks.json` の読み込み
- 存在しない場合の初期ファイル作成
- 最大10件の読み込み
- `sortOrder` 順での並び替え
- 設定ウィンドウから編集された内容の保存
- 不正JSON時の安全なフォールバック

## 5. MainWindow の基本操作を実装する

既存UI名を使って実装すること。

### 対象UI

- `PlayerWebView`
- `HomeMenuItem`
- `BackMenuItem`
- `ForwardMenuItem`
- `ReloadMenuItem`
- `ExitMenuItem`
- `AlwaysOnTopMenuItem`
- `OpenSettingsMenuItem`
- `ReloadSettingsMenuItem`
- `FavoriteButton01` 〜 `FavoriteButton10`
- `CurrentUrlText`
- `LoadingStatusText`
- `AlwaysOnTopStatusText`

### 実装する動作

- 起動時に設定ファイルを読み込む
- `restoreLastUrl` が true なら `lastUrl` を開く
- `restoreLastUrl` が false なら `homeUrl` を開く
- Homeメニュー押下で `homeUrl` に遷移する
- Backメニュー押下で戻る
- Forwardメニュー押下で進む
- Reloadメニュー押下で再読み込みする
- Exitメニュー押下でアプリを閉じる
- Always On Topメニュー押下で `Topmost` をON/OFFする
- `AlwaysOnTopStatusText` に `最前面: ON/OFF` を表示する
- WebView2の現在URLを `CurrentUrlText` に表示する
- 読み込み中、完了、失敗などを `LoadingStatusText` に表示する
- お気に入りボタン押下で登録URLへ遷移する
- 無効なブックマークはボタンを無効化する
- 未登録のボタンは無効化または空表示にする

## 6. 終了時保存を実装する

アプリ終了時に以下を `settings.json` へ保存する。

- 現在URL
- ウィンドウ位置
- ウィンドウサイズ
- 常に最前面状態

ただし、ウィンドウが最小化状態の場合は、不正な位置・サイズを保存しないこと。

## 7. SettingsWindow を実装する

ユーザー作成済みの左カテゴリ型UIを維持すること。

### 対象UI

- `GeneralSettingsCategoryButton`
- `FavoriteButtonsCategoryButton`
- `GeneralSettingsPanel`
- `FavoriteButtonsSettingsPanel`
- `HomeUrlTextBox`
- `RestoreLastUrlCheckBox`
- `StartAlwaysOnTopCheckBox`
- `RestoreWindowStateCheckBox`
- `FavoriteLabelTextBox01` 〜 `FavoriteLabelTextBox10`
- `FavoriteUrlTextBox01` 〜 `FavoriteUrlTextBox10`
- `FavoriteEnabledCheckBox01` 〜 `FavoriteEnabledCheckBox10`
- `SaveSettingsButton`
- `CancelSettingsButton`
- `ResetSettingsButton`

### 左カテゴリ切り替え

- `GeneralSettingsCategoryButton` 押下時:
  - `GeneralSettingsPanel` を表示
  - `FavoriteButtonsSettingsPanel` を非表示

- `FavoriteButtonsCategoryButton` 押下時:
  - `GeneralSettingsPanel` を非表示
  - `FavoriteButtonsSettingsPanel` を表示

### 設定画面の表示

- `OpenSettingsMenuItem` 押下で `SettingsWindow` を開く
- `Owner` は `MainWindow` にする
- 設定保存後、MainWindow側のお気に入りボタン表示と設定を再読み込みする

### 保存ボタン

`SaveSettingsButton` 押下時に以下を保存する。

- ホームURL
- 前回URL復元フラグ
- 常に最前面で起動するフラグ
- ウィンドウ位置・サイズ復元フラグ
- お気に入りボタン01〜10の表示名、URL、有効状態

保存後は設定ウィンドウを閉じる。

### キャンセルボタン

`CancelSettingsButton` 押下時は保存せずに閉じる。

### 初期値に戻すボタン

`ResetSettingsButton` 押下時は、画面上の入力欄を初期値に戻す。

この時点では即保存ではなく、保存ボタンを押した時だけJSONへ保存する。

## 8. ReloadSettingsMenuItem を実装する

`ReloadSettingsMenuItem` 押下時に以下を行う。

- `settings.json` を再読み込みする
- `bookmarks.json` を再読み込みする
- お気に入りボタン表示を更新する
- ステータスバーに再読み込み完了を表示する

## 9. README.md を追加する

以下を簡潔に記載する。

- LiteTube Dock の目的
- 使用技術
- 起動方法
- 設定ファイルの場所
- `settings.json` の説明
- `bookmarks.json` の説明
- YouTubeログイン情報はアプリ側で保存しないこと
- WebView2標準のCookie/session管理を使うこと
- 広告ブロックやYouTube UI改変は行わないこと

## 10. 実装しないこと

今回のタスクでは以下を実装しない。

- タブ機能
- 複数ウィンドウ機能
- YouTube広告ブロック
- YouTube UI改変
- YouTubeコメント自動投稿
- Google ID、パスワード、認証情報の保存
- 音量操作
- ミニモード
- テーマ切り替え
- メモリ使用量表示
- 複雑なMVVM化
- UIの全面再設計

# 受け入れ条件（目視確認基準）

## ビルド確認

- Visual Studio または `dotnet build` でビルドエラーがない
- アプリ起動時にクラッシュしない

## MainWindow確認

- 起動すると `PlayerWebView` にYouTubeが表示される
- `Home` 操作でホームURLへ遷移する
- `戻る` が使える
- `進む` が使える
- `再読み込み` が使える
- `終了` でアプリが閉じる
- `常に最前面` のON/OFFが切り替わる
- `AlwaysOnTopStatusText` がON/OFFに応じて変わる
- 現在URLが `CurrentUrlText` に表示される
- 読み込み状態が `LoadingStatusText` に表示される

## お気に入りボタン確認

- `data/bookmarks.json` の内容が `FavoriteButton01` 〜 `FavoriteButton10` に反映される
- ボタンの表示名がJSONの `label` と一致する
- 有効なボタンを押すとJSONの `url` へ遷移する
- 無効なボタンは押せない、または未登録状態として表示される

## 設定ファイル確認

- `data/settings.json` が存在しない場合、自動作成される
- `data/bookmarks.json` が存在しない場合、自動作成される
- アプリ終了時に最後のURLが保存される
- アプリ終了時にウィンドウ位置とサイズが保存される
- アプリ再起動時に保存内容が復元される

## SettingsWindow確認

- `設定` → `設定を開く` で設定ウィンドウが開く
- 左側の `基本設定` を押すと基本設定パネルが表示される
- 左側の `お気に入りボタン` を押すとお気に入り設定パネルが表示される
- 保存ボタンで設定内容がJSONへ保存される
- キャンセルボタンでは保存されずに閉じる
- 初期値に戻すボタンで画面上の入力欄が初期値に戻る
- 保存後、MainWindowのお気に入りボタン表示が更新される

## README確認

- `README.md` が追加されている
- 目的、使用技術、設定ファイル、注意事項が簡潔に書かれている

## UI確認

- `MainWindow.xaml` の既存レイアウトが大きく崩れていない
- `SettingsWindow.xaml` の左カテゴリ型レイアウトが維持されている
- 既存の `x:Name` が不必要に変更・削除されていない
