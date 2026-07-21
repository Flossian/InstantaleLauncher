using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace InstantaleLauncher
{
    /// <summary>
    /// 立ち絵高画質化の詳細パネル(メインウィンドウ下部にドッキング、開閉式)。
    /// 閉じても監視は継続する(監視の実体は PortraitWatcher が MainForm 側で保持)。
    /// </summary>
    public sealed class PortraitPanel : Panel
    {
        private readonly PortraitWatcher _watcher;

        private readonly Button _watchButton;
        private readonly Label _watchStateLabel;
        private readonly CheckBox _autoStartCheck;
        private readonly Button _applyButton;
        private readonly Button _revertButton;
        private readonly ProgressBar _progress;
        private readonly Label _resultLabel;
        private readonly Label _noticeLabel;
        private readonly ListBox _logList;

        private bool _busy;

        /// <summary>watcher に対する操作パネル(監視トグル・一括適用/復元・ログ表示)一式を構築する。</summary>
        public PortraitPanel(PortraitWatcher watcher)
        {
            _watcher = watcher;

            // アンカー基準を確定させるため、コントロール配置前にサイズを決める
            Size = new Size(860, 264);
            Dock = DockStyle.Bottom;
            BackColor = Theme.Panel;
            Visible = false;

            int x = 16;

            var title = new Label
            {
                Text = Lang.T("Portrait.Title"),
                AutoSize = true,
                Location = new Point(x, 12),
                Font = Theme.UiFont(9.5f, FontStyle.Bold),
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
            };
            Controls.Add(title);

            var closeButton = new Button
            {
                Text = "✕",
                Bounds = new Rectangle(Width - 44, 8, 28, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            Theme.StyleButton(closeButton);
            closeButton.Font = Theme.SymbolFont(9f);   // ✕ グリフの被覆をロケールに依らず確保
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += delegate { Visible = false; };
            Controls.Add(closeButton);

            _watchButton = new Button { Bounds = new Rectangle(x, 40, 120, 30) };
            Theme.StyleAccentButton(_watchButton);
            _watchButton.Click += OnWatchToggle;
            Controls.Add(_watchButton);

            _watchStateLabel = new Label
            {
                AutoSize = false,
                Bounds = new Rectangle(x + 128, 40, 150, 30),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
            };
            Controls.Add(_watchStateLabel);

            _autoStartCheck = new CheckBox
            {
                Text = Lang.T("Portrait.AutoStart"),
                AutoSize = true,
                Location = new Point(x + 286, 46),
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Checked = Settings.PortraitAutoStart,
            };
            _autoStartCheck.CheckedChanged += delegate
            {
                Settings.PortraitAutoStart = _autoStartCheck.Checked;
                Settings.Save();
            };
            Controls.Add(_autoStartCheck);

            _applyButton = new Button
            {
                Bounds = new Rectangle(x, 78, 120, 30),
                Text = Lang.T("Portrait.Apply"),
            };
            Theme.StyleButton(_applyButton);
            _applyButton.Click += delegate { RunBatch(true); };
            Controls.Add(_applyButton);

            _revertButton = new Button
            {
                Bounds = new Rectangle(x + 128, 78, 120, 30),
                Text = Lang.T("Portrait.Revert"),
            };
            Theme.StyleButton(_revertButton);
            _revertButton.Click += delegate { RunBatch(false); };
            Controls.Add(_revertButton);

            _progress = new ProgressBar
            {
                Bounds = new Rectangle(x + 256, 81, 110, 24),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Visible = false,
            };
            Controls.Add(_progress);

            _resultLabel = new Label
            {
                AutoSize = false,
                Bounds = new Rectangle(x + 374, 78, Width - (x + 374) - 16, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Theme.Running,
                BackColor = Color.Transparent,
            };
            Controls.Add(_resultLabel);

            var pathLabel = new Label
            {
                Text = Lang.F("Portrait.TargetFolder", _watcher.RootPath),
                AutoSize = false,
                AutoEllipsis = true,
                Bounds = new Rectangle(x, 114, Width - x - 16, 16),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
            };
            Controls.Add(pathLabel);

            _noticeLabel = new Label
            {
                Text = Lang.T("Portrait.RootMissing"),
                AutoSize = false,
                Bounds = new Rectangle(x, 132, Width - x - 16, 16),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Theme.Warning,
                BackColor = Color.Transparent,
                Visible = false,
            };
            Controls.Add(_noticeLabel);

            _logList = new ListBox
            {
                Bounds = new Rectangle(x, 152, Width - x - 16, Height - 152 - 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                BackColor = Theme.Background,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false,
                SelectionMode = SelectionMode.One,
            };
            Controls.Add(_logList);

            foreach (var line in _watcher.SubscribeLog(OnLogAdded))
                _logList.Items.Add(line);
            ScrollLogToEnd();

            _watcher.WatchStateChanged += OnWatchStateChanged;
            Disposed += delegate
            {
                _watcher.UnsubscribeLog(OnLogAdded);
                _watcher.WatchStateChanged -= OnWatchStateChanged;
            };

            UpdateWatchUi();
        }

        /// <summary>メインタイル一覧との境界線を上端に1本描画する。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Theme.TileBorder))
                e.Graphics.DrawLine(pen, 0, 0, Width, 0);
        }

        /// <summary>パネルを開いた際に、監視状態とログ末尾表示を最新化する。</summary>
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                UpdateWatchUi();
                ScrollLogToEnd();
            }
        }

        /// <summary>監視ボタン押下時のON/OFF切り替え。</summary>
        private void OnWatchToggle(object sender, EventArgs e)
        {
            if (_watcher.IsWatching)
            {
                _watcher.StopWatch();
            }
            else if (!_watcher.StartWatch())
            {
                UpdateWatchUi();   // ルート不在の案内を表示
            }
        }

        /// <summary>一括適用/復元をバックグラウンドスレッドで実行し、完了後にUIスレッドで結果を表示する。</summary>
        private void RunBatch(bool apply)
        {
            if (_busy) return;
            if (!_watcher.RootExists)
            {
                UpdateWatchUi();
                return;
            }
            _busy = true;
            _applyButton.Enabled = false;
            _revertButton.Enabled = false;
            _resultLabel.Text = "";
            _progress.Visible = true;

            Task.Run(delegate { return apply ? _watcher.ApplyAll() : _watcher.RevertAll(); })
                .ContinueWith(t =>
                {
                    if (IsDisposed) return;
                    BeginInvoke((Action)delegate
                    {
                        _progress.Visible = false;
                        _applyButton.Enabled = true;
                        _revertButton.Enabled = true;
                        _busy = false;
                        if (t.IsFaulted)
                        {
                            _resultLabel.ForeColor = Theme.Warning;
                            _resultLabel.Text = t.Exception.GetBaseException().Message;
                        }
                        else
                        {
                            _resultLabel.ForeColor = Theme.Running;
                            _resultLabel.Text = Lang.F(apply ? "Portrait.AppliedCount" : "Portrait.RevertedCount", t.Result);
                        }
                    });
                });
        }

        /// <summary>watcher からのログ追加通知(別スレッド発火の可能性あり)をUIスレッドでリストに反映する。</summary>
        private void OnLogAdded(string line)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)delegate
                {
                    _logList.Items.Add(line);
                    while (_logList.Items.Count > PortraitWatcher.LogCapacity)
                        _logList.Items.RemoveAt(0);
                    ScrollLogToEnd();
                });
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        /// <summary>watcher からの監視状態変化通知(別スレッド発火の可能性あり)をUIスレッドで反映する。</summary>
        private void OnWatchStateChanged()
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)UpdateWatchUi);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        /// <summary>監視ボタンの文言・状態ラベル・ルート不在の注意書きを現在の監視状態に合わせて更新する。</summary>
        private void UpdateWatchUi()
        {
            bool watching = _watcher.IsWatching;
            _watchButton.Text = Lang.T("Portrait.Watch") + (watching ? " OFF" : " ON");
            _watchStateLabel.Text = "● " + Lang.T(watching ? "Portrait.Watching" : "Portrait.NotWatching");
            _watchStateLabel.ForeColor = watching ? Theme.Running : Theme.Stopped;
            _noticeLabel.Visible = !_watcher.RootExists;
        }

        /// <summary>ログリストの表示位置を最終行までスクロールする。</summary>
        private void ScrollLogToEnd()
        {
            if (_logList.Items.Count > 0)
                _logList.TopIndex = _logList.Items.Count - 1;
        }
    }
}
