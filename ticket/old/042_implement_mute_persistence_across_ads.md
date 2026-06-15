# 共通前提（必ず遵守）

- AGENT.md が存在する場合、その内容を最優先で遵守させること
- ユーザーはコードを直接編集しない前提である
- 実装・修正はすべて CODEX が行う
- 既存設計を破壊しない
- 既存テーマ・ライブラリ構成を維持する
- ハードコード文字列は禁止
- ICommand構造を崩さない

# タスク名

042：広告切替後もミュート状態を維持する「ミュート継続」機能を実装する

# 目的

LiteTubeDockControlのチケット039で追加された「ミュート継続」設定に対応し、LiteTubeDockプレイヤー側で以下を実現する。

- 動画をミュート中に広告へ切り替わっても、広告音声をミュートのまま維持する
- 広告終了後に本編へ戻っても、ミュート状態を維持する
- 広告開始前に音声ONだった場合は、勝手にミュートしない
- 利用者が広告中または本編中に手動でミュート解除した場合は、その最新操作を優先する

本機能は広告の非表示、自動スキップ、再生妨害を行わず、利用者が指定したミュート状態だけを維持する。

# 対象ファイル（推定可）

- `E:\Dev\LiteTubeDock\App.xaml.cs`
- `E:\Dev\LiteTubeDock\MainWindow.xaml.cs`
- `E:\Dev\LiteTubeDock\Constants\AppConstants.cs`
- `E:\Dev\LiteTubeDock\LiteTubeDock.csproj`
- 起動引数を解析するクラス
- IPCコマンド定義を管理するファイル
- IPC受信・応答処理を担当するファイル
- WebView2へJavaScriptを実行している既存ファイル
- ミュート操作を担当している既存ファイル
- `get-status` 応答モデルを定義しているファイル
- 診断ログを出力しているファイル

# 実装内容（具体的変更指示）

1. LiteTubeDock本体側だけを修正すること。

2. LiteTubeDockControl側は変更しないこと。

3. Control側チケット039で追加された起動引数 `--keep-muted` を受け取れるようにすること。

4. `--keep-muted` が指定された場合、起動時から「ミュート継続」をONとして扱うこと。

5. IPCへ以下のコマンドを追加すること。

   - `set-mute-persistence`
   - `get-mute-persistence`

6. `set-mute-persistence` は、ON/OFFを明示的に設定できること。

7. `get-mute-persistence` は、現在の設定状態を返すこと。

8. 設定状態は、現在のメディア要素がミュートかどうかとは別に保持すること。

   推奨状態例：

   - `MutePersistenceEnabled`
   - `DesiredMutedState`
   - `LastUserMuteAction`

9. 「ミュート継続」がONの場合でも、常にミュートへ固定しないこと。

10. 利用者がミュートをONにした時点で、希望状態をミュートとして記録すること。

11. 利用者がミュートをOFFにした時点で、希望状態を音声ONとして更新すること。

12. Control側IPCの `toggle-mute`、プレイヤー側UI、YouTube側UIなど、利用者の明示操作を可能な範囲で検出し、最新の操作を優先すること。

13. 広告への切替時、YouTube側がvideo要素またはaudio要素を差し替えても、広告開始前の希望状態がミュートであれば、新しいメディア要素へミュートを再適用すること。

14. 広告終了後に本編へ戻った際も、希望状態がミュートであれば、ミュートを再適用すること。

15. 広告開始前の希望状態が音声ONの場合は、広告中も本編復帰後も勝手にミュートしないこと。

16. 広告中に利用者が手動でミュート解除した場合は、その操作を優先し、本編へ戻った後も音声ONを維持すること。

17. 広告中に利用者が手動でミュートした場合は、その操作を優先し、本編へ戻った後もミュートを維持すること。

18. ミュート状態の再適用は、広告であることを判定して広告を妨害する処理ではなく、メディア要素の生成・差替え・状態変化を検知して利用者の希望状態を再適用する方式とすること。

19. 以下のタイミングで、現在のメディア要素と希望ミュート状態を確認すること。

   - WebView2ナビゲーション完了
   - YouTube動画プレイヤー初期化
   - video/audio要素の追加・差替え
   - `loadedmetadata`
   - `canplay`
   - `play`
   - `volumechange`
   - YouTubeプレイヤー状態変化
   - 広告開始・広告終了に伴うDOM変化
   - 本編復帰時

20. DOM監視を行う場合は、MutationObserver等を使用し、過剰な短周期ポーリングだけに依存しないこと。

21. 補助的に再確認タイマーを使用する場合は、CPU負荷とログ量が過剰にならない間隔にすること。

22. 同じメディア要素へ不要な再適用を連続実行しないこと。

23. YouTube通常動画、ライブ、プレイリストで確認すること。

24. Shortsは既存のメディア検出・再描画処理を破壊しない範囲で対応すること。

25. Shortsで安定して状態維持できない場合は、無理に処理せずログへ明示すること。

26. YouTube以外のページでは、安全に何もしないこと。

27. YouTube以外のページで本機能がONでも、アプリを異常終了させないこと。

28. 本機能は広告の非表示、自動スキップ、再生速度変更、広告要素削除を行わないこと。

29. 既存のミュートIPC処理を共通化し、ミュート継続用に別系統の重複ロジックを増やさないこと。

30. `get-status` 応答へ最低限以下を追加すること。

   - `MutePersistenceEnabled`
   - `DesiredMutedState`
   - `ActualMutedState`
   - `MediaElementCount`
   - `LastMuteReapplyReason`

31. `set-mute-persistence` の応答へ最低限以下を含めること。

   - Result
   - ErrorCode
   - MutePersistenceEnabled
   - DesiredMutedState
   - ActualMutedState
   - CurrentUrl
   - Duration
   - Message

32. 必要に応じて以下のErrorCodeを追加すること。

   - `MUTE_PERSISTENCE_NOT_SUPPORTED`
   - `MUTE_PERSISTENCE_SET_FAILED`
   - `MEDIA_NOT_FOUND`
   - `WEBVIEW_NOT_READY`
   - `MUTE_REAPPLY_FAILED`
   - `MUTE_STATE_UNKNOWN`

33. 起動引数とIPC設定が競合した場合は、後から受信したIPC設定を優先すること。

34. Control側からONを受信した直後、現在ミュート中であれば希望状態をミュートとして保持すること。

35. Control側からONを受信した直後、現在音声ONであれば、勝手にミュートせず希望状態を音声ONとして保持すること。

36. OFFへ変更した場合は、以降の自動再適用を停止すること。

37. OFFへ変更した瞬間に、現在ミュート中の音声を勝手にONへ戻さないこと。

38. URL遷移、再読み込み、枠内全画面切替、アプリ全体フルスクリーン切替後も設定状態を維持すること。

39. LiteTubeDock再起動時は、起動引数 `--keep-muted` の有無に従うこと。

40. 診断ログへ最低限以下を記録すること。

   - PID
   - PipeName
   - CurrentUrl
   - MutePersistenceEnabled
   - DesiredMutedState
   - ActualMutedStateBefore
   - ActualMutedStateAfter
   - MediaElementCount
   - MediaElementChanged
   - ReapplyReason
   - ReapplyResult
   - ErrorCode
   - Duration

41. 広告切替やDOM変化のたびに同じ内容を大量出力しないよう、重複ログを抑制すること。

42. 既存のIPCコマンドを破壊しないこと。

43. 既存の再生、一時停止、ミュート、巻き戻し、次へ、枠内全画面、URL送信、終了処理を維持すること。

44. 既存のタイトルバー非表示、外枠非表示、Shorts再描画対策、フルスクリーン復帰処理を破壊しないこと。

45. `Constants/AppConstants.cs` の `AppVersion` と `LiteTubeDock.csproj` の以下が一致しているか確認すること。

   - Version
   - AssemblyVersion
   - FileVersion
   - InformationalVersion

46. アプリ内表示、ログ、exe/dllプロパティで確認できるバージョンが一致していること。

47. Debugビルドを実行し、警告0・エラー0を確認すること。

# 受け入れ条件（目視確認基準）

## 目視確認前提

- LiteTubeDockControl側チケット039が実装済みであること
- 最新のLiteTubeDock一式を同じビルド成果物で配置すること
- exeだけでなくdll、deps.json、runtimeconfig.jsonを同じビルドで統一すること
- 古いファイルを混在させないこと
- Control側の「設定 → 動作」で「ミュート継続」をONにすること
- ControlからLiteTubeDockを起動すること
- 広告が表示される可能性のあるYouTube動画を使用すること

## 起動引数確認

1. テスターが「ミュート継続」をONにして保存する。
2. ControlからLiteTubeDockを新規起動する。
3. LiteTubeDock側ログを開く。
4. `--keep-muted` を受け取った記録があること。
5. `MutePersistenceEnabled=True` で起動していること。

## IPC設定確認

1. LiteTubeDockを起動したまま、Control側で「ミュート継続」をOFFにして保存する。
2. LiteTubeDock側ログで `set-mute-persistence` のOFF受信を確認できること。
3. `get-mute-persistence` でOFFが返ること。
4. 再度ONにして保存する。
5. `get-mute-persistence` でONが返ること。

## 広告前ミュート時の確認

1. テスターが本編再生中にミュートをONにする。
2. 本編がミュートで再生されていることを確認する。
3. 広告へ切り替わるまで待つ。
4. 広告開始時に音声がONへ戻らないこと。
5. 広告中もミュートが維持されること。
6. 広告を手動スキップするか、自動終了まで待つ。
7. 本編へ戻った後もミュートが維持されること。
8. `get-status` で DesiredMutedState=True、ActualMutedState=True 相当を確認できること。

## 広告前音声ON時の確認

1. テスターが本編再生中に音声をONにする。
2. 広告へ切り替わるまで待つ。
3. 広告が勝手にミュートされないこと。
4. 本編へ戻った後も音声ONが維持されること。

## 広告中の手動解除確認

1. 本編をミュート状態で再生する。
2. 広告へ切り替わるまで待つ。
3. 広告中に利用者が手動でミュートを解除する。
4. 広告中の音声がONになること。
5. 本編へ戻った後も音声ONが維持されること。
6. 古いミュート希望状態が勝手に再適用されないこと。

## 広告中の手動ミュート確認

1. 本編を音声ONで再生する。
2. 広告へ切り替わるまで待つ。
3. 広告中に利用者が手動でミュートする。
4. 広告中がミュートになること。
5. 本編へ戻った後もミュートが維持されること。

## OFF時確認

1. Control側で「ミュート継続」をOFFにする。
2. 本編をミュート状態で再生する。
3. 広告へ切り替わるまで待つ。
4. LiteTubeDock側が自動再適用処理を行わないこと。
5. OFFへ変更した瞬間に現在のミュート状態が勝手に解除されないこと。

## URL遷移確認

1. 「ミュート継続」をONにする。
2. ミュート状態で別のYouTube動画へURL送信する。
3. 新しい動画でもミュート希望状態が維持されること。
4. 動画要素差替え後もミュートが再適用されること。

## 枠内全画面回帰確認

1. ミュート継続ONかつミュート状態にする。
2. 枠内全画面へ切り替える。
3. ミュートが維持されること。
4. 枠内全画面を解除する。
5. ミュートが維持されること。

## 複数Dock確認

1. LiteTubeDockを4台起動する。
2. Dock 1とDock 3だけをミュートにする。
3. 広告切替後もDock 1とDock 3だけがミュートを維持すること。
4. Dock 2とDock 4は音声ONのままであること。
5. 別Dockの希望状態が混在しないこと。

## 非YouTubeページ確認

1. YouTube以外のページを表示する。
2. ミュート継続をONにする。
3. LiteTubeDockが異常終了しないこと。
4. 不要なDOM操作を繰り返さないこと。
5. 対応対象外であることをログで確認できること。

## ログ確認

1. LiteTubeDock側ログを開く。
2. MutePersistenceEnabledを確認できること。
3. DesiredMutedStateとActualMutedStateを確認できること。
4. メディア要素差替え時のReapplyReasonを確認できること。
5. 再適用の成否とErrorCodeを確認できること。
6. 同じ内容のログが短時間に大量出力されていないこと。

## バージョン確認

1. LiteTubeDockの「バージョン情報」を開く。
2. exeファイルのプロパティを開く。
3. dllファイルのプロパティを開く。
4. アプリ内表示、exe、dllのバージョンが一致すること。
5. `AppConstants.AppVersion` とcsprojの各バージョン定義が一致すること。

## ビルド確認

- LiteTubeDockのDebugビルドが成功すること
- 警告0件であること
- エラー0件であること
