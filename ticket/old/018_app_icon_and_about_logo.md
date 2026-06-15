# チケット018: アプリアイコン設定とバージョン情報ロゴ表示

## 目的

LiteTube Dock のリソースフォルダに追加した画像素材を使い、アプリ本体のアイコンとバージョン情報画面の見栄えを整える。

## 前提

- 作業対象は `E:\Dev\LiteTubeDock` 配下のみとする。
- `E:\Dev\backup_LiteTubeDock` は編集しない。
- AGENT.md が存在する場合は、その内容を最優先で確認・遵守する。
- 既存設計、既存テーマ、既存ライブラリ構成を壊さない。
- 既存 `x:Name` は原則維持する。
- YouTube / Google のログイン、認証情報、広告、DOM操作に関する仕様には一切触れない。
- 今回は画面表示用リソース設定のみを対象とする。

## 対象ファイル

推定対象:

- `LiteTubeDock.csproj`
- `MainWindow.xaml`
- `Views` または `Windows` 配下の `AboutWindow.xaml`
- 必要に応じて `AboutWindow.xaml.cs`
- `Resources/Images/LiteTubeDock.ico`
- `Resources/Images/LiteTubeDock_AppIcon_256.png`
- `Resources/Images/LiteTubeDock_LogoLockup.png`

## 使用するリソース

すでに以下のファイルを配置済み。

```text
E:\Dev\LiteTubeDock\Resources\Images\LiteTubeDock.ico
E:\Dev\LiteTubeDock\Resources\Images\LiteTubeDock_AppIcon_256.png
E:\Dev\LiteTubeDock\Resources\Images\LiteTubeDock_LogoLockup.png
```

## 実装内容

### 1. アプリケーションアイコンを設定する

`LiteTubeDock.csproj` に、アプリケーションアイコンとして以下を指定する。

```xml
<ApplicationIcon>Resources\Images\LiteTubeDock.ico</ApplicationIcon>
```

既存の `PropertyGroup` がある場合は、既存構成に合わせて追記する。

### 2. ウィンドウアイコンを設定する

主要ウィンドウに以下のアイコンを設定する。

```text
Resources/Images/LiteTubeDock.ico
```

対象候補:

- `MainWindow`
- `SettingsWindow`
- `HelpWindow`
- `AboutWindow`

既存の `Icon` 指定がある場合は、今回のアイコンに統一する。

XAML上で指定できる場合は、可能な限りXAML側で指定する。

例:

```xml
Icon="Resources/Images/LiteTubeDock.ico"
```

パスが通らない場合は、WPFのリソース参照として正しく動く形式に調整する。

### 3. バージョン情報画面にロゴを表示する

`AboutWindow` に `LiteTubeDock_LogoLockup.png` を表示する。

表示位置は、画面上部のアプリ名付近を基本とする。

表示イメージ:

- 上部にロゴ画像
- その下に Version 0.1.1
- 概要、使用技術、開発者、開発支援、セキュリティ方針を既存通り表示

### 4. AboutWindow のレイアウトを崩さない

`AboutWindow` はスクロールなしで1画面表示する方針を維持する。

ロゴ追加によって縦に長くなりすぎる場合は、以下のどちらかで調整する。

- ロゴ表示サイズを控えめにする
- 余白を少し詰める

ただし、文字が詰まりすぎて読みづらくならないようにする。

### 5. 画像のビルド設定を確認する

画像ファイルがアプリ実行時に参照できるよう、必要に応じてビルド設定を確認・調整する。

想定:

- `LiteTubeDock.ico`
  - アプリケーションアイコンとして参照できること
- `LiteTubeDock_LogoLockup.png`
  - `AboutWindow` で表示できること
- `LiteTubeDock_AppIcon_256.png`
  - 今回未使用でも、今後用に残すこと

不要なコピー設定や重複登録は避ける。

## 受け入れ条件（目視確認基準）

### ビルド確認

- Visual Studio または `dotnet build` でビルドが成功する。
- 画像リソース参照エラーが出ない。
- XAML読み込みエラーが出ない。

### アプリアイコン確認

- LiteTube Dock の実行ファイルに `LiteTubeDock.ico` のアイコンが設定されている。
- 起動後、タスクバーのアイコンが LiteTube Dock のアイコンになっている。
- ウィンドウ左上のアイコンが LiteTube Dock のアイコンになっている。

### バージョン情報確認

- メニューから `ヘルプ` → `バージョン情報` を開ける。
- バージョン情報画面の上部に `LiteTubeDock_LogoLockup.png` が表示される。
- `Version 0.1.1` が表示される。
- 既存の概要、使用技術、開発者、開発支援、セキュリティ方針が消えていない。
- 画面内に収まり、スクロールなしで違和感なく見える。

### 既存機能確認

- MainWindow が通常通り起動する。
- Home / Back / Forward / Reload が動作する。
- お気に入りボタン表示が崩れていない。
- 設定画面、ヘルプ画面、バージョン情報画面が開ける。
- YouTube / Google のログイン、広告、DOM操作まわりの仕様変更が入っていない。
