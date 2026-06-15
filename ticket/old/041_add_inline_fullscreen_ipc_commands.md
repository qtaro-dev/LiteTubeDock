# 共通前提（必ず遵守）

- AGENT.md が存在する場合、その内容を最優先で遵守させること
- ユーザーはコードを直接編集しない前提である
- 実装・修正はすべて CODEX が行う
- 既存設計を破壊しない
- 既存テーマ・ライブラリ構成を維持する
- ハードコード文字列は禁止
- ICommand構造を崩さない

# タスク名

041：IPCからYouTube動画を枠内全画面へ切り替える機能を追加する

# 目的

LiteTubeDockを通常のウィンドウサイズ・位置のまま維持し、WebView2内のYouTube動画プレイヤーだけをウィンドウ枠いっぱいへ表示する「枠内全画面」機能をIPCから操作できるようにする。

この機能は、YouTubeプレイヤー右下にある全画面ボタンを手動で押したときと同等の動作を行う。

以下の動作は対象外とする。

- Windowsウィンドウ自体をモニター全体へ最大化する
- LiteTubeDockの位置やサイズを変更する
- Alt+Enterによるアプリ全体のフルスクリーンへ切り替える

# 対象ファイル（推定可）

- `E:\Dev\LiteTubeDock\MainWindow.xaml.cs`
- `E:\Dev\LiteTubeDock\App.xaml.cs`
- `E:\Dev\LiteTubeDock\Constants\AppConstants.cs`
- `E:\Dev\LiteTubeDock\LiteTubeDock.csproj`
- IPCコマンド定義を管理しているファイル
- IPC受信・応答処理を担当しているファイル
- WebView2へJavaScriptを実行している既存ファイル
- `get-status` 応答モデルを定義しているファイル
- 診断ログを出力しているファイル

# 実装内容（具体的変更指示）

1. LiteTubeDock本体だけを修正すること。
2. LiteTubeDockControl側は変更しないこと。
3. IPCへ `enter-inline-fullscreen`、`exit-inline-fullscreen`、`toggle-inline-fullscreen` を追加すること。
4. 機能名・ログ名・説明文では「枠内全画面」を使用すること。
5. `enter-inline-fullscreen` は、WebView2内のYouTube動画プレイヤーを、現在のLiteTubeDockウィンドウ枠内いっぱいに表示すること。
6. `exit-inline-fullscreen` は、枠内全画面を解除し、YouTubeページの通常表示へ戻すこと。
7. `toggle-inline-fullscreen` は、現在の状態に応じて枠内全画面の開始・解除を切り替えること。
8. Windowsウィンドウの位置・幅・高さ・最前面状態は変更しないこと。
9. LiteTubeDockのアプリ全体フルスクリーン処理やAlt+Enter処理は呼び出さないこと。
10. 既存のYouTubeプレイヤー右下の全画面ボタンを押した場合と同等の状態にすること。
11. 実装方法は、YouTubeプレイヤー内の全画面ボタン相当操作、Fullscreen API、または既存のYouTubeプレイヤーDOMに対する操作を検討し、現在のWebView2構成で最も安定する方法を使用すること。
12. DOMセレクターを1種類だけに依存せず、YouTube通常動画・ライブ・プレイリスト等でボタン構造が異なる可能性を考慮すること。
13. YouTube側のDOM変更で対象ボタンが見つからない場合は、例外終了せず、明確なErrorCodeを返すこと。
14. YouTube以外のページでは、対応外として安全に失敗させること。
15. Shortsについては、安定して操作できる場合のみ対応し、不安定な場合は対応外であることをErrorCodeとログで明示すること。
16. 枠内全画面への切替中も、タイトルバー非表示、外枠非表示、移動、リサイズ、IPC操作を破壊しないこと。
17. 枠内全画面の開始・解除によって、URL、再生位置、再生状態、ミュート状態を不必要に変更しないこと。
18. 一時停止中に枠内全画面へ切り替えた場合は、一時停止状態を維持すること。
19. `get-status` 応答へ `IsInlineFullscreen` 相当の状態を追加すること。
20. 状態判定は送信履歴ではなく、WebView2内の実際のFullscreen状態またはYouTubeプレイヤー状態を確認して返すこと。
21. IPC応答へ Result、ErrorCode、IsInlineFullscreen、CurrentUrl、Duration、Message を含めること。
22. 必要に応じて `INLINE_FULLSCREEN_NOT_SUPPORTED`、`INLINE_FULLSCREEN_BUTTON_NOT_FOUND`、`INLINE_FULLSCREEN_REQUEST_FAILED`、`INLINE_FULLSCREEN_EXIT_FAILED`、`INLINE_FULLSCREEN_STATE_UNKNOWN`、`WEBVIEW_NOT_READY`、`YOUTUBE_PAGE_NOT_DETECTED` を追加すること。
23. 同じ状態への重複コマンドは安全に処理すること。
24. 手動で開始・解除された場合も `get-status` が正しい状態を返すこと。
25. 診断ログへ PID、PipeName、Command、CurrentUrl、WebViewReady、YouTubeDetected、InlineFullscreenBefore、InlineFullscreenAfter、DOM操作結果、Fullscreen API結果、ErrorCode、Duration を記録すること。
26. 既存IPCコマンドとの互換性を維持すること。
27. `Constants/AppConstants.cs` の `AppVersion` と `LiteTubeDock.csproj` の Version / AssemblyVersion / FileVersion / InformationalVersion が一致しているか確認すること。
28. アプリ内表示、ログ、exe/dllプロパティで確認できるバージョンが一致すること。
29. Debugビルドを実行し、警告0・エラー0を確認すること。

# 受け入れ条件（目視確認基準）

## 目視確認前提

- LiteTubeDockの修正版一式を同じビルド成果物で配置する
- exeだけでなくdll、deps.json、runtimeconfig.jsonも同じビルドのものを使用する
- 古いファイルを混在させない
- LiteTubeDockを `--player-mode --ipc-enabled` 付きで起動する
- YouTube通常動画を表示する
- LiteTubeDockウィンドウの位置とサイズを記録しておく

## 枠内全画面開始確認

1. テスターがIPCで `enter-inline-fullscreen` を送信する。
2. LiteTubeDockウィンドウの位置・幅・高さが変わらないこと。
3. YouTubeページ上部の検索欄や動画下部の説明欄が隠れ、動画プレイヤーだけが枠内いっぱいに表示されること。
4. モニター全体を覆うアプリ全体フルスクリーンにならないこと。
5. 他のLiteTubeDockウィンドウを覆わないこと。
6. `get-status` で `IsInlineFullscreen=True` 相当が返ること。

## 枠内全画面解除確認

1. テスターがIPCで `exit-inline-fullscreen` を送信する。
2. YouTubeページの通常表示へ戻ること。
3. LiteTubeDockウィンドウの位置とサイズが変わらないこと。
4. `get-status` で `IsInlineFullscreen=False` 相当が返ること。

## 切替確認

1. `toggle-inline-fullscreen` を送信する。
2. 通常表示から枠内全画面へ切り替わること。
3. 再度送信し、通常表示へ戻ること。

## 再生状態確認

1. 動画を一時停止する。
2. 枠内全画面へ切り替える。
3. 動画が勝手に再生されないこと。
4. 解除後も一時停止状態が維持されること。
5. 再生中でも再生位置が不必要に巻き戻らないこと。

## 手動操作との同期確認

1. YouTubeプレイヤー右下の全画面ボタンを手動で押す。
2. `get-status` を実行する。
3. 枠内全画面中として検出されること。
4. 手動解除後、再度 `get-status` を実行する。
5. 枠内全画面解除済みとして検出されること。

## 複数Dock確認

1. LiteTubeDockを4台起動する。
2. Dock 2へだけ `enter-inline-fullscreen` を送信する。
3. Dock 2だけが枠内全画面になること。
4. Dock 1、3、4の表示状態が変わらないこと。
5. Dock 2のウィンドウ位置・サイズが変わらないこと。

## 非対応ページ確認

1. YouTube以外のページを表示する。
2. `enter-inline-fullscreen` を送信する。
3. アプリが異常終了しないこと。
4. 対応外を示すErrorCodeが返ること。

## ログ確認

- 枠内全画面開始前後の状態、DOM操作またはFullscreen APIの結果、ErrorCode、処理時間を確認できること。

## バージョン確認

- アプリ内表示、exe、dllのバージョンが一致すること。
- `AppConstants.AppVersion` とcsprojの各バージョン定義が一致すること。

## ビルド確認

- LiteTubeDockのDebugビルドが成功すること
- 警告0件であること
- エラー0件であること
