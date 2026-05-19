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

internal sealed record StudioToolchainStepDto(
    string Id,
    string Name,
    string Status,
    bool Ready,
    string? Path,
    string Description,
    string? InstallTitle = null,
    List<string>? InstallSteps = null,
    string? ActionLabel = null,
    string? ActionUrl = null,
    bool CanAutoInstall = false);

internal sealed record StudioToolchainStatusDto(
    bool ReadyForCookedMods,
    bool ReadyForRawModelCook,
    string Summary,
    List<StudioToolchainStepDto> Steps);

internal sealed record StudioStatusDto(
    string ScumRoot,
    string ScumPaks,
    string? BuildId,
    string UnrealPakPath,
    List<StudioPresetDto> Presets,
    List<StudioFeatureDto> Features,
    int PresetAssetCount,
    bool ScumFound,
    bool UnrealPakFound,
    StudioToolchainStatusDto Toolchain,
    List<string> Warnings);

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

internal sealed record StudioCustomVisualAssetDto(
    string Name,
    string Kind,
    string TargetRelativePath,
    string ObjectReference,
    string AssetReference);

internal sealed record StudioModelBoundsDto(
    double MinX,
    double MinY,
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ,
    double SizeX,
    double SizeY,
    double SizeZ);

internal sealed record StudioRawModelImportDto(
    string Name,
    string Format,
    string SourceRelativePath,
    long SizeBytes,
    string Status,
    List<string> AdaptationHints,
    StudioModelBoundsDto? Bounds = null,
    List<StudioRawModelPartDto>? Parts = null);

internal sealed record StudioRawModelPartDto(
    string Name,
    string Role,
    int Vertices,
    int Triangles,
    StudioModelBoundsDto Bounds,
    string Recommendation);

internal sealed record StudioCustomVisualImportResultDto(
    bool Ok,
    string? Error,
    int ImportedFileCount,
    List<StudioCustomVisualAssetDto> Assets,
    List<string> Warnings,
    List<StudioRawModelImportDto>? RawModels = null);

internal sealed record StudioRawModelCookRequestDto(
    string RawSourceRelativePath,
    string? AssetId,
    string? FieldPath,
    string? ModelKind,
    double? ScalePercent,
    double? OffsetX,
    double? OffsetY,
    double? OffsetZ,
    double? Pitch,
    double? Yaw,
    double? Roll,
    bool? AutoFitToTarget = null,
    double? TargetLongestCm = null,
    string? PaintColorHex = null,
    double? PaintStrengthPercent = null,
    double? MetallicPercent = null,
    double? RoughnessPercent = null,
    string? MaterialMode = null,
    string? MaterialReference = null,
    List<string>? RawPartNames = null,
    int? TargetTriangleCount = null,
    string? CollisionMode = null,
    double? QueryProxyLengthPercent = null,
    double? QueryProxyWidthPercent = null,
    double? QueryProxyHeightPercent = null,
    double? WeaponGripAnchorPercent = null,
    double? WeaponGripDiameterCm = null,
    double? WeaponGripBackReachCm = null,
    double? WeaponSecondHandShiftCm = null);

internal sealed record StudioRawModelCookResultDto(
    bool Ok,
    string? Error,
    string? CookedTargetRelativePath,
    List<StudioCustomVisualAssetDto> Assets,
    List<string> Warnings,
    string? UnrealLogTail = null,
    string? BlenderLogTail = null,
    StudioAssetEditDto? SuggestedEdit = null);

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

internal sealed record StudioFieldDiscoveryCandidateDto(
    string FieldPath,
    string SourceLabel,
    string Label,
    string ValueType,
    string CurrentValue,
    string CurrentDisplayValue,
    bool Exposed,
    string Visibility,
    string Section,
    string Description,
    string EditorKind,
    string? ReferencePickerKind);

internal sealed record StudioListDiscoveryCandidateDto(
    string TargetPath,
    string SourceLabel,
    string Label,
    string ItemKind,
    int ItemCount,
    bool Exposed,
    string Visibility,
    bool SupportsAddClone,
    bool SupportsRemove,
    bool SupportsClear,
    bool SupportsAddEmpty,
    bool SupportsAddReference,
    string? ReferencePickerKind);

internal sealed record StudioFieldDiscoveryReportDto(
    string AssetId,
    string RelativePath,
    string CategoryId,
    string CategoryName,
    string SourceKind,
    string AssetFormat,
    int RawFieldCandidateCount,
    int ExposedFieldCandidateCount,
    int HiddenFieldCandidateCount,
    int RawListCandidateCount,
    int ExposedListCandidateCount,
    int HiddenListCandidateCount,
    List<StudioFieldDiscoveryCandidateDto> Fields,
    List<StudioListDiscoveryCandidateDto> Lists,
    List<string> Warnings);

internal sealed record StudioReplacementContractFieldDto(
    string FieldPath,
    string Label,
    string Kind,
    string CurrentValue,
    string CurrentDisplayValue,
    bool Exposed,
    string Visibility,
    string? ReferencePickerKind);

internal sealed record StudioReplacementContractAssetDto(
    string AssetId,
    string RelativePath,
    string DisplayName,
    string Role,
    int VisualFieldCount,
    int MeshFieldCount,
    int MaterialFieldCount,
    int TextureFieldCount,
    int AnimationFieldCount,
    int PhysicsFieldCount,
    int CollisionFieldCount,
    int SocketFieldCount,
    int AttachmentFieldCount,
    int SkinFieldCount,
    int IconFieldCount,
    List<string> RequiredSockets,
    List<string> MaterialReferences,
    List<string> TextureReferences,
    List<StudioReplacementContractFieldDto> KeyFields,
    List<string> Warnings);

internal sealed record StudioReplacementContractLinkDto(
    string FromAssetId,
    string ToAssetId,
    string Label,
    string FieldPath,
    string Reference,
    string Kind);

internal sealed record StudioReplacementContractDto(
    bool Ok,
    string? Error,
    string AssetId,
    string RelativePath,
    string DisplayName,
    string DomainKind,
    string ReplacementStrategy,
    List<StudioReplacementContractAssetDto> Assets,
    List<StudioReplacementContractLinkDto> Links,
    List<string> RequiredSockets,
    List<string> MaterialReferences,
    List<string> TextureReferences,
    List<string> Warnings,
    List<string> Recommendations);

internal sealed record StudioWeaponContractFieldDto(
    string FieldPath,
    string Label,
    string Kind,
    string CurrentValue,
    string CurrentDisplayValue,
    bool Exposed,
    string Visibility,
    string? ReferencePickerKind);

internal sealed record StudioWeaponContractAssetDto(
    string AssetId,
    string RelativePath,
    string DisplayName,
    string Role,
    int MeshFieldCount,
    int FirstPersonFieldCount,
    int HandsFieldCount,
    int SocketFieldCount,
    int MaterialFieldCount,
    int SkinFieldCount,
    int IconFieldCount,
    List<string> RequiredSockets,
    List<string> MaterialReferences,
    List<StudioWeaponContractFieldDto> KeyFields,
    List<string> Warnings);

internal sealed record StudioWeaponContractLinkDto(
    string FromAssetId,
    string ToAssetId,
    string Label,
    string FieldPath,
    string Reference);

internal sealed record StudioWeaponContractDto(
    bool Ok,
    string? Error,
    string AssetId,
    string RelativePath,
    string DisplayName,
    string ProfileKind,
    List<StudioWeaponContractAssetDto> Assets,
    List<StudioWeaponContractLinkDto> Links,
    List<string> RequiredSockets,
    List<string> MaterialReferences,
    List<string> Warnings,
    List<string> Recommendations);

internal sealed record StudioVehicleProfileFieldDto(
    string FieldPath,
    string Label,
    string Kind,
    string CurrentValue,
    string CurrentDisplayValue);

internal sealed record StudioVehicleProfileAssetDto(
    string AssetId,
    string RelativePath,
    string DisplayName,
    string Role,
    int VisualFieldCount,
    int QueryFieldCount,
    int SkeletalFieldCount,
    int StaticFieldCount,
    int MaterialFieldCount,
    int SocketFieldCount,
    int SlotFieldCount,
    int MountFieldCount,
    List<string> RequiredSockets,
    List<string> MaterialReferences,
    List<StudioVehicleProfileFieldDto> KeyFields,
    List<string> Warnings);

internal sealed record StudioVehicleProfileLinkDto(
    string FromAssetId,
    string ToAssetId,
    string Label,
    string FieldPath,
    string Reference);

internal sealed record StudioVehicleProfileDto(
    bool Ok,
    string? Error,
    string AssetId,
    string RelativePath,
    string DisplayName,
    string ProfileKind,
    List<StudioVehicleProfileAssetDto> Assets,
    List<StudioVehicleProfileLinkDto> Links,
    List<string> RequiredSockets,
    List<string> MaterialReferences,
    List<string> Warnings,
    List<string> Recommendations);

internal sealed record StudioVehicleModulePlanEntryDto(
    string ModuleRole,
    string TargetAssetId,
    string TargetRelativePath,
    string TargetDisplayName,
    string TargetFieldPath,
    string TargetFieldLabel,
    string TargetCurrentValue,
    string TargetCurrentDisplayValue,
    string TargetMeshKind,
    string ReplacementStrategy,
    string SafetyLevel,
    bool CanAutoCook,
    List<string> RawPartNames,
    int RawTriangleCount,
    int TargetTriangleCount,
    StudioModelBoundsDto? RawBounds,
    double TargetLongestCm,
    List<string> RequiredSockets,
    List<string> MaterialReferences,
    string Recommendation);

internal sealed record StudioVehicleModulePlanDto(
    bool Ok,
    string? Error,
    string AssetId,
    string RawSourceRelativePath,
    string DisplayName,
    string ProfileKind,
    StudioModelBoundsDto? RawBounds,
    List<StudioVehicleModulePlanEntryDto> Entries,
    List<string> Warnings,
    List<string> NextSteps);

internal sealed record StudioVehicleModuleCookRequestDto(
    string AssetId,
    string RawSourceRelativePath,
    string TargetAssetId,
    string? TargetFieldPath = null,
    string? MaterialReference = null);

internal sealed record StudioVehicleModuleCookBatchRequestDto(
    string AssetId,
    string RawSourceRelativePath,
    int? MaxModules = null);

internal sealed record StudioVehicleModuleCookBatchItemDto(
    string TargetAssetId,
    string TargetRelativePath,
    string TargetDisplayName,
    string TargetFieldPath,
    string SafetyLevel,
    bool Ok,
    string? Error,
    string? CookedTargetRelativePath,
    StudioAssetEditDto? SuggestedEdit,
    List<string> Warnings);

internal sealed record StudioVehicleModuleCookBatchResultDto(
    bool Ok,
    string? Error,
    List<StudioVehicleModuleCookBatchItemDto> Items,
    List<StudioAssetEditDto> SuggestedEdits,
    List<string> Warnings);

internal sealed record StudioVehicleFullReplacementRequestDto(
    string AssetId,
    string RawSourceRelativePath,
    bool? InstallToGame = null,
    string? ModName = null,
    double? TargetLongestCm = null,
    int? TargetTriangleCount = null,
    string? MaterialMode = null,
    string? MaterialReference = null,
    double? ScalePercent = null,
    double? OffsetX = null,
    double? OffsetY = null,
    double? OffsetZ = null,
    double? Pitch = null,
    double? Yaw = null,
    double? Roll = null,
    string? PaintColorHex = null,
    double? PaintStrengthPercent = null,
    double? MetallicPercent = null,
    double? RoughnessPercent = null,
    string? CollisionMode = null,
    double? QueryProxyLengthPercent = null,
    double? QueryProxyWidthPercent = null,
    double? QueryProxyHeightPercent = null,
    double? SeatOffsetX = null,
    double? SeatOffsetY = null,
    double? SeatOffsetZ = null,
    double? PassengerSeatOffsetX = null,
    double? PassengerSeatOffsetY = null,
    double? PassengerSeatOffsetZ = null,
    double? EntryOffsetX = null,
    double? EntryOffsetY = null,
    double? EntryOffsetZ = null);

internal sealed record StudioVehicleFullReplacementResultDto(
    bool Ok,
    string? Error,
    StudioBuildResultDto? BuildResult,
    StudioRawModelCookResultDto? BodyCookResult,
    List<StudioRawModelCookResultDto> SuppressorCookResults,
    List<StudioAssetEditDto> SuggestedEdits,
    List<string> Warnings);

internal sealed record StudioArmorSetPlanEntryDto(
    string ModuleRole,
    string TargetAssetId,
    string TargetRelativePath,
    string TargetDisplayName,
    string TargetFieldPath,
    string TargetFieldLabel,
    string TargetCurrentValue,
    string TargetCurrentDisplayValue,
    string TargetMeshKind,
    bool CanAutoCook,
    List<string> RawPartNames,
    int RawTriangleCount,
    int TargetTriangleCount,
    double TargetLongestCm,
    string Recommendation);

internal sealed record StudioArmorSetPlanDto(
    bool Ok,
    string? Error,
    string RawSourceRelativePath,
    StudioModelBoundsDto? RawBounds,
    List<StudioArmorSetPlanEntryDto> Entries,
    List<string> Warnings,
    List<string> NextSteps);

internal sealed record StudioArmorSetCookRequestDto(
    string RawSourceRelativePath,
    string TargetAssetId,
    string? TargetFieldPath = null);

internal sealed record StudioArmorSetCookBatchRequestDto(
    string RawSourceRelativePath,
    int? MaxModules = null);

internal sealed record StudioArmorSetCookBatchItemDto(
    string ModuleRole,
    string TargetAssetId,
    string TargetRelativePath,
    string TargetDisplayName,
    string TargetFieldPath,
    string TargetMeshKind,
    bool Ok,
    string? Error,
    string? CookedTargetRelativePath,
    StudioAssetEditDto? SuggestedEdit,
    List<string> Warnings);

internal sealed record StudioArmorSetCookBatchResultDto(
    bool Ok,
    string? Error,
    List<StudioArmorSetCookBatchItemDto> Items,
    List<StudioAssetEditDto> SuggestedEdits,
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
