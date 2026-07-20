using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace InstantaleLauncher
{
    /// <summary>取得対象に選ばれたリリースアセット1件(ファイル名・ダウンロードURL・タグ)。</summary>
    public sealed class ReleaseAsset
    {
        public string Name;
        public string Url;
        public string Tag;

        public bool IsZip
        {
            get { return Name != null && Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase); }
        }
    }

    /// <summary>
    /// GitHub の最新リリースを問い合わせて、ツールを tools\ 配下へ取得・展開する。
    /// ネットワーク処理は同期。呼び出し側でバックグラウンドスレッドから使うこと。
    /// </summary>
    public static class ReleaseInstaller
    {
        private const string UserAgent = "InstantaleLauncher";

        static ReleaseInstaller()
        {
            // .NET Framework 既定では TLS1.2 が無効な場合があり、GitHub API/ダウンロードに失敗する
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
            catch { /* 古いランタイムでは列挙値が無いこともある。既定のまま続行 */ }
        }

        /// <summary>
        /// {owner}/{repo} の最新リリースから取得すべきアセットを1件決める。
        /// 優先: 1) .zip 2) 最初のアセット。アセットが無ければ null。
        /// </summary>
        public static ReleaseAsset ResolveLatest(string owner, string repo)
        {
            string url = "https://api.github.com/repos/" + owner + "/" + repo + "/releases/latest";
            string json = HttpGetString(url);

            string tag = MatchOne(json, "\"tag_name\"\\s*:\\s*\"([^\"]*)\"");

            // アセットオブジェクト内では "name" が "browser_download_url" より前に現れ、
            // 両者の間に波括弧が挟まらない。[^{}] 制約で他オブジェクトへの誤マッチを防ぐ。
            var rx = new Regex(
                "\"name\"\\s*:\\s*\"([^\"]*)\"[^{}]*?\"browser_download_url\"\\s*:\\s*\"([^\"]*)\"",
                RegexOptions.Singleline);

            ReleaseAsset first = null;
            ReleaseAsset zip = null;
            foreach (Match m in rx.Matches(json))
            {
                var asset = new ReleaseAsset
                {
                    Name = m.Groups[1].Value,
                    Url = m.Groups[2].Value,
                    Tag = tag,
                };
                if (first == null) first = asset;
                if (zip == null && asset.IsZip) zip = asset;
            }
            return zip ?? first;
        }

        /// <summary>
        /// アセットを tools\folder へ導入する。zip は展開(単一ルートフォルダは平坦化)し、
        /// それ以外は単一ファイルとしてフォルダ内へ保存する。進捗は 0..100、不明時は -1 を通知。
        /// </summary>
        public static void Install(string toolsDir, string folder, ReleaseAsset asset, Action<int> onProgress)
        {
            if (asset == null) throw new InvalidOperationException("no asset");

            string targetDir = Path.Combine(toolsDir, folder);
            string tmp = Path.Combine(Path.GetTempPath(),
                "instantale_" + Guid.NewGuid().ToString("N") + "_" + SafeFileName(asset.Name));

            try
            {
                DownloadFile(asset.Url, tmp, onProgress);

                Directory.CreateDirectory(targetDir);
                if (asset.IsZip)
                    ExtractZipFlattened(tmp, targetDir);
                else
                    File.Copy(tmp, Path.Combine(targetDir, SafeFileName(asset.Name)), true);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); }
                catch { /* 一時ファイルの削除失敗は無視 */ }
            }
        }

        /// <summary>GitHub API 等から文字列を取得する(UserAgent 必須、リダイレクト追従)。</summary>
        private static string HttpGetString(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = UserAgent;
            req.Accept = "application/vnd.github+json";
            req.Timeout = 30000;
            using (var res = (HttpWebResponse)req.GetResponse())
            using (var stream = res.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        /// <summary>URL をローカルファイルへストリーミング保存する。Content-Length があれば進捗を報告する。</summary>
        private static void DownloadFile(string url, string dest, Action<int> onProgress)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = UserAgent;
            req.Timeout = 30000;
            req.ReadWriteTimeout = 120000;
            req.AllowAutoRedirect = true;

            using (var res = (HttpWebResponse)req.GetResponse())
            using (var input = res.GetResponseStream())
            using (var output = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                long total = res.ContentLength;
                var buffer = new byte[81920];
                long read = 0;
                int lastPercent = -1;
                int n;
                while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, n);
                    read += n;
                    if (onProgress == null) continue;

                    if (total > 0)
                    {
                        int percent = (int)(read * 100 / total);
                        if (percent != lastPercent)
                        {
                            lastPercent = percent;
                            onProgress(percent);
                        }
                    }
                    else
                    {
                        onProgress(-1);   // 総量不明
                    }
                }
            }
        }

        /// <summary>
        /// zip を targetDir へ展開する。全エントリが同一の先頭フォルダを共有する場合は
        /// その1階層を取り除いて平坦化する(GitHub の zip ラッパー対策)。
        /// </summary>
        private static void ExtractZipFlattened(string zipPath, string targetDir)
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                string root = CommonRootDir(archive);
                foreach (var entry in archive.Entries)
                {
                    string rel = entry.FullName.Replace('\\', '/');
                    if (root != null && rel.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        rel = rel.Substring(root.Length);
                    rel = rel.Replace('/', Path.DirectorySeparatorChar);
                    if (rel.Length == 0) continue;   // ラッパーのルートフォルダ自身は飛ばす

                    string destPath = Path.Combine(targetDir, rel);

                    // ディレクトリエントリ(末尾がセパレータ)は作成のみ
                    if (entry.Name.Length == 0)
                    {
                        Directory.CreateDirectory(destPath);
                        continue;
                    }

                    // zip slip 対策: 展開先が targetDir 配下であることを確認
                    string fullDest = Path.GetFullPath(destPath);
                    string fullTarget = Path.GetFullPath(targetDir);
                    if (!fullDest.StartsWith(fullTarget, StringComparison.OrdinalIgnoreCase))
                        continue;

                    Directory.CreateDirectory(Path.GetDirectoryName(fullDest));
                    entry.ExtractToFile(fullDest, true);
                }
            }
        }

        /// <summary>全エントリが共有する先頭フォルダ("root/" 形式)を返す。共有していなければ null。</summary>
        private static string CommonRootDir(ZipArchive archive)
        {
            string root = null;
            foreach (var entry in archive.Entries)
            {
                string name = entry.FullName.Replace('\\', '/');
                int slash = name.IndexOf('/');
                if (slash < 0) return null;   // 先頭にファイルがある = 共通ルート無し

                string top = name.Substring(0, slash + 1);
                if (root == null) root = top;
                else if (!string.Equals(root, top, StringComparison.OrdinalIgnoreCase)) return null;
            }
            return root;
        }

        /// <summary>正規表現の最初のグループを取り出す(未マッチなら空文字)。</summary>
        private static string MatchOne(string input, string pattern)
        {
            var m = Regex.Match(input, pattern);
            return m.Success ? m.Groups[1].Value : string.Empty;
        }

        /// <summary>ファイル名に使えない文字を除去する。</summary>
        private static string SafeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "download";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
