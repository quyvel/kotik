using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Kotik
{
    /// Finds "shelves" inside the active program: long straight colour borders such as the edge of a panel, a tab bar or a
    /// status bar. The cat can walk on them, so it can live inside a maximised VS Code too.
    static class Shelves
    {
        static byte[] px;
        static short[] lum;
        const int MinRun = 260, Contrast = 6, Flat = 3, MaxShelves = 10;   // dark themes have very soft borders

        /// `area` is the window on screen; `skip` is the cat itself (its pixels are not part of the program).
        public static List<Plat> Find(IntPtr window, Rectangle area, Rectangle skip)
        {
            var found = new List<Plat>();
            area.Intersect(System.Windows.Forms.SystemInformation.VirtualScreen);
            if (area.Width < 500 || area.Height < 300) return found;
            using (var bmp = new Bitmap(area.Width, area.Height, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(area.Location, Point.Empty, area.Size);
                var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                int stride = data.Stride;
                if (px == null || px.Length < stride * bmp.Height) px = new byte[stride * bmp.Height];   // reused: no 6 MB of garbage every scan
                Marshal.Copy(data.Scan0, px, 0, stride * bmp.Height);
                bmp.UnlockBits(data);
                int w = bmp.Width, h = bmp.Height;
                if (lum == null || lum.Length < w * h) lum = new short[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x += 2)
                    {
                        int i = y * stride + x * 3;
                        lum[y * w + x] = (short)((px[i] * 29 + px[i + 1] * 150 + px[i + 2] * 77) >> 8);
                    }
                int lastY = -100;
                for (int y = 50; y < h - 30; y++)
                {
                    if (y - lastY < 6) continue;
                    int runStart = -1, gap = 0, bestA = 0, bestB = 0;
                    for (int x = 0; x < w; x += 2)
                    {
                        bool inCat = skip.Contains(area.Left + x, area.Top + y);
                        int up = lum[(y - 1) * w + x], up2 = lum[(y - 3) * w + x];
                        int dn = lum[(y + 1) * w + x], dn2 = lum[(y + 3) * w + x];
                        bool edge = inCat || (Math.Abs(up - dn) > Contrast && Math.Abs(up - up2) < Flat && Math.Abs(dn - dn2) < Flat);
                        if (edge)
                        {
                            if (runStart < 0) runStart = x;
                            gap = 0;
                            if (x - runStart > bestB - bestA) { bestA = runStart; bestB = x; }
                        }
                        else if (runStart >= 0 && ++gap > 4) runStart = -1;
                    }
                    if (bestB - bestA >= MinRun)
                    {
                        found.Add(new Plat { H = window, Shelf = true, R = new Rectangle(area.Left + bestA, area.Top + y, bestB - bestA, 1) });
                        lastY = y;
                    }
                }
            }
            found.Sort((a, b) => b.R.Width.CompareTo(a.R.Width));
            if (found.Count > MaxShelves) found.RemoveRange(MaxShelves, found.Count - MaxShelves);
            return found;
        }
    }
}
