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

チケット013fix3 プレイヤーモードReferer設定と診断ログ追加

# 目的

チケット013fix2で、プレイヤーモードの Error 153 対策として、WebView2 の `WebResourceRequested` を利用し、YouTube embed 関連リクエストへ `Referer: https://www.youtube.com/` の付与を試みる処理を追加した。

しかし、現在の実装では以下が分からない。

- 本当に Referer が付与されているか
- どのURLに Referer が付与されたか
- Referer 付与が成功したか、例外が発生したか
- 現在の Referer 値が適切か
- Error 153 / Error 152 系の原因が Referer なのか、それ以外なのか

そのため、プレイヤーモード用の実験設定として、以下を追加する。

- Referer付与ON/OFF
- Referer値の入力欄
- プレイヤーモード診断ログ
- 最後に生成したプレイヤーモードURLの記録
- 最後にReferer付与を試みたURLと結果の記録

これにより、プレイヤーモードが失敗した時に、何が起きているかをユーザーが確認しやすくする。

# 対象ファイル（推定可）

- `MainWindow.xaml.cs`
- `Views/GeneralSettingsView.xaml`
- `Views/GeneralSettingsView.xaml.cs`
- `SettingsWindow.xaml.cs`
- `Models/AppSettings.cs`
- `Services/SettingsService.cs`
- `Services/FavoritePlaybackUrlService.cs`
- `Constants/AppConstants.cs`
- `Constants/HelpContent.cs`
- `data/settings.json`
- `README.md`

必要に応じて、診断ログ用の小さなサービスやモデルを追加してよい。

# 実装内容（具体的変更指示）

## 1. 作業開始前の確認

- 必ず `AGENT.md` を読むこと。
- 作業対象は `E:\Dev\LiteTubeDock` 配下のみとすること。
- `E:\Dev\LiteTubeDock` の外側にあるファイルやフォルダは一切編集しないこと。
- 既存UIの `x:Name` は原則変更しないこと。
- MainWindow、SettingsWindow、UserControl構成を壊さないこと。
- 既存の設定保存・読み込み・キャンセル・初期値リセット処理を壊さないこと。
- YouTubeのUI改変やDOM操作による自動クリックは行わないこと。

## 2. プレイヤーモードReferer付与ON/OFFを追加する

基本設定画面に、プレイヤーモード用のReferer付与ON/OFFを追加する。

表示名:

```text
プレイヤーモードReferer付与
```

推奨 `x:Name`:

```text
PlayerModeRefererEnabledCheckBox
```

既定値:

```text
true
```

理由:

- 現在の013fix2の挙動を維持するため
- 既にReferer付与を試みる実装が入っているため

## 3. プレイヤーモードReferer値入力欄を追加する

基本設定画面に、Referer値を入力できる欄を追加する。

表示名:

```text
プレイヤーモードReferer
```

推奨 `x:Name`:

```text
PlayerModeRefererTextBox
```

既定値:

```text
https://www.youtube.com/
```

仕様:

- 空の場合は既定値 `https://www.youtube.com/` を使用する
- `http://` または `https://` で始まらない場合は不正扱い、または保存時に既定値へ戻す
- 不正なReferer値でアプリがクラッシュしないこと
- まずは全体設定として扱う
- お気に入りごとのReferer指定は実装しない

## 4. AppSettingsへ項目を追加する

`Models/AppSettings.cs` に以下を追加する。

推奨プロパティ:

```csharp
public bool PlayerModeRefererEnabled { get; set; }
public string PlayerModeReferer { get; set; }
```

JSON例:

```json
{
  "playerModeRefererEnabled": true,
  "playerModeReferer": "https://www.youtube.com/"
}
```

既定値:

```text
playerModeRefererEnabled = true
playerModeReferer = https://www.youtube.com/
```

## 5. settings.jsonへの保存・復元

`SettingsService` で追加項目を保存・復元する。

仕様:

- 初回起動時、未指定なら既定値を使用する
- 設定画面で変更して保存できる
- 再起動後も値が復元される
- 不正値は安全な既定値へフォールバックする

## 6. Referer付与処理に設定値を反映する

チケット013fix2で実装した `WebResourceRequested` のReferer付与処理を、設定値に従うように変更する。

仕様:

- `playerModeRefererEnabled = true`
  - YouTube embed関連リクエストに Referer を付与する
  - Referer値は `playerModeReferer` を使用する

- `playerModeRefererEnabled = false`
  - Refererを付与しない
  - WebResourceRequested自体は残してもよいが、ヘッダー変更は行わない

## 7. Referer付与対象は限定する

Referer付与対象は引き続き限定する。

対象候補:

- `https://www.youtube.com/embed/...`
- `https://www.youtube.com/iframe_api`
- `https://www.youtube.com/s/player/...`

避けること:

- YouTube以外のURL
- Googleログイン関連URL
- 認証情報やCookie/sessionの独自処理
- 全Webリクエストへの無差別Referer付与

## 8. プレイヤーモード診断ログを追加する

プレイヤーモード関連の診断ログをアプリ内で確認できるようにする。

最初は簡易ログでよい。

記録したい内容:

- 最後に生成したプレイヤーモードURL
- 最後に通常モードへフォールバックしたURLがあればそのURL
- Referer付与ON/OFF
- 使用したReferer値
- 最後にReferer付与を試みたリクエストURL
- Referer付与成功/スキップ/失敗
- 失敗時の簡単な例外メッセージ
- Referer付与回数

保存先はまずメモリ上でよい。  
永続ログファイルは必須ではない。

## 9. 診断ログ表示UI

診断ログを表示する方法を追加する。

推奨案:

`ヘルプ` メニュー配下に以下を追加する。

```text
プレイヤーモード診断
```

推奨 `x:Name`:

```text
PlayerModeDiagnosticsMenuItem
```

押下時に、簡易ウィンドウまたはメッセージボックスで診断情報を表示する。

簡易ウィンドウを作る場合の推奨ファイル:

```text
PlayerModeDiagnosticsWindow.xaml
PlayerModeDiagnosticsWindow.xaml.cs
```

ただし、最初は `MessageBox` でもよい。

表示内容例:

```text
プレイヤーモード診断

Referer付与: ON
Referer値: https://www.youtube.com/
最後のプレイヤーURL: https://www.youtube.com/embed/xxxx
最後のReferer対象URL: https://www.youtube.com/embed/xxxx
最後のReferer結果: 付与成功
Referer付与回数: 3
最終エラー: -
```

## 10. ステータスバーへの簡易表示

可能であれば、プレイヤーモードでReferer付与を試みた時にステータスバーへ短い状態を表示する。

例:

```text
状態: プレイヤーモードReferer付与を試行しました
```

ただし、頻繁に表示が変わりすぎる場合は不要。

診断ウィンドウで確認できればよい。

## 11. プレイヤーモードURL生成結果を記録する

`FavoritePlaybackUrlService` などでプレイヤーモードURLを生成した時、診断ログへ記録する。

記録項目:

- 元URL
- 生成後URL
- playbackMode
- autoplay / mute / loop / resumePlayback の値
- 動画ID抽出成功/失敗

実装が大きくなる場合は、まず以下だけでよい。

- 元URL
- 生成後URL
- 動画ID抽出成功/失敗

## 12. UI配置

基本設定画面では、以下のような配置を推奨する。

```text
自動再生 [ ]

プレイヤーモードReferer付与 [✓]
プレイヤーモードReferer [ https://www.youtube.com/________ ]
```

場所:

- 自動再生の近く
- または基本設定下部の詳細設定エリア

注意:

- 通常ユーザー向けにはやや高度な設定なので、表示名は分かりやすくする
- READMEとヘルプで「実験的設定」と明記する

## 13. README更新

READMEに以下を追記する。

- プレイヤーモードは実験的機能であること
- Error 153 / Error 152 系が出る場合があること
- Referer付与ON/OFFを設定できること
- Referer値を設定できること
- 既定値は `https://www.youtube.com/`
- 診断メニューで、最後に付与を試みたURLやReferer値を確認できること
- Refererを変更しても再生を保証するものではないこと
- 再生できない場合は通常モードを使うこと

## 14. ヘルプ更新

`Constants/HelpContent.cs` にもREADMEと同等の説明を追記する。

特に以下を明記する。

```text
プレイヤーモードは実験的機能です。
Error 153 / Error 152 が出る場合があります。
Referer設定は回避を試みるための実験設定であり、完全保証ではありません。
再生できない場合は通常モードを使用してください。
```

## 15. 実装しないこと

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

## 基本設定確認

- `設定` → `設定を開く` → `基本設定` を表示する
- `プレイヤーモードReferer付与` チェックボックスが表示される
- `プレイヤーモードReferer` 入力欄が表示される
- 既定値が `https://www.youtube.com/` になっている
- ON/OFFを変更して保存できる
- Referer値を変更して保存できる
- 再起動後も設定が復元される
- `settings.json` に `playerModeRefererEnabled` と `playerModeReferer` が保存される

## Referer付与確認

- `playerModeRefererEnabled = true` の状態でプレイヤーモードのお気に入りを開く
- YouTube embed関連URLにReferer付与を試みる
- 診断ログにReferer付与対象URLとReferer値が表示される
- `playerModeRefererEnabled = false` の状態ではReferer付与を行わない
- Referer付与OFFでもアプリがクラッシュしない

## 診断ログ確認

- `ヘルプ` メニュー配下に `プレイヤーモード診断` が表示される
- 診断を開くと以下が確認できる
  - Referer付与ON/OFF
  - Referer値
  - 最後のプレイヤーURL
  - 最後にReferer付与を試みたURL
  - 最後のReferer結果
  - Referer付与回数
  - 最終エラーがあればその内容
- プレイヤーモードを使った後、診断内容が更新される

## プレイヤーモード確認

- プレイヤーモードのお気に入りを開いてもアプリがクラッシュしない
- Error 153 / Error 152 が出る場合があるが、診断ログでReferer付与状況が確認できる
- 再生できない場合でも通常モードへ戻せる

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
