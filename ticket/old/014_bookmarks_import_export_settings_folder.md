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
- `MainWindow.xaml`、`SettingsWindow.xaml`、既存 `Views/*.xaml` と既存 `x:Name` は原則維持すること
- UIを大きく作り直さず、既存UIに処理を接続する形を優先すること
- YouTube / Google のログイン・ログアウト機能をアプリ側で独自実装しないこと
- Google ID、パスワード、認証トークンを保存しないこと

# タスク名

チケット014 お気に入り設定のインポート・エクスポートと設定フォルダ管理追加

# 目的

LiteTube Dock のお気に入りボタン設定が増えてきたため、設定ファイルをバックアップ・移行・共有しやすくする。

特に `bookmarks.json` は、表示名、URL、色、アイコン、再生モード、自動再生、ミュート、ループ、リジュームなどの設定を持つため、ファイルメニューからインポート・エクスポートできるようにしたい。

また、設定ファイルの保存先フォルダを設定画面から確認・変更できるようにする。

特に指定しない場合は、アプリフォルダ配下に `settings` フォルダを作成し、そこを標準のエクスポート先として利用できるようにする。

# 対象ファイル（推定可）

- `MainWindow.xaml`
- `MainWindow.xaml.cs`
- `Views/GeneralSettingsView.xaml`
- `Views/GeneralSettingsView.xaml.cs`
- `SettingsWindow.xaml.cs`
- `Models/AppSettings.cs`
- `Services/SettingsService.cs`
- `Services/BookmarkService.cs`
- `Services/AppPathService.cs`
- `Constants/AppConstants.cs`
- `data/settings.json`
- `data/bookmarks.json`
- `README.md`

必要に応じて、インポート・エクスポート用の小さなServiceを追加してよい。

# 実装内容（具体的変更指示）

## 1. 作業開始前の確認

- 必ず `AGENT.md` を読むこと。
- 作業対象は `E:\Dev\LiteTubeDock` 配下のみとすること。
- `E:\Dev\LiteTubeDock` の外側にあるファイルやフォルダは一切編集しないこと。
- 既存UIの `x:Name` は原則変更しないこと。
- MainWindow、SettingsWindow、UserControl構成を壊さないこと。
- 既存の設定保存・読み込み・キャンセル・初期値リセット処理を壊さないこと。

## 2. 標準エクスポート先フォルダを追加する

アプリ標準の設定エクスポート先として、以下を使用する。

```text
settings
```

アプリフォルダ配下、または既存のAppPathService方針に従った安全な場所に作成する。

推奨パス:

```text
E:\Dev\LiteTubeDock\settings
```

実行時はアプリ配置フォルダ基準でよい。

仕様:

- フォルダが存在しない場合は自動作成する
- 初回起動時、またはエクスポート時に作成してよい
- 既存の `data` フォルダとは役割を分ける

役割:

- `data`
  - アプリが実際に読み書きする現行設定

- `settings`
  - ユーザーが手動でバックアップ・エクスポート・インポートするための保存先

## 3. AppSettingsに設定フォルダパスを追加する

設定ファイル保存先フォルダを `settings.json` へ保存できるようにする。

推奨プロパティ:

```csharp
public string SettingsExportFolder { get; set; }
```

JSON例:

```json
{
  "settingsExportFolder": "settings"
}
```

既定値:

```text
settings
```

仕様:

- 空、null、不正なパスの場合は既定の `settings` を使用する
- アプリ外パスも許可するかどうかは安全性を考慮すること
- まずは通常のフォルダ選択で選ばれた場所を保存してよい
- パスが存在しない場合は作成する、またはエラー表示する

## 4. 設定画面に設定フォルダ項目を追加する

`Views/GeneralSettingsView.xaml` の基本設定に、設定ファイル保存先フォルダ項目を追加する。

推奨UI:

```text
設定フォルダ
[ settings________________________ ] [参照] [開く]
```

推奨 `x:Name`:

- `SettingsExportFolderTextBox`
- `BrowseSettingsExportFolderButton`
- `OpenSettingsExportFolderButton`

配置場所:

- 基本設定画面の下部
- ウィンドウサイズ設定より下でもよい

## 5. 参照ボタン

`BrowseSettingsExportFolderButton` を押すと、フォルダ選択ダイアログを開く。

仕様:

- フォルダを選択すると `SettingsExportFolderTextBox` にパスを反映する
- 保存ボタンを押すと `settings.json` に保存される
- 可能であれば、アプリフォルダ配下なら相対パスで保存してもよい
- 難しければ絶対パス保存でもよい
- パスが不正な場合はクラッシュしないこと

## 6. 開くボタン

`OpenSettingsExportFolderButton` を押すと、設定フォルダをエクスプローラーで開く。

仕様:

- フォルダが存在しない場合は作成してから開く
- 開けない場合はメッセージ表示する
- アプリがクラッシュしないこと

## 7. ファイルメニューにインポート・エクスポートを追加する

`MainWindow.xaml` の `FileMenuItem` 配下に、以下を追加する。

```text
ファイル
├─ お気に入り設定をインポート
├─ お気に入り設定をエクスポート
└─ 終了
```

推奨 `x:Name`:

- `ImportBookmarksMenuItem`
- `ExportBookmarksMenuItem`

既存の `ExitMenuItem` は維持すること。

## 8. お気に入り設定エクスポート

`ExportBookmarksMenuItem` を押した場合、現在の `data/bookmarks.json` を選択先へエクスポートする。

仕様:

- 既定の保存先は `settingsExportFolder`
- ファイル保存ダイアログを表示する
- 既定ファイル名例:

```text
bookmarks_export.json
```

または日付付き:

```text
bookmarks_yyyyMMdd_HHmmss.json
```

- 保存形式はJSON
- エクスポート対象はお気に入り設定のみ
- `settings.json` は含めない
- 成功時はステータスバーまたはメッセージで通知する
- 失敗時はエラーメッセージを表示し、クラッシュしない

## 9. お気に入り設定インポート

`ImportBookmarksMenuItem` を押した場合、JSONファイルを選択して `data/bookmarks.json` へ取り込む。

仕様:

- ファイル選択ダイアログを表示する
- 既定の開始フォルダは `settingsExportFolder`
- 選択したJSONを読み込む
- BookmarkServiceの検証・フォールバック処理を通す
- 最大10件の既存仕様を維持する
- 不正JSONの場合は取り込まない
- 取り込み前に確認メッセージを表示する

確認メッセージ例:

```text
現在のお気に入り設定を上書きします。インポートしてよろしいですか？
```

- OK/はいの場合のみインポートする
- キャンセル/いいえの場合は何もしない
- インポート成功後、MainWindowのお気に入りボタン表示を更新する
- 設定画面を開き直した場合、インポート内容が表示されること

## 10. インポート時のバックアップ

安全のため、インポート前に現在の `data/bookmarks.json` をバックアップする。

バックアップ先:

```text
settingsExportFolder
```

ファイル名例:

```text
bookmarks_backup_yyyyMMdd_HHmmss.json
```

仕様:

- バックアップに失敗した場合は、インポートを中止するか、ユーザーに確認する
- まずは安全優先で、バックアップ失敗時はインポートしない方針でよい

## 11. JSON検証

インポート時は以下を確認する。

- JSONとして読み込める
- BookmarkItem配列として扱える
- 最大10件へ収まる、または10件まで採用する
- `label` / `url` / `sortOrder` / `isEnabled` などの既存項目が安全に扱える
- 追加済みの以下項目も安全に扱える
  - `backgroundColor`
  - `foregroundColor`
  - `iconPath`
  - `iconShape`
  - `iconRounded`
  - `playbackMode`
  - `autoplay`
  - `mute`
  - `loop`
  - `resumePlayback`

不正値は既存の既定値へフォールバックすること。

## 12. 既存設定への影響

今回のインポート・エクスポート対象は、まずはお気に入り設定のみとする。

以下は対象外:

- `settings.json`
- WebView2ユーザーデータ
- YouTubeログイン状態
- Cookie/session
- アプリウィンドウ位置
- 自動再生などの全体設定

## 13. README更新

READMEに以下を追記する。

- ファイルメニューからお気に入り設定をインポート・エクスポートできること
- エクスポート先の既定フォルダは `settings`
- 設定画面で設定フォルダを指定できること
- インポート時は現在のお気に入り設定を上書きすること
- インポート前にバックアップを作成すること
- YouTubeログイン情報やCookieはエクスポート対象ではないこと

## 14. 実装しないこと

今回のタスクでは以下を実装しない。

- settings.json全体のインポート・エクスポート
- WebView2ユーザーデータのバックアップ
- YouTubeログイン状態のバックアップ
- Google ID、パスワード、認証トークンの保存
- クラウド同期
- 複数プロファイル管理
- お気に入り件数増加
- ドラッグ＆ドロップ並び替え

# 受け入れ条件（目視確認基準）

## ビルド確認

- `dotnet build` でビルドエラーがない
- 警告が出る場合は内容を報告すること
- アプリ起動時にクラッシュしない

## 設定フォルダ確認

- `設定` → `設定を開く` → `基本設定` を表示する
- `設定フォルダ` 入力欄が表示される
- `参照` ボタンが表示される
- `開く` ボタンが表示される
- `参照` でフォルダ選択できる
- `開く` でフォルダが開く
- フォルダが存在しない場合は作成される、または安全にエラー表示される
- 保存後、`settings.json` に `settingsExportFolder` が保存される

## ファイルメニュー確認

- `ファイル` メニューに `お気に入り設定をインポート` が表示される
- `ファイル` メニューに `お気に入り設定をエクスポート` が表示される
- `終了` が引き続き表示される

## エクスポート確認

- `お気に入り設定をエクスポート` を押す
- 保存ダイアログが開く
- JSONファイルとして保存できる
- 保存したJSONにお気に入り設定が含まれる
- Google ID、パスワード、認証トークンは含まれない

## インポート確認

- `お気に入り設定をインポート` を押す
- ファイル選択ダイアログが開く
- JSONを選択すると上書き確認が出る
- キャンセルした場合は取り込まれない
- OK/はいの場合のみ取り込まれる
- インポート成功後、MainWindowのお気に入りボタン表示が更新される
- インポート成功後、設定画面にも反映される
- インポート前バックアップが作成される

## 不正ファイル確認

- 不正JSONを選んでもクラッシュしない
- お気に入り形式ではないJSONを選んでもクラッシュしない
- エラーメッセージが表示される

## 既存機能確認

- アドレスバーが壊れていない
- お気に入りボタンでURL遷移できる
- お気に入り右クリック登録が壊れていない
- お気に入りごとの再生オプションが壊れていない
- お気に入りボタンの色・アイコン表示が壊れていない
- フルスクリーン切り替えが壊れていない
- ウィンドウサイズプリセットが壊れていない
- ウィンドウ位置・サイズリセットが壊れていない
- 設定保存が壊れていない
- YouTubeログイン情報、Google ID、パスワード、認証トークンは保存・エクスポートされていない
