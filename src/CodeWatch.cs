using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Kotik
{
    /// Watches the folders open in VS Code. When a C/C++/Python file is saved, it is handed to Lint (on a worker thread).
    /// Off by default. Reads only your source files on this computer; nothing is sent anywhere.
    class CodeWatch : IDisposable
    {
        static readonly string[] Exts = { ".c", ".cc", ".cpp", ".cxx", ".h", ".hpp", ".py" };
        readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
        readonly Dictionary<string, DateTime> due = new Dictionary<string, DateTime>();
        readonly Timer tick = new Timer { Interval = 700 };
        readonly HashSet<string> folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DateTime nextRefresh;
        public event Action<string> Saved = delegate { };

        public CodeWatch()
        {
            tick.Tick += delegate { Pump(); };
            tick.Start();
            Refresh();
        }

        /// Folders recently opened in VS Code, from its own workspace storage.
        static List<string> RecentFolders()
        {
            var list = new List<KeyValuePair<DateTime, string>>();
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Code\User\workspaceStorage");
            if (!Directory.Exists(root)) return new List<string>();
            foreach (var dir in Directory.GetDirectories(root))
            {
                string ws = Path.Combine(dir, "workspace.json");
                if (!File.Exists(ws)) continue;
                try
                {
                    var m = Regex.Match(File.ReadAllText(ws), "\"folder\"\\s*:\\s*\"file:///([^\"]+)\"");
                    if (!m.Success) continue;
                    string path = Uri.UnescapeDataString(m.Groups[1].Value).Replace('/', '\\');
                    if (Directory.Exists(path)) list.Add(new KeyValuePair<DateTime, string>(Directory.GetLastWriteTime(dir), path));
                }
                catch { }
            }
            list.Sort((a, b) => b.Key.CompareTo(a.Key));
            var res = new List<string>();
            foreach (var kv in list) if (res.Count < 12 && !res.Contains(kv.Value)) res.Add(kv.Value);
            return res;
        }

        void Refresh()
        {
            nextRefresh = DateTime.Now.AddMinutes(5);
            foreach (var f in RecentFolders())
            {
                if (folders.Contains(f)) continue;
                try
                {
                    var w = new FileSystemWatcher(f) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };
                    FileSystemEventHandler h = (s, e) => Touch(e.FullPath);
                    w.Changed += h;
                    w.Created += h;
                    w.Renamed += (s, e) => Touch(e.FullPath);
                    w.EnableRaisingEvents = true;
                    watchers.Add(w);
                    folders.Add(f);
                }
                catch { }
            }
        }

        void Touch(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (Array.IndexOf(Exts, ext) < 0) return;
            string low = path.ToLowerInvariant();
            if (low.Contains(@"\.git\") || low.Contains(@"\node_modules\") || low.Contains(@"\build\") || low.Contains(@"\.godot\") || low.Contains(@"\venv\") || low.Contains(@"\site-packages\")) return;
            lock (due) due[path] = DateTime.Now.AddSeconds(1.5);    // wait until the editor finished writing
        }

        void Pump()
        {
            if (DateTime.Now > nextRefresh) Refresh();
            var ready = new List<string>();
            lock (due)
            {
                foreach (var kv in due) if (DateTime.Now >= kv.Value) ready.Add(kv.Key);
                foreach (var p in ready) due.Remove(p);
            }
            foreach (var p in ready) Saved(p);
        }

        public void Dispose()
        {
            tick.Dispose();
            foreach (var w in watchers) w.Dispose();
        }

        // ---------------- showing the place ----------------

        /// Asks VS Code to put the cursor on that line (the same as "code -g file:line" in a terminal).
        public static void Reveal(string file, int line)
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] tries =
            {
                Path.Combine(local, @"Programs\Microsoft VS Code\bin\code.cmd"),
                @"C:\Program Files\Microsoft VS Code\bin\code.cmd",
            };
            string exe = "code.cmd";
            foreach (var t in tries) if (File.Exists(t)) { exe = t; break; }
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe", "/c \"\"" + exe + "\" -r -g \"" + file + ":" + line + "\"\"")
                    { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
            }
            catch { }
        }

        /// Finds the highlighted current line in the VS Code editor (a ~20 px band across the editor) — it is where the
        /// cursor went. `before` is the scan from before the jump, so a band that was already there is ignored.
        public static PointF? CursorLine(Rectangle win, List<Plat> before, List<Plat> after)
        {
            PointF? best = null;
            foreach (var a in after)
            {
                if (a.R.Left < win.Left + 120) continue;                // the explorer on the left has its own bands
                foreach (var b in after)
                {
                    int gap = b.R.Top - a.R.Top;
                    if (gap < 14 || gap > 30) continue;
                    int left = Math.Max(a.R.Left, b.R.Left), right = Math.Min(a.R.Right, b.R.Right);
                    if (right - left < Math.Min(a.R.Width, b.R.Width) * 0.6) continue;
                    bool old = before.Exists(p => Math.Abs(p.R.Top - a.R.Top) < 3);
                    var c = new PointF(left, (a.R.Top + b.R.Top) / 2f);
                    if (!old) return c;
                    if (best == null) best = c;
                }
            }
            return best;
        }
    }
}
