# Brigade DPS

Мод для The Spell Brigade (Unity IL2CPP, BepInEx 6).

[GitHub](https://github.com/slikfoul/BrigadeDps) · [История изменений](CHANGELOG.md)

## Возможности

- Общий урон, текущий DPS и доля урона каждого игрока.
- DPS, урон и доля урона каждого заклинания с игровыми иконками и названиями.
- Русский или английский интерфейс по языку игры, с использованием игровых шрифтов. Другие языки используют английский интерфейс мода.
- Полные названия заклинаний, инициалы слов или только иконки — настраивается через F5.
- Отдельный раздел босса: урон, DPS и процент от суммарного урона команды по боссу. Обычные враги в этот раздел не попадают.
- Блок босса отделён от общей статистики прозрачной декоративной линией с завитками. Внешняя рамка и фон цельные: у разделителя нет отдельной каменной подложки. Разделитель появляется вместе с блоком босса, прокручивается со строками и учитывается в высоте окна.
- После смерти босса его урон, УВС и проценты показываются ещё 10 секунд по умолчанию, затем блок и разделитель скрываются. Поле F5 «Итоги босса после смерти (с)» принимает 0–3600 секунд: можно ввести число и нажать Enter либо использовать «−/+». Значение 0 скрывает итоги сразу. Отсчёт идёт в реальном времени, включая паузу и экран результатов; скрытие окна не перезапускает его. УВС завершённого боя фиксируется в момент окончания. Настройка индивидуальная для каждого игрока.
- Оригинальная тёмная рамка карточки из игры с приглушёнными линиями, изгибами и завитками. Верх зеркально повторяет низ, без эмблемы и верхних стрелок. Рамка подстраивается под содержимое; игровые изображения подгружаются во время работы и не входят в архив.
- Компактная таблица без верхней панели и нижней строки подсказок.
- Ширина и высота всегда подстраиваются под видимые надписи и строки; небольшой промежуток между столбцами. Ручного изменения размера и значка в углу нет.
- Перемещение окна за верхнюю строку таблицы; положение сохраняется между запусками.
- Направления роста выбираются в F5: вправо/влево и вниз/вверх. Перенос окна не меняет выбранные направления, смена направления не сдвигает окно. При нехватке места окно ограничивается экраном с учётом рамки.
- Окно настроек перемещается за заголовок и сохраняет положение между запусками. Заголовки столбцов выровнены по центру: УВС на русском, DPS и DMG на английском. У сокращений и иконок добавлен небольшой отступ слева.
- Строки «Прочий урон» и «Остальные» не выводятся. Урон без распознанного заклинания остаётся в общей сумме игрока, но отдельной строки не имеет.
- Настройки в игре: отображение, иконки, боссы, результаты, одиночная игра, закрепление, масштаб, прозрачность, высота, число заклинаний, интервал DPS и клавиши.
- Окно статистики видно только в активном забеге и на открытом экране результатов. Главное меню и лобби статистику не показывают.

## Управление

| Действие | По умолчанию |
| --- | --- |
| Показать / скрыть статистику | F3 |
| Строки заклинаний | F4 |
| Открыть настройки | F5 |
| Перенести окно | Тащить верхнюю строку таблицы мышью |
| Освободить захваченный игрой курсор | Удерживать Alt |

Настройки открываются по запросу также в меню. Они сохраняются сразу; перемещение сохраняется при отпускании мыши. Длинный список прокручивается колёсиком, если достигнут предел высоты на экране. «Закрепить окно» блокирует перемещение. Кнопка «Сбросить положение» возвращает начальную позицию и масштаб. Старые настройки ручной ширины/высоты из 0.1.1 больше не используются.

«Названия по первым буквам»: `Крылья зари` → `КЗ`, `Wings of Dawn` → `WOD`. Выключение «Названия заклинаний» оставляет иконки; если игровая иконка недоступна, используются инициалы, чтобы строка оставалась узнаваемой.

## Сетевая игра

Для сбора статистики Brigade DPS необходим **хосту**. Игрокам, желающим видеть окно, нужна та же версия Brigade DPS. Остальные могут играть без мода. Протокол Brigade DPS отдельный: установленный у хоста старый DamageMeter не передаёт данные этому моду.

Начиная с 0.1.2 используется протокол 2 с DPS каждого заклинания. Версии 0.1.2–0.1.10 совместимы по сети; более старые версии нужно обновить. Хост 0.1.8 передаёт отметку времени смерти босса; при более старом хосте отсчёт начинается при получении итогов.

В F5 «УВС команды и заклинаний» переключается между двумя режимами, отдельно для каждого зрителя:

- **За интервал** — прежний режим по умолчанию: урон за последние N секунд / N. В начале забега используется прошедшее время, пока оно меньше N.
- **Как в игре** — значение `SpellAttributes.Dps` из самой игры: `TotalPerformedDamage / (Time.time - StartTime)`. `StartTime` устанавливается при получении заклинания, поэтому у каждого спелла свой период усреднения. Строка игрока показывает сумму игровых УВС всех экипированных заклинаний, включая скрытые лимитом строк. Это сумма показателей спеллов, а не весь урон игрока / время забега. Урон и проценты продолжают показывать данные счётчика мода.

Например, спелл нанёс 6000 урона за 60 секунд с момента получения и 2000 за последние 10 секунд: игровой УВС — 100, наш за интервал — 200. Для нативного режима нужен хост с 0.1.10 или новее; при отсутствии данных показывается «—». УВС босса в обоих режимах остаётся за интервал хоста и фиксируется после смерти босса.

Интервал DPS задаётся хостом (1–60 секунд, по умолчанию 10). Используется внутриигровой таймер забега. Проценты в разделе босса показывают вклад игрока в урон команды, а не долю максимального здоровья босса. Строки заклинаний показывают долю в уроне соответствующего игрока.

Не включать Brigade DPS и girthy_mods DamageMeter одновременно.

## Установка

Архив `slikfoul-BrigadeDps-0.1.10.zip` импортируется в r2modman через Import local mod. Включить BrigadeDps и запускать через Start modded. BepInExPack IL2CPP 6.0.788 указан как зависимость. Старый DamageMeter должен быть отключён.

В профиле с BepInExPack IL2CPP поместить `BrigadeDps.dll` в `BepInEx/plugins/slikfoul-BrigadeDps/` и запускать через Start modded. При обновлении игра должна быть закрыта.

Конфигурация: `BepInEx/config/slikfoul.BrigadeDps.cfg`.

## Сборка и проверки

```powershell
dotnet build src/BrigadeDps.csproj -c Release
dotnet run --project tests/BrigadeDps.Tests.csproj -c Release
pwsh -File ./package.ps1
```

Для сборки нужен .NET SDK 9 и установленная игра с BepInExPack IL2CPP. Сначала запустите игру с BepInEx, чтобы он создал сборки `interop`. По умолчанию ссылки берутся из профиля r2modman `TheSpellBrigade/profiles/Default/BepInEx`. Другой путь передаётся через `-p:BepInExDir="..."`. Проект мода использует .NET 6; автономные проверки — .NET 9 и не требуют игры.

Исходники находятся в `src`, автономные проверки — в `tests`, метаданные и иконка пакета — в `packaging`. Скрипт `package.ps1` создаёт архив для r2modman в `dist`. Игровые сборки, извлечённые изображения, локальные установки и материалы исследования не входят в репозиторий или пакет; интерфейс использует ресурсы установленной игры во время работы.

Для версии 0.1.10 сборка проходит без предупреждений; автономный набор содержит 124 проверок статистики, сетевых данных, локализации, положения окна и таймера итогов босса и переключения режима УВС. Эти проверки не заменяют проверку интерфейса и сетевого матча в самой игре.

## English

Meter and settings use the native dark card artwork with subdued ornamental lines. The upper border mirrors the lower border, without an emblem or upper arrows. Art is loaded from the game at runtime and is not bundled; a fallback frame is shown until the card assets are available. Brigade DPS shows party damage, rolling DPS, per-spell DPS and separate boss contributions. The UI follows the game's Russian/English locale and uses game fonts; other locales use English UI text. Spell names and icons come from the game.

F5 offers two local player/spell DPS modes. **Rolling interval** remains the default (last N seconds of damage / N, using elapsed run time during startup). **Game value** reads native `SpellAttributes.Dps`: damage since the spell was equipped divided by `Time.time - StartTime`. Each spell has its own starting time. The player row sums all equipped spells, even those hidden by the row limit; this is not total player damage divided by run duration. Damage and percentages still use the mod's ledger. Native values require a host on 0.1.10+; unavailable values show a dash. Boss DPS remains rolling in both modes and freezes after defeat.

F3 toggles the meter, F4 toggles spell rows, F5 opens settings. Drag the top row to move the window; hold Alt to release the cursor. The frame automatically fits the visible text and rows with compact column gaps. Long lists scroll at the configured screen-height limit.

Choose horizontal (right/left) and vertical (down/up) growth in F5 settings. Moving the meter preserves these directions; changing a direction keeps the current position. Screen bounds include the frame at the selected scale. Drag the settings title to move that window; its position is saved too. Column headings are centered, using DMG in English and УВС for Russian DPS. Initials and icon-only rows have extra left spacing.

In F5 settings, enable spell-name initials or disable spell names for icons only. Unassigned damage is included in player totals but has no separate row. One continuous outer frame and background encloses both sections. A transparent ornamental divider with completed lower corner arcs separates the boss section from party totals, without a second stone rim; it follows scrolling and is included in automatic sizing. Defeated boss totals and the divider remain visible for 10 seconds by default. The F5 field "Boss results after defeat (s)" accepts 0–3600 seconds with keyboard entry (Enter to apply) or +/- buttons; 0 hides defeated bosses immediately. This is a local display preference. The real-time countdown continues while paused, on the results screen, or with the meter hidden. Spell percentages are relative to their player's total; boss percentages are relative to party boss damage.

Install the ZIP using r2modman's local-mod import and launch with Start modded. The host and meter viewers need versions 0.1.2–0.1.10 (network compatible). Players without the mod can still join. Do not enable the old DamageMeter alongside Brigade DPS.

Build with .NET SDK 9 after launching the game once with BepInExPack IL2CPP to generate its `interop` assemblies. Use `-p:BepInExDir="..."` to override the default r2modman profile path. Run the commands above to build, execute the 124 standalone checks, and generate the local-import ZIP. Game assemblies and artwork are not distributed in this repository.
