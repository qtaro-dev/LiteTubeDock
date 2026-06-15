# 調査：LiteTubeDock側のページ内音量変更検知とIPC応答を確認

## 対象プロジェクト
`E:\Dev\LiteTubeDock`

## 目的
YouTube・Twitchなどのページ内プレイヤーで音量スライダーを変更した際、その変更をLiteTubeDock側が確実に検知し、`get-audio-status` へ正しい実音量・ミュート状態を返しているかを確認する。

今回は調査のみとし、コード修正は行わない。

## 対象ファイル（推定可）
- `Services/MediaControlService.cs`
- `Services/IpcCommandHandler.cs`
- `Models/AudioControlResult.cs`
- `Models/IpcAudioStatusData.cs`
- `MainWindow.xaml.cs`
- WebView2内のvideo/audio監視スクリプト
- `get-audio-status`
- `set-volume`
- `set-muted`
- `volumechange` 関連処理

## 調査内容
1. ページ内プレイヤーで音量を変更した際、`volumechange` を監視しているか。
2. `volumechange` 発生後に、実際のvideo/audio要素から `volume` と `muted` を再取得しているか。
3. `get-audio-status` は呼び出し時点のDOM上の実値を毎回取得して返しているか。
4. 過去のキャッシュ値やDesired状態を返していないか。
5. 複数のvideo/audio要素がある場合、どの要素を主メディアとして選択しているか。
6. YouTube広告、動画変更、Twitch画面遷移などで要素が差し替わった後、監視を付け直しているか。
7. `set-volume` が指定されたPipeNameのプレイヤーだけへ反映されているか。
8. 同じURLや同じ動画を複数Dockで再生した場合に、音量状態を共有してしまう処理がないか。

## 報告内容
- プレイヤー側の音量変更を確実に検知する実装が存在するか
- `volumechange` の監視有無
- `get-audio-status` の音量取得元
- 主メディア要素の選択方法
- メディア要素差し替え後の再監視有無
- YouTubeとTwitchで挙動が異なる可能性
- 原因となるファイル名、メソッド名、該当処理
- 修正が必要な場合のLiteTubeDock側の修正案
- 調査中にコードを変更していないこと

## 受け入れ条件
- LiteTubeDock側のみを調査している
- コード変更を行っていない
- `volumechange` の監視有無が明記されている
- `get-audio-status` の音量取得元が明記されている
- 主メディア要素の選択方法が明記されている
- メディア要素差し替え後の再監視有無が明記されている
- 原因候補を該当ファイル・メソッドとともに報告している
