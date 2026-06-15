# LiteTubeDock お気に入り連続押下時のキャンセル処理を強化

## 対象プロジェクト

`E:\Dev\LiteTubeDock`

※ `E:\Dev\LiteTubeDockControl` は変更しないこと。

## 目的

コードレビューで Medium 判定となった、お気に入りボタンを素早く連続押下した際に、古いリクエストが後からエラー表示やループ設定などを適用する可能性を修正する。

## 現象

お気に入り再生処理ではRequestIdとCancellationTokenを使用しているが、各 `await` の直後や各操作の直前に最新リクエスト確認が不足している箇所がある。

そのため、先行リクエストがキャンセルされた後でも、以下が発生する可能性がある。

- 古いリクエスト由来の警告ダイアログ
- 古いお気に入りのループ設定再適用
- 古いミュート状態の再適用
- 古いシーク処理の後追い実行
- 最新の押下内容との競合

## 実装内容

### 1. 各await後に最新リクエストを確認

以下の各段階で、処理継続前に必ず確認する。

- 動画準備待機後
- 状態取得後
- ミュート適用後
- ループ適用後
- シーク後
- シーク検証後
- 再生 / 一時停止後

確認内容:

- `IsCurrentFavoritePlaybackRequest(request)`
- `cancellationToken.ThrowIfCancellationRequested()`

### 2. キャンセルを通常失敗として握りつぶさない

`UnifiedPlayerControlService.ExecuteAsync` などで `OperationCanceledException` を一般例外として失敗結果へ変換しないこと。

キャンセルは再throwし、呼び出し側で静かに終了させる。

### 3. 古いリクエストではUI通知しない

キャンセル済み、または最新でないリクエストの場合は以下を行わない。

- MessageBox表示
- エラーダイアログ
- 成功通知
- ループ設定適用
- ミュート設定適用
- 再生 / 一時停止
- シーク

### 4. ApplyFavoriteLoopAsyncの対応

`ApplyFavoriteLoopAsync` に以下を渡せるようにする。

- CancellationToken
- RequestId または FavoritePlaybackRequest

ループ適用前後で最新リクエストか確認する。

### 5. 最新押下を常に優先

お気に入りボタンを連続押下した場合、最後に押したボタンだけが最終状態を決定する。

古い処理が遅れて戻ってきても、最新状態を上書きしないこと。

### 6. ログ

以下を追加する。

- RequestId
- Slot
- Stage
- CancelRequested
- IsLatestRequest
- OperationSkipped
- SkipReason
- Result

古いリクエストをキャンセルした場合、エラーではなくキャンセルとして記録する。

## 変更しないもの

- 同一動画の直接シーク
- スタート位置指定
- 自動再生
- ミュート
- ループ
- 動画準備待機
- LiteTubeDockControl側のコード

## バージョン

今回はバージョン番号を変更しないこと。

`Version`、`AssemblyVersion`、`FileVersion`、`InformationalVersion` は現状維持とする。

## 受け入れ条件

1. お気に入りを連続押下しても最後の押下だけが有効になる
2. 古いリクエストの警告ダイアログが後から出ない
3. 古いループ設定が後から適用されない
4. 古いミュート設定が後から適用されない
5. 古いシーク処理が後から実行されない
6. `OperationCanceledException` を通常失敗として扱わない
7. 各await後に最新リクエスト確認がある
8. キャンセル時はUI通知せず静かに終了する
9. Debug / Releaseビルドが警告0・エラー0で成功する
10. LiteTubeDockControl側に変更が入っていない
11. バージョン番号が変更されていない

## 確認手順

1. 同じ動画で異なるスタート位置のお気に入りを2つ用意する
2. 1つ目を押してすぐ2つ目を押す
3. 最終的に2つ目のスタート位置へ移動することを確認する
4. 1つ目の警告や設定が後から出ないことを確認する
5. 自動再生・ミュート・ループが異なる2つのボタンを素早く交互に押す
6. 最後に押したボタンの設定だけが残ることを確認する
7. ログで古いRequestIdがキャンセル扱いになっていることを確認する

## 実装後の報告内容

- 変更したファイル
- 実際の競合原因
- 各await後に追加した確認箇所
- OperationCanceledExceptionの扱い
- ループ・ミュート・シークのキャンセル対応
- 追加ログ
- Debug / Releaseビルド結果
- 実YouTube確認結果
- 未対応事項
