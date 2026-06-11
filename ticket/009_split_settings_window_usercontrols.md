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
- `MainWindow.xaml` と `SettingsWindow.xaml` の既存レイアウト、既存 `x:Name` は可能な限り維持すること
- UIを大きく作り直す場合は、既存機能を壊さない目的での構造整理に限定すること
- YouTube / Google のログイン・ログアウト機能をアプリ側で独自実装しないこと
- Google ID、パスワード、認証トークンを保存しないこと

# タスク名

チケット009 設定画面をカテゴリ別UserControlへ分割

# 目的

現在の `SettingsWindow.xaml` は、基本設定とお気に入りボタン設定の内容が1つのXAMLファイルに集約されている。

その結果、以下の問題が発生している。

- XAMLが長くなり、手動編集しづらい
- Visual Studioのデザインビューで、お気に入りボタン設定だけを確認しづらい
- AI修正時に設定画面全体が崩れやすい
- お気に入りボタン設定のUIが複雑化し、1枚のXAML内での保守が難しい

このため、`SettingsWindow.xaml` を設定画面の外枠専用にし、各設定カテゴリの中身を別UserControlへ分割する。

最終的には、以下のように分離する。

```text
SettingsWindow.xaml
├─ 左カテゴリメニュー
└─ 右側ContentControl
   ├─ GeneralSettingsView.xaml
   └─ FavoriteButtonsSettingsView.xaml
```

これにより、基本設定とお気に入りボタン設定を個別のXAMLファイルとしてVisual Studioデザインビューで確認・編集しやすくする。

# 対象ファイル（推定可）

- `SettingsWindow.xaml`
- `SettingsWindow.xaml.cs`
- `Views/GeneralSettingsView.xaml`
- `Views/GeneralSettingsView.xaml.cs`
- `Views/FavoriteButtonsSettingsView.xaml`
- `Views/FavoriteButtonsSettingsView.xaml.cs`
- `Models/AppSettings.cs`
- `Models/BookmarkItem.cs`
- `Services/SettingsService.cs`
- `Services/BookmarkService.cs`
- `README.md`

必要に応じて、`Views` フォルダを新規作成すること。

# 実装内容（具体的変更指示）

## 1. 作業開始前の確認

- 必ず `AGENT.md` を読むこと。
- 作業対象は `E:\Dev\LiteTubeDock` 配下のみとすること。
- `E:\Dev\LiteTubeDock` の外側にあるファイルやフォルダは一切編集しないこと。
- 既存の設定保存・読み込み・キャンセル・初期値リセット処理を壊さないこと。
- MainWindow側の機能は変更しないこと。
- 今回は設定画面の構造整理を主目的とし、不要な新機能追加はしないこと。

## 2. Viewsフォルダを作成する

以下のフォルダを作成する。

```text
Views
```

その中に以下のUserControlを追加する。

```text
Views/GeneralSettingsView.xaml
Views/GeneralSettingsView.xaml.cs
Views/FavoriteButtonsSettingsView.xaml
Views/FavoriteButtonsSettingsView.xaml.cs
```

## 3. SettingsWindow.xamlを外枠専用にする

`SettingsWindow.xaml` は以下の役割に絞る。

- 設定ウィンドウ全体の外枠
- 左側カテゴリボタン
  - 基本設定
  - お気に入りボタン
- 右側の表示領域
- 下部ボタン
  - 初期値に戻す
  - キャンセル
  - 保存

右側の表示領域には `ContentControl` を配置する。

推奨 `x:Name`:

```text
SettingsContentControl
```

## 4. 基本設定をGeneralSettingsViewへ移動する

現在 `SettingsWindow.xaml` にある基本設定パネルの中身を、`GeneralSettingsView.xaml` へ移動する。

対象となる主なUI:

- `HomeUrlTextBox`
- `RestoreLastUrlCheckBox`
- `StartAlwaysOnTopCheckBox`
- `RestoreWindowStateCheckBox`
- `WindowSizePresetComboBox`
- `WindowWidthTextBox`
- `WindowHeightTextBox`
- `ResetWindowBoundsButton`

既存の `x:Name` は可能な限り維持すること。

## 5. お気に入りボタン設定をFavoriteButtonsSettingsViewへ移動する

現在 `SettingsWindow.xaml` にあるお気に入りボタン設定の中身を、`FavoriteButtonsSettingsView.xaml` へ移動する。

対象となる主なUI:

- `FavoriteLabelTextBox01` 〜 `FavoriteLabelTextBox10`
- `FavoriteUrlTextBox01` 〜 `FavoriteUrlTextBox10`
- `FavoriteIconPathTextBox01` 〜 `FavoriteIconPathTextBox10`
- `FavoriteIconSelectButton01` 〜 `FavoriteIconSelectButton10`
- `FavoriteBackgroundColorTextBox01` 〜 `FavoriteBackgroundColorTextBox10`
- `FavoriteForegroundColorTextBox01` 〜 `FavoriteForegroundColorTextBox10`
- `FavoriteBackgroundColorButton01` 〜 `FavoriteBackgroundColorButton10`
- `FavoriteForegroundColorButton01` 〜 `FavoriteForegroundColorButton10`
- `FavoriteIconShapeComboBox01` 〜 `FavoriteIconShapeComboBox10`
- `FavoriteIconRoundedCheckBox01` 〜 `FavoriteIconRoundedCheckBox10`
- `FavoriteEnabledCheckBox01` 〜 `FavoriteEnabledCheckBox10`

既存の `x:Name` は可能な限り維持すること。

## 6. 左カテゴリボタンの切り替え処理

`SettingsWindow.xaml` の左カテゴリボタンを押した時、`SettingsContentControl` に対応するUserControlを表示する。

動作:

- `GeneralSettingsCategoryButton` 押下
  - `GeneralSettingsView` を表示

- `FavoriteButtonsCategoryButton` 押下
  - `FavoriteButtonsSettingsView` を表示

初期表示は `GeneralSettingsView` とする。

## 7. データの受け渡し方針

既存の設定保存・読み込み処理を壊さないこと。

実装しやすい方式を選んでよいが、以下のいずれかで整理する。

### 推奨案A: SettingsWindowが親として値を集約する

- `SettingsWindow` が `AppSettings` と `BookmarkItem` を保持する
- `GeneralSettingsView` に基本設定を読み込ませる
- `FavoriteButtonsSettingsView` にお気に入り一覧を読み込ませる
- 保存ボタン押下時に、各Viewから値を取得して既存保存処理へ渡す

### 推奨案B: 各UserControlにLoad/Collectメソッドを持たせる

例:

```csharp
GeneralSettingsView.LoadSettings(AppSettings settings)
GeneralSettingsView.CollectSettings(AppSettings settings)

FavoriteButtonsSettingsView.LoadBookmarks(IReadOnlyList<BookmarkItem> bookmarks)
FavoriteButtonsSettingsView.CollectBookmarks()
```

方式は既存コードと相性がよいものを選ぶこと。

## 8. 既存機能を維持する

UserControl分割後も、以下の機能は維持すること。

### 基本設定

- ホームURL編集
- 前回URL復元ON/OFF
- 常に最前面で起動ON/OFF
- ウィンドウ位置とサイズ復元ON/OFF
- ウィンドウサイズプリセット選択
- ウィンドウ幅・高さ入力
- ウィンドウ位置・サイズ初期化

### お気に入り設定

- 表示名編集
- URL編集
- アイコンパス編集
- アイコン選択ボタン
- 背景色編集
- 背景色カラーピッカー
- 文字色編集
- 文字色カラーピッカー
- アイコン形状選択
- 角丸ON/OFF
- 有効ON/OFF

### 下部ボタン

- 保存
- キャンセル
- 初期値に戻す

## 9. Visual Studioデザインビューで見やすくする

今回の目的は、カテゴリごとにデザインビューで確認しやすくすることである。

確認しやすい状態:

- `GeneralSettingsView.xaml` を開けば基本設定だけが見える
- `FavoriteButtonsSettingsView.xaml` を開けばお気に入りボタン設定だけが見える
- `SettingsWindow.xaml` には外枠とカテゴリ切り替えのみが見える

`d:DesignHeight` / `d:DesignWidth` などのデザイン時属性を追加してもよい。

例:

```xml
d:DesignHeight="600"
d:DesignWidth="1200"
```

ただし、実行時のレイアウトに悪影響を与えないこと。

## 10. 既存x:Name参照の整理

UserControlへ移動すると、`SettingsWindow.xaml.cs` から直接参照していたUI要素が参照できなくなる可能性がある。

その場合は、以下のように整理する。

- UI要素を外部から直接触るのではなく、UserControlにメソッドを用意する
- `SettingsWindow` はUserControlの公開メソッドを呼ぶ
- 不要な直接UI参照を減らす

例:

```csharp
_generalSettingsView.LoadSettings(_settings);
var updatedSettings = _generalSettingsView.GetSettings();

_favoriteButtonsSettingsView.LoadBookmarks(_bookmarks);
var updatedBookmarks = _favoriteButtonsSettingsView.GetBookmarks();
```

既存の保存処理を壊さないことを優先する。

## 11. README更新

READMEに以下を簡単に追記する。

- 設定画面はカテゴリごとにUserControlへ分割されていること
- 基本設定とお気に入りボタン設定は個別XAMLで管理していること

## 12. 実装しないこと

今回のタスクでは以下を実装しない。

- お気に入り設定UIの大幅デザイン再作成
- お気に入り件数の増減
- ドラッグ＆ドロップ並び替え
- MainWindowのお気に入りボタン表示仕様変更
- 新しい設定カテゴリ追加
- タブ式設定画面への変更
- YouTube UI改変
- Googleログイン自動化

# 受け入れ条件（目視確認基準）

## ビルド確認

- `dotnet build` でビルドエラーがない
- 警告が出る場合は内容を報告すること
- アプリ起動時にクラッシュしない

## ファイル構成確認

- `Views/GeneralSettingsView.xaml` が存在する
- `Views/GeneralSettingsView.xaml.cs` が存在する
- `Views/FavoriteButtonsSettingsView.xaml` が存在する
- `Views/FavoriteButtonsSettingsView.xaml.cs` が存在する
- `SettingsWindow.xaml` が外枠中心の構成になっている

## Visual Studioデザインビュー確認

- `GeneralSettingsView.xaml` を開くと、基本設定だけがデザインビューで確認できる
- `FavoriteButtonsSettingsView.xaml` を開くと、お気に入りボタン設定だけがデザインビューで確認できる
- `SettingsWindow.xaml` を開くと、左カテゴリと右表示領域の外枠が確認できる

## 設定画面動作確認

- `設定` → `設定を開く` で設定画面が開く
- 初期表示で基本設定が表示される
- 左側の `基本設定` を押すと基本設定が表示される
- 左側の `お気に入りボタン` を押すとお気に入り設定が表示される
- カテゴリ切り替え時にアプリがクラッシュしない

## 基本設定機能確認

- ホームURLを変更できる
- 前回URL復元ON/OFFを変更できる
- 常に最前面で起動ON/OFFを変更できる
- ウィンドウ位置とサイズ復元ON/OFFを変更できる
- ウィンドウサイズプリセットを選べる
- ウィンドウ幅・高さを編集できる
- ウィンドウ位置・サイズ初期化ボタンが動作する

## お気に入り設定機能確認

- 表示名を編集できる
- URLを編集できる
- アイコンパスを編集できる
- アイコン選択ボタンが動作する
- 背景色・文字色のカラーピッカーが動作する
- 形状を選択できる
- 角丸ON/OFFを設定できる
- 有効ON/OFFを設定できる

## 保存・キャンセル確認

- 保存ボタンで設定が保存される
- 保存後、MainWindowへ設定が反映される
- キャンセルボタンでは保存せず閉じる
- 初期値に戻すボタンが壊れていない

## 既存機能確認

- MainWindowのYouTube表示が壊れていない
- お気に入りボタンでURL遷移できる
- 背景色・文字色がMainWindowへ反映される
- アイコン表示が壊れていない
- フルスクリーン切り替えが壊れていない
- ウィンドウサイズプリセットが壊れていない
- ウィンドウ位置・サイズリセットが壊れていない
- YouTubeログイン情報、Google ID、パスワード、認証トークンは保存されていない
