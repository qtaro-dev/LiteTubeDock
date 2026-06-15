# 共通前提（必ず遵守）

- AGENT.md が存在する場合、その内容を最優先で遵守させること
- ユーザーはコードを直接編集しない前提である
- 実装・修正はすべて CODEX が行う
- 既存設計を破壊しない
- 既存テーマ・ライブラリ構成を維持する
- ハードコード文字列は禁止
- ICommand構造を崩さない

# タスク名

LiteTubeDock側IPCメディア操作診断ログ強化とMEDIA_NOT_FOUND調査改善

# 目的

LiteTubeDockControlから `play`、`pause`、`toggle-mute`、`seek-to-start` 等のIPCコマンドを送信した際、LiteTubeDock側で `MEDIA_NOT_FOUND` が断続的に返る問題を調査・改善する。

現在のログでは複数プレイヤーが同一ログファイルへ記録するため、どのPID・Pipe・URL・DOM状態で失敗したか判別しにくい。

各操作について、対象プロセス・Pipe名・現在URL・ページ読み込み状態・video/audio要素数・実行結果を記録し、実際に操作可能なメディア要素をより確実に検出できるようにする。

# 対象ファイル（推定可）

- `E:\Dev\LiteTubeDock\MainWindow.xaml.cs`
- `E:\Dev\LiteTubeDock\Services\DiagnosticLogService.cs`
- `E:\Dev\LiteTubeDock\Services\NamedPipeServerService.cs`
- `E:\Dev\LiteTubeDock\Services\MediaControlService.cs`
- `E:\Dev\LiteTubeDock\Constants\AppConstants.cs`
- `E:\Dev\LiteTubeDock\Models\StartupOptions.cs`
- その他、IPCコマンド受信、WebView2 JavaScript実行、メディア要素検出、診断ログに関係するファイル

# 実装内容（具体的変更指示）

1. IPCメディア操作ログへ、少なくとも以下を追加すること。

   - ProcessId
   - PipeName
   - Command
   - CurrentUrl
   - WebView2初期化完了状態
   - CoreWebView2生成済みか
   - NavigationCompleted済みか
   - `document.readyState`
   - `video` 要素数
   - `audio` 要素数
   - `iframe` 要素数
   - 操作対象として選択した要素種別
   - 実行結果
   - ErrorCode
   - JavaScript例外またはWebView2例外
   - 処理時間

2. 各ログ行だけで対象プロセスを判別できるよう、PIDとPipe名を必ず含めること。

3. 複数のLiteTubeDockが同一ログファイルへ書き込む場合でも、ログ行が混ざって判別不能にならない形式にすること。

4. ログ例は以下の情報量を満たすこと。

   ```text
   [IPC] PID=39780; Pipe=LiteTubeDock_39780; Command=toggle-mute; Url=https://www.youtube.com/watch?...; ReadyState=complete; VideoCount=1; AudioCount=0; IframeCount=2; Result=Success; DurationMs=18
   ```

5. 現在の単純な `document.querySelector('video, audio')` 相当の検出処理がある場合は、実際に操作可能なメディア要素を選択できるよう見直すこと。

6. メディア候補が複数ある場合は、以下を考慮して操作対象を選択すること。

   - `readyState`
   - `paused`
   - `muted`
   - `currentSrc`
   - `src`
   - `offsetWidth`
   - `offsetHeight`
   - `display`
   - `visibility`
   - `duration`
   - `currentTime`

7. 非表示、サイズ0、広告用、再生不能、未初期化の候補を優先しないこと。

8. YouTubeページで実際に表示中・再生可能なメイン動画要素を優先すること。

9. `video` または `audio` が0件の場合、すぐに `MEDIA_NOT_FOUND` を返す前に、短時間の限定再試行を行うこと。

10. 再試行は以下を満たすこと。

   - 回数または期限を限定する
   - 無限ループにしない
   - UIスレッドをブロックしない
   - 各試行結果をログへ残す
   - 既存のIPC接続タイムアウトを大幅に超過しない

11. 推奨再試行例は、200～300ms間隔で最大3回程度とするが、既存タイムアウトへ収まるよう調整すること。

12. `NavigationCompleted` 済みでもSPA遷移やYouTube内部遷移でDOMが再構築される可能性を考慮すること。

13. ページ遷移後に古いDOM参照を保持しないこと。

14. 各IPC操作のたびに現在のDOMから操作対象を再取得すること。

15. `play` は対象要素の `play()` Promise結果を確認し、拒否された場合は理由をログへ残すこと。

16. `pause` は対象要素の `pause()` 実行後、可能であれば `paused` 状態を確認すること。

17. `toggle-mute` は実行前後の `muted` 状態をログへ残すこと。

18. `seek-to-start` は実行前後の `currentTime` をログへ残すこと。

19. 操作が成功したにもかかわらず `MEDIA_NOT_FOUND` を返さないこと。

20. 操作対象は見つかったが操作自体が失敗した場合は、`MEDIA_NOT_FOUND` とは別のErrorCodeを返すこと。

21. ErrorCodeを少なくとも以下のように区別すること。

   - `MEDIA_NOT_FOUND`
   - `WEBVIEW_NOT_READY`
   - `SCRIPT_EXECUTION_FAILED`
   - `PLAY_REJECTED`
   - `MEDIA_OPERATION_FAILED`
   - `TIMEOUT`

22. 既存のIPCレスポンス形式を可能な限り維持し、Control側との互換性を壊さないこと。

23. ErrorCode追加が必要な場合は、既存JSONへ後方互換な形で追加すること。

24. `get-status` にメディア診断情報を追加できる場合は、後方互換を維持して以下を追加すること。

   - mediaElementCount
   - videoElementCount
   - audioElementCount
   - documentReadyState
   - lastMediaCommand
   - lastMediaCommandResult
   - lastMediaErrorCode

25. ログファイルの既存保存先とローテーション方針を維持すること。

26. ログへ個人情報、Cookie、認証情報、ページ本文全体、トークン等を記録しないこと。

27. URLをログへ記録する場合は、既存セキュリティ方針に従い、必要ならクエリ文字列の一部マスクを検討すること。

28. `PipeOptions.CurrentUserOnly`、PID照合、URL長制限、タイムアウト、巨大メッセージ拒否等の既存セキュリティ対策を変更しないこと。

29. チケット013の初期停止処理を壊さないこと。

30. LiteTubeDock単体起動時の通常動作を変更しないこと。

31. 実装後、Debug構成でビルドし、警告0・エラー0を確認すること。

# 受け入れ条件（目視確認基準）

## ログ識別

1. テスターがLiteTubeDockを4台以上起動する。
2. 各Dockで異なるURLを表示する。
3. Controlから再生、停止、ミュート、巻き戻しを実行する。
4. プレイヤー側ログを開く。
5. 各操作ログにPIDとPipe名が含まれることを確認する。
6. どのDockの記録か判別できることを確認する。
7. CurrentUrl、ReadyState、VideoCount、AudioCount、Result、ErrorCodeが確認できることを確認する。

## YouTube動画操作

1. YouTube動画ページを表示する。
2. 動画読み込み完了後に `play` を送信する。
3. 動画が再生されることを確認する。
4. ログに対象video要素が検出され、Successが記録されることを確認する。
5. `pause`、`toggle-mute`、`seek-to-start` も同様に確認する。

## 読み込み直後

1. YouTube動画URLへ遷移直後、早いタイミングでミュート操作を行う。
2. メディア要素がまだ存在しない場合、限定再試行が行われることを確認する。
3. 再試行中の試行回数と要素数がログへ記録されることを確認する。
4. 要素生成後に成功する場合はSuccessを返すことを確認する。
5. 最終的に見つからない場合だけ `MEDIA_NOT_FOUND` になることを確認する。

## SPA遷移

1. YouTube内で別動画へ遷移する。
2. 遷移後に再生・停止・ミュートを実行する。
3. 古いDOM参照ではなく現在の動画へ操作されることを確認する。
4. ログのCurrentUrlが現在のURLと一致することを確認する。

## エラー区別

1. メディア要素が存在しないページで操作する。
2. `MEDIA_NOT_FOUND` が返ることを確認する。
3. WebView2未準備状態を再現できる場合、`WEBVIEW_NOT_READY` 等の別ErrorCodeになることを確認する。
4. JavaScript実行失敗時に `SCRIPT_EXECUTION_FAILED` 等が記録されることを確認する。

## 回帰確認

- `ping`、`get-status`、`navigate` が従来どおり動作すること
- 初期停止が動作すること
- 単体起動が動作すること
- IPCサーバーが不正JSON後も復帰すること
- `PipeOptions.CurrentUserOnly` が維持されること
- Debug構成で警告0・エラー0でビルド成功すること
