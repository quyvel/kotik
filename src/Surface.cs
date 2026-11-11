using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Kotik
{
    /// A drawing surface shared by GDI+ and the layered window: GDI+ paints straight into the memory the window shows,
    /// so a frame costs no bitmap copies or allocations (that was most of the CPU time).
    class Surface : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Native.POINT pptDst, ref Native.SIZE psize,
            IntPtr hdcSrc, ref Native.POINT pprSrc, int crKey, ref Native.BLENDFUNCTION pblend, int dwFlags);

        readonly IntPtr dc, dib, old;
        public readonly Bitmap Bitmap;
        public readonly Graphics G;
        readonly int w, h;

        public Surface(int width, int height)
        {
            w = width;
            h = height;
            var bi = new BITMAPINFOHEADER { biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER)), biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
            IntPtr screen = GetDC(IntPtr.Zero);
            dc = CreateCompatibleDC(screen);
            ReleaseDC(IntPtr.Zero, screen);
            IntPtr bits;
            dib = CreateDIBSection(dc, ref bi, 0, out bits, IntPtr.Zero, 0);
            old = SelectObject(dc, dib);
            Bitmap = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, bits);   // premultiplied, as the layered window wants
            G = Graphics.FromImage(Bitmap);
        }

        public void Present(IntPtr hwnd, int x, int y)
        {
            var size = new Native.SIZE { cx = w, cy = h };
            var src = new Native.POINT();
            var dst = new Native.POINT { X = x, Y = y };
            var blend = new Native.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
            UpdateLayeredWindow(hwnd, IntPtr.Zero, ref dst, ref size, dc, ref src, 0, ref blend, 2);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ULWINFO
        {
            public int cbSize;
            public IntPtr hdcDst, pptDst, psize, hdcSrc, pptSrc;
            public int crKey;
            public IntPtr pblend;
            public int dwFlags;
            public IntPtr prcDirty;
        }
        [DllImport("user32.dll")] static extern bool UpdateLayeredWindowIndirect(IntPtr hwnd, ref ULWINFO info);
        IntPtr args;

        /// Sends only a changed rectangle: for a big window (the weather over the whole screen) a full frame is ~5 ms, a strip ~0.3.
        public void Present(IntPtr hwnd, int x, int y, Rectangle dirty)
        {
            if (args == IntPtr.Zero) args = Marshal.AllocHGlobal(48);
            int[] v = { x, y, w, h, 0, 0, 0x01FF0000, dirty.Left, dirty.Top, dirty.Right, dirty.Bottom };   // dst, size, src, blend, rect
            Marshal.Copy(v, 0, args, v.Length);
            var info = new ULWINFO { cbSize = Marshal.SizeOf(typeof(ULWINFO)), pptDst = args, psize = args + 8, hdcSrc = dc, pptSrc = args + 16,
                pblend = args + 24, dwFlags = 2, prcDirty = args + 28 };
            UpdateLayeredWindowIndirect(hwnd, ref info);
        }

        public void Dispose()
        {
            G.Dispose();
            Bitmap.Dispose();
            SelectObject(dc, old);
            DeleteObject(dib);
            DeleteDC(dc);
            if (args != IntPtr.Zero) Marshal.FreeHGlobal(args);
        }
    }
}
