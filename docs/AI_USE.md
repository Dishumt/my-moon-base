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
