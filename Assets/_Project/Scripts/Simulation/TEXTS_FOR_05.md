<!-- AI-assisted: written with Claude. See docs/AI_USE.md -->
# Тексти ролі 03 → для ролі 05

Роль 05, додай, будь ласка, ці ключі в `Resources/Data/Text/texts_en.json` і `texts_uk.json`.
Без них у планшеті й вечірньому звіті будуть видні `#ключі`.

Ключі `error.place_outside`, `error.place_taken`, `error.unknown_module`, `error.no_materials`,
`report.module_built` і `chapter.01.title` у тебе вже є: `ConstructionSystem.Order` тепер повертає саме їх.

Що підставляється в `{0}`:
- `report.oxygen_low.*`, `report.water_low` — відсотки (наприклад `18`);
- `report.hungry` — ім'я астронавта;
- `report.module_shielded` — тип модуля (`Habitat`, `Solar`...);
- `report.chapter_completed` — номер розділу.

Число зміни (`Delta`) у рядку звіту окреме: його показує сам звіт, у тексті його немає.

## texts_en.json
```json
{ "key": "build.err.locked", "text": "This module opens in a later chapter." },
{ "key": "build.err.no_robots", "text": "There are no robots to build it." },
{ "key": "build.err.no_module", "text": "There is no module here." },
{ "key": "build.err.not_built", "text": "Finish building this module first." },
{ "key": "build.err.already_shielded", "text": "This module is already covered with regolith (moon dust)." },
{ "key": "build.err.shield_in_progress", "text": "The robots are already covering this module." },
{ "key": "report.battery_change", "text": "Battery charge changed today." },
{ "key": "report.power_short.life_support", "text": "Life support got less power than you planned. Less oxygen was made." },
{ "key": "report.power_short.shelter", "text": "The shelter got less power than you planned." },
{ "key": "report.power_short.medbay", "text": "The medbay got less power than you planned." },
{ "key": "report.power_short.comms", "text": "The antenna got less power than you planned." },
{ "key": "report.power_short.greenhouse", "text": "The greenhouse got less power than you planned. Plants grew slower." },
{ "key": "report.power_short.robots", "text": "The robots got less power than you planned. Building was slower." },
{ "key": "report.power_short.lab", "text": "The lab got less power than you planned." },
{ "key": "report.oxygen_made", "text": "Life support made oxygen from water." },
{ "key": "report.oxygen_used", "text": "The crew breathed oxygen." },
{ "key": "report.water_used_lifesupport", "text": "Life support used water to make oxygen." },
{ "key": "report.oxygen_low.no_power", "text": "Oxygen is low ({0}%): life support has no power. Give it power blocks." },
{ "key": "report.oxygen_low.no_water", "text": "Oxygen is low ({0}%): there is not enough water to make it. Find ice." },
{ "key": "report.oxygen_low.crew_uses_more", "text": "Oxygen is low ({0}%): the crew breathes more than life support makes. Give it more power." },
{ "key": "report.oxygen_low.refilling", "text": "Oxygen is low ({0}%), but it is filling up again." },
{ "key": "report.oxygen_empty", "text": "Oxygen ran out!" },
{ "key": "report.water_low", "text": "Water is low ({0}%). Send the geologist to mine ice." },
{ "key": "report.greenhouse_no_module", "text": "The greenhouse got power, but there is no greenhouse module yet." },
{ "key": "report.greenhouse_no_water", "text": "The plants did not get enough water." },
{ "key": "report.harvest", "text": "Harvest! The greenhouse gave food." },
{ "key": "report.food_eaten", "text": "The crew ate food." },
{ "key": "report.food_empty", "text": "There is no food left! Grow it or order it from Earth." },
{ "key": "report.hungry", "text": "{0} is hungry." },
{ "key": "report.robots_no_power", "text": "Some robots had no power and could not build." },
{ "key": "report.module_shielded", "text": "Module {0} is now covered with regolith (moon dust). Less radiation inside." },
{ "key": "report.chapter_completed", "text": "Chapter {0} complete! Great job, chief engineer!" },
{ "key": "gameover.no_oxygen", "text": "The oxygen ran out. The crew flies back to Earth. Try the chapter again!" },
{ "key": "gameover.no_food", "text": "The crew had no food for 2 days. They fly back to Earth. Try the chapter again!" },
{ "key": "chapter.01.description", "text": "Land at the south pole, build a home and more solar panels." },
{ "key": "chapter.02.title", "text": "Ice and Greens" },
{ "key": "chapter.02.description", "text": "Find ice for water, grow food in a greenhouse and start doing science." },
{ "key": "chapter.03.title", "text": "Solar Storm" },
{ "key": "chapter.03.description", "text": "Storms from the Sun are coming. Build a shelter and a medbay to keep the crew safe." },
{ "key": "goal.survive_days", "text": "Live on the Moon for {0} days" },
{ "key": "goal.build_module", "text": "Build: {0}" },
{ "key": "goal.water", "text": "Store {0} water" },
{ "key": "goal.science", "text": "Get {0} science points" },
```

## texts_uk.json
```json
{ "key": "build.err.locked", "text": "Цей модуль відкриється в наступному розділі." },
{ "key": "build.err.no_robots", "text": "Немає роботів, щоб будувати." },
{ "key": "build.err.no_module", "text": "Тут немає модуля." },
{ "key": "build.err.not_built", "text": "Спершу добудуй цей модуль." },
{ "key": "build.err.already_shielded", "text": "Цей модуль уже засипаний реголітом (місячним пилом)." },
{ "key": "build.err.shield_in_progress", "text": "Роботи вже засипають цей модуль." },
{ "key": "report.battery_change", "text": "Заряд батареї за день змінився." },
{ "key": "report.power_short.life_support", "text": "Життєзабезпечення отримало менше енергії, ніж ти дав. Кисню зроблено менше." },
{ "key": "report.power_short.shelter", "text": "Укриття отримало менше енергії, ніж ти дав." },
{ "key": "report.power_short.medbay", "text": "Медмодуль отримав менше енергії, ніж ти дав." },
{ "key": "report.power_short.comms", "text": "Антена отримала менше енергії, ніж ти дав." },
{ "key": "report.power_short.greenhouse", "text": "Теплиця отримала менше енергії, ніж ти дав. Рослини росли повільніше." },
{ "key": "report.power_short.robots", "text": "Роботи отримали менше енергії, ніж ти дав. Будували повільніше." },
{ "key": "report.power_short.lab", "text": "Лабораторія отримала менше енергії, ніж ти дав." },
{ "key": "report.oxygen_made", "text": "Життєзабезпечення зробило кисень з води." },
{ "key": "report.oxygen_used", "text": "Екіпаж дихав киснем." },
{ "key": "report.water_used_lifesupport", "text": "Життєзабезпечення витратило воду, щоб зробити кисень." },
{ "key": "report.oxygen_low.no_power", "text": "Кисню мало ({0}%): життєзабезпечення без енергії. Дай йому блоки енергії." },
{ "key": "report.oxygen_low.no_water", "text": "Кисню мало ({0}%): бракує води, щоб його робити. Знайди лід." },
{ "key": "report.oxygen_low.crew_uses_more", "text": "Кисню мало ({0}%): екіпаж дихає більше, ніж робить життєзабезпечення. Дай йому більше енергії." },
{ "key": "report.oxygen_low.refilling", "text": "Кисню мало ({0}%), але він знову набирається." },
{ "key": "report.oxygen_empty", "text": "Кисень закінчився!" },
{ "key": "report.water_low", "text": "Води мало ({0}%). Відправ геолога добувати лід." },
{ "key": "report.greenhouse_no_module", "text": "Теплиці дали енергію, але модуля теплиці ще немає." },
{ "key": "report.greenhouse_no_water", "text": "Рослинам забракло води." },
{ "key": "report.harvest", "text": "Урожай! Теплиця дала їжу." },
{ "key": "report.food_eaten", "text": "Екіпаж поїв." },
{ "key": "report.food_empty", "text": "Їжа закінчилась! Вирощуй її або замов із Землі." },
{ "key": "report.hungry", "text": "{0} голодний." },
{ "key": "report.robots_no_power", "text": "Деяким роботам забракло енергії, і вони не будували." },
{ "key": "report.module_shielded", "text": "Модуль {0} засипано реголітом (місячним пилом). Всередині менше радіації." },
{ "key": "report.chapter_completed", "text": "Розділ {0} пройдено! Чудова робота, головний інженере!" },
{ "key": "gameover.no_oxygen", "text": "Кисень закінчився. Екіпаж летить на Землю. Спробуй пройти розділ ще раз!" },
{ "key": "gameover.no_food", "text": "Екіпаж 2 дні був без їжі. Він летить на Землю. Спробуй пройти розділ ще раз!" },
{ "key": "chapter.01.description", "text": "Висадись на південному полюсі, збудуй житло й більше сонячних панелей." },
{ "key": "chapter.02.title", "text": "Лід і зелень" },
{ "key": "chapter.02.description", "text": "Знайди лід для води, вирости їжу в теплиці й почни наукову роботу." },
{ "key": "chapter.03.title", "text": "Сонячний шторм" },
{ "key": "chapter.03.description", "text": "Наближаються бурі від Сонця. Збудуй укриття й медмодуль, щоб уберегти екіпаж." },
{ "key": "goal.survive_days", "text": "Прожити на Місяці {0} днів" },
{ "key": "goal.build_module", "text": "Збудувати: {0}" },
{ "key": "goal.water", "text": "Запасти {0} води" },
{ "key": "goal.science", "text": "Набрати {0} очок науки" },
```
