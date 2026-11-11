using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Kotik
{
    /// A present the cat brings: a little thing lying next to it. A click picks it up.
    class GiftForm : Form
    {
        public event Action Taken = delegate { };
        readonly Surface surface = new Surface(44, 34);
        readonly int x, y;

        public GiftForm(int kind, float fx, float fy)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            x = (int)fx - 22;
            y = (int)fy - 30;
            var g = surface.G;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using (var line = new Pen(Color.FromArgb(70, 50, 40), 1.6f))
            {
                switch (kind)
                {
                    case 0:   // a toy mouse
                        using (var b = new SolidBrush(Color.FromArgb(170, 170, 180))) g.FillEllipse(b, 8, 14, 26, 15);
                        using (var b = new SolidBrush(Color.FromArgb(240, 170, 180))) g.FillEllipse(b, 26, 11, 8, 8);
                        g.DrawBezier(line, 8, 22, 2, 20, 4, 30, 0, 32);
                        break;
                    case 1:   // a ball of yarn
                        using (var b = new SolidBrush(Color.FromArgb(220, 80, 110))) g.FillEllipse(b, 10, 8, 22, 22);
                        using (var p = new Pen(Color.FromArgb(160, 40, 70), 1.4f)) { g.DrawArc(p, 12, 10, 18, 18, 30, 200); g.DrawArc(p, 14, 12, 14, 14, 200, 160); }
                        g.DrawBezier(line, 30, 22, 36, 26, 38, 32, 44, 30);
                        break;
                    case 2:   // a feather
                        using (var b = new SolidBrush(Color.FromArgb(120, 190, 230))) g.FillEllipse(b, 6, 16, 32, 10);
                        g.DrawLine(line, 4, 24, 38, 18);
                        break;
                    case 3:   // a sock
                        using (var b = new SolidBrush(Color.FromArgb(240, 200, 90)))
                        {
                            g.FillRectangle(b, 14, 6, 12, 18);
                            g.FillEllipse(b, 14, 18, 24, 12);
                        }
                        using (var b = new SolidBrush(Color.FromArgb(220, 90, 70))) g.FillRectangle(b, 14, 6, 12, 5);
                        break;
                    case 4:   // a bottle cap
                        using (var b = new SolidBrush(Color.FromArgb(200, 60, 60))) g.FillEllipse(b, 10, 14, 24, 14);
                        using (var b = new SolidBrush(Color.FromArgb(240, 120, 110))) g.FillEllipse(b, 14, 16, 16, 8);
                        break;
                    case 7:   // a coupon
                        using (var b = new SolidBrush(Color.FromArgb(250, 240, 200))) g.FillRectangle(b, 4, 12, 36, 18);
                        using (var p = new Pen(Color.FromArgb(220, 80, 60), 1.4f) { DashPattern = new float[] { 2, 2 } }) g.DrawRectangle(p, 4, 12, 36, 18);
                        using (var f = new Font("Segoe UI", 7f, FontStyle.Bold))
                        using (var b = new SolidBrush(Color.FromArgb(220, 60, 50))) g.DrawString("-50%", f, b, 7, 14);
                        break;
                    case 6:   // a mandarin
                        using (var b = new SolidBrush(Color.FromArgb(250, 150, 40))) g.FillEllipse(b, 10, 10, 22, 20);
                        using (var b = new SolidBrush(Color.FromArgb(70, 160, 70))) g.FillEllipse(b, 20, 6, 10, 6);
                        using (var b = new SolidBrush(Color.FromArgb(255, 190, 90))) g.FillEllipse(b, 14, 13, 6, 5);
                        break;
                    default:  // a bow
                        using (var b = new SolidBrush(Color.FromArgb(170, 110, 230)))
                        {
                            g.FillPolygon(b, new[] { new PointF(22, 20), new PointF(8, 12), new PointF(8, 28) });
                            g.FillPolygon(b, new[] { new PointF(22, 20), new PointF(36, 12), new PointF(36, 28) });
                            g.FillEllipse(b, 18, 16, 8, 8);
                        }
                        break;
                }
            }
            Load += delegate { surface.Present(Handle, x, y); };
            Cursor = Cursors.Hand;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000 | 0x80 | 0x8 | 0x08000000;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            Taken();
            Close();
        }
    }

    /// The red laser dot. It follows the mouse a little behind and never takes clicks.
    class LaserForm : Form
    {
        readonly Surface surface = new Surface(40, 40);
        public PointF Dot;
        bool placed;

        public LaserForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            var g = surface.G;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(2, 2, 36, 36);
                using (var b = new PathGradientBrush(path) { CenterColor = Color.FromArgb(160, 255, 40, 40), SurroundColors = new[] { Color.FromArgb(0, 255, 0, 0) } })
                    g.FillPath(b, path);
            }
            using (var b = new SolidBrush(Color.FromArgb(255, 255, 60, 60))) g.FillEllipse(b, 15, 15, 10, 10);
            using (var b = new SolidBrush(Color.FromArgb(220, 255, 220, 220))) g.FillEllipse(b, 18, 18, 4, 4);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000 | 0x80 | 0x8 | 0x08000000 | 0x20;   // click-through
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        public void Follow(Point cur)
        {
            // the dot sits a bit away from the cursor, so the arrow does not hide it
            var target = new PointF(cur.X + 24, cur.Y + 24);
            if (!placed) { Dot = target; placed = true; }
            Dot = new PointF(Dot.X + (target.X - Dot.X) * 0.35f, Dot.Y + (target.Y - Dot.Y) * 0.35f);
            if (IsHandleCreated) surface.Present(Handle, (int)Dot.X - 20, (int)Dot.Y - 20);
        }
    }
}
