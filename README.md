# InstantaleLauncher

[Instantale](https://darmabeko.itch.io/instantale) 用の非公式ランチャー / MOD管理ツールです。
`tools\` フォルダ配下に配置した各種ツールをタイル形式で一覧・起動できるほか、GitHub Releases からの導入・更新、立ち絵高画質化の自動監視、常駐サービスの起動管理をまとめて行えます。

## 主な機能

- **ゲーム起動**: 指定した Instantale インストールフォルダの `instantale.exe` をワンクリックで起動
- **ツールタイル一覧**: `tools\` 直下のサブフォルダを自動走査し、EXE / HTML / BAT / 常駐サービス(SVC)をタイルとして表示・起動
- **GitHub Releases 連携**: 自作ツールのうち、未導入の対応ツールを一覧から直接ダウンロード・展開して導入。導入済みツールは「更新を確認」から最新版へワンクリック更新(ユーザー設定ファイルは保持)
- **立ち絵高画質化**: 対象フォルダを監視し、ワールド内の立ち絵画像を高画質版へ自動置換(元画像は復元可能な形でバックアップ)
- **常駐サービス管理**: LLM Proxy など常駐系ツールの起動/停止状態を検知・表示(ランチャー終了時はプロセスをデタッチし、稼働を継続)
- **日本語 / 英語 UI**: `lang\*.ini` による多言語対応

## 動作環境

- Windows
- .NET Framework 4.8

## セットアップ

1. `InstantaleLauncher.exe` を実行する
2. 使用するツールをダウンロードする
3. 

初回起動時にゲームフォルダ・設定などを問い合わせ、`settings.ini`(exe と同階層)に保存します。

## ビルド方法

.NET SDK(net48 ターゲットのビルドに対応したもの)を使用します。

```
dotnet build InstantaleLauncher.csproj -c Release
```

## ライセンス

[MIT License](LICENSE)
