using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Kotik
{
    enum Act { Fall, Idle, Walk, Run, Sit, Loaf, Sleep, Wash, Stretch, Crouch, Jump, Dragged, Eat, Land, Pet }

    /// The cat's body and mind: gravity, the windows it stands on, what it decides to do next, and its pose.
    /// Coordinates are screen pixels; (X, Y) is the point between the paws.
    class Brain
    {
        const float G = 2400f;
        public float X, Y, VX, VY;
        public int Dir = 1;
        public Act Act = Act.Fall;
        public Plat On;                       // what it stands on; null in the air
        public readonly Pose Pose = new Pose();
        public bool Calm;                     // "sit still": no walking around
        public Point Cursor;
        public float CursorSpeed;
        public float UserIdle;
        public bool Hungry;
        public PointF? Food;                  // where the fish lies
        public PointF? Toy;                   // Ember floating around: sometimes worth a jump
        public PointF? Laser;                 // the red dot: chase it!
        public Func<PointF, bool> AskStep = delegate { return false; };   // asks Ember to hover as a step at a point
        public PointF? Step;                  // where Ember hovers as a step right now
        PointF? afterStep;                    // the high place to jump to from Ember
        public bool Riding;                   // the cat sits on Ember and flies with it
        public bool Music;                    // music plays: the cat moves to it
        public float Beat;                    // 0..1, the loudness right now
        float groove;                         // the sway to the music
        static readonly IntPtr StepH = new IntPtr(-2);
        public Action<string> Say = delegate { };
        public Action Ate = delegate { };
        public Action Purr = delegate { };

        internal List<Plat> wins = new List<Plat>();
        List<Plat> shelves = new List<Plat>();
        internal readonly List<Plat> floors = new List<Plat>();
        readonly Random rnd = new Random();
        float actT, actLen, t, blinkT = 3, blinking, pounceCd = 10, jumpCd = 4, petT;
        float targetX;
        bool stepWait;
        float hopCd;
        PointF jumpTo;
        bool sleptLong;
        float landSpeed;

        public Brain()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            X = wa.Left + wa.Width * 0.7f;
            Y = wa.Top - 10;
            RefreshPlatforms(IntPtr.Zero);
        }

        public float Time { get { return t; } }

        // ---------- platforms ----------

        public void RefreshPlatforms(IntPtr self)
        {
            floors.Clear();
            foreach (var s in Screen.AllScreens)
                floors.Add(new Plat { H = IntPtr.Zero, R = new Rectangle(s.WorkingArea.Left, s.WorkingArea.Bottom, s.WorkingArea.Width, 1) });
            wins = Native.Windows(self);
            if (On != null && On.H == StepH) { if (!Step.HasValue) Drop(); else { X = Step.Value.X; Y = Step.Value.Y; } }
            else if (On != null && On.Shelf) { }
            else if (On != null && On.H != IntPtr.Zero && Act != Act.Dragged)
            {
                Plat now = wins.Find(w => w.H == On.H);
                if (now == null || now.R.Top < 30)
                {
                    Drop();                              // the window closed, minimised or maximised
                    return;
                }
                if (now.R.Left != On.R.Left || now.R.Top != On.R.Top)
                {
                    X += now.R.Left - On.R.Left;         // ride along when the window is dragged
                    Y = now.R.Top;
                    targetX += now.R.Left - On.R.Left;
                }
                On = now;
                if (X < On.R.Left + 6 || X > On.R.Right - 6 || Covered(On, X)) Drop();
            }
            else if (On != null && On.H == IntPtr.Zero)
            {
                Plat f = FloorAt(X);
                if (f != null && f.R.Top != (int)Y) { Y = f.R.Top; On = f; }
            }
        }

        /// New shelves from the latest screen scan. If the one under the paws is gone, the cat falls.
        public void SetShelves(List<Plat> list)
        {
            shelves = list;
            if (On == null || !On.Shelf || Act == Act.Dragged) return;
            Plat same = list.Find(p => Math.Abs(p.R.Top - On.R.Top) <= 2 && X >= p.R.Left - 4 && X <= p.R.Right + 4);
            if (same == null) Drop();
            else On = same;
        }

        Plat FloorAt(float x)
        {
            foreach (var f in floors) if (x >= f.R.Left && x <= f.R.Right) return f;
            return floors.Count > 0 ? floors[0] : null;
        }

        /// Is the top edge of `p` hidden under another window at x?
        bool Covered(Plat p, float x)
        {
            if (p.H == IntPtr.Zero || p.H == StepH) return false;
            foreach (var w in wins)
            {
                if (w.H == p.H) return false;            // everything after it is below it
                if (w.R.Contains((int)x, p.R.Top - 4)) return true;
            }
            return false;
        }

        void Drop()
        {
            On = null;
            Act = Act.Fall;
            VX = 0;
            VY = 0;
        }

        // ---------- per frame ----------

        public void Update(float dt)
        {
            if (hopCd > 0) hopCd -= dt;
            groove = Music ? groove + dt * (float)(Math.PI * 2 * 2.0) : 0;
            t += dt;
            actT += dt;
            pounceCd -= dt;
            jumpCd -= dt;
            petT -= dt;
            if (On != null && On.H == StepH && Act != Act.Jump && Act != Act.Fall) { if (!Step.HasValue) Drop(); else { X = Step.Value.X; Y = Step.Value.Y; } }
            if (Riding && Step.HasValue) { X = Step.Value.X; Y = Step.Value.Y; On = null; if (Act != Act.Loaf) Set(Act.Loaf, 99); Animate(dt); return; }
            switch (Act)
            {
                case Act.Fall:
                case Act.Jump:
                    Fly(dt);
                    break;
                case Act.Dragged:
                    break;
                case Act.Walk:
                case Act.Run:
                    Move(dt, Act == Act.Run ? (Laser.HasValue ? 250f : 170f) : 62f);
                    break;
                case Act.Crouch:
                    if (actT > (Laser.HasValue ? 0.2f : 0.35f)) Leap(jumpTo);
                    break;
                case Act.Eat:
                    if (actT > 3.5f) { Food = null; Hungry = false; Ate(); Set(Act.Pet, 2.5f); }
                    break;
                default:
                    if (stepWait && actT > actLen && Step.HasValue) { stepWait = false; jumpTo = Step.Value; Set(Act.Crouch, 99); break; }
                    if (stepWait && actT > actLen + 3) { stepWait = false; afterStep = null; }
                    if (actT > actLen) Decide();
                    break;
            }
            if (On != null && Act != Act.Dragged && Act != Act.Eat && Act != Act.Crouch)
            {
                if (Food.HasValue && On.H != IntPtr.Zero && Act != Act.Fall) { Drop(); Say(L.T("Рыбка! Иду!", "Fish! Coming!")); }
                else if (Food.HasValue && Math.Abs(Food.Value.Y - Y) < 2 && Act != Act.Walk && Act != Act.Run)
                    GoTo(Food.Value.X, true);
                else if (Laser.HasValue) ChaseLaser();
                else if (CanPounce())
                    Pounce();
                else if (CanHunt())
                {
                    pounceCd = R(25, 60);
                    jumpTo = new PointF(Toy.Value.X, Toy.Value.Y + 40);
                    Dir = jumpTo.X > X ? 1 : -1;
                    Set(Act.Crouch, 99);
                }
            }
            Animate(dt);
        }

        void Fly(float dt)
        {
            float prevY = Y;
            VY += G * dt;
            X += VX * dt;
            Y += VY * dt;
            if (VX != 0) Dir = VX > 0 ? 1 : -1;
            var vs = SystemInformation.VirtualScreen;
            if (X < vs.Left + 30) { X = vs.Left + 30; VX = Math.Abs(VX) * 0.4f; }
            if (X > vs.Right - 30) { X = vs.Right - 30; VX = -Math.Abs(VX) * 0.4f; }
            if (Y < vs.Top + 60 && VY < 0) { Y = vs.Top + 60; VY = 0; }
            if (VY <= 0) return;
            Plat best = null;
            foreach (var p in floors) Check(p, prevY, ref best);
            foreach (var p in wins) Check(p, prevY, ref best);
            foreach (var p in shelves) Check(p, prevY, ref best);
            if (Step.HasValue) Check(new Plat { H = StepH, R = new Rectangle((int)Step.Value.X - 26, (int)Step.Value.Y, 52, 1) }, prevY, ref best);
            if (best == null && Y > vs.Bottom + 100) { best = FloorAt(X); }
            if (best != null)
            {
                landSpeed = VY;
                Y = best.R.Top;
                On = best;
                VX = 0;
                VY = 0;
                if (best.H == StepH && afterStep.HasValue)
                {
                    jumpTo = afterStep.Value;
                    afterStep = null;
                    Dir = jumpTo.X > X ? 1 : -1;
                    Set(Act.Crouch, 99);
                    return;
                }
                if (landSpeed > 1300) { Set(Act.Land, 0.6f); if (t > 4) Say(Talk.Pick(Talk.Landed)); }   // not over the greeting
                else Set(Act.Land, 0.25f);
            }
        }

        void Check(Plat p, float prevY, ref Plat best)
        {
            int top = p.R.Top;
            if (Food.HasValue && p.H != IntPtr.Zero && p.H != StepH) return;   // going for the fish: straight down to the taskbar
            if (prevY <= top + 1 && Y >= top && X >= p.R.Left + 8 && X <= p.R.Right - 8 && !Covered(p, X) && (p.H == IntPtr.Zero || top > 40))
                if (best == null || top < best.R.Top) best = p;
        }

        void Move(float dt, float speed)
        {
            if (On == null) { Drop(); return; }
            float d = targetX - X;
            Dir = d > 0 ? 1 : -1;
            float step = Math.Min(Math.Abs(d), speed * dt) * Dir;
            float nx = X + step;
            if (nx < On.R.Left + 14 || nx > On.R.Right - 14 || Covered(On, nx + Dir * 20))
            {
                Set(Act.Idle, 1.5f);
                return;
            }
            X = nx;
            if (Math.Abs(targetX - X) < 2)
            {
                if (Food.HasValue && Math.Abs(Food.Value.X - X) < 30 && Math.Abs(Food.Value.Y - Y) < 2) { Set(Act.Eat, 99); return; }
                Set(Act.Idle, 1f + (float)rnd.NextDouble() * 2);
            }
        }

        public void GoTo(float x, bool run)
        {
            if (On == null) return;
            targetX = Math.Max(On.R.Left + 16, Math.Min(On.R.Right - 16, x));
            Set(run ? Act.Run : Act.Walk, 99);
        }

        void Set(Act a, float len)
        {
            Act = a;
            actT = 0;
            actLen = len;
        }

        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        void Decide()
        {
            if (Act == Act.Sleep) { sleptLong = true; }
            if (sleptLong && UserIdle < 60) { sleptLong = false; Set(Act.Stretch, 2.4f); return; }
            int hour = DateTime.Now.Hour;
            bool sleepy = UserIdle > 120 || hour < 6 || hour >= 23 || (Calendar.On("solstice") && hour >= 20);
            var options = new List<KeyValuePair<Act, float>>();
            options.Add(new KeyValuePair<Act, float>(Act.Idle, 3));
            options.Add(new KeyValuePair<Act, float>(Act.Sit, 3));
            options.Add(new KeyValuePair<Act, float>(Act.Loaf, 2));
            options.Add(new KeyValuePair<Act, float>(Act.Sleep, Music ? 0 : sleepy ? 12 : 0.8f));
            if (Music) options.Add(new KeyValuePair<Act, float>(Act.Sit, 10));
            options.Add(new KeyValuePair<Act, float>(Act.Wash, 1.5f));
            if (!Calm && !sleepy)
            {
                options.Add(new KeyValuePair<Act, float>(Act.Walk, 4));
                options.Add(new KeyValuePair<Act, float>(Act.Run, 0.8f));
                if (jumpCd <= 0) options.Add(new KeyValuePair<Act, float>(Act.Jump, 2.5f));
            }
            float sum = 0;
            foreach (var o in options) sum += o.Value;
            float r = R(0, sum);
            Act pick = Act.Idle;
            foreach (var o in options) { r -= o.Value; if (r <= 0) { pick = o.Key; break; } }
            switch (pick)
            {
                case Act.Walk: GoTo(X + Roomy(R(80, 400)), false); break;
                case Act.Run: GoTo(X + Roomy(R(200, 700)), true); break;
                case Act.Jump: if (!TryJump()) Set(Act.Sit, R(4, 8)); break;
                case Act.Sit: Set(Act.Sit, R(5, 14)); break;
                case Act.Loaf: Set(Act.Loaf, R(8, 25)); break;
                case Act.Sleep: Set(Act.Sleep, sleepy ? R(40, 120) : R(15, 40)); break;
                case Act.Wash: Set(Act.Wash, R(3, 5)); break;
                default: Set(Act.Idle, R(2, 5)); break;
            }
        }

        /// A walk distance towards the side with more room (so it does not walk into the edge and stop at once).
        float Roomy(float d)
        {
            if (On == null) return 0;
            float left = X - On.R.Left, right = On.R.Right - X;
            int side = rnd.NextDouble() < (right / Math.Max(1, left + right)) ? 1 : -1;
            return side * d;
        }

        /// Pick a reachable spot on another window (or the taskbar) and jump there.
        bool TryJump()
        {
            jumpCd = R(6, 14);
            var cands = new List<PointF>();
            var highCands = new List<PointF>();
            var all = new List<Plat>(wins);
            all.AddRange(floors);
            all.AddRange(shelves);
            foreach (var p in all)
            {
                if (p == On || (On != null && p.H == On.H && p.Shelf == On.Shelf && (p.H != IntPtr.Zero || p.R.Top == On.R.Top) && (!p.Shelf || p.R.Top == On.R.Top))) continue;
                if (p.H != IntPtr.Zero && !p.Shelf && p.R.Top < 60) continue;
                float dy = p.R.Top - Y;
                if (dy < -280 && dy > -650 && highCands.Count < 6)
                {
                    float hx = X + R(-300, 300);
                    if (hx > p.R.Left + 40 && hx < p.R.Right - 40 && !Covered(p, hx)) highCands.Add(new PointF(hx, p.R.Top));
                }
                if (dy < -280 || dy > 520) continue;
                for (int k = 0; k < 4; k++)
                {
                    float tx = X + R(-380, 380);
                    if (tx < p.R.Left + 30 || tx > p.R.Right - 30 || Math.Abs(tx - X) < 70) continue;
                    if (Covered(p, tx)) continue;
                    cands.Add(new PointF(tx, p.R.Top));
                }
            }
            if (highCands.Count > 0 && (cands.Count == 0 || rnd.NextDouble() < 0.35))
            {
                // too high for one jump: Ember hovers half-way as a step
                var hi = highCands[rnd.Next(highCands.Count)];
                var mid = new PointF((X + hi.X) / 2, Y + (hi.Y - Y) / 2);
                if (AskStep(mid))
                {
                    afterStep = hi;
                    jumpTo = mid;
                    Dir = mid.X > X ? 1 : -1;
                    Set(Act.Sit, 1.4f);            // waits a moment for Ember to get there
                    stepWait = true;
                    return true;
                }
            }
            if (cands.Count == 0) return false;
            jumpTo = cands[rnd.Next(cands.Count)];
            Dir = jumpTo.X > X ? 1 : -1;
            Set(Act.Crouch, 99);
            return true;
        }

        void Leap(PointF to)
        {
            float dist = Math.Abs(to.X - X) + Math.Abs(to.Y - Y);
            float T = Math.Max(0.5f, Math.Min(0.95f, 0.45f + dist / 1100f));
            VX = (to.X - X) / T;
            VY = (to.Y - Y - 0.5f * G * T * T) / T;
            On = null;
            Set(Act.Jump, 99);
        }

        bool CanPounce()
        {
            if (Calm || pounceCd > 0 || CursorSpeed < 900) return false;
            if (Act == Act.Sleep || Act == Act.Jump || Act == Act.Fall || Act == Act.Eat || Act == Act.Pet) return false;
            float dx = Cursor.X - X, dy = Cursor.Y - Y;
            return Math.Abs(dx) < 300 && Math.Abs(dx) > 40 && dy < 10 && dy > -220;
        }

        void ChaseLaser()
        {
            var l = Laser.Value;
            float dx = l.X - X, dy = l.Y - Y;
            if (Act == Act.Jump || Act == Act.Crouch || Act == Act.Land || stepWait) return;
            if (hopCd <= 0 && Math.Abs(dy) > 60)
            {
                var hop = LaserHop(l);
                if (hop.HasValue)
                {
                    hopCd = R(0.3f, 0.6f);
                    jumpTo = hop.Value;
                    Dir = jumpTo.X > X ? 1 : -1;
                    Set(Act.Crouch, 99);
                    return;
                }
            }
            if (dy < -260 && hopCd <= 0)
            {
                // nothing in reach: Ember helps — a step next to the cat, or, when already on it, a lift up
                bool onStep = On.H == StepH;
                var at = onStep ? new PointF(X + Math.Sign(dx) * Math.Min(Math.Abs(dx), 120), Y - Math.Min(220, -dy - 120))
                                : new PointF(X + Math.Sign(dx) * Math.Min(Math.Abs(dx), 140), Y - Math.Min(240, -dy * 0.5f));
                hopCd = onStep ? 1.2f : 3f;
                if (AskStep(at))
                {
                    if (!onStep) { afterStep = null; jumpTo = at; Dir = dx > 0 ? 1 : -1; Set(Act.Sit, 1f); stepWait = true; }
                    return;
                }
            }
            if (dy < -50 && dy > -260 && Math.Abs(dx) < 220 && pounceCd <= 0)
            {
                pounceCd = R(1.5f, 3);
                jumpTo = new PointF(l.X, l.Y + 20);
                Dir = dx > 0 ? 1 : -1;
                Set(Act.Crouch, 99);
                return;
            }
            float tx = Math.Max(On.R.Left + 16, Math.Min(On.R.Right - 16, l.X));
            bool stuck = On.H == StepH || Math.Abs(tx - X) < 4 || Covered(On, X + Math.Sign(tx - X) * 20);
            if (Math.Abs(dx) > 18 && !stuck) GoTo(l.X, true);
            else if (Math.Abs(dx) <= 18 && dy > -120 && pounceCd <= 0) { pounceCd = R(0.8f, 1.5f); Dir = dx >= 0 ? 1 : -1; Set(Act.Crouch, 99); jumpTo = new PointF(l.X + Dir * 4, Y); }
            else if (Act != Act.Sit) { Dir = dx >= 0 ? 1 : -1; Set(Act.Sit, 2f); }   // the edge, or the dot out of reach: sit and watch it
        }

        /// A window or shelf in reach that brings the cat closer to the dot (up or down), or null.
        PointF? LaserHop(PointF l)
        {
            PointF? best = null;
            double bestD = LaserCost(l, X, Y) - 70;              // must be clearly better than staying here
            var all = new List<Plat>(wins);
            all.AddRange(floors);
            all.AddRange(shelves);
            foreach (var p in all)
            {
                if (p == On || p.R.Width < 80) continue;
                if (p.H != IntPtr.Zero && !p.Shelf && p.R.Top < 60) continue;
                float dy = p.R.Top - Y;
                if (dy < -280 || dy > 520 || Math.Abs(dy) < 20) continue;
                float tx = Math.Max(p.R.Left + 30, Math.Min(p.R.Right - 30, l.X));
                if (Math.Abs(tx - X) > 520 || Covered(p, tx)) continue;
                double d = LaserCost(l, tx, p.R.Top);
                if (d < bestD) { bestD = d; best = new PointF(tx, p.R.Top); }
            }
            return best;
        }

        /// How bad a standing spot is for catching the dot: best is the dot a little above the paws (one swat away).
        static double LaserCost(PointF l, float x, float y)
        {
            float up = y - l.Y;                                   // how far the dot is above the spot
            double v = up >= 0 && up < 200 ? up * 0.3 : Math.Abs(up) * 1.5;
            return Math.Abs(l.X - x) * 0.7 + v;
        }

        bool CanHunt()
        {
            if (!Toy.HasValue || Calm || pounceCd > 0 || rnd.NextDouble() > 0.004) return false;
            if (Act != Act.Idle && Act != Act.Sit && Act != Act.Walk) return false;
            float dx = Toy.Value.X - X, dy = Toy.Value.Y - Y;
            return Math.Abs(dx) < 260 && dy > -260 && dy < -40;
        }

        void Pounce()
        {
            pounceCd = R(8, 20);
            jumpTo = new PointF(Cursor.X, Cursor.Y + 20);
            Dir = jumpTo.X > X ? 1 : -1;
            Set(Act.Crouch, 99);
        }

        // ---------- the hand ----------

        /// The ride is over: the cat drops off Ember (onto whatever is below).
        public void StopRiding()
        {
            Riding = false;
            pounceCd = R(20, 40);
            Drop();
        }

        public void Grab()
        {
            Riding = false;
            On = null;
            Set(Act.Dragged, 99);
        }

        public void Hold(Point p)
        {
            X = p.X;
            Y = p.Y + 62;
        }

        public void Throw(float vx, float vy)
        {
            VX = Math.Max(-2400, Math.Min(2400, vx));
            VY = Math.Max(-2400, Math.Min(2400, vy));
            Set(Act.Fall, 99);
            if (Math.Abs(vx) + Math.Abs(vy) > 1500) Say(Talk.Pick(Talk.Thrown));
        }

        public void Petted()
        {
            petT = 2.2f;
            Purr();
            if (Act == Act.Sleep || Act == Act.Loaf) { Set(Act.Loaf, 6); return; }
            if (On != null) Set(Act.Pet, 2.4f);
        }

        /// A fright (the pumpkin said "Boo!"): a jump straight up.
        public void Startle()
        {
            if (On == null || Act == Act.Dragged) return;
            VY = -900;
            VX = 0;
            On = null;
            Set(Act.Fall, 99);
        }

        /// A leap into a heap of leaves or snow (Weather): up and forward, about 200 px away.
        public void Dive()
        {
            if (On == null) return;
            VX = Dir * 300;
            VY = -820;
            On = null;
            Set(Act.Fall, 99);
        }

        public void WakeUp()
        {
            if (Act == Act.Sleep || Act == Act.Loaf) Set(Act.Stretch, 2.4f);
        }

        public void Call(Point p)
        {
            X = p.X;
            Y = SystemInformation.VirtualScreen.Top + 40;
            Drop();
        }

        public bool Awake { get { return Act != Act.Sleep; } }

        // ---------- pose ----------

        void Animate(float dt)
        {
            var p = Pose;
            var d = new Pose();          // defaults = standing
            d.Walk = p.Walk;
            blinkT -= dt;
            if (blinkT <= 0) { blinking = 0.13f; blinkT = R(2, 6); }
            blinking -= dt;
            d.Tail = (float)Math.Sin(t * 1.6f) * 0.5f;
            switch (Act)
            {
                case Act.Walk:
                case Act.Run:
                    bool run = Act == Act.Run;
                    d.Walk = p.Walk + dt * (run ? 17 : 9);
                    d.Stride = run ? 10 : 6;
                    d.BodyY = -26 + Math.Abs((float)Math.Sin(d.Walk)) * (run ? 2.5f : 1.2f);
                    d.HeadY = -46 + Math.Abs((float)Math.Sin(d.Walk)) * 1.5f;
                    d.Tail = (float)Math.Sin(t * (run ? 9 : 5)) * 0.5f;
                    if (run) { d.BodyAngle = 4; d.HeadX = 33; d.HeadY += 3; }
                    break;
                case Act.Sit:
                case Act.Wash:
                case Act.Pet:
                    d.Sitting = true;
                    d.BodyY = -30;
                    d.BodyAngle = 0;
                    d.HeadX = 14;
                    d.HeadY = -62;
                    d.TailUp = 0;
                    if (Act == Act.Wash)
                    {
                        d.PawUp = true;
                        d.Eyes = Eyes.Closed;
                        d.HeadTilt = -8 + (float)Math.Sin(t * 10) * 4;
                    }
                    if (Act == Act.Pet) { d.Eyes = Eyes.Happy; d.Blush = true; d.HeadTilt = (float)Math.Sin(t * 3) * 6; }
                    break;
                case Act.Loaf:
                case Act.Sleep:
                    d.Loaf = true;
                    d.BodyY = -15;
                    d.HeadX = 27;
                    d.HeadY = Act == Act.Sleep ? -24 : -30;
                    d.HeadTilt = Act == Act.Sleep ? 12 : 0;
                    d.TailUp = 0;
                    d.Tail = (float)Math.Sin(t * 0.7f) * 0.25f;
                    if (Act == Act.Sleep) { d.Eyes = Eyes.Closed; d.BodyY = -15 + (float)Math.Sin(t * 1.4f) * 0.8f; }
                    if (Riding) { d.Eyes = Eyes.Wide; d.Ears = 0.35f; d.Tail = (float)Math.Sin(t * 10) * 0.7f; }   // holding on tight
                    else if (actT % 7 > 5.5f) d.Eyes = Eyes.Closed;   // slow blinks
                    break;
                case Act.Stretch:
                    d.Stretch = true;
                    d.BodyAngle = 14;
                    d.HeadX = 36;
                    d.HeadY = -30;
                    d.Eyes = Eyes.Closed;
                    d.Tail = 0.8f;
                    if (actT > actLen) Set(Act.Idle, 1.5f);
                    break;
                case Act.Crouch:
                    d.BodyY = -19;
                    d.HeadY = -36;
                    d.HeadX = 32;
                    d.Squash = 0.2f;
                    d.Eyes = Eyes.Wide;
                    d.Tail = (float)Math.Sin(t * 14) * 0.6f;   // the butt wiggle before a jump
                    break;
                case Act.Jump:
                    d.BodyAngle = VY < 0 ? -12 : 12;
                    d.Stride = 0;
                    d.Spread = VY > 300;
                    break;
                case Act.Fall:
                    d.Spread = true;
                    d.Eyes = Eyes.Wide;
                    d.Ears = 0.3f;
                    d.Tail = (float)Math.Sin(t * 12) * 0.8f;
                    break;
                case Act.Land:
                    d.Squash = Math.Max(0, 1 - actT / actLen) * (landSpeed > 1300 ? 0.8f : 0.4f);
                    d.BodyY = -26 + d.Squash * 6;
                    if (actT > actLen) Set(Act.Idle, 1f);
                    break;
                case Act.Dragged:
                    d.Dangle = true;
                    d.Eyes = petT > 0 ? Eyes.Happy : Eyes.Wide;
                    d.Ears = 0.6f;
                    d.Tail = (float)Math.Sin(t * 4) * 0.6f;
                    break;
                case Act.Eat:
                    d.HeadX = 34;
                    d.HeadY = -24 + (float)Math.Sin(t * 12) * 1.5f;
                    d.HeadTilt = 24;
                    d.Eyes = Eyes.Happy;
                    d.Tail = (float)Math.Sin(t * 3) * 0.3f + 0.6f;
                    break;
            }
            if (Hungry && Act != Act.Eat) d.Ears = Math.Min(d.Ears, 0.55f);
            p.Hat = Season.Hat();
            if (blinking > 0 && d.Eyes == Eyes.Open) d.Eyes = Eyes.Closed;
            // look at the mouse when it is near
            float lx = Cursor.X - (X + Dir * 30), ly = Cursor.Y - (Y - 46);
            float dist = (float)Math.Sqrt(lx * lx + ly * ly);
            if (dist < 600 && dist > 1)
            {
                d.LookX = Math.Max(-1, Math.Min(1, lx * Dir / dist * 1.4f));
                d.LookY = Math.Max(-1, Math.Min(1, ly / dist * 1.4f));
            }
            if (Music && !Riding && (Act == Act.Idle || Act == Act.Sit || Act == Act.Loaf))
            {
                // listening to music: only the head turns gently side to side, slow and calm; now and then the eyes close
                d.HeadTilt = (float)Math.Sin(groove / 4) * (7 + Beat * 3);
                if (Math.Sin(groove / 12) > 0.75) d.Eyes = Eyes.Closed;
            }
            // ease the body parts towards the target pose
            float k = Math.Min(1, dt * 12);
            p.BodyY += (d.BodyY - p.BodyY) * k;
            p.BodyAngle += (d.BodyAngle - p.BodyAngle) * k;
            p.HeadX += (d.HeadX - p.HeadX) * k;
            p.HeadY += (d.HeadY - p.HeadY) * k;
            p.HeadTilt += (d.HeadTilt - p.HeadTilt) * k;
            p.Tail += (d.Tail - p.Tail) * k;
            p.TailUp = d.TailUp;
            p.LookX += (d.LookX - p.LookX) * k;
            p.LookY += (d.LookY - p.LookY) * k;
            p.Ears += (d.Ears - p.Ears) * k;
            p.Walk = d.Walk;
            p.Stride += (d.Stride - p.Stride) * k;
            p.Sitting = d.Sitting;
            p.Loaf = d.Loaf;
            p.Dangle = d.Dangle;
            p.Spread = d.Spread;
            p.PawUp = d.PawUp;
            p.Stretch = d.Stretch;
            p.Eyes = d.Eyes;
            p.Squash = d.Squash;
            p.Blush = d.Blush || petT > 0;
        }
    }
}
