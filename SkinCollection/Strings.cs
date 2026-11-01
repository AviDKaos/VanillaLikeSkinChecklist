using System;
using System.Collections.Generic;
using UnityEngine.Localization.Settings;

namespace SkinCollection
{
    /// <summary>
    /// The mod's own texts in every language the game ships. Wording follows the game's official translations
    /// where they exist: "skin" as in "Change Weapon Skin" (visual, aspecto, apparence, образ…), "Hold {0}" as in
    /// "Revive (Hold {0})", "Default" from the menu labels. Item and boat names come from the game itself.
    /// </summary>
    public static class Strings
    {
        public const string Title = "Title";
        public const string ShowUnobtainableRow = "ShowUnobtainableRow";
        public const string HoldTag = "HoldTag";
        public const string TotalSkins = "TotalSkins";
        public const string Page = "Page";
        public const string Default = "Default";
        public const string Common = "Common";
        public const string Rare = "Rare";
        public const string Legendary = "Legendary";
        public const string Unobtainable = "Unobtainable";
        public const string ShowUnobtainable = "ShowUnobtainable";
        public const string HideUnobtainable = "HideUnobtainable";
        public const string HintPress = "HintPress";
        public const string HintHold = "HintHold";
        public const string HintUnbound = "HintUnbound";
        public const string Island = "Island";
        public const string Islands = "Islands";
        public const string BowIsland = "BowIsland";
        public const string ListSeparator = "ListSeparator";

        public static event Action LanguageChanged;

        private static bool _subscribed;

        // Locale codes as the game's Localization settings report them.
        private static readonly Dictionary<string, Dictionary<string, string>> Table = new Dictionary<string, Dictionary<string, string>>
        {
            [Title] = new Dictionary<string, string>
            {
                ["en"] = "Skin Collection",
                ["zh-CN"] = "皮肤收藏",
                ["zh-TW"] = "外觀收藏",
                ["fr"] = "Collection d’apparences",
                ["de"] = "Skin-Sammlung",
                ["it"] = "Collezione skin",
                ["ja"] = "スキンコレクション",
                ["ko"] = "스킨 컬렉션",
                ["pl"] = "Kolekcja skórek",
                ["pt-BR"] = "Coleção de visuais",
                ["ru"] = "Коллекция образов",
                ["es-MX"] = "Colección de aspectos",
                ["es-ES"] = "Colección de diseños",
                ["sv"] = "Utseendesamling",
                ["tr"] = "Görünüm Koleksiyonu",
                ["uk"] = "Колекція образів",
            },
            [ShowUnobtainableRow] = new Dictionary<string, string>
            {
                ["en"] = "Show Unobtainable",
                ["zh-CN"] = "显示无法获得",
                ["zh-TW"] = "顯示無法取得",
                ["fr"] = "Afficher les inobtenables",
                ["de"] = "Nicht erhältliche anzeigen",
                ["it"] = "Mostra non ottenibili",
                ["ja"] = "入手不可を表示",
                ["ko"] = "획득 불가 표시",
                ["pl"] = "Pokaż nieosiągalne",
                ["pt-BR"] = "Mostrar indisponíveis",
                ["ru"] = "Показать недоступные",
                ["es-MX"] = "Mostrar inobtenibles",
                ["es-ES"] = "Mostrar inobtenibles",
                ["sv"] = "Visa ej tillgängliga",
                ["tr"] = "Elde Edilemezleri Göster",
                // Ukrainian control labels are nouns ("Зміна наживки", "Пропуск ролика").
                ["uk"] = "Показ недоступних",
            },
            // Mirrors the game's "(Hold {0})" labels: casing, verb form and full-width brackets per language.
            [HoldTag] = new Dictionary<string, string>
            {
                ["en"] = "(Hold)",
                ["zh-CN"] = "（长按）",
                ["zh-TW"] = "（長按）",
                ["fr"] = "(Maintenir)",
                ["de"] = "(halten)",
                ["it"] = "(Tieni premuto)",
                ["ja"] = "（長押し）",
                ["ko"] = "(길게 누르기)",
                ["pl"] = "(przytrzymaj)",
                ["pt-BR"] = "(Segure)",
                ["ru"] = "(удерживайте)",
                ["es-MX"] = "(Mantener presionado)",
                ["es-ES"] = "(mantén)",
                ["sv"] = "(Håll)",
                ["tr"] = "(Basılı Tut)",
                ["uk"] = "(Утрим.)",
            },
            // Same shape as the journal's "{n} / {total} Creatures": number first, noun as a capitalized label.
            [TotalSkins] = new Dictionary<string, string>
            {
                ["en"] = "{0} / {1} Skins",
                ["zh-CN"] = "{0} / {1} 皮肤",
                ["zh-TW"] = "{0} / {1} 外觀",
                ["fr"] = "{0} / {1} Apparences",
                ["de"] = "{0} / {1} Skins",
                ["it"] = "{0} / {1} Skin",
                ["ja"] = "{0} / {1} スキン",
                ["ko"] = "{0} / {1} 스킨",
                ["pl"] = "{0} / {1} Skórki",
                ["pt-BR"] = "{0} / {1} Visuais",
                ["ru"] = "{0} / {1} Образы",
                ["es-MX"] = "{0} / {1} Aspectos",
                ["es-ES"] = "{0} / {1} Diseños",
                ["sv"] = "{0} / {1} Utseenden",
                ["tr"] = "{0} / {1} Görünümler",
                ["uk"] = "{0} / {1} Образи",
            },
            [Page] = new Dictionary<string, string>
            {
                ["en"] = "Page {0} / {1}",
                ["zh-CN"] = "第 {0} / {1} 页",
                ["zh-TW"] = "第 {0} / {1} 頁",
                ["fr"] = "Page {0} / {1}",
                ["de"] = "Seite {0} / {1}",
                ["it"] = "Pagina {0} / {1}",
                ["ja"] = "ページ {0} / {1}",
                ["ko"] = "페이지 {0} / {1}",
                ["pl"] = "Strona {0} / {1}",
                ["pt-BR"] = "Página {0} / {1}",
                ["ru"] = "Страница {0} / {1}",
                ["es-MX"] = "Página {0} / {1}",
                ["es-ES"] = "Página {0} / {1}",
                ["sv"] = "Sida {0} / {1}",
                ["tr"] = "Sayfa {0} / {1}",
                ["uk"] = "Сторінка {0} / {1}",
            },
            // Gendered to match each language's word for "skin".
            [Default] = new Dictionary<string, string>
            {
                ["en"] = "Default",
                ["zh-CN"] = "默认",
                ["zh-TW"] = "預設",
                ["fr"] = "Normale",
                ["de"] = "Standard",
                ["it"] = "Predefinita",
                ["ja"] = "ノーマル",
                ["ko"] = "기본",
                ["pl"] = "Domyślna",
                ["pt-BR"] = "Padrão",
                ["ru"] = "Стандартный",
                ["es-MX"] = "Predeterminado",
                ["es-ES"] = "Por defecto",
                ["sv"] = "Standard",
                ["tr"] = "Varsayılan",
                ["uk"] = "Стандартний",
            },
            [Common] = new Dictionary<string, string>
            {
                ["en"] = "Common",
                ["zh-CN"] = "普通",
                ["zh-TW"] = "普通",
                ["fr"] = "Commune",
                ["de"] = "Gewöhnlich",
                ["it"] = "Comune",
                ["ja"] = "コモン",
                ["ko"] = "일반",
                ["pl"] = "Pospolita",
                ["pt-BR"] = "Comum",
                ["ru"] = "Обычный",
                ["es-MX"] = "Común",
                ["es-ES"] = "Común",
                ["sv"] = "Vanlig",
                ["tr"] = "Sıradan",
                ["uk"] = "Звичайний",
            },
            [Rare] = new Dictionary<string, string>
            {
                ["en"] = "Rare",
                ["zh-CN"] = "稀有",
                ["zh-TW"] = "稀有",
                ["fr"] = "Rare",
                ["de"] = "Selten",
                ["it"] = "Rara",
                ["ja"] = "レア",
                ["ko"] = "희귀",
                ["pl"] = "Rzadka",
                ["pt-BR"] = "Raro",
                ["ru"] = "Редкий",
                ["es-MX"] = "Raro",
                ["es-ES"] = "Raro",
                ["sv"] = "Sällsynt",
                ["tr"] = "Nadir",
                ["uk"] = "Рідкісний",
            },
            [Legendary] = new Dictionary<string, string>
            {
                ["en"] = "Legendary",
                ["zh-CN"] = "传说",
                ["zh-TW"] = "傳說",
                ["fr"] = "Légendaire",
                ["de"] = "Legendär",
                ["it"] = "Leggendaria",
                ["ja"] = "レジェンダリー",
                ["ko"] = "전설",
                ["pl"] = "Legendarna",
                ["pt-BR"] = "Lendário",
                ["ru"] = "Легендарный",
                ["es-MX"] = "Legendario",
                ["es-ES"] = "Legendario",
                ["sv"] = "Legendarisk",
                ["tr"] = "Efsanevi",
                ["uk"] = "Легендарний",
            },
            [Unobtainable] = new Dictionary<string, string>
            {
                ["en"] = "Unobtainable",
                ["zh-CN"] = "无法获得",
                ["zh-TW"] = "無法取得",
                ["fr"] = "Inobtenable",
                ["de"] = "Nicht erhältlich",
                ["it"] = "Non ottenibile",
                ["ja"] = "入手不可",
                ["ko"] = "획득 불가",
                ["pl"] = "Nieosiągalna",
                ["pt-BR"] = "Indisponível",
                ["ru"] = "Недоступный",
                ["es-MX"] = "Inobtenible",
                ["es-ES"] = "Inobtenible",
                ["sv"] = "Ej tillgänglig",
                ["tr"] = "Elde Edilemez",
                ["uk"] = "Недоступний",
            },
            [ShowUnobtainable] = new Dictionary<string, string>
            {
                ["en"] = "Show unobtainable {0}",
                ["zh-CN"] = "显示无法获得 {0}",
                ["zh-TW"] = "顯示無法取得 {0}",
                ["fr"] = "Afficher les inobtenables {0}",
                ["de"] = "Nicht erhältliche anzeigen {0}",
                ["it"] = "Mostra non ottenibili {0}",
                ["ja"] = "入手不可を表示 {0}",
                ["ko"] = "획득 불가 표시 {0}",
                ["pl"] = "Pokaż nieosiągalne {0}",
                ["pt-BR"] = "Mostrar indisponíveis {0}",
                ["ru"] = "Показать недоступные {0}",
                ["es-MX"] = "Mostrar inobtenibles {0}",
                ["es-ES"] = "Mostrar inobtenibles {0}",
                ["sv"] = "Visa ej tillgängliga {0}",
                ["tr"] = "Elde Edilemezleri Göster {0}",
                ["uk"] = "Показати недоступні {0}",
            },
            [HideUnobtainable] = new Dictionary<string, string>
            {
                ["en"] = "Hide unobtainable {0}",
                ["zh-CN"] = "隐藏无法获得 {0}",
                ["zh-TW"] = "隱藏無法取得 {0}",
                ["fr"] = "Masquer les inobtenables {0}",
                ["de"] = "Nicht erhältliche ausblenden {0}",
                ["it"] = "Nascondi non ottenibili {0}",
                ["ja"] = "入手不可を非表示 {0}",
                ["ko"] = "획득 불가 숨기기 {0}",
                ["pl"] = "Ukryj nieosiągalne {0}",
                ["pt-BR"] = "Ocultar indisponíveis {0}",
                ["ru"] = "Скрыть недоступные {0}",
                ["es-MX"] = "Ocultar inobtenibles {0}",
                ["es-ES"] = "Ocultar inobtenibles {0}",
                ["sv"] = "Dölj ej tillgängliga {0}",
                ["tr"] = "Elde Edilemezleri Gizle {0}",
                ["uk"] = "Сховати недоступні {0}",
            },
            [HintPress] = new Dictionary<string, string>
            {
                ["en"] = "Press {0} to see which skins you have and which are still missing.",
                ["zh-CN"] = "按{0}查看你已拥有和尚未获得的皮肤。",
                ["zh-TW"] = "按下 {0} 查看你已擁有和尚未取得的外觀。",
                ["fr"] = "Appuyez sur {0} pour voir les apparences que vous possédez et celles qui vous manquent.",
                ["de"] = "Drücke {0}, um zu sehen, welche Skins du hast und welche dir noch fehlen.",
                ["it"] = "Premi {0} per vedere quali skin hai e quali ti mancano ancora.",
                ["ja"] = "{0}を押すと、持っているスキンとまだ持っていないスキンを確認できる",
                ["ko"] = "{0} 누르기로 보유한 스킨과 아직 없는 스킨을 확인할 수 있습니다",
                ["pl"] = "Naciśnij {0}, aby zobaczyć, które skórki już masz, a których ci jeszcze brakuje.",
                ["pt-BR"] = "Aperte {0} para ver quais visuais você tem e quais ainda faltam.",
                ["ru"] = "Нажмите {0}, чтобы увидеть, какие образы у вас есть, а каких ещё не хватает.",
                ["es-MX"] = "Presiona {0} para ver qué aspectos tienes y cuáles te faltan.",
                ["es-ES"] = "Pulsa {0} para ver qué diseños tienes y cuáles te faltan.",
                ["sv"] = "Tryck på {0} för att se vilka utseenden du har och vilka du fortfarande saknar.",
                ["tr"] = "Hangi görünümlere sahip olduğunu ve hangilerinin eksik olduğunu görmek için {0} tuşuna bas.",
                ["uk"] = "Натисніть {0}, щоб побачити, які образи у вас є, а яких ще бракує.",
            },
            [HintHold] = new Dictionary<string, string>
            {
                ["en"] = "Hold {0} to see which skins you have and which are still missing.",
                ["zh-CN"] = "长按{0}查看你已拥有和尚未获得的皮肤。",
                ["zh-TW"] = "長按 {0} 查看你已擁有和尚未取得的外觀。",
                ["fr"] = "Maintenez {0} pour voir les apparences que vous possédez et celles qui vous manquent.",
                ["de"] = "Halte {0} gedrückt, um zu sehen, welche Skins du hast und welche dir noch fehlen.",
                ["it"] = "Tieni premuto {0} per vedere quali skin hai e quali ti mancano ancora.",
                ["ja"] = "{0}を長押しすると、持っているスキンとまだ持っていないスキンを確認できる",
                ["ko"] = "{0} 길게 누르기로 보유한 스킨과 아직 없는 스킨을 확인할 수 있습니다",
                ["pl"] = "Przytrzymaj {0}, aby zobaczyć, które skórki już masz, a których ci jeszcze brakuje.",
                ["pt-BR"] = "Segure {0} para ver quais visuais você tem e quais ainda faltam.",
                ["ru"] = "Удерживайте {0}, чтобы увидеть, какие образы у вас есть, а каких ещё не хватает.",
                ["es-MX"] = "Mantén presionado {0} para ver qué aspectos tienes y cuáles te faltan.",
                ["es-ES"] = "Mantén {0} para ver qué diseños tienes y cuáles te faltan.",
                ["sv"] = "Håll ner {0} för att se vilka utseenden du har och vilka du fortfarande saknar.",
                ["tr"] = "Hangi görünümlere sahip olduğunu ve hangilerinin eksik olduğunu görmek için {0} tuşunu basılı tut.",
                ["uk"] = "Утримуйте {0}, щоб побачити, які образи у вас є, а яких ще бракує.",
            },
            // {0} is the translated Title.
            [HintUnbound] = new Dictionary<string, string>
            {
                ["en"] = "Pick a button for \"{0}\" in the controls settings to see which skins you have.",
                ["zh-CN"] = "在按键设置中为“{0}”分配一个按键，即可查看你拥有的皮肤。",
                ["zh-TW"] = "在按鍵設定中為「{0}」指定一個按鍵，即可查看你擁有的外觀。",
                ["fr"] = "Choisissez une touche pour « {0} » dans les paramètres des commandes pour voir vos apparences.",
                ["de"] = "Lege in den Steuerungseinstellungen eine Taste für „{0}“ fest, um deine Skins zu sehen.",
                ["it"] = "Assegna un tasto a \"{0}\" nelle impostazioni dei comandi per vedere le tue skin.",
                ["ja"] = "操作設定で「{0}」にボタンを割り当てると、スキンを確認できる",
                ["ko"] = "조작 설정에서 '{0}'에 버튼을 지정하면 보유한 스킨을 확인할 수 있습니다.",
                ["pl"] = "Przypisz przycisk do „{0}” w ustawieniach sterowania, aby zobaczyć swoje skórki.",
                ["pt-BR"] = "Defina um botão para \"{0}\" nas configurações de controles para ver seus visuais.",
                ["ru"] = "Назначьте кнопку для «{0}» в настройках управления, чтобы увидеть свои образы.",
                ["es-MX"] = "Asigna un botón a \"{0}\" en la configuración de controles para ver tus aspectos.",
                ["es-ES"] = "Asigna un botón a «{0}» en los ajustes de controles para ver tus diseños.",
                ["sv"] = "Välj en knapp för \"{0}\" i kontrollinställningarna för att se dina utseenden.",
                ["tr"] = "Görünümlerini görmek için kontrol ayarlarından \"{0}\" için bir tuş ata.",
                ["uk"] = "Призначте кнопку для «{0}» у налаштуваннях керування, щоб побачити свої образи.",
            },
            // Where a skin can be won. "Island" is the game's own menu label; {0} is one island number...
            [Island] = new Dictionary<string, string>
            {
                ["en"] = "Island {0}",
                ["zh-CN"] = "{0}号岛",
                ["zh-TW"] = "{0}號島",
                ["fr"] = "Île {0}",
                ["de"] = "Insel {0}",
                ["it"] = "Isola {0}",
                ["ja"] = "島{0}",
                ["ko"] = "섬 {0}",
                ["pl"] = "Wyspa {0}",
                ["pt-BR"] = "Ilha {0}",
                ["ru"] = "Остров {0}",
                ["es-MX"] = "Isla {0}",
                ["es-ES"] = "Isla {0}",
                ["sv"] = "Ö {0}",
                ["tr"] = "Ada {0}",
                ["uk"] = "Острів {0}",
            },
            // ...or several, joined with ListSeparator ("Islands 2, 3").
            [Islands] = new Dictionary<string, string>
            {
                ["en"] = "Islands {0}",
                ["zh-CN"] = "{0}号岛",
                ["zh-TW"] = "{0}號島",
                ["fr"] = "Îles {0}",
                ["de"] = "Inseln {0}",
                ["it"] = "Isole {0}",
                ["ja"] = "島{0}",
                ["ko"] = "섬 {0}",
                ["pl"] = "Wyspy {0}",
                ["pt-BR"] = "Ilhas {0}",
                ["ru"] = "Острова {0}",
                ["es-MX"] = "Islas {0}",
                ["es-ES"] = "Islas {0}",
                ["sv"] = "Öar {0}",
                ["tr"] = "Adalar {0}",
                ["uk"] = "Острови {0}",
            },
            // The small island that sells the Bow (the game's own word for it); its machine only gives Bow skins.
            [BowIsland] = new Dictionary<string, string>
            {
                ["en"] = "Bow island",
                ["zh-CN"] = "弓之岛",
                ["zh-TW"] = "弓之島",
                ["fr"] = "Île de l’arc",
                ["de"] = "Bogeninsel",
                ["it"] = "Isola dell’arco",
                ["ja"] = "弓の島",
                ["ko"] = "활의 섬",
                ["pl"] = "Wyspa z łukiem",
                ["pt-BR"] = "Ilha do arco",
                ["ru"] = "Остров с луком",
                ["es-MX"] = "Isla del arco",
                ["es-ES"] = "Isla del arco",
                ["sv"] = "Pilbågsön",
                ["tr"] = "Yay adası",
                ["uk"] = "Острів з луком",
            },
            // Other languages fall back to the English comma.
            [ListSeparator] = new Dictionary<string, string>
            {
                ["en"] = ", ",
                ["zh-CN"] = "、",
                ["zh-TW"] = "、",
                ["ja"] = "・",
            },
        };

        /// <summary>The text for the game's current language, falling back to the base language, then English.</summary>
        public static string Get(string key, params object[] args)
        {
            EnsureSubscribed();
            if (!Table.TryGetValue(key, out var texts))
                return key;

            string text = null;
            string code = CurrentCode();
            if (code != null && !texts.TryGetValue(code, out text))
            {
                // e.g. "es" or "es-AR" → first entry for that language
                string language = code.Split('-')[0];
                foreach (var pair in texts)
                {
                    if (pair.Key.Split('-')[0].Equals(language, StringComparison.OrdinalIgnoreCase))
                    {
                        text = pair.Value;
                        break;
                    }
                }
            }
            if (text == null)
                text = texts["en"];
            return args.Length > 0 ? string.Format(text, args) : text;
        }

        private static string CurrentCode()
        {
            try
            {
                var code = LocalizationSettings.SelectedLocale?.Identifier.Code;
                if (string.IsNullOrEmpty(code))
                    return null;
                // Match the table's casing ("zh-cn" → "zh-CN").
                var parts = code.Split('-');
                return parts.Length == 2 ? parts[0].ToLowerInvariant() + "-" + parts[1].ToUpperInvariant() : code.ToLowerInvariant();
            }
            catch (Exception)
            {
                return null; // Localization not initialized yet
            }
        }

        private static void EnsureSubscribed()
        {
            if (_subscribed)
                return;
            try
            {
                LocalizationSettings.SelectedLocaleChanged += _ => LanguageChanged?.Invoke();
                _subscribed = true;
            }
            catch (Exception)
            {
                // Try again on the next lookup.
            }
        }
    }
}
