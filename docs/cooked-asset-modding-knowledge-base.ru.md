# Cooked Asset Modding / mini-DevKit knowledge base

Цель файла: дать агенту и разработчику рабочую базу знаний для SCUM Mod Studio. Это не учебник Unreal Engine, а практический протокол для игр на UE4.27 без официального dev kit, когда есть только cooked файлы, AES key, pak index, UE4SS/Dump7 и возможность cook-ить свои ассеты в легальном UE4.27.

## Главный вывод

Быстрый и качественный путь - не "заменить любую модель любым mesh" одним универсальным патчем. Для cooked UE-игры нужен слой mini-DevKit:

1. Сканировать игру и строить локальную базу контрактов ассетов: классы, компоненты, mesh/material поля, sockets, collision/query meshes, mount slots, audio events, animation classes, physics assets.
2. Для каждого семейства ассетов использовать adapter profile: `VehicleAdapter`, `WeaponAdapter`, `StaticPropAdapter`, `InventoryItemAdapter`, `WearableAdapter`.
3. Raw модель из интернета сначала превращать в набор подготовленных ролей: visual mesh, low-poly query/collision proxy, glass/material slots, optional modules, sockets/seat/grip markers.
4. Cook делать через UE4.27 automation project с правильными путями `/Game/...`, затем заменять ссылки в cooked SCUM ассетах через безопасный edit plan.
5. Не подменять runtime-контракт игры dummy/tiny ассетами, если Blueprint/C++ код SCUM продолжает ожидать material slots, sockets, QueryMeshSetup или PhysicsAsset.

## Источники, которые надо использовать

- [Buckminster Fullerene - Pak Patching](https://buckminsterfullerene02.github.io/dev-guide/Basis/PakPatching.html): практическая схема pak patching, override через PAK, путь `/Game/...`, cooked asset replacement.
- [Buckminster Fullerene - Community Modkits](https://buckminsterfullerene02.github.io/dev-guide/ModSupport/ModKits/CommunityModkits.html): как сообщества делают devkit-подобные проекты без исходников игры.
- [UE4SS documentation](https://docs.ue4ss.com/): runtime hooks, Lua, UObject/FName tooling, CXX/UHT header generation.
- [UE4SS RegisterHook](https://docs.ue4ss.com/lua-api/global-functions/registerhook.html): как проверять поведение ассетов в рантайме, ловить функции и быстро валидировать гипотезы без новой сборки PAK на каждый микрошаг.
- [UE4SS UHT-compatible headers](https://docs.ue4ss.com/dev/guides/generating-uht-compatible-headers.html): генерация заголовков и восстановление классов/структур для mini-DevKit.
- [CUE4Parse](https://github.com/FabianFG/CUE4Parse): библиотека для чтения cooked UE packages, encrypted paks, IOStore, mappings и экспортируемых данных.
- [FModel](https://github.com/4sval/FModel): практический viewer/exporter поверх CUE4Parse для pak index, AES, ассетов, текстур, моделей.
- [UAssetAPI](https://github.com/atenfyr/UAssetAPI) и [UAssetAPI docs](https://atenfyr.github.io/UAssetAPI/): чтение/запись `.uasset/.uexp`, точечные правки свойств и references.
- [UAssetGUI](https://github.com/atenfyr/UAssetGUI): ручная проверка cooked properties и сравнение с тем, что делает наша программа.
- [UE Viewer / UModel](https://www.gildor.org/en/projects/umodel): быстрый экспорт meshes/skeletons/textures из UE-игр, особенно для проверки пропорций и sockets.
- [DRG Community Modkit](https://github.com/DRG-Modding/Community-Modkit): пример community-maintained Unreal modkit, который работает через реконструированный проект и шаблоны, а не через исходники игры.
- [Satisfactory Mod Loader Starter Project](https://github.com/satisfactorymodding/SatisfactoryModLoader): пример индустриально зрелого community pipeline вокруг UE-проекта, headers, generated data и стабильной сборки модов.
- [UEAssetToolkitGenerator](https://github.com/LongerWarrior/UEAssetToolkitGenerator): пример генерации toolkit-данных/описаний ассетов из cooked проекта.

Поисковые запросы для продолжения исследований:

- `site:github.com UE4SS RegisterHook path:*.lua`
- `site:github.com CUE4Parse mappings pak aes uasset`
- `site:github.com UAssetAPI "ObjectPropertyData" "SoftObjectProperty"`
- `"community modkit" "Unreal Engine" "cooked assets"`
- `"Pak Patching" "Unreal Engine" "Modding"`
- `"UHT compatible headers" UE4SS dump`
- `"Unreal Engine" "no mod kit" "asset replacement" ".pak"`

## Локальный источник истины для SCUM

Интернет нужен для методологии, но конкретные решения по SCUM нельзя брать из чужой игры. Перед изменением ассета агент обязан собрать локальный контракт:

1. `pak index`: все ассеты рядом с target path, companion `.uasset/.uexp/.ubulk`.
2. `UAssetAPI` export/import graph: поля target ассета, object/soft references, component exports.
3. `Dump7/UE4SS CXXHeaderDump`: класс, наследование, имена свойств, типы массивов/структур.
4. `FModel/UModel`: visual mesh, skeleton, material slots, sockets, texture names, bounds.
5. `UE4SS runtime probe`: какие компоненты реально создаются, какие функции вызываются при spawn/equip/enter/start engine.
6. Game log: warnings перед fatal error почти всегда указывают на нарушенный контракт: invalid material index, missing skeleton, bad physics, missing class, uncooked shader map.

## Adapter profiles

### VehicleAdapter

SCUM транспорт - не один mesh. Минимальный контракт:

- root Blueprint class and parent class;
- root visual component: SkeletalMesh or StaticMesh;
- `AnimClass`, `PhysicsAsset`, skeleton and required sockets;
- attachment tree: chassis, engine, wheels, wings, weapons, doors, cargo, damage modules;
- `MeshSetup` and `QueryMeshSetup` for each `VehicleAttachment`;
- low-poly query/collision proxy separate from high-poly visual;
- `MountSlot` assets: mount socket, transform correction, external/internal paths, enter/exit/idle animations;
- sound fields: Wwise `AkAudioEvent` for engine start/stop/run/impact;
- material slots, regular/service/damage materials and dynamic material assumptions;
- bounds and origin relative to vehicle physics root.

Правило: для транспорта нельзя просто скрыть оригинальный visual и повесить новый huge StaticMesh поверх, если collision/query/mount/audio остаются от старой машины. Это даёт ровно текущие симптомы: проход сквозь видимую модель, блокировка в пустом месте, падение под землю, прыжки при spawn, неверная посадка, старый звук двигателя.

Для Valkyrie -> Duster:

- visual hull должен быть отдельным cooked StaticMesh;
- query mesh должен быть простым low-poly hull/box proxy, а не 97k-triangle DAE;
- модульные части Duster надо либо сопоставить с ролями Valkyrie, либо явно скрыть только после сохранения их runtime material/query contract;
- cockpit seat correction должен идти через `MountSlot`, а не через перемещение персонажа после факта;
- glass нужен отдельным material slot с прозрачным cooked material;
- окраска должна быть через cooked material/material instance или импортированный material, для которого гарантирован cooked shader map;
- engine audio надо менять на существующие Wwise events SCUM, если новые Wwise banks не добавляются.

### WeaponAdapter

Оружие - это отдельный контракт:

- SkeletalMesh или StaticMesh target;
- skeleton/root bone compatibility;
- physics asset, bounds, sockets;
- grip/hand correction assets;
- first person and third person animation profiles;
- magazine/barrel/rail attachments;
- material slots with two-sided fallback for thin imported geometry;
- pickup/world mesh and in-hand mesh могут быть разными.

Симптомы lasgun/M16:

- пустая середина и исчезающие плоскости часто означают backface culling: нужен two-sided material или исправление normals/thickness;
- белая модель означает потерянные texture/material links или material cook fallback;
- неправильный хват означает несовпадение grip correction/socket/first-person profile, а не только масштаб модели.

### StaticPropAdapter

Для зданий/сундуков/предметов без управления:

- visual mesh;
- collision complexity/proxy;
- pivot/origin and ground contact;
- material slots;
- interaction component/query volume;
- optional destruction/damage data.

## Mini-DevKit architecture for SCUM Mod Studio

### 1. Contract scanner

Нужен автоматический сканер, который строит JSON contracts:

```json
{
  "assetId": "game::scum/content/conz_files/vehicles/airplane/duster/bpc_kinglet_duster.uasset",
  "adapter": "vehicle",
  "class": "BPC_Kinglet_Duster_C",
  "visualFields": [],
  "attachments": [],
  "queryFields": [],
  "mountSlots": [],
  "audioEvents": [],
  "requiredSockets": [],
  "materialSlotContracts": []
}
```

Контракты должны жить в cache/diagnostics и переиспользоваться UI. Пользователь видит простые слова, агент и backend работают с точными paths/fields.

### 2. Raw model analyzer

Для архива из интернета:

- поддержать nested zip/rar/7z;
- сохранить sidecar textures/material files рядом с моделью;
- определить главный файл (`.dae/.fbx/.obj/.glb/.gltf/.stl/.ply/.blend`);
- вытащить parts, bounds, triangle count, material names, texture names;
- назначить semantic roles: `hull`, `glass`, `engine`, `wing`, `weapon`, `magazine`, `seat`, `collision-proxy`;
- если роль неясна, UI показывает пользователю один понятный выбор, но не требует Blender/UE.

### 3. Cook recipes

Cook recipe должен быть отдельным от edit recipe:

- `VisualStaticMeshCookRecipe`: масштаб, pivot, materials, LOD, Nanite off, collision off/query separate.
- `QueryProxyCookRecipe`: decimate/convex/box hull, low triangle budget, no visible material dependency.
- `WeaponSkeletalCookRecipe`: root weights, skeleton compatibility, bounds, sockets.
- `MaterialCookRecipe`: two-sided flag, texture binding, glass material, fallback colored material.
- `IconTextureRecipe`: optional inventory icon.

### 4. Patch recipes

Patch recipe:

- replaces object/soft references only in safe fields;
- rejects internal component references unless explicitly exported inside same Blueprint;
- changes mount/audio/material fields as a group;
- stages all companion files in PAK;
- creates a rollback manifest for the generated PAK.

## Правила от текущих ошибок

1. Если можно проходить через видимую модель, а невидимое место блокирует игрока, значит visual и collision/query живут в разных ассетах. Нужно cook-ить отдельный `QueryProxy` и патчить `QueryMeshSetup`.
2. Если транспорт прыгает, проваливается или взрывается после spawn, сначала проверять physics root, collision primitive, query mesh bounds, ground contact and mass/center assumptions.
3. Если персонаж садится в центр, править `MountSlot` contract: socket, `MountedTransformCorrection`, external/internal mount paths, enter/exit montage.
4. Если модель белая, не продолжать тесты управления. Сначала восстановить material slots: base color, normal, roughness, metallic, glass, two-sided.
5. Если weapon выглядит пустым изнутри, проверить normals/backface culling и включить two-sided material для импортированных материалов.
6. Если звук старый, искать Wwise `AkAudioEvent` рядом в pak index и патчить audio object references; новые звуки требуют отдельного Wwise/bank pipeline.
7. Если fatal error содержит `CreateAndSetMaterialInstanceDynamic` или `invalid material index`, нельзя скрывать/заменять модуль mesh без сохранения ожидаемого material slot count.

## Что реализовывать дальше в программе

Приоритет 1:

- автоматический `ContractScanner` для vehicle/weapon target;
- material hardening в UE import script: two-sided для импортированных материалов, semantic glass material, dark fallback instead of white;
- обязательный `QueryProxy` cook для транспорта;
- UI "масштаб / посадка / смещение cockpit" не как ручная игрушка, а как overrides в adapter profile;
- запрет финального PAK, если adapter contract имеет unresolved critical items.

Приоритет 2:

- генерация low-poly collision/query proxy из hull bounds;
- visual/material preview diagnostics;
- UE4SS runtime probe script templates для mount slots, components and audio events;
- сохранение known-good recipes для SCUM ассетов.

Приоритет 3:

- модульная разборка транспорта по semantic roles;
- кастомные анимации посадки через prisoner skeleton cook;
- Wwise audio import pipeline, если потребуется не только замена на существующие events.

## Как агент должен работать с этим файлом

Перед каждой задачей по "заменить модель":

1. Определи adapter family: vehicle/weapon/static prop/inventory/wearable.
2. Построй или прочитай contract target ассета.
3. Проанализируй raw archive/model и semantic parts.
4. Создай cook recipes и patch recipe.
5. Проверь build.
6. Только после структурной проверки ставь PAK в SCUM `mods/paks`.

Нельзя считать задачу выполненной, если готов только visible mesh. Для пользователя программа должна выглядеть как "скинул архив -> выбрал ассет -> получил рабочий мод", но внутри это всегда contract-driven mini-DevKit pipeline.
