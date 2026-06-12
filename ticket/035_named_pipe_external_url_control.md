# 共通前提（必ず遵守）

- AGENT.md が存在する場合、その内容を最優先で遵守させること
- ユーザーはコードを直接編集しない前提である
- 実装・修正はすべて CODEX が行う
- 既存設計を破壊しない
- 既存テーマ・ライブラリ構成を維持する
- ハードコード文字列は禁止
- ICommand構造を崩さない

# タスク名

LiteTubeDock 本体に外部URL受信用Named Pipe連携を追加する

# 目的

LiteTubeDockControl から、すでに起動中の特定LiteTubeDockプロセスへURLを送信し、そのLiteTubeDockのWebView2を指定URLへ遷移できるようにする。

現在は `--url` 起動引数による起動時URL指定のみ対応しているため、起動済みプレイヤーのURLを後から変更できない。

本チケットでは、各LiteTubeDockプロセスが自身専用のNamed Pipe受信口を持ち、LiteTubeDockControlからPID単位で以下を実行できる基盤を追加する。

- 接続確認
- 指定URLへの遷移
- 簡易状態取得

再生・停止・ミュート・巻き戻しなどのYouTubeページ内部操作は、本チケットでは実装しない。

# 対象ファイル（推定可）

- `App.xaml.cs`
- `MainWindow.xaml.cs`
- `Models/StartupOptions.cs`
- `Services/StartupArgumentService.cs`
- 既存のWebView2ナビゲーション処理を担当するファイル
- `Constants/AppConstants.cs`
- 必要に応じて新規追加:
  - `Models/IpcCommand.cs`
  - `Models/IpcResponse.cs`
  - `Services/NamedPipeServerService.cs`
  - `Services/IpcCommandHandler.cs`
  - `Constants/IpcConstants.cs`

# 実装内容（具体的変更指示）

## 1. プロセスごとのNamed Pipe受信口を作成する

LiteTubeDock起動後、各プロセスが自身専用のNamed Pipeサーバーを開始する。

Pipe名はPIDを含む形式にする。

```text
LiteTubeDock_{ProcessId}
```

同時に複数のLiteTubeDockを起動しても、Pipe名が衝突しないこと。

## 2. Named Pipeサーバーをバックグラウンドで動作させる

- 非同期で待受する
- UIスレッドをブロックしない
- アプリ終了時に安全に停止する
- 接続切断後も次の接続を待ち受ける
- クライアント未接続でも通常動作する
- Pipe処理例外でアプリ全体を落とさない
- CancellationTokenとPipeを適切に解放する

## 3. JSON形式のコマンドを受信する

初期対応コマンド:

```text
ping
navigate
get-status
```

例:

```json
{
  "command": "navigate",
  "url": "https://www.youtube.com/"
}
```

推奨モデル:

```text
IpcCommand
- Command
- Url
- RequestId
```

## 4. `ping` コマンドを実装する

応答例:

```json
{
  "success": true,
  "command": "ping",
  "processId": 12345,
  "message": "pong"
}
```

## 5. `navigate` コマンドを実装する

処理:

1. コマンド名を確認
2. URLを検証
3. Dispatcher経由でUIスレッドへ切り替え
4. 既存のURL遷移処理を再利用
5. WebView2で指定URLを開く
6. 成否をJSON応答

Named Pipe経由URLは一時指定として扱い、以下を行わない。

- `LastUrl` へ自動保存
- `bookmarks.json` への登録
- お気に入りボタンへの反映
- 既存設定の上書き

## 6. URL検証

許可形式:

```text
http://
https://
```

空URL、不正URL、相対URLは拒否し、失敗応答を返す。

## 7. `get-status` コマンドを実装する

返却候補:

```text
ProcessId
WindowTitle
IsPlayerMode
CurrentUrl
IsWebViewReady
AppVersion
```

取得不能項目は空文字または安全な既定値にする。

## 8. WebView2初期化前のコマンドを安全に処理する

- WebView2準備完了まで短時間待機
- または保留URLとして保持し初期化後に適用
- 一定時間で準備できなければ失敗応答
- UIスレッドを長時間ブロックしない

## 9. 応答形式を統一する

推奨モデル:

```text
IpcResponse
- Success
- Command
- ProcessId
- Message
- ErrorCode
- Data
```

未対応コマンドは安全に拒否する。

## 10. 1接続1コマンド方式

初期実装は、1回接続して1コマンド送信し、1応答を返して切断する方式でよい。

将来の常時接続へ拡張しやすい構造にする。

## 11. 通常モード・プレイヤーモード両対応

Named Pipe受信機能は以下の両方で利用可能にする。

```text
通常起動
--player-mode 起動
```

## 12. セキュリティと入力制限

- ローカルPC内のNamed Pipe通信のみ
- 受信JSONサイズに上限を設ける
- 極端に長いURLを拒否する
- 不正JSONで落ちない
- コマンド名は許可リスト方式
- ネットワーク通信は行わない

## 13. ログまたはデバッグ出力

以下を記録する。

- Pipeサーバー開始・停止
- 接続受付
- 受信コマンド
- 成功・失敗
- 例外内容

URLに認証情報が含まれる可能性を考慮し、ログ出力には注意する。

## 14. アプリ終了時にPipeサーバーを停止する

- CancellationTokenをキャンセル
- PipeをDispose
- バックグラウンドタスクを残さない
- 終了を遅延させない
- 停止失敗で終了不能にしない

## 15. READMEとヘルプへ追記する

記載内容:

```text
Pipe名: LiteTubeDock_{PID}
対応コマンド: ping / navigate / get-status
```

通常利用では意識不要な内部連携機能であることも記載する。

## 16. 既存機能を壊さない

- 通常起動
- プレイヤーモード
- `--url`
- `--help`
- 複数起動
- WebView2表示
- アドレスバー
- お気に入り
- 設定画面
- ショートカット
- `LastUrl`
- `settings.json`
- `bookmarks.json`

## 17. 本チケットで実装しないこと

- 再生
- 一時停止
- 停止
- ミュート
- 巻き戻し
- 音量変更
- YouTube DOM操作
- YouTube内部API呼び出し
- JavaScriptによる動画制御
- 広告スキップ
- 広告非表示
- 広告操作
- LiteTubeDockControl側の変更
- Control終了時の自動終了
- 常時接続IPC
- 認証機能

## 18. ビルド確認

```text
dotnet build LiteTubeDock.sln
```

警告・エラーがある場合は報告する。

# 受け入れ条件（目視確認基準）

## 1. 通常起動確認

OK:

- 従来どおり起動
- UIが崩れない
- WebView2表示
- Pipe未接続でも通常利用可能
- クラッシュしない

NG:

- Pipe待受で起動停止
- UIフリーズ
- 通常機能が使えない

## 2. Pipe名確認

OK:

- `LiteTubeDock_{PID}` が作成される
- 複数起動時にPIDごとに別Pipe
- Pipe名が衝突しない

NG:

- 複数起動でエラー
- 全プロセスが同じPipe名
- 起動時例外

## 3. ping確認

送信:

```json
{
  "command": "ping"
}
```

OK:

- `success: true`
- `message: "pong"`
- 対象PIDが返る
- アプリが固まらない

## 4. navigate確認

送信:

```json
{
  "command": "navigate",
  "url": "https://www.youtube.com/"
}
```

OK:

- 起動済み対象プロセスだけ指定URLへ移動
- 新規プロセスは起動しない
- `bookmarks.json` は変更されない
- `LastUrl` は自動保存されない
- 成功応答が返る

NG:

- 別プロセスが移動
- 設定やお気に入りが変わる
- アプリが落ちる

## 5. 複数プロセス指定確認

2つのLiteTubeDockを起動し、それぞれ異なるPIDのPipeへ異なるURLを送る。

OK:

- PID AだけURL Aへ移動
- PID BだけURL Bへ移動
- 相互干渉なし
- 両方から成功応答

## 6. get-status確認

送信:

```json
{
  "command": "get-status"
}
```

OK:

- PID
- プレイヤーモード状態
- 現在URL
- WebView2準備状態
- バージョン

が返る。取得不能項目があっても応答全体は返る。

## 7. 不正URL確認

送信:

```json
{
  "command": "navigate",
  "url": "not-a-url"
}
```

OK:

- URL遷移しない
- `success: false`
- エラーメッセージ
- アプリ継続利用可能

## 8. 不正JSON確認

OK:

- エラー応答または安全に切断
- アプリは落ちない
- 次の正常接続を受け付ける

## 9. WebView2初期化前確認

起動直後に `navigate` を送る。

OK:

- 初期化後に適用、または安全な失敗応答
- UIフリーズなし
- クラッシュなし

## 10. 終了確認

Pipe待受中にLiteTubeDockを終了する。

OK:

- 通常速度で終了
- Pipe停止
- プロセス残留なし
- 次回起動可能

## 11. 回帰確認

以下が従来どおり動けばOK。

- 通常起動
- `--player-mode`
- `--url`
- 複数起動
- お気に入り
- アドレスバー
- 設定保存
- ショートカット
- 終了
