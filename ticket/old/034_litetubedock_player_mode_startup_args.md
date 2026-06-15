# 共通前提（必ず遵守）

- AGENT.md が存在する場合、その内容を最優先で遵守させること
- ユーザーはコードを直接編集しない前提である
- 実装・修正はすべて AIエージェント が行う
- 既存設計を破壊しない
- 既存テーマ・ライブラリ構成を維持する
- ハードコード文字列は禁止
- ICommand構造を崩さない

# タスク名

LiteTubeDock 本体にプレイヤーモード起動引数を追加する

# 目的

LiteTubeDockControl から LiteTubeDock 本体を複数起動した際に、通常の単体利用モードとは別に、動画表示領域を優先した「プレイヤーモード」で起動できるようにする。

プレイヤーモードでは、LiteTubeDock 本体の既存お気に入りボタンや通常設定を壊さず、起動引数で指定された一時URLを表示できるようにする。

また、YouTubeの自動再生、DOM操作、広告操作、内部API呼び出しは行わない。

# 対象ファイル（推定可）

- `App.xaml`
- `App.xaml.cs`
- `MainWindow.xaml`
- `MainWindow.xaml.cs`
- `Models/AppSettings.cs`
- `Services/AppSettingsService.cs`
- 既存のナビゲーション処理・URL移動処理を担当しているファイル
- 必要に応じて新規追加:
  - `Models/StartupOptions.cs`
  - `Services/StartupArgumentService.cs`

# 実装内容（具体的変更指示）

## 1. 起動引数の受け取り

LiteTubeDock 本体で、以下の起動引数を受け取れるようにする。

```text
--player-mode
--url "https://www.youtube.com/..."
```

組み合わせ例:

```text
LiteTubeDock.exe --player-mode
LiteTubeDock.exe --url "https://www.youtube.com/"
LiteTubeDock.exe --player-mode --url "https://www.youtube.com/"
```

WPFアプリの起動時に引数を解析し、アプリ起動後のMainWindowへ渡すこと。

引数解析は専用クラスに分離することが望ましい。

例:

```text
StartupOptions
- IsPlayerMode
- InitialUrl
```

## 2. `--player-mode` の仕様

`--player-mode` が指定された場合、LiteTubeDockをプレイヤーモードで起動する。

プレイヤーモードでは、YouTube表示領域を広くするため、以下のUIを非表示にする。

- メニューバー
- 戻るボタン
- 進むボタン
- 更新ボタン
- Homeボタン
- アドレスバー
- 移動ボタン
- 現在URLをコピー
- お気に入りボタン列
- ステータスバー

ただし、既存設計上まとめて非表示にするのが難しい場合は、以下の優先順位で対応する。

1. お気に入りボタン列
2. アドレスバー周辺
3. ステータスバー
4. メニューバー
5. 戻る/進む/更新/Homeなどのナビゲーションボタン

プレイヤーモード中は、WebView2表示領域を可能な限り広く使うこと。

## 3. 通常起動時の既存挙動維持

起動引数なしで起動した場合は、既存のLiteTubeDockと同じ見た目・同じ挙動を維持すること。

以下を壊さないこと。

- 既存のお気に入りボタン表示
- 既存のURL保存・設定
- 既存のアドレスバー操作
- 既存の現在URLコピー
- 既存の設定画面
- 既存の通常ウィンドウ表示

## 4. `--url` の仕様

`--url` が指定された場合、起動時にそのURLを開く。

例:

```text
LiteTubeDock.exe --url "https://www.youtube.com/"
```

`--player-mode` と併用された場合も、そのURLを開く。

```text
LiteTubeDock.exe --player-mode --url "https://www.youtube.com/"
```

`--url` で渡されたURLは、LiteTubeDockControlから渡される一時的な再生・表示対象として扱う。

以下は禁止。

- `bookmarks.json` への自動登録
- お気に入りボタンへの自動反映
- 既存のお気に入り設定の上書き
- 通常設定ファイルへの不要な保存

## 5. URLバリデーション

`--url` の値が空、または明らかにURLとして不正な場合は、アプリが落ちないようにする。

不正なURLの場合は、以下のいずれかの安全な挙動にする。

- 既定URLを開く
- 何も開かず通常初期画面にする
- ステータス表示が残っている場合はエラー文言を表示する

プレイヤーモードではステータスバーを非表示にする可能性があるため、エラー表示が難しい場合でも例外で落とさないことを優先する。

## 6. 自動再生・YouTube内部操作は禁止

今回の実装では、以下を実装しないこと。

- YouTube動画の自動再生
- 再生ボタンの自動クリック
- 停止ボタンの自動クリック
- ミュートボタンの自動クリック
- 巻き戻し操作
- YouTube DOM操作
- YouTube内部API呼び出し
- 広告スキップ
- 広告非表示
- 広告操作

プレイヤーモードは、あくまで「表示領域を広くした状態で指定URLを開くモード」とする。

## 7. プレイヤーモード状態の内部保持

MainWindow内で、現在がプレイヤーモードかどうかを判定できる状態を保持すること。

例:

```text
_isPlayerMode
StartupOptions.IsPlayerMode
```

将来的にLiteTubeDockControlから操作連携する可能性があるため、プレイヤーモード判定をUI処理に埋め込みすぎないこと。

## 8. LiteTubeDockControl連携を想定した設計

今回の変更は、LiteTubeDockControlから以下のように起動されることを想定する。

```text
LiteTubeDock.exe --player-mode --url "https://www.youtube.com/"
```

ただし、LiteTubeDockControl側の実装は今回の対象外とする。

今回の対象はLiteTubeDock本体側の起動引数対応とプレイヤーモード表示のみ。

## 9. 可能なら簡易ヘルプ引数を追加

余力があれば、以下の引数も追加する。

```text
--help
```

`--help` が指定された場合は、対応引数を確認できる簡易メッセージを表示して終了、または通常起動前にメッセージ表示する。

ただし、必須ではない。

## 10. ビルド確認

実装後、以下を確認すること。

```text
dotnet build
```

警告・エラーがある場合は内容を報告すること。

# 受け入れ条件（目視確認基準）

## 1. 通常起動確認

ユーザーがLiteTubeDockを通常起動する。

OK条件:

- これまで通り通常画面で起動する
- メニュー、アドレスバー、お気に入りボタン、ステータスバーが表示される
- 既存のお気に入りボタンが消えていない
- 既存設定が壊れていない

NG条件:

- 通常起動でもプレイヤーモード表示になる
- お気に入りボタンが消える
- 設定が初期化される
- 起動時に例外で落ちる

## 2. プレイヤーモード起動確認

ユーザーが以下のように起動する。

```text
LiteTubeDock.exe --player-mode
```

OK条件:

- LiteTubeDockが起動する
- 通常時よりWebView2表示領域が広い
- お気に入りボタン列が非表示になる
- アドレスバー周辺が非表示、または通常より簡略化される
- ステータスバーが非表示、または通常より簡略化される
- YouTubeの自動再生は行われない

NG条件:

- アプリが起動しない
- 既存のお気に入り設定が消える
- YouTube動画が勝手に再生される
- 広告操作やDOM操作が行われる

## 3. URL指定起動確認

ユーザーが以下のように起動する。

```text
LiteTubeDock.exe --url "https://www.youtube.com/"
```

OK条件:

- LiteTubeDockが通常画面で起動する
- 指定したURLが開かれる
- お気に入りボタン設定には影響しない
- bookmarks.json が勝手に変更されない

NG条件:

- 指定URLが開かない
- お気に入りボタンにURLが勝手に登録される
- 既存設定が上書きされる
- アプリが落ちる

## 4. プレイヤーモード + URL指定確認

ユーザーが以下のように起動する。

```text
LiteTubeDock.exe --player-mode --url "https://www.youtube.com/"
```

OK条件:

- プレイヤーモードで起動する
- 指定したURLが開かれる
- WebView2表示領域が広い
- お気に入りボタン列が非表示になる
- YouTube動画は自動再生されない

NG条件:

- 通常モードで起動する
- URLが無視される
- YouTubeが自動再生される
- 既存のお気に入り設定が変更される

## 5. 不正URL確認

ユーザーが以下のように起動する。

```text
LiteTubeDock.exe --player-mode --url "not-a-url"
```

OK条件:

- アプリが落ちない
- 安全な初期状態になる
- 可能ならエラーが分かる形で表示される

NG条件:

- 例外でアプリが落ちる
- 設定ファイルが壊れる
- 空白画面のまま復帰不能になる

## 6. LiteTubeDockControl連携前提確認

ユーザーが複数のLiteTubeDockを以下の形式で起動することを想定する。

```text
LiteTubeDock.exe --player-mode --url "https://www.youtube.com/"
```

OK条件:

- 複数起動しても各プロセスが独立して起動する
- 各ウィンドウがプレイヤーモードになる
- LiteTubeDockControl側から整列しやすい通常ウィンドウとして表示される

NG条件:

- 単一インスタンス制御により複数起動できない
- 2個目以降が起動しない
- 既存設定を共有更新して競合する
