using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace InstantaleLauncher
{
    /// <summary>
    /// タイル共通の土台(案B: 縦型レイアウト)。
    /// 左上バッジ・右上状態ドット・中央アイコンボックス・中央寄せ名前・下部全幅ボタン。
    /// パスや PID 等の詳細情報はツールチップで示す。
    /// </summary>
    public abstract class TileBase : Panel
    {
        protected const int TileWidth = 172;
        protected const int TileHeight = 200;
        private const int IconBoxSize = 60;
        private const int IconSize = 40;

        private readonly Bitmap _iconBmp;
        private readonly Region _region;
        private readonly Label _badge;
        protected readonly Label NameLabel;
        protected readonly Label StateDot;
        protected readonly Button ActionButton;
        private readonly ToolTip _tip = new ToolTip();

        /// <summary>共通の見た目(角丸パネル・バッジ・状態ドット・アイコン・名前・主ボタン)を構築する。</summary>
        protected TileBase(string name, Bitmap icon, string badgeText, Color badgeColor)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            Size = new Size(TileWidth, TileHeight);
            Margin = new Padding(10);
            BackColor = Theme.Tile;
            _iconBmp = icon;

            // Control は Region を破棄しないため、自前で保持して Dispose する
            using (var path = Theme.RoundRect(new Rectangle(0, 0, TileWidth, TileHeight), 10))
            {
                _region = new Region(path);
                Region = _region;
            }

            _badge = new Label
            {
                Text = badgeText,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = badgeColor,
                Bounds = new Rectangle(12, 12, 38, 18),
                Visible = badgeText != null,
            };
            Controls.Add(_badge);

            StateDot = new Label
            {
                Text = "●",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Theme.Stopped,
                BackColor = Color.Transparent,
                Bounds = new Rectangle(TileWidth - 32, 10, 22, 22),
                Visible = false,
            };
            Controls.Add(StateDot);

            NameLabel = new Label
            {
                Text = name,
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9.5f, FontStyle.Bold),
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Bounds = new Rectangle(8, 106, TileWidth - 16, 38),
            };
            Controls.Add(NameLabel);

            ActionButton = new Button
            {
                Bounds = new Rectangle(16, TileHeight - 46, TileWidth - 32, 32),
            };
            Theme.StylePrimaryButton(ActionButton);
            Controls.Add(ActionButton);
        }

        /// <summary>タイル本体と子コントロールにまとめて同じツールチップを設定する。</summary>
        protected void SetTooltip(string text)
        {
            _tip.SetToolTip(this, text);
            foreach (Control c in Controls)
            {
                if (!ReferenceEquals(c, ActionButton))
                    _tip.SetToolTip(c, text);
            }
        }

        /// <summary>アイコンボックス・アイコン画像・タイル外枠を描画する(コントロール配置だけでは表現できない部分)。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var box = new Rectangle((TileWidth - IconBoxSize) / 2, 38, IconBoxSize, IconBoxSize);
            using (var path = Theme.RoundRect(box, 8))
            using (var brush = new SolidBrush(Theme.IconBox))
            using (var boxPen = new Pen(Theme.TileBorder))
            {
                g.FillPath(brush, path);
                g.DrawPath(boxPen, path);
            }
            if (_iconBmp != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(_iconBmp,
                    box.X + (IconBoxSize - IconSize) / 2,
                    box.Y + (IconBoxSize - IconSize) / 2,
                    IconSize, IconSize);
            }

            using (var pen = new Pen(Theme.TileBorder))
            using (var border = Theme.RoundRect(new Rectangle(0, 0, TileWidth - 1, TileHeight - 1), 10))
                g.DrawPath(pen, border);
        }

        /// <summary>ツールチップ・アイコンビットマップ・角丸リージョンを解放する。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tip.Dispose();
                if (_iconBmp != null)
                    _iconBmp.Dispose();
            }
            base.Dispose(disposing);
            if (disposing)
                _region.Dispose();   // ウィンドウ破棄後に解放する(基底が形状を参照し得るため)
        }
    }

    /// <summary>検出されたツール1件のタイル。SVC は1ボタンで起動/停止をトグルする。</summary>
    public sealed class ToolTile : TileBase
    {
        private readonly ToolInfo _tool;
        private readonly ServiceManager _services;

        public ToolInfo Tool
        {
            get { return _tool; }
        }

        /// <summary>SVC はトグルボタン(起動/停止)を、それ以外は単発起動ボタンを持つタイルを構築する。</summary>
        public ToolTile(ToolInfo tool, ServiceManager services, Action<ToolInfo> onLaunch)
            : base(tool.Name, Theme.LoadToolIcon(tool), tool.Kind.ToString().ToUpperInvariant(), Theme.BadgeColor(tool.Kind))
        {
            _tool = tool;
            _services = services;

            if (tool.Kind == ToolKind.Svc)
            {
                StateDot.Visible = true;
                ActionButton.Click += delegate { ToggleService(); };
                RefreshServiceState();
            }
            else
            {
                ActionButton.Text = PrimaryButtonText(tool.Kind);
                ActionButton.Click += delegate { onLaunch(_tool); };
                SetTooltip(_tool.EntryPath);
            }
        }

        /// <summary>種別ごとの主ボタン文言(開く/実行/起動)を返す。</summary>
        private static string PrimaryButtonText(ToolKind kind)
        {
            switch (kind)
            {
                case ToolKind.Html: return Lang.T("Tile.Open");
                case ToolKind.Bat: return Lang.T("Tile.Run");
                default: return Lang.T("Tile.Launch");
            }
        }

        /// <summary>SVC タイルの主ボタン押下時。稼働中なら停止要求、停止中なら起動を試みる。</summary>
        private void ToggleService()
        {
            if (_services.IsRunning(_tool.Folder))
            {
                _services.Stop(_tool.Folder);
                return;   // 停止完了は Exited → StateChanged 経由で反映される
            }
            try
            {
                _services.Start(_tool);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, _tool.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshServiceState();
        }

        /// <summary>SVCタイルの状態ドット・ボタン・ツールチップを更新する(UIスレッドで呼ぶこと)。</summary>
        public void RefreshServiceState()
        {
            if (_tool.Kind != ToolKind.Svc) return;
            int pid;
            bool running = _services.TryGetPid(_tool.Folder, out pid);

            StateDot.ForeColor = running ? Theme.Running : Theme.Stopped;
            if (running)
            {
                ActionButton.Text = Lang.T("Service.Stop");
                Theme.StyleDangerButton(ActionButton);
                SetTooltip(_tool.EntryPath + Environment.NewLine + Lang.F("Service.Running", pid));
            }
            else
            {
                ActionButton.Text = Lang.T("Service.Start");
                Theme.StylePrimaryButton(ActionButton);
                SetTooltip(_tool.EntryPath + Environment.NewLine + Lang.T("Service.Stopped"));
            }
        }
    }

    /// <summary>
    /// 立ち絵高画質化の常設タイル。ボタンで監視のON/OFFをトグルし、
    /// タイル本体クリックで画面下部の詳細パネル(一括適用・復元・ログ)を開閉する。
    /// </summary>
    public sealed class PortraitTile : TileBase
    {
        private readonly PortraitWatcher _watcher;
        private readonly Action _showDetails;

        /// <summary>
        /// 主ボタンは監視ON/OFFのトグル、タイル本体(ボタン以外の全コントロール)のクリックは
        /// onToggleDetails(詳細パネルの開閉)に割り当てる。
        /// </summary>
        public PortraitTile(PortraitWatcher watcher, Action onToggleDetails, Action onShowDetails)
            : base(Lang.T("Portrait.TileName"), Theme.LoadPortraitIcon(), "PS1", Theme.BadgeBat)
        {
            _watcher = watcher;
            _showDetails = onShowDetails;
            Cursor = Cursors.Hand;
            StateDot.Visible = true;

            ActionButton.Click += delegate { ToggleWatch(); };

            EventHandler open = delegate { onToggleDetails(); };
            Click += open;
            foreach (Control c in Controls)
            {
                if (ReferenceEquals(c, ActionButton)) continue;
                c.Click += open;
                c.Cursor = Cursors.Hand;
            }

            RefreshWatchState();
        }

        /// <summary>監視ボタン押下時のON/OFF切り替え。ルート不在で開始できなければ詳細パネルで案内する。</summary>
        private void ToggleWatch()
        {
            if (_watcher.IsWatching)
            {
                _watcher.StopWatch();
            }
            else if (!_watcher.StartWatch())
            {
                _showDetails();   // ルート不在の案内をパネルで見せる
            }
        }

        /// <summary>監視状態の表示(ドット・ボタン・ツールチップ)を更新する(UIスレッドで呼ぶこと)。</summary>
        public void RefreshWatchState()
        {
            bool watching = _watcher.IsWatching;
            StateDot.ForeColor = watching ? Theme.Running : Theme.Stopped;
            ActionButton.Text = Lang.T("Portrait.Watch") + (watching ? " OFF" : " ON");
            if (watching)
                Theme.StyleDangerButton(ActionButton);
            else
                Theme.StylePrimaryButton(ActionButton);
            SetTooltip(Lang.T(watching ? "Portrait.Watching" : "Portrait.NotWatching")
                + Environment.NewLine + Lang.T("Portrait.TileDetail"));
        }
    }

    /// <summary>
    /// 既知ツールの未導入タイル。ボタン押下で GitHub の最新リリースを取得して tools\ へ導入し、
    /// 完了後に再スキャンを促す(このタイルは通常タイルへ置き換わる)。
    /// </summary>
    public sealed class InstallTile : TileBase
    {
        private readonly CatalogEntry _entry;
        private readonly string _toolsDir;
        private readonly Action _onInstalled;
        private bool _busy;

        /// <summary>未導入バッジ・取得ボタンを持つタイルを構築する。onInstalled は導入成功時の再スキャン用。</summary>
        public InstallTile(CatalogEntry entry, string toolsDir, Action onInstalled)
            : base(entry.Title, Theme.LoadToolIcon(entry.Folder, entry.Kind),
                   entry.Kind.ToString().ToUpperInvariant(), Theme.BadgeColor(entry.Kind))
        {
            _entry = entry;
            _toolsDir = toolsDir;
            _onInstalled = onInstalled;

            StateDot.Visible = true;
            StateDot.ForeColor = Theme.Stopped;   // 未導入を示す

            ActionButton.Text = Lang.T("Tile.Install");
            Theme.StyleAccentButton(ActionButton);
            ActionButton.Click += delegate { StartInstall(); };
            SetTooltip(Lang.F("Tile.NotInstalled", _entry.Owner + "/" + _entry.Repo));
        }

        /// <summary>取得を開始する。以降の重い処理はバックグラウンドスレッドで行う。</summary>
        private void StartInstall()
        {
            if (_busy) return;
            _busy = true;
            ActionButton.Enabled = false;
            ActionButton.Text = Lang.T("Tile.Fetching");

            var thread = new Thread(RunInstall);
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>最新リリースの解決 → ダウンロード → 展開。完了/失敗を UI スレッドへ戻す。</summary>
        private void RunInstall()
        {
            try
            {
                var asset = ReleaseInstaller.ResolveLatest(_entry.Owner, _entry.Repo);
                if (asset == null)
                    throw new InvalidOperationException(Lang.T("Tile.NoAsset"));

                ReleaseInstaller.Install(_toolsDir, _entry.Folder, asset, ReportProgress);
                OnUi(delegate { _onInstalled(); });   // 再スキャンでこのタイルは破棄・置換される
            }
            catch (Exception ex)
            {
                OnUi(delegate { FinishError(ex.Message); });
            }
        }

        /// <summary>ダウンロード進捗(0..100、-1 は不明)をボタン文言へ反映する。</summary>
        private void ReportProgress(int percent)
        {
            OnUi(delegate
            {
                ActionButton.Text = percent < 0
                    ? Lang.T("Tile.Fetching")
                    : Lang.F("Tile.FetchingPercent", percent);
            });
        }

        /// <summary>失敗時: ボタンを元に戻し、原因をダイアログで示す。</summary>
        private void FinishError(string message)
        {
            _busy = false;
            ActionButton.Enabled = true;
            ActionButton.Text = Lang.T("Tile.Install");
            MessageBox.Show(this, message, _entry.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>UI スレッドへマーシャリングして実行する(破棄済みなら無視)。</summary>
        private void OnUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(action); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
    }
}
