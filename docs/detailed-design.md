# mdview 詳細設計書

## 1. 文書の対象と読み方

本書は 2026-10-04 時点のソースコードに基づき、mdview のデータ構造、メソッド契約、処理手順、画面との接続、例外処理、検証観点を記載する。

特記のない記述は現実装の説明とする。「要確認」「未実装」「改善方針」は実装済みの動作保証を意味しない。本書の作成ではコード・パッケージを変更しない。

- [アーキテクチャ設計書](architecture-design.md): レイヤー、依存方向、設計原則
- [基本設計書](basic-design.md): 機能と画面の基本仕様
- [Mermaid 表示設計](mermaid-design.md): 図生成方式と実機検証記録
- [AGENTS.md](../AGENTS.md): 要件・制約

対象は C# / .NET 10、Avalonia、Markdig を使った読み取り専用 Viewer。Markdown の編集・保存、外部画像取得、セッション復元は設計対象に含めない。

## 2. 実装ファイルと責務

| 配置 | 主なクラス | 責務 |
|---|---|---|
| `Domain/Models` | `MarkdownDocument` | パス・内容を持つ読込文書 |
| `Application/Models` | `MarkdownDocumentModel`、Block / Inline、`MarkdownTab`、`MarkdownTabManager`、Mermaid 結果 | 外部ライブラリに依存しないデータとタブ管理 |
| `Application/UseCases` | Loader、PathResolver、SearchState、ZoomState、MermaidDiagramService | 読込、パス解決、検索、倍率、図生成要求管理 |
| `Application/Abstractions` | Reader、Parser、Watcher、Association、Mermaid の各契約 | 外部処理への境界 |
| `Infrastructure/FileSystem` | Reader、Watcher、Association | ファイル I/O、OS 通知、レジストリ登録 |
| `Infrastructure/Markdown` | `MarkdigMarkdownParser` | AST から表示モデルへの変換 |
| `Infrastructure/Mermaid` | Generator、Validator、JsonContext | WebView と同梱 JavaScript、SVG 検証、JSON シリアライズ |
| `Presentation/ViewModels` | MainWindow / DocumentTab / MermaidDiagram の各 ViewModel | UI 状態と通知 |
| `Presentation/Views` | BlockView、BlockRenderer、CodeBlockView、MermaidDiagramView | Control の構築と表示資源管理 |
| `Presentation` | Program、App、MainWindow、MarkdownLinkHandler | 起動、画面操作、リンクの振り分け |

上表の配置は `src/mdview.<層名>/` を基準とする。`MarkdownTabManager` は現在 `Models/MarkdownTab.cs`、`DocumentTabViewModel` と `DocumentTabState` は `ViewModels/MainWindowViewModel.cs` に定義されている。

## 3. データ構造

### 3.1 文書と表示モデル

`MarkdownDocument` は `FilePath: string` と `Content: string` を持つ sealed record。コンストラクターは空白を含む未指定パスと null の内容を拒否する。内容が空文字列の文書は許容する。

`MarkdownDocumentModel` は `Blocks: IReadOnlyList<MarkdownBlock>` と init プロパティ `SearchText: string?` を持つ。外部パーサーの型は含めない。コレクションは読み取り専用の契約であり、格納実体の深い不変性までは保証しない。

| Block | データ |
|---|---|
| `MarkdownHeadingBlock` | `Level`、`Anchor`、`Inlines` |
| `MarkdownParagraphBlock` | `Inlines` |
| `MarkdownCodeBlock` | `Code`、任意の `Language` |
| `MarkdownMermaidBlock` | Mermaid の `Source` |
| `MarkdownListBlock` | `IsOrdered`、開始番号 `Start`、`Items` |
| `MarkdownListItem` | 子 `Blocks` |
| `MarkdownQuoteBlock` | 子 `Blocks` |
| `MarkdownTableBlock` | `Rows`、列ごとの `Alignments` |
| `MarkdownTableRow / Cell` | `IsHeader` と `Cells` / `Inlines` |
| `MarkdownThematicBreakBlock` | データなし |
| `MarkdownHtmlBlock` | 実行しない HTML の `Text` |

Inline は Text、Emphasis、Strong、Strikethrough、Code、Link、Image、Break、TaskList の record で構成する。強調とリンクは子 Inline を持ち、画像は `Destination / Title / AltText`、改行は `IsHard`、Task List は `IsChecked` を持つ。表の列揃えは `None / Left / Center / Right`。

### 3.2 タブと表示状態

| 要素 | プロパティ・内部状態 |
|---|---|
| `MarkdownTab` | `FilePath`、`Title`、`Document`、計算プロパティ `NormalizedPath` |
| `MarkdownTabManager` | `List<MarkdownTab>`、`Tabs`、`ActiveTab` |
| `DocumentTabViewModel` | `FilePath`、通知対象の `Title / Document / State`、通知しない `SourceContent` |
| `MainWindowViewModel` | `ObservableCollection<DocumentTabViewModel> Tabs`、`ActiveTab`、`ActiveDocument`、検索・Zoom、Watcher 辞書 |

`DocumentTabState` は `Loaded / Missing / Error`。表示エラーは `MarkdownParagraphBlock(MarkdownTextInline(message))` を持つ通常の表示モデルとして生成する。状態値自体を専用エラー画面へバインドする構造ではない。

## 4. Application のメソッド詳細

### 4.1 読込ユースケース

`MarkdownDocumentLoader.LoadAsync(string path, CancellationToken cancellationToken = default)` は、パスを検証し、`IMarkdownFileReader.ReadTextAsync` を await して `MarkdownDocument` を返す。Reader の失敗やキャンセルは呼出元へ伝播する。解析・タブ追加は担当しない。

このユースケースは存在するが、現在の UI は Reader / Parser を直接呼び出す。

### 4.2 タブ管理

`MarkdownTab.NormalizePath(path)`:

1. null・空白を拒否する。
2. `Path.GetFullPath` で絶対化する。
3. 末尾のディレクトリ区切り文字を除去する。

`MarkdownTabManager.OpenOrActivate(filePath, title, document)`:

1. パスと文書を検証する。
2. 正規化パスを既存タブと `OrdinalIgnoreCase` で比較する。
3. 一致時は既存インスタンスを `ActiveTab` にして返す。文書とタイトルは置き換えない。
4. 不一致時はタブを末尾に追加して選択する。未指定タイトルはファイル名を使う。

`CloseTab(tab)` は、未登録タブなら false を返す。登録済みなら削除して true を返し、同じインデックスに残るタブ、または末尾のタブを選択する。0件なら `ActiveTab = null`。現在は非アクティブタブを閉じた場合も選択を更新する。

### 4.3 パス解決

| メソッド | 処理 |
|---|---|
| `ResolveDocumentLink(currentMarkdownPath, linkTarget)` | 元パスを検証。空文字列・`#` 開始・絶対 URI はそのまま返す。それ以外は元文書のディレクトリと結合して絶対化 |
| `ResolveImagePath(currentMarkdownPath, imageTarget)` | 元パスを検証。絶対 URI はそのまま返す。それ以外は元文書のディレクトリと結合して絶対化 |

Resolver はファイルの存在確認、外部画像の禁止判定、URI の実行をしない。これらの方針は呼出側で適用する。URL エスケープやシンボリックリンクの解決は現時点で行わない。

### 4.4 検索アルゴリズム

`MarkdownSearchState` は元テキスト、クエリ、一致開始位置の一覧、0始まりの `CurrentMatch` を保持する。

- `SetText / SetQuery`: null を空文字列へ変換し、一致一覧を再計算する。
- 空クエリ: 件数を0、現在位置を0へ戻す。
- 検索: `IndexOf(query, offset, CurrentCultureIgnoreCase)` を繰り返し、次の offset は `一致位置 + query.Length`。重なる一致は数えない。
- 再計算後: 現在位置を残し、件数を超える場合は末尾へ制限する。
- `MoveNext / MovePrevious`: 件数がある場合に循環する。0件では変更しない。
- `IsMatch(index, length)`: 正の長さの範囲がいずれかの一致範囲と重なるか返す。

表示値は0件なら `0 / 0`、それ以外は `CurrentMatch + 1 / MatchCount`。例えば `aaaa` に `aa` は2件となる。

### 4.5 Zoom

`MarkdownZoomState` の初期 `Scale` は1.0、`Minimum = 0.75`、`Maximum = 2.0`、`Step = 0.1`。

`Increase / Decrease` は加減算後に上限・下限でクランプする。`Reset` は1.0へ戻す。Reset 用ショートカットや UI は現在接続されていない。

## 5. Infrastructure の詳細

### 5.1 ファイル読込

`MarkdownFileReader.ReadTextAsync(path, cancellationToken)` はパスを検証し、`File.ReadAllTextAsync` へ `UTF8Encoding(false, true)` とキャンセルを渡す。BOM なし UTF-8 と UTF-8 BOM を想定し、不正 UTF-8 を例外として通知する。

存在しないファイル、アクセス失敗、デコード失敗を Reader 内で表示用エラーへ変換しない。UTF-16 等の BOM が API に自動判定される挙動は要件との照合が必要である。

### 5.2 Markdown 解析

`MarkdigMarkdownParser.Parse(markdown)` は null を拒否し、固定 Pipeline で解析する。Pipeline は Pipe Tables、Task Lists、Auto Links、Strikethrough を有効化する。

処理手順:

1. Markdig AST を生成する。
2. 文書単位の `HashSet<string>(Ordinal)` を Anchor の一意性管理に使う。
3. Mermaid fenced block のソース範囲を検索テキスト上で NUL に置換する。改行と全体の長さは維持する。
4. `ConvertBlocks` で Block を再帰変換する。
5. `MarkdownDocumentModel` に Blocks と SearchText を設定して返す。

| AST | アプリモデルへの変換規則 |
|---|---|
| Heading / Paragraph | Inline を変換。Heading は Anchor を生成 |
| FencedCode | info の先頭語を言語とする。`mermaid` は大文字小文字を無視して専用 Block に変換 |
| Indented Code | `Language = null` の CodeBlock |
| List / Quote | 子 Block を再帰変換。順序付き開始番号の解析失敗は1 |
| Table | 行・セル・ヘッダー・列揃えを変換。セルの Paragraph の Inline を連結 |
| ThematicBreak / HTML | 水平線 / 実行しないテキスト |
| 未対応 Block | null として結果から除外 |

Inline はリテラル、コード、強調、画像、リンク、改行、Task List、HTML Entity、HTML テキストを変換する。強調は `~` を取り消し線、2文字 delimiter を太字、3文字以上を斜体内の太字、それ以外を斜体とする。

### 5.3 Heading Anchor

`GetPlainText` は強調やリンクの子を再帰展開し、画像の AltText とコードを含め、改行を空白へ変換する。

`CreateUniqueAnchor`:

1. `ToLowerInvariant` と Trim を適用する。
2. 連続する空白を1個の空白へ置換する。
3. Unicode の文字・数字・結合文字、`_`、`-`、空白以外を除去する。
4. 空白を `-` へ置換する。
5. 既存 Anchor と衝突したら `-1`、`-2` を順に付ける。

例: `Architecture` の重複は `architecture / architecture-1`。記号だけの見出しは空 Anchor になり得る。GitHub の完全互換は保証しない。

### 5.4 ファイル監視

`MarkdownFileWatcher(path, debounce = null)` は親ディレクトリとファイル名から `FileSystemWatcher` を作る。サブディレクトリは監視せず、FileName、LastWrite、Size、CreationTime を通知対象にする。既定 debounce は250ms。

| メソッド・イベント | 処理 |
|---|---|
| `Start` | Dispose 後なら例外。それ以外は通知を有効化 |
| Changed / Created / Deleted / Renamed | イベントの FullPath で通知予約 |
| `ScheduleNotification` | 既存 Timer を Dispose し、単発 Timer を再生成 |
| `Notify` | Dispose 済みなら終了。パスと `File.Exists` の結果を Changed イベントで通知 |
| `Dispose` | 通知停止、イベント解除、Watcher / Timer 解放。再呼出しは何もしない |

親ディレクトリがない場合の生成失敗、複数イベントの並行実行、既に実行中の Timer callback、Watcher の Error 通知は別途確認が必要。

### 5.5 ファイル関連付け

`FileAssociationService.IsSupported` は Windows のみ true。`RegisterMarkdownAssociationAsync` は実行ファイルパスとキャンセルを検証し、非 Windows なら false を返す。

Windows では `HKCU/Software/Classes` の `.md / .markdown / .mdown` に `mdview.Markdown` を登録し、`mdview.Markdown/shell/open/command` に `"実行ファイルの絶対パス" "%1"` を設定して true を返す。

UI からの呼出しと同意確認は未実装。明示的なユーザー同意前に呼び出さない。登録と OS の既定アプリ選択は別に検証する。macOS の登録は未実装。

## 6. MainWindowViewModel の詳細

### 6.1 Open

`OpenFileAsync(path)`:

1. 空白パスなら終了する。
2. `Path.GetFullPath` で絶対化する。
3. Reader で読込み、Parser で表示モデルへ変換する。
4. `OpenOrActivateDocument` に Loaded と元テキストを渡す。
5. `StartWatching` で監視を開始する。
6. try 内の失敗時はエラー文書を作り、存在するファイルは Error、不在は Missing としてタブを開き、監視を試みる。

絶対化は try の外にある。catch 内の監視開始も追加の例外を発生させ得るため、すべてのパス異常がタブ内に封じ込められているわけではない。

`OpenOrActivateDocument` は TabManager の返したタブを UI 側で照合し、存在しなければ追加する。既存 UI タブには TabManager の文書・タイトルと今回の元テキスト・状態を設定する。TabManager は重複時に文書を更新しないため、再 Open 時のデータ整合性は要確認。

### 6.2 ActiveTab と通知

同じインスタンスの再設定は何もしない。選択変更時は `ActiveDocument` をタブの文書へ変更し、検索対象を `Document.SearchText ?? SourceContent` へ変更して `SearchStatus / ActiveTab` を通知する。

`ActiveDocument` は参照が変わったときだけ通知する。`SearchQuery` の変更は検索再計算と `SearchQuery / SearchStatus` 通知、Zoom の変更は `ZoomScale` 通知を行う。

### 6.3 Reload と状態遷移

`ReloadActiveFileAsync` は ActiveTab がある場合だけ `ReloadFileAsync` を呼び出す。監視通知は `Dispatcher.UIThread.Post` で同じ処理へ渡す。通知の `Exists` は UI の再読込判定には使わず、改めて存在確認する。

| 前状態 | 条件 | 次状態・更新 |
|---|---|---|
| 任意 | 対象タブなし | 終了 |
| 任意 | ファイル不在 | Missing、元テキスト null、`File not found.` の文書 |
| Loaded | 同一テキスト | 文書と状態を維持し、解析を省略 |
| 任意 | 読込後に Dispose 済み、またはタブを閉じた | 結果を適用せず終了 |
| 任意 | 読込・解析成功、同内容省略の対象外 | Loaded、元テキストと文書を更新 |
| 任意 | 読込・解析失敗 | Error、元テキスト null、エラー文書 |

成功時はアクティブタブなら検索を再計算し、`ActiveDocument` も更新する。不在・例外時はタブの Document の更新だけであるため、表示中の文書との同期に未達事項がある。

### 6.4 Close と終了

`CloseTab` は対応する Application タブを閉じ、Watcher をイベント解除・Dispose して UI 集合から削除し、TabManager の選択結果を UI に反映する。対応する Application タブがない場合の分岐は UI 集合のみを操作する。

`Dispose` は破棄フラグを設定し、全 Watcher の購読解除・解放、辞書のクリア、図サービスの Dispose を行う。通常文書の Open / Reload 全体をキャンセルするトークンは現時点で持たない。

## 7. 画面と入力の詳細

### 7.1 XAML とバインディング

| 要素 | 接続 |
|---|---|
| タブ列 | `ItemsSource = Tabs`、`SelectedItem = ActiveTab` の TwoWay |
| タブ名・閉じる | `Title` を表示、Button.Tag にタブ、Click で Close |
| 検索バー | `IsSearchVisible`、TwoWay の `SearchQuery`、`SearchStatus` |
| 文書 | ScrollViewer → LayoutTransformControl → ItemsControl |
| Block 一覧 | `ItemsSource = ActiveDocument.Blocks`、MarkdownBlockView の Template |
| MermaidHost | 1×1 Canvas、ヒットテスト無効、画面外の生成用 WebView ホスト |

通常文書は VirtualizingStackPanel、Mermaid を含む文書は StackPanel を使う。リスト・引用の中も再帰的に Mermaid の有無を判定する。仮想化の実効性・Anchor 到達性は実機で確認する。

### 7.2 操作

| 入力 | 処理 |
|---|---|
| Ctrl / Cmd + O | StorageProvider の単一ファイル選択。フィルターは `.md / .markdown / .mdown` |
| Drag & Drop | 最初のファイルだけを取得。対応拡張子なら Copy を示して Open |
| Ctrl / Cmd + R | アクティブファイルを再読込 |
| Ctrl / Cmd + F | 検索バー表示、入力に Focus と SelectAll |
| Enter / Shift + Enter | 検索入力内で次 / 前の一致位置へ |
| Esc | 検索入力の KeyDown で検索バーを非表示 |
| Prev / Next / Close | 検索の前 / 次 / 非表示 |
| Ctrl / Cmd + 加算・減算キー | Zoom の変更後に `ScaleTransform` を適用 |
| Ctrl / Cmd + W | アクティブタブを閉じ、最後のタブなら Window も閉じる |
| タブの × | タブを閉じる。最後のタブでも Window を閉じる処理はない |

Control または Meta をコマンド修飾キーとして受け付ける。検索バーを閉じてもクエリを消去しない。起動引数の各ファイルは await せず Open を開始するため、タブ順は完了順の影響を受ける。

## 8. 描画の詳細

### 8.1 BlockView と Renderer

`MarkdownBlockView` は DataContext 変更・Visual Tree への追加で `UpdateContent` を呼び、ViewModel の通知を購読する。切離し時に解除する。

Block 参照、文書パス、検索クエリ、図サービス参照が前回と同じなら描画を省略する。SearchQuery 変更では通常 Block を再描画する。Mermaid は検索クエリを描画キーに含めない。

| Block | 描画 |
|---|---|
| Heading | WrapPanel、太字、Tag に Anchor。H1～H6 は32 / 28 / 24 / 21 / 18 / 16px |
| Paragraph | WrapPanel と Inline Control、本文16px |
| List | 項目を再帰描画。順序付き開始番号または箇条書き |
| Quote | 子 Block の再帰描画と引用の装飾 |
| Table | 最大セル数で Grid 列を作成、Star 幅、ヘッダー太字、列揃え、罫線 |
| Code / Mermaid | 専用 View |
| ThematicBreak | 高さ1の Border |
| HTML | monospace のテキスト |

リンクはクリック可能な Button、画像はローカル Bitmap として構築する。HTTP / HTTPS 画像はリンクハンドラーで空パスにし、読込失敗は代替テキストを表示する。Task List は文書を更新しない表示要素とする。

### 8.2 検索表示の制限

`CreateHighlightedText` は個々の表示文字列に対して検索し、一致部分の Run に `#806B2A` の背景を付ける。全件検索は Markdown ソース由来の SearchText を使うため、表示文字列に現れない記法・URL の一致も件数に含まれ得る。

強調等で分割された文字列をまたぐ一致、コードブロック、表セルへの検索ハイライトは同じ処理に統合されていない。`RenderTable` はパス・検索クエリを受け取らず、セル内のリンク・画像も通常段落と同じ操作経路にならない。

次 / 前の操作は現在位置と件数表示を更新するが、現在一致へのスクロールや選択表示に接続されていない。これは必須検索機能の未達事項であり、全文検索インデックスの追加を意味しない。

### 8.3 コードブロック

`MarkdownCodeBlockView` は TextEditor を `IsReadOnly = true`、行番号なし、折返しあり、monospace、高さ32～440で作る。言語 alias を拡張子へ対応させ、TextMate の文法 scope を取得する。

対象 alias は C、C++、C#、Python、JavaScript、TypeScript、JSON、XML、HTML、CSS、Bash / Shell、PowerShell、YAML、Markdown と各短縮形。未知・未指定なら文法を設定しない。

Visual Tree 追加時に DarkPlus の TextMate を導入し、切離し時に installation を Dispose する。フォントサイズはこの View 内で明示設定されていないため、コード14pxという目安への整合性は要確認。

### 8.4 リンクと Anchor

`MarkdownLinkHandler.Open` は HTTP / HTTPS を外部起動、`#` 開始を現在文書内スクロール、その他を Resolver 経由の文書 Open に振り分ける。

`OpenDocumentPath` は `#` でファイルと Anchor を分離し、Open を開始して直ちにスクロールを呼ぶ。`MainWindow.ScrollToAnchor` は現在の Visual Tree 内の Tag を大文字小文字無視で照合し、見つかれば `BringIntoView`。見つからなければ何もしない。

別文書の読込・描画完了待ち、未実体化の見出し、URI エスケープ、ファイル名中の `#`、許可するリンク種別の厳密な判定は要確認。

## 9. Mermaid の詳細

### 9.1 結果とキャッシュ

`MermaidSvgDocument` は SVG 文字列と Width / Height。`MermaidGenerationResult` は任意の Document と Error を持ち、Failure は Document を null にする。

`MermaidDiagramService.GetAsync(source, token)`:

1. キャンセルと Dispose を確認する。
2. ソース文字列を Ordinal で比較し、キャッシュ済みなら返す。
3. 同じソースの保留 Request があれば共有し、なければ生成する。
4. 購読者数を増やし、呼出側のトークンで共有 Task を待つ。
5. 終了時に購読者数を減らす。最後の購読者が抜けた未完了要求はキャンセルする。

キャッシュは成功結果のみ。UTF-8 で計算したソースと SVG の合計バイトを管理し、既定64件・16 MiBを超えないよう古い挿入順に削除する。LRU ではない。上限より大きな成功結果は返すが保存しない。Dispose はキャッシュをクリアし、保留生成をキャンセルする。

### 9.2 SVG 生成

`WebViewMermaidSvgGenerator.GenerateAsync` は次の順序で処理する。

1. 対象 OS、ソース50,000文字以下、文書設定指定なしを検証する。
2. SemaphoreSlim で生成を1件に制限する。
3. 待機取得後に10秒のタイムアウトを開始する。キュー待ち自体はこの10秒に含めない。
4. Windows の初回は Runtime を確認し、ユーザーデータフォルダーを作る。
5. UI Dispatcher で WebView を遅延生成し、準備完了を待つ。
6. 要求 ID を採番し、JSON シリアライズしたソースを `mdviewRender` へ渡す。
7. JavaScript 呼出しと結果通知を await し、要求に対応する SVG 検証結果を返す。
8. タイムアウト・生成環境の例外では Reset してエラーを返す。呼出側キャンセルは再送出する。
9. finally で Semaphore を解放する。

同梱 JS とローカル HTML だけを使用し、外部遷移、新規 Window、外部資源を制限する。ホストの組み込みは MainWindow が担当し、WebView 自体の操作は Infrastructure に閉じる。

### 9.3 SVG の検証と表示

| 検証 | 制限 |
|---|---|
| サイズ | UTF-8 で5 MiB以下、XML 文書の文字数にも上限 |
| XML | DTD 禁止、XmlResolver は null、ルートは SVG namespace の svg |
| 構造 | 許可要素のみ、25,000要素以下、祖先の深さ64以下 |
| 属性・CSS | イベント属性、base、外部 href / src、外部 url、javascript、CSS escape / comment / @ を拒否 |
| viewBox | 有限値4個、幅・高さは正数かつ100,000以下 |
| 寸法 | width / height を viewBox の幅・高さへ正規化 |

`MermaidDiagramViewModel.LoadAsync` はサービス結果を Document / Message / HasError に反映して通知する。キャンセル・Dispose は表示エラーにしない。

`MermaidDiagramView` は追加時にロード開始、切離し時にキャンセル・購読解除・SVG 解放を行う。SVG 読込は Task.Run で処理し、外部資源は無効にする。Viewbox は Uniform / DownOnly、図はヒットテスト無効。失敗時はメッセージと元ソースのコード表示へ戻す。

## 10. エラー表示と未達事項

| 失敗 | 現在の扱い・制限 |
|---|---|
| ファイル不在・アクセス・解析失敗 | Open / Reload でエラー文書へ変換。ただしパス正規化・監視失敗の封じ込めは不十分 |
| ファイル削除・再出現 | タブと監視を残す。成功時に復帰。不在時の ActiveDocument 同期は要確認 |
| 不正 UTF-8 | Reader の例外を UI で表示。別 BOM の拒否は未検証 |
| 画像失敗 | 画像領域に代替表示。巨大画像の縮小読込と Bitmap 解放は要確認 |
| 外部ブラウザー起動失敗 | 起動処理に共通の表示用例外変換がない |
| Mermaid Runtime・生成・SVG 失敗 | 図領域にエラーと元コード。他の Block は維持 |

ファイルエラーは現在 `File could not be opened: {ex.Message}` として表示する。Stack Trace は出さないが、表示文言の統一や OS 例外の詳細をそのまま見せる妥当性は確認が必要。

改善では、タブ文書の一元管理、アクティブ表示の状態同期、OS 処理の Infrastructure への移動、ViewModel への契約注入、読込の競合・キャンセル、OS / ボリュームに合ったパス比較、検索位置と描画の対応付けを優先する。新しいフレームワークや大規模な抽象化は前提としない。

## 11. 検証項目

以下の「既存テスト」はコード上の検証対象であり、本書作成時の実行結果ではない。

| 対象 | 既存テスト | 追加検証が必要な観点 |
|---|---|---|
| Domain 文書 | パス・内容保持、不正引数 | 空内容 |
| Loader | Reader 呼出し、文書生成、空パス | キャンセル・読込例外伝播 |
| Parser | GFM Block / Inline、ネスト、Anchor、空入力、Mermaid 検索除外 | 未対応 AST、記号のみの見出し、混在文書 |
| Reader | UTF-8 BOM、不正 UTF-8 | BOM なし、UTF-16 拒否、消失・アクセス失敗 |
| パス・タブ | 相対リンク・画像、fragment、重複、隣接選択 | case-sensitive ボリューム、symlink、非アクティブ Close、再 Open |
| Watcher | debounce、削除通知 | 再作成、atomic save、Rename、通知競合、親ディレクトリ消失 |
| 検索・Zoom | 件数・循環・空クエリ、倍率境界・Reset | 描画との一致、現在一致への移動、Esc とフォーカス |
| 関連付け | 非 Windows の無変更 | 承認 UI、Windows 登録・既定選択、macOS 配布 |
| Mermaid | 要求共有・キャンセル・上限、入力制限、JSON、安全性、SVG 拒否 | 全 RID の実機、Runtime 不在、Window 終了、長文負荷 |

Build / Unit Test の手順は [README](../README.md) に従う。UI の実機検証は、起動引数、Open、Drag & Drop、タブ切替・Close、監視、リンク・画像、検索、Zoom、エラーからの復帰を Windows / macOS で行う。

起動時間、アイドルメモリ、長文スクロール、画像・図を含む文書、タブ数増加の性能は未測定の値を記載せず、測定結果に基づいて改善する。本書は実装完了や全対応環境の動作確認を宣言するものではない。
