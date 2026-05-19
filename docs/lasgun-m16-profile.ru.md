# Lasgun -> M16A4 test profile

Цель: проверить, что пользователь может загрузить обычный интернет-архив с моделью оружия, а студия сама найдёт вложенную модель, подготовит материалы, cooked SkeletalMesh и подставит его в подходящий SCUM weapon asset.

## Архив

`C:\Users\User\Downloads\lasgun.zip`

Структура архива:

- `textures/*.png` - большие текстуры верхнего уровня.
- `source/textures.rar` - вложенный RAR.
- внутри `textures.rar`: `lasgun.obj` и набор сжатых текстур.

Добавленная поддержка:

- zip/rar/7z архивы как raw model packages;
- вложенные rar/7z внутри zip;
- распаковка через системный `tar.exe`/bsdtar или `7z.exe`;
- автогенерация отсутствующего `lasgun.mtl` для OBJ по текстурам `Base_Color`, `Normal`, `Roughness`, `Metallic`, `AO`.

## Выбор целевого предмета

Выбран `weapon_m16a4.uasset`:

`scum/content/conz_files/items/weapons/ranged_weapons/weapon_m16a4.uasset`

Причина: lasgun - длинная винтовка, поэтому M16A4 ближе по хвату, двухручной анимации, aim offsets, magazine/suppressor/rail sockets и holster profile, чем пистолет, дробовик или короткий SMG.

Ключевое поле:

- `e:18/p:1` - SkeletalMesh, оригинал `SK_M16A4`.

## Cook result

Создан cooked asset:

`SCUM/Content/SMS/R/SK_weapon_m16a4_lasgun/SK_weapon_m16a4_lasgun.uasset`

Проверки:

- raw import нашёл `lasgun.obj` внутри вложенного RAR;
- bounds модели: `0.894882 x 0.24825 x 0.072238`;
- создан `lasgun.mtl`;
- UE4.27 cook успешен;
- Skeleton и PhysicsAsset цели M16A4 привязаны к cooked SkeletalMesh;
- combined structural pak собран без установки в игру:
  `D:\SCUM_MOD_FACTORY\builds\ScumPakWizard\mods\pakchunk99-WindowsNoEditor_18_P.pak`;
- `UnrealPak -Test` завершился с кодом 0.

## Осторожность по Duster

Lasgun можно проверять как обычный weapon replacement. Duster/Valkyrie нельзя ставить в игру старым способом с hidden tiny attachment meshes: именно он ломал vehicle material slot/query/physics contract и приводил к fatal. Для Duster пока допустима только структурная сборка без установки или следующий безопасный шаг: отдельный module planner для chassis/wing/engine/seat/query proxies.
