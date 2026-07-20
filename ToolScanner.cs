using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InstantaleLauncher
{
    /// <summary>タイルの見た目とボタン動作を左右するツール種別。</summary>
    public enum ToolKind
    {
        Exe,
        Html,
        Bat,
        Svc
    }

    /// <summary>tools\ 配下で検出された1ツール分の情報。</summary>
    public sealed class ToolInfo
    {
        public string Name;
        public string Folder;
        public string EntryPath;
        public ToolKind Kind;
    }

    /// <summary>exe同階層の tools\ を走査し、1サブフォルダ=1ツールとして検出する。</summary>
    public static class ToolScanner
    {
        // 既知エントリ表(フォルダ名 → エントリファイル候補、先頭を優先)。名前照合はすべて大文字小文字無視。
        private static readonly Dictionary<string, string[]> KnownEntries =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                // v2 で mod_gui.bat に統合。旧版フォルダ向けに sd_upscale_gui.bat も許容
                { "InstantaleStableDiffusionMod", new[] { "mod_gui.bat", "sd_upscale_gui.bat" } },
            };

        // 既知サービス名リスト。フォルダ名が一致すれば SVC として管理する。
        private static readonly HashSet<string> KnownServices =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "InstantaleLLMProxy",
            };

        /// <summary>
        /// toolsDir 直下の各サブフォルダを1ツールとして検出する。
        /// エントリファイルの優先順位: 1) 既知エントリ表 2) exe 3) index.html。いずれも無ければそのフォルダは除外する。
        /// </summary>
        public static List<ToolInfo> Scan(string toolsDir)
        {
            var result = new List<ToolInfo>();
            if (!Directory.Exists(toolsDir))
                return result;

            string selfExeName = Path.GetFileName(typeof(ToolScanner).Assembly.Location);

            foreach (var dir in Directory.GetDirectories(toolsDir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(dir);
                string entry;

                string[] knownEntry;
                if (KnownEntries.TryGetValue(name, out knownEntry))
                {
                    // 1. 既知エントリ表が最優先。候補のいずれも無ければ非表示
                    entry = null;
                    foreach (var candidate in knownEntry)
                    {
                        entry = FindFileIgnoreCase(dir, candidate);
                        if (entry != null) break;
                    }
                    if (entry == null) continue;
                }
                else
                {
                    // 2. exe → 3. html(index.html 優先、無ければ任意の *.html) → 4. 非表示
                    entry = PickExe(dir, name, selfExeName) ?? PickHtml(dir);
                    if (entry == null) continue;
                }

                result.Add(new ToolInfo
                {
                    Name = name,
                    Folder = dir,
                    EntryPath = entry,
                    Kind = Classify(name, entry),
                });
            }
            return result;
        }

        /// <summary>エントリファイルの拡張子とフォルダ名から種別を判定する(既知サービス名なら exe でも Svc 扱い)。</summary>
        private static ToolKind Classify(string folderName, string entryPath)
        {
            var ext = Path.GetExtension(entryPath);
            bool isExe = ext.Equals(".exe", StringComparison.OrdinalIgnoreCase);
            if (isExe && KnownServices.Contains(folderName))
                return ToolKind.Svc;
            if (isExe)
                return ToolKind.Exe;
            if (ext.Equals(".bat", StringComparison.OrdinalIgnoreCase) || ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
                return ToolKind.Bat;
            return ToolKind.Html;
        }

        /// <summary>フォルダ内の exe から起動対象を選ぶ。自分自身(ランチャー)の exe は除外し、フォルダ名と同名を優先する。</summary>
        private static string PickExe(string dir, string folderName, string selfExeName)
        {
            var exes = Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly)
                .Where(p => !Path.GetFileName(p).Equals(selfExeName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (exes.Count == 0) return null;

            // フォルダ名と同名の exe を優先(大文字小文字無視)
            foreach (var exe in exes)
            {
                if (Path.GetFileNameWithoutExtension(exe).Equals(folderName, StringComparison.OrdinalIgnoreCase))
                    return exe;
            }
            return exes[0];
        }

        /// <summary>フォルダ内の html を選ぶ。index.html を優先し、無ければ最初の *.html(名前順)。</summary>
        private static string PickHtml(string dir)
        {
            var index = FindFileIgnoreCase(dir, "index.html");
            if (index != null) return index;

            return Directory.GetFiles(dir, "*.html", SearchOption.TopDirectoryOnly)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        /// <summary>大文字小文字を無視してファイル名一致を探す(Directory.GetFiles は既定で大小無視だが明示的に照合する)。</summary>
        private static string FindFileIgnoreCase(string dir, string fileName)
        {
            foreach (var path in Directory.GetFiles(dir))
            {
                if (Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    return path;
            }
            return null;
        }
    }
}
