# mdview アーキテクチャ設計書

## 1. 目的と位置付け

mdview は、Markdown ファイルを素早く開いて読むための読み取り専用デスクトップ Viewer である。編集・保存機能を持たず、少ない依存関係と単純な構成を優先する。

本書は 2026-10-04 時点のリポジトリを基に、構成、責務、データフロー、ライフサイクル、設計上の制約を整理する。設計方針と実装済みの挙動を区別し、未達事項は第12節に記載する。本書の作成によるコードや依存関係の変更は行わない。

関連資料:

- [AGENTS.md](../AGENTS.md): 製品方針と実装制約
- [基本設計書](basic-design.md): 機能・画面の基本仕様
- [Mermaid 表示設計](mermaid-design.md): 既存の Mermaid 拡張の設計・検証結果
- [README](../README.md): 開発、テスト、配布手順

## 2. 設計原則と技術構成

判断の優先順位は、依存関係の少なさ、コード量の少なさ、実行時コンポーネントの少なさ、単純な構成、起動速度、メモリ、テスト容易性、保守性とする。将来の拡張だけを目的とする抽象化やサービスは追加しない。

| 項目 | リポジトリの構成 |
|---|---|
| 対象 OS / CPU | Windows・macOS / x64・ARM64 |
| 言語・実行環境 | C# / `net10.0`（.NET 10 LTS） |
| 開発 SDK | `global.json` の 10.0.401 |
| UI | Avalonia 12.1.3、MVVM、常時ダークテーマ |
| 構造 | Onion Architecture、4プロジェクト |
| Markdown 解析 | Markdig 1.4.0 |
| コード表示 | AvaloniaEdit 12.0.0、AvaloniaEdit.TextMate 12.0.0、TextMateSharp.Grammars 2.0.4 |
| Mermaid 生成 | 同梱 Mermaid 11.12.1、Avalonia.Controls.WebView 12.1.0 |
| SVG 表示 | Svg.Controls.Skia.Avalonia 12.0.0.17 |
| テスト | xUnit v3 / Microsoft Testing Platform |

バージョンは現状の設定値であり、自動的に最新版へ追従する方針ではない。新しいパッケージは必要性、代替手段、配布サイズ、Runtime、OS 対応、ライセンスを確認してから追加する。

## 3. レイヤーと依存関係

コンパイル時のプロジェクト参照は次のとおり。

```text
mdview.Presentation ───→ mdview.Application ───→ mdview.Domain
        │                       ↑
        └──→ mdview.Infrastructure ──┘
```

Presentation から Infrastructure への参照は、外側で具体的な実装を組み立てるために必要となる。Application / Domain は Infrastructure や Presentation を参照しない。実行時には Application のインターフェースを介して外部処理を呼び出す。

| 層 | 責務 | 主な要素 | 持ち込まないもの |
|---|---|---|---|
| Domain | 文書の中心概念と不変条件 | `MarkdownDocument` | Avalonia、Markdig、ファイル I/O、OS API |
| Application | ユースケース、表示用モデル、外部処理の契約 | `MarkdownDocumentLoader`、`MarkdownTabManager`、パス・検索・Zoom、`MermaidDiagramService` | UI コントロール、OS 固有 API、外部ライブラリの型 |
| Infrastructure | 外部環境・ライブラリとの接続 | ファイル読込・監視・関連付け、Markdig 変換、Mermaid SVG 生成・検証 | タブ選択などの UI 方針 |
| Presentation | 画面、ユーザー操作、表示状態、ネイティブ描画 | `MainWindow`、ViewModel、Markdown / Mermaid の View | Markdown の編集・保存処理 |

現在の Domain は小さく、検索・Zoom・タブ管理は Application に置かれている。仕様上の概念をすべて独立クラスや Domain のサービスにする必要はない。

### 3.1 外部処理の契約

契約は [Application/Abstractions](../src/mdview.Application/Abstractions) に配置する。

| インターフェース | 実装 | 境界を越えるデータ |
|---|---|---|
| `IMarkdownFileReader` | `MarkdownFileReader` | パス、文字列、キャンセル |
| `IMarkdownParser` | `MarkdigMarkdownParser` | 元テキスト、`MarkdownDocumentModel` |
| `IMarkdownFileWatcher` | `MarkdownFileWatcher` | 変更イベントのパス・存在状態 |
| `IFileAssociationService` | `FileAssociationService` | 実行ファイルパス、登録結果 |
| `IMermaidSvgGenerator` | `WebViewMermaidSvgGenerator` | Mermaid ソース、SVG 結果、エラー、キャンセル |

Markdig AST、Avalonia Control、WebView の型は Application の公開契約に含めない。

### 3.2 起動と組み立て

`Program → App → MainWindow` の順で起動する。現在は `MainWindow` が ViewModel と Mermaid 生成サービスを組み立て、`MainWindowViewModel` がファイル読込・解析・監視の具体実装を生成する。DI コンテナーは導入していない。

設計上は、具体実装の組み立てを起動側へ寄せ、ViewModel は必要な契約を受け取る形を基本とする。単純な値オブジェクトまで DI 化せず、必要な箇所だけを注入する。

## 4. 文書モデルと状態の所有者

| データ | 所有する層・要素 | 内容 |
|---|---|---|
| 読込文書 | Domain / `MarkdownDocument` | ファイルパスと元テキスト。不正な引数を拒否 |
| 表示用文書 | Application / `MarkdownDocumentModel` | Block / Inline のツリーと検索用テキスト |
| タブ集合・選択 | Application / `MarkdownTabManager` | 正規化パスによる重複検出、追加、閉じる、隣接タブ選択 |
| UI のタブ状態 | Presentation / `DocumentTabViewModel` | パス、タイトル、文書、元テキスト、`Loaded / Missing / Error` |
| 検索・Zoom | Application / 各 State | クエリ、一致位置、現在位置、倍率 |
| 監視資源 | Presentation / `MainWindowViewModel` | 開いているパスごとの Watcher |
| 図生成結果 | Application / `MermaidDiagramService` | ウィンドウ単位の上限付き SVG キャッシュと共有要求 |

表示用モデルは C# の record を中心とする。見出し、段落、コード、Mermaid、リスト、引用、テーブル、水平線、HTML テキストを Block として表現し、強調、リンク、画像、Task List などを Inline として表現する。UI はこのモデルだけを描画する。

文書モデルはタブに保持し、タブ切替だけで再解析しない。表示コントロールの全タブ分の保持は現時点で保証しない。検索と Zoom は現在ウィンドウ単位であり、セッション保存や再起動時の復元は行わない。

## 5. 主な処理フロー

### 5.1 ファイルを開く

```text
起動引数 / ファイル選択 / Drag & Drop / 相対 Markdown リンク
  → MainWindowViewModel.OpenFileAsync
  → パスの絶対化
  → MarkdownFileReader.ReadTextAsync
  → MarkdigMarkdownParser.Parse
  → MarkdownDocumentModel
  → MarkdownTabManager.OpenOrActivate
  → DocumentTabViewModel の追加・選択
  → MarkdownBlockView → MarkdownBlockRenderer
  → Avalonia Native Controls
```

現在の UI は Reader と Parser を直接呼び出す。`MarkdownDocumentLoader` は Reader から Domain 文書を生成するユースケースとして存在するが、この UI 経路には接続されていない。

重複検出では `Path.GetFullPath` と末尾区切り文字の除去を使用する。現在の比較は両 OS とも `OrdinalIgnoreCase` であり、シンボリックリンクの実体解決は行わない。既存タブの再選択は実装済みだが、重複チェックは読込・解析後に行われる。

### 5.2 変更監視と再読込

```text
FileSystemWatcher（Changed / Created / Deleted / Renamed）
  → 250ms debounce
  → IMarkdownFileWatcher.Changed
  → Dispatcher.UIThread.Post
  → 対象タブを再読込
     ├─ 不在             → Missing、エラー文書を表示
     ├─ 読込・解析失敗   → Error、エラー文書を表示
     ├─ Loaded かつ同内容 → 現在のモデルを維持
     └─ 内容変更         → 再解析、Loaded、文書を更新
```

Watcher は対象ファイル名をフィルターとする親ディレクトリの通知を購読する。常時ポーリングは行わない。削除後もタブと監視を残し、再作成時に再読込する。手動再読込は `Ctrl/Cmd + R` から同じ再読込処理を呼ぶ。

### 5.3 Markdown 解析と描画

Markdig の Pipe Tables、Task Lists、Auto Links、Strikethrough を有効化する。解析結果は Infrastructure 内でアプリ用モデルに変換し、Markdig の型を外へ返さない。

Heading Anchor は見出し文字列の小文字化、記号の除去、空白のハイフン化、一意化で生成する。完全な GitHub 互換を保証するものではない。HTML は実行せずテキストとして扱い、Task List は読み取り専用で表示する。

`MarkdownBlockView` は Block、文書パス、検索クエリ、図サービスが同じ場合に再描画を省略する。通常の文書は `VirtualizingStackPanel` を使い、非同期に高さが変わる Mermaid を含む文書は `StackPanel` に切り替える。

コードブロックは読み取り専用の AvaloniaEdit と TextMate で構文強調する。未知の言語は通常のコード表示に戻し、独自の構文強調エンジンは実装しない。

### 5.4 リンクと画像

相対パスの解決は `MarkdownPathResolver` が行い、Markdown ファイルのディレクトリを基準とする。UI 側の `MarkdownLinkHandler` が次の操作へ振り分ける。

- HTTP / HTTPS: 明示的なクリックで OS の既定ブラウザーを起動する。
- `#anchor`: 現在の文書の見出しへスクロールする。
- 相対 Markdown リンク: 対象ファイルを開き、既存タブがあれば選択する。
- 相対画像: ローカルファイルを読み込む。HTTP / HTTPS 画像は取得せず、失敗は画像領域の代替表示にする。

外部ブラウザーの OS 分岐は現在 Presentation に存在する。設計方針に合わせる場合は Infrastructure に移し、Application の必要最小限の契約を介して呼び出す。

### 5.5 検索と Zoom

検索バーの表示・フォーカス・閉じる操作は Presentation、文字列の一致検索と前後移動は `MarkdownSearchState` が担当する。大文字小文字を区別せず、前後移動は端で循環する。表示側で一致箇所をハイライトし、件数は `現在位置 / 総件数` として表示する。

`MarkdownZoomState` は 100% を初期値とし、75～200% の範囲で倍率を管理する。UI は文書領域へ倍率を適用し、ウィンドウサイズは変更しない。

## 6. Mermaid 拡張の境界

Mermaid は初期要件の対象外だったが、既存の [Mermaid 表示設計](mermaid-design.md) に明示的な追加要求の記録がある。本書では実装済み拡張として扱う。本文の描画は引き続き Avalonia Native Controls とする。

```text
MarkdownMermaidBlock
  → MermaidDiagramView / MermaidDiagramViewModel
  → MermaidDiagramService
  → IMermaidSvgGenerator
  → WebViewMermaidSvgGenerator（WKWebView / WebView2）
  → 同梱 Mermaid で SVG 生成 → SVG 検証
  → MermaidSvgDocument → Avalonia SVG ベクター描画
```

WebView は図生成に限って遅延初期化し、生成は直列化する。JavaScript は同梱し、Node.js、CDN、外部サービスを必要としない。Windows は WebView2 Evergreen Runtime を必要とし、自動インストールしない。

生成サービスは同一ソースの要求を共有し、成功 SVG を最大64件・16 MiBで保持する。キャンセルと10秒のタイムアウトを扱い、生成できない場合は図の領域にエラーと元コードを表示する。図内テキスト検索、図内リンク、HTML ラベル、文書指定の設定は対象外とする。

## 7. スレッドと資源のライフサイクル

| 処理・資源 | 現在の扱い |
|---|---|
| ファイル読込 | 非同期 API を使用 |
| Markdown 解析 | 同期処理。現在は UI 呼出経路で実行 |
| ファイル変更通知 | Timer / OS 通知から UI Dispatcher に渡す |
| UI 更新 | UI スレッドで実行 |
| Mermaid 生成 | 非同期契約、生成要求の直列化、キャンセル |
| Mermaid SVG 読込 | `Task.Run` で処理し、表示更新は UI に戻す |
| タブを閉じる | Watcher のイベント解除と Dispose |
| ウィンドウ終了 | ViewModel、Watcher、図サービス、生成器を Dispose |
| 図 View の切離し | 生成要求のキャンセルと SVG 表示資源の解放 |

非同期 API を使うことと UI スレッドで重い処理を実行しないことは別である。大きな文書の解析・画像読込については、性能計測に基づいて必要な処理だけをバックグラウンドへ移す。

## 8. エラー処理と安全性

ファイル・画像・図の失敗は該当タブまたは表示領域で扱い、他のタブの閲覧を継続できる構造とする。ユーザーに Stack Trace を表示しない。文書が Missing / Error になっても、後続の再読込で Loaded へ復帰できるようにする。

仕様上の文字コードは UTF-8 / UTF-8 BOM に限定する。Reader は不正な UTF-8 を拒否する Encoding を指定しているが、BOM による別エンコーディングの判定については第12節の確認事項とする。

Markdown 内容の外部送信、Telemetry、Analytics、アカウント、Cloud Sync は実装しない。外部画像は自動取得せず、HTML や Markdown 内の任意スクリプトを実行しない。Mermaid の生成環境では外部通信・遷移を制限し、生成 SVG も検証する。

ファイル関連付けは `IFileAssociationService` を介して扱う。Windows のユーザー単位レジストリ登録基盤は存在するが、UI からの同意確認・呼出しは未実装である。登録処理だけで OS の既定アプリ選択を完全に管理できるとはみなさない。macOS は `.app` と Launch Services を含む配布設計が必要となる。

## 9. UI の構造

画面は OS ネイティブタイトルバー、タブ列、必要時だけ表示する小さな検索バー、文書領域で構成する。常にダークテーマとし、システムフォントを優先する。

Code-behind はファイル選択、Drag & Drop、キーボード操作、フォーカス、スクロール、表示パネルの切替など、UI 固有の処理を担当する。読込方針やファイル状態遷移は ViewModel / Application 側に置く。

Toolbar、Status Bar、Sidebar、Settings Screen、Welcome Screen は追加しない。初期サイズの固定や複雑なウィンドウ状態保存、タブ復元は設計対象に含めない。

## 10. テストと検証

| テストプロジェクト | 主な検証対象 |
|---|---|
| `mdview.Domain.Tests` | 文書モデルの引数・不変条件 |
| `mdview.Application.Tests` | 読込ユースケース、相対パス、タブ重複・閉じる、検索、Zoom、図生成サービス |
| `mdview.Infrastructure.Tests` | Markdig 変換・Anchor、ファイル読込、変更監視・debounce、関連付け、Mermaid の検証 |

Core Logic は Avalonia を起動せずに検証する。外部契約は Fake に置き換える。UI のフォーカス、スクロール、描画、実 OS の関連付け、WebView Runtime は Unit Test だけで動作保証せず、対象 OS / CPU の実機検証を行う。

GitHub Actions は macOS / Windows のマトリックスで Restore、Release Build、Test、Coverage を実行する。実機検証状況は README と Mermaid 設計の記録を参照する。本書作成時にアプリの Build / Test や追加の実機検証を実施したという意味ではない。

## 11. 配布と性能方針

通常配布は self-contained publish の ZIP とし、インストーラーは作成しない。対象 RID は `win-x64`、`win-arm64`、`osx-x64`、`osx-arm64`。現状のスクリプトは macOS `.app` を作成しない。Native AOT は明示的なオプションとして検証し、通常 publish で強制しない。

構文強調のネイティブ資産、Mermaid の同梱資産、必要なライセンスを配布物に含める。Windows の WebView2 Runtime は別の前提条件として案内する。

性能は起動速度、アイドルメモリ、スクロール、タブ切替、描画、割当量の順に評価する。同内容の再解析・再描画は省略し、OS 通知を使用する。長文、巨大画像、多数のタブ、Mermaid を含む文書の測定値を得てから、必要な改善を選択する。

## 12. 設計方針と現実装の差分

以下はソース確認で把握した事項であり、本書作成に伴う修正・新機能の追加は行っていない。

| 項目 | 現状と今後の確認・対応 |
|---|---|
| ViewModel の境界 | Reader / Parser / Watcher の具体実装生成と `File.Exists` が Presentation にある。テスト可能な契約への整理が必要 |
| 外部リンクの境界 | OS 分岐・`Process.Start`・アプリ全体への静的アクセスが Presentation にある。Infrastructure と起動側への責務整理が必要 |
| パスの同一性 | 全 OS で大文字小文字を無視する。macOS の case-sensitive ボリューム、シンボリックリンクによる重複を扱えていない |
| 重複 Open | 既存タブは選択できるが、判定前に読込・解析するため不要な処理が発生する |
| 状態の一元性 | TabManager と UI のタブモデルに文書を保持する。再読込後の文書・選択状態の整合性を確認する必要がある |
| Anchor 付き別文書リンク | 非同期 Open を待たずにスクロールする経路がある。描画完了後の移動と URL エスケープの扱いを確認する必要がある |
| エラー時の表示更新 | 再読込失敗・削除でタブ文書を更新する経路と `ActiveDocument` の同期を確認する必要がある |
| 監視の耐障害性 | 存在しない親ディレクトリ、監視開始失敗、通知の競合・連続再読込に対する扱いを確認する必要がある |
| UTF-8 限定 | `File.ReadAllTextAsync` の BOM 判定が UTF-16 等を許容しないことを検証し、要件に合わせる必要がある |
| UI 応答性 | 解析は同期処理、画像は `new Bitmap(path)`。巨大画像の縮小読込・解放と長文解析の負荷を測定する必要がある |
| タブ切替の描画 | 局所的な再描画省略はあるが、タブ間の Visual Tree 全体の再利用は保証されていない |
| 図を含む長文 | Mermaid 文書は仮想化を無効にするため、Visual Tree とメモリの増加を測定する必要がある |
| 配布・関連付け | macOS `.app`、両 OS の関連付け確認 UI、全 RID の実機確認が未完了 |

これらを解決する場合も、本文のネイティブ描画、Markdig、4層の依存方向、読み取り専用という方針を維持する。アーキテクチャや配布方式に関わる変更は、理由・代替案・影響を説明し、承認後に実施する。
