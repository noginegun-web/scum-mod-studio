namespace ScumPakWizard;

internal sealed record StudioPresetDto(
    string Id,
    string Name,
    string Description,
    string[] Surfaces);

internal sealed record StudioAssetDto(
    string AssetId,
    string PresetId,
    string PresetName,
    string RelativePath,
    string Surface,
    bool IsGameAsset,
    bool HasPresetAlternative);

internal sealed record StudioAssetQueryResultDto(
    int Total,
    int Page,
    int PageSize,
    List<StudioAssetDto> Items);

internal sealed record StudioItemDto(
    string ItemId,
    string ItemName,
    string RelativePath,
    string? IconUrl);

internal sealed record StudioFeatureDto(
    string Id,
    string Name,
    string Description,
    int AssetCount);

internal sealed record StudioStatusDto(
    string ScumRoot,
    string ScumPaks,
    string? BuildId,
    string UnrealPakPath,
    List<StudioPresetDto> Presets,
    List<StudioFeatureDto> Features,
    int PresetAssetCount);

internal sealed record StudioResearchOwnerHintDto(
    string FilePath,
    string Reason);

internal sealed record StudioResearchImportDeltaDto(
    bool ImportsOnly,
    int AddedImportCount,
    int RemovedImportCount,
    int AddedSoftObjectPathCount,
    int RemovedSoftObjectPathCount,
    List<string> AddedImports,
    List<string> RemovedImports,
    List<string> AddedSoftObjectPaths,
    List<string> RemovedSoftObjectPaths,
    List<string> Warnings);

internal sealed record StudioResearchVariantDto(
    string FilePath,
    string PackageRoot,
    string RuntimeLayout,
    bool HasOriginalTwin,
    bool IsBlueprintLike,
    List<string> Tags,
    StudioResearchImportDeltaDto? ImportDelta,
    List<StudioResearchOwnerHintDto> OwnerHints);

internal sealed record StudioResearchModPatternDto(
    string Query,
    string NormalizedRelativePath,
    string GamePackagePath,
    bool IsBlueprintLike,
    List<string> OriginalFiles,
    List<StudioResearchVariantDto> ModVariants,
    List<string> GlobalTags,
    List<string> Warnings);

internal sealed record StudioCatalogDto(
    int ItemCount,
    List<StudioItemDto> Items);

internal sealed record BuildRecipeIngredientDto(
    string ItemId,
    string ItemName,
    int Amount);

internal sealed record BuildRecipePlanDto(
    string ResultItemId,
    string ResultItemName,
    int ResultAmount,
    List<BuildRecipeIngredientDto> Ingredients);

internal sealed record StudioAssetSettingDto(
    string AssetId,
    bool Enabled,
    string? SourceMode,
    string? CompanionMode);

internal sealed record StudioModCategoryDto(
    string CategoryId,
    string Name,
    string Description,
    int AssetCount);

internal sealed record StudioModAssetDto(
    string AssetId,
    string RelativePath,
    string CategoryId,
    string CategoryName,
    string DisplayName,
    string Summary,
    string AssetFormat,
    bool SupportsSafeEdits);

internal sealed record StudioModAssetQueryResultDto(
    int Total,
    int Page,
    int PageSize,
    List<StudioModAssetDto> Items);

internal sealed record StudioModFieldDto(
    string FieldPath,
    string Label,
    string Description,
    string Section,
    string ValueType,
    string EditorKind,
    string CurrentValue,
    bool Editable,
    string? SuggestedMin,
    string? SuggestedMax,
    List<StudioModFieldOptionDto>? Options,
    string? ReferencePickerKind = null,
    string? ReferencePickerPrompt = null,
    string? CurrentDisplayValue = null);

internal sealed record StudioModFieldOptionDto(
    string Value,
    string Label);

internal sealed record StudioModListTargetDto(
    string TargetPath,
    string Label,
    string Description,
    string ItemKind,
    int ItemCount,
    bool SupportsAddClone,
    bool SupportsRemove,
    bool SupportsClear,
    bool SupportsAddEmpty,
    bool SupportsAddReference = false,
    string? ReferencePickerKind = null,
    string? ReferencePickerPrompt = null,
    List<string>? EntryLabels = null);

internal sealed record StudioReferenceOptionDto(
    string Value,
    string Label);

internal sealed record StudioModAssetSchemaDto(
    string AssetId,
    string RelativePath,
    string CategoryId,
    string CategoryName,
    string SourceKind,
    string AssetFormat,
    List<StudioModFieldDto> Fields,
    List<StudioModListTargetDto> ListTargets,
    List<string> Warnings);

internal sealed record StudioFieldEditDto(
    string FieldPath,
    string Value);

internal sealed record StudioListEditDto(
    string TargetPath,
    string Action,
    int? Index,
    int? SourceIndex,
    string? TemplateJson,
    string? RawValue = null);

internal sealed record StudioAssetEditDto(
    string AssetId,
    List<StudioFieldEditDto> Edits,
    List<StudioListEditDto>? ListEdits);

internal sealed record StudioSchemaPreviewRequestDto(
    string AssetId,
    List<StudioFieldEditDto>? Edits,
    List<StudioListEditDto>? ListEdits,
    string? SourceMode,
    string? CompanionMode);

internal sealed record StudioFeatureSettingDto(
    string FeatureId,
    bool Enabled,
    string? SourceMode,
    string? CompanionMode);

internal sealed record StudioBuildRequestDto(
    string? ModName,
    bool InstallToGame,
    bool CreateZip,
    bool SeedCompanions,
    List<string>? EnabledPresetIds,
    List<string>? EnabledFeatureIds,
    List<StudioFeatureSettingDto>? FeatureSettings,
    List<string>? SelectedAssetIds,
    List<StudioAssetSettingDto>? AssetSettings,
    List<StudioAssetEditDto>? AssetEdits,
    List<BuildRecipePlanDto>? Recipes);

internal sealed record StudioBuildResultDto(
    bool Ok,
    string? Error,
    string? OutputPakPath,
    string? OutputZipPath,
    string? InstalledPakPath,
    int PresetFileCount,
    int SeededCompanionCount,
    int OverrideCount,
    List<string> Warnings);
