namespace ScumPakWizard;

internal sealed record RuntimePaths(
    string AppRoot,
    string WorkspaceRoot,
    string TempRoot,
    string BuildsRoot);

internal sealed record ScumInstallation(
    string RootPath,
    string ExePath,
    string PaksPath,
    string? AppManifestPath,
    string? BuildId);

internal enum PresetRootMode
{
    ScumRoot,
    ContentRoot,
    ConZFilesRoot
}

internal sealed record PresetDefinition(
    string Id,
    string DisplayName,
    string Description,
    string FolderName,
    PresetRootMode RootMode,
    string[] EditableSurfaces);

internal sealed record PresetFileEntry(
    PresetDefinition Preset,
    string SourcePath,
    string TargetRelativePath);

internal sealed record BuildResult(
    string OutputPakPath,
    string? OutputZipPath,
    string? InstalledPakPath,
    int PresetFileCount,
    int SeededCompanionCount,
    int OverrideCount,
    List<string> Warnings);

internal sealed record ProcessRunResult(int ExitCode, string StdOutTail, string StdErrTail);

internal sealed record StudioFileEntry(
    string PresetId,
    string PresetName,
    string SourcePath,
    string TargetRelativePath,
    string SurfaceLabel);

internal sealed record StudioRecipeIngredient(
    string ItemId,
    string ItemName,
    int Amount);

internal sealed record StudioRecipePlan(
    string ResultItemId,
    string ResultItemName,
    int ResultAmount,
    List<StudioRecipeIngredient> Ingredients);

internal sealed record BuildInputFile(
    string SourcePath,
    string TargetRelativePath);

internal sealed record BuildTextFile(
    string TargetRelativePath,
    string Content);
