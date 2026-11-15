using System.Globalization;
using System.Text;

namespace BrigadeDps;

public static class UiText
{
    private static readonly Dictionary<string, string> English = new() {
        ["BRIGADE DPS · НАСТРОЙКИ"] = "BRIGADE DPS · SETTINGS",
        ["Изменения применяются и сохраняются сразу"] = "Changes apply and save immediately",
        ["Мод включён"] = "Enable mod",
        ["Показывать окно"] = "Show meter",
        ["Строки заклинаний"] = "Show spell rows",
        ["Иконки заклинаний"] = "Show spell icons",
        ["Названия заклинаний"] = "Show spell names",
        ["Названия по первым буквам"] = "Abbreviate spell names",
        ["Статистика босса"] = "Boss statistics",
        ["Итоги босса после смерти (с)"] = "Boss results after defeat (s)",
        ["На экране результатов"] = "Show on results screen",
        ["В одиночной игре"] = "Show in single player",
        ["Закрепить окно"] = "Lock position",
        ["Рост по горизонтали"] = "Horizontal growth",
        ["Рост по вертикали"] = "Vertical growth",
        ["Вправо"] = "Right", ["Влево"] = "Left",
        ["Вниз"] = "Down", ["Вверх"] = "Up",
        ["Масштаб"] = "Scale",
        ["Непрозрачность"] = "Opacity",
        ["Предел высоты"] = "Maximum screen height",
        ["Заклинаний на игрока"] = "Spells per player",
        ["Окно УВС (задаёт хост)"] = "DPS interval (host setting)",
        ["УВС команды и заклинаний"] = "Player and spell DPS",
        ["За интервал"] = "Rolling interval",
        ["Как в игре"] = "Game value",
        ["Показать / скрыть"] = "Toggle meter",
        ["Открыть настройки"] = "Open settings",
        ["Сбросить положение"] = "Reset position",
        ["Готово"] = "Done",
        ["ВКЛ"] = "ON", ["ВЫКЛ"] = "OFF",
        ["Нажми клавишу…"] = "Press a key…",
        ["БОСС · БОЙ"] = "BOSS · FIGHT", ["БОСС · ИТОГ"] = "BOSS · RESULT",
        ["КОМАНДА"] = "PARTY", ["УРОН"] = "DMG", ["УВС"] = "DPS",
        ["Ожидание хоста с Brigade DPS 0.1.2…"] = "Waiting for a host with Brigade DPS 0.1.2…",
        ["Урон ещё не нанесён"] = "No damage dealt yet",
        ["Игрок"] = "Player", ["с"] = "s",
        [Help] = "Drag the table's top row or settings title. Alt releases the cursor.\nGame value: spell DPS since equipped; player = sum of spells.\nBoss DPS always uses the host's rolling interval.\nGame values need host 0.1.10+; unavailable values show —."
    };
    public const string Help = "Таблица двигается за верхнюю строку, настройки — за заголовок.\nКак в игре: УВС с получения спелла; у игрока — сумма спеллов.\nУВС босса всегда за интервал хоста. Alt освобождает курсор.\nИгровые значения требуют хоста 0.1.10+; без данных показано —.";
    public static bool IsRussian(string locale) => locale?.StartsWith("ru", StringComparison.OrdinalIgnoreCase) == true;
    public static bool Has(string key) => key != null && English.ContainsKey(key);
    public static string Translate(string key, string locale) => !IsRussian(locale) && English.TryGetValue(key, out var value) ? value : key;
    public static bool IsSpell(int source) => source >= 0;
    public static string SpellLabel(string name, bool showNames, bool abbreviate)
    {
        if (!showNames) return "";
        if (!abbreviate) return name;
        var result = new StringBuilder();
        bool start = true;
        var chars = StringInfo.GetTextElementEnumerator(name ?? "");
        while (chars.MoveNext())
        {
            string element = chars.GetTextElement();
            if (element is "'" or "’") continue;
            bool word = char.IsLetterOrDigit(element, 0);
            if (word && start) result.Append(element.ToUpperInvariant());
            start = !word;
        }
        return result.ToString();
    }
}
