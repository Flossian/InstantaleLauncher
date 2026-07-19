using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace InstantaleLauncher
{
    /// <summary>
    /// 立ち絵高画質化エンジン(hd_portrait.ps1 の移植)。
    /// no_bg_image.png(高画質版)で reduced_color_image.png(ゲーム表示用)を上書きし、
    /// 元のドット絵版は reduced_color_image.orig.png へ退避する。
    /// </summary>
    public sealed class PortraitWatcher : IDisposable
    {
        public const string NoBgName = "no_bg_image.png";
        public const string ReducedName = "reduced_color_image.png";
        public const string BackupName = "reduced_color_image.orig.png";

        /// <summary>保持するログの最大行数(表示側のトリミングも同じ値を使う)。</summary>
        public const int LogCapacity = 500;

        private const int RetryCount = 5;
        private const int RetryIntervalMs = 150;
        private const int DebounceMs = 100;
        private const int SweepIntervalMs = 5000;

        private readonly object _sync = new object();
        private readonly List<string> _logLines = new List<string>();

        private FileSystemWatcher _fsw;
        private Timer _sweepTimer;
        private DateTime _watchStartUtc;
        private int _sweepBusy;

        public string RootPath { get; private set; }
        public bool IsWatching { get; private set; }

        // ログ通知は SubscribeLog/UnsubscribeLog 経由でのみ購読させる
        // (購読とスナップショット取得を同一ロックで行い、取りこぼし・重複を防ぐため)
        private event Action<string> LogAdded;
        /// <summary>監視ON/OFFの変化を通知する(任意スレッドから発火)。</summary>
        public event Action WatchStateChanged;

        /// <summary>overridePath が空なら既定ルート(%LOCALAPPDATA%\Darmabeko\Instantale\worlds)を使う。</summary>
        public PortraitWatcher(string overridePath)
        {
            RootPath = string.IsNullOrWhiteSpace(overridePath)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Darmabeko", "Instantale", "worlds")
                : overridePath;
        }

        /// <summary>監視/変換対象ルートフォルダが実在するか。</summary>
        public bool RootExists
        {
            get { return Directory.Exists(RootPath); }
        }

        /// <summary>
        /// ログ購読を開始し、既存ログのスナップショットを返す。
        /// 購読とスナップショット取得を同一ロック内で行うため、間に追加された行の取りこぼしや重複が起きない。
        /// </summary>
        public string[] SubscribeLog(Action<string> handler)
        {
            lock (_sync)
            {
                LogAdded += handler;
                return _logLines.ToArray();
            }
        }

        /// <summary>ログ購読を解除する。</summary>
        public void UnsubscribeLog(Action<string> handler)
        {
            lock (_sync)
                LogAdded -= handler;
        }

        /// <summary>FileSystemWatcher と定期スイープタイマーを起動する。ルート不在なら失敗して false。</summary>
        public bool StartWatch()
        {
            if (IsWatching) return true;
            if (!RootExists) return false;

            _watchStartUtc = DateTime.UtcNow;

            _fsw = new FileSystemWatcher(RootPath, ReducedName)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            _fsw.Created += OnFsEvent;
            _fsw.Changed += OnFsEvent;
            _fsw.Renamed += OnFsRenamed;
            _fsw.EnableRaisingEvents = true;

            // イベント取りこぼし防止の定期スイープ(監視開始以降に更新されたファイルのみ)
            _sweepTimer = new Timer(OnSweepTick, null, SweepIntervalMs, SweepIntervalMs);

            IsWatching = true;
            AddLog(Lang.T("Portrait.LogWatchStart"));
            RaiseWatchStateChanged();
            return true;
        }

        /// <summary>監視を停止する(既に停止中なら何もしない)。</summary>
        public void StopWatch()
        {
            if (!IsWatching) return;
            DisposeWatchObjects();
            IsWatching = false;
            AddLog(Lang.T("Portrait.LogWatchStop"));
            RaiseWatchStateChanged();
        }

        /// <summary>全対象を1周して変換する。戻り値は変換件数。</summary>
        public int ApplyAll()
        {
            int count = 0;
            foreach (var dir in EnumerateDirsWith(ReducedName))
            {
                if (ConvertFolder(dir))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// .orig.png を持つ全フォルダで reduced_color へコピーバック。.orig は削除しない。
        /// 監視中はコピーバックが Changed イベントを発火して即座に再適用されてしまうため、先に監視を止める。
        /// </summary>
        public int RevertAll()
        {
            if (IsWatching)
                StopWatch();

            int count = 0;
            foreach (var dir in EnumerateDirsWith(BackupName))
            {
                try
                {
                    var backup = Path.Combine(dir, BackupName);
                    var reduced = Path.Combine(dir, ReducedName);
                    File.Copy(backup, reduced, true);
                    count++;
                    AddLog(Lang.F("Portrait.LogReverted", Path.GetFileName(dir)));
                }
                catch
                {
                    // ロック等で戻せなかったフォルダはスキップ
                }
            }
            return count;
        }

        /// <summary>監視オブジェクトを解放する。ファイル変換自体は行わない(適用結果は残したまま終了する)。</summary>
        public void Dispose()
        {
            DisposeWatchObjects();
            IsWatching = false;
        }

        // ---- 変換コア ----

        /// <summary>
        /// 1フォルダを変換する。no_bg と reduced_color が両方ある場合のみ対象。
        /// 適用済み判定はサイズ+LastWriteTimeUtc 一致(File.Copy は更新日時を保持するため成立し、
        /// 監視の自己トリガーも防げる。速度優先のためハッシュは使わない)。
        /// </summary>
        private bool ConvertFolder(string dir)
        {
            for (int attempt = 0; attempt < RetryCount; attempt++)
            {
                try
                {
                    var noBg = new FileInfo(Path.Combine(dir, NoBgName));
                    var reduced = new FileInfo(Path.Combine(dir, ReducedName));
                    if (!noBg.Exists || !reduced.Exists)
                        return false;
                    if (reduced.Length == noBg.Length && reduced.LastWriteTimeUtc == noBg.LastWriteTimeUtc)
                        return false;   // 適用済み

                    var backup = Path.Combine(dir, BackupName);
                    if (!File.Exists(backup))
                        File.Copy(reduced.FullName, backup, false);   // 元のドット絵を初回だけ退避
                    File.Copy(noBg.FullName, reduced.FullName, true);

                    AddLog(Lang.F("Portrait.LogConverted", Path.GetFileName(dir)));
                    return true;
                }
                catch (IOException)
                {
                    Thread.Sleep(RetryIntervalMs);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(RetryIntervalMs);
                }
            }
            AddLog(Lang.F("Portrait.LogLocked", Path.GetFileName(dir)));
            return false;
        }

        /// <summary>RootPath 配下を再帰的に走査し、fileName を持つフォルダのパスを列挙する。走査エラーは無視して打ち切る。</summary>
        private IEnumerable<string> EnumerateDirsWith(string fileName)
        {
            if (!RootExists) yield break;
            IEnumerator<string> e;
            try
            {
                e = Directory.EnumerateFiles(RootPath, fileName, SearchOption.AllDirectories).GetEnumerator();
            }
            catch
            {
                yield break;
            }
            using (e)
            {
                while (true)
                {
                    try
                    {
                        if (!e.MoveNext()) break;
                    }
                    catch
                    {
                        break;
                    }
                    yield return Path.GetDirectoryName(e.Current);
                }
            }
        }

        // ---- 監視 ----

        /// <summary>FileSystemWatcher の Created/Changed イベントハンドラ。</summary>
        private void OnFsEvent(object sender, FileSystemEventArgs e)
        {
            ScheduleConvert(e.FullPath);
        }

        /// <summary>FileSystemWatcher の Renamed イベントハンドラ。</summary>
        private void OnFsRenamed(object sender, RenamedEventArgs e)
        {
            ScheduleConvert(e.FullPath);
        }

        /// <summary>reduced_color_image.png の変化を検知した際、少し待ってから変換を試みる(書き込み完了待ち)。</summary>
        private void ScheduleConvert(string fullPath)
        {
            if (!string.Equals(Path.GetFileName(fullPath), ReducedName, StringComparison.OrdinalIgnoreCase))
                return;
            var dir = Path.GetDirectoryName(fullPath);
            // 書き込み完了を少し待ってから変換(待機中に監視が止められたら何もしない)
            Task.Delay(DebounceMs).ContinueWith(t =>
            {
                if (!IsWatching) return;
                try { ConvertFolder(dir); }
                catch { }
            });
        }

        /// <summary>定期タイマーで全対象フォルダを再チェックし、監視イベントを取りこぼした分を拾う。</summary>
        private void OnSweepTick(object state)
        {
            if (!IsWatching) return;
            // ロック待ちリトライで1周が周期を超えた場合の多重実行を防ぐ
            if (Interlocked.Exchange(ref _sweepBusy, 1) != 0) return;
            try
            {
                foreach (var dir in EnumerateDirsWith(ReducedName))
                {
                    try
                    {
                        var reduced = new FileInfo(Path.Combine(dir, ReducedName));
                        // 監視開始後に更新されたファイルのみ対象(既存には触れない)
                        if (!reduced.Exists || reduced.LastWriteTimeUtc < _watchStartUtc)
                            continue;
                        ConvertFolder(dir);
                    }
                    catch { }
                }
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref _sweepBusy, 0);
            }
        }

        /// <summary>FileSystemWatcher とスイープタイマーを安全に破棄する。</summary>
        private void DisposeWatchObjects()
        {
            var fsw = _fsw;
            _fsw = null;
            if (fsw != null)
            {
                try
                {
                    fsw.EnableRaisingEvents = false;
                    fsw.Dispose();
                }
                catch { }
            }
            var timer = _sweepTimer;
            _sweepTimer = null;
            if (timer != null)
                timer.Dispose();
        }

        // ---- ログ ----

        /// <summary>タイムスタンプ付きでログ1行を追加し、容量超過分は古い方から切り捨てて購読者に通知する。</summary>
        private void AddLog(string message)
        {
            var line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message;
            Action<string> h;
            lock (_sync)
            {
                _logLines.Add(line);
                if (_logLines.Count > LogCapacity)
                    _logLines.RemoveRange(0, _logLines.Count - LogCapacity);
                // SubscribeLog と同一ロックで購読者を確定させる(スナップショットとの取りこぼし・重複防止)
                h = LogAdded;
            }
            if (h != null) h(line);
        }

        /// <summary>WatchStateChanged 購読者へ通知する。</summary>
        private void RaiseWatchStateChanged()
        {
            var h = WatchStateChanged;
            if (h != null) h();
        }
    }
}
