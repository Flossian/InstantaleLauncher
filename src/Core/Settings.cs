using System;
using System.IO;
using System.Text;

namespace InstantaleLauncher
{
    /// <summary>settings.ini(exe同階層)。存在しなければデフォルトで動作し、変更時に生成する。</summary>
    public static class Settings
    {
        public static string FilePath { get; private set; }
        public static string Language = "ja";
        public static bool PortraitAutoStart = false;
        public static string PortraitPath = "";
        public static string GameDir = "";

        // ウィンドウの前回位置・サイズ(未保存は int.MinValue / 0 のまま)
        public static int WindowX = int.MinValue;
        public static int WindowY = int.MinValue;
        public static int WindowWidth = 0;
        public static int WindowHeight = 0;
        public static bool WindowMaximized = false;

        /// <summary>settings.ini を読み込んでフィールドに反映する。ファイルが無ければ既定値のまま何もしない。</summary>
        public static void Load(string path)
        {
            FilePath = path;
            if (!File.Exists(path)) return;
            try
            {
                var ini = IniFile.Load(path);
                Language = ini.Get("general", "language", Language);
                PortraitAutoStart = ini.GetBool("portrait", "autostart", PortraitAutoStart);
                PortraitPath = ini.Get("portrait", "path", PortraitPath);
                GameDir = ini.Get("game", "dir", GameDir);
                WindowX = ini.GetInt("window", "x", WindowX);
                WindowY = ini.GetInt("window", "y", WindowY);
                WindowWidth = ini.GetInt("window", "width", WindowWidth);
                WindowHeight = ini.GetInt("window", "height", WindowHeight);
                WindowMaximized = ini.GetBool("window", "maximized", WindowMaximized);
            }
            catch
            {
                // 壊れた settings.ini はデフォルトで続行
            }
        }

        /// <summary>現在のフィールド値を settings.ini に書き出す(コメント付きで手動編集しやすい体裁にする)。</summary>
        public static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("[general]");
                sb.AppendLine("language=" + Language);
                sb.AppendLine();
                sb.AppendLine("[portrait]");
                sb.AppendLine("; 起動時に監視を自動開始するか");
                sb.AppendLine("autostart=" + (PortraitAutoStart ? "true" : "false"));
                sb.AppendLine(@"; 対象ルートの上書き(空なら既定の %LOCALAPPDATA%\Darmabeko\Instantale\worlds)");
                sb.AppendLine("path=" + PortraitPath);
                sb.AppendLine();
                sb.AppendLine("[game]");
                sb.AppendLine("; instantale.exe があるゲームフォルダ");
                sb.AppendLine("dir=" + GameDir);
                sb.AppendLine();
                sb.AppendLine("[window]");
                sb.AppendLine("; 前回終了時のウィンドウ位置・サイズ(自動保存)");
                if (WindowX != int.MinValue)
                {
                    sb.AppendLine("x=" + WindowX);
                    sb.AppendLine("y=" + WindowY);
                }
                if (WindowWidth > 0)
                {
                    sb.AppendLine("width=" + WindowWidth);
                    sb.AppendLine("height=" + WindowHeight);
                }
                sb.AppendLine("maximized=" + (WindowMaximized ? "true" : "false"));
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch
            {
                // 保存失敗(読み取り専用フォルダ等)は致命的ではないので無視
            }
        }
    }
}
