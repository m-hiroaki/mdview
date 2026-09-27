# mdview — AGENTS.md

## 1. プロジェクト概要

### アプリケーション名

**mdview**

### 目的

`mdview` は、Markdown ファイルを素早く開いて、快適に読むための軽量な Markdown Viewer です。

本アプリケーションは**Markdown エディタではなく、閲覧専用の Viewer**です。

IDE や Markdown エディタのような多機能なアプリケーションではなく、画像ビューアーや PDF ビューアーに近い、シンプルで軽量な操作感を目指します。

> **mdview は Markdown Viewer であり、Markdown Editor ではない。**

シンプルさそのものを重要な機能として扱ってください。

不要な機能、過剰な抽象化、過度なアーキテクチャ設計は追加しないでください。

---

# 2. 基本方針

実装方針を決定する際は、以下を優先してください。

1. 依存ライブラリが少ない
2. コード量が少ない
3. 実行時コンポーネントが少ない
4. アーキテクチャがシンプル
5. 起動が速い
6. メモリ使用量が少ない
7. テストしやすい
8. 保守しやすい

**Simplicity is a feature.**

技術的に複数の実現方法がある場合は、上記の優先順位を基本としてください。

---

# 3. 対応プラットフォーム

以下を対象とします。

* Windows
* macOS

CPU アーキテクチャについては、対象プラットフォームで一般的なものをサポートします。

想定する配布物の例：

```text
mdview.exe
mdview.app

mdview-win-x64.zip
mdview-win-arm64.zip
mdview-mac-arm64.zip
```

インストーラーは当初作成しません。

ZIP を展開して利用できる形式を基本とします。

---

# 4. 開発技術

以下を使用します。

* C#
* .NET
* Avalonia UI
* MVVM
* Onion Architecture
* xUnit
* Git
* GitHub

.NET は、実装開始時点で利用可能な **最新の LTS 版**を使用してください。

非 LTS 版よりも LTS 版を優先します。

---

# 5. アーキテクチャ

**Onion Architecture** を採用します。

基本構成：

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

依存方向：

```text
Presentation
     ↓
Application
     ↓
Domain
```

Infrastructure は、内側のレイヤーで定義されたインターフェースを実装します。

---

## 5.1 Domain

Domain には、アプリケーションの中心となる概念・ルールを配置します。

例：

* Markdown ドキュメント
* Heading
* Tab
* Document State
* Zoom State
* Search State

Domain は以下に依存してはいけません。

* Avalonia
* ファイルシステム
* Windows API
* macOS API
* WebView
* OS 固有 API
* 外部サービス

可能な限り純粋な C# としてください。

---

## 5.2 Application

Application には、ユースケースと抽象化を配置します。

例：

* Markdown ファイルを開く
* Markdown ファイルを再読み込みする
* タブを管理する
* ファイル変更を処理する
* 相対リンクを解決する
* Zoom を変更する
* 検索する

必要に応じて以下のようなインターフェースを定義します。

```text
IFileService
IFileWatcher
IFileAssociationService
IMarkdownParser
IExternalBrowserService
```

ただし、必要性のないインターフェースを機械的に作成しないでください。

---

## 5.3 Infrastructure

Infrastructure には、外部環境との接続を配置します。

例：

* ファイルシステム
* ファイル監視
* Windows のファイル関連付け
* macOS のファイル関連付け
* 外部ブラウザー起動
* Markdown Parser
* Syntax Highlighting

OS 固有処理は Infrastructure に閉じ込めてください。

---

## 5.4 Presentation

Presentation には Avalonia UI を配置します。

例：

* View
* ViewModel
* Command
* Tab UI
* Search UI
* Zoom
* Markdown 表示
* キーボードショートカット

View や Code-behind にビジネスロジックを記述しないでください。

ただし、UI 固有の処理については、合理的な理由がある場合に限り Code-behind を使用して構いません。

---

# 6. Markdown 対応

Markdown は **GitHub-Flavored Markdown（GFM）相当**を基本とします。

対象は GitHub README で一般的に使用される Markdown とします。

## 必須対応

* 見出し
* 段落
* 太字
* 斜体
* 打ち消し線
* 順序付きリスト
* 順序なしリスト
* ネストされたリスト
* 引用
* インラインコード
* fenced code block
* Syntax Highlighting
* リンク
* 相対 Markdown リンク
* 画像
* テーブル
* Task List
* URL の自動リンク
* 水平線

---

## 初期バージョンでは対応しないもの

以下は明示的に要求されない限り実装しません。

* LaTeX
* 数式
* Mermaid
* Markdown 編集
* WYSIWYG
* HTML 編集

---

# 7. Markdown Parser

Markdown Parser には **Markdig** を使用します。

GFM 対応設定を使用してください。

ただし、Markdig 固有の型をアプリケーション全体に漏らさないでください。

可能な限り、

```text
Markdown
   ↓
Markdig
   ↓
アプリケーション用 Document Model
   ↓
Avalonia
```

という構造にしてください。

Markdig を直接 UI が操作するような設計は避けてください。

---

# 8. Markdown の描画

基本方針として、**Avalonia のネイティブコントロールによる描画を優先**します。

基本構成：

```text
Markdown ファイル
        ↓
      Markdig
        ↓
Application / Document Model
        ↓
Avalonia Native Controls
```

WebView を「HTML を表示できるから」という理由だけで導入してはいけません。

以下を優先してください。

* Avalonia Native Control
* C#
* .NET
* シンプルな描画ロジック

WebView が必要になる機能が存在する場合は、

1. なぜ Native Control では実現できないのか
2. WebView によるメリット
3. WebView によるデメリット
4. より単純な代替手段

を検討した上で導入してください。

アーキテクチャ上重要な変更については、勝手に WebView に変更しないでください。

---

# 9. 画像

Markdown 内の画像を表示します。

対象は**相対パスの画像**とします。

例：

```markdown
![Architecture](images/architecture.png)
```

画像は Markdown ファイルを基準として相対パスを解決してください。

外部 URL の画像をダウンロードして表示してはいけません。

Avalonia が対応する画像形式を使用してください。

画像読み込みに失敗した場合は、アプリケーションを停止せず、画像領域にエラーまたはプレースホルダーを表示してください。

---

# 10. リンク

## 外部リンク

外部 URL は OS のデフォルトブラウザーで開きます。

---

## 相対 Markdown リンク

例えば、

```markdown
[Architecture](docs/architecture.md)
```

の場合、対象 Markdown ファイルを **新しいタブ**で開きます。

ただし、既に同じファイルがタブとして開かれている場合は、新しいタブを作成せず、そのタブをアクティブにしてください。

---

## Heading Anchor

Heading にはアンカーを生成してください。

例えば、

```markdown
## Architecture
```

に対して、

```markdown
[Architecture](#architecture)
```

のようなリンクが動作するようにしてください。

可能な範囲で GitHub と互換性のある挙動を目指します。

---

# 11. Task List

GFM の Task List を表示します。

例：

```markdown
- [x] Completed
- [ ] Not completed
```

表示例：

```text
☑ Completed
☐ Not completed
```

チェックボックスは**読み取り専用**です。

クリックしても Markdown ファイルを変更してはいけません。

---

# 12. ファイルエンコーディング

対応するエンコーディング：

* UTF-8
* UTF-8 BOM

以下は初期バージョンでは自動判定・対応しません。

* Shift-JIS
* UTF-16
* その他の文字コード

UTF-8 として読み込めない場合は、対象ドキュメント領域にエラーを表示してください。

そのファイルのエラーによってアプリケーション全体を終了させてはいけません。

---

# 13. ファイルを開く

以下の方法で Markdown ファイルを開けるようにします。

1. `.md` ファイルをダブルクリック
2. ドラッグ＆ドロップ
3. Windows: `Ctrl + O`
4. macOS: `Cmd + O`

開いたファイルはタブとして表示します。

---

# 14. ファイル関連付け

Windows と macOS の両方で `.md` ファイルの関連付けに対応します。

初回起動時に、

> `.md` ファイルを mdview で開くように設定しますか？

のように確認してください。

ユーザーの明示的な承認なしに、デフォルトアプリケーションを変更してはいけません。

OS 固有の登録処理は、

```text
IFileAssociationService
```

などの抽象化を介して実装してください。

---

# 15. タブ

複数の Markdown ファイルをタブで開けるようにします。

各タブには、

* ファイル名
* 閉じるボタン

を表示します。

タブをクリックすると、そのタブをアクティブにします。

タブを閉じた場合は、隣接するタブをアクティブにします。

---

## 15.1 重複ファイル

同じ Markdown ファイルを複数回開いた場合、新しいタブを作成しません。

既に開いているタブをアクティブにしてください。

ファイル比較には、必要に応じて正規化・Canonical Path を使用してください。

Windows と macOS のパス比較仕様の違いも考慮してください。

---

## 15.2 保存確認

mdview は読み取り専用 Viewer です。

そのため、タブを閉じる際の保存確認は不要です。

---

## 15.3 セッション保存

以下は初期バージョンでは実装しません。

* 開いていたタブの保存
* アプリ終了時のセッション保存
* 起動時のタブ復元

アプリケーションを再起動すると、タブは復元されません。

---

# 16. ファイル変更監視

開いている Markdown ファイルを OS のファイル変更通知によって監視します。

例えば VS Code などでファイルを保存した場合、

```text
ファイル変更
    ↓
変更通知
    ↓
200～300ms 程度 debounce
    ↓
ファイル再読み込み
    ↓
Markdown 再解析
    ↓
再描画
```

とします。

---

## 16.1 Debounce

エディタによっては、1回の保存で複数のファイル変更イベントが発生する場合があります。

そのため、約 **200～300ms** の debounce を使用してください。

具体的な値は実装上妥当な範囲で調整して構いません。

---

## 16.2 内容が変わっていない場合

ファイル変更イベントが発生しても、内容が変わっていない場合は不要な再描画を避けてください。

---

## 16.3 ファイル削除

開いているファイルが削除された場合でも、タブを閉じないでください。

タブを残したまま、

> ファイルが見つかりません

などの状態を表示します。

アプリケーションは継続して利用できる必要があります。

---

## 16.4 ファイル再出現

削除されたファイルが同じパスに再作成された場合は、自動的に再読み込みしてください。

---

## 16.5 手動再読み込み

Windows：

```text
Ctrl + R
```

macOS：

```text
Cmd + R
```

---

# 17. UI

UI は極力シンプルにします。

基本構成：

```text
┌─────────────────────────────────────┐
│ OS Native Title Bar                 │
├─────────────────────────────────────┤
│ Tab1   Tab2   Tab3                  │
├─────────────────────────────────────┤
│                                     │
│        Markdown Document            │
│                                     │
│                                     │
└─────────────────────────────────────┘
```

---

## 17.1 実装しない UI

初期バージョンでは以下を追加しません。

* Toolbar
* Status Bar
* Sidebar
* Navigation Panel
* Ribbon
* Welcome Screen
* Dashboard
* File Explorer
* Settings Screen

必要になった場合のみ追加してください。

---

## 17.2 Title Bar

可能な限り OS のネイティブ Title Bar を使用してください。

独自 Title Bar を描画する必要はありません。

Windows/macOS 共通化のためだけに独自 Title Bar を作らないでください。

---

## 17.3 初期ウィンドウサイズ

アプリケーション側で固定サイズを強制しません。

初期ウィンドウサイズは OS / Avalonia に任せます。

複雑なウィンドウ位置・サイズの永続化も初期バージョンでは実装しません。

---

# 18. テーマ

**常にダークテーマ**とします。

以下は初期バージョンでは実装しません。

* Light Theme
* OS Theme による自動切り替え
* Custom Theme

ダークテーマは、長時間 Markdown を読む用途に適した配色としてください。

---

# 19. フォント

システムフォントを優先します。

原則としてフォントをアプリケーションに同梱しません。

目安：

```text
本文: 約16px
コード: 約14px
```

ただし、最終的なサイズは読みやすさを優先して調整してください。

---

# 20. Zoom

Markdown の表示サイズを変更できるようにします。

Windows：

```text
Ctrl + +
Ctrl + -
```

macOS：

```text
Cmd + +
Cmd + -
```

Zoom はウィンドウサイズではなく、Markdown の表示サイズに影響します。

最小値・最大値を設定し、極端な拡大・縮小を防いでください。

必要であれば Zoom をデフォルト値に戻す機能を追加して構いません。

ただし、そのためだけに UI を複雑化しないでください。

---

# 21. 検索

以下のショートカットで検索 UI を表示します。

Windows：

```text
Ctrl + F
```

macOS：

```text
Cmd + F
```

検索バーは Markdown ドキュメント領域の上部に小さく表示します。

---

## 必須機能

* ショートカットで表示
* 検索入力へ自動フォーカス
* 一致箇所のハイライト
* 現在位置 / 総件数を表示
* 次の一致
* 前の一致
* `Esc` で閉じる
* 閉じるボタン

例：

```text
2 / 5
```

複雑な全文検索インデックスは不要です。

---

# 22. Syntax Highlighting

コードブロックの Syntax Highlighting に対応します。

自前で Syntax Highlighting エンジンを実装してはいけません。

成熟した既存ライブラリを使用してください。

対象言語の例：

* C#
* C++
* C
* Python
* JavaScript
* TypeScript
* JSON
* XML
* HTML
* CSS
* Bash
* Shell
* PowerShell
* YAML
* Markdown

使用するライブラリによって追加可能な言語がある場合は、そのライブラリの能力を活用してください。

未知の言語指定であっても、エラーにせず読みやすいコードとして表示してください。

---

# 23. パフォーマンス

以下の順番を重視します。

1. 起動速度
2. アイドル時メモリ使用量
3. スクロールの応答性
4. タブ切り替えの応答性
5. Markdown の描画速度
6. 不要なメモリアロケーションの削減

以下を避けてください。

* タブ切り替えのたびに全 Markdown を再描画
* 変更されていない Markdown の再解析
* OS 通知が利用できるのに常時ポーリング
* 巨大画像を常にフルサイズで読み込む
* 不必要に巨大な Visual Tree
* UI Thread の長時間ブロック

---

# 24. 軽量性

以下のようなライブラリ・技術は、明確な理由がない限り導入しません。

* WebView
* Chromium
* Electron
* JavaScript Runtime
* 大規模 UI Framework
* サードパーティ DI Container
* Database
* 大規模 Logging Framework

依存関係を追加する場合は、必ず「本当に必要か」を検討してください。

---

# 25. MVVM

MVVM を一貫して使用します。

基本的には、

```text
View
  ↓
ViewModel
  ↓
Application
  ↓
Domain
```

という流れにしてください。

View にビジネスロジックを記述しないでください。

---

# 26. Dependency Injection

DI が必要な場合は、.NET 標準の DI を使用します。

サードパーティの DI Container は使用しません。

また、すべてのクラスを DI Container に登録するような設計は避けてください。

単純なクラスまで無理に DI 化しないでください。

---

# 27. エラー処理

Viewer は高い耐障害性を持つ必要があります。

1つのファイルの問題によってアプリケーション全体が終了してはいけません。

例えば以下の場合でも継続動作してください。

* ファイルが存在しない
* ファイルを読み込めない
* UTF-8 として不正
* 画像読み込み失敗
* Markdown Parse エラー
* 相対リンク先が存在しない

エラーは該当するタブまたはドキュメント領域に表示します。

一般ユーザー向け UI に Stack Trace を表示しないでください。

---

# 28. テスト

テストフレームワークには **xUnit** を使用します。

特に以下を Unit Test の対象としてください。

* Markdown Parsing
* Document Model への変換
* 相対画像パス解決
* 相対 Markdown Link 解決
* Heading Anchor 生成
* 重複ファイル検出
* ファイル状態管理
* File Change Debounce
* Tab Management
* Zoom State
* Search
* Error Handling

---

# 29. テスト容易性

Core Logic は Avalonia を起動せずにテストできるようにしてください。

以下を避けます。

* Static State
* Global State
* Application 層から直接 `File.ReadAllText` を呼ぶ
* Application 層から直接 OS API を呼ぶ

例えば、

```csharp
public interface IFileService
{
    Task<string> ReadTextAsync(string path);
}
```

のような抽象化を利用し、テスト時には Mock / Fake に置き換えられるようにしてください。

ただし、テスト容易性だけを理由に過剰な抽象化を行わないでください。

---

# 30. Git / GitHub

Git と GitHub を使用します。

推奨構成：

```text
/
├── src/
├── tests/
├── docs/
├── .github/
├── README.md
├── LICENSE
├── .gitignore
└── copilot-instructions.md
```

GitHub Actions を利用して、

* Build
* Unit Test
* 対応プラットフォームの検証

などを実行します。

ただし CI は可能な限りシンプルにしてください。

---

# 31. README

README には最低限、以下を記載してください。

* mdview の目的
* 対応プラットフォーム
* 主な機能
* Build 方法
* 開発環境
* Test 方法
* Publish 方法
* 配布方法
* 既知の制限

ドキュメントは簡潔に保ってください。

---

# 32. セキュリティ / プライバシー

mdview は原則としてネットワーク通信を行いません。

禁止事項：

* 外部画像の自動ダウンロード
* Markdown 内容の外部送信
* Telemetry
* Usage Analytics
* User Account
* Cloud Sync
* 外部サービスへの Markdown アップロード

外部 URL は、ユーザーが明示的にクリックした場合に限り OS のデフォルトブラウザーで開いて構いません。

---

# 33. 不要な機能

ユーザーから明示的に要求されない限り、以下を実装しないでください。

* 最近開いたファイル
* Favorites
* Bookmarks
* 印刷
* PDF Export
* Markdown 編集
* Auto Save
* Cloud Sync
* Plugin / Extension
* Custom Theme
* Light Theme
* Settings Screen
* Telemetry
* Analytics
* Account
* Network Synchronization

---

# 34. 開発手順

以下の順番を基本とします。

### Phase 1

Solution / Project 作成

### Phase 2

Avalonia UI の構築

### Phase 3

Onion Architecture の構築

### Phase 4

Unit Test 環境構築

### Phase 5

Markdown Parser

### Phase 6

Document Model

### Phase 7

基本的な Native Rendering

### Phase 8

単一 Markdown ファイルの Open

### Phase 9

Tabs

### Phase 10

Duplicate File Detection

### Phase 11

Drag & Drop

### Phase 12

File Association

### Phase 13

File Watching

### Phase 14

Auto Reload

### Phase 15

Links / Images

### Phase 16

Syntax Highlighting

### Phase 17

Search

### Phase 18

Zoom

### Phase 19

Error Handling

### Phase 20

Packaging

### Phase 21

CI

### Phase 22

Startup / Memory / Performance の計測

### Phase 23

問題修正

### Phase 24

Release

各段階で、

```text
Build
↓
Test
↓
次の機能
```

というサイクルを維持してください。

---

# 35. AI によるコード生成ルール

GitHub Copilot / AI は、以下のルールを守ってください。

## 35.1 要件を勝手に追加しない

仕様に記載されていない機能を勝手に追加しないでください。

特にアーキテクチャに影響する仕様が不明確な場合は、実装前に確認してください。

---

## 35.2 最も単純な実装を優先

複数の実装方法がある場合は、以下を優先してください。

1. シンプル
2. コード量が少ない
3. 依存関係が少ない
4. テストしやすい
5. 保守しやすい

---

## 35.3 過剰設計を避ける

将来必要になるかもしれない、という理由だけで以下を作らないでください。

* 抽象化
* Interface
* Factory
* Strategy
* Service
* Repository
* Event Bus
* Plugin Architecture

現在の要求に必要な場合のみ導入してください。

---

# 36. アーキテクチャ変更

以下のような変更は、勝手に行ってはいけません。

* Native Rendering → WebView
* Markdig → 別 Markdown Parser
* Onion Architecture の変更
* 対応 OS の変更
* 大規模ライブラリの導入
* Database の導入
* 配布方式の変更

アーキテクチャレベルの変更が必要になった場合は、まず以下を説明してください。

1. 現在の設計ではなぜ不足しているのか
2. 提案する設計
3. 影響するコンポーネント
4. より単純な代替案
5. 変更によるメリット・デメリット

その上で承認を得てから変更してください。

---

# 37. NuGet パッケージ追加ルール

新しい NuGet パッケージを追加する場合は、以下を明確にしてください。

1. 何のために必要か
2. なぜ標準ライブラリ / Avalonia では実現できないのか
3. 代替手段
4. 配布サイズへの影響
5. 新しい Runtime の必要性
6. Windows / macOS 対応状況
7. ライセンス

特に理由がなければ、依存関係を増やさないでください。

---

# 38. Definition of Done

機能は、以下を満たした時点で完成とします。

* Build が成功する
* Unit Test が成功する
* 必要な Unit Test が追加されている
* 対象プラットフォームで動作する
* 不要な依存関係が追加されていない
* 無関係な機能を追加していない
* Onion Architecture の境界が維持されている
* UI がシンプルに保たれている
* パフォーマンスを不必要に悪化させていない

---

# 39. 製品コンセプト

mdview の理想的な利用方法は以下です。

```text
README.md をダブルクリック
        ↓
mdview が素早く起動
        ↓
Markdown がすぐ読める
        ↓
必要なら他の Markdown をタブで開く
        ↓
読む
        ↓
閉じる
```

mdview は IDE ではありません。

Markdown Editor でもありません。

複雑な設定を必要とするアプリケーションでもありません。

**Markdown を素早く開いて読むための、小さく、軽く、シンプルな Viewer** を目指してください。

---

# 40. 最終原則

実装上の判断に迷った場合は、以下の原則を優先してください。

> **mdview は Markdown Viewer であり、Markdown Editor ではない。**

> **シンプルさは機能である。**

そして、技術的に成立する複数の選択肢がある場合は、

```text
依存関係が少ない
      ↓
コードが少ない
      ↓
実行時コンポーネントが少ない
      ↓
アーキテクチャが単純
      ↓
起動が速い
      ↓
メモリ使用量が少ない
      ↓
テストしやすい
      ↓
保守しやすい
```

という観点で判断してください。
