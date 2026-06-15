# 共通前提（必ず遵守）

- AGENT.md が存在する場合、その内容を最優先で遵守させること
- ユーザーはコードを直接編集しない前提である
- 実装・修正はすべて CODEX が行う
- 既存設計を破壊しない
- 既存テーマ・ライブラリ構成を維持する
- ハードコード文字列は禁止
- ICommand構造を崩さない

# タスク名

043：ミキサー機能向けの音量・ミュート状態IPCを追加する

# 目的

将来LiteTubeDockControlへミキサー画面を実装するため、LiteTubeDock本体側へ、各Dockの音量・ミュート状態を明示的に取得・設定できるIPCコマンドを追加する。

既存の `toggle-mute` は状態反転型であり、ミキサーUIから確実にON/OFFを指定する用途には不十分である。

本チケットでは、ミキサー実装に必要なIPC基盤だけを追加し、LiteTubeDockControl側のミキサーUIは実装しない。

# 対象ファイル（推定可）

- `E:\Dev\LiteTubeDock\Constants\IpcConstants.cs`
- `E:\Dev\LiteTubeDock\Models\IpcCommand.cs`
- `E:\Dev\LiteTubeDock\Models\IpcStatusData.cs`
- `E:\Dev\LiteTubeDock\Services\IpcCommandHandler.cs`
- `E:\Dev\LiteTubeDock\Services\MediaControlService.cs`
- `E:\Dev\LiteTubeDock\Services\MutePersistenceService.cs`
- `E:\Dev\LiteTubeDock\Services\DiagnosticLogService.cs`
- 音量・メディア状態の応答モデル
- `E:\Dev\LiteTubeDock\Constants\AppConstants.cs`
- `E:\Dev\LiteTubeDock\LiteTubeDock.csproj`

# 実装内容（具体的変更指示）

1. LiteTubeDock本体側だけを修正すること。

2. LiteTubeDockControl側は変更しないこと。

3. ミキサー向けIPCとして最低限以下を追加すること。

   - `get-audio-status`
   - `set-volume`
   - `set-muted`

4. 既存の `toggle-mute` は後方互換のため維持すること。

5. `get-audio-status` は、現在の音声状態を1回の応答で取得できること。

6. `get-audio-status` の応答へ最低限以下を含めること。

   - `volume`
   - `volumePercent`
   - `isMuted`
   - `desiredMutedState`
   - `mutePersistenceEnabled`
   - `mediaElementCount`
   - `isPlaying`
   - `currentTime`
   - `duration`
   - `currentUrl`
   - `mediaTitle`
   - `success`
   - `errorCode`
   - `message`

7. 音量の内部表現はWeb標準に合わせて0.0～1.0を使用してよいが、Control側が扱いやすい0～100の整数値も応答へ含めること。

8. `set-volume` は音量を明示的に設定できること。

9. `set-volume` の入力は以下のどちらかに統一すること。

   推奨：

   - `volumePercent`: 0～100の整数

10. 0未満または100を超える値は、黙って丸めず `INVALID_VOLUME` 相当を返すこと。

11. 数値でない値、null、未指定も明確なErrorCodeを返すこと。

12. `set-volume` 実行後、実際に適用された音量を応答で返すこと。

13. `set-volume` で0を指定した場合でも、`isMuted` を自動的にTrueへ変更するかどうかを仕様として明確にすること。

14. 推奨仕様は、音量0とミュート状態を別管理し、0指定だけでは `isMuted` を変更しないこと。

15. `set-muted` は明示的にミュートON/OFFを設定できること。

16. `set-muted` の入力は以下に統一すること。

   - `muted`: true / false

17. `set-muted` はtoggleではなく、指定された状態へ必ず設定すること。

18. すでに同じ状態の場合は安全に成功として扱うこと。

19. `set-muted` の実行結果は、チケット042のミュート継続機能における利用者希望状態へ反映すること。

20. `set-muted true` の後は `DesiredMutedState=True` とすること。

21. `set-muted false` の後は `DesiredMutedState=False` とすること。

22. ミュート継続OFFの場合でも、`set-muted` 自体は正常に動作すること。

23. `set-volume` と `set-muted` は、YouTube側でvideo/audio要素が差し替えられた場合でも、現在対象の有効なメディア要素へ適用すること。

24. 複数のvideo/audio要素が存在する場合は、既存のメディア要素スコアリングを再利用すること。

25. 同じ要素検出ロジックを重複実装しないこと。

26. 有効なメディア要素が見つからない場合は `MEDIA_NOT_FOUND` を返すこと。

27. WebView2未初期化時は `WEBVIEW_NOT_READY` を返すこと。

28. JavaScript実行失敗時は `SCRIPT_EXECUTION_FAILED` を返すこと。

29. 必要に応じて以下のErrorCodeを追加すること。

   - `INVALID_VOLUME`
   - `INVALID_MUTED_VALUE`
   - `AUDIO_STATUS_UNAVAILABLE`
   - `VOLUME_SET_FAILED`
   - `MUTE_SET_FAILED`
   - `MEDIA_NOT_FOUND`
   - `WEBVIEW_NOT_READY`
   - `SCRIPT_EXECUTION_FAILED`

30. `get-audio-status` は、ミキサー画面から定期取得される可能性を考慮し、過剰に重いDOM走査や大量ログ出力を避けること。

31. 1秒未満の短周期呼び出しでも、LiteTubeDockのUIや動画再生を著しく重くしないこと。

32. 状態取得と操作処理はUIスレッドを長時間ブロックしないこと。

33. `mediaTitle` は取得可能な場合のみ返し、取得できない場合は空文字またはnullとすること。

34. `currentTime` と `duration` は取得可能な場合のみ返すこと。

35. ライブ配信等でdurationが有限値でない場合は、安全に表現できる値を返すこと。

36. YouTube通常動画、ライブ、プレイリスト、Shortsで可能な範囲を確認すること。

37. YouTube以外のページでは、安全に対応外またはメディア未検出を返すこと。

38. 出力デバイス切替IPCは本チケットでは実装しないこと。

39. `set-audio-output-device` はWebView2およびWindows制約の別調査対象とすること。

40. 既存の再生、一時停止、巻き戻し、枠内全画面、ミュート継続を破壊しないこと。

41. `get-status` との重複が大きい場合でも、ミキサーUIが必要とする音声情報を `get-audio-status` へ明確に集約すること。

42. 診断ログへ最低限以下を記録すること。

   - PID
   - PipeName
   - Command
   - RequestedVolume
   - AppliedVolume
   - RequestedMuted
   - ActualMuted
   - MediaElementCount
   - CurrentUrl
   - ErrorCode
   - Duration

43. `get-audio-status` の定期取得ログは、必要に応じて詳細ログレベルへ下げるか重複抑制すること。

44. `Constants/AppConstants.cs` の `AppVersion` と `LiteTubeDock.csproj` の以下が一致しているか確認すること。

   - Version
   - AssemblyVersion
   - FileVersion
   - InformationalVersion

45. アプリ内表示、ログ、exe/dllのバージョンが一致すること。

46. Debugビルドを実行し、警告0・エラー0を確認すること。

# 受け入れ条件（目視確認基準）

## 目視確認前提

- 最新のLiteTubeDock一式を同一ビルド成果物で配置する
- exeとdllを混在させない
- LiteTubeDockを `--player-mode --ipc-enabled` 付きで起動する
- YouTube通常動画を表示する
- IPCコマンドを送信できるテスト手段を用意する

## 音声状態取得確認

1. テスターが `get-audio-status` を送信する。
2. volumeまたはvolumePercentを取得できること。
3. isMutedを取得できること。
4. desiredMutedStateを取得できること。
5. mutePersistenceEnabledを取得できること。
6. mediaElementCountを取得できること。
7. isPlayingを取得できること。
8. currentTimeとdurationを取得できること。
9. currentUrlを取得できること。
10. mediaTitleを取得可能な場合は取得できること。

## 音量設定確認

1. `set-volume volumePercent=50` を送信する。
2. 実際の音量が約50%になること。
3. 応答で適用後音量を確認できること。
4. `get-audio-status` でも50%相当を確認できること。
5. 0、1、99、100でも正常に設定できること。
6. -1、101、文字列、未指定で明確なErrorCodeが返ること。

## 明示的ミュート確認

1. `set-muted muted=true` を送信する。
2. 必ずミュートになること。
3. 再度trueを送信しても解除されないこと。
4. `set-muted muted=false` を送信する。
5. 必ずミュート解除されること。
6. 再度falseを送信してもミュートにならないこと。

## ミュート継続連携確認

1. ミュート継続をONにする。
2. `set-muted true` を送信する。
3. DesiredMutedState=Trueになること。
4. 広告やメディア要素差替え後もミュートを維持できること。
5. `set-muted false` を送信する。
6. DesiredMutedState=Falseになること。
7. 以後、古いミュート希望が再適用されないこと。

## 音量0とミュート分離確認

1. `set-muted false` を送信する。
2. `set-volume volumePercent=0` を送信する。
3. 音量は0になること。
4. isMutedは仕様どおりfalseを維持すること。
5. `set-volume volumePercent=50` を送信する。
6. 音声が50%で戻ること。

## メディア差替え確認

1. 音量を30%へ設定する。
2. 別動画へ遷移する。
3. 新しいメディア要素に対して状態取得・設定が可能であること。
4. 広告へ切り替わってもコマンドが異常終了しないこと。

## 複数Dock確認

1. LiteTubeDockを4台起動する。
2. Dock 1へ50%、Dock 2へ20%、Dock 3へミュート、Dock 4へ80%を設定する。
3. 各Dockが個別状態を維持すること。
4. 他Dockへ設定が混ざらないこと。

## 非対応・異常系確認

1. YouTube以外のページを表示する。
2. `get-audio-status` を送信する。
3. アプリが異常終了しないこと。
4. 対応外またはMEDIA_NOT_FOUNDを返すこと。
5. WebView2初期化前でも明確なErrorCodeを返すこと。

## 負荷確認

1. `get-audio-status` を数秒間、定期的に送信する。
2. 動画再生が著しくカクつかないこと。
3. UIが固まらないこと。
4. ログが短時間に過剰増加しないこと。

## 回帰確認

1. playが動作すること。
2. pauseが動作すること。
3. toggle-muteが動作すること。
4. seek-to-startが動作すること。
5. 枠内全画面が動作すること。
6. ミュート継続が動作すること。

## ログ確認

1. LiteTubeDock側ログを開く。
2. 要求音量と適用音量を確認できること。
3. 要求ミュート状態と実状態を確認できること。
4. ErrorCodeと処理時間を確認できること。
5. 定期取得ログが過剰でないこと。

## バージョン確認

1. アプリ内バージョンを確認する。
2. exeプロパティを確認する。
3. dllプロパティを確認する。
4. すべて一致すること。

## ビルド確認

- LiteTubeDockのDebugビルドが成功すること
- 警告0件であること
- エラー0件であること
