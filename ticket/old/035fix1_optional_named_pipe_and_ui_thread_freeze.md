# チケット035fix1：Named Pipe待受の任意有効化とUIフリーズ修正

## 対象プロジェクト

- 作業フォルダ：`E:\Dev\LiteTubeDock`
- バックアップフォルダ：`E:\Dev\backup_LiteTubeDock`
- バックアップフォルダは編集しない
- `AGENT.md` が存在する場合は、その内容を最優先する

## 背景

チケット035で、LiteTubeDockControlとの連携用としてNamed Pipe受信機能を追加した。

しかし、Visual StudioからLiteTubeDock v0.1.3を単体起動すると、YouTube動画自体は再生できる一方で、以下の操作が反応しなくなる場合がある。

- ウィンドウ右上の閉じるボタン
- メニュー操作
- その他のWPF UI操作

Named Pipeの接続待受処理がWPFのUIスレッドを占有している可能性がある。

また、通常の単体プレイヤー運用ではNamed Pipe機能は不要であるため、既定では無効にし、LiteTubeDockControlから起動した場合だけ明示的に有効化できるようにする。

## 目的

1. Named Pipe待受処理によってWPFのUIが固まらないようにする
2. 通常の単体起動ではNamed Pipe機能を起動しない
3. LiteTubeDockControlから起動するときだけ、起動引数でNamed Pipe機能を有効化できるようにする
4. 既存のプレイヤーモードおよびURL指定起動を維持する

## 起動仕様

### 通常起動

```text
LiteTubeDock.exe
```

- Named Pipe機能：無効
- 従来どおり通常モードで起動
- メニュー、閉じるボタン、設定画面などを通常操作できる

### プレイヤーモードのみ

```text
LiteTubeDock.exe --player-mode --url "URL"
```

- Named Pipe機能：無効
- 指定URLをプレイヤーモードで開く
- LiteTubeDockControlとのIPC接続待受は開始しない

### LiteTubeDockControl連携用

```text
LiteTubeDock.exe --player-mode --ipc-enabled --url "URL"
```

- Named Pipe機能：有効
- 指定URLをプレイヤーモードで開く
- LiteTubeDockControlからのNamed Pipeコマンドを受信できる

## 実装要件

### 1. 起動引数を追加する

新しい起動引数として以下を追加する。

```text
--ipc-enabled
```

起動引数が指定されている場合のみ、Named Pipeサーバーを開始する。

指定されていない場合は、Named Pipe関連サービスを開始しない。

### 2. 既定値は無効にする

Named Pipe機能は、通常起動および従来のプレイヤーモード起動では無効とする。

以下の起動ではNamed Pipeサーバーを開始しない。

```text
LiteTubeDock.exe
LiteTubeDock.exe --player-mode
LiteTubeDock.exe --player-mode --url "URL"
```

### 3. UIスレッドを停止させない

Named Pipeの接続待受・読み取り処理は、WPFのUIスレッドを占有しない非同期処理として実装する。

禁止例：

```csharp
pipeServer.WaitForConnection();
```

UIスレッド上で同期的な無限待機を行わない。

推奨例：

```csharp
await pipeServer.WaitForConnectionAsync(cancellationToken);
```

Named Pipeサーバー開始処理をMainWindowのコンストラクタやLoadedイベントから呼び出す場合も、画面操作を待たせない形にする。

### 4. UI操作はDispatcher経由にする

Named Pipe受信後に以下を操作する場合は、WPFのDispatcherを使用してUIスレッドへ戻す。

- WebView2のURL変更
- ウィンドウタイトル変更
- 画面上の状態表示
- WPFコントロールの更新

例：

```csharp
await Dispatcher.InvokeAsync(() =>
{
    // WebView2やWPF UIの更新
});
```

### 5. 終了処理を追加する

LiteTubeDock終了時に、Named Pipeサーバーの待受処理を安全に停止する。

- `CancellationTokenSource` 等で待受処理をキャンセルする
- 終了時に未処理例外を出さない
- Pipe待受中でもウィンドウを正常に閉じられる
- Named Pipe無効時は終了処理で例外を出さない

### 6. 既存のPipe名・通信仕様を維持する

チケット035で実装済みの以下は、原則として変更しない。

- Pipe名
- PID単位の識別方法
- IPCコマンド形式
- IPCレスポンス形式
- URL取得・URL変更など既存コマンド
- LiteTubeDockControlとの互換性

必要な変更は、サーバーの開始条件と非同期化・終了処理に限定する。

### 7. ヘルプ表示を更新する

`--help` の起動引数一覧に以下を追加する。

```text
--ipc-enabled
    LiteTubeDockControlとの連携用Named Pipe受信機能を有効にします。
```

使用例も追記する。

```text
LiteTubeDock.exe --player-mode --ipc-enabled --url "https://www.youtube.com/"
```

## 想定変更ファイル

実際の構成を確認したうえで必要なファイルだけ変更する。

候補：

- `App.xaml.cs`
- `MainWindow.xaml.cs`
- `Services/StartupArgumentService.cs`
- `Services/NamedPipeServerService.cs`
- `Services/IpcCommandHandler.cs`
- `Constants/IpcConstants.cs`
- `Constants/HelpContent.cs`
- `README.md`

既存設計に同等のファイルがある場合は、その構成に合わせる。

## 受け入れ条件

### 確認1：通常起動

1. ユーザーが `LiteTubeDock.exe` を通常起動する
2. YouTubeページを表示する
3. メニューを開く
4. 設定画面を開く
5. ウィンドウ右上の閉じるボタンを押す

期待結果：

- すべて正常に操作できる
- Named Pipe待受は開始されない
- アプリが固まらない
- 正常終了できる

### 確認2：プレイヤーモードのみ

1. 以下で起動する

```text
LiteTubeDock.exe --player-mode --url "https://www.youtube.com/"
```

2. 指定URLが表示されることを確認する
3. ウィンドウを閉じる

期待結果：

- プレイヤーモードで起動する
- Named Pipe待受は開始されない
- ウィンドウを正常に閉じられる
- UIフリーズしない

### 確認3：IPC有効起動

1. 以下で起動する

```text
LiteTubeDock.exe --player-mode --ipc-enabled --url "https://www.youtube.com/"
```

2. LiteTubeDockControlまたは既存のIPC確認手段から接続する
3. 現在URL取得コマンドを送る
4. URL変更コマンドを送る
5. ウィンドウを閉じる

期待結果：

- PID単位のNamed Pipeへ接続できる
- 現在URLを取得できる
- URL変更が反映される
- コマンド待受中もUIが固まらない
- ウィンドウを正常に閉じられる

### 確認4：IPC接続が来ない場合

1. `--ipc-enabled` を付けて起動する
2. LiteTubeDockControlを起動せず、そのまま操作する
3. メニュー操作やウィンドウ移動を行う
4. 閉じるボタンを押す

期待結果：

- 接続待ちのままでもUIを操作できる
- アプリが応答なしにならない
- 正常終了できる

### 確認5：複数起動

1. `--ipc-enabled` を付けてLiteTubeDockを3つ起動する
2. 各プロセスのPIDを確認する
3. 各PID用のPipeへ個別に接続する
4. それぞれ異なるURLを送る

期待結果：

- 各DockをPID単位で識別できる
- 対象外のDockへコマンドが送られない
- 3つともUIが固まらない
- すべて正常終了できる

## 非対応・禁止事項

- 通常起動時にNamed Pipeサーバーを自動開始しない
- UIスレッド上で同期的な接続待ちを行わない
- YouTubeページのDOM操作を行わない
- 再生ボタンの自動クリックを行わない
- 広告の非表示・削除・スキップ処理を追加しない
- 既存のPipe通信形式を理由なく変更しない

## 完了条件

以下をすべて満たしたら完了とする。

- 通常起動ではNamed Pipe機能が無効
- `--ipc-enabled` 指定時のみNamed Pipe機能が有効
- Named Pipe接続待ち中もWPF UIが操作できる
- 閉じるボタンで正常終了できる
- LiteTubeDockControlから既存IPCコマンドを送受信できる
- 既存の通常モード・プレイヤーモード・URL指定起動が壊れていない
- ビルドが成功する
