using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Collections.Generic;

[assembly: System.Reflection.AssemblyTitle("Kotik")]
[assembly: System.Reflection.AssemblyProduct("Kotik")]
[assembly: System.Reflection.AssemblyDescription("A cat that lives on your desktop")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace Kotik
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--sheet") { Sheet.Render(args[1]); return; }
            if (args.Length >= 2 && args[0] == "--buddyshot")
            {
                // Ember in every mood, plus a thought bubble and a speech bubble
                L.Load();
                if (args.Length == 3) L.En = args[2] == "en";        // --buddyshot out.png en
                var b = new Buddy(new Brain(), delegate { });
                var moods = (Buddy.Mood[])Enum.GetValues(typeof(Buddy.Mood));
                using (var sheet = new Bitmap(200 * moods.Length, 400))
                using (var g = Graphics.FromImage(sheet))
                {
                    g.Clear(Color.FromArgb(40, 40, 46));
                    for (int i = 0; i < moods.Length; i++) { b.Feel = moods[i]; g.DrawImage(b.Preview(false, "", false), new Rectangle(i * 200 - 60, -60, 320, 220)); g.DrawString(moods[i].ToString(), SystemFonts.DefaultFont, Brushes.White, i * 200 + 70, 150); }
                    b.Feel = Buddy.Mood.Calm;
                    g.DrawImage(b.Preview(true, Lines.Gerunds[0], false), 0, 175);
                    g.DrawImage(b.Preview(false, Lines.Tips[0], true), 330, 175);
                    sheet.Save(args[1]);
                }
                return;
            }
            if (args.Length == 2 && args[0] == "--lasertest") { SelfTest.Laser(args[1]); return; }
            if (args.Length == 2 && args[0] == "--selftest") { SelfTest.Run(args[1]); return; }
            if (args.Length == 3 && args[0] == "--weathertest") { try { SetProcessDPIAware(); } catch { } Weather.Test(args[1] == "snow", args[2]); return; }
            if (args.Length == 3 && args[0] == "--lint")
            {
                var sb = new System.Text.StringBuilder();
                foreach (var f in Lint.Check(args[1])) sb.AppendLine(f.Line + " [" + f.Rule + "] " + f.Message);
                string text = System.IO.File.ReadAllText(args[1]);
                foreach (var tp in Lint.Topics) if (System.Text.RegularExpressions.Regex.IsMatch(text, tp[0], System.Text.RegularExpressions.RegexOptions.Multiline)) sb.AppendLine("topic: " + tp[1]);
                System.IO.File.WriteAllText(args[2], sb.ToString());
                return;
            }
            bool created;
            using (var single = new Mutex(true, "Kotik-single-instance", out created))
            {
                if (!created)
                {
                    try { using (var ev = EventWaitHandle.OpenExisting("Kotik-call")) ev.Set(); } catch { }
                    return;
                }
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                L.Load();
                if (!Settings.Named)
                {
                    using (var d = new NameDialog(true))
                    {
                        if (d.ShowDialog() != DialogResult.OK) return;
                        Settings.Name = d.CatName;
                        Settings.Owner = d.OwnerName;
                        Settings.Autostart = d.Autostart;
                        Settings.Named = true;
                        Settings.FedAt = DateTime.Now.AddHours(-5);
                        Settings.Since = DateTime.Today;
                    }
                }
                var pet = new PetForm();
                var call = new EventWaitHandle(false, EventResetMode.AutoReset, "Kotik-call");
                var waiter = new Thread(() =>
                {
                    while (call.WaitOne())
                        try { pet.BeginInvoke(new Action(pet.CallFromOutside)); } catch { }
                }) { IsBackground = true };
                waiter.Start();
                pet.Show();
                Application.Run(new ApplicationContext());
            }
        }
    }

    /// Two languages: Russian and English. By default — the language of Windows.
    static class L
    {
        public static bool En;
        public static void Load()
        {
            string l = Settings.Lang;
            En = l == "en" || (l != "ru" && System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "ru");
        }
        public static string T(string ru, string en) { return En ? en : ru; }
        public static T[] A<T>(T[] ru, T[] en) { return En ? en : ru; }
        /// Russian plural: 1 день, 2 дня, 5 дней; English: 1 day, 2 days.
        public static string Plural(int n, string one, string few, string many, string enOne, string enMany)
        {
            if (En) return n == 1 ? enOne : enMany;
            int m10 = n % 10, m100 = n % 100;
            if (m10 == 1 && m100 != 11) return one;
            if (m10 >= 2 && m10 <= 4 && (m100 < 10 || m100 >= 20)) return few;
            return many;
        }
    }

    /// Settings live in HKCU\Software\Kotik.
    static class Settings
    {
        const string Key = @"Software\Kotik";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        static object Get(string name, object def)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(Key)) return k.GetValue(name, def);
        }

        static void Put(string name, object v)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(Key)) k.SetValue(name, v);
        }

        public static string Name { get { return (string)Get("Name", L.T("Мурзик", "Whiskers")); } set { Put("Name", value); } }
        /// How the cat and Ember call the user; empty — «друг» / "friend".
        public static string Owner { get { string v = (string)Get("Owner", ""); return v.Length > 0 ? v : L.T("друг", "friend"); } set { Put("Owner", value ?? ""); } }
        public static string OwnerRaw { get { return (string)Get("Owner", ""); } }
        /// "" — follow the Windows language, "ru" or "en" — chosen in the settings.
        public static string Lang { get { return (string)Get("Lang", ""); } set { Put("Lang", value ?? ""); } }
        public static bool Named { get { return (int)Get("Named", 0) == 1; } set { Put("Named", value ? 1 : 0); } }
        public static int Coat { get { return Math.Max(0, Math.Min(Cat.Coats.Length - 1, (int)Get("Coat", 0))); } set { Put("Coat", value); } }
        public static int Talk { get { return (int)Get("Talk", 1); } set { Put("Talk", value); } }
        public static bool Sounds { get { return (int)Get("Sounds", 1) == 1; } set { Put("Sounds", value ? 1 : 0); } }
        /// The day the cat moved in (for "we've been together N days").
        public static DateTime Since
        {
            get { long v; return long.TryParse((string)Get("Since", "0"), out v) && v > 0 ? new DateTime(v) : DateTime.Today; }
            set { Put("Since", value.Ticks.ToString()); }
        }
        public static string LastGreet { get { return (string)Get("LastGreet", ""); } set { Put("LastGreet", value); } }
        /// Feature switches (the settings window). On by default, except the ones that touch your files or your cursor.
        static readonly string[] OffByDefault = { "codecheck", "naughty" };
        public static bool Feature(string name) { return (int)Get("F_" + name, Array.IndexOf(OffByDefault, name) >= 0 ? 0 : 1) == 1; }
        public static void SetFeature(string name, bool on) { Put("F_" + name, on ? 1 : 0); }
        public static string SeasonDone { get { return (string)Get("SeasonDone", ""); } set { Put("SeasonDone", value); } }
        public static void AddTo(string which, string item)
        {
            var list = new List<string>(((string)Get(which, "")).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            if (list.Contains(item)) return;
            list.Add(item);
            if (list.Count > 400) list.RemoveAt(0);
            Put(which, string.Join("\n", list.ToArray()));
        }
        public static bool Has(string which, string item)
        {
            return Array.IndexOf(((string)Get(which, "")).Split('\n'), item) >= 0;
        }
        public static string Said { get { return (string)Get("Said", ""); } set { Put("Said", value); } }
        /// The New Year wish and the year it was made for (the reminder comes once, the next January).
        public static string Wish { get { return (string)Get("Wish", ""); } set { Put("Wish", value); Put("WishYear", DateTime.Today.Year + (DateTime.Today.Month == 12 ? 1 : 0)); } }
        public static int WishYear { get { return (int)Get("WishYear", 0); } }
        public static bool WishThisYear { get { return Wish != "" && WishYear == DateTime.Today.Year + 1; } }
        public static int Opened { get { return (int)Get("Opened_" + DateTime.Today.Year + "_" + (DateTime.Today.Month == 1 ? "jan" : "dec"), 0); } set { Put("Opened_" + DateTime.Today.Year + "_" + (DateTime.Today.Month == 1 ? "jan" : "dec"), value); } }
        public static int Love { get { return Math.Max(0, Math.Min(100, (int)Get("Love", 10))); } set { Put("Love", Math.Max(0, Math.Min(100, value))); } }
        public static int Gifts { get { return (int)Get("Gifts", 0); } set { Put("Gifts", value); } }
        public static string LastGift { get { return (string)Get("LastGift", ""); } set { Put("LastGift", value); } }
        public static bool Buddy { get { return (int)Get("Buddy", 1) == 1; } set { Put("Buddy", value ? 1 : 0); } }
        public static bool Shelves { get { return (int)Get("Shelves", 1) == 1; } set { Put("Shelves", value ? 1 : 0); } }
        public static bool Calm { get { return (int)Get("Calm", 0) == 1; } set { Put("Calm", value ? 1 : 0); } }

        public static DateTime FedAt
        {
            get { long v; return long.TryParse((string)Get("FedAt", "0"), out v) && v > 0 ? new DateTime(v) : DateTime.Now; }
            set { Put("FedAt", value.Ticks.ToString()); }
        }

        public static bool Autostart
        {
            get { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue("Kotik") != null; }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) k.SetValue("Kotik", "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue("Kotik", false);
                }
            }
        }
    }

    /// First start (and "Rename"): name the cat, say your own name, decide whether it starts with Windows.
    class NameDialog : Form
    {
        readonly TextBox box = new TextBox();
        readonly TextBox me = new TextBox();
        readonly CheckBox auto = new CheckBox();
        public string CatName { get { return box.Text.Trim().Length > 0 ? box.Text.Trim().Replace("{", "").Replace("}", "") : L.T("Мурзик", "Whiskers"); } }
        public string OwnerName { get { return me.Text.Trim().Replace("{", "").Replace("}", ""); } }
        public bool Autostart { get { return auto.Checked; } }
        readonly Panel preview = new Panel();
        float t;

        readonly float k = 1;
        int S(float v) { return (int)Math.Round(v * k); }
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public NameDialog(bool first)
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) k = g.DpiX / 96f;
            Text = first ? L.T("У тебя теперь есть кот", "You have a cat now") : L.T("Новое имя", "New name");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(34, 32, 30);
            ForeColor = Color.FromArgb(235, 230, 220);
            Font = new Font("Segoe UI", 11f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(S(440), S(first ? 470 : 300));
            Icon = Art.AppIcon();
            preview.SetBounds(S(0), S(10), S(440), S(150));
            preview.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var pose = new Pose { Sitting = true, BodyY = -30, BodyAngle = 0, HeadX = 14, HeadY = -62, TailUp = 0, Tail = (float)Math.Sin(t * 1.5) * 0.6f };
                if (t % 4 > 3.85f) pose.Eyes = Eyes.Closed;
                e.Graphics.ScaleTransform(k, k);
                Cat.Draw(e.Graphics, 220, 140, -1, pose, Cat.Coats[Settings.Coat], t);
            };
            typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(preview, true, null);
            var timer = new System.Windows.Forms.Timer { Interval = 33 };
            timer.Tick += delegate { t += 0.033f; preview.Invalidate(); };
            timer.Start();
            var hello = new Label { Text = first ? L.T("Привет! Я буду жить у тебя на экране: гулять по окнам, спать на панели задач и иногда болтать. Как меня зовут?",
                    "Hi! I'll live on your screen: walk on your windows, nap on the taskbar and chat now and then. What's my name?")
                    : L.T("Как меня теперь зовут?", "What's my new name?"),
                AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };
            hello.SetBounds(S(20), S(165), S(400), S(first ? 70 : 34));
            box.SetBounds(S(90), S(first ? 245 : 210), S(260), S(30));
            box.Text = first ? "" : Settings.Name;
            box.TextAlign = HorizontalAlignment.Center;
            box.BackColor = Color.FromArgb(50, 47, 44);
            box.ForeColor = ForeColor;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.MaxLength = 20;
            Controls.Add(preview);
            Controls.Add(hello);
            Controls.Add(box);
            int y = first ? 290 : 250;
            if (first)
            {
                var who = new Label { Text = L.T("А как зовут тебя? (можно пропустить)", "And what's your name? (optional)"), AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };
                who.SetBounds(S(20), S(y), S(400), S(28));
                me.SetBounds(S(90), S(y + 30), S(260), S(30));
                me.TextAlign = HorizontalAlignment.Center;
                me.BackColor = box.BackColor;
                me.ForeColor = ForeColor;
                me.BorderStyle = BorderStyle.FixedSingle;
                me.MaxLength = 20;
                Controls.Add(who);
                Controls.Add(me);
                y += 76;
                auto.Text = L.T("Жить у тебя всегда (запускаться с Windows)", "Stay with you (start with Windows)");
                auto.Checked = true;
                auto.SetBounds(S(60), S(y), S(340), S(30));
                Controls.Add(auto);
                y += 46;
            }
            var ok = new Button { Text = first ? L.T("Привет!", "Hi!") : L.T("Готово", "Done"), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(200, 120, 50), ForeColor = Color.White };
            ok.FlatAppearance.BorderSize = 0;
            ok.SetBounds(S(150), S(y), S(140), S(40));
            Controls.Add(ok);
            AcceptButton = ok;
            FormClosed += delegate { timer.Stop(); };
            HandleCreated += delegate
            {
                int on = 1;
                try { DwmSetWindowAttribute(Handle, 20, ref on, 4); } catch { }   // dark title bar
            };
        }
    }

    /// Icons drawn from the cat itself.
    static class Art
    {
        public static Icon TrayIcon(Coat c)
        {
            using (var bmp = new Bitmap(64, 64))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    g.ScaleTransform(1.25f, 1.25f);
                    var pose = new Pose { HeadX = 0, HeadY = 0 };
                    Cat.Face(g, 24, 30, pose, c);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        public static Icon AppIcon() { return TrayIcon(Cat.Coats[Settings.Coat]); }
    }

    /// Developer: draws every pose into a PNG (Kotik.exe --sheet out.png).
    static class Sheet
    {
        public static void Render(string path)
        {
            using (var bmp = new Bitmap(1200, 700))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(60, 90, 120));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var poses = new[]
                {
                    new Pose(),
                    new Pose { Walk = 1.2f, Stride = 6 },
                    new Pose { Sitting = true, BodyY = -30, BodyAngle = 0, HeadX = 14, HeadY = -62, TailUp = 0 },
                    new Pose { Sitting = true, BodyY = -30, BodyAngle = 0, HeadX = 14, HeadY = -62, TailUp = 0, PawUp = true, Eyes = Eyes.Closed, HeadTilt = -8 },
                    new Pose { Loaf = true, BodyY = -15, HeadX = 27, HeadY = -30, TailUp = 0 },
                    new Pose { Loaf = true, BodyY = -15, HeadX = 27, HeadY = -24, HeadTilt = 12, TailUp = 0, Eyes = Eyes.Closed },
                    new Pose { Stretch = true, BodyAngle = 14, HeadX = 36, HeadY = -30, Eyes = Eyes.Closed, Tail = 0.8f },
                    new Pose { Spread = true, Eyes = Eyes.Wide, Ears = 0.3f },
                    new Pose { Dangle = true, Eyes = Eyes.Wide, Ears = 0.6f },
                    new Pose { HeadX = 34, HeadY = -24, HeadTilt = 24, Eyes = Eyes.Happy, Tail = 0.6f },
                    new Pose { Sitting = true, BodyY = -30, BodyAngle = 0, HeadX = 14, HeadY = -62, TailUp = 0, Eyes = Eyes.Happy, Blush = true },
                    new Pose { BodyY = -19, HeadY = -36, HeadX = 32, Squash = 0.2f, Eyes = Eyes.Wide },
                };
                for (int i = 0; i < poses.Length; i++)
                {
                    float x = 120 + (i % 6) * 190, y = 200 + (i / 6) * 300;
                    if (i == 0) poses[i].Hat = "santa";
                    if (i == 2) poses[i].Hat = "witch";
                    if (i == 1 || i == 10) poses[i].Hat = "scarf";
                    Cat.Draw(g, x, y, i % 2 == 0 ? 1 : -1, poses[i], Cat.Coats[i % Cat.Coats.Length], 0);
                }
                using (var ico = Art.TrayIcon(Cat.Coats[0]).ToBitmap()) g.DrawImage(ico, 1100, 620, 64, 64);
                bmp.Save(path);
            }
        }
    }
}

namespace Kotik
{
    /// Developer: simulated minutes of the brain against the real windows, logged to a file (no window shown).
    /// Kotik.exe --selftest log.txt, --lasertest log.txt, --weathertest snow|leaves out.png, --lint file.cpp out.txt, --buddyshot out.png
    static class SelfTest
    {
        /// A maximised editor with shelves like VS Code: the dot sits high up; does the cat climb the shelves to it?
        public static void Laser(string path)
        {
            var log = new System.Text.StringBuilder();
            var b = new Brain();
            var ed = new IntPtr(777);
            b.floors.Add(new Plat { H = IntPtr.Zero, R = new Rectangle(0, 1030, 1920, 1) });
            b.wins = new System.Collections.Generic.List<Plat> { new Plat { H = ed, R = new Rectangle(0, 0, 1920, 1030) } };
            var sh = new System.Collections.Generic.List<Plat>();
            bool gap = path.EndsWith("2.txt");     // log name ending in 2: only the status bar and the tab bar, so Ember has to help
            if (gap) b.AskStep = at => { b.Step = at; return true; };
            foreach (int y in gap ? new[] { 990, 90 } : new[] { 990, 840, 700, 560, 330, 180, 90 }) sh.Add(new Plat { H = ed, Shelf = true, R = new Rectangle(400 + y % 300, y, 900, 1) });
            b.SetShelves(sh);
            b.X = 200; b.Y = 1020;
            b.Laser = new PointF(900, 64);
            float dt = 1f / 60f;
            Act last = Act.Fall;
            float bestY = 2000;
            for (int i = 0; i < 60 * 40; i++)
            {
                b.UserIdle = 1;
                b.Update(dt);
                if (b.Y < bestY) bestY = b.Y;
                if (b.Act != last) { log.AppendLine(string.Format("{0,5:F1}s {1,-7} x={2,5:F0} y={3,5:F0}", i * dt, b.Act, b.X, b.Y)); last = b.Act; }
            }
            log.AppendLine("highest y: " + bestY);
            System.IO.File.WriteAllText(path, log.ToString());
        }

        public static void Run(string path)
        {
            var log = new System.Text.StringBuilder();
            var b = new Brain();
            var counts = new System.Collections.Generic.Dictionary<Act, int>();
            Act last = Act.Fall;
            float dt = 1f / 30f, platT = 0;
            int bad = 0;
            var vs = SystemInformation.VirtualScreen;
            for (int i = 0; i < 30 * 600; i++)
            {
                float sec = i * dt;
                b.UserIdle = sec > 400 ? sec - 400 : 1;          // the user leaves after ~7 minutes
                b.Cursor = new Point(vs.Left + vs.Width / 2, vs.Top + vs.Height / 2);
                if (i == 30 * 60) { b.Grab(); b.Hold(new Point(vs.Left + 300, vs.Top + 200)); b.Throw(1800, -900); }
                if (i == 30 * 120) b.Food = new PointF(b.X + 200, b.Y);
                platT -= dt;
                if (platT <= 0) { platT = 0.25f; b.RefreshPlatforms(IntPtr.Zero); }
                b.Update(dt);
                if (b.Act != last)
                {
                    log.AppendLine(string.Format("{0,6:F1}s {1,-8} x={2,5:F0} y={3,5:F0} on={4}", sec, b.Act, b.X, b.Y,
                        b.On == null ? "air" : b.On.H == IntPtr.Zero ? "taskbar" : "window"));
                    last = b.Act;
                    int c;
                    counts.TryGetValue(b.Act, out c);
                    counts[b.Act] = c + 1;
                }
                if (b.X < vs.Left - 5 || b.X > vs.Right + 5 || b.Y > vs.Bottom + 400) bad++;
            }
            log.AppendLine("acts:");
            foreach (var kv in counts) log.AppendLine("  " + kv.Key + " " + kv.Value);
            log.AppendLine("out of screen frames: " + bad + ", food left: " + b.Food.HasValue + ", hungry: " + b.Hungry);
            System.IO.File.WriteAllText(path, log.ToString());
        }
    }
}
