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

ウィンドウサイズプリセット追加

# 目的

用途に応じて、LiteTube Dock のウィンドウサイズを簡単に切り替えられるようにする。

# 対象ファイル（推定可）

- `MainWindow.xaml`
- `MainWindow.xaml.cs`
- `SettingsWindow.xaml`
- `SettingsWindow.xaml.cs`
- `Models/AppSettings.cs`
- `Models/WindowSettings.cs`
- `Services/SettingsService.cs`
- `data/settings.json`
- `README.md`

# 実装内容（具体的変更指示）

## 1. サイズプリセットを追加

以下のプリセットを実装する。

- `800x600`
- `960x540`
- `1280x720`
- `1600x900`
- `1920x1080`
- `Custom`

## 2. 表示メニューから選択できるようにする

`ViewMenuItem` 配下に、サイズ変更用のメニュー項目を追加する。

推奨名:

- `WindowSizeMenuItem`
- `WindowSize800x600MenuItem`
- `WindowSize960x540MenuItem`
- `WindowSize1280x720MenuItem`
- `WindowSize1600x900MenuItem`
- `WindowSize1920x1080MenuItem`

既存メニューを大きく作り直さず、項目追加で対応すること。

## 3. 設定画面にカスタムサイズを追加

基本設定パネルに以下を追加する。

- ウィンドウ幅
- ウィンドウ高さ

推奨名:

- `WindowWidthTextBox`
- `WindowHeightTextBox`

値が不正な場合は保存時にエラー表示または既定値に戻すこと。

## 4. 設定保存

`settings.json` にウィンドウサイズプリセットまたはカスタムサイズを保存する。

例:

```json
{
  "windowSizePreset": "1280x720",
  "window": {
    "width": 1280,
    "height": 720
  }
}
```

## 5. 範囲制限

カスタムサイズは以下の範囲に制限する。

- 最小幅: 640
- 最小高さ: 360
- 最大幅: 3840
- 最大高さ: 2160

# 受け入れ条件（目視確認基準）

- `dotnet build` でビルドエラーがない
- 表示メニューからサイズプリセットを選ぶとウィンドウサイズが変わる
- 設定画面で幅と高さを保存できる
- 再起動後に保存したサイズが復元される
- 不正なサイズ値でクラッシュしない
- 既存のウィンドウ位置・サイズ復元機能が壊れていない
