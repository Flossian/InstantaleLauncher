using System;
using System.Collections.Generic;
using System.IO;

namespace InstantaleLauncher
{
    /// <summary>
    /// 既知ツール1件のカタログ情報。tools\ 配下のフォルダ名・取得元リポジトリ・
    /// 取得アセットの拡張子・タイル表示種別・更新時に保持するユーザーファイルのパターンを持つ。
    /// </summary>
    public sealed class ToolCatalogEntry
    {
        /// <summary>tools\ 直下に作るフォルダ名(ToolScanner の照合・アイコン部分一致に合わせる)。</summary>
        public string FolderName;
        /// <summary>GitHub リポジトリのオーナー。</summary>
        public string Owner;
        /// <summary>GitHub リポジトリ名。</summary>
        public string Repo;
        /// <summary>取得アセットの拡張子(".zip" or ".html")。アセット選択とインストール方式を決める。</summary>
        public string AssetExt;
        /// <summary>タイルのバッジ・既定アイコンに使う表示種別。</summary>
        public ToolKind DisplayKind;
        /// <summary>
        /// 更新時にユーザー編集を保持するパターン(フォルダ直下の名前グロブ)。
        /// ディレクトリ名に一致した場合は再帰的にユーザーファイルを優先コピーする。
        /// </summary>
        public string[] PreservePatterns;

        /// <summary>"owner/repo" 形式。API URL 組み立て・メタ記録に使う。</summary>
        public string RepoSlug { get { return Owner + "/" + Repo; } }
    }

    /// <summary>既知ツールのレジストリ。未導入検出(Missing)と導入済みからの逆引き(Find)を提供する。</summary>
    public static class ToolCatalog
    {
        /// <summary>導入/更新の対象となる既知ツール一覧。立ち絵高画質化は内蔵機能のため含めない。</summary>
        public static readonly IReadOnlyList<ToolCatalogEntry> Entries = new List<ToolCatalogEntry>
        {
            new ToolCatalogEntry
            {
                FolderName = "InstantaleSaveEditor",
                Owner = "Flossian", Repo = "InstantaleSaveEditor",
                AssetExt = ".zip", DisplayKind = ToolKind.Exe,
                // setting/ と namelist/ 配下のユーザー編集を保持(新版の追加既定ファイルは取り込む)
                PreservePatterns = new[] { "setting", "namelist" },
            },
            new ToolCatalogEntry
            {
                FolderName = "InstantaleOutputViewer",
                Owner = "Flossian", Repo = "instantale-output-viewer",
                AssetExt = ".zip", DisplayKind = ToolKind.Html,
                PreservePatterns = new string[0],
            },
            new ToolCatalogEntry
            {
                FolderName = "InstantaleWorldViewer",
                // リポジトリ名は綴り違い(Veiwer)だがフォルダ名はアイコン部分一致(world)に合わせる
                Owner = "Flossian", Repo = "InstantaleWorldVeiwer",
                AssetExt = ".html", DisplayKind = ToolKind.Html,
                PreservePatterns = new string[0],
            },
            new ToolCatalogEntry
            {
                FolderName = "InstantaleLLMProxy",
                Owner = "Flossian", Repo = "InstantaleLLMProxy",
                AssetExt = ".zip", DisplayKind = ToolKind.Svc,
                // 置換ルール・設定・GUI状態・各種トグルファイル一式を保持
                PreservePatterns = new[] { "llm_replacements.txt", "llm_proxy_*.ini", "llm_proxy_*.txt" },
            },
            new ToolCatalogEntry
            {
                FolderName = "InstantaleStableDiffusionMod",
                Owner = "Flossian", Repo = "InstantaleStableDiffusionMod",
                AssetExt = ".zip", DisplayKind = ToolKind.Bat,
                PreservePatterns = new[] { "sd_upscale*.ini" },
            },
        };

        /// <summary>走査済みツール一覧に含まれない(＝未導入の)カタログエントリを返す。</summary>
        public static List<ToolCatalogEntry> Missing(IEnumerable<ToolInfo> installed)
        {
            var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (installed != null)
            {
                foreach (var t in installed)
                    have.Add(FolderBaseName(t.Folder));
            }

            var missing = new List<ToolCatalogEntry>();
            foreach (var e in Entries)
            {
                if (!have.Contains(e.FolderName))
                    missing.Add(e);
            }
            return missing;
        }

        /// <summary>フォルダ名(basename)からカタログエントリを引く。該当なしなら null。</summary>
        public static ToolCatalogEntry Find(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return null;
            foreach (var e in Entries)
            {
                if (string.Equals(e.FolderName, folderName, StringComparison.OrdinalIgnoreCase))
                    return e;
            }
            return null;
        }

        /// <summary>フルパス/相対どちらでも末尾のフォルダ名だけを取り出す。</summary>
        public static string FolderBaseName(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return "";
            return Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
    }
}
