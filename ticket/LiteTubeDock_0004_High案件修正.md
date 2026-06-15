# LiteTubeDock High案件修正

## 対象プロジェクト

`E:\Dev\LiteTubeDock`

※ `E:\Dev\LiteTubeDockControl` は変更しないこと。

## 目的

LiteTubeDock v0.2.1 のコードレビューで High 判定となった以下3件を修正する。

1. `player-set-volume` / `player-set-muted` の必須パラメータ欠落が成功扱いになる
2. `player-seek` / `seek-to` が範囲・実位置を確認せず成功扱いになる
3. 動画切替後に希望音量 / ミュートが即時再適用されない

表面的な修正ではなく、IPCの成功条件と実際のプレイヤー状態を一致させること。

---

## 1. 音量・ミュートIPCの必須パラメータ検証

### 現象

新IPCの以下コマンドで、必須値が未指定でもエラーにならず処理が進む。

- `player-set-volume`
- `player-set-muted`

未指定値がJavaScript側へ `null` として渡されると、以下の意図しない挙動になる可能性がある。

- `player-set-volume`
  - `null / 100 == 0` により音量0%へ変更される
- `player-set-muted`
  - `Boolean(null) == false` によりミュート解除される

さらに実際の状態と一致していなくても成功扱いになる可能性がある。

### 修正内容

#### C#側

`IpcCommandHandler` で以下を必須検証する。

- `player-set-volume`
  - `volumePercent` 必須
  - 0～100の数値のみ許可
  - 未指定、型不正、範囲外は失敗
- `player-set-muted`
  - `muted` 必須
  - boolのみ許可
  - 未指定、型不正は失敗

旧IPCの `set-volume` / `set-muted` と同等以上の検証にする。

#### JavaScript側

C#側を通過していても、JavaScript側でも防御する。

- `requestedVolumePercent == null` は `invalid-parameter`
- `requestedMuted == null` は `invalid-parameter`
- 不正値を暗黙変換しない
- `null` を0やfalseへ変換しない

#### 成功条件

以下を確認した場合のみ成功とする。

- 音量
  - 実際の `volume` が要求値と許容範囲内で一致
- ミュート
  - 実際の `muted` が要求値と一致

以下は成功扱いにしない。

- `volume-mismatch`
- `muted-mismatch`
- 値未指定
- 型不正
- media要素未検出
- 設定後の実状態不一致

### エラーコード

既存規約に合わせてよいが、最低限以下を区別する。

- `INVALID_PARAMETER`
- `MEDIA_NOT_FOUND`
- `VOLUME_MISMATCH`
- `MUTED_MISMATCH`

---

## 2. `player-seek` / `seek-to` の範囲・成功確認

### 現象

現在のシーク処理は、0以上の有限値であれば `currentTime` へ代入し、その時点で成功扱いになる可能性がある。

以下の場合でも成功レスポンスになる恐れがある。

- 通常動画の長さを超えている
- ライブのDVR範囲外
- seekable範囲外
- 広告中
- シーク不可
- ブラウザ側で位置がクランプされた
- 実際には要求位置へ移動していない

### 対象コマンド

- `player-seek`
- 旧互換IPC `seek-to`

両方で同じ判定規約を使うこと。

### 修正内容

#### シーク前判定

シーク前に以下を確認する。

- media要素が存在する
- 指定位置が有限値
- 指定位置が0以上
- 通常動画
  - `duration` が取得できる場合、指定位置が動画長未満
- ライブ / DVR
  - `seekable` 範囲内
- 広告中ではない
- シーク可能な状態

#### 範囲外

範囲外の場合は `currentTime` を変更せず失敗を返す。

例:

- `SEEK_OUT_OF_RANGE`
- `SEEK_NOT_SUPPORTED`
- `MEDIA_NOT_FOUND`
- `ADVERTISEMENT_ACTIVE`

#### シーク後確認

シーク命令後、実際の `currentTime` を再取得し、要求位置へ移動したか確認する。

- 短い間隔で複数回確認
- タイムアウトあり
- UIスレッドをブロックしない
- 許容誤差は既存のお気に入りシークと統一し、原則 ±2秒
- 実位置が許容範囲内に入った場合のみ成功

#### 成功条件

`seek-requested` の段階では成功にしない。

以下を満たした場合のみ成功。

- 範囲判定成功
- シーク命令成功
- 実位置が許容誤差内

### 共通化

お気に入りスタート位置側ですでに実装されている以下の考え方を、IPCシークでも共通利用できるよう整理する。

- duration判定
- seekable判定
- ライブ範囲判定
- 実位置確認
- 許容誤差
- エラーコード

同じロジックを別々に重複実装しないこと。

---

## 3. 動画切替後の希望音量・ミュート即時再適用

### 現象

NavigationCompleted後、希望音量または希望ミュートがある場合に処理は走るが、実際には `player-get-state` を呼んでいるだけで、現在のmedia要素へ希望状態を即時再適用していない可能性がある。

そのため、動画切替後に以下が発生し得る。

- YouTube側の既定音量へ戻る
- 前回ページの音量になる
- ミュートが外れる
- Control側表示と実音量がずれる

### 修正内容

#### 明示的な再適用処理を追加

NavigationCompleted後の処理で、状態取得だけでなく希望状態を実際に適用する。

実装方法は任せるが、以下のどちらかに統一する。

- 専用操作 `reapply-desired-state`
- または既存統合サービス内で明示的に `applyDesired(...)` を実行する経路

単なる `player-get-state` 呼び出しだけで終わらせないこと。

#### 適用対象

希望値が存在する場合、現在のmedia要素へ以下を即時適用する。

- `desiredVolumePercent`
- `desiredMutedState`

#### 適用タイミング

少なくとも以下のタイミングで再適用する。

- NavigationCompleted後
- 動画本編media要素の生成後
- mediaIdentity / mediaRevision変更時
- loadedmetadata
- canplay
- play / playing
- YouTube内の次動画切替後

ただしObserverやイベントを多重登録しないこと。

#### 成功確認

適用後に以下を確認する。

- 実音量が希望音量と一致
- 実ミュート状態が希望状態と一致

一致しない場合は一定回数再試行し、失敗をログへ残す。

#### Control管理モード

Control管理モード中はControl側の希望値を優先する。

管理解除後は、希望値を強制し続けないこと。

heartbeat切れや `player-clear-control-policy` 後に再適用処理が残らないようにする。

#### 旧サービスとの整理

以下の役割を確認し、同じmedia要素へ二重適用しないこと。

- `AudioPersistenceService`
- `MutePersistenceService`
- `UnifiedPlayerControlService`

必要に応じて以下を行う。

- 統合サービスへ一本化
- 旧サービスを互換用途のみに限定
- Observer / timer / event handler の重複解除
- 未使用コードの整理

---

## ログ

以下を追加または改善する。

### 音量・ミュート

- Command
- RequestedValue
- ActualValue
- ParameterPresent
- ValidationResult
- ApplyResult
- VerificationResult
- ErrorCode

### シーク

- Command
- RequestedPosition
- Duration
- SeekableStart
- SeekableEnd
- IsLive
- IsAdvertisement
- ActualPosition
- ToleranceSeconds
- VerificationAttempts
- Result
- ErrorCode

### 再適用

- Trigger
- DesiredVolume
- ActualVolume
- DesiredMuted
- ActualMuted
- MediaIdentity
- MediaRevision
- RetryCount
- Result
- ErrorCode

同一内容を短時間に大量出力しすぎないよう配慮する。

---

## 変更しないもの

- お気に入りスタート位置機能
- 同一動画の再シーク機能
- お気に入り設定UI
- プレイヤーモード
- 既存のコマンド名
- LiteTubeDockControl側のコード
- 既存設定ファイル形式

---

## バージョン

今回はバージョン番号を変更しないこと。

`Version`、`AssemblyVersion`、`FileVersion`、`InformationalVersion` は現状維持とする。

---

## 受け入れ条件

### 音量・ミュートIPC

1. `player-set-volume` で `volumePercent` 未指定は失敗になる
2. `player-set-muted` で `muted` 未指定は失敗になる
3. 未指定値によって音量0%やミュート解除が発生しない
4. 型不正・範囲外を拒否する
5. 実状態不一致を成功扱いにしない

### シークIPC

6. 動画長を超える位置は失敗になる
7. ライブのseekable範囲外は失敗になる
8. シーク不可状態は失敗になる
9. 実際に指定位置へ移動した場合のみ成功になる
10. `player-seek` と `seek-to` で同じ成功条件になる
11. お気に入り側のシーク判定とロジックが矛盾しない

### 音量・ミュート再適用

12. 動画切替後に希望音量が即時再適用される
13. 動画切替後に希望ミュート状態が即時再適用される
14. Control側表示と実音量・実ミュートが一致する
15. Control管理解除後は強制再適用されない
16. Observerやイベントが多重登録されない
17. 旧サービスと統合サービスが競合しない

### 共通

18. Debug / Releaseビルドが警告0・エラー0で成功する
19. LiteTubeDockControl側に変更が入っていない
20. バージョン番号が変更されていない

---

## 確認手順

### A. 必須パラメータ不足

1. LiteTubeDockをIPC有効で起動する
2. `player-set-volume` を `volumePercent` なしで送る
3. 失敗レスポンスになることを確認する
4. 実音量が0%へ変化しないことを確認する
5. `player-set-muted` を `muted` なしで送る
6. 失敗レスポンスになることを確認する
7. 実ミュート状態が勝手に解除されないことを確認する

### B. シーク範囲外

1. 2分程度の通常動画を開く
2. `player-seek` で10分を指定する
3. 失敗レスポンスになることを確認する
4. 実再生位置が変更されていないことを確認する

### C. 正常シーク

1. 2分以上の通常動画を開く
2. `player-seek` で54秒を指定する
3. 54秒付近へ移動することを確認する
4. 実位置確認後に成功レスポンスになることを確認する

### D. 動画切替後の音量維持

1. Controlから音量30%、ミュートOFFを設定する
2. YouTubeで次動画へ切り替える
3. 新しい動画でも実音量30%、ミュートOFFになることを確認する
4. Control側表示も一致することを確認する

### E. ミュート維持

1. ControlからミュートONを設定する
2. YouTubeで次動画へ切り替える
3. 新しい動画でもミュートONが維持されることを確認する

### F. 管理解除

1. Control管理モード中に音量30%を設定する
2. `player-clear-control-policy` を送る
3. 以後、Control希望値が強制再適用されないことを確認する

---

## 実装後の報告内容

- 変更したファイル
- High 3件それぞれの実際の原因
- 必須パラメータ検証方法
- 音量・ミュートの成功確認方法
- シーク前の範囲判定方法
- シーク後の実位置確認方法
- 動画切替後の再適用方式
- 旧サービスとの役割整理
- 追加・変更したエラーコード
- 追加ログ
- Debug / Releaseビルド結果
- 実YouTube確認結果
- LiteTubeDockControlとの連携確認結果
- 未対応事項
