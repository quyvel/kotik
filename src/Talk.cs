using System;
using System.Collections.Generic;

namespace Kotik
{
    /// Everything the cat says, in Russian and in English. {0} = your name, {1} = the cat's name.
    static class Talk
    {
        static readonly Random rnd = new Random();

        // Phrases already said are remembered in the registry (a short hash each), so a restart does not start the lists over:
        // a phrase comes back only after every other one of its list has been said.
        static HashSet<string> said;
        static string Hash(string s) { uint h = 2166136261; foreach (char c in s) { h ^= c; h *= 16777619; } return h.ToString("x8"); }
        public static string Pick(string[] list)
        {
            if (said == null) said = new HashSet<string>(Settings.Said.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            var fresh = new List<string>();
            foreach (var s in list) if (!said.Contains(Hash(s))) fresh.Add(s);
            if (fresh.Count == 0) { foreach (var s in list) said.Remove(Hash(s)); fresh.AddRange(list); }
            string pick = fresh[rnd.Next(fresh.Count)];
            said.Add(Hash(pick));
            try { Settings.Said = string.Join(",", said); } catch { }
            return pick;
        }

        public static string[] Morning { get { return L.A(MorningRu, MorningEn); } }
        static readonly string[] MorningRu = { "Доброе утро, {0}! Я выспался.", "Утро! Я уже обошёл все окна.", "Проснулись? Я давно. Ну, минуту назад." };
        static readonly string[] MorningEn = { "Good morning, {0}! I slept well.", "Morning! I've already walked around all the windows.", "Up already? I've been up for ages. Well, a minute." };

        public static string[] Day { get { return L.A(DayRu, DayEn); } }
        static readonly string[] DayRu = { "Привет, {0}! Я тут погуляю по окнам, ладно?", "О, привет! Я соскучился.", "Мяу! Это значит «привет»." };
        static readonly string[] DayEn = { "Hi, {0}! I'll take a walk on your windows, okay?", "Oh, hi! I missed you.", "Meow! That means \"hello\"." };

        public static string[] Evening { get { return L.A(EveningRu, EveningEn); } }
        static readonly string[] EveningRu = { "Добрый вечер, {0}. Самое кошачье время.", "Вечер! Пора уютно сидеть у экрана." };
        static readonly string[] EveningEn = { "Good evening, {0}. The most catlike time of day.", "Evening! Time to sit cozily by the screen." };

        public static string[] Night { get { return L.A(NightRu, NightEn); } }
        static readonly string[] NightRu = { "Ночь, {0}… Коты в это время охотятся. А люди спят.", "Не спится? Мне тоже. Посидим." };
        static readonly string[] NightEn = { "It's night, {0}… Cats hunt at this hour. People sleep.", "Can't sleep? Me neither. Let's sit together." };

        public static string[] Chatter { get { return L.A(ChatterRu, ChatterEn); } }
        static readonly string[] ChatterRu =
        {
            "Я приземлился на четыре лапы. Как всегда.",
            "Мне снилось, что я поймал Огонька. Не говори ему.",
            "Если сесть ровно посередине экрана — я центр вселенной.",
            "Мурлыкаю на частоте хорошего настроения.",
            "Коробки — лучшее изобретение людей.",
            "Мяу. Перевожу: «а что у нас поесть?»",
            "Я моргнул тебе медленно. Это значит — доверяю.",
            "Сегодня я очень пушистый. Можно гладить.",
            "Шёл, шёл и сел. Классика.",
            "Хвост трубой. Значит, всё хорошо.",
            "Не спрашивай, почему я смотрю в стену. Там что-то есть.",
            "Кот на месте, порядок на месте.",
            "Я умываюсь, не отвлекай. Это важная работа.",
            "У меня девять жизней. Эту трачу на отдых.",
            "Кто тут самый лучший кот? Не отвечай, я знаю.",
            "Если я сплю — значит, всё спокойно.",
            "Я бы помурчал погромче, но тут все работают.",
            "Мои усы чувствуют, что скоро будет рыбка.",
            "Мне нравится, когда ты рядом и печатаешь. Это как дождик.",
            "Сижу ровно, как статуэтка. Египетская.",
            "Посмотри на меня. Ну посмотри. Вот, спасибо.",
            "Я охочусь. На тишину. Пока побеждаю.",
            "Когда-нибудь я тоже научусь программировать. Начну с кода «мяу».",
            "Обожаю тёплые места. Ноутбук — тёплое место.",
            "Сегодня отличный день, чтобы ничего не уронить. Почти.",
            "Мягкие лапки, острый ум.",
            "Солнце не светит в монитор. Непорядок.",
            "Я тут подумал… нет, забыл.",
            "Если я сижу на окне — окно моё.",
            "Мяу два раза — значит очень хорошо.",
            "Курсор опять сбежал. Я дам ему фору.",
            "Пиксели сегодня особенно мягкие.",
            "Хвост — это антенна. Ловлю настроение.",
            "Я не ленюсь. Я экономлю энергию.",
            "Кто-нибудь видел мою рыбку? Нет? Жаль.",
            "Сейчас бы полежать на тёплом ноутбуке.",
            "Ты так быстро печатаешь. Это охота?",
            "У меня четыре лапы и ноль забот.",
            "Если что, я на связи. Мур.",
            "Огонёк опять умничает. Люблю его.",
            "Однажды я поймаю этот курсор. Однажды.",
            "Зевнул. Это не к тебе, это возраст.",
            "Лучшее окно — то, которое не двигается.",
            "Я считаю пиксели. Дошёл до трёх.",
            "Уши вверх — значит слушаю.",
            "Шорох вентилятора — моя любимая музыка.",
            "Я тихонько. Ты даже не заметишь.",
            "Мне снилась огромная мышь. Компьютерная.",
            "Ты тоже любишь сидеть у окна?",
            "Когтеточка бы не помешала. Намёк.",
            "Я охраняю панель задач. Пока без происшествий.",
            "Мрр-мяу. Это по-кошачьи «удачи».",
            "Жмурюсь. Это кошачья улыбка, между прочим.",
            "Пойду проверю правый угол экрана.",
            "Моргаешь? Я заметил.",
            "Котики не опаздывают. Остальные приходят рано.",
            "Пушистость — это стиль жизни.",
            "Если окно закроется, я не виноват.",
            "Пятница? Не знаю, у котов каждый день пятница.",
            "Мур на всю громкость.",
            "Помыться или поспать? Сложный выбор.",
            "Я бы помог, но у меня лапки.",
            "Вон та иконка подозрительно блестит.",
            "Обожаю, когда ты рядом.",
            "Тишина. Можно и подремать.",
            "Лапы гудят. Набегался за курсором.",
            "Хорошо, что у тебя нет пылесоса на экране.",
            "Я начал считать до ста и уснул на семи.",
            "Мне нравится твоя музыка. Иногда.",
            "Кошачий совет: потянись. Вот так.",
            "Где-то пищит курсор. Слышу.",
            "Огонёк говорит, я милый. Огонёк прав.",
            "Этот экран мне по размеру.",
            "Если долго смотреть на код, код посмотрит на тебя. Мяу.",
            "Я здесь главный по уюту.",
            "Тапочки не нужны, у меня лапы.",
            "Мяу — это короткое «я тебя люблю».",
            "Заглянул под окно — там тоже экран.",
            "Хвост сам по себе машет. Я не при делах.",
            "Кофе не пью. Сплю без допинга.",
            "Сегодня я в хорошем настроении. И вчера был.",
            "Пиши, пиши. Я рядом посижу.",
            "Интересно, что там за окном браузера.",
            "Поймал муху. Это была точка. Неважно.",
            "Если что-то сломалось — это не я. Наверное.",
            "Мрр.", "Окна — лучшие полки на свете.", "Я слежу за курсором. На всякий случай.",
            "Давай так: ты работаешь, а я лежу. Честно же?", "Мяу. Это значит «всё хорошо».",
            "Погладь меня. Просто нажми на меня.", "Меня можно взять и перенести. Только аккуратно.",
            "На панели задач тепло. Особенно возле часов.", "Если что — я на окне.",
            "Я сегодня ничего не уронил. Пока.", "Знаешь, сколько коты спят? Вот и я не считал.",
            "Хочешь, сяду на твоё окно? Я уже сижу.", "Курсор — это мышь? Подозрительно похоже.",
            "У тебя красивый рабочий стол. Мне тут нравится.", "Мур-мур. Просто так.",
            "Если долго не трогать мышь, я усну. Предупреждаю.", "Подвигай окно, на котором я сижу. Я покатаюсь!",
            "Кофе бодрит? Мне бы молочка.", "Я сегодня видел муху. На экране. Это была иконка.", "Можно я посплю у тебя на вкладках?",
            "Я не ленивый. Я энергосберегающий.", "Огонёк опять думает. Я тоже думаю. О рыбке.",
            "Знаешь, почему коты любят коробки? Я тоже не знаю. Но люблю.", "Поиграем? В меню есть лазер. Просто говорю.",
            "Если меня долго гладить, я принесу подарок. Наверное.", "Мой любимый звук — шуршание пакета.",
            "Когда ты печатаешь, я слушаю. Красиво стучишь.",
            "Иногда я смотрю в одну точку. Там что-то есть. Тебе не понять.", "Если что-то не работает — попробуй выключить и включить. Меня не надо.",
        };
        static readonly string[] ChatterEn =
        {
            "I landed on all four paws. As always.",
            "I dreamed I caught Ember. Don't tell him.",
            "If I sit right in the middle of the screen, I'm the center of the universe.",
            "Purring on the good-mood frequency.",
            "Boxes are humanity's best invention.",
            "Meow. Translation: \"what's for dinner?\"",
            "I blinked at you slowly. That means I trust you.",
            "I'm extra fluffy today. Pets allowed.",
            "Walked, walked, and sat down. A classic.",
            "Tail up. That means all is well.",
            "Don't ask why I'm staring at the wall. There's something there.",
            "Cat in place, everything in order.",
            "I'm washing, don't distract me. This is important work.",
            "I have nine lives. I'm spending this one resting.",
            "Who's the best cat here? Don't answer, I know.",
            "If I'm asleep, all is calm.",
            "I'd purr louder, but everyone's working here.",
            "My whiskers say fish is coming soon.",
            "I like it when you're near and typing. It sounds like rain.",
            "Sitting straight like a statue. An Egyptian one.",
            "Look at me. Come on, look. There, thank you.",
            "I'm hunting. For silence. Winning so far.",
            "Someday I'll learn to code too. I'll start with \"meow\".",
            "I love warm places. A laptop is a warm place.",
            "A great day to not knock anything over. Almost.",
            "Soft paws, sharp mind.",
            "No sunshine on the monitor. Unacceptable.",
            "I was thinking… nope, forgot.",
            "If I'm sitting on a window, the window is mine.",
            "Two meows mean very good.",
            "The cursor ran off again. I'll give it a head start.",
            "The pixels are especially soft today.",
            "My tail is an antenna. Picking up the mood.",
            "I'm not lazy. I'm saving energy.",
            "Has anyone seen my fish? No? Pity.",
            "I could use a nap on a warm laptop right now.",
            "You type so fast. Is that hunting?",
            "Four paws and zero worries.",
            "If anything, I'm here. Purr.",
            "Ember is being a know-it-all again. I love him.",
            "One day I'll catch that cursor. One day.",
            "Yawned. Not because of you, it's my age.",
            "The best window is the one that doesn't move.",
            "I'm counting pixels. Got to three.",
            "Ears up means I'm listening.",
            "The hum of the fan is my favorite music.",
            "I'll be quiet. You won't even notice.",
            "I dreamed of a giant mouse. A computer one.",
            "Do you like sitting by the window too?",
            "A scratching post would be nice. Just a hint.",
            "Guarding the taskbar. No incidents so far.",
            "Mrr-meow. That's cat for \"good luck\".",
            "Squinting. That's a cat smile, by the way.",
            "Going to check the right corner of the screen.",
            "You blinked? I saw that.",
            "Cats are never late. Everyone else is early.",
            "Fluffiness is a lifestyle.",
            "If the window closes, it wasn't me.",
            "Friday? Don't know, for cats every day is Friday.",
            "Purring at full volume.",
            "Wash or nap? Tough choice.",
            "I'd help, but I have paws.",
            "That icon over there looks suspiciously shiny.",
            "I love it when you're around.",
            "Quiet. Time for a nap.",
            "My paws hurt. Too much running after the cursor.",
            "Good thing there's no vacuum cleaner on your screen.",
            "I started counting to a hundred and fell asleep at seven.",
            "I like your music. Sometimes.",
            "Cat tip: stretch. Like this.",
            "Somewhere a cursor is squeaking. I can hear it.",
            "Ember says I'm cute. Ember is right.",
            "This screen is just my size.",
            "Stare at the code long enough and the code stares back. Meow.",
            "I'm in charge of coziness here.",
            "No slippers needed, I have paws.",
            "Meow is a short \"I love you\".",
            "I peeked under the window. More screen there.",
            "The tail wags on its own. Not my doing.",
            "I don't drink coffee. I sleep without it.",
            "I'm in a good mood today. Yesterday too.",
            "Keep typing. I'll sit right here.",
            "I wonder what's behind the browser window.",
            "Caught a fly. It was a dot. Never mind.",
            "If something broke, it wasn't me. Probably.",
            "Purr.", "Windows are the best shelves in the world.", "I'm keeping an eye on the cursor. Just in case.",
            "Deal: you work, I lie here. Fair?", "Meow. That means \"all good\".",
            "Pet me. Just click on me.", "You can pick me up and carry me. Gently, please.",
            "It's warm on the taskbar. Especially near the clock.", "If you need me, I'm on a window.",
            "I haven't knocked anything over today. Yet.", "Know how much cats sleep? Me neither, never counted.",
            "Want me to sit on your window? I already am.", "Is the cursor a mouse? Suspiciously similar.",
            "You have a pretty desktop. I like it here.", "Purr-purr. Just because.",
            "If you leave the mouse alone for long, I'll fall asleep. Fair warning.", "Move the window I'm sitting on. I'll go for a ride!",
            "Coffee wakes you up? I'd rather have milk.", "I saw a fly today. On the screen. It was an icon.", "Can I nap on your tabs?",
            "I'm not lazy. I'm energy-efficient.", "Ember is thinking again. I'm thinking too. About fish.",
            "Know why cats love boxes? Me neither. But I do.", "Want to play? There's a laser in the menu. Just saying.",
            "Pet me long enough and I'll bring you a present. Maybe.", "My favorite sound is a rustling bag.",
            "When you type, I listen. You type beautifully.",
            "Sometimes I stare at one spot. There's something there. You wouldn't understand.", "If something doesn't work, try turning it off and on. Not me, though.",
        };

        public static string[] Ride { get { return L.A(RideRu, RideEn); } }
        static readonly string[] RideRu =
        {
            "Ой-ой-ой, держусь!", "Мяу! Мы летим!", "Высоко! Не урони меня!", "Я капитан этого корабля.", "Мяу! Не так быстро!",
            "Я держусь! Наверное.", "Высоко-высоко!", "Это лучше, чем подоконник.", "Мрр… ладно, мне нравится.",
            "Хвост дрожит, это от восторга.", "Ещё круг! Нет! Да!", "Сверху курсор кажется маленьким.", "Не урони, я ценный.",
            "Я — кот-пилот.", "Вижу миску! Курс на миску!", "Мур. Полёт нормальный.",
        };
        static readonly string[] RideEn =
        {
            "Whoa-whoa-whoa, holding on!", "Meow! We're flying!", "So high! Don't drop me!", "I'm the captain of this ship.", "Meow! Not so fast!",
            "I'm holding on! I think.", "Higher and higher!", "Better than a windowsill.", "Mrr… fine, I like it.",
            "My tail is shaking. That's excitement.", "One more lap! No! Yes!", "The cursor looks tiny from up here.", "Don't drop me, I'm valuable.",
            "I'm a pilot cat.", "I see the bowl! Course set for the bowl!", "Purr. Flight is smooth.",
        };

        public static string[] Carried { get { return L.A(CarriedRu, CarriedEn); } }
        static readonly string[] CarriedRu =
        {
            "Сиди-сиди, ты лёгкий.", "Куда едем, пассажир?", "Держись, сейчас прыгну! Шучу.", "Ты щекочешь мне спину.",
            "Мне не тяжело. Почти.", "Я как лошадка, только мурлычу.", "Оплата рыбкой, пожалуйста.", "Держись за уши, только нежно.",
            "Сейчас покажу тебе свою полку.", "Едем медленно и красиво.", "Ты тёплый. Неожиданно.", "Мрр, поехали дальше.",
        };
        static readonly string[] CarriedEn =
        {
            "Sit tight, you're light.", "Where to, passenger?", "Hold on, I'm about to jump! Kidding.", "You're tickling my back.",
            "You're not heavy. Almost.", "I'm like a pony, only I purr.", "Payment in fish, please.", "Hold my ears, gently.",
            "Let me show you my shelf.", "Slow and stylish.", "You're warm. Unexpectedly.", "Mrr, let's keep going.",
        };

        public static string[] Petted { get { return L.A(new[] { "Мрр…", "Ещё!", "Вот тут, за ушком.", "Мурр-мурр.", "Хорошо-то как.", "Ты чудо." },
            new[] { "Purr…", "More!", "Right there, behind the ear.", "Purr-purr.", "That's so nice.", "You're wonderful." }); } }
        public static string[] Thrown { get { return L.A(new[] { "Уиии!", "Эй!", "Я умею летать!", "Аккуратнее!", "Ещё раз! Нет, стой." },
            new[] { "Wheee!", "Hey!", "I can fly!", "Careful!", "Again! No, wait." }); } }
        public static string[] Landed { get { return L.A(new[] { "Приземлился на все четыре.", "Опа.", "Котики всегда падают на лапы." },
            new[] { "Landed on all four.", "Oops-a-daisy.", "Cats always land on their feet." }); } }
        public static string[] Back { get { return L.A(new[] { "О, ты тут!", "Я тебя ждал.", "Мяу! Наконец-то.", "Я тут немного поспал." },
            new[] { "Oh, you're back!", "I was waiting for you.", "Meow! Finally.", "I took a little nap." }); } }
        public static string[] Hungry { get { return L.A(new[] { "Мяу… Есть хочу. Покорми? (значок в трее → «Покормить рыбкой»)", "Миска пустая. Совсем.", "Рыбки бы…" },
            new[] { "Meow… I'm hungry. Feed me? (tray icon → \"Feed a fish\")", "The bowl is empty. Completely.", "Some fish would be nice…" }); } }
        public static string[] Fed { get { return L.A(new[] { "Рыбка! Спасибо!", "Ням. Вкусно.", "Вот это я понимаю — забота.", "Мрр. Теперь можно и поспать." },
            new[] { "Fish! Thank you!", "Yum. Tasty.", "Now that's what I call care.", "Purr. Now I can nap." }); } }
        public static string[] Full { get { return L.A(new[] { "Я сыт, но от рыбки не откажусь." }, new[] { "I'm full, but I won't say no to fish." }); } }
        public static string[] Break { get { return L.A(BreakRu, BreakEn); } }
        static readonly string[] BreakRu =
        {
            "Может, потянешься? Как я. Не настаиваю, просто предлагаю.", "Глаза, наверное, устали. Можно глянуть в окно. Если хочешь.",
            "Если будет минутка — попей воды. Я подожду.", "Ты давно сидишь. Я бы на твоём месте вздремнул. Но решать тебе.",
            "Перерыв — не обязательно. Но я бы советовал. Мурр.", "Встань, пройдись до кухни и обратно. Я посторожу компьютер.",
        };
        static readonly string[] BreakEn =
        {
            "Maybe stretch? Like me. Not insisting, just suggesting.", "Your eyes must be tired. Look out the window, if you like.",
            "If you have a minute, drink some water. I'll wait.", "You've been sitting a long time. I'd take a nap if I were you. Your call.",
            "A break isn't required. But I'd recommend one. Purr.", "Get up, walk to the kitchen and back. I'll guard the computer.",
        };
        public static string[] Late { get { return L.A(new[] { "Уже ночь. Коты спят, и людям пора.", "Завтра будет новый день. Может, спать?", "Ты знаешь, который час? Я знаю. Поздно." },
            new[] { "It's night already. Cats sleep, and people should too.", "Tomorrow is a new day. Maybe bed?", "Do you know what time it is? I do. Late." }); } }
        public static string[] Ram { get { return L.A(new[] { "Компьютер пыхтит. Может, закрыть что-нибудь лишнее?", "Памяти почти не осталось. Мне тесно!" },
            new[] { "The computer is huffing. Maybe close something you don't need?", "Almost no memory left. It's crowded in here!" }); } }
        public static string[] Battery { get { return L.A(new[] { "Батарейка садится! Где зарядка?", "Заряд кончается. Я не шучу." },
            new[] { "Battery's running low! Where's the charger?", "The charge is running out. I'm not kidding." }); } }
        public static string[] Bye { get { return L.A(new[] { "Пока! Возвращайся.", "Я ушёл спать. Позови, если что." },
            new[] { "Bye! Come back soon.", "I went to sleep. Call me if you need me." }); } }

        /// What the cat says when you switch to an app (part of the process name → phrases).
        public static Dictionary<string, string[]> Apps { get { return L.En ? AppsEn : AppsRu; } }
        static readonly Dictionary<string, string[]> AppsRu = new Dictionary<string, string[]>
        {
            { "minecraft", new[] { "О, Minecraft! Можно я там тоже буду котом?" } },
            { "javaw", new[] { "О, Minecraft! Можно я там тоже буду котом?" } },
            { "chrome", new[] { "Опять вкладки… Сколько их там?", "Найди видео с котиками. Для меня." } },
            { "msedge", new[] { "Опять вкладки… Сколько их там?" } },
            { "firefox", new[] { "Опять вкладки… Сколько их там?" } },
            { "yandex", new[] { "Опять вкладки… Сколько их там?" } },
            { "browser", new[] { "Найди видео с котиками. Для меня." } },
            { "code", new[] { "Пишешь код? Я посижу рядом, буду ревьюером.", "Если будет баг — я его поймаю. Я же кот." } },
            { "telegram", new[] { "Передай всем привет от меня.", "Кто пишет? Мне тоже интересно." } },
            { "spotify", new[] { "Включи что-нибудь мурчащее.", "Под эту музыку хорошо спится." } },
            { "steam", new[] { "Во что играем?", "Только недолго, ладно?" } },
            { "discord", new[] { "Скажи друзьям, что у тебя есть кот." } },
            { "godot", new[] { "Опять что-то строишь? Можно мне тоже игру?" } },
        };
        static readonly Dictionary<string, string[]> AppsEn = new Dictionary<string, string[]>
        {
            { "minecraft", new[] { "Oh, Minecraft! Can I be a cat in there too?" } },
            { "javaw", new[] { "Oh, Minecraft! Can I be a cat in there too?" } },
            { "chrome", new[] { "Tabs again… How many are there?", "Find a cat video. For me." } },
            { "msedge", new[] { "Tabs again… How many are there?" } },
            { "firefox", new[] { "Tabs again… How many are there?" } },
            { "yandex", new[] { "Tabs again… How many are there?" } },
            { "browser", new[] { "Find a cat video. For me." } },
            { "code", new[] { "Writing code? I'll sit nearby and be your reviewer.", "If there's a bug, I'll catch it. I'm a cat, after all." } },
            { "telegram", new[] { "Say hi to everyone from me.", "Who's writing? I'm curious too." } },
            { "spotify", new[] { "Put on something purry.", "This music is great for napping." } },
            { "steam", new[] { "What are we playing?", "Not for too long, okay?" } },
            { "discord", new[] { "Tell your friends you have a cat." } },
            { "godot", new[] { "Building something again? Can I have a game too?" } },
        };
    }
}
