# SCUM Mod Studio

Пользователю нужны только:
- установленная `SCUM`
- папка с `ScumPakWizard.exe`

Никаких отдельных установок (`.NET`, UE, пакеров) не требуется.

## Что есть в интерфейсе

1. Ползунки модулей:
- `Радиация`
- `Трейдеры`
- `Квесты`
- `Стартовый лут`
- `Крафт`
- `NPC/Орды`

2. Выбор пресетов и ассетов:
- включение модулей
- фильтр ассетов по пресету/поиску
- точечный выбор конкретных файлов ассетов

3. Редактор крафт-планов:
- список предметов из игры (по каталогу ассетов)
- выбор результата и ингредиентов
- добавление рецептов в сборку

4. Сборка:
- выпуск `.pak`
- опционально `zip`
- опционально авто-установка в `SCUM\Content\Paks\mods`
- опционально добор companion-файлов (`.uexp/.ubulk`) из игры

## Запуск

Просто запустить:

```text
ScumPakWizard.exe
```

Студия открывается в браузере на:

```text
http://127.0.0.1:49321
```

## Сборка релиза (для разработчика)

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish_release.ps1
```

Скрипт:
- публикует self-contained `exe`
- добавляет `UnrealPak` в `Engine\Binaries\Win64`
- добавляет нужные `Engine\Config`
- собирает zip релиза

## Сборка релиза с автообновлением через GitHub

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish_update_release.ps1 `
  -RepoUrl "https://github.com/OWNER/REPO"
```

Если нужно сразу выгрузить релиз в GitHub Releases:

```powershell
$env:GITHUB_TOKEN = "github_token_with_repo_access"
powershell -ExecutionPolicy Bypass -File .\scripts\publish_update_release.ps1 `
  -RepoUrl "https://github.com/OWNER/REPO" `
  -PublishToGithub
```

Что делает этот скрипт:
- собирает self-contained выпуск программы
- добавляет `UnrealPak` и `Engine\Config` как и обычный релиз
- упаковывает установщик и файлы обновления через `Velopack`
- при публикации в GitHub Releases делает основу для автообновления установленной программы

Важно:
- автообновление работает у установленной версии программы, а не у файла из `bin\Debug`
- `RepoUrl` подставляется прямо в выпускной `appsettings.json`, чтобы установленная программа знала, откуда брать обновления
