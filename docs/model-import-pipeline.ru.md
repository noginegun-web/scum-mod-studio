# Пайплайн импорта моделей и новых ассетов

Цель: пользователь загружает модель из интернета, выбирает игровой ассет SCUM, а студия готовит модель, подгоняет её и подключает в PAK-мод.

## Ключевой вывод по UE4 mod/dev kits

UE4-игра не загружает сырой FBX/OBJ/GLTF как игровой StaticMesh или SkeletalMesh. Модель должна пройти импорт в Unreal Editor/SDK и cook под Windows, после чего появляется набор cooked файлов `.uasset`, `.uexp` и иногда `.ubulk`.

Правильная схема как у dev kits:

1. Принять raw модель: `.fbx`, `.obj`, `.gltf`, `.glb`, `.dae`, `.stl`, `.ply`, `.blend`, `.zip`, `.rar`, `.7z`.
2. Проанализировать её: габариты, pivot, материалы, текстуры, наличие skeleton/socket.
3. Выбрать целевой ассет игры: оружие, транспорт, здание, предмет, эффект.
4. Построить fit-профиль: масштаб, смещение, поворот, collision, sockets, material slots.
5. Импортировать raw модель в UE4.27 automation project через `AssetImportTask` или `ImportAssets` commandlet.
6. Cook проекта под WindowsNoEditor.
7. Забрать cooked файлы и добавить их в PAK.
8. Заменить ссылку в игровом ассете SCUM на новый cooked StaticMesh/SkeletalMesh/Material/Texture.

## Что уже реализовано

- Импорт cooked visual assets.
- Принятие raw моделей `.fbx/.obj/.glb/.gltf/.dae/.stl/.ply/.blend` и zip/rar/7z-пакетов с моделью/текстурами.
- Поддержка вложенных zip/rar/7z-пакетов и сохранение sidecar-файлов рядом с исходной моделью.
- Анализ габаритов raw `.obj`, `.gltf`, `.glb`, ASCII `.fbx`, `.dae`, `.stl`, `.ply`.
- Для `.dae` дополнительно читаются отдельные geometry parts: программа показывает роль-кандидат (`hull`, `engine`, `wing`, `weapon`, `seat-interior`, `query-proxy`) и подсказку по модульному разбору.
- Мастер замены модели выбранного ассета.
- Fit-настройки UI: масштаб, смещение X/Y/Z, Pitch/Yaw/Roll.
- Применение offset/rotation к игровым полям, если они есть в ассете.
- Автоматический raw -> cooked pipeline: программа запускает Blender для конвертации в FBX, создаёт временный UE4.27 automation project, импортирует модель через Python, cook-ит WindowsNoEditor, копирует cooked файлы в каталог пользовательских ассетов и возвращает `suggestedEdit` для выбранного visual-поля.
- Vehicle profile endpoint/UI для транспортных ассетов: дерево связанных Blueprint/VehicleAttachment ассетов, ключевые mesh/material/socket/mount поля, список обязательных сокетов и предупреждения по query/collision/skeletal контракту.
- Vehicle module plan endpoint/UI: связывает raw parts скачанной модели с модулями транспорта и помечает каждый шаг как `candidate-static-visual`, `needs-split`, `needs-optimization`, `blocked-query-proxy`, `blocked-skeletal-contract` или `mount-slot-plan`.
- Vehicle module cook endpoint/UI: готовит отдельный StaticMesh-модуль из выбранных raw parts, применяет triangle budget через Blender decimate, auto-fit по target bounds, выбирает cooked материал из оригинального target mesh (`Static Materials / Material Interface`) перед fallback-поиском и возвращает `suggestedEdit` для автоматического staged edit target attachment.
- Vehicle module batch cook endpoint/UI: готовит до 8 безопасных StaticMesh-кандидатов из текущего плана одним действием и возвращает список staged edits; unsafe skeletal/query/split-модули остаются заблокированными.

## Следующие слои

- Автосоздание sockets: `Grip`, holster sockets, attachment sockets.
- Автоподбор масштаба по габаритам целевого ассета.
- Расширенная генерация collision для StaticMesh.
- Раздельные профили для оружия, транспорта, зданий, эффектов и UI-иконок.

## Отдельное правило для транспорта SCUM

Транспортные ассеты нельзя вести через тот же быстрый путь, что одиночный предмет или оружейный mesh. `BPC_Kinglet_Duster` использует `AAirplane`, `VehicleAttachment`, `VehicleAttachmentMeshSetup`, mount slots и несколько SkeletalMesh/StaticMesh модулей. Если заменить только визуальные поля и спрятать оригинальные модули tiny-mesh ассетами, игра продолжает выполнять код самолёта, создавать dynamic material instances, проверять query meshes, damage regions и посадочные сокеты. Несовпадение material slot/skeleton/query contract уже привело к `EXCEPTION_ACCESS_VIOLATION` после спавна.

Минимальный безопасный профиль транспорта:

1. Считать дерево attachment-модулей и оставить валидными `MeshSetup`, `QueryMeshSetup`, `_slots`, `_associatedCollisionShapes`, `_regularMaterials` и `_serviceModeMaterial`.
2. Не заменять `QueryMeshSetup` интернет-моделью; для него нужен отдельный простой collision/query proxy.
3. Не заменять транспортный SkeletalMesh без совпадения skeleton, sockets и animation blueprint. Для Duster критичны `Root`, `s_Driver`, `s_Passenger`, `s_Propeller`, `s_Wings_Left`, `s_Wings_Right`, `s_Stabilizer_Left`, `s_Stabilizer_Right`, `s_Rudder`, колёсные и weapon sockets.
4. Для материалов транспорта использовать cooked Material/MaterialInstance из игры, пока не будет собран shader-safe material cook с корректной библиотекой shader map.
5. Сиденья менять через mount slot ассеты: `MountSocketName`, `MountedTransformCorrection`, `ExternalMountPaths`, `InternalMountPaths`, idle/enter/exit animations и blendspaces.

Для Valkyrie первый рабочий профиль должен разбить модель на роли: корпус/chassis, крылья, хвост/руль, двигатели/пропеллерный proxy, landing gear/wheels, weapon mounts, cockpit/seat sockets и отдельные query proxies. Оригинальные критические attachment slots лучше сохранять, пока новая collision/physics схема не проверена в игре.

Практическое правило планировщика: если raw-часть тяжелее безопасного порога или представляет собой набор из нескольких деталей, она не считается готовым auto-cook модулем. Сначала нужен split/decimate/LOD в Blender и только потом cook отдельного StaticMesh. Это особенно важно для Valkyrie: engine/rotor/bolter/hull группы слишком тяжёлые, чтобы напрямую подставлять их в Duster attachment.

Материальная политика для любого ассета: если целевой mesh найден, программа сначала читает его собственные cooked material slots и наследует такой же `Material`/`MaterialInstanceConstant`. Только если target mesh не удалось прочитать, используется общий поиск по названию модуля. Это снижает риск `uncooked shader map`, invalid material index и визуально чужих материалов.

## Основные источники

- Pak patching and cooked asset replacement: https://buckminsterfullerene02.github.io/dev-guide/Basis/PakPatching.html
- Community modkit reconstruction notes: https://buckminsterfullerene02.github.io/dev-guide/ModSupport/ModKits/CommunityModkits.html
- UE4SS UHT/CXX dump workflow: https://docs.ue4ss.com/dev/guides/generating-uht-compatible-headers.html
- UE4SS Lua hooks and runtime asset probing: https://docs.ue4ss.com/lua-api/global-functions/registerhook.html
- UAssetAPI for `.uasset/.uexp` read/write: https://atenfyr.github.io/UAssetAPI/guide/basic.html
- CUE4Parse/FModel-style cooked asset parsing: https://github.com/FabianFG/CUE4Parse
