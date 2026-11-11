using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Kotik
{
    /// Leaves in autumn, snow in winter, over the whole screen.
    /// While you are away — no input and no video; music does not count — what falls stays and piles up along
    /// the taskbar. The mouse sweeps the heap away, and the cat kicks it up when it walks through.
    /// The window is as big as the screen, so a frame only clears and sends the parts that changed (Surface.Present
    /// with a dirty rectangle): a whole-screen frame costs ~5 ms, a changed strip ~0.3 ms.
    class Weather : Deco
    {
        class Bit { public float X, Y, VX, VY, Rot, Spin, Ph, Size, Life = 1; public int C; public bool Down, Kicked; public Rectangle Was; }
        class Leaf { public float X, Y, Rot, Size; public int C; }

        public const int None = 0, Leaves = 1, Snow = 2;
        public int Kind;
        public bool Away;                  // nobody at the computer: what falls settles
        public bool Quiet;                 // a video plays: nothing falls
        public float Rate = 1;             // thicker on the last day of autumn

        const int Col = 3, MaxSnow = 70, MaxLeaves = 900;
        static readonly Color[] LeafColors = { Color.FromArgb(222, 140, 40), Color.FromArgb(200, 82, 40), Color.FromArgb(232, 190, 60),
            Color.FromArgb(170, 62, 36), Color.FromArgb(196, 160, 70) };
        readonly Rectangle scr;
        readonly int W, H, Ground;
        readonly float[] snow, leafH;
        readonly List<Bit> bits = new List<Bit>();
        readonly List<Leaf> heap = new List<Leaf>();
        readonly Random rnd = new Random();
        readonly List<Rectangle> dirty = new List<Rectangle>(), send = new List<Rectangle>();
        readonly Bitmap heapImg;           // the lying leaves, drawn once: hundreds of them every frame would cost too much
        readonly Graphics heapG;
        readonly GraphicsPath leafShape = new GraphicsPath();
        float t, spawn, frameT;
        int snowLo = int.MaxValue, snowHi = -1;   // columns of the snow that changed since the last frame
        string carved;                     // a word pressed into the snow
        float carvedX;

        public Weather() : base(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, true)
        {
            scr = Screen.PrimaryScreen.Bounds;
            W = scr.Width;
            H = scr.Height;
            Ground = Screen.PrimaryScreen.WorkingArea.Bottom - scr.Top;
            snow = new float[W / Col + 2];
            leafH = new float[W / Col + 2];
            heapImg = new Bitmap(W, MaxSnow + 30, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            heapG = Graphics.FromImage(heapImg);
            heapG.SmoothingMode = SmoothingMode.AntiAlias;
            // a leaf: pointed at both ends, 1 unit long
            leafShape.AddBezier(-0.5f, 0, -0.2f, -0.32f, 0.2f, -0.3f, 0.5f, 0);
            leafShape.AddBezier(0.5f, 0, 0.2f, 0.3f, -0.2f, 0.32f, -0.5f, 0);
            Load += delegate { surface.G.Clear(Color.Transparent); surface.Present(Handle, scr.X, scr.Y); };
        }

        int HeapTop { get { return Ground - MaxSnow - 30; } }      // where heapImg sits on the screen

        public float HeightAt(float x)
        {
            int c = Math.Max(0, Math.Min(snow.Length - 1, (int)(x / Col)));
            return Math.Max(snow[c], leafH[c]);
        }

        /// Average heap height around x (for the cat's dive and the carved word).
        public float Around(float x, float r)
        {
            float sum = 0;
            int n = 0;
            for (float px = x - r; px <= x + r; px += Col) { sum += HeightAt(px - scr.X); n++; }
            return n == 0 ? 0 : sum / n;
        }

        public bool Empty { get { return bits.Count == 0 && heap.Count == 0 && snowHi < 0 && !anySnow; } }
        bool anySnow;

        // ---------- the step ----------

        /// cat: the paws in screen coordinates; walking: it moves along the ground and kicks what lies there.
        public void Step(float dt, Point cursor, PointF cat, bool walking, int dir)
        {
            if (Kind == None && Empty) return;
            t += dt;
            float rate = Quiet ? 0 : Kind == Leaves ? (Away ? 1.6f : 0.3f) : Kind == Snow ? (Away ? 6f : 1.5f) : 0;
            spawn += dt * rate * Rate;
            while (spawn >= 1) { spawn -= 1; Spawn(); }
            float wind = (float)Math.Sin(t * 0.07) * 18 + 6;
            foreach (var b in bits)
            {
                if (b.Life <= 0) continue;
                bool leaf = b.C >= 0;
                float term = leaf ? 55 : 30 + b.Size * 9;
                b.VY += (b.VY < term ? 380 : (term - b.VY) * 2) * dt;
                b.VX += (wind - b.VX) * dt * (b.Kicked ? 0.8f : 1.5f);
                float sway = leaf ? (float)Math.Sin(t * 1.7 + b.Ph) * 55 : (float)Math.Sin(t * 1.1 + b.Ph) * 12;
                float bob = leaf ? (float)Math.Cos(t * 2.3 + b.Ph * 1.3) * 22 : 0;
                if (!b.Down)
                {
                    b.X += (b.VX + sway) * dt;
                    b.Y += (b.VY + bob) * dt;
                    b.Rot += b.Spin * dt * (0.6f + 0.4f * (float)Math.Sin(t * 2 + b.Ph));
                }
                float floor = Ground - HeightAt(b.X);
                if (!b.Down && b.VY > 0 && b.Y >= floor - (leaf ? 2 : 1))
                {
                    if (Away && !b.Kicked && Settle(b)) { b.Life = 0; continue; }
                    b.Down = true;
                    b.Y = floor - 1;
                }
                if (b.Down) b.Life -= leaf ? dt * 0.7f : 1;   // a leaf lies a moment and fades; a flake is gone at once
                if (b.X < -80 || b.X > W + 80 || b.Y > H + 20) b.Life = 0;
            }
            // the mouse sweeps (no need to click: just pass over the heap), the cat kicks with its paws
            var cur = new PointF(cursor.X - scr.X, cursor.Y - scr.Y);
            if (cur.Y > Ground - MaxSnow - 12 && cur.Y < Ground + 4) Sweep(cur.X, cur.Y, cur.X - lastCur.X, 34, 1f);
            lastCur = cur;
            var paw = new PointF(cat.X - scr.X, cat.Y - scr.Y);
            if (walking && Math.Abs(paw.Y - Ground) < 4) Sweep(paw.X + dir * 12, Ground - 4, dir * 5, 16, 0.35f);
            frameT += dt;
            if (frameT >= (bits.Count > 100 ? 1 / 15f : Kind == Snow ? 1 / 20f : 1 / 30f)) { frameT = 0; Render(); }   // a thick snowfall: only while nobody watches, gentler
        }

        PointF lastCur;

        void Spawn()
        {
            bool leaf = Kind == Leaves;
            var b = new Bit
            {
                X = rnd.Next(-60, W + 20), Y = -20, VY = leaf ? 40 : 30, Ph = (float)(rnd.NextDouble() * 7),
                Rot = rnd.Next(360), Spin = (float)(rnd.NextDouble() * 200 - 100), C = leaf ? rnd.Next(LeafColors.Length) : -1,
                Size = leaf ? 13 + (float)rnd.NextDouble() * 8 : 1.6f + (float)rnd.NextDouble() * 2.6f,
            };
            if (!leaf && Away && rnd.NextDouble() < 0.3) b.X = rnd.NextDouble() < 0.5 ? rnd.Next(0, W / 6) : rnd.Next(W * 5 / 6, W);   // drifts gather in the corners
            bits.Add(b);
        }

        /// What fell while nobody was here stays. False if there is no more room.
        bool Settle(Bit b)
        {
            int c = (int)(b.X / Col);
            if (c < 1 || c >= snow.Length - 1) return false;
            if (b.C < 0)
            {
                if (snow[c] >= MaxSnow) return false;
                float add = 1.4f + b.Size * 0.5f;
                for (int k = -1; k <= 1; k++) snow[c + k] += add * (k == 0 ? 0.5f : 0.25f);
                Relax(c - 6, c + 6);
                anySnow = true;
                return true;
            }
            if (heap.Count >= MaxLeaves) return false;
            float top = leafH[c];
            var l = new Leaf { X = b.X, Y = Ground - top - b.Size * 0.15f - (float)rnd.NextDouble() * 2, Size = b.Size, C = b.C,
                Rot = (float)(rnd.NextDouble() * 50 - 25 + (rnd.Next(2) * 180)) };
            heap.Add(l);
            int half = (int)(b.Size / 2 / Col);
            for (int k = -half; k <= half; k++)
            {
                int i = c + k;
                if (i >= 0 && i < leafH.Length) leafH[i] = Math.Min(40, leafH[i] + 2.6f * (1 - Math.Abs(k) / (half + 1f)));
            }
            DrawLeaf(heapG, l.X, l.Y - HeapTop, l.Rot, l.Size, l.C, 255);
            Mark(new Rectangle((int)l.X - 12, (int)l.Y - 12, 24, 24));
            return true;
        }

        /// Snow slides down: no column stands much higher than its neighbour.
        void Relax(int a, int b)
        {
            a = Math.Max(1, a);
            b = Math.Min(snow.Length - 2, b);
            for (int pass = 0; pass < 3; pass++)
                for (int i = a; i <= b; i++)
                    for (int k = -1; k <= 1; k += 2)
                    {
                        float d = snow[i] - snow[i + k];
                        if (d > 1.6f) { float m = (d - 1.6f) * 0.5f; snow[i] -= m; snow[i + k] += m; }
                    }
            snowLo = Math.Min(snowLo, a - 1);
            snowHi = Math.Max(snowHi, b + 1);
        }

        /// Clears the heap around (x, y): snow puffs up, leaves fly off with the push.
        void Sweep(float x, float y, float push, float r, float power)
        {
            int moved = 0;
            if (anySnow)
            {
                int c0 = Math.Max(0, (int)((x - r) / Col)), c1 = Math.Min(snow.Length - 1, (int)((x + r) / Col));
                float removed = 0;
                for (int c = c0; c <= c1; c++)
                {
                    float dx = c * Col - x, reach = (float)Math.Sqrt(Math.Max(0, r * r - dx * dx));
                    float keep = Math.Max(0, Ground - y - reach * power);
                    if (snow[c] > keep) { removed += snow[c] - keep; snow[c] = keep; }
                }
                if (removed > 0.5f) { Relax(c0 - 10, c0 + 2); Relax(c1 - 2, c1 + 10); }
                if (removed > 0.5f && bits.Count < 500)
                {
                    snowLo = Math.Min(snowLo, c0);
                    snowHi = Math.Max(snowHi, c1);
                    for (int i = 0; i < Math.Min(14, removed / 6 + 1); i++)
                        bits.Add(new Bit { X = x + rnd.Next(-(int)r, (int)r), Y = y - rnd.Next(0, 10), VX = push * 12 + rnd.Next(-70, 70), VY = -rnd.Next(90, 240) * power,
                            Size = 1.5f + (float)rnd.NextDouble() * 2, C = -1, Kicked = true, Ph = rnd.Next(7) });
                }
            }
            for (int i = heap.Count - 1; i >= 0; i--)
            {
                var l = heap[i];
                float dx = l.X - x, dy = l.Y - y;
                if (dx * dx + dy * dy > r * r) continue;
                if (power < 1 && rnd.NextDouble() > 0.25) continue;   // the cat's paws only flick a few
                heap.RemoveAt(i);
                moved++;
                bits.Add(new Bit { X = l.X, Y = l.Y - 2, VX = push * 14 + rnd.Next(-90, 90), VY = -rnd.Next(160, 340) * (0.5f + power * 0.5f), Rot = l.Rot,
                    Spin = rnd.Next(-500, 500), Size = l.Size, C = l.C, Kicked = true, Ph = rnd.Next(7) });
                int c = (int)(l.X / Col), half = (int)(l.Size / 2 / Col);
                for (int k = -half; k <= half; k++)
                    if (c + k >= 0 && c + k < leafH.Length) leafH[c + k] = Math.Max(0, leafH[c + k] - 2.6f * (1 - Math.Abs(k) / (half + 1f)));
            }
            if (moved > 0) RedrawHeap((int)(x - r - 14), (int)(x + r + 14));
            if (anySnow && snowHi >= 0)
            {
                bool left = false;
                foreach (float v in snow) if (v > 0.3f) { left = true; break; }
                if (!left) { anySnow = false; carved = null; }
            }
        }

        /// The cat landed in the heap: everything around flies up.
        public void Burst(float screenX)
        {
            float x = screenX - scr.X;
            Sweep(x, Ground - 2, 0, 70, 1.4f);
            Sweep(x, Ground - 30, 0, 50, 1.4f);
        }

        /// A word pressed into the snow; it is gone once the snow under it is swept away.
        public void Carve(string word, float screenX)
        {
            carved = word;
            carvedX = Math.Max(120, Math.Min(W - 120, screenX - scr.X));
            snowLo = 0;
            snowHi = snow.Length - 1;
        }

        void RedrawHeap(int x0, int x1)
        {
            var g = heapG;
            var clip = new Rectangle(x0, 0, x1 - x0, heapImg.Height);
            g.SetClip(clip);
            g.Clear(Color.Transparent);
            if (anySnow) DrawSnow(g, x0, x1);
            foreach (var l in heap)
                if (l.X > x0 - 12 && l.X < x1 + 12) DrawLeaf(g, l.X, l.Y - HeapTop, l.Rot, l.Size, l.C, 255);
            g.ResetClip();
            Mark(new Rectangle(x0, HeapTop, x1 - x0, heapImg.Height));
        }

        // ---------- drawing ----------

        /// A changed spot. Spots close together are joined; far apart ones go separately (sending one rectangle over
        /// the whole screen would cost as much as a full frame).
        void Mark(Rectangle r) { Join(dirty, r, 3000, 28); }

        /// Adds r to the list, joined with the rectangle it grows least if that adds under `slack` pixels.
        static void Join(List<Rectangle> dirty, Rectangle r, long slack, int most)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            int best = -1;
            long bestGrow = long.MaxValue;
            for (int i = 0; i < dirty.Count; i++)
            {
                var u = Rectangle.Union(dirty[i], r);
                long grow = (long)u.Width * u.Height - (long)dirty[i].Width * dirty[i].Height - (long)r.Width * r.Height;
                if (grow < bestGrow) { bestGrow = grow; best = i; }
            }
            if (best >= 0 && (bestGrow < slack || dirty.Count >= most)) dirty[best] = Rectangle.Union(dirty[best], r);
            else dirty.Add(r);
        }

        void Render()
        {
            foreach (var b in bits)
            {
                int s = (int)(b.C >= 0 ? b.Size : b.Size + 2) + 3;
                var now = b.Life > 0 ? new Rectangle((int)b.X - s, (int)b.Y - s, s * 2, s * 2) : Rectangle.Empty;
                Mark(b.Was.IsEmpty ? now : now.IsEmpty ? b.Was : Rectangle.Union(b.Was, now));
                b.Was = now;
            }
            bits.RemoveAll(b => b.Life <= 0);
            if (snowHi >= 0)
            {
                RedrawHeap(snowLo * Col - 6, (snowHi + 1) * Col + 6);
                snowLo = int.MaxValue;
                snowHi = -1;
            }
            var g = surface.G;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var screenBox = new Rectangle(0, 0, W, H);
            var list = dirty.ToArray();
            dirty.Clear();
            foreach (var d in list)
            {
                var r = Rectangle.Intersect(d, screenBox);
                if (r.IsEmpty) continue;
                g.SetClip(r);
                g.Clear(Color.Transparent);
                var part = Rectangle.Intersect(r, new Rectangle(0, HeapTop, W, heapImg.Height));
                if (!part.IsEmpty && (anySnow || heap.Count > 0))
                    g.DrawImage(heapImg, part, new Rectangle(part.X, part.Y - HeapTop, part.Width, part.Height), GraphicsUnit.Pixel);   // only the part needed
                foreach (var b in bits)
                {
                    if (b.Y < r.Top - 30 || b.Y > r.Bottom + 30 || b.X < r.Left - 30 || b.X > r.Right + 30) continue;
                    int a = (int)(255 * Math.Max(0, Math.Min(1, b.Life)));
                    if (b.C >= 0) DrawLeaf(g, b.X, b.Y, b.Rot, b.Size, b.C, a, b.Down ? 1 : (float)Math.Cos(t * 2.6 + b.Ph));   // tumbling: seen edge-on now and then
                    else using (var br = new SolidBrush(Color.FromArgb(a * 230 / 255, 255, 255, 255))) g.FillEllipse(br, b.X - b.Size, b.Y - b.Size, b.Size * 2, b.Size * 2);
                }
                g.ResetClip();
                Join(send, r, 200000, 3);
            }
            // sending costs ~0.15 ms a call plus a little per pixel: a few wider rectangles beat many small ones
            foreach (var r in send) surface.Present(Handle, scr.X, scr.Y, r);
            send.Clear();
        }

        void DrawLeaf(Graphics g, float x, float y, float rot, float size, int c, int alpha, float flip = 1)
        {
            var st = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(rot);
            g.ScaleTransform(size, size * Math.Max(0.18f, Math.Abs(flip)));
            var col = LeafColors[c];
            using (var br = new SolidBrush(Color.FromArgb(alpha, col))) g.FillPath(br, leafShape);
            using (var p = new Pen(Color.FromArgb(alpha * 150 / 255, col.R * 2 / 3, col.G * 2 / 3, col.B * 2 / 3), 0.07f)) g.DrawLine(p, -0.62f, 0, 0.42f, 0);
            g.Restore(st);
        }

        /// The snow between x0 and x1 into the heap picture (its y is the screen's minus HeapTop).
        void DrawSnow(Graphics g, int x0, int x1)
        {
            int c0 = Math.Max(0, x0 / Col - 6), c1 = Math.Min(snow.Length - 1, x1 / Col + 6), gy = Ground - HeapTop;
            var pts = new List<PointF>();
            for (int i = c0; i <= c1; i += 3) pts.Add(new PointF(i * Col, gy - snow[i]));
            if (pts.Count < 2) return;
            using (var p = new GraphicsPath())
            {
                p.AddCurve(pts.ToArray(), 0.4f);
                var last = pts[pts.Count - 1];
                p.AddLine(last.X, last.Y, last.X, gy + 1);
                p.AddLine(last.X, gy + 1, pts[0].X, gy + 1);
                p.CloseFigure();
                using (var br = new LinearGradientBrush(new Rectangle(0, gy - MaxSnow - 1, 10, MaxSnow + 2), Color.FromArgb(250, 252, 255), Color.FromArgb(214, 226, 242), 90f))
                    g.FillPath(br, p);
                using (var pen = new Pen(Color.FromArgb(120, 190, 205, 228), 1.2f)) g.DrawCurve(pen, pts.ToArray(), 0.4f);
                if (carved == null || carvedX < x0 - 150 || carvedX > x1 + 150) return;
                var st = g.Save();
                g.SetClip(p, CombineMode.Intersect);
                float depth = Around(carvedX + scr.X, 60), hgt = Math.Max(16, Math.Min(40, depth * 0.8f));
                using (var f = new Font("Segoe UI", hgt, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    var size = g.MeasureString(carved, f);
                    float x = carvedX - size.Width / 2, y = gy - depth * 0.5f - size.Height / 2;
                    using (var light = new SolidBrush(Color.FromArgb(255, 255, 255))) g.DrawString(carved, f, light, x + 1, y + 1);
                    using (var dark = new SolidBrush(Color.FromArgb(230, 112, 140, 186))) g.DrawString(carved, f, dark, x, y);
                }
                g.Restore(st);
            }
        }

        /// Checks: a frame of the heap after a while away, saved as a picture.
        public Bitmap Shot()
        {
            dirty.Clear();
            dirty.Add(new Rectangle(0, 0, W, H));
            Render();
            return surface.Bitmap;
        }

        /// `Kotik.exe --weathertest leaves|snow out.png`: 15 minutes away, then the mouse sweeps a stretch and the cat
        /// dives; saves the bottom of the screen and the frame times (out.png.txt).
        public static void Test(bool isSnow, string png)
        {
            var w = new Weather { Kind = isSnow ? Snow : Leaves, Away = true };
            var log = new System.Text.StringBuilder();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var cat = new PointF(w.scr.X + w.W / 2, w.scr.Y + w.Ground);
            var off = new Point(w.scr.X + 5, w.scr.Y + 5);
            for (int i = 0; i < 9000; i++) w.Step(0.1f, off, cat, false, 1);
            log.AppendLine(string.Format("away 15 min: {0} ms per step, bits {1}, heap {2}, snow avg {3:0.0}, leaves avg {4:0.0}",
                sw.Elapsed.TotalMilliseconds / 9000, w.bits.Count, w.heap.Count, Avg(w.snow), Avg(w.leafH)));
            w.Away = false;
            for (int i = 0; i < 300; i++) w.Step(0.1f, off, cat, false, 1);       // what was falling lands and fades
            sw.Restart();
            for (int i = 0; i < 300; i++) w.Step(1 / 60f, off, cat, false, 1);
            log.AppendLine(string.Format("user back, nothing touched: {0:0.000} ms per 60 fps step, bits {1}", sw.Elapsed.TotalMilliseconds / 300, w.bits.Count));
            int heapBefore = w.heap.Count;
            sw.Restart();
            for (int i = 0; i < 120; i++)
                w.Step(1 / 60f, new Point(w.scr.X + 200 + i * 4, w.scr.Y + w.Ground - 10), cat, false, 1);   // the mouse along the heap, 480 px
            log.AppendLine(string.Format("sweeping: {0:0.000} ms per step, heap {1} -> {2}, bits {3}", sw.Elapsed.TotalMilliseconds / 120, heapBefore, w.heap.Count, w.bits.Count));
            for (int i = 0; i < 60; i++) w.Step(1 / 60f, off, new PointF(cat.X + i * 2, cat.Y), true, 1);   // the cat walks through
            w.Burst(w.scr.X + w.W * 0.75f);
            for (int i = 0; i < 20; i++) w.Step(1 / 60f, off, cat, false, 1);
            L.Load();
            if (isSnow) w.Carve(L.T("ПРИВЕТ", "HI"), w.scr.X + w.W * 0.86f);
            var shot = w.Shot();
            using (var bmp = new Bitmap(w.W, 260))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(46, 52, 64));
                g.FillRectangle(Brushes.Black, 0, w.Ground - (w.H - 260), w.W, w.H - w.Ground);
                g.DrawImage(shot, new Rectangle(0, 0, w.W, 260), new Rectangle(0, w.H - 260, w.W, 260), GraphicsUnit.Pixel);
                bmp.Save(png);
            }
            System.IO.File.WriteAllText(png + ".txt", log.ToString());
        }

        static float Avg(float[] a) { float s = 0; foreach (var v in a) s += v; return s / a.Length; }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { heapG.Dispose(); heapImg.Dispose(); leafShape.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
