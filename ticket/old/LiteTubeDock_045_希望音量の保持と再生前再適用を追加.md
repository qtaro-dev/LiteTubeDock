# 共通前提（必ず遵守）

- AGENT.md が存在する場合、その内容を最優先で遵守すること
- ユーザーはコードを直接編集しない前提である
- 実装・修正はすべて CODEX が行う
- 既存設計を破壊しない
- 既存テーマ・ライブラリ構成を維持する
- ハードコード文字列は禁止
- ICommand構造を崩さない

# タスク名

希望音量の保持とmedia要素差し替え時の再適用を追加

# 目的

LiteTubeDockControlのミキサーで設定した音量を、LiteTubeDock側で確実に維持する。

一時停止解除、再生開始、動画変更、広告切替、YouTube・Twitch内部でのvideo/audio要素差し替え後も、ミキサーで設定した音量が100%へ戻らないようにする。

特に、ミキサーで30%へ設定した後に再生した際、100%で鳴る現象を防止する。

# 対象ファイル（推定可）

- `MainWindow.xaml.cs`
- `Services/MediaControlService.cs`
- `Services/MutePersistenceService.cs`
- `Services/IpcCommandHandler.cs`
- `Models/AudioControlResult.cs`
- `Constants/AppConstants.cs`
- `LiteTubeDock.csproj`
- 必要に応じて音量維持用サービス・モデル
- `docs/ipc_commands.md`

# 実装内容（具体的変更指示）

## 1. Dock単位で希望音量を保持

LiteTubeDockプロセス内で、以下を保持する。

- `desiredVolumePercent`
- `desiredMutedState`

初期値は現在の実メディア状態または既存仕様に合わせる。

`set-volume` 成功時に `desiredVolumePercent` を更新する。

`set-muted` 成功時に `desiredMutedState` を更新する。

## 2. `set-volume` と希望音量の整合

`set-volume` を受信した場合は、対象media要素へ音量を設定した後、希望音量として保存する。

要求値と実適用値が異なる場合は、実適用値を応答へ返す。

音量変更だけで自動的にミュート状態を変更しない。ミュート連動はControl側から明示的に `set-muted` を送る前提とする。

## 3. 再生前に希望音量を再適用

`play` 実行前に、現在の主media要素へ以下を再適用する。

- `desiredVolumePercent`
- `desiredMutedState`

適用失敗時は、再生を続行するか失敗とするか既存設計に合わせて統一し、ログへ記録する。

急な100%再生を防ぐため、可能な限り音量適用確認後に再生する。

## 4. media要素差し替え時の再適用

以下で新しいvideo/audio要素を検出した場合、希望音量・希望ミュートを再適用する。

- `MutationObserver`
- `loadedmetadata`
- `canplay`
- `play`
- `emptied`
- `durationchange`
- `currentSrc` 変更
- 主media要素変更
- 広告から本編への切替
- 本編から広告への切替
- YouTube・Twitch内部遷移

既存 `MutePersistenceService` の仕組みを拡張するか、新しい音量維持処理へ整理する。

## 5. YouTube限定にしない

既存ミュート継続がYouTube限定であっても、希望音量の維持はYouTube・Twitch・任意URLのvideo/audio要素へ共通適用する。

サイト固有処理が必要な場合は共通処理と分離する。

## 6. 主media要素の選択

既存 `MediaControlService` の主media要素選択ロジックを利用する。

ただし、新しいmedia要素へ再適用する際に、非表示広告用要素や未使用要素へだけ適用し、実際に再生される要素へ反映されない状態を避ける。

必要に応じて以下を考慮する。

- 再生中
- 可視状態
- readyState
- currentSrc
- サイズ
- muted
- volume
- 最終再生イベント時刻

## 7. 一時停止解除時

一時停止状態から `play` を受信した場合、再生前に希望音量・希望ミュートを再適用する。

例：

- 希望音量30%
- 一時停止中
- `play`
- 30%を確認
- 再生開始

## 8. 動画変更時

動画URL、mediaIdentity、mediaRevision、主media要素が変化しても、希望音量は維持する。

動画変更で希望音量を100%へ初期化しない。

## 9. ミュート継続との整合

既存 `MutePersistenceService` の `desiredMutedState` と競合しないようにする。

Control側から `set-muted false` を受信した場合は、ミュート継続状態も解除する。

Control側から `set-muted true` を受信した場合は、ミュート継続状態を有効にする。

## 10. 実音量の再確認

希望音量再適用後、実media要素の以下を再取得する。

- `volume`
- `muted`

要求値と実値が異なる場合は再試行または失敗ログを出す。

無限再試行はしない。

## 11. `get-audio-status`

引き続き呼び出し時点のDOM実値を返す。

希望音量と実音量を混同しない。

必要なら追加項目として以下を返してよい。

- `desiredVolumePercent`
- `desiredMutedState`

既存クライアントとの互換性を壊さない。

## 12. ログ

以下を記録する。

- PID
- mediaIdentity
- mediaRevision
- requestedVolume
- desiredVolume
- actualVolume
- desiredMuted
- actualMuted
- 再適用理由
- 対象media要素情報
- 成功/失敗
- durationMs

定期成功ログは大量出力しない。

## 13. 既存機能を壊さない

以下を維持する。

- `get-audio-status`
- `set-volume`
- `set-muted`
- `play`
- `pause`
- `seek-to`
- `seek-to-start`
- ミュート継続
- YouTubeログイン状態
- フルスクリーンIPC
- 既存の主media要素選択

## 14. バージョン確認

実装完了時に以下を一致させる。

- `Constants/AppConstants.cs`
- `LiteTubeDock.csproj`
  - `Version`
  - `AssemblyVersion`
  - `FileVersion`
  - `InformationalVersion`
- アプリ内表示
- ログ上のバージョン
- exe・dllのファイルバージョン

# 受け入れ条件（目視確認基準）

1. Controlから音量30%を設定すると、対象Dockの実音量が30%になる。
2. 30%のまま一時停止して再生しても、100%で鳴らない。
3. 再生前に希望音量30%が再適用される。
4. YouTubeで動画変更後も30%を維持する。
5. YouTube広告切替後も30%を維持する。
6. Twitchで配信・広告・画面遷移後も設定音量を維持する。
7. 新しいvideo/audio要素へ希望音量が再適用される。
8. `set-muted false` 後にミュート継続処理で再ミュートされない。
9. 同じ動画を複数Dockで再生しても、Dockごとの希望音量が独立する。
10. `get-audio-status` は実音量を返す。
11. Debugビルドが警告0・エラー0で成功する。
12. Releaseビルドが警告0・エラー0で成功する。
13. バージョン定義が一致する。
