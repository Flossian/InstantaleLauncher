using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private const int CornerRadius = 10;

        /// <summary>
        /// 更新ボタンの待機/失敗表示用マーカー。実際のグリフはフォント依存のグリフ欠落で
        /// 縦方向にクリップされることがあるため使わず、Text を空にして手描きの矢印を描く目印にする。
        /// </summary>
        protected const string UpdateGlyph = "";

        private readonly Bitmap _iconBmp;
        // 角丸クリップ領域。DPI 拡大でコントロールの実サイズが変わるため、リサイズ時に実サイズで作り直す
        // (固定値のままだと右上=更新ボタン付近が先にクリップされて見切れる)。
        private Region _region;
        private readonly Label _badge;
        protected readonly Label NameLabel;
        protected readonly Label StateDot;
        protected readonly Button ActionButton;
        /// <summary>右上の副ボタン(更新)。EnableUpdateButton を呼ぶまで null。</summary>
        protected Button UpdateButton;
        private readonly ToolTip _tip = new ToolTip();

        // ホバー強調。本体クリックで動作するタイルのみ EnableHoverHighlight で有効化する。
        private bool _hovered;

        /// <summary>共通の見た目(角丸パネル・バッジ・状態ドット・アイコン・名前・主ボタン)を構築する。</summary>
        protected TileBase(string name, Bitmap icon, string badgeText, Color badgeColor)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            Size = new Size(TileWidth, TileHeight);
            Margin = new Padding(10);
            BackColor = Theme.Tile;
            _iconBmp = icon;

            // Control は Region を破棄しないため、自前で保持して Dispose する。
            // 実サイズ(DPI 拡大後)で作るため OnSizeChanged 経由で構築する。
            RebuildRegion();

            _badge = new Label
            {
                Text = badgeText,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.UiFont(7.5f, FontStyle.Bold),
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
                Font = Theme.SymbolFont(9f),
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
                Font = Theme.UiFont(9.5f, FontStyle.Bold),
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

        /// <summary>タイル本体と子コントロールにまとめて同じツールチップを設定する(主ボタン・更新ボタンは独自)。</summary>
        protected void SetTooltip(string text)
        {
            _tip.SetToolTip(this, text);
            foreach (Control c in Controls)
            {
                if (!ReferenceEquals(c, ActionButton) && !ReferenceEquals(c, UpdateButton))
                    _tip.SetToolTip(c, text);
            }
        }

        /// <summary>
        /// 右上に更新用の副ボタンを追加する。leftOfStateDot=true のときは状態ドット(SVC)の左に、
        /// false のときはドット非表示の位置(右上)に置く。
        /// </summary>
        protected void EnableUpdateButton(bool leftOfStateDot, EventHandler onClick)
        {
            const int w = 26, h = 20;
            // 角丸コーナー(半径 CornerRadius)に食い込まないよう、上・右に十分な余白を取る。
            // SVC は状態ドット(右上)の左へ、それ以外はドット非表示の右上へ置く。
            int right = leftOfStateDot ? (TileWidth - 32 - 4) : (TileWidth - 12);
            UpdateButton = new Button
            {
                Text = UpdateGlyph,
                Bounds = new Rectangle(right - w, 13, w, h),
                TabStop = false,
            };
            Theme.StyleGhostButton(UpdateButton);
            UpdateButton.Click += onClick;
            UpdateButton.Paint += OnUpdateButtonPaint;
            Controls.Add(UpdateButton);
            UpdateButton.BringToFront();
        }

        /// <summary>
        /// 待機/失敗状態(Text が空)のときだけ、時計回り矢印を手描きする。
        /// ダウンロード中の数値("42")や展開中の"…"はテキストのまま既定描画に任せる。
        /// </summary>
        private void OnUpdateButtonPaint(object sender, PaintEventArgs e)
        {
            var b = (Button)sender;
            if (b.Text.Length != 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int d = Math.Min(b.Width, b.Height) - 9;
            var arcRect = new Rectangle((b.Width - d) / 2, (b.Height - d) / 2, d, d);
            using (var pen = new Pen(b.ForeColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawArc(pen, arcRect, -40, 280);

                // 弧の終端に矢じりを添える
                double endRad = 240.0 * Math.PI / 180.0;
                float cx = arcRect.X + arcRect.Width / 2f;
                float cy = arcRect.Y + arcRect.Height / 2f;
                float r = arcRect.Width / 2f;
                float tx = cx + r * (float)Math.Cos(endRad);
                float ty = cy + r * (float)Math.Sin(endRad);
                g.DrawLine(pen, tx - 3.2f, ty - 0.5f, tx + 0.4f, ty + 2.6f);
                g.DrawLine(pen, tx + 2.6f, ty - 2.2f, tx + 0.4f, ty + 2.6f);
            }
        }

        /// <summary>更新ボタン専用のツールチップを設定する(バージョン情報など)。</summary>
        protected void SetUpdateTooltip(string text)
        {
            if (UpdateButton != null)
                _tip.SetToolTip(UpdateButton, text);
        }

        /// <summary>
        /// タイル本体クリックで動作するタイル向けに、ホバー時の強調(背景を一段明るく・枠をアクセント色)を有効化する。
        /// 子コントロール上でもホバーを維持できるよう、本体と(呼び出し時点の)全子のマウス出入りを監視する。
        /// </summary>
        protected void EnableHoverHighlight()
        {
            HookHover(this);
            foreach (Control c in Controls)
                HookHover(c);
        }

        private void HookHover(Control c)
        {
            c.MouseEnter += OnHoverEnter;
            c.MouseLeave += OnHoverLeave;
        }

        private void OnHoverEnter(object sender, EventArgs e)
        {
            if (_hovered) return;
            _hovered = true;
            BackColor = Theme.TileHover;
            Invalidate();   // 枠色(アクセント)を描き直す
        }

        private void OnHoverLeave(object sender, EventArgs e)
        {
            // 子→子・子→本体の移動でも MouseLeave は飛ぶ。実際のカーソル位置がタイル内なら強調を維持する。
            if (!IsDisposed && ClientRectangle.Contains(PointToClient(Cursor.Position))) return;
            _hovered = false;
            BackColor = Theme.Tile;
            Invalidate();
        }

        /// <summary>実サイズで角丸クリップ領域を作り直す(DPI 拡大で固定値だと右上が見切れるのを防ぐ)。</summary>
        private void RebuildRegion()
        {
            var old = _region;
            using (var path = Theme.RoundRect(new Rectangle(0, 0, Width, Height), CornerRadius))
                _region = new Region(path);
            Region = _region;
            if (old != null) old.Dispose();
        }

        /// <summary>サイズ変更(DPI 自動スケール含む)のたびにクリップ領域を実サイズへ追従させる。</summary>
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            RebuildRegion();
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

            using (var pen = new Pen(_hovered ? Theme.Accent : Theme.TileBorder))
            using (var border = Theme.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius))
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
        public ToolTile(ToolInfo tool, ServiceManager services, Action<ToolInfo> onLaunch,
            Action<ToolCatalogEntry, ToolTile> onUpdate)
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

            // カタログ登録ツールのみ更新ボタンを表示する
            var catalog = ToolCatalog.Find(ToolCatalog.FolderBaseName(tool.Folder));
            if (catalog != null && onUpdate != null)
            {
                EnableUpdateButton(tool.Kind == ToolKind.Svc, delegate { onUpdate(catalog, this); });
                var tag = ToolInstaller.ReadInstalledTag(tool.Folder);
                SetUpdateTooltip(tag != null
                    ? Lang.F("Update.CurrentVersion", tag) + Environment.NewLine + Lang.T("Update.Button")
                    : Lang.T("Update.Button"));
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

        /// <summary>「更新を確認」で最新tagが現在と異なると判明したとき、更新ボタンをアクセント色で強調する。</summary>
        public void MarkUpdateAvailable(string latestTag)
        {
            if (UpdateButton == null) return;
            UpdateButton.FlatAppearance.BorderColor = Theme.Accent;
            UpdateButton.ForeColor = Theme.Accent;
            SetUpdateTooltip(Lang.F("Update.Available", latestTag));
        }

        /// <summary>更新のダウンロード進捗を更新ボタン上に表示する(狭いため数値のみ、詳細はツールチップ)。</summary>
        public void SetProgress(int pct)
        {
            if (UpdateButton == null) return;
            UpdateButton.Enabled = false;
            ActionButton.Enabled = false;   // 更新中は起動/停止を止める
            UpdateButton.Text = pct.ToString();
            SetUpdateTooltip(Lang.F("Install.Downloading", pct));
        }

        /// <summary>更新の展開中表示。</summary>
        public void SetInstalling()
        {
            if (UpdateButton == null) return;
            UpdateButton.Text = "…";
            SetUpdateTooltip(Lang.T("Install.Installing"));
        }

        /// <summary>更新失敗時にボタンを元に戻す(Rescan されない失敗経路のため)。</summary>
        public void SetFailed()
        {
            if (UpdateButton == null) return;
            UpdateButton.Enabled = true;
            ActionButton.Enabled = true;
            UpdateButton.Text = UpdateGlyph;
            SetUpdateTooltip(Lang.T("Install.Failed"));
        }
    }

    /// <summary>
    /// 未導入ツールのタイル。主ボタンは「ダウンロード」で、押すと GitHub 最新リリースを取得して
    /// tools\ 配下へ展開する。導入が完了すると MainForm の Rescan で通常の起動タイルに置き換わる。
    /// </summary>
    public sealed class InstallTile : TileBase
    {
        private readonly ToolCatalogEntry _entry;

        public ToolCatalogEntry Entry
        {
            get { return _entry; }
        }

        /// <summary>未導入状態(状態ドットは停止色)＋「ダウンロード」ボタンのタイルを構築する。</summary>
        public InstallTile(ToolCatalogEntry entry, Action<ToolCatalogEntry, InstallTile> onInstall)
            : base(entry.FolderName, Theme.LoadCatalogIcon(entry),
                   entry.DisplayKind.ToString().ToUpperInvariant(), Theme.BadgeColor(entry.DisplayKind))
        {
            _entry = entry;
            StateDot.Visible = true;
            StateDot.ForeColor = Theme.Stopped;   // 未導入

            ActionButton.Text = Lang.T("Install.Download");
            Theme.StyleAccentButton(ActionButton);   // 起動(ゴールド塗り)と区別するため枠のみのアクセント
            ActionButton.Click += delegate { onInstall(_entry, this); };
            SetTooltip(Lang.T("Install.NotInstalled"));
        }

        /// <summary>ダウンロード進捗(0-100%)を主ボタンに表示する。</summary>
        public void SetProgress(int pct)
        {
            ActionButton.Enabled = false;
            ActionButton.Text = Lang.F("Install.Downloading", pct);
        }

        /// <summary>展開中の表示。</summary>
        public void SetInstalling()
        {
            ActionButton.Enabled = false;
            ActionButton.Text = Lang.T("Install.Installing");
        }

        /// <summary>失敗時に「ダウンロード」ボタンへ戻す。</summary>
        public void SetFailed()
        {
            ActionButton.Enabled = true;
            Theme.StyleAccentButton(ActionButton);
            ActionButton.Text = Lang.T("Install.Download");
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

            EnableHoverHighlight();   // 本体クリックで詳細を開けることをホバーで示す
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
}
