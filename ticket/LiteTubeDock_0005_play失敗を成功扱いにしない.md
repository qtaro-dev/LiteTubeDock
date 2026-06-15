# LiteTubeDock play失敗を成功扱いにしない

## 対象プロジェクト

`E:\Dev\LiteTubeDock`

※ `E:\Dev\LiteTubeDockControl` は変更しないこと。

## 目的

コードレビューで Medium 判定となった、`play()` の Promise 拒否がIPCレスポンスへ反映されず、実際には再生できていないのに成功扱いになる問題を修正する。

## 現象

`UnifiedPlayerControlService` では `target.play()` を呼び出しているが、Promiseの完了を待たずにレスポンスを返す経路がある。

そのため、以下のような再生失敗が発生しても、IPCやお気に入り再生側では成功扱いになる可能性がある。

- ブラウザの自動再生制限
- 広告中
- media要素未準備
- サイト側の再生拒否
- `play()` Promiseのreject

## 実装内容

### 1. 再生完了を確認する

`player-play` および旧互換の再生処理で、単に `play()` を要求しただけでは成功にしないこと。

以下のいずれかの方法で、実際に再生状態へ移行したことを確認する。

- JavaScript側で `await target.play()` する
- またはC#側で短い確認ループを行い、`paused == false` になったことを確認する

WebView2の `ExecuteScriptAsync` がPromiseオブジェクトを `{}` として返す過去不具合を再発させないこと。

### 2. Promise拒否を失敗として返す

`play()` がrejectされた場合は、成功レスポンスにしない。

最低限以下を区別する。

- `PLAY_REJECTED`
- `MEDIA_NOT_FOUND`
- `MEDIA_NOT_READY`
- `ADVERTISEMENT_ACTIVE`
- `PLAY_VERIFICATION_FAILED`

### 3. 成功条件

以下を満たした場合のみ再生成功とする。

- media要素が存在する
- `play()` 要求が受理される
- 実際に `paused == false` になる
- 必要に応じて `readyState` が再生可能状態
- キャンセルされていない

`play-requested` は中間状態であり、最終成功として扱わないこと。

### 4. お気に入り自動再生への反映

お気に入りの自動再生ONでシーク後に再生する場合も、同じ成功条件を使用する。

実際に再生できなかった場合は、再生成功ログを出さないこと。

### 5. IPCレスポンス

LiteTubeDockControl側が再生成功・失敗を正しく判断できるよう、レスポンスへ以下を含める。

- Success
- ErrorCode
- OperationResult
- IsPlaying
- IsPaused
- ActualState

### 6. ログ

以下を記録する。

- Command
- MediaFound
- ReadyState
- BeforePaused
- PlayRequested
- PromiseRejected
- RejectMessage
- AfterPaused
- VerificationAttempts
- Result
- ErrorCode

同一エラーを短時間に大量出力しないよう配慮する。

## 変更しないもの

- スタート位置指定
- シーク範囲判定
- 音量・ミュート制御
- お気に入り設定UI
- IPCコマンド名
- LiteTubeDockControl側のコード

## バージョン

今回はバージョン番号を変更しないこと。

`Version`、`AssemblyVersion`、`FileVersion`、`InformationalVersion` は現状維持とする。

## 受け入れ条件

1. `play()` のrejectが成功扱いにならない
2. `play-requested` を最終成功として扱わない
3. 実際に `paused == false` になった場合のみ成功になる
4. 自動再生制限などで再生できない場合は失敗レスポンスになる
5. お気に入り自動再生も同じ成功条件を使う
6. WebView2のPromise戻り値が `{}` になる不具合を再発させない
7. Debug / Releaseビルドが警告0・エラー0で成功する
8. LiteTubeDockControl側に変更が入っていない
9. バージョン番号が変更されていない

## 確認手順

1. LiteTubeDockを起動する
2. 通常のYouTube動画で `player-play` を実行する
3. 実際に再生され、成功レスポンスになることを確認する
4. 再生できない状態または自動再生制限下で `player-play` を実行する
5. 失敗レスポンスになることを確認する
6. お気に入りの自動再生ONで動画を開く
7. 実際に再生開始した場合のみ成功ログになることを確認する
8. ログにPromise拒否理由と最終状態が残ることを確認する

## 実装後の報告内容

- 変更したファイル
- 実際の原因
- Promise拒否の取得方法
- 再生成功の確認方法
- 追加・変更したエラーコード
- お気に入り自動再生への反映
- Debug / Releaseビルド結果
- 実YouTube確認結果
- 未対応事項
