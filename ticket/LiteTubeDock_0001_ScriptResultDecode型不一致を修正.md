# LiteTubeDock ScriptResultDecode型不一致を修正

## 対象プロジェクト

`E:\Dev\LiteTubeDock`

※ `E:\Dev\LiteTubeDockControl` は変更しないこと。

## 現象

お気に入りボタンのスタート位置へ `00:00:54` を設定してYouTube動画を開いても、指定位置へ移動できず、以下のエラーが表示される。

> 再生位置を確認できませんでした。時間をおいて再度お試しください。

ログでは、スタート位置適用待機中に毎回以下が記録されている。

```text
[IPC] Event=ScriptResultDecode
Service=UnifiedPlayerControl
TargetType=String
ExceptionType=JsonException
Message=The JSON value could not be converted to System.String.
Raw={}
```

その直後に `favorite-start-state` が失敗し、以下の状態になる。

- `Url=` 空
- `MediaFound=False`
- `ReadyState=` 空
- `Duration=` 空
- `Seekable=False`
- `Reason=video-element-not-found`

40回再試行しても同じ状態が続き、最終的に `FavoritePlaybackWaitTimeout` となる。

## 原因

JavaScript側がJSONオブジェクトを返しているのに、C#側が結果を `string` としてデシリアライズしようとしている可能性が高い。

そのため、動画状態の取得そのものが成功していても、C#側で状態モデルへ変換できず、空状態として扱われている。

待機時間やYouTubeの読み込み速度ではなく、`ScriptResultDecode` の戻り値型不一致が主原因と考えられる。

## 実装内容

### 1. `favorite-start-state` の戻り値型を修正

`UnifiedPlayerControlService` 内の `favorite-start-state` 取得処理を調査し、JavaScriptの戻り値とC#側の受け取り型を一致させる。

以下のどちらか一方へ統一すること。

#### 方式A: JavaScript側でJSON文字列を返す

- JavaScript側で `JSON.stringify(...)` を使用する
- C#側は一度文字列として受け取る
- その後 `UnifiedPlayerStateResult` などの状態モデルへデシリアライズする

#### 方式B: JSONオブジェクトとして直接受け取る

- JavaScript側はオブジェクトを返す
- C#側で `string` ではなく対象モデルまたは `JsonElement` として受け取る
- 必要なモデルへ安全に変換する

既存の共通デコード処理に合わせ、二重JSON化や二重デコードを避けること。

### 2. `{}` を成功扱いしない

戻り値が空オブジェクト `{}` の場合は、正常な動画状態として扱わない。

以下を区別する。

- スクリプト実行成功＋有効な状態取得
- スクリプト実行成功＋空オブジェクト
- スクリプト実行失敗
- JSON変換失敗
- video要素未検出

空オブジェクトの場合は、ログへ明確な理由を記録する。

例:

```text
Reason=empty-script-result
```

### 3. デコード失敗時のログを改善

`ScriptResultDecode` で例外が発生した場合、以下を記録する。

- 対象コマンド
- 想定していた型
- 実際のRaw値
- Raw値のJSON種別
- 使用したデコード経路
- 例外型
- 例外メッセージ

同じデコード例外が待機ループ中に大量発生する場合は、同一エラーを毎回重複出力しすぎないよう、必要に応じて抑制または要約する。

### 4. 状態モデルへの反映

デコード成功後、以下が正しく `UnifiedPlayerStateResult` へ入ることを確認する。

- CurrentUrl
- MediaFound
- ReadyState
- DurationSeconds
- Seekable
- SeekableRanges
- CurrentTimeSeconds
- IsLive
- IsAdvertisement
- MediaIdentity
- MediaRevision
- YouTube動画ID

ログ上で空欄ではなく、実値が確認できること。

### 5. スタート位置適用処理との連携

状態取得が成功した後、既存の待機・範囲判定・シーク処理をそのまま正しく通す。

- `00:00:54` が動画範囲内なら54秒付近へ移動
- シーク後の実位置を確認
- 許容誤差±2秒
- 自動再生ONなら再生
- 自動再生OFFなら一時停止
- 範囲外なら既存の範囲外エラー
- 状態取得不能なら既存の確認失敗エラー

### 6. 共通デコード処理への影響確認

`ScriptResultDecode` が共通処理の場合、他の統合IPCや状態取得へ悪影響を与えないことを確認する。

対象例:

- `player-get-state`
- `player-play`
- `player-pause`
- `player-seek`
- `player-set-volume`
- `player-set-muted`
- `favorite-start-state`

`favorite-start-state` だけ個別処理が必要な場合でも、同じ不具合を再発させないよう処理を共通化または明確に分離する。

## 変更しないもの

- スタート位置の保存形式
- `HH:mm:ss` 入力仕様
- 数字のみ入力の変換仕様
- 動画準備待機時間
- シーク成功の許容誤差
- 自動再生
- ミュート
- ループ
- プレイヤーモード
- LiteTubeDockControl側のIPC仕様

## バージョン

今回はバージョン番号を変更しないこと。

`Version`、`AssemblyVersion`、`FileVersion`、`InformationalVersion` は現状維持とする。

## 受け入れ条件

1. `favorite-start-state` で `TargetType=String` の型変換例外が発生しない
2. `Raw={}` を正常状態として扱わない
3. `UnifiedPlayerStateResult` に動画状態が正しく入る
4. ログ上で `CurrentUrl`、`MediaFound`、`ReadyState`、`Duration`、`Seekable` が実値になる
5. YouTube通常動画で `00:00:54` を指定すると54秒付近へ移動できる
6. シーク後の実位置確認が成功する
7. 範囲外エラーと状態取得失敗エラーが正しく区別される
8. `player-get-state` など既存の統合IPCが壊れていない
9. Debug / Releaseビルドが警告0・エラー0で成功する
10. LiteTubeDockControl側に変更が入っていない
11. バージョン番号が変更されていない

## 確認手順

### 型変換エラー確認

1. LiteTubeDockを起動する
2. スタート位置 `00:00:54` を設定したYouTubeお気に入りを押す
3. ログを開く
4. 以下のエラーが出ていないことを確認する

```text
Event=ScriptResultDecode
TargetType=String
The JSON value could not be converted to System.String
```

### 状態取得確認

1. 同じ操作を行う
2. `FavoritePlaybackWait` または関連ログを確認する
3. 以下が空欄ではなく実値になっていることを確認する

- Url
- MediaFound
- ReadyState
- Duration
- Seekable
- MediaIdentity

### 54秒シーク確認

1. 1分以上あるYouTube通常動画を登録する
2. スタート位置を `00:00:54` にする
3. 自動再生をONにする
4. お気に入りボタンを押す
5. 54秒付近へ移動して再生されることを確認する

### 自動再生OFF

1. 自動再生をOFFにする
2. 同じお気に入りボタンを押す
3. 54秒付近へ移動し、一時停止状態になることを確認する

### 既存IPC確認

1. LiteTubeDockControl 0.2系から対象Dockを検出する
2. 再生・一時停止・シーク・音量・ミュートを操作する
3. 既存統合IPCが正常に動作することを確認する

## 実装後の報告内容

- 変更したファイル
- 実際の原因
- 修正前のJavaScript戻り値
- 修正前のC#受け取り型
- 修正後のデコード方式
- 空オブジェクトの扱い
- 状態モデルへ取得できた値
- `00:00:54` の実YouTube確認結果
- 既存IPCへの影響確認結果
- Debug / Releaseビルド結果
- 未対応事項
