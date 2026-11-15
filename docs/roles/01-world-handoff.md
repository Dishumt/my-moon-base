# 01 · Світ (2.5D) — передача роботи новій сесії Claude

> AI-assisted: written with Claude. See docs/AI_USE.md
>
> Цей файл — «пам'ять» попередньої сесії Claude з техлідом (роль 01). Прочитай його повністю перед роботою.
> Власник: роль 01 (техлід). Дата: неділя, 15 листопада 2026 (день дедлайну, подача до **23:59**).

---

## 0. Головне за 30 секунд

- Ти працюєш з **техлідом (роль 01)**. Його зона: `Scripts/Core/`, `Scripts/World/`, `Scripts/Editor/`, `Scenes/Main.unity`, `Prefabs/`, `Settings/`, `ProjectSettings/`, `Packages/`, `CLAUDE.md`, веб-збірка.
- **У тебе немає Unity.** Ти пишеш код у хмарі; користувач перемикається в Unity, чекає компіляцію і показує скріни Console. Тому:
  - пиши обережно (компілятор тебе не перевірить), перечитуй код перед комітом;
  - після кожного кроку давай **точну інструкцію перевірки** (яке меню, що натиснути, що має статися);
  - сцени й Inspector налаштовує користувач — кодом роби все, що можна, через меню **Moon → …** (Editor-скрипти), щоб йому лишалось одне натискання.
- **`.meta` для нових файлів створюй сам** (Unity їх згенерує інакше з різними GUID у кожного):
  ```
  fileFormatVersion: 2
  guid: <32 hex, напр. python3 -c "import uuid;print(uuid.uuid4().hex)">
  MonoImporter:
    externalObjects: {}
    serializedVersion: 2
    defaultReferences: []
    executionOrder: 0
    icon: {instanceID: 0}
    userData: 
    assetBundleName: 
    assetBundleVariant: 
  ```
  (для `.shader` — `ShaderImporter:` з полями `externalObjects, defaultTextures: [], nonModifiableTextures: [], preprocessorOverride: 0, userData, assetBundleName, assetBundleVariant`; для папки — `folderAsset: yes` + `DefaultImporter:`.)
- Кожен новий файл починай рядком `// AI-assisted: written with Claude. See docs/AI_USE.md`.
- Відповідай **українською, просто**, кожен термін — одним реченням. Перед великою задачею — план 3–5 кроків і чекай «ок».

---

## 1. Git: як почати

- Уся попередня робота світу — у гілці `claude/festive-ptolemy-oe1hln` і (після PR) у `main`. **Спершу переконайся, що її злито в `main`**, і підтягни `main` у свою гілку (`git merge origin/main`).
- Інші ролі постійно зливають у `main` — **перед кожною задачею мерж `main`** і швидко глянь, що змінилось у `Scripts/Simulation`, `Logistics`, `Radiation`, `UI` (контракти!).
- Пуш тільки у свою гілку сесії. Користувач сам робить PR у `main`.
- **Пастки, на які вже наступили:**
  - GitHub Desktop робить *stash* при Pull з незакоміченими змінами → у `Main.unity` залишились маркери `<<<<<<< Updated upstream / >>>>>>> Stashed changes` і Unity не відкрив сцену. Лікування: розібрати блоки скриптом (взяти нові налаштування з upstream + нові об'єкти зі stashed), перевірити цілісність YAML (усі `{fileID: N}` мають існувати як `--- !u!… &N`, `m_Father`/`m_Children` взаємні, `SceneRoots.m_Roots` = кореневі Transform). Користувачу: **перед Pull завжди коміть або Discard**.
  - Юля (02) комітить `.png` **без `.meta`** → у користувача з'являються «26 changed files» з `.meta`. Їх треба закомітити (правило 3).
  - Користувач часто опиняється не на тій гілці → «missing script». Першим ділом питай/перевіряй **Current Branch**.

---

## 2. Технічні нюанси Unity 6.6 у цьому проєкті

- **Тільки нова Input System** (`activeInputHandler: 1`): `Keyboard.current`, `Mouse.current`. Старий `Input.GetKey` кидає виняток.
- `Object.FindFirstObjectByType` — **застарілий** (warning). Використовуй `FindAnyObjectByType<T>()` і `FindObjectsByType<T>()` (без `FindObjectsSortMode`).
- **NUnit у тестах — старий (3.5)**: немає `Is.AnyOf`, `Assert.Multiple`, `Assert.Positive`… Пиши `Is.EqualTo(a).Or.EqualTo(b)`.
- **URP**: у Graphics Settings `Default Render Pipeline = None` — **так і треба**, пайплайн задається в Quality: `PC_RPAsset` (редактор), `Mobile_RPAsset` (Web). Не міняй.
- **WebGL**: ніяких `Thread/Task/System.IO` у рантаймі, `Shader.Find` у рантаймі не покладайся (шейдер може вирізатись) — матеріали мають бути асетами, на які посилається сцена (`ModuleLook`). Editor-код — у `Scripts/Editor/` або під `#if UNITY_EDITOR`.
- Спрайти персонажів: `Point` фільтр, `Clamp`, без стиснення, `FullRect`, pivot `(0.5, 0.03)`, PPU 72 (≈1,7 м).
- Дані — тільки через `DataLoader.Load<T>("Balance/balance")` (Resources/Data). Тексти — тільки `Loc.Get(key)` (у `Scripts/World` є тексти лише для DebugPanel — це дозволено).

---

## 3. Архітектура світу (`Assets/_Project/Scripts/World/`)

| Файл | Що робить |
|---|---|
| `CameraFollow` | камера 45°, плавно за інженером, зум колесом |
| `Billboard` | спрайт до камери. `Mode.Upright` (вертикально + витяг 1/cos нахилу — для персонажів, щоб не встромлялись у стіни), `Mode.FaceCamera` (паралельно екрану — меблі, іконки). `ExtraScale` — для сплющення/пульсу |
| `EngineerController` | WASD відносно камери, інерція (у скафандрі ковзає), `Locked` (шлюз), `SuitOn`, `Shake`, `SpriteSet suit/indoor` (кадри + `pushFrames/airFrames`) |
| `MoonHop` | місячна хода: фази Push → Flight (парабола) → приземлення на **іншу** ногу; тінь-пляма (генерований спрайт), пил з `gravityModifier = 1.62/9.81`, сплющення. `Leg`, `OnGround`, `Height`, `IsHopping` |
| `CharacterSprites` | спільний вибір кадру (напрямок/нога/політ) для інженера й астронавтів; `ToScreen(worldDir)` |
| `BaseGrid` (**Core**) | сітка бази 6×6, клітинка 8 м, `SlotId` = номер квадратика; `CanPlace`, `ToWorld`, `FromWorld` |
| `BaseView` | на World: створює `ModuleView` на кожен `ModuleState`; `Find(slot)`, `All`, `FindRoom(type)`, `SlotAt(pos)`, `PathTo(from,to)` (A* в обхід модулів) |
| `BasePathfinder` | A* сітка 0,5 м + «натягування нитки»; тести `01PathfinderTests` |
| `ModuleView` | один модуль: риштування (росте з `BuildHoursLeft`) → кімната (`RoomBuilder`) або обладнання (панелі, батарея, антена, реактор). Пише `EngineerNearby`. Точки: `OuterDoorPoint`, `ChamberPoint`, `InnerDoorPoint`, `RoomSpot(i)`, `FrontPoint(i)`, `RobotSpot(i)`, `AddBlockers`. `Interior`, `InteriorOpen` |
| `RoomBuilder` | будує кімнату: **оболонка** (бочка Habitat/Medbay, скляний тунель Greenhouse, блок+купол Lab, бункер+горб реголіту Shelter), підлога+бортик, невидимі стіни-колайдери, **меблі-спрайти** (`LayoutFor(type)`; запасний варіант — коробки), **шлюз назовні** з подвійними дверима |
| `ModuleInterior` | коли інженер у модулі: оболонка плавно зникає (solid↔glass матеріал), `_Indoor` у шейдері, меблі видно тільки коли `IsOpen` |
| `Airlock` | вхід: зачинились двері → пил → зняв скафандр → внутрішні двері; вихід навпаки. Відчиняє двері астронавтам (`AstronautView.All`), `BlowDust(sec)` |
| `ModuleLook` | матеріали (`ToonSolid/ToonGlass`, `Dust`), розміри, кольори типів, тривалості шлюзу, **список меблів** (`Furniture(name)`) |
| `ModuleParts` | `Box/Cylinder/Sphere` + фарбування через `MaterialPropertyBlock` |
| `ModuleKinds` | `IsRoom(type)` |
| `OutdoorDarkening` | на Sun: вулиця темнішає, коли інженер усередині; `alsoDarken` — Unlit-земля |
| `PoiMarker` | точка на поверхні; `Discovered` при наближенні; ховається, якщо точки немає в поточному розділі |
| `CrewView`, `AstronautView` | екіпаж: йде до `Task/TaskTarget` (номер квадратика або id точки; порожньо → перше відповідне місце), через шлюз зі скафандром, пише `InsideSlot` щокадру й `AtTaskSite`; іконка стану тільки коли погано; у закритому модулі тіло сховане |
| `RobotView` | ровер з фігур (жовтий корпус, лампа: жовта — працює, червона — нема енергії, зелена — вільний), їде до `TargetSlot`, «ангар» `CrewLook.robotHome = (0,0,-8)` |
| `CrewLook` | спрайти спеціальностей, іконки, швидкості, точки «площа»/«ангар» |
| `WorldDebugButtons` | кнопки в DebugPanel: модуль у вільну/тут, -1 год, добудувати, прибрати; завдання екіпажу по колу; тест іконок; енергія роботам |
| `Settings/Shaders/MoonToon.shader` | мультяшний шейдер: 3 рівні світла, контур (inverted hull по осях), `_Indoor`, `_LightRef`; прозорість через `_SrcBlend/_DstBlend/_ZWrite` |

### Меню (Editor) — порядок налаштування сцени
1. `Moon → Розставити базу і поверхню (відкрита сцена)` — земля 400×400, маркери точок з **усіх** `chapter_NN.json`.
2. `Moon → Зробити землю 2D (як у DST)` — Unlit-земля з текстурою Юлі.
3. `Moon → Мультяшний стиль модулів (toon)` — `ToonSolid/ToonGlass` у `ModuleLook`.
4. `Moon → Налаштувати інженера (спрайти й анімація)`.
5. `Moon → Налаштувати екіпаж і роботів` — додає `CrewLook` + `CrewView` на World.
6. `Moon → Налаштувати меблі (спрайти Юлі)` — PPU 115, pivot (0.5, 0.07).
7. `Ctrl+S`.

---

## 4. Контракти з іншими ролями (перевіряй після кожного мержу `main`)

- `ConstructionSystem.Order(GameState s, ModuleType type, int slotId)` (03) — **з `s` першим параметром**; роботам ставить `Task/TargetSlot`, `Working` залежить від `PowerSystem.Granted.Robots`. `ShieldWithRegolith(s, slot)` → `ModuleState.RegolithShield`.
- `PowerSystem.Granted` (03) — **скільки енергії реально отримала кожна система цієї години** (`PowerPlan`: LifeSupport, Greenhouse, Comms, Shelter, Lab, Medbay, Robots). Для «світло гасне» читай саме `Granted`, не `s.Power`. Доступ: `GameBootstrap.Instance.GetSystem<PowerSystem>()`.
- `GreenhouseSystem.Growth` (03) — ріст урожаю (для «рослини ростуть»).
- `CrewSystem.Assign(s, astronaut, task, targetId)` (04) — ставить `Task`, `TaskTarget`, `AtTaskSite=false`; ресурс дає тільки при `AtTaskSite`.
- 06: `Astronaut.Dose/InsideOnly/GoingHome/Health` — світ показує іконками.
- `chapter_NN.json` (03): id точок **`pad_01, ice_01, sample_01, sample_02, fact_01`**, у розділах 2–3 додаються `ice_02, sample_03, fact_02, ice_03, sample_04`.
- 05 (планшет): вкладка **Build** (сітка `BaseGrid` → `Order`), «Map» — лише перегляд світу.

---

## 5. Відомі проблеми

1. **Тонка чорна вертикальна лінія від ніг інженера вниз**, з'являється **лише під час руху камери**, не гізмо. Причину не знайдено. Попросили користувача вимикати по черзі дочірні об'єкти інженера в Play (`Shadow`, `Hop Dust`, `Square`) — відповіді ще немає. Підозри: спрайт-тінь `MoonHop` (згенерований `Sprite.Create` з Tight-мешем), частинки `Hop Dust`, артефакт Point-фільтра/FullRect. Можлива перевірка: зробити тінь `FullRect`, змістити тінь на +0.01 вгору, вимкнути частинки — по черзі.
2. Меню **«06: Перевірити дані DONKI»** (роль 06) з мережі хакатону дає 15 timeout і **все одно перезаписує** `donki_storms.json`. Користувач відкотив. Ролі 06 написано: не перезаписувати файл, якщо жоден запит не вдався. Не натискати.
3. Кадри персонажів (зі згоди Юлі) **змінені кодом**: вирівняні по шолому й ногах, прибрана світла кайма, у ходьбі вбік без скафандра домальовані кадри «ноги разом» (02 і 04), у інженера однакова сумка. «Стоїть» без скафандра = `*_front_walk_02` (окремі idle-картинки іншого масштабу). Якщо Юля перемалює — запусти меню налаштування знову.

---

## 6. План (за пріоритетом). Кожен пункт — окремий маленький коміт з інструкцією перевірки

### A. Веб-збірка (якщо ще не пройшла)
Налаштовано: Gzip + Decompression Fallback, 1280×720, у збірці тільки `Main`. Профіль `Web - Desktop - Release`, для тестів Code Optimization = *Shorter Build Time*. Помилки з браузера (F12 → Console) виправляй. Перед подачею — фінальна збірка з *Disk Size with LTO*, перевірка в Chrome і Edge, itch.io (HTML, zip вмісту, «played in browser», 1280×720, Fullscreen, публічна сторінка, перевірити в інкогніто).

### B. Меблі «живуть» (користувач обрав усе з цього)
1. **Товщина і тінь.** Для кожного меблевого спрайта: дочірня копія того ж спрайта кольором `(0.12,0.12,0.16,1)`, `sortingOrder` −1, зсув назад на ~0,06 м і вбік на ~0,03 м — «картон з товщиною»; плюс м'яка тінь-пляма на підлозі (зроби `MoonHop.BlobSprite()` публічним і використай; лежить рівно, ширина ≈ 0,9 ширини предмета). Тінь і копія теж у `hiddenWhenClosed`.
2. **Меблі відображають стан гри** (дуже наочно для дітей — головна «фішка»):
   - `oxygen_generator`: лампочка зелена, якщо `Granted.LifeSupport > 0`, інакше блимає червоним;
   - `hydroponic_rack`: зелень на полицях масштабується/яскравішає з `GreenhouseSystem.Growth` (оверлей-спрайт або tint), без енергії теплиці — тьмяніє;
   - `water_recycler`: бульбашки (частинки з `ModuleLook.dustMaterial`, блакитні), коли є енергія і `Res.Water > 0`;
   - `medical_cabinet`: маленькі «аптечки»-крапки = `Res.MedKits` (до 5);
   - `power_control_panel`: ряд LED = скільки блоків енергії отримав цей модуль;
   - `workstation_desk`: екран світиться, коли `Granted.Lab > 0`.
   Лампи/LED — маленькі згенеровані круглі спрайти, позиції в пікселях картинки → метри: `x = (px - 128) / PPU`, `y = (256 - py) / PPU - 0.07*256/PPU`. Зроби компонент `FurnitureLife` (на кожному меблі, `kind` = ім'я файлу), читає `GameState` раз на кадр.
3. **Світло гасне без енергії** (обрано): кімната → поле енергії: Habitat→LifeSupport, Greenhouse→Greenhouse, Medbay→Medbay, Lab→Lab, Shelter→Shelter (уточни в 03, чи так). Якщо `Granted.X == 0`: `_Indoor` → 0, підлога/меблі темніші (×0,35, через `SpriteRenderer.color` і `_BaseColor`), на стелі блимає червона аварійна лампа. Плавний перехід 0,5 с.
4. **Астронавти користуються меблями.** `RoomBuilder` реєструє позиції меблів у `ModuleView` (`FurnitureSpot(name, i)` = 0,6 м перед предметом). `AstronautView.RoomSpot` → за завданням: Rest → `bunk_bed` (лягти: спрайт тіла повернутий на 90° і на висоті ліжка, або «Zzz»-іконка), Greenhouse → `hydroponic_rack`, Lab → `science_bench` (геолог → `specimen_cabinet`), Medbay/Heal → `medical_cabinet`, хворий `Severe` → лежить на `bunk_bed` у Medbay, Shelter → біля `cargo_crates`, None у Habitat → `dining_table`. Обличчям до предмета (facing вгору = спиною до камери).
5. **Дрібні анімації**: LED на щитку блимають, пара з кухні (`galley_kitchen`), мерехтіння екрана, «дихання» гідропонних ламп.

### C. Робот зі спрайтів Юлі (`Art/Characters/robot`, 128 px)
Кадри: `drive_away_01..08` (їде вгору екраном), `drive_toward_01..08` (вниз), `walk_left/right_01..08` (вбік), `construction_01..12` (будує), `cleaning_01..12` (засипає реголітом / чистить), `sleep_01..08` (нема енергії або вільний). Заміни 3D-ровер у `RobotView` на білборд (Upright, pivot унизу, PPU ≈ 100 → ~1,2 м) з анімацією 10 fps; статус: `Working` → construction/cleaning, завдання без енергії → `sleep` + іконка `Icons/Status_64/energy.png` над головою, вільний → `sleep` в ангарі. Додай у `CrewSetupTool` імпорт і заповнення `CrewLook.robot…`.

### D. Поверхня: каміння й кратери Юлі (`Art/Props/Moon rocks`)
- Точки: `ice_*` → `crater_with_ice_crystals`/`frozen_ice_crater`, `sample_*` → `pale_mineral_ore`/`ice_veined_ore`, кратери — плоскі декалі на землі (SpriteRenderer, поворот 90° по X), камені — білборди.
- **Розкидати декор** детерміновано (seed): ~150 каменів/кратерів у радіусі 200 м, не ближче 6 м до сітки бази й до точок; дрібні `lunar_pebble_small`, `small_rock_cluster` частіше, `jagged_rock_spire` рідко. Меню `Moon → Розкидати каміння` (Editor, створює об'єкти в сцені під `Decor`).

### E. Відчуття Місяця (від попередньої сесії — запропонуй користувачу, роби що схвалить)
- **Сліди на реголіті**: інженер і астронавти надворі лишають сліди-декалі (на Місяці вони лишаються мільйони років — факт для дітей!). Пул ~300 декалів, найстаріші зникають.
- **Земля в небі**: великий спрайт Землі низько над горизонтом (з полюса Місяця Земля майже не рухається по небу) — красиво й навчально.
- **Тінь від хребта**: `balance.json → power.shadeHoursPerDay` — у ці години світ поступово темнішає (сонячні панелі дають менше — 03 уже рахує), наочно видно «чому впала енергія».
- **Звуки** (якщо хтось знайде вільні асети, кредити в `docs/CREDITS.md`): глухий «туп» приземлення, шипіння шлюзу, гул у модулі, сигнал тривоги без енергії.
- **Буря** (06 шле `GameEvents.StormWarning/StormStarted`): небо/світ червоніє, по екрану «іскри» частинок, легка тряска камери, усі біжать в укриття — дуже ефектно для суддів.
- **Пил від роботів** під час будівництва, іскри зварювання.

### F. Решта задач ролі 01
- `MapCamera`: ортографічна камера згори → `RenderTexture` (512², оновлення 2 рази/с), статичне `MapCamera.Texture` для вкладки Map у планшеті (05).
- Збереження: `GameBootstrap` уже зберігає в кінці дня; перевір, що після «Продовжити» світ відновлює екіпаж у модулях (`InsideSlot`) і модулі.
- Режим «Demo for judges» (з 03 і 05): усі розділи відкриті, у розділі 3 буря через хвилину.

---

## 7. Повідомлення, які ще можуть бути потрібні іншим

- **05 (Ваня)**: вкладка Build окремо від Map; кнопка «Засипати реголітом» → `ShieldWithRegolith`; рядок «Роботи» на вкладці Power; тексти `error.place_outside`, `error.place_taken`.
- **06**: `DonkiCheckTool` не має перезаписувати файл, якщо жоден запит не вдався; дзеркало `https://api.nasa.gov/DONKI/...&api_key=DEMO_KEY`.
- **02 (Юля)**: коміть `.png` разом з `.png.meta`; idle-картинки без скафандра намальовані в іншому масштабі, ніж ходьба; кадри ходьби вбік без скафандра — однакова сумка і поза «ноги разом».

---

## 8. Як завжди закінчувати крок

1. Код + `.meta` → коміт → пуш у свою гілку.
2. Користувачу: **Pull origin** → що запустити (меню) → `Ctrl+S` → **Play** → що має статися → що закомітити.
3. Нагадати додати рядок у `docs/AI_USE.md`.
