# Використання ШІ (AI use)

Space Apps вимагає чесно позначати, де допомагав ШІ. Кожен, хто додав файл з допомогою ШІ, дописує сюди рядок.
*Space Apps requires disclosure of AI use. Every file made with AI help gets a line here.*

| Файл / папка | Що робив ШІ | Хто (роль) |
|---|---|---|
| `Assets/_Project/Scripts/Core/` | каркас ядра: GameState, GameClock, GameEvents, DataLoader, Loc, SaveSystem, GameBootstrap, DebugPanel (Claude) | 01 |
| `Assets/_Project/Scripts/{Simulation,Logistics,Radiation}/*System.cs`, `*State.cs` | порожні заготовки систем (Claude) | 01 |
| `Assets/_Project/Scripts/Editor/SceneSetupTool.cs` | інструмент, що створює сцени (Claude) | 01 |
| `Assets/_Project/Tests/EditMode/01CoreTests.cs` | тести ядра (Claude) | 01 |
| `Assets/_Project/Resources/Data/**/*.json` | стартові JSON-заготовки (Claude) | 01 |
| `docs/GAME.md`, розділ «Карта» | допоміг сформулювати рішення про обмежену карту і рух на WASD (Claude) | Капітан |
| `Assets/_Project/Scripts/Simulation/`, `Resources/Data/Balance/`, `Resources/Data/Chapters/`, `Tests/EditMode/03*Tests.cs` | код систем енергії, кисню, їжі, теплиці, будівництва, розділів, їхні тести і JSON з числами; пізніше — збереження стану систем (`SimState`) (Claude) | 03 |
| `Assets/_Project/Scripts/Radiation/`, `Tests/EditMode/06*Tests.cs` | код радіації, бур, здоров'я, живої космічної погоди, інструмент перевірки DONKI і тести (Claude) | 06 |
| `Assets/_Project/Resources/Data/Nasa/*.json`, `docs/NASA_DATA.md` | Claude вибрав події DONKI, написав правило сили бур, тексти фактів для дітей і зібрав файли. **Самі числа й посилання — NASA's**: 15 подій перевірено в DONKI API 2026-11-15, джерела фактів відкрито й звірено вручну, а `radlab_dose.json` звірено з документами NASA (RadLab-сайт того дня не відповідав) | 06 |
| `Assets/_Project/Resources/Data/Text/texts_en.json`, `texts_uk.json` (ключі `report.*`, `build.err.*`, `goal.*`, `storm.*`, `weather.*`, `dose.*`) | тексти звітів і підказок для дітей написали Claude (ролі 03 і 06), їх вставлено у файли | 03, 06 |
| `Assets/_Project/Scripts/UI/` (`PowerTabView`, `CrewTabView`, `CommsTabView`, `HudController`, `GameWindows`, `StartMenu`, `NasaDataView`, `Coach`, `UiKit`; зміни в `TabletController`, `BuildGridView`), `Assets/_Project/UI/Game/`, `UI/Tablet/GameTabs.uss` | вкладки планшета Енергія / Екіпаж / Звʼязок, HUD, вечірній звіт, вікна перемоги й поразки, меню старту, «Demo for judges», екран «Дані NASA», порадник; тексти EN/UK до них (Claude) | 01 (з дозволу 05) |
| `Assets/_Project/Scripts/Core/GameStart.cs`, зміни в `GameBootstrap.cs`, `Loc.cs`; `Resources/Data/Demo/demo.json`; `Tests/EditMode/01LocTests.cs` | запуск розділу / продовжити / демо, Loc читає тексти ролі 04, тест (Claude) | 01 |
| `Assets/_Project/Scripts/Editor/GameUiSetupTool.cs`, `tools/md_texts_to_json.py` | меню Moon → Інтерфейс гри в Main; скрипт перенесення текстів з md у JSON (Claude) | 01 |
| `Resources/Data/Balance/balance.json` (`realSecondsPerDay`, `maxBlocksPerSystem`), `Chapters/chapter_0N.json` (цілі в днях, кисень розділу 1) | новий баланс довшого дня — рішення техліда, числа порахував Claude | 01 (зона 03) |
| `docs/SUBMISSION.md` | чернетка тексту подачі на Space Apps і сценарій відео (Claude) | 01 для капітана |
| `Assets/_Project/Scripts/Core/GameAudio.cs`, `SoundSynth.cs`, `Scripts/UI/GameMenu.cs`, `MapTabView.cs`, `Scripts/Editor/WebBuildTool.cs`, зміни в `HudController`, `TabletController`, `CameraFollow`, `GameBootstrap`, `UI/Game/*`, `UI/Tablet/*`, `Tests/EditMode/01AudioMenuTests.cs` | фінальні покращення 15.11: звук, згенерований кодом (без чужих аудіофайлів), меню паузи, довідка «Як грати», гарячі клавіші, поворот камери Q/E, вкладка MAP з планом поверхні, скрипт веб-збірки, тести; тексти EN/UK (Claude) | 01 |
| `Assets/WebGLTemplates/MoonBase/`, `Assets/link.xml`, `Settings/GameRuntimeTheme.tss`, `tools/serve_web.ps1`, `docs/WEB_BUILD.md`, `docs/CREDITS.md`, зміни в `StormBanner`, `MoonHop`, `Packages/manifest.json` | веб-збірка: космічний екран завантаження, збереження колайдерів і шрифт з кирилицею у WebGL, локальний сервер для перевірки, інструкція; знайдено й прибрано пакет, що ламав збірку WebGL; напис бурі не закриває планшет; кроки інженера звуком (Claude) | 01 |
