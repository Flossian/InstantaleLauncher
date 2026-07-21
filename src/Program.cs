using System;
using System.IO;
using System.Windows.Forms;

namespace InstantaleLauncher
{
    /// <summary>アプリのエントリポイント。設定と言語リソースを読み込んでからメイン画面を開く。</summary>
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // WinForms の見た目を最新スタイルに合わせる(旧クラシック描画を無効化)
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // exe と同じフォルダを基準に settings.ini / lang\*.ini を探す
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Settings.Load(Path.Combine(baseDir, "settings.ini"));
            Lang.Initialize(Settings.Language, Path.Combine(baseDir, "lang"));

            Application.Run(new MainForm());
        }
    }
}
