# mdview 基本設計書

## 1. 文書情報

- アプリケーション: mdview
- 目的: Markdown ファイルを素早く開いて読むための軽量なデスクトップ Viewer
- 対応 OS: Windows、macOS
- 対応 CPU: x64、ARM64
- UI: Avalonia UI 12.1.3
- 実行環境: .NET 10 LTS
- Markdown Parser: Markdig 1.4.0
- テスト: xUnit v3 / Microsoft Testing Platform

## 2. 製品方針

mdview は Markdown Editor ではなく、読み取り専用の Viewer とする。

- Markdown の編集、保存、Auto Save は行わない
- Markdown の内容を外部へ送信しない
- 外部画像を自動取得しない
- ユーザーがクリックした外部リンクだけを既定ブラウザーで開く
- UI と依存関係を小さく保ち、起動と閲覧を優先する
- 1つのファイルの問題でアプリケーション全体を終了させない

## 3. システム構成

Onion Architecture を採用し、依存方向を内側へ限定する。

```text
Presentation
    |
    v
Application
    |
    v
Domain

Infrastructure -- Application の抽象化を実装
```

### 3.1 プロジェクト構成

```text
src/
├── mdview.Domain/
├── mdview.Application/
├── mdview.Infrastructure/
└── mdview.Presentation/

tests/
├── mdview.Domain.Tests/
├── mdview.Application.Tests/
└── mdview.Infrastructure.Tests/
```

### 3.2 Domain

ファイルシステム、Avalonia、OS API、Markdig に依存しない中心モデルを配置する。

主なモデル:

- `MarkdownDocument`
- Markdown のドキュメント状態
- 表示用の基本的なドメイン値

### 3.3 Application

ユースケース、表示用モデル、外部環境への抽象化を配置する。

主な要素:

- `MarkdownDocumentLoader`
- `MarkdownTabManager`
- `MarkdownPathResolver`
- `MarkdownSearchState`
- `MarkdownZoomState`
- `IMarkdownFileReader`
- `IMarkdownFileWatcher`
- `IFileAssociationService`
- `MarkdownDocumentModel`

Application 層は Avalonia のコントロールや OS 固有 API を直接操作しない。

### 3.4 Infrastructure

外部環境との接続と外部ライブラリの実装を配置する。

- `MarkdownFileReader`: UTF-8 / UTF-8 BOM の読込
- `MarkdownFileWatcher`: `FileSystemWatcher` と debounce
- `FileAssociationService`: OS 固有のファイル関連付け
- `MarkdigMarkdownParser`: GFM 相当の Markdown 解析

Markdig の型は Application / Presentation に直接公開しない。

### 3.5 Presentation

Avalonia の Window、View、ViewModel、ユーザー操作を配置する。

- `MainWindow`: タブ、検索バー、文書領域、キーボード操作
- `MainWindowViewModel`: タブ、読込、再読込、検索、Zoom
- `MarkdownBlockView`: Markdown Block と描画 Control の接続
- `MarkdownBlockRenderer`: ネイティブ Control による描画
- `MarkdownLinkHandler`: 外部リンク、相対リンク、Anchor の処理

## 4. Markdown 処理

Markdown の処理は次の流れとする。

```text
Markdown ファイル
      |
      v
MarkdownFileReader
      |
      v
MarkdigMarkdownParser
      |
      v
MarkdownDocumentModel
      |
      v
MarkdownBlockRenderer
      |
      v
Avalonia Native Controls
```

### 4.1 対応要素

- 見出し
- 段落
- 太字、斜体、打ち消し線
- 順序付き / 順序なし / ネストされたリスト
- Task List
- 引用
- インラインコード
- fenced code block
- リンク
- 相対 Markdown リンク
- 相対画像
- テーブル
- 自動リンク
- 水平線
- HTML のテキスト表示

### 4.2 Heading Anchor

見出しのプレーンテキストを正規化して Anchor を生成する。

- 小文字化
- 空白をハイフンへ変換
- Anchor に使用できない文字を除去
- 同じ見出しが複数ある場合は `-1`、`-2` のように一意化

## 5. ファイル読込とタブ

### 5.1 ファイルを開く経路

- 起動時引数
- `Ctrl/Cmd + O`
- Avalonia StorageProvider のファイル選択

同じファイルは正規化したパスで比較し、新しいタブを作らず既存タブをアクティブにする。

### 5.2 タブ状態

タブは次の情報を持つ。

- ファイルパス
- 表示タイトル
- 表示用ドキュメント
- 読込済みの元テキスト
- `Loaded`、`Missing`、`Error` の状態

ファイルが削除されてもタブは閉じない。対象タブにエラー状態を表示し、同じパスにファイルが再作成された場合は再読込する。

### 5.3 タブを閉じる

保存確認は行わない。mdview は読み取り専用 Viewer のため、閉じたタブに隣接するタブを選択する。

## 6. ファイル変更監視

開いているファイルごとに `MarkdownFileWatcher` を作成する。

```text
FileSystemWatcher event
        |
        v
250ms debounce
        |
        v
ファイル存在確認
        |
        +--> 存在: 内容を再読込・再解析
        |
        +--> 不在: タブを Missing 状態にする
```

再読込前に元テキストと比較し、内容が同じ場合は再解析と再描画を行わない。

手動再読込:

- Windows: `Ctrl + R`
- macOS: `Cmd + R`

## 7. リンクと画像

### 7.1 外部リンク

HTTP / HTTPS の URL は、ユーザーがリンクをクリックした場合だけ OS の既定ブラウザーで開く。

### 7.2 相対 Markdown リンク

相対パスはリンク元 Markdown ファイルのディレクトリを基準に解決する。

- 対象ファイルを新しいタブで開く
- 既に開いている場合は既存タブをアクティブにする
- `#anchor` が付いている場合は対象見出しへ移動する

### 7.3 画像

画像の相対パスは Markdown ファイルを基準に解決する。

- ローカルファイルだけを読み込む
- HTTP / HTTPS の画像は取得しない
- ファイルがない、または読み込めない場合はプレースホルダーを表示する

## 8. 検索と Zoom

### 8.1 検索

`Ctrl/Cmd + F` で検索バーを表示し、入力欄へフォーカスする。

- 大文字小文字を区別しない
- 一致件数を表示する
- Enter で次の一致へ移動する
- Shift + Enter で前の一致へ移動する
- 前後ボタンを提供する
- `Esc` または閉じるボタンで検索バーを閉じる
- 一致文字列を文書内でハイライトする

### 8.2 Zoom

文書領域だけを拡大・縮小する。

- `Ctrl/Cmd + +`: 拡大
- `Ctrl/Cmd + -`: 縮小
- 最小倍率: 75%
- 最大倍率: 200%
- 初期倍率: 100%

## 9. ファイル関連付け

関連付け処理は `IFileAssociationService` を介して Infrastructure に隔離する。

Windows ではユーザー単位の `HKCU` に `.md`、`.markdown`、`.mdown` の関連付けを登録できる。ただし、ユーザーの明示的な同意なしに登録処理を呼び出してはいけない。

macOS の既定アプリ変更は、配布用 `.app` の Bundle Identifier と Launch Services の設計が必要であるため、未パッケージ状態では自動変更しない。

## 10. UI 設計

基本画面は次の構成とする。

```text
┌─────────────────────────────────────┐
│ Native Title Bar                    │
├─────────────────────────────────────┤
│ Tab 1       Tab 2                   │
├─────────────────────────────────────┤
│ Search Bar (表示時のみ)             │
├─────────────────────────────────────┤
│                                     │
│ Markdown Document                   │
│                                     │
└─────────────────────────────────────┘
```

- 常にダークテーマ
- Markdown Editor 用の編集 UI は置かない
- Toolbar、Sidebar、Status Bar、Dashboard は追加しない
- 文書領域はスクロール可能とする
- コードブロックは読み取り専用とする

## 11. エラー処理

ファイル単位のエラーは対象タブまたは文書領域に表示し、アプリケーション全体を終了させない。

対象例:

- ファイルが存在しない
- ファイルを読み込めない
- 不正な UTF-8
- Markdown 解析エラー
- 画像読み込みエラー
- 相対リンク先が存在しない

ユーザー向け表示に詳細な Stack Trace は含めない。

## 12. セキュリティとプライバシー

- Markdown 内容を外部送信しない
- Telemetry / Analytics を実装しない
- 外部画像を自動ダウンロードしない
- HTML を実行しない
- 外部 URL は明示的なクリック時だけ開く
- ファイル関連付けはユーザーの承認後だけ実行する

## 13. テスト方針

Avalonia を起動せずに検証できる Core Logic を優先して Unit Test を作成する。

主なテスト対象:

- Markdown Parser
- Heading Anchor 生成
- UTF-8 / BOM 読込
- 相対パス解決
- タブ重複検出とタブ状態
- ファイル変更 debounce
- ファイル削除・再出現
- 検索一致数と前後移動
- Zoom の最小値・最大値
- ファイル関連付けの OS 分岐

CI では macOS / Windows の Build と Test を実行し、テレメトリを無効にする。

## 14. 配布

インストーラーは作成せず、self-contained publish を ZIP で配布する。

対象 RID:

- `osx-arm64`
- `osx-x64`
- `win-x64`
- `win-arm64`

```sh
scripts/publish.sh osx-arm64
```

macOS `.app` バンドル、Windows 実機確認、ファイル関連付けの確認 UI は今後の課題とする。

## 15. 既知の制限

- macOS `.app` バンドルは未作成
- Windows 実機での動作確認は未実施
- ファイル関連付けのユーザー確認 UI は未実装
- 起動速度、メモリ、長文描画の定量的な性能計測は未実施
- Markdown 編集、LaTeX、Mermaid、WYSIWYG は対象外
