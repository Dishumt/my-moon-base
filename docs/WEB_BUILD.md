<!-- AI-assisted: written with Claude. See docs/AI_USE.md -->
# Веб-збірка, перевірка і itch.io

*Власник: техлід (роль 01). Збірка займає ≈ 15 хвилин, тож не лишай її на останню годину.*

## 1. Зібрати

Один із двох способів (результат однаковий: папка `Builds/Web`, її немає в git):

- **У Unity:** меню **Moon → Збірка для браузера (WebGL)**. Вона збирає за профілем «Web - Desktop - Release» (Gzip/Brotli + Decompression Fallback, шаблон `MoonBase`).
- **Без вікна Unity** (редактор закритий), у PowerShell:
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe" -batchmode -nographics -projectPath "<папка репозиторію>" -buildTarget WebGL -executeMethod WebBuildTool.Build -logFile build.log
  ```
  Наприкінці в `build.log` буде рядок `WebBuildTool: Succeeded …`.

## 2. Перевірити в браузері

`index.html` з файлу не відкриється (браузер не читає .wasm). Запусти маленький сервер:

```powershell
powershell -ExecutionPolicy Bypass -File tools\serve_web.ps1
```

і відкрий **http://localhost:8085/** у Chrome або Edge. Що перевірити за 2 хвилини:
1. Меню з написом «Space weather today · LIVE». Кнопка **EN / UK**: українські тексти мають бути видно.
2. **Demo for judges** → з'являється «How to play» → «Got it» → гра йде, звук є (кнопка SOUND у правому верхньому куті).
3. **Tab** → вкладки MAP, BUILD, POWER, CREW, SUPPLY, COMMS. **Esc** → меню паузи.
4. Консоль браузера (F12) без червоних помилок.

## 3. Завантажити на itch.io

1. Запакуй **вміст** папки `Builds/Web` в zip: `index.html` має лежати в корені архіву, а не в підпапці.
2. itch.io → Edit game → Kind of project = **HTML** → завантаж zip → ✔ *This file will be played in the browser*.
3. Viewport size **1280 × 720**, ✔ Fullscreen button. Сторінка Public.
4. Відкрий гру в **вікні інкогніто** (Chrome і Edge) і повтори пункти 1–3 з розділу «Перевірити».
5. Текст для сторінки і форми подачі — у [SUBMISSION.md](SUBMISSION.md).

## Відомі пастки

- **Пакет `com.unity.ai.inference` не додавати.** Він тягне `com.unity.dt.app-ui`, а той має нативну бібліотеку, яку WebGL-збірка не може злінкувати (`undefined symbol: _embind_register_class`). Збірка падає після 15 хвилин. Гра цим пакетом не користується. Якщо Unity сам повернув його в `Packages/manifest.json` (так було в коміті «Bugs fix»), видали рядок.
- **`Assets/link.xml` не видаляти.** Без нього у веб-збірці вирізаються `CapsuleCollider` і `SphereCollider`: у консолі «Can't add component because class 'CapsuleCollider' doesn't exist!», а в батареї й щогли антени немає зіткнень.
- **Шрифт Inter** (`Art/Fonts`, підключений у `Settings/GameRuntimeTheme.tss`): у веб-збірці немає системних шрифтів, а стандартний шрифт Unity не має кирилиці.
- Налаштування Web є у **двох місцях**: `ProjectSettings` і профіль `Assets/Settings/Build Profiles/Web - Desktop - Release.asset` (там своя копія, зокрема `webGLTemplate`, ім'я гри й стиснення). Міняєш одне — зміни й друге.
- Збірка через Unity сама дописує в `ProjectSettings/ProjectSettings.asset`, `GraphicsSettings.asset`, `Assets/Settings/Mobile_RPAsset.asset` і `UniversalRenderPipelineGlobalSettings.asset` дрібні службові рядки. Їх не треба комітити.
