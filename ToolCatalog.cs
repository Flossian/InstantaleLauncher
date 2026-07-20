using System;
using System.Collections.Generic;

namespace InstantaleLauncher
{
    /// <summary>ネットから入手可能な既知ツール1件の定義。</summary>
    public sealed class CatalogEntry
    {
        /// <summary>tools\ 配下の導入先サブフォルダ名(= タイル表示名)。</summary>
        public string Folder;
        public string Owner;
        public string Repo;
        /// <summary>導入前のバッジ/アイコン表示に使う想定種別。</summary>
        public ToolKind Kind;
        /// <summary>省略可。指定時はこのファイル名の資材を優先して取得する。</summary>
        public string AssetName;
    }

    /// <summary>
    /// ランチャーが把握している公式ツールの一覧。
    /// tools\ に未導入の項目は「入手」タイルとして表示し、最新リリースを取得して導入する。
    /// (立ち絵高画質化は C# に内蔵済みのため、外部導入対象には含めない)
    /// </summary>
    public static class ToolCatalog
    {
        public static readonly List<CatalogEntry> All = new List<CatalogEntry>
        {
            new CatalogEntry { Folder = "InstantaleSaveEditor",        Owner = "Flossian", Repo = "InstantaleSaveEditor",        Kind = ToolKind.Exe },
            new CatalogEntry { Folder = "InstantaleLLMProxy",          Owner = "Flossian", Repo = "InstantaleLLMProxy",          Kind = ToolKind.Svc },
            new CatalogEntry { Folder = "InstantaleOutputViewer",      Owner = "Flossian", Repo = "instantale-output-viewer",    Kind = ToolKind.Html },
            new CatalogEntry { Folder = "InstantaleWorldViewer",       Owner = "Flossian", Repo = "InstantaleWorldVeiwer",       Kind = ToolKind.Html, AssetName = "instantale_world_viewer.html" },
            new CatalogEntry { Folder = "InstantaleStableDiffusionMod", Owner = "Flossian", Repo = "InstantaleStableDiffusionMod", Kind = ToolKind.Bat },
        };

        /// <summary>installedFolderNames に含まれない(=未導入の)カタログ項目を返す。</summary>
        public static List<CatalogEntry> Missing(IEnumerable<string> installedFolderNames)
        {
            var installed = new HashSet<string>(installedFolderNames, StringComparer.OrdinalIgnoreCase);
            var result = new List<CatalogEntry>();
            foreach (var e in All)
            {
                if (!installed.Contains(e.Folder))
                    result.Add(e);
            }
            return result;
        }
    }
}
