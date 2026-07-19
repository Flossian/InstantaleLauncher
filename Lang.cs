using System;
using System.IO;
using System.Text;

namespace InstantaleLauncher
{
    /// <summary>
    /// i18n。ja/en は埋め込みリソースをデフォルトとし、
    /// exe 同階層の lang\{code}.ini があれば上書き読み込みする。
    /// </summary>
    public static class Lang
    {
        private static readonly System.Collections.Generic.Dictionary<string, string> Map =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>言語辞書を構築する。en(埋め込み)→ code(埋め込み)→ code(外部ファイル)の順で上書きする。</summary>
        public static void Initialize(string code, string langDir)
        {
            Map.Clear();

            // フォールバック用に en を先に敷く
            Merge(LoadEmbedded("en"));
            if (!string.Equals(code, "en", StringComparison.OrdinalIgnoreCase))
                Merge(LoadEmbedded(code));

            // 外部ファイルによる上書き
            try
            {
                var file = Path.Combine(langDir, code + ".ini");
                if (File.Exists(file))
                    Merge(IniFile.Load(file));
            }
            catch
            {
                // 外部言語ファイルが壊れていても埋め込みデフォルトで続行
            }
        }

        /// <summary>キーに対応する訳文を取得する。未登録ならキー自体をそのまま返す(未翻訳の目印にもなる)。</summary>
        public static string T(string key)
        {
            string value;
            return Map.TryGetValue(key, out value) ? value : key;
        }

        /// <summary>キーの訳文を string.Format の書式として使い、引数を埋め込む。書式不正時は素の訳文を返す。</summary>
        public static string F(string key, params object[] args)
        {
            try { return string.Format(T(key), args); }
            catch (FormatException) { return T(key); }
        }

        /// <summary>アセンブリに埋め込まれた lang\{code}.ini リソースを読み込む。未同梱の言語なら null。</summary>
        private static IniFile LoadEmbedded(string code)
        {
            var asm = typeof(Lang).Assembly;
            using (var stream = asm.GetManifestResourceStream("InstantaleLauncher.lang." + code + ".ini"))
            {
                if (stream == null) return null;
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    return IniFile.Parse(reader.ReadToEnd());
            }
        }

        /// <summary>ini の全キーを Map に上書きマージする(後勝ち)。null は無視する。</summary>
        private static void Merge(IniFile ini)
        {
            if (ini == null) return;
            foreach (var kv in ini.AllPairs())
                Map[kv.Key] = kv.Value;
        }
    }
}
