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

チケット013fix1 お気に入りごとのリジューム制御追加

# 目的

チケット013で、お気に入りごとの再生モード、自動再生、ミュート、ループ設定を追加した。

しかし、YouTube側またはWebView2側の再生位置復元により、以前再生していた動画URLをお気に入りボタンから開いた時に、動画が最初から再生されず、途中位置からリジュームされる場合がある。

お気に入りボタンごとに「リジュームを許可する / 最初から再生する」を選べるようにし、用途に応じて再生開始位置を制御できるようにする。

例:

```text
お気に入り01: リジュームON
→ 前回位置から再生してよい

お気に入り02: リジュームOFF
→ ボタンを押したらできるだけ最初から再生する
```

# 対象ファイル（推定可）

- `Views/FavoriteButtonsSettingsView.xaml`
- `Views/FavoriteButtonsSettingsView.xaml.cs`
- `MainWindow.xaml.cs`
- `Models/BookmarkItem.cs`
- `Services/BookmarkService.cs`
- `Constants/AppConstants.cs`
- `data/bookmarks.json`
- `README.md`

必要に応じて、YouTube URLパラメータ制御用の小さなヘルパーを追加してよい。

# 実装内容（具体的変更指示）

## 1. 作業開始前の確認

- 必ず `AGENT.md` を読むこと。
- 作業対象は `E:\Dev\LiteTubeDock` 配下のみとすること。
- `E:\Dev\LiteTubeDock` の外側にあるファイルやフォルダは一切編集しないこと。
- 既存UIの `x:Name` は原則変更しないこと。
- MainWindow、SettingsWindow、UserControl構成を壊さないこと。
- 既存の設定保存・読み込み・キャンセル・初期値リセット処理を壊さないこと。
- YouTubeのUI改変やDOM操作による自動クリックは行わないこと。

## 2. お気に入りごとのリジューム設定を追加する

各お気に入りに、リジューム設定を追加する。

表示名:

```text
リジューム
```

推奨 `x:Name`:

- `FavoriteResumeCheckBox01` 〜 `FavoriteResumeCheckBox10`

配置場所:

現在のお気に入り設定カード内の再生オプション行に追加する。

現在の例:

```text
再生  再生モード [通常]  自動再生 [✓]  ミュート [✓]  ループ [ ]
```

追加後の例:

```text
再生  再生モード [通常]  自動再生 [✓]  ミュート [✓]  ループ [ ]  リジューム [ ]
```

横幅が足りない場合は、再生オプションを2段にしてよい。

## 3. JSON項目を追加する

`BookmarkItem` にリジューム設定を追加する。

推奨プロパティ名:

```csharp
public bool ResumePlayback { get; set; }
```

JSON例:

```json
{
  "resumePlayback": true
}
```

既定値:

```text
true
```

理由:

- 既存挙動に近い
- 既存ユーザーの挙動を急に変えない
- 最初から再生したいボタンだけOFFにできる

## 4. BookmarkServiceの読み書き対応

`Services/BookmarkService.cs` で、`resumePlayback` の読み込み・保存を行う。

仕様:

- `resumePlayback` が未指定なら `true`
- 不正値の場合は `true`
- 既存の `playbackMode` / `autoplay` / `mute` / `loop` / 色 / アイコン設定を壊さないこと

## 5. リジュームOFF時のURL制御

`ResumePlayback == false` のお気に入りボタンを押した場合、可能な範囲で最初から再生されるようにURLを調整する。

方針:

- YouTube動画URLの場合、再生開始位置パラメータを除去する
- `t=`
- `start=`
- その他、既存URLに含まれる開始位置系パラメータがあれば安全に除去する
- embed URL生成時は `start=0` を付ける、または開始位置指定なしにする
- 通常URLの場合も、可能なら開始位置系パラメータを除去する

例:

```text
https://www.youtube.com/watch?v=VIDEO_ID&t=123s
↓
https://www.youtube.com/watch?v=VIDEO_ID
```

embed例:

```text
https://www.youtube.com/embed/VIDEO_ID?autoplay=1&mute=1
```

注意:

- YouTube側・ブラウザ側の再生位置復元により、必ず最初から再生されるとは限らない
- アプリ側では「最初から再生されやすいURLに整える」範囲に留める
- YouTube DOM操作や再生位置をJavaScriptで強制操作しないこと

## 6. リジュームON時の挙動

`ResumePlayback == true` の場合は、既存の挙動を維持する。

仕様:

- 登録URLを基本的にそのまま使う
- URLに `t=` や `start=` が入っている場合は維持してよい
- YouTube側の再生位置復元が働く場合も、そのまま許容する

## 7. プレイヤーモードとの整合性

`playbackMode = Player` の場合も、`resumePlayback` を考慮する。

- `resumePlayback = true`
  - 既存URLや既存パラメータをなるべく尊重する

- `resumePlayback = false`
  - `start=0` または開始位置指定なしでembed URLを生成する
  - ループ、ミュート、自動再生パラメータは維持する

## 8. 右クリック登録との整合性

お気に入りボタン右クリックによる「現在再生中のムービーを登録」では、リジューム設定は既存値を維持すること。

新規登録扱いの場合は既定値 `true` でよい。

重要:

- 右クリック登録時に色・アイコン・再生モード・自動再生・ミュート・ループ・リジューム設定を勝手に初期化しないこと
- 表示名、URL、有効状態のみ更新する方針を維持すること

## 9. README更新

READMEに以下を追記する。

- お気に入りごとに `リジューム` を設定できること
- ONの場合は既存URLやYouTube側の再生位置復元を許容すること
- OFFの場合は開始位置パラメータを除去し、できるだけ最初から再生すること
- ただしYouTube側仕様により、必ず最初から再生されるとは限らないこと
- YouTube UI改変やDOM操作は行わないこと

## 10. 実装しないこと

今回のタスクでは以下を実装しない。

- YouTubeプレイヤーの再生位置をJavaScriptで強制的に0秒へ戻す処理
- YouTube DOM操作
- YouTube内部API操作
- 再生履歴削除
- WebView2ユーザーデータの自動削除
- 個別ボタンごとのCookie/session分離
- 画質固定
- YouTube UI改変

# 受け入れ条件（目視確認基準）

## ビルド確認

- `dotnet build` でビルドエラーがない
- 警告が出る場合は内容を報告すること
- アプリ起動時にクラッシュしない

## 設定画面確認

- `設定` → `設定を開く` → `お気に入りボタン` を表示する
- 各お気に入りカード内に `リジューム` チェックボックスが表示される
- 既存の再生モード、自動再生、ミュート、ループ項目が壊れていない
- 既存の表示名、URL、アイコン、背景色、文字色、形状、角丸、有効が壊れていない

## 保存確認

- `リジューム` ON/OFFを変更して保存できる
- アプリ再起動後もリジューム設定が復元される
- `bookmarks.json` に `resumePlayback` が保存される

## リジュームON確認

- `リジューム` ONのボタンをクリックする
- 既存URLの挙動が維持される
- `t=` や `start=` などの開始位置指定がある場合は維持される

## リジュームOFF確認

- `リジューム` OFFのボタンをクリックする
- URLに `t=` や `start=` が含まれている場合、遷移URLから除去される
- プレイヤーモードでも開始位置指定なし、または `start=0` 扱いで開かれる
- 可能な範囲で動画が最初から再生される
- YouTube側仕様により必ず最初から再生されない場合があることを報告する

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
