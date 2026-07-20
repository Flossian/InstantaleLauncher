using System;
using System.Collections.Generic;

namespace InstantaleLauncher
{
    /// <summary>
    /// ランチャーが取得先を知っている「既知ツール」1件分の定義。
    /// 未導入時は GitHub の最新リリースから取得して tools\Folder へ展開する。
    /// </summary>
    public sealed class CatalogEntry
    {
        /// <summary>tools\ 配下に作るフォルダ名(= 導入判定のキー。ToolScanner の既知名と一致させる)。</summary>
        public string Folder;
        /// <summary>タイルに表示する名前(未指定なら Folder)。</summary>
        public string DisplayName;
        /// <summary>GitHub の owner。</summary>
        public string Owner;
        /// <summary>GitHub のリポジトリ名。</summary>
        public string Repo;
        /// <summary>未導入タイルのバッジ/アイコン表示に使う想定種別(実際の種別は導入後の走査で確定する)。</summary>
        public ToolKind Kind;

        public string Title
        {
            get { return string.IsNullOrEmpty(DisplayName) ? Folder : DisplayName; }
        }
    }

    /// <summary>
    /// 既知ツールの一覧。未導入のものはランチャーが最新リリースを取得して導入できる。
    /// ランチャー自身と、機能内蔵済みの立ち絵高画質化(HDPortraitScript)は対象外。
    /// </summary>
    public static class ToolCatalog
    {
        /// <summary>取得対象の既知ツール(表示順)。</summary>
        public static readonly IList<CatalogEntry> Entries = new List<CatalogEntry>
        {
            new CatalogEntry
            {
                Folder = "InstantaleSaveEditor",
                DisplayName = "Instantale Save Editor",
                Owner = "Flossian",
                Repo = "InstantaleSaveEditor",
                Kind = ToolKind.Exe,
            },
            new CatalogEntry
            {
                Folder = "InstantaleStableDiffusionMod",
                DisplayName = "Stable Diffusion Mod",
                Owner = "Flossian",
                Repo = "InstantaleStableDiffusionMod",
                Kind = ToolKind.Bat,
            },
            new CatalogEntry
            {
                Folder = "InstantaleLLMProxy",
                DisplayName = "LLM Proxy",
                Owner = "Flossian",
                Repo = "InstantaleLLMProxy",
                Kind = ToolKind.Svc,
            },
            new CatalogEntry
            {
                Folder = "InstantaleOutputViewer",
                DisplayName = "Output Viewer",
                Owner = "Flossian",
                Repo = "instantale-output-viewer",
                Kind = ToolKind.Html,
            },
            new CatalogEntry
            {
                Folder = "InstantaleWorldViewer",
                DisplayName = "World Viewer",
                Owner = "Flossian",
                Repo = "InstantaleWorldVeiwer",
                Kind = ToolKind.Html,
            },
        };
    }
}
