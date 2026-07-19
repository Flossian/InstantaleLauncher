using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace InstantaleLauncher
{
    /// <summary>settings.ini / lang\*.ini 共用の簡易INIパーサ。</summary>
    public sealed class IniFile
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>ファイルを UTF-8(BOM有無どちらも可)として読み込みパースする。</summary>
        public static IniFile Load(string path)
        {
            using (var reader = new StreamReader(path, Encoding.UTF8, true))
            {
                return Parse(reader.ReadToEnd());
            }
        }

        /// <summary>
        /// INI テキストをパースする。改行は LF/CRLF どちらも許容し、
        /// ";" "#" で始まる行はコメント、"[section]" でセクション切り替え、"key=value" を値として登録する。
        /// </summary>
        public static IniFile Parse(string content)
        {
            var ini = new IniFile();
            string section = "";
            foreach (var raw in content.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#"))
                    continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq < 0)
                    continue;
                ini.Set(section, line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim());
            }
            return ini;
        }

        /// <summary>値を文字列として取得する。セクション/キーが無ければ fallback を返す。</summary>
        public string Get(string section, string key, string fallback)
        {
            Dictionary<string, string> sec;
            string value;
            if (_sections.TryGetValue(section, out sec) && sec.TryGetValue(key, out value))
                return value;
            return fallback;
        }

        /// <summary>値を bool として取得する("true"/"false"/"1"/"0" のみ解釈、それ以外は fallback)。</summary>
        public bool GetBool(string section, string key, bool fallback)
        {
            var s = Get(section, key, null);
            if (s == null) return fallback;
            if (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1") return true;
            if (s.Equals("false", StringComparison.OrdinalIgnoreCase) || s == "0") return false;
            return fallback;
        }

        /// <summary>値を int として取得する。パース失敗時は fallback を返す。</summary>
        public int GetInt(string section, string key, int fallback)
        {
            var s = Get(section, key, null);
            int value;
            if (s != null && int.TryParse(s, out value))
                return value;
            return fallback;
        }

        /// <summary>セクションが無ければ作成したうえで key=value を設定する。</summary>
        public void Set(string section, string key, string value)
        {
            Dictionary<string, string> sec;
            if (!_sections.TryGetValue(section, out sec))
            {
                sec = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _sections[section] = sec;
            }
            sec[key] = value;
        }

        /// <summary>全セクションの全キー値ペアを列挙する(言語ファイルのマージ処理などで使用)。</summary>
        public IEnumerable<KeyValuePair<string, string>> AllPairs()
        {
            foreach (var sec in _sections)
                foreach (var kv in sec.Value)
                    yield return kv;
        }
    }
}
