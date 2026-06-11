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

チケット011fix1 現在のウィンドウサイズ取得ボタン追加と自動再生設定追加

# 目的

チケット011で、設定画面の `ウィンドウサイズ` に `現在のサイズ` と縦長プリセットを追加した。

しかし、`現在のサイズ` を選択しただけでは、現在表示中の MainWindow の幅・高さ取得が分かりにくく、正しく反映されない場合がある。

そのため、基本設定画面に明示的な `現在のウィンドウサイズを取得` ボタンを追加し、ユーザー操作で現在の MainWindow の幅・高さを `ウィンドウ幅` / `ウィンドウ高さ` へ反映できるようにする。

また、YouTube動画やライブを開いた際に自動再生を試みたい場合があるため、設定項目として `自動再生` チェックボックスを追加する。

ただし、自動再生は WebView2 / Chromium / YouTube 側の仕様やポリシーに依存するため、必ず再生開始できることは保証しない。アプリ側では「自動再生を試みる」設定として実装する。

# 対象ファイル（推定可）

- `Views/GeneralSettingsView.xaml`
- `Views/GeneralSettingsView.xaml.cs`
- `SettingsWindow.xaml.cs`
- `MainWindow.xaml`
- `MainWindow.xaml.cs`
- `Models/AppSettings.cs`
- `Services/SettingsService.cs`
- `Constants/AppConstants.cs`
- `data/settings.json`
- `README.md`

必要に応じて、WebView2初期化オプション用の小さなヘルパーを追加してよい。

# 実装内容（具体的変更指示）

## 1. 作業開始前の確認

- 必ず `AGENT.md` を読むこと。
- 作業対象は `E:\Dev\LiteTubeDock` 配下のみとすること。
- `E:\Dev\LiteTubeDock` の外側にあるファイルやフォルダは一切編集しないこと。
- 既存UIの `x:Name` は原則変更しないこと。
- MainWindow、SettingsWindow、UserControl構成を壊さないこと。
- 既存の設定保存・読み込み・キャンセル・初期値リセット処理を壊さないこと。
- YouTubeのUI改変やDOM操作による自動クリックは行わないこと。

## 2. 現在のウィンドウサイズ取得ボタンを追加する

`Views/GeneralSettingsView.xaml` の基本設定画面に、現在表示中の MainWindow サイズを取得するボタンを追加する。

推奨表示名:

```text
現在のウィンドウサイズを取得
```

推奨 `x:Name`:

```text
CaptureCurrentWindowSizeButton
```

配置場所:

```text
ウィンドウサイズ
ウィンドウ幅
ウィンドウ高さ
[現在のウィンドウサイズを取得]
[ウィンドウ位置・サイズを初期値に戻す]
```

既存の `ResetWindowBoundsButton` の近くに配置すること。

## 3. サイズ取得ボタン押下時の動作

`CaptureCurrentWindowSizeButton` を押した時、現在表示中の MainWindow の幅・高さを取得し、以下へ反映する。

- `WindowWidthTextBox`
- `WindowHeightTextBox`

期待動作:

```text
MainWindow が 1280x720 で表示されている
↓
設定画面を開く
↓
現在のウィンドウサイズを取得 を押す
↓
ウィンドウ幅 = 1280
ウィンドウ高さ = 720
```

仕様:

- 小数が出る場合は整数へ丸める
- WPFの `Width` / `Height` と `ActualWidth` / `ActualHeight` のどちらを使うかは、既存処理と安全に整合するものを選ぶこと
- 最大化中やフルスクリーン中の場合は、取得可能な安全なサイズを使用する
- 取得に失敗してもクラッシュしない
- 取得後の `WindowSizePresetComboBox` は `現在のサイズ` または `カスタム` にしてよい
- どちらにするかは既存実装と整合する方式を選ぶこと

## 4. SettingsWindowからGeneralSettingsViewへ現在サイズを渡す

`GeneralSettingsView` は UserControl に分離済みのため、現在の MainWindow サイズを受け渡す経路を用意する。

実装例:

```csharp
GeneralSettingsView.SetCurrentWindowSize(double width, double height)
```

または

```csharp
GeneralSettingsView.LoadSettings(AppSettings settings, double currentWindowWidth, double currentWindowHeight)
```

既存の `LoadSettings` / `CollectSettings` がある場合は、それらを壊さず拡張すること。

## 5. 自動再生チェックボックスを追加する

`Views/GeneralSettingsView.xaml` の基本設定画面に、自動再生設定用のチェックボックスを追加する。

表示名:

```text
自動再生
```

推奨 `x:Name`:

```text
AutoplayCheckBox
```

配置場所:

- 基本設定のチェック項目群の近く
- `起動時に前回URLを復元する`
- `常に最前面で起動する`
- `ウィンドウ位置とサイズを復元する`

これらの近くに追加すること。

## 6. AppSettingsへ自動再生設定を追加する

`Models/AppSettings.cs` に自動再生設定を追加する。

推奨プロパティ名:

```csharp
EnableAutoplay
```

JSON保存名は既存の命名規則に合わせること。

推奨JSON:

```json
{
  "enableAutoplay": false
}
```

既定値:

```text
false
```

理由:

- 自動再生は環境やYouTube側の仕様に依存する
- 意図せず音が出る可能性を避けるため、既定ではOFFにする

## 7. settings.jsonへの保存・復元

`enableAutoplay` を `settings.json` に保存・復元する。

仕様:

- 初回起動時、存在しない場合は `false`
- 設定画面でONにして保存すると `true` として保存
- 設定画面でOFFにして保存すると `false` として保存
- 再起動後も設定画面に反映される

## 8. WebView2自動再生の実装方針

自動再生ONの場合、WebView2初期化時に自動再生を試みる設定を追加する。

実装候補:

- `CoreWebView2EnvironmentOptions.AdditionalBrowserArguments`
- Chromium / WebView2 の `autoplay-policy` 関連フラグ

例:

```text
--autoplay-policy=no-user-gesture-required
```

注意:

- 既存のWebView2 UserDataFolder初期化を壊さないこと
- WebView2初期化順序を壊さないこと
- 自動再生設定を変更した場合、反映に再起動が必要になる可能性がある
- その場合はREADMEに明記すること
- 自動再生ONでもYouTube側仕様により必ず再生されるとは限らない

## 9. YouTube UI改変は禁止

今回の自動再生設定では、以下を行わないこと。

- YouTubeページ内の再生ボタンを自動クリックする
- JavaScriptでYouTube DOMを操作して再生する
- YouTube UIを書き換える
- YouTubeプレイヤーの内部APIを無理に呼ぶ

自動再生は、WebView2/Chromium側の許可設定を試みる範囲に留める。

## 10. ループ再生は実装しない

今回のタスクでは、ループ再生は実装しない。

ループ再生はYouTube標準機能を使用する方針とする。

READMEに必要であれば以下を追記する。

```text
ループ再生はYouTube標準のループ機能を使用してください。
```

## 11. 設定画面保存後の反映

`自動再生` 設定は保存されること。

ただし、WebView2初期化オプションとして扱う場合、保存後すぐに現在のWebView2へ反映できない可能性がある。

その場合は以下の方針でよい。

- 保存は即時反映
- 実際のWebView2自動再生オプションは次回起動時に反映
- ステータスバーまたはREADMEで、必要なら再起動後反映と分かるようにする

既存の設定保存動作を壊さないこと。

## 12. README更新

READMEに以下を追記する。

- 基本設定に `現在のウィンドウサイズを取得` ボタンがあること
- ボタンを押すと、現在のMainWindow幅・高さを設定欄へ反映できること
- 基本設定に `自動再生` チェックボックスがあること
- 自動再生はWebView2/YouTube側の仕様に依存し、必ず再生されるとは限らないこと
- 自動再生設定の反映に再起動が必要な場合は、その旨を記載すること
- ループ再生はYouTube標準機能を使用する方針であること

## 13. 実装しないこと

今回のタスクでは以下を実装しない。

- YouTubeの再生ボタンを自動クリックする処理
- YouTube DOM操作による自動再生
- YouTube API連携
- ループ再生機能
- YouTubeショート専用モード
- 動画種別による自動サイズ変更
- Googleログイン自動化
- YouTube UI改変

# 受け入れ条件（目視確認基準）

## ビルド確認

- `dotnet build` でビルドエラーがない
- 警告が出る場合は内容を報告すること
- アプリ起動時にクラッシュしない

## 現在サイズ取得ボタン確認

- `設定` → `設定を開く` で設定画面が開く
- `基本設定` に `現在のウィンドウサイズを取得` ボタンが表示される
- MainWindowを任意のサイズにする
- `現在のウィンドウサイズを取得` を押す
- `ウィンドウ幅` に現在のMainWindow幅が入る
- `ウィンドウ高さ` に現在のMainWindow高さが入る
- 小数ではなく、扱いやすい整数で表示される
- 取得後に保存すると、そのサイズがMainWindowへ反映される
- アプリ再起動後も保存したサイズが復元される

## 自動再生設定確認

- `基本設定` に `自動再生` チェックボックスが表示される
- 初期値はOFFである
- ONにして保存できる
- OFFにして保存できる
- 再起動後もON/OFF状態が復元される
- `settings.json` に `enableAutoplay` または同等の項目が保存される

## 自動再生動作確認

- 自動再生ON時、WebView2初期化時に自動再生を試みる設定が使われる
- 自動再生ONでもYouTube側仕様により必ず再生されない場合があることを報告する
- 自動再生OFF時、既存挙動を壊さない
- 自動再生設定によりアプリがクラッシュしない

## 既存機能確認

- Home / Back / Forward / Reload が動作する
- アドレスバーが壊れていない
- アドレスバー表示/非表示が壊れていない
- お気に入りボタンでURL遷移できる
- お気に入り右クリック登録が壊れていない
- お気に入りボタンの色・アイコン表示が壊れていない
- フルスクリーン切り替えが壊れていない
- ウィンドウサイズプリセットが壊れていない
- ウィンドウ位置・サイズリセットが壊れていない
- 設定保存が壊れていない
- YouTubeログイン情報、Google ID、パスワード、認証トークンは保存されていない
