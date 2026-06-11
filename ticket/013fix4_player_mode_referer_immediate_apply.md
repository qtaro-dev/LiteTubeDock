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
- プレイヤーモードは実験的機能として扱い、通常モードを安定版として維持すること

# タスク名

チケット013fix4 プレイヤーモードReferer設定の即時反映と既定値見直し

# 目的

チケット013fix3で、プレイヤーモード用のReferer設定と診断ログを追加した。

検証の結果、`https://litetubedock.local/` をRefererに設定すると、YouTube embed のプレイヤーモードで再生できるケースが確認できた。

しかし現状では、設定画面で `プレイヤーモードReferer` を変更して保存しても、アプリを終了して再起動しないとWebView2側のReferer付与処理に反映されない。

このため、Referer値やON/OFFを変更した後、アプリ再起動なしでプレイヤーモードの検証ができるようにする。

あわせて、既定Referer値を `https://www.youtube.com/` から、アプリ識別用の値として `https://litetubedock.local/` へ変更する。

# 対象ファイル（推定可）

- `MainWindow.xaml.cs`
- `SettingsWindow.xaml.cs`
- `Views/GeneralSettingsView.xaml.cs`
- `Models/AppSettings.cs`
- `Services/SettingsService.cs`
- `Constants/AppConstants.cs`
- `Constants/HelpContent.cs`
- `data/settings.json`
- `README.md`

必要に応じて、設定反映用のメソッドや小さなヘルパーを追加してよい。

# 実装内容（具体的変更指示）

## 1. 作業開始前の確認

- 必ず `AGENT.md` を読むこと。
- 作業対象は `E:\Dev\LiteTubeDock` 配下のみとすること。
- `E:\Dev\LiteTubeDock` の外側にあるファイルやフォルダは一切編集しないこと。
- 既存UIの `x:Name` は原則変更しないこと。
- MainWindow、SettingsWindow、UserControl構成を壊さないこと。
- 既存の設定保存・読み込み・キャンセル・初期値リセット処理を壊さないこと。
- YouTubeのUI改変やDOM操作による自動クリックは行わないこと。

## 2. 既定Referer値を変更する

プレイヤーモードRefererの既定値を以下に変更する。

```text
https://litetubedock.local/
```

対象:

- `AppConstants.cs` などの既定値定義
- `SettingsService` の初期値・フォールバック値
- `data/settings.json` に既定値として反映される値
- README
- アプリ内ヘルプ

変更前の既定値が `https://www.youtube.com/` の場合は、今後は `https://litetubedock.local/` を標準とする。

## 3. 設定保存後にMainWindowへ即時反映する

設定画面で以下を変更して保存した時、アプリ再起動なしでMainWindow側のReferer付与処理に反映する。

対象設定:

- `playerModeRefererEnabled`
- `playerModeReferer`

期待動作:

```text
設定画面で Referer を変更
↓
保存
↓
アプリ再起動なしで、次回のプレイヤーモード遷移から新Referer値を使う
```

## 4. MainWindow側の設定参照を最新化する

WebResourceRequestedのReferer付与処理が、古い設定値をキャッシュしたままにならないようにする。

方針:

- `MainWindow` が保持している `_settings` または同等の設定オブジェクトを、設定保存後に最新化する
- `WebResourceRequested` の処理では、毎回最新の `PlayerModeRefererEnabled` と `PlayerModeReferer` を参照する
- 古いReferer値をローカル変数や固定文字列として閉じ込めない

## 5. WebResourceRequestedイベントの多重登録を避ける

即時反映のためにイベントを再登録する場合は、多重登録に注意すること。

避けること:

- 設定保存のたびに `WebResourceRequested += ...` が重複する
- 1回のリクエストに複数回Referer付与処理が走る
- 診断ログの付与回数が異常に増える

推奨:

- イベント登録はWebView2初期化時に1回だけ
- イベント処理内で最新設定を参照する
- どうしても再登録が必要な場合は、先に解除してから登録する

## 6. 診断ログへ即時反映状態を記録する

設定保存後にReferer設定がMainWindowへ即時反映されたことを、診断ログまたは内部状態に記録する。

診断に追加してよい項目:

```text
現在のReferer設定値
現在のReferer付与ON/OFF
設定反映時刻
設定反映状態: 即時反映済み
```

最低限、診断画面で保存後のReferer値が確認できればよい。

## 7. 設定保存後のステータス表示

可能であれば、設定保存後にステータスバーへ短いメッセージを表示する。

例:

```text
設定を保存しました。プレイヤーモードReferer設定を反映しました
```

既存の設定保存メッセージがある場合は、それを邪魔しない形でよい。

## 8. 不正Referer値の扱い

`playerModeReferer` が空、または `http://` / `https://` で始まらない場合は、安全な既定値へフォールバックする。

既定値:

```text
https://litetubedock.local/
```

仕様:

- 不正値で保存してもアプリがクラッシュしない
- 設定画面上も必要に応じて補正後の値が表示される
- 診断ログにも補正後の値が出る

## 9. プレイヤーモード診断との整合性

`ヘルプ` → `プレイヤーモード診断` で表示される情報が、即時反映後の状態と一致すること。

確認対象:

- Referer付与ON/OFF
- Referer値
- 最後のReferer対象URL
- 最後のReferer結果
- Referer付与回数
- 最終エラー

## 10. README更新

READMEに以下を追記・修正する。

- プレイヤーモードRefererの既定値は `https://litetubedock.local/` であること
- この値はLiteTube Dockのアプリ識別用Refererとして使うこと
- 通常は変更不要であること
- 設定画面で変更した場合、保存後はアプリ再起動なしで次回プレイヤーモード遷移から反映されること
- プレイヤーモードは実験的機能であり、Refererを設定しても再生を保証するものではないこと

## 11. ヘルプ更新

`Constants/HelpContent.cs` に以下を追記・修正する。

```text
プレイヤーモードRefererの既定値は https://litetubedock.local/ です。
これはYouTube埋め込みプレイヤーに対して、LiteTube Dockを識別するためのRefererとして使用します。
通常は変更不要です。
設定を保存すると、次回のプレイヤーモード遷移から反映されます。
```

また、引き続き以下を明記する。

```text
プレイヤーモードは実験的機能です。
Error 152 / 153 が出る場合があります。
再生できない場合は通常モードを使用してください。
```

## 12. 実装しないこと

今回のタスクでは以下を実装しない。

- YouTube DOM操作
- YouTube再生ボタンの自動クリック
- YouTube内部API操作
- Googleログイン関連処理
- Cookie/sessionの独自操作
- 動画ごとの埋め込み可否判定API
- 画質固定
- サムネイル取得
- プレイヤーHTMLラッパー生成
- WebView2仮想ホスト名マッピング
- お気に入りごとのReferer設定
- 常時ファイルログ出力
- 通常URLへの自動フォールバックボタン追加

# 受け入れ条件（目視確認基準）

## ビルド確認

- `dotnet build` でビルドエラーがない
- 警告が出る場合は内容を報告すること
- アプリ起動時にクラッシュしない

## 既定値確認

- 新規または既定状態で `プレイヤーモードReferer` が `https://litetubedock.local/` になる
- `settings.json` に `playerModeReferer` として `https://litetubedock.local/` が保存される
- 不正値の場合も `https://litetubedock.local/` へフォールバックする

## 即時反映確認

- アプリを起動する
- 設定画面を開く
- `プレイヤーモードReferer` を変更する
- 保存する
- アプリを再起動せずにプレイヤーモードのお気に入りを開く
- 新しいReferer値が使用される
- 診断画面で新しいReferer値が確認できる

## ON/OFF即時反映確認

- `プレイヤーモードReferer付与` をOFFにして保存する
- アプリ再起動なしでプレイヤーモードのお気に入りを開く
- 診断画面でReferer付与がスキップされたことを確認できる
- 再度ONにして保存する
- アプリ再起動なしでReferer付与が再開される

## 多重登録確認

- 設定保存を複数回行う
- プレイヤーモードを開く
- Referer付与回数が不自然に複数倍にならない
- WebResourceRequestedの処理が重複実行されない

## プレイヤーモード確認

- `https://litetubedock.local/` をRefererにした状態で、プレイヤーモードを開ける
- 再生できる動画ではプレイヤーモードで表示される
- 再生できない動画ではエラー表示になる場合があるが、アプリはクラッシュしない
- 再生できない場合でも通常モードへ戻せる

## README/ヘルプ確認

- READMEに既定Referer値が `https://litetubedock.local/` と記載されている
- READMEに保存後即時反映と記載されている
- ヘルプにも同等の説明がある
- プレイヤーモードが実験的機能である説明が残っている

## 既存機能確認

- 通常モードのお気に入り再生が壊れていない
- 再生モードUIの有効/無効制御が壊れていない
- アドレスバーが壊れていない
- アドレスバーの現在URL同期が壊れていない
- お気に入り右クリック登録が壊れていない
- お気に入りボタンの色・アイコン表示が壊れていない
- リジューム設定が壊れていない
- インポート/エクスポートが壊れていない
- フルスクリーン切り替えが壊れていない
- ウィンドウサイズプリセットが壊れていない
- ウィンドウ位置・サイズリセットが壊れていない
- 設定保存が壊れていない
- YouTubeログイン情報、Google ID、パスワード、認証トークンは保存されていない
