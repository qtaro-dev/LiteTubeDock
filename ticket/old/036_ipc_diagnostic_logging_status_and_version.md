# 共通前提（必ず遵守）

- AGENT.md が存在する場合、その内容を最優先で遵守させること
- ユーザーはコードを直接編集しない前提である
- 実装・修正はすべて CODEX が行う
- 既存設計を破壊しない
- 既存テーマ・ライブラリ構成を維持する
- ハードコード文字列は禁止
- ICommand構造を崩さない

# タスク名

LiteTubeDock本体のIPC診断ログ追加・接続状態細分化・バージョン表示修正

# 目的

LiteTubeDockControlから以下の起動引数が実際に渡っていることは確認済みである。

```text
"E:\LiteTubeDock_exe\LiteTubeDock.exe" --player-mode --ipc-enabled
```

しかし、LiteTubeDockControlの「URL操作」タブでは対象プレイヤーが継続して `IPC無効` になる。

ControlとLiteTubeDockを一般ユーザー権限で起動しても改善していないため、設定や起動引数を推測で再変更するのではなく、LiteTubeDock本体側へ診断ログを追加して失敗箇所を特定できるようにする。

併せて、Control側のIPC状態表示を細分化し、現在のように接続失敗をすべて `IPC無効` と表示しないようにする。

また、Controlの詳細一覧に表示されている以下のようなGitコミットハッシュ付きバージョンを、利用者向けの短いアプリバージョンへ修正する。

```text
v1.0.0+eb415547d9fddfab08dd60563fc618acc0f2277d
```

想定表示:

```text
0.1.3
```

# 対象ファイル（推定可）

## LiteTubeDock本体

- `E:\Dev\LiteTubeDock\App.xaml.cs`
- `E:\Dev\LiteTubeDock\MainWindow.xaml.cs`
- `E:\Dev\LiteTubeDock\Models\StartupOptions.cs`
- `E:\Dev\LiteTubeDock\Services\StartupArgumentService.cs`
- `E:\Dev\LiteTubeDock\Services\NamedPipeServerService.cs`
- `E:\Dev\LiteTubeDock\Services\IpcCommandHandler.cs`
- `E:\Dev\LiteTubeDock\Constants\IpcConstants.cs`
- `E:\Dev\LiteTubeDock\Constants\AppConstants.cs`
- `E:\Dev\LiteTubeDock\LiteTubeDock.csproj`

## LiteTubeDockControl側の最小修正

- `E:\Dev\LiteTubeDockControl\MainWindow.xaml`
- `E:\Dev\LiteTubeDockControl\MainWindow.xaml.cs`
- `E:\Dev\LiteTubeDockControl\Models\ManagedDockControlItem.cs`
- `E:\Dev\LiteTubeDockControl\Models\IpcStatusData.cs`
- `E:\Dev\LiteTubeDockControl\Services\ManagedDockControlService.cs`
- `E:\Dev\LiteTubeDockControl\Services\NamedPipeClientService.cs`
- `E:\Dev\LiteTubeDockControl\Services\DockProcessService.cs`
- `E:\Dev\LiteTubeDockControl\Constants\UiTextConstants.cs`

関連処理が別ファイルに存在する場合は、そのファイルも確認すること。

# 実装内容（具体的変更指示）

## 1. LiteTubeDock本体へIPC診断ログを追加する

LiteTubeDock起動時に、IPC関連の診断ログをアプリ専用ログへ出力する。

推奨保存先:

```text
logs/litetubedock_yyyyMMdd.log
```

ログには最低限、以下を記録する。

- 起動日時
- プロセスID
- アプリバージョン
- 受信した起動引数
- `UsePlayerMode` 解析結果
- `UseIpcEnabled` 解析結果
- 起動URLの有無
- 生成したPipe名
- Named Pipe開始処理の呼び出し有無
- Named Pipe開始成功／失敗
- 接続待機開始
- クライアント接続成功
- 受信コマンド名
- コマンド処理成功／失敗
- 接続切断
- サーバー停止
- 例外型
- 例外メッセージ

認証情報・Cookie・WebView2ユーザーデータは記録しない。

## 2. 起動引数の生値と解析結果を記録する

起動直後に、生の起動引数と解析後の設定値を両方記録する。

例:

```text
[Startup]
Arguments: --player-mode --ipc-enabled
UsePlayerMode: True
UseIpcEnabled: True
StartupUrlSpecified: False
ProcessId: 52148
```

`Environment.GetCommandLineArgs()` 等で取得した値と、`StartupOptions` へ解析した後の値を比較できるようにする。

## 3. IPCサーバー開始箇所を明示的に記録する

`UseIpcEnabled == true` の場合、Named Pipeサーバー開始前後を記録する。

成功例:

```text
[IPC]
Start requested.
PipeName: LiteTubeDock_52148
CurrentUserOnly: True
Start result: Success
```

失敗例:

```text
[IPC]
Start requested.
PipeName: LiteTubeDock_52148
Start result: Failed
ExceptionType: UnauthorizedAccessException
Message: ...
```

`UseIpcEnabled == false` の場合:

```text
[IPC]
Start skipped.
Reason: UseIpcEnabled is false.
```

と記録する。

## 4. 起動時例外を握りつぶさない

Named Pipe開始処理で例外が発生した場合、アプリ全体を不必要に終了させない。

ただし例外を無視せず、例外型・メッセージ・発生箇所をログへ記録する。

必要に応じてステータス表示へ以下を出す。

```text
IPCの開始に失敗しました。ログを確認してください。
```

## 5. Pipe名生成を統一する

Pipe名を以下へ統一する。

```text
LiteTubeDock_{PID}
```

例:

```text
LiteTubeDock_52148
```

サーバー側とControl側で別々の形式を生成しない。

## 6. CurrentUserOnlyの状態を確認可能にする

セキュリティレビューで追加された以下がサーバー作成時に使用されていることを記録する。

```text
PipeOptions.CurrentUserOnly
```

サーバー作成時に例外が発生した場合はログへ記録する。

Defender設定変更や管理者権限の強制は行わない。

## 7. 接続待機・切断・タイムアウトを記録する

以下の状態を記録する。

```text
WaitingForConnection
ClientConnected
ReadStarted
CommandReceived
ResponseSent
ClientDisconnected
ConnectionTimedOut
Cancelled
ServerStopped
```

接続待機ループで同一ログを大量出力しない。

## 8. IPCコマンド受信ログを追加する

以下のコマンド名と結果を記録する。

```text
ping
get-status
navigate
play
pause
toggle-mute
seek-to-start
```

受信JSON全文やURL全文を無条件に記録しない。

例:

```text
[IPC]
Command: ping
Result: Success
ResponseProcessId: 52148
```

## 9. get-statusへ診断情報を追加する

既存互換を壊さない範囲で、`get-status` 応答へ以下を追加する。

```text
processId
ipcEnabled
pipeServerStarted
pipeName
appVersion
lastIpcError
```

例:

```json
{
  "processId": 52148,
  "ipcEnabled": true,
  "pipeServerStarted": true,
  "pipeName": "LiteTubeDock_52148",
  "appVersion": "0.1.3",
  "lastIpcError": null
}
```

不要な内部情報は返さない。

## 10. Control側のIPC状態表示を細分化する

現在の `IPC無効` 一択をやめ、少なくとも以下を区別する。

```text
IPC無効
接続待ち
接続済み
接続タイムアウト
アクセス拒否
Pipe未検出
応答不正
PID不一致
プロセス不一致
本体エラー
```

推奨判定:

- `IPC無効`: `--ipc-enabled` なし、または `get-status` で `ipcEnabled == false`
- `Pipe未検出`: 対象プロセスは存在するがPipeへ接続できない
- `接続タイムアウト`: 指定時間内に接続できない
- `アクセス拒否`: `UnauthorizedAccessException`
- `応答不正`: JSON解析失敗、必須項目不足
- `PID不一致`: 応答PIDと対象PIDが異なる
- `接続済み`: pingまたはget-status成功、PID一致、プロセス名一致

## 11. 操作ボタンの有効条件を維持する

以下の場合のみ有効にする。

```text
接続済み
```

対象:

- URL送信
- 再生
- 一時停止
- ミュート
- 先頭へ

その他の状態では無効のままとする。

## 12. Control側へ簡易診断情報を表示する

必要に応じて、選択行のツールチップまたは詳細表示へ以下を出す。

```text
Pipe名
最終接続日時
最終エラー
```

UIを過度に複雑化しない。

## 13. バージョン表示を利用者向け表記へ修正する

Gitハッシュ付き `AssemblyInformationalVersion` をそのまま表示しない。

表示優先順位:

1. LiteTubeDock本体のアプリ定数または製品バージョン
2. `AssemblyInformationalVersion` の `+` 以降を除去した値
3. `AssemblyFileVersion`
4. `AssemblyVersion`

想定表示:

```text
0.1.3
```

## 14. 本体バージョンの定義元を一本化する

バージョン定義が複数箇所にある場合は、既存設計を壊さない範囲で表示元を統一する。

候補:

```text
AppConstants
.csproj の Version
AssemblyInformationalVersion
```

## 15. ログ保存失敗を安全に扱う

ログフォルダ作成・ファイル書き込みに失敗してもLiteTubeDockを終了させない。

可能であれば `Debug.WriteLine` 等へフォールバックする。

## 16. ログ肥大化を防止する

最低限、以下のいずれかを実装する。

- 日単位ログ
- サイズ上限
- 世代数上限
- 古いログ削除

## 17. 実機確認を行う

### 前提

1. LiteTubeDockControlを一般ユーザー権限で起動
2. LiteTubeDockも一般ユーザー権限で起動
3. Control設定でIPC接続をON
4. Controlから1つ起動
5. プレイヤーは起動したまま
6. 動画は再生中でも停止中でもよい

### 確認内容

- 生の起動引数に `--ipc-enabled` がある
- 解析後 `UseIpcEnabled == true`
- Pipe名が `LiteTubeDock_{PID}`
- Pipe開始成功
- Controlからping成功
- get-statusのPID一致
- Control表示が `接続済み`

失敗した場合は、ログに基づいて失敗箇所を報告する。

## 18. 権限差の比較は補助確認とする

必要に応じて以下を比較する。

- 両方一般権限
- 両方管理者権限
- Controlのみ管理者
- LiteTubeDockのみ管理者

通常運用は両方一般権限を前提とする。

片方だけ権限が異なる場合は `アクセス拒否` 等として判別可能にする。

## 19. Defender設定を変更しない

以下を行わない。

- Defender除外登録
- リアルタイム保護無効化
- ファイアウォール無効化
- 管理者権限の強制
- UAC無効化

## 20. ビルド確認

```text
dotnet build E:\Dev\LiteTubeDock\LiteTubeDock.sln
dotnet build E:\Dev\LiteTubeDockControl\LiteTubeDockControl.sln
```

警告0・エラー0を目標とする。

# 受け入れ条件（目視確認基準）

## 1. 本体ログ生成確認

ユーザーが以下を行う。

1. LiteTubeDockControlを一般権限で起動
2. IPC接続ON
3. 1つ起動
4. LiteTubeDockを閉じる

OK条件:

- ログファイルが生成される
- 起動引数が記録される
- `UseIpcEnabled` の解析結果が記録される
- Pipe名が記録される
- Pipe開始成功／失敗が記録される
- 終了時の停止処理が記録される

## 2. 引数解析確認

ログに以下が出ること。

```text
Arguments: --player-mode --ipc-enabled
UsePlayerMode: True
UseIpcEnabled: True
```

`UseIpcEnabled: False` の場合は引数解析不具合として修正する。

## 3. Pipe開始確認

正常時:

```text
PipeName: LiteTubeDock_＜実PID＞
Start result: Success
```

失敗時は例外型とメッセージが記録される。

## 4. Control状態表示確認

IPC状態が以下から適切に選ばれる。

```text
IPC無効
接続待ち
接続済み
接続タイムアウト
アクセス拒否
Pipe未検出
応答不正
PID不一致
プロセス不一致
本体エラー
```

すべてを `IPC無効` と表示しない。

## 5. 接続成功確認

一般ユーザー権限で両アプリを起動する。

OK条件:

- LiteTubeDockログでPipe開始成功
- Controlからping成功
- PID一致
- IPC状態が接続済み
- 操作ボタンが有効

## 6. 操作確認

接続済みの対象Dockで以下を実行する。

- URL送信
- 再生
- 一時停止
- ミュート
- 先頭へ

OK条件:

- 対象Dockだけ反応する
- 本体ログへコマンド名と結果が記録される
- Controlが落ちない

## 7. バージョン表示確認

Controlの詳細一覧で以下のような短い表示になる。

```text
0.1.3
```

以下のような表示はNG。

```text
1.0.0+長いGitハッシュ
```

## 8. 権限不一致確認

可能であれば片方だけ管理者権限で確認する。

OK条件:

- 接続できない場合は `アクセス拒否` 等になる
- `IPC無効` と誤表示しない
- 両方一般権限へ戻すと接続できる

## 9. ログ安全性確認

OK条件:

- Cookieを記録しない
- 認証情報を記録しない
- WebView2ユーザーデータを記録しない
- 巨大JSON全文を記録しない
- ログが無制限に増えない

## 10. 最終報告

CODEXは以下を報告する。

```text
1. IPC失敗の実際の原因
2. 起動引数解析結果
3. Pipe開始結果
4. Control接続結果
5. 修正したファイル
6. バージョン表示の取得元
7. 実行したビルド
8. 実機確認結果
9. ユーザーが確認するログファイルの場所
```

原因を特定できなかった場合も、どの段階まで成功し、どの段階で失敗したかを明記する。
