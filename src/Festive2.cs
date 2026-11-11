using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Kotik
{
    /// Halloween: a jack-o'-lantern on the taskbar. Its candle flickers; a click makes it say "Boo!".
    class PumpkinForm : Deco
    {
        const int W = 70, H = 64;
        readonly Timer flicker = new Timer { Interval = 120 };
        readonly Random rnd = new Random();
        readonly int x, y;
        public event Action Clicked = delegate { };

        public PumpkinForm() : base(W, H, false)
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            x = wa.Left + 150;
            y = wa.Bottom - H + 4;
            flicker.Tick += delegate { Draw(); };
            Load += delegate { Draw(); flicker.Start(); };
            Cursor = Cursors.Hand;
        }

        public void Draw()
        {
            var g = surface.G;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            int glow = 150 + rnd.Next(100);
            using (var b = new SolidBrush(Color.FromArgb(60, 255, 160, 40))) g.FillEllipse(b, 2, 8, 66, 56);
            using (var b = new SolidBrush(Color.FromArgb(235, 120, 30)))
            {
                g.FillEllipse(b, 8, 16, 30, 44);
                g.FillEllipse(b, 32, 16, 30, 44);
                g.FillEllipse(b, 18, 14, 34, 48);
            }
            using (var pen = new Pen(Color.FromArgb(190, 85, 20), 1.6f))
            {
                g.DrawArc(pen, 18, 14, 34, 48, 100, 160);
                g.DrawArc(pen, 18, 14, 34, 48, -80, 160);
            }
            using (var b = new SolidBrush(Color.FromArgb(80, 120, 40))) g.FillRectangle(b, 32, 6, 6, 11);
            using (var b = new SolidBrush(Color.FromArgb(glow, 255, 220, 90)))
            {
                g.FillPolygon(b, new[] { new PointF(22, 30), new PointF(30, 30), new PointF(26, 24) });
                g.FillPolygon(b, new[] { new PointF(40, 30), new PointF(48, 30), new PointF(44, 24) });
                g.FillPolygon(b, new[] { new PointF(22, 42), new PointF(48, 42), new PointF(44, 50), new PointF(39, 46), new PointF(35, 51), new PointF(31, 46), new PointF(26, 50) });
            }
            if (IsHandleCreated) surface.Present(Handle, x, y);
        }

        protected override void OnMouseUp(MouseEventArgs e) { Clicked(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) flicker.Dispose();
            base.Dispose(disposing);
        }
    }

    /// Winter: a snowman the cat "builds" over December — one ball, then two, then three with a carrot and a hat.
    class SnowmanForm : Deco
    {
        const int W = 70, H = 100;
        readonly int x, y, stage;
        public event Action Clicked = delegate { };

        public SnowmanForm(int stage) : base(W, H, false)
        {
            this.stage = Math.Max(1, Math.Min(4, stage));
            var wa = Screen.PrimaryScreen.WorkingArea;
            x = wa.Left + 70;
            y = wa.Bottom - H + 4;
            Load += delegate { Draw(); };
            Cursor = Cursors.Hand;
        }

        public void Draw()
        {
            var g = surface.G;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using (var snow = new SolidBrush(Color.FromArgb(248, 250, 255)))
            using (var edge = new Pen(Color.FromArgb(170, 185, 205), 1.5f))
            {
                g.FillEllipse(snow, 12, 58, 46, 40); g.DrawEllipse(edge, 12, 58, 46, 40);
                if (stage >= 2) { g.FillEllipse(snow, 18, 32, 34, 30); g.DrawEllipse(edge, 18, 32, 34, 30); }
                if (stage >= 3)
                {
                    g.FillEllipse(snow, 23, 12, 24, 23); g.DrawEllipse(edge, 23, 12, 24, 23);
                    using (var b = new SolidBrush(Color.FromArgb(30, 30, 35))) { g.FillEllipse(b, 29, 19, 3, 3); g.FillEllipse(b, 38, 19, 3, 3); }
                    using (var b = new SolidBrush(Color.FromArgb(240, 130, 40))) g.FillPolygon(b, new[] { new PointF(35, 23), new PointF(35, 27), new PointF(46, 25) });
                    using (var b = new SolidBrush(Color.FromArgb(30, 30, 35))) for (int i = 0; i < 3; i++) g.FillEllipse(b, 33, 38 + i * 7, 3, 3);
                }
                if (stage >= 4)
                {
                    using (var b = new SolidBrush(Color.FromArgb(40, 40, 50))) { g.FillRectangle(b, 26, 2, 18, 12); g.FillRectangle(b, 22, 12, 26, 3); }
                    using (var b = new SolidBrush(Color.FromArgb(200, 50, 60))) g.FillRectangle(b, 22, 31, 26, 4);
                }
            }
            if (IsHandleCreated) surface.Present(Handle, x, y);
        }

        protected override void OnMouseUp(MouseEventArgs e) { Clicked(); }

        /// A little wobble after a touch.
        public void Wobble()
        {
            int n = 0;
            var tm = new Timer { Interval = 30 };
            tm.Tick += delegate
            {
                n++;
                int dx = n < 16 ? (int)(Math.Sin(n * 1.3) * (16 - n) * 0.4) : 0;
                if (IsHandleCreated) surface.Present(Handle, x + dx, y);
                if (n >= 16) { tm.Stop(); tm.Dispose(); }
            };
            tm.Start();
        }
    }
}
