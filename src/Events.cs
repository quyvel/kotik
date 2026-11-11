using System;
using System.Collections.Generic;

namespace Kotik
{
    /// A dated event: it opens on its first day and closes after the last one. Each can be switched off in the settings.
    class Ev
    {
        public string Id, Ru, RuWhat, En, EnWhat;
        public string Name { get { return L.En ? En : Ru; } }
        public string What { get { return L.En ? EnWhat : RuWhat; } }
        public int M1, D1, M2, D2;
        public Func<DateTime, bool> Rule;      // for dates that move (Black Friday)
    }

    /// All the events of the cat's year. Kept to things that are fun for anyone (the Old New Year is a Russian one).
    static class Calendar
    {
        public static readonly Ev[] All =
        {
            new Ev { Id = "halloween", Ru = "Хэллоуин", RuWhat = "шляпа ведьмы, тыква-фонарь на панели (нажми — «Бу!»), Огонёк в костюме привидения",
                En = "Halloween", EnWhat = "a witch hat, a jack-o'-lantern on the taskbar (click it for \"Boo!\"), Ember dressed as a ghost", M1 = 10, D1 = 30, M2 = 11, D2 = 1 },
            new Ev { Id = "autumn", Ru = "Листопад", RuWhat = "листья падают по всему экрану; пока тебя нет — копятся внизу, мышкой их можно разметать",
                En = "Falling leaves", EnWhat = "leaves fall over the whole screen; while you're away they pile up at the bottom, sweep them with the mouse", M1 = 11, D1 = 2, M2 = 11, D2 = 30 },
            new Ev { Id = "singles", Ru = "11.11 — день распродаж", RuWhat = "кот приносит купон «−50% на рыбку»",
                En = "11.11 — shopping day", EnWhat = "the cat brings a coupon: \"50% off fish\"", M1 = 11, D1 = 11, M2 = 11, D2 = 11 },
            new Ev { Id = "kindness", Ru = "День доброты", RuWhat = "Огонёк говорит добрые вещи и посылает тебе сердечки",
                En = "World Kindness Day", EnWhat = "Ember says kind things and sends you hearts", M1 = 11, D1 = 13, M2 = 11, D2 = 13 },
            new Ev { Id = "blackcat", Ru = "День чёрного кота", RuWhat = "кот на день становится чёрным",
                En = "Black Cat Day", EnWhat = "the cat turns black for a day", M1 = 11, D1 = 17, M2 = 11, D2 = 17 },
            new Ev { Id = "hello", Ru = "День приветствий", RuWhat = "кот здоровается со всеми программами",
                En = "World Hello Day", EnWhat = "the cat says hello to every program", M1 = 11, D1 = 21, M2 = 11, D2 = 21 },
            new Ev { Id = "blackfriday", Ru = "Чёрная пятница", RuWhat = "кот продаёт поглаживания со скидкой",
                En = "Black Friday", EnWhat = "the cat sells pets at a discount", Rule = d => d.Month == 11 && d.DayOfWeek == DayOfWeek.Friday && d.Day >= 22 && d.Day <= 28 },
            new Ev { Id = "autumnend", Ru = "Последний день осени", RuWhat = "листопад гуще обычного, «завтра зима!»",
                En = "Last day of autumn", EnWhat = "more leaves than usual, \"winter tomorrow!\"", M1 = 11, D1 = 30, M2 = 11, D2 = 30 },
            new Ev { Id = "winter", Ru = "Зима", RuWhat = "шарф, снежки с Огоньком, снеговик на панели растёт день за днём",
                En = "Winter", EnWhat = "a scarf, snowballs with Ember, a snowman on the taskbar that grows day by day", M1 = 12, D1 = 1, M2 = 2, D2 = 28 },
            new Ev { Id = "snow", Ru = "Снегопад", RuWhat = "снег по всему экрану; пока тебя нет — растут сугробы, мышкой их можно разгрести",
                En = "Snowfall", EnWhat = "snow over the whole screen; while you're away drifts grow, clear them with the mouse", M1 = 12, D1 = 1, M2 = 1, D2 = 20 },
            new Ev { Id = "solstice", Ru = "Самая длинная ночь", RuWhat = "кот спит больше обычного",
                En = "The longest night", EnWhat = "the cat sleeps more than usual", M1 = 12, D1 = 22, M2 = 12, D2 = 22 },
            new Ev { Id = "tree", Ru = "Ёлка", RuWhat = "ёлочка с огоньками; 31 декабря и 1 января под ней подарки с предсказаниями",
                En = "Christmas tree", EnWhat = "a little tree with lights; on December 31 and January 1 there are presents with fortunes under it", M1 = 12, D1 = 25, M2 = 1, D2 = 10 },
            new Ev { Id = "garland", Ru = "Гирлянда", RuWhat = "огоньки вдоль панели задач",
                En = "Garland", EnWhat = "lights along the taskbar", M1 = 12, D1 = 27, M2 = 1, D2 = 8 },
            new Ev { Id = "santa", Ru = "Новогодний колпак", RuWhat = "колпак вместо шарфа",
                En = "Santa hat", EnWhat = "a Santa hat instead of the scarf", M1 = 12, D1 = 20, M2 = 1, D2 = 10 },
            new Ev { Id = "eve", Ru = "Канун Нового года", RuWhat = "мандарины, «Загадать желание» в меню, отсчёт, салют",
                En = "New Year's Eve", EnWhat = "tangerines, \"Make a wish\" in the menu, a countdown, fireworks", M1 = 12, D1 = 30, M2 = 12, D2 = 31 },
            new Ev { Id = "newyear", Ru = "Новогодние праздники", RuWhat = "конфетти вместо сердечек, Огонёк напомнит о желании",
                En = "New Year holidays", EnWhat = "confetti instead of hearts, Ember reminds you of your wish", M1 = 1, D1 = 1, M2 = 1, D2 = 8 },
            new Ev { Id = "xmas", Ru = "Рождество", RuWhat = "Огонёк светится золотом",
                En = "Orthodox Christmas", EnWhat = "Ember glows gold", M1 = 1, D1 = 7, M2 = 1, D2 = 7 },
            new Ev { Id = "oldny", Ru = "Старый Новый год", RuWhat = "маленький салют в полночь",
                En = "Old New Year", EnWhat = "small fireworks at midnight", M1 = 1, D1 = 13, M2 = 1, D2 = 14 },
            new Ev { Id = "afterparty", Ru = "После праздников", RuWhat = "«праздники кончились, а я остался»",
                En = "After the holidays", EnWhat = "\"the holidays are over, but I'm still here\"", M1 = 1, D1 = 11, M2 = 1, D2 = 20 },
        };

        public static bool Active(Ev e, DateTime d)
        {
            if (e.Rule != null) return e.Rule(d);
            int v = d.Month * 100 + d.Day, a = e.M1 * 100 + e.D1, b = e.M2 * 100 + e.D2;
            return a <= b ? v >= a && v <= b : v >= a || v <= b;     // ranges may cross New Year
        }

        /// The event is going on today and is not switched off.
        public static bool On(string id)
        {
            if (!Settings.Feature("calendar")) return false;
            foreach (var e in All)
                if (e.Id == id) return Settings.Feature("ev_" + id) && Active(e, Season.Today);
            return false;
        }

        public static string Dates(Ev e)
        {
            if (e.Rule != null) return L.T("четвёртая пятница ноября", "the fourth Friday of November");
            string a = L.En ? Month(e.M1) + " " + e.D1 : e.D1 + " " + Month(e.M1), b = L.En ? Month(e.M2) + " " + e.D2 : e.D2 + " " + Month(e.M2);
            return a == b ? a : a + " – " + b;
        }

        static string Month(int m)
        {
            return (L.En ? new[] { "", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" }
                : new[] { "", "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря" })[m];
        }
    }

    /// How much the cat allows itself, judged by the free memory: about a tenth of what is free.
    static class Budget
    {
        /// 0: under ~450 MB free — only the cat and Ember; 1: normal; 2: over ~1 GB — everything, extra effects.
        public static int Level
        {
            get
            {
                if (!Settings.Feature("adaptive")) return 2;
                long free = Native.FreeMB();
                return free < 450 ? 0 : free < 1000 ? 1 : 2;
            }
        }

        public static string Describe()
        {
            long free = Native.FreeMB();
            int l = Level;
            string mode = l == 0 ? L.T("экономный режим: только кот и Огонёк", "saving mode: only the cat and Ember") : l == 1 ? L.T("обычный режим", "normal mode") : L.T("всё включено, с эффектами", "everything on, with effects");
            return string.Format(L.T("Свободно {0:0.0} ГБ — {1}", "{0:0.0} GB free — {1}"), free / 1024.0, mode);
        }
    }
}
