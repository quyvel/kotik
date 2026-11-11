using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Media;
using System.Windows.Forms;

namespace Kotik
{
    /// The transparent window the cat lives in. It follows the cat around the screen, draws it with its speech bubble
    /// and little hearts, takes the mouse (pet, pick up, throw) and runs the tray menu.
    class PetForm : Form
    {
        const int W = 340, H = 320, AX = 170, AY = 240;      // canvas size and where the paws are on it (80 px below them for a bubble near the screen top)
        readonly Brain brain = new Brain();
        readonly Surface surface = new Surface(W, H);
        int frameNo;
        readonly Timer frame = new Timer { Interval = 16 };     // 60 fps; drops to 30 when a frame gets expensive
        double workAvg;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly List<float[]> bits = new List<float[]>();   // particles: x, y, vy, life, kind (0 heart, 1 Z)
        readonly Random rnd = new Random();
        FoodForm food;
        DateTime lastLove = DateTime.MinValue;
        GiftForm gift;
        LaserForm laser;
        Buddy buddy;
        DateTime nextBuddy = DateTime.Now.AddMinutes(8);
        DateTime buddyApp = DateTime.MinValue;
        string lastTitle = "";
        readonly Dictionary<string, DateTime> titleSaid = new Dictionary<string, DateTime>();
        double last;
        float platT, slowT, zT, shelfT = 1, hoverT;
        DateTime nextAside;
        string bubble = "";
        float bubbleT;
        Point pressAt;
        double pressTime;
        bool pressing, dragging;
        Point prevCursor;
        float dragVX, dragVY;
        float userActive;          // seconds of use without a long break
        string lastApp = "";
        readonly Dictionary<string, DateTime> appSaid = new Dictionary<string, DateTime>();
        DateTime nextChat, nextHungry, nextLate, nextRam, nextBattery;
        DateTime hiddenUntil = DateTime.MinValue;
        bool hiddenFull;
        bool wasAway;
        Weather weather;                 // leaves and snow over the whole screen (Weather.cs)
        bool diving;
        DateTime nextDive = DateTime.Now.AddMinutes(5);

        public PetForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "Kotik";
            brain.Calm = Settings.Calm;
            brain.Say = Say;
            brain.Ate = OnAte;
            brain.Purr = delegate { Sound("purr"); };
            var now = DateTime.Now;
            nextChat = now.AddMinutes(Next(4, 9));
            nextHungry = now.AddMinutes(1);
            nextLate = now;
            nextRam = now.AddMinutes(2);
            nextBattery = now.AddMinutes(1);
            BuildTray();
            frame.Tick += delegate { Tick(); };
            try { timeBeginPeriod(1); } catch { }   // the default 15.6 ms timer cannot do 60 fps
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000 | 0x80 | 0x8 | 0x08000000;   // layered, tool window, topmost, never takes focus
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            brain.RefreshPlatforms(Handle);
            frame.Start();
            weather = new Weather();
            weather.Show();
            if (Settings.Buddy) StartBuddy(true);
            int h = DateTime.Now.Hour;
            string[] greet = h >= 5 && h < 12 ? Talk.Morning : h < 18 ? Talk.Day : h < 23 ? Talk.Evening : Talk.Night;
            int days = (int)(DateTime.Today - Settings.Since).TotalDays;
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            string season = Season.CatLine();
            if (season != null && Settings.LastGreet != today) Say(season);
            else if (days > 0 && Settings.LastGreet != today)
                Say(string.Format(L.T("Привет, {{0}}! Мы вместе уже {0} {1}.", "Hi, {{0}}! We've been together for {0} {1}."), days + 1, Days(days + 1)));   // once a day
            else
                Say(Talk.Pick(greet));
            Settings.LastGreet = today;
        }

        public static string Days(int n) { return L.Plural(n, "день", "дня", "дней", "day", "days"); }

        void StartBuddy(bool hello)
        {
            buddy = new Buddy(brain, Say);
            buddy.ShowCode = ShowFinding;
            buddy.MoodChanged = OnBuddyMood;
            brain.AskStep = at => buddy != null && buddy.BeStep(at);
            buddy.Answer = OnAnswer;
            buddy.Show();
            StartWatch();
            if (hello)
            {
                var t = new Timer { Interval = 7000 };
                string line = Season.BuddyLine() ?? Lines.Pick(Lines.Hello);
                t.Tick += delegate { t.Stop(); if (buddy != null) buddy.SayNow(line); };
                t.Start();
            }
        }

        double Next(double a, double b) { return a + rnd.NextDouble() * (b - a); }

        // ---------- the frame ----------

        [System.Runtime.InteropServices.DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);

        void Tick()
        {
            double t0 = clock.Elapsed.TotalMilliseconds;
            TickWork();
            // adaptive frame rate: smooth when it is cheap, gentler when the laptop is busy
            workAvg = workAvg * 0.95 + (clock.Elapsed.TotalMilliseconds - t0) * 0.05;
            if (frame.Interval == 16 && workAvg > 9) frame.Interval = 33;
            else if (frame.Interval == 33 && workAvg < 4) frame.Interval = 16;
        }

        void TickWork()
        {
            double now = clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.08, now - last);
            last = now;
            var cur = Cursor.Position;
            float speed = (float)Math.Sqrt(Math.Pow(cur.X - prevCursor.X, 2) + Math.Pow(cur.Y - prevCursor.Y, 2)) / Math.Max(dt, 0.001f);
            brain.CursorSpeed = brain.CursorSpeed * 0.8f + speed * 0.2f;
            brain.Cursor = cur;
            CursorGame(dt, cur, (float)Math.Sqrt(Math.Pow(cur.X - prevCursor.X, 2) + Math.Pow(cur.Y - prevCursor.Y, 2)));
            if (laser != null) { laser.Follow(cur); brain.Laser = laser.Dot; }
            if (dragging)
            {
                brain.Hold(cur);
                dragVX = dragVX * 0.6f + (cur.X - prevCursor.X) / Math.Max(dt, 0.001f) * 0.4f;
                dragVY = dragVY * 0.6f + (cur.Y - prevCursor.Y) / Math.Max(dt, 0.001f) * 0.4f;
            }
            else if (pressing && (Math.Abs(cur.X - pressAt.X) > 6 || Math.Abs(cur.Y - pressAt.Y) > 6))
            {
                dragging = true;
                brain.Grab();
            }
            prevCursor = cur;
            platT -= dt;
            if (platT <= 0) { platT = 0.25f; brain.RefreshPlatforms(Handle); }
            slowT -= dt;
            if (slowT <= 0) { slowT = 1f; EverySecond(); }
            shelfT -= dt;
            if (shelfT <= 0 && !scanning)
            {
                shelfT = 1f;
                IntPtr fgw = Native.GetForegroundWindow();
                bool due = fgw != lastScanWindow || (DateTime.Now - lastScan).TotalSeconds > 6;
                if (Settings.Shelves && !hiddenFull && DateTime.Now > shelfPause && due && budgetLevel >= 1)
                {
                    lastScanWindow = fgw;
                    lastScan = DateTime.Now;
                    StartScan();
                }
                else if (!Settings.Shelves || hiddenFull || DateTime.Now <= shelfPause) brain.SetShelves(new List<Plat>());
            }
            // not in the way: a cursor resting on the cat makes it step aside
            bool over = Math.Abs(cur.X - brain.X) < 45 && cur.Y < brain.Y + 4 && cur.Y > brain.Y - 75;
            hoverT = over && !pressing && brain.On != null && brain.Awake ? hoverT + dt : 0;
            if (hoverT > 1.6f)
            {
                hoverT = 0;
                brain.GoTo(brain.X + (cur.X < brain.X ? 170 : -170), false);
                if (DateTime.Now > nextAside) { nextAside = DateTime.Now.AddMinutes(5); Say(L.T("Мешаю? Подвинусь.", "Am I in the way? I'll move.")); }
            }
            if (hiddenFull) return;
            float peak = music ? Audio.Peak() : 0;
            beat = Math.Max(peak, beat * (float)Math.Pow(0.02, dt));
            brain.Music = music && beat > 0.02f;
            brain.Beat = Math.Min(1, beat * 1.4f);
            if (buddy != null) { buddy.Dancing = music && beat > 0.02f; buddy.Beat = Math.Min(1, beat * 1.4f); }
            if (buddy != null)
            {
                { var cp = Cursor.Position; buddy.HoverHold = Math.Abs(cp.X - buddy.X) < 160 && Math.Abs(cp.Y - buddy.Y) < 110; }   // the mouse is over Ember or its bubble
                buddy.Step(dt, brain.UserIdle);
                brain.Toy = new PointF(buddy.X, buddy.Y);
            }
            else brain.Toy = null;
            frameNo++;
            brain.Update(dt);
            if (buddy != null)
            {
                buddy.Follow();
                if (frameNo % 2 == 0 || buddy.Busy || buddy.Fireworks > 0) buddy.Render();   // half rate while it just floats
            }
            if (weather != null)
            {
                weather.Step(dt, cur, new PointF(brain.X, brain.Y), brain.On != null && (brain.Act == Act.Walk || brain.Act == Act.Run), brain.Dir);
                if (diving && brain.On != null && brain.Act != Act.Fall)
                {
                    diving = false;
                    if (Math.Abs(brain.Y - Screen.PrimaryScreen.WorkingArea.Bottom) < 6) { weather.Burst(brain.X); Hearts(2); }
                }
            }
            UpdateBits(dt);
            if (bubbleT > 0) bubbleT -= dt;
            Render();
        }

        static readonly Font BubbleFont = new Font("Segoe UI", 10f);
        static readonly Font NoteFont = new Font("Segoe UI Symbol", 15f, FontStyle.Bold);
        static readonly Font[] ZFonts = { new Font("Segoe UI", 9f, FontStyle.Bold), new Font("Segoe UI", 10f, FontStyle.Bold),
            new Font("Segoe UI", 11f, FontStyle.Bold), new Font("Segoe UI", 12f, FontStyle.Bold), new Font("Segoe UI", 13f, FontStyle.Bold),
            new Font("Segoe UI", 14f, FontStyle.Bold), new Font("Segoe UI", 15f, FontStyle.Bold), new Font("Segoe UI", 16f, FontStyle.Bold),
            new Font("Segoe UI", 17f, FontStyle.Bold), new Font("Segoe UI", 18f, FontStyle.Bold) };

        int budgetLevel = 2;
        IntPtr lastScanWindow;
        DateTime lastScan;
        bool scanning;
        DateTime shelfPause = DateTime.MinValue, nextTypingNote = DateTime.MinValue;
        Point lastSecondCursor;
        int typing;

        void RememberShelves(List<Plat> found) { lastShelves = found; brain.SetShelves(found); }

        /// The screen scan takes ~0.1 s: it runs on a worker thread so the cat never stutters.
        void StartScan()
        {
            IntPtr fg = Native.GetForegroundWindow();
            Rectangle r;
            if (fg == Handle || !Native.WindowRect(fg, out r)) { brain.SetShelves(new List<Plat>()); return; }
            var cat = new Rectangle((int)brain.X - 55, (int)brain.Y - 85, 110, 92);
            scanning = true;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                List<Plat> found;
                try { found = Shelves.Find(fg, r, cat); }
                catch { found = new List<Plat>(); }
                try { BeginInvoke(new Action(delegate { RememberShelves(found); scanning = false; })); }
                catch { scanning = false; }
            });
        }

        void Render()
        {
            // asleep and quiet: a few frames a second are enough
            bool still = (brain.Act == Act.Sleep || brain.Act == Act.Loaf) && !brain.Riding && !brain.Music && bubbleT <= 0 && bits.Count == 0;
            if (still && ++stillFrames % 6 != 0) return;
            var g = surface.G;
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                Cat.Draw(g, AX, AY, brain.Dir, brain.Pose, Season.CoatToday(Cat.Coats[Settings.Coat]), brain.Time);
                foreach (var b in bits) DrawBit(g, b);
                if (brain.Music && brain.Act != Act.Sleep)
                    for (int i = 0; i < 2; i++)
                    {
                        // little notes floating up from the head
                        float ph = (brain.Time * 0.45f + i * 0.5f) % 1f;
                        float nx = AX + brain.Dir * 18 + (float)Math.Sin(brain.Time * 3 + i * 2) * 8 + i * 14 - 7;
                        float ny = AY + brain.Pose.HeadY - 46 - ph * 46;
                        using (var b = new SolidBrush(Color.FromArgb((int)(230 * (1 - ph)), 255, 214, 120)))
                            g.DrawString(i == 0 ? "♪" : "♫", NoteFont, b, nx, ny);
                    }
                if (bubbleT > 0 && bubble != "") DrawBubble(g);
            }
            surface.Present(Handle, (int)brain.X - AX, (int)brain.Y - AY);
        }

        void DrawBubble(Graphics g)
        {
            var f = BubbleFont;
            {
                var size = g.MeasureString(bubble, f, 230);
                float bw = size.Width + 22, bh = size.Height + 14;
                float headY = AY + brain.Pose.HeadY - 30;
                float bx = AX - bw / 2 + brain.Dir * 20;
                bx = Math.Max(4, Math.Min(W - bw - 4, bx));
                float by = Math.Max(4, headY - bh - 10);
                if (brain.Y - (AY - by) < SystemInformation.VirtualScreen.Top) by = AY + 10;   // no room above: say it below
                int alpha = (int)(255 * Math.Min(1, bubbleT * 3));
                using (var path = new GraphicsPath())
                {
                    float r = 12;
                    path.AddArc(bx, by, r * 2, r * 2, 180, 90);
                    path.AddArc(bx + bw - r * 2, by, r * 2, r * 2, 270, 90);
                    path.AddArc(bx + bw - r * 2, by + bh - r * 2, r * 2, r * 2, 0, 90);
                    float tipX = Math.Max(bx + 20, Math.Min(bx + bw - 20, AX + brain.Dir * 24));
                    path.AddLine(tipX + 8, by + bh, tipX, by + bh + 10);
                    path.AddLine(tipX, by + bh + 10, tipX - 6, by + bh);
                    path.AddArc(bx, by + bh - r * 2, r * 2, r * 2, 90, 90);
                    path.CloseFigure();
                    using (var b = new SolidBrush(Color.FromArgb(alpha * 245 / 255, 255, 252, 246))) g.FillPath(b, path);
                    using (var pen = new Pen(Color.FromArgb(alpha * 200 / 255, 90, 80, 70), 1.6f)) g.DrawPath(pen, path);
                }
                using (var b = new SolidBrush(Color.FromArgb(alpha, 50, 44, 40)))
                    g.DrawString(bubble, f, b, new RectangleF(bx + 11, by + 7, size.Width + 1, size.Height + 4));   // same width as measured: same line breaks
            }
        }

        void DrawBit(Graphics g, float[] b)
        {
            int a = (int)(255 * Math.Max(0, Math.Min(1, b[3])));
            if (b[4] == 3)
            {
                // an autumn leaf
                Color[] leaf = { Color.FromArgb(220, 140, 40), Color.FromArgb(200, 80, 40), Color.FromArgb(230, 190, 60) };
                var st = g.Save();
                g.TranslateTransform(b[0], b[1]);
                g.RotateTransform(b[5] * 60 + b[1] * 2);
                using (var br = new SolidBrush(Color.FromArgb(Math.Min(a, 230), leaf[(int)b[5] % 3]))) g.FillEllipse(br, -5, -2.5f, 10, 5);
                g.Restore(st);
                return;
            }
            if (b[4] == 4)
            {
                // confetti
                Color[] cf = { Color.FromArgb(255, 90, 90), Color.FromArgb(255, 210, 70), Color.FromArgb(90, 200, 255), Color.FromArgb(160, 230, 110) };
                using (var br = new SolidBrush(Color.FromArgb(Math.Min(a, 255), cf[(int)b[5] % 4]))) g.FillRectangle(br, b[0], b[1], 4, 6);
                return;
            }
            if (b[4] == 2)
            {
                // a snowflake
                using (var br = new SolidBrush(Color.FromArgb(Math.Min(a, 200), 255, 255, 255))) g.FillEllipse(br, b[0], b[1], 3 + b[5] % 3, 3 + b[5] % 3);
                return;
            }
            if (b[4] == 0)
            {
                // a heart
                using (var p = new GraphicsPath())
                {
                    float x = b[0], y = b[1], s = 7;
                    p.AddBezier(x, y + s * 0.6f, x - s * 1.4f, y - s * 0.3f, x - s * 0.5f, y - s * 1.3f, x, y - s * 0.5f);
                    p.AddBezier(x, y - s * 0.5f, x + s * 0.5f, y - s * 1.3f, x + s * 1.4f, y - s * 0.3f, x, y + s * 0.6f);
                    using (var br = new SolidBrush(Color.FromArgb(a, 255, 90, 120))) g.FillPath(br, p);
                }
            }
            else
            {
                using (var br = new SolidBrush(Color.FromArgb(a, 120, 140, 200)))
                    g.DrawString("z", ZFonts[Math.Min(ZFonts.Length - 1, (int)b[5])], br, b[0], b[1]);
            }
        }

        void UpdateBits(float dt)
        {
            if (brain.Act == Act.Sleep)
            {
                zT -= dt;
                if (zT <= 0)
                {
                    zT = 1.2f;
                    bits.Add(new float[] { AX + brain.Dir * 34, AY - 44, -22, 2.2f, 1, 0 });
                }
            }
            foreach (var b in bits)
            {
                b[1] += b[2] * dt;
                b[0] += (float)Math.Sin((brain.Time + b[1]) * 2) * 10 * dt;
                b[3] -= dt * 0.7f;
                if (b[4] == 1) b[5] += dt * 4;
                if ((b[4] == 2 || b[4] == 3) && b[1] > AY + 6) b[3] = 0;   // melts or lies down on the ground
                if (b[4] == 4) b[2] += 120 * dt;                                // confetti falls
            }
            bits.RemoveAll(b => b[3] <= 0);
        }

        void Hearts(int n)
        {
            bool confetti = Calendar.On("newyear") || Calendar.On("eve");
            for (int i = 0; i < n; i++)
                if (confetti)
                {
                    // New Year: confetti instead of hearts
                    for (int k = 0; k < 3; k++)
                        bits.Add(new float[] { AX + rnd.Next(-40, 40), AY + brain.Pose.HeadY - 30 - rnd.Next(0, 20), -20 + rnd.Next(0, 60), 1.6f, 4, rnd.Next(4) });
                }
                else
                bits.Add(new float[] { AX + brain.Dir * (brain.Pose.HeadX - 6) + rnd.Next(-16, 16), AY + brain.Pose.HeadY - 22 - rnd.Next(0, 10),
                    -40 - rnd.Next(0, 30), 1.2f + (float)rnd.NextDouble() * 0.6f, 0, 0 });
        }

        public void Say(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try { bubble = string.Format(text, Settings.Owner, Settings.Name); } catch (FormatException) { bubble = text; }
            bubble = Buddy.Capital(bubble);
            bubbleT = Math.Min(9f, 3f + bubble.Length * 0.07f);
        }

        int trimCount, stillFrames;

        // ---------- music: Spotify plays → Ember dances, the cat sways ----------
        string song = "";
        bool music;
        float beat;
        static string[] SongLines { get { return L.A(new[] { "О, «{0}». Хороший выбор.", "Под «{0}» и работается лучше.", "«{0}»! Котик, танцуем.",
            "Слышу «{0}». Подпеваю. Мысленно.", "«{0}» — запомню, что тебе нравится." },
            new[] { "Oh, \"{0}\". Good choice.", "\"{0}\" makes work go better.", "\"{0}\"! Kitty, let's dance.",
            "I hear \"{0}\". Singing along. In my head.", "\"{0}\" — I'll remember you like it." }); } }


        void CheckMusic()
        {
            string title = Native.TitleOf("Spotify", " - ");        // "Artist - Song" while playing, even when hidden in the tray
            bool was = music;
            music = title != "" && Settings.Feature("music");
            if (music && !was && brain.Act == Act.Sleep) brain.WakeUp();     // music wakes the cat: time to dance
            if (music && title != song && buddy != null && !buddy.Busy && rnd.NextDouble() < 0.3)
            {
                int dash = title.IndexOf(" - ");
                string name = dash > 0 ? title.Substring(dash + 3) : title;
                if (name.Length > 40) name = name.Substring(0, 40) + "…";
                buddy.SayNow(string.Format(SongLines[rnd.Next(SongLines.Length)], name.Replace("{", "(").Replace("}", ")")));
            }
            if (music) song = title;
            else song = "";
        }

        // ---------- the naughty cat: catches an idly circling cursor and carries it off to hide ----------
        float wiggle;                       // how much the cursor has wandered lately (decays): circling without purpose
        bool holding;                       // the cursor is in the cat's teeth
        float holdT, pull;
        int tugs;
        float hideX;
        Point heldAt;
        DateTime nextCatch = DateTime.Now.AddMinutes(3);
        static string[] Caught { get { return L.A(new[] { "Поймал! Это теперь моё.", "Ага! Курсор мой!", "Попался! Унесу и спрячу.", "Мрр-ням. Беру с собой." },
            new[] { "Got it! It's mine now.", "Aha! The cursor is mine!", "Gotcha! I'll take it away and hide it.", "Mrr-nom. Taking it with me." }); } }
        static string[] Resist { get { return L.A(new[] { "Не отдам!", "Моё!", "Ррр!", "Держу крепко!" }, new[] { "Not giving it back!", "Mine!", "Grrr!", "Holding tight!" }); } }
        static string[] GiveUp { get { return L.A(new[] { "Ладно, забирай…", "Эх, сильная хватка.", "Ну и пожалуйста. В следующий раз не отдам." },
            new[] { "Fine, take it…", "Wow, strong grip.", "Fine then. Next time I won't give it back." }); } }
        static string[] Hidden { get { return L.A(new[] { "Спрятал! Потом поиграем.", "Вот тут полежит. Мой тайник.", "Готово. Никому не говори, где он." },
            new[] { "Hidden! Let's play later.", "It'll lie here. My secret stash.", "Done. Don't tell anyone where it is." }); } }

        PointF Mouth { get { return new PointF(brain.X + brain.Dir * 26, brain.Y - 28); } }

        void CursorGame(float dt, Point cur, float moved)
        {
            wiggle = wiggle * (float)Math.Exp(-dt / 2.0) + moved;
            if (!Settings.Feature("naughty") || dragging) { if (holding) Release(null); return; }
            if (!holding)
            {
                // the cat is mid-jump right at the cursor that has been circling about for no reason: caught
                var m = Mouth;
                float d = (float)Math.Sqrt((cur.X - m.X) * (cur.X - m.X) + (cur.Y - m.Y) * (cur.Y - m.Y));
                if (brain.Act == Act.Jump && d < 45 && wiggle > 1500 && typing == 0 && DateTime.Now > nextCatch && !Native.MouseButtonDown())
                {
                    holding = true;
                    holdT = 0;
                    pull = 0;
                    tugs = 0;
                    var wa = Screen.FromPoint(new Point((int)brain.X, (int)brain.Y)).WorkingArea;
                    hideX = brain.X < (wa.Left + wa.Right) / 2 ? wa.Left + 40 : wa.Right - 40;   // to the nearest side
                    heldAt = Point.Round(m);
                    Native.SetCursorPos(heldAt.X, heldAt.Y);
                    Say(Caught[rnd.Next(Caught.Length)]);
                }
                return;
            }
            holdT += dt;
            // a click always wins (maybe something is urgent), and so does a long fight
            if (Native.MouseButtonDown() || holdT > 7) { Release(GiveUp); return; }
            int dx = cur.X - heldAt.X, dy = cur.Y - heldAt.Y;
            float tug = (float)Math.Sqrt(dx * dx + dy * dy);
            if (tug > 25) pull += tug;
            if (pull > 260)
            {
                pull = 0;
                tugs++;
                if (tugs >= 2 && rnd.NextDouble() < 0.5 || tugs >= 4) { Release(GiveUp); return; }
                Say(Resist[rnd.Next(Resist.Length)]);
            }
            if (brain.On != null && Math.Abs(brain.X - hideX) > 12 && brain.Act != Act.Run && brain.Act != Act.Jump && brain.Act != Act.Crouch)
                brain.GoTo(hideX, true);
            if (brain.On != null && Math.Abs(brain.X - hideX) <= 14) { Release(Hidden); return; }
            heldAt = Point.Round(Mouth);
            Native.SetCursorPos(heldAt.X, heldAt.Y);
        }

        void Release(string[] line)
        {
            holding = false;
            nextCatch = DateTime.Now.AddMinutes(Next(4, 10));
            wiggle = 0;
            if (line != null) Say(line[rnd.Next(line.Length)]);
        }
        DateTime nextMoodReply = DateTime.Now.AddMinutes(2);

        static string[] CatHappy { get { return L.A(new[] { "Огонёк светится — и мне хорошо.", "Мрр! Огонёк радуется, и я тоже.", "Хорошее настроение заразно. Мяу." },
            new[] { "Ember is glowing, and I feel good.", "Purr! Ember is happy, and so am I.", "Good mood is contagious. Meow." }); } }
        static string[] CatCurious { get { return L.A(new[] { "Огонёк что-то нашёл? Я тоже хочу посмотреть.", "Что там, Огонёк? Что там?", "Любопытство у нас общее." },
            new[] { "Did Ember find something? I want to see too.", "What is it, Ember? What is it?", "We share the curiosity." }); } }
        static string[] CatSleepy { get { return L.A(new[] { "Огонёк зевает… и я зеваю.", "Тихий час. Мы оба за.", "Огонёк засыпает. Пожалуй, и я." },
            new[] { "Ember yawns… and so do I.", "Quiet hour. We're both in.", "Ember is falling asleep. Me too, I think." }); } }
        static string[] CatExcited { get { return L.A(new[] { "Огонёк носится! Догоню!", "Ура-ура! Что происходит?!", "Огонёк в ударе. Я тоже!" },
            new[] { "Ember is zooming around! I'll catch up!", "Yay-yay! What's happening?!", "Ember is on fire. Me too!" }); } }
        static string[] CatFocused { get { return L.A(new[] { "Тсс… Огонёк сосредоточен. Я тоже серьёзный.", "Работаем. Я тут охраняю тишину.", "Огонёк нахмурился. Значит, всё серьёзно." },
            new[] { "Shh… Ember is focused. I'm serious too.", "Working. I'm guarding the silence.", "Ember is frowning. Must be serious." }); } }

        /// The cat answers Ember's mood now and then (not every time — that would be too much).
        void OnBuddyMood(Buddy.Mood m)
        {
            if (DateTime.Now < nextMoodReply || bubbleT > 0 || !brain.Awake) return;
            if (rnd.NextDouble() > 0.45) return;
            nextMoodReply = DateTime.Now.AddMinutes(Next(4, 9));
            string[] list = m == Buddy.Mood.Happy ? CatHappy : m == Buddy.Mood.Curious ? CatCurious : m == Buddy.Mood.Sleepy ? CatSleepy
                : m == Buddy.Mood.Excited ? CatExcited : m == Buddy.Mood.Focused ? CatFocused : null;
            if (list == null) return;
            Say(list[rnd.Next(list.Length)]);
            if (m == Buddy.Mood.Happy) Hearts(2);
        }


        /// A heap of leaves or snow right next to the cat: sometimes it dives in.
        void TryDive(DateTime now)
        {
            if (now < nextDive || brain.On == null || !brain.Awake || dragging || holding || brain.Calm || weather.Kind == Weather.None) return;
            if (brain.Act != Act.Sit && brain.Act != Act.Idle && brain.Act != Act.Walk) return;
            var wa = Screen.PrimaryScreen.WorkingArea;
            if (Math.Abs(brain.Y - wa.Bottom) > 4) return;
            if (brain.X + brain.Dir * 230 < wa.Left + 40 || brain.X + brain.Dir * 230 > wa.Right - 40) brain.Dir = -brain.Dir;
            if (weather.Around(brain.X + brain.Dir * 200, 50) < (weather.Kind == Weather.Snow ? 16 : 8) || rnd.NextDouble() > 1 / 30.0) return;
            nextDive = now.AddMinutes(Next(5, 10));
            diving = true;
            brain.Dive();
            Say(weather.Kind == Weather.Snow ? L.T("В сугроб! Уиии!", "Into the snowdrift! Wheee!") : L.T("В кучу листьев! Уиии!", "Into the leaf pile! Wheee!"));
        }


        /// Windows sometimes pushes the cat and Ember below an ordinary window although they are topmost: put them back on top.
        void KeepOnTop()
        {
            const uint flags = 0x1 | 0x2 | 0x10 | 0x200;    // no size, no move, no activate, no owner z-order
            if (weather != null) Native.SetWindowPos(weather.Handle, new IntPtr(-1), 0, 0, 0, 0, flags);   // first: under everything else
            Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, flags);
            if (buddy != null) Native.SetWindowPos(buddy.Handle, new IntPtr(-1), 0, 0, 0, 0, flags);
            // the decorations sit on the taskbar, which is topmost too: lift them above it
            foreach (Form f in new Form[] { tree, pumpkin, snowman, garland })
                if (f != null && !f.IsDisposed) Native.SetWindowPos(f.Handle, new IntPtr(-1), 0, 0, 0, 0, flags);
        }

        // ---------- once a second: moods, reminders, comments ----------

        void EverySecond()
        {
            var now = DateTime.Now;
            if (++trimCount % 120 == 0) Native.TrimMemory();
            if (trimCount % 5 == 0) KeepOnTop();
            if (trimCount % 2 == 0) CheckMusic();
            if (buddy != null) buddy.UserTyping = typing > 0;
            if (trimCount % 30 == 1) { budgetLevel = Budget.Level; Decorate(); }
            if (buddy != null && now > nextTogether && brain.On != null && brain.Awake && !dragging && Native.IdleSeconds() < 120 && !buddy.Busy)
            {
                nextTogether = now.AddMinutes(Next(12, 25));
                if (rnd.NextDouble() < 0.6) buddy.RideCat((float)Next(25, 50));
                else { brain.Riding = true; buddy.CarryCat((float)Next(10, 16)); Say(Talk.Pick(Talk.Ride)); }
            }
            if (buddy != null && Calendar.On("kindness") && now > nextHeart) { nextHeart = now.AddMinutes(Next(8, 15)); HeartToCursor(); }
            if (buddy != null && buddy.Playing && (typing > 0 || buddy.Box.HasValue && buddy.Box.Value.Contains(Cursor.Position))) buddy.StopBall();
            if (buddy != null && Calendar.On("winter") && Budget.Level >= 1 && now > nextSnowball && brain.On != null && brain.Awake && !dragging)
            {
                nextSnowball = now.AddMinutes(Next(4, 9));
                SnowballAtBuddy();
            }
            bool full = Native.ForegroundIsFullscreen();
            if (full != hiddenFull)
            {
                hiddenFull = full;
                if (weather != null) weather.Visible = !full;
                if (full)
                {
                    Native.ShowBitmap(Handle, new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb), -10, -10);
                    if (buddy != null) Native.ShowBitmap(buddy.Handle, new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb), -10, -10);
                }
            }
            float idle = (float)Native.IdleSeconds();
            brain.UserIdle = idle;
            TryBall(now, idle);
            if (weather != null)
            {
                bool leaves = Settings.Feature("calendar") && (Calendar.On("autumn") || Calendar.On("autumnend")) && budgetLevel >= 1;
                weather.Kind = Season.Snow() ? Weather.Snow : leaves ? Weather.Leaves : Weather.None;
                weather.Rate = Calendar.On("autumnend") ? 2 : 1;
                bool video = Native.DisplayRequired();       // music does not keep the screen on, a video does
                weather.Quiet = video;
                weather.Away = idle > 60 && !video;
                TryDive(now);
            }
            if (buddy != null)
            {
                string fa = Native.ForegroundProcess();
                buddy.Free = fa == "code" || fa == "explorer" || Native.ForegroundIsDesktop();
            }
            // typing (input without mouse movement): the cat leaves the program's shelves so it is not in the way
            var c = Cursor.Position;
            typing = idle < 1 && c == lastSecondCursor ? typing + 1 : 0;
            lastSecondCursor = c;
            if (typing >= 4 && brain.On != null && brain.On.Shelf)
            {
                shelfPause = now.AddSeconds(40);
                brain.SetShelves(new List<Plat>());
                if (now > nextTypingNote && Settings.Talk > 0) { nextTypingNote = now.AddMinutes(30); Say(L.T("Не буду мешать печатать.", "I won't get in the way of your typing.")); }
            }
            if (idle > 300) { userActive = 0; wasAway = true; }
            else userActive += 1;
            if (wasAway && idle < 2)
            {
                wasAway = false;
                brain.WakeUp();
                if (weather != null && weather.Kind == Weather.Snow && weather.Around(brain.X, 80) > 24)
                {
                    // a surprise: while you were away the cat wrote you something in the snow
                    string[] words = L.En ? new[] { "HI", "MEOW", "♥", Settings.Owner.ToUpper() } : new[] { "ПРИВЕТ", "МЯУ", "♥", Settings.Owner.ToUpper(), "ЖДАЛ" };
                    weather.Carve(words[rnd.Next(words.Length)], brain.X);
                    Say(L.T("Пока тебя не было, я написал тебе на снегу. Посмотри вниз! Мышкой можно стереть.", "While you were away, I wrote you something in the snow. Look down! The mouse can erase it."));
                }
                else if (Settings.Feature("gifts") && Settings.Love >= 25 && Settings.LastGift != now.ToString("yyyy-MM-dd") && brain.On != null && gift == null) BringGift();
                else if (Settings.Talk > 0) Say(Talk.Pick(Talk.Back));
            }
            brain.Hungry = (now - Settings.FedAt).TotalHours > 7;
            int who;
            bool fireworks;
            string moment = Season.Due(now, out who, out fireworks);
            if (buddy != null && fireworks) buddy.Fireworks = 2f;
            if (moment != null)
            {
                if (who == 1 && buddy != null) buddy.SayNow(moment);
                else Say(moment);
                Hearts(3);
            }
            if (buddy != null && Settings.Talk > 0 && idle < 60)
            {
                if (now > nextBuddy && !buddy.Riding)
                {
                    nextBuddy = now.AddMinutes(Settings.Talk == 2 ? Next(8, 15) : Next(18, 35));
                    double r = rnd.NextDouble();
                    bool late = now.Hour >= 1 && now.Hour < 5 && Settings.Feature("night");
                    string[] pool = late ? Lines.Late : r < 0.2 ? Lines.CatFacts : r < 0.55 && Settings.Feature("tips") ? Lines.Tips : Lines.Kind;
                    buddy.SayNow(Lines.Pick(pool));
                }
                string fgApp = Native.ForegroundProcess();
                string title = Native.ForegroundTitle();
                if (title != lastTitle && Settings.Feature("apps"))
                {
                    lastTitle = title;
                    foreach (var pair in Lines.Titles)
                    {
                        if (title.IndexOf(pair[0], StringComparison.OrdinalIgnoreCase) < 0) continue;
                        DateTime said;
                        if (titleSaid.TryGetValue(pair[0], out said) && (now - said).TotalHours < 2) break;
                        titleSaid[pair[0]] = now;
                        if (rnd.NextDouble() < 0.6) buddy.SayNow(pair[1]);
                        break;
                    }
                }
                if ((now - buddyApp).TotalMinutes > 30 && Settings.Feature("apps"))
                    foreach (var kv in Lines.Apps)
                        if (fgApp.Contains(kv.Key) && rnd.NextDouble() < 0.02)
                        {
                            buddyApp = now;
                            buddy.SayNow(kv.Value);
                            break;
                        }
            }
            if (Settings.Talk == 0 || !brain.Awake || dragging) return;
            if (userActive > 90 * 60 && Settings.Feature("breaks"))
            {
                userActive = 0;
                Say(Talk.Pick(Talk.Break));
                return;
            }
            if (brain.Hungry && now > nextHungry)
            {
                nextHungry = now.AddMinutes(40);
                Say(Talk.Pick(Talk.Hungry));
                Sound("meow");
                return;
            }
            if ((now.Hour >= 1 && now.Hour < 5) && now > nextLate && idle < 30 && Settings.Feature("night"))
            {
                nextLate = now.AddMinutes(45);
                Say(Talk.Pick(Talk.Late));
                return;
            }
            if (now > nextRam)
            {
                nextRam = now.AddMinutes(1);
                if (Native.MemoryLoad() >= 90) { nextRam = now.AddMinutes(30); Say(Talk.Pick(Talk.Ram)); return; }
            }
            if (now > nextBattery)
            {
                nextBattery = now.AddMinutes(1);
                var ps = SystemInformation.PowerStatus;
                if (ps.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery && ps.PowerLineStatus == PowerLineStatus.Offline && ps.BatteryLifePercent <= 0.15f)
                {
                    nextBattery = now.AddMinutes(20);
                    Say(Talk.Pick(Talk.Battery));
                    return;
                }
            }
            string app = Native.ForegroundProcess();
            if (app != lastApp && Settings.Feature("apps"))
            {
                lastApp = app;
                foreach (var kv in Talk.Apps)
                {
                    if (!app.Contains(kv.Key)) continue;
                    DateTime said;
                    if (appSaid.TryGetValue(kv.Key, out said) && (now - said).TotalHours < 3) break;
                    appSaid[kv.Key] = now;
                    if (Calendar.On("hello")) { Say(L.T("Привет, программа! Сегодня день приветствий.", "Hello, program! It's World Hello Day.")); return; }
                    if (rnd.NextDouble() < (Settings.Talk == 2 ? 0.9 : 0.5)) { Say(Talk.Pick(kv.Value)); return; }
                    break;
                }
            }
            if (now > nextChat)
            {
                nextChat = now.AddMinutes(Settings.Talk == 2 ? Next(4, 9) : Next(12, 25));
                if (idle < 60 && (buddy == null || !buddy.Riding)) Say(Talk.Pick(Talk.Chatter));
            }
        }

        // ---------- the mouse ----------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                pressing = true;
                dragging = false;
                pressAt = Cursor.Position;
                pressTime = clock.Elapsed.TotalSeconds;
                Capture = true;
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Sound("meow");
            Say(L.T("Мяу!", "Meow!"));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            Capture = false;
            if (e.Button == MouseButtons.Right)
            {
                tray.ContextMenuStrip.Show(Cursor.Position);
                return;
            }
            if (!pressing) return;
            pressing = false;
            if (dragging)
            {
                dragging = false;
                brain.Throw(dragVX, dragVY);
                return;
            }
            // a click: petting
            if ((DateTime.Now - lastLove).TotalSeconds > 5) { lastLove = DateTime.Now; Settings.Love = Settings.Love + 1; }
            brain.Petted();
            Hearts(3);
            if (Calendar.On("blackfriday") && rnd.NextDouble() < 0.6)
                Say(Talk.Pick(L.A(new[] { "Поглаживание со скидкой −100%! Только сегодня.", "Акция: второе поглаживание в подарок!", "Чёрная пятница: мурчание по цене тишины." },
                    new[] { "Pets at 100% off! Today only.", "Special offer: the second pet is free!", "Black Friday: purring for the price of silence." })));
            else if (rnd.NextDouble() < 0.45) Say(Talk.Pick(Talk.Petted));
        }

        // ---------- food ----------

        void Feed()
        {
            if (food != null) { food.Close(); food = null; brain.Food = null; }   // a forgotten fish is replaced
            if (!brain.Hungry) Say(Talk.Pick(Talk.Full));
            // always on the taskbar of the cat's screen: the cat comes down from any window or shelf to eat
            var wa = Screen.FromPoint(new Point((int)brain.X, (int)brain.Y)).WorkingArea;
            float fx = brain.X + (rnd.Next(2) == 0 ? -1 : 1) * (float)Next(120, 260);
            fx = Math.Max(wa.Left + 40, Math.Min(wa.Right - 40, fx));
            float fy = wa.Bottom;
            food = new FoodForm(fx, fy);
            food.Show();
            brain.Food = new PointF(fx, fy);
        }

        void ToggleLaser()
        {
            if (laser != null) { laser.Close(); laser = null; brain.Laser = null; Say(L.T("Эх. Было весело.", "Aww. That was fun.")); return; }
            laser = new LaserForm();
            laser.Show();
            Say(L.T("Красная точка!!! Где она?!", "The red dot!!! Where is it?!"));
            if (buddy != null) buddy.SayNow(L.T("Води мышкой — {1} будет охотиться. Это лазер.", "Move the mouse and {1} will hunt. It's a laser."));
            var stop = new Timer { Interval = 90000 };
            stop.Tick += delegate { stop.Stop(); if (laser != null) ToggleLaser(); };
            stop.Start();
        }

        static string[] GiftNames { get { return L.A(new[] { "мышку", "клубок", "пёрышко", "носок", "крышечку", "бантик", "мандарин", "купон «−50% на рыбку»" },
            new[] { "a mouse", "a ball of yarn", "a feather", "a sock", "a bottle cap", "a bow", "a tangerine", "a \"50% off fish\" coupon" }); } }

        /// The cat brings a present and leaves it next to itself. Click it to keep it.
        void BringGift()
        {
            int kind = Calendar.On("eve") || Calendar.On("newyear") ? 6 : Calendar.On("singles") ? 7 : rnd.Next(GiftNames.Length - 2);
            Settings.LastGift = DateTime.Now.ToString("yyyy-MM-dd");
            float gx = brain.X + brain.Dir * 46;
            gift = new GiftForm(kind, gx, brain.Y);
            gift.Taken += delegate
            {
                Settings.Gifts = Settings.Gifts + 1;
                Hearts(4);
                Say(L.T("Ура, тебе понравилось!", "Yay, you liked it!"));
                int n = Settings.Gifts;
                if (buddy != null) buddy.SayNow(string.Format(L.T("{{1}} принёс тебе уже {0} {1}.", "{{1}} has brought you {0} {1} so far."), n,
                    L.Plural(n, "подарок", "подарка", "подарков", "present", "presents")));
                gift = null;
            };
            gift.Show();
            Say(L.T("Я принёс тебе ", "I brought you ") + GiftNames[kind] + L.T("! Это подарок. Нажми, чтобы забрать.", "! It's a present. Click it to take it."));
        }

        void OnAte()
        {
            if (food != null) { food.Close(); food = null; }
            Settings.FedAt = DateTime.Now;
            Settings.Love = Settings.Love + 4;
            brain.Hungry = false;
            Hearts(5);
            Say(Talk.Pick(Talk.Fed));
            Sound("meow");
        }

        // ---------- sounds ----------

        void Sound(string name)
        {
            if (!Settings.Sounds) return;
            try
            {
                if (name == "purr")
                {
                    // your own recorded purr wins: put purr.wav next to Kotik.exe
                    string dir = Path.GetDirectoryName(Application.ExecutablePath);
                    foreach (var f in new[] { "purr.wav" })
                        if (File.Exists(Path.Combine(dir, f))) { new SoundPlayer(Path.Combine(dir, f)).Play(); return; }
                }
                var s = typeof(PetForm).Assembly.GetManifestResourceStream("Kotik." + name + ".wav");
                if (s == null) return;
                var p = new SoundPlayer(s);
                p.Play();
            }
            catch { }
        }

        // ---------- the tray ----------

        void BuildTray()
        {
            tray.Icon = Art.TrayIcon(Cat.Coats[Settings.Coat]);
            tray.Text = Settings.Name;
            tray.Visible = true;
            var m = new ContextMenuStrip();
            m.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            m.ForeColor = Color.FromArgb(235, 230, 220);
            m.Opening += delegate { Fill(m); };
            tray.ContextMenuStrip = m;
            tray.MouseClick += delegate (object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) brain.Call(Cursor.Position);
            };
            Fill(m);
        }

        void Fill(ContextMenuStrip m)
        {
            m.Items.Clear();
            int hearts = 1 + Settings.Love / 25;
            var head = new ToolStripMenuItem(Settings.Name + "   " + new string('♥', Math.Min(5, hearts)) + new string('♡', 5 - Math.Min(5, hearts)) +
                (Settings.Gifts > 0 ? L.T("   подарков: ", "   presents: ") + Settings.Gifts : "")) { Enabled = false };
            m.Items.Add(head);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem(L.T("Покормить рыбкой", "Feed a fish"), null, delegate { Feed(); }));
            m.Items.Add(new ToolStripMenuItem(L.T("Позвать к курсору", "Call to the cursor"), null, delegate { brain.Call(Cursor.Position); }));
            m.Items.Add(new ToolStripMenuItem(laser == null ? L.T("Поиграть лазером", "Play with the laser") : L.T("Убрать лазер", "Put the laser away"), null, delegate { ToggleLaser(); }));
            if (buddy != null)
            {
                m.Items.Add(new ToolStripMenuItem(L.T("Огонёк, садись на кота", "Ember, ride the cat"), null, delegate { buddy.RideCat(40); }));
                m.Items.Add(new ToolStripMenuItem(L.T("Огонёк, покатай кота", "Ember, give the cat a ride"), null, delegate { brain.Riding = true; buddy.CarryCat(14); Say(Talk.Pick(Talk.Ride)); }));
            }
            if (Calendar.On("winter") && Budget.Level >= 1)
                m.Items.Add(new ToolStripMenuItem(L.T("Кинуть снежок в кота", "Throw a snowball at the cat"), null, delegate { SnowballAtCat(); }));
            if (Calendar.On("eve") && Season.Today.Day == 31)
                m.Items.Add(new ToolStripMenuItem(!Settings.WishThisYear ? L.T("Загадать желание…", "Make a wish…") : L.T("Желание загадано ✓", "Wish made ✓"), null, delegate { MakeWish(); }));
            if (resting)
                m.Items.Add(new ToolStripMenuItem(L.T("Разбудить", "Wake up"), null, delegate { WakeFromRest(); }));
            else
            {
                var rest = new ToolStripMenuItem(L.T("Отдохнуть…", "Take a rest…"));
                rest.DropDownItems.Add(new ToolStripMenuItem(L.T("15 минут", "15 minutes"), null, delegate { Rest(DateTime.Now.AddMinutes(15)); }));
                rest.DropDownItems.Add(new ToolStripMenuItem(L.T("30 минут", "30 minutes"), null, delegate { Rest(DateTime.Now.AddMinutes(30)); }));
                rest.DropDownItems.Add(new ToolStripMenuItem(L.T("1 час", "1 hour"), null, delegate { Rest(DateTime.Now.AddHours(1)); }));
                rest.DropDownItems.Add(new ToolStripMenuItem(L.T("2 часа", "2 hours"), null, delegate { Rest(DateTime.Now.AddHours(2)); }));
                rest.DropDownItems.Add(new ToolStripMenuItem(L.T("До завтра", "Until tomorrow"), null, delegate { Rest(DateTime.Today.AddDays(1).AddHours(8)); }));
                m.Items.Add(rest);
            }
            m.Items.Add(new ToolStripMenuItem(L.T("Больше настроек…", "More settings…"), null, delegate { OpenSettings(); }));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem(L.T("Выход", "Quit"), null, delegate
            {
                Say(Talk.Pick(Talk.Bye));
                var t = new Timer { Interval = 1400 };
                t.Tick += delegate { tray.Visible = false; Application.Exit(); };
                t.Start();
            }));
        }

        SettingsForm settingsWin;

        void OpenSettings()
        {
            if (settingsWin != null && !settingsWin.IsDisposed) { settingsWin.Activate(); return; }
            settingsWin = new SettingsForm(delegate
            {
                brain.Calm = Settings.Calm;
                tray.Text = Settings.Name;
                tray.Icon = Art.TrayIcon(Cat.Coats[Settings.Coat]);
                if (Settings.Buddy && buddy == null) StartBuddy(true);
                else if (!Settings.Buddy && buddy != null) { buddy.Close(); buddy = null; }
                if (Settings.Feature("codecheck")) StartWatch();
                Decorate();
            });
            settingsWin.Show();
        }

        // ---------- code hints (optional, off by default) ----------

        CodeWatch watch;
        readonly Dictionary<string, DateTime> noticed = new Dictionary<string, DateTime>();

        void StartWatch()
        {
            if (watch != null || !Settings.Feature("codecheck")) return;
            watch = new CodeWatch();
            watch.Saved += path =>
            {
                if (buddy == null || !Settings.Feature("codecheck") || Settings.Has("IgnoredFiles", path)) return;
                // only your own typing: VS Code in front with this very file, and the keyboard used a moment ago
                // (files written by programs, generators or AI assistants are skipped)
                string fg = Native.ForegroundProcess(), title = Native.ForegroundTitle();
                double idle = Native.IdleSeconds();
                bool typed = fg == "code" && idle < 6
                    && title.IndexOf(System.IO.Path.GetFileName(path), StringComparison.OrdinalIgnoreCase) >= 0;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    List<Finding> found;
                    string text = "";
                    try { found = Lint.Check(path); text = System.IO.File.ReadAllText(path); } catch { found = new List<Finding>(); }
                    try { BeginInvoke(new Action(delegate { OnReview(path, text, found, typed); })); } catch { }
                });
            };
        }


        readonly Dictionary<string, string> seenText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// Line numbers (1-based) that are new compared with the last save; null if this file was not seen before.
        HashSet<int> NewLines(string path, string text)
        {
            string old;
            bool had = seenText.TryGetValue(path, out old);
            seenText[path] = text;
            var lines = text.Split('\n');
            if (!had && lines.Length > 60) return null;          // a big file seen for the first time: only remember it
            var oldSet = new HashSet<string>();
            if (had) foreach (var s in old.Split('\n')) oldSet.Add(s.Trim());
            var res = new HashSet<int>();
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Trim().Length > 0 && !oldSet.Contains(lines[i].Trim())) res.Add(i + 1);
            return res;
        }

        readonly Dictionary<string, Finding> queued = new Dictionary<string, Finding>();   // noticed in new lines, not shown yet
        static string[] FixedLines { get { return L.A(new[] { "Исправлено — молодец! ", "Вот теперь хорошо. ", "Чисто! Так держать. " }, new[] { "Fixed, well done! ", "Now that's good. ", "Clean! Keep it up. " }); } }

        void OnReview(string path, string text, List<Finding> found, bool typed)
        {
            if (buddy == null) return;
            // the place Ember talked about is fixed: say so, forget it; findings that are gone leave the queue
            var now = new Dictionary<string, Finding>();
            foreach (var f in found) now[f.Key] = f;
            string praise = "";
            var cur = buddy.Pending ?? buddy.Last;
            if (cur != null && string.Equals(cur.File, path, StringComparison.OrdinalIgnoreCase) && !now.ContainsKey(cur.Key))
            {
                buddy.Fixed();
                praise = FixedLines[rnd.Next(FixedLines.Length)];
            }
            foreach (var k in new List<string>(queued.Keys))
                if (string.Equals(queued[k].File, path, StringComparison.OrdinalIgnoreCase))
                {
                    if (now.ContainsKey(k)) queued[k] = now[k]; else queued.Remove(k);   // fresh line numbers
                }
            var fresh = NewLines(path, text);
            // not typed by hand, nothing new, or a big paste: nothing new to look at (but the queue may still have something)
            if (!typed || fresh == null || fresh.Count == 0 || fresh.Count > 40) { NextQueued(path, praise); return; }
            var sb = new System.Text.StringBuilder();
            var all = text.Split('\n');
            foreach (int n in fresh) sb.Append(all[n - 1]).Append('\n');
            text = sb.ToString();                                // topics: only in what was just written
            found = found.FindAll(f => fresh.Contains(f.Line) || fresh.Contains(f.Line - 1) || fresh.Contains(f.Line + 1));
            // something new in the code: Ember gets curious (once per topic, ever)
            foreach (var tp in Lint.Topics)
                if (!Settings.Has("SeenTopics", tp[0]) && System.Text.RegularExpressions.Regex.IsMatch(text, tp[0], System.Text.RegularExpressions.RegexOptions.Multiline))
                {
                    Settings.AddTo("SeenTopics", tp[0]);
                    buddy.SayNow(L.T("О, ", "Oh, ") + tp[1] + L.T("! Что-то новенькое. ", "! Something new. ") + tp[2]);
                    return;
                }
            foreach (var f in found) if (!Settings.Has("Dismissed", f.Key)) queued[f.Key] = f;
            NextQueued(path, praise);
        }

        /// One thing at a time: the first waiting problem in this file (by line), or just the praise.
        void NextQueued(string path, string praise)
        {
            Finding next = null;
            foreach (var f in queued.Values)
            {
                if (!string.Equals(f.File, path, StringComparison.OrdinalIgnoreCase) || Settings.Has("Dismissed", f.Key)) continue;
                if (buddy.Pending != null && buddy.Pending.Key == f.Key) return;          // already waiting for a click
                DateTime when;
                if (noticed.TryGetValue(f.Key, out when) && (DateTime.Now - when).TotalMinutes < 10) continue;   // not the same nag again
                if (next == null || f.Line < next.Line) next = f;
            }
            if (next != null)
            {
                noticed[next.Key] = DateTime.Now;
                queued.Remove(next.Key);
                buddy.Notice(next, praise == "" ? "" : praise + L.T("А ещё… ", "And also… "));
            }
            else if (praise != "") buddy.SayNow(praise.Trim());
        }

        /// The click on Ember: VS Code jumps to the line, Ember finds it on the screen and points at it.
        void ShowFinding(Finding f)
        {
            IntPtr fg = Native.GetForegroundWindow();
            Rectangle r;
            List<Plat> before = new List<Plat>();
            if (Native.ForegroundProcess() == "code" && Native.WindowRect(fg, out r)) before = Shelves.Find(fg, r, Rectangle.Empty);
            CodeWatch.Reveal(f.File, f.Line);
            var t = new Timer { Interval = 900 };
            t.Tick += delegate
            {
                t.Stop();
                IntPtr w = Native.GetForegroundWindow();
                Rectangle wr;
                string say = (f.Message + L.T(" Правый клик по мне: «ошибка» или «так задумано».", " Right-click me: \"a bug\" or \"intended\".")).Replace("{", "{{").Replace("}", "}}");
                if (buddy == null) return;
                if (Native.ForegroundProcess() == "code" && Native.WindowRect(w, out wr))
                {
                    var after = Shelves.Find(w, wr, Rectangle.Empty);
                    var line = CodeWatch.CursorLine(wr, before, after);
                    if (line.HasValue) { buddy.PointAt(line.Value, say); return; }
                }
                buddy.SayLong(L.T("Строка ", "Line ") + f.Line + ": " + say);
            };
            t.Start();
        }

        void OnAnswer(Finding f, int a)
        {
            buddy.Fixed();
            if (a == 0) { Settings.AddTo("Dismissed", f.Key); Hearts(3); if (buddy != null) buddy.SayNow(L.T("Рад, что пригодилось. Исправишь — и я успокоюсь.", "Glad it helped. Fix it and I'll calm down.")); }
            else if (a == 1) { Settings.AddTo("Dismissed", f.Key); if (buddy != null) buddy.SayNow(L.T("Понял, так и задумано. Больше про это место не скажу.", "Got it, that's intended. I won't mention this spot again.")); }
            else { Settings.AddTo("IgnoredFiles", f.File); if (buddy != null) buddy.SayNow(L.T("Хорошо, этот файл больше не смотрю.", "Okay, I won't look at this file any more.")); }
        }

        TreeForm tree;
        PumpkinForm pumpkin;
        SnowmanForm snowman;
        DateTime nextTogether = DateTime.Now.AddMinutes(6);

        DateTime nextHeart = DateTime.Now.AddMinutes(5), nextBall = DateTime.Now.AddMinutes(2);
        List<Plat> lastShelves = new List<Plat>();
        GarlandForm garland;
        DateTime nextSnowball = DateTime.Now.AddMinutes(3);

        /// Opens and closes the tree, the garland, the pumpkin and the snowman with their dates (and only when there is memory to spare).
        void Decorate()
        {
            bool room = Budget.Level >= 1 && !hiddenFull && !resting;
            bool wantTree = room && Calendar.On("tree"), wantGarland = room && Calendar.On("garland");
            if (wantTree && tree == null)
            {
                tree = new TreeForm();
                tree.Clicked += delegate
                {
                    if (tree.Presents > 0) OpenPresent();
                    else
                    {
                        string[] lines = L.A(new[] { "Ёлочка, гори!", "Красиво мигает. Я бы потрогал лапой.", "Новый узор! Мне нравится.", "Можно я залезу на верхушку? Шучу. Почти.",
                            "Под ёлкой пока пусто. Но я верю.", "Огоньки как светлячки. Буду ловить.", "Мигай быстрее! Нет, медленнее! Вот так." },
                            new[] { "Light up, little tree!", "It blinks so prettily. I'd touch it with my paw.", "A new pattern! I like it.", "Can I climb to the top? Kidding. Almost.",
                            "Nothing under the tree yet. But I believe.", "The lights are like fireflies. I'll catch them.", "Blink faster! No, slower! Just like that." });
                        Say(lines[rnd.Next(lines.Length)]);
                        if (buddy != null && rnd.NextDouble() < 0.3) buddy.SayNow(L.T("Ёлка мигает в такт моим мыслям.", "The tree blinks in time with my thoughts."));
                    }
                };
                tree.Show();
            }
            else if (!wantTree && tree != null) { tree.Close(); tree = null; }
            if (wantGarland && garland == null) { garland = new GarlandForm(); garland.Show(); }
            else if (!wantGarland && garland != null) { garland.Close(); garland = null; }
            if (tree != null)
            {
                var d = Season.Today;
                int want = (d.Month == 12 && d.Day == 31) || (d.Month == 1 && d.Day == 1) ? Math.Max(0, 3 - Settings.Opened) : 0;
                if (tree.Presents != want) { tree.Presents = want; tree.Draw(); }
            }
            bool wantPumpkin = room && Calendar.On("halloween");
            if (wantPumpkin && pumpkin == null)
            {
                pumpkin = new PumpkinForm();
                pumpkin.Clicked += delegate
                {
                    Say(L.T("БУ!!! …Ой. Это я сам испугался.", "BOO!!! …Oops. I scared myself."));
                    brain.Startle();
                    if (buddy != null) buddy.SayNow(L.T("У-у-у… Это я, привидение. Не бойся.", "Oooooh… It's me, the ghost. Don't be scared."));
                };
                pumpkin.Show();
            }
            else if (!wantPumpkin && pumpkin != null) { pumpkin.Close(); pumpkin = null; }
            int stage = Calendar.On("winter") && room ? SnowmanStage() : 0;
            if (stage > 0 && snowman == null)
            {
                snowman = new SnowmanForm(stage);
                snowman.Clicked += delegate
                {
                    snowman.Wobble();
                    string[] lines = stage < 4
                        ? L.A(new[] { "Я его строю! Приходи завтра — будет больше.", "Не трогай, он ещё не готов! Ну ладно, можно потрогать.", "Это фундамент. Великие снеговики начинаются с одного шара.", "Лапы мёрзнут, но я стараюсь." },
                            new[] { "I'm building it! Come back tomorrow, it'll be bigger.", "Don't touch, it's not ready! Okay, fine, you can touch it.", "This is the foundation. Great snowmen start with one ball.", "My paws are freezing, but I'm trying." })
                        : L.A(new[] { "Это я построил! Красивый, правда? Почти как я.", "Тише, снеговик спит.", "Морковку не трогай, это его нос.", "Он мой друг. Молчаливый, но надёжный.", "Снеговик говорит: «Привет». Я перевёл.", "Если он растает — я построю нового." },
                            new[] { "I built it! Pretty, right? Almost like me.", "Shh, the snowman is sleeping.", "Don't touch the carrot, it's his nose.", "He's my friend. Quiet, but reliable.", "The snowman says \"Hi\". I translated.", "If he melts, I'll build a new one." });
                    Say(lines[rnd.Next(lines.Length)]);
                    if (buddy != null && rnd.NextDouble() < 0.35) buddy.SayNow(rnd.Next(2) == 0 ? L.T("Снеговик с цилиндром — это стильно.", "A snowman in a top hat. Stylish.") : L.T("{1} — архитектор. Я горжусь.", "{1} is an architect. I'm proud."));
                };
                snowman.Show();
            }
            else if (stage == 0 && snowman != null) { snowman.Close(); snowman = null; }
        }

        /// December: one ball, then two, then three, then the hat; January: finished.
        static int SnowmanStage()
        {
            var d = Season.Today;
            if (d.Month == 12) return 1 + Math.Min(3, (d.Day - 1) / 4);
            return 4;
        }

        /// The tree's presents: three forecasts for the new year, one per click.
        static string[] Forecasts { get { return L.A(new[]
        {
            "Предсказание: в этом году ты закончишь проект, который давно откладывался.",
            "Предсказание: тебя ждёт много хорошего кода и мало багов.",
            "Предсказание: кто-то скажет тебе спасибо за твою работу.",
            "Предсказание: в этом году будет повод собой гордиться.",
            "Предсказание: ты научишься чему-то, что сейчас кажется сложным.",
            "Предсказание: кот будет рядом. Это я тебе гарантирую.",
        }, new[]
        {
            "Fortune: this year you'll finish a project you've been putting off.",
            "Fortune: lots of good code and few bugs are ahead.",
            "Fortune: someone will thank you for your work.",
            "Fortune: this year will give you a reason to be proud of yourself.",
            "Fortune: you'll learn something that seems hard right now.",
            "Fortune: the cat will be by your side. I guarantee it.",
        }); } }

        void OpenPresent()
        {
            if (tree == null || tree.Presents == 0) return;
            Settings.Opened = Settings.Opened + 1;
            tree.Presents--;
            tree.Draw();
            if (buddy != null) buddy.SayNow(Forecasts[(Settings.Opened * 5 + DateTime.Today.Day) % Forecasts.Length]);
            else Say(Forecasts[(Settings.Opened * 5) % Forecasts.Length]);
            Hearts(5);
        }

        /// Kindness day: Ember sends a heart to the mouse pointer.
        void HeartToCursor()
        {
            var c = Cursor.Position;
            new SnowballForm(new PointF(buddy.X, buddy.Y), new PointF(c.X, c.Y), delegate { }, 1).Show();
            buddy.SayNow(Lines.Pick(L.A(new[] { "Держи сердечко.", "Это тебе. Просто так.", "Сегодня день доброты — лови!" }, new[] { "Here's a heart.", "This is for you. Just because.", "It's Kindness Day, catch!" })));
        }

        /// The ball game: only where nothing happens (VS Code with no typing, the desktop), never over videos.
        void TryBall(DateTime now, float idle)
        {
            if (buddy == null || buddy.Playing || !Settings.Feature("ball") || budgetLevel < 1 || now < nextBall) return;
            if (idle < 15 || idle > 200 || typing > 0) return;
            string app = Native.ForegroundProcess();
            bool desk = Native.ForegroundIsDesktop();
            if (!(app == "code" || app == "notepad" || desk)) return;
            Rectangle r;
            RectangleF box;
            if (desk) box = Screen.PrimaryScreen.WorkingArea;
            else
            {
                if (!Native.WindowRect(Native.GetForegroundWindow(), out r)) return;
                float top = r.Top + 90, bottom = r.Bottom - 40;
                foreach (var p in lastShelves)
                {
                    if (p.R.Top < r.Top + r.Height * 0.25f) top = Math.Max(top, p.R.Top + 6);          // under the tab bar
                    if (p.R.Top > r.Top + r.Height * 0.6f) bottom = Math.Min(bottom, p.R.Top - 6);     // above the status bar or panel
                }
                box = new RectangleF(r.Left + 70, top, r.Width - 140, bottom - top);
            }
            if (box.Width < 300 || box.Height < 200) return;
            if (box.Contains(Cursor.Position)) return;     // the mouse is in there: the place is in use
            nextBall = now.AddMinutes(Next(4, 10));
            if (rnd.NextDouble() < 0.5) return;
            buddy.StartBall(box);
        }


        void MakeWish()
        {
            using (var d = new WishDialog())
                if (d.ShowDialog() == DialogResult.OK && d.Wish.Trim() != "")
                {
                    Settings.Wish = d.Wish.Trim();
                    Hearts(6);
                    if (buddy != null) buddy.SayNow(L.T("Записал. Никому не скажу. Только тебе — в январе напомню.", "Noted. I won't tell anyone. I'll only remind you in January."));
                }
        }

        /// From the menu: a snowball flies from the mouse at the cat, and the cat throws one back.
        void SnowballAtCat()
        {
            var from = Cursor.Position;
            var at = new PointF(brain.X, brain.Y - 40);
            new SnowballForm(from, at, delegate
            {
                Say(rnd.Next(2) == 0 ? L.T("Ах так?!", "Oh, is that so?!") : L.T("Холодно! Ну держись!", "Cold! Now you're in for it!"));
                var back = new Timer { Interval = 900 };
                back.Tick += delegate
                {
                    back.Stop();
                    new SnowballForm(new PointF(brain.X, brain.Y - 50), new PointF(from.X, from.Y), delegate { Say(L.T("Попал! Мяу-ха-ха!", "Got you! Meow-ha-ha!")); }).Show();
                };
                back.Start();
            }).Show();
        }

        /// In winter the cat sometimes throws a snowball at Ember.
        void SnowballAtBuddy()
        {
            Say(L.T("Огонёк, лови!", "Ember, catch!"));
            var to = new PointF(buddy.X, buddy.Y);
            new SnowballForm(new PointF(brain.X, brain.Y - 50), to, delegate
            {
                if (buddy != null) buddy.SayNow(rnd.Next(2) == 0 ? L.T("Ай! Холодно! Ну, {1}…", "Ouch! Cold! Oh, {1}…") : L.T("Попал. Ладно, ты чемпион по снежкам.", "You got me. Fine, you're the snowball champion."));
            }).Show();
        }

        bool resting;
        Timer restTimer;

        /// "Take a rest": both go away for a while; the drawing stops and the memory is handed back to Windows.
        void Rest(DateTime until)
        {
            hiddenUntil = until;
            Say(L.T("Пойду посплю. До встречи!", "I'm off for a nap. See you!"));
            if (buddy != null) buddy.SayNow(L.T("И я с ним. Отдыхай тоже.", "Me too. You rest as well."));
            var t = new Timer { Interval = 2500 };
            t.Tick += delegate
            {
                t.Stop();
                resting = true;
                frame.Stop();
                var empty = new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                Native.ShowBitmap(Handle, empty, -10, -10);
                if (buddy != null) Native.ShowBitmap(buddy.Handle, empty, -10, -10);
                Native.TrimMemory();
                restTimer = new Timer { Interval = 15000 };
                restTimer.Tick += delegate { if (DateTime.Now >= hiddenUntil) WakeFromRest(); };
                restTimer.Start();
            };
            t.Start();
        }

        void WakeFromRest()
        {
            if (restTimer != null) { restTimer.Stop(); restTimer = null; }
            hiddenUntil = DateTime.MinValue;
            if (!resting) return;
            resting = false;
            last = clock.Elapsed.TotalSeconds;
            var wa = Screen.PrimaryScreen.WorkingArea;
            brain.Call(new Point(wa.Left + rnd.Next(wa.Width / 4, wa.Width * 3 / 4), wa.Top));
            frame.Start();
            Say(Talk.Pick(Talk.Back));
        }

        public void CallFromOutside()
        {
            if (resting) { WakeFromRest(); return; }
            hiddenUntil = DateTime.MinValue;
            brain.Call(Cursor.Position);
            Say(L.T("Мяу! Я тут.", "Meow! I'm here."));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { tray.Visible = false; tray.Dispose(); }
            base.Dispose(disposing);
        }
    }

    /// The fish in a bowl the cat runs to.
    class FoodForm : Form
    {
        public FoodForm(float x, float y)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            var bmp = new Bitmap(60, 34, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var bowl = new SolidBrush(Color.FromArgb(90, 150, 220)))
                    g.FillPie(bowl, 6, 4, 48, 56, 0, 180);
                using (var pen = new Pen(Color.FromArgb(40, 70, 120), 2)) g.DrawArc(pen, 6, 4, 48, 56, 0, 180);
                using (var fish = new SolidBrush(Color.FromArgb(250, 170, 80)))
                {
                    g.FillEllipse(fish, 16, 22, 22, 11);
                    g.FillPolygon(fish, new[] { new PointF(36, 27), new PointF(46, 20), new PointF(46, 34) });
                }
                using (var eye = new SolidBrush(Color.Black)) g.FillEllipse(eye, 20, 25, 3, 3);
            }
            Load += delegate { Native.ShowBitmap(Handle, bmp, (int)x - 30, (int)y - 32); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000 | 0x80 | 0x8 | 0x08000000 | 0x20;   // also click-through
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }
    }

    class DarkColors : ProfessionalColorTable
    {
        static readonly Color Bg = Color.FromArgb(34, 32, 30), Hi = Color.FromArgb(70, 60, 48);
        public override Color ToolStripDropDownBackground { get { return Bg; } }
        public override Color ImageMarginGradientBegin { get { return Bg; } }
        public override Color ImageMarginGradientMiddle { get { return Bg; } }
        public override Color ImageMarginGradientEnd { get { return Bg; } }
        public override Color MenuItemSelected { get { return Hi; } }
        public override Color MenuItemBorder { get { return Hi; } }
        public override Color MenuBorder { get { return Color.FromArgb(80, 70, 60); } }
        public override Color SeparatorDark { get { return Color.FromArgb(70, 64, 58); } }
        public override Color CheckBackground { get { return Hi; } }
        public override Color CheckSelectedBackground { get { return Hi; } }
    }
}
