# 共通前提（必ず遵守）

- AGENT.md が存在する場合、その内容を最優先で遵守させること
- ユーザーはコードを直接編集しない前提である
- 実装・修正はすべて CODEX が行う
- 既存設計を破壊しない
- 既存テーマ・ライブラリ構成を維持する
- ハードコード文字列は禁止
- ICommand構造を崩さない

# タスク名

YouTube Shorts再生中のリサイズ・フルスクリーン切替不具合修正と診断ログ追加

# 目的

LiteTubeDockでYouTube Shortsを再生中または一時停止中に、ウィンドウサイズ変更やAlt+Enterによるフルスクリーン切替を行うと、再生状態・表示内容・動画位置が不正に変化する問題を調査し修正する。

確認されている主な症状は以下。

- 再生中にウィンドウサイズを変更すると動画が停止する
- 再生中にフルスクリーンへ切り替えると映像だけ真っ黒になり、音声のみ継続する
- フルスクリーン切替時に、数個前に見ていたShortsへ戻る
- 一時停止中にフルスクリーンへ切り替えると、勝手に再生される

画面サイズ変更は表示領域の変更だけに留め、URL再読込、過去動画への移動、再生状態変更を発生させないようにする。

あわせて、通常リサイズ・最大化・フルスクリーン切替前後のWebView2状態、URL、動画要素、再生状態を詳細ログへ記録できるようにする。

# 対象ファイル（推定可）

- `E:\Dev\LiteTubeDock\MainWindow.xaml`
- `E:\Dev\LiteTubeDock\MainWindow.xaml.cs`
- `E:\Dev\LiteTubeDock\Services\DiagnosticLogService.cs`
- `E:\Dev\LiteTubeDock\Services\MediaControlService.cs`
- `E:\Dev\LiteTubeDock\Services\WindowStateService.cs`
- `E:\Dev\LiteTubeDock\Models\WindowDisplayState.cs`
- `E:\Dev\LiteTubeDock\Constants\AppConstants.cs`
- `E:\Dev\LiteTubeDock\Constants\UiTextConstants.cs`
- その他、Alt+Enter、フルスクリーン切替、ウィンドウサイズ変更、WebView2再配置、Shorts再生状態保持に関係するファイル

# 実装内容（具体的変更指示）

## 1. 対象イベントの整理

1. 少なくとも以下のイベント・処理経路を確認すること。

   - WPF `SizeChanged`
   - WPF `StateChanged`
   - WPF `LocationChanged`
   - Alt+Enterショートカット
   - フルスクリーン開始
   - フルスクリーン解除
   - 最大化
   - 最大化解除
   - カスタムサイズ変更
   - WebView2サイズ更新
   - WebView2可視状態更新
   - WebView2再初期化
   - URL再設定
   - NavigationStarting
   - NavigationCompleted

2. ウィンドウサイズ変更だけで、WebView2を再生成・再初期化しないこと。

3. ウィンドウサイズ変更だけで、現在URLへ再遷移しないこと。

4. ウィンドウサイズ変更だけで、HOME URL、お気に入りURL、前回URL等を再適用しないこと。

5. Alt+Enterによるフルスクリーン切替で、URL履歴を戻す・進める処理を実行しないこと。

6. フルスクリーン切替とブラウザ履歴操作が同じショートカット処理やキーイベントで競合していないか確認すること。

## 2. リサイズ前後の状態保持

1. 通常リサイズ開始前またはフルスクリーン切替前に、現在の再生状態を取得すること。

2. 少なくとも以下を保持すること。

   - CurrentUrl
   - Shortsか通常動画か
   - `document.readyState`
   - video要素数
   - 対象video要素の識別情報
   - `currentSrc`
   - `paused`
   - `muted`
   - `currentTime`
   - `duration`
   - `readyState`
   - `networkState`
   - `videoWidth`
   - `videoHeight`
   - 表示上の幅・高さ
   - `visibility`
   - `display`
   - ページ内でのShorts識別情報が取得可能ならその情報
   - 現在のウィンドウ状態

3. リサイズ後またはフルスクリーン切替後に、同じURL・同じ動画・同じ再生状態が維持されているか確認すること。

4. 一時停止中だった場合は、切替後も一時停止を維持すること。

5. 再生中だった場合は、切替後も同じ動画・同じ再生位置付近で再生を継続すること。

6. ミュート状態を変更しないこと。

7. 再生位置は、通常の切替処理による短時間経過分を除き、大きく前後しないこと。

8. 数個前のShortsへ戻る現象を防止すること。

9. `history.back()`、`history.go()`、WebView2の戻る・進む処理、URL再設定がフルスクリーン切替中に呼ばれていないか確認すること。

## 3. Shorts固有のDOM再構築対策

1. YouTube Shortsでは、リサイズや表示モード切替によりDOMやvideo要素が再構築される可能性を考慮すること。

2. リサイズ前のDOM要素参照を保持したまま再利用しないこと。

3. 切替後は、現在のDOMから再度video要素を取得すること。

4. チケット037で実装したメディア要素スコアリング方式を再利用すること。

5. 複数video要素が存在する場合、現在表示中・再生対象のShortsを優先すること。

6. 非表示、サイズ0、広告用、過去Shorts用のvideo要素を操作対象として選ばないこと。

7. currentSrc、表示サイズ、paused、readyState、再生位置等を利用して、切替前後で同一動画か判定すること。

8. 同一動画でない場合は、URL、currentSrc、ページ内識別情報をログへ残すこと。

9. 前のShortsへ戻ってしまった場合、WebView2のURLが変化したのか、同一URL内でDOM表示対象だけが変わったのか判別できるようにすること。

## 4. 真っ黒画面対策

1. フルスクリーン切替後に音声は継続するが映像が真っ黒になる現象を調査すること。

2. 切替前後で以下を確認すること。

   - video要素の存在
   - `readyState`
   - `videoWidth`
   - `videoHeight`
   - CSS上の幅・高さ
   - `display`
   - `visibility`
   - `opacity`
   - 親要素サイズ
   - WebView2実表示サイズ
   - WebView2可視状態
   - GPU描画関連例外
   - Navigation発生有無

3. WebView2のサイズ更新が0×0や極端に小さい値を一時的に通っていないか確認すること。

4. フルスクリーン切替中にWebView2を一時的に非表示・再表示している場合、その処理が映像描画へ影響していないか確認すること。

5. 真っ黒状態を検出した場合でも、安易にページ全体を再読み込みしないこと。

6. 必要な場合は、WebView2のレイアウト再計算、表示更新、video要素の再取得等、再生状態を破壊しない手段を優先すること。

7. ページ再読込を最終手段としても自動実行しないこと。

## 5. 一時停止状態の維持

1. フルスクリーン切替前にvideo要素が一時停止中だった場合、その状態を記録すること。

2. 切替後に同一動画が再生状態へ変わっていた場合は、一時停止へ戻すこと。

3. 一時停止復元は、切替処理に伴う一度限りの補正とすること。

4. ユーザーが切替後に再生ボタンを押した場合、その操作を妨げないこと。

5. 継続的な強制停止監視を追加しないこと。

6. 再生中だった場合は、切替後に勝手に一時停止へ変更しないこと。

7. 切替前の状態が取得できなかった場合は、勝手に再生または停止させないこと。

## 6. 通常リサイズ時の動作

1. マウスによるウィンドウサイズ変更中、再生・停止・ミュート状態を変更しないこと。

2. リサイズ中に毎フレーム重いJavaScript診断を実行しないこと。

3. `SizeChanged`が大量発生するため、診断・補正処理はデバウンスすること。

4. 推奨として、サイズ変更停止後200～500ms程度で1回だけ状態確認を行うこと。

5. リサイズ中のUIスレッドをブロックしないこと。

6. カスタムサイズから別カスタムサイズへ変更しても、Shortsが停止・巻き戻り・過去動画へ移動しないこと。

## 7. フルスクリーン切替処理

1. Alt+Enterで通常表示からフルスクリーンへ切り替えられること。

2. Alt+Enterでフルスクリーンから通常表示へ戻せること。

3. 切替前の通常ウィンドウ位置・サイズ・状態を保持すること。

4. フルスクリーン解除後、元のカスタムサイズと位置へ戻ること。

5. Alt+Enterキー入力がWebView2側とWPF側で二重処理されないこと。

6. キーリピートや連打で複数回切替処理が重複しないこと。

7. 切替処理中は再入を防止すること。

8. フルスクリーン切替完了後にのみ状態確認・必要な補正を行うこと。

9. 切替処理だけで再生コマンド・一時停止コマンドを送らないこと。

10. 状態補正が必要な場合のみ、切替前状態へ戻すこと。

## 8. 診断ログ追加

1. リサイズ・フルスクリーン切替専用の診断ログを追加すること。

2. 少なくとも以下のイベントをログへ記録すること。

   - ResizeStarted
   - ResizeCompleted
   - FullscreenEnterStarted
   - FullscreenEnterCompleted
   - FullscreenExitStarted
   - FullscreenExitCompleted
   - WindowStateChanged
   - NavigationStarting
   - NavigationCompleted
   - MediaStateBefore
   - MediaStateAfter
   - MediaStateRestored
   - BlackVideoSuspected
   - ShortsIdentityChanged
   - UnexpectedPlaybackStateChanged

3. 各ログには少なくとも以下を含めること。

   - Timestamp
   - ProcessId
   - AppVersion
   - WindowMode
   - 変更前のウィンドウ幅・高さ
   - 変更後のウィンドウ幅・高さ
   - 変更前の位置
   - 変更後の位置
   - CurrentUrl
   - URL変更有無
   - NavigationStarting発生有無
   - NavigationCompleted発生有無
   - `document.readyState`
   - video要素数
   - audio要素数
   - iframe要素数
   - 対象videoの`currentSrc`
   - `paused`
   - `muted`
   - `currentTime`
   - `duration`
   - `readyState`
   - `networkState`
   - `videoWidth`
   - `videoHeight`
   - 表示幅
   - 表示高さ
   - `display`
   - `visibility`
   - 同一動画判定結果
   - 補正処理の有無
   - 補正結果
   - 例外内容
   - 処理時間

4. URLは既存のログマスク方針を維持すること。

5. Cookie、認証情報、トークン、ページ本文全体をログへ記録しないこと。

6. ログ書込失敗でアプリを異常終了させないこと。

7. チケット038のログ専用ウィンドウで追加ログを閲覧・検索・コピーできること。

## 9. エラー・状態判定

1. リサイズ後にURLが変わった場合は、異常状態としてログへ記録すること。

2. 切替前後でcurrentSrcが変わった場合は、Shorts識別変化として記録すること。

3. 一時停止前後で`paused=false`へ変わった場合は、UnexpectedPlaybackStateChangedとして記録すること。

4. 再生中から`paused=true`へ変わった場合も同様に記録すること。

5. 音声継続中かつvideoWidthまたはvideoHeightが0、表示サイズ0、visibility/display異常等の場合は、BlackVideoSuspectedとして記録すること。

6. 異常を検出しても、ユーザーへ過剰なダイアログを連続表示しないこと。

7. 画面上へ通知する場合は、既存ステータス欄またはログ中心とすること。

## 10. 非対象範囲

本チケットでは以下を対象外とすること。

- YouTubeサイト自体の仕様変更への恒久保証
- YouTubeアカウントログイン問題
- 広告制御
- DRM制御
- Shortsの自動送り制御
- 他サイトの縦型動画への個別最適化
- WebView2ランタイム自体の修正
- GPUドライバー修正

## 11. 回帰防止

1. 通常の横動画再生を壊さないこと。

2. 通常YouTubeページ、トップページ、ライブ配信を壊さないこと。

3. アドレスバー、戻る、進む、再読込、ホームを壊さないこと。

4. IPCの再生、一時停止、ミュート、巻き戻しを壊さないこと。

5. チケット013の初期停止を壊さないこと。

6. チケット037のメディア要素スコアリングと診断ログを壊さないこと。

7. プレイヤーモード・通常モードの両方で動作確認すること。

8. Debug構成で警告0・エラー0を確認すること。

# 受け入れ条件（目視確認基準）

## 通常リサイズ・再生中

1. テスターがLiteTubeDockを通常モードで起動する。
2. YouTube Shortsを1本再生する。
3. 現在のShorts内容と再生位置を確認する。
4. ウィンドウ端をドラッグして複数回サイズ変更する。
5. 動画が勝手に一時停止しないことを確認する。
6. 数個前のShortsへ戻らないことを確認する。
7. 現在URLが変わらないことを確認する。
8. 映像が真っ黒にならないことを確認する。

## 通常リサイズ・一時停止中

1. YouTube Shortsを一時停止する。
2. ウィンドウサイズを変更する。
3. 切替後も一時停止状態を維持することを確認する。
4. 勝手に再生されないことを確認する。
5. 再生位置が大きく変化しないことを確認する。

## フルスクリーン切替・再生中

1. カスタムサイズ960×763前後でYouTube Shortsを再生する。
2. Alt+Enterを押す。
3. フルスクリーンへ切り替わることを確認する。
4. 映像が真っ黒にならないことを確認する。
5. 音声だけ再生される状態にならないことを確認する。
6. 同じShortsが継続表示されることを確認する。
7. 数個前のShortsへ戻らないことを確認する。
8. 再生位置が大きく巻き戻らないことを確認する。

## フルスクリーン切替・一時停止中

1. YouTube Shortsを一時停止する。
2. Alt+Enterを押す。
3. フルスクリーンへ切り替わることを確認する。
4. 切替後も一時停止状態であることを確認する。
5. 勝手に再生されないことを確認する。
6. 同じShorts・同じ再生位置付近であることを確認する。

## フルスクリーン解除

1. フルスクリーン中にAlt+Enterを押す。
2. 元のカスタムサイズ・位置へ戻ることを確認する。
3. 再生中なら再生継続、一時停止中なら一時停止維持を確認する。
4. Shortsが過去動画へ戻らないことを確認する。
5. 映像が真っ黒にならないことを確認する。

## 連続切替

1. 再生中のShortsでAlt+Enterを複数回繰り返す。
2. 毎回、通常表示とフルスクリーンが正しく切り替わることを確認する。
3. URL、Shorts内容、再生状態が不正に変化しないことを確認する。
4. アプリが固まらないことを確認する。
5. WebView2が再初期化されないことをログで確認する。

## 通常動画との比較

1. 通常の横型YouTube動画を再生する。
2. 通常リサイズ、フルスクリーン切替、解除を行う。
3. 再生状態、URL、再生位置が維持されることを確認する。
4. Shorts専用修正によって通常動画が壊れていないことを確認する。

## ログ確認

1. チケット038のログウィンドウを開く。
2. Shorts再生中に通常リサイズを行う。
3. ResizeStarted、ResizeCompleted、MediaStateBefore、MediaStateAfterが記録されることを確認する。
4. Alt+Enterでフルスクリーンへ切り替える。
5. FullscreenEnterStarted、FullscreenEnterCompletedが記録されることを確認する。
6. ログにPID、サイズ前後、CurrentUrl、video要素数、currentSrc、paused、currentTime、videoWidth、videoHeightが含まれることを確認する。
7. URL再遷移が発生していない場合、NavigationStartingが発生していないことを確認できること。
8. 異常状態を再現できた場合、BlackVideoSuspected、ShortsIdentityChanged、UnexpectedPlaybackStateChanged等が記録されることを確認する。

## 回帰確認

- WebView2の通常表示が動作すること
- アドレスバー、戻る、進む、再読込、ホームが動作すること
- お気に入りボタンが動作すること
- IPCの再生、一時停止、ミュート、巻き戻しが動作すること
- 初期停止が動作すること
- ログウィンドウが動作すること
- 通常モードとプレイヤーモードの両方で異常終了しないこと
- Debug構成で警告0・エラー0でビルド成功すること
