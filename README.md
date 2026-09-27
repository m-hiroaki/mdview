# mdview

mdview は Markdown ファイルを素早く開いて読むための、軽量なデスクトップ Viewer です。Markdown の編集機能を持たず、シンプルな閲覧体験を目指しています。

> **開発中:** 基本的な Markdown 表示、タブ、リンク、画像、検索、Zoom、ファイル変更監視を実装しています。Windows の実機確認、macOS `.app` 化、ファイル関連付けの確認 UI は今後の作業です。

## 対応環境

- 対象 OS: Windows、macOS
- 想定 CPU: x64、ARM64
- 開発 SDK: .NET 10 LTS（`global.json` で SDK 10.0.401 を指定）
- UI: Avalonia UI 12.1.3

現在の実機確認は macOS arm64 のみです。GitHub Actions では macOS / Windows の Build と Test を実行します。

## 現在実装されているもの

- Avalonia のダークテーマとネイティブタイトルバーを使った基本画面
- GFM の見出し、段落、強調、取り消し線、リスト、引用、fenced code block、リンク、画像記法、テーブル、Task List、自動リンク、水平線を扱う Markdig パーサー
- Markdig の型を UI に公開しない表示用 Document Model
- Avalonia ネイティブコントロールによる見出し、本文、リスト、引用、テーブル、Task List、コードブロックの描画
- 読み取り専用コードブロックの TextMate 構文強調。未対応・未知の言語は通常のコード表示
- UTF-8 / UTF-8 BOM のファイル読込基盤

パーサーと描画部品はファイル読込・タブ管理・ファイル変更監視と結線されています。ファイル関連付けは Windows のユーザー単位登録基盤のみ実装済みで、ユーザーの明示的な同意なしには実行されません。

## 開発環境の準備

1. .NET 10 SDK をインストールします。リポジトリの `global.json` が SDK 10.0.401 を選択します。
2. リポジトリのルートでパッケージを復元し、ビルドします。

```sh
dotnet restore mdview.sln
dotnet build mdview.sln --configuration Release
```

アプリの基本画面を起動するには、次を実行します。

```sh
dotnet run --project src/mdview.Presentation/mdview.Presentation.csproj
```

起動時引数、`Ctrl/Cmd + O`、およびアプリ内のファイル選択から Markdown ファイルを開けます。

### Windows の開発ツール一括セットアップ

Windows では、管理者権限が必要になる場合がある PowerShell から次を実行します。Windows App Installer に含まれる `winget` を使用して、.NET SDK、Git、Visual Studio C++ Build Tools、ARM64 toolchain、Windows SDK をインストールし、NuGet restore まで実行します。

```powershell
.\scripts\setup-windows.ps1
```

NuGet restore を省略する場合:

```powershell
.\scripts\setup-windows.ps1 -SkipRestore
```

Native AOT を使わない通常のビルドだけであれば C++ toolchain は必須ではありませんが、`win-arm64` の Native AOT publish には必要です。スクリプトはリポジトリの `global.json` にある SDK バージョンも確認します。

対象 SDK、Git、Visual Studio Build Tools が既にインストール済みの場合、スクリプトはそれぞれの `winget install` をスキップします。`0x8A15002B` などの winget 終了コードが表示される場合は、まず `dotnet --list-sdks` で `global.json` の `10.0.401` が存在するか確認してください。

## テスト

すべてのテストを実行します。テスト時の .NET CLI / Microsoft Testing Platform のテレメトリを無効にする例です。

macOS / Linux:

```sh
DOTNET_CLI_TELEMETRY_OPTOUT=1 TESTINGPLATFORM_TELEMETRY_OPTOUT=1 dotnet test mdview.sln --configuration Release
```

Windows PowerShell:

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = "1"
dotnet test mdview.sln --configuration Release
```

テストフレームワークは xUnit v3、実行基盤は Microsoft Testing Platform です。

カバレッジを Cobertura XML 形式で測定するには、macOS / Linux では次を実行します。

```sh
scripts/coverage.sh
```

Windows PowerShell では次を実行します。

```powershell
.\scripts\coverage.ps1
```

各テストプロジェクトの Cobertura XML が `coverage/<テストプロジェクト名>/coverage.cobertura.xml` に出力されます。GitHub Actions では macOS / Windows の各ジョブで測定し、成果物として保存します。

同時に `coverage/report/index.html` が生成されます。ブラウザーで開くと、全体・プロジェクト・ファイル・クラス・メソッド単位のカバレッジ率と、ソースコードの行単位の実行状況を色分けして確認できます。

## Publish と配布

自己完結型の publish を作成する例:

```sh
dotnet publish src/mdview.Presentation/mdview.Presentation.csproj --configuration Release --runtime osx-arm64 --self-contained true
dotnet publish src/mdview.Presentation/mdview.Presentation.csproj --configuration Release --runtime osx-x64 --self-contained true
dotnet publish src/mdview.Presentation/mdview.Presentation.csproj --configuration Release --runtime win-x64 --self-contained true
dotnet publish src/mdview.Presentation/mdview.Presentation.csproj --configuration Release --runtime win-arm64 --self-contained true
```

成果物は各 RID の `bin/Release/net10.0/<RID>/publish/` に出力されます。macOS / Linux では、次のスクリプトで自己完結型 publish と ZIP を作成できます。

```sh
scripts/publish.sh osx-arm64
scripts/publish.sh osx-x64
scripts/publish.sh win-x64
scripts/publish.sh win-arm64
```

ZIP は `artifacts/mdview-<RID>.zip` に作成されます。macOS `.app` バンドルとインストーラーは作成しません。

Windows PowerShell では、`publish.ps1` を使用できます。

```powershell
.\scripts\publish.ps1 win-x64
.\scripts\publish.ps1 win-arm64
```

Native AOT を検証・有効化する場合は、最後に `--aot` を指定します。通常の publish では AOT は有効になりません。

```sh
scripts/publish.sh osx-arm64 artifacts/aot --aot
scripts/publish.sh win-x64 artifacts/aot --aot
```

`--aot` 指定時は `PublishAot=true` と `InvariantGlobalization=true` を MSBuild に渡します。AvaloniaEdit / TextMate を含むため、各 RID で publish 後に起動確認を行ってください。

Windows PowerShell では次のように指定します。

```powershell
.\scripts\publish.ps1 win-x64 artifacts\aot -Aot
```

### Windows ARM64 Native AOT の前提条件

Windows ARM64 の Native AOT では、.NET SDK だけでなく Visual Studio 2022 または Visual Studio Build Tools の C++ toolchain が必要です。次のコンポーネントをインストールしてください。

- Desktop development with C++ または C++ Build Tools workload
- MSVC v143 の ARM64 / ARM64EC build tools
- Windows SDK

Visual Studio Installer で ARM64 用の C++ build tools を選択した後、Developer PowerShell または Developer Command Prompt から次を実行します。

```powershell
.\scripts\publish.ps1 win-arm64 artifacts\aot -Aot
```

`Platform linker not found` が表示される場合は、`microsoft.dotnet.ilcompiler` の問題ではなく、`link.exe` と ARM64 用 MSVC linker が見つかっていない状態です。通常の PowerShell ではなく、Visual Studio の Developer PowerShell で実行してください。CI の `windows-latest` runner には通常この toolchain が用意されています。

## 主な依存関係

- [Avalonia](https://github.com/AvaloniaUI/Avalonia): Windows / macOS 対応のネイティブ UI（MIT）
- [Markdig](https://github.com/xoofx/markdig): Markdown / GFM パーサー（BSD-2-Clause）
- AvaloniaEdit / TextMateSharp: 読み取り専用コードブロックの構文強調（MIT）

TextMate の Onigwrap 依存は OS / CPU 向けネイティブ資産を NuGet 経由で提供するため、Oniguruma の別途インストールは不要です。自己完結型 Publish には .NET ランタイムも同梱され、文法データの実行時取得はありません。配布物の最終サイズは Publish 工程で測定します。

## プロジェクト構成

```text
src/
├── mdview.Domain/          # UI・ファイルシステムに依存しないモデル
├── mdview.Application/     # ユースケース、抽象化、Markdown 表示モデル
├── mdview.Infrastructure/  # ファイル読込、Markdig 実装
└── mdview.Presentation/    # Avalonia UI、ViewModel、ネイティブ描画

tests/
├── mdview.Domain.Tests/
├── mdview.Application.Tests/
└── mdview.Infrastructure.Tests/
```

依存の基本方向は `Presentation → Application → Domain` です。Infrastructure は Application の抽象化を実装します。

## 既知の制限・未実装機能

- ユーザーの明示的な操作による `.md` ファイル読込、複数タブ、重複ファイル検出
- ファイル変更監視、自動再読込、削除・再出現時の状態管理
- 外部リンク、相対 Markdown リンク、Heading Anchor、相対画像、検索、Zoom
- Windows のユーザー単位ファイル関連付け基盤（確認 UI は未実装）
- Windows での実機動作確認、長文の負荷測定、macOS `.app` バンドル

mdview は Markdown の内容を外部送信せず、Telemetry、Analytics、アカウント、Cloud Sync を実装しません。外部画像の自動ダウンロードも行いません。HTML は実行せず、テキストとして扱います。

開発計画の詳細は [task.md](task.md)、実装方針は [AGENTS.md](AGENTS.md) を参照してください。
