# mdview

mdview は Markdown ファイルを素早く開いて読むための、軽量なデスクトップ Viewer です。Markdown の編集機能を持たず、シンプルな閲覧体験を目指しています。

> **開発中:** 現在はアプリの土台、GFM パーサー、ネイティブ描画の基礎を実装した段階です。Markdown ファイルを開く操作やタブ管理はまだ実装されていません。アプリを起動すると空のダークテーマ画面が表示されます。

## 対応環境

- 対象 OS: Windows、macOS
- 想定 CPU: x64、ARM64
- 開発 SDK: .NET 10 LTS（`global.json` で SDK 10.0.401 を指定）
- UI: Avalonia UI 12.1.3

現在の実機確認は macOS arm64 のみです。Windows を含む全対象環境の検証と配布用 ZIP の作成は今後の作業です。

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

現状はファイルを開く操作がないため、起動後の文書領域は空です。

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

## Publish と配布

自己完結型の publish を作成する例:

```sh
dotnet publish src/mdview.Presentation/mdview.Presentation.csproj --configuration Release --runtime osx-arm64 --self-contained true
dotnet publish src/mdview.Presentation/mdview.Presentation.csproj --configuration Release --runtime osx-x64 --self-contained true
dotnet publish src/mdview.Presentation/mdview.Presentation.csproj --configuration Release --runtime win-x64 --self-contained true
dotnet publish src/mdview.Presentation/mdview.Presentation.csproj --configuration Release --runtime win-arm64 --self-contained true
```

成果物は各 RID の `bin/Release/net10.0/<RID>/publish/` に出力されます。配布方式は ZIP 展開を想定し、publish ディレクトリを ZIP 化します。現時点では ZIP 作成、macOS `.app` バンドル、GitHub Actions の CI は未整備です。インストーラーは作成しません。

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
- Windows での動作確認、長文の負荷測定、配布 ZIP と CI

mdview は Markdown の内容を外部送信せず、Telemetry、Analytics、アカウント、Cloud Sync を実装しません。外部画像の自動ダウンロードも行いません。HTML は実行せず、テキストとして扱います。

開発計画の詳細は [task.md](task.md)、実装方針は [AGENTS.md](AGENTS.md) を参照してください。
