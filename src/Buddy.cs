using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Kotik
{
    /// Ember («Огонёк»): a small amber spark that floats near the top of the screen. It thinks before it speaks (and shows
    /// what it is "doing" meanwhile), gives tips, chats with the cat and dodges when the cat hunts it.
    class Buddy : Form
    {
        const int W = 320, H = 220, AX = 160, AY = 150;      // the centre of the body on the canvas
        readonly Bitmap canvas = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppArgb);   // only for previews
        readonly Surface surface = new Surface(W, H);
        readonly Brain cat;
        readonly Action<string> catSay;
        readonly Random rnd = new Random();
        public float X, Y;
        float vx, vy, t, spin, blinkT = 3, blinking, happyT, thinkT, sayT, dodgeT;
        PointF home;
        float homeT;
        string thinking = "", saying = "", queued = "";
        string catReply = "";
        float catReplyT;
        float visitT = 60;
        bool visiting;
        bool pressing, dragging;
        Point pressAt;
        public bool Asleep;
        public float Fireworks;                // New Year: bursts of sparks around it
        public RectangleF? Box;                // the ball game: walls to bounce between
        public enum Mood { Calm, Happy, Curious, Sleepy, Excited, Focused }
        public Mood Feel = Mood.Calm;
        public Action<Mood> MoodChanged = delegate { };
        public bool UserTyping;                // set by the cat's window: you are typing right now
        public bool Dancing;                   // music plays (Spotify)
        public float Beat;                     // 0..1, the loudness right now
        float moodT = 40, animT = 25, anim, winkT, lookT, hopT;
        int animKind;

        /// Changes the mood for a while (an event: a click, a ride, a fixed bug…). The cat feels it too.
        public void SetMood(Mood m, float secs)
        {
            moodT = secs;
            if (m == Feel) return;
            Feel = m;
            MoodChanged(m);
        }

        void UpdateMood(float dt)
        {
            moodT -= dt;
            if (Asleep) { if (Feel != Mood.Sleepy) SetMood(Mood.Sleepy, 30); return; }
            if (UserTyping && Feel != Mood.Focused && Feel != Mood.Happy && Feel != Mood.Excited) { SetMood(Mood.Focused, 20); return; }
            if (moodT > 0) return;
            if (UserTyping) { SetMood(Mood.Focused, 20); return; }
            double r = rnd.NextDouble();
            int h = DateTime.Now.Hour;
            Mood next = r < 0.35 ? Mood.Calm : r < 0.6 ? Mood.Happy : r < 0.8 ? Mood.Curious : r < 0.9 ? Mood.Excited : Mood.Sleepy;
            if ((h >= 23 || h < 6) && r < 0.5) next = Mood.Sleepy;
            SetMood(next, 60 + (float)rnd.NextDouble() * 90);
        }

        /// Small things it does now and then: a somersault, a hop, a wink, a look around.
        void UpdateAnim(float dt)
        {
            if (anim > 0) anim -= dt;
            if (winkT > 0) winkT -= dt;
            if (lookT > 0) lookT -= dt;
            if (hopT > 0) hopT -= dt;
            if ((animT -= dt) > 0 || Busy || Asleep) return;
            animT = (Feel == Mood.Excited ? 10 : Feel == Mood.Sleepy ? 60 : 25) + (float)rnd.NextDouble() * 25;
            animKind = rnd.Next(4);
            if (animKind == 0) anim = 0.9f;                         // a somersault of the rays
            else if (animKind == 1) { vy -= 260; hopT = 0.6f; }     // a hop
            else if (animKind == 2) winkT = 0.45f;                  // a wink
            else lookT = 1.6f;                                      // a look around
        }

        public Finding Pending, Last;          // a noticed problem in the code (waits for a click) and the last one shown
        public Action<Finding> ShowCode = delegate { };
        public Action<Finding, int> Answer = delegate { };   // 0 a real bug, 1 intended, 2 do not check this file
        float pointT;
        float stepT, rideT, carryT, rideTalkT;
        public bool Riding { get { return rideT > 0 || carryT > 0; } }
        PointF stepAt;
        public bool Carrying { get { return carryT > 0; } }

        /// Hovers at a point as a step for the cat (the top of its body is the point).
        public bool BeStep(PointF at)
        {
            if (ballT > 0 || pointT > 0 || carryT > 0 || dragging) return false;
            stepAt = at;
            stepT = 6;
            rideT = 0;
            Think(Lines.Pick(Lines.StepUp));
            return true;
        }

        /// Sits on the cat's back for a while.
        public void RideCat(float secs) { if (carryT <= 0 && stepT <= 0) { rideT = secs; visiting = false; rideTalkT = 8; Think(Lines.Pick(Lines.Ride)); } }

        /// Gives the cat a ride: flies slowly in a small loop with the cat on top.
        public void CarryCat(float secs) { SetMood(Mood.Excited, secs + 20); carryT = secs; rideT = 0; stepT = 0; visiting = false; rideTalkT = 6; carryAt = new PointF(X, Y); Think(Lines.Pick(Lines.Carry)); }
        PointF carryAt;
        float ballT;
        public bool Playing { get { return ballT > 0; } }
        public bool Free;                      // on the desktop or in a code editor it may fly around
        PointF roam;
        static readonly Font ThinkFont = new Font("Segoe UI", 9.5f, FontStyle.Italic), SayFont = new Font("Segoe UI", 10f),
            ZFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        Point lastCur;

        public Buddy(Brain cat, Action<string> catSay)
        {
            this.cat = cat;
            this.catSay = catSay;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "Ember";
            var wa = Screen.PrimaryScreen.WorkingArea;
            X = wa.Right - 260;
            Y = wa.Top - 60;
            NewHome();
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

        PointF perch = new PointF(80, -130);   // where to hang around, relative to the cat
        DateTime pinnedUntil = DateTime.MinValue;

        /// Ember keeps close to the cat (not over videos or text in the middle of the screen) and changes its perch now
        /// and then. If you drag it somewhere, it stays there for ten minutes.
        void NewHome()
        {
            float side = rnd.NextDouble() < 0.5 ? -1 : 1;
            perch = new PointF(side * (70 + (float)rnd.NextDouble() * 50), -(115 + (float)rnd.NextDouble() * 40));
            homeT = 20 + (float)rnd.NextDouble() * 25;
            var wa = Screen.PrimaryScreen.WorkingArea;
            var cur = Cursor.Position;
            for (int k = 0; k < 10; k++)
            {
                roam = new PointF(wa.Left + 120 + (float)rnd.NextDouble() * (wa.Width - 240), wa.Top + 160 + (float)rnd.NextDouble() * (wa.Height - 320));
                if (Math.Abs(roam.X - cur.X) + Math.Abs(roam.Y - cur.Y) > 400) break;
            }
        }

        PointF Home()
        {
            if (DateTime.Now < pinnedUntil) return home;
            if (Free && !visiting && perch.X > 0) return roam;   // half of the time it flies, half it stays with the cat
            var wa = Screen.FromPoint(new Point((int)cat.X, (int)cat.Y)).WorkingArea;
            float hx = Math.Max(wa.Left + 60, Math.Min(wa.Right - 60, cat.X + perch.X));
            float hy = Math.Max(wa.Top + 150, cat.Y + perch.Y);
            return new PointF(hx, hy);
        }

        /// Says something after a short "thinking" moment.
        public void Think(string phrase) { Think(phrase, false); }
        // explaining a problem in the code: the bubble stays 15–45 s depending on the length of the text,
        // and does not go away while the mouse is over Ember
        bool slowNext, sayingSlow;
        public bool HoverHold;                          // PetForm: the cursor is over Ember
        public void Think(string phrase, bool slow)
        {
            if (string.IsNullOrEmpty(phrase) || thinkT > 0 || sayT > 0) return;
            slowNext = slow;
            int d = DaysToNewYear();
            try { queued = string.Format(phrase, Settings.Owner, Settings.Name, d, PetForm.Days(d)); } catch (FormatException) { queued = phrase; }
            queued = Capital(queued);
            thinking = Lines.Pick(Lines.Gerunds);
            thinkT = 1.4f + (float)rnd.NextDouble() * 1.2f;
        }

        /// "друг, привет" → "Друг, привет": a name put at the start of a phrase.
        public static string Capital(string s) { return s.Length > 0 && char.IsLower(s[0]) ? char.ToUpper(s[0]) + s.Substring(1) : s; }

        public static int DaysToNewYear()
        {
            return (int)(new DateTime(DateTime.Today.Year + 1, 1, 1) - DateTime.Today).TotalDays;
        }

        public void Step(float dt, float userIdle)
        {
            Move(dt, userIdle);
            if (stepT > 0 || carryT > 0) cat.Step = new PointF(X, Y - 18);
        }

        /// Sitting on the cat: called after the cat moved this frame, so Ember does not trail behind.
        public void Follow()
        {
            if (rideT > 0) { X = cat.X - cat.Dir * 4; Y = cat.Y - 70 + (float)Math.Sin(t * 8) * 1.5f; }
        }

        void Move(float dt, float userIdle)
        {
            t += dt;
            homeT -= dt;
            happyT -= dt;
            dodgeT -= dt;
            visitT -= dt;
            Asleep = userIdle > 240;
            UpdateMood(dt);
            UpdateAnim(dt);
            float spinRate = thinkT > 0 ? 3.2f : Feel == Mood.Excited ? 1.6f : Feel == Mood.Sleepy ? 0.12f : Feel == Mood.Focused ? 0.2f : 0.35f;
            if (anim > 0 && animKind == 0) spinRate = 9;
            if (Dancing) spinRate += Beat * 2.5f;
            spin += dt * spinRate;
            if (Fireworks > 0) Fireworks -= dt * 0.02f;
            if (pointT > 0) pointT -= dt;
            if (stepT > 0) { stepT -= dt; if (stepT <= 0) cat.Step = null; }
            if (rideT > 0) rideT -= dt;
            if (Riding && thinkT <= 0 && sayT <= 0 && catReplyT <= 0 && (rideTalkT -= dt) <= 0)
            {
                rideTalkT = 7 + (float)rnd.NextDouble() * 5;
                Think(Lines.Pick(carryT > 0 ? Lines.CarryTalk : Lines.RideTalk));
                catReply = Talk.Pick(carryT > 0 ? Talk.Ride : Talk.Carried);
                catReplyT = 5;
            }
            if (carryT > 0)
            {
                carryT -= dt;
                if (carryT <= 0) { cat.Step = null; cat.StopRiding(); Think(Lines.Pick(Lines.CarryEnd)); }
            }
            if (thinkT > 0)
            {
                thinkT -= dt;
                if (thinkT <= 0)
                {
                    saying = queued;
                    sayingSlow = slowNext; slowNext = false;
                    sayT = sayingSlow ? Math.Max(15f, Math.Min(45f, 6f + saying.Length * 0.15f)) : Math.Min(10f, 3.5f + saying.Length * 0.07f);
                }
            }
            else if (sayT > 0 && !(sayingSlow && HoverHold)) sayT -= dt;
            if (catReplyT > 0)
            {
                catReplyT -= dt;
                if (catReplyT <= 0 && catReply != "") { catSay(catReply); catReply = ""; }
            }
            if (homeT <= 0) NewHome();
            // where to float: home, or above the cat for a chat
            PointF target = Home();
            if (stepT > 0) target = new PointF(stepAt.X, stepAt.Y + 18);
            else if (rideT > 0) { X = cat.X - cat.Dir * 4; Y = cat.Y - 70 + (float)Math.Sin(t * 8) * 1.5f; vx = vy = 0; }
            else if (carryT > 0)
            {
                var wa = Screen.FromPoint(new Point((int)carryAt.X, (int)carryAt.Y)).WorkingArea;
                float lx = Math.Max(wa.Left + 300, Math.Min(wa.Right - 300, carryAt.X)), ly = wa.Bottom - 260;
                target = new PointF(lx + 240 * (float)Math.Cos(t * 0.9f), ly + 90 * (float)Math.Sin(t * 1.8f));
            }
            if (!Asleep && visitT <= 0 && cat.On != null && cat.Awake && !dragging && !Riding && stepT <= 0)
            {
                visiting = true;
                visitT = 240 + (float)rnd.NextDouble() * 300;
                var pair = Lines.Chats[rnd.Next(Lines.Chats.Length)];
                Think(pair[0]);
                catReply = pair[1];
                catReplyT = 5.5f;
            }
            if (visiting)
            {
                target = new PointF(cat.X + cat.Dir * 60, Math.Max(Screen.PrimaryScreen.WorkingArea.Top + 150, cat.Y - 130));
                if (sayT <= 0 && thinkT <= 0 && catReplyT <= 0) { visiting = false; NewHome(); }
            }
            if (Asleep) target = new PointF(target.X, target.Y + 20);
            // keep out of the way of the mouse
            var cur = Cursor.Position;
            float mx = X - cur.X, my = Y - cur.Y;
            float md = (float)Math.Sqrt(mx * mx + my * my);
            // only a fast pass of the mouse makes it dodge; a slow approach is welcome (you can click it)
            float cspeed = (float)Math.Sqrt(Math.Pow(cur.X - lastCur.X, 2) + Math.Pow(cur.Y - lastCur.Y, 2)) / Math.Max(dt, 0.001f);
            lastCur = cur;
            if (md < 80 && cspeed > 1500 && !pressing && md > 1 && carryT <= 0 && stepT <= 0) { vx += mx / md * 500 * dt; vy += my / md * 500 * dt; }
            // the cat hunts it: dodge upwards
            float cx = cat.X - X, cy = (cat.Y - 30) - Y;
            if ((cat.Act == Act.Jump || cat.Act == Act.Crouch) && Math.Abs(cx) < 140 && Math.Abs(cy) < 160 && dodgeT <= 0 && stepT <= 0 && carryT <= 0)
            {
                vy -= 700;
                vx += cx > 0 ? -250 : 250;
                dodgeT = 2;
                if (rnd.NextDouble() < 0.5) Think(Lines.Pick(Lines.Dodge));
            }
            if (ballT > 0 && Box.HasValue && !dragging)
            {
                // a bouncing ball inside a quiet part of a window
                ballT -= dt;
                var b = Box.Value;
                vy += 1500 * dt;
                X += vx * dt;
                Y += vy * dt;
                spin += vx * dt * 0.03f;
                if (X < b.Left + 26) { X = b.Left + 26; vx = Math.Abs(vx) * 0.9f; }
                if (X > b.Right - 26) { X = b.Right - 26; vx = -Math.Abs(vx) * 0.9f; }
                if (Y < b.Top + 26) { Y = b.Top + 26; vy = Math.Abs(vy) * 0.85f; }
                if (Y > b.Bottom - 26)
                {
                    Y = b.Bottom - 26;
                    vy = -Math.Abs(vy) * 0.86f;
                    if (Math.Abs(vy) < 380) vy = -(700 + (float)rnd.NextDouble() * 400);   // keeps on jumping
                    vx *= 0.98f;
                    if (Math.Abs(vx) < 120) vx = (rnd.NextDouble() < 0.5 ? -1 : 1) * (300 + (float)rnd.NextDouble() * 300);
                }
                if (ballT <= 0) { Box = null; NewHome(); }
            }
            else if (rideT > 0)
            {
                // sitting on the cat: position already set above
            }
            else if (dragging)
            {
                X = cur.X;
                Y = cur.Y;
                vx = vy = 0;
            }
            else
            {
                // a soft spring towards the target, plus a gentle bob
                float ax = (target.X - X) * 1.6f, ay = (target.Y + (float)Math.Sin(t * 1.3f) * 10 - Y) * 1.6f;
                vx += ax * dt;
                vy += ay * dt;
                vx *= (float)Math.Pow(0.18, dt);
                vy *= (float)Math.Pow(0.18, dt);
                X += vx * dt;
                Y += vy * dt;
            }
            blinkT -= dt;
            if (blinkT <= 0) { blinking = 0.14f; blinkT = 2.5f + (float)rnd.NextDouble() * 4; }
            blinking -= dt;
        }

        // ---------- drawing ----------

        /// Thinking, talking or being dragged: draw every frame.
        public bool Busy { get { return thinkT > 0 || sayT > 0 || dragging || happyT > 0 || ballT > 0 || pointT > 0 || stepT > 0 || rideT > 0 || carryT > 0; } }

        public void Render()
        {
            var g = surface.G;
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                DrawBody(g);
                if (thinkT > 0) Bubble(g, thinking, true);
                else if (sayT > 0) Bubble(g, saying, false);
            }
            int bob = Dancing && !Busy ? (int)(Beat * 12) : 0, sway = Dancing && !Busy ? (int)(Math.Sin(t * 3.2) * 6) : 0;   // dancing to the music
            surface.Present(Handle, (int)X - AX + sway, (int)Y - AY - bob);
        }

        void DrawBody(Graphics g)
        {
            if (Fireworks > 0)
            {
                // three bursts that bloom and fade in turn
                Color[] cols = { Color.FromArgb(255, 220, 90), Color.FromArgb(120, 220, 255), Color.FromArgb(255, 120, 170) };
                for (int k = 0; k < 3; k++)
                {
                    float ph = (t * 0.6f + k / 3f) % 1f;
                    float cx = AX + (float)Math.Sin(k * 2.1 + Math.Floor(t * 0.6f + k / 3f)) * 90, cy = AY - 60 + k * 14;
                    using (var b = new SolidBrush(Color.FromArgb((int)(255 * (1 - ph)), cols[k])))
                        for (int i = 0; i < 12; i++)
                        {
                            double a = i * Math.PI / 6;
                            g.FillEllipse(b, cx + (float)Math.Cos(a) * ph * 46 - 2, cy + (float)Math.Sin(a) * ph * 46 - 2, 4, 4);
                        }
                }
            }
            bool gold = Calendar.On("xmas");
            if (pointT > 0)
            {
                // an arrow to the line of code
                float bounce = (float)Math.Sin(t * 6) * 4;
                using (var pen = new Pen(Color.FromArgb(230, 245, 166, 35), 4) { EndCap = LineCap.ArrowAnchor, StartCap = LineCap.Round })
                    g.DrawLine(pen, AX - 30 + bounce, AY, AX - 110 + bounce, AY);
            }
            var amber = gold ? Color.FromArgb(255, 250, 215, 90) : Color.FromArgb(Asleep ? 190 : 255, 255, 176, 59);
            var tip = gold ? Color.FromArgb(255, 255, 240, 170) : Color.FromArgb(Asleep ? 170 : 235, 255, 214, 110);
            var line = gold ? Color.FromArgb(170, 120, 30) : Color.FromArgb(178, 96, 18);
            if (Calendar.On("halloween") && Season.Today.Day >= 31 || Calendar.On("halloween") && Season.Today.Month == 11)
            {
                DrawGhost(g);
                return;
            }
            // glow
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(AX - 46, AY - 46, 92, 92);
                using (var b = new PathGradientBrush(path) { CenterColor = Color.FromArgb(Asleep ? 30 : 70, 255, 170, 120), SurroundColors = new[] { Color.FromArgb(0, 255, 170, 120) } })
                    g.FillPath(b, path);
            }
            // flames: twelve pointed tongues around the centre, long and short in turn, flickering
            for (int i = 0; i < 12; i++)
            {
                float a = spin * 0.6f + i * (float)Math.PI / 6;
                float len = (i % 2 == 0 ? 29 : 21) + (float)Math.Sin(t * 5 + i * 2.3) * 2.2f + (happyT > 0 ? 3 : 0);
                if (Feel == Mood.Happy) len += 2;
                else if (Feel == Mood.Sleepy) len -= 4;
                else if (Feel == Mood.Curious) len += (float)Math.Sin(t * 5 + i) * 3;
                else if (Feel == Mood.Excited) len += 3 + (float)Math.Sin(t * 14 + i * 2) * 2.5f;
                if (Dancing) len += Beat * 7;
                float w = i % 2 == 0 ? 0.28f : 0.22f;
                var flame = new[]
                {
                    new PointF(AX + (float)Math.Cos(a - w) * 13, AY + (float)Math.Sin(a - w) * 13),
                    new PointF(AX + (float)Math.Cos(a) * len, AY + (float)Math.Sin(a) * len),
                    new PointF(AX + (float)Math.Cos(a + w) * 13, AY + (float)Math.Sin(a + w) * 13),
                };
                using (var b = new SolidBrush(i % 2 == 0 ? amber : tip)) g.FillPolygon(b, flame);
                using (var pen = new Pen(line, 1.6f) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, flame);
            }
            using (var b = new SolidBrush(amber)) g.FillEllipse(b, AX - 17, AY - 17, 34, 34);
            using (var pen = new Pen(line, 2)) g.DrawEllipse(pen, AX - 17, AY - 17, 34, 34);
            using (var b = new SolidBrush(Color.FromArgb(Asleep ? 60 : 110, 255, 240, 200))) g.FillEllipse(b, AX - 10, AY - 13, 12, 8);   // a highlight
            // face: looks at the cat or the mouse
            var cur = Cursor.Position;
            float lx = cur.X - X, ly = cur.Y - Y;
            if (visiting) { lx = cat.X - X; ly = cat.Y - Y; }
            float ld = Math.Max(1, (float)Math.Sqrt(lx * lx + ly * ly));
            float ex = lx / ld * 2.2f, ey = ly / ld * 2.2f;
            if (lookT > 0) { ex = (float)Math.Sin(lookT * 4) * 2.6f; ey = -0.5f; }     // looking around
            var eye = Color.FromArgb(50, 26, 18);
            bool happyEyes = happyT > 0 || Feel == Mood.Happy;
            using (var pen = new Pen(eye, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var b = new SolidBrush(eye))
            {
                if (Asleep || blinking > 0 || happyEyes && Feel != Mood.Sleepy)
                {
                    float up = happyEyes ? -1 : 1;
                    g.DrawArc(pen, AX - 9 + ex, AY - 6 + ey, 7, 5, up > 0 ? 10 : 190, 160);
                    g.DrawArc(pen, AX + 2 + ex, AY - 6 + ey, 7, 5, up > 0 ? 10 : 190, 160);
                }
                else if (Feel == Mood.Sleepy)
                {
                    g.DrawLine(pen, AX - 8 + ex, AY - 3 + ey, AX - 3 + ex, AY - 3 + ey);   // heavy eyelids
                    g.DrawLine(pen, AX + 3 + ex, AY - 3 + ey, AX + 8 + ex, AY - 3 + ey);
                }
                else
                {
                    float big = Feel == Mood.Excited ? 1.5f : 0;
                    float lb = Feel == Mood.Curious ? 1.3f : 0;            // curious: one eye a bit bigger
                    if (winkT > 0) g.DrawLine(pen, AX - 8 + ex, AY - 3 + ey, AX - 3 + ex, AY - 3 + ey);
                    else g.FillEllipse(b, AX - 8 + ex - big / 2, AY - 7 + ey - big / 2, 5 + big, 7 + big);
                    g.FillEllipse(b, AX + 3 + ex - (big + lb) / 2, AY - 7 + ey - (big + lb) / 2, 5 + big + lb, 7 + big + lb);
                }
                // focused or thinking: a straight mouth
                if (thinkT > 0 || Feel == Mood.Focused) g.DrawLine(pen, AX - 3 + ex, AY + 6 + ey, AX + 3 + ex, AY + 6 + ey);   // a thinking face
                else if (Feel == Mood.Excited) g.FillEllipse(b, AX - 3 + ex, AY + 3 + ey, 6, 5);                   // an open "o"
                else if (Feel == Mood.Sleepy) g.DrawArc(pen, AX - 3 + ex, AY + 3 + ey, 6, 3, 20, 140);
                else g.DrawArc(pen, AX - 4 + ex, AY + 2 + ey, 8, 5, 20, 140);
            }
            if (Feel == Mood.Curious && !Asleep && (t % 6) < 2)
                using (var b = new SolidBrush(Color.FromArgb(200, 255, 230, 200)))
                    g.DrawString("?", ZFont, b, AX + 20, AY - 40 - (t % 6) * 3);
            if (Feel == Mood.Excited && !Asleep)
                using (var b = new SolidBrush(Color.FromArgb(220, 255, 230, 140)))
                    for (int i = 0; i < 4; i++)
                    {
                        double a = t * 1.5 + i * Math.PI / 2;
                        float sx = AX + (float)Math.Cos(a) * 40, sy = AY + (float)Math.Sin(a) * 40, s = 2.5f + (float)Math.Sin(t * 8 + i) * 1.5f;
                        g.FillPolygon(b, new[] { new PointF(sx, sy - s * 2), new PointF(sx + s / 2, sy), new PointF(sx, sy + s * 2), new PointF(sx - s / 2, sy) });
                    }
            if (happyT > 0 || Feel == Mood.Happy)
                using (var b = new SolidBrush(Color.FromArgb(120, 255, 120, 130)))
                {
                    g.FillEllipse(b, AX - 14, AY + 1, 6, 4);
                    g.FillEllipse(b, AX + 8, AY + 1, 6, 4);
                }
            if (Asleep)
                using (var b = new SolidBrush(Color.FromArgb(160, 255, 200, 170)))
                    g.DrawString("z", ZFont, b, AX + 18, AY - 34 - (t * 8) % 14);
        }

        /// Halloween: Ember under a white sheet, with two holes for the eyes.
        void DrawGhost(Graphics g)
        {
            float bob = (float)Math.Sin(t * 2) * 3;
            using (var p = new GraphicsPath())
            {
                p.AddArc(AX - 22, AY - 30 + bob, 44, 44, 180, 180);
                p.AddLine(AX + 22, AY - 8 + bob, AX + 22, AY + 22 + bob);
                for (int i = 0; i < 4; i++)
                {
                    float x0 = AX + 22 - i * 11;
                    p.AddBezier(x0, AY + 22 + bob, x0 - 3, AY + 28 + bob + (float)Math.Sin(t * 4 + i) * 2, x0 - 8, AY + 28 + bob, x0 - 11, AY + 22 + bob);
                }
                p.CloseFigure();
                using (var b = new SolidBrush(Color.FromArgb(235, 250, 250, 255))) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(180, 160, 170, 190), 1.6f)) g.DrawPath(pen, p);
            }
            using (var b = new SolidBrush(Color.FromArgb(40, 30, 50)))
            {
                g.FillEllipse(b, AX - 10, AY - 10 + bob, 7, 10);
                g.FillEllipse(b, AX + 3, AY - 10 + bob, 7, 10);
                g.FillEllipse(b, AX - 4, AY + 4 + bob, 8, 6);   // "oooo"
            }
        }

        void Bubble(Graphics g, string text, bool think)
        {
            var f = think ? ThinkFont : SayFont;
            {
                var size = g.MeasureString(text, f, 250);
                float bw = size.Width + 22, bh = size.Height + 14;
                float bx = Math.Max(4, Math.Min(W - bw - 4, AX - bw / 2));
                float by = Math.Max(4, AY - 44 - bh);
                using (var path = new GraphicsPath())
                {
                    float r = 12;
                    path.AddArc(bx, by, r * 2, r * 2, 180, 90);
                    path.AddArc(bx + bw - r * 2, by, r * 2, r * 2, 270, 90);
                    path.AddArc(bx + bw - r * 2, by + bh - r * 2, r * 2, r * 2, 0, 90);
                    path.AddArc(bx, by + bh - r * 2, r * 2, r * 2, 90, 90);
                    path.CloseFigure();
                    var fill = think ? Color.FromArgb(235, 255, 241, 232) : Color.FromArgb(245, 255, 250, 245);
                    using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                    using (var pen = new Pen(Color.FromArgb(200, 230, 150, 50), 1.6f)) g.DrawPath(pen, path);
                }
                if (think)
                    for (int i = 0; i < 2; i++)
                        using (var b = new SolidBrush(Color.FromArgb(220, 255, 241, 232)))
                            g.FillEllipse(b, AX - 4 + i * 6, by + bh + 4 + i * 8, 7 - i * 2, 7 - i * 2);
                using (var b = new SolidBrush(think ? Color.FromArgb(170, 90, 60) : Color.FromArgb(60, 40, 34)))
                    g.DrawString(text, f, b, new RectangleF(bx + 11, by + 7, size.Width + 1, size.Height + 4));
            }
        }

        // ---------- the mouse ----------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) { pressing = true; return; }
            pressing = true;
            pressAt = Cursor.Position;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (pressing && !dragging && (Math.Abs(Cursor.Position.X - pressAt.X) > 6 || Math.Abs(Cursor.Position.Y - pressAt.Y) > 6))
            {
                dragging = true;
                rideT = 0;
                stepT = 0;
                if (carryT > 0) { carryT = 0; cat.Step = null; cat.StopRiding(); }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            Capture = false;
            if (!pressing) return;
            pressing = false;
            if (dragging && e.Button == MouseButtons.Left)
            {
                dragging = false;
                home = new PointF(X, Y);
                pinnedUntil = DateTime.Now.AddMinutes(10);
                Think(L.T("Хорошее место. Побуду тут немного.", "Nice spot. I'll stay here a while."));
                return;
            }
            if (e.Button == MouseButtons.Right)
            {
                if (Last == null && Pending == null) return;
                var f = Pending ?? Last;
                var m = new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer(new DarkColors()), ForeColor = Color.FromArgb(235, 230, 220) };
                m.Items.Add(new ToolStripMenuItem(L.T("Да, это ошибка — спасибо", "Yes, it's a bug — thanks"), null, delegate { Answer(f, 0); }));
                m.Items.Add(new ToolStripMenuItem(L.T("Нет, так и задумано", "No, it's intended"), null, delegate { Answer(f, 1); }));
                m.Items.Add(new ToolStripMenuItem(L.T("Не проверять этот файл", "Don't check this file"), null, delegate { Answer(f, 2); }));
                m.Show(Cursor.Position);
                return;
            }
            if (Pending != null)
            {
                var f = Pending;
                Pending = null;
                Last = f;
                ShowCode(f);
                return;
            }
            happyT = 2.5f;
            SetMood(Mood.Happy, 45);
            if (Riding) { Think(Lines.Pick(carryT > 0 ? Lines.CarryTalk : Lines.RideTalk)); return; }
            double r = rnd.NextDouble();
            Think(Lines.Pick(r < 0.2 ? Lines.CatFacts : r < 0.55 && Settings.Feature("tips") ? Lines.Tips : Lines.Kind));
        }

        public void SayNow(string s) { Think(s); }

        /// Something in the code looks wrong: say so and wait for a click.
        public void Notice(Finding f, string before = "")
        {
            SetMood(Mood.Curious, 60);
            Pending = f;
            Think((before + L.T("Хм… ", "Hmm… ") + System.IO.Path.GetFileName(f.File) + L.T(", строка ", ", line ") + f.Line + ": " + f.Message
                + L.T(" Нажми на меня — покажу.", " Click me and I'll show you.")).Replace("{", "{{").Replace("}", "}}"));
        }

        /// The problem it talked about is gone from the file: stop pointing.
        public void Fixed()
        {
            Pending = Last = null;
            pointT = 0;
            pinnedUntil = DateTime.Now;
            happyT = 2.5f;
            SetMood(Mood.Happy, 60);
        }

        /// Flies to the line and points at it with an arrow.
        public void PointAt(PointF line, string text)
        {
            home = new PointF(line.X + 380, line.Y);
            pinnedUntil = DateTime.Now.AddSeconds(50);
            pointT = 50;
            visiting = false;
            ballT = 0;
            Think(text, true);
        }
        /// Explains a problem in words, without the arrow: stays just as long.
        public void SayLong(string text) { Think(text, true); }

        public void StartBall(RectangleF box)
        {
            Box = box;
            ballT = 22;
            X = Math.Max(box.Left + 30, Math.Min(box.Right - 30, X));
            Y = Math.Max(box.Top + 30, Math.Min(box.Bottom - 30, Y));
            vx = (rnd.NextDouble() < 0.5 ? -1 : 1) * 500;
            vy = -700;
            Think(Lines.Pick(Lines.Ball));
        }

        public void StopBall() { if (ballT > 0) { ballT = 0; Box = null; NewHome(); } }

        /// Debug: the body and a bubble into an image.
        public Bitmap Preview(bool think, string text, bool happy)
        {
            happyT = happy ? 1 : 0;
            using (var g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.FromArgb(40, 44, 52));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                DrawBody(g);
                if (text != "") Bubble(g, text, think);
            }
            return (Bitmap)canvas.Clone();
        }
    }

    /// What Ember says, in Russian and in English. {0} = your name, {1} = the cat, {2} {3} = days to New Year and the word for it.
    static class Lines
    {
        public static string Pick(string[] list) { return Talk.Pick(list); }

        public static string[] CatFacts { get { return L.A(CatFactsRu, CatFactsEn); } }
        static readonly string[] CatFactsRu =
        {
            "Факт о котах: Кошки почти всегда приземляются на лапы: у них есть «рефлекс переворота», он появляется уже в 3–4 недели.",
            "Факт о котах: Кошки спят 12–16 часов в сутки. {1} не ленится, он соблюдает норму.",
            "Факт о котах: В каждом ухе у кошки по 32 мышцы, и ухо поворачивается почти на 180 градусов.",
            "Факт о котах: Кошки не чувствуют сладкого вкуса — у них нет нужного рецептора.",
            "Факт о котах: Отпечаток носа у каждой кошки свой, как у людей отпечатки пальцев.",
            "Факт о котах: Кошки видят в темноте в несколько раз лучше людей.",
            "Факт о котах: Усы помогают кошке понять, пролезет ли она в щель. Это её линейка.",
            "Факт о котах: Взрослые кошки мяукают почти только людям. Друг с другом они так не разговаривают.",
            "Факт о котах: Сердце кошки бьётся 140–220 раз в минуту — почти вдвое чаще человеческого.",
            "Факт о котах: Кошки потеют только подушечками лап.",
            "Факт о котах: Хвост трубой означает «я рад тебя видеть».",
            "Факт о котах: Медленное моргание — кошачье «я тебе доверяю». Попробуй ответить тем же.",
            "Факт о котах: Кошка может прыгнуть в высоту в несколько раз выше собственного роста.",
            "Факт о котах: Все котята рождаются с голубыми глазами, настоящий цвет появляется позже.",
            "Факт о котах: Кошки живут рядом с людьми около 10 тысяч лет — сами пришли за мышами к амбарам.",
            "Факт о котах: У кошек есть третье веко — прозрачная мигательная перепонка.",
            "Факт о котах: Кошки узнают голос своего человека среди чужих голосов. Учёные это проверяли.",
            "Факт о котах: Когда кошка «месит» лапками — это привычка из детства, так котята просят молоко.",
            "Факт о котах: Кошка трётся о тебя щекой и оставляет свой запах. Это значит: «ты мой».",
            "Факт о котах: Самая долгоживущая известная кошка, Крем Пафф, прожила 38 лет.",
            "Факт о котах: Кошки не умеют спускаться с дерева головой вниз: когти загнуты назад, поэтому они слезают задом.",
            "Факт о котах: Кошки слышат ультразвук — примерно втрое выше, чем человек.",
            "Факт о котах: Когти у кошек втягиваются, поэтому не стачиваются при ходьбе.",
            "Факт о котах: Язык кошки покрыт крошечными крючками — это одновременно и расчёска.",
            "Факт о котах: Кошка пьёт, касаясь воды кончиком языка и вытягивая водяной столбик — около четырёх раз в секунду.",
            "Факт о котах: В Британии и Японии чёрная кошка — к удаче.",
            "Факт о котах: Домашние кошки — почти единственные из кошачьих, кто ходит с поднятым вертикально хвостом.",
            "Факт о котах: Кошка, показывающая живот, тебе доверяет. Но это не всегда приглашение его гладить.",
            "Факт о котах: Учёные выяснили: приютским кошкам с коробками спокойнее. Коробка — это домик.",
            "Факт о котах: Глаза кошки светятся в темноте из-за зеркального слоя за сетчаткой.",
            "Факт о котах: Котята начинают мурлыкать уже в первые дни жизни.",
            "Факт о котах: Кошка тратит на умывание до половины времени, когда не спит.",
            "Факт о котах: Группа кошек по-английски называется «clowder». Нас двое — уже почти clowder.",
            "Факт о котах: Мурлыканье — это низкий звук примерно 25–150 герц. Есть гипотеза, что он помогает заживлению.",
            "Факт о котах: Кошки чувствуют запахи в разы лучше людей — а у этого кота ещё и нюх на рыбку.",
        };
        static readonly string[] CatFactsEn =
        {
            "Cat fact: Cats almost always land on their feet thanks to the \"righting reflex\", which appears at 3–4 weeks old.",
            "Cat fact: Cats sleep 12–16 hours a day. {1} isn't lazy, he's meeting the quota.",
            "Cat fact: Each cat ear has 32 muscles and can turn almost 180 degrees.",
            "Cat fact: Cats can't taste sweetness: they lack the receptor for it.",
            "Cat fact: Every cat's nose print is unique, like a human fingerprint.",
            "Cat fact: Cats see in the dark several times better than people.",
            "Cat fact: Whiskers tell a cat whether it fits through a gap. They're its ruler.",
            "Cat fact: Adult cats meow almost only at people. They don't talk to each other that way.",
            "Cat fact: A cat's heart beats 140–220 times a minute, almost twice as fast as a human's.",
            "Cat fact: Cats sweat only through their paw pads.",
            "Cat fact: A tail straight up means \"I'm glad to see you\".",
            "Cat fact: A slow blink is cat for \"I trust you\". Try blinking back.",
            "Cat fact: A cat can jump several times its own height.",
            "Cat fact: All kittens are born with blue eyes; the real color comes later.",
            "Cat fact: Cats have lived with people for about 10,000 years. They came to the barns for the mice on their own.",
            "Cat fact: Cats have a third eyelid, a see-through membrane.",
            "Cat fact: Cats recognize their human's voice among strangers. Scientists checked.",
            "Cat fact: Kneading is a habit from kittenhood: that's how kittens ask for milk.",
            "Cat fact: When a cat rubs its cheek on you, it leaves its scent. That means \"you're mine\".",
            "Cat fact: The oldest known cat, Creme Puff, lived to 38.",
            "Cat fact: Cats can't climb down a tree head first: their claws curve backwards, so they back down.",
            "Cat fact: Cats hear ultrasound, roughly three times higher than humans can.",
            "Cat fact: Cats' claws retract, so they don't wear down when walking.",
            "Cat fact: A cat's tongue is covered with tiny hooks. It doubles as a comb.",
            "Cat fact: A cat drinks by touching the water with the tip of its tongue and pulling up a column of water, about four times a second.",
            "Cat fact: In Britain and Japan a black cat brings good luck.",
            "Cat fact: House cats are almost the only felines that walk with their tails held straight up.",
            "Cat fact: A cat showing its belly trusts you. That's not always an invitation to rub it, though.",
            "Cat fact: Scientists found shelter cats with boxes are calmer. A box is a little house.",
            "Cat fact: Cats' eyes glow in the dark because of a mirror layer behind the retina.",
            "Cat fact: Kittens start purring in their first days of life.",
            "Cat fact: A cat spends up to half of its waking time grooming.",
            "Cat fact: A group of cats is called a \"clowder\". There are two of us, almost a clowder.",
            "Cat fact: Purring is a low sound of about 25–150 hertz. One hypothesis says it helps healing.",
            "Cat fact: Cats smell many times better than people, and this cat has a special nose for fish.",
        };

        /// What it "does" while thinking, shown in a thought bubble before it speaks.
        public static string[] Gerunds { get { return L.A(GerundsRu, GerundsEn); } }
        static readonly string[] GerundsRu =
        {
            "Ищу подходящее слово…", "Распутываю клубок…", "Перечитываю…", "Думаю на скорости света…", "Листаю память…",
            "Собираю мысль по кусочкам…", "Чешу затылок (если он у меня есть)…", "Подбираю ключик…",
            "Размышляю…", "Прикидываю…", "Шуршу мыслями…", "Сопоставляю…", "Колдую…", "Перебираю варианты…",
            "Компилирую мысль…", "Мурлыкаю (у кота научился)…", "Взвешиваю…", "Сверяюсь с памятью…", "Разгораюсь…",
        };
        static readonly string[] GerundsEn =
        {
            "Looking for the right word…", "Untangling the yarn…", "Rereading…", "Thinking at the speed of light…", "Flipping through memory…",
            "Piecing a thought together…", "Scratching my head (if I have one)…", "Finding the right key…",
            "Pondering…", "Figuring…", "Rustling my thoughts…", "Comparing…", "Doing some magic…", "Weighing the options…",
            "Compiling a thought…", "Purring (learned it from the cat)…", "Weighing…", "Checking my memory…", "Warming up…",
        };

        public static string[] Hello { get { return L.A(HelloRu, HelloEn); } }
        static readonly string[] HelloRu =
        {
            "Привет, {0}. Я тоже тут. Если соскучишься — нажми на меня.",
            "О, привет! Не обращай внимания, я просто летаю и думаю.",
            "{0}, привет. Я Огонёк. Свечу понемногу и присматриваю за котом.",
        };
        static readonly string[] HelloEn =
        {
            "Hi, {0}. I'm here too. If you miss me, click on me.",
            "Oh, hi! Don't mind me, I just float around and think.",
            "Hi, {0}. I'm Ember. I glow a little and keep an eye on the cat.",
        };

        public static string[] Tips { get { return L.A(TipsRu, TipsEn); } }
        static readonly string[] TipsRu =
        {
            "Совет: в C++ после delete обнули указатель — висячий указатель коварнее NULL.",
            "Факт: std::vector при росте переезжает в новую память — старые указатели на элементы становятся висячими.",
            "Совет: unique_ptr почти всегда лучше голого new. Память сама освободится.",
            "Факт: i++ и ++i для int одинаковы, а для итераторов ++i чуть дешевле.",
            "Совет: включи -Wall -Wextra. Компилятор — бесплатный ревьюер.",
            "Факт: размер int не обязан быть 4 байта. Нужна точность — бери int32_t.",
            "Совет: передавай большие объекты по const&, а не по значению.",
            "Факт: деление целых в C++ отбрасывает дробь: 7 / 2 == 3.",
            "Совет: санитайзер -fsanitize=address ловит висячие указатели прямо при запуске.",
            "Факт: std::endl ещё и сбрасывает буфер — в циклах лучше '\\n'.",
            "Совет: в Python пиши with open(...) — файл закроется сам.",
            "Факт: в Python список по умолчанию в аргументе создаётся один раз на все вызовы.",
            "Совет: f-строки в Python — самый удобный формат строк, а x= внутри фигурных скобок покажет и имя, и значение.",
            "Факт: в Python is сравнивает объекты, а == — значения.",
            "Совет: виртуальное окружение venv — и библиотеки разных проектов не дерутся.",
            "Факт: из-за GIL потоки Python не ускоряют счёт, но отлично ждут сеть и диск.",
            "Факт: процесс — это своя память, поток — общая память внутри процесса.",
            "Совет: два потока трогают одну переменную — нужен мьютекс или atomic.",
            "Факт: deadlock — когда два потока ждут друг друга вечно. Бери мьютексы в одном порядке.",
            "Факт: TCP гарантирует порядок и доставку, UDP — скорость без гарантий.",
            "Факт: recv может вернуть не всё сообщение сразу. Читай в цикле.",
            "Факт: порт 80 — HTTP, 443 — HTTPS, 22 — SSH.",
            "Факт: fork() возвращает 0 в ребёнке и PID ребёнка в родителе.",
            "Совет: zombie-процесс появляется, если родитель не вызвал wait().",
            "Факт: виртуальная память позволяет каждому процессу думать, что вся память — его.",
            "Совет: перед оптимизацией — профилировщик. Угадывать узкое место бесполезно.",
            "Факт: бинарный поиск находит элемент среди миллиона за 20 шагов.",
            "Совет: имя переменной — это комментарий, который всегда актуален.",
            "Факт: хеш-таблица ищет за O(1) в среднем, но в худшем — за O(n).",
            "Совет: маленькие функции проще тестировать. И читать в три часа ночи.",
            "Факт: стек маленький (обычно 1–8 МБ). Огромные массивы — в кучу или в vector.",
            "Совет: git stash спрячет незаконченное, если надо срочно переключиться.",
            "Факт: в C++ порядок вычисления аргументов функции не определён.",
            "Совет: assert — дешёвый способ поймать ошибку там, где она родилась.",
            "Факт: std::map — дерево, отсортировано; std::unordered_map — хеш, быстрее, но без порядка.",
            "Совет: читай текст ошибки компилятора с первой строки — остальные часто её следствие.",
            "Факт: DNS превращает имя сайта в IP-адрес. Без него пришлось бы помнить цифры.",
            "Факт: в Python range не создаёт список — числа появляются по одному.",
            "Совет: rubber duck debugging — объясни код вслух. Я могу быть уточкой.",
            "Факт: первый баг 1947 года был настоящим мотыльком в реле.",
            "Совет: коммить почаще. Git помнит всё, даже то, что ты забыл.",
            "Если баг не ловится — объясни код коту. Серьёзно, работает.",
            "В C++ всегда инициализируй переменные. Мусор в памяти — плохой сосед.",
            "Сначала сделай, чтобы работало. Потом — чтобы было красиво. Потом — быстро.",
            "Читай ошибку целиком. Ответ часто в самой последней строке.",
            "Большую задачу режь на маленькие. Так проще.",
            "Имена — это документация. Лучше enemyCount, чем ec.",
            "Godot: сигналы хороши, когда объекты не должны знать друг о друге.",
            "Не бойся удалять код. Лучший код — тот, который не пришлось писать.",
            "Перед тем как чинить, воспроизведи баг. Иначе чинишь наугад.",
        };
        static readonly string[] TipsEn =
        {
            "Tip: in C++, null a pointer after delete. A dangling pointer is sneakier than NULL.",
            "Fact: a growing std::vector moves to new memory, and old pointers to its elements dangle.",
            "Tip: unique_ptr is almost always better than a bare new. The memory frees itself.",
            "Fact: i++ and ++i are the same for int, but for iterators ++i is a bit cheaper.",
            "Tip: turn on -Wall -Wextra. The compiler is a free reviewer.",
            "Fact: int doesn't have to be 4 bytes. Need exact size? Use int32_t.",
            "Tip: pass big objects by const&, not by value.",
            "Fact: integer division in C++ drops the fraction: 7 / 2 == 3.",
            "Tip: -fsanitize=address catches dangling pointers right when you run the program.",
            "Fact: std::endl also flushes the buffer. In loops, '\\n' is better.",
            "Tip: in Python, write with open(...) and the file closes itself.",
            "Fact: in Python, a default list argument is created once for all calls.",
            "Tip: f-strings are the handiest Python formatting, and x= inside the braces shows both the name and the value.",
            "Fact: in Python, is compares objects and == compares values.",
            "Tip: a venv virtual environment keeps the libraries of different projects from fighting.",
            "Fact: because of the GIL, Python threads don't speed up math, but they're great at waiting for the network and disk.",
            "Fact: a process has its own memory; threads share memory inside a process.",
            "Tip: two threads touching one variable need a mutex or an atomic.",
            "Fact: a deadlock is two threads waiting for each other forever. Lock mutexes in the same order.",
            "Fact: TCP guarantees order and delivery; UDP gives speed without guarantees.",
            "Fact: recv may not return the whole message at once. Read in a loop.",
            "Fact: port 80 is HTTP, 443 is HTTPS, 22 is SSH.",
            "Fact: fork() returns 0 in the child and the child's PID in the parent.",
            "Tip: a zombie process appears when the parent doesn't call wait().",
            "Fact: virtual memory lets every process think all the memory is its own.",
            "Tip: profile before you optimize. Guessing the bottleneck is useless.",
            "Fact: binary search finds an element among a million in 20 steps.",
            "Tip: a variable name is a comment that's always up to date.",
            "Fact: a hash table finds things in O(1) on average, but O(n) in the worst case.",
            "Tip: small functions are easier to test. And to read at 3 a.m.",
            "Fact: the stack is small (usually 1–8 MB). Huge arrays go on the heap or in a vector.",
            "Tip: git stash hides unfinished work when you need to switch fast.",
            "Fact: in C++ the order in which function arguments are evaluated is unspecified.",
            "Tip: assert is a cheap way to catch a bug right where it's born.",
            "Fact: std::map is a sorted tree; std::unordered_map is a hash, faster but unordered.",
            "Tip: read compiler errors from the first one. The rest are often its consequences.",
            "Fact: DNS turns a site name into an IP address. Without it you'd have to remember numbers.",
            "Fact: Python's range doesn't build a list; the numbers appear one at a time.",
            "Tip: rubber duck debugging — explain your code out loud. I can be the duck.",
            "Fact: the first \"bug\" in 1947 was a real moth in a relay.",
            "Tip: commit often. Git remembers everything, even what you forgot.",
            "If a bug won't get caught, explain the code to the cat. Seriously, it works.",
            "In C++ always initialize variables. Garbage in memory is a bad neighbor.",
            "First make it work. Then make it pretty. Then make it fast.",
            "Read the whole error. The answer is often in the very last line.",
            "Cut a big task into small ones. It's easier that way.",
            "Names are documentation. enemyCount beats ec.",
            "Godot: signals are great when objects shouldn't know about each other.",
            "Don't be afraid to delete code. The best code is code you didn't have to write.",
            "Reproduce the bug before you fix it. Otherwise you're fixing blind.",
        };

        public static string[] Kind { get { return L.A(KindRu, KindEn); } }
        static readonly string[] KindRu =
        {
            "Ошибки — это просто ступеньки. Иногда скользкие.",
            "У тебя отлично получается, даже когда кажется, что нет.",
            "Сделай паузу — мысли сами разложатся.",
            "Если сегодня тяжело, завтра будет понятнее.",
            "Здорово, что ты не бросаешь.",
            "Медленно — тоже вперёд.",
            "Помни: всё сложное когда-то было непонятным для всех.",
            "Улыбнись. Кот уже улыбается.",
            "Встань, потянись, посмотри вдаль. Глаза скажут спасибо.",
            "Ты задаёшь хорошие вопросы. Это главное умение.",
            "Каждая исправленная ошибка — новый опыт.",
            "Мне нравится, как ты думаешь.",
            "Не сравнивай себя с другими — сравнивай с тем, что было вчера.",
            "Можно не знать ответ. Важно знать, где искать.",
            "Хорошо, что ты есть.",
            "Отдых — это тоже часть работы.",
            "Если не получается — разбей задачу на кусочки поменьше.",
            "Ты справишься. У тебя уже не раз получалось.",
            "Кот рядом, я рядом — значит всё нормально.",
            "Сегодня ты на день опытнее, чем вчера.",
            "Попей чаю. Код подождёт пять минут.",
            "Не бойся удалять код. Хороший код часто короче.",
            "Любопытство — твоя суперсила.",
            "Спасибо, что пускаешь нас на свой экран.",
            "Ты сегодня молодец.", "Не забывай пить воду. Я не пью, но тебе надо.",
            "Если что-то не получается — значит, ты учишься.", "Я рад, что ты тут.",
            "Можно сделать перерыв. Мир подождёт пять минут.", "Мне нравится, как ты придумываешь идеи.",
        };
        static readonly string[] KindEn =
        {
            "Mistakes are just steps. Sometimes slippery ones.",
            "You're doing great, even when it doesn't feel like it.",
            "Take a pause, and your thoughts will sort themselves out.",
            "If today is hard, tomorrow will be clearer.",
            "It's great that you don't give up.",
            "Slow is still forward.",
            "Remember: everything hard was once confusing for everyone.",
            "Smile. The cat already is.",
            "Stand up, stretch, look into the distance. Your eyes will thank you.",
            "You ask good questions. That's the most important skill.",
            "Every fixed bug is a lesson learned.",
            "I like the way you think.",
            "Don't compare yourself with others, compare yourself with yesterday's you.",
            "It's okay not to know the answer. What matters is knowing where to look.",
            "I'm glad you exist.",
            "Rest is part of the work too.",
            "Stuck? Break the task into smaller pieces.",
            "You'll manage. You've done it before.",
            "The cat is near, I'm near, so everything's fine.",
            "Today you're one day more experienced than yesterday.",
            "Have some tea. The code will wait five minutes.",
            "Don't be afraid to delete code. Good code is often shorter.",
            "Curiosity is your superpower.",
            "Thanks for letting us on your screen.",
            "You did well today.", "Don't forget to drink water. I don't, but you need it.",
            "If something isn't working, it means you're learning.", "I'm glad you're here.",
            "You can take a break. The world will wait five minutes.", "I like how you come up with ideas.",
        };

        public static string[] StepUp { get { return L.A(new[] { "Залезай, я ступенька!", "Давай, прыгай на меня — подсажу.", "Лифт для котов подан." },
            new[] { "Climb on, I'm a step!", "Come on, jump on me, I'll give you a boost.", "The cat elevator has arrived." }); } }
        public static string[] Ride { get { return L.A(new[] { "Но, лошадка! То есть, котик.", "Можно я прокачусь? Спасибо.", "Отсюда отличный вид.", "Котик-такси, едем куда-нибудь." },
            new[] { "Giddy-up, horsey! I mean, kitty.", "Mind if I ride? Thanks.", "Great view from up here.", "Kitty taxi, take me anywhere." }); } }
        public static string[] Carry { get { return L.A(new[] { "Полетели! Держись крепче.", "Пассажир, пристегнитесь. Хвостом.", "Экскурсия по рабочему столу начинается!" },
            new[] { "Let's fly! Hold on tight.", "Passenger, fasten your seatbelt. With your tail.", "The desktop tour begins!" }); } }
        public static string[] CarryEnd { get { return L.A(new[] { "Приземляемся. Спасибо, что летали с Огоньком.", "Всё, конечная.", "Понравилось? Мне — очень." },
            new[] { "Landing. Thank you for flying Ember Air.", "Last stop, everyone off.", "Did you like it? I loved it." }); } }

        public static string[] RideTalk { get { return L.A(RideTalkRu, RideTalkEn); } }
        static readonly string[] RideTalkRu =
        {
            "Мягко идём. Как на облаке.", "Правее, пожалуйста. Шучу, вези куда хочешь.", "Отсюда видно весь рабочий стол.",
            "Ты лучший транспорт на этом экране.", "Осторожно, впереди окно!", "Я почти не вешу, честно. Я же искорка.",
            "Остановка «Панель задач». Следующая — «Где угодно».", "Кот-такси, пять звёзд.", "Смотри, курсор! Не гонись, я упаду.",
            "Мне нравится ехать с тобой.", "Если что, я держусь за уши. Аккуратно.", "Дорогу знаешь? Я тоже нет. Отлично.",
        };
        static readonly string[] RideTalkEn =
        {
            "Smooth ride. Like on a cloud.", "A bit to the right, please. Kidding, take me anywhere.", "You can see the whole desktop from here.",
            "You're the best transport on this screen.", "Careful, window ahead!", "I barely weigh anything, honest. I'm a spark.",
            "Next stop: Taskbar. After that: Anywhere.", "Cat taxi, five stars.", "Look, the cursor! Don't chase it, I'll fall.",
            "I like riding with you.", "Just so you know, I'm holding your ears. Gently.", "Do you know the way? Me neither. Perfect.",
        };
        public static string[] CarryTalk { get { return L.A(CarryTalkRu, CarryTalkEn); } }
        static readonly string[] CarryTalkRu =
        {
            "Держишься? Ещё круг!", "Смотри вниз — вон твоя миска.", "Лечу ровно, не бойся.", "Это называется «вираж».",
            "Высота двести пикселей, полёт нормальный.", "Хвост не отпускай, это руль.", "Сейчас будет плавный поворот.",
            "Видишь, как красиво сверху?", "Ты храбрый кот.", "Почти как самолёт, только пушистее.", "Ещё немного — и на посадку.",
            "Пассажир, не высовывайте лапы.",
        };
        static readonly string[] CarryTalkEn =
        {
            "Holding on? One more lap!", "Look down, there's your bowl.", "Flying steady, don't worry.", "This is called a \"banked turn\".",
            "Altitude two hundred pixels, flight is smooth.", "Don't let go of the tail, it's the rudder.", "Smooth turn coming up.",
            "See how pretty it is from above?", "You're a brave cat.", "Almost like a plane, only fluffier.", "Just a little more and we land.",
            "Passenger, keep your paws inside.",
        };

        public static string[] Ball { get { return L.A(new[] { "Тут пусто и тихо. Можно я попрыгаю?", "Я мячик! Бум! Бум!", "Пока ты не печатаешь — я тут поскачу.", "Не мешаю? Я быстро." },
            new[] { "It's empty and quiet here. Can I bounce?", "I'm a ball! Boing! Boing!", "While you're not typing, I'll bounce around here.", "Am I in the way? I'll be quick." }); } }

        public static string[] Dodge { get { return L.A(new[] { "Хи! Не поймаешь!", "{1}, я не мышь!", "Ой! Почти." }, new[] { "Hee! Can't catch me!", "{1}, I'm not a mouse!", "Oops! Almost." }); } }

        public static string[] Late { get { return L.A(new[] { "Уже поздно, {0}. Я бы на твоём месте поспал. Но я не на твоём месте.", "Ночь. Лучшие идеи приходят после сна." },
            new[] { "It's late, {0}. I'd go to sleep if I were you. But I'm not you.", "Night. The best ideas come after sleep." }); } }

        /// Short dialogues: Ember says the first line, the cat answers.
        public static string[][] Chats { get { return L.A(ChatsRu, ChatsEn); } }
        static readonly string[][] ChatsRu =
        {
            new[] { "{1}, научить тебя программировать?", "Уже умею. Ctrl+Z — моя любимая команда." },
            new[] { "Почему ты всегда садишься на самое высокое окно?", "Оттуда видно все баги." },
            new[] { "Хочешь, покатаю?", "Только не быстро. Хотя… давай быстро!" },
            new[] { "{1}, что такое указатель?", "Это когда я лапой показываю на миску." },
            new[] { "Ты знаешь, что такое поток?", "Это когда я бегу за курсором и за лазером одновременно." },
            new[] { "Тебе не скучно?", "Скучно бывает тем, кто не спит." },
            new[] { "Как думаешь, {0} не устаёт?", "Давай мурлыкнем погромче — станет легче." },
            new[] { "Какой твой любимый язык?", "Мяу. Он компилируется сразу в сердце." },
            new[] { "Ты опять ловил лазер?", "Я его почти поймал. Он хитрый." },
            new[] { "Почему коты любят коробки?", "Коробка — это окно, которое обнимает." },
            new[] { "{1}, у тебя есть мечта?", "Большая тёплая клавиатура. И рыбка." },
            new[] { "Слушай, а ты боишься пылесоса?", "Я ничего не боюсь. Кроме пылесоса." },
            new[] { "Хочешь сыграть в снежки?", "Только если ты будешь мишенью." },
            new[] { "Сколько будет два плюс два?", "Четыре лапы. Всегда четыре." },
            new[] { "Ты знаешь, что я сделан из света?", "А я — из пуха. Мы оба мягкие." },
            new[] { "Почему ты моешься так долго?", "Красота требует времени." },
            new[] { "{1}, давай посмотрим, что там пишет {0}.", "Я буду смотреть. А ты думай." },
            new[] { "Что ты делаешь, когда {0} уходит?", "Сторожу экран. Иногда сплю на посту." },
            new[] { "Ты слышал про Новый год?", "Это когда ёлка, и на ней можно висеть?" },
            new[] { "Тебе нравится твоя шапка?", "Она греет уши. Значит, нравится." },
            new[] { "Как прошёл день?", "Поспал, поел, поймал курсор. Идеально." },
            new[] { "Помнишь, как ты прыгал по мне как по ступеньке?", "Ты отличная ступенька. Мягкая." },
            new[] { "Почему у тебя хвост трубой?", "Это я рад тебя видеть." },
            new[] { "{1}, а что такое рекурсия?", "Это когда я гоняюсь за своим хвостом." },
            new[] { "Ты бы хотел стать большим котом?", "Я и так большой. Внутри." },
            new[] { "Давай устроим тихий час?", "У котов весь день — тихий час." },
            new[] { "Кто тебе больше нравится, я или рыбка?", "…А можно рыбку и тебя?" },
            new[] { "{1}, ты видел баг?", "Видел. Съел. Не благодари." },
            new[] { "Как думаешь, мы хорошая команда?", "Лучшая. Ты летаешь, я мурлычу." },
            new[] { "Ты знаешь, сколько тут проектов?", "Много. И в каждом нужен кот." },
            new[] { "{1}, ты опять спишь?", "Мрр. Я медитирую." },
            new[] { "Как тебе на панели задач?", "Тепло. Рекомендую." },
            new[] { "Кто сегодня поймал курсор?", "Я! Почти." },
            new[] { "Давай не будем мешать. {0} работает.", "Я не мешаю. Я украшаю." },
            new[] { "Хочешь, расскажу про рекурсию?", "Чтобы понять рекурсию, надо понять рекурсию. Мяу." },
            new[] { "Я посчитал: ты спишь часов шестнадцать в сутки.", "Мало." },
            new[] { "{1}, а ты знаешь, что такое баг?", "Это жук. Я их ловлю." },
            new[] { "Может, поможем с кодом? Это будет приятно.", "Я лягу на клавиатуру. Это помощь." },
            new[] { "Ты когда-нибудь видел снег?", "Видел. Белый пиксель. Скучно." },
        };
        static readonly string[][] ChatsEn =
        {
            new[] { "{1}, want me to teach you programming?", "I already can. Ctrl+Z is my favorite command." },
            new[] { "Why do you always sit on the highest window?", "You can see all the bugs from there." },
            new[] { "Want a ride?", "Not too fast. Actually… fast!" },
            new[] { "{1}, what's a pointer?", "It's when I point my paw at the bowl." },
            new[] { "Do you know what a thread is?", "It's when I chase the cursor and the laser at the same time." },
            new[] { "Aren't you bored?", "Only those who don't sleep get bored." },
            new[] { "Do you think {0} is getting tired?", "Let's purr louder, that'll help." },
            new[] { "What's your favorite language?", "Meow. It compiles straight to the heart." },
            new[] { "Were you chasing the laser again?", "I almost caught it. It's sneaky." },
            new[] { "Why do cats love boxes?", "A box is a window that hugs you." },
            new[] { "{1}, do you have a dream?", "A big warm keyboard. And fish." },
            new[] { "Hey, are you afraid of the vacuum cleaner?", "I'm not afraid of anything. Except the vacuum cleaner." },
            new[] { "Want to have a snowball fight?", "Only if you're the target." },
            new[] { "What's two plus two?", "Four paws. Always four." },
            new[] { "Did you know I'm made of light?", "And I'm made of fluff. We're both soft." },
            new[] { "Why do you wash for so long?", "Beauty takes time." },
            new[] { "{1}, let's see what {0} is writing.", "I'll look. You think." },
            new[] { "What do you do when {0} goes away?", "Guard the screen. Sometimes I fall asleep on duty." },
            new[] { "Have you heard about New Year?", "Is that when there's a tree and you can hang on it?" },
            new[] { "Do you like your hat?", "It keeps my ears warm. So yes." },
            new[] { "How was your day?", "Slept, ate, caught the cursor. Perfect." },
            new[] { "Remember jumping on me like a step?", "You're a great step. A soft one." },
            new[] { "Why is your tail sticking up?", "That means I'm happy to see you." },
            new[] { "{1}, what's recursion?", "It's when I chase my own tail." },
            new[] { "Would you like to be a big cat?", "I already am. On the inside." },
            new[] { "Shall we have a quiet hour?", "For cats the whole day is a quiet hour." },
            new[] { "Who do you like more, me or fish?", "…Can I have the fish and you?" },
            new[] { "{1}, did you see a bug?", "Saw it. Ate it. You're welcome." },
            new[] { "Do you think we're a good team?", "The best. You fly, I purr." },
            new[] { "Do you know how many projects there are here?", "Lots. And every one of them needs a cat." },
            new[] { "{1}, sleeping again?", "Purr. I'm meditating." },
            new[] { "How do you like the taskbar?", "Warm. Recommended." },
            new[] { "Who caught the cursor today?", "Me! Almost." },
            new[] { "Let's not bother {0}. Work time.", "I'm not bothering. I'm decorating." },
            new[] { "Want me to explain recursion?", "To understand recursion, you must understand recursion. Meow." },
            new[] { "I counted: you sleep about sixteen hours a day.", "Not enough." },
            new[] { "{1}, do you know what a bug is?", "A beetle. I catch them." },
            new[] { "Shall we help with the code? That would be nice.", "I'll lie on the keyboard. That's help." },
            new[] { "Have you ever seen snow?", "I have. A white pixel. Boring." },
        };

        /// Window title fragments: what is going on on the screen.
        public static string[][] Titles { get { return L.A(TitlesRu, TitlesEn); } }
        static readonly string[][] TitlesRu =
        {
            new[] { ".cpp", "C++! Не забывай про const и ссылки — меньше копий, меньше багов." },
            new[] { ".h -", "Заголовочный файл. Не забудь #pragma once." },
            new[] { ".gd", "GDScript! Подсказка: типизируй переменные — Godot подскажет ошибки раньше." },
            new[] { ".py", "Python. Отступы — это не красота, это синтаксис." },
            new[] { ".cs", "C#! {1}, между прочим, написан на нём." },
            new[] { ".json", "JSON: следи за запятыми. Последняя лишняя — главный враг." },
            new[] { ".md", "Пишешь документацию? Уважаю. Это редкость." },
            new[] { "YouTube", "Смотришь? Мы с котом посидим тихо." },
        };
        static readonly string[][] TitlesEn =
        {
            new[] { ".cpp", "C++! Remember const and references: fewer copies, fewer bugs." },
            new[] { ".h -", "A header file. Don't forget #pragma once." },
            new[] { ".gd", "GDScript! Tip: give your variables types and Godot will catch errors earlier." },
            new[] { ".py", "Python. Indentation isn't decoration, it's syntax." },
            new[] { ".cs", "C#! {1} is written in it, by the way." },
            new[] { ".json", "JSON: watch the commas. A trailing one is the main enemy." },
            new[] { ".md", "Writing docs? Respect. That's rare." },
            new[] { "YouTube", "Watching? The cat and I will sit quietly." },
        };

        public static System.Collections.Generic.Dictionary<string, string> Apps { get { return L.En ? AppsEn : AppsRu; } }
        static readonly System.Collections.Generic.Dictionary<string, string> AppsRu = new System.Collections.Generic.Dictionary<string, string>
        {
            { "javaw", "Майнкрафт! Удачи. Строй с умом." },
            { "telegram", "Если пишешь друзьям — передай привет от нас с котом." },
        };
        static readonly System.Collections.Generic.Dictionary<string, string> AppsEn = new System.Collections.Generic.Dictionary<string, string>
        {
            { "javaw", "Minecraft! Good luck. Build wisely." },
            { "telegram", "If you're writing to friends, say hi from the cat and me." },
        };
    }
}
