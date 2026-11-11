using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace Kotik
{
    /// A window the cat can stand on (H is IntPtr.Zero for the taskbar line).
    class Plat
    {
        public IntPtr H;
        public Rectangle R;
        public bool Shelf;      // a colour border inside a program, not a window edge
    }

    /// Windows API: per-pixel transparent windows, the list of windows the cat can stand on, idle time, memory, battery.
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);

        /// Puts a 32-bit ARGB bitmap on the screen at (x, y): transparent pixels show what is behind and let clicks through.
        public static void ShowBitmap(IntPtr hwnd, Bitmap bmp, int x, int y)
        {
            IntPtr screen = GetDC(IntPtr.Zero);
            IntPtr mem = CreateCompatibleDC(screen);
            IntPtr hbmp = bmp.GetHbitmap(Color.FromArgb(0));
            IntPtr old = SelectObject(mem, hbmp);
            var size = new SIZE { cx = bmp.Width, cy = bmp.Height };
            var src = new POINT();
            var dst = new POINT { X = x, Y = y };
            var blend = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
            UpdateLayeredWindow(hwnd, screen, ref dst, ref size, mem, ref src, 0, ref blend, 2);
            SelectObject(mem, old);
            DeleteObject(hbmp);
            DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, screen);
        }

        delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextLength(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder s, int max);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT r, int size);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int v, int size);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

        /// Ordinary visible windows, topmost first. The cat walks on their top edges.
        public static List<Plat> Windows(IntPtr self)
        {
            var list = new List<Plat>();
            EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                if (h == self || !IsWindowVisible(h) || IsIconic(h) || GetWindowTextLength(h) == 0) return true;
                int ex = GetWindowLong(h, -20);
                if ((ex & 0x80) != 0) return true;                // tool windows
                int cloaked;
                if (DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true;   // hidden store apps
                var cls = new StringBuilder(64);
                GetClassName(h, cls, 64);
                string c = cls.ToString();
                if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd") return true;
                RECT r;
                if (DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf(typeof(RECT))) != 0) GetWindowRect(h, out r);  // the visible frame
                if (r.Right - r.Left < 160 || r.Bottom - r.Top < 80) return true;
                list.Add(new Plat { H = h, R = new Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top) });
                return true;
            }, IntPtr.Zero);
            return list;
        }

        /// The visible frame of a window (without the invisible resize border of Windows 10).
        public static bool WindowRect(IntPtr h, out Rectangle rect)
        {
            RECT r;
            rect = Rectangle.Empty;
            if (h == IntPtr.Zero || IsIconic(h)) return false;
            if (DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf(typeof(RECT))) != 0 && !GetWindowRect(h, out r)) return false;
            var cls = new StringBuilder(64);
            GetClassName(h, cls, 64);
            string c = cls.ToString();
            if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd") return false;
            rect = new Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            return true;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int max);

        [DllImport("psapi.dll")] static extern bool EmptyWorkingSet(IntPtr h);

        /// Hands unused memory back to Windows (the cat keeps only what it needs right now).
        public static void TrimMemory()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try { EmptyWorkingSet(System.Diagnostics.Process.GetCurrentProcess().Handle); } catch { }
        }

        public static string ForegroundTitle()
        {
            var sb = new StringBuilder(512);
            GetWindowText(GetForegroundWindow(), sb, 512);
            return sb.ToString();
        }

        public static string ForegroundProcess()
        {
            try
            {
                uint pid;
                GetWindowThreadProcessId(GetForegroundWindow(), out pid);
                return System.Diagnostics.Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant();
            }
            catch { return ""; }
        }

        public static bool ForegroundIsDesktop()
        {
            var cls = new StringBuilder(64);
            GetClassName(GetForegroundWindow(), cls, 64);
            string c = cls.ToString();
            return c == "Progman" || c == "WorkerW";
        }

        /// The title of any window (shown or hidden in the tray) of the named program that contains `part`, or "".
        public static string TitleOf(string process, string part)
        {
            var ids = new System.Collections.Generic.HashSet<uint>();
            try { foreach (var p in System.Diagnostics.Process.GetProcessesByName(process)) { ids.Add((uint)p.Id); p.Dispose(); } } catch { }
            if (ids.Count == 0) return "";
            string found = "";
            EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (!ids.Contains(pid)) return true;
                var sb = new StringBuilder(256);
                GetWindowText(h, sb, 256);
                if (sb.ToString().Contains(part)) { found = sb.ToString(); return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        /// True when the foreground window covers a whole screen (a game or a video): the cat hides then.
        public static bool ForegroundIsFullscreen()
        {
            IntPtr h = GetForegroundWindow();
            var cls = new StringBuilder(64);
            GetClassName(h, cls, 64);
            string c = cls.ToString();
            if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd") return false;
            RECT r;
            if (!GetWindowRect(h, out r)) return false;
            var s = System.Windows.Forms.Screen.FromHandle(h).Bounds;
            return r.Left <= s.Left && r.Top <= s.Top && r.Right >= s.Right && r.Bottom >= s.Bottom;
        }

        [DllImport("powrprof.dll")] static extern uint CallNtPowerInformation(int level, IntPtr inBuf, int inLen, out uint outBuf, int outLen);

        /// Something keeps the screen on — a video in the browser or a player (music does not): ES_DISPLAY_REQUIRED.
        public static bool DisplayRequired()
        {
            try { uint s; return CallNtPowerInformation(16, IntPtr.Zero, 0, out s, 4) == 0 && (s & 2) != 0; }
            catch { return false; }
        }

        [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        /// Seconds since the last key press or mouse move anywhere.
        public static double IdleSeconds()
        {
            var li = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
            if (!GetLastInputInfo(ref li)) return 0;
            return unchecked((uint)Environment.TickCount - li.dwTime) / 1000.0;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        class MEMORYSTATUSEX
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
            public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX m);

        public static long FreeMB()
        {
            var m = new MEMORYSTATUSEX();
            return GlobalMemoryStatusEx(m) ? (long)(m.ullAvailPhys / (1024 * 1024)) : 4096;
        }

        public static int MemoryLoad()
        {
            var m = new MEMORYSTATUSEX();
            return GlobalMemoryStatusEx(m) ? (int)m.dwMemoryLoad : 0;
        }

        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
        public static bool MouseButtonDown() { return (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0; }
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        public static readonly IntPtr TOPMOST = new IntPtr(-1);
    }
}
