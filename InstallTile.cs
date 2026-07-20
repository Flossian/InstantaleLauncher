using System;
using System.Windows.Forms;

namespace InstantaleLauncher
{
    /// <summary>
    /// 未導入の既知ツール用タイル。ボタン押下で最新リリースを取得して tools\ へ導入する。
    /// 導入処理自体は MainForm(onInstall)側で非同期に行い、成否に応じて再スキャン/再有効化される。
    /// </summary>
    public sealed class InstallTile : TileBase
    {
        private readonly CatalogEntry _entry;

        /// <summary>入手ボタン付きのタイルを構築する。バッジ/アイコンは想定種別で表示する。</summary>
        public InstallTile(CatalogEntry entry, Action<CatalogEntry, InstallTile> onInstall)
            : base(entry.Folder,
                   Theme.LoadToolIcon(entry.Folder, entry.Kind),
                   entry.Kind.ToString().ToUpperInvariant(),
                   Theme.BadgeColor(entry.Kind))
        {
            _entry = entry;
            StateDot.Visible = false;

            ActionButton.Text = Lang.T("Install.Get");
            ActionButton.Click += delegate { onInstall(_entry, this); };
            SetTooltip(Lang.F("Install.Tooltip", _entry.Owner + "/" + _entry.Repo));
        }

        /// <summary>取得開始時: ボタンを無効化して「取得中…」表示にする(UIスレッドで呼ぶこと)。</summary>
        public void BeginInstall()
        {
            ActionButton.Enabled = false;
            ActionButton.Text = Lang.T("Install.Downloading");
        }

        /// <summary>取得失敗時: ボタンを再び有効化して「入手」表示へ戻す(UIスレッドで呼ぶこと)。</summary>
        public void EndInstall()
        {
            ActionButton.Enabled = true;
            ActionButton.Text = Lang.T("Install.Get");
        }
    }
}
