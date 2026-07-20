using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace InstantaleLauncher
{
    /// <summary>リリース資材1件(ダウンロード URL とファイル名・サイズ)。</summary>
    public sealed class ReleaseAsset
    {
        public string Name;
        public string DownloadUrl;
        public long Size;
    }

    /// <summary>最新リリースの情報(タグ名と資材一覧)。</summary>
    public sealed class ReleaseInfo
    {
        public string Tag;
        public readonly List<ReleaseAsset> Assets = new List<ReleaseAsset>();
    }

    /// <summary>
    /// GitHub Releases API から最新リリースを取得し、資材をダウンロードする。
    /// 依存は標準の System.Net / System.dll のみ(追加参照なし)。
    /// </summary>
    public static class GitHubReleases
    {
        private const string UserAgent = "InstantaleLauncher";

        /// <summary>指定リポジトリの最新(draft/prerelease を除く)リリースを取得する。</summary>
        public static ReleaseInfo GetLatest(string owner, string repo)
        {
            EnsureTls();
            string url = "https://api.github.com/repos/" + owner + "/" + repo + "/releases/latest";
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = UserAgent;           // GitHub API は UA 必須
            req.Accept = "application/vnd.github+json";
            req.Timeout = 30000;

            string body;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var stream = resp.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                body = reader.ReadToEnd();

            var root = Json.Parse(body) as Dictionary<string, object>;
            if (root == null) throw new FormatException("Unexpected release JSON");

            var info = new ReleaseInfo();
            object tag;
            if (root.TryGetValue("tag_name", out tag) && tag is string) info.Tag = (string)tag;

            object assetsObj;
            if (root.TryGetValue("assets", out assetsObj) && assetsObj is List<object>)
            {
                foreach (var a in (List<object>)assetsObj)
                {
                    var ad = a as Dictionary<string, object>;
                    if (ad == null) continue;
                    var asset = new ReleaseAsset();
                    object v;
                    if (ad.TryGetValue("name", out v) && v is string) asset.Name = (string)v;
                    if (ad.TryGetValue("browser_download_url", out v) && v is string) asset.DownloadUrl = (string)v;
                    if (ad.TryGetValue("size", out v) && v is double) asset.Size = (long)(double)v;
                    if (!string.IsNullOrEmpty(asset.Name) && !string.IsNullOrEmpty(asset.DownloadUrl))
                        info.Assets.Add(asset);
                }
            }
            return info;
        }

        /// <summary>資材をローカルファイルへストリーミング保存する(リダイレクト追従)。</summary>
        public static void Download(string downloadUrl, string destFile)
        {
            EnsureTls();
            var req = (HttpWebRequest)WebRequest.Create(downloadUrl);
            req.UserAgent = UserAgent;
            req.AllowAutoRedirect = true;
            req.Timeout = 60000;
            req.ReadWriteTimeout = 300000;

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var stream = resp.GetResponseStream())
            using (var file = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    file.Write(buffer, 0, read);
            }
        }

        /// <summary>古い既定のままだと TLS 失敗するため、TLS 1.2 を有効化する(失敗は無視)。</summary>
        private static void EnsureTls()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
            catch { }
        }
    }
}
