using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace InstantaleLauncher
{
    /// <summary>取得した最新リリースの選択アセット情報(タグ・アセット名・ダウンロードURL)。</summary>
    public sealed class ReleaseAsset
    {
        public string Tag;
        public string AssetName;
        public string DownloadUrl;
    }

    /// <summary>
    /// GitHub の releases/latest を叩いて最新アセットを解決し、ファイルをダウンロードする。
    /// 追加参照を避けるため JSON は軽量な手動抽出(正規表現)で処理する。
    /// </summary>
    public static class ReleaseFetcher
    {
        // GitHub は User-Agent 無しのリクエストを 403 で弾く
        private const string UserAgent = "InstantaleLauncher";

        /// <summary>指定カタログエントリの最新リリースから、拡張子に合致するアセットを1件選んで返す。</summary>
        public static ReleaseAsset FetchLatest(ToolCatalogEntry e)
        {
            EnsureTls();
            var url = "https://api.github.com/repos/" + e.Owner + "/" + e.Repo + "/releases/latest";

            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = UserAgent;
            req.Accept = "application/vnd.github+json";
            req.Timeout = 20000;

            string json;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var stream = resp.GetResponseStream())
            using (var reader = new StreamReader(stream))
                json = reader.ReadToEnd();

            var asset = SelectAsset(json, e.AssetExt);
            if (asset == null)
                throw new InvalidOperationException("No matching asset for " + e.RepoSlug);
            asset.Tag = ExtractTag(json);
            return asset;
        }

        /// <summary>
        /// url からファイルを destFile へ受信する。Content-Length があれば onPercent へ進捗(0-100)を通知する。
        /// </summary>
        public static void Download(string url, string destFile, Action<int> onPercent)
        {
            EnsureTls();
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = UserAgent;
            req.Accept = "application/octet-stream";
            req.AllowAutoRedirect = true;
            req.Timeout = 30000;
            req.ReadWriteTimeout = 120000;

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var input = resp.GetResponseStream())
            using (var output = new FileStream(destFile, FileMode.Create, FileAccess.Write))
            {
                long total = resp.ContentLength;
                long received = 0;
                int lastPct = -1;
                var buffer = new byte[81920];
                int n;
                while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, n);
                    received += n;
                    if (total > 0 && onPercent != null)
                    {
                        int pct = (int)(received * 100 / total);
                        if (pct != lastPct)
                        {
                            lastPct = pct;
                            onPercent(pct);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 通信前に TLS1.2 を有効化する(実装容易さ・保守性を優先し 1.2 固定)。
        /// .NET Framework 4.5/4.6 は既定で TLS1.2 が無効なことがあり、GitHub は TLS1.2+ 必須。
        /// TLS1.3(Tls13)は未対応環境で例外になるため扱わない。
        /// </summary>
        private static void EnsureTls()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
            catch { /* 既に設定済み等 */ }
        }

        /// <summary>JSON から tag_name を抽出する。見つからなければ空文字。</summary>
        private static string ExtractTag(string json)
        {
            var m = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : "";
        }

        /// <summary>
        /// assets 配列内の name/browser_download_url ペアを走査し、拡張子に一致する先頭アセットを選ぶ。
        /// 一致が無ければ先頭アセット。アセットが1件も無ければ null。
        /// release 直下の "name"(リリースタイトル)を拾わないよう assets 配列以降のみを対象にする。
        /// </summary>
        private static ReleaseAsset SelectAsset(string json, string ext)
        {
            // "assets_url" ではなく "assets":[ を厳密に探す(閉じ引用符の直後がコロン+角括弧)
            var arr = Regex.Match(json, "\"assets\"\\s*:\\s*\\[");
            string scope = arr.Success ? json.Substring(arr.Index + arr.Length) : json;

            var rx = new Regex(
                "\"name\"\\s*:\\s*\"([^\"]*)\"[\\s\\S]*?\"browser_download_url\"\\s*:\\s*\"([^\"]*)\"");

            ReleaseAsset first = null;
            foreach (Match m in rx.Matches(scope))
            {
                var name = m.Groups[1].Value;
                var dl = m.Groups[2].Value;
                var asset = new ReleaseAsset { AssetName = name, DownloadUrl = dl };
                if (first == null) first = asset;
                if (!string.IsNullOrEmpty(ext) &&
                    name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    return asset;
            }
            return first;
        }
    }
}
