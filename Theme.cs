using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace InstantaleLauncher
{
    /// <summary>ダークテーマの配色・ボタンスタイル・ツールアイコン読み込み。</summary>
    public static class Theme
    {
        public static readonly Color Background = ColorTranslator.FromHtml("#1B1D23");
        public static readonly Color Panel = ColorTranslator.FromHtml("#22252D");
        public static readonly Color Tile = ColorTranslator.FromHtml("#252932");
        public static readonly Color TileBorder = ColorTranslator.FromHtml("#353B48");
        public static readonly Color Text = ColorTranslator.FromHtml("#E8E6E3");
        public static readonly Color TextDim = ColorTranslator.FromHtml("#9AA0A8");
        public static readonly Color Accent = ColorTranslator.FromHtml("#C9A15A");
        public static readonly Color ButtonBack = ColorTranslator.FromHtml("#2C313B");
        public static readonly Color ButtonHover = ColorTranslator.FromHtml("#3A4150");

        public static readonly Color BadgeExe = ColorTranslator.FromHtml("#3D7EBF");
        public static readonly Color BadgeHtml = ColorTranslator.FromHtml("#8A5CBF");
        public static readonly Color BadgeBat = ColorTranslator.FromHtml("#2FA98C");
        public static readonly Color BadgeSvc = Accent;

        public static readonly Color Running = ColorTranslator.FromHtml("#4FBE6C");
        public static readonly Color Stopped = ColorTranslator.FromHtml("#6B7280");
        public static readonly Color Warning = ColorTranslator.FromHtml("#E0A93E");

        public static readonly Color AccentHover = ColorTranslator.FromHtml("#D9B26B");
        public static readonly Color Danger = ColorTranslator.FromHtml("#C24A4A");
        public static readonly Color DangerHover = ColorTranslator.FromHtml("#D25B5B");
        public static readonly Color IconBox = Panel;

        /// <summary>ツール種別に対応するバッジ色を返す。</summary>
        public static Color BadgeColor(ToolKind kind)
        {
            switch (kind)
            {
                case ToolKind.Exe: return BadgeExe;
                case ToolKind.Html: return BadgeHtml;
                case ToolKind.Bat: return BadgeBat;
                default: return BadgeSvc;
            }
        }

        /// <summary>標準ボタン。枠線のみのフラットスタイル。</summary>
        public static void StyleButton(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = TileBorder;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = ButtonHover;
            b.BackColor = ButtonBack;
            b.ForeColor = Text;
            b.UseVisualStyleBackColor = false;
        }

        /// <summary>強調ボタン(再スキャン等)。枠線と文字をアクセントカラーにする。</summary>
        public static void StyleAccentButton(Button b)
        {
            StyleButton(b);
            b.FlatAppearance.BorderColor = Accent;
            b.ForeColor = Accent;
        }

        /// <summary>タイルの主ボタン(起動/実行)。塗りつぶしゴールド+濃色文字。</summary>
        public static void StylePrimaryButton(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = AccentHover;
            b.BackColor = Accent;
            b.ForeColor = Background;
            b.Font = BoldButtonFont(b);
            b.UseVisualStyleBackColor = false;
        }

        /// <summary>稼働中サービスの停止ボタン。塗りつぶし赤。</summary>
        public static void StyleDangerButton(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = DangerHover;
            b.BackColor = Danger;
            b.ForeColor = Color.White;
            b.Font = BoldButtonFont(b);
            b.UseVisualStyleBackColor = false;
        }

        private static Font _boldButtonFont;

        /// <summary>
        /// 主/停止ボタン共用の太字フォント。SVCタイルは状態変化のたびに再スタイルされるため、
        /// 毎回 new Font するとGDIハンドルがリークする。全ボタンが同一の既定フォント基準なので共有できる。
        /// </summary>
        private static Font BoldButtonFont(Button b)
        {
            if (_boldButtonFont == null)
                _boldButtonFont = new Font(b.Font, FontStyle.Bold);
            return _boldButtonFont;
        }

        // ---- アイコン ----
        // exe はアイコンリソースを持たないため抽出はせず、埋め込み PNG(icons\*.png)を
        // ツール名 → 種別既定の順で割り当てる。リソースが読めない場合は null を返し、
        // タイル側(TileBase)が空のアイコンボックスとして描画する。

        /// <summary>ツール名から専用アイコンを選ぶ。該当がなければ種別既定のアイコン。</summary>
        public static Bitmap LoadToolIcon(ToolInfo tool)
        {
            return LoadToolIcon(tool.Name, tool.Kind);
        }

        /// <summary>フォルダ名と種別から専用アイコンを選ぶ(未導入タイルなど ToolInfo が無い場合に使う)。</summary>
        public static Bitmap LoadToolIcon(string name, ToolKind kind)
        {
            var n = (name ?? string.Empty).ToLowerInvariant();
            Bitmap bmp = null;
            if (n.Contains("save")) bmp = LoadEmbeddedIcon("SaveEditor");
            else if (n.Contains("output")) bmp = LoadEmbeddedIcon("OutputViewer");
            else if (n.Contains("world")) bmp = LoadEmbeddedIcon("WorldViewer");
            else if (n.Contains("proxy") || n.Contains("llm")) bmp = LoadEmbeddedIcon("LLMProxy");
            return bmp ?? LoadEmbeddedIcon(DefaultIconName(kind));
        }

        /// <summary>種別ごとの既定アイコンのリソース名を返す。</summary>
        private static string DefaultIconName(ToolKind kind)
        {
            switch (kind)
            {
                case ToolKind.Exe: return "ExeDefault";
                case ToolKind.Html: return "HtmlDefault";
                case ToolKind.Bat: return "BatDefault";
                default: return "SvcDefault";
            }
        }

        /// <summary>立ち絵高画質化タイルのアイコン。</summary>
        public static Bitmap LoadPortraitIcon()
        {
            return LoadEmbeddedIcon("HDPortraitScript");
        }

        /// <summary>埋め込み icons\{name}.png を読む。無ければ null。</summary>
        private static Bitmap LoadEmbeddedIcon(string name)
        {
            try
            {
                var asm = typeof(Theme).Assembly;
                using (var stream = asm.GetManifestResourceStream("InstantaleLauncher.icons." + name + ".png"))
                {
                    if (stream == null) return null;
                    using (var img = new Bitmap(stream))
                        return new Bitmap(img);   // ストリームから切り離したコピーを返す
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>角丸矩形のパスを生成する(タイルの背景・アイコンボックス等で共用)。</summary>
        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

    }
}
