using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace InstantaleLauncher
{
    /// <summary>
    /// アプリのメイン画面。上部ツールバー・中央のツールタイル一覧・下部の立ち絵詳細パネル・ステータスバーで構成される。
    /// tools\ フォルダの走査とタイル生成、ゲーム本体の起動、ウィンドウ位置の保存も担う。
    /// </summary>
    public sealed class MainForm : Form
    {
        private readonly string _toolsDir;
        private readonly ServiceManager _services = new ServiceManager();
        private readonly PortraitWatcher _watcher;

        private readonly FlowLayoutPanel _flow;
        private readonly Label _countLabel;
        private readonly ToolStripStatusLabel _pathLabel;
        private readonly ToolStripStatusLabel _scanLabel;
        private readonly Dictionary<string, ToolTile> _tilesByFolder =
            new Dictionary<string, ToolTile>(StringComparer.OrdinalIgnoreCase);

        private PortraitTile _portraitTile;
        private readonly PortraitPanel _portraitPanel;
        private readonly Button _gameButton;
        private readonly ToolTip _gameTip = new ToolTip();

        /// <summary>ツールバー・ステータスバー・タイルグリッド・立ち絵パネルを組み立て、初回スキャンを予約する。</summary>
        public MainForm()
        {
            Text = Lang.T("App.Title");
            Font = SystemFonts.MessageBoxFont;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            ClientSize = new Size(860, 560);
            MinimumSize = new Size(560, 360);
            StartPosition = FormStartPosition.CenterScreen;
            RestoreWindowBounds();

            _toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
            _watcher = new PortraitWatcher(Settings.PortraitPath);

            // ---- ツールバー ----
            var toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Theme.Panel,
            };
            _gameButton = new Button
            {
                Text = Lang.T("Game.Launch"),
                Bounds = new Rectangle(16, 9, 110, 30),
            };
            Theme.StylePrimaryButton(_gameButton);
            _gameButton.Click += delegate { LaunchGame(); };
            toolbar.Controls.Add(_gameButton);

            var gameDirButton = new Button
            {
                Text = "…",
                Bounds = new Rectangle(130, 9, 34, 30),
            };
            Theme.StyleButton(gameDirButton);
            _gameTip.SetToolTip(gameDirButton, Lang.T("Game.SelectDirTip"));
            gameDirButton.Click += delegate { SelectGameDir(); };
            toolbar.Controls.Add(gameDirButton);
            UpdateGameTooltip();

            _countLabel = new Label
            {
                AutoSize = true,
                Location = new Point(180, 15),
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
            };
            toolbar.Controls.Add(_countLabel);

            var rescanButton = new Button
            {
                Text = Lang.T("Toolbar.Rescan"),
                Size = new Size(110, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            rescanButton.Location = new Point(toolbar.Width, 9);   // 位置は Layout で確定
            Theme.StyleAccentButton(rescanButton);
            rescanButton.Click += delegate { Rescan(); };
            toolbar.Controls.Add(rescanButton);
            toolbar.Layout += delegate
            {
                rescanButton.Location = new Point(toolbar.ClientSize.Width - rescanButton.Width - 16, 9);
            };

            // ---- ステータスバー ----
            var status = new StatusStrip
            {
                BackColor = Theme.Panel,
                SizingGrip = false,
            };
            _pathLabel = new ToolStripStatusLabel
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Theme.TextDim,
            };
            _scanLabel = new ToolStripStatusLabel
            {
                ForeColor = Theme.TextDim,
            };
            status.Items.Add(_pathLabel);
            status.Items.Add(_scanLabel);

            // ---- タイルグリッド ----
            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(12),
                BackColor = Theme.Background,
            };

            // ---- 立ち絵高画質化の詳細パネル(画面下部・開閉式) ----
            _portraitPanel = new PortraitPanel(_watcher);

            Controls.Add(_flow);
            Controls.Add(_portraitPanel);
            Controls.Add(toolbar);
            Controls.Add(status);

            _services.StateChanged += OnServiceStateChanged;
            _watcher.WatchStateChanged += OnWatchStateChanged;

            Load += delegate
            {
                Rescan();
                if (Settings.PortraitAutoStart)
                    _watcher.StartWatch();   // ルート不在なら false で無視(エラーにしない)
            };
            FormClosing += delegate
            {
                SaveWindowBounds();
                // 管理下のサービスは終了させず残す(デタッチのみ)
                _services.Dispose();
                _watcher.Dispose();
            };
        }

        /// <summary>Controls に載らないコンポーネント(ツールチップ)を破棄する。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _gameTip.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>settings.ini の前回位置・サイズを復元。画面外や未保存なら既定(中央)のまま。</summary>
        private void RestoreWindowBounds()
        {
            if (Settings.WindowWidth > 0 && Settings.WindowHeight > 0)
            {
                Size = new Size(
                    Math.Max(Settings.WindowWidth, MinimumSize.Width),
                    Math.Max(Settings.WindowHeight, MinimumSize.Height));
            }
            if (Settings.WindowX != int.MinValue)
            {
                var bounds = new Rectangle(Settings.WindowX, Settings.WindowY, Size.Width, Size.Height);
                foreach (var screen in Screen.AllScreens)
                {
                    if (screen.WorkingArea.IntersectsWith(bounds))
                    {
                        StartPosition = FormStartPosition.Manual;
                        Location = bounds.Location;
                        break;
                    }
                }
            }
            if (Settings.WindowMaximized)
                WindowState = FormWindowState.Maximized;
        }

        /// <summary>現在のウィンドウ位置・サイズ・最大化状態を settings.ini に保存する(終了時に呼ぶ)。</summary>
        private void SaveWindowBounds()
        {
            // 最大化・最小化中は通常時の矩形(RestoreBounds)を保存する
            var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            Settings.WindowX = b.X;
            Settings.WindowY = b.Y;
            Settings.WindowWidth = b.Width;
            Settings.WindowHeight = b.Height;
            Settings.WindowMaximized = WindowState == FormWindowState.Maximized;
            Settings.Save();
        }

        /// <summary>tools\ を再走査してタイル一覧を丸ごと作り直す。既存タイルは全て破棄してから再生成する。</summary>
        private void Rescan()
        {
            var tools = ToolScanner.Scan(_toolsDir);

            _flow.SuspendLayout();
            // Dispose は自身を親の Controls から取り除くため、foreach で列挙すると取りこぼす
            while (_flow.Controls.Count > 0)
                _flow.Controls[0].Dispose();
            _tilesByFolder.Clear();

            // 立ち絵高画質化タイルは検出に依存せず常時表示
            _portraitTile = new PortraitTile(_watcher, TogglePortraitPanel, ShowPortraitPanel);
            _flow.Controls.Add(_portraitTile);

            var installedNames = new List<string>();
            foreach (var tool in tools)
            {
                // 前回終了時に残した(または手動起動された)サービスを拾い直す
                if (tool.Kind == ToolKind.Svc)
                    _services.Attach(tool);

                var tile = new ToolTile(tool, _services, LaunchTool);
                _tilesByFolder[tool.Folder] = tile;
                _flow.Controls.Add(tile);
                installedNames.Add(tool.Name);
            }

            // 未導入の既知ツールは「入手」タイルとして表示し、ネットから最新リリースを取得できるようにする
            var missing = ToolCatalog.Missing(installedNames);
            foreach (var entry in missing)
                _flow.Controls.Add(new InstallTile(entry, InstallTool));

            if (tools.Count == 0 && missing.Count == 0)
            {
                _flow.Controls.Add(new Label
                {
                    Text = Lang.T("Status.ToolsMissing"),
                    AutoSize = true,
                    ForeColor = Theme.TextDim,
                    Margin = new Padding(12, 20, 12, 12),
                });
            }

            _flow.ResumeLayout();

            _countLabel.Text = Lang.F("Toolbar.ToolCount", tools.Count);
            _pathLabel.Text = Lang.F("Status.ToolsPath", _toolsDir);
            _scanLabel.Text = Lang.F("Status.LastScan", DateTime.Now.ToString("HH:mm:ss"));
        }

        /// <summary>ゲーム本体の起動。フォルダ未設定・exe不在なら先にフォルダ選択を促す。</summary>
        private void LaunchGame()
        {
            if (string.IsNullOrEmpty(Settings.GameDir) ||
                !File.Exists(Path.Combine(Settings.GameDir, "instantale.exe")))
            {
                if (!SelectGameDir()) return;
            }
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(Settings.GameDir, "instantale.exe"),
                    WorkingDirectory = Settings.GameDir,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Lang.T("Game.Launch"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>ゲームフォルダのパスを入力して settings.ini に保存。instantale.exe が無いフォルダは受け付けない。</summary>
        private bool SelectGameDir()
        {
            using (var dlg = new GameDirDialog(Settings.GameDir))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                Settings.GameDir = dlg.SelectedDir;
                Settings.Save();
                UpdateGameTooltip();
                return true;
            }
        }

        /// <summary>ゲーム起動ボタン横の「…」ボタンに、現在のゲームフォルダ(未設定なら案内文)をツールチップ表示する。</summary>
        private void UpdateGameTooltip()
        {
            _gameTip.SetToolTip(_gameButton, string.IsNullOrEmpty(Settings.GameDir)
                ? Lang.T("Game.DirNotSet")
                : Path.Combine(Settings.GameDir, "instantale.exe"));
        }

        /// <summary>EXE/HTML/BAT の起動。作業ディレクトリをツールフォルダにして ShellExecute。以降は関与しない。</summary>
        private void LaunchTool(ToolInfo tool)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = tool.EntryPath,
                    WorkingDirectory = tool.Folder,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, tool.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 未導入ツールの最新リリースをバックグラウンドで取得・導入する。
        /// 成功時は再スキャンして起動タイルへ差し替え、失敗時はボタンを戻してエラーを表示する。
        /// </summary>
        private void InstallTool(CatalogEntry entry, InstallTile tile)
        {
            tile.BeginInstall();
            Task.Run(delegate
            {
                Exception error = null;
                try { ReleaseInstaller.Install(entry, _toolsDir); }
                catch (Exception ex) { error = ex; }

                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (error == null)
                        {
                            Rescan();   // 導入済みになったので一覧を作り直す(入手タイル→起動タイル)
                        }
                        else
                        {
                            if (!tile.IsDisposed) tile.EndInstall();
                            MessageBox.Show(this, error.Message, Lang.F("Install.Failed", entry.Folder),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    });
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            });
        }

        /// <summary>立ち絵詳細パネルを開く(既に開いていれば何もしない)。</summary>
        private void ShowPortraitPanel()
        {
            _portraitPanel.Visible = true;
        }

        /// <summary>立ち絵詳細パネルの表示/非表示を切り替える(タイルクリックで呼ばれる)。</summary>
        private void TogglePortraitPanel()
        {
            _portraitPanel.Visible = !_portraitPanel.Visible;
        }

        /// <summary>
        /// ServiceManager からの状態変化通知(別スレッド発火の可能性あり)を受けて、
        /// 該当フォルダのタイルの表示をUIスレッドで更新する。
        /// </summary>
        private void OnServiceStateChanged(string folder)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)delegate
                {
                    ToolTile tile;
                    if (_tilesByFolder.TryGetValue(folder, out tile) && !tile.IsDisposed)
                        tile.RefreshServiceState();
                });
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        /// <summary>
        /// PortraitWatcher からの監視状態変化通知(別スレッド発火の可能性あり)を受けて、
        /// 立ち絵タイルの表示をUIスレッドで更新する。
        /// </summary>
        private void OnWatchStateChanged()
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)delegate
                {
                    if (_portraitTile != null && !_portraitTile.IsDisposed)
                        _portraitTile.RefreshWatchState();
                });
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
    }
}
