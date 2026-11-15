<!-- AI-assisted: written with Claude. See docs/AI_USE.md -->
# Подача на Space Apps — чернетка для капітана

*Чернетка від ролі 01. Тексти для форми — **англійською** (так вимагає Space Apps). Перевір, виправ і встав у форму. Квадратні дужки `[...]` — заповнити.*
*Дедлайн: **неділя 15.11, 23:59**. Подавати краще до 23:00: сайт у останню годину повільний.*

## Чекліст подачі

| Що | Хто | Готово |
|---|---|---|
| Веб-збірка на itch.io, публічна, відкривається в інкогніто (Chrome і Edge) | 01 | ☐ |
| Відео 30 с (або 7 слайдів), посилання доступне всім | капітан + 02 | ☐ |
| Текст форми (нижче) | капітан | ☐ |
| `docs/AI_USE.md` повний, у формі — розділ «Use of AI» | капітан | ☐ |
| `docs/CREDITS.md` — чужі асети, шрифти, звуки з ліцензіями | 02 | ☐ |
| Посилання на репозиторій GitHub (публічний або доступ для суддів) | 01 | ☐ |
| Ніяких логотипів NASA, прапорів, емблем місій ні в грі, ні у відео | усі | ☐ |

---

## Project name
**My Moon Base** — a junior astronaut mission trainer

## High-level summary (≈ 150 words)
My Moon Base is a free browser game for kids aged 7–12. The player is the chief engineer of the first base at the Moon's south pole. Solar panels give only a few power blocks, so every choice has a cost: more power for life support means more oxygen but slower robots; a greenhouse gives food but drinks water; a shelter keeps the crew safe from radiation but needs power exactly when a solar storm comes. The storms are real 2024 events from NASA DONKI, and today's space weather is fetched live. Every evening a report explains what changed and why, so kids learn cause and effect, not just numbers. Three short chapters build up the trade-offs: power and oxygen, then ice, food and science, then radiation and health. The game runs in any desktop browser, in English and Ukrainian, and has a 5-minute "Demo for judges".

## Link to project demo
[30-second video link — YouTube / Google Drive, «Anyone with the link»]

## Link to final project
- Play: [itch.io link]
- Code: https://github.com/pinnkipuy/moon-company-iasasus

## Detailed description

**What it does.** A 2.5D survival-and-management game (3D world, 2D characters, camera at an angle). You walk the engineer around the base and the lunar surface with W A S D and run the base from an engineer's tablet. The game is paused while the tablet is open, so kids have time to think.

**How it works — the trade-offs a junior astronaut learns:**
- **Launch from Earth.** Choose a rocket, pack cargo and fuel; a traffic light shows the risk. Heavier cargo needs more fuel; a failed launch costs days.
- **Power.** Solar panels give power blocks; in the shadow of the ridge they give less and the battery helps. The player shares the blocks between life support, robots, greenhouse, antenna, shelter, lab and medbay (up to 4 blocks each). The tablet shows what each block gives and how long oxygen and food will last.
- **Water → oxygen, food.** Life support makes oxygen from water and power. Water comes from ice in permanently dark craters; the greenhouse grows food but needs water and power.
- **Building.** Robots build modules on a base grid and can cover them with regolith (Moon soil) to block radiation. They work only with power.
- **Crew.** Four specialists (engineer, medic, botanist, geologist); each job is done better by its specialist. Jobs outside give a higher radiation dose.
- **Radiation and real solar storms.** Every astronaut has a dose bar based on NASA dose numbers. Before a storm the player gets a warning with the real date and class of a NASA DONKI event and must bring everyone into a powered shelter.
- **Communication.** At the south pole Earth hangs low over the horizon and the mountains hide it, so messages to Earth go only in comms windows; the antenna needs power.
- **Evening report.** Every game day ends with a short report of what changed and **why**, with alarms in red. A coach line at the bottom of the screen always suggests the next sensible step.
- **Surface map, sound and help.** The tablet has a MAP tab: a plan of the surface with the base, the crew and every place to visit (a question mark until the engineer gets there) and how far it is. All sounds (clicks, storm alarm, launch, evening report) are made by code, so there are no third-party audio files. ESC opens a pause menu with a "How to play" page; the first landing shows it by itself.

**Benefits and impact.** Kids feel real engineering trade-offs instead of memorising facts: there is never enough power for everything, every action has a price, and safety (oxygen, radiation) comes first. The game is free, runs in a browser without installing anything, and is in two languages.

**Tools.** Unity 6.6 (URP, WebGL build), C#, UI Toolkit, GitHub. Art: [02: sprites made by the team / tools used]. AI assistant: Claude (see "Use of AI").

**Creativity.** The real NASA space weather of the day is shown on the start screen (LIVE), and the in-game storms are real events, so a kid can say "this storm really happened on 10 May 2024".

## NASA data
- **NASA DONKI** — 15 real 2024 space-weather events (X-class solar flares, solar energetic particle events, the G5 geomagnetic storm of 10 May 2024) are the solar storms of the game, with date, class and source link; the start menu asks the DONKI API for the last 7 days and labels the result **LIVE** (or **ARCHIVE** if offline).
- **NASA OSDR / RadLab and NASA-STD-3001** — typical ISS dose ≈ 0.5 mSv/day and the 600 mSv career limit drive the dose bars (time compressed ×50, explained in the game).
- **NASA OSDR** — 8 kid-friendly facts about the body and plants in space, with study IDs.
- **NASA Moon Trek (LRO)** — reference for the south pole terrain.
- Full list, links and what we simplified: `docs/NASA_DATA.md`. The in-game **"NASA data in the game"** screen lists every source.

## Space agency partner data
- **CSA** — growing plants in space (greenhouse). [03: add the link]

## Use of artificial intelligence
We used **Claude** (an AI assistant by Anthropic) as a coding helper: most C# scripts, data files and kid-friendly texts were drafted with Claude and then reviewed, tested and changed by the team. Every such file starts with an "AI-assisted" line, JSON files have an `"_ai"` field, and the full list is in `docs/AI_USE.md`. All NASA numbers and links were checked by hand against NASA sources (15 of 15 DONKI events verified on 15 Nov 2026). No AI-generated NASA logos, flags or mission emblems are used. [02: say whether any images were AI-generated and with which tool.]

## References
See `docs/NASA_DATA.md` (sources) and `docs/CREDITS.md` (assets).

---

## Сценарій відео (30 секунд)

Запис екрана (OBS або Win+G), гра англійською, 1280×720. Голос або субтитри англійською.

| Секунди | Що на екрані | Текст |
|---|---|---|
| 0–4 | Меню «MY MOON BASE», блок «Space weather today · LIVE» | "My Moon Base: you run the first base at the Moon's south pole." |
| 4–9 | Екран запуску: вантаж, світлофор, старт ракети | "Pack the rocket — every kilo costs fuel." |
| 9–15 | Tab → вкладка «Енергія»: + / − блоки, прогноз кисню | "There is never enough power. Oxygen, robots or food?" |
| 15–20 | Роботи будують модуль; вечірній звіт | "Every evening the base tells you what changed and why." |
| 20–27 | Demo for judges: напис про бурю, «Всі в укриття», дати DONKI | "Solar storms are real NASA events. Get the crew to the shelter!" |
| 27–30 | Екран «NASA data in the game» | "Real NASA data. Play free in your browser." |

## Текст для сторінки itch.io

**Title:** My Moon Base
**Short description:** Run the first base at the Moon's south pole. Share the power, keep the crew safe from real solar storms. For kids 7–12.
**Controls:** W A S D — walk · Q / E — turn the camera · TAB — tablet · ESC — menu · P — pause · 1 2 3 — speed ×1 ×2 ×4 · M — sound · H — how to play.
**Tip for judges:** press **DEMO FOR JUDGES** in the menu: chapter 3, a real NASA solar storm starts in about a minute.
**Made for** the NASA Space Apps Challenge 2026, challenge "Build a Junior Astronaut Mission Trainer". AI use: see the repository (`docs/AI_USE.md`).
