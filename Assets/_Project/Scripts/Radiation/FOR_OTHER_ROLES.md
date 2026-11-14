<!-- AI-assisted: written with Claude. See docs/AI_USE.md -->
# Роль 06 → іншим ролям

Що зробила роль 06 і що потрібно від вас. Сама 06 ваших файлів не міняла.

## 05 Інтерфейс: тексти EN/UK

Додай у `Resources/Data/Text/texts_en.json` і `texts_uk.json`. У звіті `{0}` — це `ReportLine.Arg` (ім'я або id події), число — `ReportLine.Delta`.

| key | EN | UK |
|---|---|---|
| report.dose_day | {0}: radiation dose today | {0}: доза радіації за день |
| report.dose_why_outside | {0} was outside a lot. Outside nothing stops the Sun's rays. | {0} багато був надворі. Надворі ніщо не зупиняє промені Сонця. |
| report.dose_why_inside | {0} was inside. Walls stop part of the rays. | {0} був усередині. Стіни затримують частину променів. |
| report.dose_why_shielded | {0} was in a module covered with regolith (Moon soil): a small dose. | {0} був у модулі, засипаному реголітом (місячним ґрунтом): доза мала. |
| report.dose_why_storm_outside | {0} was outside in a solar storm: a big dose! | {0} був надворі під час сонячної бурі: велика доза! |
| report.dose_why_storm_module | {0} hid in a normal module in the storm. A shelter with power is safer. | {0} ховався від бурі у звичайному модулі. Укриття з енергією безпечніше. |
| report.dose_why_storm_shelter | {0} hid in the shelter in the storm. Well done! | {0} сховався від бурі в укритті. Молодець! |
| report.dose_yellow | {0} has a high radiation dose (yellow zone). Only inside jobs now. | {0} має велику дозу радіації (жовта зона). Тепер тільки робота всередині. |
| report.dose_red | {0} reached the radiation limit and flies home with the next supply. Ask for a replacement! | {0} досяг ліміту радіації і полетить додому з наступною поставкою. Замов заміну! |
| report.storm_warning | A solar storm is coming! Get everyone inside and give the shelter power. | Наближається сонячна буря! Заведи всіх усередину і дай укриттю енергію. |
| report.storm_started | The solar storm has started! | Сонячна буря почалася! |
| report.storm_ended | The solar storm is over. It is safe outside again. | Сонячна буря минула. Надворі знову безпечно. |
| report.severe | {0} feels very bad and lies in the medbay. Only medicine can help. | {0} дуже погано і лежить у медмодулі. Допоможуть тільки ліки. |
| report.medkit_given | The medic gave {0} medicine. | Медик дав {0} ліки. |
| report.recovered | {0} is healthy again! | {0} знову здоровий! |
| report.waiting_medkits | {0} sick crew wait for medicine. Order med kits with the next supply! | Хворих, що чекають на ліки: {0}. Замов аптечки з наступною поставкою! |
| report.no_medic | {0} sick crew wait: no medic can give medicine. | Хворих, що чекають: {0}. Немає медика, який дасть ліки. |
| error.storm_busy | A storm is already coming. | Буря вже наближається. |
| error.already_sick | Already sick. | Уже хворіє. |
| storm.event | Real event: {0} on {1}. Source: NASA DONKI | Справжня подія: {0}, {1}. Джерело: NASA DONKI |
| storm.shelter_need | The shelter needs {0} power blocks. | Укриттю потрібно {0} блоків енергії. |
| dose.zone.green | Safe | Безпечно |
| dose.zone.yellow | Inside only | Тільки всередині |
| dose.zone.red | Flies home | Летить додому |
| time.compressed | In real life the dose builds up over months. In the game time runs about 50 times faster. | Насправді доза набирається місяцями. У грі час іде приблизно в 50 разів швидше. |
| weather.title | Space weather today | Космічна погода сьогодні |
| weather.live_flare | Latest solar flare: class {0}, {1} (UTC) | Останній сонячний спалах: клас {0}, {1} (UTC) |
| weather.quiet | The Sun is quiet: no flares in the last 7 days. | Сонце спокійне: за 7 днів спалахів не було. |
| weather.archive_event | No connection. A past real event: class {0} flare, {1}. | Немає зв'язку. Справжня подія з минулого: спалах класу {0}, {1}. |

Для `report.dose_day` у `{0}` ім'я, а доза — у `Delta` (у % ліміту). Для `report.storm_*` `{0}` = `Arg` = id події: щоб показати дату й клас, візьми
`GameBootstrap.Instance.GetSystem<RadiationSystem>().FindEvent(line.Arg)` → `classType`, `Day`, `sourceUrl`. Для `report.storm_warning` години = `Delta`.

### Що читати з коду (05)

- Смужка дози: `astronaut.Dose` (0..100+ %), колір: `radiation.Zone(astronaut)` → `DoseZone.Green/Yellow/Red`.
- Попередження: `GameEvents.StormWarning += id => ...`; години: `state.Radiation.HoursUntilStorm(state)`; подія: `radiation.FindEvent(id)` (дата, клас, джерело). Скільки енергії укриттю: `radiation.ShelterPowerNeeded(state)`, чи готове: `radiation.IsShelterReady(state)`.
- Буря почалась: `GameEvents.StormStarted`; іде: `state.Radiation.StormActive`.
- Кнопка «Всі в укриття» → `CrewSystem.RecallAllInside(state)` (04).
- Хворі без ліків: `GetSystem<HealthSystem>().WaitingForMedicine(state)`.
- Екран «Дані NASA»: список джерел — у кінці `docs/NASA_DATA.md`; факти — `NasaFactList.Load().items[i].Text` (мова гри) і `sourceUrl`.
- Головне меню, блок «Space weather today»: постав компонент `SpaceWeatherFetcher` на об'єкт меню, слухай `SpaceWeatherFetcher.Updated`:
  `label.text = Loc.Get(r.TextKey, r.ClassType, r.TimeUtc)`, поруч завжди `r.Badge` (LIVE / ARCHIVE).

## 04 Логістика

- `GoingHome = true` ставить 06, коли доза ≥ 100 %. Забирати людину і привозити заміну — ваша частина (уже працює в `SupplySystem.Deliver`).
- Жовта зона: 06 ставить `InsideOnly = true` і скидає завдання надворі (`Task = None`) у всіх, крім інженера.
- Тяжкий стан: 06 ставить `Task = Medbay`, `TaskTarget` = слот медмодуля; після одужання — `Task = None`.
- Медик «доглядає» хворих, коли в нього `Task = Heal`, медмодуль збудований і `Power.Medbay > 0`.

## 03 Симуляція

- Числа балансу радіації лежать у моїй папці: `Resources/Data/Nasa/radiation_balance.json` (щоб не міняти ваш `balance.json`). Якщо хочете тримати все в `Balance/`, скажіть — перенесу.
- Читаю `Astronaut.Satiety` (< 25 → шанс захворіти ×2), `Power.Shelter`, `Power.Medbay`, `ModuleState.RegolithShield`.

## 01 Техлід

- Сцену 06 не міняв: `RadiationDebug` сам з'являється в сцені `06-Radiation` (через `RuntimeInitializeOnLoadMethod`).
- `Scripts/Radiation/Tools/DonkiCheckTool.cs` — меню **Moon → 06: Перевірити дані DONKI** (увесь файл під `#if UNITY_EDITOR`, у веб-збірку не потрапляє).
- Moon Trek: для текстури поверхні шукай на https://trek.nasa.gov/moon/ «Shackleton» (мозаїки LRO LROC / LOLA південного полюса).
- `SpaceWeatherFetcher` робить запит до `kauai.ccmc.gsfc.nasa.gov`. Перевір у веб-збірці: якщо браузер заблокує (CORS), гра покаже ARCHIVE — це правильна поведінка.
