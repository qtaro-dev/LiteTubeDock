# LiteTubeDock YouTube動画ID判定の対応URLを拡張

## 対象プロジェクト

`E:\Dev\LiteTubeDock`

※ `E:\Dev\LiteTubeDockControl` は変更しないこと。

## 目的

コードレビューで Low 判定となった、同一YouTube動画ID判定が主要URL形式を十分に扱えない問題を改善する。

現在対応している形式に加え、Shorts、live、youtube-nocookie形式でも同一動画判定できるようにする。

## 現在対応している主な形式

- `https://www.youtube.com/watch?v=VIDEO_ID`
- `https://youtu.be/VIDEO_ID`
- `https://www.youtube.com/embed/VIDEO_ID`

## 追加対応する形式

最低限、以下へ対応する。

- `https://www.youtube.com/shorts/VIDEO_ID`
- `https://www.youtube.com/live/VIDEO_ID`
- `https://www.youtube-nocookie.com/embed/VIDEO_ID`
- `https://youtube.com/watch?v=VIDEO_ID`
- `https://m.youtube.com/watch?v=VIDEO_ID`

可能であれば以下も確認する。

- `music.youtube.com/watch?v=VIDEO_ID`
- クエリやフラグメント付きURL
- プレイリストパラメータ付きURL
- `si` など共有用パラメータ付き `youtu.be`

## 実装内容

### 1. 動画ID抽出の共通化

`MainWindow.xaml.cs` などに複数の動画ID抽出処理がある場合は、共通サービスまたは共通ヘルパーへ集約する。

候補:

- `FavoritePlaybackUrlService`
- `YouTubeUrlService`
- 既存のURL解析サービス

同じURL形式に対して箇所ごとに判定結果が変わらないこと。

### 2. URL正規化

以下を適切に無視して動画IDを抽出する。

- クエリ順序
- 不要な共有パラメータ
- フラグメント
- 大文字小文字差が影響しないホスト名
- `www` の有無

### 3. 動画ID検証

抽出したIDが空や不正形式の場合は同一動画扱いにしない。

YouTube動画IDとして明らかに不正な値を受け入れないこと。

### 4. 同一動画判定

URL文字列が違っても動画IDが一致する場合は同一動画として扱う。

例:

- `watch?v=ABC`
- `youtu.be/ABC`
- `shorts/ABC`
- `live/ABC`
- `youtube-nocookie.com/embed/ABC`

同一動画の場合は既存仕様どおり、ページ再読み込みを省略して直接シークできること。

### 5. ログ

同一動画判定時に以下を記録する。

- RegisteredUrl
- CurrentUrl
- RegisteredVideoId
- CurrentVideoId
- RegisteredUrlType
- CurrentUrlType
- SameVideo
- ParseResult
- ParseFailureReason

## 変更しないもの

- 同一動画の直接シーク仕様
- スタート位置指定
- 再生・音量・ミュート制御
- お気に入りUI
- LiteTubeDockControl側のコード

## バージョン

今回はバージョン番号を変更しないこと。

`Version`、`AssemblyVersion`、`FileVersion`、`InformationalVersion` は現状維持とする。

## 受け入れ条件

1. `watch?v=` 形式から動画IDを取得できる
2. `youtu.be` 形式から動画IDを取得できる
3. `embed` 形式から動画IDを取得できる
4. `shorts` 形式から動画IDを取得できる
5. `live` 形式から動画IDを取得できる
6. `youtube-nocookie.com/embed` 形式から動画IDを取得できる
7. モバイルURLでも動画IDを取得できる
8. URL形式が違っても動画ID一致なら同一動画扱いになる
9. 動画ID抽出処理が共通化される
10. 不正URLや空IDを同一動画扱いにしない
11. Debug / Releaseビルドが警告0・エラー0で成功する
12. LiteTubeDockControl側に変更が入っていない
13. バージョン番号が変更されていない

## 確認手順

1. 同じ動画を `watch?v=` と `youtu.be` 形式で2つのお気に入りへ登録する
2. 片方を開いた状態でもう片方を押す
3. 同一動画として直接シークされることを確認する
4. 同じ動画のShorts URLと通常URLで同じ確認を行う
5. live URLと通常URLで同じ確認を行う
6. youtube-nocookie embed URLでも動画IDが一致することを確認する
7. 不正URLでは同一動画判定されないことを確認する

## 実装後の報告内容

- 変更したファイル
- 追加対応したURL形式
- 動画ID抽出の共通化方法
- URL正規化方法
- 不正IDの判定方法
- 追加ログ
- Debug / Releaseビルド結果
- 実YouTube確認結果
- 未対応URL形式
