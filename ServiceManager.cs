using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace InstantaleLauncher
{
    /// <summary>
    /// 常駐サービス(SVC)の起動/停止/状態管理。
    /// ランチャー終了時はプロセスを終了させず残す(デタッチ)。
    /// 再起動時は Attach で同一 exe パスの既存プロセスを拾い直して管理を引き継ぐ。
    /// </summary>
    public sealed class ServiceManager : IDisposable
    {
        private readonly Dictionary<string, Process> _procs =
            new Dictionary<string, Process>(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new object();

        /// <summary>状態が変化したツールフォルダのパスを通知する(任意スレッドから発火)。</summary>
        public event Action<string> StateChanged;

        /// <summary>指定フォルダのサービスが管理下で稼働中かどうか。</summary>
        public bool IsRunning(string folder)
        {
            int pid;
            return TryGetPid(folder, out pid);
        }

        /// <summary>稼働中なら PID を取得する。管理下に無い/既に終了済みなら false。</summary>
        public bool TryGetPid(string folder, out int pid)
        {
            pid = 0;
            lock (_sync)
            {
                Process p;
                if (!_procs.TryGetValue(folder, out p))
                    return false;
                try
                {
                    if (p.HasExited) return false;
                    pid = p.Id;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>プロセスを起動して管理下に登録する。既に稼働中なら何もしない。</summary>
        public void Start(ToolInfo tool)
        {
            lock (_sync)
            {
                if (IsRunningUnlocked(tool.Folder)) return;

                var psi = new ProcessStartInfo
                {
                    FileName = tool.EntryPath,
                    WorkingDirectory = tool.Folder,
                    UseShellExecute = true,
                };
                var p = Process.Start(psi);
                if (p == null)
                    throw new InvalidOperationException("Process.Start returned null: " + tool.EntryPath);

                RegisterUnlocked(tool.Folder, p);
            }
            RaiseStateChanged(tool.Folder);
        }

        /// <summary>
        /// 前回のランチャー(または手動)で起動された同一 exe の既存プロセスを探して管理下に置く。
        /// 見つからなければ何もしない。
        /// </summary>
        public void Attach(ToolInfo tool)
        {
            lock (_sync)
            {
                if (IsRunningUnlocked(tool.Folder)) return;
            }

            string exePath;
            try { exePath = Path.GetFullPath(tool.EntryPath); }
            catch { return; }

            Process found = null;
            foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exePath)))
            {
                if (found == null && IsSameExe(p, exePath))
                {
                    found = p;
                    continue;
                }
                p.Dispose();
            }
            if (found == null) return;

            bool registered;
            lock (_sync)
            {
                if (IsRunningUnlocked(tool.Folder))
                {
                    found.Dispose();
                    return;
                }
                registered = RegisterUnlocked(tool.Folder, found);
            }
            if (registered)
                RaiseStateChanged(tool.Folder);
        }

        /// <summary>停止。CloseMainWindow → 猶予後 Kill。UIを固めないよう非同期で行う。</summary>
        public void Stop(string folder)
        {
            Process p;
            lock (_sync)
            {
                if (!_procs.TryGetValue(folder, out p))
                    return;
            }
            Task.Run(() => StopProcess(p));
        }

        /// <summary>管理を手放すだけでプロセスは残す。ランチャー終了時に呼ぶ。</summary>
        public void Dispose()
        {
            List<Process> procs;
            lock (_sync)
            {
                procs = new List<Process>(_procs.Values);
                _procs.Clear();
            }
            foreach (var p in procs)
            {
                try
                {
                    p.EnableRaisingEvents = false;
                    p.Dispose();
                }
                catch { }
            }
        }

        /// <summary>_sync を保持した状態で呼ぶこと。通知は呼び出し側でロック外から行う。</summary>
        private bool RegisterUnlocked(string folder, Process p)
        {
            try
            {
                p.EnableRaisingEvents = true;
            }
            catch
            {
                // 既に終了している等
                p.Dispose();
                return false;
            }
            p.Exited += (s, e) =>
            {
                lock (_sync)
                {
                    Process cur;
                    if (_procs.TryGetValue(folder, out cur) && ReferenceEquals(cur, p))
                        _procs.Remove(folder);
                }
                RaiseStateChanged(folder);
            };
            _procs[folder] = p;
            return true;
        }

        /// <summary>StateChanged 購読者へ通知する。</summary>
        private void RaiseStateChanged(string folder)
        {
            var h = StateChanged;
            if (h != null) h(folder);
        }

        /// <summary>プロセス p の実行ファイルパスが exePath と一致するか(Attach 時の同一プロセス判定)。</summary>
        private static bool IsSameExe(Process p, string exePath)
        {
            try
            {
                return string.Equals(Path.GetFullPath(p.MainModule.FileName), exePath,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // アクセス拒否・ビット数違い・終了済み等は対象外
                return false;
            }
        }

        /// <summary>_sync を保持した状態で呼ぶこと。IsRunning のロック無し版。</summary>
        private bool IsRunningUnlocked(string folder)
        {
            Process p;
            if (!_procs.TryGetValue(folder, out p)) return false;
            try { return !p.HasExited; }
            catch { return false; }
        }

        /// <summary>穏やかな終了(CloseMainWindow)を試み、猶予後も残っていれば Kill する。</summary>
        private static void StopProcess(Process p)
        {
            try
            {
                if (p.HasExited) return;
                p.CloseMainWindow();
                if (!p.WaitForExit(3000))
                {
                    p.Kill();
                    p.WaitForExit(2000);
                }
            }
            catch
            {
                // 既に終了している等。状態は Exited イベント側で整合が取れる
            }
        }
    }
}
