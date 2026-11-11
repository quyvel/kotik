using System;
using System.Drawing;
using System.Windows.Forms;

namespace Kotik
{
    /// "More settings…": everything in one window — the cat, the features, the calendar of events and memory.
    /// The right-click menu keeps only the everyday actions.
    class SettingsForm : Form
    {
        static readonly Color Bg = Color.FromArgb(30, 28, 26), Panel = Color.FromArgb(42, 39, 36), Text1 = Color.FromArgb(235, 230, 220),
            Dim = Color.FromArgb(165, 155, 145), Accent = Color.FromArgb(230, 150, 80);
        readonly FlowLayoutPanel col = new FlowLayoutPanel();
        readonly float k;
        readonly Action changed;

        int S(float v) { return (int)Math.Round(v * k); }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public SettingsForm(Action onChange)
        {
            changed = onChange;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) k = g.DpiX / 96f;
            Text = L.T("Котик — настройки", "Kotik — settings");
            Icon = Art.AppIcon();
            BackColor = Bg;
            ForeColor = Text1;
            Font = new Font("Segoe UI", 10.5f);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(560), S(680));
            MinimumSize = new Size(S(420), S(400));
            col.Dock = DockStyle.Fill;
            col.FlowDirection = FlowDirection.TopDown;
            col.WrapContents = false;
            col.AutoScroll = true;
            col.Padding = new Padding(S(18), S(14), S(18), S(18));
            Controls.Add(col);
            HandleCreated += delegate { int on = 1; try { DwmSetWindowAttribute(Handle, 20, ref on, 4); } catch { } };
            Build();
        }

        Label Head(string s)
        {
            var l = new Label { Text = s, AutoSize = true, ForeColor = Accent, Font = new Font("Segoe UI Semibold", 13f), Margin = new Padding(0, S(14), 0, S(4)) };
            col.Controls.Add(l);
            return l;
        }

        Label Note(string s)
        {
            var l = new Label { Text = s, AutoSize = true, ForeColor = Dim, MaximumSize = new Size(S(500), 0), Margin = new Padding(0, 0, 0, S(6)) };
            col.Controls.Add(l);
            return l;
        }

        CheckBox Box(string text, bool value, Action<bool> set)
        {
            var c = new CheckBox { Text = text, Checked = value, AutoSize = true, ForeColor = Text1, MaximumSize = new Size(S(510), 0), Margin = new Padding(0, S(2), 0, S(2)) };
            c.CheckedChanged += delegate { set(c.Checked); changed(); };
            col.Controls.Add(c);
            return c;
        }

        void Build()
        {
            Head(L.T("Кот", "The cat"));
            var nameRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0) };
            var name = new TextBox { Text = Settings.Name, Width = S(200), BackColor = Panel, ForeColor = Text1, BorderStyle = BorderStyle.FixedSingle, MaxLength = 20 };
            name.TextChanged += delegate { string v = name.Text.Trim().Replace("{", "").Replace("}", ""); if (v.Length > 0) { Settings.Name = v; changed(); } };
            nameRow.Controls.Add(new Label { Text = L.T("Имя:", "Name:"), AutoSize = true, Margin = new Padding(0, S(6), S(8), 0) });
            nameRow.Controls.Add(name);
            var coat = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = S(160), BackColor = Panel, ForeColor = Text1, FlatStyle = FlatStyle.Flat, Margin = new Padding(S(16), S(2), 0, 0) };
            foreach (var c in Cat.Coats) coat.Items.Add(c.Name);
            coat.SelectedIndex = Settings.Coat;
            coat.SelectedIndexChanged += delegate { Settings.Coat = coat.SelectedIndex; changed(); };
            nameRow.Controls.Add(new Label { Text = L.T("Окрас:", "Coat:"), AutoSize = true, Margin = new Padding(S(16), S(6), S(8), 0) });
            nameRow.Controls.Add(coat);
            col.Controls.Add(nameRow);
            var meRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, S(6), 0, 0) };
            var me = new TextBox { Text = Settings.OwnerRaw, Width = S(200), BackColor = Panel, ForeColor = Text1, BorderStyle = BorderStyle.FixedSingle, MaxLength = 20 };
            me.TextChanged += delegate { Settings.Owner = me.Text.Trim().Replace("{", "").Replace("}", ""); changed(); };
            meRow.Controls.Add(new Label { Text = L.T("Как зовут тебя:", "Your name:"), AutoSize = true, Margin = new Padding(0, S(6), S(8), 0) });
            meRow.Controls.Add(me);
            col.Controls.Add(meRow);
            var langRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, S(6), 0, 0) };
            langRow.Controls.Add(new Label { Text = L.T("Язык:", "Language:"), AutoSize = true, Margin = new Padding(0, S(4), S(8), 0) });
            string[] langs = { "", "ru", "en" };
            string[] langNames = { L.T("как в Windows", "as in Windows"), "Русский", "English" };
            for (int i = 0; i < 3; i++)
            {
                string code = langs[i];
                var r = new RadioButton { Text = langNames[i], AutoSize = true, Checked = Settings.Lang == code, ForeColor = Text1 };
                r.CheckedChanged += delegate
                {
                    if (!r.Checked || Settings.Lang == code) return;
                    Settings.Lang = code;
                    L.Load();
                    changed();
                    while (col.Controls.Count > 0) col.Controls[0].Dispose();
                    Text = L.T("Котик — настройки", "Kotik — settings");
                    BeginInvoke(new Action(Build));
                };
                langRow.Controls.Add(r);
            }
            col.Controls.Add(langRow);
            var talkRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, S(6), 0, 0) };
            talkRow.Controls.Add(new Label { Text = L.T("Болтливость:", "Chattiness:"), AutoSize = true, Margin = new Padding(0, S(4), S(8), 0) });
            string[] levels = { L.T("молчит", "silent"), L.T("иногда", "sometimes"), L.T("часто", "often") };
            for (int i = 0; i < 3; i++)
            {
                int lv = i;
                var r = new RadioButton { Text = levels[i], AutoSize = true, Checked = Settings.Talk == i, ForeColor = Text1 };
                r.CheckedChanged += delegate { if (r.Checked) { Settings.Talk = lv; changed(); } };
                talkRow.Controls.Add(r);
            }
            col.Controls.Add(talkRow);
            Box(L.T("Звуки (мурчание, мяу)", "Sounds (purring, meows)"), Settings.Sounds, v => Settings.Sounds = v);
            Box(L.T("Сидеть смирно (не гулять)", "Sit still (no walks)"), Settings.Calm, v => Settings.Calm = v);
            Box(L.T("Гулять внутри программ (по полочкам в окнах)", "Walk inside programs (on the shelves of windows)"), Settings.Shelves, v => Settings.Shelves = v);
            Box(L.T("Огонёк рядом", "Ember is around"), Settings.Buddy, v => Settings.Buddy = v);
            Box(L.T("Запускаться вместе с Windows", "Start with Windows"), Settings.Autostart, v => Settings.Autostart = v);

            Head(L.T("Функции", "Features"));
            string[][] list =
            {
                new[] { "gifts", L.T("Подарки от кота", "Gifts from the cat") },
                new[] { "apps", L.T("Комментарии к программам", "Comments on the programs you open") },
                new[] { "breaks", L.T("Напоминания о перерыве", "Break reminders") },
                new[] { "night", L.T("Ночные напоминания", "Late-night reminders") },
                new[] { "tips", L.T("Советы Огонька по коду", "Ember's coding tips") },
                new[] { "hats", L.T("Наряды по сезону", "Seasonal outfits") },
                new[] { "snow", L.T("Снег зимой", "Snow in winter") },
                new[] { "ball", L.T("Огонёк скачет мячиком в тихих местах", "Ember bounces like a ball in quiet spots") },
                new[] { "music", L.T("Под музыку из Spotify Огонёк танцует, а кот качает хвостом", "With Spotify music Ember dances and the cat sways its tail") },
                new[] { "naughty", L.T("Вредина: кот ловит курсор, когда ты без дела им крутишь, и уносит прятать (двигает твой курсор)",
                    "Naughty: when you idly wiggle the cursor, the cat catches it and carries it away (moves your cursor)") },
                new[] { "codecheck", L.T("Огонёк смотрит код (C, C++, Python) в папках недавних проектов VS Code и подсказывает. Читает файлы только на этом компьютере",
                    "Ember looks at your code (C, C++, Python) in recent VS Code project folders and gives hints. Files are read only on this computer") },
            };
            foreach (var f in list)
            {
                string key = f[0];
                Box(f[1], Settings.Feature(key), v => Settings.SetFeature(key, v));
            }

            Head(L.T("Праздники и события", "Holidays and events"));
            Box(L.T("Календарь включён (все события ниже)", "Calendar on (all the events below)"), Settings.Feature("calendar"), v => Settings.SetFeature("calendar", v));
            Note(L.T("Каждое событие само открывается в свой день и само закрывается. Можно выключить любое.", "Each event opens by itself on its day and closes by itself. Any of them can be switched off."));
            foreach (var e in Calendar.All)
            {
                string id = e.Id;
                bool now = Calendar.Active(e, Season.Today);
                var c = Box(string.Format("{0}{1} — {2}", now ? L.T("● сейчас: ", "● now: ") : "", e.Name, Calendar.Dates(e)), Settings.Feature("ev_" + id), v => Settings.SetFeature("ev_" + id, v));
                if (now) c.ForeColor = Accent;
                Note("      " + e.What);
            }

            Head(L.T("Память", "Memory"));
            Box(L.T("Подстраиваться под свободную память (около 10 % от свободной)", "Adapt to free memory (about 10% of what's free)"), Settings.Feature("adaptive"), v => Settings.SetFeature("adaptive", v));
            Note(Budget.Describe() + L.T(". Когда памяти мало, выключаются украшения, снег, листья и полочки; кот и Огонёк остаются.", ". When memory is low, the decorations, snow, leaves and shelves turn off; the cat and Ember stay."));
        }
    }
}
