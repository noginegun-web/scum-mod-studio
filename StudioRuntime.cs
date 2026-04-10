using System.Globalization;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace ScumPakWizard;

internal sealed class StudioRuntime
{
    private const string DefaultAesKeyHex = "0x0B1F4E543FB798EFC5BD861BB405BE7081CD03698EA9BA06469462A3B113CA81";
    private const string DataTableRowAssetPrefix = "datatable-row::";
    private const string ItemSpawningParametersLaneId = "item-spawning-parameters";
    private const string ItemSpawningCooldownGroupsLaneId = "item-spawning-cooldown-groups";
    private const string ItemSpawningParametersTableRelativePath = "scum/content/conz_files/data/tables/items/spawning/itemspawningparameters.uasset";
    private const string ItemSpawningCooldownGroupsTableRelativePath = "scum/content/conz_files/data/tables/items/spawning/itemspawningcooldowngroups.uasset";
    private const string StarterSpawnFieldPrefix = "starter-spawn:";
    private const string RecipeFieldPrefix = "recipe:";
    private const string CraftingUiDataRegistryRelativePath = "scum/content/conz_files/ui/crafting/craftinguidata.uasset";
    private const string ForeignSubstanceAttributeFieldPrefix = "foreign-substance-attribute:";
    private const string WeaponSyntheticFieldPrefix = "weapon-field:";
    private const string SyntheticSideEffectsTargetPath = "synthetic:side-effects";
    private const string SyntheticCraftingUiCategoryRecipesTargetPrefix = "synthetic:crafting-ui-category-recipes:";
    private const string SyntheticCargoMajorSpawnerOptionsTargetPath = "synthetic:cargo-major-spawner-options";
    private const string SyntheticCargoMajorSpawnerPresetOptionsTargetPath = "synthetic:cargo-major-spawner-preset-options";
    private const string SyntheticCargoMinorSpawnerOptionsTargetPath = "synthetic:cargo-minor-spawner-options";
    private const string SyntheticRecipeAllowedTypesTargetPrefix = "synthetic:recipe-allowed-types:";
    private const string QuestManagerAssetId = "game::scum/content/conz_files/quests/questmangerdata.uasset";

    private static readonly StarterSpawnFlagSpec[] StarterSpawnFlags =
    [
        new("tournament", "SpawnLoadout.Parameters.TournamentMode", "Турнирный старт", "Нужно ли выдавать предмет только для турнирного режима."),
        new("aerial", "SpawnLoadout.Parameters.AerialSpawn", "Старт с парашютом", "Выдавать предмет только при воздушном старте."),
        new("cold", "SpawnLoadout.Parameters.ColdClimate", "Холодный климат", "Выдавать предмет только для холодного старта."),
        new("supporter", "SpawnLoadout.Parameters.SupporterPack1", "Supporter Pack 1", "Связать выдачу предмета с Supporter Pack 1."),
        new("deluxe-basic", "SpawnLoadout.Parameters.DigitalDeluxBasicSurvivalPack", "Digital Deluxe: базовый набор", "Связать выдачу с базовым набором Digital Deluxe."),
        new("deluxe-biome", "SpawnLoadout.Parameters.DigitalDeluxeBiomeSuits", "Digital Deluxe: костюмы по биому", "Связать выдачу с биомными костюмами Digital Deluxe.")
    ];

    private static readonly StarterSpawnCharacterSpec[] StarterSpawnCharacters =
    [
        new("regular-male", "Character.Type.RegularMale", "Обычный мужчина"),
        new("regular-female", "Character.Type.RegularFemale", "Обычная женщина"),
        new("danny-trejo", "Character.Type.DannyTrejo", "Danny Trejo"),
        new("luis-moncada", "Character.Type.LuisMoncada", "Luis Moncada"),
        new("raymond-cruz", "Character.Type.RaymondCruz", "Raymond Cruz")
    ];

    private static readonly List<StudioModFieldOptionDto> StarterConditionModeOptions =
    [
        new("ignore", "Не учитывать"),
        new("require", "Обязательно"),
        new("exclude", "Запретить")
    ];

    private static readonly List<StudioModFieldOptionDto> StarterCharacterModeOptions =
    [
        new("ignore", "Не указывать"),
        new("allow", "Разрешить"),
        new("exclude", "Запретить")
    ];

    private static readonly List<StudioModFieldOptionDto> StarterEquipOptions =
    [
        new("EPrisonerItemEquipType::Inventory", "Положить в инвентарь"),
        new("EPrisonerItemEquipType::Hands", "Выдать сразу в руки"),
        new("EPrisonerItemEquipType::Holsters", "Положить в оружейный слот")
    ];

    private static readonly List<StudioModFieldOptionDto> StarterBiomeOptions =
    [
        new("EBiomeType::None", "Любой биом"),
        new("EBiomeType::Cold", "Только холодный биом"),
        new("EBiomeType::Forest", "Только лесной биом"),
        new("EBiomeType::South", "Только южный биом")
    ];

    private static readonly List<StudioModFieldOptionDto> CraftingIngredientPurposeOptions =
    [
        new("ECraftingIngredientPurpose::Material", "Расходный материал"),
        new("ECraftingIngredientPurpose::Tool", "Инструмент")
    ];

    private static readonly List<StudioModFieldOptionDto> CraftingIngredientMixingTypeOptions =
    [
        new("ECraftingIngredientMixingType::NoMixing", "Не смешивать с другими вариантами"),
        new("ECraftingIngredientMixingType::LimitToClass", "Смешивать только внутри одного предмета"),
        new("ECraftingIngredientMixingType::LimitToType", "Смешивать только внутри одной группы"),
        new("ECraftingIngredientMixingType::Unlimited", "Разрешить смешивание без ограничений")
    ];

    private static readonly List<StudioModFieldOptionDto> DispositionOptions =
    [
        new("EDisposition::Neutral", "Нейтральное вещество"),
        new("EDisposition::Good", "Полезное вещество"),
        new("EDisposition::Bad", "Вредное вещество")
    ];

    private static readonly List<StudioModFieldOptionDto> TraderTypeOptions =
    [
        new("ETraderType::None", "Не выбран"),
        new("ETraderType::Armorer", "Оружейник"),
        new("ETraderType::GeneralGoods", "Общие товары"),
        new("ETraderType::Mechanic", "Механик"),
        new("ETraderType::Doctor", "Доктор"),
        new("ETraderType::Harbourmaster", "Лодки и порт"),
        new("ETraderType::Bartender", "Бармен"),
        new("ETraderType::Barber", "Парикмахер"),
        new("ETraderType::TradesEverything", "Скупщик всего")
    ];

    private static readonly List<StudioModFieldOptionDto> FoodCookLevelOptions =
    [
        new("EFoodCookLevel::Raw", "Сырым"),
        new("EFoodCookLevel::Undercooked", "Недоготовленным"),
        new("EFoodCookLevel::Cooked", "Нормально приготовленным"),
        new("EFoodCookLevel::Overcooked", "Передержанным"),
        new("EFoodCookLevel::Burned", "Сгоревшим")
    ];

    private static readonly List<StudioModFieldOptionDto> FoodCookQualityOptions =
    [
        new("EFoodCookQuality::Ruined", "Испорченным"),
        new("EFoodCookQuality::Bad", "Плохим"),
        new("EFoodCookQuality::Poor", "Слабым"),
        new("EFoodCookQuality::Good", "Хорошим"),
        new("EFoodCookQuality::Excellent", "Отличным"),
        new("EFoodCookQuality::Perfect", "Идеальным")
    ];

    private static readonly List<StudioModFieldOptionDto> PlantGrowthStageOptions =
    [
        new("EPlantGrowthStage::None", "Без финальной стадии"),
        new("EPlantGrowthStage::Seeding", "Посев"),
        new("EPlantGrowthStage::Vegetating", "Рост"),
        new("EPlantGrowthStage::Flowering", "Цветение"),
        new("EPlantGrowthStage::Ripening", "Созревание")
    ];

    private static readonly List<StudioModFieldOptionDto> ItemRarityOptions =
    [
        new("EItemRarity::ExtremelyRare", "Чрезвычайно редкий"),
        new("EItemRarity::VeryRare", "Очень редкий"),
        new("EItemRarity::Rare", "Редкий"),
        new("EItemRarity::Uncommon", "Необычный"),
        new("EItemRarity::Common", "Обычный"),
        new("EItemRarity::Abundant", "Частый")
    ];

    private static readonly RecipeSkillLevelSpec[] RecipeSkillLevels =
    [
        new("noskill", "NoSkill", "без навыка"),
        new("basic", "Basic", "на базовом уровне"),
        new("medium", "Medium", "на среднем уровне"),
        new("advanced", "Advanced", "на продвинутом уровне"),
        new("master", "AboveAdvanced", "на уровне мастер")
    ];

    private static readonly string[] CurveCompatibleSideEffectClasses =
    [
        "PrisonerBodyConditionOrSymptomSideEffect_StrengthModifier",
        "PrisonerBodyConditionOrSymptomSideEffect_IntelligenceModifier",
        "PrisonerBodyConditionOrSymptomSideEffect_ConstitutionModifier",
        "PrisonerBodyConditionOrSymptomSideEffect_DexterityModifier",
        "PrisonerBodyConditionOrSymptomSideEffect_MaxStaminaModifier",
        "PrisonerBodyConditionOrSymptomSideEffect_StaminaModifier",
        "PrisonerBodyConditionOrSymptomSideEffect_PerformanceScoreModifier",
        "PrisonerBodyConditionOrSymptomSideEffect_GroundMovementSpeedModifier",
        "PrisonerBodyConditionOrSymptomSideEffect_WaterMovementSpeedModifier",
        "PrisonerBodyConditionOrSymptomSideEffect_MaxMovementPace",
        "PrisonerBodyConditionOrSymptomSideEffect_HealingSpeedModifier",
        "PrisonerBodyConditionOrSymptomSideEffect_ImmuneSystemEfficiencyModifier"
    ];

    private readonly object _sync = new();

    private readonly RuntimePaths _runtimePaths;
    private readonly ScumInstallation _scum;
    private readonly string _unrealPakPath;
    private readonly List<PresetDefinition> _presets;
    private readonly ModBuilder _builder;
    private readonly List<StudioFileEntry> _presetFiles;
    private readonly Dictionary<string, StudioFileEntry> _presetFileById;
    private readonly ItemIconService _iconService;
    private readonly ScumKnowledgeBaseService _knowledgeBase;
    private readonly List<StudioFeatureDto> _features;
    private readonly Dictionary<string, HashSet<string>> _featureAssetIds;
    private readonly Dictionary<string, StudioItemDto> _knownKbItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _gameExtractedSourceByPath = new(StringComparer.OrdinalIgnoreCase);

    private PakIndex? _pakIndexCache;
    private List<StudioItemDto>? _itemsCache;
    private List<StudioAssetDto>? _presetAssetsCache;
    private List<StudioAssetDto>? _gameAssetsCache;
    private List<StudioModCategoryDto>? _modCategoriesCache;
    private List<StudioModAssetDto>? _modAssetsCache;
    private List<StudioModCategoryDto>? _visibleModCategoriesCache;
    private List<StudioModAssetDto>? _visibleModAssetsCache;
    private List<StudioReferenceOptionDto>? _foreignSubstanceAttributeReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _foreignSubstanceReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _bodyEffectSideEffectReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _bodyEffectSymptomReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _encounterCharacterPresetReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _encounterClassReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _cargoDropEncounterClassReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _encounterNpcClassReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _craftingIngredientGroupReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _craftingItemRecipeReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _craftingPlaceableRecipeReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _skillReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _skillBlueprintReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _questGiverReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _questAssetReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _gameEventPrimaryLoadoutReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _gameEventSecondaryLoadoutReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _gameEventTertiaryLoadoutReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _gameEventOutfitLoadoutReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _gameEventMandatoryLoadoutReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _gameEventSupportLoadoutReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _fishSpeciesReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _plantSpeciesReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _plantPestReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _plantDiseaseReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _regularItemSpawnerPresetReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _advancedItemSpawnerPresetReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _advancedItemSpawnerSubpresetReferenceOptionsCache;
    private List<StudioReferenceOptionDto>? _vehicleSpawnPresetReferenceOptionsCache;
    private List<StudioModFieldOptionDto>? _itemSpawningCooldownGroupFieldOptionsCache;
    private List<SideEffectTemplateInfo>? _sideEffectTemplateCache;

    private sealed class GameplayTagQueryNode
    {
        public GameplayTagQueryExprType Type { get; set; }
        public List<int> TagIndices { get; } = [];
        public List<GameplayTagQueryNode> Children { get; } = [];
    }

    private sealed record SideEffectTemplateInfo(
        string ClassName,
        string DisplayName,
        string SourcePath,
        int ExportIndex,
        bool IsBundledTemplate,
        bool SupportsCurveReuse);

    private sealed record ResearchPackageCandidate(
        string FilePath,
        string PackageRoot,
        string RuntimeLayout);

    private sealed record ResearchImportSnapshot(
        List<string> Imports,
        List<string> SoftObjectPaths);

    private enum GameplayTagQueryExprType : byte
    {
        Undefined = 0,
        AnyTagsMatch = 1,
        AllTagsMatch = 2,
        NoTagsMatch = 3,
        AnyExprMatch = 4,
        AllExprMatch = 5,
        NoExprMatch = 6
    }

    private sealed class StarterSpawnConditionModel
    {
        public HashSet<string> RequiredTags { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ForbiddenTags { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AnyTags { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> OriginalTagOrder { get; } = [];
        public string UserDescription { get; set; } = string.Empty;
        public string AutoDescription { get; set; } = string.Empty;
        public bool SupportsSafeRewrite { get; set; }
    }

    private sealed record StarterSpawnFlagSpec(
        string Key,
        string Tag,
        string Label,
        string Description);

    private sealed record StarterSpawnCharacterSpec(
        string Key,
        string Tag,
        string Label);

    private sealed record RecipeSkillLevelSpec(
        string Key,
        string PropertyName,
        string Label);

    private StudioRuntime(
        RuntimePaths runtimePaths,
        ScumInstallation scum,
        string unrealPakPath,
        List<PresetDefinition> presets)
    {
        _runtimePaths = runtimePaths;
        _scum = scum;
        _unrealPakPath = unrealPakPath;
        _presets = presets;
        _builder = new ModBuilder(runtimePaths, scum, unrealPakPath, DefaultAesKeyHex);
        _presetFiles = _builder.GetStudioFileEntries(presets).ToList();
        _presetFileById = _presetFiles.ToDictionary(GetAssetId, x => x, StringComparer.OrdinalIgnoreCase);
        _iconService = new ItemIconService(runtimePaths, scum, unrealPakPath, DefaultAesKeyHex);
        _knowledgeBase = new ScumKnowledgeBaseService();
        (_features, _featureAssetIds) = BuildFeatureCatalog(_presetFiles);
    }

    public static StudioRuntime Create()
    {
        var appRoot = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var workspaceRoot = ResolveWorkspaceRoot(appRoot);
        var runtimePaths = new RuntimePaths(
            appRoot,
            workspaceRoot,
            Path.Combine(Path.GetTempPath(), "ScumPakWizard"),
            Path.Combine(workspaceRoot, "builds", "ScumPakWizard"));

        Directory.CreateDirectory(runtimePaths.TempRoot);
        Directory.CreateDirectory(runtimePaths.BuildsRoot);

        var scum = ScumLocator.Locate()
            ?? throw new InvalidOperationException("SCUM не найден автоматически. Убедись, что игра установлена.");

        var unrealPakPath = UnrealPakLocator.Locate(appRoot);
        if (string.IsNullOrWhiteSpace(unrealPakPath))
        {
            throw new InvalidOperationException(
                "UnrealPak.exe не найден. Ожидается Engine\\Binaries\\Win64\\UnrealPak.exe внутри папки программы.");
        }

        var presets = PresetCatalog.Load(appRoot);
        if (presets.Count == 0)
        {
            throw new InvalidOperationException("Встроенные пресеты не найдены (папка presets пуста).");
        }

        return new StudioRuntime(runtimePaths, scum, unrealPakPath, presets);
    }

    private static string ResolveWorkspaceRoot(string appRoot)
    {
        var probes = new List<string?>
        {
            Directory.GetCurrentDirectory(),
            appRoot,
            Directory.GetParent(appRoot)?.FullName,
            Directory.GetParent(Directory.GetParent(appRoot)?.FullName ?? string.Empty)?.FullName
        };

        foreach (var probe in probes.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var candidate = FindWorkspaceRootUpward(probe!);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private static string? FindWorkspaceRootUpward(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current is not null)
        {
            var hasMainProject = Directory.Exists(Path.Combine(current.FullName, "ScumPakWizard"));
            var hasResearchRoots = Directory.Exists(Path.Combine(current.FullName, "analysis"))
                || Directory.Exists(Path.Combine(current.FullName, "mods"));

            if (hasMainProject && hasResearchRoots)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    public StudioStatusDto GetStatus()
    {
        return new StudioStatusDto(
            _scum.RootPath,
            _scum.PaksPath,
            _scum.BuildId,
            _unrealPakPath,
            _presets.Select(x => new StudioPresetDto(x.Id, x.DisplayName, x.Description, x.EditableSurfaces)).ToList(),
            _features,
            _presetFiles.Count);
    }

    public StudioResearchModPatternDto InspectResearchModPattern(string assetPath, bool includeImportDiff, int maxItems)
    {
        maxItems = Math.Clamp(maxItems, 4, 40);

        var warnings = new List<string>();
        var normalized = NormalizeResearchRelativePath(assetPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new StudioResearchModPatternDto(
                assetPath,
                string.Empty,
                string.Empty,
                false,
                [],
                [],
                [],
                ["Не удалось привести путь ассета к игровому relative path."]);
        }

        var gamePackagePath = BuildResearchGamePackagePath(normalized);
        var isBlueprintLike = IsBlueprintLikePath(normalized);
        var originalFiles = FindOriginalResearchFiles(normalized, warnings);
        var variants = BuildResearchVariants(normalized, isBlueprintLike, originalFiles, includeImportDiff, maxItems, warnings);
        var globalTags = variants
            .SelectMany(x => x.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (variants.Count == 0)
        {
            warnings.Add("Локальные mod-варианты по этому runtime path не найдены в известных research-корнях.");
        }

        return new StudioResearchModPatternDto(
            assetPath,
            normalized,
            gamePackagePath,
            isBlueprintLike,
            originalFiles,
            variants,
            globalTags,
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    public StudioAssetQueryResultDto GetAssets(string? search, string? presetId, string? scope, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 40, 500);

        IEnumerable<StudioAssetDto> query = GetAssetRowsByScope(scope);

        if (!string.IsNullOrWhiteSpace(presetId))
        {
            query = query.Where(x => x.PresetId.Equals(presetId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.RelativePath.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.PresetName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Surface.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var materialized = query.ToList();
        var total = materialized.Count;
        var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var actualPage = Math.Min(page, pageCount);
        var skip = (actualPage - 1) * pageSize;
        var items = materialized
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        return new StudioAssetQueryResultDto(total, actualPage, pageSize, items);
    }

    private IEnumerable<StudioAssetDto> GetAssetRowsByScope(string? scope)
    {
        var normalized = (scope ?? "all").Trim().ToLowerInvariant();

        lock (_sync)
        {
            _presetAssetsCache ??= BuildPresetAssetRows();
        }

        if (normalized == "preset")
        {
            return _presetAssetsCache!;
        }

        lock (_sync)
        {
            _gameAssetsCache ??= BuildGameAssetRows();
        }

        if (normalized == "game")
        {
            return _gameAssetsCache!;
        }

        return _presetAssetsCache!.Concat(_gameAssetsCache!);
    }

    private List<StudioAssetDto> BuildPresetAssetRows()
    {
        return _presetFiles
            .OrderBy(x => x.TargetRelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(x => new StudioAssetDto(
                GetAssetId(x),
                x.PresetId,
                x.PresetName,
                x.TargetRelativePath,
                x.SurfaceLabel,
                false,
                false))
            .ToList();
    }

    private List<StudioAssetDto> BuildGameAssetRows()
    {
        var pakIndex = GetOrLoadPakIndex();
        var presetPathSet = _presetFiles
            .Select(x => PathUtil.NormalizeRelative(x.TargetRelativePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new List<StudioAssetDto>(12000);
        foreach (var path in pakIndex.GetAllRelativePaths())
        {
            if (!IsModdableGameAsset(path))
            {
                continue;
            }

            var normalized = PathUtil.NormalizeRelative(path);
            var hasPresetAlternative = presetPathSet.Contains(normalized);
            result.Add(new StudioAssetDto(
                $"game::{normalized.ToLowerInvariant()}",
                "game",
                "Файлы Игры",
                normalized,
                ResolveSurfaceLabel(normalized),
                true,
                hasPresetAlternative));
        }

        result.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    public List<StudioModCategoryDto> GetModdingCategories()
    {
        lock (_sync)
        {
            _modAssetsCache ??= BuildModAssets();
            _modCategoriesCache ??= BuildModCategories(_modAssetsCache);
            _visibleModAssetsCache ??= BuildVisibleModAssets(_modAssetsCache);
            _visibleModCategoriesCache ??= BuildModCategories(_visibleModAssetsCache);
            return _visibleModCategoriesCache.ToList();
        }
    }

    public StudioModAssetQueryResultDto GetModdingAssets(string? categoryId, string? search, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 40, 300);

        List<StudioModAssetDto> allAssets;
        lock (_sync)
        {
            _modAssetsCache ??= BuildModAssets();
            _modCategoriesCache ??= BuildModCategories(_modAssetsCache);
            _visibleModAssetsCache ??= BuildVisibleModAssets(_modAssetsCache);
            _visibleModCategoriesCache ??= BuildModCategories(_visibleModAssetsCache);
            allAssets = _visibleModAssetsCache;
        }

        IEnumerable<StudioModAssetDto> query = allAssets;
        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            query = query.Where(x => x.CategoryId.Equals(categoryId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var looseTerm = NormalizeLooseSearch(term);
            query = query.Where(x =>
                x.RelativePath.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.CategoryName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Summary.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(looseTerm) && (
                    NormalizeLooseSearch(x.RelativePath).Contains(looseTerm, StringComparison.Ordinal)
                    || NormalizeLooseSearch(x.DisplayName).Contains(looseTerm, StringComparison.Ordinal)
                    || NormalizeLooseSearch(x.Summary).Contains(looseTerm, StringComparison.Ordinal))));
        }

        var materialized = query.ToList();
        var total = materialized.Count;
        var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var actualPage = Math.Min(page, pageCount);
        var skip = (actualPage - 1) * pageSize;
        var items = materialized
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        return new StudioModAssetQueryResultDto(total, actualPage, pageSize, items);
    }

    public List<StudioReferenceOptionDto> GetModdingReferenceOptions(string pickerKind, string? term, int limit)
    {
        limit = Math.Clamp(limit, 1, 80);
        var normalizedPicker = (pickerKind ?? string.Empty).Trim().ToLowerInvariant();

        if (normalizedPicker is "crafting-ingredient-asset" or "crafting-ingredient")
        {
            return GetCraftingIngredientReferenceOptions(term, limit);
        }

        if (normalizedPicker is "item-asset" or "item-reference")
        {
            return GetItemAssetReferenceOptions(term, limit);
        }

        List<StudioReferenceOptionDto> allOptions;
        lock (_sync)
        {
            allOptions = normalizedPicker switch
            {
                "foreign-substance-all" or "foreign-substance-source" =>
                    _foreignSubstanceReferenceOptionsCache ??= BuildForeignSubstanceReferenceOptions(),
                "foreign-substance" or "foreign-substance-attribute" =>
                    _foreignSubstanceAttributeReferenceOptionsCache ??= BuildForeignSubstanceAttributeReferenceOptions(),
                "bodyeffect-symptom" or "bodyeffect-symptom-class" =>
                    _bodyEffectSymptomReferenceOptionsCache ??= BuildBodyEffectSymptomReferenceOptions(),
                "bodyeffect-side-effect" =>
                    _bodyEffectSideEffectReferenceOptionsCache ??= BuildBodyEffectSideEffectReferenceOptions(),
                "encounter-character-preset" or "encounter-preset" =>
                    _encounterCharacterPresetReferenceOptionsCache ??= BuildEncounterCharacterPresetReferenceOptions(),
                "encounter-class" =>
                    _encounterClassReferenceOptionsCache ??= BuildEncounterClassReferenceOptions(),
                "cargo-drop-encounter-class" =>
                    _cargoDropEncounterClassReferenceOptionsCache ??= BuildCargoDropEncounterClassReferenceOptions(),
                "encounter-npc-class" =>
                    _encounterNpcClassReferenceOptionsCache ??= BuildEncounterNpcClassReferenceOptions(),
                "crafting-item-recipe-asset" or "crafting-item-recipe" =>
                    _craftingItemRecipeReferenceOptionsCache ??= BuildCraftingRecipeReferenceOptions(
                        recipeClassName: "ItemCraftingRecipe",
                        labelPrefix: "Рецепт предмета"),
                "crafting-placeable-recipe-asset" or "crafting-placeable-recipe" =>
                    _craftingPlaceableRecipeReferenceOptionsCache ??= BuildCraftingRecipeReferenceOptions(
                        recipeClassName: "PlaceableCraftingRecipe",
                        labelPrefix: "Рецепт строительства"),
                "crafting-recipe-asset" or "crafting-recipe" =>
                    (_craftingItemRecipeReferenceOptionsCache ??= BuildCraftingRecipeReferenceOptions(
                        recipeClassName: "ItemCraftingRecipe",
                        labelPrefix: "Рецепт предмета"))
                    .Concat(_craftingPlaceableRecipeReferenceOptionsCache ??= BuildCraftingRecipeReferenceOptions(
                        recipeClassName: "PlaceableCraftingRecipe",
                        labelPrefix: "Рецепт строительства"))
                    .ToList(),
                "skill-asset" or "skill-reference" =>
                    _skillReferenceOptionsCache ??= BuildSkillReferenceOptions(),
                "skill-blueprint-asset" or "skill-blueprint-reference" =>
                    _skillBlueprintReferenceOptionsCache ??= BuildSkillBlueprintReferenceOptions(),
                "quest-giver" or "quest-giver-class" =>
                    _questGiverReferenceOptionsCache ??= BuildQuestGiverReferenceOptions(),
                "quest-asset" or "quest-reference" =>
                    _questAssetReferenceOptionsCache ??= BuildQuestAssetReferenceOptions(),
                "gameevent-primary-loadout" =>
                    _gameEventPrimaryLoadoutReferenceOptionsCache ??= BuildGameEventLoadoutReferenceOptions(
                        "/ui/gameevents/itemselection/rifles/",
                        "основное оружие"),
                "gameevent-secondary-loadout" =>
                    _gameEventSecondaryLoadoutReferenceOptionsCache ??= BuildGameEventLoadoutReferenceOptions(
                        "/ui/gameevents/itemselection/pistols/",
                        "пистолет"),
                "gameevent-tertiary-loadout" =>
                    _gameEventTertiaryLoadoutReferenceOptionsCache ??= BuildGameEventLoadoutReferenceOptions(
                        "/ui/gameevents/itemselection/melee/",
                        "ближний бой"),
                "gameevent-outfit-loadout" =>
                    _gameEventOutfitLoadoutReferenceOptionsCache ??= BuildGameEventLoadoutReferenceOptions(
                        "/ui/gameevents/itemselection/outfits/",
                        "одежда"),
                "gameevent-mandatory-loadout" =>
                    _gameEventMandatoryLoadoutReferenceOptionsCache ??= BuildGameEventLoadoutReferenceOptions(
                        "/ui/gameevents/itemselection/mandatory/",
                        "обязательное снаряжение"),
                "gameevent-support-loadout" =>
                    _gameEventSupportLoadoutReferenceOptionsCache ??= BuildGameEventLoadoutReferenceOptions(
                        "/ui/gameevents/itemselection/support/",
                        "дополнительное снаряжение"),
                "fish-species-asset" or "fish-species" =>
                    _fishSpeciesReferenceOptionsCache ??= BuildFishSpeciesReferenceOptions(),
                "plant-species-asset" or "plant-species" =>
                    _plantSpeciesReferenceOptionsCache ??= BuildPlantSpeciesReferenceOptions(),
                "plant-pest-asset" or "plant-pest" =>
                    _plantPestReferenceOptionsCache ??= BuildPlantPestReferenceOptions(),
                "plant-disease-asset" or "plant-disease" =>
                    _plantDiseaseReferenceOptionsCache ??= BuildPlantDiseaseReferenceOptions(),
                "item-spawner-preset" or "regular-item-spawner-preset" =>
                    _regularItemSpawnerPresetReferenceOptionsCache ??= BuildRegularItemSpawnerPresetReferenceOptions(),
                "advanced-item-spawner-preset" or "container-loot-preset" or "examine-data-preset" =>
                    _advancedItemSpawnerPresetReferenceOptionsCache ??= BuildAdvancedItemSpawnerPresetReferenceOptions(),
                "advanced-item-spawner-subpreset" or "container-subpreset-preset" =>
                    _advancedItemSpawnerSubpresetReferenceOptionsCache ??= BuildAdvancedItemSpawnerSubpresetReferenceOptions(),
                "vehicle-spawn-preset" or "vehicle-preset" =>
                    _vehicleSpawnPresetReferenceOptionsCache ??= BuildVehicleSpawnPresetReferenceOptions(),
                _ => []
            };
        }

        if (allOptions.Count == 0)
        {
            return [];
        }

        IEnumerable<StudioReferenceOptionDto> query = allOptions;

        if (!string.IsNullOrWhiteSpace(term))
        {
            var trimmed = term.Trim();
            var searchTerms = ExpandReferenceSearchTerms(pickerKind, trimmed).ToList();
            var normalizedTerms = searchTerms
                .Select(NormalizeLooseSearch)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            query = query.Where(option => MatchesReferenceSearch(option, searchTerms, normalizedTerms));
        }

        return query
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private List<StudioReferenceOptionDto> GetItemAssetReferenceOptions(string? term, int limit)
    {
        var catalog = GetItemCatalog();
        IEnumerable<StudioReferenceOptionDto> query = catalog.Items
            .Select(item => new StudioReferenceOptionDto(
                BuildBlueprintClassReferenceFromRelativePath(item.RelativePath),
                item.ItemName));

        if (!string.IsNullOrWhiteSpace(term))
        {
            var trimmed = term.Trim();
            var searchTerms = ExpandReferenceSearchTerms("item-asset", trimmed).ToList();
            var normalizedTerms = searchTerms
                .Select(NormalizeLooseSearch)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            query = query.Where(option => MatchesReferenceSearch(option, searchTerms, normalizedTerms));
        }

        return query
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private List<StudioReferenceOptionDto> GetCraftingIngredientReferenceOptions(string? term, int limit)
    {
        List<StudioReferenceOptionDto> groupOptions;
        lock (_sync)
        {
            groupOptions = _craftingIngredientGroupReferenceOptionsCache ??= BuildCraftingIngredientGroupReferenceOptions();
        }

        if (string.IsNullOrWhiteSpace(term))
        {
            return groupOptions
                .Take(limit)
                .ToList();
        }

        var trimmed = term.Trim();
        var searchTerms = ExpandReferenceSearchTerms("crafting-ingredient-asset", trimmed).ToList();
        var normalizedTerms = searchTerms
            .Select(NormalizeLooseSearch)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var combined = new List<StudioReferenceOptionDto>(limit * 3);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddOption(StudioReferenceOptionDto option)
        {
            if (!seen.Add(option.Value))
            {
                return;
            }

            combined.Add(option);
        }

        foreach (var option in groupOptions.Where(option => MatchesReferenceSearch(option, searchTerms, normalizedTerms)))
        {
            AddOption(option);
        }

        foreach (var option in SearchCraftingIngredientPlayableItemOptions(searchTerms, normalizedTerms, Math.Max(limit * 4, 32)))
        {
            AddOption(option);
        }

        return combined
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private List<StudioReferenceOptionDto> BuildForeignSubstanceReferenceOptions()
    {
        return BuildBlueprintReferenceOptions(
            relativePath =>
                relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                && relativePath.Contains("/characters/prisoner/blueprints/metabolism/foreignsubstances/", StringComparison.OrdinalIgnoreCase)
                && !relativePath.Contains("uidata", StringComparison.OrdinalIgnoreCase),
            assetInfo => PrefixReferenceOptionLabel("Вещество", assetInfo.DisplayName));
    }

    private List<StudioReferenceOptionDto> BuildForeignSubstanceAttributeReferenceOptions()
    {
        var result = new List<StudioReferenceOptionDto>(48);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _modAssetsCache ??= BuildModAssets();

        foreach (var assetInfo in _modAssetsCache)
        {
            var relativePath = PathUtil.NormalizeRelative(assetInfo.RelativePath);
            if (!relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                || !relativePath.Contains("/characters/prisoner/blueprints/metabolism/foreignsubstances/", StringComparison.OrdinalIgnoreCase)
                || relativePath.Contains("uidata", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryBuildSelectionFromAssetId(assetInfo.AssetId, out var selection))
            {
                continue;
            }

            var sourceWarnings = new List<string>(2);
            var sourceMode = ResolveSourceMode(null, selection.PresetSourcePath is not null);
            var sourcePath = ResolveAssetSourcePath(selection, sourceMode, sourceWarnings, includeCompanions: true);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                continue;
            }

            var readableSourcePath = PrepareIsolatedAssetReadSource(sourcePath);
            var asset = new UAsset(readableSourcePath, EngineVersion.VER_UE4_27, null, CustomSerializationFlags.None);
            if (!IsForeignSubstanceAttributeModifierAsset(asset, relativePath))
            {
                continue;
            }

            var value = BuildBlueprintClassReferenceFromRelativePath(relativePath);
            if (!seen.Add(value))
            {
                continue;
            }

            result.Add(new StudioReferenceOptionDto(value, assetInfo.DisplayName));
        }

        return result
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<StudioReferenceOptionDto> BuildBodyEffectSymptomReferenceOptions()
    {
        return BuildBlueprintReferenceOptions(
            relativePath =>
                relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                && relativePath.Contains("/characters/prisoner/blueprints/bodyeffects/symptoms/", StringComparison.OrdinalIgnoreCase),
            assetInfo => PrefixReferenceOptionLabel("Симптом", assetInfo.DisplayName));
    }

    private List<StudioReferenceOptionDto> BuildBodyEffectSideEffectReferenceOptions()
    {
        return GetSideEffectTemplates()
            .Select(template => new StudioReferenceOptionDto(
                $"script:{template.ClassName}",
                ResolveBodyEffectSideEffectDisplayName(template.ClassName)))
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<StudioReferenceOptionDto> BuildCraftingIngredientGroupReferenceOptions()
    {
        var result = new List<StudioReferenceOptionDto>(512);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _modAssetsCache ??= BuildModAssets();

        void AddOption(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
            {
                return;
            }

            result.Add(new StudioReferenceOptionDto(value, label));
        }

        foreach (var assetInfo in _modAssetsCache.Where(assetInfo =>
                     assetInfo.CategoryId.Equals("crafting-ingredients", StringComparison.OrdinalIgnoreCase)))
        {
            AddOption(
                BuildBlueprintClassReferenceFromRelativePath(assetInfo.RelativePath),
                BuildCraftingIngredientReferenceLabel(assetInfo.RelativePath));
        }

        return result
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<StudioReferenceOptionDto> BuildCraftingRecipeReferenceOptions(string recipeClassName, string labelPrefix)
    {
        var result = new List<StudioReferenceOptionDto>(1024);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _modAssetsCache ??= BuildModAssets();

        void AddOption(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
            {
                return;
            }

            result.Add(new StudioReferenceOptionDto(value, label));
        }

        foreach (var assetInfo in _modAssetsCache.Where(assetInfo =>
                     assetInfo.CategoryId.Equals("crafting-recipes", StringComparison.OrdinalIgnoreCase)))
        {
            var relativePath = PathUtil.NormalizeRelative(assetInfo.RelativePath);
            if (!IsCraftingRecipeAsset(relativePath))
            {
                continue;
            }

            var expectedClassName = ResolveCraftingRecipeReferenceClassName(relativePath);
            if (!string.Equals(expectedClassName, recipeClassName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = BuildGameObjectReferenceFromRelativePath(
                relativePath,
                classPackage: "/Script/ConZ",
                className: expectedClassName);

            var stem = Path.GetFileNameWithoutExtension(relativePath);
            var localizedName = LocalizeAssetStem(
                stem.StartsWith("CR_", StringComparison.OrdinalIgnoreCase)
                    ? stem[3..]
                    : stem);
            AddOption(value, $"{labelPrefix}: {localizedName}");
        }

        return result
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IEnumerable<StudioReferenceOptionDto> SearchCraftingIngredientPlayableItemOptions(
        IReadOnlyList<string> searchTerms,
        IReadOnlyList<string> normalizedTerms,
        int limit)
    {
        var results = new List<StudioReferenceOptionDto>(limit);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAdd(string relativePath, string prefix, string displayName)
        {
            var stem = Path.GetFileNameWithoutExtension(relativePath);
            if (!MatchesLooseSearchText(displayName, stem, searchTerms, normalizedTerms))
            {
                return;
            }

            var value = BuildBlueprintClassReferenceFromRelativePath(relativePath);
            if (!seen.Add(value))
            {
                return;
            }

            var option = new StudioReferenceOptionDto(value, PrefixReferenceOptionLabel(prefix, displayName));
            results.Add(option);
        }

        List<StudioItemDto>? cachedItems;
        lock (_sync)
        {
            cachedItems = _itemsCache?.ToList();
        }

        if (cachedItems is not null && cachedItems.Count > 0)
        {
            foreach (var item in cachedItems)
            {
                if (string.IsNullOrWhiteSpace(item.RelativePath))
                {
                    continue;
                }

                TryAdd(item.RelativePath, "Предмет", item.ItemName);
                if (results.Count >= limit)
                {
                    return results;
                }
            }
        }
        else
        {
            var pakIndex = GetOrLoadPakIndex();
            foreach (var relativePath in pakIndex.GetAllRelativePaths())
            {
                if (!relativePath.StartsWith("scum/content/conz_files/items/", StringComparison.OrdinalIgnoreCase)
                    || !relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                    || !IsLikelyPlayableItemAsset(relativePath))
                {
                    continue;
                }

                var itemId = Path.GetFileNameWithoutExtension(relativePath);
                TryAdd(relativePath, "Предмет", HumanizeItemName(itemId));
                if (results.Count >= limit)
                {
                    return results;
                }
            }
        }

        _modAssetsCache ??= BuildModAssets();
        foreach (var assetInfo in _modAssetsCache.Where(assetInfo =>
                     assetInfo.CategoryId.Equals("weapons-items", StringComparison.OrdinalIgnoreCase)
                     && !assetInfo.RelativePath.StartsWith("scum/content/conz_files/items/", StringComparison.OrdinalIgnoreCase)))
        {
            var prefix = assetInfo.RelativePath.Contains("/gameresources/food/", StringComparison.OrdinalIgnoreCase)
                ? "Еда"
                : "Предмет";
            TryAdd(assetInfo.RelativePath, prefix, assetInfo.DisplayName);
            if (results.Count >= limit)
            {
                break;
            }
        }

        return results;
    }

    private string BuildCraftingIngredientReferenceLabel(string relativePath)
    {
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var baseName = stem.StartsWith("CI_", StringComparison.OrdinalIgnoreCase) ? stem[3..] : stem;
        var consumesWhole = false;
        if (baseName.EndsWith("_Destroy", StringComparison.OrdinalIgnoreCase))
        {
            baseName = baseName[..^"_Destroy".Length];
            consumesWhole = true;
        }
        else if (baseName.EndsWith("Destroy", StringComparison.OrdinalIgnoreCase))
        {
            baseName = baseName[..^"Destroy".Length];
            consumesWhole = true;
        }

        if (baseName.StartsWith("Group_", StringComparison.OrdinalIgnoreCase))
        {
            var label = PrefixReferenceOptionLabel(
                "Группа ингредиентов",
                LocalizeAssetStem(baseName["Group_".Length..]));
            return consumesWhole ? $"{label} (расходуется полностью)" : label;
        }

        var ingredientLabel = PrefixReferenceOptionLabel("Ингредиент", LocalizeAssetStem(baseName));
        return consumesWhole ? $"{ingredientLabel} (расходуется полностью)" : ingredientLabel;
    }

    private List<SideEffectTemplateInfo> GetSideEffectTemplates()
    {
        lock (_sync)
        {
            _sideEffectTemplateCache ??= BuildSideEffectTemplateCatalog();
            return _sideEffectTemplateCache;
        }
    }

    private List<SideEffectTemplateInfo> BuildSideEffectTemplateCatalog()
    {
        var templates = new Dictionary<string, SideEffectTemplateInfo>(StringComparer.OrdinalIgnoreCase);

        var bundledDonorRoot = Path.Combine(_runtimePaths.AppRoot, "donors", "sideeffects");
        if (Directory.Exists(bundledDonorRoot))
        {
            foreach (var donorAssetPath in Directory.EnumerateFiles(bundledDonorRoot, "*.uasset", SearchOption.AllDirectories))
            {
                CollectSideEffectTemplatesFromAssetFile(donorAssetPath, templates, isBundledTemplate: true);
            }
        }

        _modAssetsCache ??= BuildModAssets();
        foreach (var assetInfo in _modAssetsCache)
        {
            var relativePath = PathUtil.NormalizeRelative(assetInfo.RelativePath);
            if (!relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                || (!relativePath.Contains("/bodyeffects/", StringComparison.OrdinalIgnoreCase)
                    && !relativePath.Contains("/metabolism/foreignsubstances/", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!TryBuildSelectionFromAssetId(assetInfo.AssetId, out var selection))
            {
                continue;
            }

            var sourceWarnings = new List<string>(2);
            var sourceMode = ResolveSourceMode(null, selection.PresetSourcePath is not null);
            var sourcePath = ResolveAssetSourcePath(selection, sourceMode, sourceWarnings, includeCompanions: true);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                continue;
            }

            CollectSideEffectTemplatesFromAssetFile(sourcePath, templates, isBundledTemplate: false);
        }

        AugmentCurveCompatibleSideEffectTemplates(templates);

        return templates.Values
            .OrderBy(template => template.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(template => template.ClassName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void CollectSideEffectTemplatesFromAssetFile(
        string sourcePath,
        Dictionary<string, SideEffectTemplateInfo> output,
        bool isBundledTemplate)
    {
        var readableSourcePath = isBundledTemplate
            ? sourcePath
            : PrepareIsolatedAssetReadSource(sourcePath);

        UAsset asset;
        try
        {
            asset = new UAsset(readableSourcePath, EngineVersion.VER_UE4_27, null, CustomSerializationFlags.None);
        }
        catch
        {
            return;
        }

        for (var exportIndex = 0; exportIndex < asset.Exports.Count; exportIndex++)
        {
            if (asset.Exports[exportIndex] is not NormalExport export)
            {
                continue;
            }

            if (!TryResolveSideEffectClassName(asset, export, out var className)
                || !IsStandaloneSafeSideEffectExport(export))
            {
                continue;
            }

            var template = new SideEffectTemplateInfo(
                className,
                ResolveBodyEffectSideEffectDisplayName(className),
                sourcePath,
                exportIndex,
                isBundledTemplate,
                SupportsCurveReuse: IsCurveReusableSideEffectTemplate(className, export));

            if (output.TryGetValue(className, out var existing))
            {
                if (existing.IsBundledTemplate && !isBundledTemplate)
                {
                    output[className] = template;
                }

                continue;
            }

            output[className] = template;
        }
    }

    private static void AugmentCurveCompatibleSideEffectTemplates(Dictionary<string, SideEffectTemplateInfo> output)
    {
        var reusableTemplate = output.Values
            .FirstOrDefault(template => template.SupportsCurveReuse);
        if (reusableTemplate is null)
        {
            return;
        }

        foreach (var className in CurveCompatibleSideEffectClasses)
        {
            if (output.ContainsKey(className))
            {
                continue;
            }

            output[className] = reusableTemplate with
            {
                ClassName = className,
                DisplayName = ResolveBodyEffectSideEffectDisplayName(className)
            };
        }
    }

    private static bool TryResolveSideEffectClassName(UAsset asset, NormalExport export, out string className)
    {
        className = string.Empty;

        if (TryResolveImportObjectName(asset, export.ClassIndex, out var importedClassName)
            && importedClassName.StartsWith("PrisonerBodyConditionOrSymptomSideEffect_", StringComparison.OrdinalIgnoreCase))
        {
            className = importedClassName;
            return true;
        }

        if (TryResolveImportObjectName(asset, export.TemplateIndex, out var templateName))
        {
            var normalizedTemplate = templateName.StartsWith("Default__", StringComparison.OrdinalIgnoreCase)
                ? templateName["Default__".Length..]
                : templateName;
            if (normalizedTemplate.StartsWith("PrisonerBodyConditionOrSymptomSideEffect_", StringComparison.OrdinalIgnoreCase))
            {
                className = normalizedTemplate;
                return true;
            }
        }

        var exportName = export.ObjectName?.ToString() ?? string.Empty;
        if (exportName.StartsWith("PrisonerBodyConditionOrSymptomSideEffect_", StringComparison.OrdinalIgnoreCase))
        {
            className = exportName;
            return true;
        }

        return false;
    }

    private static bool IsStandaloneSafeSideEffectExport(NormalExport export)
    {
        return export.Data.Count > 0 && export.Data.All(IsStandaloneSafeSideEffectProperty);
    }

    private static bool IsCurveReusableSideEffectTemplate(string className, NormalExport export)
    {
        return CurveCompatibleSideEffectClasses.Contains(className, StringComparer.OrdinalIgnoreCase)
               && export.Data.Count == 1
               && export.Data[0] is StructPropertyData structProperty
               && PropertyNamesMatch(structProperty.Name?.ToString(), "_modifierVsSeverity")
               && IsStandaloneSafeSideEffectProperty(structProperty);
    }

    private static bool IsStandaloneSafeSideEffectProperty(PropertyData property)
    {
        return property switch
        {
            FloatPropertyData or DoublePropertyData or IntPropertyData or Int8PropertyData or Int16PropertyData
                or Int64PropertyData or UInt16PropertyData or UInt32PropertyData or UInt64PropertyData
                or BytePropertyData or BoolPropertyData or EnumPropertyData or NamePropertyData
                or StrPropertyData or TextPropertyData or RichCurveKeyPropertyData => true,
            StructPropertyData structProperty => (structProperty.Value ?? []).All(IsStandaloneSafeSideEffectProperty),
            ArrayPropertyData arrayProperty => IsStandaloneSafeSideEffectArray(arrayProperty),
            ObjectPropertyData or SoftObjectPropertyData or SoftObjectPathPropertyData or MapPropertyData => false,
            _ => false
        };
    }

    private static bool IsStandaloneSafeSideEffectArray(ArrayPropertyData property)
    {
        if (property.DummyStruct is not null && !IsStandaloneSafeSideEffectProperty(property.DummyStruct))
        {
            return false;
        }

        return (property.Value ?? []).All(IsStandaloneSafeSideEffectProperty);
    }

    private List<StudioReferenceOptionDto> BuildSkillReferenceOptions()
    {
        var result = new List<StudioReferenceOptionDto>(96);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sdkSkillClasses = BuildSkillScriptClassLookup();
        _modAssetsCache ??= BuildModAssets();

        foreach (var assetInfo in _modAssetsCache)
        {
            var relativePath = PathUtil.NormalizeRelative(assetInfo.RelativePath);
            if (!IsSkillPickerAssetPath(relativePath))
            {
                continue;
            }

            var stem = Path.GetFileNameWithoutExtension(relativePath);
            if (string.IsNullOrWhiteSpace(stem))
            {
                continue;
            }

            var normalizedStem = NormalizeLooseSearch(stem);
            if (!sdkSkillClasses.TryGetValue(normalizedStem, out var className))
            {
                className = ToPascalCaseIdentifier(stem);
            }

            var value = $"class:{className}";
            if (!seen.Add(value))
            {
                continue;
            }

            result.Add(new StudioReferenceOptionDto(value, PrefixReferenceOptionLabel("Навык", assetInfo.DisplayName)));
        }

        return result
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<StudioReferenceOptionDto> BuildSkillBlueprintReferenceOptions()
    {
        return BuildBlueprintReferenceOptions(
            IsSkillPickerAssetPath,
            assetInfo => PrefixReferenceOptionLabel("Навык", assetInfo.DisplayName));
    }

    private List<StudioReferenceOptionDto> BuildQuestGiverReferenceOptions()
    {
        if (!TryBuildSelectionFromAssetId(QuestManagerAssetId, out var selection))
        {
            return [];
        }

        var warnings = new List<string>(2);
        var sourcePath = ResolveAssetSourcePath(selection, "game", warnings, includeCompanions: true);
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return [];
        }

        var readableSourcePath = PrepareIsolatedAssetReadSource(sourcePath);
        var asset = new UAsset(readableSourcePath, EngineVersion.VER_UE4_27, null, CustomSerializationFlags.None);
        if (!TryFindQuestGiversSetupMap(asset, out var mapProperty))
        {
            return [];
        }

        var result = new List<StudioReferenceOptionDto>(mapProperty.Value.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in mapProperty.Value)
        {
            string rawValue;
            string displayValue;
            switch (entry.Key)
            {
                case ObjectPropertyData objectKey when TryExtractObjectReferencePickerValue(asset, objectKey.Value, out var objectRawValue, out var objectDisplayValue):
                    rawValue = objectRawValue;
                    displayValue = objectDisplayValue;
                    break;
                case SoftObjectPropertyData softObjectKey:
                    rawValue = ExtractSoftObjectReference(softObjectKey.Value);
                    displayValue = rawValue;
                    break;
                case SoftObjectPathPropertyData softObjectPathKey:
                    rawValue = ExtractSoftObjectReference(softObjectPathKey.Value);
                    displayValue = rawValue;
                    break;
                default:
                    continue;
            }

            if (string.IsNullOrWhiteSpace(rawValue) || !seen.Add(rawValue))
            {
                continue;
            }

            result.Add(new StudioReferenceOptionDto(
                rawValue,
                PrefixReferenceOptionLabel("Источник квестов", ResolveQuestGiverDisplayName(displayValue))));
        }

        return result
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<StudioReferenceOptionDto> BuildQuestAssetReferenceOptions()
    {
        return BuildBlueprintReferenceOptions(
            relativePath =>
                relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                && (relativePath.Contains("/quests/questdata/", StringComparison.OrdinalIgnoreCase)
                    || relativePath.Contains("/quests/testquests/", StringComparison.OrdinalIgnoreCase)),
            assetInfo => assetInfo.DisplayName);
    }

    private List<StudioReferenceOptionDto> BuildGameEventLoadoutReferenceOptions(string folderToken, string slotLabel)
    {
        var result = new List<StudioReferenceOptionDto>(48);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _modAssetsCache ??= BuildModAssets();

        foreach (var assetInfo in _modAssetsCache)
        {
            var relativePath = PathUtil.NormalizeRelative(assetInfo.RelativePath);
            if (!relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                || !relativePath.Contains(folderToken, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = BuildSoftGameAssetReferenceFromRelativePath(relativePath);
            if (!seen.Add(value))
            {
                continue;
            }

            var displayName = ResolveGameEventLoadoutOptionName(assetInfo.DisplayName);
            result.Add(new StudioReferenceOptionDto(
                value,
                $"Набор события: {slotLabel} / {displayName}"));
        }

        return result
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<StudioReferenceOptionDto> BuildFishSpeciesReferenceOptions()
    {
        var result = new List<StudioReferenceOptionDto>(24);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _modAssetsCache ??= BuildModAssets();

        foreach (var assetInfo in _modAssetsCache)
        {
            var relativePath = PathUtil.NormalizeRelative(assetInfo.RelativePath);
            if (!relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                || !relativePath.Contains("/characters/spawnerpresets/fishspeciespresets/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryBuildSelectionFromAssetId(assetInfo.AssetId, out var selection))
            {
                continue;
            }

            var warnings = new List<string>();
            var sourceMode = ResolveSourceMode(null, selection.PresetSourcePath is not null);
            var sourcePath = ResolveAssetSourcePath(selection, sourceMode, warnings, includeCompanions: true);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                continue;
            }

            try
            {
                var readableSourcePath = PrepareIsolatedAssetReadSource(sourcePath);
                var asset = new UAsset(readableSourcePath, EngineVersion.VER_UE4_27, null, CustomSerializationFlags.None);
                foreach (var import in asset.Imports)
                {
                    var classPackage = import.ClassPackage?.ToString() ?? string.Empty;
                    var className = import.ClassName?.ToString() ?? string.Empty;
                    var objectName = import.ObjectName?.ToString() ?? string.Empty;
                    if (!classPackage.Equals("/Script/SCUM", StringComparison.OrdinalIgnoreCase)
                        || !className.Equals("FishSpeciesData", StringComparison.OrdinalIgnoreCase)
                        || string.IsNullOrWhiteSpace(objectName)
                        || !TryResolvePackageImportPath(asset, import.OuterIndex, out var packagePath)
                        || !packagePath.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var value = BuildImportedObjectReferenceRawValue(packagePath, objectName, classPackage, className);
                    if (!seen.Add(value))
                    {
                        continue;
                    }

                    result.Add(new StudioReferenceOptionDto(
                        value,
                        PrefixReferenceOptionLabel("Вид рыбы", ResolveFishSpeciesDisplayName(objectName))));
                }
            }
            catch
            {
                // Ignore unreadable templates and continue collecting from the rest.
            }
        }

        return result
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<StudioReferenceOptionDto> BuildPlantSpeciesReferenceOptions()
    {
        return BuildSoftGameAssetReferenceOptions(
            relativePath =>
                relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                && relativePath.Contains("/foliage/farming/", StringComparison.OrdinalIgnoreCase)
                && Path.GetFileNameWithoutExtension(relativePath).StartsWith("DA_PlantSpecies_", StringComparison.OrdinalIgnoreCase),
            assetInfo => PrefixReferenceOptionLabel("Вид растения", ResolvePlantDisplayName(assetInfo.RelativePath, "DA_PlantSpecies_")));
    }

    private List<StudioReferenceOptionDto> BuildPlantPestReferenceOptions()
    {
        return BuildGameObjectReferenceOptions(
            relativePath =>
                relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                && relativePath.Contains("/foliage/farming/pests/", StringComparison.OrdinalIgnoreCase)
                && Path.GetFileNameWithoutExtension(relativePath).StartsWith("DA_PlantPest_", StringComparison.OrdinalIgnoreCase),
            "/Script/SCUM",
            "PlantPestSpecies",
            assetInfo => PrefixReferenceOptionLabel("Вредитель", ResolvePlantDisplayName(assetInfo.RelativePath, "DA_PlantPest_")));
    }

    private List<StudioReferenceOptionDto> BuildPlantDiseaseReferenceOptions()
    {
        return BuildGameObjectReferenceOptions(
            relativePath =>
                relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                && relativePath.Contains("/foliage/farming/diseases/", StringComparison.OrdinalIgnoreCase)
                && Path.GetFileNameWithoutExtension(relativePath).StartsWith("DA_PlantDisease_", StringComparison.OrdinalIgnoreCase),
            "/Script/SCUM",
            "PlantDiseaseSpecies",
            assetInfo => PrefixReferenceOptionLabel("Болезнь", ResolvePlantDisplayName(assetInfo.RelativePath, "DA_PlantDisease_")));
    }

    private List<StudioReferenceOptionDto> BuildRegularItemSpawnerPresetReferenceOptions()
    {
        return BuildBlueprintReferenceOptions(
            relativePath =>
                relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                && IsCargoDropLootPresetAsset(relativePath),
            assetInfo => $"Пресет грузового дропа: {ResolveCargoDropLootPresetDisplayName(assetInfo.RelativePath)}");
    }

    private List<StudioReferenceOptionDto> BuildAdvancedItemSpawnerPresetReferenceOptions()
    {
        var result = new List<StudioReferenceOptionDto>(96);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _gameAssetsCache ??= BuildGameAssetRows();

        foreach (var assetInfo in _gameAssetsCache)
        {
            var relativePath = PathUtil.NormalizeRelative(assetInfo.RelativePath);
            if (!relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                || !IsCargoDropPackagePresetAsset(relativePath))
            {
                continue;
            }

            var value = BuildBlueprintClassReferenceFromRelativePath(relativePath);
            if (!seen.Add(value))
            {
                continue;
            }

            result.Add(new StudioReferenceOptionDto(
                value,
                $"Контейнерный набор дропа: {ResolveCargoDropPackagePresetDisplayName(relativePath)}"));
        }

        return result
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<StudioReferenceOptionDto> BuildAdvancedItemSpawnerSubpresetReferenceOptions()
    {
        var result = new List<StudioReferenceOptionDto>(192);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _gameAssetsCache ??= BuildGameAssetRows();

        foreach (var assetInfo in _gameAssetsCache)
        {
            var relativePath = PathUtil.NormalizeRelative(assetInfo.RelativePath);
            if (!relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                || !IsAdvancedItemSpawnerPresetAsset(relativePath)
                || IsCargoDropPackagePresetAsset(relativePath)
                || IsAlwaysHiddenCatalogLane(relativePath))
            {
                continue;
            }

            if (!TryDescribeAdvancedItemSpawnerPresetAsset(relativePath, out var descriptor))
            {
                continue;
            }

            var value = BuildBlueprintClassReferenceFromRelativePath(relativePath);
            if (!seen.Add(value))
            {
                continue;
            }

            result.Add(new StudioReferenceOptionDto(value, descriptor.DisplayName));
        }

        return result
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<StudioReferenceOptionDto> BuildVehicleSpawnPresetReferenceOptions()
    {
        return BuildGameObjectReferenceOptions(
            relativePath =>
                relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                && relativePath.Contains("/vehicles/spawningpresets/automaticspawn/", StringComparison.OrdinalIgnoreCase),
            "/Script/ConZ",
            "VehiclePreset",
            assetInfo => $"Пресет транспорта: {ResolveVehicleSpawnPresetOptionDisplayName(assetInfo.RelativePath)}");
    }

    private static string ResolveAdvancedItemSpawnerPresetDisplayName(string relativePath)
    {
        var normalized = PathUtil.NormalizeRelative(relativePath);
        var segments = normalized
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var spawnerIndex = Array.FindIndex(segments, segment => segment.Equals("spawnerpresets2", StringComparison.OrdinalIgnoreCase));
        var contextSegments = spawnerIndex >= 0 && spawnerIndex + 1 < segments.Length - 1
            ? segments[(spawnerIndex + 1)..^1].ToList()
            : [];
        if (contextSegments.Count > 0 && contextSegments[0].Equals("Buildings", StringComparison.OrdinalIgnoreCase))
        {
            contextSegments.RemoveAt(0);
        }

        var context = contextSegments.Count > 0
            ? string.Join(" / ", contextSegments.Select(LocalizeCompositeAssetName))
            : string.Empty;

        var stem = Path.GetFileNameWithoutExtension(normalized);
        if (stem.StartsWith("Examine_", StringComparison.OrdinalIgnoreCase))
        {
            stem = stem["Examine_".Length..];
        }

        var name = LocalizeCompositeAssetName(stem);

        return string.IsNullOrWhiteSpace(context)
            ? name
            : $"{context} / {name}";
    }

    private static string LocalizeCompositeAssetName(string rawStem)
    {
        if (string.IsNullOrWhiteSpace(rawStem))
        {
            return "Без названия";
        }

        var directLocalized = CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(rawStem)));
        var fallbackLocalized = CapitalizeFirst(NormalizeLocalizedLabel(LocalizeCompactGameplayName(rawStem)));
        if (!string.Equals(directLocalized, fallbackLocalized, StringComparison.OrdinalIgnoreCase))
        {
            return directLocalized;
        }

        var parts = rawStem
            .Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (parts.Count == 0)
        {
            return "Без названия";
        }

        if (parts.Count == 1)
        {
            return CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(parts[0])));
        }

        var localizedParts = new List<string>(parts.Count);
        foreach (var part in parts)
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                localizedParts.Add(part);
                continue;
            }

            var localized = LocalizeAssetStem(part);
            if (string.IsNullOrWhiteSpace(localized))
            {
                localized = HumanizeCamel(part);
            }

            localizedParts.Add(localized);
        }

        return CapitalizeFirst(
            NormalizeLocalizedLabel(
                LocalizeCommonGameplayTerms(string.Join(" ", localizedParts))));
    }

    private static string ResolveCargoDropLootPresetDisplayName(string relativePath)
    {
        if (TryDescribeExamineDataPresetAsset(relativePath, out var descriptor))
        {
            return TrimReferenceDescriptorPrefix(descriptor.DisplayName, "Набор предметов:");
        }

        var stem = Path.GetFileNameWithoutExtension(relativePath);
        if (stem.StartsWith("EX_", StringComparison.OrdinalIgnoreCase))
        {
            stem = stem["EX_".Length..];
        }

        if (stem.EndsWith("_CargoDrop", StringComparison.OrdinalIgnoreCase))
        {
            stem = stem[..^"_CargoDrop".Length];
        }

        return CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(stem)));
    }

    private static string ResolveCargoDropPackagePresetDisplayName(string relativePath)
    {
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        if (stem.StartsWith("Examine_", StringComparison.OrdinalIgnoreCase))
        {
            stem = stem["Examine_".Length..];
        }

        if (stem.EndsWith("_CargoDrop", StringComparison.OrdinalIgnoreCase))
        {
            stem = stem[..^"_CargoDrop".Length];
        }

        return CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(stem)));
    }

    private static bool TryFindQuestGiversSetupMap(UAsset asset, out MapPropertyData mapProperty)
    {
        foreach (var export in asset.Exports.OfType<NormalExport>())
        {
            foreach (var property in export.Data.OfType<MapPropertyData>())
            {
                if (PropertyNamesMatch(property.Name?.ToString(), "QuestGiversSetup"))
                {
                    mapProperty = property;
                    return true;
                }
            }
        }

        mapProperty = null!;
        return false;
    }

    private static string ResolveGameEventLoadoutOptionName(string displayName)
    {
        var label = (displayName ?? string.Empty).Trim();
        if (label.StartsWith("Набор события:", StringComparison.OrdinalIgnoreCase))
        {
            label = label["Набор события:".Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            return "без названия";
        }

        return CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(label)));
    }

    private static string ResolveGameEventLoadoutEntryLabel(string rawReference)
    {
        var candidate = (rawReference ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.Empty;
        }

        var slashIndex = candidate.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < candidate.Length - 1)
        {
            candidate = candidate[(slashIndex + 1)..];
        }

        var dotIndex = candidate.IndexOf('.');
        if (dotIndex > 0)
        {
            candidate = candidate[..dotIndex];
        }

        if (candidate.EndsWith("_C", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[..^2];
        }

        return LocalizeAssetStem(candidate);
    }

    private static string ResolveQuestGiverDisplayName(string rawDisplayValue)
    {
        var label = (rawDisplayValue ?? string.Empty).Trim();
        var markerIndex = label.LastIndexOf('.');
        if (markerIndex >= 0 && markerIndex < label.Length - 1)
        {
            label = label[(markerIndex + 1)..];
        }
        else
        {
            var slashIndex = label.LastIndexOf('/');
            if (slashIndex >= 0 && slashIndex < label.Length - 1)
            {
                label = label[(slashIndex + 1)..];
            }
        }

        if (label.EndsWith("_C", StringComparison.OrdinalIgnoreCase))
        {
            label = label[..^2];
        }

        label = LocalizeAssetStem(label);
        label = label
            .Replace("BP ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Notice Board", "доска заданий", StringComparison.OrdinalIgnoreCase)
            .Replace("Mobile Phone", "телефон", StringComparison.OrdinalIgnoreCase)
            .Replace("Mobile телефон", "телефон", StringComparison.OrdinalIgnoreCase)
            .Replace("General Goods", "общие товары", StringComparison.OrdinalIgnoreCase)
            .Replace("Arms Dealer", "оружейник", StringComparison.OrdinalIgnoreCase)
            .Replace("Mechanic", "механик", StringComparison.OrdinalIgnoreCase)
            .Replace("Doctor", "доктор", StringComparison.OrdinalIgnoreCase)
            .Replace("Bartender", "бармен", StringComparison.OrdinalIgnoreCase)
            .Replace("Banker", "банкир", StringComparison.OrdinalIgnoreCase)
            .Replace("Harbourmaster", "портовый торговец", StringComparison.OrdinalIgnoreCase)
            .Trim();

        return CapitalizeFirst(NormalizeLocalizedLabel(label));
    }

    private static string ResolveFishSpeciesDisplayName(string relativePath)
    {
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        if (stem.StartsWith("SpeciesData_", StringComparison.OrdinalIgnoreCase))
        {
            stem = stem["SpeciesData_".Length..];
        }

        return CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(stem)));
    }

    private static string ResolvePlantDisplayName(string relativePath, string assetPrefix)
    {
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        if (stem.StartsWith(assetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            stem = stem[assetPrefix.Length..];
        }

        return CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(stem)));
    }

    private List<StudioReferenceOptionDto> BuildEncounterCharacterPresetReferenceOptions()
    {
        return BuildBlueprintReferenceOptions(
            relativePath =>
                relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                && relativePath.Contains("/encounters/character_presets/", StringComparison.OrdinalIgnoreCase),
            assetInfo => assetInfo.DisplayName);
    }

    private List<StudioReferenceOptionDto> BuildEncounterClassReferenceOptions()
    {
        return BuildBlueprintReferenceOptions(
            relativePath =>
            {
                if (!relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var normalized = PathUtil.NormalizeRelative(relativePath).ToLowerInvariant();
                if (normalized.Contains("/encounterzones/", StringComparison.Ordinal)
                    || normalized.Contains("/character_presets/", StringComparison.Ordinal))
                {
                    return false;
                }

                var stem = Path.GetFileNameWithoutExtension(normalized);
                return normalized.Contains("/encounters/encounterclasses/", StringComparison.Ordinal)
                    || normalized.Contains("/worldevents/cargodrop/", StringComparison.Ordinal)
                    || normalized.Contains("/cargo_drop/", StringComparison.Ordinal)
                    || stem.Contains("dropship", StringComparison.Ordinal)
                    || stem.Contains("cargodrop", StringComparison.Ordinal)
                    || stem.EndsWith("encounter", StringComparison.Ordinal);
            },
            assetInfo => PrefixReferenceOptionLabel("Класс события", assetInfo.DisplayName));
    }

    private List<StudioReferenceOptionDto> BuildCargoDropEncounterClassReferenceOptions()
    {
        return BuildBlueprintReferenceOptions(
            relativePath =>
            {
                if (!relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var normalized = PathUtil.NormalizeRelative(relativePath).ToLowerInvariant();
                if (normalized.Contains("/worldevents/", StringComparison.Ordinal))
                {
                    return false;
                }

                return normalized.Contains("/encounters/encounterclasses/cargo_drop/", StringComparison.Ordinal)
                    || normalized.Contains("/encounters/encounterclasses/dropship", StringComparison.Ordinal)
                    || normalized.Contains("/cargo_drop/", StringComparison.Ordinal);
            },
            assetInfo => PrefixReferenceOptionLabel("Защита грузового дропа", ResolveCargoDropEncounterReferenceLabel(assetInfo.RelativePath)));
    }

    private static string ResolveCargoDropEncounterReferenceLabel(string relativePath)
    {
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var key = NormalizeAssetKey(stem);
        return key switch
        {
            "bpencountercargodropevent" => "событие грузового дропа",
            "bpencountercargodropeventflyingguardian" => "событие грузового дропа с летающим стражем",
            _ => LocalizeAssetStem(stem)
        };
    }

    private List<StudioReferenceOptionDto> BuildEncounterNpcClassReferenceOptions()
    {
        var result = new List<StudioReferenceOptionDto>(32);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _modAssetsCache ??= BuildModAssets();

        foreach (var assetInfo in _modAssetsCache)
        {
            var relativePath = PathUtil.NormalizeRelative(assetInfo.RelativePath);
            if (!relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                || !relativePath.Contains("/encounters/character_presets/npcs/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryBuildSelectionFromAssetId(assetInfo.AssetId, out var selection))
            {
                continue;
            }

            var sourceWarnings = new List<string>(2);
            var sourceMode = ResolveSourceMode(null, selection.PresetSourcePath is not null);
            var sourcePath = ResolveAssetSourcePath(selection, sourceMode, sourceWarnings, includeCompanions: true);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                continue;
            }

            var readableSourcePath = PrepareIsolatedAssetReadSource(sourcePath);
            var asset = new UAsset(readableSourcePath, EngineVersion.VER_UE4_27, null, CustomSerializationFlags.None);
            foreach (var export in asset.Exports.OfType<NormalExport>())
            {
                for (var i = 0; i < export.Data.Count; i++)
                {
                    var rootProperty = export.Data[i];
                    var rootLabel = GetReadablePropertyName(rootProperty, i);
                    CollectEncounterNpcClassOptions(asset, rootProperty, rootLabel, relativePath, result, seen, 0);
                }
            }
        }

        return result
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void CollectEncounterNpcClassOptions(
        UAsset asset,
        PropertyData property,
        string label,
        string relativePath,
        List<StudioReferenceOptionDto> output,
        HashSet<string> seen,
        int depth)
    {
        if (depth > 6)
        {
            return;
        }

        if (property is StructPropertyData structProperty)
        {
            for (var i = 0; i < structProperty.Value.Count; i++)
            {
                var child = structProperty.Value[i];
                var childName = GetReadablePropertyName(child, i);
                CollectEncounterNpcClassOptions(asset, child, $"{label}.{childName}", relativePath, output, seen, depth + 1);
            }

            return;
        }

        if (property is ArrayPropertyData arrayProperty && arrayProperty.Value is not null)
        {
            for (var i = 0; i < arrayProperty.Value.Length; i++)
            {
                CollectEncounterNpcClassOptions(asset, arrayProperty.Value[i], $"{label}[{i}]", relativePath, output, seen, depth + 1);
            }

            return;
        }

        if (property is not MapPropertyData mapProperty)
        {
            return;
        }

        var userLabel = ToUserFieldLabel(relativePath, label);
        if (!userLabel.Contains("классы персонажей", StringComparison.OrdinalIgnoreCase)
            && !userLabel.Contains("состав пресета npc", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var entry in mapProperty.Value)
        {
            if (!TryExtractReferencePickerValue(asset, entry.Key, out var rawValue, out var displayValue))
            {
                continue;
            }

            if (!seen.Add(rawValue))
            {
                continue;
            }

            output.Add(new StudioReferenceOptionDto(
                rawValue,
                PrefixReferenceOptionLabel("Класс NPC", ResolveEncounterNpcClassDisplayName(displayValue))));
        }
    }

    private static bool TryExtractReferencePickerValue(
        UAsset asset,
        PropertyData property,
        out string rawValue,
        out string displayValue)
    {
        rawValue = string.Empty;
        displayValue = string.Empty;

        switch (property)
        {
            case ObjectPropertyData objectProperty:
                return TryExtractObjectReferencePickerValue(asset, objectProperty.Value, out rawValue, out displayValue);
            case SoftObjectPropertyData softObjectProperty:
                rawValue = ExtractSoftObjectReference(softObjectProperty.Value);
                displayValue = ResolveReferenceDisplayValue(rawValue);
                return !string.IsNullOrWhiteSpace(rawValue);
            case SoftObjectPathPropertyData softObjectPathProperty:
                rawValue = ExtractSoftObjectReference(softObjectPathProperty.Value);
                displayValue = ResolveReferenceDisplayValue(rawValue);
                return !string.IsNullOrWhiteSpace(rawValue);
            default:
                return false;
        }
    }

    private static string ResolveEncounterNpcClassDisplayName(string displayValue)
    {
        var cleaned = ShortenReadableReferenceLabel(displayValue);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "Не распознано";
        }

        var normalized = NormalizeAssetKey(cleaned);
        var match = Regex.Match(
            normalized,
            @"(?:bp)?(?:armednpc)?(?<role>guard|drifter)(?:lvl|level)(?<level>\d+)(?<suffix>.*)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (match.Success)
        {
            var role = match.Groups["role"].Value.Equals("guard", StringComparison.OrdinalIgnoreCase)
                ? "Охранник"
                : "Скиталец";
            var suffix = match.Groups["suffix"].Value;
            var suffixLabel = string.Empty;
            if (suffix.Contains("radiation", StringComparison.OrdinalIgnoreCase))
            {
                suffixLabel = " (радиация)";
            }
            else if (suffix.Contains("abandonedbunker", StringComparison.OrdinalIgnoreCase))
            {
                suffixLabel = " (заброшенный бункер)";
            }

            return $"{role}, уровень {match.Groups["level"].Value}{suffixLabel}";
        }

        return LocalizeAssetStem(cleaned);
    }

    private Dictionary<string, string> BuildSkillScriptClassLookup()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sdkHeaderPath = ResolveScumSdkHeaderPath();
        if (string.IsNullOrWhiteSpace(sdkHeaderPath) || !File.Exists(sdkHeaderPath))
        {
            return result;
        }

        foreach (var line in File.ReadLines(sdkHeaderPath))
        {
            var match = Regex.Match(
                line,
                @"class\s+U(?<name>[A-Za-z0-9_]*Skill)\b",
                RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                continue;
            }

            var className = match.Groups["name"].Value.Trim();
            if (string.IsNullOrWhiteSpace(className))
            {
                continue;
            }

            result[NormalizeLooseSearch(className)] = className;
        }

        return result;
    }

    private static bool IsSkillPickerAssetPath(string relativePath)
    {
        if (!relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
            || !relativePath.Contains("/skills/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("uidata", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("/ui/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(relativePath);
        return !string.IsNullOrWhiteSpace(stem)
            && stem.EndsWith("Skill", StringComparison.OrdinalIgnoreCase)
            && !stem.StartsWith("FC_", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToPascalCaseIdentifier(string value)
    {
        var parts = Regex
            .Split(value ?? string.Empty, @"[^A-Za-z0-9]+")
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value?.Length ?? 0);
        foreach (var part in parts)
        {
            builder.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1)
            {
                builder.Append(part[1..]);
            }
        }

        return builder.ToString();
    }

    private static string PrefixReferenceOptionLabel(string prefix, string displayName)
    {
        var safePrefix = (prefix ?? string.Empty).Trim();
        var safeDisplayName = (displayName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(safePrefix))
        {
            return safeDisplayName;
        }

        if (safeDisplayName.StartsWith($"{safePrefix}:", StringComparison.OrdinalIgnoreCase))
        {
            return safeDisplayName;
        }

        return $"{safePrefix}: {safeDisplayName}";
    }

    private List<StudioReferenceOptionDto> BuildBlueprintReferenceOptions(
        Func<string, bool> includePath,
        Func<StudioModAssetDto, string> buildLabel)
    {
        var result = new List<StudioReferenceOptionDto>(96);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _modAssetsCache ??= BuildModAssets();

        foreach (var assetInfo in _modAssetsCache)
        {
            var relativePath = PathUtil.NormalizeRelative(assetInfo.RelativePath);
            if (!includePath(relativePath))
            {
                continue;
            }

            var value = BuildBlueprintClassReferenceFromRelativePath(relativePath);
            if (!seen.Add(value))
            {
                continue;
            }

            result.Add(new StudioReferenceOptionDto(value, buildLabel(assetInfo)));
        }

        return result
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<StudioReferenceOptionDto> BuildGameObjectReferenceOptions(
        Func<string, bool> includePath,
        string classPackage,
        string className,
        Func<StudioAssetDto, string> buildLabel)
    {
        var result = new List<StudioReferenceOptionDto>(48);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _gameAssetsCache ??= BuildGameAssetRows();

        foreach (var assetInfo in _gameAssetsCache)
        {
            var relativePath = PathUtil.NormalizeRelative(assetInfo.RelativePath);
            if (!includePath(relativePath))
            {
                continue;
            }

            var value = BuildGameObjectReferenceFromRelativePath(relativePath, classPackage, className);
            if (!seen.Add(value))
            {
                continue;
            }

            result.Add(new StudioReferenceOptionDto(value, buildLabel(assetInfo)));
        }

        return result
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<StudioReferenceOptionDto> BuildSoftGameAssetReferenceOptions(
        Func<string, bool> includePath,
        Func<StudioAssetDto, string> buildLabel)
    {
        var result = new List<StudioReferenceOptionDto>(48);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _gameAssetsCache ??= BuildGameAssetRows();

        foreach (var assetInfo in _gameAssetsCache)
        {
            var relativePath = PathUtil.NormalizeRelative(assetInfo.RelativePath);
            if (!includePath(relativePath))
            {
                continue;
            }

            var value = BuildSoftGameAssetReferenceFromRelativePath(relativePath);
            if (!seen.Add(value))
            {
                continue;
            }

            result.Add(new StudioReferenceOptionDto(value, buildLabel(assetInfo)));
        }

        return result
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string? ResolveScumSdkHeaderPath()
    {
        var desktopRoot = Path.Combine(_runtimePaths.WorkspaceRoot, "c_drive_offload", "Desktop");
        if (!Directory.Exists(desktopRoot))
        {
            return null;
        }

        var direct = Directory
            .EnumerateFiles(desktopRoot, "SCUM.hpp", SearchOption.AllDirectories)
            .FirstOrDefault(path => path.Contains("CXXHeaderDump", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        return null;
    }

    private static string ResolveBodyEffectSideEffectDisplayName(string className)
    {
        var suffix = className.StartsWith("PrisonerBodyConditionOrSymptomSideEffect_", StringComparison.OrdinalIgnoreCase)
            ? className["PrisonerBodyConditionOrSymptomSideEffect_".Length..]
            : className;

        return suffix.ToLowerInvariant() switch
        {
            "strengthmodifier" => "Бонус или штраф к силе",
            "intelligencemodifier" => "Бонус или штраф к интеллекту",
            "constitutionmodifier" => "Бонус или штраф к телосложению",
            "dexteritymodifier" => "Бонус или штраф к ловкости",
            "groundmovementspeedmodifier" => "Бонус или штраф к скорости ходьбы и бега",
            "watermovementspeedmodifier" => "Бонус или штраф к скорости плавания",
            "staminamodifier" => "Бонус или штраф к текущей выносливости",
            "maxstaminamodifier" => "Бонус или штраф к максимальной выносливости",
            "performancescoremodifier" => "Бонус или штраф к успешности действий",
            "healingspeedmodifier" => "Бонус или штраф к скорости лечения",
            "immunesystemefficiencymodifier" => "Бонус или штраф к иммунитету",
            "periodicaffect" => "Периодический приступ",
            "damage" => "Урон",
            "blackout" => "Потемнение в глазах",
            "bloodshoteyes" => "Покраснение глаз",
            "blurredvision" => "Затуманенное зрение",
            "disorientation" => "Дезориентация",
            "doublevision" => "Двоение в глазах",
            "eyepressure" => "Давление на глаза",
            "fatigue" => "Усталость",
            "fever" => "Жар",
            "hallucinations" => "Галлюцинации",
            "headache" => "Головная боль",
            "heightenedsenses" => "Обострённые чувства",
            "irritatedthroat" => "Раздражение горла",
            "leukopenia" => "Лейкопения",
            "maxmovementpace" => "Ограничение максимального темпа движения",
            "stuffednose" => "Заложенный нос",
            "unconsciousness" => "Потеря сознания",
            "weakness" => "Слабость",
            _ => NormalizeLocalizedLabel(LocalizeCommonGameplayTerms(HumanizeCamel(suffix)))
        };
    }

    private static string NormalizeLooseSearch(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();
        return new string(chars);
    }

    private static bool MatchesReferenceSearch(
        StudioReferenceOptionDto option,
        IReadOnlyList<string> searchTerms,
        IReadOnlyList<string> normalizedTerms)
    {
        var normalizedLabel = NormalizeLooseSearch(option.Label);
        var normalizedValue = NormalizeLooseSearch(option.Value);
        return searchTerms.Any(searchTerm =>
                   option.Label.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                   || option.Value.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
               || normalizedTerms.Any(loose =>
                   normalizedLabel.Contains(loose, StringComparison.Ordinal)
                   || normalizedValue.Contains(loose, StringComparison.Ordinal));
    }

    private static bool MatchesLooseSearchText(
        string primaryText,
        string secondaryText,
        IReadOnlyList<string> searchTerms,
        IReadOnlyList<string> normalizedTerms)
    {
        var normalizedPrimary = NormalizeLooseSearch(primaryText);
        var normalizedSecondary = NormalizeLooseSearch(secondaryText);
        return searchTerms.Any(searchTerm =>
                   (!string.IsNullOrWhiteSpace(primaryText) && primaryText.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                   || (!string.IsNullOrWhiteSpace(secondaryText) && secondaryText.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
               || normalizedTerms.Any(loose =>
                   (!string.IsNullOrWhiteSpace(normalizedPrimary) && normalizedPrimary.Contains(loose, StringComparison.Ordinal))
                   || (!string.IsNullOrWhiteSpace(normalizedSecondary) && normalizedSecondary.Contains(loose, StringComparison.Ordinal)));
    }

    private static IEnumerable<string> ExpandReferenceSearchTerms(string? pickerKind, string term)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(term))
        {
            return result;
        }

        var trimmed = term.Trim();
        result.Add(trimmed);

        var normalizedTerm = NormalizeLooseSearch(trimmed);
        var normalizedPicker = NormalizeLooseSearch(pickerKind ?? string.Empty);

        void Add(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value);
                }
            }
        }

        if (normalizedPicker == "bodyeffectsideeffect")
        {
            if (normalizedTerm.Contains("сил", StringComparison.Ordinal))
            {
                Add("strength", "strength modifier", "сила");
            }

            if (normalizedTerm.Contains("интел", StringComparison.Ordinal))
            {
                Add("intelligence", "intelligence modifier", "интеллект");
            }

            if (normalizedTerm.Contains("ловк", StringComparison.Ordinal))
            {
                Add("dexterity", "dexterity modifier", "ловкость");
            }

            if (normalizedTerm.Contains("вынослив", StringComparison.Ordinal))
            {
                Add("stamina", "endurance", "max stamina", "выносливость");
            }

            if (normalizedTerm.Contains("телослож", StringComparison.Ordinal)
                || normalizedTerm.Contains("конституц", StringComparison.Ordinal))
            {
                Add("constitution", "constitution modifier", "телосложение");
            }

            if (normalizedTerm.Contains("скор", StringComparison.Ordinal)
                || normalizedTerm.Contains("движ", StringComparison.Ordinal))
            {
                Add("speed", "movement", "ground movement", "water movement", "движение");
            }

            if (normalizedTerm.Contains("урон", StringComparison.Ordinal)
                || normalizedTerm.Contains("боль", StringComparison.Ordinal))
            {
                Add("damage", "урон");
            }

            if (normalizedTerm.Contains("иммун", StringComparison.Ordinal))
            {
                Add("immune", "immune system", "immunity");
            }

            if (normalizedTerm.Contains("зрен", StringComparison.Ordinal)
                || normalizedTerm.Contains("глаз", StringComparison.Ordinal))
            {
                Add("vision", "double vision");
            }

            if (normalizedTerm.Contains("дезори", StringComparison.Ordinal))
            {
                Add("disorientation");
            }

            if (normalizedTerm.Contains("приступ", StringComparison.Ordinal)
                || normalizedTerm.Contains("период", StringComparison.Ordinal))
            {
                Add("periodic", "periodic affect");
            }
        }

        if (normalizedPicker == "foreignsubstanceattribute")
        {
            if (normalizedTerm.Contains("инфек", StringComparison.Ordinal))
            {
                Add("infection", "agent", "агент инфекции");
            }

            if (normalizedTerm.Contains("коф", StringComparison.Ordinal))
            {
                Add("caffeine", "кофеин");
            }

            if (normalizedTerm.Contains("антиб", StringComparison.Ordinal))
            {
                Add("antibiotic", "antibiotics", "антибиотики");
            }

            if (normalizedTerm.Contains("угол", StringComparison.Ordinal)
                || normalizedTerm.Contains("угл", StringComparison.Ordinal))
            {
                Add("charcoal", "activated charcoal", "активированный уголь");
            }
        }

        if (normalizedPicker == "craftingingredientasset" || normalizedPicker == "craftingingredient")
        {
            if (normalizedTerm.Contains("верев", StringComparison.Ordinal)
                || normalizedTerm.Contains("верёв", StringComparison.Ordinal)
                || normalizedTerm.Contains("нит", StringComparison.Ordinal))
            {
                Add("rope", "thread", "string", "верёвка", "нить");
            }

            if (normalizedTerm.Contains("тряп", StringComparison.Ordinal)
                || normalizedTerm.Contains("ткан", StringComparison.Ordinal)
                || normalizedTerm.Contains("cloth", StringComparison.Ordinal))
            {
                Add("rag", "cloth", "fabric", "textile", "тряпка", "ткань");
            }

            if (normalizedTerm.Contains("провол", StringComparison.Ordinal)
                || normalizedTerm.Contains("металл", StringComparison.Ordinal)
                || normalizedTerm.Contains("желез", StringComparison.Ordinal))
            {
                Add("wire", "metal", "scrap", "sheet", "provod", "проволока", "металл");
            }

            if (normalizedTerm.Contains("палк", StringComparison.Ordinal)
                || normalizedTerm.Contains("дерев", StringComparison.Ordinal)
                || normalizedTerm.Contains("доск", StringComparison.Ordinal))
            {
                Add("stick", "wood", "plank", "branch", "палка", "дерево", "доска");
            }

            if (normalizedTerm.Contains("инстру", StringComparison.Ordinal))
            {
                Add("tool", "knife", "axe", "saw", "pliers", "screwdriver", "hammer", "инструмент");
            }

            if (normalizedTerm.Contains("нож", StringComparison.Ordinal)
                || normalizedTerm.Contains("лезв", StringComparison.Ordinal))
            {
                Add("knife", "blade", "razor", "нож");
            }

            if (normalizedTerm.Contains("кам", StringComparison.Ordinal)
                || normalizedTerm.Contains("stone", StringComparison.Ordinal))
            {
                Add("stone", "rock", "камень");
            }

            if (normalizedTerm.Contains("кост", StringComparison.Ordinal))
            {
                Add("bone", "кости", "кость");
            }

            if (normalizedTerm.Contains("кож", StringComparison.Ordinal)
                || normalizedTerm.Contains("шкур", StringComparison.Ordinal))
            {
                Add("leather", "skin", "hide", "fur", "кожа", "шкура");
            }

            if (normalizedTerm.Contains("гвоз", StringComparison.Ordinal))
            {
                Add("nail", "nails", "гвоздь", "гвозди");
            }

            if (normalizedTerm.Contains("еда", StringComparison.Ordinal)
                || normalizedTerm.Contains("пищ", StringComparison.Ordinal))
            {
                Add("food", "meat", "bread", "corn", "cheese", "еда", "пища");
            }

            if (normalizedTerm.Contains("мяс", StringComparison.Ordinal))
            {
                Add("meat", "steak", "organ", "мясо");
            }

            if (normalizedTerm.Contains("вода", StringComparison.Ordinal)
                || normalizedTerm.Contains("жидк", StringComparison.Ordinal))
            {
                Add("water", "liquid", "bottle", "вода", "жидкость");
            }
        }

        if (normalizedPicker == "itemspawnerpreset" || normalizedPicker == "regularitemspawnerpreset")
        {
            if (normalizedTerm.Contains("дом", StringComparison.Ordinal)
                || normalizedTerm.Contains("house", StringComparison.Ordinal))
            {
                Add("house", "дом");
            }

            if (normalizedTerm.Contains("воен", StringComparison.Ordinal)
                || normalizedTerm.Contains("mili", StringComparison.Ordinal))
            {
                Add("military", "военный");
            }

            if (normalizedTerm.Contains("полиц", StringComparison.Ordinal))
            {
                Add("police", "полиция");
            }

            if (normalizedTerm.Contains("мед", StringComparison.Ordinal))
            {
                Add("medical", "medical lab", "медицинский");
            }

            if (normalizedTerm.Contains("рын", StringComparison.Ordinal))
            {
                Add("market", "рынок");
            }

            if (normalizedTerm.Contains("склад", StringComparison.Ordinal)
                || normalizedTerm.Contains("мастер", StringComparison.Ordinal))
            {
                Add("workshop", "warehouse", "склад", "мастерская");
            }
        }

        if (normalizedPicker == "advanceditemspawnerpreset"
            || normalizedPicker == "containerlootpreset"
            || normalizedPicker == "advanceditemspawnersubpreset"
            || normalizedPicker == "containersubpresetpreset")
        {
            if (normalizedTerm.Contains("здан", StringComparison.Ordinal)
                || normalizedTerm.Contains("build", StringComparison.Ordinal))
            {
                Add("buildings", "здания");
            }

            if (normalizedTerm.Contains("ванн", StringComparison.Ordinal))
            {
                Add("bathroom", "ванная");
            }

            if (normalizedTerm.Contains("мед", StringComparison.Ordinal))
            {
                Add("medical", "clinic", "hospital", "медицинский");
            }

            if (normalizedTerm.Contains("воен", StringComparison.Ordinal))
            {
                Add("military", "военный");
            }

            if (normalizedTerm.Contains("бунк", StringComparison.Ordinal)
                || normalizedTerm.Contains("locker", StringComparison.Ordinal))
            {
                Add("bunker", "locker", "оружейный шкаф");
            }
        }

        if (normalizedPicker == "questasset" || normalizedPicker == "questreference")
        {
            if (normalizedTerm.Contains("тел", StringComparison.Ordinal)
                || normalizedTerm.Contains("phone", StringComparison.Ordinal))
            {
                Add("телефон", "phone", "mobile");
            }

            if (normalizedTerm.Contains("аван", StringComparison.Ordinal)
                || normalizedTerm.Contains("outpost", StringComparison.Ordinal))
            {
                Add("аванпост", "outpost");
            }

            if (normalizedTerm.Contains("механ", StringComparison.Ordinal))
            {
                Add("механик", "mechanic");
            }

            if (normalizedTerm.Contains("оруж", StringComparison.Ordinal)
                || normalizedTerm.Contains("arm", StringComparison.Ordinal))
            {
                Add("оружейник", "arms", "dealer");
            }

            if (normalizedTerm.Contains("общ", StringComparison.Ordinal)
                || normalizedTerm.Contains("goods", StringComparison.Ordinal))
            {
                Add("общие товары", "general goods");
            }
        }

        if (normalizedPicker == "vehiclespawnpreset" || normalizedPicker == "vehiclepreset")
        {
            if (normalizedTerm.Contains("маш", StringComparison.Ordinal)
                || normalizedTerm.Contains("авто", StringComparison.Ordinal)
                || normalizedTerm.Contains("трансп", StringComparison.Ordinal))
            {
                Add("vehicle", "car", "transport", "spawn preset", "машина", "транспорт");
            }

            if (normalizedTerm.Contains("байк", StringComparison.Ordinal)
                || normalizedTerm.Contains("мото", StringComparison.Ordinal))
            {
                Add("bike", "motorcycle", "city bike", "dirt bike", "mountain bike");
            }

            if (normalizedTerm.Contains("тракт", StringComparison.Ordinal))
            {
                Add("tractor");
            }
        }

        if (normalizedPicker.StartsWith("gameevent", StringComparison.Ordinal))
        {
            if (normalizedTerm.Contains("авто", StringComparison.Ordinal)
                || normalizedTerm.Contains("винтов", StringComparison.Ordinal)
                || normalizedTerm.Contains("rifle", StringComparison.Ordinal))
            {
                Add("rifles", "ak", "m16", "mp5", "ump", "винтовка", "автомат");
            }

            if (normalizedTerm.Contains("пист", StringComparison.Ordinal))
            {
                Add("pistol", "pistols", "m9", "block", "judge", "deagle", "пистолет");
            }

            if (normalizedTerm.Contains("нож", StringComparison.Ordinal)
                || normalizedTerm.Contains("ближ", StringComparison.Ordinal)
                || normalizedTerm.Contains("melee", StringComparison.Ordinal))
            {
                Add("melee", "katana", "axe", "bushman", "bat", "ближний бой");
            }

            if (normalizedTerm.Contains("одеж", StringComparison.Ordinal)
                || normalizedTerm.Contains("форм", StringComparison.Ordinal)
                || normalizedTerm.Contains("outfit", StringComparison.Ordinal))
            {
                Add("outfit", "outfits", "military", "mma", "bear", "одежда");
            }

            if (normalizedTerm.Contains("гранат", StringComparison.Ordinal)
                || normalizedTerm.Contains("обяз", StringComparison.Ordinal)
                || normalizedTerm.Contains("gear", StringComparison.Ordinal))
            {
                Add("mandatory", "grenades", "обязательное", "снаряжение");
            }
        }

        if (normalizedPicker == "fishspeciesasset" || normalizedPicker == "fishspecies")
        {
            if (normalizedTerm.Contains("карп", StringComparison.Ordinal))
            {
                Add("carp", "карась", "crucian carp", "prussian carp");
            }

            if (normalizedTerm.Contains("сом", StringComparison.Ordinal))
            {
                Add("catfish", "сом");
            }

            if (normalizedTerm.Contains("окун", StringComparison.Ordinal))
            {
                Add("bass", "окунь");
            }

            if (normalizedTerm.Contains("укле", StringComparison.Ordinal))
            {
                Add("bleak", "уклейка");
            }

            if (normalizedTerm.Contains("амур", StringComparison.Ordinal))
            {
                Add("amur", "амур");
            }

            if (normalizedTerm.Contains("голав", StringComparison.Ordinal))
            {
                Add("chub", "голавль");
            }

            if (normalizedTerm.Contains("ерш", StringComparison.Ordinal)
                || normalizedTerm.Contains("ёрш", StringComparison.Ordinal))
            {
                Add("ruffe", "ёрш");
            }

            if (normalizedTerm.Contains("щук", StringComparison.Ordinal)
                || normalizedTerm.Contains("pike", StringComparison.Ordinal))
            {
                Add("pike", "щука");
            }

            if (normalizedTerm.Contains("тун", StringComparison.Ordinal)
                || normalizedTerm.Contains("tuna", StringComparison.Ordinal))
            {
                Add("tuna", "тунец");
            }

            if (normalizedTerm.Contains("сардин", StringComparison.Ordinal)
                || normalizedTerm.Contains("sard", StringComparison.Ordinal))
            {
                Add("sardine", "сардина");
            }

            if (normalizedTerm.Contains("дент", StringComparison.Ordinal)
                || normalizedTerm.Contains("dentex", StringComparison.Ordinal))
            {
                Add("dentex", "дентекс");
            }

            if (normalizedTerm.Contains("орат", StringComparison.Ordinal)
                || normalizedTerm.Contains("orata", StringComparison.Ordinal)
                || normalizedTerm.Contains("дорад", StringComparison.Ordinal))
            {
                Add("orata", "ората", "дорада");
            }

            if (normalizedTerm.Contains("гранат", StringComparison.Ordinal)
                || normalizedTerm.Contains("grenade", StringComparison.Ordinal))
            {
                Add("grenade", "граната");
            }

            if (normalizedTerm.Contains("дилд", StringComparison.Ordinal)
                || normalizedTerm.Contains("dildo", StringComparison.Ordinal))
            {
                Add("dildo", "дилдо");
            }
        }

        if (normalizedPicker == "plantspeciesasset" || normalizedPicker == "plantspecies")
        {
            if (normalizedTerm.Contains("яблок", StringComparison.Ordinal)
                || normalizedTerm.Contains("яблон", StringComparison.Ordinal))
            {
                Add("apple", "apple tree", "яблоня");
            }

            if (normalizedTerm.Contains("брок", StringComparison.Ordinal))
            {
                Add("broccoli", "брокколи");
            }

            if (normalizedTerm.Contains("капуст", StringComparison.Ordinal))
            {
                Add("cabbage", "капуста");
            }

            if (normalizedTerm.Contains("коноп", StringComparison.Ordinal)
                || normalizedTerm.Contains("каннаб", StringComparison.Ordinal))
            {
                Add("cannabis", "конопля");
            }

            if (normalizedTerm.Contains("кукуруз", StringComparison.Ordinal))
            {
                Add("corn", "кукуруза");
            }

            if (normalizedTerm.Contains("карто", StringComparison.Ordinal))
            {
                Add("potato", "картофель");
            }

            if (normalizedTerm.Contains("помид", StringComparison.Ordinal)
                || normalizedTerm.Contains("томат", StringComparison.Ordinal))
            {
                Add("tomato", "томат", "помидор");
            }

            if (normalizedTerm.Contains("тыкв", StringComparison.Ordinal))
            {
                Add("pumpkin", "тыква");
            }
        }

        if (normalizedPicker == "plantpestasset" || normalizedPicker == "plantpest")
        {
            if (normalizedTerm.Contains("тля", StringComparison.Ordinal))
            {
                Add("aphids", "тля");
            }

            if (normalizedTerm.Contains("улит", StringComparison.Ordinal)
                || normalizedTerm.Contains("слиз", StringComparison.Ordinal))
            {
                Add("snails", "slugs", "улитки", "слизни");
            }

            if (normalizedTerm.Contains("черв", StringComparison.Ordinal))
            {
                Add("worms", "cutworms", "черви");
            }
        }

        if (normalizedPicker == "plantdiseaseasset" || normalizedPicker == "plantdisease")
        {
            if (normalizedTerm.Contains("гнил", StringComparison.Ordinal))
            {
                Add("rot", "гниль");
            }

            if (normalizedTerm.Contains("ржав", StringComparison.Ordinal))
            {
                Add("rust", "ржавчина");
            }

            if (normalizedTerm.Contains("плес", StringComparison.Ordinal)
                || normalizedTerm.Contains("милд", StringComparison.Ordinal))
            {
                Add("mould", "downy mildew", "плесень");
            }
        }

        if (normalizedPicker == "encounternpcclass")
        {
            if (normalizedTerm.Contains("охран", StringComparison.Ordinal))
            {
                Add("guard");
            }

            if (normalizedTerm.Contains("скитал", StringComparison.Ordinal))
            {
                Add("drifter");
            }

            if (normalizedTerm.Contains("радиац", StringComparison.Ordinal))
            {
                Add("radiation");
            }
        }

        if (normalizedPicker == "encountercharacterpreset")
        {
            if (normalizedTerm.Contains("зомб", StringComparison.Ordinal))
            {
                Add("zombie");
            }

            if (normalizedTerm.Contains("живот", StringComparison.Ordinal))
            {
                Add("animal");
            }

            if (normalizedTerm.Contains("npc", StringComparison.Ordinal)
                || normalizedTerm.Contains("охран", StringComparison.Ordinal)
                || normalizedTerm.Contains("скитал", StringComparison.Ordinal))
            {
                Add("npc", "guard", "drifter");
            }
        }

        if (normalizedPicker is "craftingingredientasset" or "craftingingredient")
        {
            if (normalizedTerm.Contains("вер", StringComparison.Ordinal)
                || normalizedTerm.Contains("нит", StringComparison.Ordinal))
            {
                Add("rope", "thread", "string", "верёвка", "нить");
            }

            if (normalizedTerm.Contains("тряп", StringComparison.Ordinal)
                || normalizedTerm.Contains("ткан", StringComparison.Ordinal))
            {
                Add("rag", "cloth", "fabric", "тряпка", "ткань");
            }

            if (normalizedTerm.Contains("пал", StringComparison.Ordinal)
                || normalizedTerm.Contains("доск", StringComparison.Ordinal)
                || normalizedTerm.Contains("дерев", StringComparison.Ordinal))
            {
                Add("stick", "wood", "plank", "палка", "дерево", "доска");
            }

            if (normalizedTerm.Contains("металл", StringComparison.Ordinal)
                || normalizedTerm.Contains("провол", StringComparison.Ordinal))
            {
                Add("metal", "wire", "scrap", "sheet", "металл", "проволока");
            }

            if (normalizedTerm.Contains("инстру", StringComparison.Ordinal))
            {
                Add("tool", "knife", "axe", "saw", "screwdriver", "молоток", "инструмент");
            }

            if (normalizedTerm.Contains("вода", StringComparison.Ordinal)
                || normalizedTerm.Contains("жидк", StringComparison.Ordinal)
                || normalizedTerm.Contains("топл", StringComparison.Ordinal))
            {
                Add("water", "fuel", "gasoline", "oil", "вода", "топливо");
            }
        }

        return result;
    }

    public StudioModAssetSchemaDto GetModdingAssetSchema(string assetId)
    {
        var warnings = new List<string>();
        if (!TryBuildSelectionFromAssetId(assetId, out var selection))
        {
            return new StudioModAssetSchemaDto(
                assetId,
                string.Empty,
                "unknown",
                "Unknown",
                "unknown",
                "unknown",
                [],
                [],
                ["Ассет не найден."]);
        }

        var normalized = PathUtil.NormalizeRelative(selection.TargetRelativePath);
        var category = ClassifyModCategory(normalized);
        var extension = Path.GetExtension(normalized).ToLowerInvariant();
        var sourceMode = ResolveSourceMode(null, selection.PresetSourcePath is not null);
        var sourcePath = ResolveAssetSourcePath(selection, sourceMode, warnings, includeCompanions: true);
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return new StudioModAssetSchemaDto(
                assetId,
                normalized,
                category.Id,
                category.Name,
                sourceMode,
                extension.TrimStart('.'),
                [],
                [],
                warnings.Count > 0 ? warnings : ["Не удалось подготовить исходный файл ассета."]);
        }

        return BuildSchemaFromPreparedSource(assetId, normalized, category, sourceMode, sourcePath, selection, warnings);
    }

    public StudioModAssetSchemaDto PreviewModdingAssetSchema(StudioSchemaPreviewRequestDto request)
    {
        var warnings = new List<string>();
        if (request is null || string.IsNullOrWhiteSpace(request.AssetId))
        {
            return new StudioModAssetSchemaDto(
                string.Empty,
                string.Empty,
                "unknown",
                "Unknown",
                "preview",
                "unknown",
                [],
                [],
                ["Не указан assetId для предпросмотра."]);
        }

        if (!TryBuildSelectionFromAssetId(request.AssetId, out var selection))
        {
            return new StudioModAssetSchemaDto(
                request.AssetId,
                string.Empty,
                "unknown",
                "Unknown",
                "preview",
                "unknown",
                [],
                [],
                ["Ассет для предпросмотра не найден."]);
        }

        var normalized = PathUtil.NormalizeRelative(selection.TargetRelativePath);
        var category = ClassifyModCategory(normalized);
        var extension = Path.GetExtension(normalized).ToLowerInvariant();
        var sourceMode = ResolveSourceMode(request.SourceMode, selection.PresetSourcePath is not null);
        var sourcePath = ResolveAssetSourcePath(selection, sourceMode, warnings, includeCompanions: true);
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return new StudioModAssetSchemaDto(
                request.AssetId,
                normalized,
                category.Id,
                category.Name,
                "preview",
                extension.TrimStart('.'),
                [],
                [],
                warnings.Count > 0 ? warnings : ["Не удалось подготовить исходный файл ассета для предпросмотра."]);
        }

        var hasFieldEdits = request.Edits is { Count: > 0 };
        var hasListEdits = request.ListEdits is { Count: > 0 };
        if (!hasFieldEdits && !hasListEdits)
        {
            return BuildSchemaFromPreparedSource(request.AssetId, normalized, category, sourceMode, sourcePath, selection, warnings);
        }

        var previewRoot = Path.Combine(_runtimePaths.TempRoot, "schema-preview", DateTime.Now.ToString("yyyyMMdd-HHmmss"), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(previewRoot);
        var previewAssetPath = Path.Combine(previewRoot, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, previewAssetPath, overwrite: true);
        CopyCompanionFilesForEdit(sourcePath, previewAssetPath);

        var changed = false;
        if (extension.Equals(".uasset", StringComparison.OrdinalIgnoreCase))
        {
            if (hasListEdits)
            {
                changed |= ApplyUassetListEdits(previewAssetPath, request.ListEdits!, warnings);
            }

            if (hasFieldEdits)
            {
                changed |= ApplyUassetEdits(previewAssetPath, request.Edits!, warnings);
            }
        }
        else if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            if (hasListEdits)
            {
                changed |= ApplyJsonListEdits(previewAssetPath, request.ListEdits!, warnings);
            }

            if (hasFieldEdits)
            {
                changed |= ApplyJsonEdits(previewAssetPath, request.Edits!, warnings);
            }
        }
        else
        {
            warnings.Add("Для этого формата предпросмотр изменений пока не поддерживается.");
            return new StudioModAssetSchemaDto(
                request.AssetId,
                normalized,
                category.Id,
                category.Name,
                "preview",
                extension.TrimStart('.'),
                [],
                [],
                warnings);
        }

        if (!changed)
        {
            return new StudioModAssetSchemaDto(
                request.AssetId,
                normalized,
                category.Id,
                category.Name,
                "preview",
                extension.TrimStart('.'),
                [],
                [],
                warnings.Count > 0 ? warnings : ["Изменения для предпросмотра не обнаружены."]);
        }

        warnings.Insert(0, "Показан результат с уже сохранёнными изменениями этой системы.");
        return BuildSchemaFromPreparedSource(request.AssetId, normalized, category, "preview", previewAssetPath, selection, warnings);
    }

    private StudioModAssetSchemaDto BuildSchemaFromPreparedSource(
        string assetId,
        string normalized,
        ModCategory category,
        string sourceMode,
        string sourcePath,
        AssetSelection selection,
        List<string> warnings)
    {
        var extension = Path.GetExtension(normalized).ToLowerInvariant();
        try
        {
            if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                return BuildJsonSchema(assetId, normalized, category, sourceMode, sourcePath, warnings);
            }

            if (extension.Equals(".uasset", StringComparison.OrdinalIgnoreCase))
            {
                return BuildUassetSchema(assetId, normalized, category, sourceMode, sourcePath, selection, warnings);
            }

            warnings.Add("Для этого формата доступен только просмотр и копирование в пак.");
            return new StudioModAssetSchemaDto(
                assetId,
                normalized,
                category.Id,
                category.Name,
                sourceMode,
                extension.TrimStart('.'),
                [],
                [],
                warnings);
        }
        catch (Exception ex)
        {
            warnings.Add($"Не удалось разобрать ассет: {ex.Message}");
            return new StudioModAssetSchemaDto(
                assetId,
                normalized,
                category.Id,
                category.Name,
                sourceMode,
                extension.TrimStart('.'),
                [],
                [],
                warnings);
        }
    }

    private List<StudioModAssetDto> BuildModAssets()
    {
        var pakIndex = GetOrLoadPakIndex();
        var result = new List<StudioModAssetDto>(30000);
        foreach (var path in pakIndex.GetAllRelativePaths())
        {
            if (!IsModdableGameAsset(path))
            {
                continue;
            }

            var normalized = PathUtil.NormalizeRelative(path);
            var extension = Path.GetExtension(normalized).ToLowerInvariant();
            if (extension is not ".uasset" and not ".json" and not ".ini" and not ".csv" and not ".txt")
            {
                continue;
            }

            var category = ClassifyModCategory(normalized);
            if (!IsStudioCategoryEnabled(category.Id, normalized))
            {
                continue;
            }

            var descriptor = DescribeModAsset(normalized, category.Id);
            var supportsSafe = extension is ".uasset" or ".json";
            result.Add(new StudioModAssetDto(
                $"game::{normalized.ToLowerInvariant()}",
                normalized,
                category.Id,
                category.Name,
                descriptor.DisplayName,
                descriptor.Summary,
                extension.TrimStart('.'),
                supportsSafe));
        }

        AppendSyntheticItemSpawningRowAssets(result);

        result.Sort((a, b) =>
        {
            var categoryCompare = string.Compare(a.CategoryName, b.CategoryName, StringComparison.OrdinalIgnoreCase);
            if (categoryCompare != 0)
            {
                return categoryCompare;
            }

            if (a.CategoryId.Equals("crafting-recipes", StringComparison.OrdinalIgnoreCase)
                && b.CategoryId.Equals("crafting-recipes", StringComparison.OrdinalIgnoreCase))
            {
                var aIsRegistry = IsCraftingUiDataRegistryAsset(a.RelativePath);
                var bIsRegistry = IsCraftingUiDataRegistryAsset(b.RelativePath);
                if (aIsRegistry != bIsRegistry)
                {
                    return aIsRegistry ? -1 : 1;
                }
            }

            var displayCompare = string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (displayCompare != 0)
            {
                return displayCompare;
            }

            return string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase);
        });
        return result;
    }

    private List<StudioModAssetDto> BuildVisibleModAssets(List<StudioModAssetDto> rawAssets)
    {
        var result = new List<StudioModAssetDto>(rawAssets.Count);
        foreach (var asset in rawAssets)
        {
            if (ShouldShowAssetInVisibleCatalog(asset))
            {
                result.Add(asset);
            }
        }

        return result;
    }

    private bool ShouldShowAssetInVisibleCatalog(StudioModAssetDto asset)
    {
        if (asset.AssetId.StartsWith(DataTableRowAssetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var relativePath = PathUtil.NormalizeRelative(asset.RelativePath);
        if (IsAlwaysHiddenCatalogLane(relativePath))
        {
            return false;
        }

        return true;
    }

    private static bool IsAlwaysHiddenCatalogLane(string relativePath)
    {
        var path = relativePath.ToLowerInvariant();
        var fileName = Path.GetFileNameWithoutExtension(path);
        return path.Contains("/items/weapons/weapon_clips/", StringComparison.Ordinal)
               || path.Contains("/items/weapons/attachmentsockets/", StringComparison.Ordinal)
               || path.Contains("/items/weapons/attachments/", StringComparison.Ordinal)
               || path.Contains("/items/weapons/weapon_parts/", StringComparison.Ordinal)
               || path.Contains("/items/weapons/malfunctions/", StringComparison.Ordinal)
               || (path.Contains("/items/crafting/ingredients/", StringComparison.Ordinal)
                   && fileName.StartsWith("ci_", StringComparison.Ordinal))
               || path.Contains("/items/fishing/attachmentsockets/", StringComparison.Ordinal)
               || path.Contains("/items/weapons/turrets/turret_01_wip/", StringComparison.Ordinal)
               || path.Contains("/quests/interactables/", StringComparison.Ordinal)
               || path.Contains("/quests/tasksdata/actionmatchers/", StringComparison.Ordinal)
               || path.EndsWith("/quests/bp_noticeboard.uasset", StringComparison.Ordinal)
               || path.EndsWith("/quests/bp_questbin.uasset", StringComparison.Ordinal)
               || path.EndsWith("/quests/bp_questmanager.uasset", StringComparison.Ordinal)
               || path.EndsWith("/quests/bp_questbin_mailbox.uasset", StringComparison.Ordinal)
               || path.Contains("/wwiseaudio/", StringComparison.Ordinal)
               || path.Contains("/foliage/farming/models/", StringComparison.Ordinal)
               || path.EndsWith("/foliage/farming/bp_garden.uasset", StringComparison.Ordinal)
               || path.EndsWith("/foliage/farming/bp_gardenmanager.uasset", StringComparison.Ordinal)
               || path.EndsWith("/economy/table_tradeabledesc.uasset", StringComparison.Ordinal)
               || (path.Contains("/economy/traderservices/", StringComparison.Ordinal)
                   && fileName.StartsWith("servicebp_", StringComparison.Ordinal))
               || path.EndsWith("/worldevents/bp_worldeventmanager.uasset", StringComparison.Ordinal)
               || path.EndsWith("/encounters/bp_globalencountermanager.uasset", StringComparison.Ordinal)
               || path.EndsWith("/encounters/encountermanagercommondata.uasset", StringComparison.Ordinal)
               || path.EndsWith("/encounters/bp_encounterstaticzone.uasset", StringComparison.Ordinal)
               || path.EndsWith("/encounters/encounterzones/bp_mediumthreatzone.uasset", StringComparison.Ordinal)
               || path.EndsWith("/characters/npcs/bp_guardedzonemanager.uasset", StringComparison.Ordinal)
               || path.EndsWith("/encounters/encounterclasses/bb_encounters/bp_basebbflyingattackerencounter.uasset", StringComparison.Ordinal)
               || path.EndsWith("/encounters/encounterclasses/bp_encounterspawnairbornecharactersbase.uasset", StringComparison.Ordinal)
               || (path.Contains("radiation", StringComparison.Ordinal)
                   && path.Contains("/manual/codex/", StringComparison.Ordinal))
               || (path.Contains("radiation", StringComparison.Ordinal)
                   && path.Contains("/models/", StringComparison.Ordinal))
               || (path.Contains("radiation", StringComparison.Ordinal)
                   && path.Contains("/particles/", StringComparison.Ordinal))
               || (path.Contains("radiation", StringComparison.Ordinal)
                   && path.Contains("/items/postspawnactions/", StringComparison.Ordinal));
    }

    private void AppendSyntheticItemSpawningRowAssets(List<StudioModAssetDto> output)
    {
        AppendSyntheticDataTableRowAssets(
            output,
            ItemSpawningParametersLaneId,
            ItemSpawningParametersTableRelativePath,
            "item-spawning",
            "Лут и спавн предметов",
            BuildItemSpawningParametersRowDisplayName,
            BuildItemSpawningParametersRowSummary);

        AppendSyntheticDataTableRowAssets(
            output,
            ItemSpawningCooldownGroupsLaneId,
            ItemSpawningCooldownGroupsTableRelativePath,
            "item-spawning",
            "Лут и спавн предметов",
            BuildItemSpawningCooldownGroupDisplayName,
            BuildItemSpawningCooldownGroupSummary);
    }

    private void AppendSyntheticDataTableRowAssets(
        List<StudioModAssetDto> output,
        string laneId,
        string tableRelativePath,
        string categoryId,
        string categoryName,
        Func<UAsset, StructPropertyData, string> displayNameFactory,
        Func<UAsset, StructPropertyData, string> summaryFactory)
    {
        if (!TryLoadGameDataTableAsset(tableRelativePath, out var asset, out var dataTableExport))
        {
            return;
        }

        foreach (var row in dataTableExport.Table.Data)
        {
            var rowName = row.Name?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(rowName))
            {
                continue;
            }

            output.Add(new StudioModAssetDto(
                $"{DataTableRowAssetPrefix}{laneId}::{rowName}",
                tableRelativePath,
                categoryId,
                categoryName,
                displayNameFactory(asset, row),
                summaryFactory(asset, row),
                "uasset",
                true));
        }
    }

    private bool TryLoadGameDataTableAsset(string tableRelativePath, out UAsset asset, out DataTableExport dataTableExport)
    {
        asset = null!;
        dataTableExport = null!;
        var warnings = new List<string>(2);
        if (!TryResolveGameAssetSource(tableRelativePath, out var sourcePath, warnings, includeCompanions: true)
            || string.IsNullOrWhiteSpace(sourcePath)
            || !File.Exists(sourcePath))
        {
            return false;
        }

        try
        {
            var readableSourcePath = PrepareIsolatedAssetReadSource(sourcePath);
            asset = new UAsset(readableSourcePath, EngineVersion.VER_UE4_27, null, CustomSerializationFlags.None);
            return TryGetFirstDataTableExport(asset, out dataTableExport, out _);
        }
        catch
        {
            asset = null!;
            dataTableExport = null!;
            return false;
        }
    }

    private string BuildItemSpawningParametersRowDisplayName(UAsset asset, StructPropertyData row)
    {
        return $"Правило спавна: {LocalizeItemSpawningRowName(GetDataTableRowName(row))}";
    }

    private string BuildItemSpawningParametersRowSummary(UAsset asset, StructPropertyData row)
    {
        var parts = new List<string>(4);
        var maxOccurrences = FindStructChildProperty<IntPropertyData>(row, "MaxOccurrences", out _);
        if (maxOccurrences is not null)
        {
            parts.Add($"максимум {maxOccurrences.Value}");
        }

        var enabledLocations = GetEnabledItemSpawningLocations(row).Take(3).ToList();
        if (enabledLocations.Count > 0)
        {
            parts.Add($"зоны: {string.Join(", ", enabledLocations)}");
        }

        var variations = FindStructChildProperty<ArrayPropertyData>(row, "Variations", out _);
        if ((variations?.Value?.Length ?? 0) > 0)
        {
            parts.Add($"вариантов: {variations!.Value!.Length}");
        }

        var cooldownGroupLabel = ResolveItemSpawningCooldownGroupFromRow(row);
        if (!string.IsNullOrWhiteSpace(cooldownGroupLabel))
        {
            parts.Add($"откат: {cooldownGroupLabel}");
        }

        return parts.Count > 0
            ? $"{CapitalizeFirst(string.Join("; ", parts))}."
            : "Где предмет может появляться, сколько его можно держать в мире и к какой группе отката он привязан.";
    }

    private string BuildItemSpawningCooldownGroupDisplayName(UAsset asset, StructPropertyData row)
    {
        return $"Группа отката: {LocalizeItemSpawningCooldownGroupName(GetDataTableRowName(row))}";
    }

    private string BuildItemSpawningCooldownGroupSummary(UAsset asset, StructPropertyData row)
    {
        var parts = new List<string>(2);
        var cooldownStruct = FindStructChildProperty<StructPropertyData>(row, "cooldown", out _);
        if (TryExtractFloatInterval(cooldownStruct, out var min, out var max))
        {
            parts.Add(min == max
                ? $"откат {FormatCompactNumber(min)} сек"
                : $"откат {FormatCompactNumber(min)}-{FormatCompactNumber(max)} сек");
        }

        var isAffected = FindStructChildProperty<BoolPropertyData>(row, "IsAffectedByLowerGroups", out _);
        if (isAffected is not null)
        {
            parts.Add(isAffected.Value
                ? "учитывает более низкие группы"
                : "не зависит от более низких групп");
        }

        return parts.Count > 0
            ? $"{CapitalizeFirst(string.Join("; ", parts))}."
            : "Общая группа отката, которую используют правила спавна предметов.";
    }

    private List<string> GetEnabledItemSpawningLocations(StructPropertyData row)
    {
        var allowedLocations = FindStructChildProperty<StructPropertyData>(row, "AllowedLocations", out _);
        if (allowedLocations is null)
        {
            return [];
        }

        var result = new List<string>(8);
        foreach (var child in allowedLocations.Value)
        {
            if (child is BoolPropertyData boolProperty && boolProperty.Value)
            {
                var localized = LocalizeItemSpawningLocationName(child.Name?.ToString() ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(localized))
                {
                    result.Add(localized);
                }
            }
        }

        return result;
    }

    private string ResolveItemSpawningCooldownGroupFromRow(StructPropertyData row)
    {
        var cooldownGroup = FindStructChildProperty<StructPropertyData>(row, "CooldownGroup", out _);
        if (cooldownGroup is null)
        {
            return string.Empty;
        }

        var rowNameProperty = FindStructChildProperty<NamePropertyData>(cooldownGroup, "RowName", out _);
        var rowName = rowNameProperty?.Value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rowName) || rowName.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return "без группы";
        }

        return LocalizeItemSpawningCooldownGroupName(rowName);
    }

    private static bool TryExtractFloatInterval(StructPropertyData? intervalStruct, out double min, out double max)
    {
        min = 0d;
        max = 0d;
        if (intervalStruct is null)
        {
            return false;
        }

        var minProperty = FindStructChildProperty<FloatPropertyData>(intervalStruct, "Min", out _);
        var maxProperty = FindStructChildProperty<FloatPropertyData>(intervalStruct, "Max", out _);
        if (minProperty is null || maxProperty is null)
        {
            return false;
        }

        min = minProperty.Value;
        max = maxProperty.Value;
        return true;
    }

    private static string GetDataTableRowName(StructPropertyData row)
    {
        return row.Name?.ToString() ?? string.Empty;
    }

    private static string FormatCompactNumber(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string LocalizeItemSpawningRowName(string rowName)
    {
        if (string.IsNullOrWhiteSpace(rowName))
        {
            return "Неизвестное правило";
        }

        return CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(rowName)));
    }

    private static string LocalizeItemSpawningCooldownGroupName(string rowName)
    {
        if (string.IsNullOrWhiteSpace(rowName))
        {
            return "Без группы";
        }

        var parts = rowName
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Trim())
            .ToArray();
        if (parts.Length >= 2 && parts[0].Equals("Weapons", StringComparison.OrdinalIgnoreCase))
        {
            var group = parts[1].ToLowerInvariant() switch
            {
                "handguns" => "пистолеты",
                "shotguns" => "дробовики",
                "smgs" => "пистолеты-пулемёты",
                "assaultrifles" => "штурмовые винтовки",
                "rifles" => "винтовки",
                "dmrsnipers" => "DMR и снайперские винтовки",
                "lmgs" => "пулемёты",
                "rpgs" => "гранатомёты",
                "explosives" => "взрывчатка",
                _ => CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(parts[1])))
            };

            if (parts.Length >= 4 && parts[2].Equals("Tier", StringComparison.OrdinalIgnoreCase))
            {
                var tier = parts[3].ToLowerInvariant() switch
                {
                    "low" => "низкий tier",
                    "mid" => "средний tier",
                    "high" => "высокий tier",
                    "veryhigh" => "очень высокий tier",
                    _ => CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(parts[3])))
                };

                return $"{group} / {tier}";
            }

            return group;
        }

        return CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(rowName.Replace('.', ' '))));
    }

    private static string LocalizeItemSpawningLocationName(string rawName)
    {
        return rawName.ToLowerInvariant() switch
        {
            "coastal" => "берег",
            "continantal" => "материк",
            "mountain" => "горы",
            "urban" => "город",
            "rural" => "деревня",
            "industrial" => "промзона",
            "police" => "полиция",
            "militarybasic" => "военная база: базовый уровень",
            "militarymedium" => "военная база: средний уровень",
            "militaryadvanced" => "военная база: высокий уровень",
            "militaryww2" => "военные объекты WW2",
            "sport" => "спорт",
            "market" => "рынок",
            "gasstation" => "заправка",
            "airfield" => "аэродром",
            _ => CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(rawName)))
        };
    }

    private static List<StudioModCategoryDto> BuildModCategories(List<StudioModAssetDto> assets)
    {
        return assets
            .GroupBy(x => x.CategoryId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new StudioModCategoryDto(
                group.Key,
                group.First().CategoryName,
                ResolveCategoryDescription(group.Key),
                group.Count()))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private StudioModAssetSchemaDto BuildJsonSchema(
        string assetId,
        string normalizedRelativePath,
        ModCategory category,
        string sourceMode,
        string sourcePath,
        List<string> warnings)
    {
        var root = JsonNode.Parse(File.ReadAllText(sourcePath));
        if (root is null)
        {
            warnings.Add("JSON пустой или повреждён.");
            return new StudioModAssetSchemaDto(
                assetId,
                normalizedRelativePath,
                category.Id,
                category.Name,
                sourceMode,
                "json",
                [],
                [],
                warnings);
        }

        var fields = new List<StudioModFieldDto>(320);
        var listTargets = new List<StudioModListTargetDto>(120);
        var rootLabel = BuildRootDisplayName(normalizedRelativePath);
        CollectJsonSchema(
            root,
            "$",
            rootLabel,
            normalizedRelativePath,
            fields,
            listTargets,
            depth: 0);
        if (fields.Count == 0)
        {
            warnings.Add("Безопасные скалярные поля не найдены.");
        }

        return new StudioModAssetSchemaDto(
            assetId,
            normalizedRelativePath,
            category.Id,
            category.Name,
            sourceMode,
            "json",
            fields.Take(600).ToList(),
            listTargets.Take(180).ToList(),
            warnings);
    }

    private static void CollectJsonSchema(
        JsonNode node,
        string path,
        string label,
        string relativePath,
        List<StudioModFieldDto> fields,
        List<StudioModListTargetDto> listTargets,
        int depth)
    {
        if (depth > 12)
        {
            return;
        }

        if (node is JsonObject obj)
        {
            foreach (var pair in obj)
            {
                if (pair.Value is null)
                {
                    continue;
                }

                var childPath = $"{path}.{pair.Key}";
                var childLabel = $"{label}.{pair.Key}";
                CollectJsonSchema(
                    pair.Value,
                    childPath,
                    childLabel,
                    relativePath,
                    fields,
                    listTargets,
                    depth + 1);
            }

            return;
        }

        if (node is JsonArray arr)
        {
            var listLabel = ToUserFieldLabel(relativePath, label);
            listTargets.Add(new StudioModListTargetDto(
                path,
                listLabel,
                ResolveListTargetDescription(relativePath, listLabel),
                ResolveJsonArrayItemKind(arr),
                arr.Count,
                SupportsAddClone: arr.Count > 0,
                SupportsRemove: arr.Count > 0,
                SupportsClear: arr.Count > 0,
                SupportsAddEmpty: true));

            for (var i = 0; i < arr.Count; i++)
            {
                var value = arr[i];
                if (value is null)
                {
                    continue;
                }

                CollectJsonSchema(
                    value,
                    $"{path}[{i}]",
                    $"{label}[{i}]",
                    relativePath,
                    fields,
                    listTargets,
                    depth + 1);
            }

            return;
        }

        if (node is not JsonValue scalar)
        {
            return;
        }

        if (scalar.TryGetValue(out bool boolValue))
        {
            TryAddSafeField(fields, relativePath, path, label, "bool", boolValue ? "true" : "false", boolValue ? "true" : "false");
            return;
        }

        if (scalar.TryGetValue(out int intValue))
        {
            var value = intValue.ToString(CultureInfo.InvariantCulture);
            TryAddSafeField(fields, relativePath, path, label, "int", value, value);
            return;
        }

        if (scalar.TryGetValue(out long longValue))
        {
            var value = longValue.ToString(CultureInfo.InvariantCulture);
            TryAddSafeField(fields, relativePath, path, label, "long", value, value);
            return;
        }

        if (scalar.TryGetValue(out double doubleValue))
        {
            var value = doubleValue.ToString(CultureInfo.InvariantCulture);
            TryAddSafeField(fields, relativePath, path, label, "double", value, value);
            return;
        }

        if (scalar.TryGetValue(out string? textValue) && textValue is not null)
        {
            TryAddSafeField(fields, relativePath, path, label, "string", textValue, textValue);
        }
    }

    private StudioModAssetSchemaDto BuildUassetSchema(
        string assetId,
        string normalizedRelativePath,
        ModCategory category,
        string sourceMode,
        string sourcePath,
        AssetSelection selection,
        List<string> warnings)
    {
        var fields = new List<StudioModFieldDto>(512);
        var listTargets = new List<StudioModListTargetDto>(120);
        var readableSourcePath = PrepareIsolatedAssetReadSource(sourcePath);
        var asset = new UAsset(readableSourcePath, EngineVersion.VER_UE4_27, null, CustomSerializationFlags.None);
        if (!string.IsNullOrWhiteSpace(selection.SyntheticLaneId)
            && !string.IsNullOrWhiteSpace(selection.SyntheticRowName))
        {
            return BuildSyntheticDataTableRowSchema(
                assetId,
                normalizedRelativePath,
                category,
                sourceMode,
                asset,
                selection,
                warnings);
        }

        if (IsCraftingUiDataRegistryAsset(normalizedRelativePath))
        {
            return BuildCraftingUiDataSchema(
                assetId,
                normalizedRelativePath,
                category,
                sourceMode,
                asset,
                warnings);
        }

        if (IsCraftingRecipeAsset(normalizedRelativePath))
        {
            return BuildCraftingRecipeSchema(
                assetId,
                normalizedRelativePath,
                category,
                sourceMode,
                asset,
                warnings);
        }

        if (IsStarterSpawnEquipmentAsset(normalizedRelativePath))
        {
            return BuildStarterSpawnEquipmentSchema(
                assetId,
                normalizedRelativePath,
                category,
                sourceMode,
                asset,
                warnings);
        }

        for (var exportIndex = 0; exportIndex < asset.Exports.Count; exportIndex++)
        {
            if (asset.Exports[exportIndex] is not NormalExport normalExport)
            {
                continue;
            }

            var exportName = ResolveExportLabel(asset, normalExport, normalizedRelativePath, exportIndex);
            if (normalExport.Data.Count == 0)
            {
                continue;
            }

            for (var i = 0; i < normalExport.Data.Count; i++)
            {
                var rootProperty = normalExport.Data[i];
                var rootName = GetReadablePropertyName(rootProperty, i);
                var fieldPath = $"e:{exportIndex}/p:{i}";
                var label = $"{exportName}.{rootName}";
                CollectUassetFields(
                    asset,
                    rootProperty,
                    fieldPath,
                    label,
                    normalizedRelativePath,
                    fields,
                    listTargets,
                    depth: 0);
            }
        }

        AppendSyntheticSchemaFields(asset, normalizedRelativePath, fields);
        AppendSyntheticSchemaListTargets(asset, normalizedRelativePath, listTargets);

        if (fields.Count == 0)
        {
            warnings.Add(ResolveNoSafeFieldWarning(normalizedRelativePath, listTargets.Count > 0));
        }

        var prettyFields = fields;

        return new StudioModAssetSchemaDto(
            assetId,
            normalizedRelativePath,
            category.Id,
            category.Name,
            sourceMode,
            "uasset",
            prettyFields.Take(800).ToList(),
            listTargets.Take(200).ToList(),
            warnings);
    }

    private StudioModAssetSchemaDto BuildSyntheticDataTableRowSchema(
        string assetId,
        string normalizedRelativePath,
        ModCategory category,
        string sourceMode,
        UAsset asset,
        AssetSelection selection,
        List<string> warnings)
    {
        if (!TryGetFirstDataTableExport(asset, out var dataTableExport, out var exportIndex))
        {
            warnings.Add("Не удалось открыть игровую таблицу для анализа.");
            return new StudioModAssetSchemaDto(
                assetId,
                normalizedRelativePath,
                category.Id,
                category.Name,
                sourceMode,
                "uasset",
                [],
                [],
                warnings);
        }

        if (!TryFindDataTableRow(dataTableExport, selection.SyntheticRowName ?? string.Empty, out var row, out var rowIndex))
        {
            warnings.Add("Не удалось найти нужную строку в игровой таблице.");
            return new StudioModAssetSchemaDto(
                assetId,
                normalizedRelativePath,
                category.Id,
                category.Name,
                sourceMode,
                "uasset",
                [],
                [],
                warnings);
        }

        return (selection.SyntheticLaneId ?? string.Empty).ToLowerInvariant() switch
        {
            ItemSpawningParametersLaneId => BuildItemSpawningParametersRowSchema(
                assetId,
                normalizedRelativePath,
                category,
                sourceMode,
                asset,
                exportIndex,
                row,
                rowIndex,
                warnings),
            ItemSpawningCooldownGroupsLaneId => BuildItemSpawningCooldownGroupRowSchema(
                assetId,
                normalizedRelativePath,
                category,
                sourceMode,
                asset,
                exportIndex,
                row,
                rowIndex,
                warnings),
            _ => new StudioModAssetSchemaDto(
                assetId,
                normalizedRelativePath,
                category.Id,
                category.Name,
                sourceMode,
                "uasset",
                [],
                [],
                ["Эта строка таблицы пока не поддерживается в студии."])
        };
    }

    private StudioModAssetSchemaDto BuildItemSpawningParametersRowSchema(
        string assetId,
        string normalizedRelativePath,
        ModCategory category,
        string sourceMode,
        UAsset asset,
        int exportIndex,
        StructPropertyData row,
        int rowIndex,
        List<string> warnings)
    {
        var fields = new List<StudioModFieldDto>(64);
        var listTargets = new List<StudioModListTargetDto>(8);
        var values = row.Value ?? [];
        for (var i = 0; i < values.Count; i++)
        {
            var child = values[i];
            var childName = child.Name?.ToString() ?? GetReadablePropertyName(child, i);
            if (string.Equals(childName, "CooldownGroup", StringComparison.OrdinalIgnoreCase)
                && child is StructPropertyData cooldownGroupStruct)
            {
                AppendItemSpawningCooldownGroupField(fields, cooldownGroupStruct, exportIndex, rowIndex, i);
                continue;
            }

            if (string.Equals(childName, "Variations", StringComparison.OrdinalIgnoreCase)
                && child is ArrayPropertyData variationsArray)
            {
                AppendItemSpawningVariationsListTarget(
                    asset,
                    listTargets,
                    variationsArray,
                    exportIndex,
                    rowIndex,
                    i,
                    normalizedRelativePath);
                continue;
            }

            CollectUassetFields(
                asset,
                child,
                $"e:{exportIndex}/r:{rowIndex}/p:{i}",
                $"Правило спавна {LocalizeItemSpawningRowName(GetDataTableRowName(row))}.{GetReadablePropertyName(child, i)}",
                normalizedRelativePath,
                fields,
                listTargets,
                depth: 0);
        }

        if (fields.Count == 0)
        {
            warnings.Add(ResolveNoSafeFieldWarning(normalizedRelativePath, listTargets.Count > 0));
        }

        return new StudioModAssetSchemaDto(
            assetId,
            normalizedRelativePath,
            category.Id,
            category.Name,
            sourceMode,
            "uasset",
            fields.Take(800).ToList(),
            listTargets.Take(200).ToList(),
            warnings);
    }

    private static void AppendItemSpawningVariationsListTarget(
        UAsset asset,
        List<StudioModListTargetDto> listTargets,
        ArrayPropertyData variationsArray,
        int exportIndex,
        int rowIndex,
        int propertyIndex,
        string relativePath)
    {
        var values = variationsArray.Value ?? [];
        var itemKind = ResolveItemSpawningArrayItemKind(variationsArray, values);
        List<string>? entryLabels = null;
        if (itemKind.Equals("reference", StringComparison.OrdinalIgnoreCase))
        {
            entryLabels = values
                .Select((value, index) => ResolveArrayEntryLabel(asset, relativePath, "Варианты предмета", value, index))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
        }

        listTargets.Add(new StudioModListTargetDto(
            $"e:{exportIndex}/r:{rowIndex}/p:{propertyIndex}",
            "Варианты предмета",
            ResolveListTargetDescription(relativePath, "Варианты предмета"),
            itemKind,
            values.Length,
            SupportsAddClone: false,
            SupportsRemove: values.Length > 0,
            SupportsClear: values.Length > 0,
            SupportsAddEmpty: false,
            SupportsAddReference: itemKind.Equals("reference", StringComparison.OrdinalIgnoreCase),
            ReferencePickerKind: itemKind.Equals("reference", StringComparison.OrdinalIgnoreCase) ? "item-asset" : null,
            ReferencePickerPrompt: itemKind.Equals("reference", StringComparison.OrdinalIgnoreCase)
                ? "Найди предмет, который должен входить в варианты появления для этого правила спавна."
                : null,
            EntryLabels: entryLabels));
    }

    private static string ResolveItemSpawningArrayItemKind(ArrayPropertyData arrayProperty, PropertyData[] values)
    {
        if (values.Length > 0)
        {
            return ResolveUassetArrayItemKind(values);
        }

        var arrayType = arrayProperty.ArrayType?.ToString() ?? string.Empty;
        if (arrayType.Equals("ObjectProperty", StringComparison.OrdinalIgnoreCase)
            || arrayType.Equals("SoftObjectProperty", StringComparison.OrdinalIgnoreCase)
            || arrayType.Equals("SoftObjectPathProperty", StringComparison.OrdinalIgnoreCase))
        {
            return "reference";
        }

        return "unknown";
    }

    private StudioModAssetSchemaDto BuildItemSpawningCooldownGroupRowSchema(
        string assetId,
        string normalizedRelativePath,
        ModCategory category,
        string sourceMode,
        UAsset asset,
        int exportIndex,
        StructPropertyData row,
        int rowIndex,
        List<string> warnings)
    {
        var fields = new List<StudioModFieldDto>(24);
        var listTargets = new List<StudioModListTargetDto>(4);
        var values = row.Value ?? [];
        for (var i = 0; i < values.Count; i++)
        {
            var child = values[i];
            if (child is StructPropertyData intervalStruct
                && string.Equals(child.Name?.ToString(), "cooldown", StringComparison.OrdinalIgnoreCase))
            {
                AppendItemSpawningCooldownIntervalFields(fields, intervalStruct, exportIndex, rowIndex, i);
                continue;
            }

            CollectUassetFields(
                asset,
                child,
                $"e:{exportIndex}/r:{rowIndex}/p:{i}",
                $"Группа отката {LocalizeItemSpawningCooldownGroupName(GetDataTableRowName(row))}.{GetReadablePropertyName(child, i)}",
                normalizedRelativePath,
                fields,
                listTargets,
                depth: 0);
        }

        if (fields.Count == 0)
        {
            warnings.Add(ResolveNoSafeFieldWarning(normalizedRelativePath, listTargets.Count > 0));
        }

        return new StudioModAssetSchemaDto(
            assetId,
            normalizedRelativePath,
            category.Id,
            category.Name,
            sourceMode,
            "uasset",
            fields.Take(800).ToList(),
            listTargets.Take(200).ToList(),
            warnings);
    }

    private static void AppendItemSpawningCooldownIntervalFields(
        List<StudioModFieldDto> fields,
        StructPropertyData intervalStruct,
        int exportIndex,
        int rowIndex,
        int propertyIndex)
    {
        var minProperty = FindStructChildProperty<FloatPropertyData>(intervalStruct, "Min", out var minIndex);
        if (minProperty is not null)
        {
            var currentValue = minProperty.Value.ToString(CultureInfo.InvariantCulture);
            fields.Add(new StudioModFieldDto(
                $"e:{exportIndex}/r:{rowIndex}/p:{propertyIndex}/p:{minIndex}",
                "Время отката (минимум)",
                "Минимальная пауза перед тем, как эта группа отката снова разрешит появление предметов.",
                "Откат появления",
                "float",
                "number",
                currentValue,
                true,
                "0",
                "86400",
                null,
                null,
                null,
                currentValue));
        }

        var maxProperty = FindStructChildProperty<FloatPropertyData>(intervalStruct, "Max", out var maxIndex);
        if (maxProperty is not null)
        {
            var currentValue = maxProperty.Value.ToString(CultureInfo.InvariantCulture);
            fields.Add(new StudioModFieldDto(
                $"e:{exportIndex}/r:{rowIndex}/p:{propertyIndex}/p:{maxIndex}",
                "Время отката (максимум)",
                "Максимальная пауза перед тем, как эта группа отката снова разрешит появление предметов.",
                "Откат появления",
                "float",
                "number",
                currentValue,
                true,
                "0",
                "86400",
                null,
                null,
                null,
                currentValue));
        }
    }

    private void AppendItemSpawningCooldownGroupField(
        List<StudioModFieldDto> fields,
        StructPropertyData cooldownGroupStruct,
        int exportIndex,
        int rowIndex,
        int propertyIndex)
    {
        var rowNameProperty = FindStructChildProperty<NamePropertyData>(cooldownGroupStruct, "RowName", out var rowNameIndex);
        if (rowNameProperty is null)
        {
            return;
        }

        var currentValue = rowNameProperty.Value?.ToString();
        if (string.IsNullOrWhiteSpace(currentValue))
        {
            currentValue = "None";
        }

        fields.Add(new StudioModFieldDto(
            $"e:{exportIndex}/r:{rowIndex}/p:{propertyIndex}/p:{rowNameIndex}",
            "Группа отката",
            "Выбери общую группу отката, которая управляет задержкой повторного появления этого предмета. Если группа пустая, предмет работает без общей таблицы отката.",
            "Откат появления",
            "name",
            "select",
            currentValue,
            true,
            null,
            null,
            GetItemSpawningCooldownGroupFieldOptions(),
            null,
            null,
            currentValue.Equals("None", StringComparison.OrdinalIgnoreCase)
                ? "Без группы отката"
                : LocalizeItemSpawningCooldownGroupName(currentValue)));
    }

    private List<StudioModFieldOptionDto> GetItemSpawningCooldownGroupFieldOptions()
    {
        lock (_sync)
        {
            return _itemSpawningCooldownGroupFieldOptionsCache ??= BuildItemSpawningCooldownGroupFieldOptions();
        }
    }

    private List<StudioModFieldOptionDto> BuildItemSpawningCooldownGroupFieldOptions()
    {
        var options = new List<StudioModFieldOptionDto>
        {
            new("None", "Без группы отката")
        };

        if (!TryLoadGameDataTableAsset(ItemSpawningCooldownGroupsTableRelativePath, out _, out var dataTableExport))
        {
            return options;
        }

        foreach (var row in dataTableExport.Table.Data)
        {
            var rowName = row.Name?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(rowName))
            {
                continue;
            }

            options.Add(new StudioModFieldOptionDto(
                rowName,
                LocalizeItemSpawningCooldownGroupName(rowName)));
        }

        return options
            .DistinctBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private StudioModAssetSchemaDto BuildCraftingRecipeSchema(
        string assetId,
        string normalizedRelativePath,
        ModCategory category,
        string sourceMode,
        UAsset asset,
        List<string> warnings)
    {
        if (!TryGetFirstNormalExport(asset, out var normalExport, out var exportIndex))
        {
            warnings.Add("Не удалось открыть рецепт для анализа.");
            return new StudioModAssetSchemaDto(
                assetId,
                normalizedRelativePath,
                category.Id,
                category.Name,
                sourceMode,
                "uasset",
                [],
                [],
                warnings);
        }

        var fields = new List<StudioModFieldDto>(180);
        var listTargets = new List<StudioModListTargetDto>(8);

        var product = FindTopLevelProperty<SoftObjectPropertyData>(normalExport, "Product", out _);
        if (product is not null)
        {
            fields.Add(new StudioModFieldDto(
                $"{RecipeFieldPrefix}product",
                "Что создаётся",
                "Выбери предмет, который игрок получит после успешного крафта.",
                "Результат",
                "soft-object",
                "item-picker",
                ExtractSoftObjectReference(product.Value),
                true,
                null,
                null,
                null));
        }
        else
        {
            warnings.Add("У рецепта не найден итоговый предмет.");
        }

        var productQuantity = FindTopLevelProperty<IntPropertyData>(normalExport, "ProductQuantity", out _);
        fields.Add(new StudioModFieldDto(
            $"{RecipeFieldPrefix}product-quantity",
            "Сколько предметов получается",
            "Сколько готовых предметов выдаёт рецепт за одно создание.",
            "Результат",
            "int",
            "number",
            (productQuantity?.Value ?? 1).ToString(CultureInfo.InvariantCulture),
            true,
            "1",
            "999",
            null));

        var relevantSkill = FindTopLevelProperty<ObjectPropertyData>(normalExport, "RelevantSkill", out _);
        var skillCurrentValue = string.Empty;
        var skillCurrentDisplayValue = string.Empty;
        if (relevantSkill is not null)
        {
            if (!TryExtractObjectReferencePickerValue(asset, relevantSkill.Value, out skillCurrentValue, out skillCurrentDisplayValue))
            {
                skillCurrentDisplayValue = ResolveObjectReferenceLabel(asset, relevantSkill.Value);
            }
        }

        fields.Add(new StudioModFieldDto(
            $"{RecipeFieldPrefix}skill-info",
            "Какой навык влияет на рецепт",
            relevantSkill is null
                ? "Выбери навык, который будет определять скорость, сложность и прокачку этого рецепта. Если поле ещё отсутствует, студия добавит его при сохранении."
                : "Выбери навык, который будет определять скорость, сложность и прокачку этого рецепта.",
            "Навык",
            "object",
            "reference-picker",
            skillCurrentValue,
            true,
            null,
            null,
            null,
            "skill-asset",
            "Найди навык, который должен влиять на этот рецепт, и выбери его из списка.",
            skillCurrentDisplayValue));

        AddRecipePerSkillFloatFields(
            fields,
            FindTopLevelProperty<StructPropertyData>(normalExport, "Duration", out _),
            $"{RecipeFieldPrefix}duration:",
            "Время крафта",
            "Сколько секунд длится создание рецепта на каждом уровне навыка.",
            "Время крафта",
            min: "0",
            max: "3600");

        AddRecipePerSkillFloatFields(
            fields,
            FindTopLevelProperty<StructPropertyData>(normalExport, "ExperienceReward", out _),
            $"{RecipeFieldPrefix}exp:",
            "Опыт за крафт",
            "Сколько опыта получает игрок за успешный крафт.",
            "Награда",
            min: "0",
            max: "5000");

        AddRecipePerSkillFloatFields(
            fields,
            FindTopLevelProperty<StructPropertyData>(normalExport, "FamePointReward", out _),
            $"{RecipeFieldPrefix}fame:",
            "Очки славы за крафт",
            "Сколько очков славы выдаётся за успешный крафт.",
            "Награда",
            min: "0",
            max: "100");

        var ingredients = FindTopLevelProperty<ArrayPropertyData>(normalExport, "Ingredients", out var ingredientsIndex);
        if (ingredients is not null)
        {
            var slots = ingredients.Value ?? [];
            listTargets.Add(new StudioModListTargetDto(
                $"e:{exportIndex}/p:{ingredientsIndex}",
                "Слоты ингредиентов",
                "Добавляй, копируй или убирай слоты ингредиентов, чтобы собрать свой состав рецепта.",
                "ingredient-slot",
                slots.Length,
                SupportsAddClone: slots.Length > 0,
                SupportsRemove: slots.Length > 0,
                SupportsClear: slots.Length > 0,
                SupportsAddEmpty: false));

            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] is not StructPropertyData ingredientSlot)
                {
                    continue;
                }

                var section = $"Ингредиент {i + 1}";
                var allowedTypes = FindStructChildProperty<ArrayPropertyData>(ingredientSlot, "AllowedTypes", out _);
                var allowedValues = allowedTypes?.Value ?? [];
                var entryLabels = allowedValues
                    .Select((value, entryIndex) => ResolveArrayEntryLabel(asset, normalizedRelativePath, "Варианты ингредиента", value, entryIndex))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToList();

                listTargets.Add(new StudioModListTargetDto(
                    $"{SyntheticRecipeAllowedTypesTargetPrefix}{exportIndex}:{ingredientsIndex}:{i}",
                    $"{section}: допустимые группы и предметы",
                    "Какие группы ингредиентов или готовые предметы можно положить в этот слот рецепта. Если список пока отсутствует в ассете, студия создаст его сама.",
                    "reference",
                    allowedValues.Length,
                    SupportsAddClone: false,
                    SupportsRemove: allowedValues.Length > 0,
                    SupportsClear: allowedValues.Length > 0,
                    SupportsAddEmpty: false,
                    SupportsAddReference: true,
                    ReferencePickerKind: "crafting-ingredient-asset",
                    ReferencePickerPrompt: "Найди группу или игровой предмет, который должен подходить в этот слот рецепта.",
                    EntryLabels: entryLabels));

                fields.Add(new StudioModFieldDto(
                    $"{RecipeFieldPrefix}ingredient:{i}:allowed-display",
                    "Что подходит в слот",
                    "Справка по этому слоту: какие группы или предметы сюда можно положить.",
                    section,
                    "string",
                    "text",
                    ResolveCraftingAllowedTypesSummary(asset, FindStructChildProperty<ArrayPropertyData>(ingredientSlot, "AllowedTypes", out _)),
                    false,
                    null,
                    null,
                    null));

                var purpose = FindStructChildProperty<EnumPropertyData>(ingredientSlot, "Purpose", out _);
                fields.Add(new StudioModFieldDto(
                    $"{RecipeFieldPrefix}ingredient:{i}:purpose",
                    "Роль ингредиента",
                    "Определи, это расходный материал или инструмент, который нужен только как условие рецепта. Если настройки ещё нет, студия безопасно добавит её.",
                    section,
                    "enum",
                    "select",
                    purpose?.Value?.ToString() ?? "ECraftingIngredientPurpose::Material",
                    true,
                    null,
                    null,
                    CraftingIngredientPurposeOptions));

                var mixingType = FindStructChildProperty<EnumPropertyData>(ingredientSlot, "MixingType", out _);
                fields.Add(new StudioModFieldDto(
                    $"{RecipeFieldPrefix}ingredient:{i}:mixing",
                    "Как можно смешивать варианты",
                    "Разрешать ли смешивать разные варианты подходящих предметов в одном слоте рецепта. Если этой настройки нет, студия добавит её сама.",
                    section,
                    "enum",
                    "select",
                    mixingType?.Value?.ToString() ?? "ECraftingIngredientMixingType::NoMixing",
                    true,
                    null,
                    null,
                    CraftingIngredientMixingTypeOptions));

                var consumeWhole = FindStructChildProperty<BoolPropertyData>(ingredientSlot, "ShouldConsumeWhole", out _);
                fields.Add(new StudioModFieldDto(
                    $"{RecipeFieldPrefix}ingredient:{i}:consume-whole",
                    "Тратить предмет целиком",
                    "Если включено, слот забирает весь предмет, а не только часть его ресурса. Если флага нет, студия создаст его при сохранении.",
                    section,
                    "bool",
                    "toggle",
                    consumeWhole is not null && consumeWhole.Value ? "true" : "false",
                    true,
                    null,
                    null,
                    null));

                var isResource = FindStructChildProperty<BoolPropertyData>(ingredientSlot, "IsResource", out _);
                fields.Add(new StudioModFieldDto(
                    $"{RecipeFieldPrefix}ingredient:{i}:resource",
                    "Это жидкость или ресурс",
                    "Включай только для воды, топлива и других ресурсных ингредиентов, которые расходуются частями. Если настройки нет, студия добавит её.",
                    section,
                    "bool",
                    "toggle",
                    isResource is not null && isResource.Value ? "true" : "false",
                    true,
                    null,
                    null,
                    null));

                var liters = FindStructChildProperty<FloatPropertyData>(ingredientSlot, "Liters", out _);
                fields.Add(new StudioModFieldDto(
                    $"{RecipeFieldPrefix}ingredient:{i}:liters",
                    "Сколько литров нужно",
                    "Сколько литров жидкости или ресурса должен содержать этот слот рецепта. Если поля ещё нет, студия создаст его.",
                    section,
                    "float",
                    "number",
                    (liters?.Value ?? 0f).ToString(CultureInfo.InvariantCulture),
                    true,
                    "0",
                    "20",
                    null));

                var nutrientInclusion = FindStructChildProperty<FloatPropertyData>(ingredientSlot, "NutrientInclusionFactor", out _);
                fields.Add(new StudioModFieldDto(
                    $"{RecipeFieldPrefix}ingredient:{i}:nutrient",
                    "Вклад в питательность",
                    "Какая доля питательных свойств этого ингредиента переносится в готовый результат. Если поля нет, студия безопасно добавит его.",
                    section,
                    "float",
                    "number",
                    (nutrientInclusion?.Value ?? 0f).ToString(CultureInfo.InvariantCulture),
                    true,
                    "0",
                    "5",
                    null));

                AddRecipePerSkillIntFields(
                    fields,
                    FindStructChildProperty<StructPropertyData>(ingredientSlot, "Amount", out _),
                    $"{RecipeFieldPrefix}ingredient:{i}:amount:",
                    "Сколько нужно",
                    "Сколько единиц этого ингредиента нужно для крафта.",
                    section,
                    min: "0",
                    max: "999");

                AddRecipePerSkillIntFields(
                    fields,
                    FindStructChildProperty<StructPropertyData>(ingredientSlot, "AdditionalAmount", out _),
                    $"{RecipeFieldPrefix}ingredient:{i}:additional:",
                    "Дополнительно сверху",
                    "Сколько предметов сверху может потребовать этот слот рецепта.",
                    section,
                    min: "0",
                    max: "999");

                var qualityInfluence = FindStructChildProperty<FloatPropertyData>(ingredientSlot, "ProductQualityInfluence", out _);
                fields.Add(new StudioModFieldDto(
                    $"{RecipeFieldPrefix}ingredient:{i}:quality",
                    "Влияние на качество результата",
                    "Насколько этот ингредиент влияет на качество готового предмета. Если поля нет, студия добавит его при сохранении.",
                    section,
                    "float",
                    "number",
                    (qualityInfluence?.Value ?? 0f).ToString(CultureInfo.InvariantCulture),
                    true,
                    "0",
                    "5",
                    null));

                var returnOnUncraft = FindStructChildProperty<BoolPropertyData>(ingredientSlot, "ReturnOnUncraft", out _);
                fields.Add(new StudioModFieldDto(
                    $"{RecipeFieldPrefix}ingredient:{i}:return",
                    "Возвращать при разборе",
                    "Нужно ли возвращать этот ингредиент при обратном разборе предмета. Если этой настройки нет, студия создаст её сама.",
                    section,
                    "bool",
                    "toggle",
                    returnOnUncraft is not null && returnOnUncraft.Value ? "true" : "false",
                    true,
                    null,
                    null,
                    null));

                AddRecipePerSkillIntFields(
                    fields,
                    FindStructChildProperty<StructPropertyData>(ingredientSlot, "DamagePercentageOnUncraft", out _),
                    $"{RecipeFieldPrefix}ingredient:{i}:damage:",
                    "Потеря прочности при разборе (%)",
                    "Какой процент прочности теряет этот ингредиент при обратном разборе.",
                    section,
                    min: "0",
                    max: "100");
            }
        }
        else
        {
            warnings.Add("У рецепта не найден список ингредиентов.");
        }

        return new StudioModAssetSchemaDto(
            assetId,
            normalizedRelativePath,
            category.Id,
            category.Name,
            sourceMode,
            "uasset",
            fields,
            listTargets,
            warnings);
    }

    private StudioModAssetSchemaDto BuildCraftingUiDataSchema(
        string assetId,
        string normalizedRelativePath,
        ModCategory category,
        string sourceMode,
        UAsset asset,
        List<string> warnings)
    {
        if (!TryGetFirstNormalExport(asset, out var normalExport, out var exportIndex))
        {
            warnings.Add("Не удалось открыть реестр крафта для анализа.");
            return new StudioModAssetSchemaDto(
                assetId,
                normalizedRelativePath,
                category.Id,
                category.Name,
                sourceMode,
                "uasset",
                [],
                [],
                warnings);
        }

        var fields = new List<StudioModFieldDto>(4);
        var listTargets = new List<StudioModListTargetDto>(64);

        var itemCategories = FindTopLevelProperty<ArrayPropertyData>(normalExport, "ItemCategories", out var itemCategoriesIndex);
        var placeableCategories = FindTopLevelProperty<ArrayPropertyData>(normalExport, "PlaceableCategories", out var placeableCategoriesIndex);

        fields.Add(new StudioModFieldDto(
            "crafting-registry:item-categories",
            "Категорий предметного крафта",
            "Сколько категорий предметов использует меню крафта.",
            "Обзор",
            "int",
            "text",
            ((itemCategories?.Value ?? []).Length).ToString(CultureInfo.InvariantCulture),
            false,
            null,
            null,
            null));

        fields.Add(new StudioModFieldDto(
            "crafting-registry:placeable-categories",
            "Категорий строительства",
            "Сколько категорий строительства и базы использует меню крафта.",
            "Обзор",
            "int",
            "text",
            ((placeableCategories?.Value ?? []).Length).ToString(CultureInfo.InvariantCulture),
            false,
            null,
            null,
            null));

        if (itemCategories is not null)
        {
            AppendCraftingUiCategoryRecipeTargets(
                asset,
                normalizedRelativePath,
                exportIndex,
                itemCategories,
                itemCategoriesIndex,
                isPlaceable: false,
                listTargets);
        }

        if (placeableCategories is not null)
        {
            AppendCraftingUiCategoryRecipeTargets(
                asset,
                normalizedRelativePath,
                exportIndex,
                placeableCategories,
                placeableCategoriesIndex,
                isPlaceable: true,
                listTargets);
        }

        if (listTargets.Count == 0)
        {
            warnings.Add("В реестре крафта не найдены категории с массивами рецептов.");
        }

        return new StudioModAssetSchemaDto(
            assetId,
            normalizedRelativePath,
            category.Id,
            category.Name,
            sourceMode,
            "uasset",
            fields,
            listTargets,
            warnings);
    }

    private void AppendCraftingUiCategoryRecipeTargets(
        UAsset asset,
        string normalizedRelativePath,
        int exportIndex,
        ArrayPropertyData categories,
        int categoriesPropertyIndex,
        bool isPlaceable,
        List<StudioModListTargetDto> output)
    {
        var categoryValues = categories.Value ?? [];
        for (var i = 0; i < categoryValues.Length; i++)
        {
            if (categoryValues[i] is not StructPropertyData categoryStruct)
            {
                continue;
            }

            var tagStruct = FindStructChildProperty<StructPropertyData>(categoryStruct, "Tag", out _);
            var tagValue = ResolveGameplayTagStructValue(tagStruct);
            var categoryLabel = ResolveCraftingUiCategoryLabel(tagValue, isPlaceable);
            var recipes = EnsureStructObjectArrayChild(asset, categoryStruct, "Recipes");
            var recipeValues = recipes.Value ?? [];
            var entryLabels = recipeValues
                .Select((value, entryIndex) => ResolveArrayEntryLabel(asset, normalizedRelativePath, "Рецепты категории", value, entryIndex))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
            var pickerKind = isPlaceable
                ? "crafting-placeable-recipe-asset"
                : "crafting-item-recipe-asset";

            output.Add(new StudioModListTargetDto(
                $"{SyntheticCraftingUiCategoryRecipesTargetPrefix}{exportIndex}:{categoriesPropertyIndex}:{i}",
                $"Общий пул крафта: {categoryLabel}",
                "Какие рецепты входят в эту категорию меню крафта. Добавляй новые рецепты из игры или убирай лишние без ручного редактирования внутренних структур.",
                "reference",
                recipeValues.Length,
                SupportsAddClone: false,
                SupportsRemove: recipeValues.Length > 0,
                SupportsClear: recipeValues.Length > 0,
                SupportsAddEmpty: false,
                SupportsAddReference: true,
                ReferencePickerKind: pickerKind,
                ReferencePickerPrompt: isPlaceable
                    ? "Найди рецепт строительства, который должен появиться в этой категории."
                    : "Найди рецепт предмета, который должен появиться в этой категории.",
                EntryLabels: entryLabels));
        }
    }

    private static string ResolveGameplayTagStructValue(StructPropertyData? tagStruct)
    {
        if (tagStruct is null)
        {
            return string.Empty;
        }

        var nameProperty = tagStruct.Value
            .OfType<NamePropertyData>()
            .FirstOrDefault(property => string.Equals(property.Name?.ToString(), "TagName", StringComparison.OrdinalIgnoreCase));
        return nameProperty?.Value?.ToString() ?? string.Empty;
    }

    private static string ResolveCraftingUiCategoryLabel(string tagValue, bool isPlaceable)
    {
        if (string.IsNullOrWhiteSpace(tagValue))
        {
            return isPlaceable ? "Категория строительства" : "Категория предметов";
        }

        var normalized = tagValue.StartsWith("CraftingCategory.", StringComparison.OrdinalIgnoreCase)
            ? tagValue["CraftingCategory.".Length..]
            : tagValue;
        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return isPlaceable ? "Категория строительства" : "Категория предметов";
        }

        var localizedParts = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            var localized = ResolveCraftingUiCategoryTokenLabel(part);
            if (string.IsNullOrWhiteSpace(localized))
            {
                continue;
            }

            localizedParts.Add(localized);
        }

        if (localizedParts.Count == 0)
        {
            return isPlaceable ? "Категория строительства" : "Категория предметов";
        }

        return string.Join(" / ", localizedParts);
    }

    private static string ResolveCraftingUiCategoryTokenLabel(string token)
    {
        return token switch
        {
            "Items" => "Предметы",
            "Placeables" => "Постройки",
            "BaseBuilding" => "База",
            "Ammunition" => "Боеприпасы",
            "Bullets" => "Пули",
            "ArrowsAndBolts" => "Стрелы и болты",
            "MeleeWeapons" => "Ближний бой",
            "RangedWeapons" => "Дальний бой",
            "Firearms" => "Огнестрел",
            "Attachments" => "Обвес и крепления",
            "BowsAndCrossbows" => "Луки и арбалеты",
            "Explosives" => "Взрывчатка",
            "Clothing" => "Одежда",
            "Equipment" => "Снаряжение",
            "GeneralBuildingElements" => "Общие элементы",
            "OutdoorElements" => "Уличные элементы",
            "Furniture" => "Мебель",
            "Utility" => "Служебные объекты",
            "Storage" => "Хранилища",
            "Lights" => "Освещение",
            "Defenses" => "Оборона",
            "Traps" => "Ловушки",
            "Flags" => "Флаги",
            "Favorites" => "Избранное",
            _ => LocalizeAssetStem(token)
        };
    }

    private StudioModAssetSchemaDto BuildStarterSpawnEquipmentSchema(
        string assetId,
        string normalizedRelativePath,
        ModCategory category,
        string sourceMode,
        UAsset asset,
        List<string> warnings)
    {
        if (!TryGetFirstNormalExport(asset, out var normalExport, out var exportIndex))
        {
            warnings.Add("Не удалось открыть стартовый предмет для анализа.");
            return new StudioModAssetSchemaDto(
                assetId,
                normalizedRelativePath,
                category.Id,
                category.Name,
                sourceMode,
                "uasset",
                [],
                [],
                warnings);
        }

        var fields = new List<StudioModFieldDto>(24);

        var itemClass = FindTopLevelProperty<SoftObjectPropertyData>(normalExport, "ItemClass", out _);
        if (itemClass is null)
        {
            warnings.Add("У стартового предмета не найдено поле выдаваемого предмета. При сохранении студия создаст его заново.");
        }

        fields.Add(new StudioModFieldDto(
            $"{StarterSpawnFieldPrefix}item",
            "Какой предмет выдавать",
            "Выбери предмет, который попадёт игроку в стартовый набор.",
            "Стартовый предмет",
            "soft-object",
            "item-picker",
            itemClass is null ? string.Empty : ExtractSoftObjectReference(itemClass.Value),
            true,
            null,
            null,
            null));

        var equipType = FindTopLevelProperty<EnumPropertyData>(normalExport, "EquipType", out _);
        if (equipType is null)
        {
            warnings.Add("У стартового предмета не найдено поле места выдачи. При сохранении студия создаст его заново.");
        }

        var equipValue = equipType?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(equipValue))
        {
            equipValue = "EPrisonerItemEquipType::Inventory";
        }

        fields.Add(new StudioModFieldDto(
            $"{StarterSpawnFieldPrefix}equip",
            "Куда положить предмет",
            "Определи, где у персонажа появится этот предмет при старте.",
            "Стартовый предмет",
            "enum",
            "select",
            equipValue,
            true,
            null,
            null,
            StarterEquipOptions));

        var biomeRequirement = FindTopLevelProperty<EnumPropertyData>(normalExport, "BiomeRequriment", out _)
            ?? FindTopLevelProperty<EnumPropertyData>(normalExport, "BiomeRequirement", out _);
        if (biomeRequirement is null)
        {
            warnings.Add("У стартового предмета не найдено поле ограничения по биому. При сохранении студия создаст его заново.");
        }

        var biomeValue = biomeRequirement?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(biomeValue))
        {
            biomeValue = "EBiomeType::None";
        }

        fields.Add(new StudioModFieldDto(
            $"{StarterSpawnFieldPrefix}biome",
            "Ограничение по биому",
            "Если нужно, предмет будет выдаваться только для выбранного биома старта.",
            "Условия выдачи",
            "enum",
            "select",
            biomeValue,
            true,
            null,
            null,
            StarterBiomeOptions));

        var conditionProperty = FindTopLevelProperty<StructPropertyData>(normalExport, "Condition", out _);
        var parsedCondition = new StarterSpawnConditionModel();
        string? parseWarning = null;
        var parsedConditionOk = conditionProperty is not null
            && TryParseStarterSpawnCondition(conditionProperty, out parsedCondition, out parseWarning);
        var conditionModel = parsedConditionOk
            ? parsedCondition
            : new StarterSpawnConditionModel();

        if (!string.IsNullOrWhiteSpace(parseWarning))
        {
            warnings.Add(parseWarning);
        }

        var conditionEditable = conditionProperty is null || conditionModel.SupportsSafeRewrite;
        if (conditionProperty is null)
        {
            warnings.Add("У стартового предмета нет отдельного блока условий. Если ты задашь ограничения ниже, студия создаст их автоматически.");
        }
        else if (!conditionEditable)
        {
            warnings.Add("Условия выдачи у этого стартового предмета используют нестандартную схему. Их можно просмотреть, но изменять в студии сейчас нельзя.");
        }

        warnings.Add("Для каждого типа персонажа можно отдельно указать: разрешить, запретить или не добавлять отдельное правило.");

        foreach (var flag in StarterSpawnFlags)
        {
            var currentValue = "ignore";
            if (conditionModel.RequiredTags.Contains(flag.Tag))
            {
                currentValue = "require";
            }
            else if (conditionModel.ForbiddenTags.Contains(flag.Tag))
            {
                currentValue = "exclude";
            }

            fields.Add(new StudioModFieldDto(
                $"{StarterSpawnFieldPrefix}flag:{flag.Key}",
                flag.Label,
                flag.Description,
                "Условия выдачи",
                "enum",
                "select",
                currentValue,
                conditionEditable,
                null,
                null,
                StarterConditionModeOptions));
        }

        foreach (var character in StarterSpawnCharacters)
        {
            var currentValue = "ignore";
            if (conditionModel.AnyTags.Contains(character.Tag))
            {
                currentValue = "allow";
            }
            else if (conditionModel.ForbiddenTags.Contains(character.Tag))
            {
                currentValue = "exclude";
            }

            fields.Add(new StudioModFieldDto(
                $"{StarterSpawnFieldPrefix}character:{character.Key}",
                character.Label,
                "Разреши, запрети или вообще не указывай это правило для выбранного типа персонажа.",
                "Типы персонажей",
                "enum",
                "select",
                currentValue,
                conditionEditable,
                null,
                null,
                StarterCharacterModeOptions));
        }

        return new StudioModAssetSchemaDto(
            assetId,
            normalizedRelativePath,
            category.Id,
            category.Name,
            sourceMode,
            "uasset",
            fields,
            [],
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static bool IsStarterSpawnEquipmentAsset(string relativePath)
    {
        var path = relativePath.ToLowerInvariant();
        return path.Contains("/data/spawnequipment/", StringComparison.Ordinal)
               && path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCraftingRecipeAsset(string relativePath)
    {
        var path = relativePath.ToLowerInvariant();
        return path.Contains("/items/crafting/recipes/", StringComparison.Ordinal)
               && path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCraftingUiDataRegistryAsset(string relativePath)
    {
        var path = PathUtil.NormalizeRelative(relativePath);
        return path.Equals(CraftingUiDataRegistryRelativePath, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveCraftingRecipeReferenceClassName(string relativePath)
    {
        var path = PathUtil.NormalizeRelative(relativePath).ToLowerInvariant();
        return path.Contains("/items/crafting/recipes/placeables/", StringComparison.Ordinal)
            ? "PlaceableCraftingRecipe"
            : "ItemCraftingRecipe";
    }

    private static bool TryGetFirstDataTableExport(UAsset asset, out DataTableExport dataTableExport, out int exportIndex)
    {
        for (var i = 0; i < asset.Exports.Count; i++)
        {
            if (asset.Exports[i] is not DataTableExport candidate)
            {
                continue;
            }

            dataTableExport = candidate;
            exportIndex = i;
            return true;
        }

        dataTableExport = null!;
        exportIndex = -1;
        return false;
    }

    private static bool TryFindDataTableRow(DataTableExport dataTableExport, string rowName, out StructPropertyData row, out int rowIndex)
    {
        var rows = dataTableExport.Table.Data;
        for (var i = 0; i < rows.Count; i++)
        {
            var candidateName = rows[i].Name?.ToString() ?? string.Empty;
            if (!string.Equals(candidateName, rowName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            row = rows[i];
            rowIndex = i;
            return true;
        }

        row = null!;
        rowIndex = -1;
        return false;
    }

    private static bool TryGetFirstNormalExport(UAsset asset, out NormalExport normalExport, out int exportIndex)
    {
        for (var i = 0; i < asset.Exports.Count; i++)
        {
            if (asset.Exports[i] is not NormalExport candidate)
            {
                continue;
            }

            normalExport = candidate;
            exportIndex = i;
            return true;
        }

        normalExport = null!;
        exportIndex = -1;
        return false;
    }

    private static TProperty? FindTopLevelProperty<TProperty>(
        NormalExport export,
        string propertyName,
        out int index)
        where TProperty : PropertyData
    {
        for (var i = 0; i < export.Data.Count; i++)
        {
            if (export.Data[i] is not TProperty typed)
            {
                continue;
            }

            if (!string.Equals(typed.Name?.ToString(), propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            index = i;
            return typed;
        }

        index = -1;
        return null;
    }

    private static TProperty? FindStructChildProperty<TProperty>(
        StructPropertyData structProperty,
        string propertyName,
        out int index)
        where TProperty : PropertyData
    {
        var values = structProperty.Value ?? [];
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not TProperty typed)
            {
                continue;
            }

            if (!string.Equals(typed.Name?.ToString(), propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            index = i;
            return typed;
        }

        index = -1;
        return null;
    }

    private static void AppendSyntheticSchemaFields(UAsset asset, string relativePath, List<StudioModFieldDto> output)
    {
        if (IsForeignSubstanceAttributeModifierAsset(asset, relativePath)
            && TryFindForeignSubstanceModifierExport(asset, out var normalExport, out _))
        {
            AppendForeignSubstanceAttributeFields(normalExport, output);
        }

        if (IsRangedWeaponAssetPath(relativePath)
            && TryFindWeaponOwnerExport(asset, out var weaponExport, out _))
        {
            AppendSyntheticWeaponFields(weaponExport, output);
        }
    }

    private void AppendSyntheticSchemaListTargets(UAsset asset, string relativePath, List<StudioModListTargetDto> output)
    {
        AppendSyntheticCargoDropContainerListTargets(asset, relativePath, output);

        if (!SupportsSyntheticSideEffects(relativePath))
        {
            return;
        }

        if (!TryFindSideEffectsArray(asset, out _, out _, out var sideEffects)
            && GetSideEffectTemplates().Count == 0)
        {
            return;
        }

        var values = sideEffects?.Value ?? [];
        var sideEffectsTargetPath = sideEffects is null
            ? SyntheticSideEffectsTargetPath
            : ResolveSideEffectsTargetPath(asset, sideEffects);
        var entryLabels = values
            .Select((value, index) => ResolveArrayEntryLabel(asset, relativePath, "Побочные эффекты", value, index))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        var enhancedTarget = new StudioModListTargetDto(
            sideEffectsTargetPath,
            "Побочные эффекты",
            "Выбери, что именно должно происходить с персонажем: бонус к силе, штраф к ловкости, урон, изменение скорости и другие последствия. Студия сама создаст безопасный игровой эффект внутри ассета.",
            "reference",
            values.Length,
            SupportsAddClone: false,
            SupportsRemove: values.Length > 0,
            SupportsClear: values.Length > 0,
            SupportsAddEmpty: false,
            SupportsAddReference: true,
            ReferencePickerKind: "bodyeffect-side-effect",
            ReferencePickerPrompt: "Найди, какое последствие нужно добавить в этот эффект.",
            EntryLabels: entryLabels);

        var existingIndex = output.FindIndex(target =>
            string.Equals(target.TargetPath, sideEffectsTargetPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(target.TargetPath, SyntheticSideEffectsTargetPath, StringComparison.OrdinalIgnoreCase)
            || (string.Equals(target.Label, "Побочные эффекты", StringComparison.OrdinalIgnoreCase)
                && string.Equals(target.TargetPath, sideEffectsTargetPath, StringComparison.OrdinalIgnoreCase)));
        if (existingIndex >= 0)
        {
            output[existingIndex] = enhancedTarget;
            return;
        }

        output.Add(enhancedTarget);
    }

    private void AppendSyntheticCargoDropContainerListTargets(UAsset asset, string relativePath, List<StudioModListTargetDto> output)
    {
        if (!IsCargoDropContainerAsset(relativePath) || !TryFindCargoDropContainerOwnerExport(asset, out var ownerExport))
        {
            return;
        }

        var majorSpawnerOptions = FindTopLevelProperty<ArrayPropertyData>(ownerExport, "MajorSpawnerOptions", out _);
        if (majorSpawnerOptions is null || majorSpawnerOptions.Value is null)
        {
            UpsertSyntheticListTarget(
                output,
                new StudioModListTargetDto(
                    SyntheticCargoMajorSpawnerOptionsTargetPath,
                    "Основные обычные пресеты лута",
                    ResolveListTargetDescription(relativePath, "Основные обычные пресеты лута"),
                    "reference",
                    0,
                    SupportsAddClone: false,
                    SupportsRemove: false,
                    SupportsClear: false,
                    SupportsAddEmpty: false,
                    SupportsAddReference: true,
                    ReferencePickerKind: "item-spawner-preset",
                    ReferencePickerPrompt: "Найди обычный пресет появления предметов, который контейнер грузового дропа должен уметь выбирать."));
        }

        var majorSpawnerPresetOptions = FindTopLevelProperty<ArrayPropertyData>(ownerExport, "MajorSpawnerPresetOptions", out _);
        if (majorSpawnerPresetOptions is null || majorSpawnerPresetOptions.Value is null)
        {
            UpsertSyntheticListTarget(
                output,
                new StudioModListTargetDto(
                    SyntheticCargoMajorSpawnerPresetOptionsTargetPath,
                    "Основные расширенные пресеты лута",
                    ResolveListTargetDescription(relativePath, "Основные расширенные пресеты лута"),
                    "reference",
                    0,
                    SupportsAddClone: false,
                    SupportsRemove: false,
                    SupportsClear: false,
                    SupportsAddEmpty: false,
                    SupportsAddReference: true,
                    ReferencePickerKind: "advanced-item-spawner-preset",
                    ReferencePickerPrompt: "Найди расширенный пресет лута, который грузовой дроп должен уметь выбирать."));
        }

        var minorSpawnerOptions = FindTopLevelProperty<ArrayPropertyData>(ownerExport, "MinorSpawnerOptions", out _);
        if (minorSpawnerOptions is null || minorSpawnerOptions.Value is null)
        {
            UpsertSyntheticListTarget(
                output,
                new StudioModListTargetDto(
                    SyntheticCargoMinorSpawnerOptionsTargetPath,
                    "Дополнительные наборы лута",
                    ResolveListTargetDescription(relativePath, "Дополнительные наборы лута"),
                    "struct",
                    0,
                    SupportsAddClone: false,
                    SupportsRemove: false,
                    SupportsClear: false,
                    SupportsAddEmpty: true,
                    SupportsAddReference: false,
                    ReferencePickerKind: null,
                    ReferencePickerPrompt: null));
        }
    }

    private static void UpsertSyntheticListTarget(List<StudioModListTargetDto> output, StudioModListTargetDto target)
    {
        var existingIndex = output.FindIndex(existing =>
            string.Equals(existing.TargetPath, target.TargetPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(existing.Label, target.Label, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            output[existingIndex] = target;
            return;
        }

        output.Add(target);
    }

    private static bool SupportsSyntheticSideEffects(string relativePath)
    {
        var normalized = relativePath.ToLowerInvariant();
        return normalized.Contains("/bodyeffects/", StringComparison.Ordinal)
               || normalized.Contains("/metabolism/foreignsubstances/", StringComparison.Ordinal);
    }

    private static bool TryFindSideEffectsArray(
        UAsset asset,
        out NormalExport ownerExport,
        out int ownerExportIndex,
        out ArrayPropertyData? sideEffects)
    {
        for (var exportIndex = 0; exportIndex < asset.Exports.Count; exportIndex++)
        {
            if (asset.Exports[exportIndex] is not NormalExport export)
            {
                continue;
            }

            var arrayProperty = FindTopLevelProperty<ArrayPropertyData>(export, "_sideEffects", out _);
            if (arrayProperty is null)
            {
                continue;
            }

            ownerExport = export;
            ownerExportIndex = exportIndex;
            sideEffects = arrayProperty;
            return true;
        }

        for (var exportIndex = 0; exportIndex < asset.Exports.Count; exportIndex++)
        {
            if (asset.Exports[exportIndex] is not NormalExport export)
            {
                continue;
            }

            if ((export.ObjectName?.ToString() ?? string.Empty).StartsWith("Default__", StringComparison.OrdinalIgnoreCase))
            {
                ownerExport = export;
                ownerExportIndex = exportIndex;
                sideEffects = null;
                return true;
            }
        }

        sideEffects = null;
        if (TryGetFirstNormalExport(asset, out ownerExport, out ownerExportIndex))
        {
            return true;
        }

        ownerExport = null!;
        ownerExportIndex = -1;
        return false;
    }

    private static string ResolveSideEffectsTargetPath(UAsset asset, ArrayPropertyData sideEffects)
    {
        for (var exportIndex = 0; exportIndex < asset.Exports.Count; exportIndex++)
        {
            if (asset.Exports[exportIndex] is not NormalExport export)
            {
                continue;
            }

            var propertyIndex = export.Data.FindIndex(property => ReferenceEquals(property, sideEffects));
            if (propertyIndex >= 0)
            {
                return $"e:{exportIndex}/p:{propertyIndex}";
            }
        }

        return SyntheticSideEffectsTargetPath;
    }

    private static bool IsForeignSubstanceAttributeModifierAsset(UAsset asset, string relativePath)
    {
        if (!relativePath.Contains("/metabolism/foreignsubstances/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IsForeignSubstanceAttributeModifierAsset(asset);
    }

    private static bool IsForeignSubstanceAttributeModifierAsset(UAsset asset)
    {
        return asset.Imports.Any(import =>
            string.Equals(import.ObjectName?.ToString(), "PrisonerForeignSubstance_AttributeModifier", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRangedWeaponAssetPath(string relativePath)
    {
        if (!relativePath.Contains("/items/weapons/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (relativePath.Contains("/items/weapons/ranged_weapons/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var stem = Path.GetFileNameWithoutExtension(relativePath);
        return stem.StartsWith("weapon_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryFindWeaponOwnerExport(UAsset asset, out NormalExport export, out int exportIndex)
    {
        var anchorProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DamagePerShot",
            "MaxLoadedAmmo",
            "EventMaxAmmo",
            "DefaultAmmunitionItemClass",
            "ROF",
            "MaxRange",
            "ViewKickMultiplier",
            "MaxRecoilOffset",
            "RecoilRecoverySpeed",
            "UseCustomWeaponViewKickData",
            "WeaponViewKickData"
        };

        for (var i = 0; i < asset.Exports.Count; i++)
        {
            if (asset.Exports[i] is not NormalExport candidate)
            {
                continue;
            }

            var hasAnchorProperty = candidate.Data.Any(property =>
                anchorProperties.Any(anchor => PropertyNamesMatch(property.Name?.ToString(), anchor)));
            if (!hasAnchorProperty)
            {
                continue;
            }

            export = candidate;
            exportIndex = i;
            return true;
        }

        return TryGetFirstNormalExport(asset, out export, out exportIndex);
    }

    private static FloatPropertyData? FindTopLevelFloatPropertyLoose(NormalExport export, string propertyName)
    {
        return export.Data
            .OfType<FloatPropertyData>()
            .FirstOrDefault(property => PropertyNamesMatch(property.Name?.ToString(), propertyName));
    }

    private static bool PropertyNamesMatch(string? left, string? right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return NormalizePropertyName(left) == NormalizePropertyName(right);
    }

    private static string NormalizePropertyName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch))
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }

    private static bool TryFindForeignSubstanceModifierExport(UAsset asset, out NormalExport export, out int exportIndex)
    {
        var anchorProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "_disposition",
            "_absorptionRate",
            "_baseDiscardRate",
            "_strengthChange",
            "_strengthModifier",
            "_constitutionChange",
            "_constitutionModifier",
            "_dexterityChange",
            "_dexterityModifier",
            "_intelligenceChange",
            "_intelligenceModifier"
        };

        for (var i = 0; i < asset.Exports.Count; i++)
        {
            if (asset.Exports[i] is not NormalExport candidate)
            {
                continue;
            }

            var hasAnchorProperty = candidate.Data.Any(property =>
                anchorProperties.Any(anchor => PropertyNamesMatch(property.Name?.ToString(), anchor)));
            if (!hasAnchorProperty)
            {
                continue;
            }

            export = candidate;
            exportIndex = i;
            return true;
        }

        return TryGetFirstNormalExport(asset, out export, out exportIndex);
    }

    private static void AppendForeignSubstanceAttributeFields(
        NormalExport export,
        List<StudioModFieldDto> output)
    {
        var specs = new[]
        {
            ("_strengthChange", "Прямое изменение силы", "На сколько очков вещество сразу повышает или понижает силу. Если параметра ещё нет в файле, студия добавит его сама при сохранении.", "Влияние на характеристики"),
            ("_strengthModifier", "Влияние на силу", "Дополнительный бонус или штраф к силе, пока вещество активно. Если параметра ещё нет в файле, студия добавит его сама при сохранении.", "Влияние на характеристики"),
            ("_constitutionChange", "Прямое изменение телосложения", "На сколько очков вещество сразу повышает или понижает телосложение. Если параметра ещё нет в файле, студия добавит его сама при сохранении.", "Влияние на характеристики"),
            ("_constitutionModifier", "Влияние на телосложение", "Дополнительный бонус или штраф к телосложению, пока вещество активно. Если параметра ещё нет в файле, студия добавит его сама при сохранении.", "Влияние на характеристики"),
            ("_dexterityChange", "Прямое изменение ловкости", "На сколько очков вещество сразу повышает или понижает ловкость. Если параметра ещё нет в файле, студия добавит его сама при сохранении.", "Влияние на характеристики"),
            ("_dexterityModifier", "Влияние на ловкость", "Дополнительный бонус или штраф к ловкости, пока вещество активно. Если параметра ещё нет в файле, студия добавит его сама при сохранении.", "Влияние на характеристики"),
            ("_intelligenceChange", "Прямое изменение интеллекта", "На сколько очков вещество сразу повышает или понижает интеллект. Если параметра ещё нет в файле, студия добавит его сама при сохранении.", "Влияние на характеристики"),
            ("_intelligenceModifier", "Влияние на интеллект", "Дополнительный бонус или штраф к интеллекту, пока вещество активно. Если параметра ещё нет в файле, студия добавит его сама при сохранении.", "Влияние на характеристики")
        };

        foreach (var spec in specs)
        {
            if (FindTopLevelFloatPropertyLoose(export, spec.Item1) is not null)
            {
                continue;
            }

            output.Add(new StudioModFieldDto(
                $"{ForeignSubstanceAttributeFieldPrefix}{spec.Item1}",
                spec.Item2,
                spec.Item3,
                spec.Item4,
                "float",
                "number",
                "0",
                true,
                "-100",
                "100",
                null));
        }
    }

    private static void AppendSyntheticWeaponFields(
        NormalExport export,
        List<StudioModFieldDto> output)
    {
        var specs = new[]
        {
            ("ViewKickMultiplier", "Сила отдачи камеры", "Насколько резко экран и оружие дёргаются при выстреле. Если override ещё не записан в этот ассет, студия создаст его сама при сохранении.", "Отдача"),
            ("MaxRecoilOffset", "Максимальная отдача", "Насколько сильно оружие может уводить вверх или в сторону при стрельбе. Если override ещё не записан в этот ассет, студия создаст его сама при сохранении.", "Отдача"),
            ("RecoilRecoverySpeed", "Скорость возврата после отдачи", "Как быстро оружие возвращается под контроль после выстрела. Если override ещё не записан в этот ассет, студия создаст его сама при сохранении.", "Отдача")
        };

        foreach (var spec in specs)
        {
            if (FindTopLevelFloatPropertyLoose(export, spec.Item1) is not null)
            {
                continue;
            }

            output.Add(new StudioModFieldDto(
                $"{WeaponSyntheticFieldPrefix}{spec.Item1}",
                spec.Item2,
                spec.Item3,
                spec.Item4,
                "float",
                "number",
                "0",
                true,
                "-100",
                "100",
                null));
        }
    }

    private static void AddRecipePerSkillFloatFields(
        List<StudioModFieldDto> fields,
        StructPropertyData? structProperty,
        string fieldPrefix,
        string labelPrefix,
        string description,
        string section,
        string? min = null,
        string? max = null)
    {
        foreach (var spec in RecipeSkillLevels)
        {
            var current = structProperty is null
                ? null
                : FindStructChildProperty<FloatPropertyData>(structProperty, spec.PropertyName, out _);

            fields.Add(new StudioModFieldDto(
                $"{fieldPrefix}{spec.Key}",
                $"{labelPrefix}: {spec.Label}",
                description,
                section,
                "float",
                "number",
                (current?.Value ?? 0f).ToString(CultureInfo.InvariantCulture),
                true,
                min,
                max,
                null));
        }
    }

    private static void AddRecipePerSkillIntFields(
        List<StudioModFieldDto> fields,
        StructPropertyData? structProperty,
        string fieldPrefix,
        string labelPrefix,
        string description,
        string section,
        string? min = null,
        string? max = null)
    {
        foreach (var spec in RecipeSkillLevels)
        {
            var current = structProperty is null
                ? null
                : FindStructChildProperty<IntPropertyData>(structProperty, spec.PropertyName, out _);

            fields.Add(new StudioModFieldDto(
                $"{fieldPrefix}{spec.Key}",
                $"{labelPrefix}: {spec.Label}",
                description,
                section,
                "int",
                "number",
                (current?.Value ?? 0).ToString(CultureInfo.InvariantCulture),
                true,
                min,
                max,
                null));
        }
    }

    private string ResolveCraftingAllowedTypesSummary(UAsset asset, ArrayPropertyData? allowedTypes)
    {
        var values = allowedTypes?.Value ?? [];
        var names = values
            .OfType<ObjectPropertyData>()
            .Select(x => ResolveObjectReferenceLabel(asset, x.Value))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return names.Count == 0
            ? "Группы и предметы для этого слота не распознаны"
            : string.Join(" / ", names);
    }

    private static string ResolveObjectReferenceLabel(UAsset asset, FPackageIndex packageIndex)
    {
        var index = packageIndex.Index;
        if (index < 0)
        {
            var importIndex = -index - 1;
            if (importIndex >= 0 && importIndex < asset.Imports.Count)
            {
                var objectName = asset.Imports[importIndex].ObjectName?.ToString() ?? string.Empty;
                return LocalizeReferenceObjectName(objectName);
            }
        }
        else if (index > 0)
        {
            var exportIndex = index - 1;
            if (exportIndex >= 0 && exportIndex < asset.Exports.Count)
            {
                var objectName = asset.Exports[exportIndex].ObjectName?.ToString() ?? string.Empty;
                return LocalizeReferenceObjectName(objectName);
            }
        }

        return "Не распознано";
    }

    private static bool TryExtractObjectReferencePickerValue(
        UAsset asset,
        FPackageIndex packageIndex,
        out string rawValue,
        out string displayValue)
    {
        rawValue = string.Empty;
        displayValue = string.Empty;

        var index = packageIndex.Index;
        if (index > 0)
        {
            var exportIndex = index - 1;
            if (exportIndex >= 0
                && exportIndex < asset.Exports.Count
                && !string.IsNullOrWhiteSpace(asset.Exports[exportIndex].ObjectName?.ToString()))
            {
                rawValue = $"export:{asset.Exports[exportIndex].ObjectName}";
                displayValue = ResolveObjectReferenceLabel(asset, packageIndex);
                return true;
            }

            return false;
        }

        if (index == 0)
        {
            return false;
        }

        var importIndex = -index - 1;
        if (importIndex < 0 || importIndex >= asset.Imports.Count)
        {
            return false;
        }

        var import = asset.Imports[importIndex];
        var classPackage = import.ClassPackage?.ToString() ?? string.Empty;
        var className = import.ClassName?.ToString() ?? string.Empty;
        var objectName = import.ObjectName?.ToString() ?? string.Empty;

        if (classPackage.Equals("/Script/CoreUObject", StringComparison.OrdinalIgnoreCase)
            && className.Equals("Class", StringComparison.OrdinalIgnoreCase)
            && !objectName.StartsWith("Default__", StringComparison.OrdinalIgnoreCase)
            && TryResolveImportObjectName(asset, import.OuterIndex, out var outerObjectName)
            && outerObjectName.Equals("/Script/SCUM", StringComparison.OrdinalIgnoreCase))
        {
            rawValue = $"class:{objectName}";
            displayValue = ResolveReferenceDisplayValue(rawValue);
            return true;
        }

        if (classPackage.Equals("/Script/SCUM", StringComparison.OrdinalIgnoreCase)
            && objectName.StartsWith("Default__", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(className))
        {
            rawValue = $"script:{className}";
            displayValue = ResolveReferenceDisplayValue(rawValue);
            return true;
        }

        if (classPackage.Equals("/Script/Engine", StringComparison.OrdinalIgnoreCase)
            && className.Equals("BlueprintGeneratedClass", StringComparison.OrdinalIgnoreCase)
            && objectName.EndsWith("_C", StringComparison.OrdinalIgnoreCase)
            && TryResolvePackageImportPath(asset, import.OuterIndex, out var blueprintPackagePath))
        {
            rawValue = $"{blueprintPackagePath}.{objectName}";
            displayValue = ResolveReferenceDisplayValue(rawValue);
            return true;
        }

        if (classPackage.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase)
            && className.EndsWith("_C", StringComparison.OrdinalIgnoreCase))
        {
            rawValue = $"{classPackage}.{className}";
            displayValue = ResolveReferenceDisplayValue(rawValue);
            return true;
        }

        if (classPackage.Equals("/Script/Engine", StringComparison.OrdinalIgnoreCase)
            && className.Equals("BlueprintGeneratedClass", StringComparison.OrdinalIgnoreCase)
            && objectName.EndsWith("_C", StringComparison.OrdinalIgnoreCase)
            && TryResolvePackageImportPath(asset, import.OuterIndex, out var generatedClassPackagePath))
        {
            rawValue = $"{generatedClassPackagePath}.{objectName}";
            displayValue = ResolveReferenceDisplayValue(rawValue);
            return true;
        }

        if (objectName.StartsWith("Default__", StringComparison.OrdinalIgnoreCase)
            && className.EndsWith("_C", StringComparison.OrdinalIgnoreCase)
            && TryResolvePackageImportPath(asset, import.OuterIndex, out var packagePath))
        {
            rawValue = $"{packagePath}.{className}";
            displayValue = ResolveReferenceDisplayValue(rawValue);
            return true;
        }

        if (objectName.EndsWith("_C", StringComparison.OrdinalIgnoreCase)
            && TryResolvePackageImportPath(asset, import.OuterIndex, out var genericBlueprintPackagePath))
        {
            rawValue = $"{genericBlueprintPackagePath}.{objectName}";
            displayValue = ResolveReferenceDisplayValue(rawValue);
            return true;
        }

        if (className.EndsWith("_C", StringComparison.OrdinalIgnoreCase)
            && TryResolvePackageImportPath(asset, import.OuterIndex, out var genericClassPackagePath))
        {
            rawValue = $"{genericClassPackagePath}.{className}";
            displayValue = ResolveReferenceDisplayValue(rawValue);
            return true;
        }

        if (TryResolvePackageImportPath(asset, import.OuterIndex, out var importedPackagePath)
            && importedPackagePath.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(objectName)
            && !string.IsNullOrWhiteSpace(classPackage)
            && !string.IsNullOrWhiteSpace(className))
        {
            rawValue = BuildImportedObjectReferenceRawValue(importedPackagePath, objectName, classPackage, className);
            displayValue = ResolveReferenceDisplayValue(rawValue);
            return true;
        }

        return false;
    }

    private static bool TryResolveImportObjectName(UAsset asset, FPackageIndex packageIndex, out string objectName)
    {
        objectName = string.Empty;
        var index = packageIndex.Index;
        if (index >= 0)
        {
            return false;
        }

        var importIndex = -index - 1;
        if (importIndex < 0 || importIndex >= asset.Imports.Count)
        {
            return false;
        }

        objectName = asset.Imports[importIndex].ObjectName?.ToString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(objectName);
    }

    private static bool TryResolvePackageImportPath(UAsset asset, FPackageIndex packageIndex, out string packagePath)
    {
        packagePath = string.Empty;
        var index = packageIndex.Index;
        if (index >= 0)
        {
            return false;
        }

        var importIndex = -index - 1;
        if (importIndex < 0 || importIndex >= asset.Imports.Count)
        {
            return false;
        }

        var import = asset.Imports[importIndex];
        packagePath = import.ObjectName?.ToString() ?? string.Empty;
        return packagePath.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveReferenceDisplayValue(string rawValue)
    {
        var normalized = (rawValue ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (TryParseImportedObjectReferenceRawValue(normalized, out var importedObjectPath, out _, out _))
        {
            normalized = importedObjectPath;
        }

        if (TryResolveFriendlyAssetReferenceDisplayName(normalized, out var friendlyDisplayName))
        {
            return friendlyDisplayName;
        }

        if (normalized.StartsWith("script:", StringComparison.OrdinalIgnoreCase))
        {
            var className = normalized["script:".Length..].Trim();
            if (className.StartsWith("PrisonerBodyConditionOrSymptomSideEffect_", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveBodyEffectSideEffectDisplayName(className);
            }

            return LocalizeReferenceObjectName(className);
        }

        if (normalized.StartsWith("class:", StringComparison.OrdinalIgnoreCase))
        {
            var className = normalized["class:".Length..].Trim();
            return LocalizeReferenceObjectName(className);
        }

        var tail = normalized;
        var dotIndex = tail.LastIndexOf('.');
        if (dotIndex >= 0 && dotIndex < tail.Length - 1)
        {
            tail = tail[(dotIndex + 1)..];
        }
        else
        {
            var slashIndex = tail.LastIndexOf('/');
            if (slashIndex >= 0 && slashIndex < tail.Length - 1)
            {
                tail = tail[(slashIndex + 1)..];
            }
        }

        return LocalizeReferenceObjectName(tail);
    }

    private static bool TryResolveFriendlyAssetReferenceDisplayName(string rawReference, out string displayName)
    {
        displayName = string.Empty;
        if (!TryResolveRelativeAssetPathFromGameReference(rawReference, out var relativePath))
        {
            return false;
        }

        if (IsCargoDropLootPresetAsset(relativePath))
        {
            displayName = ResolveCargoDropLootPresetDisplayName(relativePath);
            return true;
        }

        if (IsCargoDropPackagePresetAsset(relativePath))
        {
            displayName = ResolveCargoDropPackagePresetDisplayName(relativePath);
            return true;
        }

        if (TryDescribeRegularItemSpawnerPresetAsset(relativePath, out var regularDescriptor))
        {
            displayName = TrimReferenceDescriptorPrefix(regularDescriptor.DisplayName, "Пресет появления предметов:");
            return true;
        }

        if (TryDescribeExamineDataPresetAsset(relativePath, out var examineDescriptor))
        {
            displayName = TrimReferenceDescriptorPrefix(examineDescriptor.DisplayName, "Набор предметов:");
            return true;
        }

        if (TryDescribeAdvancedItemSpawnerPresetAsset(relativePath, out var advancedDescriptor))
        {
            displayName = TrimReferenceDescriptorPrefix(advancedDescriptor.DisplayName, "Контейнерный пресет:");
            return true;
        }

        return false;
    }

    private static bool TryResolveRelativeAssetPathFromGameReference(string rawReference, out string relativePath)
    {
        relativePath = string.Empty;
        var normalized = (rawReference ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.StartsWith("script:", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("class:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TryParseImportedObjectReferenceRawValue(normalized, out var importedObjectPath, out _, out _))
        {
            normalized = importedObjectPath;
        }

        var packagePath = normalized;
        var dotIndex = packagePath.IndexOf('.');
        if (dotIndex > 0)
        {
            packagePath = packagePath[..dotIndex];
        }

        if (packagePath.StartsWith("/Game/ConZ_Files/", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = "scum/content/conz_files/" + packagePath["/Game/ConZ_Files/".Length..] + ".uasset";
            return true;
        }

        if (packagePath.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = "scum/content/" + packagePath["/Game/".Length..] + ".uasset";
            return true;
        }

        return false;
    }

    private static string TrimReferenceDescriptorPrefix(string value, string prefix)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[prefix.Length..].Trim();
        }

        return string.IsNullOrWhiteSpace(normalized) ? "без названия" : normalized;
    }

    private static string LocalizeReferenceObjectName(string rawName)
    {
        var name = (rawName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Не указано";
        }

        if (name.StartsWith("Default__", StringComparison.OrdinalIgnoreCase))
        {
            name = name["Default__".Length..];
        }

        if (name.EndsWith("_C", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^2];
        }

        if (name.EndsWith("_FS", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^3];
        }

        if (name.StartsWith("Weapon_", StringComparison.OrdinalIgnoreCase))
        {
            name = name["Weapon_".Length..];
        }

        if (name.StartsWith("CI_Group_", StringComparison.OrdinalIgnoreCase))
        {
            return $"Группа: {LocalizeAssetStem(name["CI_Group_".Length..])}";
        }

        if (name.StartsWith("CI_", StringComparison.OrdinalIgnoreCase))
        {
            return LocalizeAssetStem(name["CI_".Length..]);
        }

        return LocalizeAssetStem(name);
    }

    private static bool TryParseStarterSpawnCondition(
        StructPropertyData? conditionProperty,
        out StarterSpawnConditionModel model,
        out string? warning)
    {
        model = new StarterSpawnConditionModel();
        warning = null;

        if (conditionProperty is null)
        {
            warning = "У стартового предмета не найден блок условий выдачи. Будет использована пустая схема.";
            return false;
        }

        model.UserDescription = ReadConditionText(conditionProperty, "UserDescription");
        model.AutoDescription = ReadConditionText(conditionProperty, "AutoDescription");

        var tags = ReadConditionTagDictionary(conditionProperty);
        foreach (var tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag)
                && !model.OriginalTagOrder.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                model.OriginalTagOrder.Add(tag);
            }
        }

        var tokens = ReadConditionTokenStream(conditionProperty);
        if (tokens.Count < 3)
        {
            warning = "У стартового предмета повреждён GameplayTagQuery: поток токенов слишком короткий.";
            return false;
        }

        var index = tokens.Count >= 2 && tokens[0] == 0 && tokens[1] == 1 ? 2 : 0;
        if (!TryParseGameplayTagQueryNode(tokens, ref index, out var root))
        {
            warning = "У стартового предмета используется непростой GameplayTagQuery, который студия пока не может безопасно разложить на понятные настройки.";
            return false;
        }

        var hasExtraTokens = index < tokens.Count && tokens.Skip(index).Any(x => x != 0);
        if (hasExtraTokens)
        {
            warning = "У стартового предмета найден расширенный GameplayTagQuery с дополнительными токенами. Понятные настройки будут показаны частично.";
        }

        if (root.Type != GameplayTagQueryExprType.AllExprMatch)
        {
            warning = "У стартового предмета используется нестандартная логика условий выдачи. Понятные настройки будут показаны частично.";
        }

        var hasUnsupportedChildren = false;
        foreach (var child in root.Type == GameplayTagQueryExprType.AllExprMatch ? root.Children : [root])
        {
            switch (child.Type)
            {
                case GameplayTagQueryExprType.AllTagsMatch:
                    AddResolvedTags(child.TagIndices, tags, model.RequiredTags);
                    break;
                case GameplayTagQueryExprType.NoTagsMatch:
                    AddResolvedTags(child.TagIndices, tags, model.ForbiddenTags);
                    break;
                case GameplayTagQueryExprType.AnyTagsMatch:
                    AddResolvedTags(child.TagIndices, tags, model.AnyTags);
                    break;
                default:
                    hasUnsupportedChildren = true;
                    break;
            }
        }

        model.SupportsSafeRewrite =
            !hasExtraTokens
            && root.Type == GameplayTagQueryExprType.AllExprMatch
            && !hasUnsupportedChildren;

        if (hasUnsupportedChildren && string.IsNullOrWhiteSpace(warning))
        {
            warning = "У стартового предмета встречаются дополнительные типы условий выдачи. Понятные настройки будут показаны частично.";
        }

        return true;
    }

    private static string ReadConditionText(StructPropertyData conditionProperty, string childName)
    {
        var property = conditionProperty.Value
            .OfType<StrPropertyData>()
            .FirstOrDefault(x => string.Equals(x.Name?.ToString(), childName, StringComparison.OrdinalIgnoreCase));
        return property?.Value?.ToString() ?? string.Empty;
    }

    private static List<string> ReadConditionTagDictionary(StructPropertyData conditionProperty)
    {
        var tagDictionary = conditionProperty.Value
            .OfType<ArrayPropertyData>()
            .FirstOrDefault(x => string.Equals(x.Name?.ToString(), "TagDictionary", StringComparison.OrdinalIgnoreCase));

        if (tagDictionary?.Value is not { Length: > 0 })
        {
            return [];
        }

        var result = new List<string>(tagDictionary.Value.Length);
        foreach (var entry in tagDictionary.Value.OfType<StructPropertyData>())
        {
            var tagName = entry.Value
                .OfType<NamePropertyData>()
                .FirstOrDefault(x => string.Equals(x.Name?.ToString(), "TagName", StringComparison.OrdinalIgnoreCase))
                ?.Value?.ToString();

            result.Add(tagName ?? string.Empty);
        }

        return result;
    }

    private static List<byte> ReadConditionTokenStream(StructPropertyData conditionProperty)
    {
        var tokenStream = conditionProperty.Value
            .OfType<ArrayPropertyData>()
            .FirstOrDefault(x => string.Equals(x.Name?.ToString(), "QueryTokenStream", StringComparison.OrdinalIgnoreCase));

        return tokenStream?.Value?
            .OfType<BytePropertyData>()
            .Select(x => x.Value)
            .ToList()
            ?? [];
    }

    private static bool TryParseGameplayTagQueryNode(
        IReadOnlyList<byte> tokens,
        ref int index,
        out GameplayTagQueryNode node)
    {
        node = new GameplayTagQueryNode();
        if (index >= tokens.Count)
        {
            return false;
        }

        var exprType = (GameplayTagQueryExprType)tokens[index++];
        if (!Enum.IsDefined(exprType))
        {
            return false;
        }

        node.Type = exprType;
        if (index >= tokens.Count)
        {
            return false;
        }

        var count = tokens[index++];
        switch (exprType)
        {
            case GameplayTagQueryExprType.AnyTagsMatch:
            case GameplayTagQueryExprType.AllTagsMatch:
            case GameplayTagQueryExprType.NoTagsMatch:
                for (var i = 0; i < count; i++)
                {
                    if (index >= tokens.Count)
                    {
                        return false;
                    }

                    node.TagIndices.Add(tokens[index++]);
                }

                return true;

            case GameplayTagQueryExprType.AnyExprMatch:
            case GameplayTagQueryExprType.AllExprMatch:
            case GameplayTagQueryExprType.NoExprMatch:
                for (var i = 0; i < count; i++)
                {
                    if (!TryParseGameplayTagQueryNode(tokens, ref index, out var child))
                    {
                        return false;
                    }

                    node.Children.Add(child);
                }

                return true;

            default:
                return false;
        }
    }

    private static void AddResolvedTags(IEnumerable<int> tagIndices, IReadOnlyList<string> tags, HashSet<string> output)
    {
        foreach (var tagIndex in tagIndices)
        {
            if (tagIndex < 0 || tagIndex >= tags.Count)
            {
                continue;
            }

            var tag = tags[tagIndex];
            if (!string.IsNullOrWhiteSpace(tag))
            {
                output.Add(tag);
            }
        }
    }

    private static void CollectUassetFields(
        UAsset asset,
        PropertyData property,
        string fieldPath,
        string label,
        string relativePath,
        List<StudioModFieldDto> output,
        List<StudioModListTargetDto> listTargets,
        int depth)
    {
        if (depth > 8)
        {
            return;
        }

        if (property is RichCurveKeyPropertyData richCurveKey)
        {
            AddRichCurveKeyFields(output, relativePath, fieldPath, label, richCurveKey);
            return;
        }

        if (property is StructPropertyData structProperty)
        {
            for (var i = 0; i < structProperty.Value.Count; i++)
            {
                var child = structProperty.Value[i];
                var childName = GetReadablePropertyName(child, i);
                CollectUassetFields(
                    asset,
                    child,
                    $"{fieldPath}/p:{i}",
                    $"{label}.{childName}",
                    relativePath,
                    output,
                    listTargets,
                    depth + 1);
            }

            return;
        }

        if (property is ArrayPropertyData arrayProperty && property is not SetPropertyData)
        {
            var values = arrayProperty.Value;
            if (values is null)
            {
                return;
            }

            var isCurvePointArray = IsCurvePointArray(arrayProperty, values);
            var listLabel = isCurvePointArray
                ? ResolveCurvePointListLabel(relativePath)
                : NormalizeListTargetLabel(relativePath, ToUserFieldLabel(relativePath, label));
            if (isCurvePointArray || ShouldExposeListTarget(relativePath, listLabel))
            {
                var picker = ResolveListTargetReferencePicker(relativePath, listLabel, values);
                List<string>? entryLabels = null;
                if (isCurvePointArray)
                {
                    entryLabels = BuildCurvePointEntryLabels(values.Length);
                }
                else if (ResolveUassetArrayItemKind(values).Equals("reference", StringComparison.OrdinalIgnoreCase)
                    || (ResolveUassetArrayItemKind(values).Equals("struct", StringComparison.OrdinalIgnoreCase)
                        && IsAdvancedItemSpawnerPresetSubpresetListSurface(relativePath, listLabel)))
                {
                    entryLabels = values
                        .Select((value, index) => ResolveArrayEntryLabel(asset, relativePath, listLabel, value, index))
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToList();
                }

                listTargets.Add(new StudioModListTargetDto(
                    fieldPath,
                    listLabel,
                    ResolveListTargetDescription(relativePath, listLabel),
                    ResolveUassetArrayItemKind(values),
                    values.Length,
                    SupportsAddClone: values.Length > 0 && !picker.SupportsAddReference,
                    SupportsRemove: values.Length > 0,
                    SupportsClear: values.Length > 0,
                    SupportsAddEmpty: !picker.SupportsAddReference && CanSafelyAddEmptyArrayItem(arrayProperty),
                    SupportsAddReference: picker.SupportsAddReference,
                    ReferencePickerKind: picker.ReferencePickerKind,
                    ReferencePickerPrompt: picker.ReferencePickerPrompt,
                    EntryLabels: entryLabels));
            }

            for (var i = 0; i < values.Length; i++)
            {
                var item = values[i];
                CollectUassetFields(
                    asset,
                    item,
                    $"{fieldPath}/a:{i}",
                    $"{label}[{i}]",
                    relativePath,
                    output,
                    listTargets,
                    depth + 1);
            }

            return;
        }

        if (property is SetPropertyData setProperty)
        {
            var values = setProperty.Value ?? [];
            var listLabel = NormalizeListTargetLabel(relativePath, ToUserFieldLabel(relativePath, label));
            if (ShouldExposeListTarget(relativePath, listLabel))
            {
                var picker = ResolveListTargetReferencePicker(relativePath, listLabel, values);
                List<string>? entryLabels = null;
                if (ResolveUassetSetItemKind(setProperty).Equals("reference", StringComparison.OrdinalIgnoreCase))
                {
                    entryLabels = values
                        .Select((value, index) => ResolveArrayEntryLabel(asset, relativePath, listLabel, value, index))
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToList();
                }

                listTargets.Add(new StudioModListTargetDto(
                    fieldPath,
                    listLabel,
                    ResolveListTargetDescription(relativePath, listLabel),
                    ResolveUassetSetItemKind(setProperty),
                    values.Length,
                    SupportsAddClone: false,
                    SupportsRemove: values.Length > 0,
                    SupportsClear: values.Length > 0,
                    SupportsAddEmpty: false,
                    SupportsAddReference: picker.SupportsAddReference,
                    ReferencePickerKind: picker.ReferencePickerKind,
                    ReferencePickerPrompt: picker.ReferencePickerPrompt,
                    EntryLabels: entryLabels));
            }

            for (var i = 0; i < values.Length; i++)
            {
                var item = values[i];
                CollectUassetFields(
                    asset,
                    item,
                    $"{fieldPath}/s:{i}",
                    $"{label}[{i}]",
                    relativePath,
                    output,
                    listTargets,
                    depth + 1);
            }

            return;
        }

        if (property is MapPropertyData mapProperty)
        {
            var mapEntries = mapProperty.Value;
            var listLabel = NormalizeListTargetLabel(relativePath, ToUserFieldLabel(relativePath, label));
            var entryLabels = new List<string>(mapEntries.Count);
            if (ShouldExposeListTarget(relativePath, listLabel))
            {
                var picker = ResolveMapTargetReferencePicker(relativePath, listLabel, mapProperty);
                for (var index = 0; index < mapEntries.Count; index++)
                {
                    var entry = mapEntries.ElementAt(index);
                    entryLabels.Add(ResolveMapEntryLabel(asset, relativePath, label, entry.Key, index));
                }

                listTargets.Add(new StudioModListTargetDto(
                    fieldPath,
                    listLabel,
                    ResolveListTargetDescription(relativePath, listLabel),
                    ResolveUassetMapItemKind(mapProperty),
                    mapEntries.Count,
                    SupportsAddClone: false,
                    SupportsRemove: mapEntries.Count > 0,
                    SupportsClear: mapEntries.Count > 0,
                    SupportsAddEmpty: false,
                    SupportsAddReference: picker.SupportsAddReference,
                    ReferencePickerKind: picker.ReferencePickerKind,
                    ReferencePickerPrompt: picker.ReferencePickerPrompt,
                    EntryLabels: entryLabels));
            }

            var mapIndex = 0;
            foreach (var entry in mapEntries)
            {
                var entryLabel = ResolveMapEntryLabel(asset, relativePath, label, entry.Key, mapIndex);
                var entryValueLabel = ResolveMapEntryValueLabel(relativePath, label, entryLabel, entry.Value);
                CollectUassetFields(
                    asset,
                    entry.Value,
                    $"{fieldPath}/m:{mapIndex}:v",
                    entryValueLabel,
                    relativePath,
                    output,
                    listTargets,
                    depth + 1);
                mapIndex++;
            }

            return;
        }

        if (TryDescribeEditableProperty(asset, property, out var valueType, out var currentValue, out var currentDisplayValue)
            && TryAddSafeField(output, relativePath, fieldPath, label, valueType, currentValue, currentDisplayValue))
        {
            return;
        }

        if (property is ObjectPropertyData
            && TryDescribeReadableReference(asset, property, out var readableReference))
        {
            TryAddReadOnlyInfoField(output, relativePath, fieldPath, label, readableReference);
        }
    }

    private static string ResolveArrayEntryLabel(UAsset asset, string relativePath, string parentLabel, PropertyData valueProperty, int fallbackIndex)
    {
        parentLabel = NormalizeListTargetLabel(relativePath, parentLabel);
        var path = relativePath.ToLowerInvariant();
        var normalizedParent = NormalizeAssetKey(parentLabel);
        string prefix;
        if (path.Contains("/quests/", StringComparison.Ordinal)
            && (parentLabel.Contains("нужно держать рядом", StringComparison.OrdinalIgnoreCase)
                || parentLabel.Contains("подходящие предметы", StringComparison.OrdinalIgnoreCase)))
        {
            prefix = "предмет";
        }
        else if (IsCargoDropMajorSpawnerOptionsSurface(relativePath, parentLabel))
        {
            prefix = "пресет";
        }
        else if (IsCargoDropMajorSpawnerPresetOptionsSurface(relativePath, parentLabel))
        {
            prefix = "пресет";
        }
        else if (IsAdvancedItemSpawnerPresetSubpresetListSurface(relativePath, parentLabel))
        {
            prefix = "подпакет";
        }
        else if (IsVehicleSpawnPresetListSurface(relativePath, parentLabel))
        {
            prefix = "пресет транспорта";
        }
        else if (IsItemSpawnerPresetItemListSurface(relativePath, parentLabel))
        {
            prefix = "предмет";
        }
        else if (IsItemSpawningVariationListSurface(relativePath, parentLabel))
        {
            prefix = "вариант";
        }
        else if (path.Contains("/ui/gameevents/itemselection/", StringComparison.Ordinal))
        {
            prefix = parentLabel.Contains("креплен", StringComparison.OrdinalIgnoreCase)
                ? "крепление"
                : "предмет";
        }
        else if (IsGameEventMarkerAsset(relativePath) && IsGameEventLoadoutListLabel(parentLabel))
        {
            prefix = "набор";
        }
        else if (IsPlantSpeciesListSurface(relativePath, parentLabel))
        {
            prefix = "растение";
        }
        else if (IsPlantPestListSurface(relativePath, parentLabel))
        {
            prefix = "вредитель";
        }
        else if (IsPlantDiseaseListSurface(relativePath, parentLabel))
        {
            prefix = "болезнь";
        }
        else if (normalizedParent.Contains("sideeffects", StringComparison.Ordinal)
            || parentLabel.Contains("побочные эффекты", StringComparison.OrdinalIgnoreCase))
        {
            prefix = "эффект";
        }
        else if (path.Contains("/foreignsubstances/", StringComparison.Ordinal))
        {
            prefix = "вещество";
        }
        else
        {
            prefix = "элемент";
        }

        return valueProperty switch
        {
            ObjectPropertyData objectValue when IsCargoDropMajorSpawnerOptionsSurface(relativePath, parentLabel)
                || IsCargoDropMajorSpawnerPresetOptionsSurface(relativePath, parentLabel)
                => $"{prefix} {ResolveCargoLootObjectEntryLabel(asset, objectValue.Value)}",
            SoftObjectPropertyData softObjectValue when IsCargoDropMajorSpawnerOptionsSurface(relativePath, parentLabel)
                || IsCargoDropMajorSpawnerPresetOptionsSurface(relativePath, parentLabel)
                => $"{prefix} {ResolveCargoLootReferenceLabel(ExtractSoftObjectReference(softObjectValue.Value))}",
            SoftObjectPathPropertyData softObjectPathValue when IsCargoDropMajorSpawnerOptionsSurface(relativePath, parentLabel)
                || IsCargoDropMajorSpawnerPresetOptionsSurface(relativePath, parentLabel)
                => $"{prefix} {ResolveCargoLootReferenceLabel(ExtractSoftObjectReference(softObjectPathValue.Value))}",
            ObjectPropertyData objectValue when IsGameEventMarkerAsset(relativePath) && IsGameEventLoadoutListLabel(parentLabel)
                => $"{prefix} {ResolveGameEventLoadoutEntryLabel(ResolveObjectReferenceLabel(asset, objectValue.Value))}",
            SoftObjectPropertyData softObjectValue when IsGameEventMarkerAsset(relativePath) && IsGameEventLoadoutListLabel(parentLabel)
                => $"{prefix} {ResolveGameEventLoadoutEntryLabel(ExtractSoftObjectReference(softObjectValue.Value))}",
            SoftObjectPathPropertyData softObjectPathValue when IsGameEventMarkerAsset(relativePath) && IsGameEventLoadoutListLabel(parentLabel)
                => $"{prefix} {ResolveGameEventLoadoutEntryLabel(ExtractSoftObjectReference(softObjectPathValue.Value))}",
            StructPropertyData structValue when IsAdvancedItemSpawnerPresetSubpresetListSurface(relativePath, parentLabel)
                => ResolveAdvancedSpawnerSubpresetEntryLabel(asset, structValue, prefix, fallbackIndex),
            ObjectPropertyData objectValue when (normalizedParent.Contains("sideeffects", StringComparison.Ordinal)
                                                || parentLabel.Contains("побочные эффекты", StringComparison.OrdinalIgnoreCase))
                && TryResolveSideEffectClassFromObjectReference(asset, objectValue.Value, out var sideEffectClassName)
                => $"{prefix} {ResolveBodyEffectSideEffectDisplayName(sideEffectClassName)}",
            ObjectPropertyData objectValue when path.Contains("/foliage/farming/", StringComparison.Ordinal)
                => $"{prefix} {ResolvePlantReferenceEntryLabel(ResolveObjectReferenceLabel(asset, objectValue.Value))}",
            SoftObjectPropertyData softObjectValue when path.Contains("/foliage/farming/", StringComparison.Ordinal)
                => $"{prefix} {ResolvePlantReferenceEntryLabel(ExtractSoftObjectReference(softObjectValue.Value))}",
            SoftObjectPathPropertyData softObjectPathValue when path.Contains("/foliage/farming/", StringComparison.Ordinal)
                => $"{prefix} {ResolvePlantReferenceEntryLabel(ExtractSoftObjectReference(softObjectPathValue.Value))}",
            ObjectPropertyData objectValue => $"{prefix} {ShortenReadableReferenceLabel(ResolveObjectReferenceLabel(asset, objectValue.Value))}",
            SoftObjectPropertyData softObjectValue => $"{prefix} {ShortenReadableReferenceLabel(ExtractSoftObjectReference(softObjectValue.Value))}",
            SoftObjectPathPropertyData softObjectPathValue => $"{prefix} {ShortenReadableReferenceLabel(ExtractSoftObjectReference(softObjectPathValue.Value))}",
            NamePropertyData nameValue => $"{prefix} {LocalizeAssetStem(nameValue.Value?.ToString() ?? string.Empty)}",
            StrPropertyData stringValue => $"{prefix} {LocalizeAssetStem(stringValue.Value?.ToString() ?? string.Empty)}",
            EnumPropertyData enumValue => $"{prefix} {LocalizeAssetStem(enumValue.Value?.ToString() ?? string.Empty)}",
            _ => $"{prefix} {fallbackIndex + 1}"
        };
    }

    private static string ResolveAdvancedSpawnerSubpresetEntryLabel(
        UAsset asset,
        StructPropertyData structValue,
        string prefix,
        int fallbackIndex)
    {
        var presetLabel = TryFindStructReferenceChild(structValue, "Preset", out var presetProperty)
            ? presetProperty switch
            {
                ObjectPropertyData objectValue => ResolveAdvancedItemSpawnerReferenceLabel(ResolveObjectReferenceLabel(asset, objectValue.Value)),
                SoftObjectPropertyData softObjectValue => ResolveAdvancedItemSpawnerReferenceLabel(ExtractSoftObjectReference(softObjectValue.Value)),
                SoftObjectPathPropertyData softObjectPathValue => ResolveAdvancedItemSpawnerReferenceLabel(ExtractSoftObjectReference(softObjectPathValue.Value)),
                _ => $"{fallbackIndex + 1}"
            }
            : $"{fallbackIndex + 1}";

        var rarityValue = FindStructChildProperty<EnumPropertyData>(structValue, "Rarity", out _)?.Value?.ToString();
        if (!string.IsNullOrWhiteSpace(rarityValue))
        {
            return $"{prefix} {presetLabel} / редкость {LocalizeItemRarity(rarityValue)}";
        }

        return $"{prefix} {presetLabel}";
    }

    private static string NormalizeListTargetLabel(string relativePath, string userLabel)
    {
        if (IsCurvePointListLabel(relativePath, userLabel))
        {
            return ResolveCurvePointListLabel(relativePath);
        }

        if (IsGameEventMarkerAsset(relativePath) && !string.IsNullOrWhiteSpace(userLabel))
        {
            var normalized = userLabel.ToLowerInvariant();
            var normalizedKey = NormalizeAssetKey(userLabel);
            if (normalized.Contains("основное оружие", StringComparison.Ordinal) || normalizedKey.Contains("possibleprimaryweapons", StringComparison.Ordinal))
            {
                return "Основное оружие на выбор";
            }

            if (normalized.Contains("пистолет", StringComparison.Ordinal) || normalizedKey.Contains("possiblesecondaryweapons", StringComparison.Ordinal))
            {
                return "Пистолет на выбор";
            }

            if (normalized.Contains("ближний бой", StringComparison.Ordinal) || normalizedKey.Contains("possibletertiaryweapons", StringComparison.Ordinal))
            {
                return "Ближний бой на выбор";
            }

            if (normalized.Contains("одежда", StringComparison.Ordinal) || normalizedKey.Contains("possibleoutfits", StringComparison.Ordinal))
            {
                return "Одежда на выбор";
            }

            if (normalized.Contains("обязательный набор", StringComparison.Ordinal) || normalizedKey.Contains("mandatorygear", StringComparison.Ordinal))
            {
                return "Обязательный набор";
            }

            if (normalized.Contains("дополнительное снаряжение", StringComparison.Ordinal) || normalizedKey.Contains("possiblesupportitems", StringComparison.Ordinal))
            {
                return "Дополнительное снаряжение";
            }
        }

        if (IsPlantSpeciesListSurface(relativePath, userLabel))
        {
            return "Виды растений";
        }

        if (IsCargoDropMajorSpawnerOptionsSurface(relativePath, userLabel))
        {
            return "Основные обычные пресеты лута";
        }

        if (IsCargoDropMajorSpawnerPresetOptionsSurface(relativePath, userLabel))
        {
            return "Основные расширенные пресеты лута";
        }

        if (IsCargoDropMinorSpawnerOptionsSurface(relativePath, userLabel))
        {
            return "Дополнительные наборы лута";
        }

        if (IsExamineDataPresetItemListSurface(relativePath, userLabel))
        {
            return "Предметы набора";
        }

        if (IsCargoDropEncounterVariantsSurface(relativePath, userLabel))
        {
            return "Варианты защиты грузового дропа";
        }

        if (IsRegularItemSpawnerPresetItemListSurface(relativePath, userLabel))
        {
            return "Предметы пресета";
        }

        if (IsPlantPestListSurface(relativePath, userLabel))
        {
            return "Вредители";
        }

        if (IsPlantDiseaseListSurface(relativePath, userLabel))
        {
            return "Болезни";
        }

        return userLabel;
    }

    private static bool IsCurvePointArray(ArrayPropertyData arrayProperty, PropertyData[] values)
    {
        if (!string.Equals(arrayProperty.ArrayType?.ToString(), "StructProperty", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (values.Length > 0)
        {
            return values.All(IsCurvePointStruct);
        }

        return arrayProperty.DummyStruct is not null && IsCurvePointStruct(arrayProperty.DummyStruct);
    }

    private static bool IsCurvePointStruct(PropertyData property)
    {
        if (property is RichCurveKeyPropertyData)
        {
            return true;
        }

        if (property is not StructPropertyData structProperty || structProperty.Value.Count == 0)
        {
            return false;
        }

        return structProperty.Value.Any(child => child is RichCurveKeyPropertyData);
    }

    private static bool IsCurvePointListLabel(string relativePath, string userLabel)
    {
        if (string.IsNullOrWhiteSpace(userLabel))
        {
            return false;
        }

        var normalized = userLabel.ToLowerInvariant();
        return normalized.Contains("точки кривой", StringComparison.Ordinal)
            || normalized.Contains("rich curve", StringComparison.Ordinal)
            || normalized.Contains("float curve", StringComparison.Ordinal)
            || normalized.Contains("editor curve data", StringComparison.Ordinal)
            || (relativePath.Contains("/spawn_amount_curves/", StringComparison.OrdinalIgnoreCase)
                && normalized.Contains("keys", StringComparison.Ordinal));
    }

    private static string ResolveCurvePointListLabel(string relativePath)
    {
        var path = relativePath.ToLowerInvariant();
        if (path.Contains("/encounters/spawn_amount_curves/", StringComparison.Ordinal))
        {
            return "Кривая количества спавна / точки кривой";
        }

        if (path.Contains("/cooking/data/curves/", StringComparison.Ordinal))
        {
            return "Кривая приготовления / точки кривой";
        }

        if (path.Contains("/data/weapon/malfunctionprobabilitycurves/", StringComparison.Ordinal))
        {
            return "Кривая отказов оружия / точки кривой";
        }

        if (path.Contains("/minigames/lockpicking/", StringComparison.Ordinal))
        {
            return "Кривая взлома / точки кривой";
        }

        if (path.Contains("/skills/", StringComparison.Ordinal))
        {
            return "Кривая навыка / точки кривой";
        }

        if (path.Contains("/vehicles/", StringComparison.Ordinal))
        {
            return "Кривая транспорта / точки кривой";
        }

        return "Кривая / точки кривой";
    }

    private static List<string> BuildCurvePointEntryLabels(int count)
    {
        var result = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            result.Add($"Точка {i + 1}");
        }

        return result;
    }

    private static string ResolveCargoLootReferenceLabel(string rawReference)
    {
        if (TryResolveRelativeAssetPathFromGameReference(rawReference, out var relativePath))
        {
            if (IsCargoDropLootPresetAsset(relativePath))
            {
                return ResolveCargoDropLootPresetDisplayName(relativePath);
            }

            if (IsCargoDropPackagePresetAsset(relativePath))
            {
                return ResolveCargoDropPackagePresetDisplayName(relativePath);
            }
        }

        return ShortenReadableReferenceLabel(rawReference);
    }

    private static string ResolveAdvancedItemSpawnerReferenceLabel(string rawReference)
    {
        if (TryResolveRelativeAssetPathFromGameReference(rawReference, out var relativePath)
            && TryDescribeAdvancedItemSpawnerPresetAsset(relativePath, out var descriptor))
        {
            return TrimReferenceDescriptorPrefix(descriptor.DisplayName, "Контейнерный пресет:");
        }

        return ShortenReadableReferenceLabel(rawReference);
    }

    private static string ResolveCargoLootObjectEntryLabel(UAsset asset, FPackageIndex packageIndex)
    {
        if (TryExtractObjectReferencePickerValue(asset, packageIndex, out var rawValue, out var displayValue))
        {
            var resolvedFromRaw = ResolveCargoLootReferenceLabel(rawValue);
            if (!string.IsNullOrWhiteSpace(resolvedFromRaw))
            {
                return resolvedFromRaw;
            }

            var resolvedFromDisplay = ResolveCargoLootReferenceLabel(displayValue);
            if (!string.IsNullOrWhiteSpace(resolvedFromDisplay))
            {
                return resolvedFromDisplay;
            }
        }

        return ResolveCargoLootReferenceLabel(ResolveObjectReferenceLabel(asset, packageIndex));
    }

    private static string LocalizeItemRarity(string rawValue)
    {
        var value = (rawValue ?? string.Empty).Trim();
        if (value.StartsWith("EItemRarity::", StringComparison.OrdinalIgnoreCase))
        {
            value = value["EItemRarity::".Length..];
        }

        return value.ToLowerInvariant() switch
        {
            "extremelyrare" => "чрезвычайно редкий",
            "veryrare" => "очень редкий",
            "rare" => "редкий",
            "uncommon" => "необычный",
            "common" => "обычный",
            "abundant" => "частый",
            _ => CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(value)))
        };
    }

    private static bool IsPlantSpeciesListSurface(string relativePath, string label)
    {
        if (!relativePath.Contains("/foliage/farming/", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var normalized = label.ToLowerInvariant();
        var normalizedKey = NormalizeAssetKey(label);
        return normalized.Contains("виды растений", StringComparison.Ordinal)
            || normalized.Contains("plant species", StringComparison.Ordinal)
            || normalized.Contains("растительный species", StringComparison.Ordinal)
            || normalizedKey.Contains("plantspecies", StringComparison.Ordinal)
            || normalizedKey.Contains("растительныйspecies", StringComparison.Ordinal);
    }

    private static bool IsPlantPestListSurface(string relativePath, string label)
    {
        if (!relativePath.Contains("/foliage/farming/", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var normalized = label.ToLowerInvariant();
        var normalizedKey = NormalizeAssetKey(label);
        return normalized.Contains("вредители", StringComparison.Ordinal)
            || normalized.Contains("pests", StringComparison.Ordinal)
            || normalizedKey.Contains("pests", StringComparison.Ordinal);
    }

    private static bool IsPlantDiseaseListSurface(string relativePath, string label)
    {
        if (!relativePath.Contains("/foliage/farming/", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var normalized = label.ToLowerInvariant();
        var normalizedKey = NormalizeAssetKey(label);
        return normalized.Contains("болезни", StringComparison.Ordinal)
            || normalized.Contains("diseases", StringComparison.Ordinal)
            || normalizedKey.Contains("diseases", StringComparison.Ordinal);
    }

    private static bool IsExamineDataPresetAsset(string relativePath)
    {
        return !string.IsNullOrWhiteSpace(relativePath)
            && relativePath.Contains("/items/spawnerpresets/examine_data_presets/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVehicleSpawnGroupAsset(string relativePath)
    {
        return !string.IsNullOrWhiteSpace(relativePath)
            && relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
            && relativePath.Contains("/vehicles/spawningpresets/spawngroups/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVehicleSpawnPresetListSurface(string relativePath, string label)
    {
        if (!IsVehicleSpawnGroupAsset(relativePath) || string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var normalized = label.ToLowerInvariant();
        var normalizedKey = NormalizeAssetKey(label);
        return normalized.Contains("доступные варианты транспорта", StringComparison.Ordinal)
            || normalized.Contains("vehicle presets", StringComparison.Ordinal)
            || normalizedKey.Contains("vehiclepresets", StringComparison.Ordinal);
    }

    private static bool IsRegularItemSpawnerPresetAsset(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        return relativePath.Contains("/items/spawnerpresets/", StringComparison.OrdinalIgnoreCase)
            && !relativePath.Contains("/items/spawnerpresets/examine_data_presets/", StringComparison.OrdinalIgnoreCase)
            && !relativePath.Contains("/items/spawnerpresets2/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCargoDropLootPresetAsset(string relativePath)
    {
        return IsExamineDataPresetAsset(relativePath)
            && Path.GetFileNameWithoutExtension(relativePath).EndsWith("_CargoDrop", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCargoDropPackagePresetAsset(string relativePath)
    {
        return !string.IsNullOrWhiteSpace(relativePath)
            && relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
            && relativePath.Contains("/items/spawnerpresets2/special_packages/cargo_drops/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdvancedItemSpawnerPresetAsset(string relativePath)
    {
        return !string.IsNullOrWhiteSpace(relativePath)
            && relativePath.Contains("/items/spawnerpresets2/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsItemSpawnerPresetItemListSurface(string relativePath, string label)
    {
        if (!(IsExamineDataPresetAsset(relativePath) || IsRegularItemSpawnerPresetAsset(relativePath))
            || string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var normalized = label.ToLowerInvariant();
        var normalizedKey = NormalizeAssetKey(label);
        return normalized.Contains("предметы набора", StringComparison.Ordinal)
            || normalized.Contains("предметы пресета", StringComparison.Ordinal)
            || normalized.Contains("предметы появления", StringComparison.Ordinal)
            || normalized.Contains("item classes", StringComparison.Ordinal)
            || normalizedKey.Contains("itemclasses", StringComparison.Ordinal);
    }

    private static bool IsExamineDataPresetItemListSurface(string relativePath, string label)
    {
        return IsExamineDataPresetAsset(relativePath)
            && IsItemSpawnerPresetItemListSurface(relativePath, label);
    }

    private static bool IsRegularItemSpawnerPresetItemListSurface(string relativePath, string label)
    {
        return IsRegularItemSpawnerPresetAsset(relativePath)
            && IsItemSpawnerPresetItemListSurface(relativePath, label);
    }

    private static bool IsAdvancedItemSpawnerPresetItemListSurface(string relativePath, string label)
    {
        if (!IsCargoDropPackagePresetAsset(relativePath) || string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var normalized = label.ToLowerInvariant();
        var normalizedKey = NormalizeAssetKey(label);
        return normalized.Contains("предметы контейнерного набора", StringComparison.Ordinal)
            || normalized.Contains("fixed items", StringComparison.Ordinal)
            || normalizedKey.Contains("fixeditems", StringComparison.Ordinal);
    }

    private static bool IsAdvancedItemSpawnerPresetSubpresetListSurface(string relativePath, string label)
    {
        if (!IsAdvancedItemSpawnerPresetAsset(relativePath)
            || IsCargoDropPackagePresetAsset(relativePath)
            || string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var normalized = label.ToLowerInvariant();
        var normalizedKey = NormalizeAssetKey(label);
        return normalized.Contains("готовые подпакеты лута", StringComparison.Ordinal)
            || normalized.Contains("подпакеты лута", StringComparison.Ordinal)
            || normalized.Contains("subpresets", StringComparison.Ordinal)
            || normalizedKey.Contains("subpresets", StringComparison.Ordinal);
    }

    private static bool IsItemSpawningVariationListSurface(string relativePath, string label)
    {
        return relativePath.Contains("/data/tables/items/spawning/", StringComparison.OrdinalIgnoreCase)
            && label.Contains("варианты предмета", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGameEventMarkerAsset(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var path = relativePath.ToLowerInvariant();
        return path.Contains("/gameevents/markers/", StringComparison.Ordinal)
            && path.EndsWith("locationmarker.uasset", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGameEventLoadoutListLabel(string userLabel)
    {
        if (string.IsNullOrWhiteSpace(userLabel))
        {
            return false;
        }

        var label = userLabel.ToLowerInvariant();
        var normalizedKey = NormalizeAssetKey(userLabel);
        return label.Contains("основное оружие", StringComparison.Ordinal)
            || label.Contains("пистолет", StringComparison.Ordinal)
            || label.Contains("ближний бой", StringComparison.Ordinal)
            || label.Contains("одежда на выбор", StringComparison.Ordinal)
            || label.Contains("обязательный набор", StringComparison.Ordinal)
            || label.Contains("дополнительное снаряжение", StringComparison.Ordinal)
            || normalizedKey.Contains("possibleprimaryweapons", StringComparison.Ordinal)
            || normalizedKey.Contains("possiblesecondaryweapons", StringComparison.Ordinal)
            || normalizedKey.Contains("possibletertiaryweapons", StringComparison.Ordinal)
            || normalizedKey.Contains("possibleoutfits", StringComparison.Ordinal)
            || normalizedKey.Contains("mandatorygear", StringComparison.Ordinal)
            || normalizedKey.Contains("possiblesupportitems", StringComparison.Ordinal);
    }

    private static bool IsCargoDropWorldEventAsset(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var path = relativePath.ToLowerInvariant();
        return path.Contains("/worldevents/cargodrop/", StringComparison.Ordinal);
    }

    private static bool IsCargoDropContainerAsset(string relativePath)
    {
        return IsCargoDropWorldEventAsset(relativePath)
            && string.Equals(
                NormalizeAssetKey(Path.GetFileNameWithoutExtension(relativePath)),
                "bpcargo",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryFindCargoDropContainerOwnerExport(UAsset asset, out NormalExport export)
    {
        foreach (var candidate in asset.Exports.OfType<NormalExport>())
        {
            if (FindTopLevelProperty<FloatPropertyData>(candidate, "DetonationCountdown", out _) is not null
                || FindTopLevelProperty<ArrayPropertyData>(candidate, "MajorSpawnerOptions", out _) is not null
                || FindTopLevelProperty<ArrayPropertyData>(candidate, "MajorSpawnerPresetOptions", out _) is not null
                || FindTopLevelProperty<ArrayPropertyData>(candidate, "MinorSpawnerOptions", out _) is not null)
            {
                export = candidate;
                return true;
            }
        }

        export = asset.Exports.OfType<NormalExport>().FirstOrDefault()!;
        return export is not null;
    }

    private static bool IsCargoDropMajorSpawnerOptionsSurface(string relativePath, string label)
    {
        if (!IsCargoDropContainerAsset(relativePath) || string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var normalized = label.ToLowerInvariant();
        var normalizedKey = NormalizeAssetKey(label);
        return normalized.Contains("основные пресеты лута", StringComparison.Ordinal)
            || normalized.Contains("основные обычные пресеты лута", StringComparison.Ordinal)
            || normalizedKey.Contains("majorspawneroptions", StringComparison.Ordinal);
    }

    private static bool IsCargoDropMajorSpawnerPresetOptionsSurface(string relativePath, string label)
    {
        if (!IsCargoDropContainerAsset(relativePath) || string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var normalized = label.ToLowerInvariant();
        var normalizedKey = NormalizeAssetKey(label);
        return normalized.Contains("контейнерные наборы лута", StringComparison.Ordinal)
            || normalized.Contains("основные расширенные пресеты лута", StringComparison.Ordinal)
            || normalized.Contains("расширенные пресеты лута", StringComparison.Ordinal)
            || normalizedKey.Contains("majorspawnerpresetoptions", StringComparison.Ordinal);
    }

    private static bool IsCargoDropMinorSpawnerOptionsSurface(string relativePath, string label)
    {
        if (!IsCargoDropContainerAsset(relativePath) || string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var normalized = label.ToLowerInvariant();
        var normalizedKey = NormalizeAssetKey(label);
        return normalized.Contains("дополнительные варианты лута", StringComparison.Ordinal)
            || normalized.Contains("дополнительные наборы лута", StringComparison.Ordinal)
            || normalizedKey.Contains("minorspawneroptions", StringComparison.Ordinal);
    }

    private static bool IsCargoDropEncounterVariantsSurface(string relativePath, string label)
    {
        if (!IsCargoDropWorldEventAsset(relativePath) || string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var normalized = label.ToLowerInvariant();
        var normalizedKey = NormalizeAssetKey(label);
        return normalized.Contains("варианты защиты грузового дропа", StringComparison.Ordinal)
            || normalized.Contains("варианты событий", StringComparison.Ordinal)
            || normalized.Contains("событие classes", StringComparison.Ordinal)
            || normalized.Contains("encounter data", StringComparison.Ordinal)
            || normalized.Contains("encounter classes", StringComparison.Ordinal)
            || normalizedKey.Contains("encounterdata", StringComparison.Ordinal)
            || normalizedKey.Contains("encounterclasses", StringComparison.Ordinal)
            || normalizedKey.Contains("событиеclasses", StringComparison.Ordinal);
    }

    private static string ResolvePlantReferenceEntryLabel(string raw)
    {
        var rawValue = (raw ?? string.Empty).Trim();
        var stemMatch = Regex.Match(
            rawValue,
            @"DA_Plant(?:Species|Pest|Disease)_[A-Za-z0-9_]+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var stem = stemMatch.Success ? stemMatch.Value : string.Empty;
        if (string.IsNullOrWhiteSpace(stem))
        {
            var candidate = ShortenReadableReferenceLabel(rawValue);
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return "Не распознано";
            }

            if (TryResolvePlantCompositeLabel(candidate, out var compositeLabel))
            {
                return compositeLabel;
            }

            stem = Path.GetFileNameWithoutExtension(candidate);
            if (string.IsNullOrWhiteSpace(stem))
            {
                stem = candidate;
            }
        }

        if (stem.StartsWith("DA_PlantPest_", StringComparison.OrdinalIgnoreCase))
        {
            return ResolvePlantDisplayName(stem, "DA_PlantPest_");
        }

        if (stem.StartsWith("DA_PlantDisease_", StringComparison.OrdinalIgnoreCase))
        {
            return ResolvePlantDisplayName(stem, "DA_PlantDisease_");
        }

        if (stem.StartsWith("DA_PlantSpecies_", StringComparison.OrdinalIgnoreCase))
        {
            return ResolvePlantDisplayName(stem, "DA_PlantSpecies_");
        }

        return CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(stem)));
    }

    private static bool TryResolvePlantCompositeLabel(string candidate, out string label)
    {
        label = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var trimmed = candidate.Trim();
        foreach (var marker in new[]
                 {
                     "растительный Species ",
                     "растительный Pest ",
                     "растительный Disease "
                 })
        {
            var index = trimmed.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            var suffix = trimmed[(index + marker.Length)..].Trim();
            if (string.IsNullOrWhiteSpace(suffix))
            {
                return false;
            }

            label = CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(suffix)));
            return true;
        }

        return false;
    }

    private static bool IsQuestRewardSkillExperienceSurface(string relativePath, string label)
    {
        if (!relativePath.Contains("/quests/", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var normalized = label.ToLowerInvariant();
        var normalizedKey = NormalizeAssetKey(label);
        return normalized.Contains("опыт навыков", StringComparison.Ordinal)
            || normalized.Contains("reward skill experience", StringComparison.Ordinal)
            || normalized.Contains("skill experience reward", StringComparison.Ordinal)
            || normalized.Contains("награда skill experience", StringComparison.Ordinal)
            || normalizedKey.Contains("rewardskillexperience", StringComparison.Ordinal);
    }

    private static bool IsQuestRewardCurrencySurface(string relativePath, string label)
    {
        if (!relativePath.Contains("/quests/", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var normalized = label.ToLowerInvariant();
        var normalizedKey = NormalizeAssetKey(label);
        return normalized.Contains("денежная награда", StringComparison.Ordinal)
            || normalized.Contains("reward currency", StringComparison.Ordinal)
            || normalized.Contains("награда currency", StringComparison.Ordinal)
            || normalizedKey.Contains("rewardcurrency", StringComparison.Ordinal);
    }

    private static string LocalizeQuestCurrencyType(string raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.StartsWith("ECurrencyType::", StringComparison.OrdinalIgnoreCase))
        {
            value = value["ECurrencyType::".Length..];
        }

        if (value.Equals("Normal", StringComparison.OrdinalIgnoreCase))
        {
            return "обычная валюта";
        }

        return CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(value)));
    }

    private static string ResolveMapEntryLabel(UAsset asset, string relativePath, string parentLabel, PropertyData keyProperty, int fallbackIndex)
    {
        var path = relativePath.ToLowerInvariant();
        var normalizedParent = NormalizeAssetKey(parentLabel);
        var isQuestRewardSkillExperience = IsQuestRewardSkillExperienceLabel(relativePath, parentLabel);

        if (IsCargoDropEncounterVariantsSurface(relativePath, parentLabel))
        {
            return keyProperty switch
            {
                ObjectPropertyData objectKey => CapitalizeFirst(ShortenReadableReferenceLabel(ResolveObjectReferenceLabel(asset, objectKey.Value))),
                SoftObjectPropertyData softObjectKey => CapitalizeFirst(ShortenReadableReferenceLabel(ExtractSoftObjectReference(softObjectKey.Value))),
                SoftObjectPathPropertyData softObjectPathKey => CapitalizeFirst(ShortenReadableReferenceLabel(ExtractSoftObjectReference(softObjectPathKey.Value))),
                _ => $"Вариант защиты {fallbackIndex + 1}"
            };
        }

        string prefix;
        if (path.Contains("/foreignsubstances/", StringComparison.Ordinal))
        {
            prefix = "вещество";
        }
        else if (IsQuestRewardSkillExperienceSurface(relativePath, parentLabel))
        {
            prefix = "навык";
        }
        else if (IsQuestRewardCurrencySurface(relativePath, parentLabel))
        {
            prefix = "валюта";
        }
        else if (path.Contains("/encounters/", StringComparison.Ordinal) || path.Contains("/npcs/", StringComparison.Ordinal))
        {
            if (normalizedParent.Contains("characterclasses", StringComparison.Ordinal)
                || normalizedParent.Contains("limitedcharacters", StringComparison.Ordinal)
                || parentLabel.Contains("лимит конкретных npc", StringComparison.OrdinalIgnoreCase))
            {
                prefix = "класс NPC";
            }
            else if (normalizedParent.Contains("possiblecharacters", StringComparison.Ordinal))
            {
                prefix = "пресет";
            }
            else
            {
                prefix = "вариант";
            }
        }
        else if (isQuestRewardSkillExperience)
        {
            prefix = "навык";
        }
        else if (path.Contains("/economy/", StringComparison.Ordinal))
        {
            prefix = normalizedParent.Contains("expensivetradeablesspawnchancemap", StringComparison.Ordinal)
                ? "уровень"
                : "элемент";
        }
        else
        {
            prefix = "элемент";
        }

        if (keyProperty is EnumPropertyData questCurrencyKey && IsQuestRewardCurrencySurface(relativePath, parentLabel))
        {
            return LocalizeQuestCurrencyType(questCurrencyKey.Value?.ToString() ?? string.Empty);
        }

        switch (keyProperty)
        {
            case ObjectPropertyData objectKey:
                return $"{prefix} {ShortenReadableReferenceLabel(ResolveObjectReferenceLabel(asset, objectKey.Value))}";
            case SoftObjectPropertyData softObjectKey:
                if (path.Contains("/quests/", StringComparison.Ordinal)
                    && normalizedParent.Contains("rewardskillexperience", StringComparison.Ordinal))
                {
                    return $"{prefix} {ResolveSkillClassReferenceLabel(ExtractSoftObjectReference(softObjectKey.Value))}";
                }
                return $"{prefix} {ShortenReadableReferenceLabel(ExtractSoftObjectReference(softObjectKey.Value))}";
            case SoftObjectPathPropertyData softObjectPathKey:
                if (path.Contains("/quests/", StringComparison.Ordinal)
                    && normalizedParent.Contains("rewardskillexperience", StringComparison.Ordinal))
                {
                    return $"{prefix} {ResolveSkillClassReferenceLabel(ExtractSoftObjectReference(softObjectPathKey.Value))}";
                }
                return $"{prefix} {ShortenReadableReferenceLabel(ExtractSoftObjectReference(softObjectPathKey.Value))}";
            case NamePropertyData nameKey:
                return $"{prefix} {LocalizeAssetStem(nameKey.Value?.ToString() ?? string.Empty)}";
            case StrPropertyData stringKey:
                return $"{prefix} {LocalizeAssetStem(stringKey.Value?.ToString() ?? string.Empty)}";
            case EnumPropertyData enumKey:
                return $"{prefix} {LocalizeAssetStem(enumKey.Value?.ToString() ?? string.Empty)}";
            case Int8PropertyData int8Key:
                return path.Contains("/economy/", StringComparison.Ordinal)
                    ? $"уровень {int8Key.Value}"
                    : $"{prefix} {int8Key.Value}";
            case Int16PropertyData int16Key:
                return path.Contains("/economy/", StringComparison.Ordinal)
                    ? $"уровень {int16Key.Value}"
                    : $"{prefix} {int16Key.Value}";
            case IntPropertyData intKey:
                return path.Contains("/economy/", StringComparison.Ordinal)
                    ? $"уровень {intKey.Value}"
                    : $"{prefix} {intKey.Value}";
            case BytePropertyData byteKey:
                return path.Contains("/economy/", StringComparison.Ordinal)
                    ? $"уровень {byteKey.Value}"
                    : $"{prefix} {byteKey.Value}";
            case UInt16PropertyData uint16Key:
                return path.Contains("/economy/", StringComparison.Ordinal)
                    ? $"уровень {uint16Key.Value}"
                    : $"{prefix} {uint16Key.Value}";
            case UInt32PropertyData uint32Key:
                return path.Contains("/economy/", StringComparison.Ordinal)
                    ? $"уровень {uint32Key.Value}"
                    : $"{prefix} {uint32Key.Value}";
            case UInt64PropertyData uint64Key:
                return path.Contains("/economy/", StringComparison.Ordinal)
                    ? $"уровень {uint64Key.Value}"
                    : $"{prefix} {uint64Key.Value}";
            case Int64PropertyData longKey:
                return path.Contains("/economy/", StringComparison.Ordinal)
                    ? $"уровень {longKey.Value}"
                    : $"{prefix} {longKey.Value}";
            default:
                return $"элемент {fallbackIndex + 1}";
        }
    }

    private static string ShortenReadableReferenceLabel(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var candidate = raw.Trim();
        var slashIndex = candidate.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < candidate.Length - 1)
        {
            candidate = candidate[(slashIndex + 1)..];
        }

        var markerIndex = candidate.LastIndexOf(" / ", StringComparison.Ordinal);
        if (markerIndex >= 0 && markerIndex < candidate.Length - 3)
        {
            candidate = candidate[(markerIndex + 3)..];
        }

        candidate = candidate.Trim();
        if (candidate.EndsWith("_C", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[..^2];
        }
        else if (candidate.EndsWith(" C", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[..^2];
        }

        return LocalizeAssetStem(candidate);
    }

    private static string ResolveSkillClassReferenceLabel(string rawReference)
    {
        var candidate = (rawReference ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.Empty;
        }

        var slashIndex = candidate.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < candidate.Length - 1)
        {
            candidate = candidate[(slashIndex + 1)..];
        }

        var dotIndex = candidate.IndexOf('.');
        if (dotIndex > 0)
        {
            candidate = candidate[..dotIndex];
        }

        if (candidate.EndsWith("_C", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[..^2];
        }

        return LocalizeAssetStem(candidate);
    }

    private static string ResolveMapEntryValueLabel(string relativePath, string parentLabel, string entryLabel, PropertyData valueProperty)
    {
        var path = relativePath.ToLowerInvariant();
        var normalizedParent = NormalizeAssetKey(parentLabel);
        var isQuestRewardSkillExperience = IsQuestRewardSkillExperienceLabel(relativePath, parentLabel);
        var localizedParentLabel = path.Contains("/quests/", StringComparison.Ordinal)
            ? ToUserFieldLabel(relativePath, parentLabel)
            : parentLabel;
        if (IsCargoDropEncounterVariantsSurface(relativePath, parentLabel))
        {
            localizedParentLabel = NormalizeListTargetLabel(relativePath, ToUserFieldLabel(relativePath, parentLabel));
        }
        if (valueProperty is StructPropertyData or ArrayPropertyData or MapPropertyData)
        {
            return $"{localizedParentLabel}.{entryLabel}";
        }

        if (IsQuestRewardSkillExperienceSurface(relativePath, parentLabel))
        {
            return $"{parentLabel}.{entryLabel}.Сколько опыта дать";
        }

        if (IsQuestRewardCurrencySurface(relativePath, parentLabel))
        {
            return $"{parentLabel}.{entryLabel}.Сколько денег выдать";
        }

        if ((path.Contains("/encounters/", StringComparison.Ordinal) || path.Contains("/npcs/", StringComparison.Ordinal))
            && (normalizedParent.Contains("characterclasses", StringComparison.Ordinal)
                || normalizedParent.Contains("possiblecharacters", StringComparison.Ordinal)
                || normalizedParent.Contains("limitedcharacters", StringComparison.Ordinal)
                || parentLabel.Contains("лимит конкретных npc", StringComparison.OrdinalIgnoreCase)))
        {
            if (normalizedParent.Contains("limitedcharacters", StringComparison.Ordinal)
                || parentLabel.Contains("лимит конкретных npc", StringComparison.OrdinalIgnoreCase))
            {
                return $"{parentLabel}.{entryLabel}.Максимум таких NPC";
            }

            return $"{parentLabel}.{entryLabel}.SelectionWeight";
        }

        if (path.Contains("/economy/", StringComparison.Ordinal)
            && normalizedParent.Contains("expensivetradeablesspawnchancemap", StringComparison.Ordinal))
        {
            return $"{parentLabel}.{entryLabel}.Chance";
        }

        if (isQuestRewardSkillExperience)
        {
            return $"{localizedParentLabel}.{entryLabel}.Сколько опыта дать";
        }

        return $"{parentLabel}.{entryLabel}";
    }

    private static bool TryDescribeReadableReference(UAsset asset, PropertyData property, out string currentValue)
    {
        currentValue = string.Empty;
        if (property is not ObjectPropertyData objectProperty)
        {
            return false;
        }

        currentValue = ResolveObjectReferenceLabel(asset, objectProperty.Value);
        return !string.IsNullOrWhiteSpace(currentValue);
    }

    private static void AddRichCurveKeyFields(
        List<StudioModFieldDto> output,
        string relativePath,
        string fieldPath,
        string label,
        RichCurveKeyPropertyData property)
    {
        var pointLabel = NormalizeCurvePointLabel(ToUserFieldLabel(relativePath, label));
        TryAddSafeFieldResolved(
            output,
            relativePath,
            $"{fieldPath}/rk:time",
            $"{pointLabel} / когда начинается эта ступень",
            "float",
            property.Value.Time.ToString(CultureInfo.InvariantCulture),
            property.Value.Time.ToString(CultureInfo.InvariantCulture));
        TryAddSafeFieldResolved(
            output,
            relativePath,
            $"{fieldPath}/rk:value",
            $"{pointLabel} / насколько сильно действует эта ступень",
            "float",
            property.Value.Value.ToString(CultureInfo.InvariantCulture),
            property.Value.Value.ToString(CultureInfo.InvariantCulture));
    }

    private static string NormalizeCurvePointLabel(string label)
    {
        var value = (label ?? string.Empty)
            .Replace(" / кривая эффекта", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" / точки кривой", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("ступень ", "точка ", StringComparison.OrdinalIgnoreCase)
            .Replace("по уровень ", "по уровню ", StringComparison.OrdinalIgnoreCase)
            .Replace("по тяжесть ", "по тяжести ", StringComparison.OrdinalIgnoreCase)
            .Replace("по сила эффекта ", "по силе эффекта ", StringComparison.OrdinalIgnoreCase);

        return NormalizeLocalizedLabel(value);
    }

    private static string ResolveNoSafeFieldWarning(string relativePath, bool hasListTargets)
    {
        var path = relativePath.ToLowerInvariant();
        if (hasListTargets)
        {
            if (path.Contains("/bodyeffects/", StringComparison.Ordinal)
                || path.Contains("/metabolism/", StringComparison.Ordinal))
            {
                return "У этой системы нет прямых числовых настроек. Сначала добавь нужные последствия или связи, а потом открой их новые параметры.";
            }

            return "У этого раздела нет отдельных числовых настроек, но можно менять состав связанных элементов.";
        }
        if (path.Contains("/bodyeffects/symptoms/", StringComparison.Ordinal))
        {
            return "Этот симптом сам по себе почти ничего не меняет и обычно только включается другими эффектами. Его лучше использовать как справку, а менять родительский эффект или вещество.";
        }

        if (path.Contains("/ui/", StringComparison.Ordinal))
        {
            return "Этот раздел относится к интерфейсу или отображению. Для обычного режима студии он не редактируется.";
        }

        return "У этого раздела пока нет понятных безопасных настроек. Обычно его лучше пропустить или менять через связанные игровые системы.";
    }

    private static bool TryDescribeEditableProperty(
        UAsset asset,
        PropertyData property,
        out string valueType,
        out string currentValue,
        out string currentDisplayValue)
    {
        switch (property)
        {
            case IntPropertyData intProperty:
                valueType = "int";
                currentValue = intProperty.Value.ToString(CultureInfo.InvariantCulture);
                currentDisplayValue = currentValue;
                return true;
            case Int8PropertyData int8Property:
                valueType = "int8";
                currentValue = int8Property.Value.ToString(CultureInfo.InvariantCulture);
                currentDisplayValue = currentValue;
                return true;
            case Int16PropertyData int16Property:
                valueType = "int16";
                currentValue = int16Property.Value.ToString(CultureInfo.InvariantCulture);
                currentDisplayValue = currentValue;
                return true;
            case Int64PropertyData int64Property:
                valueType = "int64";
                currentValue = int64Property.Value.ToString(CultureInfo.InvariantCulture);
                currentDisplayValue = currentValue;
                return true;
            case UInt16PropertyData uint16Property:
                valueType = "uint16";
                currentValue = uint16Property.Value.ToString(CultureInfo.InvariantCulture);
                currentDisplayValue = currentValue;
                return true;
            case UInt32PropertyData uint32Property:
                valueType = "uint32";
                currentValue = uint32Property.Value.ToString(CultureInfo.InvariantCulture);
                currentDisplayValue = currentValue;
                return true;
            case UInt64PropertyData uint64Property:
                valueType = "uint64";
                currentValue = uint64Property.Value.ToString(CultureInfo.InvariantCulture);
                currentDisplayValue = currentValue;
                return true;
            case BytePropertyData byteProperty:
                valueType = "byte";
                currentValue = byteProperty.Value.ToString(CultureInfo.InvariantCulture);
                currentDisplayValue = currentValue;
                return true;
            case FloatPropertyData floatProperty:
                valueType = "float";
                currentValue = floatProperty.Value.ToString(CultureInfo.InvariantCulture);
                currentDisplayValue = currentValue;
                return true;
            case DoublePropertyData doubleProperty:
                valueType = "double";
                currentValue = doubleProperty.Value.ToString(CultureInfo.InvariantCulture);
                currentDisplayValue = currentValue;
                return true;
            case BoolPropertyData boolProperty:
                valueType = "bool";
                currentValue = boolProperty.Value ? "true" : "false";
                currentDisplayValue = currentValue;
                return true;
            case StrPropertyData strProperty:
                valueType = "string";
                currentValue = strProperty.Value?.ToString() ?? string.Empty;
                currentDisplayValue = currentValue;
                return true;
            case NamePropertyData nameProperty:
                valueType = "name";
                currentValue = nameProperty.Value?.ToString() ?? string.Empty;
                currentDisplayValue = currentValue;
                return true;
            case EnumPropertyData enumProperty:
                valueType = "enum";
                currentValue = enumProperty.Value?.ToString() ?? string.Empty;
                currentDisplayValue = currentValue;
                return true;
            case TextPropertyData textProperty:
                valueType = "text";
                currentValue = textProperty.Value?.ToString() ?? string.Empty;
                currentDisplayValue = currentValue;
                return true;
            case ObjectPropertyData objectProperty when TryExtractObjectReferencePickerValue(asset, objectProperty.Value, out var objectReference, out var objectDisplayValue):
                valueType = "object";
                currentValue = objectReference;
                currentDisplayValue = objectDisplayValue;
                return true;
            case SoftObjectPropertyData softObjectProperty:
                valueType = "soft-object";
                currentValue = ExtractSoftObjectReference(softObjectProperty.Value);
                currentDisplayValue = ResolveReferenceDisplayValue(currentValue);
                return true;
            case SoftObjectPathPropertyData softObjectPathProperty:
                valueType = "soft-object-path";
                currentValue = ExtractSoftObjectReference(softObjectPathProperty.Value);
                currentDisplayValue = ResolveReferenceDisplayValue(currentValue);
                return true;
            default:
                valueType = string.Empty;
                currentValue = string.Empty;
                currentDisplayValue = string.Empty;
                return false;
        }
    }

    private static string GetReadablePropertyName(PropertyData property, int fallbackIndex)
    {
        var name = property.Name?.Value.ToString();
        return string.IsNullOrWhiteSpace(name)
            ? $"item{fallbackIndex}"
            : name;
    }

    private static string ResolveExportLabel(UAsset asset, NormalExport export, string relativePath, int exportIndex)
    {
        var fallback = export.ObjectName?.Value.ToString() ?? $"export_{exportIndex}";
        var path = relativePath.ToLowerInvariant();
        if (!path.Contains("/bodyeffects/", StringComparison.Ordinal)
            && !path.Contains("/metabolism/", StringComparison.Ordinal))
        {
            return fallback;
        }

        try
        {
            var classType = export.GetExportClassType().Value.ToString();
            return string.IsNullOrWhiteSpace(classType)
                ? fallback
                : classType;
        }
        catch
        {
            return fallback;
        }
    }

    private static string ToUserFieldLabel(string relativePath, string technicalLabel)
    {
        var label = (technicalLabel ?? string.Empty)
            .Replace("$.", string.Empty, StringComparison.Ordinal)
            .Replace("$", string.Empty, StringComparison.Ordinal);
        var dotIndex = label.IndexOf('.');
        if (dotIndex > 0)
        {
            var exportName = label[..dotIndex];
            var nestedPath = label[(dotIndex + 1)..];
            if (TryGetExportContextLabel(relativePath, exportName, out var exportContext))
            {
                label = $"{exportContext}.{nestedPath}";
            }
            else
            {
                label = nestedPath;
            }
        }

        label = label
            .Replace("Default__", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_C.", ".", StringComparison.OrdinalIgnoreCase)
            .Replace("_", " ", StringComparison.Ordinal)
            .Replace("Vs", " vs ", StringComparison.Ordinal)
            .Replace("MI", "MI ", StringComparison.Ordinal);

        label = HumanizeCamel(label);
        label = LocalizeIndexedSegments(label);
        label = LocalizeCommonGameplayTerms(label);

        var path = relativePath.ToLowerInvariant();
        if (path.Contains("/data/tables/items/spawning/", StringComparison.OrdinalIgnoreCase))
        {
            label = NormalizeItemSpawningUserLabel(label);
        }

        if (path.Contains("/bodyeffects/", StringComparison.OrdinalIgnoreCase) || path.Contains("movementsettings", StringComparison.OrdinalIgnoreCase))
        {
            label = label
                .Replace("точки кривой / ", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("какой symptom запускается", "какой симптом запускается", StringComparison.OrdinalIgnoreCase)
                .Replace("symptom", "симптом", StringComparison.OrdinalIgnoreCase)
                .Replace("уровень опьянения", "опьянение", StringComparison.OrdinalIgnoreCase)
                .Replace("интенсивность", "тяжесть эффекта", StringComparison.OrdinalIgnoreCase)
                .Replace("длительность", "время действия", StringComparison.OrdinalIgnoreCase)
                .Replace("темп движения", "скорость движения", StringComparison.OrdinalIgnoreCase)
                .Replace("нагрузка веса", "весовая нагрузка", StringComparison.OrdinalIgnoreCase)
                .Replace("сила прыжка", "высота прыжка", StringComparison.OrdinalIgnoreCase)
                .Replace("предельная скорость", "максимальная скорость", StringComparison.OrdinalIgnoreCase)
                .Replace("сопротивление", "сила сопротивления", StringComparison.OrdinalIgnoreCase)
                .Replace("урон / потеря здоровья", "урон", StringComparison.OrdinalIgnoreCase)
                .Replace("скорость движения по земле / модификатор", "скорость движения по земле", StringComparison.OrdinalIgnoreCase)
                .Replace("скорость плавания / модификатор", "скорость плавания", StringComparison.OrdinalIgnoreCase)
                .Replace("максимальная выносливость / модификатор", "максимальная выносливость", StringComparison.OrdinalIgnoreCase)
                .Replace("результативность / модификатор", "успешность действий", StringComparison.OrdinalIgnoreCase)
                .Replace("успешность действий / модификатор", "успешность действий", StringComparison.OrdinalIgnoreCase)
                .Replace("влияние на интеллект / модификатор", "влияние на интеллект", StringComparison.OrdinalIgnoreCase)
                .Replace("периодический приступ / шанс", "шанс приступа", StringComparison.OrdinalIgnoreCase)
                .Replace("периодический приступ / интервал", "интервал между приступами", StringComparison.OrdinalIgnoreCase)
                .Replace("периодический приступ / время действия по телосложению", "время действия приступа по телосложению", StringComparison.OrdinalIgnoreCase)
                .Replace("алкогольное опьянение / тяжесть эффекта по уровню алкоголя в организме", "тяжесть опьянения по уровню алкоголя", StringComparison.OrdinalIgnoreCase)
                .Replace("прилив энергии / тяжесть эффекта по уровню стимулятора в организме", "сила прилива энергии по количеству вещества", StringComparison.OrdinalIgnoreCase)
                .Replace("вещество источник эффекта class", "какое вещество запускает эффект", StringComparison.OrdinalIgnoreCase)
                .Replace("какой симптом запускается class", "какой симптом запускается", StringComparison.OrdinalIgnoreCase)
                .Replace(" / положение точки кривой", " / когда начинается эта ступень", StringComparison.OrdinalIgnoreCase)
                .Replace(" / значение точки кривой", " / насколько сильно действует эта ступень", StringComparison.OrdinalIgnoreCase);
        }

        if (path.Contains("/metabolism/", StringComparison.OrdinalIgnoreCase))
        {
            label = label
                .Replace("модификатор по телосложению Ratio", "коэффициент по телосложению", StringComparison.OrdinalIgnoreCase);
        }

        if (path.Contains("/items/crafting/recipes/", StringComparison.OrdinalIgnoreCase))
        {
            label = label
                .Replace("награда fame", "очки славы за крафт", StringComparison.OrdinalIgnoreCase)
                .Replace("результат крафта", "что создаётся", StringComparison.OrdinalIgnoreCase)
                .Replace("основной навык рецепта", "какой навык влияет на рецепт", StringComparison.OrdinalIgnoreCase)
                .Replace("назначение ингредиента", "роль ингредиента", StringComparison.OrdinalIgnoreCase)
                .Replace("правило смешивания", "как можно смешивать варианты", StringComparison.OrdinalIgnoreCase)
                .Replace("варианты ингредиента", "подходящие варианты ингредиента", StringComparison.OrdinalIgnoreCase)
                .Replace("возможный предмет", "подходящий предмет", StringComparison.OrdinalIgnoreCase)
                .Replace("положение точки кривой", "когда начинается эта ступень", StringComparison.OrdinalIgnoreCase)
                .Replace("значение точки кривой", "насколько сильно действует эта ступень", StringComparison.OrdinalIgnoreCase);
        }

        if (path.Contains("/worldevents/cargodrop/", StringComparison.OrdinalIgnoreCase))
        {
            label = label
                .Replace("has spawn delay", "использовать задержку перед запуском", StringComparison.OrdinalIgnoreCase)
                .Replace("has появление delay", "использовать задержку перед запуском", StringComparison.OrdinalIgnoreCase)
                .Replace("шанс Multiplier", "вес выбора", StringComparison.OrdinalIgnoreCase)
                .Replace("Chance Multiplier", "вес выбора", StringComparison.OrdinalIgnoreCase);

            label = ReplaceSemanticLabelPart(label, "detonation countdown", "время до взрыва после приземления");
            label = ReplaceSemanticLabelPart(label, "major spawner preset options", "основные расширенные пресеты лута");
            label = ReplaceSemanticLabelPart(label, "major spawner options", "основные обычные пресеты лута");
            label = ReplaceSemanticLabelPart(label, "minor spawner options", "дополнительные наборы лута");
            label = ReplaceSemanticLabelPart(label, "encounter classes", "варианты защиты грузового дропа");
            label = ReplaceSemanticLabelPart(label, "encounter data", "варианты защиты грузового дропа");
            label = ReplaceSemanticLabelPart(label, "encounter class", "защитное событие");
            label = ReplaceSemanticLabelPart(label, "encounter weight", "вес выбора");
            label = ReplaceSemanticLabelPart(label, "chance multiplier", "множитель шанса выбора");
            label = ReplaceSemanticLabelPart(label, "spawner preset", "расширенный пресет лута");
            label = ReplaceSemanticLabelPart(label, "preset", "обычный пресет лута");
            label = ReplaceSemanticLabelPart(label, "weight", "вес выбора");
            label = ReplaceSemanticLabelPart(label, "cargo drop classes", "варианты контейнера грузового дропа");
        }

        if (path.Contains("/encounters/", StringComparison.OrdinalIgnoreCase) || path.Contains("/npcs/", StringComparison.OrdinalIgnoreCase))
        {
            label = label
                .Replace("минимум removal sight дистанция", "дистанция скрытия вне поля зрения", StringComparison.OrdinalIgnoreCase)
                .Replace("минимум removal дистанция", "дистанция полного удаления", StringComparison.OrdinalIgnoreCase)
                .Replace("allowed group появление points", "сколько групповых точек появления можно использовать", StringComparison.OrdinalIgnoreCase)
                .Replace("is group появление", "появлять сразу группой", StringComparison.OrdinalIgnoreCase)
                .Replace("количество персонажей за цикл", "сколько персонажей может появиться", StringComparison.OrdinalIgnoreCase)
                .Replace("добавка к числу персонажей за игрока", "добавка к числу NPC за игрока", StringComparison.OrdinalIgnoreCase)
                .Replace("минимум дистанция between characters", "минимальная дистанция между персонажами", StringComparison.OrdinalIgnoreCase)
                .Replace("radius", "радиус действия события", StringComparison.OrdinalIgnoreCase)
                .Replace("prioritize exterior орда появление points", "предпочитать внешние точки появления орды", StringComparison.OrdinalIgnoreCase)
                .Replace("cargo drop event character spawn distance interval", "интервал дистанции появления защитников грузового дропа", StringComparison.OrdinalIgnoreCase)
                .Replace("cargo drop event character появление дистанция интервал", "интервал дистанции появления защитников грузового дропа", StringComparison.OrdinalIgnoreCase)
                .Replace("check allowed surfaces", "проверять подходящие поверхности", StringComparison.OrdinalIgnoreCase)
                .Replace("preset type / tag name", "тип пресета NPC", StringComparison.OrdinalIgnoreCase)
                .Replace("preset тип / тег", "тип пресета NPC", StringComparison.OrdinalIgnoreCase)
                .Replace("character classes", "состав пресета NPC", StringComparison.OrdinalIgnoreCase)
                .Replace("limited characters", "лимит конкретных NPC", StringComparison.OrdinalIgnoreCase)
                .Replace("maximum count", "максимум таких NPC", StringComparison.OrdinalIgnoreCase)
                .Replace("max count", "максимум таких NPC", StringComparison.OrdinalIgnoreCase)
                .Replace("selection weight", "вес выбора", StringComparison.OrdinalIgnoreCase)
                .Replace("selection вес в пресете", "вес выбора", StringComparison.OrdinalIgnoreCase)
                .Replace("weight", "вес в пресете", StringComparison.OrdinalIgnoreCase)
                .Replace("tag name", "тег", StringComparison.OrdinalIgnoreCase)
                .Replace("состав пресета NPC / вариант", "состав пресета NPC / персонаж", StringComparison.OrdinalIgnoreCase)
                .Replace("начальная пауза ожидания перед ответом", "задержка перед первым ответом", StringComparison.OrdinalIgnoreCase)
                .Replace("пауза ожидания перед ответом", "пауза между ответами", StringComparison.OrdinalIgnoreCase)
                .Replace("анимации ожидания перед ответом", "анимации ожидания NPC", StringComparison.OrdinalIgnoreCase)
                .Replace("response", "ответ", StringComparison.OrdinalIgnoreCase)
                .Replace("idle", "ожидание", StringComparison.OrdinalIgnoreCase);

            if (label.Contains("preset", StringComparison.OrdinalIgnoreCase)
                && label.Contains("тег", StringComparison.OrdinalIgnoreCase))
            {
                label = "тип пресета NPC";
            }
        }

        if (path.Contains("/economy/", StringComparison.OrdinalIgnoreCase))
        {
            label = Regex.Replace(label, @"\s#(\d+)", " / уровень $1", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            label = label
                .Replace("trader тип", "тип торговца", StringComparison.OrdinalIgnoreCase)
                .Replace("tag name", "тег", StringComparison.OrdinalIgnoreCase)
                .Replace("prosperity level info per level", "уровни процветания", StringComparison.OrdinalIgnoreCase)
                .Replace("prosperity level threshold gold", "порог золота для уровня", StringComparison.OrdinalIgnoreCase)
                .Replace("prosperity level threshold", "порог наличных для уровня", StringComparison.OrdinalIgnoreCase)
                .Replace("gbcrefresh rate per hour", "скорость обновления GBC в час", StringComparison.OrdinalIgnoreCase)
                .Replace("gscrefresh rate per hour", "скорость обновления GSC в час", StringComparison.OrdinalIgnoreCase)
                .Replace("expensive tradeables появление шанс map", "шанс появления дорогих товаров", StringComparison.OrdinalIgnoreCase)
                .Replace("expensive tradeables появление chance map", "шанс появления дорогих товаров", StringComparison.OrdinalIgnoreCase)
                .Replace("expensive tradeables spawn chance map", "шанс появления дорогих товаров", StringComparison.OrdinalIgnoreCase)
                .Replace("vehicle появление group parent tag / тег", "тег группы спавна транспорта", StringComparison.OrdinalIgnoreCase)
                .Replace("vehicle spawn group parent tag / тег", "тег группы спавна транспорта", StringComparison.OrdinalIgnoreCase);

            if (label.Contains("expensive tradeables", StringComparison.OrdinalIgnoreCase)
                && label.Contains("map", StringComparison.OrdinalIgnoreCase))
            {
                label = "шанс появления дорогих товаров";
            }

            label = Regex.Replace(
                label,
                @"шанс появления дорогих товаров\s*/\s*элемент\s+(\d+)",
                "шанс появления дорогих товаров / уровень $1",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (label.Contains("vehicle", StringComparison.OrdinalIgnoreCase)
                && label.Contains("parent tag", StringComparison.OrdinalIgnoreCase))
            {
                label = "тег группы спавна транспорта";
            }

            if (path.Contains("durabilityvspricemultipliercurve", StringComparison.OrdinalIgnoreCase))
            {
                label = label.Replace("float curve", "цена по прочности", StringComparison.OrdinalIgnoreCase);
            }
        }

        if (path.Contains("/quests/", StringComparison.OrdinalIgnoreCase))
        {
            label = label
                .Replace("possible rewards", "варианты наград", StringComparison.OrdinalIgnoreCase)
                .Replace("possible награды", "варианты наград", StringComparison.OrdinalIgnoreCase)
                .Replace("number of quests that unlocks a level", "сколько выполненных квестов нужно для уровня", StringComparison.OrdinalIgnoreCase)
                .Replace("quest givers setup", "настройки источников квестов", StringComparison.OrdinalIgnoreCase)
                .Replace("quests generated per day", "сколько квестов выдавать за день", StringComparison.OrdinalIgnoreCase)
                .Replace("initially generated quests", "сколько квестов готово сразу", StringComparison.OrdinalIgnoreCase)
                .Replace("available quests", "максимум доступных квестов", StringComparison.OrdinalIgnoreCase)
                .Replace("quests probability per level", "шанс квестов по уровню", StringComparison.OrdinalIgnoreCase)
                .Replace("quests probability per уровень", "шанс квестов по уровню", StringComparison.OrdinalIgnoreCase)
                .Replace("quests available per уровень", "минимум доступных квестов по уровню", StringComparison.OrdinalIgnoreCase)
                .Replace("require completed quests from other npc level", "с какого уровня учитывать чужие выполненные квесты", StringComparison.OrdinalIgnoreCase)
                .Replace("require completed quests from other npc уровень", "с какого уровня учитывать чужие выполненные квесты", StringComparison.OrdinalIgnoreCase)
                .Replace("require completed quests from other npc num", "сколько чужих выполненных квестов нужно", StringComparison.OrdinalIgnoreCase)
                .Replace("allow auto complete", "разрешить авто-завершение", StringComparison.OrdinalIgnoreCase)
                .Replace("special quest sets", "специальные наборы квестов", StringComparison.OrdinalIgnoreCase)
                .Replace("possible rewards", "варианты награды", StringComparison.OrdinalIgnoreCase)
                .Replace("tags requirements", "условия по тегам", StringComparison.OrdinalIgnoreCase)
                .Replace("token stream version", "служебная версия потока условий", StringComparison.OrdinalIgnoreCase)
                .Replace("query token stream", "служебный поток условий", StringComparison.OrdinalIgnoreCase)
                .Replace("user description", "подсказка условия", StringComparison.OrdinalIgnoreCase)
                .Replace("auto description", "автоописание условия", StringComparison.OrdinalIgnoreCase)
                .Replace("must be in vicinity", "нужно держать рядом", StringComparison.OrdinalIgnoreCase)
                .Replace("accepted items", "подходящие предметы", StringComparison.OrdinalIgnoreCase)
                .Replace("allow child classes", "засчитывать похожие предметы по классу", StringComparison.OrdinalIgnoreCase)
                .Replace("accepted item uses", "минимум оставшихся использований", StringComparison.OrdinalIgnoreCase)
                .Replace("max random additional count", "случайная добавка к количеству", StringComparison.OrdinalIgnoreCase)
                .Replace("min accepted cook level", "минимальная готовность еды", StringComparison.OrdinalIgnoreCase)
                .Replace("max accepted cook level", "максимальная готовность еды", StringComparison.OrdinalIgnoreCase)
                .Replace("min accepted cook quality", "минимальное качество еды", StringComparison.OrdinalIgnoreCase)
                .Replace("min accepted item mass", "минимальная масса предмета", StringComparison.OrdinalIgnoreCase)
                .Replace("min accepted item health", "минимальное состояние предмета", StringComparison.OrdinalIgnoreCase)
                .Replace("min accepted item resource amount", "минимум ресурса в предмете", StringComparison.OrdinalIgnoreCase)
                .Replace("min accepted item resource ratio", "минимальная заполненность ресурса", StringComparison.OrdinalIgnoreCase)
                .Replace("allow packages not full", "разрешить неполные упаковки", StringComparison.OrdinalIgnoreCase)
                .Replace("reward skill experience", "опыт навыков за квест", StringComparison.OrdinalIgnoreCase)
                .Replace("reward currency", "денежная награда", StringComparison.OrdinalIgnoreCase)
                .Replace("reward trade deals", "варианты выдачи у торговца", StringComparison.OrdinalIgnoreCase)
                .Replace("reward trade deal", "выдача у торговца", StringComparison.OrdinalIgnoreCase)
                .Replace("tradeable class", "выдаваемый товар", StringComparison.OrdinalIgnoreCase)
                .Replace("base purchase price", "цена товара", StringComparison.OrdinalIgnoreCase)
                .Replace("amount in store", "количество товара", StringComparison.OrdinalIgnoreCase)
                .Replace("override purchase ability", "переопределить доступность покупки", StringComparison.OrdinalIgnoreCase)
                .Replace("can be purchased by player", "разрешить покупку игроку", StringComparison.OrdinalIgnoreCase)
                .Replace("required fame points", "требуемые очки славы", StringComparison.OrdinalIgnoreCase)
                .Replace("can be auto completed", "разрешить авто-завершение шага", StringComparison.OrdinalIgnoreCase)
                .Replace("can be auto started", "запускать шаг автоматически", StringComparison.OrdinalIgnoreCase)
                .Replace("sequence index", "порядок шага", StringComparison.OrdinalIgnoreCase)
                .Replace("also count vicinity", "засчитывать похожие предметы рядом", StringComparison.OrdinalIgnoreCase)
                .Replace("count", "сколько нужно рядом", StringComparison.OrdinalIgnoreCase)
                .Replace("rewards", "награды", StringComparison.OrdinalIgnoreCase)
                .Replace("награда skill experience", "опыт навыков за квест", StringComparison.OrdinalIgnoreCase)
                .Replace("reward skill experience", "опыт навыков за квест", StringComparison.OrdinalIgnoreCase)
                .Replace("награда currency", "денежная награда", StringComparison.OrdinalIgnoreCase)
                .Replace("reward currency", "денежная награда", StringComparison.OrdinalIgnoreCase)
                .Replace("награда trade deal", "торговая награда", StringComparison.OrdinalIgnoreCase)
                .Replace("reward trade deal", "торговая награда", StringComparison.OrdinalIgnoreCase)
                .Replace("reward fame", "награда славой", StringComparison.OrdinalIgnoreCase)
                .Replace("reward", "награда", StringComparison.OrdinalIgnoreCase);
            label = Regex.Replace(
                label,
                @"possible\s+награды\s+(\d+)",
                "варианты награды $1",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            label = Regex.Replace(
                label,
                @"possible\s+rewards?\s+(\d+)",
                "варианты награды $1",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            label = Regex.Replace(
                label,
                @"(?:награда|reward)\s+skill\s+experience",
                "опыт навыков за квест",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            label = Regex.Replace(
                label,
                @"skill\s+experience",
                "опыт навыков",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            label = Regex.Replace(
                label,
                @"(?:quest\s+)?item set data\s*/\s*items\s+(\d+)",
                "нужно держать рядом / предмет $1",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            label = Regex.Replace(
                label,
                @"item set\s*/\s*items\s+(\d+)",
                "нужно держать рядом / предмет $1",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            label = Regex.Replace(
                label,
                @"(?:quest\s+)?item set data\s*/\s*items",
                "нужно держать рядом",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            label = Regex.Replace(
                label,
                @"item set\s*/\s*items",
                "нужно держать рядом",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            label = Regex.Replace(
                label,
                @"item\s+экипировать(?:\s+data)?\s*/\s*items\s+(\d+)",
                "нужно держать рядом / предмет $1",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            label = Regex.Replace(
                label,
                @"item\s+экипировать(?:\s+data)?\s*/\s*items",
                "нужно держать рядом",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            label = ReplaceSemanticLabelPart(label, "quests", "квесты");
        }

        if (path.Contains("/skills/", StringComparison.OrdinalIgnoreCase))
        {
            label = label
                .Replace("опыт awards", "награда опытом", StringComparison.OrdinalIgnoreCase)
                .Replace("skill parameters", "параметры навыка", StringComparison.OrdinalIgnoreCase)
                .Replace("experience awards", "награда опытом", StringComparison.OrdinalIgnoreCase)
                .Replace("no skill", "без навыка", StringComparison.OrdinalIgnoreCase)
                .Replace("basic skill", "базовый уровень", StringComparison.OrdinalIgnoreCase)
                .Replace("medium skill", "средний уровень", StringComparison.OrdinalIgnoreCase)
                .Replace("advanced skill", "продвинутый уровень", StringComparison.OrdinalIgnoreCase)
                .Replace("above advanced", "мастер", StringComparison.OrdinalIgnoreCase)
                .Replace("points for detected item", "опыт за найденный предмет", StringComparison.OrdinalIgnoreCase)
                .Replace("points for detected watching prisoner", "опыт за замеченного игрока", StringComparison.OrdinalIgnoreCase)
                .Replace("points for detected camouflaged prisoner", "опыт за замеченного замаскированного игрока", StringComparison.OrdinalIgnoreCase)
                .Replace("item detection radius", "радиус поиска предметов", StringComparison.OrdinalIgnoreCase)
                .Replace("item detection highlight by day radius", "радиус подсветки предметов днём", StringComparison.OrdinalIgnoreCase)
                .Replace("item detection highlight by night radius", "радиус подсветки предметов ночью", StringComparison.OrdinalIgnoreCase)
                .Replace("watching detection time", "время обнаружения наблюдателя", StringComparison.OrdinalIgnoreCase)
                .Replace("watching detection radius", "радиус обнаружения наблюдателя", StringComparison.OrdinalIgnoreCase)
                .Replace("watching detection chance", "шанс обнаружения наблюдателя", StringComparison.OrdinalIgnoreCase)
                .Replace("character detection angle", "угол обнаружения персонажа", StringComparison.OrdinalIgnoreCase)
                .Replace("character detection time", "время обнаружения персонажа", StringComparison.OrdinalIgnoreCase)
                .Replace("character detection periodic range increment", "добавка к радиусу повторной проверки", StringComparison.OrdinalIgnoreCase)
                .Replace("hidden character detection range", "радиус обнаружения скрытого игрока", StringComparison.OrdinalIgnoreCase)
                .Replace("hidden character hot spot detection range", "радиус замечания видимых частей скрытого игрока", StringComparison.OrdinalIgnoreCase)
                .Replace("hidden character sound detection range", "радиус обнаружения скрытого игрока по звуку", StringComparison.OrdinalIgnoreCase)
                .Replace("track detection range", "радиус поиска следов", StringComparison.OrdinalIgnoreCase)
                .Replace("trap detection range", "радиус поиска ловушек", StringComparison.OrdinalIgnoreCase)
                .Replace("focus mode not moving focus range multiplier", "дальность фокуса стоя на месте", StringComparison.OrdinalIgnoreCase)
                .Replace("focus mode slow moving focus range multiplier", "дальность фокуса при медленном движении", StringComparison.OrdinalIgnoreCase)
                .Replace("focus mode medium moving focus range multiplier", "дальность фокуса при движении", StringComparison.OrdinalIgnoreCase)
                .Replace("container item spawn probability modifier", "шанс найти предмет в контейнере", StringComparison.OrdinalIgnoreCase)
                .Replace("flashbang flash fade out duration multiplier", "длительность ослепления флешкой", StringComparison.OrdinalIgnoreCase)
                .Replace("chance", "шанс", StringComparison.OrdinalIgnoreCase)
                .Replace("min duration", "минимальная длительность", StringComparison.OrdinalIgnoreCase)
                .Replace("max duration", "максимальная длительность", StringComparison.OrdinalIgnoreCase);
        }

        if (path.Contains("/vehicles/spawningpresets/automaticspawn/", StringComparison.OrdinalIgnoreCase)
            && path.Contains("radiation", StringComparison.OrdinalIgnoreCase))
        {
            label = label
                .Replace("fuel amount spawn percentage range", "топливо при появлении", StringComparison.OrdinalIgnoreCase)
                .Replace("fuel amount появление percentage range", "топливо при появлении", StringComparison.OrdinalIgnoreCase)
                .Replace("battery charge spawn percentage range", "заряд аккумулятора при появлении", StringComparison.OrdinalIgnoreCase)
                .Replace("батарейка charge появление percentage range", "заряд аккумулятора при появлении", StringComparison.OrdinalIgnoreCase)
                .Replace("spawn health percentage range", "состояние транспорта при появлении", StringComparison.OrdinalIgnoreCase)
                .Replace("появление health percentage range", "состояние транспорта при появлении", StringComparison.OrdinalIgnoreCase)
                .Replace("spawn chance", "шанс появления", StringComparison.OrdinalIgnoreCase)
                .Replace("появление шанс", "шанс появления", StringComparison.OrdinalIgnoreCase)
                .Replace("is functionality attachment", "влияет на работоспособность транспорта", StringComparison.OrdinalIgnoreCase);
        }

        if (path.Contains("/vehicles/spawningpresets/spawngroups/", StringComparison.OrdinalIgnoreCase))
        {
            label = ReplaceSemanticLabelPart(label, "vehicle presets", "доступные варианты транспорта");
        }

        if (path.Contains("/characters/spawnerpresets/fish", StringComparison.OrdinalIgnoreCase))
        {
            label = ReplaceSemanticLabelPart(label, "fish spawn data", "виды рыбы");
            label = ReplaceSemanticLabelPart(label, "fish появление data", "виды рыбы");
            label = ReplaceSemanticLabelPart(label, "fish species data", "вид рыбы");
            label = ReplaceSemanticLabelPart(label, "species data", "вид рыбы");
            label = ReplaceSemanticLabelPart(label, "fish species", "вид рыбы");
            label = ReplaceSemanticLabelPart(label, "fish вид рыбы", "вид рыбы");
            label = ReplaceSemanticLabelPart(label, "fish какой вид рыбы", "вид рыбы");
            label = ReplaceSemanticLabelPart(label, "spawning weight", "вес появления");
            label = ReplaceSemanticLabelPart(label, "spawning chance", "шанс появления");
            label = ReplaceSemanticLabelPart(label, "spawning шанс", "шанс появления");
        }

        if (IsRegularItemSpawnerPresetAsset(relativePath))
        {
            label = ReplaceSemanticLabelPart(label, "spawner data", "пресет появления");
            label = ReplaceSemanticLabelPart(label, "item classes", "предметы пресета");
            label = ReplaceSemanticLabelPart(label, "always spawn", "всегда создавать предмет");
            label = ReplaceSemanticLabelPart(label, "always появление", "всегда создавать предмет");
            label = ReplaceSemanticLabelPart(label, "use item zone", "учитывать игровую зону предмета");
            label = ReplaceSemanticLabelPart(label, "use item rarity", "учитывать редкость предмета");
            label = ReplaceSemanticLabelPart(label, "use item spawn group", "учитывать группу появления предмета");
            label = ReplaceSemanticLabelPart(label, "use item появление group", "учитывать группу появления предмета");
            label = ReplaceSemanticLabelPart(label, "use item group", "учитывать группу появления предмета");
            label = ReplaceSemanticLabelPart(label, "probability", "шанс появления предмета");
            label = ReplaceSemanticLabelPart(label, "initial damage", "начальная повреждённость");
            label = ReplaceSemanticLabelPart(label, "начальный damage", "начальная повреждённость");
            label = ReplaceSemanticLabelPart(label, "randomize damage", "разброс повреждённости");
            label = ReplaceSemanticLabelPart(label, "initial usage", "начальный ресурс или заряд");
            label = ReplaceSemanticLabelPart(label, "начальный usage", "начальный ресурс или заряд");
            label = ReplaceSemanticLabelPart(label, "начальный расход за крафт", "начальный ресурс или заряд");
            label = ReplaceSemanticLabelPart(label, "randomize usage", "разброс ресурса или заряда");
            label = ReplaceSemanticLabelPart(label, "randomize расход за крафт", "разброс ресурса или заряда");
            label = ReplaceSemanticLabelPart(label, "random расход за крафт", "разброс ресурса или заряда");
            label = ReplaceSemanticLabelPart(label, "initial dirtiness", "начальная грязь");
            label = ReplaceSemanticLabelPart(label, "randomize dirtiness", "разброс грязи");
            label = ReplaceSemanticLabelPart(label, "min ammo count", "минимум патронов");
            label = ReplaceSemanticLabelPart(label, "max ammo count", "максимум патронов");
            label = ReplaceSemanticLabelPart(label, "min stack amount", "минимум в стопке");
            label = ReplaceSemanticLabelPart(label, "max stack amount", "максимум в стопке");
            label = ReplaceSemanticLabelPart(label, "use collision trace to adjust spawn location", "подправлять место появления по столкновению");
            label = ReplaceSemanticLabelPart(label, "use collision trace to adjust spawn rotation", "подправлять поворот предмета по столкновению");
        }

        if (path.Contains("/items/spawnerpresets/examine_data_presets/", StringComparison.OrdinalIgnoreCase))
        {
            label = ReplaceSemanticLabelPart(label, "spawner data", "набор предметов");
            label = ReplaceSemanticLabelPart(label, "item classes", "предметы набора");
            label = ReplaceSemanticLabelPart(label, "always spawn", "всегда выдавать");
            label = ReplaceSemanticLabelPart(label, "always появление", "всегда выдавать");
            label = ReplaceSemanticLabelPart(label, "use item zone", "учитывать игровую зону предмета");
            label = ReplaceSemanticLabelPart(label, "use item rarity", "учитывать редкость предмета");
            label = ReplaceSemanticLabelPart(label, "use item spawn group", "учитывать группу появления предмета");
            label = ReplaceSemanticLabelPart(label, "use item появление group", "учитывать группу появления предмета");
            label = ReplaceSemanticLabelPart(label, "use item group", "учитывать группу появления предмета");
            label = ReplaceSemanticLabelPart(label, "probability", "шанс появления набора");
            label = ReplaceSemanticLabelPart(label, "initial damage", "начальная повреждённость");
            label = ReplaceSemanticLabelPart(label, "начальный damage", "начальная повреждённость");
            label = ReplaceSemanticLabelPart(label, "randomize damage", "разброс повреждённости");
            label = ReplaceSemanticLabelPart(label, "initial usage", "начальный ресурс или заряд");
            label = ReplaceSemanticLabelPart(label, "начальный usage", "начальный ресурс или заряд");
            label = ReplaceSemanticLabelPart(label, "начальный расход за крафт", "начальный ресурс или заряд");
            label = ReplaceSemanticLabelPart(label, "randomize usage", "разброс ресурса или заряда");
            label = ReplaceSemanticLabelPart(label, "randomize расход за крафт", "разброс ресурса или заряда");
            label = ReplaceSemanticLabelPart(label, "random расход за крафт", "разброс ресурса или заряда");
            label = ReplaceSemanticLabelPart(label, "initial dirtiness", "начальная грязь");
            label = ReplaceSemanticLabelPart(label, "randomize dirtiness", "разброс грязи");
            label = ReplaceSemanticLabelPart(label, "min ammo count", "минимум патронов");
            label = ReplaceSemanticLabelPart(label, "max ammo count", "максимум патронов");
            label = ReplaceSemanticLabelPart(label, "min stack amount", "минимум в стопке");
            label = ReplaceSemanticLabelPart(label, "max stack amount", "максимум в стопке");
        }

        if (path.Contains("/items/spawnerpresets2/", StringComparison.OrdinalIgnoreCase))
        {
            label = ReplaceSemanticLabelPart(label, "always spawn", "всегда выдавать");
            label = ReplaceSemanticLabelPart(label, "always появление", "всегда выдавать");
            label = ReplaceSemanticLabelPart(label, "probability", "шанс появления набора");
            label = ReplaceSemanticLabelPart(label, "fixed items", "предметы контейнерного набора");
            label = ReplaceSemanticLabelPart(label, "subpresets", "готовые подпакеты лута");
            label = ReplaceSemanticLabelPart(label, "preset", "готовый подпакет");
            label = ReplaceSemanticLabelPart(label, "rarity", "редкость набора");
            label = ReplaceSemanticLabelPart(label, "quantity", "сколько предметов выдавать");
            label = ReplaceSemanticLabelPart(label, "allow duplicates", "разрешать повторы предметов");
            label = ReplaceSemanticLabelPart(label, "should filter items by zone", "учитывать игровую зону предмета");
            label = ReplaceSemanticLabelPart(label, "initial damage", "начальная повреждённость");
            label = ReplaceSemanticLabelPart(label, "начальный damage", "начальная повреждённость");
            label = ReplaceSemanticLabelPart(label, "random damage", "разброс повреждённости");
            label = ReplaceSemanticLabelPart(label, "initial usage", "начальный ресурс или заряд");
            label = ReplaceSemanticLabelPart(label, "начальный usage", "начальный ресурс или заряд");
            label = ReplaceSemanticLabelPart(label, "начальный расход за крафт", "начальный ресурс или заряд");
            label = ReplaceSemanticLabelPart(label, "random usage", "разброс ресурса или заряда");
            label = ReplaceSemanticLabelPart(label, "random расход за крафт", "разброс ресурса или заряда");
        }

        if (path.Contains("/foliage/farming/", StringComparison.OrdinalIgnoreCase))
        {
            var speciesNameLabel = path.Contains("/pests/", StringComparison.OrdinalIgnoreCase)
                ? "название вредителя"
                : path.Contains("/diseases/", StringComparison.OrdinalIgnoreCase)
                    ? "название болезни"
                    : "название растения";

            label = label
                .Replace("растительный species", "виды растений", StringComparison.OrdinalIgnoreCase)
                .Replace("species name", speciesNameLabel, StringComparison.OrdinalIgnoreCase)
                .Replace("plant species", "виды растений", StringComparison.OrdinalIgnoreCase)
                .Replace("pests", "вредители", StringComparison.OrdinalIgnoreCase)
                .Replace("diseases", "болезни", StringComparison.OrdinalIgnoreCase)
                .Replace("seeds optimaltemperature", "лучшая температура для семян", StringComparison.OrdinalIgnoreCase)
                .Replace("seeds optimal temperature", "лучшая температура для семян", StringComparison.OrdinalIgnoreCase)
                .Replace("seed", "пакет семян", StringComparison.OrdinalIgnoreCase)
                .Replace("growth optimal temperature", "лучшая температура роста", StringComparison.OrdinalIgnoreCase)
                .Replace("stage growth time game hours", "сколько часов длится одна стадия роста", StringComparison.OrdinalIgnoreCase)
                .Replace("last stage lifetime", "сколько живёт финальная стадия", StringComparison.OrdinalIgnoreCase)
                .Replace("last stage death start percent", "когда финальная стадия начинает портиться", StringComparison.OrdinalIgnoreCase)
                .Replace("last stage", "финальная стадия роста", StringComparison.OrdinalIgnoreCase)
                .Replace("harvesting reduction percentage", "штраф к урожаю", StringComparison.OrdinalIgnoreCase);
        }

        if (path.Contains("/ui/gameevents/itemselection/", StringComparison.OrdinalIgnoreCase))
        {
            label = label
                .Replace("main item", "главный предмет", StringComparison.OrdinalIgnoreCase)
                .Replace("support items", "дополнительные предметы", StringComparison.OrdinalIgnoreCase)
                .Replace("selection name", "название набора", StringComparison.OrdinalIgnoreCase)
                .Replace("attachments", "крепления", StringComparison.OrdinalIgnoreCase)
                .Replace("team index", "номер команды события", StringComparison.OrdinalIgnoreCase)
                .Replace("size x", "ширина карточки", StringComparison.OrdinalIgnoreCase)
                .Replace("size y", "высота карточки", StringComparison.OrdinalIgnoreCase);
        }

        if (IsGameEventMarkerAsset(relativePath))
        {
            label = label
                .Replace("drop zone parameters", "параметры зоны сброса", StringComparison.OrdinalIgnoreCase)
                .Replace("ctf parameters", "параметры захвата флага", StringComparison.OrdinalIgnoreCase)
                .Replace("game event parameters", "настройки события", StringComparison.OrdinalIgnoreCase)
                .Replace("possible primary weapons", "основное оружие на выбор", StringComparison.OrdinalIgnoreCase)
                .Replace("possible secondary weapons", "пистолет на выбор", StringComparison.OrdinalIgnoreCase)
                .Replace("possible tertiary weapons", "ближний бой на выбор", StringComparison.OrdinalIgnoreCase)
                .Replace("possible outfits", "одежда на выбор", StringComparison.OrdinalIgnoreCase)
                .Replace("possible support items", "дополнительное снаряжение", StringComparison.OrdinalIgnoreCase)
                .Replace("mandatory gear", "обязательный набор", StringComparison.OrdinalIgnoreCase)
                .Replace("round duration", "длительность раунда", StringComparison.OrdinalIgnoreCase)
                .Replace("round время", "длительность раунда", StringComparison.OrdinalIgnoreCase)
                .Replace("round limit", "лимит раундов", StringComparison.OrdinalIgnoreCase)
                .Replace("win limit", "лимит побед", StringComparison.OrdinalIgnoreCase)
                .Replace("respawn delay", "задержка возрождения", StringComparison.OrdinalIgnoreCase)
                .Replace("allow respawn", "разрешить возрождение", StringComparison.OrdinalIgnoreCase)
                .Replace("timeout duration", "время ожидания события", StringComparison.OrdinalIgnoreCase)
                .Replace("team limit", "лимит игроков по командам", StringComparison.OrdinalIgnoreCase)
                .Replace("min participants", "минимум участников", StringComparison.OrdinalIgnoreCase)
                .Replace("(минимум) participants", "минимум участников", StringComparison.OrdinalIgnoreCase)
                .Replace("friendly fire", "огонь по своим", StringComparison.OrdinalIgnoreCase)
                .Replace("entry fee", "плата за вход", StringComparison.OrdinalIgnoreCase)
                .Replace("points per enemy kill", "очки за убийство врага", StringComparison.OrdinalIgnoreCase)
                .Replace("points per enemy убить", "очки за убийство врага", StringComparison.OrdinalIgnoreCase)
                .Replace("points per team kill", "штраф за убийство союзника", StringComparison.OrdinalIgnoreCase)
                .Replace("points per team убить", "штраф за убийство союзника", StringComparison.OrdinalIgnoreCase)
                .Replace("points per death", "штраф за смерть", StringComparison.OrdinalIgnoreCase)
                .Replace("points per suicide", "штраф за самоубийство", StringComparison.OrdinalIgnoreCase)
                .Replace("points per assist", "очки за помощь", StringComparison.OrdinalIgnoreCase)
                .Replace("points per headshot", "очки за выстрел в голову", StringComparison.OrdinalIgnoreCase)
                .Replace("points per round win", "очки за победу в раунде", StringComparison.OrdinalIgnoreCase)
                .Replace("points for participation", "очки славы за участие", StringComparison.OrdinalIgnoreCase)
                .Replace("score to fame conversion factor", "перевод очков в славу", StringComparison.OrdinalIgnoreCase)
                .Replace("event name", "название события", StringComparison.OrdinalIgnoreCase)
                .Replace("event description", "описание события", StringComparison.OrdinalIgnoreCase)
                .Replace("prerequisites text", "подсказка режима", StringComparison.OrdinalIgnoreCase)
                .Replace("weapon text", "подсказка по оружию", StringComparison.OrdinalIgnoreCase)
                .Replace("rewards text", "подсказка по наградам", StringComparison.OrdinalIgnoreCase)
                .Replace("skip key phase", "пропустить фазу ключа", StringComparison.OrdinalIgnoreCase)
                .Replace("points per capture", "очки за захват флага", StringComparison.OrdinalIgnoreCase)
                .Replace("fame points", "очки славы", StringComparison.OrdinalIgnoreCase)
                .Replace("score", "очки", StringComparison.OrdinalIgnoreCase);
            label = Regex.Replace(
                label,
                @"\(\s*минимум\s*\)\s*participants",
                "минимум участников",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            label = Regex.Replace(
                label,
                @"\bmin\s+participants\b",
                "минимум участников",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            label = Regex.Replace(
                label,
                @"\bmax\s+participants\b",
                "максимум участников",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        if (path.Contains("/items/weapons/", StringComparison.OrdinalIgnoreCase))
        {
            label = label
                .Replace("damage per shot", "урон за выстрел", StringComparison.OrdinalIgnoreCase)
                .Replace("max loaded ammo", "патронов в магазине", StringComparison.OrdinalIgnoreCase)
                .Replace("event max ammo", "патронов для события", StringComparison.OrdinalIgnoreCase)
                .Replace("zero range", "дистанция пристрелки", StringComparison.OrdinalIgnoreCase)
                .Replace("field of view", "поле зрения при прицеливании", StringComparison.OrdinalIgnoreCase)
                .Replace("depth of field focal distance", "дистанция фокуса при прицеливании", StringComparison.OrdinalIgnoreCase)
                .Replace("view kick multiplier", "сила отдачи камеры", StringComparison.OrdinalIgnoreCase)
                .Replace("max recoil offset", "максимальная отдача", StringComparison.OrdinalIgnoreCase)
                .Replace("recoil recovery speed", "скорость возврата после отдачи", StringComparison.OrdinalIgnoreCase)
                .Replace("max range", "максимальная дальность", StringComparison.OrdinalIgnoreCase)
                .Replace("rof", "скорострельность", StringComparison.OrdinalIgnoreCase)
                .Replace("weight", "вес", StringComparison.OrdinalIgnoreCase)
                .Replace("noise level", "уровень шума", StringComparison.OrdinalIgnoreCase)
                .Replace("damage over time", "урон со временем", StringComparison.OrdinalIgnoreCase)
                .Replace("default ammunition item class", "патрон по умолчанию", StringComparison.OrdinalIgnoreCase)
                .Replace("size x", "ширина в инвентаре", StringComparison.OrdinalIgnoreCase)
                .Replace("size y", "высота в инвентаре", StringComparison.OrdinalIgnoreCase);
        }

        if (path.Contains("/spawnequipment/", StringComparison.OrdinalIgnoreCase))
        {
            label = label
                .Replace("item class", "выдаваемый предмет", StringComparison.OrdinalIgnoreCase)
                .Replace("condition", "состояние предмета", StringComparison.OrdinalIgnoreCase);
        }

        if (path.Contains("/fortifications/locks/", StringComparison.OrdinalIgnoreCase))
        {
            label = label
                .Replace("number of neutralization attempts", "число попыток обезвреживания", StringComparison.OrdinalIgnoreCase)
                .Replace("are fame points required", "требуются очки славы", StringComparison.OrdinalIgnoreCase)
                .Replace("locked message", "текст при закрытом замке", StringComparison.OrdinalIgnoreCase)
                .Replace("lockpick message", "текст для взлома", StringComparison.OrdinalIgnoreCase);
        }

        label = NormalizeLocalizedLabel(label);
        if (IsGameEventMarkerAsset(relativePath))
        {
            label = label
                .Replace(" (минимум) participants", " / минимум участников", StringComparison.OrdinalIgnoreCase)
                .Replace(" (максимум) participants", " / максимум участников", StringComparison.OrdinalIgnoreCase)
                .Replace(" / minimum participants", " / минимум участников", StringComparison.OrdinalIgnoreCase)
                .Replace(" / maximum participants", " / максимум участников", StringComparison.OrdinalIgnoreCase);
        }

        if (path.Contains("/items/weapons/", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedWeaponLabel = NormalizeAssetKey(label);
            if (normalizedWeaponLabel.Contains("usecustommalfunctionchances", StringComparison.Ordinal))
            {
                label = "использовать свой шанс неисправности";
            }
            else if (normalizedWeaponLabel.Contains("malfunctionchancesvaluemin", StringComparison.Ordinal))
            {
                label = "общий шанс неисправности (минимум)";
            }
            else if (normalizedWeaponLabel.Contains("malfunctionchancesvaluemax", StringComparison.Ordinal))
            {
                label = "общий шанс неисправности (максимум)";
            }
            else if (normalizedWeaponLabel.Contains("malfunctionchances", StringComparison.Ordinal))
            {
                label = "общий шанс неисправности";
            }
        }

        if (path.Contains("/quests/", StringComparison.OrdinalIgnoreCase))
        {
            label = Regex.Replace(
                label,
                @"item\s+экипировать(?:\s+data)?\s*/\s*items\s+(\d+)",
                "нужно держать рядом / предмет $1",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            label = Regex.Replace(
                label,
                @"item\s+экипировать(?:\s+data)?\s*/\s*items",
                "нужно держать рядом",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            label = label
                .Replace("possible награды", "варианты награды", StringComparison.OrdinalIgnoreCase)
                .Replace("награда skill experience", "опыт навыков за квест", StringComparison.OrdinalIgnoreCase)
                .Replace("награда currency", "денежная награда", StringComparison.OrdinalIgnoreCase)
                .Replace("награда trade deals", "варианты выдачи у торговца", StringComparison.OrdinalIgnoreCase)
                .Replace("награда trade deal", "выдача у торговца", StringComparison.OrdinalIgnoreCase)
                .Replace("random additional сколько нужно рядом", "случайная добавка к количеству", StringComparison.OrdinalIgnoreCase)
                .Replace("accepted cook level", "готовность еды", StringComparison.OrdinalIgnoreCase)
                .Replace("accepted cook quality", "качество еды", StringComparison.OrdinalIgnoreCase)
                .Replace("accepted item mass", "масса предмета", StringComparison.OrdinalIgnoreCase)
                .Replace("accepted item health", "состояние предмета", StringComparison.OrdinalIgnoreCase)
                .Replace("accepted item resource amount", "ресурс в предмете", StringComparison.OrdinalIgnoreCase)
                .Replace("accepted item resource ratio", "заполненность ресурса", StringComparison.OrdinalIgnoreCase);
            label = Regex.Replace(
                label,
                @"варианты награды\s+#?(\d+)",
                "вариант награды $1",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            label = Regex.Replace(
                label,
                @"денежная награда\s*/\s*элемент\s+e\s*currency\s*тип::([a-z0-9_]+)",
                match => $"денежная награда / {LocalizeQuestCurrencyType(match.Groups[1].Value)}",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return CapitalizeFirst(label.Trim());
    }

    private static string NormalizeItemSpawningUserLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return label;
        }

        return label
            .Replace("Разрешённые зоны / военные WW 2", "Разрешённые зоны / военные (WW2)", StringComparison.OrdinalIgnoreCase)
            .Replace("Разрешённые зоны / берег", "Разрешённые зоны / побережье", StringComparison.OrdinalIgnoreCase)
            .Replace("Разрешённые зоны / деревня", "Разрешённые зоны / сельская местность", StringComparison.OrdinalIgnoreCase)
            .Replace("Откат на одного бойца / min", "Откат на одного бойца (минимум)", StringComparison.OrdinalIgnoreCase)
            .Replace("Откат на одного бойца / max", "Откат на одного бойца (максимум)", StringComparison.OrdinalIgnoreCase)
            .Replace("Время отката / min", "Время отката (минимум)", StringComparison.OrdinalIgnoreCase)
            .Replace("Время отката / max", "Время отката (максимум)", StringComparison.OrdinalIgnoreCase)
            .Replace("Группа отката / row name", "Группа отката", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetExportContextLabel(string relativePath, string exportName, out string contextLabel)
    {
        contextLabel = string.Empty;
        if (string.IsNullOrWhiteSpace(exportName))
        {
            return false;
        }

        var normalizedExport = NormalizeAssetKey(exportName);
        var normalizedRoot = NormalizeAssetKey(Path.GetFileNameWithoutExtension(relativePath));
        if (normalizedExport == normalizedRoot
            || normalizedExport == $"default{normalizedRoot}c"
            || normalizedExport == $"{normalizedRoot}c")
        {
            return false;
        }

        var path = relativePath.ToLowerInvariant();
        if (!path.Contains("/bodyeffects/", StringComparison.Ordinal)
            && !path.Contains("/metabolism/", StringComparison.Ordinal))
        {
            return false;
        }

        var knownContexts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["prisonerbodyconditionorsymptomsideeffectdamage"] = "урон",
            ["prisonerbodyconditionorsymptomsideeffectdisorientation"] = "дезориентация",
            ["prisonerbodyconditionorsymptomsideeffectdoublevision"] = "двоение в глазах",
            ["prisonerbodyconditionorsymptomsideeffectgroundmovementspeedmodifier"] = "бонус или штраф к скорости ходьбы и бега",
            ["prisonerbodyconditionorsymptomsideeffectwatermovementspeedmodifier"] = "бонус или штраф к скорости плавания",
            ["prisonerbodyconditionorsymptomsideeffectintelligencemodifier"] = "бонус или штраф к интеллекту",
            ["prisonerbodyconditionorsymptomsideeffectstrengthmodifier"] = "бонус или штраф к силе",
            ["prisonerbodyconditionorsymptomsideeffectconstitutionmodifier"] = "бонус или штраф к телосложению",
            ["prisonerbodyconditionorsymptomsideeffectendurancemodifier"] = "бонус или штраф к выносливости",
            ["prisonerbodyconditionorsymptomsideeffectdexteritymodifier"] = "бонус или штраф к ловкости",
            ["prisonerbodyconditionorsymptomsideeffectmaxmovementpace"] = "ограничение максимального темпа движения",
            ["prisonerbodyconditionorsymptomsideeffectmaxstaminamodifier"] = "бонус или штраф к максимальной выносливости",
            ["prisonerbodyconditionorsymptomsideeffectstaminamodifier"] = "бонус или штраф к текущей выносливости",
            ["prisonerbodyconditionorsymptomsideeffectperformancescoremodifier"] = "бонус или штраф к успешности действий",
            ["prisonerbodyconditionorsymptomsideeffectperiodicaffect"] = "периодический приступ"
        };

        if (knownContexts.TryGetValue(normalizedExport, out var known))
        {
            contextLabel = CapitalizeFirst(known);
            return true;
        }

        return false;
    }

    private static string HumanizeCamel(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var chars = new List<char>(input.Length + 10);
        for (var i = 0; i < input.Length; i++)
        {
            var current = input[i];
            if (i > 0)
            {
                var previous = input[i - 1];
                var next = i < input.Length - 1 ? input[i + 1] : '\0';
                var splitBeforeUpper = char.IsLetter(current)
                                       && char.IsUpper(current)
                                       && (char.IsLower(previous)
                                           || (char.IsUpper(previous) && next != '\0' && char.IsLower(next)));
                var splitBeforeDigit = char.IsDigit(current) && char.IsLetter(previous);
                var splitBeforeLetter = char.IsLetter(current) && char.IsDigit(previous);
                if (splitBeforeUpper || splitBeforeDigit || splitBeforeLetter)
                {
                    chars.Add(' ');
                }
            }

            chars.Add(current);
        }

        return string.Join(' ', new string(chars.ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string LocalizeIndexedSegments(string input)
    {
        var value = input;
        value = Regex.Replace(value, @"Keys\[(\d+)\]", match => $"ступень {int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) + 1}", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"Ingredients?\[(\d+)\]", match => $"ингредиент {int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) + 1}", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"Allowed Types?\[(\d+)\]", match => $"разрешённый тип {int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) + 1}", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\[(\d+)\]", match => $" #{int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) + 1}", RegexOptions.IgnoreCase);
        return value;
    }

    private static string LocalizeCommonGameplayTerms(string input)
    {
        var replacements = new (string From, string To)[]
        {
            ("response idle initial interval", "начальная пауза ожидания перед ответом"),
            ("response idle interval", "пауза ожидания перед ответом"),
            ("response idle montages", "анимации ожидания перед ответом"),
            ("should constitution affect absorption capacity", "учитывать телосложение для максимума вещества"),
            ("severity vs energy booster substance amount ratio", "тяжесть эффекта по уровню стимулятора в организме"),
            ("absorption capacity modifier vs constitution ratio", "максимум вещества по телосложению"),
            ("absorption capacity constitution modifier ratio", "модификатор максимума вещества по телосложению"),
            ("disposition", "тип воздействия"),
            ("absorption capacity", "максимум вещества в организме"),
            ("absorption rate", "скорость всасывания"),
            ("base discard rate", "базовая скорость выведения"),
            ("discard rate multiplier per other substance", "влияние других веществ на выведение"),
            ("amount to discard per water transferred to bladder", "выведение с мочой"),
            ("other substance", "других веществ"),
            ("absorbed alcohol ratio", "уровень алкоголя в организме"),
            ("attribute modifier foreign substance classes", "связанные вещества эффекта"),
            ("energy booster substance amount ratio", "уровень стимулятора в организме"),
            ("energy booster substance class", "вещество-источник эффекта"),
            ("side effects", "побочные эффекты"),
            ("base body part multiplier", "общая защита части тела"),
            ("sharp melee damage reduction", "защита от режущего ближнего урона"),
            ("blunt melee damage reduction", "защита от тупого ближнего урона"),
            ("is ignored by spawners", "не использовать для спавна в мире"),
            ("cached max draw distance", "максимальная дистанция отображения"),
            ("mesh slices", "части 3D-модели"),
            ("skeletal mesh socket overrides", "переопределение сокетов скелета"),
            ("static mesh socket overrides", "переопределение сокетов статической модели"),
            ("override materials", "переопределение материалов"),
            ("damage ratio", "потеря здоровья"),
            ("damage percentage on uncraft", "повреждение при разборе"),
            ("intensity", "сила эффекта"),
            ("vomit probability", "шанс рвоты"),
            ("chance", "шанс"),
            ("modifier vs severity", "сила эффекта по тяжести"),
            ("modifier", "модификатор"),
            ("editor curve data", "кривая эффекта"),
            ("keys", "точки кривой"),
            ("ingredients", "ингредиенты"),
            ("allowed types", "варианты ингредиента"),
            ("allowed items", "варианты предмета"),
            ("max occurrences", "максимум копий в мире"),
            ("allowed locations", "разрешённые зоны"),
            ("cooldown per squad member", "откат на одного бойца"),
            ("cooldown group", "группа отката"),
            ("is subject to allow traps config", "подчиняться серверному правилу ловушек"),
            ("should override initial and random usage", "переопределять ресурс предмета"),
            ("initial usage override", "начальный ресурс"),
            ("random usage override", "случайная добавка к ресурсу"),
            ("spawn rotation randomization", "случайный разброс поворота"),
            ("is affected by lower groups", "учитывать более низкие группы"),
            ("coastal", "побережье"),
            ("continantal", "континент"),
            ("mountain", "горы"),
            ("urban", "город"),
            ("rural", "сельская местность"),
            ("industrial", "промзона"),
            ("police", "полиция"),
            ("military basic", "военные (база)"),
            ("military medium", "военные (средний уровень)"),
            ("military advanced", "военные (продвинутый уровень)"),
            ("military ww2", "военные (ww2)"),
            ("sport", "спорт"),
            ("market", "рынок"),
            ("gas station", "заправка"),
            ("airfield", "аэродром"),
            ("possible ingredient", "возможный предмет"),
            ("character classes", "классы персонажей"),
            ("possible characters", "персонажи для события"),
            ("selection weight", "вес выбора"),
            ("encounter data", "варианты событий"),
            ("encounter class", "какое событие может запуститься"),
            ("encounter weight", "вес выбора события"),
            ("initial encounter spawn delay", "задержка перед первым запуском события"),
            ("encounter spawn check interval", "интервал проверки запуска события"),
            ("encounter cooldown interval", "пауза между запусками события"),
            ("character spawn distance range", "дистанция появления персонажей"),
            ("character fallback spawn distance range", "запасная дистанция появления персонажей"),
            ("exterior spawn points view check distance", "дистанция проверки видимости внешних точек появления"),
            ("fallback location z check distance", "проверка высоты для запасной точки появления"),
            ("fallback location zcheck distance", "проверка высоты для запасной точки появления"),
            ("use fast sight check", "использовать быструю проверку видимости"),
            ("use visited point spawn prohibition", "запретить повторное появление в недавно использованной точке"),
            ("visited points removal time", "через сколько очищать список использованных точек"),
            ("defender horde encounter class", "класс орды защитников"),
            ("dropship class", "класс десантного корабля"),
            ("dropship sentry respawn class", "класс охраны десантного корабля"),
            ("min removal sight distance", "минимальная дистанция скрытия вне поля зрения"),
            ("min removal distance", "минимальная дистанция удаления"),
            ("allowed group spawn points", "разрешённые групповые точки появления"),
            ("is group spawn", "появлять NPC группой"),
            ("character base amount range", "размер группы NPC"),
            ("character amount player cap", "лимит NPC на игрока"),
            ("character respawn time range", "время повторного появления NPC"),
            ("character respawn batch size", "размер волны повторного появления NPC"),
            ("minimum distance between characters", "минимальная дистанция между NPC"),
            ("allow fallback spawns", "разрешить запасные точки появления"),
            ("can ever remove low priority characters", "удалять NPC низкого приоритета"),
            ("ignore global character limit", "игнорировать общий лимит персонажей"),
            ("ignore characters outside of zone or unowned when counting", "не учитывать чужих и внешних персонажей при подсчёте"),
            ("removal time after virtualization", "время удаления после виртуализации"),
            ("can ever be virtualized", "разрешить виртуализацию NPC"),
            ("is subject to new player protection", "защищать новичков от этого события"),
            ("periodic horde trigger check time", "интервал проверки случайной орды"),
            ("periodic horde trigger chance", "шанс случайной орды"),
            ("horde activation chance per noise tag", "шанс орды по типу шума"),
            ("cooldown interval between noise trigger checks", "пауза между проверками шума"),
            ("minimum necessary spawned character num", "минимум персонажей для этого режима"),
            ("spawn point types additional data", "дополнительные правила для типов точек появления"),
            ("spawn point types", "типы точек появления"),
            ("character hidden spawn type", "скрытый тип появления персонажа"),
            ("allowed spawn points", "разрешённые точки появления"),
            ("tag name", "внутренний тег"),
            ("prosperity level info per level", "уровни процветания"),
            ("prosperity level threshold gold", "порог процветания по золоту"),
            ("prosperity level threshold", "порог процветания"),
            ("gbc refresh rate per hour", "обновление GBC в час"),
            ("gsc refresh rate per hour", "обновление GSC в час"),
            ("gbcrefresh rate per hour", "обновление GBC в час"),
            ("gscrefresh rate per hour", "обновление GSC в час"),
            ("expensive tradeables spawn chance map", "шанс появления дорогих товаров"),
            ("vehicle spawn group parent tag", "группа появления транспорта"),
            ("repair experience multiplier", "опыт за ремонт"),
            ("fill fuel experience multiplier", "опыт за заправку"),
            ("drain fuel experience multiplier", "опыт за слив топлива"),
            ("charge battery experience multiplier", "опыт за заряд аккумулятора"),
            ("repair radius", "дистанция ремонта"),
            ("repair time multiplier", "время ремонта"),
            ("tool usage multiplier", "расход инструмента"),
            ("fill fuel duration multiplier", "время заправки"),
            ("drain fuel duration multiplier", "время слива топлива"),
            ("charge battery duration multiplier", "время зарядки аккумулятора"),
            ("points per driven distance in km", "опыт за километр езды"),
            ("engine start via motion duration", "время запуска с толкача"),
            ("max speed modifier", "максимальная скорость"),
            ("throttle modifier", "сила разгона"),
            ("wheel steering lerp speed multiplier", "скорость поворота колёс"),
            ("engine handling parameters", "параметры двигателя"),
            ("ignition duration multiplier", "время зажигания"),
            ("ignition failure chance", "шанс сбоя зажигания"),
            ("gearbox handling parameters", "параметры коробки передач"),
            ("gear change delay multiplier", "задержка переключения передачи"),
            ("gear up ratio multiplier", "порог переключения вверх"),
            ("gear down ratio multiplier", "порог переключения вниз"),
            ("engine stalling by acceleration parameters", "срыв двигателя при разгоне"),
            ("engine stalling by up shift parameters", "срыв двигателя при переключении вверх"),
            ("engine stalling when starting via motion", "срыв двигателя при запуске с толкача"),
            ("stop engine chance", "шанс заглушить двигатель"),
            ("throttle curve frequency multiplier", "частота рывков газа"),
            ("brake curve frequency multiplier", "частота рывков тормоза"),
            ("force magnitude scale curve frequency multiplier", "частота силы рывка"),
            ("manual bcu upgrade success chance", "шанс ручного улучшения BCU"),
            ("extract blood sample duration multiplier", "время взятия крови"),
            ("ingredient title", "название ингредиента"),
            ("product", "результат крафта"),
            ("relevant skill", "основной навык рецепта"),
            ("additional amount", "дополнительный расход"),
            ("usage", "расход за крафт"),
            ("purpose", "назначение ингредиента"),
            ("mixing type", "правило смешивания"),
            ("should consume whole", "тратить предмет целиком"),
            ("is resource", "это жидкость или ресурс"),
            ("liters", "литры"),
            ("nutrient inclusion factor", "вклад в питательность"),
            ("rope", "верёвка"),
            ("thread", "нить"),
            ("wire", "проволока"),
            ("sticks", "палки"),
            ("stick", "палка"),
            ("stone", "камень"),
            ("wooden arrows", "деревянные стрелы"),
            ("wooden spear", "деревянное копьё"),
            ("improvised bow", "самодельный лук"),
            ("improvised courier backpack", "самодельный курьерский рюкзак"),
            ("tree bark rope", "верёвка из коры"),
            ("fire drill", "огневое сверло"),
            ("bonfire", "костёр"),
            ("fire ring", "костровое кольцо"),
            ("fireplace", "кострище"),
            ("fireworks trap", "ловушка с фейерверком"),
            ("underpants", "трусы"),
            ("undershirt", "майка"),
            ("mailbox", "почтовый ящик"),
            ("bodybag", "мешок для тела"),
            ("camera", "камера"),
            ("fetch", "принести"),
            ("bring", "принести"),
            ("kill", "убить"),
            ("puppets", "куклы"),
            ("ammo box", "коробка патронов"),
            ("ammobox", "коробка патронов"),
            ("weapon flashlight", "оружейный фонарь"),
            ("activated charcoal", "активированный уголь"),
            ("potassium iodide", "йодид калия"),
            ("vitamin pills", "витамины"),
            ("medical clothes", "медицинская одежда"),
            ("multiple puppet parts", "части кукол"),
            ("tongue depressors", "шпатели для языка"),
            ("telephone booths", "телефонные будки"),
            ("sabotage acs", "терминалы саботажа"),
            ("aeroplane repair kit", "ремкомплект самолёта"),
            ("car repair kit", "ремкомплект машины"),
            ("gasoline canister small", "маленькая канистра бензина"),
            ("metal scraps", "металлолом"),
            ("oil filter", "масляный фильтр"),
            ("brake oil", "тормозная жидкость"),
            ("car jack", "домкрат"),
            ("car battery cables", "провода для аккумулятора"),
            ("batteries", "батарейки"),
            ("battery", "батарейка"),
            ("bobby pin", "шпилька"),
            ("painkillers", "обезболивающее"),
            ("quest manager", "менеджер квестов"),
            ("quest common data", "общие данные квестов"),
            ("general goods", "общие товары"),
            ("find", "найти"),
            ("interact", "взаимодействовать с"),
            ("set", "экипировать"),
            ("disable tvs", "отключить телевизоры"),
            ("churches", "церкви"),
            ("fountains", "фонтаны"),
            ("disposable masks", "одноразовые маски"),
            ("medical gloves", "медицинские перчатки"),
            ("high top shoes", "высокие ботинки"),
            ("boxer briefs", "боксеры"),
            ("canned sardine", "консервированная сардина"),
            ("wool gloves", "шерстяные перчатки"),
            ("military beanie", "военная вязаная шапка"),
            ("kitchen knife", "кухонный нож"),
            ("handgun", "пистолет"),
            ("sock", "носок"),
            ("bra supporter", "лиф"),
            ("fishing reel", "рыболовная катушка"),
            ("improvised fishing reel", "самодельная рыболовная катушка"),
            ("fishing reel pro", "рыболовная катушка Pro"),
            ("premium boilies pack", "премиальные бойлы"),
            ("pipe wrench", "трубный ключ"),
            ("phone", "телефон"),
            ("tier", "уровень"),
            ("blunt", "тупым оружием"),
            ("bows", "луком"),
            ("axe", "топор"),
            ("improvised", "самодельный"),
            ("plant", "растительный"),
            ("knife", "нож"),
            ("machete", "мачете"),
            ("rag", "тряпка"),
            ("nails", "гвозди"),
            ("nail", "гвоздь"),
            ("lockpick", "отмычка"),
            ("dial lock", "кодовый замок"),
            ("combination", "комбинация"),
            ("shocker", "шокер"),
            ("screwdriver", "отвёртка"),
            ("widget", "виджет"),
            ("one handed", "одноручный"),
            ("two handed", "двуручный"),
            ("1 h", "одноручный"),
            ("2 h", "двуручный"),
            ("1h", "одноручный"),
            ("2h", "двуручный"),
            ("aloe vera", "алоэ вера"),
            ("balaclava", "балаклава"),
            ("barbed wire", "колючая проволока"),
            ("baseball bat", "бейсбольная бита"),
            ("baseball cap", "бейсболка"),
            ("backpack", "рюкзак"),
            ("pants", "штаны"),
            ("shirt", "рубашка"),
            ("hoodie", "худи"),
            ("jacket", "куртка"),
            ("vest", "жилет"),
            ("boots", "ботинки"),
            ("shoes", "ботинки"),
            ("gloves", "перчатки"),
            ("glove", "перчатка"),
            ("hat", "шляпа"),
            ("cap", "кепка"),
            ("briefs", "трусы"),
            ("bandage", "бинт"),
            ("supporter pack", "набор supporter"),
            ("digital deluxe", "набор digital deluxe"),
            ("begin play", "стартовый"),
            ("sardine", "сардина"),
            ("garlic", "чеснок"),
            ("parachute", "парашют"),
            ("water", "вода"),
            ("fishing line", "рыболовная леска"),
            ("fishing hook", "рыболовный крючок"),
            ("fishing rod", "удилище"),
            ("fishing floater", "поплавок"),
            ("fishing bait", "наживка"),
            ("species data", "вид рыбы"),
            ("floater", "поплавок"),
            ("bait", "наживка"),
            ("bread", "хлеб"),
            ("cheese", "сыр"),
            ("corn", "кукуруза"),
            ("cricket", "сверчок"),
            ("grasshopper", "кузнечик"),
            ("meat", "мясо"),
            ("redworm", "красный червь"),
            ("worm", "червь"),
            ("amur", "амур"),
            ("bass", "окунь"),
            ("bleak", "уклейка"),
            ("carp", "карп"),
            ("catfish", "сом"),
            ("chub", "голавль"),
            ("crucian carp", "карась"),
            ("dentex", "дентекс"),
            ("dildo", "дилдо"),
            ("grenade", "граната"),
            ("orata", "ората"),
            ("pike", "щука"),
            ("prussian carp", "серебряный карась"),
            ("ruffe", "ёрш"),
            ("sardine", "сардина"),
            ("tuna", "тунец"),
            ("inmate", "тюремный"),
            ("ground movement speed modifier", "влияние на скорость ходьбы и бега"),
            ("water movement speed modifier", "влияние на скорость плавания"),
            ("strength change", "прямое изменение силы"),
            ("intelligence change", "прямое изменение интеллекта"),
            ("constitution change", "прямое изменение телосложения"),
            ("dexterity change", "прямое изменение ловкости"),
            ("strength modifier", "влияние на силу"),
            ("intelligence modifier", "влияние на интеллект"),
            ("constitution modifier", "влияние на телосложение"),
            ("endurance modifier", "влияние на выносливость"),
            ("dexterity modifier", "влияние на ловкость"),
            ("max stamina modifier", "влияние на максимальную выносливость"),
            ("stamina modifier", "влияние на выносливость"),
            ("performance score modifier", "влияние на успешность действий"),
            ("periodic affect", "периодический приступ"),
            ("symptom class", "какой симптом запускается"),
            ("points per minute of walking", "опыт за минуту ходьбы"),
            ("points per minute of jogging", "опыт за минуту бега трусцой"),
            ("points per minute of running", "опыт за минуту спринта"),
            ("energy consumption multiplier", "расход энергии"),
            ("water consumption multiplier", "расход воды"),
            ("stamina recovery multiplier", "восстановление выносливости"),
            ("value when experience is maximal", "значение на максимальном опыте"),
            ("value when experience is minimal", "значение на минимальном опыте"),
            ("no skill experience awards", "награда опытом без навыка"),
            ("basic skill experience awards", "награда опытом на базовом уровне"),
            ("medium skill experience awards", "награда опытом на среднем уровне"),
            ("advanced skill experience awards", "награда опытом на продвинутом уровне"),
            ("above advanced skill experience awards", "награда опытом на уровне мастер"),
            ("no skill parameters", "параметры без навыка"),
            ("basic parameters", "параметры базового уровня"),
            ("medium parameters", "параметры среднего уровня"),
            ("advanced parameters", "параметры продвинутого уровня"),
            ("above advanced parameters", "параметры уровня мастер"),
            ("basic skill parameters", "параметры базового уровня"),
            ("medium skill parameters", "параметры среднего уровня"),
            ("advanced skill parameters", "параметры продвинутого уровня"),
            ("float curve", "кривая"),
            ("number of neutralization attempts", "число попыток обезвреживания"),
            ("are fame points required", "требуются очки славы"),
            ("locked message", "текст при закрытом замке"),
            ("lockpick message", "текст для взлома"),
            ("item class", "выдаваемый предмет"),
            ("damage per shot", "урон за выстрел"),
            ("max loaded ammo", "патронов в магазине"),
            ("event max ammo", "патронов для события"),
            ("default ammunition item class", "патрон по умолчанию"),
            ("field of view", "поле зрения"),
            ("depth of field focal distance", "дистанция фокуса"),
            ("view kick multiplier", "сила отдачи камеры"),
            ("max recoil offset", "максимальная отдача"),
            ("recoil recovery speed", "скорость возврата после отдачи"),
            ("max range", "максимальная дальность"),
            ("noise level", "уровень шума"),
            ("damage over time", "урон со временем"),
            ("guarded zone tick time", "время обновления охраняемой зоны"),
            ("dropship spawn distance xy", "дистанция появления десантного корабля по оси XY"),
            ("dropship spawn distance", "дистанция появления десантного корабля"),
            ("root nodes", "корневые узлы"),
            ("all nodes", "все узлы"),
            ("use severity to determine life threatening status", "определять опасное для жизни состояние по тяжести эффекта"),
            ("severity range to be life threatening", "диапазон тяжести для опасного состояния"),
            ("life threatening status", "опасное для жизни состояние"),
            ("max movement pace vs severity", "скорость движения по уровню тяжести"),
            ("severity range", "диапазон тяжести"),
            ("lower bound", "нижняя граница"),
            ("upper bound", "верхняя граница"),
            ("allowed types", "разрешённые типы"),
            ("allowed items", "разрешённые предметы"),
            ("experience reward", "опыт за крафт"),
            ("fame point reward", "очки славы за крафт"),
            ("product quality influence", "влияние на качество результата"),
            ("return on uncraft", "возврат при разборе"),
            ("duration", "время"),
            ("stamina", "выносливость"),
            ("movement pace", "скорость движения"),
            ("jump z velocity", "высота прыжка"),
            ("terminal velocity", "предельная скорость"),
            ("max speed", "максимальная скорость"),
            ("drag intensity", "сила сопротивления"),
            ("weight load ratio", "весовая нагрузка"),
            ("alcohol", "алкоголь"),
            ("intoxication", "опьянение"),
            ("hangover", "похмелье"),
            ("nausea", "тошнота"),
            ("constitution", "телосложение"),
            ("guarded zone", "охраняемая зона"),
            ("dropship", "десантный корабль"),
            ("spawn", "появление"),
            ("distance", "дистанция"),
            ("tick time", "время обновления"),
            ("character encounter", "сценарий появления персонажей"),
            ("npc encounter", "сценарий появления NPC"),
            ("encounter", "событие"),
            ("horde", "орда"),
            ("npc", "NPC"),
            ("civilian", "гражданские"),
            ("military", "военные"),
            ("hospital", "больница"),
            ("animals", "животные"),
            ("animal", "животные"),
            ("aggressive", "агрессивные"),
            ("non aggressive", "неагрессивные"),
            ("guard", "охранник"),
            ("drifter", "скиталец"),
            ("outpost", "аванпост"),
            ("ltz", "зона низкой угрозы"),
            ("mtz", "зона средней угрозы"),
            ("htz", "зона высокой угрозы"),
            ("poi", "точка интереса"),
            ("factory", "завод"),
            ("medical", "медицинский"),
            ("airfield", "аэродром"),
            ("large", "большой"),
            ("small", "малый"),
            ("armory", "оружейник"),
            ("boat shop", "лодочный торговец"),
            ("barmen", "бармен"),
            ("barber", "парикмахер"),
            ("mechanic", "механик"),
            ("doctor", "доктор"),
            ("severity", "тяжесть эффекта"),
            ("response", "ответ"),
            ("idle", "ожидание"),
            ("initial", "начальный"),
            ("interval", "интервал"),
            ("montages", "анимации"),
            ("montage", "анимация"),
            ("nodes", "узлы"),
            ("node", "узел"),
            ("value", "значение"),
            ("type", "тип"),
            ("min", "минимум"),
            ("max", "максимум"),
            (" vs ", " по "),
            ("no skill", "без навыка"),
            ("basic", "базовый"),
            ("medium", "средний"),
            ("advanced", "продвинутый"),
            ("above advanced", "мастер")
        };

        var value = input;
        foreach (var replacement in replacements.OrderByDescending(x => x.From.Length))
        {
            value = ReplaceSemanticLabelPart(value, replacement.From, replacement.To);
        }

        return value;
    }

    private static string ReplaceSemanticLabelPart(string input, string from, string to)
    {
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(from))
        {
            return input;
        }

        if (!from.Any(char.IsLetterOrDigit))
        {
            return input.Replace(from, to, StringComparison.OrdinalIgnoreCase);
        }

        var pattern = $@"(?<![A-Za-zА-Яа-я0-9]){Regex.Escape(from)}(?![A-Za-zА-Яа-я0-9])";
        return Regex.Replace(input, pattern, to, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string NormalizeLocalizedLabel(string input)
    {
        var value = input
            .Replace(". ", " / ", StringComparison.Ordinal)
            .Replace(".", " / ", StringComparison.Ordinal)
            .Replace(" / минимум", " (минимум)", StringComparison.OrdinalIgnoreCase)
            .Replace(" / максимум", " (максимум)", StringComparison.OrdinalIgnoreCase)
            .Replace(" / базовый", " / базовый уровень", StringComparison.OrdinalIgnoreCase)
            .Replace(" / средний", " / средний уровень", StringComparison.OrdinalIgnoreCase)
            .Replace(" / продвинутый", " / продвинутый уровень", StringComparison.OrdinalIgnoreCase)
            .Replace(" / мастер", " / уровень мастер", StringComparison.OrdinalIgnoreCase)
            .Replace(" #", " ", StringComparison.Ordinal)
            .Replace(" / / ", " / ", StringComparison.Ordinal)
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim();

        value = Regex.Replace(value, @"\s+/\s+", " / ");
        value = Regex.Replace(value, @"\s{2,}", " ");
        return value.Trim(' ', '/', '-');
    }

    private static string CapitalizeFirst(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        return char.ToUpperInvariant(input[0]) + input[1..];
    }

    private static string BuildRootDisplayName(string relativePath)
    {
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        if (string.IsNullOrWhiteSpace(stem))
        {
            return "ассет";
        }

        if (stem.StartsWith("CR_", StringComparison.OrdinalIgnoreCase))
        {
            return $"Рецепт {LocalizeAssetStem(stem[3..])}";
        }

        if (stem.StartsWith("CI_", StringComparison.OrdinalIgnoreCase))
        {
            return $"Ингредиент {LocalizeAssetStem(stem[3..])}";
        }

        return LocalizeAssetStem(stem);
    }

    private static bool TryAddSafeField(
        List<StudioModFieldDto> output,
        string relativePath,
        string fieldPath,
        string sourceLabel,
        string valueType,
        string currentValue,
        string currentDisplayValue)
    {
        var userLabel = ToUserFieldLabel(relativePath, sourceLabel);
        return TryAddSafeFieldResolved(output, relativePath, fieldPath, userLabel, valueType, currentValue, currentDisplayValue);
    }

    private static bool TryAddSafeFieldResolved(
        List<StudioModFieldDto> output,
        string relativePath,
        string fieldPath,
        string userLabel,
        string valueType,
        string currentValue,
        string currentDisplayValue)
    {
        userLabel = ApplyFieldLabelContext(relativePath, fieldPath, userLabel);
        if (!ShouldExposeSafeField(relativePath, userLabel, valueType))
        {
            return false;
        }

        if (relativePath.Contains("/ui/gameevents/itemselection/", StringComparison.OrdinalIgnoreCase)
            && userLabel.Contains("название набора", StringComparison.OrdinalIgnoreCase)
            && LooksLikeOpaqueLocalizationKey(currentValue))
        {
            return false;
        }

        var section = ResolveFieldSection(relativePath, userLabel);
        var description = ResolveFieldDescription(relativePath, userLabel);
        var picker = ResolveFieldReferencePicker(relativePath, userLabel, valueType, currentValue);
        var options = ResolveFieldOptions(relativePath, userLabel, valueType, currentValue);
        var editorKind = ResolveEditorKind(relativePath, userLabel, valueType, options, picker.ReferencePickerKind);
        if (valueType is "object" or "soft-object" or "soft-object-path"
            && editorKind == "asset-ref")
        {
            return false;
        }

                var (min, max) = ResolveSuggestedRange(relativePath, userLabel, valueType);

        output.Add(new StudioModFieldDto(
            fieldPath,
            userLabel,
            description,
            section,
            valueType,
            editorKind,
            currentValue,
            true,
            min,
            max,
            options,
            picker.ReferencePickerKind,
            picker.ReferencePickerPrompt,
            currentDisplayValue));
        return true;
    }

    private static string ApplyFieldLabelContext(string relativePath, string fieldPath, string userLabel)
    {
        if (relativePath.Contains("bp_economymanager", StringComparison.OrdinalIgnoreCase)
            && userLabel.StartsWith("Уровни процветания / ", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(fieldPath, @"^e:\d+/p:(\d+)/p:\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success
                && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var propertyIndex)
                && propertyIndex >= 2
                && propertyIndex <= 6)
            {
                var suffix = userLabel["Уровни процветания / ".Length..];
                return $"Уровни процветания / уровень {propertyIndex - 1} / {suffix}";
            }
        }

        if (relativePath.Contains("/items/weapons/", StringComparison.OrdinalIgnoreCase)
            && string.Equals(userLabel, "Общий шанс неисправности", StringComparison.OrdinalIgnoreCase))
        {
            if (fieldPath.EndsWith("/p:0/p:0", StringComparison.OrdinalIgnoreCase))
            {
                return "Общий шанс неисправности (минимум)";
            }

            if (fieldPath.EndsWith("/p:0/p:1", StringComparison.OrdinalIgnoreCase))
            {
                return "Общий шанс неисправности (максимум)";
            }
        }

        return userLabel;
    }

    private static void TryAddReadOnlyInfoField(
        List<StudioModFieldDto> output,
        string relativePath,
        string fieldPath,
        string sourceLabel,
        string currentValue)
    {
        var userLabel = ToUserFieldLabel(relativePath, sourceLabel);
        if (!ShouldExposeReferenceInfoField(relativePath, userLabel))
        {
            return;
        }

        if (userLabel.Contains("Связанные вещества эффекта", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(currentValue))
        {
            userLabel = $"Связанные вещества эффекта / {currentValue}";
        }

        output.Add(new StudioModFieldDto(
            fieldPath,
            userLabel,
            ResolveReferenceInfoDescription(relativePath, userLabel),
            ResolveFieldSection(relativePath, userLabel),
            "string",
            "text",
            currentValue,
            false,
            null,
            null,
            null));
    }

    private static bool ShouldExposeSafeField(string relativePath, string userLabel, string valueType)
    {
        if (string.IsNullOrWhiteSpace(userLabel))
        {
            return false;
        }

        if (userLabel.Length > 160)
        {
            return false;
        }

        var label = userLabel.ToLowerInvariant();
        var path = relativePath.ToLowerInvariant();

        var technicalNoiseTokens = new[]
        {
            "icon", "ico", "image", "brush", "texture", "material", "mesh", "skeletal",
            "sound", "audio", "anim", "widget", "sprite", "atlas", "thumbnail", "font",
            "padding", "margin", "alignment", "offset", "shadow", "color", "opacity",
            "pre infinity", "post infinity", "extrap", "default значение", "default value"
        };

        if (technicalNoiseTokens.Any(token => label.Contains(token, StringComparison.Ordinal)))
        {
            return false;
        }

        var hardBlockedTechnicalTokens = new[]
        {
            "draw distance", "дистанция отображения", "ignored by spawners", "игнорируется спавнерами",
            "не использовать для спавна в мире", "socket", "сокет",
            "mesh slice", "части 3d-модели", "override materials", "переопределение материалов",
            "asset user data", "query token stream", "token stream version",
            "net update frequency", "частота обновления сети",
            "служебный поток условий", "служебная версия потока условий"
        };

        if (hardBlockedTechnicalTokens.Any(token => label.Contains(token, StringComparison.Ordinal)))
        {
            return false;
        }

        if (path.Contains("/items/crafting/recipes/", StringComparison.Ordinal))
        {
            var blockedCraftTokens = new[]
            {
                "ручное название варианта", "название варианта", "audio", "sound", "caption"
            };
            if (blockedCraftTokens.Any(token => label.Contains(token, StringComparison.Ordinal)))
            {
                return false;
            }

            var allowedCraftTokens = new[]
            {
                "что создаётся", "ингредиент", "подходящий предмет", "варианты ингредиента",
                "время", "опыт за крафт", "очки славы за крафт", "дополнительный расход",
                "расход за крафт", "влияние на качество результата", "роль ингредиента",
                "как можно смешивать варианты", "возврат при разборе", "повреждение при разборе",
                "тратить предмет целиком", "это жидкость или ресурс", "литры", "питательност",
                "когда начинается эта ступень", "насколько сильно действует эта ступень"
            };

            return allowedCraftTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (path.Contains("/data/spawnequipment/", StringComparison.Ordinal))
        {
            return label.Contains("выдаваемый предмет", StringComparison.Ordinal);
        }

        if (path.Contains("/characters/spawnerpresets/fishspeciespresets/", StringComparison.Ordinal))
        {
            var allowedFishTokens = new[]
            {
                "вид рыбы", "вес появления"
            };

            return allowedFishTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (path.Contains("/foliage/farming/", StringComparison.Ordinal))
        {
            var allowedPlantTokens = new[]
            {
                "название растения", "название вредителя", "название болезни",
                "пакет семян",
                "лучшая температура для семян", "лучшая температура роста",
                "сколько часов длится одна стадия роста", "финальная стадия роста",
                "сколько живёт финальная стадия", "когда финальная стадия начинает портиться",
                "штраф к урожаю"
            };

            return allowedPlantTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (IsCargoDropContainerAsset(relativePath))
        {
            var allowedCargoTokens = new[]
            {
                "время до взрыва после приземления",
                "основные пресеты лута",
                "основные обычные пресеты лута",
                "контейнерные наборы лута",
                "основные расширенные пресеты лута"
            };

            return allowedCargoTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (path.Contains("/ui/gameevents/itemselection/", StringComparison.Ordinal))
        {
            var allowedItemSelectionTokens = new[]
            {
                "главный предмет", "название набора", "номер команды события"
            };

            return allowedItemSelectionTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (path.Contains("/items/spawnerpresets/examine_data_presets/", StringComparison.Ordinal))
        {
            var allowedSpawnerTokens = new[]
            {
                "всегда выдавать", "шанс появления набора", "учитывать игровую зону предмета",
                "учитывать редкость предмета", "учитывать группу появления предмета",
                "начальная повреждённость", "разброс повреждённости",
                "начальный ресурс или заряд", "разброс ресурса или заряда",
                "начальная грязь", "разброс грязи",
                "минимум патронов", "максимум патронов",
                "минимум в стопке", "максимум в стопке"
            };

            return allowedSpawnerTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (IsAdvancedItemSpawnerPresetAsset(relativePath))
        {
            var allowedAdvancedSpawnerTokens = new[]
            {
                "всегда выдавать", "шанс появления набора",
                "сколько предметов выдавать", "разрешать повторы предметов", "учитывать игровую зону предмета",
                "готовый подпакет", "редкость набора",
                "начальная повреждённость", "разброс повреждённости",
                "начальный ресурс или заряд", "разброс ресурса или заряда"
            };

            return allowedAdvancedSpawnerTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (IsRegularItemSpawnerPresetAsset(relativePath))
        {
            var allowedSpawnerTokens = new[]
            {
                "всегда создавать предмет", "шанс появления предмета", "учитывать игровую зону предмета",
                "учитывать редкость предмета", "учитывать группу появления предмета",
                "начальная повреждённость", "разброс повреждённости",
                "начальный ресурс или заряд", "разброс ресурса или заряда",
                "начальная грязь", "разброс грязи",
                "минимум патронов", "максимум патронов",
                "минимум в стопке", "максимум в стопке",
                "подправлять место появления по столкновению", "подправлять поворот предмета по столкновению"
            };

            return allowedSpawnerTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (path.Contains("/data/tables/items/spawning/", StringComparison.Ordinal))
        {
            var allowedItemSpawningTokens = new[]
            {
                "максимум копий в мире", "разрешённые зоны", "откат на одного бойца", "группа отката",
                "подчиняться серверному правилу ловушек", "варианты предмета",
                "переопределять ресурс предмета", "начальный ресурс", "случайная добавка к ресурсу",
                "случайный разброс поворота", "учитывать более низкие группы", "время отката",
                "побережье", "континент", "горы", "город", "сельская местность", "промзона",
                "полиция", "военные (база)", "военные (средний уровень)", "военные (продвинутый уровень)",
                "военные (ww2)", "спорт", "рынок", "заправка", "аэродром"
            };

            return allowedItemSpawningTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (IsGameEventMarkerAsset(relativePath))
        {
            var allowedGameEventTokens = new[]
            {
                "минимум участников", "максимум участников", "participant", "participants", "огонь по своим", "плата за вход",
                "очки за убийство врага", "штраф за убийство союзника", "штраф за смерть", "штраф за самоубийство",
                "очки за помощь", "очки за выстрел в голову", "очки за победу в раунде",
                "очки славы за участие", "перевод очков в славу",
                "длительность раунда", "лимит раундов", "лимит побед", "задержка возрождения",
                "разрешить возрождение", "время ожидания события", "лимит игроков по командам",
                "пропустить фазу ключа", "очки за захват флага"
            };

            return allowedGameEventTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (path.Contains("/items/weapons/attachmentsockets/", StringComparison.Ordinal)
            || path.Contains("/items/weapons/attachments/", StringComparison.Ordinal)
            || path.Contains("/items/weapons/weapon_parts/", StringComparison.Ordinal)
            || path.Contains("/items/weapons/malfunctions/", StringComparison.Ordinal))
        {
            return false;
        }

        if (path.Contains("/items/weapons/weapon_clips/", StringComparison.Ordinal))
        {
            var allowedWeaponClipTokens = new[]
            {
                "патронов в магазине", "патронов для события", "патрон по умолчанию",
                "вес", "ширина в инвентаре", "высота в инвентаре"
            };

            return allowedWeaponClipTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (path.Contains("/items/weapons/ranged_weapons/", StringComparison.Ordinal))
        {
            var allowedRangedWeaponTokens = new[]
            {
                "урон за выстрел", "патронов в магазине", "патронов для события", "патрон по умолчанию",
                "сила отдачи", "сила отдачи камеры", "максимальная отдача", "скорость возврата после отдачи",
                "максимальная дальность", "скорострельность", "вес", "ширина в инвентаре", "высота в инвентаре",
                "общий шанс неисправности", "использовать свой шанс неисправности",
                "уровень шума", "урон со временем"
            };

            return allowedRangedWeaponTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (path.Contains("/items/weapons/", StringComparison.Ordinal))
        {
            var allowedWeaponTokens = new[]
            {
                "урон за выстрел", "патронов в магазине", "патронов для события", "патрон по умолчанию",
                "вес", "ширина в инвентаре", "высота в инвентаре",
                "общий шанс неисправности", "использовать свой шанс неисправности"
            };

            return allowedWeaponTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (path.Contains("/skills/", StringComparison.Ordinal))
        {
            var allowedSkillTokens = new[]
            {
                "опыт за минуту", "опыт за ", "награда опытом",
                "расход энергии", "расход воды", "восстановление выносливости",
                "значение на минимальном опыте", "значение на максимальном опыте",
                "параметры без навыка", "параметры базового уровня", "параметры среднего уровня",
                "параметры продвинутого уровня", "уровень мастер",
                "дистанция ремонта", "время ремонта", "расход инструмента",
                "время заправки", "время слива топлива", "время зарядки аккумулятора",
                "время запуска с толкача", "максимальная скорость", "сила разгона",
                "скорость поворота колёс", "параметры двигателя", "время зажигания",
                "шанс сбоя зажигания", "параметры коробки передач", "задержка переключения передачи",
                "порог переключения вверх", "порог переключения вниз",
                "срыв двигателя", "шанс заглушить двигатель", "частота рывков",
                "ручного улучшения bcu", "время взятия крови",
                "когда начинается эта ступень", "насколько сильно действует эта ступень"
            };

            return allowedSkillTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (path.Contains("/fortifications/locks/", StringComparison.Ordinal))
        {
            var allowedLockTokens = new[]
            {
                "время", "число попыток обезвреживания", "требуются очки славы"
            };

            return allowedLockTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (path.Contains("/quests/", StringComparison.Ordinal))
        {
            var allowedQuestTokens = new[]
            {
                "выполненных квестов нужно", "источников квестов", "квестов выдавать за день",
                "квестов готово сразу", "доступных квестов", "шанс квестов по уровню",
                "чужих выполненных квестов", "авто-завершение", "порядок шага",
                "нужно держать рядом", "оставшихся использований", "запускать шаг автоматически",
                "засчитывать похожие предметы рядом", "засчитывать похожие предметы по классу",
                "подходящие предметы", "награда", "варианты награды", "опыт навыков", "условия по тегам", " / квесты",
                "подсказка условия", "автоописание условия"
            };

            return allowedQuestTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (valueType == "byte" && (label.Contains("нижняя граница / тип", StringComparison.Ordinal)
            || label.Contains("верхняя граница / тип", StringComparison.Ordinal)
            || label.EndsWith(" / тип", StringComparison.Ordinal)))
        {
            return false;
        }

        if (label.Contains("tag name", StringComparison.Ordinal)
            || label.Contains("внутренний тег", StringComparison.Ordinal)
            || label.Contains("тег группы спавна транспорта", StringComparison.Ordinal)
            || label.Contains("тип пресета npc", StringComparison.Ordinal))
        {
            return false;
        }

        if (label.Contains(" / тип", StringComparison.Ordinal) && valueType is "byte" or "enum")
        {
            return false;
        }

        if (valueType is "string" or "text" or "name" or "enum")
        {
            var semanticStringTokens = new[]
            {
                "id", "tag", "state", "type", "category", "purpose", "group", "effect", "condition", "title",
                "тип", "катег", "группа", "эффект", "состояни", "название", "разреш"
            };

            if (!semanticStringTokens.Any(token => label.Contains(token, StringComparison.Ordinal)))
            {
                return false;
            }
        }

        if (valueType is "soft-object" or "soft-object-path" or "object")
        {
            var safeReferenceTokens = new[]
            {
                "item class", "default ammunition", "reward item", "spawn item",
                "выдаваемый предмет", "предмет", "патрон", "награда", "добыча", "что создаётся", "результат",
                "навык", "вещество", "симптом", "побочный эффект", "источник эффекта",
                "событие", "класс события", "класс орды", "десантного корабля"
            };

            if (!safeReferenceTokens.Any(token => label.Contains(token, StringComparison.Ordinal)))
            {
                return false;
            }
        }

        if (path.Contains("/ui/", StringComparison.OrdinalIgnoreCase) && valueType is "string" or "text" or "name")
        {
            return false;
        }

        return true;
    }

    private static bool ShouldExposeReferenceInfoField(string relativePath, string userLabel)
    {
        if (string.IsNullOrWhiteSpace(userLabel))
        {
            return false;
        }

        var label = userLabel.ToLowerInvariant();
        var path = relativePath.ToLowerInvariant();
        if (path.Contains("/skills/", StringComparison.Ordinal)
            && (label.Contains("curve", StringComparison.Ordinal)
                || label.Contains("кривая", StringComparison.Ordinal)))
        {
            return false;
        }

        if (label.Contains("ui data", StringComparison.Ordinal)
            || label.Contains("данные интерфейса", StringComparison.Ordinal)
            || path.Contains("/ui/", StringComparison.Ordinal))
        {
            return false;
        }

        var usefulTokens = new[]
        {
            "симптом", "вещество", "вещества", "навык", "предмет", "предметы", "патрон", "награда", "награды"
        };

        return usefulTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
    }

    private static bool ShouldExposeListTarget(string relativePath, string userLabel)
    {
        var label = userLabel.ToLowerInvariant();
        var path = relativePath.ToLowerInvariant();

        var blockedTokens = new[]
        {
            "tag dictionary", "query token", "asset user data", "root nodes", "all nodes",
            "action sequence", "attachment sockets", "supported firing modes", "items",
            "mesh slices", "socket overrides", "override materials", "сокет", "сокеты",
            "переопределение материалов", "переопределение сокетов", "части 3d-модели", "части 3d модели"
        };

        var allowQuestItemTargets = path.Contains("/quests/", StringComparison.Ordinal)
            && (label.Contains("нужно держать рядом", StringComparison.Ordinal)
                || label.Contains("подходящие предметы", StringComparison.Ordinal)
                || (label.Contains("item экипировать", StringComparison.Ordinal) && label.Contains("items", StringComparison.Ordinal)));
        if (!allowQuestItemTargets && blockedTokens.Any(token => label.Contains(token, StringComparison.Ordinal)))
        {
            return false;
        }

        if (path.Contains("/data/spawnequipment/", StringComparison.Ordinal))
        {
            return false;
        }

        if (IsCargoDropContainerAsset(relativePath))
        {
            return IsCargoDropMajorSpawnerOptionsSurface(relativePath, userLabel)
                || IsCargoDropMajorSpawnerPresetOptionsSurface(relativePath, userLabel);
        }

        if (IsCargoDropWorldEventAsset(relativePath))
        {
            return IsCargoDropEncounterVariantsSurface(relativePath, userLabel);
        }

        if (path.Contains("/items/spawnerpresets/examine_data_presets/", StringComparison.Ordinal))
        {
            return IsExamineDataPresetItemListSurface(relativePath, userLabel);
        }

        if (IsCargoDropPackagePresetAsset(relativePath))
        {
            return IsAdvancedItemSpawnerPresetItemListSurface(relativePath, userLabel);
        }

        if (IsAdvancedItemSpawnerPresetAsset(relativePath))
        {
            return IsAdvancedItemSpawnerPresetItemListSurface(relativePath, userLabel)
                || IsAdvancedItemSpawnerPresetSubpresetListSurface(relativePath, userLabel);
        }

        if (IsRegularItemSpawnerPresetAsset(relativePath))
        {
            return IsRegularItemSpawnerPresetItemListSurface(relativePath, userLabel);
        }

        if (path.Contains("/data/tables/items/spawning/", StringComparison.Ordinal))
        {
            return IsItemSpawningVariationListSurface(relativePath, userLabel);
        }

        if (path.Contains("/ui/gameevents/itemselection/", StringComparison.Ordinal))
        {
            return label.Contains("дополнительные предметы", StringComparison.Ordinal)
                || label.Contains("крепления", StringComparison.Ordinal);
        }

        if (IsStandaloneGameplayCurvePointsSurface(relativePath, userLabel))
        {
            return true;
        }

        if (IsGameEventMarkerAsset(relativePath))
        {
            return IsGameEventLoadoutListLabel(userLabel);
        }

        if (IsVehicleSpawnGroupAsset(relativePath))
        {
            return IsVehicleSpawnPresetListSurface(relativePath, userLabel);
        }

        if (path.Contains("/items/weapons/", StringComparison.Ordinal))
        {
            return false;
        }

        if (path.Contains("/skills/", StringComparison.Ordinal))
        {
            return label.Contains("точки кривой", StringComparison.Ordinal);
        }

        if (path.Contains("/quests/", StringComparison.Ordinal))
        {
            var blockedQuestTokens = new[]
            {
                "служебный поток условий", "query token stream",
                "служебная версия потока условий", "token stream version"
            };
            if (blockedQuestTokens.Any(token => label.Contains(token, StringComparison.Ordinal)))
            {
                return false;
            }

            if (IsQuestRewardVariantsLabel(relativePath, userLabel)
                || IsQuestRewardSkillExperienceLabel(relativePath, userLabel))
            {
                return true;
            }

            var allowedQuestTokens = new[]
            {
                "настройки источников квестов", "quest givers setup",
                "специальные наборы квестов", "special quest sets",
                "варианты награды", "possible rewards",
                "опыт навыков за квест", "reward skill experience",
                "нужно держать рядом", "must be in vicinity",
                "подходящие предметы", "accepted items"
            };

            return allowedQuestTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (path.Contains("/bodyeffects/", StringComparison.Ordinal)
            || path.Contains("/metabolism/", StringComparison.Ordinal))
        {
            var allowedBodyEffectTokens = new[]
            {
                "побочные эффекты", "вещество", "вещества", "симптом", "анимац", "точки кривой"
            };

            return allowedBodyEffectTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        if (path.Contains("/characters/spawnerpresets/fishspeciespresets/", StringComparison.Ordinal))
        {
            return label.Contains("виды рыбы", StringComparison.Ordinal);
        }

        if (path.Contains("/foliage/farming/", StringComparison.Ordinal))
        {
            return IsPlantSpeciesListSurface(relativePath, userLabel)
                || IsPlantPestListSurface(relativePath, userLabel)
                || IsPlantDiseaseListSurface(relativePath, userLabel);
        }

        if (path.Contains("/items/crafting/", StringComparison.Ordinal))
        {
            return true;
        }

        if (path.Contains("/encounters/", StringComparison.Ordinal)
            || path.Contains("/npcs/", StringComparison.Ordinal))
        {
            var allowedEncounterTokens = new[]
            {
                "классы персонажей", "персонажи для события", "состав пресета npc", "лимит конкретных npc", "точки появления"
            };

            return allowedEncounterTokens.Any(token => label.Contains(token, StringComparison.Ordinal));
        }

        return false;
    }

    private static bool IsStandaloneGameplayCurvePointsSurface(string relativePath, string userLabel)
    {
        if (!userLabel.Contains("точки кривой", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = relativePath.ToLowerInvariant();
        return path.Contains("/encounters/spawn_amount_curves/", StringComparison.Ordinal)
            || path.Contains("/cooking/data/curves/", StringComparison.Ordinal)
            || path.Contains("/data/weapon/malfunctionprobabilitycurves/", StringComparison.Ordinal)
            || path.Contains("/data/batteryeffectscurves/", StringComparison.Ordinal)
            || path.Contains("/curves/falling/", StringComparison.Ordinal)
            || path.Contains("/curves/landing/", StringComparison.Ordinal)
            || path.Contains("/minigames/lockpicking/", StringComparison.Ordinal)
            || path.Contains("/basebuilding/energydamagecurves/", StringComparison.Ordinal)
            || path.Contains("/characters/mechanoids/sentry/curves/", StringComparison.Ordinal)
            || path.Contains("/characters/zombies2/data/observingcurve", StringComparison.Ordinal)
            || path.Contains("/vehicles/", StringComparison.Ordinal);
    }

    private static bool IsQuestRewardVariantsLabel(string relativePath, string userLabel)
    {
        if (!relativePath.Contains("/quests/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var label = userLabel.ToLowerInvariant();
        var normalized = NormalizeAssetKey(userLabel);
        return label.Contains("варианты награды", StringComparison.Ordinal)
            || label.Contains("варианты наград", StringComparison.Ordinal)
            || label.Contains("possible rewards", StringComparison.Ordinal)
            || label.Contains("possible награды", StringComparison.Ordinal)
            || normalized.Contains("possiblerewards", StringComparison.Ordinal);
    }

    private static bool IsQuestRewardSkillExperienceLabel(string relativePath, string userLabel)
    {
        if (!relativePath.Contains("/quests/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var label = userLabel.ToLowerInvariant();
        var normalized = NormalizeAssetKey(userLabel);
        return label.Contains("опыт навыков", StringComparison.Ordinal)
            || label.Contains("skill experience", StringComparison.Ordinal)
            || normalized.Contains("rewardskillexperience", StringComparison.Ordinal)
            || normalized.Contains("skillexperience", StringComparison.Ordinal);
    }

    private static string ResolveFieldSection(string relativePath, string userLabel)
    {
        var label = userLabel.ToLowerInvariant();
        var path = relativePath.ToLowerInvariant();

        if (IsGameEventMarkerAsset(relativePath))
        {
            if (label.Contains("название события", StringComparison.Ordinal)
                || label.Contains("описание события", StringComparison.Ordinal)
                || label.Contains("подсказка режима", StringComparison.Ordinal)
                || label.Contains("подсказка по оружию", StringComparison.Ordinal)
                || label.Contains("подсказка по наградам", StringComparison.Ordinal))
            {
                return "Тексты события";
            }

            return "Правила события";
        }

        if (IsVehicleSpawnGroupAsset(relativePath)
            && IsVehicleSpawnPresetListSurface(relativePath, userLabel))
        {
            return "Группа спавна транспорта";
        }

        if (label.Contains("когда начинается эта ступень", StringComparison.Ordinal)
            || label.Contains("насколько сильно действует эта ступень", StringComparison.Ordinal)
            || label.Contains("положение точки кривой", StringComparison.Ordinal)
            || label.Contains("значение точки кривой", StringComparison.Ordinal))
        {
            if (path.Contains("/encounters/spawn_amount_curves/", StringComparison.Ordinal))
            {
                return "Кривые спавна";
            }

            if (path.Contains("/cooking/data/curves/", StringComparison.Ordinal))
            {
                return "Приготовление";
            }

            if (path.Contains("/minigames/lockpicking/", StringComparison.Ordinal))
            {
                return "Взлом";
            }

            if (path.Contains("/data/weapon/malfunctionprobabilitycurves/", StringComparison.Ordinal))
            {
                return "Отказы оружия";
            }

            if (path.Contains("/data/batteryeffectscurves/", StringComparison.Ordinal))
            {
                return "Батареи и фонари";
            }

            if (path.Contains("/curves/falling/", StringComparison.Ordinal)
                || path.Contains("/curves/landing/", StringComparison.Ordinal))
            {
                return "Падение и приземление";
            }

            if (path.Contains("/basebuilding/energydamagecurves/", StringComparison.Ordinal))
            {
                return "База и энергия";
            }

            if (path.Contains("/characters/mechanoids/sentry/curves/", StringComparison.Ordinal))
            {
                return "Турели и механоиды";
            }

            if (path.Contains("/characters/zombies2/data/observingcurve", StringComparison.Ordinal))
            {
                return "Восприятие зомби";
            }

            if (path.Contains("/vehicles/", StringComparison.Ordinal))
            {
                return "Транспорт";
            }

            if (path.Contains("/skills/", StringComparison.Ordinal))
            {
                return "Кривая навыка";
            }

            return "Кривые";
        }

        if (label.Contains("что создаётся", StringComparison.Ordinal))
        {
            return "Результат";
        }

        if (label.Contains("подходящие поверхности", StringComparison.Ordinal))
        {
            return "Точки появления";
        }

        if (label.Contains("ингредиент", StringComparison.Ordinal)
            || label.Contains("подходящ", StringComparison.Ordinal)
            || label.Contains("расход за крафт", StringComparison.Ordinal)
            || label.Contains("дополнительный расход", StringComparison.Ordinal)
            || label.Contains("литры", StringComparison.Ordinal))
        {
            return "Ингредиенты";
        }

        if (label.Contains("роль ингредиента", StringComparison.Ordinal)
            || label.Contains("смешивать варианты", StringComparison.Ordinal)
            || label.Contains("это жидкость или ресурс", StringComparison.Ordinal))
        {
            return "Правила рецепта";
        }

        if (label.Contains("повреждение при разборе", StringComparison.Ordinal)
            || label.Contains("возврат при разборе", StringComparison.Ordinal))
        {
            return "Разборка";
        }

        if (label.Contains("анимац", StringComparison.Ordinal))
        {
            return "Анимации";
        }

        if (IsCargoDropContainerAsset(relativePath))
        {
            if (label.Contains("время до взрыва после приземления", StringComparison.Ordinal))
            {
                return "Таймер контейнера";
            }

            if (label.Contains("основные пресеты лута", StringComparison.Ordinal)
                || label.Contains("основные обычные пресеты лута", StringComparison.Ordinal)
                || label.Contains("контейнерные наборы лута", StringComparison.Ordinal)
                || label.Contains("основные расширенные пресеты лута", StringComparison.Ordinal)
                )
            {
                return "Основной лут";
            }
        }

        if (label.Contains("вес выбора", StringComparison.Ordinal)
            || label.Contains("классы персонажей", StringComparison.Ordinal)
            || label.Contains("персонажи для события", StringComparison.Ordinal)
            || label.Contains("какое событие может запуститься", StringComparison.Ordinal)
            || label.Contains("задержку перед запуском", StringComparison.Ordinal))
        {
            return "Состав события";
        }

        if (label.Contains("точки появления", StringComparison.Ordinal))
        {
            return "Точки появления";
        }

        if (label.Contains("порог процветания", StringComparison.Ordinal)
            || label.Contains("обновление gbc", StringComparison.Ordinal)
            || label.Contains("обновление gsc", StringComparison.Ordinal))
        {
            return "Уровни торговли";
        }

        if (path.Contains("/foliage/farming/", StringComparison.OrdinalIgnoreCase))
        {
            if (label.Contains("пакет семян", StringComparison.Ordinal))
            {
                return "Посадка";
            }

            if (label.Contains("виды растений", StringComparison.Ordinal))
            {
                return "Список растений";
            }

            if (label.Contains("температура", StringComparison.Ordinal)
                || label.Contains("стадия", StringComparison.Ordinal)
                || label.Contains("рост", StringComparison.Ordinal))
            {
                return "Рост растения";
            }

            if (label.Contains("вредител", StringComparison.Ordinal)
                || label.Contains("болезни", StringComparison.Ordinal)
                || label.Contains("болезн", StringComparison.Ordinal))
            {
                return "Опасности";
            }

            if (label.Contains("урож", StringComparison.Ordinal))
            {
                return "Урожай";
            }

            if (label.Contains("название", StringComparison.Ordinal))
            {
                return "Описание";
            }
        }

        if (path.Contains("/data/tables/items/spawning/", StringComparison.OrdinalIgnoreCase))
        {
            if (label.Contains("переопределять ресурс предмета", StringComparison.Ordinal)
                || label.Contains("начальный ресурс", StringComparison.Ordinal)
                || label.Contains("случайная добавка к ресурсу", StringComparison.Ordinal))
            {
                return "Состояние появления";
            }

            if (label.Contains("разрешённые зоны", StringComparison.Ordinal)
                || label.Contains("побережье", StringComparison.Ordinal)
                || label.Contains("континент", StringComparison.Ordinal)
                || label.Contains("горы", StringComparison.Ordinal)
                || label.Contains("город", StringComparison.Ordinal)
                || label.Contains("сельская местность", StringComparison.Ordinal)
                || label.Contains("промзона", StringComparison.Ordinal)
                || label.Contains("полиция", StringComparison.Ordinal)
                || label.Contains("военные", StringComparison.Ordinal)
                || label.Contains("спорт", StringComparison.Ordinal)
                || label.Contains("рынок", StringComparison.Ordinal)
                || label.Contains("заправка", StringComparison.Ordinal)
                || label.Contains("аэродром", StringComparison.Ordinal))
            {
                return "Зоны появления";
            }
        }

        if (path.Contains("/items/spawnerpresets/examine_data_presets/", StringComparison.OrdinalIgnoreCase))
        {
            if (IsExamineDataPresetItemListSurface(relativePath, userLabel))
            {
                return "Состав набора";
            }

            if (label.Contains("патрон", StringComparison.Ordinal)
                || label.Contains("стопке", StringComparison.Ordinal))
            {
                return "Количество выдачи";
            }

            if (label.Contains("поврежд", StringComparison.Ordinal)
                || label.Contains("ресурс", StringComparison.Ordinal)
                || label.Contains("заряд", StringComparison.Ordinal)
                || label.Contains("гряз", StringComparison.Ordinal))
            {
                return "Состояние предметов";
            }

            return "Правила появления";
        }

        if (IsAdvancedItemSpawnerPresetAsset(relativePath))
        {
            if (IsAdvancedItemSpawnerPresetSubpresetListSurface(relativePath, userLabel)
                || label.Contains("готовый подпакет", StringComparison.Ordinal)
                || label.Contains("редкость набора", StringComparison.Ordinal))
            {
                return "Подпакеты лута";
            }

            if (IsCargoDropPackagePresetAsset(relativePath)
                && IsAdvancedItemSpawnerPresetItemListSurface(relativePath, userLabel))
            {
                return "Состав набора";
            }

            if (label.Contains("сколько предметов выдавать", StringComparison.Ordinal))
            {
                return "Количество выдачи";
            }

            if (label.Contains("поврежд", StringComparison.Ordinal)
                || label.Contains("ресурс", StringComparison.Ordinal)
                || label.Contains("заряд", StringComparison.Ordinal))
            {
                return "Состояние предметов";
            }

            return "Правила набора";
        }

        if (IsRegularItemSpawnerPresetAsset(relativePath))
        {
            if (IsRegularItemSpawnerPresetItemListSurface(relativePath, userLabel))
            {
                return "Состав пресета";
            }

            if (label.Contains("патрон", StringComparison.Ordinal)
                || label.Contains("стопке", StringComparison.Ordinal))
            {
                return "Количество";
            }

            if (label.Contains("поврежд", StringComparison.Ordinal)
                || label.Contains("ресурс", StringComparison.Ordinal)
                || label.Contains("заряд", StringComparison.Ordinal)
                || label.Contains("гряз", StringComparison.Ordinal))
            {
                return "Состояние предмета";
            }

            return "Правила появления";
        }

        if (path.Contains("/data/tables/items/spawning/", StringComparison.Ordinal))
        {
            if (label.Contains("варианты предмета", StringComparison.Ordinal))
            {
                return "Варианты предмета";
            }

            if (label.Contains("разрешённые зоны", StringComparison.Ordinal)
                || label.Contains("побережье", StringComparison.Ordinal)
                || label.Contains("континент", StringComparison.Ordinal)
                || label.Contains("горы", StringComparison.Ordinal)
                || label.Contains("город", StringComparison.Ordinal)
                || label.Contains("сельская местность", StringComparison.Ordinal)
                || label.Contains("промзона", StringComparison.Ordinal)
                || label.Contains("полиция", StringComparison.Ordinal)
                || label.Contains("военные", StringComparison.Ordinal)
                || label.Contains("спорт", StringComparison.Ordinal)
                || label.Contains("рынок", StringComparison.Ordinal)
                || label.Contains("заправка", StringComparison.Ordinal)
                || label.Contains("аэродром", StringComparison.Ordinal))
            {
                return "Зоны появления";
            }

            if (label.Contains("откат", StringComparison.Ordinal)
                || label.Contains("группа отката", StringComparison.Ordinal))
            {
                return "Откат появления";
            }

            if (label.Contains("ресурс", StringComparison.Ordinal)
                || label.Contains("ловуш", StringComparison.Ordinal))
            {
                return "Состояние предмета";
            }

            return "Правила появления";
        }

        if (path.Contains("/ui/gameevents/itemselection/", StringComparison.OrdinalIgnoreCase))
        {
            if (label.Contains("главный предмет", StringComparison.Ordinal))
            {
                return "Снаряжение события";
            }

            if (label.Contains("номер команды события", StringComparison.Ordinal))
            {
                return "Описание";
            }

            if (label.Contains("название набора", StringComparison.Ordinal))
            {
                return "Описание";
            }
        }

        if (label.Contains("симптом", StringComparison.Ordinal))
        {
            return "Симптомы";
        }

        if ((path.Contains("/bodyeffects/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/metabolism/", StringComparison.OrdinalIgnoreCase))
            && (label.Contains("прямое изменение", StringComparison.Ordinal)
                || label.Contains("прибавка к", StringComparison.Ordinal)
                || label.Contains("влияние на силу", StringComparison.Ordinal)
                || label.Contains("влияние на интеллект", StringComparison.Ordinal)
                || label.Contains("влияние на телосложение", StringComparison.Ordinal)
                || label.Contains("влияние на ловкость", StringComparison.Ordinal)
                || label.Contains("влияние на выносливость", StringComparison.Ordinal)
                || label.Contains("влияние на максимальную выносливость", StringComparison.Ordinal)
                || label.Contains("влияние на результативность", StringComparison.Ordinal)
                || label.Contains("влияние на успешность действий", StringComparison.Ordinal)
                || label.Contains("множитель силы", StringComparison.Ordinal)
                || label.Contains("множитель интеллекта", StringComparison.Ordinal)
                || label.Contains("множитель телосложения", StringComparison.Ordinal)
                || label.Contains("множитель ловкости", StringComparison.Ordinal)
                || label.Contains("множитель выносливости", StringComparison.Ordinal)
                || label.Contains("множитель максимальной выносливости", StringComparison.Ordinal)
                || label.Contains("множитель результативности", StringComparison.Ordinal)
                || label.Contains("множитель успешности действий", StringComparison.Ordinal)))
        {
            return "Влияние на характеристики";
        }

        if ((path.Contains("/bodyeffects/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/metabolism/", StringComparison.OrdinalIgnoreCase))
            && (label.Contains("изменение силы", StringComparison.Ordinal)
                || label.Contains("изменение интеллекта", StringComparison.Ordinal)
                || label.Contains("изменение телосложения", StringComparison.Ordinal)
                || label.Contains("изменение ловкости", StringComparison.Ordinal)
                || label.Contains("изменение выносливости", StringComparison.Ordinal)
                || label.Contains("максимальная выносливость", StringComparison.Ordinal)
                || label.Contains("результативность", StringComparison.Ordinal)
                || label.Contains("успешность действий", StringComparison.Ordinal)
                || label.Contains("скорость движения по земле", StringComparison.Ordinal)
                || label.Contains("скорость плавания", StringComparison.Ordinal)
                || label.Contains("периодический приступ", StringComparison.Ordinal)
                || label.Contains("урон", StringComparison.Ordinal)
                || label.Contains("двоение в глазах", StringComparison.Ordinal)
                || label.Contains("дезориентация", StringComparison.Ordinal)))
        {
            return "Побочные эффекты";
        }

        if (label.Contains("общая защита части тела", StringComparison.Ordinal)
            || label.Contains("защита от режущего ближнего урона", StringComparison.Ordinal)
            || label.Contains("защита от тупого ближнего урона", StringComparison.Ordinal))
        {
            return "Защита";
        }

        if (label.Contains("вещество", StringComparison.Ordinal)
            || label.Contains("вещества", StringComparison.Ordinal))
        {
            return "Источники эффекта";
        }

        if (label.Contains("тип торговца", StringComparison.Ordinal))
        {
            return "Торговец";
        }

        if (label.Contains("уровни процветания", StringComparison.Ordinal)
            || label.Contains("порог наличных для уровня", StringComparison.Ordinal)
            || label.Contains("порог золота для уровня", StringComparison.Ordinal))
        {
            return "Процветание";
        }

        if (label.Contains("дорогих товаров", StringComparison.Ordinal))
        {
            return "Редкие товары";
        }

        if (label.Contains("состав пресета npc", StringComparison.Ordinal)
            || label.Contains("вес в пресете", StringComparison.Ordinal)
            || label.Contains("тип пресета npc", StringComparison.Ordinal))
        {
            return "Пресет NPC";
        }

        if (label.Contains("сколько персонажей может появиться", StringComparison.Ordinal)
            || label.Contains("добавка к числу npc за игрока", StringComparison.Ordinal)
            || label.Contains("лимит npc на игрока", StringComparison.Ordinal)
            || label.Contains("сколько персонажей возвращать за один цикл", StringComparison.Ordinal)
            || label.Contains("размер волны повторного появления npc", StringComparison.Ordinal)
            || label.Contains("размер группы npc", StringComparison.Ordinal)
            || label.Contains("минимальная дистанция между персонажами", StringComparison.Ordinal)
            || label.Contains("минимальная дистанция между npc", StringComparison.Ordinal)
            || label.Contains("минимум персонажей для этого режима", StringComparison.Ordinal))
        {
            return "Количество NPC";
        }

        if (label.Contains("время повторного появления npc", StringComparison.Ordinal))
        {
            return "Повторное появление";
        }

        if (label.Contains("появлять npc группой", StringComparison.Ordinal))
        {
            return "Появление групп";
        }

        if (label.Contains("дистанция скрытия вне поля зрения", StringComparison.Ordinal)
            || label.Contains("дистанция полного удаления", StringComparison.Ordinal)
            || label.Contains("радиус действия события", StringComparison.Ordinal)
            || label.Contains("виртуальный режим", StringComparison.Ordinal)
            || label.Contains("виртуализацию npc", StringComparison.Ordinal)
            || label.Contains("удаления после виртуализации", StringComparison.Ordinal))
        {
            return "Дальность и очистка";
        }

        if (label.Contains("шанс орды", StringComparison.Ordinal)
            || label.Contains("проверками шума", StringComparison.Ordinal)
            || label.Contains("типу шума", StringComparison.Ordinal))
        {
            return "Запуск события";
        }

        if (label.Contains("защищать новичков", StringComparison.Ordinal)
            || label.Contains("игнорировать общий лимит персонажей", StringComparison.Ordinal)
            || label.Contains("не учитывать чужих и внешних персонажей", StringComparison.Ordinal))
        {
            return "Ограничения";
        }

        if (label.Contains("телосложение", StringComparison.Ordinal)
            && label.Contains("вещества", StringComparison.Ordinal))
        {
            return "Всасывание";
        }

        if (label.Contains("тип воздействия", StringComparison.Ordinal))
        {
            return "Тип вещества";
        }

        if (label.Contains("всасывания", StringComparison.Ordinal)
            || label.Contains("в организме", StringComparison.Ordinal))
        {
            return "Всасывание";
        }

        if (label.Contains("выведения", StringComparison.Ordinal)
            || label.Contains("с мочой", StringComparison.Ordinal))
        {
            return "Выведение";
        }

        if (label.Contains("урон", StringComparison.Ordinal))
        {
            return "Урон";
        }

        if (label.Contains("патрон", StringComparison.Ordinal)
            || label.Contains("магазин", StringComparison.Ordinal)
            || label.Contains("боеприп", StringComparison.Ordinal))
        {
            return "Боеприпасы";
        }

        if (label.Contains("отдач", StringComparison.Ordinal)
            || label.Contains("скорость возврата", StringComparison.Ordinal))
        {
            return "Отдача";
        }

        if (label.Contains("прицел", StringComparison.Ordinal)
            || label.Contains("поле зрения", StringComparison.Ordinal)
            || label.Contains("дистанция пристрелки", StringComparison.Ordinal))
        {
            return "Прицеливание";
        }

        if (label.Contains("опыт", StringComparison.Ordinal))
        {
            return "Прокачка";
        }

        if (path.Contains("/skills/", StringComparison.OrdinalIgnoreCase)
            && (label.Contains("параметры", StringComparison.Ordinal)
                || label.Contains("дистанция ремонта", StringComparison.Ordinal)
                || label.Contains("время ремонта", StringComparison.Ordinal)
                || label.Contains("запуска с толкача", StringComparison.Ordinal)
                || label.Contains("скорость поворота колёс", StringComparison.Ordinal)
                || label.Contains("срыв двигателя", StringComparison.Ordinal)
                || label.Contains("шанс ручного улучшения bcu", StringComparison.Ordinal)
                || label.Contains("время взятия крови", StringComparison.Ordinal)))
        {
            return "Навыки";
        }

        if (label.Contains("выдаваемый предмет", StringComparison.Ordinal)
            || label.Contains("состояние предмета", StringComparison.Ordinal))
        {
            return "Стартовые вещи";
        }

        if (label.Contains("источников квестов", StringComparison.Ordinal)
            || label.Contains("квестов выдавать за день", StringComparison.Ordinal)
            || label.Contains("доступных квестов", StringComparison.Ordinal)
            || label.Contains("шанс квестов по уровню", StringComparison.Ordinal))
        {
            return "Источники квестов";
        }

        if (label.Contains("условия по тегам", StringComparison.Ordinal)
            || label.Contains("нужно держать рядом", StringComparison.Ordinal)
            || label.Contains("чужих выполненных квестов", StringComparison.Ordinal))
        {
            return "Условия";
        }

        if (label.Contains("авто-завершение", StringComparison.Ordinal)
            || label.Contains("порядок шага", StringComparison.Ordinal)
            || label.Contains("запускать шаг автоматически", StringComparison.Ordinal))
        {
            return "Ход квеста";
        }

        if (label.Contains("награда", StringComparison.Ordinal))
        {
            return "Награды";
        }

        if (label.Contains("навык", StringComparison.Ordinal)
            || label.Contains("ходьб", StringComparison.Ordinal)
            || label.Contains("спринт", StringComparison.Ordinal))
        {
            return "Навыки";
        }

        if (label.Contains("замок", StringComparison.Ordinal)
            || label.Contains("взлом", StringComparison.Ordinal)
            || label.Contains("обезвреж", StringComparison.Ordinal))
        {
            return "Взлом и защита";
        }

        if (path.Contains("/characters/spawnerpresets/fish", StringComparison.Ordinal)
            || path.Contains("/characters/animals2/fish/", StringComparison.Ordinal)
            || path.Contains("/items/fishing/", StringComparison.Ordinal))
        {
            return "Рыбалка";
        }

        if (label.Contains("рыб", StringComparison.Ordinal)
            || label.Contains("ловл", StringComparison.Ordinal)
            || label.Contains("спавн", StringComparison.Ordinal))
        {
            return "Рыбалка";
        }

        if (label.Contains("скорость движения", StringComparison.Ordinal)
            || label.Contains("высота прыжка", StringComparison.Ordinal)
            || label.Contains("максимальная скорость", StringComparison.Ordinal))
        {
            return "Движение";
        }

        if (label.Contains("пауза", StringComparison.Ordinal)
            || label.Contains("интервал", StringComparison.Ordinal)
            || label.Contains("задержка", StringComparison.Ordinal))
        {
            return "Тайминги";
        }

        if (path.Contains("/vehicles/spawningpresets/automaticspawn/", StringComparison.Ordinal)
            && path.Contains("radiation", StringComparison.Ordinal)
            && (label.Contains("состояние транспорта при появлении", StringComparison.Ordinal)
                || label.Contains("топливо при появлении", StringComparison.Ordinal)
                || label.Contains("заряд аккумулятора при появлении", StringComparison.Ordinal)
                || label.Contains("шанс появления", StringComparison.Ordinal)
                || label.Contains("работоспособность транспорта", StringComparison.Ordinal)))
        {
            return "Радиационный транспорт";
        }

        if (label.Contains("опасное для жизни", StringComparison.Ordinal)
            || label.Contains("диапазон тяжести", StringComparison.Ordinal)
            || label.Contains("нижняя граница", StringComparison.Ordinal)
            || label.Contains("верхняя граница", StringComparison.Ordinal))
        {
            return "Пороги эффекта";
        }

        if (path.Contains("/items/crafting/", StringComparison.OrdinalIgnoreCase) || label.Contains("крафт", StringComparison.Ordinal))
        {
            return "Крафт";
        }

        if (path.Contains("/encounters/", StringComparison.OrdinalIgnoreCase) || path.Contains("/npcs/", StringComparison.OrdinalIgnoreCase))
        {
            return "Поведение NPC";
        }

        if (label.Contains("алкоголь", StringComparison.Ordinal)
            || label.Contains("опьян", StringComparison.Ordinal)
            || label.Contains("похмел", StringComparison.Ordinal)
            || label.Contains("тошнот", StringComparison.Ordinal))
        {
            return "Опьянение";
        }

        if (path.Contains("/items/weapons/", StringComparison.Ordinal)
            && label.Contains("неисправност", StringComparison.Ordinal))
        {
            return "Неисправности";
        }

        if (path.Contains("radiation", StringComparison.OrdinalIgnoreCase) || label.Contains("радиац", StringComparison.Ordinal))
        {
            return "Радиация";
        }

        if (label.Contains("вынослив", StringComparison.Ordinal)
            || label.Contains("скорост", StringComparison.Ordinal)
            || label.Contains("движен", StringComparison.Ordinal)
            || label.Contains("устал", StringComparison.Ordinal)
            || label.Contains("нагруз", StringComparison.Ordinal))
        {
            return "Персонаж";
        }

        if (path.Contains("/economy/", StringComparison.OrdinalIgnoreCase)
            || label.Contains("цена", StringComparison.Ordinal)
            || label.Contains("стоим", StringComparison.Ordinal)
            || label.Contains("трейдер", StringComparison.Ordinal))
        {
            return "Экономика";
        }

        return "Общие";
    }

    private static string ResolveFieldDescription(string relativePath, string userLabel)
    {
        var label = userLabel.ToLowerInvariant();
        if (IsGameEventMarkerAsset(relativePath))
        {
            if (label.Contains("минимум участников", StringComparison.Ordinal))
            {
                return "Сколько игроков минимум нужно, чтобы событие могло начаться.";
            }

            if (label.Contains("огонь по своим", StringComparison.Ordinal))
            {
                return "Если включено, игроки одной команды смогут наносить урон друг другу.";
            }

            if (label.Contains("плата за вход", StringComparison.Ordinal))
            {
                return "Сколько нужно заплатить за участие в событии.";
            }

            if (label.Contains("очки за убийство врага", StringComparison.Ordinal)
                || label.Contains("штраф за убийство союзника", StringComparison.Ordinal)
                || label.Contains("штраф за смерть", StringComparison.Ordinal)
                || label.Contains("штраф за самоубийство", StringComparison.Ordinal)
                || label.Contains("очки за помощь", StringComparison.Ordinal)
                || label.Contains("очки за выстрел в голову", StringComparison.Ordinal)
                || label.Contains("очки за победу в раунде", StringComparison.Ordinal)
                || label.Contains("очки за захват флага", StringComparison.Ordinal))
            {
                return "Сколько очков игрок получает или теряет за это действие внутри события.";
            }

            if (label.Contains("очки славы за участие", StringComparison.Ordinal)
                || label.Contains("перевод очков в славу", StringComparison.Ordinal))
            {
                return "Как событие выдаёт или пересчитывает очки славы по итогам матча.";
            }

            if (label.Contains("длительность раунда", StringComparison.Ordinal)
                || label.Contains("лимит раундов", StringComparison.Ordinal)
                || label.Contains("лимит побед", StringComparison.Ordinal))
            {
                return "Правила длины матча и условия его завершения.";
            }

            if (label.Contains("задержка возрождения", StringComparison.Ordinal)
                || label.Contains("разрешить возрождение", StringComparison.Ordinal))
            {
                return "Как работает повторное появление участника во время события.";
            }

            if (label.Contains("пропустить фазу ключа", StringComparison.Ordinal))
            {
                return "Если включено, зона сброса сразу переходит к боевой части без отдельной фазы ключа.";
            }

            if (label.Contains("название события", StringComparison.Ordinal))
            {
                return "Как это событие будет называться в интерфейсе и уведомлениях.";
            }
        }

        if (IsCargoDropContainerAsset(relativePath))
        {
            if (label.Contains("время до взрыва после приземления", StringComparison.Ordinal))
            {
                return "Сколько секунд контейнер грузового дропа остаётся доступным после приземления, прежде чем взорвётся.";
            }

            if (label.Contains("основные пресеты лута", StringComparison.Ordinal)
                || label.Contains("основные обычные пресеты лута", StringComparison.Ordinal))
            {
                return "Какие обычные пресеты появления предметов контейнер может выбирать для основных ячеек с крупным лутом.";
            }

            if (label.Contains("контейнерные наборы лута", StringComparison.Ordinal)
                || label.Contains("основные расширенные пресеты лута", StringComparison.Ordinal))
            {
                return "Какие расширенные наборы грузового дропа контейнер может выбирать для основных ячеек с крупным лутом. Такой набор может выдавать не один предмет, а связанную подборку вещей.";
            }
        }

        if (label.Contains("когда начинается эта ступень", StringComparison.Ordinal)
            || label.Contains("положение точки кривой", StringComparison.Ordinal))
        {
            if (relativePath.Contains("/encounters/spawn_amount_curves/", StringComparison.OrdinalIgnoreCase))
            {
                return "На каком входном значении кривая спавна переходит к следующей точке.";
            }

            if (relativePath.Contains("/cooking/data/curves/", StringComparison.OrdinalIgnoreCase))
            {
                return "При каком уровне нагрева, времени или ошибки приготовления начинает действовать эта точка кривой.";
            }

            if (relativePath.Contains("/minigames/lockpicking/", StringComparison.OrdinalIgnoreCase))
            {
                return "На каком этапе поведения мини-игры начинает действовать эта точка кривой.";
            }

            return "На каком входном значении начинает действовать эта точка кривой.";
        }

        if (label.Contains("насколько сильно действует эта ступень", StringComparison.Ordinal)
            || label.Contains("значение точки кривой", StringComparison.Ordinal))
        {
            if (relativePath.Contains("/encounters/spawn_amount_curves/", StringComparison.OrdinalIgnoreCase))
            {
                return "Сколько врагов или событий должна давать кривая спавна в этой точке.";
            }

            if (relativePath.Contains("/cooking/data/curves/", StringComparison.OrdinalIgnoreCase))
            {
                return "Как сильно эта точка меняет качество, массу или другой результат приготовления.";
            }

            if (relativePath.Contains("/minigames/lockpicking/", StringComparison.OrdinalIgnoreCase))
            {
                return "Насколько сильно меняется затухание, приближение или другой эффект взлома в этой точке.";
            }

            return "Какое значение выдаёт кривая в этой точке.";
        }

        if (label.Contains("что создаётся", StringComparison.Ordinal))
        {
            return "Главный предмет, который игрок получит после завершения рецепта.";
        }

        if (label.Contains("тип торговца", StringComparison.Ordinal))
        {
            return "Определяет специализацию торговца и его роль в системе экономики.";
        }

        if (relativePath.Contains("/items/spawnerpresets/examine_data_presets/", StringComparison.OrdinalIgnoreCase))
        {
            if (label.Contains("всегда выдавать", StringComparison.Ordinal))
            {
                return "Если включено, этот набор предметов не будет пропускаться и сможет выдаваться каждый раз, когда игра выберет этот слот лута.";
            }

            if (label.Contains("шанс появления набора", StringComparison.Ordinal))
            {
                return "Чем выше это число, тем чаще срабатывает набор предметов. В обычных пресетах SCUM для таких шансов часто используются значения по шкале 0..100.";
            }

            if (label.Contains("учитывать игровую зону предмета", StringComparison.Ordinal))
            {
                return "Если включено, игра дополнительно проверяет, подходит ли зона появления для самих предметов этого набора.";
            }

            if (label.Contains("учитывать редкость предмета", StringComparison.Ordinal))
            {
                return "Если включено, игра учитывает редкость предметов и может реже выбирать более ценный лут.";
            }

            if (label.Contains("учитывать группу появления предмета", StringComparison.Ordinal))
            {
                return "Если включено, игра дополнительно учитывает группу появления, к которой относится предмет.";
            }

            if (label.Contains("начальная повреждённость", StringComparison.Ordinal))
            {
                return "С каким базовым износом или повреждением будут появляться предметы из этого набора.";
            }

            if (label.Contains("разброс повреждённости", StringComparison.Ordinal))
            {
                return "Насколько сильно игра может случайно менять износ предметов вокруг базового значения при выдаче этого набора.";
            }

            if (label.Contains("начальный ресурс или заряд", StringComparison.Ordinal))
            {
                return "С каким количеством ресурса, топлива, заряда или оставшихся использований будут появляться предметы из этого набора.";
            }

            if (label.Contains("разброс ресурса или заряда", StringComparison.Ordinal))
            {
                return "Насколько сильно игра может случайно менять стартовый ресурс или заряд предметов внутри этого набора.";
            }

            if (label.Contains("начальная грязь", StringComparison.Ordinal))
            {
                return "Насколько чистыми или грязными будут появляться предметы из этого набора.";
            }

            if (label.Contains("разброс грязи", StringComparison.Ordinal))
            {
                return "Насколько сильно игра может случайно менять уровень грязи у предметов внутри этого набора.";
            }

            if (label.Contains("минимум патронов", StringComparison.Ordinal) || label.Contains("максимум патронов", StringComparison.Ordinal))
            {
                return "Минимальное и максимальное количество патронов, которое могут получить оружие или магазины из этого набора.";
            }

            if (label.Contains("минимум в стопке", StringComparison.Ordinal) || label.Contains("максимум в стопке", StringComparison.Ordinal))
            {
                return "Минимальный и максимальный размер стопки для предметов, которые могут складываться при выдаче из этого набора.";
            }
        }

        if (IsAdvancedItemSpawnerPresetAsset(relativePath))
        {
            if (label.Contains("готовый подпакет", StringComparison.Ordinal))
            {
                return "Какой готовый контейнерный подпакет может выбирать этот контейнер внутри своего пресета.";
            }

            if (label.Contains("редкость набора", StringComparison.Ordinal))
            {
                return "Насколько редким должен считаться этот подпакет при выборе внутри контейнера.";
            }

            if (label.Contains("всегда выдавать", StringComparison.Ordinal))
            {
                return "Если включено, этот контейнерный пресет не будет пропускаться после выбора и обязательно выдаст свой результат.";
            }

            if (label.Contains("шанс появления набора", StringComparison.Ordinal))
            {
                return "Чем выше это число, тем чаще контейнер, шкафчик или другой объект поиска будет выбирать именно этот готовый пресет.";
            }

            if (label.Contains("сколько предметов выдавать", StringComparison.Ordinal))
            {
                return IsCargoDropPackagePresetAsset(relativePath)
                    ? "Минимум и максимум предметов, которые контейнер возьмёт из этого готового набора при одном выборе."
                    : "Минимум и максимум предметов, которые этот контейнерный пресет попытается выдать за одно срабатывание.";
            }

            if (label.Contains("разрешать повторы предметов", StringComparison.Ordinal))
            {
                return "Если включено, контейнер может выбрать один и тот же предмет из набора несколько раз за одну выдачу.";
            }

            if (label.Contains("учитывать игровую зону предмета", StringComparison.Ordinal))
            {
                return IsCargoDropPackagePresetAsset(relativePath)
                    ? "Если включено, игра дополнительно проверяет, подходит ли зона грузового дропа для предметов внутри этого набора."
                    : "Если включено, игра дополнительно проверяет, подходит ли зона поиска или спавна для предметов этого контейнерного пресета.";
            }

            if (label.Contains("начальная повреждённость", StringComparison.Ordinal))
            {
                return "С каким базовым износом или повреждением будут появляться предметы из этого контейнерного пресета.";
            }

            if (label.Contains("разброс повреждённости", StringComparison.Ordinal))
            {
                return "Насколько сильно игра может случайно менять износ предметов вокруг базового значения при выдаче из этого пресета.";
            }

            if (label.Contains("начальный ресурс или заряд", StringComparison.Ordinal))
            {
                return "С каким количеством ресурса, топлива, заряда или оставшихся использований будут появляться предметы из этого контейнерного пресета.";
            }

            if (label.Contains("разброс ресурса или заряда", StringComparison.Ordinal))
            {
                return "Насколько сильно игра может случайно менять стартовый ресурс или заряд предметов внутри этого контейнерного пресета.";
            }
        }

        if (relativePath.Contains("/data/tables/items/spawning/", StringComparison.OrdinalIgnoreCase))
        {
            if (label.Contains("максимум копий в мире", StringComparison.Ordinal))
            {
                return "Сколько экземпляров этого предмета игра старается держать одновременно во всём мире через это правило появления.";
            }

            if (label.Contains("разрешённые зоны", StringComparison.Ordinal))
            {
                return "В каких типах местности или лут-зон это правило вообще может сработать.";
            }

            if (label.Contains("побережье", StringComparison.Ordinal)
                || label.Contains("континент", StringComparison.Ordinal)
                || label.Contains("горы", StringComparison.Ordinal)
                || label.Contains("город", StringComparison.Ordinal)
                || label.Contains("сельская местность", StringComparison.Ordinal)
                || label.Contains("промзона", StringComparison.Ordinal)
                || label.Contains("полиция", StringComparison.Ordinal)
                || label.Contains("военные (база)", StringComparison.Ordinal)
                || label.Contains("военные (средний уровень)", StringComparison.Ordinal)
                || label.Contains("военные (продвинутый уровень)", StringComparison.Ordinal)
                || label.Contains("военные (ww2)", StringComparison.Ordinal)
                || label.Contains("спорт", StringComparison.Ordinal)
                || label.Contains("рынок", StringComparison.Ordinal)
                || label.Contains("заправка", StringComparison.Ordinal)
                || label.Contains("аэродром", StringComparison.Ordinal))
            {
                return "Если включено, правило может использоваться в этой зоне поиска и появления предметов.";
            }

            if (label.Contains("откат на одного бойца", StringComparison.Ordinal))
            {
                return "Сколько времени добавлять к откату этого предмета за каждого игрока в отряде или команде, если система использует персональный масштаб появления.";
            }

            if (label.Contains("группа отката", StringComparison.Ordinal))
            {
                return "Общая группа отката. Предметы в одной группе делят между собой паузу повторного появления.";
            }

            if (label.Contains("подчиняться серверному правилу ловушек", StringComparison.Ordinal))
            {
                return "Если включено, это правило будет учитывать серверную настройку, которая может отдельно разрешать или запрещать появление ловушек.";
            }

            if (label.Contains("варианты предмета", StringComparison.Ordinal))
            {
                return "Какие похожие игровые предметы система может выбрать вместо основного предмета в этом правиле появления.";
            }

            if (label.Contains("переопределять ресурс предмета", StringComparison.Ordinal))
            {
                return "Если включено, игра использует указанные ниже значения ресурса вместо обычных настроек самого предмета.";
            }

            if (label.Contains("начальный ресурс", StringComparison.Ordinal))
            {
                return "Базовый процент ресурса, топлива, заряда или прочности, с которым предмет будет появляться по этому правилу.";
            }

            if (label.Contains("случайная добавка к ресурсу", StringComparison.Ordinal))
            {
                return "Насколько сильно игра может случайно увеличить или уменьшить стартовый ресурс вокруг базового значения.";
            }

            if (label.Contains("случайный разброс поворота", StringComparison.Ordinal))
            {
                return "Насколько сильно игра может случайно повернуть предмет при появлении, чтобы одинаковые объекты не лежали всегда под одним углом.";
            }

            if (label.Contains("время отката", StringComparison.Ordinal))
            {
                return "Минимальная и максимальная пауза перед тем, как группа снова разрешит появление предметов.";
            }

            if (label.Contains("учитывать более низкие группы", StringComparison.Ordinal))
            {
                return "Если включено, эта группа отката будет дополнительно смотреть на более низкие ступени той же ветки и не позволит спавниться слишком часто.";
            }
        }

        if (IsRegularItemSpawnerPresetAsset(relativePath))
        {
            if (label.Contains("всегда создавать предмет", StringComparison.Ordinal))
            {
                return "Если включено, игра не будет пропускать этот пресет после его выбора и обязательно создаст один из предметов из списка.";
            }

            if (label.Contains("шанс появления предмета", StringComparison.Ordinal))
            {
                return "Чем выше это число, тем чаще срабатывает этот пресет появления. В обычных пресетах SCUM для таких шансов часто используются значения по шкале 0..100.";
            }

            if (label.Contains("учитывать игровую зону предмета", StringComparison.Ordinal))
            {
                return "Если включено, игра дополнительно проверяет, подходит ли зона появления для самих предметов этого пресета.";
            }

            if (label.Contains("учитывать редкость предмета", StringComparison.Ordinal))
            {
                return "Если включено, игра учитывает редкость предметов и реже выбирает более ценные вещи.";
            }

            if (label.Contains("учитывать группу появления предмета", StringComparison.Ordinal))
            {
                return "Если включено, игра учитывает служебную группу появления предмета, чтобы лут лучше совпадал с типом точки спавна.";
            }

            if (label.Contains("начальная повреждённость", StringComparison.Ordinal))
            {
                return "С каким базовым износом или повреждением предмет обычно появляется.";
            }

            if (label.Contains("разброс повреждённости", StringComparison.Ordinal))
            {
                return "Насколько сильно игра может случайно менять износ предмета вокруг базового значения.";
            }

            if (label.Contains("начальный ресурс или заряд", StringComparison.Ordinal))
            {
                return "С каким количеством ресурса, топлива, заряда или оставшегося использования предмет обычно появляется.";
            }

            if (label.Contains("разброс ресурса или заряда", StringComparison.Ordinal))
            {
                return "Насколько сильно игра может случайно менять начальный ресурс или заряд предмета.";
            }

            if (label.Contains("начальная грязь", StringComparison.Ordinal))
            {
                return "Насколько чистым или грязным предмет появляется по умолчанию.";
            }

            if (label.Contains("разброс грязи", StringComparison.Ordinal))
            {
                return "Насколько сильно игра может случайно менять уровень грязи у появившегося предмета.";
            }

            if (label.Contains("минимум патронов", StringComparison.Ordinal) || label.Contains("максимум патронов", StringComparison.Ordinal))
            {
                return "Минимальное и максимальное количество патронов, которое может получить оружие или магазин при появлении.";
            }

            if (label.Contains("минимум в стопке", StringComparison.Ordinal) || label.Contains("максимум в стопке", StringComparison.Ordinal))
            {
                return "Минимальный и максимальный размер стопки для предметов, которые могут складываться.";
            }

            if (label.Contains("подправлять место появления по столкновению", StringComparison.Ordinal))
            {
                return "Если включено, игра будет немного сдвигать предмет, чтобы он не застревал в поверхности при появлении.";
            }

            if (label.Contains("подправлять поворот предмета по столкновению", StringComparison.Ordinal))
            {
                return "Если включено, игра будет подстраивать угол предмета под окружение, чтобы он лежал естественнее.";
            }
        }

        if (label.Contains("название растения", StringComparison.Ordinal))
        {
            return "Как это растение будет называться в системе фермерства.";
        }

        if (label.Contains("название вредителя", StringComparison.Ordinal))
        {
            return "Как этот вредитель будет называться в фермерских уведомлениях и подсказках.";
        }

        if (label.Contains("название болезни", StringComparison.Ordinal))
        {
            return "Как эта болезнь будет называться в фермерских уведомлениях и подсказках.";
        }

        if (label.Contains("главный предмет", StringComparison.Ordinal)
            && relativePath.Contains("/ui/gameevents/itemselection/", StringComparison.OrdinalIgnoreCase))
        {
            return "Какой главный предмет игрок получит при выборе этого набора в игровом событии.";
        }

        if (label.Contains("название набора", StringComparison.Ordinal)
            && relativePath.Contains("/ui/gameevents/itemselection/", StringComparison.OrdinalIgnoreCase))
        {
            return "Как этот набор будет называться в меню выбора снаряжения игрового события.";
        }

        if (label.Contains("номер команды события", StringComparison.Ordinal)
            && relativePath.Contains("/ui/gameevents/itemselection/", StringComparison.OrdinalIgnoreCase))
        {
            return "Для какой команды предназначен этот комплект события. Обычно это 1 или 2.";
        }

        if (label.Contains("пакет семян", StringComparison.Ordinal))
        {
            return "Какой пакет семян использует эта культура при посадке. Меняй только на реальные семена растений из SCUM.";
        }

        if (label.Contains("лучшая температура для семян", StringComparison.Ordinal))
        {
            return "Температурный диапазон, в котором семена этого растения лучше всего стартуют после посадки.";
        }

        if (label.Contains("лучшая температура роста", StringComparison.Ordinal))
        {
            return "Температурный диапазон, в котором растение лучше всего растёт на обычных стадиях.";
        }

        if (label.Contains("сколько часов длится одна стадия роста", StringComparison.Ordinal))
        {
            return "Сколько игровых часов растение проводит на каждой обычной стадии роста.";
        }

        if (label.Contains("финальная стадия роста", StringComparison.Ordinal))
        {
            return "До какой финальной стадии это растение доходит перед сбором урожая.";
        }

        if (label.Contains("сколько живёт финальная стадия", StringComparison.Ordinal))
        {
            return "Сколько игровых часов растение может оставаться на финальной стадии перед старением.";
        }

        if (label.Contains("когда финальная стадия начинает портиться", StringComparison.Ordinal))
        {
            return "На какой части жизни финальной стадии растение начинает терять свежесть и качество.";
        }

        if (label.Contains("штраф к урожаю", StringComparison.Ordinal))
        {
            return "Насколько этот вредитель или болезнь уменьшает итоговый урожай растения.";
        }

        if (label.Contains("порог наличных для уровня", StringComparison.Ordinal)
            || label.Contains("порог золота для уровня", StringComparison.Ordinal))
        {
            return "Сколько денег или золота нужно, чтобы экономика перешла на этот уровень процветания.";
        }

        if (label.Contains("скорость обновления gbc в час", StringComparison.Ordinal)
            || label.Contains("скорость обновления gsc в час", StringComparison.Ordinal))
        {
            return "Служебная скорость обновления соответствующего канала экономики на этом уровне процветания.";
        }

        if (label.Contains("дорогих товаров", StringComparison.Ordinal))
        {
            return "Шанс появления дорогих товаров на каждом уровне экономики.";
        }

        if (label.Contains("тип пресета npc", StringComparison.Ordinal))
        {
            return "Внутренний тег, который определяет тип этого NPC-пресета.";
        }

        if (label.Contains("состав пресета npc", StringComparison.Ordinal)
            || label.Contains("вес в пресете", StringComparison.Ordinal))
        {
            return "Определяет, какие классы NPC входят в этот пресет и насколько часто они выбираются.";
        }

        if (label.Contains("лимит конкретных npc", StringComparison.Ordinal))
        {
            return "Позволяет задать отдельный максимум одновременно живых NPC для конкретного класса внутри этого события.";
        }

        if (label.Contains("максимум таких npc", StringComparison.Ordinal))
        {
            return "Сколько NPC этого конкретного класса событие может держать одновременно.";
        }

        if (label.Contains("дистанция скрытия вне поля зрения", StringComparison.Ordinal))
        {
            return "На каком расстоянии система может скрыть NPC, если игрок его больше не видит.";
        }

        if (label.Contains("дистанция полного удаления", StringComparison.Ordinal)
            || label.Contains("минимальная дистанция удаления", StringComparison.Ordinal))
        {
            return "На каком расстоянии NPC можно полностью убрать из мира, чтобы освободить лимит события.";
        }

        if (label.Contains("сколько групповых точек появления можно использовать", StringComparison.Ordinal)
            || label.Contains("разрешённые групповые точки появления", StringComparison.Ordinal))
        {
            return "Сколько точек для группового появления событие сможет задействовать одновременно.";
        }

        if (label.Contains("появлять сразу группой", StringComparison.Ordinal)
            || label.Contains("появлять npc группой", StringComparison.Ordinal))
        {
            return "Если включено, событие старается выпускать NPC сразу группой, а не по одному.";
        }

        if (label.Contains("сколько персонажей может появиться", StringComparison.Ordinal)
            || label.Contains("размер группы npc", StringComparison.Ordinal))
        {
            return "Минимум и максимум NPC, которые это событие старается держать активными за один цикл.";
        }

        if (label.Contains("добавка к числу npc за игрока", StringComparison.Ordinal)
            || label.Contains("лимит npc на игрока", StringComparison.Ordinal))
        {
            return "Насколько событие может повышать число NPC в зависимости от числа игроков рядом.";
        }

        if (label.Contains("время до нового появления", StringComparison.Ordinal)
            || label.Contains("время повторного появления npc", StringComparison.Ordinal))
        {
            return "Через сколько секунд система может снова добрать NPC после потерь или очистки.";
        }

        if (label.Contains("сколько персонажей возвращать за один цикл", StringComparison.Ordinal)
            || label.Contains("размер волны повторного появления npc", StringComparison.Ordinal))
        {
            return "Сколько NPC можно вернуть за один шаг перезаполнения события.";
        }

        if (label.Contains("минимальная дистанция между персонажами", StringComparison.Ordinal)
            || label.Contains("минимальная дистанция между npc", StringComparison.Ordinal))
        {
            return "Минимальное расстояние между появляющимися NPC, чтобы они не рождались слишком кучно.";
        }

        if (label.Contains("разрешить запасные точки появления", StringComparison.Ordinal))
        {
            return "Если обычные точки заняты, событие сможет использовать запасные места появления.";
        }

        if (label.Contains("разрешать убирать лишних слабых персонажей", StringComparison.Ordinal)
            || label.Contains("удалять npc низкого приоритета", StringComparison.Ordinal))
        {
            return "Позволяет системе убирать менее важные цели, если лимиты события переполнены.";
        }

        if (label.Contains("радиус действия события", StringComparison.Ordinal))
        {
            return "Радиус, внутри которого событие считает игроков и управляет своим составом.";
        }

        if (label.Contains("разрешить виртуальный режим вдали от игроков", StringComparison.Ordinal)
            || label.Contains("разрешить виртуализацию npc", StringComparison.Ordinal))
        {
            return "Позволяет перевести NPC в облегчённый режим, когда рядом нет игроков.";
        }

        if (label.Contains("через сколько удалять после виртуального режима", StringComparison.Ordinal)
            || label.Contains("время удаления после виртуализации", StringComparison.Ordinal))
        {
            return "Сколько секунд ждать после виртуального режима перед окончательным удалением NPC.";
        }

        if (label.Contains("защищать новичков от этого события", StringComparison.Ordinal))
        {
            return "Если включено, событие слабее затрагивает новых игроков, пока на них действует защита новичка.";
        }

        if (label.Contains("игнорировать общий лимит персонажей", StringComparison.Ordinal))
        {
            return "Разрешает этому событию работать поверх общего лимита NPC сервера.";
        }

        if (label.Contains("не учитывать чужих и внешних персонажей при подсчёте", StringComparison.Ordinal))
        {
            return "При подсчёте лимита событие будет смотреть только на свои актуальные цели внутри нужной зоны.";
        }

        if (label.Contains("пауза между проверками шума", StringComparison.Ordinal))
        {
            return "Как часто событие проверяет шумы и решает, нужно ли запускать орду.";
        }

        if (label.Contains("шанс орды по типу шума", StringComparison.Ordinal))
        {
            return "Вероятность поднять орду для конкретного типа шума.";
        }

        if (label.Contains("интервал проверки случайной орды", StringComparison.Ordinal))
        {
            return "Как часто система делает фоновую проверку на случайный запуск орды.";
        }

        if (label.Contains("шанс случайной орды", StringComparison.Ordinal))
        {
            return "Базовый шанс случайного запуска орды при очередной проверке.";
        }

        if (label.Contains("предпочитать внешние точки появления орды", StringComparison.Ordinal))
        {
            return "Если включено, орда чаще будет выбирать наружные точки появления вместо внутренних.";
        }

        if (label.Contains("дополнительные правила для типов точек появления", StringComparison.Ordinal)
            || label.Contains("типы точек появления", StringComparison.Ordinal)
            || label.Contains("скрытый тип появления персонажа", StringComparison.Ordinal)
            || label.Contains("минимум персонажей для этого режима", StringComparison.Ordinal))
        {
            return "Тонкая настройка того, какие типы точек появления и режимы скрытого спавна может использовать событие.";
        }

        if (label.Contains("подходящий предмет", StringComparison.Ordinal) || label.Contains("варианты ингредиента", StringComparison.Ordinal))
        {
            return "Какие предметы или группы предметов подходят в этот слот рецепта.";
        }

        if (label.Contains("дополнительный расход", StringComparison.Ordinal))
        {
            return "Сколько ещё единиц рецепту нужно сверх базового расхода.";
        }

        if (label.Contains("расход за крафт", StringComparison.Ordinal))
        {
            return "Сколько единиц этого ингредиента уходит на создание предмета.";
        }

        if (label.Contains("роль ингредиента", StringComparison.Ordinal))
        {
            return "Показывает, это расходный материал или инструмент, который нужен для рецепта.";
        }

        if (label.Contains("смешивать варианты", StringComparison.Ordinal))
        {
            return "Определяет, можно ли совмещать разные варианты этого ингредиента в одном крафте.";
        }

        if (label.Contains("тратить предмет целиком", StringComparison.Ordinal))
        {
            return "Если включено, предмет исчезает полностью, а не расходуется частично.";
        }

        if (label.Contains("повреждение при разборе", StringComparison.Ordinal))
        {
            return "Насколько портится предмет, если игрок разберёт его обратно.";
        }

        if (label.Contains("возврат при разборе", StringComparison.Ordinal))
        {
            return "Разрешает вернуть этот ингредиент обратно при разборе предмета.";
        }

        if (label.Contains("это жидкость или ресурс", StringComparison.Ordinal))
        {
            return "Включай только для жидкостей или специальных ресурсных ингредиентов.";
        }

        if (label.Contains("литры", StringComparison.Ordinal))
        {
            return "Сколько литров жидкости нужно для использования этого ингредиента.";
        }

        if (label.Contains("питательност", StringComparison.Ordinal))
        {
            return "Какая доля питательных свойств переносится в результат.";
        }

        if (label.Contains("пауза", StringComparison.Ordinal) || label.Contains("интервал", StringComparison.Ordinal))
        {
            return "Время ожидания перед действием, ответом или следующим циклом.";
        }

        if (label.Contains("скорость движения", StringComparison.Ordinal))
        {
            return "Показывает, как эффект или система меняет скорость передвижения.";
        }

        if (label.Contains("учитывать телосложение", StringComparison.Ordinal))
        {
            return "Определяет, влияет ли телосложение персонажа на предел накопления этого вещества.";
        }

        if (label.Contains("вес выбора", StringComparison.Ordinal))
        {
            return "Чем выше число, тем чаще игра выбирает этот вариант из набора.";
        }

        if (label.Contains("использовать задержку перед запуском", StringComparison.Ordinal))
        {
            return "Если включено, эта защита грузового дропа будет запускаться не сразу, а с паузой после начала события.";
        }

        if (label.Contains("какое событие может запуститься", StringComparison.Ordinal))
        {
            return "Выбери готовый игровой класс события, который эта зона или менеджер сможет запускать.";
        }

        if (label.Contains("задержка перед первым запуском события", StringComparison.Ordinal))
        {
            return "Сколько ждать после активации зоны перед первой попыткой запустить событие.";
        }

        if (label.Contains("интервал проверки запуска события", StringComparison.Ordinal))
        {
            return "Как часто зона проверяет, можно ли прямо сейчас запустить одно из доступных событий.";
        }

        if (label.Contains("пауза между запусками события", StringComparison.Ordinal))
        {
            return "Минимальная пауза между завершением одного события и новым запуском.";
        }

        if (label.Contains("дистанция появления персонажей", StringComparison.Ordinal)
            || label.Contains("запасная дистанция появления персонажей", StringComparison.Ordinal))
        {
            return "На каком расстоянии от игрока или центра зоны разрешено появление участников события.";
        }

        if (label.Contains("быструю проверку видимости", StringComparison.Ordinal))
        {
            return "Ускоряет проверку, видит ли игрок точку появления. Обычно это повышает производительность.";
        }

        if (label.Contains("недавно использованной точке", StringComparison.Ordinal)
            || label.Contains("список использованных точек", StringComparison.Ordinal))
        {
            return "Не даёт событию слишком часто использовать одну и ту же точку появления подряд.";
        }

        if (label.Contains("классы персонажей", StringComparison.Ordinal)
            || label.Contains("персонажи для события", StringComparison.Ordinal))
        {
            return "Набор участников события: какие NPC, животные или зомби входят в выборку и с каким весом они выбираются.";
        }

        if (label.Contains("лимит конкретных npc", StringComparison.Ordinal))
        {
            return "Набор отдельных лимитов по классам NPC. Можно точечно ограничить, сколько целей каждого класса разрешено событию.";
        }

        if (label.Contains("разрешённые точки появления", StringComparison.Ordinal))
        {
            return "Сколько доступных точек событие может использовать для появления.";
        }

        if (label.Contains("интервал дистанции появления защитников грузового дропа", StringComparison.Ordinal))
        {
            return "Минимальная и максимальная дистанция, на которой охрана грузового дропа может появиться рядом с событием.";
        }

        if (label.Contains("подходящие поверхности", StringComparison.Ordinal))
        {
            return "Если включено, событие будет проверять, подходит ли поверхность под выбранный сценарий появления.";
        }

        if (label.Contains("порог процветания", StringComparison.Ordinal))
        {
            return "Порог, после которого торговая зона переходит на следующий уровень процветания.";
        }

        if (label.Contains("обновление gbc", StringComparison.Ordinal)
            || label.Contains("обновление gsc", StringComparison.Ordinal))
        {
            return "Скорость почасового обновления экономики на этом уровне торговой зоны.";
        }

        if (label.Contains("группа появления транспорта", StringComparison.Ordinal))
        {
            return "Внутренняя экономическая группа, которая отвечает за правила появления транспорта.";
        }

        if (label.Contains("источников квестов", StringComparison.Ordinal))
        {
            return "Настройки для каждого источника квестов: сколько заданий он выдаёт, сколько держит в запасе и как открывает новый уровень квестов.";
        }

        if (label.Contains("квестов выдавать за день", StringComparison.Ordinal))
        {
            return "Сколько новых заданий этот источник может подготовить за одни игровые сутки.";
        }

        if (label.Contains("квестов готово сразу", StringComparison.Ordinal))
        {
            return "Сколько квестов источник держит уже подготовленными, чтобы игрок увидел их без ожидания.";
        }

        if (label.Contains("доступных квестов", StringComparison.Ordinal))
        {
            return "Максимум заданий, который этот источник может одновременно показывать игроку.";
        }

        if (label.Contains("шанс квестов по уровню", StringComparison.Ordinal))
        {
            return "Какой шанс выпадения у заданий каждого уровня для этого источника квестов.";
        }

        if (label.Contains("чужих выполненных квестов", StringComparison.Ordinal))
        {
            return "Позволяет связать прогресс у одного источника квестов с уже выполненными заданиями у других NPC или терминалов.";
        }

        if (label.Contains(" / квесты", StringComparison.Ordinal))
        {
            return "Список квестов, которые входят в этот набор. Можно добавлять готовые задания и убирать лишние.";
        }

        if (label.Contains("специальные наборы квестов", StringComparison.Ordinal))
        {
            return "Отдельные тематические наборы заданий, которые включаются только при выполнении особых условий.";
        }

        if (label.Contains("варианты награды", StringComparison.Ordinal))
        {
            return "Несколько возможных комплектов награды для этого квеста. Можно оставить один вариант или скопировать готовый набор и настроить его отдельно.";
        }

        if (label.Contains("условия по тегам", StringComparison.Ordinal))
        {
            return "Показывает текстовое условие, при котором специальный набор квестов становится доступен.";
        }

        if (label.Contains("подходящие предметы", StringComparison.Ordinal))
        {
            return "Какие именно предметы подходят для этого условия. Можно оставить несколько альтернатив, и шаг засчитается по любому из них.";
        }

        if (label.Contains("засчитывать похожие предметы по классу", StringComparison.Ordinal))
        {
            return "Если включено, шаг сможет принимать не только точный предмет, но и совместимые игровые варианты того же семейства.";
        }

        if (label.Contains("оставшихся использований", StringComparison.Ordinal))
        {
            return "Минимальный остаток прочности или числа использований у предмета, который ещё считается подходящим для этого шага.";
        }

        if (label.Contains("минимальное состояние предмета", StringComparison.Ordinal))
        {
            return "Насколько целым должен быть предмет, чтобы квест принял его для этого шага.";
        }

        if (label.Contains("готовность еды", StringComparison.Ordinal))
        {
            return "Позволяет ограничить, насколько сырым или пережаренным может быть съедобный предмет, чтобы шаг принял его.";
        }

        if (label.Contains("качество еды", StringComparison.Ordinal))
        {
            return "Минимальное качество приготовленной еды, которое квест считает подходящим.";
        }

        if (label.Contains("масса предмета", StringComparison.Ordinal))
        {
            return "Минимальный вес предмета, который квест будет засчитывать для этого условия.";
        }

        if (label.Contains("ресурс в предмете", StringComparison.Ordinal))
        {
            return "Минимальный абсолютный остаток ресурса внутри предмета: например топлива, воды или другого наполнения.";
        }

        if (label.Contains("заполненность ресурса", StringComparison.Ordinal))
        {
            return "Минимальная доля заполненности ресурса внутри предмета. Обычно это число от 0 до 1.";
        }

        if (label.Contains("неполные упаковки", StringComparison.Ordinal))
        {
            return "Если включено, шаг будет принимать не только полные пачки или контейнеры, но и уже частично использованные.";
        }

        if (label.Contains("нужно держать рядом", StringComparison.Ordinal))
        {
            return "Какие предметы, материалы или объекты должны лежать рядом с игроком, чтобы этот шаг квеста считался выполненным.";
        }

        if (label.Contains("разрешить авто-завершение шага", StringComparison.Ordinal)
            || label.Contains("разрешить авто-завершение", StringComparison.Ordinal))
        {
            return "Если условие уже выполнено, шаг или источник квестов может закрыть его автоматически без лишнего подтверждения.";
        }

        if (label.Contains("запускать шаг автоматически", StringComparison.Ordinal))
        {
            return "Если включено, этот шаг квеста стартует сам, как только предыдущие условия выполнены.";
        }

        if (label.Contains("порядок шага", StringComparison.Ordinal))
        {
            return "Порядковый номер этого шага внутри цепочки квеста.";
        }

        if (label.Contains("сколько опыта дать", StringComparison.Ordinal))
        {
            return "Сколько опыта получит выбранный навык, когда игрок завершит этот квест или шаг.";
        }

        if (label.Contains("опыт навыков", StringComparison.Ordinal))
        {
            return "Какие навыки получают дополнительный опыт за выполнение этого квеста или конкретного шага.";
        }

        if (label.Contains("сколько денег выдать", StringComparison.Ordinal))
        {
            return "Сколько денег игрок получает в выбранной валюте за выполнение этого квеста или шага.";
        }

        if (label.Contains("денежная награда", StringComparison.Ordinal))
        {
            return "Денежная часть награды. Для каждой валюты можно задать свою сумму.";
        }

        if (label.Contains("награда славой", StringComparison.Ordinal))
        {
            return "Сколько очков славы игрок получает за выполнение этого шага или квеста.";
        }

        if (relativePath.Contains("/quests/", StringComparison.OrdinalIgnoreCase)
            && label.Contains("сколько опыта дать", StringComparison.Ordinal))
        {
            return "Сколько опыта получит выбранный навык после выполнения этого квеста.";
        }

        if (relativePath.Contains("/quests/", StringComparison.OrdinalIgnoreCase)
            && label.Contains("опыт навыков за квест", StringComparison.Ordinal))
        {
            return "Какие навыки получают дополнительный опыт после завершения этого квеста.";
        }

        if (label.Contains("тип воздействия", StringComparison.Ordinal))
        {
            return "Показывает, считается вещество полезным, вредным или нейтральным.";
        }

        if (label.Contains("скорость всасывания", StringComparison.Ordinal)
            || label.Contains("максимум вещества в организме", StringComparison.Ordinal))
        {
            return "Определяет, как быстро вещество накапливается в организме и до какого уровня.";
        }

        if (label.Contains("скорость выведения", StringComparison.Ordinal)
            || label.Contains("выведение с мочой", StringComparison.Ordinal)
            || label.Contains("влияние других веществ на выведение", StringComparison.Ordinal))
        {
            return "Определяет, как быстро вещество покидает организм.";
        }

        if (label.Contains("какой симптом запускается", StringComparison.Ordinal))
        {
            return "Выбери симптом, который должен включаться этим эффектом или побочной реакцией.";
        }

        if (label.Contains("прямое изменение силы", StringComparison.Ordinal)
            || label.Contains("прямое изменение интеллекта", StringComparison.Ordinal)
            || label.Contains("прямое изменение телосложения", StringComparison.Ordinal)
            || label.Contains("прямое изменение ловкости", StringComparison.Ordinal)
            || label.Contains("прибавка к силе", StringComparison.Ordinal)
            || label.Contains("прибавка к интеллекту", StringComparison.Ordinal)
            || label.Contains("прибавка к телосложению", StringComparison.Ordinal)
            || label.Contains("прибавка к ловкости", StringComparison.Ordinal))
        {
            return "На сколько очков вещество сразу повышает или понижает эту характеристику.";
        }

        if (label.Contains("влияние на силу", StringComparison.Ordinal)
            || label.Contains("влияние на интеллект", StringComparison.Ordinal)
            || label.Contains("влияние на телосложение", StringComparison.Ordinal)
            || label.Contains("влияние на ловкость", StringComparison.Ordinal)
            || label.Contains("влияние на выносливость", StringComparison.Ordinal)
            || label.Contains("влияние на максимальную выносливость", StringComparison.Ordinal)
            || label.Contains("влияние на результативность", StringComparison.Ordinal)
            || label.Contains("влияние на успешность действий", StringComparison.Ordinal)
            || label.Contains("множитель силы", StringComparison.Ordinal)
            || label.Contains("множитель интеллекта", StringComparison.Ordinal)
            || label.Contains("множитель телосложения", StringComparison.Ordinal)
            || label.Contains("множитель ловкости", StringComparison.Ordinal)
            || label.Contains("множитель выносливости", StringComparison.Ordinal)
            || label.Contains("множитель максимальной выносливости", StringComparison.Ordinal)
            || label.Contains("множитель результативности", StringComparison.Ordinal)
            || label.Contains("множитель успешности действий", StringComparison.Ordinal))
        {
            return "Во сколько раз вещество или эффект меняет параметр. 1 = без изменений, больше 1 = усиление, меньше 1 = ослабление.";
        }

        if (label.Contains("изменение силы", StringComparison.Ordinal)
            || label.Contains("изменение интеллекта", StringComparison.Ordinal)
            || label.Contains("изменение телосложения", StringComparison.Ordinal)
            || label.Contains("изменение ловкости", StringComparison.Ordinal)
            || label.Contains("изменение выносливости", StringComparison.Ordinal))
        {
            return "Насколько сильно эффект меняет соответствующий параметр персонажа.";
        }

        if (label.Contains("максимальная выносливость", StringComparison.Ordinal)
            || label.Contains("результативность", StringComparison.Ordinal)
            || label.Contains("успешность действий", StringComparison.Ordinal))
        {
            return "Определяет, какой бонус или штраф этот эффект даёт персонажу.";
        }

        if (label.Contains("общая защита части тела", StringComparison.Ordinal))
        {
            return "Общий множитель защиты этой части тела. Чем выше число, тем сильнее защита.";
        }

        if (label.Contains("защита от режущего ближнего урона", StringComparison.Ordinal))
        {
            return "Насколько хорошо предмет защищает от ножей и другого режущего ближнего оружия.";
        }

        if (label.Contains("защита от тупого ближнего урона", StringComparison.Ordinal))
        {
            return "Насколько хорошо предмет защищает от дубинок, кулаков и другого тупого ближнего оружия.";
        }

        if (label.Contains("какое вещество запускает эффект", StringComparison.Ordinal)
            || label.Contains("вещество-источник эффекта", StringComparison.Ordinal))
        {
            return "Выбери вещество или стимулятор, которое должно запускать этот эффект.";
        }

        if (label.Contains("анимац", StringComparison.Ordinal))
        {
            return "Список или параметр анимаций, которые может использовать система.";
        }

        var isRadiationVehiclePreset =
            relativePath.Contains("/vehicles/spawningpresets/automaticspawn/", StringComparison.OrdinalIgnoreCase)
            && relativePath.Contains("radiation", StringComparison.OrdinalIgnoreCase);

        if (!isRadiationVehiclePreset
            && (label.Contains("опасное для жизни", StringComparison.Ordinal)
                || label.Contains("нижняя граница", StringComparison.Ordinal)
                || label.Contains("верхняя граница", StringComparison.Ordinal)))
        {
            return "Порог, при котором эффект переходит в более опасную стадию.";
        }

        if (label.Contains("интенсив", StringComparison.Ordinal))
        {
            return "Насколько сильно эффект влияет на персонажа.";
        }

        if (label.Contains("длитель", StringComparison.Ordinal) || label.Contains("время", StringComparison.Ordinal))
        {
            return "Время действия эффекта или процесса.";
        }

        if (relativePath.Contains("/items/weapons/", StringComparison.OrdinalIgnoreCase))
        {
            if (label.Contains("использовать свой шанс неисправности", StringComparison.Ordinal))
            {
                return "Если включено, оружие использует свой собственный диапазон общей неисправности из этого ассета, а не базовое значение родителя.";
            }

            if (label.Contains("общий шанс неисправности (минимум)", StringComparison.Ordinal))
            {
                return "Нижняя граница базового диапазона общей неисправности оружия. Конкретные типы осечки и поломки дальше уточняются отдельной кривой отказов и внутренними правилами оружия.";
            }

            if (label.Contains("общий шанс неисправности (максимум)", StringComparison.Ordinal))
            {
                return "Верхняя граница базового диапазона общей неисправности оружия. Конкретные типы осечки и поломки дальше уточняются отдельной кривой отказов и внутренними правилами оружия.";
            }

            if (label.Contains("общий шанс неисправности", StringComparison.Ordinal))
            {
                return "Базовый диапазон общей неисправности этого оружия. Он задаёт общий риск отказа, а конкретные типы осечки и поломки дальше уточняются отдельной кривой отказов и внутренними правилами оружия.";
            }
        }

        if (isRadiationVehiclePreset)
        {
            if (label.Contains("шанс появления", StringComparison.Ordinal))
            {
                return "Насколько часто этот вариант радиационного транспорта может быть выбран системой появления.";
            }

            if (label.Contains("состояние транспорта при появлении", StringComparison.Ordinal))
            {
                return "Диапазон состояния транспорта при появлении. Ниже минимума и выше максимума система не выдаёт.";
            }

            if (label.Contains("топливо при появлении", StringComparison.Ordinal))
            {
                return "Диапазон стартового топлива для этой машины при появлении в радиационной зоне.";
            }

            if (label.Contains("заряд аккумулятора при появлении", StringComparison.Ordinal))
            {
                return "Диапазон стартового заряда аккумулятора для этой машины при появлении в радиационной зоне.";
            }

            if (label.Contains("влияет на работоспособность транспорта", StringComparison.Ordinal))
            {
                return "Если включено, отсутствие или поломка этого узла влияет на возможность нормально использовать транспорт.";
            }
        }

        if (label.Contains("шанс появления", StringComparison.Ordinal)
            && relativePath.Contains("/characters/spawnerpresets/fishspawningpresets/", StringComparison.OrdinalIgnoreCase))
        {
            return "Насколько часто этот водный пресет участвует в выборе появления рыбы.";
        }

        if (label.Contains("шанс", StringComparison.Ordinal) || label.Contains("probability", StringComparison.Ordinal))
        {
            return "Вероятность срабатывания (обычно 0..1).";
        }

        if (label.Contains("цена", StringComparison.Ordinal) || label.Contains("стоим", StringComparison.Ordinal))
        {
            return "Экономический параметр продажи/покупки.";
        }

        if (label.Contains("урон за выстрел", StringComparison.Ordinal))
        {
            return "Сколько здоровья снимает одно попадание.";
        }

        if (label.Contains("патронов в магазине", StringComparison.Ordinal))
        {
            return "Максимум патронов, которые вмещает оружие.";
        }

        if (label.Contains("скорострельност", StringComparison.Ordinal))
        {
            return "Как быстро оружие делает следующие выстрелы.";
        }

        if (label.Contains("сила отдачи", StringComparison.Ordinal) || label.Contains("отдач", StringComparison.Ordinal))
        {
            return "Насколько сильно оружие уводит при выстреле.";
        }

        if (label.Contains("выдаваемый предмет", StringComparison.Ordinal))
        {
            return "Какой предмет будет выдаваться в этой ячейке набора.";
        }

        if (label.Contains("состояние предмета", StringComparison.Ordinal))
        {
            return "Насколько целым или изношенным будет выдан предмет.";
        }

        if (label.Contains("число попыток обезвреживания", StringComparison.Ordinal))
        {
            return "Сколько попыток даётся игроку на обезвреживание или взлом.";
        }

        if (label.Contains("опыт за минуту", StringComparison.Ordinal))
        {
            return "Сколько опыта получает навык за минуту этого действия.";
        }

        if (label.Contains("значение на минимальном опыте", StringComparison.Ordinal)
            || label.Contains("значение на максимальном опыте", StringComparison.Ordinal))
        {
            return "Предел бонуса навыка на старте и на полном развитии.";
        }

        if (label.Contains("вид рыбы", StringComparison.Ordinal))
        {
            return "Какой вид рыбы может водиться в этом пресете водоёма.";
        }

        if (label.Contains("вес появления", StringComparison.Ordinal)
            && relativePath.Contains("/characters/spawnerpresets/fishspeciespresets/", StringComparison.OrdinalIgnoreCase))
        {
            return "Насколько часто этот вид рыбы выбирается по сравнению с другими видами в этом водоёме.";
        }

        if (label.Contains("опыт за ", StringComparison.Ordinal))
        {
            return "Сколько опыта получает этот навык за выбранное действие или дистанцию.";
        }

        if (label.Contains("дистанция ремонта", StringComparison.Ordinal))
        {
            return "С какого расстояния персонаж может безопасно выполнять ремонт на этом уровне навыка.";
        }

        if (label.Contains("расход инструмента", StringComparison.Ordinal))
        {
            return "Во сколько раз быстрее или медленнее тратится инструмент. Меньше число обычно выгоднее.";
        }

        if (label.Contains("время ремонта", StringComparison.Ordinal)
            || label.Contains("время заправки", StringComparison.Ordinal)
            || label.Contains("время слива топлива", StringComparison.Ordinal)
            || label.Contains("время зарядки аккумулятора", StringComparison.Ordinal)
            || label.Contains("время запуска с толкача", StringComparison.Ordinal)
            || label.Contains("время зажигания", StringComparison.Ordinal)
            || label.Contains("время взятия крови", StringComparison.Ordinal)
            || label.Contains("задержка переключения передачи", StringComparison.Ordinal))
        {
            return "Во сколько раз навык ускоряет или замедляет это действие. Меньше число обычно означает быстрее.";
        }

        if (label.Contains("максимальная скорость", StringComparison.Ordinal)
            || label.Contains("сила разгона", StringComparison.Ordinal)
            || label.Contains("скорость поворота колёс", StringComparison.Ordinal)
            || label.Contains("параметры двигателя", StringComparison.Ordinal)
            || label.Contains("параметры коробки передач", StringComparison.Ordinal))
        {
            return "Насколько уверенно персонаж управляет транспортом на этом уровне навыка.";
        }

        if (label.Contains("шанс сбоя зажигания", StringComparison.Ordinal)
            || label.Contains("шанс заглушить двигатель", StringComparison.Ordinal)
            || label.Contains("шанс ручного улучшения bcu", StringComparison.Ordinal))
        {
            return "Вероятность успешного или неудачного исхода для этого действия на выбранном уровне навыка.";
        }

        if (label.Contains("ингредиент", StringComparison.Ordinal))
        {
            return "Параметр состава рецепта и требований к предметам.";
        }

        if (relativePath.Contains("radiation", StringComparison.OrdinalIgnoreCase))
        {
            return "Параметр радиационного эффекта или зоны.";
        }

        if (relativePath.Contains("/encounters/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("/npcs/", StringComparison.OrdinalIgnoreCase))
        {
            return "Безопасный параметр поведения NPC, орд или игровых событий.";
        }

        if (relativePath.Contains("/economy/", StringComparison.OrdinalIgnoreCase))
        {
            return "Безопасный параметр торговли, аванпоста или экономики.";
        }

        if (relativePath.Contains("/fortifications/locks/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("/minigames/lockpicking/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("/basebuilding/", StringComparison.OrdinalIgnoreCase))
        {
            return "Безопасный параметр замка, взлома, ловушки или защиты базы.";
        }

        if (relativePath.Contains("/vehicles/", StringComparison.OrdinalIgnoreCase))
        {
            return "Безопасный параметр транспорта, столкновения или урона при аварии.";
        }

        if (relativePath.Contains("/skills/", StringComparison.OrdinalIgnoreCase))
        {
            return "Безопасный параметр навыка и его прокачки.";
        }

        if (relativePath.Contains("/quests/", StringComparison.OrdinalIgnoreCase))
        {
            return "Безопасный параметр цели, награды или хода квеста.";
        }

        return "Игровая настройка этого раздела.";
    }

    private static string ResolveReferenceInfoDescription(string relativePath, string userLabel)
    {
        var label = userLabel.ToLowerInvariant();
        if (label.Contains("связанные вещества эффекта", StringComparison.Ordinal))
        {
            return "Справка: какие вещества или стимуляторы могут включать этот эффект.";
        }

        if (label.Contains("симптом", StringComparison.Ordinal))
        {
            return "Справка: какой игровой симптом связан с этим эффектом.";
        }

        if (label.Contains("вещество", StringComparison.Ordinal)
            || label.Contains("вещества", StringComparison.Ordinal))
        {
            return "Справка: какое вещество или стимулятор запускает этот эффект.";
        }

        if (label.Contains("навык", StringComparison.Ordinal))
        {
            return "Справка: какая игровая система или навык связаны с этим параметром.";
        }

        return ResolveFieldDescription(relativePath, userLabel);
    }

    private static List<StudioModFieldOptionDto>? ResolveFieldOptions(string relativePath, string userLabel, string valueType, string currentValue)
    {
        if (valueType != "enum")
        {
            return null;
        }

        if (currentValue.StartsWith("ECraftingIngredientPurpose::", StringComparison.OrdinalIgnoreCase)
            || userLabel.Contains("роль ингредиента", StringComparison.OrdinalIgnoreCase))
        {
            return CraftingIngredientPurposeOptions;
        }

        if (currentValue.StartsWith("ECraftingIngredientMixingType::", StringComparison.OrdinalIgnoreCase)
            || userLabel.Contains("смешивать варианты", StringComparison.OrdinalIgnoreCase))
        {
            return CraftingIngredientMixingTypeOptions;
        }

        if (currentValue.StartsWith("EDisposition::", StringComparison.OrdinalIgnoreCase)
            || userLabel.Contains("тип воздействия", StringComparison.OrdinalIgnoreCase))
        {
            return DispositionOptions;
        }

        if (currentValue.StartsWith("ETraderType::", StringComparison.OrdinalIgnoreCase)
            || userLabel.Contains("тип торговца", StringComparison.OrdinalIgnoreCase))
        {
            return TraderTypeOptions;
        }

        if (currentValue.StartsWith("EFoodCookLevel::", StringComparison.OrdinalIgnoreCase)
            || userLabel.Contains("готовность еды", StringComparison.OrdinalIgnoreCase))
        {
            return FoodCookLevelOptions;
        }

        if (currentValue.StartsWith("EFoodCookQuality::", StringComparison.OrdinalIgnoreCase)
            || userLabel.Contains("качество еды", StringComparison.OrdinalIgnoreCase))
        {
            return FoodCookQualityOptions;
        }

        if (currentValue.StartsWith("EPlantGrowthStage::", StringComparison.OrdinalIgnoreCase)
            || userLabel.Contains("финальная стадия роста", StringComparison.OrdinalIgnoreCase))
        {
            return PlantGrowthStageOptions;
        }

        if (currentValue.StartsWith("EItemRarity::", StringComparison.OrdinalIgnoreCase)
            || userLabel.Contains("редкость набора", StringComparison.OrdinalIgnoreCase))
        {
            return ItemRarityOptions;
        }

        return null;
    }

    private static (string? ReferencePickerKind, string? ReferencePickerPrompt) ResolveFieldReferencePicker(
        string relativePath,
        string userLabel,
        string valueType,
        string currentValue)
    {
        if (valueType is not ("object" or "soft-object" or "soft-object-path"))
        {
            return (null, null);
        }

        var label = userLabel.ToLowerInvariant();
        var path = relativePath.ToLowerInvariant();

        if (IsCargoDropContainerAsset(relativePath))
        {
            if (label.Contains("основные пресеты лута", StringComparison.Ordinal)
                || label.Contains("основные обычные пресеты лута", StringComparison.Ordinal)
                || label.Contains("обычный пресет лута", StringComparison.Ordinal))
            {
                return (
                    "item-spawner-preset",
                    "Найди совместимый пресет грузового дропа, который контейнер должен использовать для этого варианта лута.");
            }

            if (label.Contains("контейнерные наборы лута", StringComparison.Ordinal)
                || label.Contains("основные расширенные пресеты лута", StringComparison.Ordinal)
                || label.Contains("контейнерный набор лута", StringComparison.Ordinal)
                || label.Contains("расширенный пресет лута", StringComparison.Ordinal))
            {
                return (
                    "advanced-item-spawner-preset",
                    "Найди совместимый контейнерный набор грузового дропа, который должен использовать этот вариант лута.");
            }
        }

        if ((path.Contains("/bodyeffects/", StringComparison.Ordinal)
                || path.Contains("/metabolism/", StringComparison.Ordinal))
            && label.Contains("вещество-источник эффекта", StringComparison.Ordinal))
        {
            return (
                "foreign-substance-all",
                "Найди вещество, которое должно запускать этот эффект, и выбери его из списка.");
        }

        if (path.Contains("/items/crafting/recipes/", StringComparison.Ordinal)
            && (label.Contains("какой навык влияет на рецепт", StringComparison.Ordinal)
                || label.Contains("основной навык рецепта", StringComparison.Ordinal)
                || label.Contains("навык влияет на рецепт", StringComparison.Ordinal)))
        {
            return (
                "skill-asset",
                "Найди навык, который должен влиять на этот рецепт, и выбери его из списка.");
        }

        if (path.Contains("/bodyeffects/", StringComparison.Ordinal)
            && label.Contains("какой симптом запускается", StringComparison.Ordinal))
        {
            return (
                "bodyeffect-symptom",
                "Найди симптом, который должен запускаться этим эффектом, и выбери его из списка.");
        }

        if (path.Contains("/quests/", StringComparison.Ordinal)
            && label.Contains(" / квесты", StringComparison.Ordinal))
        {
            return (
                "quest-asset",
                "Найди квест, который должен входить в этот набор, и выбери его из списка.");
        }

        if (path.Contains("/characters/spawnerpresets/fishspeciespresets/", StringComparison.Ordinal)
            && label.Contains("вид рыбы", StringComparison.Ordinal))
        {
            return (
                "fish-species-asset",
                "Найди вид рыбы, который должен водиться в этом пресете водоёма.");
        }

        if (path.Contains("/ui/gameevents/itemselection/", StringComparison.Ordinal)
            && label.Contains("главный предмет", StringComparison.Ordinal))
        {
            return (
                "item-asset",
                "Найди главный предмет, который игрок должен получать при выборе этого набора события.");
        }

        if (path.Contains("/foliage/farming/", StringComparison.Ordinal)
            && label.Contains("пакет семян", StringComparison.Ordinal))
        {
            return (
                "item-asset",
                "Найди пакет семян, который должен использоваться для посадки этой культуры.");
        }

        if (path.Contains("/items/spawnerpresets2/", StringComparison.Ordinal)
            && label.Contains("готовый подпакет", StringComparison.Ordinal))
        {
            return (
                "advanced-item-spawner-subpreset",
                "Найди совместимый контейнерный подпакет, который этот контейнер сможет выбирать.");
        }

        if (valueType == "object"
            && currentValue.StartsWith("script:PrisonerBodyConditionOrSymptomSideEffect_", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "bodyeffect-side-effect",
                "Найди, какое последствие нужно включить: сила, скорость, урон, приступ или другой эффект.");
        }

        if ((path.Contains("/encounterzones/", StringComparison.Ordinal)
                || path.Contains("/encounters/", StringComparison.Ordinal)
                || path.Contains("/characters/npcs/", StringComparison.Ordinal))
            && (label.Contains("какое событие может запуститься", StringComparison.Ordinal)
                || label.Contains("класс орды защитников", StringComparison.Ordinal)
                || label.Contains("класс десантного корабля", StringComparison.Ordinal)
                || label.Contains("класс охраны десантного корабля", StringComparison.Ordinal)))
        {
            return (
                "encounter-class",
                "Найди подходящий класс события из игры и выбери его из списка.");
        }

        return (null, null);
    }

    private static string ResolveEditorKind(
        string relativePath,
        string userLabel,
        string valueType,
        IReadOnlyCollection<StudioModFieldOptionDto>? options,
        string? referencePickerKind)
    {
        if (options is not null && options.Count > 0)
        {
            return "select";
        }

        if (!string.IsNullOrWhiteSpace(referencePickerKind))
        {
            return "reference-picker";
        }

        if (valueType == "object")
        {
            return "asset-ref";
        }

        if (valueType is "soft-object" or "soft-object-path")
        {
            var label = userLabel.ToLowerInvariant();
            if (relativePath.Contains("/spawnequipment/", StringComparison.OrdinalIgnoreCase)
                || label.Contains("предмет", StringComparison.Ordinal)
                || label.Contains("патрон", StringComparison.Ordinal)
                || label.Contains("награда", StringComparison.Ordinal)
                || label.Contains("что создаётся", StringComparison.Ordinal))
            {
                return "item-picker";
            }

            return "asset-ref";
        }

        return valueType switch
        {
            "bool" => "toggle",
            "int" or "int8" or "int16" or "int64" or "uint16" or "uint32" or "uint64" or "byte" or "float" or "double" or "long" => "number",
            _ => "text"
        };
    }

    private static (string? min, string? max) ResolveSuggestedRange(string relativePath, string userLabel, string valueType)
    {
        if (valueType == "bool")
        {
            return (null, null);
        }

        var label = userLabel.ToLowerInvariant();
        if (label.Contains("положение точки кривой", StringComparison.Ordinal))
        {
            return (null, null);
        }

        if (label.Contains("значение точки кривой", StringComparison.Ordinal))
        {
            if (label.Contains("шанс", StringComparison.Ordinal))
            {
                return ("0", "1");
            }

            if (label.Contains("скорость", StringComparison.Ordinal))
            {
                return ("0", "3000");
            }

            return (null, null);
        }

        if (IsCargoDropContainerAsset(relativePath) && label.Contains("время до взрыва после приземления", StringComparison.Ordinal))
        {
            return ("5", "600");
        }

        if (IsCargoDropContainerAsset(relativePath) && label.Contains("множитель шанса выбора", StringComparison.Ordinal))
        {
            return ("0", "10");
        }

        if (relativePath.Contains("/data/tables/items/spawning/", StringComparison.OrdinalIgnoreCase))
        {
            if (label.Contains("максимум копий в мире", StringComparison.Ordinal))
            {
                return ("0", "1000");
            }

            if (label.Contains("откат на одного бойца", StringComparison.Ordinal)
                || label.Contains("время отката", StringComparison.Ordinal))
            {
                return ("0", "86400");
            }

            if (label.Contains("начальный ресурс", StringComparison.Ordinal)
                || label.Contains("случайная добавка к ресурсу", StringComparison.Ordinal))
            {
                return ("0", "100");
            }

            if (label.Contains("случайный разброс поворота", StringComparison.Ordinal))
            {
                return ("0", "360");
            }
        }

        if ((IsExamineDataPresetAsset(relativePath) || IsRegularItemSpawnerPresetAsset(relativePath))
            && (label.Contains("шанс появления", StringComparison.Ordinal)
                || label.Contains("probability", StringComparison.Ordinal)))
        {
            return ("0", "100");
        }

        if (IsExamineDataPresetAsset(relativePath)
            && (label.Contains("поврежд", StringComparison.Ordinal)
                || label.Contains("ресурс", StringComparison.Ordinal)
                || label.Contains("заряд", StringComparison.Ordinal)
                || label.Contains("гряз", StringComparison.Ordinal)))
        {
            return ("0", "100");
        }

        if (IsAdvancedItemSpawnerPresetAsset(relativePath)
            && label.Contains("сколько предметов выдавать", StringComparison.Ordinal))
        {
            return ("0", "20");
        }

        if (IsAdvancedItemSpawnerPresetAsset(relativePath)
            && (label.Contains("поврежд", StringComparison.Ordinal)
                || label.Contains("ресурс", StringComparison.Ordinal)
                || label.Contains("заряд", StringComparison.Ordinal)))
        {
            return ("0", "100");
        }

        if (IsAdvancedItemSpawnerPresetAsset(relativePath)
            && (label.Contains("шанс появления", StringComparison.Ordinal)
                || label.Contains("probability", StringComparison.Ordinal)))
        {
            return ("0", "100");
        }

        if (IsRegularItemSpawnerPresetAsset(relativePath)
            && (label.Contains("поврежд", StringComparison.Ordinal)
                || label.Contains("ресурс", StringComparison.Ordinal)
                || label.Contains("заряд", StringComparison.Ordinal)
                || label.Contains("гряз", StringComparison.Ordinal)))
        {
            return ("0", "100");
        }

        if (relativePath.Contains("/vehicles/spawningpresets/automaticspawn/", StringComparison.OrdinalIgnoreCase)
            && relativePath.Contains("radiation", StringComparison.OrdinalIgnoreCase))
        {
            if (label.Contains("шанс появления", StringComparison.Ordinal)
                || label.Contains("состояние транспорта при появлении", StringComparison.Ordinal)
                || label.Contains("топливо при появлении", StringComparison.Ordinal)
                || label.Contains("заряд аккумулятора при появлении", StringComparison.Ordinal))
            {
                return ("0", "100");
            }
        }

        if (label.Contains("общий шанс неисправности", StringComparison.Ordinal))
        {
            return ("0", "100");
        }

        if (label.Contains("шанс", StringComparison.Ordinal) || label.Contains("probability", StringComparison.Ordinal))
        {
            return ("0", "1");
        }

        if (label.Contains("вес появления", StringComparison.Ordinal))
        {
            return ("0", "100");
        }

        if (label.Contains("опыт за ", StringComparison.Ordinal))
        {
            return ("0", "10000");
        }

        if (label.Contains("сколько опыта дать", StringComparison.Ordinal))
        {
            return ("0", "10000");
        }

        if (label.Contains("сколько денег выдать", StringComparison.Ordinal))
        {
            return ("0", "100000");
        }

        if (label.Contains("дистанция ремонта", StringComparison.Ordinal))
        {
            return ("0", "1000");
        }

        if (label.Contains("расход инструмента", StringComparison.Ordinal)
            || label.Contains("время ремонта", StringComparison.Ordinal)
            || label.Contains("время заправки", StringComparison.Ordinal)
            || label.Contains("время слива топлива", StringComparison.Ordinal)
            || label.Contains("время зарядки аккумулятора", StringComparison.Ordinal)
            || label.Contains("время запуска с толкача", StringComparison.Ordinal)
            || label.Contains("время зажигания", StringComparison.Ordinal)
            || label.Contains("время взятия крови", StringComparison.Ordinal)
            || label.Contains("задержка переключения передачи", StringComparison.Ordinal)
            || label.Contains("максимальная скорость", StringComparison.Ordinal)
            || label.Contains("сила разгона", StringComparison.Ordinal)
            || label.Contains("скорость поворота колёс", StringComparison.Ordinal)
            || label.Contains("порог переключения вверх", StringComparison.Ordinal)
            || label.Contains("порог переключения вниз", StringComparison.Ordinal)
            || label.Contains("частота рывков", StringComparison.Ordinal))
        {
            return ("0", "5");
        }

        if (label.Contains("интенсив", StringComparison.Ordinal) || label.Contains("severity", StringComparison.OrdinalIgnoreCase))
        {
            return ("0", "5");
        }

        if (label.Contains("длитель", StringComparison.Ordinal)
            || label.Contains("время", StringComparison.Ordinal)
            || label.Contains("cooldown", StringComparison.OrdinalIgnoreCase))
        {
            return ("0", "3600");
        }

        if (label.Contains("кол", StringComparison.Ordinal)
            || label.Contains("count", StringComparison.OrdinalIgnoreCase)
            || label.Contains("amount", StringComparison.OrdinalIgnoreCase)
            || label.Contains("quantity", StringComparison.OrdinalIgnoreCase))
        {
            return ("0", "999");
        }

        if (label.Contains("повреждение при разборе", StringComparison.Ordinal))
        {
            return ("0", "100");
        }

        if (label.Contains("литры", StringComparison.Ordinal))
        {
            return ("0", "20");
        }

        if (label.Contains("скорост", StringComparison.Ordinal)
            || label.Contains("velocity", StringComparison.OrdinalIgnoreCase))
        {
            return ("0", "3000");
        }

        if (label.Contains("цена", StringComparison.Ordinal)
            || label.Contains("стоим", StringComparison.Ordinal)
            || label.Contains("price", StringComparison.OrdinalIgnoreCase))
        {
            return ("0", "100000");
        }

        if (label.Contains("номер команды события", StringComparison.Ordinal))
        {
            return ("1", "2");
        }

        if (label.Contains("состояние предмета", StringComparison.Ordinal))
        {
            return ("0", "1");
        }

        if (label.Contains("минимальное состояние предмета", StringComparison.Ordinal))
        {
            return ("0", "1");
        }

        if (label.Contains("заполненность ресурса", StringComparison.Ordinal))
        {
            return ("0", "1");
        }

        return (null, null);
    }

    private static string ResolveListTargetDescription(string relativePath, string userLabel)
    {
        var label = userLabel.ToLowerInvariant();
        var path = relativePath.ToLowerInvariant();
        if (IsGameEventMarkerAsset(relativePath))
        {
            if (label.Contains("основное оружие", StringComparison.Ordinal))
            {
                return "Какие основные стволы игрок может выбрать перед стартом события.";
            }

            if (label.Contains("пистолет", StringComparison.Ordinal))
            {
                return "Какие пистолеты доступны игроку как второй слот вооружения.";
            }

            if (label.Contains("ближний бой", StringComparison.Ordinal))
            {
                return "Какие варианты ближнего боя доступны игроку как третий слот вооружения.";
            }

            if (label.Contains("одежда", StringComparison.Ordinal))
            {
                return "Какие готовые комплекты одежды игрок может выбрать для этого события.";
            }

            if (label.Contains("обязательный набор", StringComparison.Ordinal)
                || label.Contains("дополнительное снаряжение", StringComparison.Ordinal))
            {
                return "Что событие выдаёт всем участникам поверх выбранного оружия: гранаты, расходники и другое обязательное снаряжение.";
            }
        }

        if (path.Contains("/data/tables/items/spawning/", StringComparison.Ordinal)
            && label.Contains("варианты предмета", StringComparison.Ordinal))
        {
            return "Какие похожие варианты может выбрать система вместо основного предмета в этом правиле появления.";
        }

        if (IsCargoDropPackagePresetAsset(relativePath) && IsAdvancedItemSpawnerPresetItemListSurface(relativePath, userLabel))
        {
            return "Какие конкретные предметы входят в этот готовый контейнерный набор грузового дропа. Сюда можно добавлять или убирать вещи по одной.";
        }

        if (IsAdvancedItemSpawnerPresetSubpresetListSurface(relativePath, userLabel))
        {
            return "Какие готовые подпакеты лута может выбирать этот контейнер. Новый элемент добавляется через безопасное копирование совместимого шаблона с заменой самой ссылки на подпакет.";
        }

        if (label.Contains("точки кривой", StringComparison.Ordinal))
        {
            if (path.Contains("/encounters/spawn_amount_curves/", StringComparison.Ordinal))
            {
                return "Ключевые точки кривой спавна. Добавляй или убирай точки, чтобы менять, сколько врагов или событий появляется при разных условиях.";
            }

            if (path.Contains("/cooking/data/curves/", StringComparison.Ordinal))
            {
                return "Ключевые точки кулинарной кривой. Они задают, как меняется качество, масса или другой результат приготовления по мере нагрева и времени.";
            }

            if (path.Contains("/minigames/lockpicking/", StringComparison.Ordinal))
            {
                return "Ключевые точки кривой взлома. Добавляй новую точку, чтобы менять затухание, приближение или другой эффект мини-игры по этапам.";
            }

            if (path.Contains("/data/weapon/malfunctionprobabilitycurves/", StringComparison.Ordinal))
            {
                return "Ключевые точки кривой отказов оружия. Они задают, как меняется шанс осечки или поломки при износе и нагрузке.";
            }

            if (path.Contains("/data/batteryeffectscurves/", StringComparison.Ordinal))
            {
                return "Ключевые точки кривой батареи и света. Они задают, как меняется сила света, глюки и другие эффекты по мере разряда.";
            }

            if (path.Contains("/curves/falling/", StringComparison.Ordinal)
                || path.Contains("/curves/landing/", StringComparison.Ordinal))
            {
                return "Ключевые точки кривой падения и приземления. Они задают, как меняется урон и реакция при росте скорости и силы удара.";
            }

            if (path.Contains("/basebuilding/energydamagecurves/", StringComparison.Ordinal))
            {
                return "Ключевые точки кривой энергоурона базы. Они задают, как защитные и энергетические системы наносят урон при разных значениях силы.";
            }

            if (path.Contains("/characters/mechanoids/sentry/curves/", StringComparison.Ordinal))
            {
                return "Ключевые точки кривой турели. Они задают, как меняются её реакции, прицеливание и другие параметры поведения.";
            }

            if (path.Contains("/characters/zombies2/data/observingcurve", StringComparison.Ordinal))
            {
                return "Ключевые точки кривой восприятия зомби. Они задают, как быстро зомби замечает цель при разных условиях.";
            }

            if (path.Contains("/skills/", StringComparison.Ordinal))
            {
                return "Ключевые точки кривой навыка. Добавляй новую ступень, чтобы задать, как бонус меняется от низкого опыта к высокому.";
            }

            if (path.Contains("/vehicles/", StringComparison.Ordinal))
            {
                return "Ключевые точки кривой транспорта. Они задают, как меняется урон или эффект при росте скорости и силы столкновения.";
            }
        }

        if (IsVehicleSpawnPresetListSurface(relativePath, userLabel))
        {
            return "Какие варианты транспорта может выбрать эта группа спавна. Можно добавлять совместимые пресеты машин и убирать ненужные.";
        }

        if (IsCargoDropMajorSpawnerOptionsSurface(relativePath, userLabel))
        {
            return "Какие обычные пресеты появления предметов грузовой контейнер может выбирать для основных слотов крупного лута.";
        }

        if (IsCargoDropMajorSpawnerPresetOptionsSurface(relativePath, userLabel))
        {
            return "Какие расширенные контейнерные наборы грузовой контейнер может выбирать для основных слотов крупного лута.";
        }

        if (IsCargoDropEncounterVariantsSurface(relativePath, userLabel))
        {
            return "Какие сценарии защиты может выбрать грузовой дроп. Добавь совместимый класс охраны и затем настрой его вес выбора и задержку запуска.";
        }

        if (label.Contains("персонажи для события", StringComparison.Ordinal))
        {
            return "Общий пул вариантов для события. Сюда можно добавить новый пресет NPC, зомби, животного или орды, а затем настроить его вес выбора.";
        }

        if (label.Contains("состав пресета npc", StringComparison.Ordinal))
        {
            return "Какие боевые классы может выбрать этот NPC-пресет и насколько часто они выпадают.";
        }

        if (label.Contains("лимит конкретных npc", StringComparison.Ordinal))
        {
            return "Отдельные лимиты по классам NPC. Сюда можно добавить класс и указать, сколько таких NPC разрешено держать одновременно.";
        }

        if (label.Contains("настройки источников квестов", StringComparison.Ordinal)
            || label.Contains("quest givers setup", StringComparison.Ordinal))
        {
            return "Какие игровые источники раздают квесты и по каким правилам они выдают задания в день, держат запас и открывают новые уровни.";
        }

        if (label.Contains("специальные наборы квестов", StringComparison.Ordinal)
            || label.Contains("special quest sets", StringComparison.Ordinal))
        {
            return "Дополнительные наборы квестов с отдельными условиями. Можно копировать готовые наборы и подстраивать их под свои правила.";
        }

        if (IsQuestRewardSkillExperienceLabel(relativePath, userLabel))
        {
            return "Какие навыки получают опыт за этот квест. Добавь навык и укажи, сколько опыта он должен получить.";
        }

        if (IsQuestRewardVariantsLabel(relativePath, userLabel))
        {
            return "Готовые наборы награды для этого квеста. Можно копировать удачный вариант и затем отдельно менять деньги, славу и опыт навыков.";
        }

        if (label.Contains("подходящие предметы", StringComparison.Ordinal)
            && path.Contains("/quests/", StringComparison.Ordinal))
        {
            return "Какие именно предметы подходят для этого шага. Можно добавить несколько вариантов, и квест засчитает любой из них.";
        }

        if (label.Contains("нужно держать рядом", StringComparison.Ordinal)
            || label.Contains("must be in vicinity", StringComparison.Ordinal))
        {
            return "Какие предметы, ресурсы или объекты игрок должен держать рядом, чтобы этот шаг засчитался.";
        }

        if (label.Contains("виды рыбы", StringComparison.Ordinal))
        {
            return "Какие виды рыбы водятся в этом пресете водоёма и с каким весом они выбираются при спавне.";
        }

        if (IsExamineDataPresetItemListSurface(relativePath, userLabel))
        {
            return "Какие предметы может выдать этот контейнер, тайник или грузовой дроп. Можно расширять набор новыми вещами или убирать лишние.";
        }

        if (IsRegularItemSpawnerPresetItemListSurface(relativePath, userLabel))
        {
            return "Какие предметы вообще может выбрать этот обычный пресет появления. Можно добавлять новые вещи в выбор или убирать неподходящие.";
        }

        if (path.Contains("/ui/gameevents/itemselection/", StringComparison.Ordinal))
        {
            if (label.Contains("дополнительные предметы", StringComparison.Ordinal))
            {
                return "Какие дополнительные вещи игрок получает вместе с этим набором события: гранаты, бинты, стрелы, одежду и другие предметы.";
            }

            if (label.Contains("крепления", StringComparison.Ordinal))
            {
                return "Какие прицелы, планки и другие крепления выдаются вместе с главным предметом этого набора события.";
            }
        }

        if (IsPlantSpeciesListSurface(relativePath, userLabel))
        {
            return "Какие культуры вообще доступны системе фермерства. Добавляй сюда новые виды растений или убирай лишние.";
        }

        if (IsPlantPestListSurface(relativePath, userLabel))
        {
            return "Какие вредители могут поражать это растение. Можно добавить новый риск или убрать лишнюю угрозу.";
        }

        if (IsPlantDiseaseListSurface(relativePath, userLabel))
        {
            return "Какие болезни могут поражать это растение. Можно расширить список рисков или убрать неподходящую болезнь.";
        }

        if (label.Contains("ингредиенты", StringComparison.Ordinal) && !label.Contains("варианты", StringComparison.Ordinal))
        {
            return "Слоты рецепта. Добавляй новый ингредиент, убирай лишний или пересобирай состав рецепта.";
        }

        if (label.Contains("варианты ингредиента", StringComparison.Ordinal))
        {
            return "Какие группы или варианты предметов принимает этот слот рецепта.";
        }

        if (label.Contains("ингредиент", StringComparison.Ordinal))
        {
            return "Состав рецепта. Можно добавить ещё один ингредиент, убрать лишний или очистить список.";
        }

        if (label.Contains("рецепт", StringComparison.Ordinal))
        {
            return "Набор рецептов. Можно расширить его или убрать лишние записи.";
        }

        if (label.Contains("анимац", StringComparison.Ordinal))
        {
            return "Список анимаций. Можно расширить набор действий NPC или эффекта.";
        }

        if (label.Contains("узлы", StringComparison.Ordinal))
        {
            return "Список узлов логики или зон. Изменяй осторожно, чтобы не нарушить структуру системы событий.";
        }

        if (label.Contains("ответ", StringComparison.Ordinal) || label.Contains("ожидани", StringComparison.Ordinal))
        {
            return "Список параметров поведения. Добавляй или удаляй элементы аккуратно, чтобы менять логику системы.";
        }

        if (label.Contains("связанные вещества", StringComparison.Ordinal))
        {
            return "Какие вещества или стимуляторы могут включать этот эффект. Можно убрать лишние связи или добавить новое вещество из списка.";
        }

        if (label.Contains("побочные эффекты", StringComparison.Ordinal))
        {
            return "Какие последствия включает эффект: штрафы к атрибутам, изменение скорости, приступы, урон и другие реакции.";
        }

        if (relativePath.Contains("bodyeffects", StringComparison.OrdinalIgnoreCase))
        {
            return "Список частей эффекта. Можно расширить эффект, убрать лишние последствия или пересобрать его состав.";
        }

        return "Список значений. Доступны безопасные операции добавления/удаления.";
    }

    private static string ResolveJsonArrayItemKind(JsonArray arr)
    {
        var first = arr.FirstOrDefault(node => node is not null);
        if (first is null)
        {
            return "unknown";
        }

        return first switch
        {
            JsonObject => "object",
            JsonArray => "array",
            JsonValue => "scalar",
            _ => "unknown"
        };
    }

    private static string ResolveUassetArrayItemKind(PropertyData[] values)
    {
        if (values.Length == 0 || values[0] is null)
        {
            return "unknown";
        }

        return values[0] switch
        {
            StructPropertyData => "struct",
            ArrayPropertyData => "array",
            ObjectPropertyData => "reference",
            SoftObjectPropertyData => "reference",
            SoftObjectPathPropertyData => "reference",
            _ => "scalar"
        };
    }

    private static string ResolveUassetMapItemKind(MapPropertyData mapProperty)
    {
        var values = mapProperty.Value;
        if (values.Count == 0)
        {
            var keyType = mapProperty.KeyType?.ToString() ?? string.Empty;
            var isReferenceKeyType =
                string.Equals(keyType, "ObjectProperty", StringComparison.OrdinalIgnoreCase)
                || string.Equals(keyType, "SoftObjectProperty", StringComparison.OrdinalIgnoreCase)
                || string.Equals(keyType, "SoftObjectPathProperty", StringComparison.OrdinalIgnoreCase);
            return isReferenceKeyType ? "reference-map" : "map";
        }

        var first = values.ElementAt(0).Key;
        return first switch
        {
            ObjectPropertyData => "reference-map",
            SoftObjectPropertyData => "reference-map",
            SoftObjectPathPropertyData => "reference-map",
            _ => "map"
        };
    }

    private static string ResolveUassetSetItemKind(SetPropertyData setProperty)
    {
        var values = setProperty.Value ?? [];
        if (values.Length == 0)
        {
            var arrayType = setProperty.ArrayType?.ToString() ?? string.Empty;
            return arrayType switch
            {
                "StructProperty" => "struct",
                "ObjectProperty" => "reference",
                "SoftObjectProperty" => "reference",
                "SoftObjectPathProperty" => "reference",
                _ => "scalar"
            };
        }

        return ResolveUassetArrayItemKind(values);
    }

    private static bool CanSafelyAddEmptyArrayItem(ArrayPropertyData arrayProperty)
    {
        var arrayType = arrayProperty.ArrayType?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(arrayType))
        {
            return false;
        }

        if (arrayType.Equals("StructProperty", StringComparison.OrdinalIgnoreCase))
        {
            return arrayProperty.DummyStruct is not null;
        }

        return arrayType.Equals("BoolProperty", StringComparison.OrdinalIgnoreCase)
            || arrayType.Equals("FloatProperty", StringComparison.OrdinalIgnoreCase)
            || arrayType.Equals("DoubleProperty", StringComparison.OrdinalIgnoreCase)
            || arrayType.Equals("IntProperty", StringComparison.OrdinalIgnoreCase)
            || arrayType.Equals("Int8Property", StringComparison.OrdinalIgnoreCase)
            || arrayType.Equals("Int16Property", StringComparison.OrdinalIgnoreCase)
            || arrayType.Equals("Int64Property", StringComparison.OrdinalIgnoreCase)
            || arrayType.Equals("UInt16Property", StringComparison.OrdinalIgnoreCase)
            || arrayType.Equals("UInt32Property", StringComparison.OrdinalIgnoreCase)
            || arrayType.Equals("UInt64Property", StringComparison.OrdinalIgnoreCase)
            || arrayType.Equals("NameProperty", StringComparison.OrdinalIgnoreCase)
            || arrayType.Equals("StrProperty", StringComparison.OrdinalIgnoreCase)
            || arrayType.Equals("TextProperty", StringComparison.OrdinalIgnoreCase)
            || arrayType.Equals("Guid", StringComparison.OrdinalIgnoreCase);
    }

    private static (bool SupportsAddReference, string? ReferencePickerKind, string? ReferencePickerPrompt)
        ResolveListTargetReferencePicker(
            string relativePath,
            string userLabel,
            PropertyData[] values)
    {
        var label = userLabel.ToLowerInvariant();
        if (IsCargoDropMajorSpawnerOptionsSurface(relativePath, userLabel)
            && (values.Length == 0
                || values.All(value => value is ObjectPropertyData or SoftObjectPropertyData or SoftObjectPathPropertyData)))
        {
            return (
                true,
                "item-spawner-preset",
                "Найди совместимый пресет грузового дропа, который контейнер должен уметь выбирать.");
        }

        if (IsCargoDropMajorSpawnerPresetOptionsSurface(relativePath, userLabel)
            && (values.Length == 0
                || values.All(value => value is ObjectPropertyData or SoftObjectPropertyData or SoftObjectPathPropertyData)))
        {
            return (
                true,
                "advanced-item-spawner-preset",
                "Найди совместимый контейнерный набор грузового дропа, который контейнер должен уметь выбирать.");
        }

        if (IsVehicleSpawnPresetListSurface(relativePath, userLabel)
            && (values.Length == 0
                || values.All(value => value is ObjectPropertyData or SoftObjectPropertyData or SoftObjectPathPropertyData)))
        {
            return (
                true,
                "vehicle-spawn-preset",
                "Найди пресет транспорта, который группа сможет выбрать при спавне, и нажми добавить.");
        }

        if (relativePath.Contains("/bodyeffects/symptoms/", StringComparison.OrdinalIgnoreCase)
            && label.Contains("связанные вещества эффекта", StringComparison.Ordinal)
            && (values.Length == 0 || values.All(value => value is ObjectPropertyData)))
        {
            return (
                true,
                "foreign-substance-attribute",
                "Найди вещество, которое должно запускать этот эффект, и нажми добавить.");
        }

        if (relativePath.Contains("/bodyeffects/", StringComparison.OrdinalIgnoreCase)
            && label.Contains("побочные эффекты", StringComparison.Ordinal)
            && (values.Length == 0 || values.All(value => value is ObjectPropertyData)))
        {
            return (
                true,
                "bodyeffect-side-effect",
                "Найди, какое последствие нужно добавить в эту систему.");
        }

        if (relativePath.Contains("/quests/", StringComparison.OrdinalIgnoreCase)
            && label.Contains(" / квесты", StringComparison.Ordinal)
            && (values.Length == 0
                || values.All(value => value is ObjectPropertyData or SoftObjectPropertyData or SoftObjectPathPropertyData)))
        {
            return (
                true,
                "quest-asset",
                "Найди квест, который нужно добавить в этот набор.");
        }

        if (relativePath.Contains("/quests/", StringComparison.OrdinalIgnoreCase)
            && label.Contains("подходящие предметы", StringComparison.Ordinal)
            && (values.Length == 0
                || values.All(value => value is ObjectPropertyData or SoftObjectPropertyData or SoftObjectPathPropertyData)))
        {
            return (
                true,
                "item-asset",
                "Найди предмет, который должен подходить для этого шага квеста.");
        }

        if (relativePath.Contains("/ui/gameevents/itemselection/", StringComparison.OrdinalIgnoreCase)
            && (label.Contains("дополнительные предметы", StringComparison.Ordinal)
                || label.Contains("крепления", StringComparison.Ordinal))
            && (values.Length == 0 || values.All(value => value is ObjectPropertyData)))
        {
            return (
                true,
                "item-asset",
                label.Contains("крепления", StringComparison.Ordinal)
                    ? "Найди крепление, которое должно выдаваться вместе с этим набором события."
                    : "Найди дополнительный предмет, который должен выдаваться вместе с этим набором события.");
        }

        if (relativePath.Contains("/items/spawnerpresets/examine_data_presets/", StringComparison.OrdinalIgnoreCase)
            && IsExamineDataPresetItemListSurface(relativePath, userLabel)
            && (values.Length == 0
                || values.All(value => value is ObjectPropertyData or SoftObjectPropertyData or SoftObjectPathPropertyData)))
        {
            return (
                true,
                "item-asset",
                "Найди предмет, который этот контейнер, тайник или грузовой дроп должен уметь выдавать.");
        }

        if (IsCargoDropPackagePresetAsset(relativePath)
            && IsAdvancedItemSpawnerPresetItemListSurface(relativePath, userLabel)
            && (values.Length == 0
                || values.All(value => value is ObjectPropertyData or SoftObjectPropertyData or SoftObjectPathPropertyData)))
        {
            return (
                true,
                "item-asset",
                "Найди предмет, который должен входить в этот контейнерный набор грузового дропа.");
        }

        if (IsAdvancedItemSpawnerPresetSubpresetListSurface(relativePath, userLabel)
            && (values.Length == 0 || values.All(value => value is StructPropertyData)))
        {
            return (
                true,
                "advanced-item-spawner-subpreset",
                "Найди готовый контейнерный подпакет, который должен входить в этот пресет.");
        }

        if (IsRegularItemSpawnerPresetAsset(relativePath)
            && IsRegularItemSpawnerPresetItemListSurface(relativePath, userLabel)
            && (values.Length == 0
                || values.All(value => value is ObjectPropertyData or SoftObjectPropertyData or SoftObjectPathPropertyData)))
        {
            return (
                true,
                "item-asset",
                "Найди предмет, который может появиться по этому пресету.");
        }

        if (relativePath.Contains("/data/tables/items/spawning/", StringComparison.OrdinalIgnoreCase)
            && IsItemSpawningVariationListSurface(relativePath, userLabel)
            && (values.Length == 0
                || values.All(value => value is ObjectPropertyData or SoftObjectPropertyData or SoftObjectPathPropertyData)))
        {
            return (
                true,
                "item-asset",
                "Найди предмет, который должен входить в варианты появления для этого правила спавна.");
        }

        if (IsGameEventMarkerAsset(relativePath) && IsGameEventLoadoutListLabel(userLabel))
        {
            if (label.Contains("основное оружие", StringComparison.Ordinal))
            {
                return (
                    true,
                    "gameevent-primary-loadout",
                    "Найди набор основного оружия, который игрок сможет выбрать в этом событии.");
            }

            if (label.Contains("пистолет", StringComparison.Ordinal))
            {
                return (
                    true,
                    "gameevent-secondary-loadout",
                    "Найди набор пистолета, который игрок сможет выбрать как второй слот в этом событии.");
            }

            if (label.Contains("ближний бой", StringComparison.Ordinal))
            {
                return (
                    true,
                    "gameevent-tertiary-loadout",
                    "Найди набор ближнего боя, который игрок сможет выбрать как третий слот в этом событии.");
            }

            if (label.Contains("одежда", StringComparison.Ordinal))
            {
                return (
                    true,
                    "gameevent-outfit-loadout",
                    "Найди комплект одежды, который игрок сможет выбрать для этого события.");
            }

            if (label.Contains("обязательный набор", StringComparison.Ordinal))
            {
                return (
                    true,
                    "gameevent-mandatory-loadout",
                    "Найди обязательный набор снаряжения, который событие должно выдавать всем участникам.");
            }

            if (label.Contains("дополнительное снаряжение", StringComparison.Ordinal))
            {
                return (
                    true,
                    "gameevent-support-loadout",
                    "Найди дополнительный набор снаряжения, который событие должно выдавать участникам.");
            }
        }

        if (relativePath.Contains("/characters/spawnerpresets/fishspeciespresets/", StringComparison.OrdinalIgnoreCase)
            && label.Contains("виды рыбы", StringComparison.Ordinal)
            && (values.Length == 0 || values.All(value => value is StructPropertyData)))
        {
            return (
                true,
                "fish-species-asset",
                "Найди вид рыбы, который должен водиться в этом пресете водоёма.");
        }

        if (relativePath.Contains("/foliage/farming/", StringComparison.OrdinalIgnoreCase)
            && IsPlantSpeciesListSurface(relativePath, userLabel)
            && (values.Length == 0
                || values.All(value => value is SoftObjectPropertyData or SoftObjectPathPropertyData or ObjectPropertyData)))
        {
            return (
                true,
                "plant-species-asset",
                "Найди растение, которое должно входить в общий список доступных культур.");
        }

        if (relativePath.Contains("/foliage/farming/", StringComparison.OrdinalIgnoreCase)
            && IsPlantPestListSurface(relativePath, userLabel)
            && (values.Length == 0
                || values.All(value => value is ObjectPropertyData or SoftObjectPropertyData or SoftObjectPathPropertyData)))
        {
            return (
                true,
                "plant-pest-asset",
                "Найди вредителя, который должен уметь поражать это растение.");
        }

        if (relativePath.Contains("/foliage/farming/", StringComparison.OrdinalIgnoreCase)
            && IsPlantDiseaseListSurface(relativePath, userLabel)
            && (values.Length == 0
                || values.All(value => value is ObjectPropertyData or SoftObjectPropertyData or SoftObjectPathPropertyData)))
        {
            return (
                true,
                "plant-disease-asset",
                "Найди болезнь, которую это растение должно уметь подхватывать.");
        }

        return (false, null, null);
    }

    private static (bool SupportsAddReference, string? ReferencePickerKind, string? ReferencePickerPrompt)
        ResolveMapTargetReferencePicker(
            string relativePath,
            string userLabel,
            MapPropertyData mapProperty)
    {
        var label = userLabel.ToLowerInvariant();
        var entries = mapProperty.Value ?? new TMap<PropertyData, PropertyData>();
        var supportsReferenceKey = entries.Count > 0
            ? entries.All(entry => entry.Key is ObjectPropertyData or SoftObjectPropertyData or SoftObjectPathPropertyData)
            : string.Equals(mapProperty.KeyType?.ToString(), "ObjectProperty", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mapProperty.KeyType?.ToString(), "SoftObjectProperty", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mapProperty.KeyType?.ToString(), "SoftObjectPathProperty", StringComparison.OrdinalIgnoreCase);
        var supportsNumericValue = entries.Count > 0
            ? entries.All(entry => entry.Value is FloatPropertyData or DoublePropertyData or IntPropertyData or Int8PropertyData or Int16PropertyData or Int64PropertyData or BytePropertyData or UInt16PropertyData or UInt32PropertyData or UInt64PropertyData)
            : string.Equals(mapProperty.ValueType?.ToString(), "FloatProperty", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mapProperty.ValueType?.ToString(), "DoubleProperty", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mapProperty.ValueType?.ToString(), "IntProperty", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mapProperty.ValueType?.ToString(), "Int8Property", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mapProperty.ValueType?.ToString(), "Int16Property", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mapProperty.ValueType?.ToString(), "Int64Property", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mapProperty.ValueType?.ToString(), "ByteProperty", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mapProperty.ValueType?.ToString(), "UInt16Property", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mapProperty.ValueType?.ToString(), "UInt32Property", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mapProperty.ValueType?.ToString(), "UInt64Property", StringComparison.OrdinalIgnoreCase);
        var supportsStructValue = entries.Count > 0
            ? entries.All(entry => entry.Value is StructPropertyData)
            : string.Equals(mapProperty.ValueType?.ToString(), "StructProperty", StringComparison.OrdinalIgnoreCase);

        if (IsCargoDropEncounterVariantsSurface(relativePath, userLabel)
            && supportsReferenceKey
            && supportsStructValue
            && entries.Count > 0)
        {
            return (
                true,
                "cargo-drop-encounter-class",
                "Найди готовый вариант защиты грузового дропа и добавь его в список защитников события.");
        }

        if ((relativePath.Contains("/encounters/", StringComparison.OrdinalIgnoreCase)
                || relativePath.Contains("/npcs/", StringComparison.OrdinalIgnoreCase))
            && (label.Contains("классы персонажей", StringComparison.Ordinal)
                || label.Contains("состав пресета npc", StringComparison.Ordinal))
            && supportsReferenceKey
            && supportsNumericValue)
        {
            return (
                true,
                "encounter-npc-class",
                "Найди боевой класс NPC, который этот пресет сможет выбирать, и нажми добавить.");
        }

        if ((relativePath.Contains("/encounters/", StringComparison.OrdinalIgnoreCase)
                || relativePath.Contains("/npcs/", StringComparison.OrdinalIgnoreCase))
            && label.Contains("персонажи для события", StringComparison.Ordinal)
            && supportsReferenceKey
            && supportsNumericValue)
        {
            return (
                true,
                "encounter-character-preset",
                "Найди пресет врага, NPC, животного или орды, который это событие сможет выбирать, и нажми добавить.");
        }

        if ((relativePath.Contains("/encounters/", StringComparison.OrdinalIgnoreCase)
                || relativePath.Contains("/npcs/", StringComparison.OrdinalIgnoreCase))
            && label.Contains("лимит конкретных npc", StringComparison.Ordinal)
            && supportsReferenceKey
            && supportsNumericValue)
        {
            return (
                true,
                "encounter-npc-class",
                "Найди класс NPC, для которого нужно задать отдельный лимит одновременно живых целей.");
        }

        if (IsQuestRewardSkillExperienceLabel(relativePath, userLabel)
            && supportsReferenceKey
            && supportsNumericValue)
        {
            return (
                true,
                "skill-asset",
                "Найди навык, который должен получать опыт за этот квест, и нажми добавить.");
        }

        if (relativePath.Contains("/quests/", StringComparison.OrdinalIgnoreCase)
            && (label.Contains("настройки источников квестов", StringComparison.Ordinal)
                || label.Contains("quest givers setup", StringComparison.Ordinal))
            && supportsReferenceKey
            && supportsStructValue)
        {
            return (
                true,
                "quest-giver",
                "Найди, кто должен выдавать квесты: телефон, доска заданий или нужный торговец.");
        }

        return (false, null, null);
    }
    private ModAssetDescriptor DescribeModAsset(string relativePath, string categoryId)
    {
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var cleanStem = LocalizeAssetStem(stem);
        var lowerStem = stem.ToLowerInvariant();

        if (categoryId.Equals("crafting-recipes", StringComparison.OrdinalIgnoreCase))
        {
            if (IsCraftingUiDataRegistryAsset(relativePath))
            {
                return new ModAssetDescriptor(
                    "Крафт: общий пул рецептов",
                    "Главный список категорий и рецептов, которые видит игрок в меню крафта. Здесь можно добавлять или убирать рецепты по категориям.");
            }

            var baseName = stem.StartsWith("CR_", StringComparison.OrdinalIgnoreCase) ? stem[3..] : stem;
            return new ModAssetDescriptor(
                $"Рецепт: {LocalizeAssetStem(baseName)}",
                "Настройки рецепта: состав, параметры и требования крафта.");
        }

        if (categoryId.Equals("crafting-ingredients", StringComparison.OrdinalIgnoreCase))
        {
            var baseName = stem.StartsWith("CI_", StringComparison.OrdinalIgnoreCase) ? stem[3..] : stem;
            return new ModAssetDescriptor(
                $"Ингредиент: {LocalizeAssetStem(baseName)}",
                "Группы и правила использования ингредиента в рецептах.");
        }

        if (categoryId.Equals("body-effects", StringComparison.OrdinalIgnoreCase))
        {
            if (TryDescribeBodyEffectAsset(relativePath, out var bodyDescriptor))
            {
                return bodyDescriptor;
            }

            if (lowerStem.Contains("alcohol", StringComparison.Ordinal) || lowerStem.Contains("drunk", StringComparison.Ordinal) || lowerStem.Contains("intox", StringComparison.Ordinal))
            {
                return new ModAssetDescriptor(
                    cleanStem.Equals("Алкогольное опьянение", StringComparison.OrdinalIgnoreCase)
                        ? cleanStem
                        : $"Алкогольное опьянение: {cleanStem}",
                    "Эффекты опьянения, их тяжесть, длительность и влияние на персонажа.");
            }

            if (relativePath.Contains("/bodyeffects/symptoms/", StringComparison.OrdinalIgnoreCase))
            {
                return new ModAssetDescriptor(
                    cleanStem,
                    "Отдельный симптом, который обычно включается другими эффектами. Здесь чаще бывает только справочная или визуальная связь.");
            }

            if (relativePath.Contains("/bodyeffects/conditions/", StringComparison.OrdinalIgnoreCase))
            {
                return new ModAssetDescriptor(
                    cleanStem,
                    "Основное состояние персонажа: тяжесть эффекта, последствия и связанные симптомы.");
            }

            return new ModAssetDescriptor(
                cleanStem,
                "Параметры дебафа/бафа, усталости и поведения персонажа.");
        }

        if (categoryId.Equals("radiation", StringComparison.OrdinalIgnoreCase))
        {
            if (TryDescribeRadiationAsset(relativePath, out var radiationDescriptor))
            {
                return radiationDescriptor;
            }

            return new ModAssetDescriptor(
                $"Радиация: {cleanStem}",
                "Интенсивность, нарастание и связанные параметры радиации.");
        }

        if (categoryId.Equals("foreign-substances", StringComparison.OrdinalIgnoreCase))
        {
            if (!relativePath.Contains("/foreignsubstances/", StringComparison.OrdinalIgnoreCase))
            {
                return new ModAssetDescriptor(
                    $"Метаболизм: {cleanStem}",
                    "Общие правила обмена веществ, обработки доз и поведения метаболической системы.");
            }

            var baseName = stem.EndsWith("_FS", StringComparison.OrdinalIgnoreCase) ? stem[..^3] : stem;
            return new ModAssetDescriptor(
                $"Вещество: {LocalizeAssetStem(baseName)}",
                "Как вещество действует на персонажа, как быстро всасывается и выводится.");
        }

        if (categoryId.Equals("economy-trader", StringComparison.OrdinalIgnoreCase))
        {
            if (TryDescribeEconomyTraderAsset(relativePath, out var economyDescriptor))
            {
                return economyDescriptor;
            }

            return new ModAssetDescriptor(
                $"Экономика: {cleanStem}",
                "Цены, торговые коэффициенты и поведение трейдеров.");
        }

        if (categoryId.Equals("npc-encounters", StringComparison.OrdinalIgnoreCase))
        {
            if (TryDescribeNpcEncounterAsset(relativePath, out var npcDescriptor))
            {
                return npcDescriptor;
            }

            return new ModAssetDescriptor(
                cleanStem,
                "Появление врагов, состав орд, зоны событий и базовые параметры NPC.");
        }

        if (categoryId.Equals("starter-kit", StringComparison.OrdinalIgnoreCase))
        {
            if (TryDescribeStarterSpawnAsset(relativePath, out var starterDescriptor))
            {
                return starterDescriptor;
            }

            var baseName = stem.StartsWith("SpawnEquipment_", StringComparison.OrdinalIgnoreCase) ? stem["SpawnEquipment_".Length..] : stem;
            return new ModAssetDescriptor(
                $"Стартовая вещь: {LocalizeAssetStem(baseName)}",
                "Что игрок получит на старте, кому это можно выдать и при каких условиях.");
        }

        if (categoryId.Equals("seasonal-rewards", StringComparison.OrdinalIgnoreCase))
        {
            var seasonalName = stem.StartsWith("BP_", StringComparison.OrdinalIgnoreCase)
                ? stem[3..]
                : stem;
            return new ModAssetDescriptor(
                $"Сезонная награда: {LocalizeAssetStem(seasonalName)}",
                "Праздничный контейнер или ивентовая награда: её содержимое, состав и выдача.");
        }

        if (categoryId.Equals("weapons-items", StringComparison.OrdinalIgnoreCase))
        {
            if (TryDescribeAdvancedItemSpawnerPresetAsset(relativePath, out var advancedSpawnerDescriptor))
            {
                return advancedSpawnerDescriptor;
            }

            if (TryDescribeRegularItemSpawnerPresetAsset(relativePath, out var regularSpawnerDescriptor))
            {
                return regularSpawnerDescriptor;
            }

            if (TryDescribeExamineDataPresetAsset(relativePath, out var itemSpawnerDescriptor))
            {
                return itemSpawnerDescriptor;
            }

            if (relativePath.Contains("/ui/gameevents/itemselection/", StringComparison.OrdinalIgnoreCase))
            {
                return new ModAssetDescriptor(
                    $"Набор события: {cleanStem}",
                    "Главный предмет, дополнительные вещи и крепления для выбора в игровом событии.");
            }

            if (lowerStem.StartsWith("weaponmalfunctionprobability_", StringComparison.Ordinal)
                && relativePath.Contains("/data/weapon/malfunctionprobabilitycurves/", StringComparison.OrdinalIgnoreCase))
            {
                var curveName = stem["WeaponMalfunctionProbability_".Length..];
                return new ModAssetDescriptor(
                    $"Кривая отказов оружия: {LocalizeCompactGameplayName(curveName)}",
                    "Ключевые точки кривой отказов оружия. Здесь можно менять, как растёт риск осечки, плохого патрона или другой неисправности.");
            }

            if (lowerStem.StartsWith("weapon_", StringComparison.Ordinal))
            {
                return new ModAssetDescriptor(
                    $"Оружие: {LocalizeAssetStem(stem[7..])}",
                    "Урон, магазин, отдача, шум, вес и другие безопасные параметры оружия.");
            }

            if (lowerStem.StartsWith("magazine_", StringComparison.Ordinal) || lowerStem.EndsWith("_clip", StringComparison.Ordinal))
            {
                return new ModAssetDescriptor(
                    $"Магазин: {cleanStem}",
                    "Вместимость магазина и связанная конфигурация боеприпасов.");
            }

            return new ModAssetDescriptor(
                cleanStem,
                "Свойства предмета, которые можно безопасно менять в игре.");
        }

        if (categoryId.Equals("skills-progression", StringComparison.OrdinalIgnoreCase))
        {
            if (lowerStem.StartsWith("fc_", StringComparison.Ordinal))
            {
                return new ModAssetDescriptor(
                    $"Кривая навыка: {cleanStem}",
                    "Кривая бонуса навыка для взлома, тактики и похожих мини-игр.");
            }

            return new ModAssetDescriptor(
                $"Навык: {cleanStem}",
                "Скорость прокачки и бонусы навыка на разных уровнях.");
        }

        if (categoryId.Equals("quests", StringComparison.OrdinalIgnoreCase))
        {
            if (TryDescribeQuestAsset(relativePath, out var questDescriptor))
            {
                return questDescriptor;
            }

            return new ModAssetDescriptor(
                $"Квест: {cleanStem}",
                "Квестовые шаги, объекты взаимодействия и служебные условия задания.");
        }

        if (categoryId.Equals("locks-base", StringComparison.OrdinalIgnoreCase))
        {
            if (TryDescribeLockBaseAsset(relativePath, out var lockDescriptor))
            {
                return lockDescriptor;
            }

            return new ModAssetDescriptor(
                cleanStem,
                "Время, попытки и правила для замков, взлома и защиты базы.");
        }

        if (categoryId.Equals("fishing-spawn", StringComparison.OrdinalIgnoreCase))
        {
            if (TryDescribeFishingAsset(relativePath, out var fishingDescriptor))
            {
                return fishingDescriptor;
            }

            return new ModAssetDescriptor(
                cleanStem,
                "Какая рыба появляется, насколько часто и в каких условиях.");
        }

        if (categoryId.Equals("vehicles", StringComparison.OrdinalIgnoreCase))
        {
            if (TryDescribeVehicleAsset(relativePath, out var vehicleDescriptor))
            {
                return vehicleDescriptor;
            }

            return new ModAssetDescriptor(
                $"Транспорт: {cleanStem}",
                "Столкновения, урон по машине и последствия аварий для транспорта.");
        }

        if (categoryId.Equals("plants-farming", StringComparison.OrdinalIgnoreCase))
        {
            if (TryDescribeFarmingAsset(relativePath, out var farmingDescriptor))
            {
                return farmingDescriptor;
            }

            return new ModAssetDescriptor(
                $"Фермерство: {cleanStem}",
                "Растения, вредители и болезни, которые используются в системе фермерства.");
        }

        return new ModAssetDescriptor(cleanStem, ResolveCategoryDescription(categoryId));
    }

    private bool TryDescribeStarterSpawnAsset(string relativePath, out ModAssetDescriptor descriptor)
    {
        descriptor = null!;
        var warnings = new List<string>(2);
        if (!TryResolveGameAssetSource(relativePath, out var sourcePath, warnings, includeCompanions: true)
            || string.IsNullOrWhiteSpace(sourcePath)
            || !File.Exists(sourcePath))
        {
            return false;
        }

        try
        {
            var readableSourcePath = PrepareIsolatedAssetReadSource(sourcePath);
            var asset = new UAsset(readableSourcePath, EngineVersion.VER_UE4_27, null, CustomSerializationFlags.None);
            if (!TryGetFirstNormalExport(asset, out var normalExport, out _))
            {
                return false;
            }

            var itemClass = FindTopLevelProperty<SoftObjectPropertyData>(normalExport, "ItemClass", out _);
            if (itemClass is null)
            {
                return false;
            }

            var itemName = ResolveReadableItemName(ExtractSoftObjectReference(itemClass.Value));
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return false;
            }

            descriptor = new ModAssetDescriptor(
                $"Стартовая вещь: {itemName}",
                "Что игрок получит на старте, кому это можно выдать и при каких условиях.");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryDescribeCraftingRecipeAsset(string relativePath, out ModAssetDescriptor descriptor)
    {
        descriptor = null!;
        var warnings = new List<string>(2);
        if (!TryResolveGameAssetSource(relativePath, out var sourcePath, warnings, includeCompanions: true)
            || string.IsNullOrWhiteSpace(sourcePath)
            || !File.Exists(sourcePath))
        {
            return false;
        }

        try
        {
            var readableSourcePath = PrepareIsolatedAssetReadSource(sourcePath);
            var asset = new UAsset(readableSourcePath, EngineVersion.VER_UE4_27, null, CustomSerializationFlags.None);
            if (!TryGetFirstNormalExport(asset, out var normalExport, out _))
            {
                return false;
            }

            var product = FindTopLevelProperty<SoftObjectPropertyData>(normalExport, "Product", out _);
            if (product is null)
            {
                return false;
            }

            var itemName = ResolveReadableItemName(ExtractSoftObjectReference(product.Value));
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return false;
            }

            descriptor = new ModAssetDescriptor(
                $"Рецепт: {itemName}",
                "Что создаётся, из чего собирается и как ведёт себя этот рецепт.");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDescribeBodyEffectAsset(string relativePath, out ModAssetDescriptor descriptor)
    {
        descriptor = null!;
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        if (!TryResolveBodyEffectDisplayName(stem, out var displayName))
        {
            return false;
        }

        var summary = relativePath.Contains("/bodyeffects/symptoms/", StringComparison.OrdinalIgnoreCase)
            ? "Отдельный симптом, который обычно включается другими эффектами. Здесь чаще бывает только справочная или визуальная связь."
            : relativePath.Contains("/bodyeffects/conditions/", StringComparison.OrdinalIgnoreCase)
                ? "Основное состояние персонажа: тяжесть эффекта, последствия и связанные симптомы."
                : "Параметры дебафа/бафа, усталости и поведения персонажа.";

        descriptor = new ModAssetDescriptor(displayName, summary);
        return true;
    }

    private static bool TryResolveBodyEffectDisplayName(string stem, out string displayName)
    {
        displayName = string.Empty;
        if (string.IsNullOrWhiteSpace(stem))
        {
            return false;
        }

        var localizedStem = LocalizeAssetStem(stem);
        var humanizedStem = HumanizeAssetStem(stem);
        if (!string.Equals(NormalizeLooseSearch(localizedStem), NormalizeLooseSearch(humanizedStem), StringComparison.Ordinal))
        {
            displayName = localizedStem;
            return true;
        }

        var compact = NormalizeAssetKey(stem);
        if (compact.EndsWith("poisoning", StringComparison.Ordinal))
        {
            var sourceKey = compact[..^"poisoning".Length];
            var sourceName = LocalizeAssetStem(sourceKey);
            displayName = $"Отравление: {sourceName}";
            return true;
        }

        if (compact.EndsWith("overdose", StringComparison.Ordinal))
        {
            var sourceKey = compact[..^"overdose".Length];
            var sourceName = LocalizeAssetStem(sourceKey);
            displayName = $"Передозировка: {sourceName}";
            return true;
        }

        if (compact.Contains("applybandagesordisinfect", StringComparison.Ordinal))
        {
            var baseName = ResolveBodyEffectBaseName(compact, "applybandagesordisinfect");
            displayName = $"{baseName}: перевязка и обеззараживание";
            return true;
        }

        if (compact.Contains("removebandages", StringComparison.Ordinal))
        {
            var baseName = ResolveBodyEffectBaseName(compact, "removebandages");
            displayName = $"{baseName}: снять повязку";
            return true;
        }

        if (compact.Contains("applygel", StringComparison.Ordinal))
        {
            var baseName = ResolveBodyEffectBaseName(compact, "applygel");
            displayName = $"{baseName}: нанести лечебный гель";
            return true;
        }

        if (compact.Contains("spawnillusionsafter1s", StringComparison.Ordinal))
        {
            displayName = "Приступ галлюцинаций: запуск иллюзий";
            return true;
        }

        if (compact.Contains("changeconsumption", StringComparison.Ordinal))
        {
            displayName = "Истощение: расход ресурсов";
            return true;
        }

        if (compact.Contains("changeresting", StringComparison.Ordinal))
        {
            displayName = "Истощение: восстановление во время отдыха";
            return true;
        }

        if (compact.Contains("changesmoking", StringComparison.Ordinal))
        {
            displayName = "Истощение: влияние во время курения";
            return true;
        }

        if (compact.EndsWith("controller", StringComparison.Ordinal))
        {
            var baseName = LocalizeAssetStem(compact[..^"controller".Length]);
            displayName = $"Контроллер эффекта: {baseName}";
            return true;
        }

        if (compact.Contains("eyeirritation", StringComparison.Ordinal))
        {
            var stage = compact.EndsWith("mild", StringComparison.Ordinal)
                ? "лёгкая стадия"
                : compact.EndsWith("medium", StringComparison.Ordinal)
                    || stem.Contains("сред", StringComparison.OrdinalIgnoreCase)
                    ? "средняя стадия"
                    : compact.EndsWith("strong", StringComparison.Ordinal)
                        ? "тяжёлая стадия"
                        : "стадии";
            displayName = $"Раздражение глаз: {stage}";
            return true;
        }

        if (compact.StartsWith("burninjury", StringComparison.Ordinal))
        {
            var bodyPart = ResolveBodyPartLabel(compact);
            displayName = string.IsNullOrWhiteSpace(bodyPart)
                ? "Ожог"
                : $"Ожог: {bodyPart}";
            return true;
        }

        if (compact.StartsWith("infection", StringComparison.Ordinal))
        {
            var bodyPart = ResolveBodyPartLabel(compact);
            displayName = string.IsNullOrWhiteSpace(bodyPart)
                ? "Инфекция"
                : $"Инфекция: {bodyPart}";
            return true;
        }

        if (compact.StartsWith("defaultmusclegroup", StringComparison.Ordinal) && compact.Contains("burninjury", StringComparison.Ordinal))
        {
            displayName = "Связь мышечных групп и ожогов";
            return true;
        }

        if (compact.StartsWith("defaultmusclegroup", StringComparison.Ordinal) && compact.Contains("infection", StringComparison.Ordinal))
        {
            displayName = "Связь мышечных групп и инфекций";
            return true;
        }

        return false;
    }

    private static string ResolveBodyEffectBaseName(string compactStem, string suffix)
    {
        var normalized = compactStem.Replace(suffix, string.Empty, StringComparison.Ordinal);
        var bodyPart = ResolveBodyPartLabel(normalized);
        normalized = normalized
            .Replace("abdomen", string.Empty, StringComparison.Ordinal)
            .Replace("arms", string.Empty, StringComparison.Ordinal)
            .Replace("chest", string.Empty, StringComparison.Ordinal)
            .Replace("feet", string.Empty, StringComparison.Ordinal)
            .Replace("hands", string.Empty, StringComparison.Ordinal)
            .Replace("head", string.Empty, StringComparison.Ordinal)
            .Replace("legs", string.Empty, StringComparison.Ordinal);

        var baseName = LocalizeAssetStem(normalized);
        if (!string.IsNullOrWhiteSpace(bodyPart))
        {
            return $"{baseName}: {bodyPart}";
        }

        return baseName;
    }

    private static string ResolveBodyPartLabel(string compactStem)
    {
        if (compactStem.Contains("abdomen", StringComparison.Ordinal))
        {
            return "живот";
        }

        if (compactStem.Contains("arms", StringComparison.Ordinal))
        {
            return "руки";
        }

        if (compactStem.Contains("chest", StringComparison.Ordinal))
        {
            return "грудь";
        }

        if (compactStem.Contains("feet", StringComparison.Ordinal))
        {
            return "ступни";
        }

        if (compactStem.Contains("hands", StringComparison.Ordinal))
        {
            return "кисти";
        }

        if (compactStem.Contains("head", StringComparison.Ordinal))
        {
            return "голова";
        }

        if (compactStem.Contains("legs", StringComparison.Ordinal))
        {
            return "ноги";
        }

        return string.Empty;
    }

    private static bool TryDescribeQuestAsset(string relativePath, out ModAssetDescriptor descriptor)
    {
        descriptor = null!;
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        if (stem.StartsWith("BP_ActionMatcher_", StringComparison.OrdinalIgnoreCase))
        {
            var action = stem["BP_ActionMatcher_".Length..];
            descriptor = new ModAssetDescriptor(
                $"Квестовое действие: {LocalizeCompactGameplayName(action)}",
                "Шаг задания, который отслеживает конкретное действие игрока.");
            return true;
        }

        if (stem.StartsWith("BP_QuestInteractableSphere_", StringComparison.OrdinalIgnoreCase))
        {
            var target = stem["BP_QuestInteractableSphere_".Length..];
            descriptor = new ModAssetDescriptor(
                $"Квестовый объект: {LocalizeCompactGameplayName(target)}",
                "Точка взаимодействия в мире, которую использует квест.");
            return true;
        }

        if (stem.Equals("BP_QuestInteractableDynamic", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                "Квестовый динамический объект",
                "Общий шаблон квестового объекта, который появляется и меняется во время задания.");
            return true;
        }

        if (stem.Equals("BP_NoticeBoard", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                "Доска заданий",
                "Общий объект, через который игрок может получать задания.");
            return true;
        }

        if (stem.Equals("BP_QuestManager", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                "Менеджер квестов",
                "Главный системный объект, который управляет выдачей и ходом заданий.");
            return true;
        }

        if (stem.Equals("QuestCommonData", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                "Общие данные квестов",
                "Глобальные таблицы и настройки, которыми пользуются разные задания.");
            return true;
        }

        if (stem.Equals("QuestMangerData", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                "Данные менеджера квестов",
                "Служебные данные менеджера квестов и их внутренних связей.");
            return true;
        }

        if (stem.StartsWith("BP_QuestBin", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = stem.Equals("BP_QuestBin_MailBox", StringComparison.OrdinalIgnoreCase)
                ? "почтовый ящик"
                : "контейнер";
            descriptor = new ModAssetDescriptor(
                $"Квестовый {suffix}",
                "Объект для передачи, сдачи или хранения квестовых предметов.");
            return true;
        }

        if (stem.StartsWith("Quest", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(stem, @"^T\d+_", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            descriptor = new ModAssetDescriptor(
                BuildQuestDisplayName(stem),
                "Отдельное задание, цель или шаг в квестовой цепочке.");
            return true;
        }

        return false;
    }

    private static string BuildQuestDisplayName(string stem)
    {
        if (string.IsNullOrWhiteSpace(stem))
        {
            return "Квест";
        }

        var tierQuestMatch = Regex.Match(
            stem,
            @"^T(?<tier>\d+)_(?<giver>[A-Za-z]+)_(?<objective>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (tierQuestMatch.Success)
        {
            var tier = tierQuestMatch.Groups["tier"].Value;
            var giver = ResolveQuestGiverLabel(tierQuestMatch.Groups["giver"].Value);
            var objective = LocalizeQuestObjectiveName(tierQuestMatch.Groups["objective"].Value);
            return $"Квест: уровень {tier} / {giver} / {objective}";
        }

        var tutorialTaskMatch = Regex.Match(
            stem,
            @"^Task[_ ]?(?<step>\d+)[_ ]?(?<objective>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (tutorialTaskMatch.Success)
        {
            var step = tutorialTaskMatch.Groups["step"].Value;
            var objective = LocalizeQuestObjectiveName(tutorialTaskMatch.Groups["objective"].Value);
            return $"Обучение: шаг {step} / {objective}";
        }

        var clean = stem.StartsWith("Quest_", StringComparison.OrdinalIgnoreCase)
            ? stem["Quest_".Length..]
            : stem;
        return $"Квест: {LocalizeQuestObjectiveName(clean)}";
    }

    private static string ResolveQuestGiverLabel(string rawCode)
    {
        var normalized = (rawCode ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "ar" => "оружейник",
            "dc" => "доктор",
            "gg" => "общие товары",
            "mc" => "механик",
            "bm" => "лодочный торговец",
            "me" => "механик",
            "bb" => "бармен",
            "hb" => "парикмахер",
            _ => LocalizeCompactGameplayName(rawCode ?? string.Empty)
        };
    }

    private static string LocalizeQuestObjectiveName(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return "без названия";
        }

        var value = LocalizeCompactGameplayName(rawValue)
            .Replace("найти a ", "найти ", StringComparison.OrdinalIgnoreCase)
            .Replace("найти an ", "найти ", StringComparison.OrdinalIgnoreCase)
            .Replace("найти the ", "найти ", StringComparison.OrdinalIgnoreCase)
            .Replace("принести a ", "принести ", StringComparison.OrdinalIgnoreCase)
            .Replace("принести an ", "принести ", StringComparison.OrdinalIgnoreCase)
            .Replace("принести the ", "принести ", StringComparison.OrdinalIgnoreCase)
            .Replace("взаимодействовать с a ", "взаимодействовать с ", StringComparison.OrdinalIgnoreCase)
            .Replace("взаимодействовать с an ", "взаимодействовать с ", StringComparison.OrdinalIgnoreCase)
            .Replace("взаимодействовать с the ", "взаимодействовать с ", StringComparison.OrdinalIgnoreCase)
            .Replace("взаимодействовать с отключить телевизоры", "отключить телевизоры", StringComparison.OrdinalIgnoreCase);

        value = Regex.Replace(
            value,
            @"\b(a|an|the)\b",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return CapitalizeFirst(NormalizeLocalizedLabel(value));
    }

    private static bool TryDescribeLockBaseAsset(string relativePath, out ModAssetDescriptor descriptor)
    {
        descriptor = null!;
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var lowerStem = stem.ToLowerInvariant();

        if (lowerStem.StartsWith("baseexpansionkit", StringComparison.Ordinal))
        {
            var levelMatch = Regex.Match(lowerStem, @"lvl\s*(\d+)");
            var level = levelMatch.Success ? levelMatch.Groups[1].Value : "?";
            descriptor = new ModAssetDescriptor(
                $"Расширение базы: уровень {level}",
                "Требования и параметры улучшения базы на выбранном уровне.");
            return true;
        }

        if (lowerStem.Contains("diallocksetcombination", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Замок: установка комбинации",
                "Правила и параметры выбора комбинации для кодового замка.");
            return true;
        }

        if (lowerStem.Contains("diallockminigame", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Замок: дисковый взлом",
                "Скорость, окно ошибки и другие параметры мини-игры дискового замка.");
            return true;
        }

        if (lowerStem.Contains("lockbombdefusal", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Замок: обезвреживание взрывчатки",
                "Параметры мини-игры обезвреживания ловушки или взрывного замка.");
            return true;
        }

        if (lowerStem.Contains("lockpicking", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Взлом замка",
                "Время, окно успеха и ощущения от мини-игры взлома замка.");
            return true;
        }

        if (lowerStem.Contains("explosionfailurepenalty", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Наказание за неудачный взрыв",
                "Что получает игрок при ошибке во время силового взлома.");
            return true;
        }

        if (lowerStem.Contains("triggerfailurepenalty", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Наказание за ошибку триггера",
                "Штрафы и последствия при неудачном срабатывании защитного механизма.");
            return true;
        }

        if (lowerStem.Contains("shocker", StringComparison.Ordinal))
        {
            var size = lowerStem.Contains("killbox", StringComparison.Ordinal)
                ? "killbox"
                : lowerStem.Contains("big", StringComparison.Ordinal)
                    ? "большой"
                    : lowerStem.Contains("medium", StringComparison.Ordinal)
                        ? "средний"
                        : "малый";

            descriptor = new ModAssetDescriptor(
                $"Защита замка: шокер ({size})",
                "Настройки электрической защиты замка и её силы.");
            return true;
        }

        if (lowerStem.Contains("screwdriverdata", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Инструмент взлома: отвёртка",
                "Параметры отвёртки, которые влияют на мини-игру взлома.");
            return true;
        }

        return false;
    }

    private static string LocalizeCompactGameplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "без названия";
        }

        var text = value
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim();

        var compactReplacements = new (string From, string To)[]
        {
            ("questcommondata", "quest common data"),
            ("questmangerdata", "quest manager data"),
            ("generalgoods", "general goods"),
            ("ammobox", " ammo box"),
            ("activatedcharcoal", "activated charcoal"),
            ("potassiumiodide", "potassium iodide"),
            ("painkillers", "painkillers"),
            ("vitaminpills", "vitamin pills"),
            ("medicalclothes", "medical clothes"),
            ("medicalgloves", "medical gloves"),
            ("disposablemasks", "disposable masks"),
            ("emergencybandage", "emergency bandage"),
            ("multiplepuppetparts", "multiple puppet parts"),
            ("tonguedepressors", "tongue depressors"),
            ("telephonebooths", "telephone booths"),
            ("sabotageacs", "sabotage acs"),
            ("disabletvs", "disable tvs"),
            ("churches", "churches"),
            ("fountains", "fountains"),
            ("aeroplanerepairkit", "aeroplane repair kit"),
            ("brakeoil", "brake oil"),
            ("carjack", "car jack"),
            ("carbatterycables", "car battery cables"),
            ("carrepairkit", "car repair kit"),
            ("gasolinecanistersmall", "gasoline canister small"),
            ("metalscraps", "metal scraps"),
            ("oilfilter", "oil filter"),
            ("wrenchpipe", "pipe wrench"),
            ("boxerbriefs", "boxer briefs"),
            ("cannedsardine", "canned sardine"),
            ("hightopshoes", "high top shoes"),
            ("woolgloves", "wool gloves"),
            ("militarybeanie", "military beanie"),
            ("kitchenknife", "kitchen knife"),
            ("handgun", "handgun"),
            ("sock", "sock"),
            ("brasupporter", "bra supporter"),
            ("fishingreelpro", "fishing reel pro"),
            ("improvisedfishingreel", "improvised fishing reel"),
            ("premiumboiliespack", "premium boilies pack"),
            ("ducttape", "duct tape"),
            ("bobbypins", "bobby pins"),
            ("fishingline", "fishing line"),
            ("fishinghook", "fishing hook"),
            ("fishingrod", "fishing rod"),
            ("fishingfloater", "fishing floater"),
            ("fishingbait", "fishing bait"),
            ("fishingreel", "fishing reel"),
            ("fishinglinepiece", "fishing line piece"),
            ("fishinghookpack", "fishing hook pack"),
            ("fishingfloaterpack", "fishing floater pack"),
            ("craftstoneknife", "craft stone knife"),
            ("craftcourierbackpack", "craft courier backpack"),
            ("craftbowandarrows", "craft bow and arrows"),
            ("findingfoodandwater", "finding food and water"),
            ("urinationanddefecation", "urination and defecation"),
            ("lightfire", "light fire"),
            ("famemoney", "fame money"),
            ("craft", "craft "),
            ("chop", "chop "),
            ("cut", "cut "),
            ("eat", "eat "),
            ("fuel", "fuel "),
            ("light", "light "),
            ("search", "search "),
            ("worldobject", "world object"),
            ("woodenspear", "wooden spear"),
            ("woodenarrows", "wooden arrows"),
            ("stoneknife", "stone knife"),
            ("firedrill", "fire drill"),
            ("treebarkrope", "tree bark rope"),
            ("improvisedbow", "improvised bow"),
            ("improvisedcourierbackpack", "improvised courier backpack"),
            ("underpants", "underpants"),
            ("undershirt", "undershirt"),
            ("bodybag2", "bodybag 2"),
            ("camera2", "camera 2"),
            ("findaphone", "find a phone"),
            ("findanoutpost", "find an outpost"),
            ("weaponflashlightm9", "weapon flashlight m9"),
            ("puppetsblunt", "puppets blunt"),
            ("puppetsbows", "puppets bows")
        };

        foreach (var replacement in compactReplacements)
        {
            text = ReplaceSemanticLabelPart(text, replacement.From, replacement.To);
        }

        text = HumanizeCamel(text);
        text = LocalizeCommonGameplayTerms(text);
        text = NormalizeLocalizedLabel(text);
        return CapitalizeFirst(text);
    }

    private static bool TryDescribeEconomyTraderAsset(string relativePath, out ModAssetDescriptor descriptor)
    {
        descriptor = null!;
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var path = relativePath.ToLowerInvariant();

        if (path.Contains("/traderpersonalities/", StringComparison.Ordinal))
        {
            var outpostMatch = Regex.Match(stem, @"outpost_([a-z]_\d)");
            var outpostLabel = outpostMatch.Success
                ? $"аванпост {outpostMatch.Groups[1].Value.Replace('_', '-').ToUpperInvariant()}"
                : "аванпост";

            var role = "торговец";
            if (stem.Contains("_armory_", StringComparison.OrdinalIgnoreCase))
            {
                role = "оружейник";
            }
            else if (stem.Contains("_mechanic_", StringComparison.OrdinalIgnoreCase))
            {
                role = "механик";
            }
            else if (stem.Contains("_doctor_", StringComparison.OrdinalIgnoreCase))
            {
                role = "доктор";
            }
            else if (stem.Contains("_boat_shop_", StringComparison.OrdinalIgnoreCase))
            {
                role = "лодочный торговец";
            }
            else if (stem.Contains("_barber_", StringComparison.OrdinalIgnoreCase))
            {
                role = "парикмахер";
            }
            else if (stem.Contains("_barmen_", StringComparison.OrdinalIgnoreCase))
            {
                role = "бармен";
            }
            else if (stem.Contains("_traderpersonality_", StringComparison.OrdinalIgnoreCase))
            {
                role = "общие товары";
            }

            descriptor = new ModAssetDescriptor(
                $"Торговец: {CapitalizeFirst(role)}, {outpostLabel}",
                "Тип и поведение конкретного торговца в одном из аванпостов.");
            return true;
        }

        if (path.Contains("/outpostdescriptions/", StringComparison.Ordinal))
        {
            var zone = stem.Replace("_tradeoutpostdescription", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace('_', '-')
                .ToUpperInvariant();
            descriptor = new ModAssetDescriptor(
                $"Аванпост: {zone}",
                "Описание и базовые параметры конкретного торгового аванпоста.");
            return true;
        }

        if (stem.Equals("BP_EconomyManager", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                "Глобальная экономика",
                "Общие уровни процветания, редкие товары и правила для всей экономики сервера.");
            return true;
        }

        if (stem.Equals("BP_TradeOutpostManager", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                "Менеджер торговых аванпостов",
                "Глобальное управление торговыми точками и их связями.");
            return true;
        }

        if (stem.Equals("DurabilityVsPriceMultiplierCurve", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                "Цена предмета по прочности",
                "Кривая, которая меняет стоимость предмета в зависимости от его износа.");
            return true;
        }

        if (path.Contains("/traderservices/", StringComparison.Ordinal))
        {
            if (stem.Equals("BP_BCUUpgradeService", StringComparison.OrdinalIgnoreCase))
            {
                descriptor = new ModAssetDescriptor(
                    "Сервис: улучшение BCU",
                    "Условия и базовые правила услуги улучшения BCU у торговцев.");
                return true;
            }

            if (stem.Equals("BP_HaircutAndMakeupService", StringComparison.OrdinalIgnoreCase))
            {
                descriptor = new ModAssetDescriptor(
                    "Сервис: стрижка и макияж",
                    "Правила услуги смены внешности у парикмахера.");
                return true;
            }

            if (stem.Equals("BP_PlasticSurgeryService", StringComparison.OrdinalIgnoreCase))
            {
                descriptor = new ModAssetDescriptor(
                    "Сервис: пластическая хирургия",
                    "Правила и ограничения услуги полной смены внешности.");
                return true;
            }
        }

        return false;
    }

    private static bool TryDescribeFishingAsset(string relativePath, out ModAssetDescriptor descriptor)
    {
        descriptor = null!;
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var lowerStem = stem.ToLowerInvariant();
        var cleanStem = LocalizeAssetStem(stem);
        var path = relativePath.ToLowerInvariant();

        if (stem.StartsWith("SpeciesPreset_", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                $"Пресет видов рыбы: {FormatFishingPresetName(stem["SpeciesPreset_".Length..])}",
                "Какие виды рыбы могут появляться в этом типе водоёма и с каким весом они выбираются.");
            return true;
        }

        if (stem.StartsWith("AquaSpawningPreset_", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                $"Частота появления рыбы: {FormatFishingPresetName(stem["AquaSpawningPreset_".Length..])}",
                "Насколько часто этот пресет участвует в общем выборе появления рыбы.");
            return true;
        }

        if (path.Contains("/characters/spawnerpresets/", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                $"Спавн рыбы: {cleanStem}",
                "Какая рыба может появляться в этом пресете водоёма и с какой частотой.");
            return true;
        }

        if (lowerStem.Contains("fishingbait", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                $"Наживка: {cleanStem.Replace("Fishingbait", string.Empty, StringComparison.OrdinalIgnoreCase).Trim()}".Trim(),
                "Параметры рыболовной наживки и её игровая роль в рыбалке.");
            return true;
        }

        if (lowerStem.Contains("fishinghook", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                $"Крючок: {cleanStem}",
                "Параметры рыболовного крючка и связанных снастей.");
            return true;
        }

        if (lowerStem.Contains("fishingline", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                $"Леска: {cleanStem}",
                "Параметры лески и её прочности в рыболовной системе.");
            return true;
        }

        if (lowerStem.Contains("fishingreel", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                $"Катушка: {cleanStem}",
                "Параметры катушки, натяжения лески и связанных рыболовных снастей.");
            return true;
        }

        if (lowerStem.Contains("fishingrod", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                $"Удилище: {cleanStem}",
                "Параметры удилища, снастей и связанных креплений.");
            return true;
        }

        if (lowerStem.Contains("boilies", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                $"Наживка: {cleanStem}",
                "Параметры готовой рыболовной наживки и её привлекательности для рыбы.");
            return true;
        }

        if (lowerStem.Contains("floater", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                $"Поплавок: {cleanStem}",
                "Параметры поплавка и связанных рыболовных снастей.");
            return true;
        }

        if (lowerStem.Contains("wirecomponent", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Рыбалка: базовый компонент лески",
                "Служебный шаблон рыболовной лески и связанной оснастки.");
            return true;
        }

        if (lowerStem.Contains("trophyactor", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Рыбалка: трофейный улов",
                "Служебный шаблон для показа и обработки трофейной рыбы.");
            return true;
        }

        return false;
    }

    private static bool TryDescribeRegularItemSpawnerPresetAsset(string relativePath, out ModAssetDescriptor descriptor)
    {
        descriptor = null!;
        if (!IsRegularItemSpawnerPresetAsset(relativePath))
        {
            return false;
        }

        var segments = relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var spawnerIndex = Array.FindIndex(segments, segment => segment.Equals("spawnerpresets", StringComparison.OrdinalIgnoreCase));
        var folder = spawnerIndex >= 0 && spawnerIndex + 2 < segments.Length
            ? segments[spawnerIndex + 1].ToLowerInvariant()
            : string.Empty;
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var displayName = LocalizeAssetStem(stem);
        var context = folder switch
        {
            "house" => "дом",
            "medical_lab" => "медицинская лаборатория",
            "military" => "военный лут",
            "police" => "полиция",
            "office" => "офис",
            "stable" => "ферма и стойла",
            "market" => "рынок",
            "workshop_warehouse" => "мастерская и склад",
            _ => string.Empty
        };

        descriptor = string.IsNullOrWhiteSpace(context)
            ? new ModAssetDescriptor(
                $"Пресет появления предметов: {displayName}",
                "Какие предметы создаёт этот обычный спавнер, как часто он срабатывает и в каком состоянии появляются вещи.")
            : new ModAssetDescriptor(
                $"Пресет появления предметов: {context} / {displayName}",
                "Какие предметы создаёт этот обычный спавнер, как часто он срабатывает и в каком состоянии появляются вещи.");
        return true;
    }

    private static bool TryDescribeAdvancedItemSpawnerPresetAsset(string relativePath, out ModAssetDescriptor descriptor)
    {
        descriptor = null!;
        if (!IsAdvancedItemSpawnerPresetAsset(relativePath))
        {
            return false;
        }

        if (IsCargoDropPackagePresetAsset(relativePath))
        {
            var cargoDisplayName = ResolveCargoDropPackagePresetDisplayName(relativePath);
            descriptor = new ModAssetDescriptor(
                $"Контейнерный набор дропа: {cargoDisplayName}",
                "Готовый набор предметов для контейнера грузового дропа: какие вещи входят внутрь, можно ли повторять одинаковые предметы и сколько штук брать за один выбор.");
            return true;
        }

        var displayName = ResolveAdvancedItemSpawnerPresetDisplayName(relativePath);
        descriptor = new ModAssetDescriptor(
            $"Контейнерный пресет: {displayName}",
            "Готовый пресет лута для ящика, шкафа, коробки или другой точки поиска. Здесь можно менять шанс срабатывания, количество выдачи и состояние предметов.");
        return true;
    }

    private static bool TryDescribeExamineDataPresetAsset(string relativePath, out ModAssetDescriptor descriptor)
    {
        descriptor = null!;
        var path = relativePath.ToLowerInvariant();
        if (!path.Contains("/items/spawnerpresets/examine_data_presets/", StringComparison.Ordinal))
        {
            return false;
        }

        var segments = relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var folder = segments.Length > 1 ? segments[^2].ToLowerInvariant() : string.Empty;
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var name = stem.StartsWith("EX_", StringComparison.OrdinalIgnoreCase) ? stem[3..] : stem;
        if (name.EndsWith("_CargoDrop", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^"_CargoDrop".Length];
        }

        if (folder.Equals("house", StringComparison.OrdinalIgnoreCase) && name.StartsWith("House_", StringComparison.OrdinalIgnoreCase))
        {
            name = name["House_".Length..];
        }

        if (folder.Equals("hunting", StringComparison.OrdinalIgnoreCase) && name.StartsWith("Hunting_", StringComparison.OrdinalIgnoreCase))
        {
            name = name["Hunting_".Length..];
        }

        if (folder.Equals("market", StringComparison.OrdinalIgnoreCase) && name.StartsWith("Market_", StringComparison.OrdinalIgnoreCase))
        {
            name = name["Market_".Length..];
        }

        if (folder.Equals("military", StringComparison.OrdinalIgnoreCase) && name.StartsWith("Military_", StringComparison.OrdinalIgnoreCase))
        {
            name = name["Military_".Length..];
        }

        var context = folder switch
        {
            "cargo" => "грузовой дроп",
            "house" => "дом и бытовой лут",
            "hunting" => "охотничий лут",
            "market" => "рынок",
            "medical_lab" => "медицинский контейнер",
            "military" => "военный лут",
            _ => "контейнерный лут"
        };

        var cleanName = CapitalizeFirst(
            NormalizeLocalizedLabel(
                LocalizeCommonGameplayTerms(
                    HumanizeCamel(name.Replace('_', ' ')))));

        descriptor = new ModAssetDescriptor(
            $"Набор предметов: {context} / {cleanName}",
            "Какие предметы может выдать этот контейнер, тайник или грузовой дроп и по каким простым правилам они выбираются.");
        return true;
    }

    private static string FormatFishingPresetName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return "без названия";
        }

        var parts = rawName
            .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(LocalizeFishingPresetPart)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        if (parts.Length == 0)
        {
            return LocalizeAssetStem(rawName);
        }

        return string.Join(" / ", parts);
    }

    private static bool TryDescribeFarmingAsset(string relativePath, out ModAssetDescriptor descriptor)
    {
        descriptor = null!;
        var stem = Path.GetFileNameWithoutExtension(relativePath);

        if (stem.Equals("DA_PlantSpeciesList", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                "Список видов растений",
                "Какие культуры вообще доступны системе фермерства. Можно добавлять новые виды растений или убирать лишние.");
            return true;
        }

        if (stem.StartsWith("DA_PlantSpecies_", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                $"Вид растения: {ResolvePlantDisplayName(stem, "DA_PlantSpecies_")}",
                "Температура, пакет семян, скорость роста, финальная стадия, вредители и болезни конкретной культуры.");
            return true;
        }

        if (stem.StartsWith("DA_PlantPest_", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                $"Вредитель: {ResolvePlantDisplayName(stem, "DA_PlantPest_")}",
                "Отдельный вредитель фермерства и его штраф к урожаю.");
            return true;
        }

        if (stem.StartsWith("DA_PlantDisease_", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                $"Болезнь: {ResolvePlantDisplayName(stem, "DA_PlantDisease_")}",
                "Отдельная болезнь растений и её влияние на урожай.");
            return true;
        }

        return false;
    }

    private static string LocalizeFishingPresetPart(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "lake" => "озеро",
            "river" => "река",
            "sea" => "море",
            "deep" => "глубина",
            "shallow" => "мелководье",
            "big" => "крупная рыба",
            "small" => "мелкая рыба",
            "low" => "низкая",
            "mid" => "средняя",
            "high" => "высокая",
            "veryhigh" => "очень высокая",
            _ => LocalizeAssetStem(value)
        };
    }

    private static bool TryDescribeVehicleAsset(string relativePath, out ModAssetDescriptor descriptor)
    {
        descriptor = null!;
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var lowerStem = stem.ToLowerInvariant();
        var cleanStem = LocalizeAssetStem(stem);
        var path = relativePath.ToLowerInvariant();

        if (path.Contains("/vehicles/spawningpresets/spawngroups/", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                $"Группа спавна транспорта: {ResolveVehicleSpawnGroupDisplayName(stem)}",
                "Какие пресеты транспорта может выбирать эта группа автоматического спавна.");
            return true;
        }

        if (path.Contains("/vehicles/spawningpresets/automaticspawn/", StringComparison.Ordinal)
            && lowerStem.Contains("spawnpreset", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                $"Пресет транспорта: {ResolveVehicleSpawnPresetOptionDisplayName(relativePath)}",
                "Готовый пресет транспорта для автоматического появления: состав узлов, состояние и шанс выбора.");
            return true;
        }

        if (lowerStem.Contains("collisiondamagepropagationcurve", StringComparison.Ordinal))
        {
            var part = lowerStem.Contains("bumper", StringComparison.Ordinal)
                ? "бампера"
                : lowerStem.Contains("door", StringComparison.Ordinal)
                    ? "двери"
                    : lowerStem.Contains("wheel", StringComparison.Ordinal)
                        ? "колёс"
                        : lowerStem.Contains("body", StringComparison.Ordinal)
                            ? "кузова"
                            : lowerStem.Contains("centerfront", StringComparison.Ordinal)
                                ? "передней части"
                                : "частей транспорта";
            descriptor = new ModAssetDescriptor(
                $"Транспорт: урон {part} от столкновения",
                "Кривая, которая определяет, как авария переносит урон на детали транспорта.");
            return true;
        }

        if (lowerStem.Contains("passengerdamagevscollisiondamagecurve", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                lowerStem.Contains("bike", StringComparison.Ordinal)
                    ? "Транспорт: урон пассажиру на мотоцикле"
                    : "Транспорт: урон пассажиру при столкновении",
                "Кривая, которая определяет, сколько урона получают пассажиры при аварии.");
            return true;
        }

        if (lowerStem.Contains("hitdamagevsvehiclespeedinkph", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Транспорт: урон от наезда по скорости",
                "Кривая, которая задаёт урон от удара транспортом в зависимости от скорости.");
            return true;
        }

        if (lowerStem.Contains("burndamagepersecond", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Транспорт: урон от температуры",
                "Кривая урона от перегрева или контакта с горячими частями транспорта.");
            return true;
        }

        if (lowerStem.Contains("tractor", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                $"Трактор: {cleanStem}",
                "Параметры столкновения и переноса урона для деталей трактора.");
            return true;
        }

        return false;
    }

    private static string ResolveVehicleSpawnGroupDisplayName(string stem)
    {
        if (string.IsNullOrWhiteSpace(stem))
        {
            return "без названия";
        }

        var key = stem.StartsWith("VSG_", StringComparison.OrdinalIgnoreCase)
            ? stem["VSG_".Length..]
            : stem;

        return key.ToLowerInvariant() switch
        {
            "default" => "базовая группа",
            "city" => "город",
            "bicycle" => "велосипеды",
            "motorcycle" => "мотоциклы",
            "wheelbarrow" => "тачки",
            "boat" => "лодки",
            "militaryrubberboat" => "военная надувная лодка",
            "hydroplane" => "гидросамолёт",
            "civilianairplane" => "гражданский самолёт",
            "sup" => "сапборд",
            _ => CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(key)))
        };
    }

    private static string ResolveVehicleSpawnPresetOptionDisplayName(string relativePath)
    {
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var lowerStem = stem.ToLowerInvariant();

        if (lowerStem.Contains("radiationzone", StringComparison.Ordinal))
        {
            var vehicleName = ResolveRadiationVehiclePresetName(lowerStem);
            var condition = ResolveRadiationVehiclePresetCondition(lowerStem);
            return string.IsNullOrWhiteSpace(condition)
                ? vehicleName
                : $"{vehicleName} ({condition})";
        }

        var match = Regex.Match(
            lowerStem,
            @"^(?<name>[a-z0-9]+)spawnpreset(?:_|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(stem)));
        }

        return match.Groups["name"].Value.ToLowerInvariant() switch
        {
            "laika" => "Laika",
            "rager" => "Rager",
            "wolfswagen" => "Wolfswagen",
            "ris" => "RIS",
            "cruiser" => "Cruiser",
            "citybike" => "City Bike",
            "mountainbike" => "Mountain Bike",
            "dirtbike" => "Dirt Bike",
            "sidecarbike" => "Sidecar Bike",
            "tractor" => "Tractor",
            "wheelbarrowmetal" => "Тачка (металлическая)",
            "kingletduster" => "Kinglet Duster",
            "kingletmariner" => "Kinglet Mariner",
            _ => CapitalizeFirst(NormalizeLocalizedLabel(LocalizeAssetStem(match.Groups["name"].Value)))
        };
    }

    private static bool TryDescribeRadiationAsset(string relativePath, out ModAssetDescriptor descriptor)
    {
        descriptor = null!;
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var lowerStem = stem.ToLowerInvariant();
        var path = relativePath.ToLowerInvariant();

        if (path.Contains("/characters/prisoner/blueprints/bodyeffects/conditions/radiation/", StringComparison.Ordinal))
        {
            if (lowerStem.Contains("acuteradiationsyndrome", StringComparison.Ordinal))
            {
                descriptor = new ModAssetDescriptor(
                    "Радиация: острый радиационный синдром",
                    "Тяжёлая стадия облучения: пороги, интенсивность и влияние на состояние персонажа.");
                return true;
            }

            if (lowerStem.Contains("radiationpresence", StringComparison.Ordinal))
            {
                descriptor = new ModAssetDescriptor(
                    "Радиация: фоновое облучение",
                    "Базовый эффект облучения персонажа и его нарастание в радиационных зонах.");
                return true;
            }

            descriptor = new ModAssetDescriptor(
                $"Радиация: {LocalizeAssetStem(stem)}",
                "Состояние облучения персонажа: тяжесть, скорость накопления и последствия.");
            return true;
        }

        if (path.Contains("/vehicles/spawningpresets/automaticspawn/", StringComparison.Ordinal)
            && lowerStem.Contains("spawnpreset", StringComparison.Ordinal)
            && lowerStem.Contains("radiationzone", StringComparison.Ordinal))
        {
            var vehicleName = ResolveRadiationVehiclePresetName(lowerStem);
            var condition = ResolveRadiationVehiclePresetCondition(lowerStem);
            var displayName = string.IsNullOrWhiteSpace(condition)
                ? $"Радиационный транспорт: {vehicleName}"
                : $"Радиационный транспорт: {vehicleName} ({condition})";

            descriptor = new ModAssetDescriptor(
                displayName,
                "Какой вариант транспорта может появиться в радиационной зоне и в каком стартовом состоянии.");
            return true;
        }

        if (path.Contains("/vehicles/spawningpresets/spawngroups/", StringComparison.Ordinal)
            && lowerStem.Contains("radiation", StringComparison.Ordinal))
        {
            var location = lowerStem.Contains("city", StringComparison.Ordinal)
                ? "город"
                : lowerStem.Contains("default", StringComparison.Ordinal)
                    ? "общая зона"
                    : LocalizeAssetStem(stem);
            descriptor = new ModAssetDescriptor(
                $"Радиация: группа спавна транспорта ({location})",
                "Какие пресеты транспорта использует радиационная зона при автоматическом выборе машины.");
            return true;
        }

        if (path.Contains("/items/postspawnactions/", StringComparison.Ordinal)
            && lowerStem.Contains("radiation", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Радиация: пост-действие предмета",
                "Служебное действие после спавна, связанное с радиационной зоной или её лутом.");
            return true;
        }

        return false;
    }

    private static string ResolveRadiationVehiclePresetName(string lowerStem)
    {
        var match = Regex.Match(
            lowerStem,
            @"^(?<name>[a-z0-9]+)spawnpreset(?:_|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return LocalizeAssetStem(lowerStem);
        }

        var rawName = match.Groups["name"].Value.ToLowerInvariant();
        return rawName switch
        {
            "laika" => "Laika",
            "rager" => "Rager",
            "wolfswagen" => "Wolfswagen",
            _ => CapitalizeFirst(LocalizeAssetStem(rawName))
        };
    }

    private static string ResolveRadiationVehiclePresetCondition(string lowerStem)
    {
        if (lowerStem.Contains("nobattery", StringComparison.Ordinal))
        {
            return "без аккумулятора";
        }

        if (lowerStem.Contains("nodriverseat", StringComparison.Ordinal))
        {
            return "без водительского сиденья";
        }

        if (lowerStem.Contains("notires", StringComparison.Ordinal))
        {
            return "без колёс";
        }

        return string.Empty;
    }

    private static bool TryDescribeNpcEncounterAsset(string relativePath, out ModAssetDescriptor descriptor)
    {
        descriptor = null!;
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var path = relativePath.ToLowerInvariant();
        var lowerStem = stem.ToLowerInvariant();

        if (path.Contains("/gameevents/markers/", StringComparison.Ordinal))
        {
            var eventName = lowerStem.Contains("teamdeathmatch", StringComparison.Ordinal)
                ? "командный бой"
                : lowerStem.Contains("deathmatch", StringComparison.Ordinal)
                    ? "бой насмерть"
                    : lowerStem.Contains("dropzone", StringComparison.Ordinal)
                        ? "зона сброса"
                        : lowerStem.Contains("ctf", StringComparison.Ordinal)
                            ? "захват флага"
                            : LocalizeAssetStem(stem);

            descriptor = new ModAssetDescriptor(
                $"Маркер события: {eventName}",
                "Точка запуска игрового события: его правила, выдаваемые наборы оружия и одежды, плата за вход и очки.");
            return true;
        }

        if (stem.Equals("ArmedNPCDifficultyLevelSettings", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                "Сложность вооружённых NPC",
                "Точность, урон, паузы между атаками и поведение стрельбы для уровней сложности NPC.");
            return true;
        }

        if (lowerStem.Equals("bp_armednpcguardbase", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Базовый шаблон NPC: охранники",
                "Общие правила поведения, логики и связей для вооружённых охранников.");
            return true;
        }

        if (lowerStem.Equals("bp_armednpcdrifterbase", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Базовый шаблон NPC: скитальцы",
                "Общие правила поведения, логики и связей для вооружённых скитальцев.");
            return true;
        }

        if (lowerStem.StartsWith("npcguardcommondata_", StringComparison.Ordinal)
            || lowerStem.StartsWith("npcdriftercommondata_", StringComparison.Ordinal))
        {
            var role = lowerStem.StartsWith("npcguardcommondata_", StringComparison.Ordinal)
                ? "охранники"
                : "скитальцы";
            var levelMatch = Regex.Match(stem, @"lvl[_\-]?(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var levelLabel = levelMatch.Success ? levelMatch.Groups[1].Value : "?";
            var suffix = lowerStem.Contains("radiation", StringComparison.Ordinal) ? " (радиация)" : string.Empty;
            descriptor = new ModAssetDescriptor(
                $"Общие данные NPC: {CapitalizeFirst(role)}, уровень {levelLabel}{suffix}",
                "Базовые параметры здоровья, реакций и поведения конкретного семейства вооружённых NPC.");
            return true;
        }

        if (path.Contains("/character_presets/npcs/", StringComparison.Ordinal))
        {
            var role = stem.StartsWith("guard_class_", StringComparison.OrdinalIgnoreCase)
                ? "Охранник"
                : stem.StartsWith("drifter_class_", StringComparison.OrdinalIgnoreCase)
                    ? "Скиталец"
                    : "NPC";

            var classMatch = Regex.Match(stem, @"class_(\d+)");
            var classLabel = classMatch.Success ? classMatch.Groups[1].Value : "?";
            var suffix = stem.Contains("radiation", StringComparison.OrdinalIgnoreCase) ? " (радиация)" : string.Empty;
            descriptor = new ModAssetDescriptor(
                $"NPC: {role}, класс {classLabel}{suffix}",
                "Состав пресета конкретного вооружённого NPC и шанс выбора его класса.");
            return true;
        }

        if (path.Contains("/character_presets/animals/", StringComparison.Ordinal))
        {
            var flavor = lowerStem.Contains("non_aggressive", StringComparison.Ordinal)
                || lowerStem.Contains("nonaggressive", StringComparison.Ordinal)
                ? "мирные"
                : lowerStem.Contains("aggressive", StringComparison.Ordinal)
                    ? "агрессивные"
                    : "разные";

            descriptor = new ModAssetDescriptor(
                $"Пресет животных: {CapitalizeFirst(flavor)}",
                "Какие животные может выбрать система спавна и с каким весом.");
            return true;
        }

        if (path.Contains("/character_presets/zombies/", StringComparison.Ordinal))
        {
            var family = lowerStem.Contains("nuclear", StringComparison.Ordinal)
                ? "радиационные"
                : lowerStem.Contains("military", StringComparison.Ordinal)
                    ? "военные"
                    : lowerStem.Contains("police", StringComparison.Ordinal)
                        ? "полицейские"
                        : lowerStem.Contains("civilian", StringComparison.Ordinal)
                            ? "гражданские"
                            : "разные";

            descriptor = new ModAssetDescriptor(
                $"Пресет зомби: {CapitalizeFirst(family)}",
                "Какие типы зомби может выбрать система спавна и с каким весом.");
            return true;
        }

        if (lowerStem.StartsWith("htz_", StringComparison.Ordinal)
            || lowerStem.StartsWith("mtz_", StringComparison.Ordinal)
            || lowerStem.StartsWith("ltz_", StringComparison.Ordinal))
        {
            var threat = lowerStem.StartsWith("htz_", StringComparison.Ordinal)
                ? "высокая угроза"
                : lowerStem.StartsWith("mtz_", StringComparison.Ordinal)
                    ? "средняя угроза"
                    : "низкая угроза";
            var location = lowerStem.Contains("coalmine_tunnels", StringComparison.Ordinal)
                ? "туннели угольной шахты"
                : lowerStem.Contains("medical_health_center", StringComparison.Ordinal)
                    ? "медцентр"
                    : lowerStem.Contains("medical_hospital", StringComparison.Ordinal)
                        ? "больница"
                        : lowerStem.Contains("police_station", StringComparison.Ordinal)
                            ? "полицейский участок"
                            : lowerStem.Contains("prison", StringComparison.Ordinal)
                                ? "тюрьма"
                                : "зона угрозы";

            var subject = lowerStem.Contains("_npc_", StringComparison.Ordinal)
                ? "NPC"
                : lowerStem.Contains("_character_", StringComparison.Ordinal)
                    ? "персонажей"
                    : string.Empty;

            var childSuffix = lowerStem.EndsWith("_child", StringComparison.Ordinal) ? " (дочерний шаблон)" : string.Empty;
            descriptor = new ModAssetDescriptor(
                string.IsNullOrWhiteSpace(subject)
                    ? $"{CapitalizeFirst(threat)}: {location}, общий шаблон{childSuffix}"
                    : $"{CapitalizeFirst(threat)}: {location}, событие {subject}{childSuffix}",
                "Шаблон события для конкретной зоны угрозы и её набора врагов.");
            return true;
        }

        if (path.Contains("/encounterzones/", StringComparison.Ordinal))
        {
            var zoneTier = lowerStem.Contains("high", StringComparison.Ordinal)
                ? "высокая"
                : lowerStem.Contains("medium", StringComparison.Ordinal)
                    ? "средняя"
                    : lowerStem.Contains("low", StringComparison.Ordinal)
                        ? "низкая"
                        : string.Empty;
            descriptor = new ModAssetDescriptor(
                string.IsNullOrWhiteSpace(zoneTier)
                    ? $"Зона события: {LocalizeAssetStem(stem)}"
                    : $"Зона угрозы: {zoneTier}",
                "Параметры зоны, в которой работают орды, враги и связанные события.");
            return true;
        }

        if (path.Contains("/spawn_amount_curves/", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                $"Кривая количества спавна: {LocalizeAssetStem(stem)}",
                "Сколько врагов или событий может появляться при разных условиях.");
            return true;
        }

        if (path.Contains("/worldevents/cargodrop/", StringComparison.Ordinal)
            && lowerStem.Equals("bp_cargo", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Контейнер грузового дропа",
                "Какие наборы лута может выбирать контейнер грузового дропа и через сколько секунд он взрывается после приземления.");
            return true;
        }

        if (path.Contains("/worldevents/cargodrop/", StringComparison.Ordinal)
            || path.Contains("/cargo_drop/", StringComparison.Ordinal)
            || lowerStem.Contains("cargodrop", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Событие: грузовой дроп",
                "Поведение события грузового дропа, защитников и связанных триггеров.");
            return true;
        }

        if (lowerStem.Contains("basebbflyingattackerencounter", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Событие базы: летающий атакующий",
                "Настройки атакующего события, которое работает вокруг базы.");
            return true;
        }

        if (lowerStem.Contains("dropshipsentryencounter", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Событие дропшипа: охрана",
                "Шаблон охраны и защитников для события с дропшипом.");
            return true;
        }

        if (lowerStem.Contains("encounterhordebase", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Шаблон орды",
                "Базовые правила появления орды, её размеров и условий запуска.");
            return true;
        }

        if (lowerStem.Contains("spawnairbornecharactersbase", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Шаблон воздушного спавна NPC",
                "Базовые правила появления NPC из воздушных событий.");
            return true;
        }

        if (lowerStem.Contains("spawncharactersbase", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Шаблон наземного спавна NPC",
                "Базовые правила обычного появления NPC в событии.");
            return true;
        }

        if (lowerStem.Contains("staticzone", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Статическая зона события",
                "Постоянная зона, в которой работают правила encounter-системы.");
            return true;
        }

        if (lowerStem.Contains("abandonedbunker", StringComparison.Ordinal) && lowerStem.Contains("dropship", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Бункерное событие: дропшип",
                "Событие заброшенного бункера с участием дропшипа и защитников.");
            return true;
        }

        if (lowerStem.Contains("dropship", StringComparison.Ordinal) && lowerStem.Contains("flyingattacker", StringComparison.Ordinal))
        {
            descriptor = new ModAssetDescriptor(
                "Событие дропшипа: летающий атакующий",
                "Настройки боевого события с летающим атакующим NPC.");
            return true;
        }

        if (lowerStem.Contains("horde", StringComparison.Ordinal))
        {
            var hordeFlavor = lowerStem.Contains("civilian", StringComparison.Ordinal)
                ? "гражданская"
                : lowerStem.Contains("military", StringComparison.Ordinal)
                    ? "военная"
                    : lowerStem.Contains("police", StringComparison.Ordinal)
                        ? "полицейская"
                        : lowerStem.Contains("nuclear", StringComparison.Ordinal)
                            ? "радиационная"
                            : "разная";

            descriptor = new ModAssetDescriptor(
                $"Пресет орды: {CapitalizeFirst(hordeFlavor)}",
                "Какие враги входят в орду и как выбирается её состав.");
            return true;
        }

        if (stem.Equals("BP_GlobalEncounterManager", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                "Глобальный менеджер событий",
                "Главные параметры всей системы событий, угроз и орд.");
            return true;
        }

        if (stem.Equals("EncounterManagerCommonData", StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new ModAssetDescriptor(
                "Общие данные системы событий",
                "Базовые правила работы системы событий, которыми пользуются разные зоны и пресеты.");
            return true;
        }

        return false;
    }

    private string ResolveReadableItemName(string softReference)
    {
        var normalized = (softReference ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var tail = normalized;
        var dotIndex = tail.LastIndexOf(".");
        if (dotIndex >= 0 && dotIndex < tail.Length - 1)
        {
            tail = tail[(dotIndex + 1)..];
        }
        else
        {
            var slashIndex = tail.LastIndexOf("/");
            if (slashIndex >= 0 && slashIndex < tail.Length - 1)
            {
                tail = tail[(slashIndex + 1)..];
            }
        }

        if (tail.EndsWith("_C", StringComparison.OrdinalIgnoreCase))
        {
            tail = tail[..^2];
        }

        if (_knownKbItems.TryGetValue(tail, out var knownItem) && !string.IsNullOrWhiteSpace(knownItem.ItemName))
        {
            return knownItem.ItemName;
        }

        return LocalizeAssetStem(tail);
    }

    private static string HumanizeAssetStem(string stem)
    {
        if (string.IsNullOrWhiteSpace(stem))
        {
            return "Без названия";
        }

        var text = stem
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim();

        text = HumanizeCamel(text);
        return string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Trim();
    }

    private static string LocalizeAssetStem(string stem)
    {
        var normalizedKey = NormalizeAssetKey(stem);
        var exactNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["activatedcharcoal"] = "активированный уголь",
            ["alphaamanitin"] = "альфа-аманитин",
            ["antibiotics"] = "антибиотики",
            ["bladderfull"] = "переполненный мочевой пузырь",
            ["basicinjury"] = "базовая травма",
            ["bleeding"] = "кровотечение",
            ["bleedinginjury"] = "кровоточащая рана",
            ["bloodshoteyes"] = "покраснение глаз",
            ["bpprisonermetabolism"] = "метаболизм персонажа",
            ["bodysimulation"] = "симуляция состояния тела",
            ["breathingobstructed"] = "затруднённое дыхание",
            ["blurredvision"] = "затуманенное зрение",
            ["burninjury"] = "ожог",
            ["burping"] = "отрыжка",
            ["caffeine"] = "кофеин",
            ["caffeineoverdose"] = "передозировка кофеином",
            ["cannabinoidpoisoning"] = "отравление каннабиноидами",
            ["colonfull"] = "переполненный кишечник",
            ["choking"] = "удушье",
            ["coughing"] = "кашель",
            ["diarrhea"] = "диарея",
            ["disorientation"] = "дезориентация",
            ["dizziness"] = "головокружение",
            ["doublevision"] = "двоение в глазах",
            ["eyeirritation"] = "раздражение глаз",
            ["eyepressure"] = "давление в глазах",
            ["farting"] = "метеоризм",
            ["fatigue"] = "усталость",
            ["feelingcold"] = "ощущение холода",
            ["feelinghot"] = "ощущение жара",
            ["fever"] = "жар",
            ["fooddisgust"] = "отвращение к еде",
            ["footabrasions"] = "ссадины ступней",
            ["headache"] = "головная боль",
            ["hallucinationepisode"] = "приступ галлюцинаций",
            ["hallucinations"] = "галлюцинации",
            ["hallucinationsds"] = "галлюцинации (DS)",
            ["hallucinationsnm"] = "галлюцинации (NM)",
            ["handabrasions"] = "ссадины кистей",
            ["healthlow"] = "критически низкое здоровье",
            ["heavyinjuries"] = "тяжёлые травмы",
            ["heightenedsenses"] = "обострённые чувства",
            ["hunger"] = "голод",
            ["hyperthermia"] = "перегрев",
            ["hypothermia"] = "переохлаждение",
            ["energydeficiency"] = "нехватка энергии",
            ["waterdeficiency"] = "обезвоживание",
            ["commoncold"] = "простуда",
            ["influenza"] = "грипп",
            ["infection"] = "инфекция",
            ["infectioncontroller"] = "контроллер инфекции",
            ["character"] = "персонажи",
            ["characters"] = "персонажи",
            ["armednpcs"] = "вооружённые NPC",
            ["scavenger"] = "мародёр",
            ["scavengers"] = "мародёры",
            ["puppets"] = "куклы",
            ["loot"] = "лут",
            ["civilian"] = "гражданский",
            ["military"] = "военный",
            ["police"] = "полиция",
            ["medical"] = "медицина",
            ["radiation"] = "радиация",
            ["spawnpreset"] = "пресет спавна",
            ["examine"] = "осмотр",
            ["buildings"] = "здания",
            ["bunker"] = "бункер",
            ["tv"] = "ТВ",
            ["canteen"] = "столовая",
            ["vending"] = "торговый",
            ["wending"] = "торговый",
            ["machine"] = "автомат",
            ["soda"] = "газировка",
            ["trash"] = "мусор",
            ["locker"] = "шкаф",
            ["bathroom"] = "ванная",
            ["kitchen"] = "кухня",
            ["bedroom"] = "спальня",
            ["garage"] = "гараж",
            ["office"] = "офис",
            ["desk"] = "стол",
            ["wardrobe"] = "гардероб",
            ["filecabinet"] = "картотечный шкаф",
            ["dressingroom"] = "раздевалка",
            ["specialpackages"] = "спецпакеты",
            ["warehouse"] = "склад",
            ["workshop"] = "мастерская",
            ["worldshelffuses"] = "полка с предохранителями",
            ["worldshelf"] = "мировая полка",
            ["cardbox"] = "картонная коробка",
            ["depository"] = "хранилище",
            ["crate"] = "ящик",
            ["tooldrawer"] = "ящик с инструментами",
            ["toolbox"] = "ящик с инструментами",
            ["cart"] = "тележка",
            ["tooldesk"] = "верстак",
            ["green"] = "зелёный",
            ["big"] = "большой",
            ["pile"] = "куча",
            ["small"] = "малый",
            ["dead"] = "мёртвые",
            ["keycard"] = "ключ-карта",
            ["keycardbunker"] = "ключ-карта бункера",
            ["keycardbunkerlevel01"] = "ключ-карта бункера: уровень 01",
            ["keycardbunkerlevel02"] = "ключ-карта бункера: уровень 02",
            ["irritatedthroat"] = "раздражение горла",
            ["drunkenness"] = "алкогольное опьянение",
            ["boostofenergy"] = "прилив энергии",
            ["intelligenceenhancement"] = "усиление интеллекта",
            ["killboxgaspoisoning"] = "газовое отравление в Killbox",
            ["knockout"] = "потеря сознания",
            ["knockoutwithblackout"] = "обморок с потемнением в глазах",
            ["leukopenia"] = "лейкопения",
            ["limping"] = "хромота",
            ["exhaustion"] = "истощение",
            ["ibotenicacid"] = "иботеновая кислота",
            ["intagenta"] = "агент инфекции A",
            ["intagentb"] = "агент инфекции B",
            ["intagentc"] = "агент инфекции C",
            ["needtodefecate"] = "позыв к дефекации",
            ["needtourinate"] = "позыв к мочеиспусканию",
            ["nicotinepoisoning"] = "отравление никотином",
            ["overburdened"] = "перегрузка весом",
            ["enduranceskill"] = "выносливость",
            ["awarenessskill"] = "внимательность",
            ["archeryskill"] = "стрельба из лука",
            ["aviationskill"] = "авиация",
            ["boxingskill"] = "бокс",
            ["camouflageskill"] = "маскировка",
            ["cookingskill"] = "кулинария",
            ["demolitionskill"] = "подрывное дело",
            ["drivingskill"] = "вождение",
            ["engineeringskill"] = "инженерия",
            ["farmingskill"] = "фермерство",
            ["fcdialpaddurationmodifier"] = "время ввода кодового замка",
            ["fclockpickinganglemodifier"] = "размер рабочей зоны взлома",
            ["fclockpickingdurabilitymodifier"] = "расход отмычки при взломе",
            ["fclockpickingdurationmodifier"] = "длительность взлома",
            ["fclockpickingstartanglemodifier"] = "стартовая позиция взлома",
            ["fctacticsdetectiontimedistancescale"] = "связь времени обнаружения и дистанции",
            ["fcvoltagematchingdurationmodifier"] = "длительность подбора напряжения",
            ["handgunskill"] = "пистолеты",
            ["meleeweaponsskill"] = "ближний бой",
            ["medicalskill"] = "медицина",
            ["motorcycleskill"] = "мотоциклы",
            ["riflesskill"] = "винтовки",
            ["muscimol"] = "мусцимол",
            ["pain"] = "боль",
            ["phoenixtearspresence"] = "эффект слёз феникса",
            ["painkillers"] = "обезболивающее",
            ["potassiumiodide"] = "йодид калия",
            ["psilocybin"] = "псилоцибин",
            ["runningskill"] = "бег",
            ["sepsis"] = "сепсис",
            ["sneezing"] = "чихание",
            ["snipingskill"] = "снайперское оружие",
            ["staminalow"] = "низкая выносливость",
            ["stealthskill"] = "скрытность",
            ["stomachfull"] = "полный желудок",
            ["stomachfullness"] = "наполненность желудка",
            ["stomachgrowling"] = "урчание в животе",
            ["stomachempty"] = "пустой желудок",
            ["stuffednose"] = "заложенность носа",
            ["teargasexposure"] = "воздействие слезоточивого газа",
            ["tacticsskill"] = "тактика",
            ["thirst"] = "жажда",
            ["thieveryskill"] = "воровство",
            ["trenchfoot"] = "траншейная стопа",
            ["tutorialbleeding"] = "учебное кровотечение",
            ["tutorialinjury"] = "учебная травма",
            ["unconsciousness"] = "бессознательность",
            ["vomiting"] = "рвота",
            ["weakness"] = "слабость",
            ["wetfeet"] = "мокрые ступни",
            ["survivalskill"] = "выживание",
            ["eyeirritationmedium"] = "раздражение глаз: средняя стадия",
            ["eyeirritationсредний"] = "раздражение глаз: средняя стадия",
            ["bomblocktag"] = "взрывной замок",
            ["bpprisonercommondata"] = "общие параметры персонажа",
            ["bpglobalguardedzonemanager"] = "глобальные охраняемые зоны",
            ["bpguardedzonemanager"] = "охраняемая зона",
            ["bpeconomymanager"] = "менеджер экономики",
            ["bptradeoutpostmanager"] = "менеджер торговых зон",
            ["bpquestmanager"] = "менеджер квестов",
            ["economyspecificdata"] = "общие экономические параметры",
            ["durabilityvspricemultipliercurve"] = "цена по прочности",
            ["bpglobalencountermanager"] = "менеджер глобальных событий",
            ["encountermanagercommondata"] = "общие параметры событий",
            ["questcommondata"] = "общие данные квестов",
            ["questmangerdata"] = "данные менеджера квестов",
            ["grenades"] = "гранаты",
            ["bearoutfit"] = "медвежий костюм",
            ["greenmilitaryoutfit"] = "зелёная военная форма",
            ["whitemilitaryoutfit"] = "белая военная форма",
            ["brownmilitaryoutfit"] = "коричневая военная форма",
            ["brawl"] = "драка",
            ["brawlblack"] = "драка: чёрная форма",
            ["brawlwhite"] = "драка: белая форма",
            ["mma"] = "ММА",
            ["mmablack"] = "ММА: чёрная форма",
            ["mmawhite"] = "ММА: белая форма",
            ["blackhawkcrossbow"] = "арбалет Black Hawk",
            ["compoundbow"] = "составной лук",
            ["improvisedbow"] = "самодельный лук",
            ["improvisedgrenadelauncher"] = "самодельный гранатомёт",
            ["improvisedflamethrower"] = "самодельный огнемёт",
            ["improvisedrifle"] = "самодельная винтовка",
            ["improvisedhandgun"] = "самодельный пистолет",
            ["mosinnagant"] = "Mosin-Nagant",
            ["tommygun"] = "Tommy Gun",
            ["flaregun"] = "Flare Gun",
            ["carbonhunter"] = "Carbon Hunter",
            ["aks74u"] = "AKS-74U",
            ["ak15"] = "AK-15",
            ["ak47"] = "AK-47",
            ["akm"] = "AKM",
            ["asval"] = "AS VAL",
            ["m1garand"] = "M1 Garand",
            ["m16a4"] = "M16A4",
            ["m1911"] = "M1911",
            ["m1887"] = "M1887",
            ["m82a1"] = "M82A1",
            ["mp5"] = "MP5",
            ["mp5k"] = "MP5K",
            ["mp5sd"] = "MP5 SD",
            ["rpk74"] = "RPK-74",
            ["scarl"] = "SCAR-L",
            ["scardmr"] = "SCAR DMR",
            ["svddragunov"] = "SVD Dragunov",
            ["vssvz"] = "VSS Vz",
            ["vhs2"] = "VHS-2",
            ["bpencounterstaticzone"] = "статическая зона события",
            ["bpencounterhordebase"] = "базовая орда",
            ["bpencounterspawncharactersbase"] = "база появления персонажей",
            ["bpencounterspawnairbornecharactersbase"] = "база воздушного появления персонажей",
            ["bpencountercargodropevent"] = "событие грузового дропа",
            ["bpencountercargodropeventflyingguardian"] = "событие грузового дропа с летающим стражем",
            ["guardedzonedefenderhordepreset"] = "пресет защитников охраняемой зоны",
            ["guardedzonedefenderhordeencounter"] = "событие защитников охраняемой зоны",
            ["metabolismconfiguration"] = "настройки метаболизма",
            ["movementsettings"] = "настройки движения",
            ["radiationsourcedescription"] = "источник радиации",
            ["apple"] = "яблоко",
            ["appletree"] = "яблоня",
            ["apricot"] = "абрикос",
            ["broccoli"] = "брокколи",
            ["cabbage"] = "капуста",
            ["cannabis"] = "конопля",
            ["carrot"] = "морковь",
            ["cherry"] = "вишня",
            ["chilli"] = "чили",
            ["corn"] = "кукуруза",
            ["cucumber"] = "огурец",
            ["fig"] = "инжир",
            ["garlic"] = "чеснок",
            ["lemon"] = "лимон",
            ["lettuce"] = "салат",
            ["onion"] = "лук",
            ["orange"] = "апельсин",
            ["peach"] = "персик",
            ["pear"] = "груша",
            ["pepper"] = "перец",
            ["peppers"] = "перец",
            ["plum"] = "слива",
            ["potato"] = "картофель",
            ["pumpkin"] = "тыква",
            ["spinach"] = "шпинат",
            ["sweetmelon"] = "сладкая дыня",
            ["tangerine"] = "мандарин",
            ["tobacco"] = "табак",
            ["tomato"] = "томат",
            ["watermelon"] = "арбуз",
            ["zucchini"] = "кабачок",
            ["aphids"] = "тля",
            ["carrotweevils"] = "морковные долгоносики",
            ["citruscanker"] = "цитрусовый рак",
            ["cricket"] = "сверчок",
            ["cutworms"] = "подгрызающие гусеницы",
            ["fruitfly"] = "плодовая мушка",
            ["grasshopper"] = "кузнечик",
            ["maggots"] = "личинки",
            ["mites"] = "клещи",
            ["potatobeetle"] = "картофельный жук",
            ["slugs"] = "слизни",
            ["snails"] = "улитки",
            ["worms"] = "черви",
            ["anthracnose"] = "антракноз",
            ["downymildew"] = "ложная мучнистая роса",
            ["mould"] = "плесень",
            ["rot"] = "гниль",
            ["rust"] = "ржавчина",
            ["scab"] = "парша"
        };

        if (TryLocalizeSeedStem(stem, out var localizedSeed))
        {
            return localizedSeed;
        }

        if (exactNames.TryGetValue(normalizedKey, out var localized))
        {
            return CapitalizeFirst(localized);
        }

        return CapitalizeFirst(LocalizeCompactGameplayName(stem));
    }

    private static bool LooksLikeOpaqueLocalizationKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Regex.IsMatch(
            value.Trim(),
            "^[0-9A-F]{32}$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool TryLocalizeSeedStem(string stem, out string localized)
    {
        localized = string.Empty;
        if (string.IsNullOrWhiteSpace(stem)
            || stem.StartsWith("MI_", StringComparison.OrdinalIgnoreCase)
            || stem.StartsWith("SM_", StringComparison.OrdinalIgnoreCase)
            || stem.StartsWith("M_", StringComparison.OrdinalIgnoreCase)
            || stem.StartsWith("T_", StringComparison.OrdinalIgnoreCase)
            || !stem.EndsWith("_Seeds", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var baseStem = stem[..^"_Seeds".Length].TrimEnd('_');
        if (string.IsNullOrWhiteSpace(baseStem) || string.Equals(baseStem, stem, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var baseLabel = LocalizeAssetStem(baseStem);
        if (string.IsNullOrWhiteSpace(baseLabel))
        {
            return false;
        }

        localized = $"Семена: {baseLabel.ToLowerInvariant()}";
        return true;
    }

    private static string NormalizeAssetKey(string stem)
    {
        if (string.IsNullOrWhiteSpace(stem))
        {
            return string.Empty;
        }

        return new string(stem.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static bool IsStudioCategoryEnabled(string categoryId, string normalizedPath)
    {
        var path = normalizedPath.ToLowerInvariant();
        if (IsCraftingUiDataRegistryAsset(path))
        {
            return true;
        }

        if (categoryId is "weapons-items"
            && path.Contains("/ui/gameevents/itemselection/", StringComparison.Ordinal))
        {
            return !path.Contains("/item_icons/", StringComparison.Ordinal)
                && !path.Contains("/stringtables/", StringComparison.Ordinal);
        }

        var blockedVisualTokens = new[]
        {
            "/textures/", "/texture/", "/materials/", "/material/", "/meshes/", "/mesh/",
            "/skeletal", "/animations/", "/anim/", "/vfx/", "/fx/", "/sounds/", "/audio/", "/icons/", "/ui/"
        };

        if (blockedVisualTokens.Any(token => path.Contains(token, StringComparison.Ordinal)))
        {
            return false;
        }

        var fileName = Path.GetFileNameWithoutExtension(normalizedPath);
        if (fileName.StartsWith("T_", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("M_", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("MI_", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("SK_", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("SM_", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("UI_", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("ui_data", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("uidata", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("widget", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("rendertarget", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("_ES", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("_TR", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.Contains("/stringtables/", StringComparison.Ordinal)
            || path.Contains("/item_icons/", StringComparison.Ordinal)
            || path.Contains("/icons/", StringComparison.Ordinal))
        {
            return false;
        }

        if (categoryId is "economy-trader")
        {
            if (path.Contains("/manual/", StringComparison.Ordinal)
                || path.Contains("/codex/", StringComparison.Ordinal)
                || path.Contains("/visuals/", StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (categoryId is "body-effects")
        {
            if (path.Contains("/bodyeffects/symptoms/hallucinationillusions/", StringComparison.Ordinal)
                || path.Contains("/companioncrow/", StringComparison.Ordinal)
                || path.Contains("/fakeitems/", StringComparison.Ordinal)
                || path.Contains("/miniaturezombie/", StringComparison.Ordinal)
                || fileName.StartsWith("ABP_", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("BP_", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("BPC_", StringComparison.OrdinalIgnoreCase)
                || fileName.Contains("responseidle", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (categoryId is "foreign-substances")
        {
            if (path.Contains("/manual/", StringComparison.Ordinal)
                || path.Contains("/codex/", StringComparison.Ordinal)
                || path.Contains("/visuals/", StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (categoryId is "economy-trader")
        {
            if (path.Contains("/manual/", StringComparison.Ordinal)
                || path.Contains("/codex/", StringComparison.Ordinal)
                || path.Contains("/visuals/", StringComparison.Ordinal)
                || path.Contains("/stringtables/", StringComparison.Ordinal)
                || path.Contains("/item_icons/", StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (categoryId is "crafting-recipes"
            or "crafting-ingredients"
            or "starter-kit"
            or "foreign-substances"
            or "quests"
            or "plants-farming"
            or "radiation"
            or "body-effects")
        {
            return true;
        }

        if (categoryId is "economy-trader")
        {
            return path.Contains("/economy/", StringComparison.Ordinal)
                || path.Contains("tradeable", StringComparison.OrdinalIgnoreCase);
        }

        if (categoryId is "npc-encounters")
        {
            if (fileName.Contains("_lpc", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith("_child", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return path.Contains("/encounters/", StringComparison.Ordinal)
                || path.Contains("/gameevents/markers/", StringComparison.Ordinal)
                || path.Contains("/worldevents/", StringComparison.Ordinal)
                || (path.Contains("/npcs/", StringComparison.Ordinal)
                    && (path.Contains("spawn", StringComparison.Ordinal)
                        || path.Contains("config", StringComparison.Ordinal)
                        || path.Contains("table", StringComparison.Ordinal)
                        || path.Contains("zone", StringComparison.Ordinal)
                        || path.Contains("difficulty", StringComparison.Ordinal)));
        }

        if (categoryId is "vehicles")
        {
            return path.Contains("hitdamagevsvehiclespeed", StringComparison.Ordinal)
                || path.Contains("/vehicles/spawningpresets/spawngroups/", StringComparison.Ordinal)
                || (path.Contains("/vehicles/", StringComparison.Ordinal)
                    && (path.Contains("damage", StringComparison.Ordinal)
                        || path.Contains("collision", StringComparison.Ordinal)
                        || path.Contains("config", StringComparison.Ordinal)
                        || path.Contains("settings", StringComparison.Ordinal)
                        || path.Contains("/data/", StringComparison.Ordinal)));
        }

        if (categoryId is "skills-progression")
        {
            return path.Contains("/skills/", StringComparison.Ordinal)
                && !path.Contains("/animations/", StringComparison.Ordinal)
                && !path.Contains("preset", StringComparison.Ordinal);
        }

        if (categoryId is "locks-base")
        {
            if (fileName.Contains("intro", StringComparison.OrdinalIgnoreCase)
                || fileName.Contains("testlock", StringComparison.OrdinalIgnoreCase)
                || fileName.Contains("practicelock", StringComparison.OrdinalIgnoreCase)
                || fileName.Contains("worldowned", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith("tag", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("Oven", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("Elements", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("Flags", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("Upgrading", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return path.Contains("/fortifications/locks/", StringComparison.Ordinal)
                || path.Contains("/basebuilding/", StringComparison.Ordinal)
                || path.Contains("/minigames/lockpicking/", StringComparison.Ordinal);
        }

        if (categoryId is "fishing-spawn")
        {
            return path.Contains("/characters/spawnerpresets/", StringComparison.Ordinal)
                || path.Contains("/characters/animals2/fish/", StringComparison.Ordinal)
                || path.Contains("/items/fishing/", StringComparison.Ordinal);
        }

        if (categoryId is "plants-farming")
        {
            return fileName.Equals("DA_PlantSpeciesList", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("DA_PlantSpecies_", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("DA_PlantPest_", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("DA_PlantDisease_", StringComparison.OrdinalIgnoreCase);
        }

        if (categoryId is "item-spawning")
        {
            return false;
        }

        if (categoryId is "weapons-items")
        {
            return (path.Contains("/items/weapons/", StringComparison.Ordinal)
                    || path.Contains("/data/weapon/malfunctionprobabilitycurves/", StringComparison.Ordinal)
                    || path.Contains("/ui/gameevents/itemselection/", StringComparison.Ordinal)
                    || path.Contains("/items/equipment/active_items/", StringComparison.Ordinal)
                    || path.Contains("/items/spawnerpresets/examine_data_presets/", StringComparison.Ordinal)
                    || path.Contains("/items/spawnerpresets2/", StringComparison.Ordinal)
                    || path.Contains("/gameresources/food/", StringComparison.Ordinal))
                && !path.Contains("/item_icons/", StringComparison.Ordinal)
                && !path.Contains("/stringtables/", StringComparison.Ordinal);
        }

        return false;
    }

    public StudioCatalogDto GetItemCatalog()
    {
        lock (_sync)
        {
            if (_itemsCache is not null)
            {
                return new StudioCatalogDto(_itemsCache.Count, _itemsCache);
            }

            var pakIndex = GetOrLoadPakIndex();

            var items = pakIndex.GetAllRelativePaths()
                .Where(path => path.StartsWith("scum/content/conz_files/items/", StringComparison.OrdinalIgnoreCase))
                .Where(path => path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
                .Where(IsLikelyPlayableItemAsset)
                .Select(path =>
                {
                    var id = Path.GetFileNameWithoutExtension(path);
                    var name = HumanizeItemName(id);
                    return new StudioItemDto(id, name, path, null);
                })
                .DistinctBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.ItemName, StringComparer.OrdinalIgnoreCase)
                .Take(15000)
                .ToList();

            _iconService.RebuildMappings(items, pakIndex);

            var itemsWithIcons = items
                .Select(item => item with
                {
                    IconUrl = _iconService.HasIconForItem(item.ItemId)
                        ? $"/api/icon?itemId={Uri.EscapeDataString(item.ItemId)}"
                        : null
                })
                .ToList();

            _itemsCache = itemsWithIcons;
            return new StudioCatalogDto(itemsWithIcons.Count, itemsWithIcons);
        }
    }

    public async Task<StudioCatalogDto> SearchItemCatalogAsync(string? term, int limit)
    {
        limit = Math.Clamp(limit, 20, 400);
        var normalizedTerm = term?.Trim();

        var kbItems = await _knowledgeBase.SearchItemsAsync(normalizedTerm, limit).ConfigureAwait(false);
        if (kbItems.Count > 0)
        {
            var localizedKbItems = kbItems
                .Select(item => item with
                {
                    ItemName = LocalizeAssetStem(item.ItemId)
                })
                .ToList();

            RememberKnowledgeBaseItems(localizedKbItems);
            return new StudioCatalogDto(localizedKbItems.Count, localizedKbItems);
        }

        if (_itemsCache is null)
        {
            _ = GetItemCatalog();
        }

        List<StudioItemDto> localItems;
        Dictionary<string, StudioItemDto> kbOverrides;
        lock (_sync)
        {
            localItems = _itemsCache?.ToList() ?? [];
            kbOverrides = new Dictionary<string, StudioItemDto>(_knownKbItems, StringComparer.OrdinalIgnoreCase);
        }

        IEnumerable<StudioItemDto> query = localItems;
        if (!string.IsNullOrWhiteSpace(normalizedTerm))
        {
            query = query.Where(item =>
                item.ItemId.Contains(normalizedTerm, StringComparison.OrdinalIgnoreCase)
                || item.ItemName.Contains(normalizedTerm, StringComparison.OrdinalIgnoreCase));
        }

        var result = query
            .Take(limit)
            .Select(item =>
            {
                if (!kbOverrides.TryGetValue(item.ItemId, out var kb))
                {
                    return item;
                }

                return item with
                {
                    ItemName = string.IsNullOrWhiteSpace(kb.ItemName) ? item.ItemName : kb.ItemName,
                    IconUrl = string.IsNullOrWhiteSpace(kb.IconUrl) ? item.IconUrl : kb.IconUrl
                };
            })
            .ToList();

        return new StudioCatalogDto(result.Count, result);
    }

    public bool TryGetItemIcon(string itemId, out byte[] pngBytes)
    {
        if (_itemsCache is null)
        {
            _ = GetItemCatalog();
        }

        return _iconService.TryGetIconPng(itemId, out pngBytes);
    }

    public StudioBuildResultDto Build(StudioBuildRequestDto request)
    {
        try
        {
            var preWarnings = new List<string>();
            var selectedPresetIds = ResolveSelectedPresets(request).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var featureSettings = BuildFeatureSettingsMap(request);
            var selectedFeatureIds = ResolveSelectedFeatures(request, featureSettings).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var assetSettings = BuildAssetSettingsMap(request);

            var selectedAssetsById = new Dictionary<string, AssetSelection>(StringComparer.OrdinalIgnoreCase);

            if (selectedPresetIds.Count > 0)
            {
                foreach (var entry in _presetFiles.Where(x => selectedPresetIds.Contains(x.PresetId)))
                {
                    var assetId = GetAssetId(entry);
                    selectedAssetsById[assetId] = new AssetSelection(assetId, entry.TargetRelativePath, entry.SourcePath);
                }
            }

            if (selectedFeatureIds.Count > 0)
            {
                foreach (var featureId in selectedFeatureIds)
                {
                    if (!_featureAssetIds.TryGetValue(featureId, out var featureAssets))
                    {
                        continue;
                    }

                    foreach (var assetId in featureAssets)
                    {
                        if (_presetFileById.TryGetValue(assetId, out var entry))
                        {
                            selectedAssetsById[assetId] = new AssetSelection(assetId, entry.TargetRelativePath, entry.SourcePath);
                        }
                    }
                }
            }

            if (request.SelectedAssetIds is { Count: > 0 })
            {
                var explicitSet = request.SelectedAssetIds
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (selectedAssetsById.Count == 0)
                {
                    foreach (var assetId in explicitSet)
                    {
                        if (TryBuildSelectionFromAssetId(assetId, out var selection))
                        {
                            selectedAssetsById[assetId] = selection;
                        }
                    }
                }
                else
                {
                    var toDrop = selectedAssetsById.Keys
                        .Where(assetId => !explicitSet.Contains(assetId))
                        .ToList();

                    foreach (var assetId in toDrop)
                    {
                        selectedAssetsById.Remove(assetId);
                    }

                    foreach (var assetId in explicitSet)
                    {
                        if (selectedAssetsById.ContainsKey(assetId))
                        {
                            continue;
                        }

                        if (TryBuildSelectionFromAssetId(assetId, out var selection))
                        {
                            selectedAssetsById[assetId] = selection;
                        }
                    }
                }
            }

            ApplyFeatureDefaultsToAssetSettings(selectedFeatureIds, featureSettings, assetSettings);

            foreach (var setting in assetSettings.Values.Where(x => !x.Enabled))
            {
                selectedAssetsById.Remove(setting.AssetId);
            }

            foreach (var setting in assetSettings.Values.Where(x => x.Enabled))
            {
                if (selectedAssetsById.ContainsKey(setting.AssetId))
                {
                    continue;
                }

                if (TryBuildSelectionFromAssetId(setting.AssetId, out var selection))
                {
                    selectedAssetsById[setting.AssetId] = selection;
                }
            }

            var textFiles = BuildRecipeFiles(request);
            var editedFiles = BuildEditedAssetFiles(request, assetSettings, preWarnings);
            if (selectedAssetsById.Count == 0 && textFiles.Count == 0 && editedFiles.Count == 0)
            {
                return new StudioBuildResultDto(
                    false,
                    "Нет ассетов для сборки: выбери модуль, категорию или конкретные файлы.",
                    null,
                    null,
                    null,
                    0,
                    0,
                    0,
                    []);
            }

            var binaryFiles = new List<BuildInputFile>(selectedAssetsById.Count);
            var forceCompanionAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var skipCompanionAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var selection in selectedAssetsById.Values)
            {
                var setting = assetSettings.GetValueOrDefault(selection.AssetId);
                var sourceMode = ResolveSourceMode(setting?.SourceMode, selection.PresetSourcePath is not null);
                var companionMode = ResolveCompanionMode(setting?.CompanionMode);

                if (companionMode == "force")
                {
                    forceCompanionAssets.Add(selection.TargetRelativePath);
                }
                else if (companionMode == "none")
                {
                    skipCompanionAssets.Add(selection.TargetRelativePath);
                }

                var sourcePath = ResolveAssetSourcePath(selection, sourceMode, preWarnings);
                if (sourcePath is null)
                {
                    continue;
                }

                binaryFiles.Add(new BuildInputFile(sourcePath, selection.TargetRelativePath));
            }

            if (editedFiles.Count > 0)
            {
                binaryFiles.AddRange(editedFiles);
            }

            if (binaryFiles.Count == 0 && textFiles.Count == 0 && editedFiles.Count == 0)
            {
                return new StudioBuildResultDto(
                    false,
                    "Не удалось подготовить файлы ассетов для сборки (источники не найдены).",
                    null,
                    null,
                    null,
                    0,
                    0,
                    0,
                    preWarnings);
            }

            var outputName = string.IsNullOrWhiteSpace(request.ModName)
                ? $"pakchunk99-scum-studio-{DateTime.Now:yyyyMMdd-HHmmss}-WindowsNoEditor"
                : PathUtil.SanitizeFileName(request.ModName);

            var result = _builder.BuildFromEntries(
                binaryFiles,
                textFiles,
                outputName,
                request.InstallToGame,
                request.SeedCompanions,
                request.CreateZip,
                forceCompanionAssets,
                skipCompanionAssets,
                _ => { });

            var allWarnings = preWarnings
                .Concat(result.Warnings)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new StudioBuildResultDto(
                true,
                null,
                result.OutputPakPath,
                result.OutputZipPath,
                result.InstalledPakPath,
                result.PresetFileCount,
                result.SeededCompanionCount,
                result.OverrideCount,
                allWarnings);
        }
        catch (Exception ex)
        {
            return new StudioBuildResultDto(false, ex.Message, null, null, null, 0, 0, 0, []);
        }
    }

    private List<string> ResolveSelectedPresets(StudioBuildRequestDto request)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (request.EnabledPresetIds is { Count: > 0 })
        {
            foreach (var preset in request.EnabledPresetIds.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                result.Add(preset);
            }
        }

        result.IntersectWith(_presets.Select(x => x.Id));
        return result.ToList();
    }

    private List<string> ResolveSelectedFeatures(
        StudioBuildRequestDto request,
        IReadOnlyDictionary<string, StudioFeatureSettingDto> featureSettings)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (request.EnabledFeatureIds is { Count: > 0 })
        {
            foreach (var featureId in request.EnabledFeatureIds.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                result.Add(featureId);
            }
        }

        foreach (var setting in featureSettings.Values.Where(x => x.Enabled))
        {
            result.Add(setting.FeatureId);
        }

        result.IntersectWith(_featureAssetIds.Keys);
        return result.ToList();
    }

    private static Dictionary<string, StudioFeatureSettingDto> BuildFeatureSettingsMap(StudioBuildRequestDto request)
    {
        var result = new Dictionary<string, StudioFeatureSettingDto>(StringComparer.OrdinalIgnoreCase);
        if (request.FeatureSettings is null || request.FeatureSettings.Count == 0)
        {
            return result;
        }

        foreach (var setting in request.FeatureSettings)
        {
            if (string.IsNullOrWhiteSpace(setting.FeatureId))
            {
                continue;
            }

            result[setting.FeatureId] = setting with
            {
                FeatureId = setting.FeatureId.Trim()
            };
        }

        return result;
    }

    private static Dictionary<string, StudioAssetSettingDto> BuildAssetSettingsMap(StudioBuildRequestDto request)
    {
        var result = new Dictionary<string, StudioAssetSettingDto>(StringComparer.OrdinalIgnoreCase);
        if (request.AssetSettings is null || request.AssetSettings.Count == 0)
        {
            return result;
        }

        foreach (var setting in request.AssetSettings)
        {
            if (string.IsNullOrWhiteSpace(setting.AssetId))
            {
                continue;
            }

            result[setting.AssetId] = setting with
            {
                AssetId = setting.AssetId.Trim()
            };
        }

        return result;
    }

    private void ApplyFeatureDefaultsToAssetSettings(
        IEnumerable<string> selectedFeatureIds,
        IReadOnlyDictionary<string, StudioFeatureSettingDto> featureSettings,
        Dictionary<string, StudioAssetSettingDto> assetSettings)
    {
        foreach (var featureId in selectedFeatureIds)
        {
            if (!featureSettings.TryGetValue(featureId, out var featureSetting))
            {
                continue;
            }

            if (!_featureAssetIds.TryGetValue(featureId, out var featureAssets) || featureAssets.Count == 0)
            {
                continue;
            }

            foreach (var assetId in featureAssets)
            {
                if (assetSettings.TryGetValue(assetId, out var existing))
                {
                    assetSettings[assetId] = existing with
                    {
                        SourceMode = string.IsNullOrWhiteSpace(existing.SourceMode) ? featureSetting.SourceMode : existing.SourceMode,
                        CompanionMode = string.IsNullOrWhiteSpace(existing.CompanionMode) ? featureSetting.CompanionMode : existing.CompanionMode
                    };
                    continue;
                }

                assetSettings[assetId] = new StudioAssetSettingDto(
                    assetId,
                    true,
                    featureSetting.SourceMode,
                    featureSetting.CompanionMode);
            }
        }
    }

    private bool TryBuildSelectionFromAssetId(string assetId, out AssetSelection selection)
    {
        if (_presetFileById.TryGetValue(assetId, out var presetEntry))
        {
            selection = new AssetSelection(assetId, presetEntry.TargetRelativePath, presetEntry.SourcePath);
            return true;
        }

        if (TryParseDataTableRowAssetId(assetId, out var syntheticRelativePath, out var syntheticLaneId, out var syntheticRowName))
        {
            selection = new AssetSelection(assetId, syntheticRelativePath, null, syntheticLaneId, syntheticRowName);
            return true;
        }

        if (TryParseGameAssetId(assetId, out var gameRelativePath))
        {
            selection = new AssetSelection(assetId, gameRelativePath, null);
            return true;
        }

        selection = null!;
        return false;
    }

    private static bool TryParseGameAssetId(string assetId, out string relativePath)
    {
        const string prefix = "game::";
        if (!assetId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = string.Empty;
            return false;
        }

        var raw = assetId[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            relativePath = string.Empty;
            return false;
        }

        relativePath = PathUtil.NormalizeRelative(raw);
        return true;
    }

    private static bool TryParseDataTableRowAssetId(
        string assetId,
        out string relativePath,
        out string laneId,
        out string rowName)
    {
        relativePath = string.Empty;
        laneId = string.Empty;
        rowName = string.Empty;
        if (string.IsNullOrWhiteSpace(assetId)
            || !assetId.StartsWith(DataTableRowAssetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = assetId[DataTableRowAssetPrefix.Length..];
        var parts = payload.Split(new[] { "::" }, 2, StringSplitOptions.None);
        if (parts.Length != 2)
        {
            return false;
        }

        laneId = parts[0].Trim();
        rowName = parts[1].Trim();
        if (string.IsNullOrWhiteSpace(laneId) || string.IsNullOrWhiteSpace(rowName))
        {
            return false;
        }

        relativePath = laneId.ToLowerInvariant() switch
        {
            ItemSpawningParametersLaneId => ItemSpawningParametersTableRelativePath,
            ItemSpawningCooldownGroupsLaneId => ItemSpawningCooldownGroupsTableRelativePath,
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(relativePath);
    }

    private string? ResolveAssetSourcePath(AssetSelection selection, string sourceMode, List<string> warnings, bool includeCompanions = false)
    {
        if (sourceMode == "preset")
        {
            if (!string.IsNullOrWhiteSpace(selection.PresetSourcePath) && File.Exists(selection.PresetSourcePath))
            {
                return selection.PresetSourcePath;
            }

            warnings.Add($"Preset-источник недоступен для {selection.TargetRelativePath}, использую файл игры.");
            return TryResolveGameAssetSource(selection.TargetRelativePath, out var gamePath, warnings, includeCompanions)
                ? gamePath
                : null;
        }

        if (sourceMode == "game")
        {
            if (TryResolveGameAssetSource(selection.TargetRelativePath, out var gamePath, warnings, includeCompanions))
            {
                return gamePath;
            }

            if (!string.IsNullOrWhiteSpace(selection.PresetSourcePath) && File.Exists(selection.PresetSourcePath))
            {
                warnings.Add($"Файл игры не найден для {selection.TargetRelativePath}, использую preset-версию.");
                return selection.PresetSourcePath;
            }

            return null;
        }

        if (!string.IsNullOrWhiteSpace(selection.PresetSourcePath) && File.Exists(selection.PresetSourcePath))
        {
            return selection.PresetSourcePath;
        }

        return TryResolveGameAssetSource(selection.TargetRelativePath, out var autoGamePath, warnings, includeCompanions)
            ? autoGamePath
            : null;
    }

    private static string ResolveSourceMode(string? sourceMode, bool hasPresetSource)
    {
        var mode = (sourceMode ?? string.Empty).Trim().ToLowerInvariant();
        return mode switch
        {
            "game" => "game",
            "preset" => hasPresetSource ? "preset" : "game",
            _ => hasPresetSource ? "preset" : "game"
        };
    }

    private static string ResolveCompanionMode(string? companionMode)
    {
        var mode = (companionMode ?? string.Empty).Trim().ToLowerInvariant();
        return mode switch
        {
            "force" => "force",
            "none" => "none",
            _ => "auto"
        };
    }

    private List<StudioResearchVariantDto> BuildResearchVariants(
        string normalizedRelativePath,
        bool isBlueprintLike,
        IReadOnlyList<string> originalFiles,
        bool includeImportDiff,
        int maxItems,
        List<string> warnings)
    {
        var variants = new List<StudioResearchVariantDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var packageRoot in EnumerateResearchPackageRoots())
        {
            foreach (var candidate in FindResearchPackageCandidates(packageRoot, normalizedRelativePath))
            {
                if (!seen.Add(candidate.FilePath))
                {
                    continue;
                }

                var hasOriginalTwin = originalFiles.Count > 0;
                var variantWarnings = new List<string>();
                StudioResearchImportDeltaDto? importDelta = null;
                if (includeImportDiff && hasOriginalTwin)
                {
                    importDelta = TryBuildResearchImportDelta(originalFiles[0], candidate.FilePath, maxItems, variantWarnings);
                }

                var ownerHints = new List<StudioResearchOwnerHintDto>();
                if (isBlueprintLike || !hasOriginalTwin)
                {
                    ownerHints = FindResearchOwnerHints(candidate.PackageRoot, candidate.FilePath, normalizedRelativePath, maxItems);
                }

                var tags = BuildResearchVariantTags(
                    normalizedRelativePath,
                    isBlueprintLike,
                    hasOriginalTwin,
                    importDelta,
                    ownerHints);

                if (variantWarnings.Count > 0)
                {
                    warnings.AddRange(variantWarnings.Select(x => $"{Path.GetFileName(candidate.FilePath)}: {x}"));
                }

                variants.Add(new StudioResearchVariantDto(
                    candidate.FilePath,
                    candidate.PackageRoot,
                    candidate.RuntimeLayout,
                    hasOriginalTwin,
                    isBlueprintLike,
                    tags,
                    importDelta,
                    ownerHints));
            }
        }

        return variants
            .OrderByDescending(x => x.HasOriginalTwin)
            .ThenByDescending(x => x.OwnerHints.Count)
            .ThenBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<string> FindOriginalResearchFiles(string normalizedRelativePath, List<string> warnings)
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contentRelativePath = StripResearchContentPrefix(normalizedRelativePath)
            .Replace('/', Path.DirectorySeparatorChar);

        foreach (var root in GetResearchOriginalRoots())
        {
            foreach (var candidate in BuildResearchOriginalCandidates(root, contentRelativePath))
            {
                if (File.Exists(candidate))
                {
                    results.Add(candidate);
                }
            }
        }

        if (results.Count == 0
            && TryResolveGameAssetSource(normalizedRelativePath, out var extractedPath, warnings, includeCompanions: false)
            && File.Exists(extractedPath))
        {
            results.Add(extractedPath);
        }

        return results
            .OrderBy(path => path.Contains("uasset_extract_all", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IEnumerable<string> GetResearchOriginalRoots()
    {
        var workspace = _runtimePaths.WorkspaceRoot;
        var roots = new[]
        {
            Path.Combine(workspace, "analysis", "uasset_extract_all"),
            Path.Combine(workspace, "analysis", "orig_assets_full"),
            Path.Combine(workspace, "analysis", "orig_assets"),
            Path.Combine(workspace, "extract_original_full"),
            Path.Combine(workspace, "tmp_original_asset_extract")
        };

        foreach (var root in roots)
        {
            if (Directory.Exists(root))
            {
                yield return root;
            }
        }
    }

    private static IEnumerable<string> BuildResearchOriginalCandidates(string root, string contentRelativePath)
    {
        yield return Path.Combine(root, "SCUM", "Content", "ConZ_Files", contentRelativePath);
        yield return Path.Combine(root, "ConZ_Files", contentRelativePath);
        yield return Path.Combine(root, contentRelativePath);
    }

    private IEnumerable<string> EnumerateResearchPackageRoots()
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new[]
        {
            Path.Combine(_runtimePaths.WorkspaceRoot, "mods"),
            Path.Combine(_runtimePaths.WorkspaceRoot, "analysis", "user_mods")
        };

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var child in Directory.GetDirectories(root))
            {
                if (LooksLikeResearchPackageRoot(child))
                {
                    results.Add(child);
                }

                foreach (var grandChild in Directory.GetDirectories(child))
                {
                    if (LooksLikeResearchPackageRoot(grandChild))
                    {
                        results.Add(grandChild);
                    }
                }
            }
        }

        return results.OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
    }

    private static bool LooksLikeResearchPackageRoot(string directory)
    {
        var name = Path.GetFileName(directory);
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.Equals("SCUM", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Content", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ConZ_Files", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Engine", StringComparison.OrdinalIgnoreCase)
            || name.Equals("WindowsNoEditor", StringComparison.OrdinalIgnoreCase)
            || name.Equals("pak_content", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var directChildren = Directory.GetDirectories(directory)
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (directChildren.Contains("pak_content")
            || directChildren.Contains("SCUM")
            || directChildren.Contains("ConZ_Files")
            || directChildren.Contains("Items")
            || directChildren.Contains("UI")
            || directChildren.Contains("Characters")
            || directChildren.Contains("Blueprints")
            || directChildren.Contains("AdminCommands")
            || directChildren.Contains("Encounters")
            || directChildren.Contains("BaseBuilding")
            || directChildren.Contains("WorldEvents")
            || directChildren.Contains("Minigames")
            || directChildren.Contains("Models")
            || directChildren.Contains("Foliage"))
        {
            return true;
        }

        if (Directory.EnumerateFiles(directory, "*.uasset", SearchOption.TopDirectoryOnly).Any())
        {
            return true;
        }

        return directChildren.Count <= 5 && Directory.EnumerateFiles(directory, "*.uasset", SearchOption.AllDirectories).Take(1).Any();
    }

    private IEnumerable<ResearchPackageCandidate> FindResearchPackageCandidates(string packageRoot, string normalizedRelativePath)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contentRelativePath = StripResearchContentPrefix(normalizedRelativePath)
            .Replace('/', Path.DirectorySeparatorChar);

        var candidates = new[]
        {
            new ResearchPackageCandidate(
                Path.Combine(packageRoot, "pak_content", "SCUM", "Content", "ConZ_Files", contentRelativePath),
                packageRoot,
                "pak_content"),
            new ResearchPackageCandidate(
                Path.Combine(packageRoot, "SCUM", "Content", "ConZ_Files", contentRelativePath),
                packageRoot,
                "scum-content"),
            new ResearchPackageCandidate(
                Path.Combine(packageRoot, "ConZ_Files", contentRelativePath),
                packageRoot,
                "conz-files"),
            new ResearchPackageCandidate(
                Path.Combine(packageRoot, contentRelativePath),
                packageRoot,
                "flat-runtime")
        };

        foreach (var candidate in candidates)
        {
            var actualPath = ResolveExistingFilePath(candidate.FilePath);
            if (!string.IsNullOrWhiteSpace(actualPath) && seen.Add(actualPath))
            {
                yield return candidate with { FilePath = actualPath };
            }
        }
    }

    private static string? ResolveExistingFilePath(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName) || !Directory.Exists(directory))
        {
            return path;
        }

        return Directory.EnumerateFiles(directory, fileName, SearchOption.TopDirectoryOnly).FirstOrDefault() ?? path;
    }

    private static string NormalizeResearchRelativePath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return string.Empty;
        }

        var raw = assetPath.Trim();
        var separatorIndex = raw.IndexOf("::", StringComparison.Ordinal);
        if (separatorIndex >= 0 && separatorIndex < raw.Length - 2)
        {
            raw = raw[(separatorIndex + 2)..];
        }

        raw = PathUtil.NormalizeRelative(raw);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        if (raw.StartsWith("game/", StringComparison.OrdinalIgnoreCase))
        {
            raw = "scum/content/" + raw["game/".Length..];
        }
        else if (raw.StartsWith("content/conz_files/", StringComparison.OrdinalIgnoreCase))
        {
            raw = "scum/" + raw;
        }
        else if (raw.StartsWith("conz_files/", StringComparison.OrdinalIgnoreCase))
        {
            raw = "scum/content/" + raw;
        }
        else if (!raw.StartsWith("scum/content/conz_files/", StringComparison.OrdinalIgnoreCase)
            && LooksLikeConZContentRelativePath(raw))
        {
            raw = "scum/content/conz_files/" + raw;
        }

        return PathUtil.NormalizeRelative(raw).ToLowerInvariant();
    }

    private static bool LooksLikeConZContentRelativePath(string raw)
    {
        return raw.StartsWith("items/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("ui/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("characters/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("blueprints/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("admincommands/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("encounters/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("basebuilding/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("worldevents/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("minigames/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("foliage/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("quests/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("skills/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("data/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("cooking/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("vehicles/", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripResearchContentPrefix(string normalizedRelativePath)
    {
        if (normalizedRelativePath.StartsWith("scum/content/conz_files/", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedRelativePath["scum/content/conz_files/".Length..];
        }

        if (normalizedRelativePath.StartsWith("content/conz_files/", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedRelativePath["content/conz_files/".Length..];
        }

        if (normalizedRelativePath.StartsWith("conz_files/", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedRelativePath["conz_files/".Length..];
        }

        return normalizedRelativePath;
    }

    private static string BuildResearchGamePackagePath(string normalizedRelativePath)
    {
        var contentRelativePath = StripResearchContentPrefix(normalizedRelativePath);
        var withoutExtension = Path.ChangeExtension(contentRelativePath.Replace('\\', '/'), null)?.Replace('\\', '/') ?? contentRelativePath;
        return "/Game/ConZ_Files/" + withoutExtension.TrimStart('/');
    }

    private static bool IsBlueprintLikePath(string normalizedRelativePath)
    {
        var stem = Path.GetFileNameWithoutExtension(normalizedRelativePath);
        return stem.StartsWith("BP_", StringComparison.OrdinalIgnoreCase)
            || stem.StartsWith("ABP_", StringComparison.OrdinalIgnoreCase)
            || stem.StartsWith("WBP_", StringComparison.OrdinalIgnoreCase)
            || stem.StartsWith("BPC_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRuntimeOnlyWall(string normalizedRelativePath)
    {
        return normalizedRelativePath.Contains("/ui/hud/", StringComparison.OrdinalIgnoreCase)
            || normalizedRelativePath.Contains("/minigames/", StringComparison.OrdinalIgnoreCase)
            || normalizedRelativePath.Contains("/worldevents/", StringComparison.OrdinalIgnoreCase)
            || normalizedRelativePath.EndsWith("/bp_conzgamestate.uasset", StringComparison.OrdinalIgnoreCase);
    }

    private List<StudioResearchOwnerHintDto> FindResearchOwnerHints(
        string packageRoot,
        string assetFilePath,
        string normalizedRelativePath,
        int maxItems)
    {
        var stem = Path.GetFileNameWithoutExtension(assetFilePath);
        var gamePackagePath = BuildResearchGamePackagePath(normalizedRelativePath);
        var searchTokens = new[]
        {
            stem,
            gamePackagePath,
            Path.GetFileNameWithoutExtension(gamePackagePath)
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(token => token.Trim())
        .Where(token => token.Length >= 4)
        .ToList();

        if (searchTokens.Count == 0)
        {
            return [];
        }

        var tokenBytes = searchTokens
            .SelectMany(token => new[]
            {
                Encoding.ASCII.GetBytes(token),
                Encoding.Unicode.GetBytes(token)
            })
            .Where(bytes => bytes.Length > 0)
            .ToList();

        var results = new List<StudioResearchOwnerHintDto>();
        foreach (var candidate in Directory.EnumerateFiles(packageRoot, "*.uasset", SearchOption.AllDirectories))
        {
            if (candidate.Equals(assetFilePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (results.Count >= maxItems)
            {
                break;
            }

            if (FileContainsAnyAsciiToken(candidate, tokenBytes))
            {
                results.Add(new StudioResearchOwnerHintDto(
                    candidate,
                    $"Внутри файла найдено имя или игровой путь для {stem}."));
            }
        }

        return results;
    }

    private StudioResearchImportDeltaDto? TryBuildResearchImportDelta(string originalFile, string modFile, int maxItems, List<string> warnings)
    {
        try
        {
            var left = ReadResearchImportSnapshot(originalFile);
            var right = ReadResearchImportSnapshot(modFile);

            var addedImports = right.Imports
                .Except(left.Imports, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var removedImports = left.Imports
                .Except(right.Imports, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var addedSoftPaths = right.SoftObjectPaths
                .Except(left.SoftObjectPaths, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var removedSoftPaths = left.SoftObjectPaths
                .Except(right.SoftObjectPaths, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new StudioResearchImportDeltaDto(
                true,
                addedImports.Count,
                removedImports.Count,
                addedSoftPaths.Count,
                removedSoftPaths.Count,
                TakeLimited(addedImports, maxItems),
                TakeLimited(removedImports, maxItems),
                TakeLimited(addedSoftPaths, maxItems),
                TakeLimited(removedSoftPaths, maxItems),
                warnings.ToList());
        }
        catch (OutOfMemoryException)
        {
            warnings.Add("Импорт-дифф не удалось построить: не хватило памяти на этом ассете.");
            return null;
        }
        catch (Exception ex)
        {
            warnings.Add($"Импорт-дифф не удалось построить: {ex.Message}");
            return null;
        }
    }

    private static ResearchImportSnapshot ReadResearchImportSnapshot(string path)
    {
        var flags = CustomSerializationFlags.SkipLoadingExports | CustomSerializationFlags.SkipPreloadDependencyLoading;
        var asset = new UAsset(path, false, EngineVersion.VER_UE4_27, null, flags);

        var imports = asset.Imports
            .Select(FormatResearchImportLabel)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var softPaths = (asset.SoftObjectPathList ?? [])
            .Select(FormatResearchSoftPath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ResearchImportSnapshot(imports, softPaths);
    }

    private static string FormatResearchImportLabel(Import import)
    {
        var classPackage = import.ClassPackage?.ToString() ?? string.Empty;
        var className = import.ClassName?.ToString() ?? string.Empty;
        var objectName = import.ObjectName?.ToString() ?? string.Empty;
        return $"{classPackage} -> {className}:{objectName}";
    }

    private static string FormatResearchSoftPath(FSoftObjectPath path)
    {
        var packageName = path.AssetPath.PackageName?.ToString() ?? string.Empty;
        var assetName = path.AssetPath.AssetName?.ToString() ?? string.Empty;
        var subPath = path.SubPathString?.ToString() ?? string.Empty;

        var result = packageName;
        if (!string.IsNullOrWhiteSpace(assetName))
        {
            result = string.IsNullOrWhiteSpace(result) ? assetName : $"{result}.{assetName}";
        }

        if (!string.IsNullOrWhiteSpace(subPath))
        {
            result = string.IsNullOrWhiteSpace(result) ? subPath : $"{result}:{subPath}";
        }

        return result;
    }

    private static bool FileContainsAnyAsciiToken(string filePath, IReadOnlyList<byte[]> tokens)
    {
        var info = new FileInfo(filePath);
        if (!info.Exists || info.Length <= 0 || info.Length > 4 * 1024 * 1024)
        {
            return false;
        }

        var bytes = File.ReadAllBytes(filePath);
        foreach (var token in tokens)
        {
            if (token.Length == 0)
            {
                continue;
            }

            if (ContainsToken(bytes, token))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsToken(byte[] source, byte[] token)
    {
        if (token.Length == 0 || source.Length < token.Length)
        {
            return false;
        }

        for (var i = 0; i <= source.Length - token.Length; i++)
        {
            var matched = true;
            for (var j = 0; j < token.Length; j++)
            {
                if (source[i + j] != token[j])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> BuildResearchVariantTags(
        string normalizedRelativePath,
        bool isBlueprintLike,
        bool hasOriginalTwin,
        StudioResearchImportDeltaDto? importDelta,
        IReadOnlyList<StudioResearchOwnerHintDto> ownerHints)
    {
        var tags = new List<string>();
        var runtimeOnlyWall = isBlueprintLike && IsRuntimeOnlyWall(normalizedRelativePath);

        if (hasOriginalTwin)
        {
            tags.Add("same-path override");
            if (isBlueprintLike)
            {
                tags.Add("same-path BP override");
            }

            if (HasResearchImportChanges(importDelta))
            {
                tags.Add("dependency retarget / import rewrite");
            }
            else
            {
                tags.Add("typed-property or serialized override");
            }

            if (HasResearchLocalDependencyAdds(importDelta))
            {
                tags.Add("hybrid replace + new local dependency");
            }
        }
        else if (isBlueprintLike)
        {
            if (ownerHints.Count > 0)
            {
                tags.Add("new BP + owner link");
            }
            else if (runtimeOnlyWall)
            {
                tags.Add("runtime-only wall");
            }
            else
            {
                tags.Add("owner-unproven new BP");
            }
        }
        else
        {
            tags.Add(ownerHints.Count > 0 ? "new asset + owner link" : "new asset / owner not proven");
        }

        if (runtimeOnlyWall)
        {
            tags.Add("runtime-only wall");
            tags.Add("replace-only / research-only");
        }

        return tags
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasResearchImportChanges(StudioResearchImportDeltaDto? importDelta)
    {
        return importDelta is not null
            && (importDelta.AddedImportCount > 0
                || importDelta.RemovedImportCount > 0
                || importDelta.AddedSoftObjectPathCount > 0
                || importDelta.RemovedSoftObjectPathCount > 0);
    }

    private static bool HasResearchLocalDependencyAdds(StudioResearchImportDeltaDto? importDelta)
    {
        if (importDelta is null)
        {
            return false;
        }

        return importDelta.AddedImports.Any(x => x.Contains("/Game/ConZ_Files/", StringComparison.OrdinalIgnoreCase))
            || importDelta.AddedSoftObjectPaths.Any(x => x.Contains("/Game/ConZ_Files/", StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> TakeLimited(List<string> values, int maxItems)
    {
        return values.Count <= maxItems
            ? values
            : values.Take(maxItems).ToList();
    }

    private bool TryResolveGameAssetSource(string targetRelativePath, out string sourcePath, List<string> warnings, bool includeCompanions = false)
    {
        var normalized = PathUtil.NormalizeRelative(targetRelativePath).ToLowerInvariant();
        if (_gameExtractedSourceByPath.TryGetValue(normalized, out var cached) && File.Exists(cached))
        {
            if (includeCompanions)
            {
                EnsureExtractedCompanionFiles(normalized, warnings);
            }

            sourcePath = cached;
            return true;
        }

        var pakIndex = GetOrLoadPakIndex();
        if (!pakIndex.TryGetPakFor(normalized, out var pakPath))
        {
            warnings.Add($"Файл не найден в pak-индексе игры: {targetRelativePath}");
            sourcePath = string.Empty;
            return false;
        }

        var extractRoot = Path.Combine(_runtimePaths.TempRoot, "asset-source-cache");
        Directory.CreateDirectory(extractRoot);

        var expected = Path.Combine(extractRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(expected))
        {
            var fileName = Path.GetFileName(normalized);
            if (!TryExtractAssetFromPak(pakPath, extractRoot, fileName, warnings))
            {
                warnings.Add($"Не удалось извлечь {targetRelativePath} из {Path.GetFileName(pakPath)}");
                sourcePath = string.Empty;
                return false;
            }
        }

        sourcePath = File.Exists(expected)
            ? expected
            : TryFindExtractedMatch(Path.Combine(_runtimePaths.TempRoot, "asset-source-cache"), normalized) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            warnings.Add($"Не удалось подготовить исходный файл игры: {targetRelativePath}");
            sourcePath = string.Empty;
            return false;
        }

        if (includeCompanions)
        {
            EnsureExtractedCompanionFiles(normalized, warnings);
        }

        _gameExtractedSourceByPath[normalized] = sourcePath;
        return true;
    }

    private void EnsureExtractedCompanionFiles(string normalizedRelativePath, List<string> warnings)
    {
        var extension = Path.GetExtension(normalizedRelativePath);
        if (!extension.Equals(".uasset", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".umap", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var pakIndex = GetOrLoadPakIndex();
        var extractRoot = Path.Combine(_runtimePaths.TempRoot, "asset-source-cache");
        Directory.CreateDirectory(extractRoot);

        var stem = normalizedRelativePath[..^extension.Length];
        foreach (var companionExt in new[] { ".uexp", ".ubulk", ".uptnl" })
        {
            var companionRelative = $"{stem}{companionExt}";
            var expected = Path.Combine(extractRoot, companionRelative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(expected))
            {
                continue;
            }

            if (!pakIndex.TryGetPakFor(companionRelative, out var pakPath))
            {
                continue;
            }

            var companionFileName = Path.GetFileName(companionRelative);
            _ = TryExtractAssetFromPak(pakPath, extractRoot, companionFileName, warnings);
        }
    }

    private string PrepareIsolatedAssetReadSource(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        if (!extension.Equals(".uasset", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".umap", StringComparison.OrdinalIgnoreCase))
        {
            return sourcePath;
        }

        var scratchRoot = Path.Combine(_runtimePaths.TempRoot, "asset-read-scratch");
        var scratchDir = Path.Combine(scratchRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratchDir);

        var isolatedPath = Path.Combine(scratchDir, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, isolatedPath, overwrite: true);
        CopyCompanionFilesForEdit(sourcePath, isolatedPath);
        return isolatedPath;
    }

    private bool TryExtractAssetFromPak(string pakPath, string extractRoot, string fileName, List<string> warnings)
    {
        var cryptoRoot = Path.Combine(_runtimePaths.TempRoot, "asset-source-crypto");
        Directory.CreateDirectory(cryptoRoot);
        var cryptoPath = CryptoKeyWriter.Write(cryptoRoot, DefaultAesKeyHex);

        var extractResult = ProcessRunner.Run(
            _unrealPakPath,
            $"\"{pakPath}\" -Extract \"{extractRoot}\" -extracttomountpoint -Filter=\"*{fileName}\" -cryptokeys=\"{cryptoPath}\"",
            _runtimePaths.WorkspaceRoot,
            _ => { },
            timeoutMs: 10 * 60 * 1000);

        if (extractResult.ExitCode == 0)
        {
            return true;
        }

        warnings.Add($"UnrealPak extract failed for {fileName} from {Path.GetFileName(pakPath)}");
        return false;
    }

    private static bool IsLikelyPlayableItemAsset(string relativePath)
    {
        var normalized = PathUtil.NormalizeRelative(relativePath).ToLowerInvariant();
        if (!normalized.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalized.Contains("/item_icons/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/icons/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/ui/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/crafting/recipes/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/crafting/ingredients/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/items/spawnerpresets/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/items/spawnerpresets2/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/items/tags/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/items/crafting/chopping/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileNameWithoutExtension(normalized);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (fileName.StartsWith("ico_", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("icon_", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("_icon", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("t_", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("tx_", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("m_", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("mi_", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("mat_", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("sm_", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("sk_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool IsModdableGameAsset(string relativePath)
    {
        var normalized = PathUtil.NormalizeRelative(relativePath).ToLowerInvariant();
        if (!normalized.StartsWith("scum/content/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalized.EndsWith(".uexp", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".ubulk", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".uptnl", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".locres", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var extension = Path.GetExtension(normalized);
        return extension.Equals(".uasset", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".umap", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ini", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveSurfaceLabel(string relativePath)
    {
        var path = PathUtil.NormalizeRelative(relativePath).ToLowerInvariant();
        if (path.Contains("/items/crafting/recipes/", StringComparison.OrdinalIgnoreCase))
        {
            return "Рецепты крафта";
        }

        if (path.Contains("/items/crafting/ingredients/", StringComparison.OrdinalIgnoreCase))
        {
            return "Ингредиенты крафта";
        }

        if (path.Contains("/economy/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("tradeable", StringComparison.OrdinalIgnoreCase))
        {
            return "Экономика";
        }

        if (path.Contains("/quests/", StringComparison.OrdinalIgnoreCase))
        {
            return "Квесты";
        }

        if (path.Contains("/spawnequipment/", StringComparison.OrdinalIgnoreCase))
        {
            return "Стартовый набор";
        }

        if (path.Contains("/encounters/", StringComparison.OrdinalIgnoreCase))
        {
            return "События";
        }

        if (path.Contains("/npcs/", StringComparison.OrdinalIgnoreCase))
        {
            return "NPC";
        }

        if (path.Contains("radiation", StringComparison.OrdinalIgnoreCase))
        {
            return "Радиация";
        }

        if (path.Contains("/vehicles/", StringComparison.OrdinalIgnoreCase))
        {
            return "Транспорт";
        }

        if (path.Contains("/weapons/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/items/weapons/", StringComparison.OrdinalIgnoreCase))
        {
            return "Оружие";
        }

        if (path.Contains("/items/", StringComparison.OrdinalIgnoreCase))
        {
            return "Предметы";
        }

        if (path.Contains("/ui/", StringComparison.OrdinalIgnoreCase))
        {
            return "Интерфейс";
        }

        if (path.Contains("/data/", StringComparison.OrdinalIgnoreCase))
        {
            return "Данные";
        }

        return "Общее";
    }

    private static string? TryFindExtractedMatch(string extractRoot, string targetRelativePath)
    {
        var expectedNormalized = PathUtil.NormalizeRelative(targetRelativePath);
        var fileName = Path.GetFileName(expectedNormalized);
        foreach (var candidate in Directory.EnumerateFiles(extractRoot, fileName, SearchOption.AllDirectories))
        {
            var rel = PathUtil.NormalizeRelative(Path.GetRelativePath(extractRoot, candidate));
            if (rel.Equals(expectedNormalized, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private List<BuildTextFile> BuildRecipeFiles(StudioBuildRequestDto request)
    {
        var files = new List<BuildTextFile>();
        var recipes = request.Recipes ?? [];
        if (recipes.Count == 0)
        {
            return files;
        }

        var safeModName = string.IsNullOrWhiteSpace(request.ModName)
            ? $"scum-studio-{DateTime.Now:yyyyMMdd-HHmmss}"
            : PathUtil.SanitizeFileName(request.ModName);

        var manifest = new
        {
            generatedAtUtc = DateTime.UtcNow,
            modName = safeModName,
            note = "Recipe plans generated by ScumPakWizard Studio",
            recipeCount = recipes.Count,
            recipes
        };

        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        files.Add(new BuildTextFile(
            $"SCUM/Content/ConZ_Files/Data/ModFactory/RecipePlans/{safeModName}_manifest.json",
            manifestJson));

        for (var i = 0; i < recipes.Count; i++)
        {
            var recipe = recipes[i];
            var recipeJson = JsonSerializer.Serialize(recipe, new JsonSerializerOptions { WriteIndented = true });
            files.Add(new BuildTextFile(
                $"SCUM/Content/ConZ_Files/Data/ModFactory/RecipePlans/{safeModName}_{i + 1:000}.json",
                recipeJson));
        }

        return files;
    }

    private List<BuildInputFile> BuildEditedAssetFiles(
        StudioBuildRequestDto request,
        IReadOnlyDictionary<string, StudioAssetSettingDto> assetSettings,
        List<string> warnings)
    {
        var result = new List<BuildInputFile>();
        if (request.AssetEdits is null || request.AssetEdits.Count == 0)
        {
            return result;
        }

        var runRoot = Path.Combine(_runtimePaths.TempRoot, "asset-edits", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(runRoot);

        var preparedEdits = new List<(StudioAssetEditDto EditRequest, AssetSelection Selection, string SourcePath, bool HasFieldEdits, bool HasListEdits)>();

        foreach (var editRequest in request.AssetEdits)
        {
            if (string.IsNullOrWhiteSpace(editRequest.AssetId))
            {
                continue;
            }

            var hasFieldEdits = editRequest.Edits is { Count: > 0 };
            var hasListEdits = editRequest.ListEdits is { Count: > 0 };
            if (!hasFieldEdits && !hasListEdits)
            {
                continue;
            }

            if (!TryBuildSelectionFromAssetId(editRequest.AssetId, out var selection))
            {
                warnings.Add($"Asset edit skipped: неизвестный assetId {editRequest.AssetId}");
                continue;
            }

            var setting = assetSettings.GetValueOrDefault(editRequest.AssetId);
            var sourceMode = ResolveSourceMode(setting?.SourceMode, selection.PresetSourcePath is not null);
            var sourcePath = ResolveAssetSourcePath(selection, sourceMode, warnings, includeCompanions: true);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                warnings.Add($"Asset edit skipped: источник не найден для {selection.TargetRelativePath}");
                continue;
            }

            preparedEdits.Add((editRequest, selection, sourcePath, hasFieldEdits, hasListEdits));
        }

        if (preparedEdits.Count == 0)
        {
            return result;
        }

        var groupedEdits = new Dictionary<string, List<(StudioAssetEditDto EditRequest, AssetSelection Selection, string SourcePath, bool HasFieldEdits, bool HasListEdits)>>(StringComparer.OrdinalIgnoreCase);
        var groupSourcePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var groupSelections = new Dictionary<string, AssetSelection>(StringComparer.OrdinalIgnoreCase);
        var groupOrder = new List<string>();

        foreach (var prepared in preparedEdits)
        {
            var groupKey = PathUtil.NormalizeRelative(prepared.Selection.TargetRelativePath).ToLowerInvariant();
            if (!groupedEdits.TryGetValue(groupKey, out var group))
            {
                group = [];
                groupedEdits[groupKey] = group;
                groupSourcePaths[groupKey] = prepared.SourcePath;
                groupSelections[groupKey] = prepared.Selection;
                groupOrder.Add(groupKey);
            }
            else
            {
                var existingSourcePath = groupSourcePaths[groupKey];
                if (!string.Equals(
                        Path.GetFullPath(existingSourcePath),
                        Path.GetFullPath(prepared.SourcePath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add(
                        $"Для {prepared.Selection.TargetRelativePath} найдены правки с разными источниками. Использую первый источник: {Path.GetFileName(existingSourcePath)}.");
                }
            }

            group.Add(prepared);
        }

        foreach (var groupKey in groupOrder)
        {
            var editsForAsset = groupedEdits[groupKey];
            if (editsForAsset.Count == 0)
            {
                continue;
            }

            var sourcePath = groupSourcePaths[groupKey];
            var selection = groupSelections[groupKey];
            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            var editDir = Path.Combine(runRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(editDir);
            var editedAssetPath = Path.Combine(editDir, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, editedAssetPath, overwrite: true);
            CopyCompanionFilesForEdit(sourcePath, editedAssetPath);

            bool changed;
            if (extension.Equals(".uasset", StringComparison.OrdinalIgnoreCase))
            {
                changed = false;
                foreach (var prepared in editsForAsset)
                {
                    if (prepared.HasListEdits)
                    {
                        changed |= ApplyUassetListEdits(editedAssetPath, prepared.EditRequest.ListEdits!, warnings);
                    }

                    if (prepared.HasFieldEdits)
                    {
                        changed |= ApplyUassetEdits(editedAssetPath, prepared.EditRequest.Edits!, warnings);
                    }
                }
            }
            else if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                changed = false;
                foreach (var prepared in editsForAsset)
                {
                    if (prepared.HasListEdits)
                    {
                        changed |= ApplyJsonListEdits(editedAssetPath, prepared.EditRequest.ListEdits!, warnings);
                    }

                    if (prepared.HasFieldEdits)
                    {
                        changed |= ApplyJsonEdits(editedAssetPath, prepared.EditRequest.Edits!, warnings);
                    }
                }
            }
            else
            {
                warnings.Add($"Asset edit skipped: формат {extension} пока не поддерживается для {selection.TargetRelativePath}");
                continue;
            }

            if (!changed)
            {
                continue;
            }

            result.Add(new BuildInputFile(editedAssetPath, selection.TargetRelativePath));
            AddEditedCompanionInputs(result, editedAssetPath, selection.TargetRelativePath);
        }

        return result;
    }

    private static void AddEditedCompanionInputs(List<BuildInputFile> result, string editedAssetPath, string targetRelativePath)
    {
        if (!targetRelativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var stem = Path.Combine(
            Path.GetDirectoryName(editedAssetPath) ?? string.Empty,
            Path.GetFileNameWithoutExtension(editedAssetPath));
        var targetStem = targetRelativePath[..^".uasset".Length];

        foreach (var ext in new[] { ".uexp", ".ubulk", ".uptnl" })
        {
            var companionPath = $"{stem}{ext}";
            if (!File.Exists(companionPath))
            {
                continue;
            }

            result.Add(new BuildInputFile(companionPath, $"{targetStem}{ext}"));
        }
    }

    private static void CopyCompanionFilesForEdit(string sourceAssetPath, string editedAssetPath)
    {
        var sourceDir = Path.GetDirectoryName(sourceAssetPath);
        var targetDir = Path.GetDirectoryName(editedAssetPath);
        if (string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(targetDir))
        {
            return;
        }

        var stem = Path.GetFileNameWithoutExtension(sourceAssetPath);
        foreach (var ext in new[] { ".uexp", ".ubulk", ".uptnl" })
        {
            var src = Path.Combine(sourceDir, $"{stem}{ext}");
            if (!File.Exists(src))
            {
                continue;
            }

            var dst = Path.Combine(targetDir, $"{stem}{ext}");
            File.Copy(src, dst, overwrite: true);
        }
    }

    private bool ApplyUassetEdits(string editedAssetPath, List<StudioFieldEditDto> edits, List<string> warnings)
    {
        var asset = new UAsset(editedAssetPath, EngineVersion.VER_UE4_27, null, CustomSerializationFlags.None);
        var applied = 0;

        if (edits.Any(x => x.FieldPath?.StartsWith(RecipeFieldPrefix, StringComparison.OrdinalIgnoreCase) == true)
            && TryGetFirstNormalExport(asset, out var recipeExport, out _))
        {
            applied += ApplyCraftingRecipeEdits(asset, recipeExport, edits, warnings);
        }

        if (edits.Any(x => x.FieldPath?.StartsWith(StarterSpawnFieldPrefix, StringComparison.OrdinalIgnoreCase) == true)
            && TryGetFirstNormalExport(asset, out var starterExport, out _))
        {
            applied += ApplyStarterSpawnEquipmentEdits(asset, starterExport, edits, warnings);
        }

        foreach (var edit in edits.Where(x =>
                     x.FieldPath?.StartsWith(StarterSpawnFieldPrefix, StringComparison.OrdinalIgnoreCase) != true
                     && x.FieldPath?.StartsWith(RecipeFieldPrefix, StringComparison.OrdinalIgnoreCase) != true))
        {
            if (string.IsNullOrWhiteSpace(edit.FieldPath))
            {
                continue;
            }

            if (TryApplySyntheticFieldEdit(asset, edit, warnings, out var syntheticApplied))
            {
                if (syntheticApplied)
                {
                    applied++;
                }

                continue;
            }

            if (TryApplyRichCurveKeyEdit(asset, edit, warnings))
            {
                applied++;
                continue;
            }

            if (!TryResolveUassetProperty(asset, edit.FieldPath, out var property))
            {
                warnings.Add($"UAsset edit skipped: путь не найден {edit.FieldPath}");
                continue;
            }

            if (!TryAssignEditablePropertyValue(asset, property, edit.Value, out var error))
            {
                warnings.Add($"UAsset edit skipped: {edit.FieldPath} ({error})");
                continue;
            }

            applied++;
        }

        if (applied == 0)
        {
            return false;
        }

        asset.Write(editedAssetPath);
        return true;
    }

    private static bool TryApplySyntheticFieldEdit(
        UAsset asset,
        StudioFieldEditDto edit,
        List<string> warnings,
        out bool applied)
    {
        applied = false;

        if (edit.FieldPath.StartsWith(ForeignSubstanceAttributeFieldPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryApplyForeignSubstanceAttributeFieldEdit(asset, edit, warnings, out applied);
        }

        if (edit.FieldPath.StartsWith(WeaponSyntheticFieldPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryApplyWeaponSyntheticFieldEdit(asset, edit, warnings, out applied);
        }

        return false;
    }

    private static bool TryApplyForeignSubstanceAttributeFieldEdit(
        UAsset asset,
        StudioFieldEditDto edit,
        List<string> warnings,
        out bool applied)
    {
        applied = false;
        var propertyName = edit.FieldPath[ForeignSubstanceAttributeFieldPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            warnings.Add("UAsset edit skipped: не указан параметр вещества-модификатора.");
            return true;
        }

        var allowedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "_strengthChange",
            "_strengthModifier",
            "_constitutionChange",
            "_constitutionModifier",
            "_dexterityChange",
            "_dexterityModifier",
            "_intelligenceChange",
            "_intelligenceModifier"
        };

        if (!allowedProperties.Contains(propertyName))
        {
            warnings.Add($"UAsset edit skipped: неподдерживаемый параметр вещества {propertyName}");
            return true;
        }

        if (!IsForeignSubstanceAttributeModifierAsset(asset))
        {
            warnings.Add($"UAsset edit skipped: параметр {propertyName} можно добавлять только в вещества, которые модифицируют характеристики.");
            return true;
        }

        if (!TryFindForeignSubstanceModifierExport(asset, out var export, out _))
        {
            warnings.Add($"UAsset edit skipped: экспорт для {propertyName} не найден.");
            return true;
        }

        if (!float.TryParse(edit.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
        {
            warnings.Add($"UAsset edit skipped: {propertyName} (ожидается float)");
            return true;
        }

        var conflictingProperty = export.Data
            .OfType<PropertyData>()
            .FirstOrDefault(property =>
                PropertyNamesMatch(property.Name?.ToString(), propertyName)
                && property is not FloatPropertyData);
        if (conflictingProperty is not null)
        {
            warnings.Add($"UAsset edit skipped: {propertyName} уже существует, но хранится не как число.");
            return true;
        }

        var existingFloatProperty = FindTopLevelFloatPropertyLoose(export, propertyName);
        if (existingFloatProperty is not null)
        {
            existingFloatProperty.Value = value;
        }
        else
        {
            EnsureTopLevelFloatProperty(asset, export, propertyName).Value = value;
        }

        applied = true;
        return true;
    }

    private static bool TryApplyWeaponSyntheticFieldEdit(
        UAsset asset,
        StudioFieldEditDto edit,
        List<string> warnings,
        out bool applied)
    {
        applied = false;
        var propertyName = edit.FieldPath[WeaponSyntheticFieldPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            warnings.Add("UAsset edit skipped: не указан параметр оружия.");
            return true;
        }

        var allowedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ViewKickMultiplier",
            "MaxRecoilOffset",
            "RecoilRecoverySpeed"
        };
        if (!allowedProperties.Contains(propertyName))
        {
            warnings.Add($"UAsset edit skipped: неподдерживаемый параметр оружия {propertyName}");
            return true;
        }

        if (!TryFindWeaponOwnerExport(asset, out var export, out _))
        {
            warnings.Add($"UAsset edit skipped: экспорт для {propertyName} не найден.");
            return true;
        }

        if (!float.TryParse(edit.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
        {
            warnings.Add($"UAsset edit skipped: {propertyName} (ожидается float)");
            return true;
        }

        var conflictingProperty = export.Data
            .OfType<PropertyData>()
            .FirstOrDefault(property =>
                PropertyNamesMatch(property.Name?.ToString(), propertyName)
                && property is not FloatPropertyData);
        if (conflictingProperty is not null)
        {
            warnings.Add($"UAsset edit skipped: {propertyName} уже существует, но хранится не как число.");
            return true;
        }

        EnsureTopLevelFloatProperty(asset, export, propertyName).Value = value;
        applied = true;
        return true;
    }

    private static int ApplyCraftingRecipeEdits(
        UAsset asset,
        NormalExport export,
        List<StudioFieldEditDto> edits,
        List<string> warnings)
    {
        var applied = 0;
        foreach (var edit in edits.Where(x => x.FieldPath.StartsWith(RecipeFieldPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            var suffix = edit.FieldPath[RecipeFieldPrefix.Length..];
            if (string.IsNullOrWhiteSpace(suffix)
                || suffix.Contains("allowed-display", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (suffix.Equals("product", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryBuildSoftObjectReference(asset, edit.Value, out var productReference))
                {
                    warnings.Add("Рецепт не изменён: итоговый предмет должен быть ссылкой вида /Game/.../Asset.Asset_C");
                    continue;
                }

                EnsureTopLevelSoftObjectProperty(asset, export, "Product").Value = productReference;
                applied++;
                continue;
            }

            if (suffix.Equals("product-quantity", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(edit.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity) || quantity < 1)
                {
                    warnings.Add("Рецепт не изменён: количество результата должно быть целым числом от 1.");
                    continue;
                }

                EnsureTopLevelIntProperty(asset, export, "ProductQuantity").Value = quantity;
                applied++;
                continue;
            }

            if (suffix.Equals("skill-info", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryBuildObjectReferenceFromPicker(asset, edit.Value, out var skillReference, out var error))
                {
                    warnings.Add($"Рецепт не изменён: основной навык не удалось сохранить ({error})");
                    continue;
                }

                EnsureTopLevelObjectProperty(asset, export, "RelevantSkill").Value = skillReference;
                applied++;
                continue;
            }

            if (suffix.StartsWith("duration:", StringComparison.OrdinalIgnoreCase))
            {
                var property = EnsureTopLevelStructProperty(asset, export, "Duration", "PerSkillLevelFloatData");
                if (TryApplyRecipePerSkillFloatEdit(asset, property, suffix["duration:".Length..], edit.Value, out var error))
                {
                    applied++;
                }
                else
                {
                    warnings.Add($"Рецепт: время крафта не изменено ({error})");
                }

                continue;
            }

            if (suffix.StartsWith("exp:", StringComparison.OrdinalIgnoreCase))
            {
                var property = EnsureTopLevelStructProperty(asset, export, "ExperienceReward", "PerSkillLevelFloatData");
                if (TryApplyRecipePerSkillFloatEdit(asset, property, suffix["exp:".Length..], edit.Value, out var error))
                {
                    applied++;
                }
                else
                {
                    warnings.Add($"Рецепт: опыт за крафт не изменён ({error})");
                }

                continue;
            }

            if (suffix.StartsWith("fame:", StringComparison.OrdinalIgnoreCase))
            {
                var property = EnsureTopLevelStructProperty(asset, export, "FamePointReward", "PerSkillLevelFloatData");
                if (TryApplyRecipePerSkillFloatEdit(asset, property, suffix["fame:".Length..], edit.Value, out var error))
                {
                    applied++;
                }
                else
                {
                    warnings.Add($"Рецепт: очки славы не изменены ({error})");
                }

                continue;
            }

            if (suffix.StartsWith("ingredient:", StringComparison.OrdinalIgnoreCase))
            {
                if (TryApplyCraftingIngredientEdit(asset, export, suffix, edit.Value, out var error))
                {
                    applied++;
                }
                else if (!string.IsNullOrWhiteSpace(error))
                {
                    warnings.Add($"Рецепт: ингредиент не изменён ({error})");
                }

                continue;
            }

            warnings.Add($"Рецепт: неизвестная настройка {suffix}");
        }

        return applied;
    }

    private static bool TryApplyRichCurveKeyEdit(UAsset asset, StudioFieldEditDto edit, List<string> warnings)
    {
        var markerIndex = edit.FieldPath.IndexOf("/rk:", StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return false;
        }

        var basePath = edit.FieldPath[..markerIndex];
        var keyPart = edit.FieldPath[(markerIndex + 4)..].Trim();
        if (!TryResolveUassetProperty(asset, basePath, out var property) || property is not RichCurveKeyPropertyData richCurveKey)
        {
            warnings.Add($"UAsset edit skipped: точка кривой не найдена {edit.FieldPath}");
            return true;
        }

        if (!float.TryParse(edit.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var numericValue))
        {
            warnings.Add($"UAsset edit skipped: {edit.FieldPath} (ожидается float)");
            return true;
        }

        var key = richCurveKey.Value;
        if (keyPart.Equals("time", StringComparison.OrdinalIgnoreCase))
        {
            key.Time = numericValue;
            richCurveKey.Value = key;
            return true;
        }

        if (keyPart.Equals("value", StringComparison.OrdinalIgnoreCase))
        {
            key.Value = numericValue;
            richCurveKey.Value = key;
            return true;
        }

        warnings.Add($"UAsset edit skipped: неизвестный параметр точки кривой {edit.FieldPath}");
        return true;
    }

    private static int ApplyStarterSpawnEquipmentEdits(
        UAsset asset,
        NormalExport export,
        List<StudioFieldEditDto> edits,
        List<string> warnings)
    {
        var applied = 0;
        var conditionEdits = new List<StudioFieldEditDto>(16);

        foreach (var edit in edits.Where(x => x.FieldPath.StartsWith(StarterSpawnFieldPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            var suffix = edit.FieldPath[StarterSpawnFieldPrefix.Length..];
            if (suffix.StartsWith("flag:", StringComparison.OrdinalIgnoreCase)
                || suffix.StartsWith("character:", StringComparison.OrdinalIgnoreCase))
            {
                conditionEdits.Add(edit);
                continue;
            }

            if (suffix.Equals("item", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryBuildSoftObjectReference(asset, edit.Value, out var softReference))
                {
                    warnings.Add("Стартовый предмет не изменён: ссылка на предмет должна быть вида /Game/.../Asset.Asset_C");
                    continue;
                }

                var property = EnsureTopLevelSoftObjectProperty(asset, export, "ItemClass");
                property.Value = softReference;
                applied++;
                continue;
            }

            if (suffix.Equals("equip", StringComparison.OrdinalIgnoreCase))
            {
                if (!StarterEquipOptions.Any(x => string.Equals(x.Value, edit.Value, StringComparison.OrdinalIgnoreCase)))
                {
                    warnings.Add($"Стартовый предмет: неподдерживаемый способ выдачи {edit.Value}");
                    continue;
                }

                var property = EnsureTopLevelEnumProperty(asset, export, "EPrisonerItemEquipType", "EquipType");
                property.Value = CreateFName(asset, edit.Value);
                applied++;
                continue;
            }

            if (suffix.Equals("biome", StringComparison.OrdinalIgnoreCase))
            {
                if (!StarterBiomeOptions.Any(x => string.Equals(x.Value, edit.Value, StringComparison.OrdinalIgnoreCase)))
                {
                    warnings.Add($"Стартовый предмет: неподдерживаемое ограничение по биому {edit.Value}");
                    continue;
                }

                var property = EnsureTopLevelEnumProperty(asset, export, "EBiomeType", "BiomeRequriment", "BiomeRequirement");
                property.Value = CreateFName(asset, edit.Value);
                applied++;
                continue;
            }
        }

        if (conditionEdits.Count > 0)
        {
            applied += ApplyStarterSpawnConditionEdits(asset, export, conditionEdits, warnings);
        }

        return applied;
    }

    private static int ApplyStarterSpawnConditionEdits(
        UAsset asset,
        NormalExport export,
        List<StudioFieldEditDto> edits,
        List<string> warnings)
    {
        var existingCondition = FindTopLevelProperty<StructPropertyData>(export, "Condition", out _);
        var model = new StarterSpawnConditionModel();

        if (existingCondition is not null)
        {
            if (!TryParseStarterSpawnCondition(existingCondition, out model, out var parseWarning))
            {
                warnings.Add($"Условия выдачи стартового предмета не изменены: {parseWarning ?? "не удалось безопасно разобрать GameplayTagQuery"}");
                return 0;
            }

            if (!string.IsNullOrWhiteSpace(parseWarning))
            {
                warnings.Add($"Условия выдачи стартового предмета не изменены: {parseWarning}");
                return 0;
            }
        }

        var applied = 0;
        foreach (var edit in edits)
        {
            var suffix = edit.FieldPath[StarterSpawnFieldPrefix.Length..];
            if (suffix.StartsWith("flag:", StringComparison.OrdinalIgnoreCase))
            {
                var key = suffix["flag:".Length..];
                var spec = StarterSpawnFlags.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
                if (spec is null)
                {
                    warnings.Add($"Условия выдачи: неизвестный переключатель {key}");
                    continue;
                }

                var mode = (edit.Value ?? string.Empty).Trim().ToLowerInvariant();
                model.RequiredTags.Remove(spec.Tag);
                model.ForbiddenTags.Remove(spec.Tag);
                model.AnyTags.Remove(spec.Tag);
                switch (mode)
                {
                    case "ignore":
                        applied++;
                        break;
                    case "require":
                        model.RequiredTags.Add(spec.Tag);
                        applied++;
                        break;
                    case "exclude":
                        model.ForbiddenTags.Add(spec.Tag);
                        applied++;
                        break;
                    default:
                        warnings.Add($"Условия выдачи: значение {edit.Value} не поддерживается для {spec.Label}");
                        break;
                }

                continue;
            }

            if (suffix.StartsWith("character:", StringComparison.OrdinalIgnoreCase))
            {
                var key = suffix["character:".Length..];
                var spec = StarterSpawnCharacters.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
                if (spec is null)
                {
                    warnings.Add($"Условия выдачи: неизвестный тип персонажа {key}");
                    continue;
                }

                var mode = (edit.Value ?? string.Empty).Trim().ToLowerInvariant();
                if (TryParseBool(mode, out var legacyEnabled))
                {
                    mode = legacyEnabled ? "allow" : "ignore";
                }

                model.RequiredTags.Remove(spec.Tag);
                model.AnyTags.Remove(spec.Tag);
                model.ForbiddenTags.Remove(spec.Tag);

                switch (mode)
                {
                    case "ignore":
                        applied++;
                        break;
                    case "allow":
                    case "require":
                        model.AnyTags.Add(spec.Tag);
                        applied++;
                        break;
                    case "exclude":
                        model.ForbiddenTags.Add(spec.Tag);
                        applied++;
                        break;
                    default:
                        warnings.Add($"Условия выдачи: значение {edit.Value} не поддерживается для {spec.Label}");
                        break;
                }
            }
        }

        if (applied == 0)
        {
            return 0;
        }

        var condition = EnsureConditionProperty(asset, export, existingCondition);
        WriteStarterSpawnCondition(asset, condition, model);
        return applied;
    }

    private static SoftObjectPropertyData EnsureTopLevelSoftObjectProperty(UAsset asset, NormalExport export, string propertyName)
    {
        var property = FindTopLevelProperty<SoftObjectPropertyData>(export, propertyName, out _);
        if (property is not null)
        {
            return property;
        }

        EnsureAssetNameReference(asset, "SoftObjectProperty");
        property = new SoftObjectPropertyData(CreateFName(asset, propertyName));
        export.Data.Add(property);
        return property;
    }

    private static ObjectPropertyData EnsureTopLevelObjectProperty(UAsset asset, NormalExport export, string propertyName)
    {
        var property = FindTopLevelProperty<ObjectPropertyData>(export, propertyName, out _);
        if (property is not null)
        {
            return property;
        }

        EnsureAssetNameReference(asset, "ObjectProperty");
        property = new ObjectPropertyData(CreateFName(asset, propertyName));
        export.Data.Add(property);
        return property;
    }

    private static EnumPropertyData EnsureTopLevelEnumProperty(
        UAsset asset,
        NormalExport export,
        string enumTypeName,
        params string[] propertyNames)
    {
        var property = propertyNames
            .Select(name => FindTopLevelProperty<EnumPropertyData>(export, name, out _))
            .FirstOrDefault(candidate => candidate is not null);
        if (property is null)
        {
            var propertyName = propertyNames.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "Value";
            EnsureAssetNameReference(asset, "EnumProperty");
            EnsureAssetNameReference(asset, enumTypeName);
            property = new EnumPropertyData(CreateFName(asset, propertyName));
            export.Data.Add(property);
        }

        property.EnumType = CreateFName(asset, enumTypeName);
        return property;
    }

    private static IntPropertyData EnsureTopLevelIntProperty(
        UAsset asset,
        NormalExport export,
        string propertyName)
    {
        var property = FindTopLevelProperty<IntPropertyData>(export, propertyName, out _);
        if (property is not null)
        {
            return property;
        }

        EnsureAssetNameReference(asset, "IntProperty");
        property = new IntPropertyData(CreateFName(asset, propertyName));
        export.Data.Add(property);
        return property;
    }

    private static FloatPropertyData EnsureTopLevelFloatProperty(
        UAsset asset,
        NormalExport export,
        string propertyName)
    {
        var property = FindTopLevelProperty<FloatPropertyData>(export, propertyName, out _);
        if (property is not null)
        {
            return property;
        }

        EnsureAssetNameReference(asset, "FloatProperty");
        property = new FloatPropertyData(CreateFName(asset, propertyName));
        export.Data.Add(property);
        return property;
    }

    private static StructPropertyData EnsureTopLevelStructProperty(
        UAsset asset,
        NormalExport export,
        string propertyName,
        string structType)
    {
        var property = FindTopLevelProperty<StructPropertyData>(export, propertyName, out _);
        if (property is null)
        {
            EnsureAssetNameReference(asset, "StructProperty");
            EnsureAssetNameReference(asset, structType);
            property = new StructPropertyData(CreateFName(asset, propertyName), CreateFName(asset, structType))
            {
                Value = []
            };
            export.Data.Add(property);
        }

        property.StructType = CreateFName(asset, structType);
        property.Value ??= [];
        return property;
    }

    private static StructPropertyData EnsureStructChildStructProperty(
        UAsset asset,
        StructPropertyData parent,
        string propertyName,
        string structType)
    {
        var property = FindStructChildProperty<StructPropertyData>(parent, propertyName, out _);
        if (property is null)
        {
            EnsureAssetNameReference(asset, "StructProperty");
            EnsureAssetNameReference(asset, structType);
            property = new StructPropertyData(CreateFName(asset, propertyName), CreateFName(asset, structType))
            {
                Value = []
            };
            parent.Value.Add(property);
        }

        property.StructType = CreateFName(asset, structType);
        property.Value ??= [];
        return property;
    }

    private static ArrayPropertyData EnsureStructObjectArrayChild(
        UAsset asset,
        StructPropertyData parent,
        string propertyName)
    {
        var property = FindStructChildProperty<ArrayPropertyData>(parent, propertyName, out _);
        if (property is null)
        {
            EnsureAssetNameReference(asset, "ArrayProperty");
            EnsureAssetNameReference(asset, "ObjectProperty");
            property = new ArrayPropertyData(CreateFName(asset, propertyName))
            {
                ArrayType = CreateFName(asset, "ObjectProperty"),
                Value = []
            };
            parent.Value.Add(property);
        }

        property.ArrayType = CreateFName(asset, "ObjectProperty");
        property.Value ??= [];
        return property;
    }

    private static ObjectPropertyData EnsureStructObjectChild(
        UAsset asset,
        StructPropertyData parent,
        string propertyName)
    {
        var property = FindStructChildProperty<ObjectPropertyData>(parent, propertyName, out _);
        if (property is not null)
        {
            return property;
        }

        EnsureAssetNameReference(asset, "ObjectProperty");
        property = new ObjectPropertyData(CreateFName(asset, propertyName));
        parent.Value.Add(property);
        return property;
    }

    private static FloatPropertyData EnsureStructFloatChild(
        UAsset asset,
        StructPropertyData parent,
        string propertyName)
    {
        var property = FindStructChildProperty<FloatPropertyData>(parent, propertyName, out _);
        if (property is not null)
        {
            return property;
        }

        EnsureAssetNameReference(asset, "FloatProperty");
        property = new FloatPropertyData(CreateFName(asset, propertyName));
        parent.Value.Add(property);
        return property;
    }

    private static IntPropertyData EnsureStructIntChild(
        UAsset asset,
        StructPropertyData parent,
        string propertyName)
    {
        var property = FindStructChildProperty<IntPropertyData>(parent, propertyName, out _);
        if (property is not null)
        {
            return property;
        }

        EnsureAssetNameReference(asset, "IntProperty");
        property = new IntPropertyData(CreateFName(asset, propertyName));
        parent.Value.Add(property);
        return property;
    }

    private static BoolPropertyData EnsureStructBoolChild(
        UAsset asset,
        StructPropertyData parent,
        string propertyName)
    {
        var property = FindStructChildProperty<BoolPropertyData>(parent, propertyName, out _);
        if (property is not null)
        {
            return property;
        }

        EnsureAssetNameReference(asset, "BoolProperty");
        property = new BoolPropertyData(CreateFName(asset, propertyName));
        parent.Value.Add(property);
        return property;
    }

    private static EnumPropertyData EnsureStructEnumChild(
        UAsset asset,
        StructPropertyData parent,
        string enumTypeName,
        params string[] propertyNames)
    {
        var property = propertyNames
            .Select(name => FindStructChildProperty<EnumPropertyData>(parent, name, out _))
            .FirstOrDefault(candidate => candidate is not null);
        if (property is null)
        {
            var propertyName = propertyNames.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "Value";
            EnsureAssetNameReference(asset, "EnumProperty");
            EnsureAssetNameReference(asset, enumTypeName);
            property = new EnumPropertyData(CreateFName(asset, propertyName));
            parent.Value.Add(property);
        }

        property.EnumType = CreateFName(asset, enumTypeName);
        return property;
    }

    private static bool TryApplyRecipePerSkillFloatEdit(
        UAsset asset,
        StructPropertyData property,
        string levelKey,
        string rawValue,
        out string error)
    {
        var spec = RecipeSkillLevels.FirstOrDefault(x => x.Key.Equals(levelKey, StringComparison.OrdinalIgnoreCase));
        if (spec is null)
        {
            error = $"неизвестный уровень навыка {levelKey}";
            return false;
        }

        if (!float.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
        {
            error = "ожидается число";
            return false;
        }

        EnsureStructFloatChild(asset, property, spec.PropertyName).Value = value;
        error = string.Empty;
        return true;
    }

    private static bool TryApplyRecipePerSkillIntEdit(
        UAsset asset,
        StructPropertyData property,
        string levelKey,
        string rawValue,
        out string error)
    {
        var spec = RecipeSkillLevels.FirstOrDefault(x => x.Key.Equals(levelKey, StringComparison.OrdinalIgnoreCase));
        if (spec is null)
        {
            error = $"неизвестный уровень навыка {levelKey}";
            return false;
        }

        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            error = "ожидается целое число";
            return false;
        }

        EnsureStructIntChild(asset, property, spec.PropertyName).Value = value;
        error = string.Empty;
        return true;
    }

    private static bool TryApplyCraftingIngredientEdit(
        UAsset asset,
        NormalExport export,
        string suffix,
        string rawValue,
        out string? error)
    {
        error = null;
        var parts = suffix.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3
            || !parts[0].Equals("ingredient", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ingredientIndex))
        {
            error = $"непонятный путь {suffix}";
            return false;
        }

        var ingredients = FindTopLevelProperty<ArrayPropertyData>(export, "Ingredients", out _);
        var slots = ingredients?.Value ?? [];
        if (ingredientIndex < 0 || ingredientIndex >= slots.Length || slots[ingredientIndex] is not StructPropertyData ingredientSlot)
        {
            error = $"слот ингредиента #{ingredientIndex + 1} не найден";
            return false;
        }

        var field = parts[2].ToLowerInvariant();
        switch (field)
        {
            case "allowed-display":
                return false;
            case "purpose":
                EnsureStructEnumChild(asset, ingredientSlot, "ECraftingIngredientPurpose", "Purpose").Value.Value = new FString(rawValue, Encoding.UTF8);
                return true;
            case "mixing":
                EnsureStructEnumChild(asset, ingredientSlot, "ECraftingIngredientMixingType", "MixingType").Value.Value = new FString(rawValue, Encoding.UTF8);
                return true;
            case "consume-whole":
                if (!TryParseBool(rawValue, out var consumeWhole))
                {
                    error = "ожидается true/false";
                    return false;
                }

                EnsureStructBoolChild(asset, ingredientSlot, "ShouldConsumeWhole").Value = consumeWhole;
                return true;
            case "resource":
                if (!TryParseBool(rawValue, out var isResource))
                {
                    error = "ожидается true/false";
                    return false;
                }

                EnsureStructBoolChild(asset, ingredientSlot, "IsResource").Value = isResource;
                return true;
            case "liters":
                if (!float.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var liters))
                {
                    error = "ожидается число";
                    return false;
                }

                EnsureStructFloatChild(asset, ingredientSlot, "Liters").Value = liters;
                return true;
            case "nutrient":
                if (!float.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var nutrientFactor))
                {
                    error = "ожидается число";
                    return false;
                }

                EnsureStructFloatChild(asset, ingredientSlot, "NutrientInclusionFactor").Value = nutrientFactor;
                return true;
            case "quality":
                if (!float.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var quality))
                {
                    error = "ожидается число";
                    return false;
                }

                EnsureStructFloatChild(asset, ingredientSlot, "ProductQualityInfluence").Value = quality;
                return true;
            case "return":
                if (!TryParseBool(rawValue, out var shouldReturn))
                {
                    error = "ожидается true/false";
                    return false;
                }

                EnsureStructBoolChild(asset, ingredientSlot, "ReturnOnUncraft").Value = shouldReturn;
                return true;
            case "amount":
            case "additional":
            case "damage":
                if (parts.Length < 4)
                {
                    error = "не указан уровень навыка";
                    return false;
                }

                var propertyName = field switch
                {
                    "amount" => "Amount",
                    "additional" => "AdditionalAmount",
                    "damage" => "DamagePercentageOnUncraft",
                    _ => "Amount"
                };
                var nested = EnsureStructChildStructProperty(asset, ingredientSlot, propertyName, "PerSkillLevelIntegerData");
                return TryApplyRecipePerSkillIntEdit(asset, nested, parts[3], rawValue, out error);
            default:
                error = $"неподдерживаемое поле ингредиента {field}";
                return false;
        }
    }

    private static StructPropertyData EnsureConditionProperty(UAsset asset, NormalExport export, StructPropertyData? existingCondition)
    {
        if (existingCondition is not null)
        {
            existingCondition.StructType = CreateFName(asset, "GameplayTagQuery");
            existingCondition.SerializeNone = true;
            existingCondition.Value ??= [];
            return existingCondition;
        }

        EnsureAssetNameReference(asset, "StructProperty");
        EnsureAssetNameReference(asset, "GameplayTagQuery");
        var created = new StructPropertyData(CreateFName(asset, "Condition"), CreateFName(asset, "GameplayTagQuery"))
        {
            SerializeNone = true,
            Value = []
        };
        export.Data.Insert(0, created);
        return created;
    }

    private static void WriteStarterSpawnCondition(UAsset asset, StructPropertyData conditionProperty, StarterSpawnConditionModel model)
    {
        conditionProperty.StructType = CreateFName(asset, "GameplayTagQuery");
        conditionProperty.SerializeNone = true;
        conditionProperty.Value ??= [];

        var orderedRequired = OrderStarterConditionTags(
            model.RequiredTags,
            model.OriginalTagOrder,
            StarterSpawnFlags.Select(x => x.Tag));
        var orderedForbidden = OrderStarterConditionTags(
            model.ForbiddenTags,
            model.OriginalTagOrder,
            StarterSpawnFlags.Select(x => x.Tag).Concat(StarterSpawnCharacters.Select(x => x.Tag)));
        var orderedAny = OrderStarterConditionTags(
            model.AnyTags,
            model.OriginalTagOrder,
            StarterSpawnCharacters.Select(x => x.Tag));
        var dictionary = orderedRequired
            .Concat(orderedForbidden)
            .Concat(orderedAny)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tagDictionary = EnsureConditionArrayChild(conditionProperty, asset, "TagDictionary", "StructProperty");
        tagDictionary.ArrayType = CreateFName(asset, "StructProperty");
        EnsureAssetNameReference(asset, "NameProperty");
        EnsureAssetNameReference(asset, "GameplayTag");
        tagDictionary.DummyStruct ??= new StructPropertyData(CreateFName(asset, "TagDictionary"), CreateFName(asset, "GameplayTag"))
        {
            SerializeNone = true,
            Value = [new NamePropertyData(CreateFName(asset, "TagName"))]
        };
        tagDictionary.Value = dictionary
            .Select(tag => (PropertyData)CreateGameplayTagStruct(asset, "TagDictionary", tag))
            .ToArray();

        var tokenStream = EnsureConditionArrayChild(conditionProperty, asset, "QueryTokenStream", "ByteProperty");
        tokenStream.ArrayType = CreateFName(asset, "ByteProperty");
        EnsureAssetNameReference(asset, "ByteProperty");
        tokenStream.Value = BuildStarterConditionTokenStream(dictionary, orderedRequired, orderedForbidden, orderedAny)
            .Select((value, index) => (PropertyData)new BytePropertyData(CreateFName(asset, index.ToString(CultureInfo.InvariantCulture)))
            {
                Value = value
            })
            .ToArray();

        var autoDescription = EnsureConditionStringChild(conditionProperty, asset, "AutoDescription");
        autoDescription.Value = new FString(BuildStarterConditionDescription(orderedRequired, orderedForbidden, orderedAny), Encoding.UTF8);
    }

    private static List<string> OrderStarterConditionTags(
        IEnumerable<string> source,
        IEnumerable<string> originalOrder,
        IEnumerable<string> preferredOrder)
    {
        var remaining = new HashSet<string>(source.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>(remaining.Count);

        foreach (var tag in originalOrder)
        {
            if (remaining.Remove(tag))
            {
                ordered.Add(tag);
            }
        }

        foreach (var tag in preferredOrder)
        {
            if (remaining.Remove(tag))
            {
                ordered.Add(tag);
            }
        }

        ordered.AddRange(remaining.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        return ordered;
    }

    private static List<byte> BuildStarterConditionTokenStream(
        IReadOnlyList<string> dictionary,
        IReadOnlyList<string> requiredTags,
        IReadOnlyList<string> forbiddenTags,
        IReadOnlyList<string> anyTags)
    {
        var tagIndex = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < dictionary.Count && i < byte.MaxValue; i++)
        {
            tagIndex[dictionary[i]] = (byte)i;
        }

        var tokens = new List<byte>(32) { 0, 1 };
        var childCount = anyTags.Count > 0 ? 3 : 2;
        tokens.Add((byte)GameplayTagQueryExprType.AllExprMatch);
        tokens.Add((byte)childCount);

        AppendTagQueryLeaf(tokens, GameplayTagQueryExprType.AllTagsMatch, requiredTags, tagIndex);
        AppendTagQueryLeaf(tokens, GameplayTagQueryExprType.NoTagsMatch, forbiddenTags, tagIndex);

        if (anyTags.Count > 0)
        {
            AppendTagQueryLeaf(tokens, GameplayTagQueryExprType.AnyTagsMatch, anyTags, tagIndex);
        }

        return tokens;
    }

    private static void AppendTagQueryLeaf(
        List<byte> tokens,
        GameplayTagQueryExprType type,
        IReadOnlyList<string> tags,
        IReadOnlyDictionary<string, byte> tagIndex)
    {
        var resolved = new List<byte>(tags.Count);
        foreach (var tag in tags)
        {
            if (tagIndex.TryGetValue(tag, out var index))
            {
                resolved.Add(index);
            }
        }

        tokens.Add((byte)type);
        tokens.Add((byte)Math.Min(resolved.Count, byte.MaxValue));
        foreach (var index in resolved)
        {
            tokens.Add(index);
        }
    }

    private static string BuildStarterConditionDescription(
        IReadOnlyList<string> requiredTags,
        IReadOnlyList<string> forbiddenTags,
        IReadOnlyList<string> anyTags)
    {
        static string FormatGroup(string groupName, IReadOnlyList<string> tags)
        {
            return tags.Count == 0
                ? $"{groupName}( )"
                : $"{groupName}( {string.Join(", ", tags)} )";
        }

        if (anyTags.Count == 0)
        {
            return $"ALL( {FormatGroup("ALL", requiredTags)}, {FormatGroup("NONE", forbiddenTags)} )";
        }

        return $"ALL( {FormatGroup("ALL", requiredTags)}, {FormatGroup("NONE", forbiddenTags)}, {FormatGroup("ANY", anyTags)} )";
    }

    private static ArrayPropertyData EnsureConditionArrayChild(
        StructPropertyData conditionProperty,
        UAsset asset,
        string childName,
        string arrayTypeName)
    {
        var property = conditionProperty.Value
            .OfType<ArrayPropertyData>()
            .FirstOrDefault(x => string.Equals(x.Name?.ToString(), childName, StringComparison.OrdinalIgnoreCase));
        if (property is not null)
        {
            return property;
        }

        EnsureAssetNameReference(asset, "ArrayProperty");
        EnsureAssetNameReference(asset, arrayTypeName);
        property = new ArrayPropertyData(CreateFName(asset, childName))
        {
            ArrayType = CreateFName(asset, arrayTypeName),
            Value = []
        };
        conditionProperty.Value.Add(property);
        return property;
    }

    private static StrPropertyData EnsureConditionStringChild(
        StructPropertyData conditionProperty,
        UAsset asset,
        string childName)
    {
        var property = conditionProperty.Value
            .OfType<StrPropertyData>()
            .FirstOrDefault(x => string.Equals(x.Name?.ToString(), childName, StringComparison.OrdinalIgnoreCase));
        if (property is not null)
        {
            return property;
        }

        EnsureAssetNameReference(asset, "StrProperty");
        property = new StrPropertyData(CreateFName(asset, childName))
        {
            Value = new FString(string.Empty, Encoding.UTF8)
        };
        conditionProperty.Value.Add(property);
        return property;
    }

    private static StructPropertyData CreateGameplayTagStruct(UAsset asset, string propertyName, string tag)
    {
        EnsureAssetNameReference(asset, "StructProperty");
        EnsureAssetNameReference(asset, "GameplayTag");
        EnsureAssetNameReference(asset, "NameProperty");
        return new StructPropertyData(CreateFName(asset, propertyName), CreateFName(asset, "GameplayTag"))
        {
            SerializeNone = true,
            Value =
            [
                new NamePropertyData(CreateFName(asset, "TagName"))
                {
                    Value = CreateFName(asset, tag)
                }
            ]
        };
    }

    private static FName CreateFName(UAsset asset, string value)
    {
        return asset.HasUnversionedProperties
            ? FName.DefineDummy(asset, value)
            : CreateRegisteredFName(asset, value);
    }

    private static FName CreateRegisteredFName(UAsset asset, string value)
    {
        EnsureAssetNameReference(asset, value);
        return new FName(asset, value, 0);
    }

    private static void EnsureAssetNameReference(UAsset asset, string value)
    {
        if (asset.HasUnversionedProperties || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var name = new FString(value, Encoding.UTF8);
        if (!asset.ContainsNameReference(name))
        {
            asset.AddNameReference(name, false, false);
        }
    }

    private bool ApplyUassetListEdits(string editedAssetPath, List<StudioListEditDto> listEdits, List<string> warnings)
    {
        var asset = new UAsset(editedAssetPath, EngineVersion.VER_UE4_27, null, CustomSerializationFlags.None);
        var applied = 0;

        foreach (var listEdit in listEdits)
        {
            if (string.IsNullOrWhiteSpace(listEdit.TargetPath))
            {
                continue;
            }

            if (string.Equals(listEdit.TargetPath, SyntheticSideEffectsTargetPath, StringComparison.OrdinalIgnoreCase))
            {
                if (TryApplySyntheticSideEffectsListEdit(asset, listEdit, warnings))
                {
                    applied++;
                }

                continue;
            }

            if (listEdit.TargetPath.StartsWith(SyntheticRecipeAllowedTypesTargetPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (TryApplySyntheticRecipeAllowedTypesListEdit(asset, listEdit, warnings))
                {
                    applied++;
                }

                continue;
            }

            if (listEdit.TargetPath.StartsWith(SyntheticCraftingUiCategoryRecipesTargetPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (TryApplySyntheticCraftingUiCategoryRecipesListEdit(asset, listEdit, warnings))
                {
                    applied++;
                }

                continue;
            }

            if (string.Equals(listEdit.TargetPath, SyntheticCargoMajorSpawnerOptionsTargetPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(listEdit.TargetPath, SyntheticCargoMajorSpawnerPresetOptionsTargetPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(listEdit.TargetPath, SyntheticCargoMinorSpawnerOptionsTargetPath, StringComparison.OrdinalIgnoreCase))
            {
                if (TryApplySyntheticCargoDropListEdit(asset, listEdit, warnings))
                {
                    applied++;
                }

                continue;
            }

            if (!TryResolveUassetProperty(asset, listEdit.TargetPath, out var property))
            {
                warnings.Add($"List edit skipped: список не найден {listEdit.TargetPath}");
                continue;
            }

            var action = (listEdit.Action ?? string.Empty).Trim().ToLowerInvariant();
            if (property is ArrayPropertyData arrayProperty)
            {
                var values = arrayProperty.Value ?? [];

                if (action == "add-reference")
                {
                    if (!TryCreateReferenceListItem(asset, arrayProperty, values, listEdit, out var cloned, out var referenceError))
                    {
                        warnings.Add($"List edit skipped: {referenceError} ({listEdit.TargetPath})");
                        continue;
                    }

                    if (ContainsEquivalentReference(asset, values, cloned))
                    {
                        warnings.Add($"List edit skipped: такая ссылка уже есть в списке {listEdit.TargetPath}");
                        continue;
                    }

                    var expanded = new PropertyData[values.Length + 1];
                    Array.Copy(values, expanded, values.Length);
                    expanded[^1] = cloned;
                    arrayProperty.Value = expanded;
                    applied++;
                    continue;
                }

                if (action == "add-clone")
                {
                    if (values.Length == 0)
                    {
                        warnings.Add($"List edit skipped: список пуст, нечего копировать {listEdit.TargetPath}");
                        continue;
                    }

                    var sourceIndex = listEdit.SourceIndex ?? (values.Length - 1);
                    if (sourceIndex < 0 || sourceIndex >= values.Length)
                    {
                        warnings.Add($"List edit skipped: неверный sourceIndex={sourceIndex} для {listEdit.TargetPath}");
                        continue;
                    }

                    if (values[sourceIndex].Clone() is not PropertyData cloned)
                    {
                        warnings.Add($"List edit skipped: не удалось скопировать элемент {sourceIndex} в {listEdit.TargetPath}");
                        continue;
                    }

                    var expanded = new PropertyData[values.Length + 1];
                    Array.Copy(values, expanded, values.Length);
                    expanded[^1] = cloned;
                    arrayProperty.Value = expanded;
                    applied++;
                    continue;
                }

                if (action == "add-empty")
                {
                    if (!TryCreateEmptyArrayListItem(asset, arrayProperty, out var created, out var emptyError))
                    {
                        warnings.Add($"List edit skipped: {emptyError} ({listEdit.TargetPath})");
                        continue;
                    }

                    var expanded = new PropertyData[values.Length + 1];
                    Array.Copy(values, expanded, values.Length);
                    expanded[^1] = created;
                    arrayProperty.Value = expanded;
                    applied++;
                    continue;
                }

                if (action == "remove-index")
                {
                    if (values.Length == 0)
                    {
                        warnings.Add($"List edit skipped: список пуст {listEdit.TargetPath}");
                        continue;
                    }

                    var index = listEdit.Index ?? (values.Length - 1);
                    if (index < 0 || index >= values.Length)
                    {
                        warnings.Add($"List edit skipped: неверный index={index} для {listEdit.TargetPath}");
                        continue;
                    }

                    var reduced = values.ToList();
                    reduced.RemoveAt(index);
                    arrayProperty.Value = reduced.ToArray();
                    applied++;
                    continue;
                }

                if (action == "clear")
                {
                    if (values.Length == 0)
                    {
                        continue;
                    }

                    arrayProperty.Value = [];
                    applied++;
                    continue;
                }

                warnings.Add($"List edit skipped: неподдерживаемое действие {listEdit.Action} для UAsset");
                continue;
            }

            if (property is SetPropertyData setProperty)
            {
                var values = setProperty.Value ?? [];

                if (action == "add-reference")
                {
                    if (!TryCreateReferenceSetItem(asset, setProperty, values, listEdit, out var created, out var referenceError))
                    {
                        warnings.Add($"List edit skipped: {referenceError} ({listEdit.TargetPath})");
                        continue;
                    }

                    if (ContainsEquivalentReference(asset, values, created))
                    {
                        warnings.Add($"List edit skipped: такая ссылка уже есть в наборе {listEdit.TargetPath}");
                        continue;
                    }

                    var expanded = new PropertyData[values.Length + 1];
                    Array.Copy(values, expanded, values.Length);
                    expanded[^1] = created;
                    setProperty.Value = expanded;
                    applied++;
                    continue;
                }

                if (action == "remove-index")
                {
                    if (values.Length == 0)
                    {
                        warnings.Add($"List edit skipped: список пуст {listEdit.TargetPath}");
                        continue;
                    }

                    var index = listEdit.Index ?? (values.Length - 1);
                    if (index < 0 || index >= values.Length)
                    {
                        warnings.Add($"List edit skipped: неверный index={index} для {listEdit.TargetPath}");
                        continue;
                    }

                    var reduced = values.ToList();
                    reduced.RemoveAt(index);
                    setProperty.Value = reduced.ToArray();
                    applied++;
                    continue;
                }

                if (action == "clear")
                {
                    if (values.Length == 0)
                    {
                        continue;
                    }

                    setProperty.Value = [];
                    applied++;
                    continue;
                }

                warnings.Add($"List edit skipped: неподдерживаемое действие {listEdit.Action} для набора UAsset");
                continue;
            }

            if (property is MapPropertyData mapProperty)
            {
                var entries = mapProperty.Value ?? new TMap<PropertyData, PropertyData>();

                if (action == "add-reference")
                {
                    if (!TryCreateReferenceMapEntry(asset, mapProperty, entries, listEdit, out var keyProperty, out var valueProperty, out var referenceError))
                    {
                        warnings.Add($"List edit skipped: {referenceError} ({listEdit.TargetPath})");
                        continue;
                    }

                    if (ContainsEquivalentReferenceKey(asset, entries, keyProperty))
                    {
                        warnings.Add($"List edit skipped: такая ссылка уже есть в карте {listEdit.TargetPath}");
                        continue;
                    }

                    var expanded = entries.ToList();
                    expanded.Add(new KeyValuePair<PropertyData, PropertyData>(keyProperty, valueProperty));
                    mapProperty.Value = new TMap<PropertyData, PropertyData>(expanded);
                    applied++;
                    continue;
                }

                if (action == "remove-index")
                {
                    if (entries.Count == 0)
                    {
                        warnings.Add($"List edit skipped: список пуст {listEdit.TargetPath}");
                        continue;
                    }

                    var index = listEdit.Index ?? (entries.Count - 1);
                    if (index < 0 || index >= entries.Count)
                    {
                        warnings.Add($"List edit skipped: неверный index={index} для {listEdit.TargetPath}");
                        continue;
                    }

                    var reduced = entries.ToList();
                    reduced.RemoveAt(index);
                    mapProperty.Value = new TMap<PropertyData, PropertyData>(reduced);
                    applied++;
                    continue;
                }

                if (action == "clear")
                {
                    if (entries.Count == 0)
                    {
                        continue;
                    }

                    mapProperty.Value = new TMap<PropertyData, PropertyData>();
                    applied++;
                    continue;
                }

                warnings.Add($"List edit skipped: неподдерживаемое действие {listEdit.Action} для карты UAsset");
                continue;
            }

            warnings.Add($"List edit skipped: цель не является безопасным списком {listEdit.TargetPath}");
        }

        if (applied == 0)
        {
            return false;
        }

        asset.Write(editedAssetPath);
        return true;
    }

    private bool TryApplySyntheticRecipeAllowedTypesListEdit(
        UAsset asset,
        StudioListEditDto listEdit,
        List<string> warnings)
    {
        if (!TryResolveRecipeAllowedTypesArray(asset, listEdit.TargetPath, out var allowedTypes, out var values, out var error))
        {
            warnings.Add($"List edit skipped: {error} ({listEdit.TargetPath})");
            return false;
        }

        var action = (listEdit.Action ?? string.Empty).Trim().ToLowerInvariant();
        if (action == "add-reference")
        {
            if (!TryCreateReferenceListItem(asset, allowedTypes, values, listEdit, out var created, out error))
            {
                warnings.Add($"List edit skipped: {error} ({listEdit.TargetPath})");
                return false;
            }

                    if (ContainsEquivalentReference(asset, values, created))
            {
                warnings.Add("List edit skipped: такой вариант ингредиента уже есть в этом слоте.");
                return false;
            }

            var expanded = new PropertyData[values.Length + 1];
            Array.Copy(values, expanded, values.Length);
            expanded[^1] = created;
            allowedTypes.Value = expanded;
            return true;
        }

        if (action == "remove-index")
        {
            if (values.Length == 0)
            {
                warnings.Add("List edit skipped: в этом слоте пока нет добавленных вариантов ингредиента.");
                return false;
            }

            var index = listEdit.Index ?? (values.Length - 1);
            if (index < 0 || index >= values.Length)
            {
                warnings.Add($"List edit skipped: неверный index={index} для {listEdit.TargetPath}");
                return false;
            }

            var reduced = values.ToList();
            reduced.RemoveAt(index);
            allowedTypes.Value = reduced.ToArray();
            return true;
        }

        if (action == "clear")
        {
            if (values.Length == 0)
            {
                return false;
            }

            allowedTypes.Value = [];
            return true;
        }

        warnings.Add($"List edit skipped: действие {listEdit.Action} не поддерживается для вариантов ингредиента.");
        return false;
    }

    private bool TryApplySyntheticCraftingUiCategoryRecipesListEdit(
        UAsset asset,
        StudioListEditDto listEdit,
        List<string> warnings)
    {
        if (!TryResolveCraftingUiCategoryRecipesArray(asset, listEdit.TargetPath, out var recipes, out var values, out var categoryLabel, out var error))
        {
            warnings.Add($"List edit skipped: {error} ({listEdit.TargetPath})");
            return false;
        }

        var action = (listEdit.Action ?? string.Empty).Trim().ToLowerInvariant();
        if (action == "add-reference")
        {
            if (!TryCreateReferenceListItem(asset, recipes, values, listEdit, out var created, out error))
            {
                warnings.Add($"List edit skipped: {error} ({listEdit.TargetPath})");
                return false;
            }

            if (ContainsEquivalentReference(asset, values, created))
            {
                warnings.Add($"List edit skipped: такой рецепт уже есть в категории «{categoryLabel}».");
                return false;
            }

            var expanded = new PropertyData[values.Length + 1];
            Array.Copy(values, expanded, values.Length);
            expanded[^1] = created;
            recipes.Value = expanded;
            return true;
        }

        if (action == "remove-index")
        {
            if (values.Length == 0)
            {
                warnings.Add($"List edit skipped: категория «{categoryLabel}» пока не содержит рецептов.");
                return false;
            }

            var index = listEdit.Index ?? (values.Length - 1);
            if (index < 0 || index >= values.Length)
            {
                warnings.Add($"List edit skipped: неверный index={index} для {listEdit.TargetPath}");
                return false;
            }

            var reduced = values.ToList();
            reduced.RemoveAt(index);
            recipes.Value = reduced.ToArray();
            return true;
        }

        if (action == "clear")
        {
            if (values.Length == 0)
            {
                return false;
            }

            recipes.Value = [];
            return true;
        }

        warnings.Add($"List edit skipped: действие {listEdit.Action} не поддерживается для пула рецептов категории.");
        return false;
    }

    private bool TryApplySyntheticCargoDropListEdit(
        UAsset asset,
        StudioListEditDto listEdit,
        List<string> warnings)
    {
        if (!TryFindCargoDropContainerOwnerExport(asset, out var ownerExport))
        {
            warnings.Add("List edit skipped: не найден основной экспорт контейнера грузового дропа.");
            return false;
        }

        if (string.Equals(listEdit.TargetPath, SyntheticCargoMajorSpawnerOptionsTargetPath, StringComparison.OrdinalIgnoreCase))
        {
            var array = EnsureTopLevelObjectArrayProperty(asset, ownerExport, "MajorSpawnerOptions");
            return TryApplySyntheticArrayReferenceListEdit(asset, array, listEdit, warnings, "обычный пресет лута");
        }

        if (string.Equals(listEdit.TargetPath, SyntheticCargoMajorSpawnerPresetOptionsTargetPath, StringComparison.OrdinalIgnoreCase))
        {
            var array = EnsureTopLevelObjectArrayProperty(asset, ownerExport, "MajorSpawnerPresetOptions");
            return TryApplySyntheticArrayReferenceListEdit(asset, array, listEdit, warnings, "расширенный пресет лута");
        }

        if (string.Equals(listEdit.TargetPath, SyntheticCargoMinorSpawnerOptionsTargetPath, StringComparison.OrdinalIgnoreCase))
        {
            var array = EnsureTopLevelCargoDropMinorSpawnerArray(asset, ownerExport);
            var values = array.Value ?? [];
            var action = (listEdit.Action ?? string.Empty).Trim().ToLowerInvariant();

            if (action != "add-empty")
            {
                warnings.Add("List edit skipped: для пустого списка дополнительных наборов сначала нужно создать новый элемент.");
                return false;
            }

            if (!TryCreateEmptyArrayListItem(asset, array, out var created, out var error))
            {
                warnings.Add($"List edit skipped: {error} ({listEdit.TargetPath})");
                return false;
            }

            var expanded = new PropertyData[values.Length + 1];
            Array.Copy(values, expanded, values.Length);
            expanded[^1] = created;
            array.Value = expanded;
            return true;
        }

        warnings.Add($"List edit skipped: неизвестная synthetic-цель {listEdit.TargetPath}");
        return false;
    }

    private bool TryApplySyntheticArrayReferenceListEdit(
        UAsset asset,
        ArrayPropertyData arrayProperty,
        StudioListEditDto listEdit,
        List<string> warnings,
        string subject)
    {
        var values = arrayProperty.Value ?? [];
        var action = (listEdit.Action ?? string.Empty).Trim().ToLowerInvariant();

        if (action == "add-reference")
        {
            if (!TryCreateReferenceListItem(asset, arrayProperty, values, listEdit, out var created, out var error))
            {
                warnings.Add($"List edit skipped: {error} ({listEdit.TargetPath})");
                return false;
            }

            if (ContainsEquivalentReference(asset, values, created))
            {
                warnings.Add($"List edit skipped: такой {subject} уже есть в этом списке.");
                return false;
            }

            var expanded = new PropertyData[values.Length + 1];
            Array.Copy(values, expanded, values.Length);
            expanded[^1] = created;
            arrayProperty.Value = expanded;
            return true;
        }

        if (action == "clear")
        {
            if (values.Length == 0)
            {
                return false;
            }

            arrayProperty.Value = [];
            return true;
        }

        warnings.Add($"List edit skipped: действие {listEdit.Action} не поддерживается для пустого synthetic-списка.");
        return false;
    }

    private bool TryApplySyntheticSideEffectsListEdit(
        UAsset asset,
        StudioListEditDto listEdit,
        List<string> warnings)
    {
        if (!TryFindSideEffectsArray(asset, out var ownerExport, out _, out var sideEffects))
        {
            warnings.Add("List edit skipped: не найден экспорт, в который можно добавить побочный эффект.");
            return false;
        }

        var action = (listEdit.Action ?? string.Empty).Trim().ToLowerInvariant();
        var values = sideEffects?.Value ?? [];

        if (action == "add-reference")
        {
            sideEffects ??= EnsureTopLevelObjectArrayProperty(asset, ownerExport, "_sideEffects");
            values = sideEffects.Value ?? [];
            if (!TryCreateReferenceListItem(asset, sideEffects, values, listEdit, out var created, out var error))
            {
                warnings.Add($"List edit skipped: {error} ({listEdit.TargetPath})");
                return false;
            }

                    if (ContainsEquivalentReference(asset, values, created))
            {
                warnings.Add("List edit skipped: такой побочный эффект уже добавлен.");
                return false;
            }

            var expanded = new PropertyData[values.Length + 1];
            Array.Copy(values, expanded, values.Length);
            expanded[^1] = created;
            sideEffects.Value = expanded;
            return true;
        }

        if (action == "remove-index")
        {
            if (sideEffects is null || values.Length == 0)
            {
                warnings.Add("List edit skipped: в этой системе пока нет побочных эффектов.");
                return false;
            }

            var index = listEdit.Index ?? (values.Length - 1);
            if (index < 0 || index >= values.Length)
            {
                warnings.Add($"List edit skipped: неверный index={index} для {listEdit.TargetPath}");
                return false;
            }

            var reduced = values.ToList();
            reduced.RemoveAt(index);
            sideEffects.Value = reduced.ToArray();
            return true;
        }

        if (action == "clear")
        {
            if (sideEffects is null || values.Length == 0)
            {
                return false;
            }

            sideEffects.Value = [];
            return true;
        }

        warnings.Add($"List edit skipped: действие {listEdit.Action} не поддерживается для побочных эффектов.");
        return false;
    }

    private bool TryCreateReferenceListItem(
        UAsset asset,
        ArrayPropertyData arrayProperty,
        PropertyData[] values,
        StudioListEditDto listEdit,
        out PropertyData property,
        out string error)
    {
        property = null!;
        error = string.Empty;

        var rawReference = listEdit.RawValue ?? listEdit.TemplateJson;
        if (IsSideEffectsArray(arrayProperty)
            && TryResolveSideEffectClassFromRawValue(rawReference, out var sideEffectClassName))
        {
            if (ContainsSideEffectClass(asset, values, sideEffectClassName))
            {
                error = "такой побочный эффект уже есть в этой системе";
                return false;
            }

            return TryCreateSideEffectListItem(asset, arrayProperty, sideEffectClassName, out property, out error);
        }

        if (IsFishSpeciesArray(arrayProperty))
        {
            return TryCreateFishSpeciesListItem(asset, arrayProperty, values, listEdit, rawReference, out property, out error);
        }

        if (IsAdvancedItemSpawnerSubpresetArray(arrayProperty))
        {
            return TryCreateAdvancedItemSpawnerSubpresetListItem(asset, arrayProperty, values, listEdit, rawReference, out property, out error);
        }

        if (values.Length > 0)
        {
            var sourceIndex = listEdit.SourceIndex ?? (values.Length - 1);
            if (sourceIndex < 0 || sourceIndex >= values.Length)
            {
                error = $"неверный sourceIndex={sourceIndex}";
                return false;
            }

            if (values[sourceIndex].Clone() is not PropertyData cloned)
            {
                error = $"не удалось подготовить новую ссылку по шаблону {sourceIndex}";
                return false;
            }

            if (!TryAssignReferenceCloneValue(asset, cloned, rawReference, out error))
            {
                return false;
            }

            property = cloned;
            return true;
        }

        var arrayType = arrayProperty.ArrayType?.ToString() ?? string.Empty;
        var rawValue = rawReference;
        if (arrayType.Equals("ObjectProperty", StringComparison.OrdinalIgnoreCase))
        {
            var created = new ObjectPropertyData(new FName());
            if (!TryAssignReferenceCloneValue(asset, created, rawValue, out error))
            {
                return false;
            }

            property = created;
            return true;
        }

        if (arrayType.Equals("SoftObjectProperty", StringComparison.OrdinalIgnoreCase))
        {
            var created = new SoftObjectPropertyData(new FName());
            if (!TryAssignReferenceCloneValue(asset, created, rawValue, out error))
            {
                return false;
            }

            property = created;
            return true;
        }

        if (arrayType.Equals("SoftObjectPathProperty", StringComparison.OrdinalIgnoreCase))
        {
            var created = new SoftObjectPathPropertyData(new FName());
            if (!TryAssignReferenceCloneValue(asset, created, rawValue, out error))
            {
                return false;
            }

            property = created;
            return true;
        }

        error = "этот список нельзя безопасно пополнять без готового шаблона";
        return false;
    }

    private bool TryCreateReferenceSetItem(
        UAsset asset,
        SetPropertyData setProperty,
        PropertyData[] values,
        StudioListEditDto listEdit,
        out PropertyData property,
        out string error)
    {
        property = null!;
        error = string.Empty;

        var rawReference = listEdit.RawValue ?? listEdit.TemplateJson;
        if (string.IsNullOrWhiteSpace(rawReference))
        {
            error = "не передана ссылка, которую нужно добавить в набор";
            return false;
        }

        if (values.Length > 0)
        {
            var sourceIndex = listEdit.SourceIndex ?? (values.Length - 1);
            if (sourceIndex < 0 || sourceIndex >= values.Length)
            {
                error = $"неверный sourceIndex={sourceIndex}";
                return false;
            }

            if (values[sourceIndex].Clone() is not PropertyData cloned)
            {
                error = $"не удалось подготовить новую ссылку по шаблону {sourceIndex}";
                return false;
            }

            if (!TryAssignReferenceCloneValue(asset, cloned, rawReference, out error))
            {
                return false;
            }

            property = cloned;
            return true;
        }

        var arrayType = setProperty.ArrayType?.ToString() ?? string.Empty;
        if (arrayType.Equals("ObjectProperty", StringComparison.OrdinalIgnoreCase))
        {
            var created = new ObjectPropertyData(new FName());
            if (!TryAssignReferenceCloneValue(asset, created, rawReference, out error))
            {
                return false;
            }

            property = created;
            return true;
        }

        if (arrayType.Equals("SoftObjectProperty", StringComparison.OrdinalIgnoreCase))
        {
            var created = new SoftObjectPropertyData(new FName());
            if (!TryAssignReferenceCloneValue(asset, created, rawReference, out error))
            {
                return false;
            }

            property = created;
            return true;
        }

        if (arrayType.Equals("SoftObjectPathProperty", StringComparison.OrdinalIgnoreCase))
        {
            var created = new SoftObjectPathPropertyData(new FName());
            if (!TryAssignReferenceCloneValue(asset, created, rawReference, out error))
            {
                return false;
            }

            property = created;
            return true;
        }

        error = "этот набор нельзя безопасно пополнять без готового шаблона";
        return false;
    }

    private static bool IsFishSpeciesArray(ArrayPropertyData arrayProperty)
    {
        var arrayType = arrayProperty.ArrayType?.ToString() ?? string.Empty;
        var propertyName = arrayProperty.Name?.ToString() ?? string.Empty;
        return arrayType.Equals("StructProperty", StringComparison.OrdinalIgnoreCase)
            && propertyName.Equals("FishSpawnData", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdvancedItemSpawnerSubpresetArray(ArrayPropertyData arrayProperty)
    {
        var arrayType = arrayProperty.ArrayType?.ToString() ?? string.Empty;
        var propertyName = arrayProperty.Name?.ToString() ?? string.Empty;
        return arrayType.Equals("StructProperty", StringComparison.OrdinalIgnoreCase)
            && propertyName.Equals("Subpresets", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCreateFishSpeciesListItem(
        UAsset asset,
        ArrayPropertyData arrayProperty,
        PropertyData[] values,
        StudioListEditDto listEdit,
        string? rawReference,
        out PropertyData property,
        out string error)
    {
        property = null!;
        error = string.Empty;

        PropertyData created;
        if (values.Length > 0)
        {
            var sourceIndex = listEdit.SourceIndex ?? (values.Length - 1);
            if (sourceIndex < 0 || sourceIndex >= values.Length)
            {
                error = $"неверный sourceIndex={sourceIndex}";
                return false;
            }

            if (values[sourceIndex].Clone() is not PropertyData cloned)
            {
                error = $"не удалось подготовить новый вид рыбы по шаблону {sourceIndex}";
                return false;
            }

            created = cloned;
        }
        else if (!TryCreateEmptyArrayListItem(asset, arrayProperty, out created, out error))
        {
            return false;
        }

        if (created is not StructPropertyData fishStruct
            || !TryFindStructReferenceChild(fishStruct, "FishSpeciesData", out var fishReference))
        {
            error = "в шаблоне вида рыбы не найдено поле FishSpeciesData";
            return false;
        }

        if (!TryAssignReferenceCloneValue(asset, fishReference, rawReference, out error))
        {
            return false;
        }

        property = fishStruct;
        return true;
    }

    private static bool TryCreateAdvancedItemSpawnerSubpresetListItem(
        UAsset asset,
        ArrayPropertyData arrayProperty,
        PropertyData[] values,
        StudioListEditDto listEdit,
        string? rawReference,
        out PropertyData property,
        out string error)
    {
        property = null!;
        error = string.Empty;

        PropertyData created;
        if (values.Length > 0)
        {
            var sourceIndex = listEdit.SourceIndex ?? (values.Length - 1);
            if (sourceIndex < 0 || sourceIndex >= values.Length)
            {
                error = $"неверный sourceIndex={sourceIndex}";
                return false;
            }

            if (values[sourceIndex].Clone() is not PropertyData cloned)
            {
                error = $"не удалось подготовить новый подпакет по шаблону {sourceIndex}";
                return false;
            }

            created = cloned;
        }
        else if (!TryCreateEmptyArrayListItem(asset, arrayProperty, out created, out error))
        {
            return false;
        }

        if (created is not StructPropertyData subpresetStruct
            || !TryFindStructReferenceChild(subpresetStruct, "Preset", out var presetReference))
        {
            error = "в шаблоне подпакета не найдено поле Preset";
            return false;
        }

        if (!TryAssignReferenceCloneValue(asset, presetReference, rawReference, out error))
        {
            return false;
        }

        property = subpresetStruct;
        return true;
    }

    private static bool TryFindStructReferenceChild(
        StructPropertyData structProperty,
        string childName,
        out PropertyData referenceProperty)
    {
        referenceProperty = structProperty.Value.FirstOrDefault(property =>
            string.Equals(property.Name?.ToString(), childName, StringComparison.OrdinalIgnoreCase)
            && property is ObjectPropertyData or SoftObjectPropertyData or SoftObjectPathPropertyData)!;
        return referenceProperty is not null;
    }

    private static bool TryCreateEmptyArrayListItem(
        UAsset asset,
        ArrayPropertyData arrayProperty,
        out PropertyData property,
        out string error)
    {
        property = null!;
        error = string.Empty;

        var arrayType = arrayProperty.ArrayType?.ToString() ?? string.Empty;
        var elementName = FName.DefineDummy(asset, arrayProperty.Name?.ToString() ?? "Item", int.MinValue);

        switch (arrayType)
        {
            case "StructProperty":
                if (arrayProperty.DummyStruct?.Clone() is StructPropertyData dummyStruct)
                {
                    property = dummyStruct;
                    return true;
                }

                error = "для пустой структуры нет шаблона, по которому можно создать новый элемент";
                return false;
            case "BoolProperty":
                property = new BoolPropertyData(elementName) { Value = false };
                return true;
            case "FloatProperty":
                property = new FloatPropertyData(elementName) { Value = 0f };
                return true;
            case "DoubleProperty":
                property = new DoublePropertyData(elementName) { Value = 0d };
                return true;
            case "IntProperty":
                property = new IntPropertyData(elementName) { Value = 0 };
                return true;
            case "Int8Property":
                property = new Int8PropertyData(elementName) { Value = 0 };
                return true;
            case "Int16Property":
                property = new Int16PropertyData(elementName) { Value = 0 };
                return true;
            case "Int64Property":
                property = new Int64PropertyData(elementName) { Value = 0 };
                return true;
            case "UInt16Property":
                property = new UInt16PropertyData(elementName) { Value = 0 };
                return true;
            case "UInt32Property":
                property = new UInt32PropertyData(elementName) { Value = 0 };
                return true;
            case "UInt64Property":
                property = new UInt64PropertyData(elementName) { Value = 0 };
                return true;
            case "NameProperty":
                property = new NamePropertyData(elementName) { Value = FName.FromString(asset, "None") };
                return true;
            case "StrProperty":
                property = new StrPropertyData(elementName) { Value = FString.FromString(string.Empty) };
                return true;
            case "TextProperty":
                property = new TextPropertyData(elementName)
                {
                    HistoryType = TextHistoryType.RawText,
                    Value = FString.FromString(string.Empty),
                    Namespace = null,
                    CultureInvariantString = null,
                    Flags = 0
                };
                return true;
            case "Guid":
                property = new GuidPropertyData(elementName) { Value = Guid.Empty };
                return true;
            default:
                error = "этот список нельзя безопасно пополнить пустым элементом без отдельного шаблона";
                return false;
        }
    }

    private static bool TryCreateReferenceMapEntry(
        UAsset asset,
        MapPropertyData mapProperty,
        TMap<PropertyData, PropertyData> entries,
        StudioListEditDto listEdit,
        out PropertyData keyProperty,
        out PropertyData valueProperty,
        out string error)
    {
        keyProperty = null!;
        valueProperty = null!;
        error = string.Empty;

        var rawReference = listEdit.RawValue ?? listEdit.TemplateJson ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawReference))
        {
            error = "не передана ссылка, которую нужно добавить в карту";
            return false;
        }

        if (entries.Count == 0)
        {
            if (!TryCreateReferenceMapKeyProperty(asset, mapProperty.KeyType?.ToString(), rawReference, out keyProperty, out error))
            {
                return false;
            }

            if (!TryCreateDefaultMapValueProperty(asset, mapProperty.ValueType?.ToString(), out valueProperty, out error))
            {
                keyProperty = null!;
                return false;
            }

            return true;
        }

        var sourceIndex = listEdit.SourceIndex ?? (entries.Count - 1);
        if (sourceIndex < 0 || sourceIndex >= entries.Count)
        {
            error = $"неверный sourceIndex={sourceIndex}";
            return false;
        }

        var sourceEntry = entries.ElementAt(sourceIndex);
        if (sourceEntry.Key.Clone() is not PropertyData clonedKey)
        {
            error = $"не удалось подготовить новый ключ по шаблону {sourceIndex}";
            return false;
        }

        if (sourceEntry.Value.Clone() is not PropertyData clonedValue)
        {
            error = $"не удалось подготовить новое значение по шаблону {sourceIndex}";
            return false;
        }

        if (!TryAssignReferenceCloneValue(asset, clonedKey, rawReference, out error))
        {
            return false;
        }

        keyProperty = clonedKey;
        valueProperty = clonedValue;
        return true;
    }

    private static bool TryCreateReferenceMapKeyProperty(
        UAsset asset,
        string? keyType,
        string rawReference,
        out PropertyData keyProperty,
        out string error)
    {
        keyProperty = null!;
        error = string.Empty;

        PropertyData? created = null;
        if (string.Equals(keyType, "ObjectProperty", StringComparison.OrdinalIgnoreCase))
        {
            created = new ObjectPropertyData(new FName());
        }
        else if (string.Equals(keyType, "SoftObjectProperty", StringComparison.OrdinalIgnoreCase))
        {
            created = new SoftObjectPropertyData(new FName());
        }
        else if (string.Equals(keyType, "SoftObjectPathProperty", StringComparison.OrdinalIgnoreCase))
        {
            created = new SoftObjectPathPropertyData(new FName());
        }

        if (created is null)
        {
            error = "эту карту нельзя безопасно пополнять без готового шаблона ключа";
            return false;
        }

        if (!TryAssignReferenceCloneValue(asset, created, rawReference, out error))
        {
            return false;
        }

        keyProperty = created;
        return true;
    }

    private static bool TryCreateDefaultMapValueProperty(
        UAsset asset,
        string? valueType,
        out PropertyData valueProperty,
        out string error)
    {
        valueProperty = null!;
        error = string.Empty;
        var propertyName = FName.DefineDummy(asset, "Value", int.MinValue);

        if (string.Equals(valueType, "FloatProperty", StringComparison.OrdinalIgnoreCase))
        {
            valueProperty = new FloatPropertyData(propertyName) { Value = 1.0f };
            return true;
        }

        if (string.Equals(valueType, "DoubleProperty", StringComparison.OrdinalIgnoreCase))
        {
            valueProperty = new DoublePropertyData(propertyName) { Value = 1.0d };
            return true;
        }

        if (string.Equals(valueType, "IntProperty", StringComparison.OrdinalIgnoreCase))
        {
            valueProperty = new IntPropertyData(propertyName) { Value = 1 };
            return true;
        }

        if (string.Equals(valueType, "Int8Property", StringComparison.OrdinalIgnoreCase))
        {
            valueProperty = new Int8PropertyData(propertyName) { Value = 1 };
            return true;
        }

        if (string.Equals(valueType, "Int16Property", StringComparison.OrdinalIgnoreCase))
        {
            valueProperty = new Int16PropertyData(propertyName) { Value = 1 };
            return true;
        }

        if (string.Equals(valueType, "Int64Property", StringComparison.OrdinalIgnoreCase))
        {
            valueProperty = new Int64PropertyData(propertyName) { Value = 1 };
            return true;
        }

        if (string.Equals(valueType, "ByteProperty", StringComparison.OrdinalIgnoreCase))
        {
            valueProperty = new BytePropertyData(propertyName) { Value = 1 };
            return true;
        }

        if (string.Equals(valueType, "UInt16Property", StringComparison.OrdinalIgnoreCase))
        {
            valueProperty = new UInt16PropertyData(propertyName) { Value = 1 };
            return true;
        }

        if (string.Equals(valueType, "UInt32Property", StringComparison.OrdinalIgnoreCase))
        {
            valueProperty = new UInt32PropertyData(propertyName) { Value = 1 };
            return true;
        }

        if (string.Equals(valueType, "UInt64Property", StringComparison.OrdinalIgnoreCase))
        {
            valueProperty = new UInt64PropertyData(propertyName) { Value = 1 };
            return true;
        }

        error = "эту карту нельзя безопасно пополнять без готового шаблона значения";
        return false;
    }

    private static bool TryResolveRecipeAllowedTypesArray(
        UAsset asset,
        string targetPath,
        out ArrayPropertyData allowedTypes,
        out PropertyData[] values,
        out string error)
    {
        allowedTypes = null!;
        values = [];
        error = string.Empty;

        if (!TryParseSyntheticRecipeAllowedTypesTarget(targetPath, out var exportIndex, out var ingredientsIndex, out var ingredientIndex))
        {
            error = "не удалось разобрать путь списка допустимых ингредиентов";
            return false;
        }

        if (exportIndex < 0
            || exportIndex >= asset.Exports.Count
            || asset.Exports[exportIndex] is not NormalExport export)
        {
            error = "не найден экспорт рецепта";
            return false;
        }

        if (ingredientsIndex < 0
            || ingredientsIndex >= export.Data.Count
            || export.Data[ingredientsIndex] is not ArrayPropertyData ingredients)
        {
            error = "не найден список слотов рецепта";
            return false;
        }

        var slots = ingredients.Value ?? [];
        if (ingredientIndex < 0
            || ingredientIndex >= slots.Length
            || slots[ingredientIndex] is not StructPropertyData ingredientSlot)
        {
            error = $"слот ингредиента #{ingredientIndex + 1} не найден";
            return false;
        }

        allowedTypes = EnsureStructObjectArrayChild(asset, ingredientSlot, "AllowedTypes");
        values = allowedTypes.Value ?? [];
        return true;
    }

    private static bool TryResolveCraftingUiCategoryRecipesArray(
        UAsset asset,
        string targetPath,
        out ArrayPropertyData recipes,
        out PropertyData[] values,
        out string categoryLabel,
        out string error)
    {
        recipes = null!;
        values = [];
        categoryLabel = "Категория крафта";
        error = string.Empty;

        if (!TryParseSyntheticCraftingUiCategoryRecipesTarget(targetPath, out var exportIndex, out var categoriesIndex, out var categoryIndex))
        {
            error = "не удалось разобрать путь категории пула крафта";
            return false;
        }

        if (exportIndex < 0
            || exportIndex >= asset.Exports.Count
            || asset.Exports[exportIndex] is not NormalExport export)
        {
            error = "не найден экспорт реестра крафта";
            return false;
        }

        if (categoriesIndex < 0
            || categoriesIndex >= export.Data.Count
            || export.Data[categoriesIndex] is not ArrayPropertyData categories)
        {
            error = "не найден массив категорий крафта";
            return false;
        }

        var categoryValues = categories.Value ?? [];
        if (categoryIndex < 0
            || categoryIndex >= categoryValues.Length
            || categoryValues[categoryIndex] is not StructPropertyData categoryStruct)
        {
            error = $"не найдена категория крафта #{categoryIndex + 1}";
            return false;
        }

        var tagStruct = FindStructChildProperty<StructPropertyData>(categoryStruct, "Tag", out _);
        var tagValue = ResolveGameplayTagStructValue(tagStruct);
        var categoriesName = categories.Name?.ToString() ?? string.Empty;
        var isPlaceable = categoriesName.Equals("PlaceableCategories", StringComparison.OrdinalIgnoreCase);
        categoryLabel = ResolveCraftingUiCategoryLabel(tagValue, isPlaceable);

        recipes = EnsureStructObjectArrayChild(asset, categoryStruct, "Recipes");
        values = recipes.Value ?? [];
        return true;
    }

    private static bool TryParseSyntheticRecipeAllowedTypesTarget(
        string targetPath,
        out int exportIndex,
        out int ingredientsIndex,
        out int ingredientIndex)
    {
        exportIndex = -1;
        ingredientsIndex = -1;
        ingredientIndex = -1;

        var normalized = (targetPath ?? string.Empty).Trim();
        if (!normalized.StartsWith(SyntheticRecipeAllowedTypesTargetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = normalized[SyntheticRecipeAllowedTypesTargetPrefix.Length..];
        var parts = payload.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 3
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out exportIndex)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ingredientsIndex)
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out ingredientIndex);
    }

    private static bool TryParseSyntheticCraftingUiCategoryRecipesTarget(
        string targetPath,
        out int exportIndex,
        out int categoriesIndex,
        out int categoryIndex)
    {
        exportIndex = -1;
        categoriesIndex = -1;
        categoryIndex = -1;

        var normalized = (targetPath ?? string.Empty).Trim();
        if (!normalized.StartsWith(SyntheticCraftingUiCategoryRecipesTargetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = normalized[SyntheticCraftingUiCategoryRecipesTargetPrefix.Length..];
        var parts = payload.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 3
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out exportIndex)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out categoriesIndex)
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out categoryIndex);
    }

    private bool TryCreateSideEffectListItem(
        UAsset asset,
        ArrayPropertyData arrayProperty,
        string className,
        out PropertyData property,
        out string error)
    {
        property = null!;
        error = string.Empty;

        if (!TryFindSideEffectsArray(asset, out _, out var ownerExportIndex, out _))
        {
            error = "не найден экспорт, в который можно встроить новый побочный эффект";
            return false;
        }

        var template = GetSideEffectTemplates()
            .FirstOrDefault(candidate => string.Equals(candidate.ClassName, className, StringComparison.OrdinalIgnoreCase));
        if (template is null)
        {
            error = "для этого побочного эффекта нет безопасного шаблона";
            return false;
        }

        var readableTemplatePath = template.IsBundledTemplate
            ? template.SourcePath
            : PrepareIsolatedAssetReadSource(template.SourcePath);
        var donorAsset = new UAsset(readableTemplatePath, EngineVersion.VER_UE4_27, null, CustomSerializationFlags.None);
        if (template.ExportIndex < 0
            || template.ExportIndex >= donorAsset.Exports.Count
            || donorAsset.Exports[template.ExportIndex] is not NormalExport donorExport)
        {
            error = "не удалось открыть шаблон побочного эффекта";
            return false;
        }

        var clonedExport = DeepCloneSideEffectExportToAsset(asset, donorExport, className, ownerExportIndex);

        asset.Exports.Add(clonedExport);
        var exportReference = new FPackageIndex(asset.Exports.Count);
        property = new ObjectPropertyData(new FName())
        {
            Value = exportReference
        };

        return true;
    }

    private static bool IsSideEffectsArray(ArrayPropertyData arrayProperty)
    {
        return string.Equals(arrayProperty.Name?.ToString(), "_sideEffects", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveSideEffectClassFromRawValue(string? rawValue, out string className)
    {
        className = string.Empty;
        var normalized = (rawValue ?? string.Empty).Trim();
        if (!normalized.StartsWith("script:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidate = normalized["script:".Length..].Trim();
        if (!candidate.StartsWith("PrisonerBodyConditionOrSymptomSideEffect_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        className = candidate;
        return true;
    }

    private static bool ContainsSideEffectClass(UAsset asset, IEnumerable<PropertyData> values, string className)
    {
        foreach (var value in values.OfType<ObjectPropertyData>())
        {
            if (TryResolveSideEffectClassFromObjectReference(asset, value.Value, out var existingClassName)
                && string.Equals(existingClassName, className, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveSideEffectClassFromObjectReference(UAsset asset, FPackageIndex reference, out string className)
    {
        className = string.Empty;
        if (reference.Index > 0)
        {
            var exportIndex = reference.Index - 1;
            if (exportIndex >= 0
                && exportIndex < asset.Exports.Count
                && asset.Exports[exportIndex] is NormalExport export
                && TryResolveSideEffectClassName(asset, export, out className))
            {
                return true;
            }
        }

        if (reference.Index < 0
            && TryResolveImportObjectName(asset, reference, out var importedName))
        {
            var normalized = importedName.StartsWith("Default__", StringComparison.OrdinalIgnoreCase)
                ? importedName["Default__".Length..]
                : importedName;
            if (normalized.StartsWith("PrisonerBodyConditionOrSymptomSideEffect_", StringComparison.OrdinalIgnoreCase))
            {
                className = normalized;
                return true;
            }
        }

        return false;
    }

    private static string BuildUniqueSideEffectObjectName(UAsset asset, string className)
    {
        var existingNames = new HashSet<string>(
            asset.Exports
                .OfType<NormalExport>()
                .Select(export => export.ObjectName?.ToString() ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);

        var baseName = className;
        if (!existingNames.Contains(baseName))
        {
            return baseName;
        }

        for (var suffix = 0; suffix < 999; suffix++)
        {
            var candidate = $"{className}_{suffix}";
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{className}_{Guid.NewGuid():N}";
    }

    private static NormalExport DeepCloneSideEffectExportToAsset(
        UAsset asset,
        NormalExport donorExport,
        string className,
        int ownerExportIndex)
    {
        var cloned = new NormalExport
        {
            Asset = asset,
            ClassIndex = EnsureSideEffectClassImport(asset, className),
            TemplateIndex = EnsureSideEffectTemplateImport(asset, className),
            OuterIndex = new FPackageIndex(ownerExportIndex + 1),
            ObjectFlags = donorExport.ObjectFlags,
            ObjectName = CreateRegisteredFName(asset, BuildUniqueSideEffectObjectName(asset, className)),
            Data = donorExport.Data.Select(property => (PropertyData)property.Clone()).ToList(),
            Extras = donorExport.Extras?.ToArray() ?? Array.Empty<byte>(),
            SerializationControl = donorExport.SerializationControl,
            ScriptSerializationStartOffset = donorExport.ScriptSerializationStartOffset,
            ScriptSerializationEndOffset = donorExport.ScriptSerializationEndOffset,
            SerializationBeforeSerializationDependencies = donorExport.SerializationBeforeSerializationDependencies?.ToList() ?? [],
            CreateBeforeSerializationDependencies = donorExport.CreateBeforeSerializationDependencies?.ToList() ?? [],
            SerializationBeforeCreateDependencies = donorExport.SerializationBeforeCreateDependencies?.ToList() ?? []
        };

        foreach (var property in cloned.Data)
        {
            RebindObjectNamesToAsset(asset, property, new HashSet<object>(ReferenceEqualityComparer.Instance));
        }

        return cloned;
    }

    private static void RebindObjectNamesToAsset(UAsset asset, object? node, HashSet<object> visited)
    {
        if (node is null)
        {
            return;
        }

        var nodeType = node.GetType();
        if (!nodeType.IsValueType && !visited.Add(node))
        {
            return;
        }

        foreach (var field in nodeType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType == typeof(FName))
            {
                var current = (FName)(field.GetValue(node) ?? new FName());
                field.SetValue(node, RebindFNameToAsset(asset, current));
                continue;
            }

            if (field.FieldType.IsPrimitive
                || field.FieldType.IsEnum
                || field.FieldType == typeof(string)
                || field.FieldType == typeof(FString))
            {
                continue;
            }

            var value = field.GetValue(node);
            if (value is null)
            {
                continue;
            }

            if (value is IEnumerable enumerable and not string)
            {
                foreach (var child in enumerable)
                {
                    RebindObjectNamesToAsset(asset, child, visited);
                }

                continue;
            }

            if (field.FieldType.Namespace?.StartsWith("UAssetAPI", StringComparison.Ordinal) == true)
            {
                RebindObjectNamesToAsset(asset, value, visited);
            }
        }
    }

    private static FName RebindFNameToAsset(UAsset asset, FName value)
    {
        var text = TryExtractFNameText(value);
        return string.IsNullOrWhiteSpace(text) || string.Equals(text, "None", StringComparison.OrdinalIgnoreCase)
            ? new FName()
            : CreateRegisteredFName(asset, text);
    }

    private static string? TryExtractFNameText(FName value)
    {
        try
        {
            return value.ToString();
        }

        catch
        {
            var dummyField = typeof(FName).GetField("DummyValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return dummyField?.GetValue(value)?.ToString();
        }
    }

    private static FPackageIndex EnsureSideEffectClassImport(UAsset asset, string className)
    {
        var scriptPackageImportIndex = EnsureImport(
            asset,
            "/Script/CoreUObject",
            "Package",
            new FPackageIndex(0),
            "/Script/ConZ");
        var classImportIndex = EnsureImport(
            asset,
            "/Script/CoreUObject",
            "Class",
            new FPackageIndex(-(scriptPackageImportIndex + 1)),
            className);
        return new FPackageIndex(-(classImportIndex + 1));
    }

    private static FPackageIndex EnsureSideEffectTemplateImport(UAsset asset, string className)
    {
        var scriptPackageImportIndex = EnsureImport(
            asset,
            "/Script/CoreUObject",
            "Package",
            new FPackageIndex(0),
            "/Script/ConZ");
        var defaultImportIndex = EnsureImport(
            asset,
            "/Script/ConZ",
            className,
            new FPackageIndex(-(scriptPackageImportIndex + 1)),
            $"Default__{className}");
        return new FPackageIndex(-(defaultImportIndex + 1));
    }

    private static ArrayPropertyData EnsureTopLevelObjectArrayProperty(UAsset asset, NormalExport export, string propertyName)
    {
        var property = FindTopLevelProperty<ArrayPropertyData>(export, propertyName, out _);
        if (property is not null)
        {
            property.ArrayType = CreateFName(asset, "ObjectProperty");
            property.Value ??= [];
            return property;
        }

        EnsureAssetNameReference(asset, "ArrayProperty");
        EnsureAssetNameReference(asset, "ObjectProperty");
        property = new ArrayPropertyData(CreateFName(asset, propertyName))
        {
            ArrayType = CreateFName(asset, "ObjectProperty"),
            Value = []
        };
        export.Data.Add(property);
        return property;
    }

    private static ArrayPropertyData EnsureTopLevelCargoDropMinorSpawnerArray(UAsset asset, NormalExport export)
    {
        var property = FindTopLevelProperty<ArrayPropertyData>(export, "MinorSpawnerOptions", out _);
        if (property is not null)
        {
            property.ArrayType = CreateFName(asset, "StructProperty");
            property.Value ??= [];
            property.DummyStruct ??= CreateCargoDropSpawnerPresetDummyStruct(asset);
            return property;
        }

        EnsureAssetNameReference(asset, "ArrayProperty");
        EnsureAssetNameReference(asset, "StructProperty");
        EnsureAssetNameReference(asset, "CargoDropSpawnerPreset");
        property = new ArrayPropertyData(CreateFName(asset, "MinorSpawnerOptions"))
        {
            ArrayType = CreateFName(asset, "StructProperty"),
            Value = [],
            DummyStruct = CreateCargoDropSpawnerPresetDummyStruct(asset)
        };
        export.Data.Add(property);
        return property;
    }

    private static StructPropertyData CreateCargoDropSpawnerPresetDummyStruct(UAsset asset)
    {
        EnsureAssetNameReference(asset, "StructProperty");
        EnsureAssetNameReference(asset, "CargoDropSpawnerPreset");
        var dummy = new StructPropertyData(CreateFName(asset, "MinorSpawnerOptions"), CreateFName(asset, "CargoDropSpawnerPreset"))
        {
            Value = []
        };
        EnsureStructObjectChild(asset, dummy, "Preset").Value = new FPackageIndex(0);
        EnsureStructObjectChild(asset, dummy, "SpawnerPreset").Value = new FPackageIndex(0);
        EnsureStructFloatChild(asset, dummy, "ChanceMultiplier").Value = 1.0f;
        return dummy;
    }

    private static bool ContainsEquivalentReference(UAsset asset, PropertyData[] values, PropertyData candidate)
    {
        if (!TryBuildComparableReferenceSignature(asset, candidate, out var candidateSignature))
        {
            return false;
        }

        return values.Any(existing =>
            TryBuildComparableReferenceSignature(asset, existing, out var existingSignature)
            && string.Equals(existingSignature, candidateSignature, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsEquivalentReferenceKey(UAsset asset, TMap<PropertyData, PropertyData> entries, PropertyData candidate)
    {
        return ContainsEquivalentReference(asset, entries.Select(entry => entry.Key).ToArray(), candidate);
    }

    private static bool TryBuildComparableReferenceSignature(UAsset asset, PropertyData property, out string signature)
    {
        switch (property)
        {
            case ObjectPropertyData objectProperty when TryExtractObjectReferencePickerValue(asset, objectProperty.Value, out var objectRawValue, out _):
                signature = $"ref:{NormalizeComparableReferenceValue(objectRawValue)}";
                return true;
            case SoftObjectPropertyData softProperty:
                signature = $"ref:{NormalizeComparableReferenceValue(ExtractSoftObjectReference(softProperty.Value))}";
                return true;
            case SoftObjectPathPropertyData softPathProperty:
                signature = $"ref:{NormalizeComparableReferenceValue(ExtractSoftObjectReference(softPathProperty.Value))}";
                return true;
            case StructPropertyData structProperty when TryFindStructReferenceChild(structProperty, "FishSpeciesData", out var nestedReference)
                                                  && TryBuildComparableReferenceSignature(asset, nestedReference, out signature):
                return true;
            case StructPropertyData structProperty when TryFindStructReferenceChild(structProperty, "Preset", out var presetReference)
                                                  && TryBuildComparableReferenceSignature(asset, presetReference, out signature):
                return true;
            default:
                signature = string.Empty;
                return false;
        }
    }

    private static string NormalizeComparableReferenceValue(string rawValue)
    {
        return (rawValue ?? string.Empty)
            .Trim()
            .Replace('\\', '/')
            .ToLowerInvariant();
    }

    private static bool TryResolveUassetProperty(UAsset asset, string fieldPath, out PropertyData property)
    {
        property = null!;
        var tokens = fieldPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 2)
        {
            return false;
        }

        if (!TryParseTokenIndex(tokens[0], "e:", out var exportIndex))
        {
            return false;
        }

        if (exportIndex < 0 || exportIndex >= asset.Exports.Count || asset.Exports[exportIndex] is not NormalExport export)
        {
            return false;
        }

        object? container = export.Data;
        PropertyData? current = null;
        for (var i = 1; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (token.StartsWith("r:", StringComparison.OrdinalIgnoreCase))
            {
                if (export is not DataTableExport dataTableExport
                    || !TryParseTokenIndex(token, "r:", out var rowIndex))
                {
                    return false;
                }

                var rows = dataTableExport.Table.Data;
                if (rowIndex < 0 || rowIndex >= rows.Count)
                {
                    return false;
                }

                current = rows[rowIndex];
                container = GetChildContainer(current);
                continue;
            }

            if (token.StartsWith("p:", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseTokenIndex(token, "p:", out var propertyIndex))
                {
                    return false;
                }

                if (container is not List<PropertyData> propertyList || propertyIndex < 0 || propertyIndex >= propertyList.Count)
                {
                    return false;
                }

                current = propertyList[propertyIndex];
                container = GetChildContainer(current);
                continue;
            }

            if (token.StartsWith("a:", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseTokenIndex(token, "a:", out var itemIndex))
                {
                    return false;
                }

                if (container is not PropertyData[] arrayItems || itemIndex < 0 || itemIndex >= arrayItems.Length)
                {
                    return false;
                }

                current = arrayItems[itemIndex];
                container = GetChildContainer(current);
                continue;
            }

            if (token.StartsWith("s:", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseTokenIndex(token, "s:", out var itemIndex))
                {
                    return false;
                }

                if (container is not PropertyData[] setItems || itemIndex < 0 || itemIndex >= setItems.Length)
                {
                    return false;
                }

                current = setItems[itemIndex];
                container = GetChildContainer(current);
                continue;
            }

            if (token.StartsWith("m:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = token.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length != 3 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mapIndex))
                {
                    return false;
                }

                if (container is not TMap<PropertyData, PropertyData> mapItems || mapIndex < 0 || mapIndex >= mapItems.Count)
                {
                    return false;
                }

                var entry = mapItems.ElementAt(mapIndex);
                current = parts[2].Equals("k", StringComparison.OrdinalIgnoreCase)
                    ? entry.Key
                    : parts[2].Equals("v", StringComparison.OrdinalIgnoreCase)
                        ? entry.Value
                        : null;
                if (current is null)
                {
                    return false;
                }

                container = GetChildContainer(current);
                continue;
            }

            return false;
        }

        if (current is null)
        {
            return false;
        }

        property = current;
        return true;
    }

    private static object? GetChildContainer(PropertyData property)
    {
        return property switch
        {
            StructPropertyData structProperty => structProperty.Value,
            ArrayPropertyData arrayProperty => arrayProperty.Value,
            MapPropertyData mapProperty => mapProperty.Value,
            _ => null
        };
    }

    private static bool TryParseTokenIndex(string token, string prefix, out int index)
    {
        index = -1;
        if (!token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return int.TryParse(token[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
    }

    private static bool TryAssignEditablePropertyValue(UAsset asset, PropertyData property, string rawValue, out string error)
    {
        rawValue ??= string.Empty;
        switch (property)
        {
            case IntPropertyData intProperty:
                if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    intProperty.Value = intValue;
                    error = string.Empty;
                    return true;
                }

                error = "ожидается int";
                return false;
            case Int8PropertyData int8Property:
                if (sbyte.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var int8Value))
                {
                    int8Property.Value = int8Value;
                    error = string.Empty;
                    return true;
                }

                error = "ожидается int8";
                return false;
            case Int16PropertyData int16Property:
                if (short.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var int16Value))
                {
                    int16Property.Value = int16Value;
                    error = string.Empty;
                    return true;
                }

                error = "ожидается int16";
                return false;
            case Int64PropertyData int64Property:
                if (long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var int64Value))
                {
                    int64Property.Value = int64Value;
                    error = string.Empty;
                    return true;
                }

                error = "ожидается int64";
                return false;
            case UInt16PropertyData uint16Property:
                if (ushort.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uint16Value))
                {
                    uint16Property.Value = uint16Value;
                    error = string.Empty;
                    return true;
                }

                error = "ожидается uint16";
                return false;
            case UInt32PropertyData uint32Property:
                if (uint.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uint32Value))
                {
                    uint32Property.Value = uint32Value;
                    error = string.Empty;
                    return true;
                }

                error = "ожидается uint32";
                return false;
            case UInt64PropertyData uint64Property:
                if (ulong.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uint64Value))
                {
                    uint64Property.Value = uint64Value;
                    error = string.Empty;
                    return true;
                }

                error = "ожидается uint64";
                return false;
            case BytePropertyData byteProperty:
                if (byte.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var byteValue))
                {
                    byteProperty.Value = byteValue;
                    error = string.Empty;
                    return true;
                }

                error = "ожидается byte (0-255)";
                return false;
            case FloatPropertyData floatProperty:
                if (float.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var floatValue))
                {
                    floatProperty.Value = floatValue;
                    error = string.Empty;
                    return true;
                }

                error = "ожидается float";
                return false;
            case DoublePropertyData doubleProperty:
                if (double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
                {
                    doubleProperty.Value = doubleValue;
                    error = string.Empty;
                    return true;
                }

                error = "ожидается double";
                return false;
            case BoolPropertyData boolProperty:
                if (TryParseBool(rawValue, out var boolValue))
                {
                    boolProperty.Value = boolValue;
                    error = string.Empty;
                    return true;
                }

                error = "ожидается bool (true/false, 1/0)";
                return false;
            case StrPropertyData strProperty:
                strProperty.Value = new FString(rawValue, System.Text.Encoding.UTF8);
                error = string.Empty;
                return true;
            case NamePropertyData nameProperty:
                nameProperty.Value.Value = new FString(rawValue, System.Text.Encoding.UTF8);
                error = string.Empty;
                return true;
            case EnumPropertyData enumProperty:
                enumProperty.Value.Value = new FString(rawValue, System.Text.Encoding.UTF8);
                error = string.Empty;
                return true;
            case TextPropertyData textProperty:
                textProperty.Value = new FString(rawValue, System.Text.Encoding.UTF8);
                error = string.Empty;
                return true;
            case ObjectPropertyData objectProperty:
                if (TryBuildObjectReferenceFromPicker(asset, rawValue, out var objectReference, out error))
                {
                    objectProperty.Value = objectReference;
                    return true;
                }

                return false;
            case SoftObjectPropertyData softObjectProperty:
                if (TryBuildSoftObjectReference(asset, rawValue, out var softReference))
                {
                    softObjectProperty.Value = softReference;
                    error = string.Empty;
                    return true;
                }

                error = "ожидается ссылка вида /Game/.../Asset.Asset_C";
                return false;
            case SoftObjectPathPropertyData softObjectPathProperty:
                if (TryBuildSoftObjectReference(asset, rawValue, out var softObjectPath))
                {
                    softObjectPathProperty.Value = softObjectPath;
                    error = string.Empty;
                    return true;
                }

                error = "ожидается ссылка вида /Game/.../Asset.Asset_C";
                return false;
            default:
                error = "тип пока не поддерживается для безопасного редактирования";
                return false;
        }
    }

    private static bool TryAssignReferenceCloneValue(UAsset asset, PropertyData property, string? rawValue, out string error)
    {
        var normalized = (rawValue ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = "не выбрана новая ссылка";
            return false;
        }

        switch (property)
        {
            case ObjectPropertyData objectProperty:
                if (TryBuildObjectReferenceFromPicker(asset, normalized, out var objectReference, out error))
                {
                    objectProperty.Value = objectReference;
                    return true;
                }

                return false;
            case SoftObjectPropertyData softObjectProperty:
                if (TryBuildSoftObjectReferenceFromPicker(asset, normalized, out var softReference))
                {
                    softObjectProperty.Value = softReference;
                    error = string.Empty;
                    return true;
                }

                error = "ожидается ссылка вида /Game/.../Asset.Asset_C";
                return false;
            case SoftObjectPathPropertyData softObjectPathProperty:
                if (TryBuildSoftObjectReferenceFromPicker(asset, normalized, out var softPathReference))
                {
                    softObjectPathProperty.Value = softPathReference;
                    error = string.Empty;
                    return true;
                }

                error = "ожидается ссылка вида /Game/.../Asset.Asset_C";
                return false;
            default:
                error = "этот тип ссылки пока нельзя добавлять безопасно";
                return false;
        }
    }

    private static string ExtractSoftObjectReference(FSoftObjectPath path)
    {
        var assetName = path.AssetPath.AssetName?.ToString();
        if (!string.IsNullOrWhiteSpace(assetName))
        {
            return assetName;
        }

        var packageName = path.AssetPath.PackageName?.ToString();
        return packageName ?? string.Empty;
    }

    private static bool TryBuildSoftObjectReference(UAsset asset, string rawValue, out FSoftObjectPath value)
    {
        var normalized = (rawValue ?? string.Empty).Trim();
        if (!normalized.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase)
            || !normalized.Contains('.', StringComparison.Ordinal))
        {
            value = default;
            return false;
        }

        var assetName = new FName(asset, normalized, 0);
        var emptyPackage = new FName();
        var subPath = new FString(string.Empty, System.Text.Encoding.UTF8);
        value = new FSoftObjectPath(emptyPackage, assetName, subPath);
        return true;
    }

    private static bool TryBuildSoftObjectReferenceFromPicker(UAsset asset, string rawValue, out FSoftObjectPath value)
    {
        if (TryBuildSoftObjectReference(asset, rawValue, out value))
        {
            return true;
        }

        var normalized = (rawValue ?? string.Empty).Trim();
        if (!normalized.StartsWith("class:", StringComparison.OrdinalIgnoreCase))
        {
            value = default;
            return false;
        }

        var className = normalized["class:".Length..].Trim();
        if (string.IsNullOrWhiteSpace(className)
            || !className.EndsWith("Skill", StringComparison.OrdinalIgnoreCase))
        {
            value = default;
            return false;
        }

        if (!className.EndsWith("Skill", StringComparison.Ordinal))
        {
            className = $"{className[..^"skill".Length]}Skill";
        }

        var blueprintPath = $"/Game/ConZ_Files/Skills/{className}.{className}_C";
        return TryBuildSoftObjectReference(asset, blueprintPath, out value);
    }

    private static bool TryBuildObjectReferenceFromPicker(
        UAsset asset,
        string rawValue,
        out FPackageIndex value,
        out string error)
    {
        if (TryBuildExistingExportReference(asset, rawValue, out value))
        {
            error = string.Empty;
            return true;
        }

        if (TryBuildScriptClassReference(asset, rawValue, out value))
        {
            error = string.Empty;
            return true;
        }

        if (TryBuildScriptDefaultObjectReference(asset, rawValue, out value))
        {
            error = string.Empty;
            return true;
        }

        if (TryBuildBlueprintDefaultObjectReference(asset, rawValue, out value))
        {
            error = string.Empty;
            return true;
        }

        if (TryBuildImportedObjectReference(asset, rawValue, out value))
        {
            error = string.Empty;
            return true;
        }

        value = new FPackageIndex(0);
        error = "не удалось собрать безопасную Unreal-ссылку для выбранного объекта";
        return false;
    }

    private static bool TryBuildExistingExportReference(UAsset asset, string rawValue, out FPackageIndex value)
    {
        var normalized = (rawValue ?? string.Empty).Trim();
        if (!normalized.StartsWith("export:", StringComparison.OrdinalIgnoreCase))
        {
            value = new FPackageIndex(0);
            return false;
        }

        var objectName = normalized["export:".Length..].Trim();
        if (string.IsNullOrWhiteSpace(objectName))
        {
            value = new FPackageIndex(0);
            return false;
        }

        for (var i = 0; i < asset.Exports.Count; i++)
        {
            if (!string.Equals(asset.Exports[i].ObjectName?.ToString(), objectName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = new FPackageIndex(i + 1);
            return true;
        }

        value = new FPackageIndex(0);
        return false;
    }

    private static bool TryBuildScriptClassReference(UAsset asset, string rawValue, out FPackageIndex value)
    {
        var normalized = (rawValue ?? string.Empty).Trim();
        if (!normalized.StartsWith("class:", StringComparison.OrdinalIgnoreCase))
        {
            value = new FPackageIndex(0);
            return false;
        }

        var className = normalized["class:".Length..].Trim();
        if (string.IsNullOrWhiteSpace(className))
        {
            value = new FPackageIndex(0);
            return false;
        }

        var scriptPackageImportIndex = EnsureImport(
            asset,
            "/Script/CoreUObject",
            "Package",
            new FPackageIndex(0),
            "/Script/SCUM");
        var classImportIndex = EnsureImport(
            asset,
            "/Script/CoreUObject",
            "Class",
            new FPackageIndex(-(scriptPackageImportIndex + 1)),
            className);

        value = new FPackageIndex(-(classImportIndex + 1));
        return true;
    }

    private static bool TryBuildBlueprintDefaultObjectReference(UAsset asset, string rawValue, out FPackageIndex value)
    {
        var normalized = (rawValue ?? string.Empty).Trim();
        if (!normalized.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase))
        {
            value = new FPackageIndex(0);
            return false;
        }

        var dotIndex = normalized.LastIndexOf('.');
        if (dotIndex <= 0 || dotIndex >= normalized.Length - 1)
        {
            value = new FPackageIndex(0);
            return false;
        }

        var packagePath = normalized[..dotIndex];
        var className = normalized[(dotIndex + 1)..];
        if (string.IsNullOrWhiteSpace(packagePath)
            || string.IsNullOrWhiteSpace(className)
            || !className.EndsWith("_C", StringComparison.OrdinalIgnoreCase))
        {
            value = new FPackageIndex(0);
            return false;
        }

        var packageImportIndex = EnsureImport(
            asset,
            "/Script/CoreUObject",
            "Package",
            new FPackageIndex(0),
            packagePath);
        _ = EnsureImport(
            asset,
            "/Script/Engine",
            "BlueprintGeneratedClass",
            new FPackageIndex(-(packageImportIndex + 1)),
            className);
        var defaultObjectName = $"Default__{className}";
        var defaultImportIndex = EnsureImport(
            asset,
            packagePath,
            className,
            new FPackageIndex(-(packageImportIndex + 1)),
            defaultObjectName);

        value = new FPackageIndex(-(defaultImportIndex + 1));
        return true;
    }

    private static bool TryBuildScriptDefaultObjectReference(UAsset asset, string rawValue, out FPackageIndex value)
    {
        var normalized = (rawValue ?? string.Empty).Trim();
        if (!normalized.StartsWith("script:", StringComparison.OrdinalIgnoreCase))
        {
            value = new FPackageIndex(0);
            return false;
        }

        var className = normalized["script:".Length..].Trim();
        if (string.IsNullOrWhiteSpace(className))
        {
            value = new FPackageIndex(0);
            return false;
        }

        var scriptPackageImportIndex = EnsureImport(
            asset,
            "/Script/CoreUObject",
            "Package",
            new FPackageIndex(0),
            "/Script/SCUM");
        EnsureImport(
            asset,
            "/Script/CoreUObject",
            "Class",
            new FPackageIndex(-(scriptPackageImportIndex + 1)),
            className);
        var defaultImportIndex = EnsureImport(
            asset,
            "/Script/SCUM",
            className,
            new FPackageIndex(-(scriptPackageImportIndex + 1)),
            $"Default__{className}");

        value = new FPackageIndex(-(defaultImportIndex + 1));
        return true;
    }

    private static bool TryBuildImportedObjectReference(UAsset asset, string rawValue, out FPackageIndex value)
    {
        if (!TryParseImportedObjectReferenceRawValue(rawValue, out var objectPath, out var classPackage, out var className))
        {
            value = new FPackageIndex(0);
            return false;
        }

        var dotIndex = objectPath.LastIndexOf('.');
        if (dotIndex <= 0 || dotIndex >= objectPath.Length - 1)
        {
            value = new FPackageIndex(0);
            return false;
        }

        var packagePath = objectPath[..dotIndex];
        var objectName = objectPath[(dotIndex + 1)..];
        if (!packagePath.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(objectName))
        {
            value = new FPackageIndex(0);
            return false;
        }

        var packageImportIndex = EnsureImport(
            asset,
            "/Script/CoreUObject",
            "Package",
            new FPackageIndex(0),
            packagePath);
        var objectImportIndex = EnsureImport(
            asset,
            classPackage,
            className,
            new FPackageIndex(-(packageImportIndex + 1)),
            objectName);

        value = new FPackageIndex(-(objectImportIndex + 1));
        return true;
    }

    private static int EnsureImport(
        UAsset asset,
        string classPackage,
        string className,
        FPackageIndex outerIndex,
        string objectName)
    {
        for (var i = 0; i < asset.Imports.Count; i++)
        {
            var existing = asset.Imports[i];
            if (string.Equals(existing.ClassPackage?.ToString(), classPackage, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.ClassName?.ToString(), className, StringComparison.OrdinalIgnoreCase)
                && existing.OuterIndex.Index == outerIndex.Index
                && string.Equals(existing.ObjectName?.ToString(), objectName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        asset.Imports.Add(new Import(classPackage, className, outerIndex, objectName, false, asset));
        return asset.Imports.Count - 1;
    }

    private static string BuildBlueprintClassReferenceFromRelativePath(string relativePath)
    {
        var normalized = PathUtil.NormalizeRelative(relativePath);
        var withoutExtension = normalized.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
            ? normalized[..^".uasset".Length]
            : normalized;
        var withoutPrefix = withoutExtension.StartsWith("scum/content/conz_files/", StringComparison.OrdinalIgnoreCase)
            ? withoutExtension["scum/content/conz_files/".Length..]
            : withoutExtension;
        var stem = Path.GetFileNameWithoutExtension(normalized);
        return $"/Game/ConZ_Files/{withoutPrefix}.{stem}_C";
    }

    private static string BuildSoftGameAssetReferenceFromRelativePath(string relativePath)
    {
        var normalized = PathUtil.NormalizeRelative(relativePath);
        var withoutExtension = normalized.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
            ? normalized[..^".uasset".Length]
            : normalized;
        var fromConzFiles = withoutExtension.StartsWith("scum/content/conz_files/", StringComparison.OrdinalIgnoreCase);
        var withoutPrefix = fromConzFiles
            ? withoutExtension["scum/content/conz_files/".Length..]
            : withoutExtension.StartsWith("scum/content/", StringComparison.OrdinalIgnoreCase)
                ? withoutExtension["scum/content/".Length..]
                : withoutExtension;
        var stem = Path.GetFileNameWithoutExtension(normalized);

        if (fromConzFiles)
        {
            return $"/Game/ConZ_Files/{withoutPrefix}.{stem}";
        }

        if (withoutPrefix.StartsWith("conz_files/", StringComparison.OrdinalIgnoreCase))
        {
            return $"/Game/ConZ_Files/{withoutPrefix["conz_files/".Length..]}.{stem}";
        }

        return $"/Game/{withoutPrefix}.{stem}";
    }

    private static string BuildGameObjectReferenceFromRelativePath(string relativePath, string classPackage, string className)
    {
        var normalized = PathUtil.NormalizeRelative(relativePath);
        var withoutExtension = normalized.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
            ? normalized[..^".uasset".Length]
            : normalized;
        var withoutPrefix = withoutExtension.StartsWith("scum/content/", StringComparison.OrdinalIgnoreCase)
            ? withoutExtension["scum/content/".Length..]
            : withoutExtension;
        var packagePath = $"/Game/{withoutPrefix}";
        var objectName = Path.GetFileNameWithoutExtension(normalized);
        return BuildImportedObjectReferenceRawValue(packagePath, objectName, classPackage, className);
    }

    private static string BuildImportedObjectReferenceRawValue(
        string packagePath,
        string objectName,
        string classPackage,
        string className)
    {
        return $"object:{packagePath}.{objectName}|{classPackage}|{className}";
    }

    private static bool TryParseImportedObjectReferenceRawValue(
        string rawValue,
        out string objectPath,
        out string classPackage,
        out string className)
    {
        objectPath = string.Empty;
        classPackage = string.Empty;
        className = string.Empty;

        var normalized = (rawValue ?? string.Empty).Trim();
        if (!normalized.StartsWith("object:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = normalized["object:".Length..];
        var parts = payload.Split('|', 3, StringSplitOptions.TrimEntries);
        if (parts.Length != 3
            || string.IsNullOrWhiteSpace(parts[0])
            || string.IsNullOrWhiteSpace(parts[1])
            || string.IsNullOrWhiteSpace(parts[2]))
        {
            return false;
        }

        objectPath = parts[0];
        classPackage = parts[1];
        className = parts[2];
        return true;
    }

    private static bool TryParseBool(string rawValue, out bool value)
    {
        if (bool.TryParse(rawValue, out value))
        {
            return true;
        }

        if (rawValue.Equals("1", StringComparison.OrdinalIgnoreCase)
            || rawValue.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || rawValue.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        if (rawValue.Equals("0", StringComparison.OrdinalIgnoreCase)
            || rawValue.Equals("no", StringComparison.OrdinalIgnoreCase)
            || rawValue.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }

        return false;
    }

    private static bool ApplyJsonEdits(string editedAssetPath, List<StudioFieldEditDto> edits, List<string> warnings)
    {
        var root = JsonNode.Parse(File.ReadAllText(editedAssetPath));
        if (root is null)
        {
            warnings.Add($"JSON edit skipped: пустой файл {editedAssetPath}");
            return false;
        }

        var applied = 0;
        foreach (var edit in edits)
        {
            if (string.IsNullOrWhiteSpace(edit.FieldPath))
            {
                continue;
            }

            if (!TrySetJsonPathValue(root, edit.FieldPath, edit.Value))
            {
                warnings.Add($"JSON edit skipped: путь не найден {edit.FieldPath}");
                continue;
            }

            applied++;
        }

        if (applied == 0)
        {
            return false;
        }

        File.WriteAllText(
            editedAssetPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return true;
    }

    private static bool ApplyJsonListEdits(string editedAssetPath, List<StudioListEditDto> listEdits, List<string> warnings)
    {
        var root = JsonNode.Parse(File.ReadAllText(editedAssetPath));
        if (root is null)
        {
            warnings.Add($"JSON list edit skipped: пустой файл {editedAssetPath}");
            return false;
        }

        var applied = 0;
        foreach (var listEdit in listEdits)
        {
            if (string.IsNullOrWhiteSpace(listEdit.TargetPath))
            {
                continue;
            }

            if (!TryResolveJsonArray(root, listEdit.TargetPath, out var arr))
            {
                warnings.Add($"JSON list edit skipped: список не найден {listEdit.TargetPath}");
                continue;
            }

            var action = (listEdit.Action ?? string.Empty).Trim().ToLowerInvariant();
            if (action == "add-empty")
            {
                JsonNode? node = null;
                if (!string.IsNullOrWhiteSpace(listEdit.TemplateJson))
                {
                    try
                    {
                        node = JsonNode.Parse(listEdit.TemplateJson!);
                    }
                    catch
                    {
                        warnings.Add($"JSON list edit: некорректный шаблон, добавлен пустой объект ({listEdit.TargetPath})");
                    }
                }

                arr.Add(node ?? new JsonObject());
                applied++;
                continue;
            }

            if (action == "add-clone")
            {
                if (arr.Count == 0)
                {
                    warnings.Add($"JSON list edit skipped: список пуст, нечего копировать {listEdit.TargetPath}");
                    continue;
                }

                var sourceIndex = listEdit.SourceIndex ?? (arr.Count - 1);
                if (sourceIndex < 0 || sourceIndex >= arr.Count)
                {
                    warnings.Add($"JSON list edit skipped: неверный sourceIndex={sourceIndex} для {listEdit.TargetPath}");
                    continue;
                }

                var source = arr[sourceIndex];
                var clone = source is null ? null : JsonNode.Parse(source.ToJsonString());
                arr.Add(clone);
                applied++;
                continue;
            }

            if (action == "remove-index")
            {
                if (arr.Count == 0)
                {
                    warnings.Add($"JSON list edit skipped: список пуст {listEdit.TargetPath}");
                    continue;
                }

                var index = listEdit.Index ?? (arr.Count - 1);
                if (index < 0 || index >= arr.Count)
                {
                    warnings.Add($"JSON list edit skipped: неверный index={index} для {listEdit.TargetPath}");
                    continue;
                }

                arr.RemoveAt(index);
                applied++;
                continue;
            }

            if (action == "clear")
            {
                if (arr.Count == 0)
                {
                    continue;
                }

                arr.Clear();
                applied++;
                continue;
            }

            warnings.Add($"JSON list edit skipped: неподдерживаемое действие {listEdit.Action}");
        }

        if (applied == 0)
        {
            return false;
        }

        File.WriteAllText(
            editedAssetPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return true;
    }

    private static bool TrySetJsonPathValue(JsonNode root, string path, string rawValue)
    {
        if (!TryParseJsonPath(path, out var tokens) || tokens.Count == 0)
        {
            return false;
        }

        JsonNode? current = root;
        for (var i = 0; i < tokens.Count - 1; i++)
        {
            var token = tokens[i];
            if (token.IsIndex)
            {
                if (current is not JsonArray arr || token.Index < 0 || token.Index >= arr.Count)
                {
                    return false;
                }

                current = arr[token.Index];
            }
            else
            {
                if (current is not JsonObject obj || !obj.TryGetPropertyValue(token.PropertyName!, out current))
                {
                    return false;
                }
            }

            if (current is null)
            {
                return false;
            }
        }

        var leaf = tokens[^1];
        if (leaf.IsIndex)
        {
            if (current is not JsonArray arr || leaf.Index < 0 || leaf.Index >= arr.Count)
            {
                return false;
            }

            arr[leaf.Index] = ConvertJsonValue(rawValue, arr[leaf.Index]);
            return true;
        }

        if (current is not JsonObject objLeaf || !objLeaf.TryGetPropertyValue(leaf.PropertyName!, out var existing))
        {
            return false;
        }

        objLeaf[leaf.PropertyName!] = ConvertJsonValue(rawValue, existing);
        return true;
    }

    private static bool TryResolveJsonArray(JsonNode root, string path, out JsonArray arr)
    {
        arr = null!;
        if (string.Equals(path, "$", StringComparison.Ordinal))
        {
            if (root is JsonArray rootArr)
            {
                arr = rootArr;
                return true;
            }

            return false;
        }

        if (!TryParseJsonPath(path, out var tokens) || tokens.Count == 0)
        {
            return false;
        }

        JsonNode? current = root;
        foreach (var token in tokens)
        {
            if (token.IsIndex)
            {
                if (current is not JsonArray currentArr || token.Index < 0 || token.Index >= currentArr.Count)
                {
                    return false;
                }

                current = currentArr[token.Index];
            }
            else
            {
                if (current is not JsonObject currentObj || !currentObj.TryGetPropertyValue(token.PropertyName!, out current))
                {
                    return false;
                }
            }

            if (current is null)
            {
                return false;
            }
        }

        if (current is JsonArray targetArray)
        {
            arr = targetArray;
            return true;
        }

        return false;
    }

    private static JsonNode ConvertJsonValue(string rawValue, JsonNode? existingNode)
    {
        if (existingNode is JsonValue existingScalar)
        {
            if (existingScalar.TryGetValue(out bool _))
            {
                if (TryParseBool(rawValue, out var boolValue))
                {
                    return JsonValue.Create(boolValue) ?? JsonValue.Create(false)!;
                }

                return JsonValue.Create(false) ?? JsonValue.Create(false)!;
            }

            if (existingScalar.TryGetValue(out int _)
                && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                return JsonValue.Create(intValue) ?? JsonValue.Create(0)!;
            }

            if (existingScalar.TryGetValue(out long _)
                && long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            {
                return JsonValue.Create(longValue) ?? JsonValue.Create(0L)!;
            }

            if (existingScalar.TryGetValue(out double _)
                && double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
            {
                return JsonValue.Create(doubleValue) ?? JsonValue.Create(0.0)!;
            }
        }

        return JsonValue.Create(rawValue) ?? JsonValue.Create(string.Empty)!;
    }

    private static bool TryParseJsonPath(string path, out List<JsonPathToken> tokens)
    {
        tokens = [];
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("$", StringComparison.Ordinal))
        {
            return false;
        }

        var i = 1;
        while (i < path.Length)
        {
            if (path[i] == '.')
            {
                i++;
                var start = i;
                while (i < path.Length && path[i] is not '.' and not '[')
                {
                    i++;
                }

                if (i <= start)
                {
                    return false;
                }

                tokens.Add(JsonPathToken.ForProperty(path[start..i]));
                continue;
            }

            if (path[i] == '[')
            {
                i++;
                var start = i;
                while (i < path.Length && path[i] != ']')
                {
                    i++;
                }

                if (i >= path.Length || i <= start)
                {
                    return false;
                }

                if (!int.TryParse(path[start..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                {
                    return false;
                }

                tokens.Add(JsonPathToken.ForIndex(index));
                i++;
                continue;
            }

            return false;
        }

        return tokens.Count > 0;
    }

    private static string GetAssetId(StudioFileEntry entry)
    {
        return $"{entry.PresetId}::{entry.TargetRelativePath}".ToLowerInvariant();
    }

    private void RememberKnowledgeBaseItems(IEnumerable<StudioItemDto> items)
    {
        lock (_sync)
        {
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.ItemId))
                {
                    continue;
                }

                _knownKbItems[item.ItemId] = item;
            }
        }
    }

    private PakIndex GetOrLoadPakIndex()
    {
        if (_pakIndexCache is not null)
        {
            return _pakIndexCache;
        }

        lock (_sync)
        {
            if (_pakIndexCache is not null)
            {
                return _pakIndexCache;
            }

            var runStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var tmpRoot = Path.Combine(_runtimePaths.TempRoot, $"catalog-{runStamp}");
            Directory.CreateDirectory(tmpRoot);
            var cryptoPath = CryptoKeyWriter.Write(tmpRoot, DefaultAesKeyHex);
            _pakIndexCache = PakIndexService.LoadOrBuild(_scum, _unrealPakPath, cryptoPath, _ => { });
            return _pakIndexCache;
        }
    }

    private static (List<StudioFeatureDto> features, Dictionary<string, HashSet<string>> featureAssets) BuildFeatureCatalog(
        IReadOnlyList<StudioFileEntry> presetFiles)
    {
        var featureAssets = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var featureCategories = new Dictionary<string, ModCategory>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in presetFiles)
        {
            var category = ClassifyModCategory(file.TargetRelativePath);
            if (category.Id.Equals("other", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var assetId = GetAssetId(file);
            if (!featureAssets.TryGetValue(category.Id, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                featureAssets[category.Id] = set;
                featureCategories[category.Id] = category;
            }

            set.Add(assetId);
        }

        var features = featureCategories.Values
            .Select(category => new StudioFeatureDto(
                category.Id,
                category.Name,
                category.Description,
                featureAssets[category.Id].Count))
            .Where(x => x.AssetCount > 0)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (features, featureAssets);
    }

    private static string HumanizeItemName(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "Неизвестный предмет";
        }

        return LocalizeAssetStem(id);
    }

    private static ModCategory ClassifyModCategory(string relativePath)
    {
        var path = PathUtil.NormalizeRelative(relativePath).ToLowerInvariant();
        if (IsCraftingUiDataRegistryAsset(path))
        {
            return new ModCategory("crafting-recipes", "Крафт: рецепты", ResolveCategoryDescription("crafting-recipes"));
        }

        if (path.Contains("/items/crafting/recipes/", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("crafting-recipes", "Крафт: рецепты", ResolveCategoryDescription("crafting-recipes"));
        }

        if (path.Contains("/items/crafting/ingredients/", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("crafting-ingredients", "Крафт: ингредиенты", ResolveCategoryDescription("crafting-ingredients"));
        }

        if (path.Contains("/data/spawnequipment/", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("starter-kit", "Стартовый набор", ResolveCategoryDescription("starter-kit"));
        }

        if (path.Contains("/metabolism/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("foreignsubstance", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("foreign-substances", "Вещества и метаболизм", ResolveCategoryDescription("foreign-substances"));
        }

        if (path.Contains("/economy/", StringComparison.OrdinalIgnoreCase) || path.Contains("tradeable", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("economy-trader", "Трейдеры и экономика", ResolveCategoryDescription("economy-trader"));
        }

        if (path.Contains("/quests/", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("quests", "Квесты", ResolveCategoryDescription("quests"));
        }

        if (path.Contains("/foliage/farming/", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("plants-farming", "Растения и фермерство", ResolveCategoryDescription("plants-farming"));
        }

        if (path.Contains("/data/tables/items/spawning/", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("item-spawning", "Лут и спавн предметов", ResolveCategoryDescription("item-spawning"));
        }

        if (path.Contains("/data/weapon/malfunctionprobabilitycurves/", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("weapons-items", "Оружие и предметы", ResolveCategoryDescription("weapons-items"));
        }

        if (path.Contains("/items/spawnerpresets2/", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("weapons-items", "Оружие и предметы", ResolveCategoryDescription("weapons-items"));
        }

        if (path.Contains("/items/spawnerpresets/examine_data_presets/", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("weapons-items", "Оружие и предметы", ResolveCategoryDescription("weapons-items"));
        }

        if (path.Contains("/encounters/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/npcs/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/gameevents/markers/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/worldevents/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("cargodrop", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("npc-encounters", "Враги, орды и события", ResolveCategoryDescription("npc-encounters"));
        }

        if (path.Contains("/skills/", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("skills-progression", "Навыки и прокачка", ResolveCategoryDescription("skills-progression"));
        }

        if (path.Contains("/fortifications/locks/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/basebuilding/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/minigames/lockpicking/", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("locks-base", "База, замки и взлом", ResolveCategoryDescription("locks-base"));
        }

        if (path.Contains("/characters/spawnerpresets/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/characters/animals2/fish/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/items/fishing/", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("fishing-spawn", "Рыбалка и спавн рыбы", ResolveCategoryDescription("fishing-spawn"));
        }

        if (path.Contains("radiation", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("radiation", "Радиация", ResolveCategoryDescription("radiation"));
        }

        if (path.Contains("/items/christmas_items/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("christmas_present", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("seasonal-rewards", "Сезонные награды", ResolveCategoryDescription("seasonal-rewards"));
        }

        if (path.Contains("/bodyeffects/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("bodysimulation", StringComparison.OrdinalIgnoreCase)
            || path.Contains("movementsettings", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("body-effects", "Персонаж, дебафы и эффекты", ResolveCategoryDescription("body-effects"));
        }

        if (path.Contains("/vehicles/", StringComparison.OrdinalIgnoreCase) || path.Contains("hitdamagevsvehiclespeed", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("vehicles", "Транспорт", ResolveCategoryDescription("vehicles"));
        }

        if (path.Contains("/ui/gameevents/itemselection/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/items/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/gameresources/food/", StringComparison.OrdinalIgnoreCase))
        {
            return new ModCategory("weapons-items", "Оружие и предметы", ResolveCategoryDescription("weapons-items"));
        }

        return new ModCategory("other", "Прочее", ResolveCategoryDescription("other"));
    }

    private static string ResolveCategoryDescription(string categoryId)
    {
        return categoryId switch
        {
            "crafting-recipes" => "Изменение состава/настроек рецептов и времени крафта.",
            "crafting-ingredients" => "Изменение требований ингредиентов и групп замен.",
            "starter-kit" => "Параметры стартовой экипировки игрока.",
            "foreign-substances" => "Стимуляторы, токсины, скорость всасывания и выведения веществ.",
            "economy-trader" => "Торговые таблицы, цены и параметры экономики.",
            "quests" => "Квестовые таблицы, условия и журнал.",
            "plants-farming" => "Культуры, вредители и болезни для системы фермерства.",
            "item-spawning" => "Правила появления предметов по зонам, вариации предметов и общие группы отката.",
            "npc-encounters" => "Параметры врагов, орд, охраняемых зон и игровых событий.",
            "skills-progression" => "Прокачка навыков и их бонусы по уровням.",
            "locks-base" => "Параметры замков, взлома, базы и связанных мини-игр.",
            "fishing-spawn" => "Появление рыбы, пресеты водоёмов и рыболовные настройки.",
            "radiation" => "Поведение радиационных зон и связанных эффектов.",
            "seasonal-rewards" => "Праздничные подарки, ивентовые контейнеры и их содержимое.",
            "body-effects" => "Дебафы, бафы, состояние персонажа и связанные эффекты.",
            "vehicles" => "Транспорт, контейнеры, крепления оружия и связанный урон.",
            "weapons-items" => "Свойства оружия, магазинов, еды и других игровых предметов.",
            _ => "Остальные поддерживаемые ассеты."
        };
    }

    private sealed record AssetSelection(
        string AssetId,
        string TargetRelativePath,
        string? PresetSourcePath,
        string? SyntheticLaneId = null,
        string? SyntheticRowName = null);

    private sealed record ModCategory(
        string Id,
        string Name,
        string Description);

    private sealed record ModAssetDescriptor(
        string DisplayName,
        string Summary);

    private sealed record JsonPathToken(bool IsIndex, int Index, string? PropertyName)
    {
        public static JsonPathToken ForIndex(int index) => new(true, index, null);
        public static JsonPathToken ForProperty(string property) => new(false, -1, property);
    }

    private sealed record FeatureRule(
        string Id,
        string Name,
        string Description,
        Func<string, bool> Match);
}
