<!-- AI-assisted: written with Claude. See docs/AI_USE.md -->
# Тексти ролі 04 → для ролі 05

Роль 05, додай, будь ласка, ці ключі в `Resources/Data/Text/texts_en.json` і `texts_uk.json`.

**Оновлення (вибір ракети, набори модулів, підказки):** повний і свіжий список текстів ролі 04 лежить у
`Resources/Data/Launch/texts_launch_en.json` і `texts_launch_uk.json` (той самий формат `{"items":[...]}`).
Екран запуску читає їх сам через `LaunchText`, якщо ключа немає в твоїх файлах, тож нічого не зламано.
Але **звіт дня** (`report.kit_building`, `report.kit_unpacked`) і планшет читають тільки `Loc` —
ці ключі треба перенести у твої файли. Можна просто скопіювати весь `items` з тих двох файлів.

## texts_en.json
```json
{ "key": "cargo.water", "text": "Water tanks" },
{ "key": "cargo.food", "text": "Food boxes" },
{ "key": "cargo.materials", "text": "Building materials" },
{ "key": "cargo.medkits", "text": "Med kit" },
{ "key": "cargo.solar_panel", "text": "Solar panel kit" },
{ "key": "cargo.battery", "text": "Battery kit" },
{ "key": "cargo.crew_seat", "text": "Crew seat" },
{ "key": "launch.title_first", "text": "FIRST MISSION: PACK THE SHIP" },
{ "key": "launch.title_supply", "text": "SUPPLY ORDER" },
{ "key": "launch.cargo", "text": "CARGO" },
{ "key": "launch.ship", "text": "SHIP" },
{ "key": "launch.fuel", "text": "FUEL" },
{ "key": "launch.kg", "text": "{0} kg" },
{ "key": "launch.fuel_value", "text": "Fuel: {0} kg · needed: {1} kg" },
{ "key": "launch.seats", "text": "Crew seats: {0} × {1} kg" },
{ "key": "launch.mass", "text": "Ship mass: {0} kg (cargo {1} kg). Heavier ship needs more fuel." },
{ "key": "launch.risk", "text": "Risk: {0}%" },
{ "key": "launch.link_ok", "text": "LINK OK" },
{ "key": "launch.link_off", "text": "NO LINK" },
{ "key": "launch.legend_crew", "text": "CREW" },
{ "key": "launch.legend_cargo", "text": "CARGO" },
{ "key": "launch.hint.Green", "text": "Enough fuel. The flight is safe." },
{ "key": "launch.hint.Yellow", "text": "Just enough fuel. It may fail!" },
{ "key": "launch.hint.Red", "text": "Not enough fuel. The ship will turn back." },
{ "key": "launch.go", "text": "LAUNCH" },
{ "key": "launch.send", "text": "SEND ORDER" },
{ "key": "launch.flying", "text": "Flying to the Moon..." },
{ "key": "launch.turning_back", "text": "Out of fuel! Turning back to Earth." },
{ "key": "launch.success", "text": "Landed on the Moon!" },
{ "key": "launch.failed_repack", "text": "The ship came back. Take less cargo or add fuel, then try again." },
{ "key": "launch.must_launch", "text": "First, fly the crew to the Moon." },
{ "key": "launch.order_sent", "text": "Order sent to Earth. The ship starts on schedule." },
{ "key": "launch.status.Draft", "text": "DRAFT" },
{ "key": "launch.status.Sent", "text": "SENT · WAITING FOR LAUNCH" },
{ "key": "launch.status.Launched", "text": "FLYING" },
{ "key": "launch.status.Returned", "text": "CAME BACK · 3 DAYS LATE" },
{ "key": "error.comms_closed", "text": "No contact with Earth now. Wait for the next comms window." },
{ "key": "error.order_already_sent", "text": "The order is already sent." },
{ "key": "error.order_locked", "text": "The ship is already flying. Change the next order." },
{ "key": "error.unknown_cargo", "text": "Unknown cargo." },
{ "key": "error.not_found", "text": "Not found." },
{ "key": "crew.err_severe", "text": "Very sick. Can only rest in the med bay." },
{ "key": "crew.err_no_place", "text": "There is no place for this job yet." },
{ "key": "crew.err_no_reserve", "text": "Nobody with this job is waiting on Earth." },
{ "key": "crew.err_already_requested", "text": "A replacement is already on the list." },
{ "key": "report.comms_no_power", "text": "The antenna has no power. No contact with Earth." },
{ "key": "report.supply_not_sent", "text": "The supply ship is waiting: the order was not sent." },
{ "key": "report.supply_launched", "text": "The supply ship took off from Earth." },
{ "key": "report.supply_returned", "text": "Not enough fuel! The supply ship turned back. It comes {0} days later." },
{ "key": "report.supply_arrived", "text": "The supply ship landed on the pad." },
{ "key": "report.went_home", "text": "{0} flew home to Earth." },
{ "key": "report.crew_arrived", "text": "{0} arrived from Earth." },
{ "key": "report.ice_mined", "text": "Water from mined ice" },
{ "key": "report.ice_empty", "text": "No more ice in crater {0}." },
{ "key": "report.samples_empty", "text": "No more samples at {0}." },
{ "key": "report.science_gained", "text": "Science from samples and the lab" }
```

## texts_uk.json
```json
{ "key": "cargo.water", "text": "Баки з водою" },
{ "key": "cargo.food", "text": "Ящики з їжею" },
{ "key": "cargo.materials", "text": "Будматеріали" },
{ "key": "cargo.medkits", "text": "Аптечка" },
{ "key": "cargo.solar_panel", "text": "Набір сонячної панелі" },
{ "key": "cargo.battery", "text": "Набір батареї" },
{ "key": "cargo.crew_seat", "text": "Місце для людини" },
{ "key": "launch.title_first", "text": "ПЕРША МІСІЯ: ПАКУЄМО КОРАБЕЛЬ" },
{ "key": "launch.title_supply", "text": "ЗАМОВЛЕННЯ ПОСТАВКИ" },
{ "key": "launch.cargo", "text": "ВАНТАЖ" },
{ "key": "launch.ship", "text": "КОРАБЕЛЬ" },
{ "key": "launch.fuel", "text": "ПАЛИВО" },
{ "key": "launch.kg", "text": "{0} кг" },
{ "key": "launch.fuel_value", "text": "Паливо: {0} кг · треба: {1} кг" },
{ "key": "launch.seats", "text": "Місця екіпажу: {0} × {1} кг" },
{ "key": "launch.mass", "text": "Маса корабля: {0} кг (вантаж {1} кг). Важчому кораблю треба більше палива." },
{ "key": "launch.risk", "text": "Ризик: {0}%" },
{ "key": "launch.link_ok", "text": "ЗВ'ЯЗОК Є" },
{ "key": "launch.link_off", "text": "НЕМАЄ ЗВ'ЯЗКУ" },
{ "key": "launch.legend_crew", "text": "ЕКІПАЖ" },
{ "key": "launch.legend_cargo", "text": "ВАНТАЖ" },
{ "key": "launch.hint.Green", "text": "Палива із запасом. Політ безпечний." },
{ "key": "launch.hint.Yellow", "text": "Палива впритул. Може не вийти!" },
{ "key": "launch.hint.Red", "text": "Палива не вистачить. Корабель повернеться." },
{ "key": "launch.go", "text": "ЗАПУСК" },
{ "key": "launch.send", "text": "ВІДПРАВИТИ" },
{ "key": "launch.flying", "text": "Летимо до Місяця..." },
{ "key": "launch.turning_back", "text": "Паливо скінчилось! Повертаємось на Землю." },
{ "key": "launch.success", "text": "Сіли на Місяць!" },
{ "key": "launch.failed_repack", "text": "Корабель повернувся. Візьми менше вантажу або більше палива і спробуй ще." },
{ "key": "launch.must_launch", "text": "Спершу доправ екіпаж на Місяць." },
{ "key": "launch.order_sent", "text": "Замовлення на Землі. Корабель стартує за розкладом." },
{ "key": "launch.status.Draft", "text": "ЧЕРНЕТКА" },
{ "key": "launch.status.Sent", "text": "ВІДПРАВЛЕНО · ЧЕКАЄ СТАРТУ" },
{ "key": "launch.status.Launched", "text": "ЛЕТИТЬ" },
{ "key": "launch.status.Returned", "text": "ПОВЕРНУВСЯ · НА 3 ДНІ ПІЗНІШЕ" },
{ "key": "error.comms_closed", "text": "Зараз немає зв'язку із Землею. Дочекайся вікна зв'язку." },
{ "key": "error.order_already_sent", "text": "Замовлення вже відправлене." },
{ "key": "error.order_locked", "text": "Корабель уже летить. Зміни наступне замовлення." },
{ "key": "error.unknown_cargo", "text": "Невідомий вантаж." },
{ "key": "error.not_found", "text": "Не знайдено." },
{ "key": "crew.err_severe", "text": "Тяжко хворіє. Може тільки лежати в медмодулі." },
{ "key": "crew.err_no_place", "text": "Для цієї роботи ще немає місця." },
{ "key": "crew.err_no_reserve", "text": "На Землі немає людини з такою професією." },
{ "key": "crew.err_already_requested", "text": "Заміна вже в списку." },
{ "key": "report.comms_no_power", "text": "Антена без енергії. Немає зв'язку із Землею." },
{ "key": "report.supply_not_sent", "text": "Корабель поставки чекає: замовлення не відправлене." },
{ "key": "report.supply_launched", "text": "Корабель поставки стартував із Землі." },
{ "key": "report.supply_returned", "text": "Не вистачило палива! Поставка повернулась і прийде на {0} дні пізніше." },
{ "key": "report.supply_arrived", "text": "Поставка сіла на посадковий майданчик." },
{ "key": "report.went_home", "text": "{0} полетів(-ла) додому на Землю." },
{ "key": "report.crew_arrived", "text": "{0} прилетів(-ла) із Землі." },
{ "key": "report.ice_mined", "text": "Вода з видобутого льоду" },
{ "key": "report.ice_empty", "text": "У кратері {0} лід скінчився." },
{ "key": "report.samples_empty", "text": "На точці {0} зразки скінчились." },
{ "key": "report.science_gained", "text": "Наука зі зразків і лабораторії" }
```

## Для 03
Набори модулів (`kit_greenhouse`, `kit_solar`...) після посадки дають будматеріали (ціна модуля з `modules.json`)
і одразу викликають `ConstructionSystem.Order(type, slot)` у вільному квадратику поруч із базою.
Якщо модуль ще не відкритий розділом або немає роботів — будматеріали просто лишаються на складі.
`SolarPanelKits` і `BatteryKits` більше не наповнюються (поля лишились, щоб не ламати збереження).

## Вкладка «ПОСТАВКИ» (оновлення від 04)
Поки в `Tablet.uxml` лежить заглушка `pane-supply-stub`, екран запуску (`LaunchScreen`, роль 04) сам ховає її
і ставить у `pane-supply` свій вміст (`Scripts/Logistics/SupplyTabView.cs`): коли наступна поставка, 5 кроків
(`step.*` — твої тексти), що в замовленні, порада і кнопка «ПАКУВАТИ КОРАБЕЛЬ →».
Твої файли при цьому не змінюються. Якщо зробиш свою вкладку — просто прибери заглушку `pane-supply-stub`,
і роль 04 перестане туди щось додавати. Відкрити замовлення зі свого коду: `LaunchScreen.Instance?.OpenSupplyOrder();`

## API для 05 (планшет)
- `CrewSystem.Assign(state, astronaut, task)` — сама знайде місце; або `Assign(astronaut, task, targetId)`.
- `CrewSystem.RecallAllInside(state)` — кнопка «Всі в укриття».
- `CrewSystem.RequestReplacement(state, astronaut)` / `CancelReplacement(state, name)`.
- `SupplySystem.ChangeCargo / SetFuel / Evaluate / Send`, стан у `state.Supply` (`Status`, `NextLaunchAt`, `ArrivalAt`).
- `CommsSystem.IsOpen(state)`, `UpcomingWindows(state, 5)`, `HoursUntilOpen(state)`.
- `LaunchScreen.Open()` — відкрити екран запуску.

## Погода на Землі (нові ключі звіту дня від 04)
Звіт дня читає тільки `Loc`, тому додай ці два ключі у свої `texts_en.json` / `texts_uk.json`
(`{0}` — кількість годин, приходить у `Delta`):
```
{ "key": "report.supply_storm", "text": "Thunderstorm at the launch site on Earth! The supply ship will start {0} hours later." },
{ "key": "report.supply_weather_ok", "text": "It was windy on Earth, but the supply ship took off." },
{ "key": "report.supply_storm", "text": "Гроза на космодромі на Землі! Корабель поставки стартує на {0} год пізніше." },
{ "key": "report.supply_weather_ok", "text": "На Землі був вітер, але корабель поставки злетів." },
```
