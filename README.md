# mdview

<img src="src/mdview.Presentation/assets/mdview-icon.png" alt="mdview アプリアイコン" width="96" height="96">

## アプリの概要

mdview は、Markdown ファイルを素早く開いて読むための軽量なデスクトップ Viewer です。Windows / macOS（x64・ARM64）を対象とし、常にダークテーマで表示します。

タブ、相対リンク・画像、自動再読込、構文強調、検索、Zoom、Mermaid 表示を備えています。現在開発中で、一部の機能は未完成です。

## 開発環境

- C# / .NET 10 LTS（SDK 10.0.401、`global.json` で指定）
- Avalonia UI 12.1.3 / MVVM
- Markdig 1.4.0
- xUnit v3 / Microsoft Testing Platform
- Git / GitHub Actions

Windows の開発ツールは [setup-windows.ps1](scripts/setup-windows.ps1) でセットアップできます。Windows の Mermaid 表示には WebView2 Evergreen Runtime が必要です。

## ビルド方法

リポジトリのルートで実行します。

```sh
dotnet restore mdview.sln
dotnet build mdview.sln --configuration Release
```

ファイルを指定して起動する例:

```sh
dotnet run --project src/mdview.Presentation -- README.md
```

ZIP 配布物の作成には [publish.sh](scripts/publish.sh) または [publish.ps1](scripts/publish.ps1) を使用します。

通常の自己完結型 publish と ZIP 作成:

```sh
# macOS
scripts/publish.sh osx-arm64
scripts/publish.sh osx-x64
```

```powershell
# Windows
.\scripts\publish.ps1 win-arm64
.\scripts\publish.ps1 win-x64
```

出力先は `artifacts/<RID>/`、ZIP は `artifacts/mdview-<RID>.zip` です。使用する CPU に合わせてコマンドを選んでください。

Native AOT でコンパイルして ZIP 配布物を作成する例:

```sh
# macOS ARM64
scripts/publish.sh osx-arm64 artifacts/aot --aot
```

```powershell
# Windows ARM64
.\scripts\publish.ps1 win-arm64 artifacts\aot -Aot

# Windows x64
.\scripts\publish.ps1 win-x64 artifacts\aot -Aot
```

x64 向けは RID を `osx-x64` / `win-x64` に変更します。対象 OS の開発環境で実行し、macOS は Xcode Command Line Tools、Windows は対象 CPU 向けの Visual Studio C++ Build Tools と Windows SDK を用意してください。

## テスト方法

```sh
dotnet test mdview.sln --configuration Release
```

カバレッジ取得は、リポジトリのルートで実行します。スクリプト内でパッケージとレポート生成ツールを restore するため、事前の restore は不要です。

```sh
# macOS
scripts/coverage.sh
```

```powershell
# Windows
.\scripts\coverage.ps1
```

各テストプロジェクトの結果は `coverage/<プロジェクト名>/coverage.cobertura.xml`、HTML レポートは `coverage/report/index.html` に出力されます。HTML レポートをブラウザーで開いて確認できます。再実行時は既存の `coverage/` を作り直します。

GitHub Actions でも macOS / Windows の Build と Test、カバレッジ取得を実行します。

開発ツールのテレメトリを無効にする場合は、`DOTNET_CLI_TELEMETRY_OPTOUT`、`TESTINGPLATFORM_TELEMETRY_OPTOUT`、`AVALONIA_TELEMETRY_OPTOUT` を環境変数として `1` に設定してください。

## フォルダ構成

```text
src/
├── mdview.Domain/          # 文書モデル
├── mdview.Application/     # ユースケース・抽象化・表示モデル
├── mdview.Infrastructure/  # ファイル I/O・解析・OS 連携
└── mdview.Presentation/    # Avalonia UI・ViewModel・描画

tests/                     # 各層の Unit Test
docs/                      # 設計書・サンプル
scripts/                   # セットアップ・配布・カバレッジ
.github/                   # CI
```

Onion Architecture を採用し、基本の依存方向は `Presentation → Application → Domain` とします。Infrastructure は Application の抽象化を実装します。

## 関連ドキュメント情報

- [基本設計書](docs/basic-design.md): 機能・画面仕様
- [アーキテクチャ設計書](docs/architecture-design.md): 構成・責務・依存関係
- [詳細設計書](docs/detailed-design.md): 処理・状態遷移・実装上の課題
- [Mermaid 表示設計](docs/mermaid-design.md): 図生成方式・検証記録
- [開発計画](task.md): 開発フェーズ・作業記録
- [実装方針](AGENTS.md): 要件・制約

## 制約

- 読み取り専用です。編集・保存、セッション復元、LaTeX は対象外です。
- 対応文字コードの仕様は UTF-8 / UTF-8 BOM、画像はローカル相対パスです。外部画像の取得や Markdown 内容の外部送信は行いません。
- 検索の一致位置へのスクロール・一部のハイライト、Anchor 移動、再読込エラー時の表示同期などに未達事項があります。詳細は詳細設計書を参照してください。
- ファイル関連付けの確認 UI、macOS の関連付け・`.app` 化は未実装です。
- 全対象 OS / CPU の実機確認と性能測定は未完了です。
