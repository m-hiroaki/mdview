# Mermaid 表示設計

## 方針と状態

公式 Mermaid → SVG → Avalonia のベクター描画を採用する方向で設計する。
本文の Markdig / ネイティブ描画 / Onion Architecture は維持する。
macOS 先行実装に Windows 対応を追加済み。依存は Avalonia.Controls.WebView 12.1.0、Svg.Controls.Skia.Avalonia 12.0.0.17、公式 Mermaid 11.12.1。
macOS ARM64 実機で通常版と Native AOT 版の SVG 生成・描画を確認した。Windows ARM64 で図生成を検証済み。Intel Mac / Windows x64 の実機確認は未実施。
ユーザーが Mermaid 対応とこの方式を明示的に要求したため、AGENTS.md 第6項の対象外条件を解除する。
WebView は図生成専用。Markdown 本文を HTML 化して表示する用途には使わない。

## 対象範囲

- Markdown の fenced code block の言語指定 `mermaid` を認識する。
- Windows ARM64 / x64、macOS ARM64 / x64 を対象とし、フローチャートとシーケンス図を動作保証する。
- その他の図種は検証後に対応範囲を広げる。独自の Mermaid 構文解析・レイアウトエンジンは作らない。
- 常にダークテーマ、読み取り専用。図内リンク、コールバック、アニメーション、外部画像、外部アイコン、HTML ラベル、ユーザー指定 CSS は初期対象外。
- PNG 化やスクリーンショットを通常表示経路に入れない。

## 処理経路と責務

```text
MarkdigMarkdownParser
  → MarkdownMermaidBlock(Source)
  → MermaidDiagramViewModel
  → MermaidDiagramService（生成要求・キャッシュ）
  → IMermaidSvgGenerator
      macOS: WKWebView / Windows: WebView2
      共通のローカル HTML + 公式 Mermaid + JS ブリッジ
  → SVG の検証
  → MermaidSvgDocument（SVG 文字列・viewBox）
  → MermaidDiagramView（SVG 描画ライブラリでベクター描画）
```

| 層 | 追加・変更する要素 |
|---|---|
| Domain | 追加なし。OS や SVG ライブラリを持ち込まない |
| Application | Mermaid ブロック、生成インターフェース、結果、生成ユースケース、上限付き SVG キャッシュ |
| Infrastructure | 公式 Mermaid 資産、JS ブリッジ、OS WebView アダプター、SVG 検証 |
| Presentation | 専用 ViewModel、SVG Control、ブロック振り分け、表示資源の解放 |

最初のインターフェースは `IMermaidSvgGenerator.GenerateAsync(string source, CancellationToken cancellationToken)` 一つとする。
結果は成功 SVG または構造化されたエラーを返し、Avalonia、SkiaSharp、WKWebView の型は公開しない。
キャンセルは通常のキャンセルとして扱い、ユーザー向け生成失敗に変換しない。
OS 分岐は起動時の composition root に一か所だけ置く。Windows 実装用の空プロジェクトや汎用プラグイン機構は作らない。

## macOS の生成環境

1. Mermaid が必要になった時に生成用 WKWebView を一つ遅延生成する。
2. 非永続の Web データストアを使い、同梱 HTML / JavaScript だけを読み込む。HTTP サーバーは起動しない。
3. WebView に計測可能なホスト領域を用意する。画面には表示しないが、`display:none` にして文字計測を壊さない。
4. 資産読み込み・Mermaid 初期化・フォント準備の完了通知を待つ。
5. JSON でソースと要求 ID を渡し、`mermaid.render` を呼ぶ。ソースを HTML や実行コードとして連結しない。
6. 非同期完了メッセージで SVG またはエラーを受け取る。Mermaid のイベント binding は実行しない。
7. 一時 DOM を片付け、次の要求を処理する。

初期候補は Avalonia NativeWebView の利用。WKWebView の非永続ストア、要求制限、非表示ホスト、ライフサイクルを扱えるか検証する。
扱えない場合は Infrastructure 内の薄い WKWebView アダプターを検討する。その際は .NET からの Objective-C 接続、SDK、配布資産と AOT への影響を比較し、独自の大きな interop 層は避ける。
NativeWebView の採用・直接接続の採用はこの検証後に決める。
WebView の作成・JS 呼び出し・解放は macOS のメインスレッドで行う。非同期 API はバックグラウンド実行を意味しない。

生成は一度に一件。暫定上限はソース 50,000 文字、生成 10 秒、出力 SVG 5 MiB とし、実測で調整する。
キャンセル後も JavaScript が走る場合があるため、要求 ID で古い結果を破棄する。
タイムアウト・Web プロセス障害時は生成環境を破棄し、次の要求で再作成する。永久に待つキューを残さない。
タブ終了時は不要な待機要求を取り消し、アプリ終了時はイベント購読と WebView を解放する。

## 共通 Mermaid 設定と SVG の互換性

- 公式 Mermaid のバージョンと必要な依存資産を固定して同梱する。CDN は使用しない。
- `startOnLoad: false`、`securityLevel: strict`、`theme: dark`、ルート設定 `htmlLabels: false` を固定する。
- 文書内の frontmatter / directive がこれらの固定値や資産取得設定を上書きできないよう、採用バージョンで確認する。保証できない設定指定は生成前に明示的なエラーにする。
- OS のフォント設定を生成と描画の両方に渡す。macOS と Windows で同じピクセル配置になることは保証しない。
- SVG の CSS、`text` / `tspan`、marker、clipPath、transform、viewBox の対応を検証する。
- WebKit で CSS を描画属性へ解決し、アニメーション用 style を除去する。ネストした tspan の解釈差を避けるため、文字 run を WebKit で測ったベースライン位置の SVG text に変換する。画像化や文字のパス化は行わない。
- `foreignObject` を含む SVG は対応外としてエラー表示する。タグを削除してラベル欠落を成功扱いにしない。
- 有限かつ正の viewBox 寸法を検証し、過大な寸法・要素数を制限する。
- DTD / 外部エンティティ、script、イベント属性、外部参照、外部 CSS、埋め込み画像などを拒否する。内部の `#id` 参照は許可する。
- SVG ライブラリの外部リソース resolver も無効化する。WebView の通信遮断だけでは SVG 描画時の通信を防げない。

## Avalonia での描画・Zoom

Svg.Skia 系の Avalonia コントロールを第一候補とする。Avalonia 12.1.3 / SkiaSharp との互換性を確認し、必要最小限のパッケージだけを選ぶ。
SVG をベクター描画命令として保持し、Control の描画時に現在のスケールで描画する。Bitmap へ事前変換しない。
通常は文書幅に収め、縦横比を保つ。大きな図は Zoom と文書領域のスクロールで読む。
Zoom / Retina の倍率変更では Mermaid の再生成を行わない。
現在の Zoom は RenderTransform によるため、拡大後の図全体がスクロール範囲に入るか検証する。必要なら文書領域のレイアウト寸法にも Zoom を反映する。
SVG の viewBox 単位を Avalonia の DIP に対応させ、Retina の物理ピクセル倍率は描画側に任せる。
OS フォントの fallback・日本語 shaping が WebKit と SVG 描画側で異なる可能性があるため、日本語・絵文字・多言語ラベルを確認する。

## キャッシュ・表示状態・検索

- SVG キャッシュキーはソース、Mermaid バージョン、固定設定、フォント識別情報。Zoom / 検索語は含めない。
- SVG 文字列は合計バイト数と件数で上限を設ける。生成中の同じ要求も共有する。
- SVG の描画資源は Presentation が所有する。ビューから外れた時に解放し、再表示では SVG キャッシュから復元できる。
- 非アクティブ文書の全図を起動時に生成しない。表示が必要な図から生成する。
- 再読込・検索・タブ切り替えで同じソースの図を再生成しない。
- ViewModel の状態は未生成 / 生成中 / 表示可能 / エラー。古い文書の非同期結果を新しいブロックへ適用しない。
- 生成中は図領域に短いメッセージ。失敗時は短いエラーと読み取り専用ソースを表示する。
- 初期版は図内ラベルの検索ハイライトを行わない。Mermaid ソースは通常検索の対象から除外し、件数と可視ハイライトが食い違わないよう検索用テキストの組み立てを変更する。

## Windows 対応

`WebViewMermaidSvgGenerator` を Windows / macOS で共用し、composition root で対応 OS を選択する。
既存の Avalonia.Controls.WebView 12.1.0 が Windows で WebView2 を使用するため、新しい NuGet パッケージは追加しない。
JS ブリッジ、Mermaid 資産、SVG 検証、キャッシュ、ViewModel、Avalonia の描画を再利用する。

- WebView2 Evergreen Runtime は OS 側の導入を前提とする。Microsoft の手順に従い、マシン / ユーザーの登録バージョンを図生成前に確認する。未導入時は図の領域にエラーと元のコードを表示する。
- Runtime は ZIP に同梱しない。実行時の自動ダウンロード・インストールもしない。固定バージョン Runtime / Edge プレビュー版の利用は対象外。
- 非公開プロファイルを使い、ブラウザーデータの保存先は `%LOCALAPPDATA%/mdview/MermaidWebView2` とする。実行ファイルの隣への書き込みは不要。WebView2 の管理ファイルはディスクに残る場合がある。
- Windows は小さな固定 HTML を読み込み、NavigationCompleted 後に同梱の Mermaid とブリッジをネイティブの InvokeScript で実行する。大きな Mermaid bundle を HTML に含めず、WebView2 の NavigateToString の 2 MiB 制限にも抵触しない。CSP はページ側のスクリプト・画像・接続・フレームを禁止する。
- Avalonia の Windows アダプターは HTML を data URI に変換するため、生成した HTML と完全一致する data URI と about:blank のみ許可する。他の data URI、外部 URL、ローカルファイルへの移動、新しいウィンドウは禁止する。
- macOS の nonce 付きインライン資産と非永続ストアの設定は維持する。
- WebView の操作・解放は UI スレッドで行い、生成は一度に一件。既存のタイムアウト、キャンセル、破棄後の再作成を共用する。

Windows ARM64 実機でフローチャート、シーケンス図、日本語ラベル、構文エラー後の次の図の生成と既存 SvgSource による SVG の読み込みを確認した。
Windows x64、Windows Native AOT、Web プロセス強制終了、Runtime 未導入の実機検証は未実施。
この Windows 開発環境ではアプリケーション制御が Avalonia.Generators.dll をブロックするため、Presentation を含む通常ビルドの再検証は未完了。図生成・SVG 読み込みは別の XAML 不使用の検証ホストで確認した。コード生成相当の検証専用 partial class を一時的に追加した Release ビルドは成功した（製品ソースには含めない）。xUnit は Domain 5 件、Application 19 件、Infrastructure 43 件が成功した。

## 依存関係の判断

| 候補 | 必要性・代替 | Runtime / 配布 / ライセンス |
|---|---|---|
| 公式 Mermaid 資産 | 公式構文解析と配置。標準 C# / Avalonia に同等機能なし。自作は保守負担が大きい | MIT。本体と推移的依存のライセンスを収集。JS 資産分増加。Node.js はビルド時の資産作成のみで、利用者環境に不要 |
| WebView 接続パッケージ | Mermaid に DOM と文字計測を提供。CLI は Node.js / ブラウザー同梱が重い | macOS は OS の WKWebView、将来 Windows は WebView2。候補パッケージのライセンス・RID・AOT 対応は選定前確認 |
| Svg.Skia 系 | SVG を既存描画基盤でベクター表示。標準 Image だけでの対応は前提にしない | リポジトリは MIT。選定パッケージと全依存を確認。Windows / macOS の publish 検証が必要 |

macOS ARM64 の Release / self-contained publish では、変更前 124,525,691 bytes、追加後 132,953,517 bytes。増分は約 8.0 MiB。
同じ deflate 圧縮条件の ZIP は 46,279,751 bytes → 49,490,923 bytes（約 3.1 MiB 増）。
主な増分は Mermaid を含む Infrastructure DLL 約 2.6 MiB、WebView DLL 約 1.4 MiB、SVG 関連 DLL、SkiaSharp のネイティブ資産差分約 0.6 MiB。
Mermaid の npm 配布アーカイブは公式レジストリの SHA-512 integrity と照合した。同梱 bundle のライセンスコメントを保持し、関連 notices / ライセンスを `licenses/` に出力する。
Native AOT は現在の配布で任意のため、通常 publish を先に保証し、AOT の成否と制約を別途記録する。

## 検証と実装順序

1. macOS で非表示 WKWebView → 公式 Mermaid → SVG 文字列取得の最小試作。
2. SVG 描画候補でフローチャート / シーケンス図を表示。CSS、矢印、日本語、改行、Retina、Zoom を確認。
3. 資産制限・SVG 検証・タイムアウト・プロセス障害復帰・配布サイズとメモリを評価し、依存を確定。
4. Markdig の専用ブロック変換、Application の生成とキャッシュ、専用 ViewModel / View を統合。
5. 自動再読込・検索・タブ・Zoom と統合し、通常 Markdown の回帰を確認。
6. osx-arm64 / osx-x64 の publish と実機確認。Windows Build を維持。

xUnit: fenced block 認識、引用・リスト内の図、通常コード維持、キャッシュ共有・上限、キャンセル、古い結果破棄、SVG 制限、検索除外、生成失敗。
macOS 統合検証: WKWebView の読み込みと bridge、外部要求遮断、文字計測、SVG 描画、複数図、障害復帰、閉じたタブへの遅延応答。
性能測定: Mermaid なしの起動、初回図生成、再表示、Zoom、多数図、WebView 初期化後のアイドルメモリ。
Build → Test → 次段階のサイクルを維持する。

### 実施済みの検証

Mermaid を含む文書は非仮想化の StackPanel で配置する。図の非同期読み込みと仮想化の推定高さが干渉してスクロール位置が戻るため、図のサイズを明示し、通常 Markdown のみ仮想化を維持する。図を多数含む文書は Visual Tree とメモリ使用量が増える。

- Release ビルド、51 件の xUnit テストが成功。
- macOS ARM64 の通常版と Native AOT 版で、20 図を含む文書の末尾までのスクロール、先頭からの再スクロール、150% 拡大表示、通常文書に切り替えた際の仮想化復帰を確認。
- macOS ARM64 の Native AOT で、WKWebView の初期化、図生成、SVG ベクター描画、日本語ラベル、構文エラー後の生成、キャンセルを確認。
- JavaScript に渡すソースの JSON 化は事前生成した型情報を使う。Infrastructure.Tests では JSON リフレクションを無効にして、AOT と同じ制約でシリアライズの回帰を検出する。
- macOS ARM64 の WKWebView で、フローチャート、シーケンス図、subgraph、日本語の複数行ラベル、loop / Note の SVG 生成とベクター描画を確認。
- WebView 初期化込みの小さな図の生成・描画はローカル検証で約 0.6～0.9 秒、初期化後の別の図は約 0.02～0.07 秒。恒常的な性能保証値ではない。
- 構文エラー、設定 directive 拒否、キャンセル、その後の生成を確認。
- Retina 相当の 192 DPI 描画で日本語・線・矢印・文字位置を確認。
- osx-arm64 / osx-x64 / win-x64 / win-arm64 の通常 self-contained publish が成功。最終 osx-arm64 配布物の起動と図表示を実機確認。
- 配布物の `licenses/` に関連ライセンス・bundle notices が出力されることを確認。
- Intel Mac / Windows x64 の実機確認、各 OS の他 RID での Native AOT、Web プロセス強制終了・タイムアウトの実機検証、多数図とアイドルメモリの定量測定は未実施。

## 参照

- [Mermaid API](https://mermaid.js.org/config/usage.html)
- [Mermaid 設定・htmlLabels](https://mermaid.js.org/config/schema-docs/config.html)
- [Avalonia NativeWebView](https://docs.avaloniaui.net/controls/web/nativewebview)
- [WebView 環境設定](https://docs.avaloniaui.net/controls/web/webview-environment)
- [WebView2 Runtime の検出・配布](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution)
- [WebView2 のローカルコンテンツと HTML サイズ制限](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/working-with-local-content)
- [Svg.Skia / Avalonia 対応](https://github.com/wieslawsoltes/Svg.Skia)
- [Mermaid ソースとライセンス](https://github.com/mermaid-js/mermaid)
