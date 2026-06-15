# 共通前提（必ず遵守）

- AGENT.md が存在する場合、その内容を最優先で遵守させること
- ユーザーはコードを直接編集しない前提である
- 実装・修正はすべて CODEX が行う
- 既存設計を破壊しない
- 既存テーマ・ライブラリ構成を維持する
- ハードコード文字列は禁止
- ICommand構造を崩さない

# タスク名

LiteTubeDock 音量・ミュートIPC応答形式の修正

# 目的

LiteTubeDockControl から音量変更・ミュート変更・状態取得を行った際に発生する
`SCRIPT_EXECUTION_FAILED`
`The JSON value could not be converted to System.String. Path: $`
を解消する。

LiteTubeDock側の音量・ミュート関連IPCについて、JavaScript実行結果とC#側の戻り値型を一致させ、Control側が安定して解析できる明確な応答形式へ統一する。

# 対象ファイル（推定可）

- LiteTubeDock のIPCコマンド受信処理
- WebView2上でJavaScriptを実行する処理
- 音量設定・音量取得処理
- ミュート設定・ミュート取得処理
- IPCレスポンス用Model
- `docs/ipc_commands.md`
- バージョン定義ファイル
- `LiteTubeDock.csproj`

# 実装内容（具体的変更指示）

1. 音量・ミュート関連IPCを調査する
   - LiteTubeDockControlから呼び出されているコマンド名を確認する。
   - 各コマンドで実行しているJavaScriptと、その生の戻り値を確認する。
   - 数値、真偽値、文字列、JSONオブジェクトのどれが返るかを整理する。
   - `System.String` への固定変換が存在する場合は見直す。

2. 応答形式を統一する
   - 音量値は数値型として扱う。
   - ミュート状態は真偽値として扱う。
   - 成功可否、返却値、エラー内容を明確に表現できる既存設計に沿ったレスポンス形式へ統一する。
   - 既存IPCコマンドの互換性を壊さない。
   - 不要な二重JSON化や文字列化を行わない。

3. 以下のIPC操作を正常化する
   - 音量設定
   - 現在音量取得
   - ミュート設定
   - 現在ミュート状態取得

4. 実際のプレイヤーへ反映されることを確認する
   - 音量50%指定で実際の音量が変化する。
   - 音量0%指定で実際に無音になる。
   - ミュートONで実際に無音になる。
   - ミュートOFFで元の音量状態へ戻る。

5. ログを改善する
   - 失敗時に、IPCコマンド名、JavaScriptの生戻り値、変換対象型、例外詳細をログへ残す。
   - IPC利用側へは解析可能なエラー応答を返す。
   - UI向け表示文言はLiteTubeDockControl側の責務とし、本体側で長い例外文を直接表示しない。

6. IPC仕様書を更新する
   - 音量設定・取得、ミュート設定・取得について、引数と戻り値型を明記する。
   - 数値・真偽値・文字列の扱いを曖昧にしない。
   - LiteTubeDockControl側が同じ仕様を参照できるようにする。

7. バージョン整合性を確認する
   - アプリ内表示用バージョンと `LiteTubeDock.csproj` の以下を一致させる。
     - `Version`
     - `AssemblyVersion`
     - `FileVersion`
     - `InformationalVersion`

# 受け入れ条件（目視確認基準）

1. LiteTubeDockを1台起動し、音量設定IPCで実際の音量が変化する。
2. 音量0%指定で実際に無音になる。
3. ミュートON/OFFが実際のプレイヤーへ反映される。
4. 現在音量取得で数値が返る。
5. 現在ミュート状態取得で真偽値が返る。
6. `SCRIPT_EXECUTION_FAILED` が発生しない。
7. `The JSON value could not be converted to System.String. Path: $` が発生しない。
8. `docs/ipc_commands.md` に引数・戻り値型が記載されている。
9. LiteTubeDockがビルド成功する。
10. アプリ内表示用バージョンとcsprojの各バージョン値が一致している。
