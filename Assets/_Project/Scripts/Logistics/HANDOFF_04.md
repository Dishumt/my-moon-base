<!-- AI-assisted: written with Claude. See docs/AI_USE.md -->
# Роль 04: що готово і що лишилось зробити (передача справ)

Гілка: `claude/compassionate-lovelace-58ttvi`. Усе нижче — в ній. У Unity це перевіряв тільки Артем (до вкладки планшета включно — ще ні).

## 1. Артему: перевірити і зробити PR
1. Закрий Unity → GitHub Desktop → **Pull origin** → відкрий Unity → Console без червоних помилок.
2. Сцена `Scenes/Sandbox/Earth/04-Earth` → Play → перша місія → «ДАЛІ» → **Tab** → вкладка «ПОСТАВКИ» → «ПАКУВАТИ КОРАБЕЛЬ →» → вантаж → «ВІДПРАВИТИ» → DebugPanel «+1 день» → поставка сіла.
3. Test Runner → EditMode → Run All (нові: RocketTests, MissionPlannerTests, SmallerRocketTests, SupplyScheduleTests).
4. Червона помилка в Console → скопіюй її Claude у новій сесії разом із цим файлом.
5. GitHub Desktop → **Create Pull Request** у `main`. У PR є сцена `.unity` → зливає **Єгор**.
6. Додай у `docs/AI_USE.md`: «04: екран запуску (ракети, вантажі, підказки, майданчик, звіт із зірками), вкладка поставок — код написано з Claude; картинки ракет Art/Launch/Rockets — <чим зроблені>».

## 2. Єгору: поставити в `Main` (5–10 хв)
Найшвидше — скопіювати з моєї сцени:
1. Відкрий `04-Earth`, вибери об'єкт **UI** → на компоненті **Launch Screen** ⋮ → *Copy Component*.
2. У `Main` створи порожній об'єкт **LaunchScreen** → додай **UI Document**: Panel Settings = `Settings/GamePanelSettings`, Source Asset = `UI/Launch/LaunchScreen.uxml`, **Sort Order = 10**.
3. На тому ж об'єкті ⋮ → *Paste Component As New* (прийдуть картинки ракет, іконки вантажів, скафандри). `Open On Start` = ✓ — гра починається з першої місії.
4. Планшет (якщо його ще немає в `Main`): об'єкт **Tablet** → UI Document (той самий Panel Settings, Source Asset = `UI/Tablet/Tablet.uxml`, Sort Order = 0) + компонент **Tablet Controller**.
5. Play у `Main`: екран запуску → ПІДТВЕРДИТИ → майданчик (скафандри беруться з твого `CrewLook`) → звіт → Місяць. **Tab** → «ПОСТАВКИ».

Що приходить у світ через `GameState`: вода/їжа/кисень/аптечки/будматеріали в `Res`; набір модуля одразу викликає `ConstructionSystem.Order` у вільному квадратику біля бази (новий `ModuleState` — роботи їдуть будувати).

## 3. Вані (05)
Вкладку «ПОСТАВКИ» заповнює роль 04, поки є заглушка `pane-supply-stub`; твої файли не змінені. Деталі й тексти — `TEXTS_FOR_05.md`. Звіт дня показує `#report.kit_building` / `#report.kit_unpacked`, доки ці два ключі не перенесені у твої `texts_*.json` (вони є в `Resources/Data/Launch/texts_launch_*.json`).

## 4. Де що лежить (щоб швидко правити)
| Що змінити | Де |
|---|---|
| Ракети: маса, бак, час польоту, висота відносно людей, де люк | `Resources/Data/Launch/rockets.json` |
| Вантажі, маси, набори модулів | `Resources/Data/Launch/cargo_items.json` |
| Період поставок, запас днів, вікна зв'язку | `Resources/Data/Launch/launch.json` |
| Усі тексти екрана EN/UK (підказки, факти, поради) | `Resources/Data/Launch/texts_launch_en.json`, `_uk.json` |
| Розкладка майданчика (вежа, дим, вогонь) | `LaunchPadView.cs` → `Layout` |
| Дні запасів, цілі, порада | `MissionPlanner.cs` |
| Вкладка планшета | `SupplyTabView.cs`, стилі `.sup-*` у `UI/Launch/LaunchScreen.uss` |

## 5. Ідеї, які ще не зроблені
- Карта Земля–Місяць із пунктирним польотом після «СТАРТ!».
- Майданчик для планових поставок (роботи вантажать ящики, анімація в момент старту за розкладом).
- Паливо як бак; погода в день запуску; кнопка «Порадь мені».
