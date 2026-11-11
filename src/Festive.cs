using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Kotik
{
    /// A base for small decorations: a transparent window that never takes focus (and, if asked, never takes clicks).
    class Deco : Form
    {
        readonly bool through;
        protected readonly Surface surface;

        public Deco(int w, int h, bool clickThrough)
        {
            through = clickThrough;
            surface = new Surface(w, h);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000 | 0x80 | 0x8 | 0x08000000 | (through ? 0x20 : 0);
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        public Bitmap Image { get { return surface.Bitmap; } }

        protected override void Dispose(bool disposing)
        {
            if (disposing) surface.Dispose();
            base.Dispose(disposing);
        }
    }

    /// A little Christmas tree that stands on the taskbar; its lights blink, a click changes the pattern.
    class TreeForm : Deco
    {
        const int W = 90, H = 128;
        readonly Timer blink = new Timer { Interval = 450 };
        readonly Random rnd = new Random();
        int x, y, phase, pattern;
        public event Action Clicked = delegate { };
        public float X { get { return x + W / 2f; } }
        public int Presents;

        public TreeForm() : base(W, H, false)
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            x = wa.Right - 260;
            y = wa.Bottom - H + 4;
            blink.Tick += delegate { phase++; Draw(); };
            Load += delegate { Draw(); blink.Start(); };
            Cursor = Cursors.Hand;
        }

        public void Draw()
        {
            var g = surface.G;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using (var trunk = new SolidBrush(Color.FromArgb(110, 70, 40))) g.FillRectangle(trunk, 39, 106, 12, 18);
            if (Presents > 0)
            {
                Color[] box = { Color.FromArgb(220, 60, 70), Color.FromArgb(70, 140, 220), Color.FromArgb(240, 190, 60) };
                for (int i = 0; i < Presents; i++)
                {
                    float px = i % 2 == 0 ? 8 + i * 6 : 60 - i * 4, sz = 18 - i * 2;
                    using (var b = new SolidBrush(box[i % 3])) g.FillRectangle(b, px, 124 - sz, sz, sz);
                    using (var b = new SolidBrush(Color.FromArgb(250, 245, 230))) { g.FillRectangle(b, px + sz / 2 - 1.5f, 124 - sz, 3, sz); g.FillRectangle(b, px, 124 - sz / 2 - 1.5f, sz, 3); }
                }
            }
            Color[] greens = { Color.FromArgb(30, 110, 60), Color.FromArgb(40, 130, 70), Color.FromArgb(50, 150, 80) };
            for (int k = 0; k < 3; k++)
            {
                float top = 14 + k * 26, half = 18 + k * 10, bottom = top + 40;
                using (var b = new SolidBrush(greens[k]))
                    g.FillPolygon(b, new[] { new PointF(45, top), new PointF(45 - half - 6, bottom), new PointF(45 + half + 6, bottom) });
            }
            // the star
            var star = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5;
                float r = i % 2 == 0 ? 9 : 4;
                star[i] = new PointF(45 + (float)Math.Cos(a) * r, 12 + (float)Math.Sin(a) * r);
            }
            using (var b = new SolidBrush(Color.FromArgb(255, 220, 80))) g.FillPolygon(b, star);
            // the lights
            Color[] cols = { Color.FromArgb(255, 80, 80), Color.FromArgb(255, 210, 70), Color.FromArgb(90, 190, 255), Color.FromArgb(200, 120, 255) };
            int n = 0;
            for (int row = 0; row < 4; row++)
                for (int i = -2; i <= 2; i++)
                {
                    float lx = 45 + i * (8 + row * 3.5f), ly = 40 + row * 20 + Math.Abs(i) * 2;
                    if (Math.Abs(lx - 45) > 12 + row * 9) continue;
                    bool on = pattern == 0 ? (n + phase) % 2 == 0 : pattern == 1 ? (n + phase) % 4 != 0 : rnd.Next(3) > 0;
                    var c = cols[n % cols.Length];
                    using (var b = new SolidBrush(on ? c : Color.FromArgb(90, c))) g.FillEllipse(b, lx - 3, ly - 3, 6, 6);
                    n++;
                }
            if (IsHandleCreated) surface.Present(Handle, x, y);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pattern = (pattern + 1) % 3;
            Draw();
            Clicked();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) blink.Dispose();
            base.Dispose(disposing);
        }
    }

    /// Lights along the top edge of the taskbar. Clicks go through.
    class GarlandForm : Deco
    {
        readonly Timer blink = new Timer { Interval = 400 };
        int phase;
        readonly int x, y, w;

        public GarlandForm() : this(Screen.PrimaryScreen.WorkingArea) { }

        GarlandForm(Rectangle wa) : base(wa.Width, 22, true)
        {
            x = wa.Left;
            y = wa.Bottom - 9;
            w = wa.Width;
            blink.Tick += delegate { phase++; Draw(); };
            Load += delegate { Draw(); blink.Start(); };
        }

        public void Draw()
        {
            var g = surface.G;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            Color[] cols = { Color.FromArgb(255, 80, 80), Color.FromArgb(255, 210, 70), Color.FromArgb(90, 220, 120), Color.FromArgb(90, 170, 255) };
            const int step = 46;
            using (var wire = new Pen(Color.FromArgb(150, 40, 60, 40), 1.6f))
                for (int sx = 0; sx < w; sx += step)
                    g.DrawBezier(wire, sx, 3, sx + step / 3f, 13, sx + step * 2 / 3f, 13, sx + step, 3);
            int n = 0;
            for (int sx = step / 2; sx < w; sx += step)
            {
                bool on = (n + phase) % 3 != 0;
                var c = cols[n % cols.Length];
                if (on)
                    using (var glow = new SolidBrush(Color.FromArgb(70, c))) g.FillEllipse(glow, sx - 7, 6, 14, 14);
                using (var b = new SolidBrush(on ? c : Color.FromArgb(120, c))) g.FillEllipse(b, sx - 3.5f, 9, 7, 9);
                n++;
            }
            if (IsHandleCreated) surface.Present(Handle, x, y);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) blink.Dispose();
            base.Dispose(disposing);
        }
    }

    /// A snowball flying along an arc. When it arrives it bursts into snow and calls back.
    class SnowballForm : Deco
    {
        readonly Timer tick = new Timer { Interval = 16 };
        readonly PointF from, to;
        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        readonly Action hit;
        const float Time = 0.7f;
        bool landed;

        readonly int kind;   // 0 snowball, 1 heart

        public SnowballForm(PointF from, PointF to, Action hit) : this(from, to, hit, 0) { }

        public SnowballForm(PointF from, PointF to, Action hit, int kind) : base(60, 60, true)
        {
            this.kind = kind;
            this.from = from;
            this.to = to;
            this.hit = hit;
            tick.Tick += delegate { Step(); };
            Load += delegate { tick.Start(); };
        }

        void Step()
        {
            float t = (float)clock.Elapsed.TotalSeconds / Time;
            var g = surface.G;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float px, py;
            if (t < 1)
            {
                px = from.X + (to.X - from.X) * t;
                py = from.Y + (to.Y - from.Y) * t - (float)Math.Sin(t * Math.PI) * 120;
                if (kind == 1)
                    using (var hp = new GraphicsPath())
                    {
                        hp.AddBezier(30, 38, 18, 30, 22, 18, 30, 26);
                        hp.AddBezier(30, 26, 38, 18, 42, 30, 30, 38);
                        using (var b = new SolidBrush(Color.FromArgb(255, 90, 120))) g.FillPath(b, hp);
                    }
                else
                {
                    using (var b = new SolidBrush(Color.FromArgb(250, 252, 255))) g.FillEllipse(b, 22, 22, 16, 16);
                    using (var p = new Pen(Color.FromArgb(170, 190, 210), 1.4f)) g.DrawEllipse(p, 22, 22, 16, 16);
                }
            }
            else
            {
                // the burst
                px = to.X;
                py = to.Y;
                float k = Math.Min(1, (t - 1) * 3);
                using (var b = new SolidBrush(kind == 1 ? Color.FromArgb((int)(240 * (1 - k)), 255, 120, 150) : Color.FromArgb((int)(240 * (1 - k)), 250, 252, 255)))
                    for (int i = 0; i < 9; i++)
                    {
                        double a = i * Math.PI * 2 / 9;
                        g.FillEllipse(b, 28 + (float)Math.Cos(a) * k * 24, 28 + (float)Math.Sin(a) * k * 24, 5, 5);
                    }
                if (!landed) { landed = true; hit(); }
                if (k >= 1) { tick.Stop(); Close(); return; }
            }
            if (IsHandleCreated) surface.Present(Handle, (int)px - 30, (int)py - 30);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tick.Dispose();
            base.Dispose(disposing);
        }
    }
}
