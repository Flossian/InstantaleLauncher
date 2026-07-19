using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace InstantaleLauncher
{
    /// <summary>
    /// ゲームフォルダのパスを直接入力するダイアログ。
    /// instantale.exe が存在するフォルダのみ受け付ける(exe 自体のパスや引用符付きも許容)。
    /// </summary>
    public sealed class GameDirDialog : Form
    {
        private readonly TextBox _pathBox;
        private readonly Label _errorLabel;

        /// <summary>OK で確定したゲームフォルダ。</summary>
        public string SelectedDir { get; private set; }

        /// <summary>initialDir をテキストボックスの初期値として表示するダイアログを構築する。</summary>
        public GameDirDialog(string initialDir)
        {
            Text = Lang.T("Game.SelectDirTip");
            Font = SystemFonts.MessageBoxFont;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 150);

            var prompt = new Label
            {
                Text = Lang.T("Game.SelectDir"),
                AutoSize = false,
                Bounds = new Rectangle(16, 14, ClientSize.Width - 32, 34),
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
            };
            Controls.Add(prompt);

            _pathBox = new TextBox
            {
                Bounds = new Rectangle(16, 52, ClientSize.Width - 32, 24),
                BackColor = Theme.Panel,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Text = initialDir ?? "",
            };
            Controls.Add(_pathBox);

            _errorLabel = new Label
            {
                AutoSize = false,
                Bounds = new Rectangle(16, 80, ClientSize.Width - 32, 18),
                ForeColor = Theme.Warning,
                BackColor = Color.Transparent,
                Visible = false,
            };
            Controls.Add(_errorLabel);

            var okButton = new Button
            {
                Text = Lang.T("Common.OK"),
                Bounds = new Rectangle(ClientSize.Width - 208, 106, 90, 30),
            };
            Theme.StylePrimaryButton(okButton);
            okButton.Click += OnOk;
            Controls.Add(okButton);

            var cancelButton = new Button
            {
                Text = Lang.T("Common.Cancel"),
                Bounds = new Rectangle(ClientSize.Width - 106, 106, 90, 30),
                DialogResult = DialogResult.Cancel,
            };
            Theme.StyleButton(cancelButton);
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;

            Shown += delegate
            {
                _pathBox.SelectAll();
                _pathBox.Focus();
            };
        }

        /// <summary>入力されたパスを検証し、instantale.exe が見つかれば SelectedDir を確定して閉じる。</summary>
        private void OnOk(object sender, EventArgs e)
        {
            // 引用符付きコピー("C:\...")や exe 自体のパスもフォルダとして解釈する
            var path = _pathBox.Text.Trim().Trim('"');
            try
            {
                if (path.EndsWith("instantale.exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                    path = Path.GetDirectoryName(path);
                if (path != null && File.Exists(Path.Combine(path, "instantale.exe")))
                {
                    SelectedDir = Path.GetFullPath(path);
                    DialogResult = DialogResult.OK;
                    return;
                }
            }
            catch
            {
                // 不正なパス文字などはエラー表示に落とす
            }
            _errorLabel.Text = Lang.T("Game.ExeMissing");
            _errorLabel.Visible = true;
        }
    }
}
