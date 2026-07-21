using System.Collections.Generic;
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
        public static readonly Color TileHover = ColorTranslator.FromHtml("#2E323C");   // 本体クリック可能タイルのホバー背景(Tile より一段明るい)
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

        /// <summary>
        /// タイル副ボタン(更新「⟳」)用の控えめなスタイル。枠のみ・小サイズ・淡色文字。
        /// 更新ありのときは呼び出し側でアクセント色に強調する。
        /// </summary>
        public static void StyleGhostButton(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = TileBorder;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = ButtonHover;
            b.BackColor = Tile;
            b.ForeColor = TextDim;
            b.Font = GhostButtonFont();
            b.Padding = Padding.Empty;   // 小さいボタンでも ⟳ グリフが上下に切れないよう内側余白を無くす
            b.TextAlign = ContentAlignment.MiddleCenter;
            b.UseVisualStyleBackColor = false;
        }

        // ---- フォント ----
        // タイルは再スキャンのたびに作り直される。Label/Button は Font を破棄しないため、
        // 都度 new Font するとGDIハンドルがリークする。ファミリ+サイズ+スタイル別に共有キャッシュする。
        // テキストはロケール依存の UI ファミリ(日本語環境では Yu Gothic UI 等)で統一し、
        // 記号グリフ(⟳ ● ✕)だけはグリフ被覆が確実な Segoe UI を使う。

        /// <summary>全テキストラベル共通の基準ファミリ。ロケールに追従し、環境間で書体が揃う。</summary>
        public static readonly FontFamily UiFamily = SystemFonts.MessageBoxFont.FontFamily;

        private static readonly Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();

        /// <summary>ロケール依存 UI ファミリのフォントを取得する(共有キャッシュ)。名前・見出し等のテキストに使う。</summary>
        public static Font UiFont(float size, FontStyle style = FontStyle.Regular)
        {
            return CachedFont(UiFamily.Name, size, style);
        }

        /// <summary>記号グリフ(⟳ ● ✕)用。ロケールに依らずグリフ被覆が確実な Segoe UI を使う。</summary>
        public static Font SymbolFont(float size, FontStyle style = FontStyle.Regular)
        {
            return CachedFont("Segoe UI", size, style);
        }

        /// <summary>ファミリ+サイズ+スタイルをキーにフォントを共有する(破棄はプロセス終了時にOSへ委ねる)。</summary>
        private static Font CachedFont(string family, float size, FontStyle style)
        {
            var key = family + "|" +
                size.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" + (int)style;
            Font f;
            if (!_fontCache.TryGetValue(key, out f))
            {
                f = new Font(family, size, style);
                _fontCache[key] = f;
            }
            return f;
        }

        /// <summary>主/停止ボタン共用の太字フォント。既定サイズ(=UIファミリ)を太字化して共有する。</summary>
        private static Font BoldButtonFont(Button b)
        {
            return UiFont(b.Font.SizeInPoints, FontStyle.Bold);
        }

        /// <summary>副ボタン(⟳)共用の記号フォント。</summary>
        private static Font GhostButtonFont()
        {
            return SymbolFont(10f);
        }

        // ---- アイコン ----
        // exe はアイコンリソースを持たないため抽出はせず、埋め込み PNG(icons\*.png)を
        // ツール名 → 種別既定の順で割り当てる。リソースが読めない場合は null を返し、
        // タイル側(TileBase)が空のアイコンボックスとして描画する。

        /// <summary>ツール名から専用アイコンを選ぶ。該当がなければ種別既定のアイコン。</summary>
        public static Bitmap LoadToolIcon(ToolInfo tool)
        {
            return LoadIconByName(tool.Name, tool.Kind);
        }

        /// <summary>未導入タイル用。カタログのフォルダ名＋種別からアイコンを選ぶ。</summary>
        public static Bitmap LoadCatalogIcon(ToolCatalogEntry entry)
        {
            return LoadIconByName(entry.FolderName, entry.DisplayKind);
        }

        /// <summary>名前の部分一致で専用アイコンを選び、無ければ種別既定のアイコンを返す。</summary>
        private static Bitmap LoadIconByName(string name, ToolKind kind)
        {
            var n = (name ?? "").ToLowerInvariant();
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
