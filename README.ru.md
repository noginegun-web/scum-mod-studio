# SCUM Mod Studio

Пользователю нужны только:
- установленная `SCUM`
- папка с `ScumPakWizard.exe`

Никаких отдельных установок (`.NET`, UE, пакеров) не требуется.
Релизная папка уже содержит нужные инструменты упаковки в `Engine\Binaries\Win64`.

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
- настройки выбранного ассета загружаются только после клика по нему, чтобы список открывался быстрее
- расширенный каталог предметов: вложения оружия, части оружия, ближнее оружие, одежда/рюкзаки, детали транспорта и еда

3. Редактор крафт-планов:
- список предметов из игры (по каталогу ассетов)
- выбор результата и ингредиентов
- добавление рецептов в сборку

4. Сборка:
- выпуск `.pak`
- опционально `zip`
- опционально авто-установка в `SCUM\Content\Paks\mods`
- опционально добор companion-файлов (`.uexp/.ubulk`) из игры

5. Пользовательские визуальные ассеты:
- импорт cooked UE 4.27 Windows файлов `.uasset/.uexp/.ubulk/.uptnl`
- импорт cooked-папки `Content` с сохранением структуры `/Game/...`
- импорт raw моделей `.fbx/.obj/.glb/.gltf/.dae/.stl/.ply/.blend` и zip/rar/7z-пакетов с моделью/текстурами через кнопку подготовки в UE4
- анализ частей `.dae` модели для будущей автосборки модульного транспорта
- план модулей транспорта: программа сопоставляет части raw-модели с `VehicleAttachment` ассетами и помечает безопасные/опасные шаги
- подготовка отдельного модуля из плана или batch-подготовка всех безопасных StaticMesh-кандидатов: фильтр raw parts, упрощение под triangle budget, auto-fit, наследование cooked материала оригинального mesh и автоматическое добавление staged-правки в нужный attachment
- обычный raw→cooked cook сразу подставляет cooked asset в выбранное visual-поле и сохраняет изменение в staged мод
- выбор своих Static Mesh, Skeletal Mesh, Material/Material Instance и Texture/Icon в найденных visual-полях предметов
- автоматическое добавление выбранных пользовательских ассетов и companion-файлов в итоговый `.pak`

Сырые модели из интернета (`.fbx`, `.glb`, `.gltf`, `.dae`, `.stl`, `.ply`, `.obj`, `.blend`, `.zip`, `.rar`, `.7z`) программа готовит через установленный Unreal Engine 4.27. Blender нужен для `.blend/.obj/.glb/.gltf/.dae/.stl/.ply` и запускается студией в фоне; клиенту не нужно открывать Blender или UE4 руками. Вложенные zip/rar/7z архивы тоже распаковываются, если в системе доступен `tar.exe`/bsdtar или `7z.exe`.

### Транспорт и модульные ассеты

Транспорт SCUM нельзя безопасно менять как один произвольный mesh. Самолёты и машины состоят из `VehicleAttachment`-модулей, query/collision мешей, damage regions, mount slots, сокетов, материалов и анимационных Blueprint-контрактов. Для таких ассетов студия теперь блокирует общий raw→skeletal путь и требует cooked материал из игры для транспортных StaticMesh-полей.

Для полноценной замены вроде `BPC_Kinglet_Duster` -> Valkyrie нужен отдельный vehicle profile: разобрать модель на модули, сохранить обязательные сокеты (`s_Driver`, `s_Passenger`, крылья, колёса, оружие, пропеллер), оставить или пересобрать query/collision proxies, перенести сиденья и mount paths, а материалы привязать к существующим cooked `.mi` SCUM или к корректно приготовленным shader-safe материалам.

В интерфейсе для транспортных ассетов показывается vehicle profile и vehicle module plan: связанные attachment-модули, sockets, материалы, части загруженной модели, предупреждения по query/collision/skeletal контракту и статусы `candidate-static-visual`, `needs-split`, `needs-optimization`, `blocked-query-proxy`, `blocked-skeletal-contract`. Когда безопасный StaticMesh-модуль готовится через кнопку `Подготовить модуль` или batch-кнопку `Подготовить безопасные`, студия сама добавляет staged edit для target attachment и выбирает правильный формат ссылки: `/Game/...Asset.Asset` для asset-полей или `object:/Game...|StaticMesh` для object-полей.

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
  -RepoUrl "https://github.com/noginegun-web/scum-mod-studio-releases"
```

Если нужно сразу выгрузить релиз в GitHub Releases:

```powershell
$env:GITHUB_TOKEN = "github_token_with_repo_access"
powershell -ExecutionPolicy Bypass -File .\scripts\publish_update_release.ps1 `
  -RepoUrl "https://github.com/noginegun-web/scum-mod-studio-releases" `
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
- исходники держим в приватном репозитории, а публичные обновления Velopack публикуем в отдельный бинарный репозиторий `noginegun-web/scum-mod-studio-releases`

Подробная памятка для другого агента: `docs/github-release-flow.ru.md`.
Рабочая база знаний по cooked asset modding и mini-DevKit подходу: `docs/cooked-asset-modding-knowledge-base.ru.md`.
