using System;
using System.IO;

namespace InstantaleLauncher
{
    /// <summary>
    /// カタログ項目の最新リリースを取得し、tools\&lt;Folder&gt; へ導入する。
    /// .zip 資材は展開し、それ以外(単体 .html 等)はそのままフォルダへ配置する。
    /// </summary>
    public static class ReleaseInstaller
    {
        /// <summary>entry の最新リリースをダウンロードして toolsDir 配下へ導入する(ブロッキング)。</summary>
        public static void Install(CatalogEntry entry, string toolsDir)
        {
            ReleaseInfo info = GitHubReleases.GetLatest(entry.Owner, entry.Repo);
            ReleaseAsset asset = PickAsset(info, entry);
            if (asset == null)
                throw new InvalidOperationException("最新リリースに取得可能な資材がありません: " + entry.Owner + "/" + entry.Repo);

            string targetDir = Path.Combine(toolsDir, entry.Folder);
            Directory.CreateDirectory(targetDir);

            string tempFile = Path.Combine(
                Path.GetTempPath(),
                "instantale_dl_" + Guid.NewGuid().ToString("N") + "_" + SanitizeFileName(asset.Name));
            try
            {
                GitHubReleases.Download(asset.DownloadUrl, tempFile);

                if (asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    ZipExtractor.ExtractToDirectory(tempFile, targetDir);
                else
                    File.Copy(tempFile, Path.Combine(targetDir, SanitizeFileName(asset.Name)), true);
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); }
                catch { /* 一時ファイルの削除失敗は無視 */ }
            }
        }

        /// <summary>資材の選択: 明示指定(AssetName)→ .zip → 先頭資材 の優先順。</summary>
        private static ReleaseAsset PickAsset(ReleaseInfo info, CatalogEntry entry)
        {
            if (info == null || info.Assets.Count == 0) return null;

            if (!string.IsNullOrEmpty(entry.AssetName))
            {
                foreach (var a in info.Assets)
                    if (string.Equals(a.Name, entry.AssetName, StringComparison.OrdinalIgnoreCase)) return a;
            }
            foreach (var a in info.Assets)
                if (a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return a;

            return info.Assets[0];
        }

        /// <summary>ファイル名に使えない文字を '_' へ置換する。</summary>
        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
