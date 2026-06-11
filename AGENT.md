# AGENT.md

## プロジェクト名

LiteTube Dock

## 概要

LiteTube Dock は、Windows デスクトップ上で YouTube や登録済みURLを、専用の1ウィンドウで開きっぱなしにするための軽量Webプレイヤーです。

このアプリは通常ブラウザの代替ではなく、作業BGM・YouTubeライブ・登録URLの簡易表示を目的とした専用アプリです。

使用技術は以下です。

- .NET 8
- WPF
- C#
- Microsoft Edge WebView2
- JSON形式の設定ファイル

## 最重要ルール

- 明示的な指示がない限り、UIを再設計しないこと。
- 既存の `MainWindow.xaml` のレイアウトをできるだけ維持すること。
- 既存の `SettingsWindow.xaml` のレイアウトをできるだけ維持すること。
- 既存の `x:Name` を維持すること。
- 明確な理由なしに既存UI要素を削除・リネームしないこと。
- WPFプロジェクトを Windows Forms、WinUI、MAUI、Avalonia、Electron などへ置き換えないこと。
- タブブラウジング機能は実装しないこと。
- YouTubeの広告ブロックは実装しないこと。
- YouTubeのUI改変は実装しないこと。
- GoogleアカウントのID、パスワード、認証トークン、認証情報をアプリ側に保存しないこと。
- YouTubeのログイン状態は、WebView2標準の Cookie / Session / UserDataFolder 管理に任せること。

## 作業ディレクトリ

作業対象のプロジェクトディレクトリは以下です。

```text
E:\Dev\LiteTubeDock
```

作業対象は `E:\Dev\LiteTubeDock` 配下のみです。

`E:\Dev\LiteTubeDock` の外側にあるファイルやフォルダは編集しないこと。

手動バックアップは以下に存在します。

```text
E:\Dev\backup_LiteTubeDock
```

このバックアップフォルダは参照・編集・削除しないこと。  
明示的な指示がない限り、一切触らないこと。

## YouTubeログイン・ログアウト方針

LiteTube Dock は、YouTube / Google アカウントのログイン・ログアウト機能をアプリ側で独自実装しません。

### ログインについて

ログインして使いたいユーザーは、LiteTube Dock 内で YouTube トップ画面を開き、YouTube標準のログイン画面から手動でログインします。

ログインしないユーザーは、未ログイン状態のまま利用します。

### ログアウトについて

ログアウトしたい場合は、YouTube画面上のアカウントメニューからユーザー自身がログアウトします。

LiteTube Dock側に独自のログアウトボタンやログアウト処理を実装しないこと。

### 自動ログイン禁止

以下は実装禁止です。

- Google IDをアプリ側に保存すること
- Googleパスワードをアプリ側に保存すること
- 認証トークンをアプリ側に保存すること
- Cookieを独自形式で保存・復元すること
- Googleログイン画面へIDやパスワードを自動入力すること
- ログインボタンを自動クリックすること
- Googleログイン処理を自動化すること

### ログイン状態の保持

ログイン状態の保持は、WebView2 の `UserDataFolder` に保存される標準の Cookie / Session に任せます。

推奨フォルダ:

```text
data\webview2-user-data
```

ログイン状態を完全にリセットしたい場合は、アプリを終了したうえで `data\webview2-user-data` を削除する運用とします。

## 現在のUI構造

現在のメインUIは `MainWindow.xaml` を基準とします。

重要な名前付き要素は以下です。

- `RootGrid`
- `TopMenu`
- `FileMenuItem`
- `ExitMenuItem`
- `ViewMenuItem`
- `AlwaysOnTopMenuItem`
- `BackMenuItem`
- `ForwardMenuItem`
- `ReloadMenuItem`
- `HomeMenuItem`
- `SettingsMenuItem`
- `OpenSettingsMenuItem`
- `ReloadSettingsMenuItem`
- `PlayerArea`
- `PlayerWebView`
- `FavoriteButtonPanel`
- `FavoriteButton01` through `FavoriteButton10`
- `StatusBarArea`
- `CurrentUrlText`
- `LoadingStatusText`
- `AlwaysOnTopStatusText`

処理を追加する場合は、これらの既存名を優先して使用すること。

## 設定画面UI構造

設定画面は `SettingsWindow.xaml` を基準とします。

現在の設定画面は、左カテゴリ選択型UIです。

重要な名前付き要素は以下です。

- `GeneralSettingsCategoryButton`
- `FavoriteButtonsCategoryButton`
- `GeneralSettingsPanel`
- `FavoriteButtonsSettingsPanel`
- `HomeUrlTextBox`
- `RestoreLastUrlCheckBox`
- `StartAlwaysOnTopCheckBox`
- `RestoreWindowStateCheckBox`
- `FavoriteLabelTextBox01` through `FavoriteLabelTextBox10`
- `FavoriteUrlTextBox01` through `FavoriteUrlTextBox10`
- `FavoriteEnabledCheckBox01` through `FavoriteEnabledCheckBox10`
- `SaveSettingsButton`
- `CancelSettingsButton`
- `ResetSettingsButton`

設定画面を変更する場合も、既存の左カテゴリ型レイアウトを大きく崩さないこと。

## 実装優先方針

実装は小さく安全な単位で行うこと。

初期実装の主な対象は以下です。

1. `PlayerWebView` で YouTube を表示する。
2. Home、Back、Forward、Reload を実装する。
3. JSONから最大10件のお気に入りURLボタンを読み込む。
4. お気に入りボタン押下で登録URLへ遷移する。
5. 以下の状態を保存・復元する。
   - 最後に開いていたURL
   - ウィンドウ位置
   - ウィンドウサイズ
   - 常に最前面状態
6. 常に最前面ON/OFFを実装する。
7. ステータスバーに現在URLと読み込み状態を表示する。
8. 設定JSONファイルが存在しない場合は自動作成する。
9. READMEに起動方法、設定ファイル、注意事項を記載する。

## データファイル

設定はJSON形式を使用します。

推奨ファイルは以下です。

```text
data\settings.json
data\bookmarks.json
```

これらのファイルが存在しない場合は、アプリ起動時に既定内容で自動作成すること。

## コーディング方針

- UIとロジックは可能な範囲で分離すること。
- 設定読み書きやブックマーク読み書きは、小さなServiceクラスに分けること。
- ユーザー固有の絶対パスをハードコードしないこと。
- ただし、READMEやドキュメント上の作業例としてパスを書くのは可。
- 既定URL、ファイル名、フォルダ名は定数化すること。
- アプリを軽量・単純に保つこと。
- 不要な依存パッケージを追加しないこと。
- 必要以上に複雑なアーキテクチャを導入しないこと。
- 複雑なMVVM化は、明示的に指示されるまで行わないこと。

## 実装しないこと

以下は明示的な指示がない限り実装しないこと。

- タブ機能
- 複数ウィンドウ機能
- YouTube広告ブロック
- YouTube UI改変
- YouTubeコメント自動投稿
- Google ID / パスワード / 認証トークンの保存
- Googleログインの自動化
- 独自ログインボタン
- 独自ログアウトボタン
- 音量操作
- ミニモード
- テーマ切り替え
- メモリ使用量表示
- UIの全面再設計

## 検証

変更後は以下を確認すること。

- プロジェクトが正常にビルドできること。
- アプリがクラッシュせず起動すること。
- YouTubeが `PlayerWebView` に表示されること。
- お気に入りボタンが表示されること。
- 基本ナビゲーションが動作すること。
- 設定ファイルが正しく作成・読み込みされること。
- 既存UIレイアウトが大きく崩れていないこと。
- Google ID、パスワード、認証トークンが `settings.json` や `bookmarks.json` に保存されていないこと。
