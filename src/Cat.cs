using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Kotik
{
    enum Eyes { Open, Closed, Happy, Wide }

    /// Everything that shapes one frame of the cat. The brain sets it, Cat.Draw paints it.
    class Pose
    {
        public float BodyY = -26;       // body centre above the feet
        public float BodyAngle;         // degrees, + = nose down
        public float Walk;              // leg cycle phase (radians), 0 = standing
        public float Stride;            // how far the legs swing
        public float HeadX = 30, HeadY = -46, HeadTilt;
        public float Tail;              // tail sway, -1..1
        public float TailUp = 1;        // 1 = tail up, 0 = tail on the ground
        public bool Sitting, Loaf, Dangle, Spread, PawUp, Stretch;
        public Eyes Eyes = Eyes.Open;
        public float LookX, LookY;      // where the pupils point, -1..1
        public float Squash;            // landing squash, 0..1
        public bool Blush;
        public float Ears = 1;          // 1 = up, 0 = flat (sad or scared)
        public string Hat = "";        // "witch" on Halloween, "santa" around New Year
    }

    class Coat
    {
        public string Ru, En;
        public string Name { get { return L.En ? En : Ru; } }
        public Color Main, Dark, Light, Line, Eye;
        public bool Stripes, Patches;
    }

    /// Draws a round cartoon cat (side view, 3/4 face) with GDI+. Origin = between the feet on the ground, facing right.
    static class Cat
    {
        public static readonly Coat[] Coats =
        {
            new Coat { Ru = "Рыжий", En = "Ginger", Main = Color.FromArgb(236, 146, 64), Dark = Color.FromArgb(198, 104, 38), Light = Color.FromArgb(252, 222, 184),
                Line = Color.FromArgb(120, 62, 26), Eye = Color.FromArgb(120, 190, 80), Stripes = true },
            new Coat { Ru = "Серый", En = "Grey", Main = Color.FromArgb(146, 150, 160), Dark = Color.FromArgb(104, 108, 120), Light = Color.FromArgb(218, 220, 226),
                Line = Color.FromArgb(62, 64, 74), Eye = Color.FromArgb(230, 190, 70), Stripes = true },
            new Coat { Ru = "Чёрный", En = "Black", Main = Color.FromArgb(52, 52, 60), Dark = Color.FromArgb(34, 34, 40), Light = Color.FromArgb(84, 84, 96),
                Line = Color.FromArgb(18, 18, 22), Eye = Color.FromArgb(240, 210, 60) },
            new Coat { Ru = "Белый", En = "White", Main = Color.FromArgb(246, 244, 238), Dark = Color.FromArgb(220, 216, 208), Light = Color.FromArgb(255, 255, 255),
                Line = Color.FromArgb(130, 126, 120), Eye = Color.FromArgb(90, 160, 230) },
            new Coat { Ru = "Трёхцветный", En = "Calico", Main = Color.FromArgb(246, 242, 234), Dark = Color.FromArgb(232, 140, 60), Light = Color.FromArgb(255, 255, 255),
                Line = Color.FromArgb(110, 90, 70), Eye = Color.FromArgb(120, 190, 80), Patches = true },
        };

        static Pen Outline(Coat c) { return new Pen(c.Line, 2.2f) { LineJoin = LineJoin.Round }; }

        static void Blob(Graphics g, GraphicsPath p, Color fill, Coat c)
        {
            using (var b = new SolidBrush(fill)) g.FillPath(b, p);
            using (var pen = Outline(c)) g.DrawPath(pen, p);
        }

        static GraphicsPath Ellipse(float cx, float cy, float w, float h)
        {
            var p = new GraphicsPath();
            p.AddEllipse(cx - w / 2, cy - h / 2, w, h);
            return p;
        }

        static GraphicsPath Leg(float x0, float y0, float x1, float y1, float w)
        {
            // a rounded stub from the hip (x0,y0) to the paw (x1,y1)
            var p = new GraphicsPath();
            float dx = x1 - x0, dy = y1 - y0;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.1f) len = 0.1f;
            float nx = -dy / len * w / 2, ny = dx / len * w / 2;
            p.AddLine(x0 + nx, y0 + ny, x1 + nx, y1 + ny);
            p.AddArc(x1 - w / 2, y1 - w / 2, w, w, (float)(Math.Atan2(ny, nx) * 180 / Math.PI), -180);
            p.AddLine(x1 - nx, y1 - ny, x0 - nx, y0 - ny);
            p.CloseFigure();
            return p;
        }

        public static void Draw(Graphics g, float x, float y, int dir, Pose p, Coat c, float t)
        {
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(x, y);
            if (p.Squash > 0) g.ScaleTransform(1 + 0.25f * p.Squash, 1 - 0.3f * p.Squash);
            g.ScaleTransform(dir, 1);
            if (p.Dangle)
            {
                // held by the scruff: the whole cat hangs from a point above its head
                g.TranslateTransform(0, -70);
                g.RotateTransform(-80 + (float)Math.Sin(t * 3) * 8);
                g.TranslateTransform(-26, 46);
            }
            float by = p.BodyY;
            // shadow on the ground
            if (!p.Dangle && p.BodyY > -60)
                using (var sh = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillEllipse(sh, -36, -4, 72, 8);

            // tail
            float sway = p.Tail * 14;
            using (var tail = new GraphicsPath())
            {
                PointF a = p.Sitting ? new PointF(-18, -10) : new PointF(-28, by - 2);
                PointF b, cc, d;
                if (p.TailUp > 0.5f)
                {
                    b = new PointF(-50, by - 6);
                    cc = new PointF(-56 + sway * 0.3f, by - 34);
                    d = new PointF(-44 + sway, by - 50);
                }
                else if (p.Sitting)
                {
                    b = new PointF(-42, -8);
                    cc = new PointF(-38, 2);
                    d = new PointF(-4 + sway * 0.5f, 0);       // wrapped around the paws
                }
                else
                {
                    b = new PointF(-46, by + 4);
                    cc = new PointF(-50, by + 20);
                    d = new PointF(-12 + sway * 0.4f, by + 22);
                }
                tail.AddBezier(a, b, cc, d);
                using (var pen = new Pen(c.Line, 12) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(pen, tail);
                using (var pen = new Pen(c.Patches ? Color.FromArgb(70, 60, 56) : c.Main, 8) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(pen, tail);
                if (c.Stripes)
                    using (var pen = new Pen(c.Dark, 8) { DashPattern = new float[] { 0.6f, 0.9f } }) g.DrawPath(pen, tail);
            }

            var bodyState = g.Save();
            g.TranslateTransform(0, by);
            g.RotateTransform(p.BodyAngle);
            g.TranslateTransform(0, -by);
            float sw = (float)Math.Sin(p.Walk) * p.Stride;
            float legY = by + 8;
            // back legs behind the body
            if (!p.Loaf)
            {
                if (p.Sitting)
                {
                    // nothing behind: the haunch is drawn over the body below
                }
                else
                {
                    float spread = p.Spread ? 10 : 0;
                    using (var l = Leg(-20, legY, -22 - sw - spread, -5, 11)) Blob(g, l, c.Dark, c);
                    using (var l = Leg(14, legY, 16 + sw + spread, -5, 11)) Blob(g, l, c.Dark, c);
                }
            }
            // body
            float bw = p.Loaf ? 70 : p.Sitting ? 42 : 64, bh = p.Loaf ? 30 : p.Sitting ? 50 : 34;
            using (var body = Ellipse(p.Sitting ? 2 : -2, by, bw, bh))
            {
                Blob(g, body, c.Main, c);
                var clip = g.Clip;
                g.SetClip(body, CombineMode.Intersect);
                using (var belly = new SolidBrush(c.Light))
                {
                    if (p.Sitting) g.FillEllipse(belly, 6, by - 14, 22, 40);
                    else g.FillEllipse(belly, -24, by + 2, 46, 20);
                }
                if (c.Stripes)
                    using (var pen = new Pen(c.Dark, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        for (int i = 0; i < 4; i++) g.DrawLine(pen, -20 + i * 11, by - 17, -24 + i * 11, by - 6);
                if (c.Patches)
                {
                    using (var b1 = new SolidBrush(c.Dark)) g.FillEllipse(b1, -30, by - 18, 26, 18);
                    using (var b2 = new SolidBrush(Color.FromArgb(70, 60, 56))) g.FillEllipse(b2, 2, by - 20, 22, 14);
                }
                g.Clip = clip;
                using (var pen = Outline(c)) g.DrawPath(pen, body);
            }
            // front legs
            if (!p.Loaf)
            {
                if (p.Sitting)
                {
                    using (var haunch = Ellipse(-10, -12, 36, 24)) Blob(g, haunch, c.Main, c);
                    using (var foot = Ellipse(-2, -4, 22, 9)) Blob(g, foot, c.Light, c);
                    using (var l = Leg(10, by + 6, 11, -5, 11)) Blob(g, l, c.Main, c);
                    if (p.PawUp)
                        using (var l = Leg(18, by, 28, by - 18, 10)) Blob(g, l, c.Main, c);
                    else
                        using (var l = Leg(19, by + 6, 21, -5, 11)) Blob(g, l, c.Main, c);
                }
                else if (p.Stretch)
                {
                    using (var l = Leg(18, legY - 4, 44, -5, 11)) Blob(g, l, c.Main, c);
                    using (var l = Leg(24, legY - 4, 50, -5, 11)) Blob(g, l, c.Main, c);
                }
                else
                {
                    float spread = p.Spread ? 10 : 0;
                    using (var l = Leg(-12, legY, -12 + sw - spread, -5, 11)) Blob(g, l, c.Main, c);
                    using (var l = Leg(22, legY, 22 - sw + spread, -5, 11)) Blob(g, l, c.Main, c);
                }
            }
            else
            {
                // loaf: just the tips of the front paws peek out
                using (var paw = Ellipse(30, by + 12, 14, 8)) Blob(g, paw, c.Light, c);
            }
            g.Restore(bodyState);

            DrawHead(g, p, c, t);
            g.Restore(state);
        }

        /// Just the head (for icons), centred at (x, y).
        public static void Face(Graphics g, float x, float y, Pose p, Coat c)
        {
            var s = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(x - p.HeadX, y - p.HeadY);
            DrawHead(g, p, c, 0);
            g.Restore(s);
        }

        static void DrawHead(Graphics g, Pose p, Coat c, float t)
        {
            var s = g.Save();
            g.TranslateTransform(p.HeadX, p.HeadY);
            g.RotateTransform(p.HeadTilt);
            float r = 21;
            // ears (behind the head outline)
            float earUp = 0.4f + 0.6f * p.Ears;
            foreach (int k in new[] { -1, 1 })
            {
                float ex = k * 11 + 3;
                var ear = new GraphicsPath();
                ear.AddPolygon(new[] { new PointF(ex - 9, -12), new PointF(ex + k * 4 * (1 - earUp) + 1, -r - 12 * earUp), new PointF(ex + 9, -10) });
                Blob(g, ear, k < 0 && c.Patches ? c.Dark : c.Main, c);
                using (var inner = new SolidBrush(Color.FromArgb(240, 160, 170)))
                    g.FillPolygon(inner, new[] { new PointF(ex - 5, -13), new PointF(ex + k * 3 * (1 - earUp) + 1, -r - 6 * earUp), new PointF(ex + 5, -12) });
                ear.Dispose();
            }
            using (var head = Ellipse(0, 0, r * 2.15f, r * 1.9f))
            {
                Blob(g, head, c.Main, c);
                var clip = g.Clip;
                g.SetClip(head, CombineMode.Intersect);
                using (var muzzle = new SolidBrush(c.Light)) g.FillEllipse(muzzle, 0, 2, 24, 18);
                if (c.Stripes)
                    using (var pen = new Pen(c.Dark, 3.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        g.DrawLine(pen, -4, -19, -2, -11);
                        g.DrawLine(pen, 3, -20, 3, -12);
                        g.DrawLine(pen, 10, -18, 8, -11);
                    }
                if (c.Patches)
                    using (var b = new SolidBrush(c.Dark)) g.FillEllipse(b, -26, -20, 22, 20);
                g.Clip = clip;
                using (var pen = Outline(c)) g.DrawPath(pen, head);
            }
            // eyes: 3/4 view, the far eye a little smaller
            DrawEye(g, 4, -3, 0.85f, p, c, t);
            DrawEye(g, 16, -3, 1f, p, c, t);
            // nose, mouth, whiskers — inside the head
            using (var nose = new SolidBrush(Color.FromArgb(235, 120, 140)))
                g.FillPolygon(nose, new[] { new PointF(13, 4), new PointF(18, 4), new PointF(15.5f, 7) });
            using (var pen = new Pen(c.Line, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawArc(pen, 11.5f, 6.5f, 4, 4, 0, 160);
                g.DrawArc(pen, 15.5f, 6.5f, 4, 4, 20, 160);
                using (var wp = new Pen(Color.FromArgb(160, c.Line), 1.2f))
                {
                    g.DrawLine(wp, 19, 6, 33, 3);
                    g.DrawLine(wp, 19, 8, 33, 9);
                    g.DrawLine(wp, 6, 8, -6, 6);
                }
            }
            if (p.Blush)
                using (var b = new SolidBrush(Color.FromArgb(110, 255, 120, 140)))
                {
                    g.FillEllipse(b, 16, 1, 6, 4);
                    g.FillEllipse(b, 0, 2, 7, 4);
                }
            if (p.Hat == "scarf")
            {
                using (var red = new SolidBrush(Color.FromArgb(200, 50, 60)))
                using (var white = new SolidBrush(Color.FromArgb(245, 240, 235)))
                {
                    g.FillEllipse(red, -14, 13, 34, 11);
                    g.FillRectangle(red, -12, 18, 9, 18);
                    g.FillRectangle(white, -12, 24, 9, 3);
                    g.FillRectangle(white, -12, 31, 9, 3);
                }
            }
            if (p.Hat == "santa")
            {
                using (var red = new SolidBrush(Color.FromArgb(220, 40, 50)))
                    g.FillPolygon(red, new[] { new PointF(-16, -15), new PointF(-30, -44), new PointF(14, -17) });
                using (var white = new SolidBrush(Color.White))
                {
                    g.FillEllipse(white, -20, -21, 38, 10);
                    g.FillEllipse(white, -36, -50, 12, 12);
                }
            }
            else if (p.Hat == "witch")
            {
                using (var black = new SolidBrush(Color.FromArgb(40, 30, 50)))
                {
                    g.FillEllipse(black, -24, -21, 48, 9);
                    g.FillPolygon(black, new[] { new PointF(-12, -17), new PointF(-4, -52), new PointF(12, -17) });
                }
                using (var band = new SolidBrush(Color.FromArgb(150, 80, 200))) g.FillRectangle(band, -11, -24, 22, 5);
            }
            g.Restore(s);
        }

        static void DrawEye(Graphics g, float x, float y, float k, Pose p, Coat c, float t)
        {
            using (var pen = new Pen(c.Line, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                if (p.Eyes == Eyes.Closed)
                {
                    g.DrawArc(pen, x - 4 * k, y - 1, 8 * k, 5, 10, 160);
                    return;
                }
                if (p.Eyes == Eyes.Happy)
                {
                    g.DrawArc(pen, x - 4 * k, y - 1, 8 * k, 7, 200, 140);
                    return;
                }
            }
            float w = 8.5f * k, h = (p.Eyes == Eyes.Wide ? 12.5f : 11f) * k;
            using (var white = new SolidBrush(Color.White)) g.FillEllipse(white, x - w / 2, y - h / 2, w, h);
            using (var iris = new SolidBrush(c.Eye)) g.FillEllipse(iris, x - w / 2 + 0.6f + p.LookX * 1.4f, y - h / 2 + 0.8f + p.LookY * 1.6f, w - 1.2f, h - 1.6f);
            float pw = p.Eyes == Eyes.Wide ? 4.5f * k : 3f * k;
            using (var pupil = new SolidBrush(Color.FromArgb(20, 20, 24)))
                g.FillEllipse(pupil, x - pw / 2 + p.LookX * 1.8f, y - h * 0.32f + p.LookY * 2f, pw, h * 0.64f);
            using (var shine = new SolidBrush(Color.White)) g.FillEllipse(shine, x - w * 0.15f + p.LookX, y - h * 0.32f, 2.6f * k, 2.6f * k);
            using (var pen = new Pen(c.Line, 1.6f)) g.DrawEllipse(pen, x - w / 2, y - h / 2, w, h);
        }
    }
}
