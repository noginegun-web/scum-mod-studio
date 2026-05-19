# Vehicle profile: Duster -> Valkyrie

Цель профиля: заменить внешний вид `BPC_Kinglet_Duster` на Valkyrie без нарушения runtime-контракта SCUM. Это не одиночная mesh-подмена, а мини-DevKit слой поверх cooked asset modding.

## Почему прошлый pak упал

После спавна `BPC_Kinglet_Duster` лог показал ошибку `CreateAndSetMaterialInstanceDynamic`: material index `0` invalid на модуле `BPC_Kinglet_Duster_Legs_Front`, затем `EXCEPTION_ACCESS_VIOLATION` в GameThread. Это означает, что игра продолжила выполнять код модульного самолёта, но один из ожидаемых mesh/material contracts был заменён tiny/dummy ассетом без валидного слота.

Вторая проблема: DAE-материалы `SMS_DAE_*` дали `Tried to access an uncooked shader map ID in a cooked application`. Для транспорта нельзя полагаться на материалы из скачанной модели; пока безопаснее привязывать existing cooked `.mi` SCUM.

## Обязательный контракт Duster

- Root actor: `BPC_Kinglet_Duster`, класс `AAirplane`.
- Основной visual: `SK_Kinglet`, поле `e:35/p:1`.
- Anim class: `ABP_Kinglet_Duster`, поле `e:35/p:0`.
- Physics asset: `PA_Kinglet_Duster_2_final_version_really_A`, поле `e:35/p:2`.
- Propeller mesh/material: `SM_Plane_01_Propeller`, `MI_PropellerMotionBlur`.
- Chassis slot: `BPC_Kinglet_Duster_Chassis`.
- Seat tags: `MountSlot.Kinglet.Pilot`, `MountSlot.Kinglet.Passenger`.
- Weapon sockets: `s_Weapon_Right`, `s_Weapon_Left`.
- Propeller location: X `329`, Y `0`, Z `158`.

Главные sockets из `SK_Kinglet`/skeleton: `Root`, `s_Driver`, `s_Passenger`, `s_Propeller`, `s_Wing_Central`, `s_Wings_Left`, `s_Wings_Right`, `s_Stabilizer_Left`, `s_Stabilizer_Right`, `s_Rudder`, `s_Legs_Front`, `s_Leg_Rear`, `s_Wheel_Left`, `s_Wheel_Right`, `s_Wheel_Rear`, `s_Weapon_Left`, `s_Weapon_Right`, `s_Weapon_Mount_Left`, `s_Weapon_Mount_Right`.

## Attachment tree

- `BPC_Kinglet_Duster_Chassis`
- `BPC_Kinglet_Duster_Engine`
- `BPC_Kinglet_Duster_Legs_Front`
- `BPC_Kinglet_Duster_Leg_Back`
- `BPC_Kinglet_Duster_Wheel_FrontLeft`
- `BPC_Kinglet_Duster_Wheel_FrontRight`
- `BPC_Kinglet_Duster_Wheel_Back`
- `BPC_Kinglet_Duster_Strut_Left`
- `BPC_Kinglet_Duster_Strut_Right`
- `BPC_Kinglet_Duster_Wing_Central`
- `BPC_Kinglet_Duster_Wings_Left`
- `BPC_Kinglet_Duster_Wings_Right`
- `BPC_Kinglet_Duster_Wings_Airfoil_Left`
- `BPC_Kinglet_Duster_Wings_Airfoil_Right`
- `BPC_Kinglet_Duster_Wing_Aileron_LowerLeft`
- `BPC_Kinglet_Duster_Wing_Aileron_LowerRight`
- `BPC_Kinglet_Duster_Wing_Aileron_UpperLeft`
- `BPC_Kinglet_Duster_Wing_Aileron_UpperRight`
- `BPC_Kinglet_Duster_Stabilizer_Left`
- `BPC_Kinglet_Duster_Stabilizer_Right`
- `BPC_Kinglet_Duster_Elevator_Left`
- `BPC_Kinglet_Duster_Elevator_Right`
- `BPC_Kinglet_Duster_Propeller`
- `BPC_Kinglet_Duster_Rudder`
- `BPC_Kinglet_Weapon_Mount_Left`
- `BPC_Kinglet_Weapon_Mount_Right`

У каждого модуля нужно сохранить или корректно пересобрать `MeshSetup`, `QueryMeshSetup`, slots, material slots и damage/destruction settings.

## Seat profile

Driver:

- Mount socket: `s_Driver`.
- Idle: `Player_Cropduster_Driver_Idle`.
- Anim instance: `ABP_Prisoner_Airplane`.
- Transform correction: X `0`, Y `-12`, Z `0`.
- Blendspace: `BP_Crop_Duster_Driver`.

Passenger:

- Mount socket: `s_Passenger`.
- Idle: `Player_Cropduster_Passenger_Idle`.
- Anim instance: `ABP_Prisoner_VehicleBase`.
- Transform correction: X `0`, Y `-12`, Z `-5`.
- Blendspace: `BP_Crop_Duster_Passenger`.

Для Valkyrie посадку надо переносить не заменой prisoner skeleton, а правкой mount slot ассетов: `MountSocketName`, `MountedTransformCorrection`, external/internal path destinations и enter/exit animations. Новые анимации возможны только отдельным cook-профилем под prisoner skeleton; безопасный первый этап - переиспользовать Cropduster/Mariner анимации и точно выставить сокеты.

## Разбиение Valkyrie

Исходный DAE содержит 13 геометрий и примерно 97k triangles. Предлагаемое соответствие:

- `Hull*`, `Interior*`, `Dec*` -> chassis/visible hull.
- `Ins+eng*`, `Cylinder*` -> engine/propeller or VTOL nacelle visual modules.
- `Bolter*` -> weapon mounts.
- Крылья и хвост нужно отделить геометрически по bounds и/или вручную в Blender, чтобы они легли в `Wings_Left/Right`, `Stabilizer_Left/Right`, `Rudder`, `Elevator`, `Aileron` slots.
- Query/collision meshes должны быть отдельными упрощёнными proxy, а не полной Valkyrie-геометрией.

Программа теперь показывает анализ DAE parts сразу после импорта архива. Это не финальная автосегментация, а вход для vehicle planner: какие части можно использовать как chassis, engine/rotor, weapon mounts, cockpit/seat area и какие части надо упрощать в query/collision proxy.

## Новые проверки в студии

- `/api/modding/vehicle-profile?assetId=...` строит профиль связанных транспортных ассетов и показывает модули, sockets, materials, query/static/skeletal поля.
- `/api/modding/vehicle-module-plan?assetId=...&rawSourceRelativePath=...` строит план разборки конкретной raw-модели под выбранный транспорт. Для Duster + Valkyrie он показывает все найденные attachments, связывает их с raw parts (`Hull`, `Ins+eng`, `Cylinder`, `Bolter`) и блокирует unsafe шаги.
- `/api/modding/vehicle-module-cook` готовит один безопасный StaticMesh-модуль из плана: фильтрует DAE по именам raw parts, применяет target triangle budget, auto-fit, материал оригинального target mesh и возвращает `suggestedEdit`, который UI кладёт в staged edits target attachment.
- `/api/modding/vehicle-module-cook-batch` готовит до 8 безопасных StaticMesh-кандидатов из плана одним вызовом и возвращает staged edits для каждого успешного attachment. Skeletal/query/split-модули не трогает.
- UI блокирует опасную транспортную raw→skeletal замену.
- UI требует режим материала `Из игры / импортированный .mi` для raw cook транспорта.
- DAE импорт через Blender выключает импорт интернет-материалов, если выбран игровой `.mi`, чтобы не повторять ошибку `uncooked shader map`.
- DAE-анализ теперь использует имена `node/instance_geometry`, а не только технические `geometry id`; поэтому Valkyrie parts отображаются как `Hull_Cube.009`, `Ins+eng...`, `Cylinder...`, `Bolter...`.

Проверенный текущий smoke test: `BPC_Kinglet_Duster_Wing_Central` приготовлен из raw part `Hull.001_Cube.002` как отдельный StaticMesh `SM_bpc_kinglet_duster_wing_central_model_Hull_001_Cube_002`; auto-fit цель `650 cm`, budget `6000` tris, material наследован из оригинального `SM_Kinglet_Wing_Central` как `MI_Plane_01_Body_A`. Pak не устанавливался: это проверка cook/структуры, а не финальный игровой патч. Новая проверка должна также видеть staged edit для поля `Mesh Setup / Mesh / Mesh`, причём для asset-поля используется `/Game/...Asset.Asset`, а не `object:/Game...|StaticMesh`.

## Следующий безопасный pak

1. Не трогать `QueryMeshSetup` и физику оригинала.
2. Не скрывать skeletal attachment-модули tiny meshes.
3. Сначала отделить в Blender один лёгкий static visual module, который планировщик пометит `candidate-static-visual`.
4. Если планировщик пишет `needs-optimization`, сделать split/decimate/LOD и повторить импорт raw-модели.
5. Затем добавить overlay/chassis visual Valkyrie как отдельный валидный StaticMesh module.
6. Только после стабильного spawn переносить seats/sockets и крылья.
7. После каждого pak: `UnrealPak -Test`, проверка списка файлов, запуск игры, spawn, чтение `SCUM.log`.
