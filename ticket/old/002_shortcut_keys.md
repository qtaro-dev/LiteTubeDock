# 共通前提（必ず遵守）

- AGENT.md が存在する場合、その内容を最優先で遵守させること
- ユーザーはコードを直接編集しない前提である
- 実装・修正はすべて CODEX が行う
- 既存設計を破壊しない
- 既存テーマ・ライブラリ構成を維持する
- ハードコード文字列は禁止
- ICommand構造を崩さない
- 作業対象は `E:\Dev\LiteTubeDock` 配下のみとすること
- `E:\Dev\LiteTubeDock` の外側にあるファイルやフォルダは一切編集しないこと
- `MainWindow.xaml` と `SettingsWindow.xaml` の既存レイアウト、既存 `x:Name` は原則維持すること
- UIを大きく作り直さず、既存UIに処理を接続する形を優先すること

# タスク名

ショートカットキー操作追加

# 目的

LiteTube Dock の操作性を上げるため、キーボードだけでお気に入り遷移・基本操作ができるようにする。

# 対象ファイル（推定可）

- `MainWindow.xaml`
- `MainWindow.xaml.cs`
- `Models/AppSettings.cs`
- `Services/SettingsService.cs`
- `data/settings.json`
- `README.md`

# 実装内容（具体的変更指示）

## 1. お気に入りボタンのショートカット

以下のキーをお気に入りボタンに割り当てる。

- `D1` / `NumPad1` → `FavoriteButton01`
- `D2` / `NumPad2` → `FavoriteButton02`
- `D3` / `NumPad3` → `FavoriteButton03`
- `D4` / `NumPad4` → `FavoriteButton04`
- `D5` / `NumPad5` → `FavoriteButton05`
- `D6` / `NumPad6` → `FavoriteButton06`
- `D7` / `NumPad7` → `FavoriteButton07`
- `D8` / `NumPad8` → `FavoriteButton08`
- `D9` / `NumPad9` → `FavoriteButton09`
- `D0` / `NumPad0` → `FavoriteButton10`

無効または未登録のボタンに対応するキーが押された場合は、何もしないこと。

## 2. F1〜F10の割り当て

以下もお気に入りボタンに割り当てる。

- `F1` → `FavoriteButton01`
- `F2` → `FavoriteButton02`
- `F3` → `FavoriteButton03`
- `F4` → `FavoriteButton04`
- `F5` → `FavoriteButton05`
- `F6` → `FavoriteButton06`
- `F7` → `FavoriteButton07`
- `F8` → `FavoriteButton08`
- `F9` → `FavoriteButton09`
- `F10` → `FavoriteButton10`

## 3. 基本操作ショートカット

以下を追加する。

- `Ctrl + R` → 再読み込み
- `Alt + Left` → 戻る
- `Alt + Right` → 進む
- `Ctrl + H` → ホーム
- `Ctrl + Q` → 終了

## 4. 設定化

ショートカットキーの有効/無効を `settings.json` に保存できるようにする。

例:

```json
{
  "enableShortcutKeys": true
}
```

初期値は `true` とする。

## 5. ステータス表示

ショートカットキーで遷移した場合も、通常のボタン押下と同じように `CurrentUrlText` と `LoadingStatusText` が更新されること。

# 受け入れ条件（目視確認基準）

- `dotnet build` でビルドエラーがない
- `1`〜`0` でお気に入り01〜10へ遷移できる
- `F1`〜`F10` でお気に入り01〜10へ遷移できる
- 無効ボタンに対応するキーを押してもクラッシュしない
- `Ctrl + R` で再読み込みできる
- `Alt + Left` で戻る
- `Alt + Right` で進む
- `Ctrl + H` でホームへ移動する
- `Ctrl + Q` で終了できる
- ショートカット操作後もステータスバーが更新される
