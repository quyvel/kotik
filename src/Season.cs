using System;
using System.Collections.Generic;

namespace Kotik
{
    /// What the calendar (Events.cs) means for the cat today: outfits, coat, snow, and the special moments of each date.
    /// Every moment fires once (remembered in the registry), so a restart does not repeat it.
    static class Season
    {
        public static DateTime? Fake;          // checks only: pretend it is another day
        public static DateTime Today { get { return Fake.HasValue ? Fake.Value.Date : DateTime.Today; } }

        public static string Hat()
        {
            if (!Settings.Feature("hats")) return "";
            if (Calendar.On("halloween")) return "witch";
            if (Calendar.On("santa")) return "santa";
            if (Calendar.On("winter")) return "scarf";
            return "";
        }

        /// On the day of the black cat the cat is black, whatever its usual coat.
        public static Coat CoatToday(Coat usual) { return Calendar.On("blackcat") ? Cat.Coats[2] : usual; }

        public static bool Snow() { return Settings.Feature("snow") && Calendar.On("snow") && Budget.Level >= 1; }

        /// The cat's first words of the day on special dates (null = an ordinary day). {0} — your name, {1} — the cat's.
        public static string CatLine()
        {
            if (!Settings.Feature("calendar")) return null;
            var d = Today;
            if (Calendar.On("halloween")) return d.Day == 31 ? L.T("С Хэллоуином, {0}! Я в шляпе ведьмы. Мяу-ха-ха!", "Happy Halloween, {0}! I'm wearing a witch hat. Meow-ha-ha!")
                : d.Month == 11 ? L.T("Хэллоуин прошёл, а шляпа осталась. Мне идёт.", "Halloween is over, but the hat stays. It suits me.")
                : L.T("Хэллоуин близко! Я уже примерил шляпу.", "Halloween is coming! I've already tried on the hat.");
            if (Calendar.On("singles")) return L.T("11.11 — день распродаж! Купи мне рыбку со скидкой.", "11.11, the big sale day! Buy me some fish at a discount.");
            if (Calendar.On("blackcat")) return L.T("Сегодня день чёрного кота. Так что сегодня я чёрный. Не пугайся, это я.", "It's Black Cat Day, so today I'm black. Don't be scared, it's me.");
            if (Calendar.On("hello")) return L.T("Сегодня всемирный день приветствий! Привет! Привет! И тебе привет!", "It's World Hello Day! Hello! Hello! And hello to you!");
            if (Calendar.On("blackfriday")) return L.T("Чёрная пятница! Самое ценное в этом доме — я. И я бесплатный.", "Black Friday! The most valuable thing in this house is me. And I'm free.");
            if (Calendar.On("autumnend")) return L.T("Последний день осени. Завтра зима — достану шарф.", "Last day of autumn. Winter tomorrow, I'll get my scarf out.");
            if (d.Month == 12 && d.Day == 1) return L.T("Первый день зимы! Я надел шарф. Тепло.", "First day of winter! I put on my scarf. Cozy.");
            if (Calendar.On("solstice")) return L.T("Самая длинная ночь в году. Значит, можно спать дольше. Я так и сделаю.", "The longest night of the year. That means more sleep. I'll do exactly that.");
            if (d.Month == 12 && d.Day == 31) return L.T("Сегодня Новый год! Я в колпаке. Где мандарины?", "It's New Year's Eve! I've got my hat on. Where are the tangerines?");
            if (d.Month == 1 && d.Day == 1) return L.T("С Новым годом, {0}! Мяу! Это будет лучший год. Я уверен.", "Happy New Year, {0}! Meow! This will be the best year. I'm sure of it.");
            if (Calendar.On("xmas")) return L.T("С Рождеством! Я весь день буду мурчать. Это подарок.", "Merry Christmas! I'll purr all day long. That's my present.");
            if (Calendar.On("oldny")) return L.T("Старый Новый год! Ещё один повод. Я в деле.", "The Old New Year! One more reason to celebrate. I'm in.");
            if (Calendar.On("afterparty")) return L.T("Праздники кончились, а я остался. Мрр.", "The holidays are over, but I'm still here. Purr.");
            if (Calendar.On("tree")) return L.T("Ёлка стоит! Можно я на неё залезу? Шучу. Наверное.", "The tree is up! Can I climb it? Just kidding. Probably.");
            if (Calendar.On("santa")) return L.T("Скоро Новый год! Колпак мне идёт?", "New Year is coming! Does the hat suit me?");
            if (d.DayOfWeek == DayOfWeek.Friday) return L.T("Пятница! Почти выходные. А у котов всегда выходные.", "Friday! Almost the weekend. Cats are always on a weekend.");
            if (d.DayOfWeek == DayOfWeek.Monday) return L.T("Понедельник… Давай начнём его с мурчания.", "Monday… Let's start it with a purr.");
            return null;
        }

        /// Ember's words on special dates. {2} {3} — the number of days left and the word for it.
        public static string BuddyLine()
        {
            if (!Settings.Feature("calendar")) return null;
            var d = Today;
            if (Calendar.On("halloween") && d.Day == 31) return L.T("Сегодня Хэллоуин. Я бы нарядился тыквой, но я и так оранжевый.", "It's Halloween. I'd dress up as a pumpkin, but I'm already orange.");
            if (Calendar.On("kindness")) return L.T("Сегодня день доброты. Ты молодец, {0}. И это не потому, что сегодня такой день.", "It's Kindness Day. You're doing great, {0}. And not just because of the day.");
            if (Calendar.On("blackcat")) return L.T("{1} сегодня чёрный. Говорят, это к удаче. Проверим?", "{1} is black today. They say it brings luck. Shall we check?");
            if (d.Month == 12 && d.Day == 31) return L.T("Новый год сегодня! Что бы тебе хотелось оставить в старом году?", "New Year's Eve! What would you leave behind in the old year?");
            if (d.Month == 1 && d.Day == 1) return L.T("С Новым годом, {0}! Пусть в нём будет много законченных проектов.", "Happy New Year, {0}! May it bring lots of finished projects.");
            if (Calendar.On("santa") && d.Month == 12) return L.T("До Нового года {2} {3}. Ёлка уже наряжена?", "{2} {3} until the New Year. Is the tree decorated yet?");
            return null;
        }

        static string[] Kind { get { return L.A(KindRu, KindEn); } }
        static readonly string[] KindRu =
        {
            "Сегодня день доброты. Напиши кому-нибудь хорошее. Просто так.", "Ты справляешься лучше, чем думаешь.",
            "Доброта — это когда помогаешь, даже если никто не видит. Как {1}, который охраняет панель задач.",
        };
        static readonly string[] KindEn =
        {
            "It's Kindness Day. Write something nice to someone. Just because.", "You're doing better than you think.",
            "Kindness is helping even when nobody sees. Like {1}, guarding the taskbar.",
        };

        /// One special moment due right now, or null. who: 0 = the cat, 1 = Ember. Fireworks for the first minutes of the year.
        public static string Due(DateTime now, out int who, out bool fireworks)
        {
            who = 1;
            fireworks = false;
            if (!Settings.Feature("calendar")) return null;
            int days = (int)(Today - Settings.Since).TotalDays + 1;
            foreach (int m in new[] { 7, 30, 50, 100, 365 })
                if (days == m && Once("days" + m))
                {
                    who = 0;
                    return string.Format(L.T("Мы вместе уже {0} {1}! Мрр-мрр. Это праздник.", "We've been together for {0} {1}! Purr-purr. That's a celebration."), m, PetForm.Days(m));
                }
            if (Calendar.On("kindness") && now.Hour >= 12 && now.Hour % 3 == 0 && Once("kind" + now.Hour))
                return Kind[now.Hour % Kind.Length];
            // the last minutes of the year: a countdown, then fireworks
            if (Calendar.On("eve") && now.Day == 31 && now.Hour == 23 && now.Minute >= 55 && Once("count" + now.Minute))
                return string.Format(L.T("До Нового года {0} мин!", "{0} min to the New Year!"), 60 - now.Minute);
            if (Calendar.On("newyear") && now.Month == 1 && now.Day == 1 && now.Hour == 0 && now.Minute < 30)
            {
                fireworks = true;
                if (Once("newyear")) return L.T("С НОВЫМ ГОДОМ, {0}!!! ✨", "HAPPY NEW YEAR, {0}!!! ✨");
            }
            if (Calendar.On("newyear") && now.Day >= 2 && now.Hour >= 12 && Settings.Wish != "" && Settings.WishYear == now.Year && Once("wish"))
                return L.T("Помнишь своё новогоднее желание? «", "Remember your New Year wish? \"") + Settings.Wish.Replace("{", "{{").Replace("}", "}}")
                    + L.T("». Я помню. Давай сделаем, чтобы сбылось.", "\". I do. Let's make it come true.");
            if (Calendar.On("oldny") && now.Day == 14 && now.Hour == 0 && now.Minute < 15)
            {
                fireworks = true;
                if (Once("oldny")) return L.T("Старый Новый год наступил! Маленький салют — специально для тебя.", "The Old New Year is here! Small fireworks, just for you.");
            }
            if (Calendar.On("xmas") && now.Hour >= 10 && Once("xmas"))
                return L.T("С Рождеством, {0}. Тёплого тебе января.", "Merry Christmas, {0}. Have a warm January.");
            return null;
        }

        /// True the first time a moment is asked for on this date (the key is remembered).
        static bool Once(string key)
        {
            string day = Today.ToString("yyyy-MM-dd");
            string k = day + ":" + key;
            var done = new List<string>(Settings.SeasonDone.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            if (done.Contains(k)) return false;
            done.RemoveAll(x => !x.StartsWith(day));   // keep only today's
            done.Add(k);
            Settings.SeasonDone = string.Join("|", done.ToArray());
            return true;
        }
    }
}
