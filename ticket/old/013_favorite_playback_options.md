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
- YouTube UIを不正に改変するDOM操作、自動クリック、内部API呼び出しは行わないこと

# タスク名

チケット013 お気に入りごとの再生オプション追加

# 目的

お気に入りボタンごとに、通常のYouTubeページで開くか、動画プレイヤー中心の埋め込み表示で開くかを選べるようにする。

また、各お気に入りごとに、自動再生・ミュート・ループを補助設定として持てるようにする。

これにより、たとえば以下のような使い分けを可能にする。

```text
お気に入り01: 通常YouTubeページで開く
お気に入り02: 埋め込みプレイヤー表示 + 自動再生 + ミュート
お気に入り03: 埋め込みプレイヤー表示 + ループ
```

なお、画質をアプリ側で確実に固定することは今回の対象外とする。  
YouTube側の仕様に依存するため、画質固定や画質強制変更は実装しない。

# 対象ファイル（推定可）

- `Views/FavoriteButtonsSettingsView.xaml`
- `Views/FavoriteButtonsSettingsView.xaml.cs`
- `MainWindow.xaml.cs`
- `Models/BookmarkItem.cs`
- `Services/BookmarkService.cs`
- `Constants/AppConstants.cs`
- `data/bookmarks.json`
- `README.md`

必要に応じて、YouTube URL変換用の小さなヘルパーを追加してよい。

# 実装内容（具体的変更指示）

## 1. 作業開始前の確認

- 必ず `AGENT.md` を読むこと。
- 作業対象は `E:\Dev\LiteTubeDock` 配下のみとすること。
- `E:\Dev\LiteTubeDock` の外側にあるファイルやフォルダは一切編集しないこと。
- 既存UIの `x:Name` は原則変更しないこと。
- MainWindow、SettingsWindow、UserControl構成を壊さないこと。
- 既存の設定保存・読み込み・キャンセル・初期値リセット処理を壊さないこと。
- YouTubeのUI改変やDOM操作による自動クリックは行わないこと。

## 2. 現在の表示文言を日本語化する

お気に入り設定画面にあるアイコン形状の選択肢が、現在 `Rectangle` / `Square` の英語表記になっている。

ユーザー視点では分かりにくいため、画面表示上は日本語にする。

表示名:

```text
Square    → 四角
Rectangle → 横長
```

内部保存値は既存互換を優先してよい。

推奨:

- JSON内部値は既存どおり `Square` / `Rectangle` のままでもよい
- 画面表示だけ `四角` / `横長` にする
- READMEにも「四角 / 横長」と記載する

既存データとの互換性を壊さないこと。

## 3. お気に入りごとの再生オプション項目を追加する

`Views/FavoriteButtonsSettingsView.xaml` の各お気に入りカード内に、再生オプション設定を追加する。

スクリーンショット上では、現在以下のようなカード構成になっている。

```text
表示名
URL
アイコン
背景色 / 文字色
形状 / 角丸
```

このカード内の空いている部分、特に `形状 / 角丸` の近く、またはカード下部に以下を追加する。

```text
再生モード [通常 ▼]
自動再生 [ ]  ミュート [ ]  ループ [ ]
```

横幅が足りない場合は、以下のように2段にしてよい。

```text
再生モード [通常 ▼]
自動再生 [ ]  ミュート [ ]  ループ [ ]
```

## 4. 再生モードを追加する

お気に入りごとに再生モードを選択できるようにする。

選択肢:

```text
通常
プレイヤー
```

意味:

- `通常`
  - 既存どおり登録URLをそのまま開く
  - YouTube通常ページを表示する
  - コメント欄や関連UIはYouTube側表示に従う

- `プレイヤー`
  - YouTube動画URLを可能な範囲で `embed` 形式に変換して開く
  - 動画プレイヤー中心の表示を目的とする
  - コメント欄なしの表示に近づける

推奨 `x:Name`:

- `FavoritePlaybackModeComboBox01` 〜 `FavoritePlaybackModeComboBox10`

JSON内部値の例:

```json
{
  "playbackMode": "Normal"
}
```

候補値:

```text
Normal
Player
```

表示名は日本語にする。

```text
Normal → 通常
Player → プレイヤー
```

既定値:

```text
Normal
```

## 5. お気に入りごとの自動再生設定を追加する

各お気に入りに自動再生設定を追加する。

推奨 `x:Name`:

- `FavoriteAutoplayCheckBox01` 〜 `FavoriteAutoplayCheckBox10`

表示名:

```text
自動再生
```

JSON項目:

```json
{
  "autoplay": false
}
```

既定値:

```text
false
```

仕様:

- `playbackMode = Player` の場合、embed URLへ `autoplay=1` を付ける
- `playbackMode = Normal` の場合は、基本的には既存のWebView2自動再生設定に従う
- 自動再生はYouTube / WebView2側仕様に依存し、必ず再生されるとは限らない
- YouTube DOM操作や再生ボタン自動クリックは行わない

## 6. お気に入りごとのミュート設定を追加する

各お気に入りにミュート設定を追加する。

推奨 `x:Name`:

- `FavoriteMuteCheckBox01` 〜 `FavoriteMuteCheckBox10`

表示名:

```text
ミュート
```

JSON項目:

```json
{
  "mute": false
}
```

既定値:

```text
false
```

仕様:

- `playbackMode = Player` の場合、embed URLへ `mute=1` を付ける
- `playbackMode = Normal` の場合、URLパラメータで安全に扱える範囲に留める
- YouTubeページ内のミュートボタンを自動クリックしない
- 実装が難しい場合は、まず `Player` モード時のみ有効でもよい
- READMEにその制約を記載すること

## 7. お気に入りごとのループ設定を追加する

各お気に入りにループ設定を追加する。

推奨 `x:Name`:

- `FavoriteLoopCheckBox01` 〜 `FavoriteLoopCheckBox10`

表示名:

```text
ループ
```

JSON項目:

```json
{
  "loop": false
}
```

既定値:

```text
false
```

仕様:

- `playbackMode = Player` の場合、embed URLへ `loop=1` を付ける
- YouTubeの仕様上、単体動画をループする場合は `playlist=動画ID` が必要になることがある
- 可能であれば、動画IDを抽出して `playlist=<videoId>` を追加する
- 動画IDが抽出できない場合は、ループ設定を無視してもクラッシュしない
- `playbackMode = Normal` の場合は、YouTube標準のループ機能を使う方針でよい

## 8. YouTube URLをembed形式へ変換する

`playbackMode = Player` の場合、登録URLがYouTube動画URLならembed形式へ変換して遷移する。

対応するURL例:

```text
https://www.youtube.com/watch?v=VIDEO_ID
https://youtube.com/watch?v=VIDEO_ID
https://youtu.be/VIDEO_ID
```

変換例:

```text
https://www.youtube.com/watch?v=VIDEO_ID
↓
https://www.youtube.com/embed/VIDEO_ID
```

パラメータ例:

```text
https://www.youtube.com/embed/VIDEO_ID?autoplay=1&mute=1&loop=1&playlist=VIDEO_ID
```

注意:

- 動画IDが抽出できないURLは、通常URLのまま開いてよい
- チャンネルURL、YouTubeトップ、検索ページなどは無理にembed化しない
- URL変換に失敗してもクラッシュしない
- YouTube以外のURLは通常URLとして開く

## 9. BookmarkItemへ項目を追加する

`Models/BookmarkItem.cs` に以下を追加する。

推奨プロパティ:

```csharp
public string PlaybackMode { get; set; }
public bool Autoplay { get; set; }
public bool Mute { get; set; }
public bool Loop { get; set; }
```

JSON例:

```json
{
  "label": "BGM",
  "url": "https://www.youtube.com/watch?v=VIDEO_ID",
  "sortOrder": 2,
  "isEnabled": true,
  "backgroundColor": "#F0F0F0",
  "foregroundColor": "#000000",
  "iconPath": "",
  "iconShape": "Square",
  "iconRounded": true,
  "playbackMode": "Player",
  "autoplay": true,
  "mute": true,
  "loop": false
}
```

既存の `bookmarks.json` へ項目がない場合は、既定値で補完すること。

## 10. BookmarkServiceの読み書き対応

`Services/BookmarkService.cs` で、追加項目の読み込み・保存・不正値フォールバックを行う。

仕様:

- `playbackMode` が未指定なら `Normal`
- `playbackMode` が不正なら `Normal`
- `autoplay` 未指定なら `false`
- `mute` 未指定なら `false`
- `loop` 未指定なら `false`
- 既存の色・アイコン設定を壊さないこと

## 11. MainWindowのお気に入り遷移処理を更新する

お気に入りボタンを押した時、対象BookmarkItemの再生オプションに従って遷移URLを決定する。

処理方針:

```text
BookmarkItem.Url
↓
PlaybackMode が Player か確認
↓
YouTube動画URLなら embed URL に変換
↓
Autoplay / Mute / Loop に応じてパラメータ付与
↓
WebView2で遷移
```

左クリックによる通常のお気に入り遷移のみ更新する。

右クリック登録処理は既存挙動を維持すること。

## 12. 設定画面の保存・読み込み対応

`FavoriteButtonsSettingsView` で、追加した以下の値を読み込み・保存できるようにする。

- 再生モード
- 自動再生
- ミュート
- ループ

保存ボタンで `bookmarks.json` に反映されること。

キャンセルした場合は保存されないこと。

初期値に戻す場合は既定値へ戻ること。

## 13. 画質設定について

今回のタスクでは、画質設定は実装しない。

理由:

- YouTube側の自動画質制御に依存する
- アプリ側から安全に固定するのが難しい
- YouTube UI改変や内部API操作につながる可能性がある

READMEに以下を記載すること。

```text
画質設定はYouTube側の制御に依存するため、LiteTube Dock側では固定しません。
低負荷で使いたい場合は、プレイヤーモード、ミュート、小さいウィンドウサイズを組み合わせてください。
```

## 14. README更新

READMEに以下を追記する。

- お気に入りごとに再生モードを設定できること
- `通常` と `プレイヤー` の違い
- プレイヤーモードではYouTube embed URLを使用すること
- お気に入りごとに自動再生・ミュート・ループを設定できること
- 自動再生やループはYouTube / WebView2側仕様に依存すること
- 画質固定は実装しないこと
- 低負荷で使う場合の推奨として、プレイヤーモード・ミュート・小さいウィンドウサイズを使うこと

## 15. 実装しないこと

今回のタスクでは以下を実装しない。

- 画質固定
- YouTube画質メニューの自動操作
- YouTube DOM操作
- YouTube再生ボタンの自動クリック
- YouTube API連携
- サムネイル取得
- コメント欄だけをDOMで非表示にする処理
- プレイヤー内部APIの直接制御
- お気に入り件数増加
- マルチビュー制御

# 受け入れ条件（目視確認基準）

## ビルド確認

- `dotnet build` でビルドエラーがない
- 警告が出る場合は内容を報告すること
- アプリ起動時にクラッシュしない

## 設定画面確認

- `設定` → `設定を開く` → `お気に入りボタン` を表示する
- 各お気に入りカード内に `再生モード` が表示される
- `再生モード` に `通常` と `プレイヤー` が表示される
- 各お気に入りカード内に `自動再生` が表示される
- 各お気に入りカード内に `ミュート` が表示される
- 各お気に入りカード内に `ループ` が表示される
- 既存の表示名、URL、アイコン、背景色、文字色、形状、角丸、有効が壊れていない
- アイコン形状の表示が `四角` / `横長` など日本語で分かりやすくなっている

## 保存確認

- 再生モードを変更して保存できる
- 自動再生ON/OFFを変更して保存できる
- ミュートON/OFFを変更して保存できる
- ループON/OFFを変更して保存できる
- アプリ再起動後も設定が復元される
- `bookmarks.json` に `playbackMode` / `autoplay` / `mute` / `loop` が保存される

## 通常モード確認

- 再生モード `通常` のお気に入りボタンをクリックする
- 登録URLがそのまま開く
- 既存と同じようにYouTube通常ページが表示される

## プレイヤーモード確認

- YouTube動画URLを登録したお気に入りで `プレイヤー` を選択する
- 保存する
- 対象のお気に入りボタンをクリックする
- YouTube embed プレイヤー形式で表示される
- コメント欄は表示されず、プレイヤー中心の表示になる

## 自動再生・ミュート・ループ確認

- プレイヤーモードで自動再生ONにした場合、URLに自動再生用パラメータが付与される
- プレイヤーモードでミュートONにした場合、URLにミュート用パラメータが付与される
- プレイヤーモードでループONにした場合、可能であれば `loop=1` と `playlist=VIDEO_ID` が付与される
- ただしYouTube側仕様により必ず期待どおり動作するとは限らないことを報告する

## フォールバック確認

- YouTubeトップやチャンネルURLでプレイヤーモードを選んでもクラッシュしない
- 動画IDが抽出できない場合は通常URLで開く、または安全にフォールバックする
- YouTube以外のURLでもクラッシュしない

## 既存機能確認

- アドレスバーが壊れていない
- アドレスバーの現在URL同期が壊れていない
- お気に入り右クリック登録が壊れていない
- お気に入りボタンの色・アイコン表示が壊れていない
- フルスクリーン切り替えが壊れていない
- ウィンドウサイズプリセットが壊れていない
- ウィンドウ位置・サイズリセットが壊れていない
- 設定保存が壊れていない
- YouTubeログイン情報、Google ID、パスワード、認証トークンは保存されていない
