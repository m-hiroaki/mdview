# mdview 開発計画

## 目的と進め方

`mdview` を、Markdown を素早く開いて読むための軽量な閲覧専用アプリとして段階的に開発する。シンプルさと少ない依存関係を優先し、要求されていない機能や大規模な抽象化は追加しない。

各段階で **実装 → Build → Test → 次の段階** の順に進める。アーキテクチャに関わる変更や要件が不明確な場合は、着手前に確認する。

## 開発タスク

### Phase 0 — リポジトリと開発環境の確認

- [x] Git の状態、既存ファイル、既存プロジェクトの有無を確認する。
- [x] macOS の開発環境と Windows/macOS 向け検証・配布方法を確認する。
- [x] 実装開始時点で利用可能な最新の .NET LTS と対応する Avalonia の構成を確認する。
- [x] 初期スコープと、外部 NuGet パッケージが必要な場合の理由・影響を整理する。

**完了条件:** 現在のリポジトリ状態、使用する SDK、初期の技術選択が明確になっている。

**調査結果（2026-09-27）:**

- リポジトリは `main` ブランチで調査時点の作業ツリーは clean。アプリの Solution / project はまだなく、ルートには `AGENTS.md` と本計画書がある。
- 開発機は Apple Silicon の macOS 27.0。調査時点では `.NET SDK 10.0.400` と .NET runtime `10.0.11` が導入済みで、Avalonia の Desktop / MVVM テンプレートも利用できた。
- .NET 10 が現行 LTS。調査時点の最新は SDK `10.0.401`、runtime `10.0.12`。Phase 1 で更新・固定済み。
- Avalonia は Windows/macOS 対応のクロスプラットフォーム UI として要件に合致する。調査時点の最新安定版 `12.1.3` は MIT ライセンスで、Avalonia 12 は直近リリースのため、採用時にテンプレートからの生成・Build・macOS 起動を先に確認する。Windows の実機確認は GitHub Actions の Windows runner を利用する。
- 初期依存候補は Avalonia Desktop（ネイティブ UI に必須、MIT）、Markdig `1.4.0`（要件指定の GFM Parser、BSD-2-Clause）、xUnit v3（指定テスト基盤、Apache-2.0）。Markdig は必要な GFM 拡張だけを有効にし、初期対象外の構文を不用意に増やさない。
- Syntax Highlighting は要件上必要だが、ネイティブ描画・依存関係・ライセンス・配布サイズを Phase 5 で比較して選ぶ。現時点では追加パッケージを決めない。MVVM Toolkit や DI パッケージも初期導入せず、必要性が確認された場合のみ検討する。
- 自己完結型 ZIP を基本とし、WebView / Chromium / ネットワーク機能は導入しない。各パッケージの実際の Publish サイズは配布工程で測定する。

### Phase 1 — Solution と最小構成

- [x] .NET 10 の SDK/runtime を最新 servicing に更新し、使用する SDK をリポジトリで固定する。
- [x] `src/` に Domain、Application、Infrastructure、Presentation の各プロジェクトを作成する。
- [x] `tests/` に Domain、Application、Infrastructure の xUnit テストプロジェクトを作成する。
- [x] 依存方向を `Presentation → Application → Domain` とし、Infrastructure は内側で定義する必要な抽象化のみ実装する。
- [x] 必要最小限の Solution、共通ビルド設定、`.gitignore` を整える。
- [x] 空の状態で Solution 全体の Build と Test を実行する。

**完了条件:** 構成が AGENTS.md に沿い、初期 Build/Test が成功する。

**実施結果（2026-09-27）:**

- .NET SDK `10.0.401` と runtime `10.0.12` を導入し、`global.json` で SDK を固定した。SDK 10 の `dotnet test` は既定で VSTest を使うため、同ファイルで xUnit v3 と互換性のある Microsoft.Testing.Platform を選択した。
- `mdview.sln`、4つの本体プロジェクト、3つのテストプロジェクトを作成した。Presentation は次の Phase 2 で Avalonia Desktop アプリへ移行する最小クラスライブラリとして置いている。
- Domain / Application / Infrastructure / Presentation の ProjectReference を設定し、共通の `net10.0` / Nullable / ImplicitUsings 設定と最小 `.gitignore` を追加した。
- 各テストプロジェクトにテストランナーの起動確認を置き、Release Build 成功、テスト 3 件成功を確認した。テスト実行時は MTP のテレメトリをオプトアウトした。
- 既存の Avalonia テンプレートは `12.1.1` だった。Phase 2 で `12.1.3` に更新し、同じバージョンのパッケージを採用した。

### Phase 2 — Avalonia の基本 UI

- [x] Presentation に Avalonia アプリを構築し、標準のネイティブ Title Bar を使う。
- [x] 常時ダークテーマと、タブ領域・Markdown 表示領域を持つ最小 UI を作る。
- [x] MVVM を導入し、View にビジネスロジックを置かない。
- [x] 固定ウィンドウサイズ、Welcome Screen、Toolbar、Sidebar などを追加しない。
- [x] macOS で起動し、基本レイアウトを確認する。

**完了条件:** 空のアプリが macOS で起動し、指定された簡素な構成を表示する。

**実施結果（2026-09-27）:**

- Avalonia 12.1.3 の Desktop テンプレートを使い、Presentation を実行可能なアプリにした。Fluent テーマを Dark に固定し、既定のネイティブ Window Title Bar を維持している。
- MVVM の最小 ViewModel と空のタブコレクション、タブ行・スクロール可能な文書領域を用意した。空の起動画面に案内文などは表示しない。
- システムフォントを使い、Inter フォント・診断サポートなどテンプレート由来の任意依存を除いた。追加した NuGet は Avalonia / Desktop / Fluent の3パッケージ（MIT）。
- Release Build 成功、xUnit v3 テスト 3 件成功。`dotnet run` で macOS 起動プロセスが例外なく動作することを確認した。実行環境では画面キャプチャを取得できなかったため、レイアウトは XAML と起動状態で確認。

### Phase 3 — Domain / Application の基礎

- [x] Markdown ドキュメント、タブ、表示状態など、必要な最小限のモデルを定義する。
- [x] ファイル読込など必要な Application 抽象化を定義し、Infrastructure 側で実装する。
- [x] Domain と Application のルールを Avalonia 非依存でテストする。
- [x] 不要な Interface、Factory、Service、DI 登録を増やさない。

**完了条件:** コアロジックを UI 起動なしでテストでき、依存方向が維持されている。

**実施結果（2026-09-27）:**

- Domain に不変の `MarkdownDocument`（ファイルパスと本文）を追加した。ファイルシステムや Avalonia には依存せず、空のパスと null 本文を拒否する。
- Application に `IMarkdownFileReader` と `MarkdownDocumentLoader` を追加し、読込処理を Infrastructure に委譲する。読込失敗はここで握りつぶさず、後続のタブ表示層で個別に扱えるよう呼び出し元へ返す。
- Infrastructure の `MarkdownFileReader` は .NET 標準 API のみで UTF-8 / UTF-8 BOM を読み、不正 UTF-8 は例外として返す。追加 NuGet はない。
- Domain / Application のルールに加え、UTF-8 読込をテストし、Avalonia を起動せずに Release Build と全11テストが成功した。
- Tab / 表示状態のモデルと重複検出は、仕様が具体化する Phase 6 で実装する。現段階で先行して抽象化しない。

### Phase 4 — Markdown Parser と Document Model

- [x] Markdig の GFM 対応設定を使用する。
- [x] Markdig の型を Presentation やアプリケーション全体に漏らさず、必要な表示用 Document Model に変換する。
- [x] 見出し、段落、強調、取り消し線、リスト、引用、コード、リンク、画像、テーブル、Task List、自動リンク、水平線を対象にする。
- [x] Heading Anchor の生成規則を実装する。
- [x] 対応要素、Document Model 変換、Anchor をテストする。

**完了条件:** 代表的な GFM 文書を安定してモデル化でき、重要な変換規則にテストがある。

**実施結果（2026-09-27）:**

- Markdig `1.4.0`（BSD-2-Clause）を Infrastructure のみに追加した。Pipe Tables、Task Lists、Auto Links、Strikethrough のみを有効にし、数式や Mermaid など対象外の拡張は有効化していない。
- Application に Markdig 非依存の `MarkdownDocumentModel` とブロック/インライン型を定義し、`IMarkdownParser` の実装で見出し、段落、コード、ネストしたリスト、引用、テーブル、Task List、リンク、画像、改行、水平線を変換する。HTML は実行せず、モデルに文字列として保持する。
- 見出し文字列からアンカーを生成し、同一スラッグおよび自然発生する番号との衝突を回避する。
- GFM 構文・Document Model・重複アンカーを Infrastructure の xUnit テストで確認した。Release Build 成功、全15テスト成功、追加依存は Markdig のみ。

### Phase 5 — Native Markdown Rendering

- [x] Document Model を Avalonia のネイティブコントロールで描画する。
- [x] 見出し・本文・リスト・引用・テーブル・Task List・リンク・画像・コードブロックを読みやすく表示する。
- [x] Task List を読み取り専用にする。
- [x] Syntax Highlighting は成熟したライブラリを調査し、要件と依存影響を確認してから選択する。未知の言語指定は通常のコードとして表示する。
- [x] WebView は導入しない。導入が必要と判明した場合は、理由・代替案・影響を提示して事前確認する。
- [x] `VirtualizingStackPanel` による項目仮想化を導入し、長文の定量的な負荷測定は Phase 11 で行う。

**完了条件:** Markdown をネイティブ UI で閲覧でき、描画エラーでアプリ全体が停止しない。

**実施結果（2026-09-27）:**

- Application の Document Model を Avalonia のネイティブ `TextBlock`、`Border`、`Grid`、`StackPanel` と仮想化項目リストで描画する。見出し、本文、インライン書式、ネストしたリスト、Task List、引用、テーブル、水平線、HTML文字列、コードブロックを扱う。リンクは視覚表示のみ、画像は代替テキスト表示とし、パス解決と操作は Phase 7 で追加する。
- コードブロックには AvaloniaEdit `12.0.0` を読み取り専用で使用し、TextMate `12.0.0` と TextMateSharp.Grammars `2.0.4` で色付けする。言語名を認識できない場合は通常のコード表示にフォールバックする。Task List は読み取り専用の `☑` / `☐` として描画する。
- ライブラリ選定理由: Avalonia 自体には言語文法/構文解析エンジンがなく、自前実装は禁止されている。AvaloniaEdit + TextMate は Avalonia 12 に対応したネイティブ表示と広範な既存文法を提供するため採用。代替の ColorCode.Core `2.0.15` は MIT だが、Avalonia 用 formatter がなく、対応する描画アダプターの追加実装が必要なため採用しなかった。
- 依存/配布影響: AvaloniaEdit (MIT, NuGet 約490 KB)、AvaloniaEdit.TextMate (MIT, 約52 KB)、TextMateSharp.Grammars (MIT, 約878 KB) を直接参照し、TextMateSharp `2.0.4` と Onigwrap `1.0.11` (MIT) が推移的に加わる。Onigwrap パッケージには複数 OS 用ネイティブ資産（nupkg 約4 MB）が含まれ、macOS arm64/x64 と Windows arm64/x64 向けの資産を同梱する。別途インストールするランタイムやネットワーク取得は不要。実配布サイズは Phase 11 で測定する。
- 一時的な C# コードブロックを表示させた状態で macOS arm64 アプリを起動し、TextMate 初期化時の例外がないことを確認した。検証用サンプルは削除済み。画面キャプチャが取得できないため、レイアウトの最終目視確認は保留。
- Release Build 成功、全15テスト成功。WebView / HTML 実行 / 外部通信は追加していない。

### Phase 6 — ファイルを開く・タブ管理

- [x] UTF-8 / UTF-8 BOM の Markdown ファイル読込を実装する。
- [x] `Ctrl/Cmd + O`、ドラッグ＆ドロップ、起動時引数からファイルを開く経路を実装する。
- [x] 複数タブ、タブ切替、閉じる操作、隣接タブへの選択移動を実装する。
- [x] 正規化したパスで重複ファイルを検出し、既存タブをアクティブにする。
- [x] 読込失敗、存在しないファイル、不正 UTF-8 を対象タブ内で表示する。
- [x] タブ管理、パス重複、エラー処理をテストする。

**完了条件:** ファイルを開く方法が機能し、同一ファイルのタブ重複や1ファイルの失敗によるアプリ終了がない。

**実施結果（2026-09-27）:**

- `MarkdownFileReader` は UTF-8 / UTF-8 BOM を読み込み、不正 UTF-8 は例外を返す。
- `MainWindow` で起動時引数、Open File Dialog、キー入力、タブの閉じる操作を実装した。
- `MarkdownTabManager` と `MainWindowViewModel` により、同一ファイルの重複防止と、閉じた時の隣接タブ選択を実現した。
- `MarkdownTabManagerTests` と `MarkdownPathResolverTests` を追加し、アプリケーション層で重複検出・パス解決の基盤を検証した。
- `dotnet test mdview.sln --no-restore` で 22 件成功、失敗 0 を確認した。

### Phase 7 — リンクと画像

- [x] 外部リンクを、ユーザーがクリックした場合にのみ OS の既定ブラウザーで開く。
- [x] 相対 Markdown リンクを元ファイル基準で解決し、新規タブで開く。同じファイルが開いていれば既存タブを選択する。
- [x] Heading Anchor へのリンクで該当位置へ移動する。
- [x] 相対画像を Markdown ファイル基準で解決して表示する。
- [x] 外部画像は取得せず、画像読込失敗はプレースホルダー等で扱う。
- [x] パス解決とリンク動作をテストする。

**完了条件:** リンク・画像を安全に扱い、ネットワーク通信を発生させない。

**進行状況（2026-09-27）:**

- `MarkdownPathResolver` により相対 Markdown / 画像パスを解決する。
- Markdown のリンクをクリックすると、外部 URL は既定ブラウザーで、相対 Markdown はタブで開く。
- 見出しに生成した Anchor を付与し、同一文書内の Anchor リンクで対象位置へ移動する。
- 相対画像はローカルファイルだけを読み込み、外部 URL・存在しない画像・読込失敗は取得せずプレースホルダーを表示する。
- `MarkdownLinkHandler` とレンダラーを `MainWindow` に接続した。

### Phase 8 — ファイル変更監視と再読込

- [x] 開いているファイルを OS のファイル変更通知で監視する。
- [x] 200〜300ms 程度の debounce を行い、内容が変わらない場合は再描画を避ける。
- [x] `Ctrl/Cmd + R` による手動再読込を実装する。
- [x] ファイル削除時はタブを維持して状態を表示し、同じパスへの再作成時に再読込する。
- [x] debounce、状態遷移、再読込をテストする。

**完了条件:** 外部編集後に適切に更新され、削除・再出現・連続イベントに耐える。

**実施結果（2026-09-27）:**

- `IMarkdownFileWatcher` と `MarkdownFileWatcher` を追加し、`FileSystemWatcher` のイベントを 250ms debounce するようにした。
- 開いているタブごとに watcher を保持し、タブを閉じると watcher も破棄するようにした。
- ファイル内容が変わらない場合は再解析せず、削除時はタブを残して `Missing` 状態とエラー表示にするようにした。
- ファイルが再作成された場合は自動的に再読込し、`Ctrl/Cmd + R` でアクティブタブを手動再読込できるようにした。
- `MarkdownFileWatcherTests` を追加し、変更通知の debounce と削除通知を検証した。
- `dotnet test mdview.sln --no-restore` で 24 件成功、失敗 0 を確認した。

### Phase 9 — 検索と Zoom

- [x] `Ctrl/Cmd + F` で検索 UI を表示し、入力欄へフォーカスする。
- [x] 一致箇所のハイライト、現在位置/総件数、前後移動、閉じるボタン、`Esc` を実装する。
- [x] `Ctrl/Cmd + +` と `Ctrl/Cmd + -` で文書表示を拡大・縮小する。
- [x] Zoom の最小値・最大値を設ける。
- [x] Search と Zoom の状態・境界条件をテストする。

**完了条件:** 検索と表示サイズ変更が文書領域に限定され、通常のタブ操作を妨げない。

**進行状況（2026-09-27）:**

- `MarkdownSearchState` と `MarkdownZoomState` を追加し、検索一致数、前後移動、Zoom の境界値を Application 層で管理するようにした。
- `Ctrl/Cmd + F`、Enter / Shift+Enter、前後ボタン、Esc、`Ctrl/Cmd + +`、`Ctrl/Cmd + -` を MainWindow に接続した。
- 検索クエリの変更を各 Markdown ブロックへ反映し、一致文字列を背景色付きで表示するようにした。
- `dotnet test mdview.sln --no-restore` で 27 件成功、失敗 0 を確認した。

### Phase 10 — ファイル関連付け

- [x] Windows/macOS の関連付け方法と配布形態を調査し、OS 固有処理を Infrastructure に隔離する。
- [ ] ユーザーへ明示的に確認し、承認なしに既定アプリを変更しない。
- [x] OS ごとの処理を可能な範囲で検証する。

**完了条件:** ユーザー承認を前提に関連付けでき、OS 固有 API が UI/Core に漏れていない。

**進行状況（2026-09-27）:**

- `IFileAssociationService` を Application に追加し、OS 固有処理を Infrastructure の `FileAssociationService` に隔離した。
- Windows ではユーザー単位の `HKCU` に `.md`、`.markdown`、`.mdown` の関連付けを書き込む実装を用意した。呼び出し側が明示的に実行しない限り変更は発生しない。
- macOS では未署名・未パッケージの実行ファイルから既定アプリを安全に変更する処理を追加せず、現時点では未対応として扱う。
- Infrastructure テストで macOS 上の関連付け処理が変更を行わないことを確認した。
- ユーザー確認ダイアログと macOS の配布用 `.app` / Launch Services 登録は未完了であり、Phase 11 の配布形態確定後に続ける。

### Phase 11 — 品質確認・CI・配布

- [x] Unit Test を追加・実行し、全プロジェクトの Build/Test を通す。
- [x] GitHub Actions にシンプルな Build/Test の CI を追加し、テスト実行時は `TESTINGPLATFORM_TELEMETRY_OPTOUT=1` を設定する。
- [x] Windows と macOS の一般的な CPU アーキテクチャ向けに ZIP 配布物を作成できるようにする。インストーラーは作らない。
- [x] README に目的、対応 OS、機能、開発環境、Build/Test/Publish/配布方法、既知の制限を記載する。
- [ ] 起動速度、アイドル時メモリ、スクロール、タブ切替、描画速度を確認し、問題があれば修正する。
- [ ] 最終的に対応 OS の Build/Test と手動動作確認を行う。

**完了条件:** Definition of Done を満たし、制限事項と再現可能な開発・配布手順が文書化されている。

**進行状況（2026-09-27）:**

- `.github/workflows/ci.yml` に macOS / Windows の Release Build/Test を追加した。
- `scripts/publish.sh` に `osx-arm64`、`osx-x64`、`win-x64`、`win-arm64` 向け self-contained publish と ZIP 作成を追加した。
- `scripts/publish.sh` に任意の `--aot` オプションを追加し、Native AOT publish 時は `PublishAot=true` と `InvariantGlobalization=true` を指定するようにした。macOS arm64 でネイティブ実行ファイルの生成を確認した。
- `Microsoft.Testing.Extensions.CodeCoverage` と `scripts/coverage.sh` / `scripts/coverage.ps1` を追加し、macOS / Windows でテストカバレッジを Cobertura XML として出力できるようにした。
- `dotnet-reportgenerator-globaltool` を追加し、`coverage/report/index.html` で全体・ファイル・クラス・メソッド・行単位のカバレッジを可視化できるようにした。
- Release Build は成功し、`dotnet test mdview.sln --configuration Release --no-restore` は 28 件成功、失敗 0 だった。
- Windows 実機確認、macOS `.app` バンドル、ユーザー確認ダイアログ、性能計測は未完了である。

## 全体の完了基準

- [ ] Build と Unit Test が成功する。
- [ ] Windows と macOS で動作確認できる。
- [ ] Onion Architecture と MVVM の境界を維持する。
- [ ] Markdown Viewer / 読み取り専用という製品方針を維持する。
- [ ] 不要な依存・機能、外部画像取得、Telemetry、Markdown 内容の送信を含めない。
- [ ] 起動・描画・操作性能に重大な問題がない。
