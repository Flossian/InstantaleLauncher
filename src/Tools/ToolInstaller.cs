using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace InstantaleLauncher
{
    /// <summary>
    /// ダウンロード済みアセットを tools\&lt;フォルダ&gt;\ へ展開・配置する。
    /// 更新時はユーザーの設定・置換ルールを保持し、差し替えは移動直前まで既存フォルダに触らない。
    /// </summary>
    public static class ToolInstaller
    {
        private const string MetaFile = ".launcher_meta.ini";

        /// <summary>
        /// downloadedFile を dest(=toolsDir\entry.FolderName)へ導入する。
        /// isUpdate の場合は既存 dest の PreservePatterns 一致ファイルを新版へ引き継ぐ。
        /// </summary>
        public static void Install(ToolCatalogEntry e, string downloadedFile, string tag, string toolsDir, bool isUpdate)
        {
            if (!Directory.Exists(toolsDir))
                Directory.CreateDirectory(toolsDir);

            var dest = Path.Combine(toolsDir, e.FolderName);
            // 展開先は dest と同一ボリューム(toolsDir 配下)に置き、最後の差し替えを高速な rename で行う
            var staging = Path.Combine(toolsDir, ".stage_" + Guid.NewGuid().ToString("N"));

            try
            {
                if (string.Equals(e.AssetExt, ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    // ExtractToDirectory は展開先が既存だと例外。GUID 名なので未作成のまま渡す
                    ZipFile.ExtractToDirectory(downloadedFile, staging);
                    FlattenSingleTopFolder(staging);
                }
                else
                {
                    // 生 html は index.html として保存(ToolScanner は index.html のみ html エントリとして拾う)
                    Directory.CreateDirectory(staging);
                    File.Copy(downloadedFile, Path.Combine(staging, "index.html"), true);
                }

                if (isUpdate && Directory.Exists(dest))
                {
                    PreserveUserFiles(dest, staging, e.PreservePatterns, false);
                    PreserveUserFiles(dest, staging, e.PreserveIfMissingPatterns, true);
                }

                WriteMeta(staging, tag, e.RepoSlug);
                ReplaceDir(dest, staging);
            }
            finally
            {
                TryDeleteDir(staging);
            }
        }

        /// <summary>導入済みツールフォルダの .launcher_meta.ini から現在のタグを読む。無ければ null。</summary>
        public static string ReadInstalledTag(string toolFolder)
        {
            try
            {
                var path = Path.Combine(toolFolder, MetaFile);
                if (!File.Exists(path)) return null;
                var ini = IniFile.Load(path);
                var tag = ini.Get("", "tag", null);
                return string.IsNullOrEmpty(tag) ? null : tag;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// zip 展開結果がファイル無し＋単一サブフォルダのみなら、その中身を staging 直下へ引き上げる
        /// (GitHub の zip にありがちな単一トップフォルダの平坦化)。
        /// </summary>
        private static void FlattenSingleTopFolder(string staging)
        {
            var files = Directory.GetFiles(staging);
            var dirs = Directory.GetDirectories(staging);
            if (files.Length != 0 || dirs.Length != 1) return;

            // トップフォルダ内に同名の子フォルダがあると staging 直下への Move が衝突するため、
            // 先にトップフォルダ自体を一時名へ改名してから中身を引き上げる
            var inner = Path.Combine(staging, ".flatten_" + Guid.NewGuid().ToString("N"));
            Directory.Move(dirs[0], inner);
            foreach (var f in Directory.GetFiles(inner))
                File.Move(f, Path.Combine(staging, Path.GetFileName(f)));
            foreach (var d in Directory.GetDirectories(inner))
                Directory.Move(d, Path.Combine(staging, Path.GetFileName(d)));
            Directory.Delete(inner, true);
        }

        /// <summary>
        /// dest でパターンに一致するファイル/フォルダを staging の同名パスへコピーする。
        /// パターンは名前グロブで、\ (または /) 区切りでサブフォルダ内も指定できる(中間セグメントもグロブ可)。
        /// onlyIfMissing=false: 上書きコピー(ユーザー編集を優先。新版が追加した既定ファイルは staging 側に残るため和集合になる)。
        /// onlyIfMissing=true: staging に同名が無い場合のみコピー(同梱物は新版を採用し、ユーザー追加分だけを引き継ぐ)。
        /// </summary>
        private static void PreserveUserFiles(string dest, string staging, string[] patterns, bool onlyIfMissing)
        {
            if (patterns == null) return;
            foreach (var p in patterns)
            {
                var segments = p.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 0)
                    PreserveWalk(dest, staging, segments, 0, onlyIfMissing);
            }
        }

        /// <summary>パターンのセグメントを1階層ずつ照合しながら降り、最終セグメントに一致した実体をコピーする。</summary>
        private static void PreserveWalk(string dest, string staging, string[] segments, int index, bool onlyIfMissing)
        {
            if (!Directory.Exists(dest)) return;
            var rx = GlobToRegex(segments[index]);
            bool last = index == segments.Length - 1;

            foreach (var entry in Directory.GetFileSystemEntries(dest))
            {
                var name = Path.GetFileName(entry);
                if (string.Equals(name, MetaFile, StringComparison.OrdinalIgnoreCase)) continue;
                if (!rx.IsMatch(name)) continue;

                var target = Path.Combine(staging, name);
                if (!last)
                {
                    if (Directory.Exists(entry))
                        PreserveWalk(entry, target, segments, index + 1, onlyIfMissing);
                    continue;
                }

                if (onlyIfMissing && (File.Exists(target) || Directory.Exists(target))) continue;
                if (Directory.Exists(entry))
                {
                    CopyDirOver(entry, target);
                }
                else
                {
                    Directory.CreateDirectory(staging);   // サブフォルダ指定時は staging 側が未作成のことがある
                    File.Copy(entry, target, true);
                }
            }
        }

        /// <summary>src 配下の全ファイルを dst へ再帰的に上書きコピーする(dst 側の既存ファイルは残す)。</summary>
        private static void CopyDirOver(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(src))
                CopyDirOver(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        /// <summary>ファイル名グロブ(* ?)を正規表現に変換する(ReleaseFetcher のアセット名照合と共用)。</summary>
        internal static Regex GlobToRegex(string glob)
        {
            var pattern = "^" + Regex.Escape(glob).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return new Regex(pattern, RegexOptions.IgnoreCase);
        }

        /// <summary>tag / repo を .launcher_meta.ini に記録する(更新判定・現在バージョン表示に使う)。</summary>
        private static void WriteMeta(string dir, string tag, string repoSlug)
        {
            var path = Path.Combine(dir, MetaFile);
            var text = "tag=" + tag + Environment.NewLine + "repo=" + repoSlug + Environment.NewLine;
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        /// <summary>
        /// dest を staging で置き換える。既存 dest は退避してから staging を rename し、
        /// 失敗時は退避を戻す(移動直前まで既存フォルダに触らないため、途中失敗で導入済み環境が壊れない)。
        /// </summary>
        private static void ReplaceDir(string dest, string staging)
        {
            string backup = null;
            if (Directory.Exists(dest))
            {
                backup = dest + ".old_" + Guid.NewGuid().ToString("N");
                Directory.Move(dest, backup);
            }

            try
            {
                Directory.Move(staging, dest);
            }
            catch
            {
                if (backup != null && !Directory.Exists(dest))
                    Directory.Move(backup, dest);   // ロールバック
                throw;
            }

            if (backup != null)
                TryDeleteDir(backup);
        }

        /// <summary>ディレクトリを黙って削除する(存在しなくても・失敗しても無視)。</summary>
        private static void TryDeleteDir(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch { /* 一時領域の掃除失敗は致命的でない */ }
        }
    }
}
