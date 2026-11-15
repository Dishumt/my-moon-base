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
