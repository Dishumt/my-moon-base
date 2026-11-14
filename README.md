# 🌙 moon-company-iasasus (робоча назва)

> **About (EN).** A browser-based 2.5D survival sim for kids aged 7–12. You are the chief engineer of the first permanent base at the lunar south pole: launch cargo from Earth, build modules with robots, explore the surface, split limited power between life support, water, food and radiation shielding, and keep a crew of four specialists safe. Every choice has a cost, and the solar storms are real events from NASA's DONKI database. Built with Unity (WebGL) for the NASA Space Apps Challenge 2026, challenge "Build a Junior Astronaut Mission Trainer". *Work in progress during the hackathon, Nov 14–15, 2026.*

Браузерна 2.5D-гра для дітей 7–12 років про будівництво бази на південному полюсі Місяця.

Ми робимо її для NASA Space Apps Challenge 2026, челендж [Build a Junior Astronaut Mission Trainer](https://www.spaceappschallenge.org/2026/challenges/junior-astronaut-mission-trainer/?tab=details).

> ⏰ **Дедлайн подачі: неділя, 15 листопада 2026, 23:59** за часом нашого Local Event. Після цього подати вже не можна.

- 📖 **Що це за гра і як вона працює:** [docs/GAME.md](docs/GAME.md)
- 🤖 **Інструкції для Claude:** [CLAUDE.md](CLAUDE.md)
- 🧑‍🚀 **Завдання кожної ролі:** [docs/roles/](docs/roles/)

---

## Команда

| # | Роль | Хто | Що читати |
|---|---|---|---|
| 01 | Техлід (Unity) | _Єгор_ | [01-tech-lead.md](docs/roles/01-tech-lead.md) |
| 02 | Арт-дизайнер | _Юля_ | [02-art.md](docs/roles/02-art.md) |
| 03 | Симуляція й будівництво | _Тимофій_ | [03-simulation.md](docs/roles/03-simulation.md) |
| 04 | Запуск, поставки й екіпаж | _Артем_ | [04-logistics.md](docs/roles/04-logistics.md) |
| 05 | Планшет, інтерфейс і тексти | _Ваня_ | [05-tablet-ui.md](docs/roles/05-tablet-ui.md) |
| 06 | Радіація, здоров'я й дані NASA | _ім'я_ | [06-radiation-data.md](docs/roles/06-radiation-data.md) |

Капітан команди, крім своєї ролі, стежить за планом, часом і подачею проєкту.

---

## 🧰 Що встановити (кожен)

Усе нижче безкоштовне.

- [ ] Акаунт GitHub і прийняте запрошення в репозиторій.
- [ ] [GitHub Desktop](https://desktop.github.com/): git мишкою, без команд.
- [ ] Unity Hub і **Unity 6.6** (точна версія: `6000.6.9f1`, вписує техлід) з модулем **Web Build Support**. Unity Personal безкоштовна. Це кілька гігабайт, тож **починайте завантаження одразу**.
- [ ] Прочитати [docs/GAME.md](docs/GAME.md) і файл своєї ролі.

### Claude: два способи

| | Claude Code (платний план) | Чат claude.ai (безкоштовно) |
|---|---|---|
| Що вміє | сам читає й змінює файли проєкту, робить git, `/sync` | відповідає в чаті, а файли ви зберігаєте самі |
| Як почати | відкрити папку проєкту і написати «Я роль NN…» | новий чат → **завантажити** `CLAUDE.md`, файл своєї ролі і файли, які змінюєте → написати «Я роль NN…» |
| Git | просите Claude | GitHub Desktop |
| Обмеження | ліміти плану | менше повідомлень на день: пишіть одну чітку задачу за раз |

- **Підказки у файлах ролей підходять для обох способів.**
- **У чаті:** Claude видасть повний вміст файлу з шляхом. Збережіть його в цю папку (наприклад, через VS Code або Блокнот), перемкніться в Unity, перевірте і закомітьте через GitHub Desktop.
- **Якщо Claude Code є лише в когось одного**, сідайте поруч: хай найскладніші задачі робляться на його комп'ютері. Акаунтами не діліться, це заборонено правилами Claude.

---

## 📦 Як наповнюємо репозиторій (хто, що і в якому порядку)

На старті в репозиторії лежать **тільки ці документи**. Далі все додається по кроках.

**Крок 1. Капітан: документи в GitHub**
1. GitHub Desktop → File → New repository → назва `moon-company-iasasus` → Create.
2. Скопіюйте в цю папку: `README.md`, `CLAUDE.md`, `.gitignore`, папки `docs/` і `.claude/`.
3. Commit to main → **Publish repository** (зніміть галочку «Keep this code private», якщо репозиторій має бути публічним для суддів).
4. На github.com: Settings → Collaborators → запросіть усіх 5.

**Крок 2. Техлід: Unity-проєкт і каркас коду** (деталі у [01-tech-lead.md](docs/roles/01-tech-lead.md))
1. Склонуйте репозиторій.
2. Створіть Unity-проєкт і перенесіть у папку репозиторію `Assets/`, `Packages/` і `ProjectSettings/`.
3. Створіть каркас коду, папки ролей і тестові сцени.
4. Push у `main` і напишіть у чат: **«Каркас готовий, всі робимо /sync»** (або «Fetch origin» у GitHub Desktop).

**До кроку 2** ролі 02–06 не чекають. Вони роблять те, що не потребує проєкту: малюють, збирають дані NASA, пишуть числа й тексти (див. «Задача 0» у файлі своєї ролі).

**Крок 3. Кожна роль: своє, у своїх папках, через гілки й Pull Request**

| Роль | Що додає | Куди |
|---|---|---|
| 02 Арт | картинки PNG, текстури модулів, `docs/CREDITS.md` | `Assets/_Project/Art/` |
| 03 Симуляція | скрипти систем, числа балансу, розділи | `Scripts/Simulation/`, `Resources/Data/Balance/`, `Resources/Data/Chapters/` |
| 04 Логістика | скрипти запуску, поставок, завдань; екран запуску; список вантажів | `Scripts/Logistics/`, `UI/Launch/`, `Resources/Data/Launch/` |
| 05 Інтерфейс | планшет, HUD, звіти, тексти EN/UK | `Scripts/UI/`, `UI/Tablet/`, `UI/Common/`, `Resources/Data/Text/` |
| 06 Радіація і дані | скрипти радіації й здоров'я, дані NASA, `docs/NASA_DATA.md` | `Scripts/Radiation/`, `Resources/Data/Nasa/` |
| Усі | свої тести; рядок у `docs/AI_USE.md` про те, де допомагав ШІ | `Tests/EditMode/`, `docs/` |

**Як додати своє:**
- **У GitHub Desktop:**
  1. Current branch → New branch `03-sim/greenhouse` (від `main`).
  2. Додайте чи змініть файли.
  3. Напишіть опис → **Commit**.
  4. **Push origin** → **Create Pull Request**.
- **У Claude Code:** «створи гілку…», «закоміть», «запуш і створи Pull Request».

**Крок 4. Техлід зливає** Pull Request-и приблизно кожні 2 години. Після кожного злиття всі оновлюються:
- у GitHub Desktop: «Fetch origin», потім Branch → **Update from main**;
- або `/sync` у Claude Code.

**Правила:**
- не працюємо в `main`;
- сцену `Main.unity` змінює тільки техлід;
- **завжди комітимо `.meta`-файли** разом з асетами.

---

## 🚀 Перший запуск (після кроку 2)

1. Unity Hub → **Add** → **Add project from disk** → оберіть папку репозиторію. Перший імпорт триває 5–15 хв.
2. Відкрийте свою сцену `Assets/_Project/Scenes/Sandbox/NN-....unity` і натисніть ▶ Play.
3. Напишіть Claude (у Claude Code або в чаті з завантаженими файлами):
   > Я роль 03 (Симуляція). Прочитай CLAUDE.md і docs/roles/03-simulation.md і скажи, з чого почнемо.

---

## 🤖 Як працюємо з Claude

- Claude знає правила проєкту з `CLAUDE.md`. У чаті claude.ai цей файл завантажуєте самі.
- **Перед кожною новою задачею оновіться:** `/sync` або «Update from main» у GitHub Desktop.
- На кожну задачу відкривайте нову сесію чи новий чат.
- Спершу просіть план, потім код: «Спершу поясни план, потім роби».
- Після кожного кроку перевіряйте в Unity і комітьте.
- Якщо в Unity помилка, скопіюйте червоний текст з Console і вставте Claude.
- Якщо щось треба змінити в чужій зоні, напишіть власнику в чат команди.

---

## 📁 Структура (після кроку 2)

```
Assets/_Project/
  Scripts/         Core · World · Simulation · Logistics · UI · Radiation
  UI/              Tablet · Launch · Common   (UI Toolkit: .uxml + .uss)
  Resources/Data/  Balance · Chapters · Launch · Nasa · Text   (JSON)
  Art/  Prefabs/  Scenes/ (Main + Sandbox)  Tests/EditMode/
docs/              GAME.md · roles/ · NASA_DATA.md · CREDITS.md · AI_USE.md
.claude/skills/    команда /sync
```

---

## ⏱️ План

Хакатон уже йде (14–15 листопада). Години рахуємо від нашого старту.

| Години | Що робимо | Що має бути в кінці |
|---|---|---|
| 0–1 | Капітан викладає документи, усі ставлять програми | Усі мають доступ до репозиторію |
| 0–3 | Техлід робить каркас. Інші — «Задача 0» своєї ролі, без Unity | Каркас лежить у `main` |
| 3–9 | Кожен робить свою систему й перевіряє її у Sandbox-сцені | Системи працюють окремо |
| ~9 | Перше злиття в `main` | Час іде, ресурси змінюються, інженер ходить |
| 9–15 | Планшет, запуск, поставки й радіація працюють разом. Перша веб-збірка | Розділ 1 проходиться в браузері |
| Ніч | Сон хоча б 4–5 годин | — |
| 20–30 | Розділи 2–3, бурі з DONKI, теплиця, лід, арт, тексти, туторіал | Основа гри готова |
| **30** | **Нових функцій більше не додаємо** | — |
| 30–36 | Тестування, баланс, баги, відео, опис проєкту | Матеріали для подачі |
| до 21:00 неділі | Фінальна збірка і подача | ✅ з запасом до 23:59 |

---

## 📤 Подача (за правилами Space Apps 2026)

- [ ] **Усе англійською:** сторінка проєкту і всі матеріали за посиланнями. Якщо відео з голосом іншою мовою, потрібні англійські субтитри.
- [ ] **Демо:** відео **до 30 секунд** або слайди **до 7 штук** (разом з титульним). Якщо більше, проєкт не допустять до глобального суддівства (капітан і 02).
- [ ] **Посилання на гру:** веб-версія на itch.io, відкривається без входу й паролів (01). Перевірте її в режимі інкогніто.
- [ ] **Опис проєкту:** що робить, як працює, які інструменти використали (капітан і 05).
- [ ] **Дані NASA:** які і навіщо, з `docs/NASA_DATA.md` (06). Для глобальних нагород потрібен мінімум один ресурс NASA.
- [ ] **Інші ресурси:** усі чужі асети, код і дані з `docs/CREDITS.md` (02).
- [ ] **Використання ШІ:** з `docs/AI_USE.md`, де і як допомагав ШІ (капітан).
- [ ] Відправити **до 21:00 неділі**, з запасом до 23:59. Після подачі можна редагувати й подавати знову до дедлайну.

**Правила, які не можна порушити:**
- ❌ логотипи, прапори й емблеми місій NASA, також у згенерованих картинках;
- ❌ фото, відео чи голос людей молодше 18 років у демо й у грі (на плейтестах з дітьми не знімайте їх);
- ✅ код і дані, зроблені за допомогою ШІ, позначені в описі й у самих файлах (див. CLAUDE.md, правило 10);
- ✅ картинки й відео, згенеровані ШІ, мають **видимий водяний знак**;
- ✅ саму ідею рішення команда має придумати сама: ШІ — тільки інструмент.

---

## 🛰️ Дані NASA, які ми використовуємо

- [DONKI](https://ccmc.gsfc.nasa.gov/tools/DONKI/): сонячні бурі
- [RadLab (OSDR)](https://visualization.osdr.nasa.gov/radlab/gui/data-api/): реальні дози радіації на МКС
- [OSDR](https://osdr.nasa.gov/bio/repo/): дослідження здоров'я астронавтів
- [Moon Trek](https://trek.nasa.gov/moon/): мапа південного полюса Місяця
- [CSA: їжа на Місяці](https://www.asc-csa.gc.ca/eng/youth-educators/toolkits/food-production/)
- [NASA Open APIs](https://api.nasa.gov/)
