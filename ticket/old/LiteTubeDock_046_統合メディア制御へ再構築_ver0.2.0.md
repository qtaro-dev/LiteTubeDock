# 共通前提（必ず遵守）

- `E:\Dev\LiteTubeDock` のみ変更すること
- AGENT.md が存在する場合は最優先で遵守すること
- ユーザーはコードを直接編集しない
- 既存UI、設定、ログイン状態、起動引数、WebView2構成は壊さない
- 旧処理と新処理を同時実行する二重制御を残さない
- 文字列のハードコードを避け、既存定数・設計へ合わせる
- 実装後、バージョンを `0.2.0` 系へ更新する

# タスク名

LiteTubeDockのメディア制御を統合PlayerControlServiceへ再構築

# 目的

現在分散している再生・一時停止・シーク・次動画・チャプター・音量・ミュート・状態取得・動画変更検知を、1つの統合制御層へ置き換える。

LiteTubeDockControlから将来プレイリスト・再生キューを安定して操作できるよう、サイト差異をLiteTubeDock側で吸収する。

# 対象ファイル（推定可）

- `MainWindow.xaml.cs`
- `Services/MediaControlService.cs`
- `Services/AudioPersistenceService.cs`
- `Services/MutePersistenceService.cs`
- `Services/IpcCommandHandler.cs`
- `Models/IpcCommand.cs`
- `Models/IpcResponse.cs`
- `Models/IpcStatusData.cs`
- `Models/AudioControlResult.cs`
- `Constants/IpcConstants.cs`
- `Constants/AppConstants.cs`
- `LiteTubeDock.csproj`
- `docs/ipc_commands.md`
- 必要に応じて新規Service / Adapter / Model

# 実装内容

## 1. 統合制御サービスを追加

新規に `UnifiedPlayerControlService` 相当を作成する。

責務：

- サイト判定
- 主media要素選択
- 再生
- 一時停止
- 停止
- 任意時刻シーク
- 次動画
- 前動画
- 次チャプター
- 前チャプター
- 音量
- ミュート
- 希望音量・希望ミュート維持
- 動画変更検知
- 再生完了検知
- 操作成功確認
- 統一状態生成

## 2. サイト別Adapterを分離

最低限、以下を用意する。

- `YouTubePlayerAdapter`
- `TwitchPlayerAdapter`
- `GenericMediaPlayerAdapter`

共通インターフェースを定義し、IPC処理やMainWindowがサイト固有DOMを直接扱わないようにする。

## 3. 主media要素選択を一元化

現在複数サービスで行われているvideo/audio探索を1か所へ統合する。

選択条件：

- 再生中
- 可視
- readyState
- currentSrc
- サイズ
- 最終イベント時刻
- サイト固有条件

各機能が個別に `querySelector` しないこと。

## 4. 統一状態モデルを追加

最低限、以下を返す。

- `siteType`
- `playerType`
- `currentUrl`
- `mediaIdentity`
- `mediaRevision`
- `title`
- `currentTimeSeconds`
- `durationSeconds`
- `isPlaying`
- `isPaused`
- `isEnded`
- `isSeekable`
- `isLive`
- `volumePercent`
- `isMuted`
- `desiredVolumePercent`
- `desiredMutedState`
- `canGoNext`
- `canGoPrevious`
- `canGoNextChapter`
- `canGoPreviousChapter`
- `currentChapter`
- `chapterCount`
- `errorCode`
- `message`

## 5. 統一IPCを追加

新規コマンド例：

- `player-get-state`
- `player-play`
- `player-pause`
- `player-stop`
- `player-seek`
- `player-next`
- `player-previous`
- `player-next-chapter`
- `player-previous-chapter`
- `player-set-volume`
- `player-set-muted`
- `player-set-control-policy`
- `player-clear-control-policy`

既存IPCは互換層として残してよいが、内部では統合サービスを通すこと。

## 6. 成功確認を必須化

IPC送信・DOMクリック成功だけで完了扱いにしない。

例：次動画

1. 操作実行
2. URL / mediaIdentity / mediaRevisionを監視
3. 実際の動画変更を確認
4. 成功応答

変化しない場合は明確な失敗応答を返す。

## 7. Control管理モード

Controlから有効化された場合のみ、希望音量・希望ミュートを強制維持する。

保持項目：

- enabled
- desiredVolumePercent
- desiredMutedState
- lastHeartbeatAt
- expirationSeconds

以下で再適用する。

- volumechange
- loadedmetadata
- canplay
- play
- playing
- media要素差し替え
- 動画変更
- 広告切替
- SPA遷移

Controlが異常終了した場合、期限切れで自動解除する。

## 8. 再生完了検知

プレイリスト自動遷移用に、以下を判定する。

- ended
- duration到達
- サイト側自動遷移
- 再生エラー
- ライブ終了

統一状態で終了理由を返す。

## 9. 旧処理を整理

以下を新サービスへ移行後、重複処理を削除する。

- 個別media探索
- 個別音量保持
- 個別ミュート保持
- 旧次へ処理
- 旧状態取得処理
- MainWindowからの直接JavaScript操作

削除できないものは実装報告へ理由を記載する。

## 10. IPC仕様書更新

`docs/ipc_commands.md` に以下を記載する。

- 新統一IPC
- リクエスト
- 応答
- ErrorCode
- 状態モデル
- Control管理モード
- 旧IPC互換方針

## 11. バージョン更新

LiteTubeDockを `0.2.0` へ更新する。

一致対象：

- `AppConstants.AppVersion`
- `Version`
- `AssemblyVersion`
- `FileVersion`
- `InformationalVersion`
- アプリ内表示
- ログ
- Debug/Release exe・dll

推奨：

- 表示バージョン：`0.2.0`
- Assembly/FileVersion：`0.2.0.0`

# 受け入れ条件

1. すべてのメディア操作が統合サービスを経由する。
2. YouTube/Twitch/汎用mediaのAdapterが分離されている。
3. 主media要素選択が1か所に統合されている。
4. 次動画は実際のmediaIdentity変更まで確認する。
5. 30%設定後、再生・次動画・広告切替でも30%を維持する。
6. Control管理中のみ音量を強制維持する。
7. Control停止後は期限切れで管理解除される。
8. 再生完了状態と終了理由を取得できる。
9. 旧処理の二重実行がない。
10. `docs/ipc_commands.md` が更新されている。
11. Debug/Releaseビルドが警告0・エラー0で成功する。
12. バージョンが `0.2.0 / 0.2.0.0` で一致する。
13. 実装報告に残存旧処理・削除処理・未対応事項を明記する。
