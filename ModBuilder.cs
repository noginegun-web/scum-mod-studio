using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ScumPakWizard;

internal sealed class ModBuilder
{
    private static readonly string[] CompanionExtensions = [".uexp", ".ubulk", ".uptnl"];
    private static readonly Regex ChunkPakNameRegex = new(
        @"^pakchunk\d+-WindowsNoEditor_(\d+)_P(?:\.pak)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly RuntimePaths _runtimePaths;
    private readonly ScumInstallation _scum;
    private readonly string _unrealPakPath;
    private readonly string _aesKeyHex;

    public ModBuilder(
        RuntimePaths runtimePaths,
        ScumInstallation scum,
        string unrealPakPath,
        string aesKeyHex)
    {
        _runtimePaths = runtimePaths;
        _scum = scum;
        _unrealPakPath = unrealPakPath;
        _aesKeyHex = aesKeyHex;
    }

    public BuildResult Build(
        IReadOnlyList<PresetDefinition> selectedPresets,
        string outputName,
        bool installToGame,
        bool seedMissingCompanions,
        bool createZip,
        Action<string>? log = null)
    {
        var files = selectedPresets
            .SelectMany(EnumeratePresetFiles)
            .Select(x => new BuildInputFile(x.SourcePath, x.TargetRelativePath))
            .ToList();

        return BuildFromEntries(
            files,
            [],
            outputName,
            installToGame,
            seedMissingCompanions,
            createZip,
            null,
            null,
            log);
    }

    public BuildResult BuildFromEntries(
        IReadOnlyList<BuildInputFile> inputFiles,
        IReadOnlyList<BuildTextFile> textFiles,
        string outputName,
        bool installToGame,
        bool seedMissingCompanions,
        bool createZip,
        IReadOnlyCollection<string>? forceCompanionAssets = null,
        IReadOnlyCollection<string>? skipCompanionAssets = null,
        Action<string>? log = null)
    {
        var writeLog = log ?? (_ => { });
        var warnings = new List<string>();
        var runStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var runRoot = Path.Combine(_runtimePaths.TempRoot, $"run-{runStamp}");
        var stageRoot = Path.Combine(runRoot, "stage");
        var extractRoot = Path.Combine(runRoot, "extract");
        Directory.CreateDirectory(stageRoot);
        Directory.CreateDirectory(extractRoot);

        var binaryMap = new Dictionary<string, BuildInputFile>(StringComparer.OrdinalIgnoreCase);
        var textMap = new Dictionary<string, BuildTextFile>(StringComparer.OrdinalIgnoreCase);
        var overrideCount = 0;

        foreach (var file in inputFiles)
        {
            var target = PathUtil.NormalizeRelative(file.TargetRelativePath);
            if (binaryMap.ContainsKey(target) || textMap.ContainsKey(target))
            {
                overrideCount++;
            }

            binaryMap[target] = file;
            textMap.Remove(target);
        }

        foreach (var text in textFiles)
        {
            var target = PathUtil.NormalizeRelative(text.TargetRelativePath);
            if (binaryMap.ContainsKey(target) || textMap.ContainsKey(target))
            {
                overrideCount++;
            }

            textMap[target] = text;
            binaryMap.Remove(target);
        }

        if (binaryMap.Count == 0 && textMap.Count == 0)
        {
            throw new InvalidOperationException("После объединения нет файлов для сборки.");
        }

        writeLog("Копирую выбранные изменения в staging...");
        foreach (var file in binaryMap.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(file.Value.SourcePath))
            {
                warnings.Add($"Файл пресета не найден: {file.Value.SourcePath}");
                continue;
            }

            CopyToStage(file.Value.SourcePath, stageRoot, file.Key);
        }

        foreach (var text in textMap.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            WriteTextToStage(stageRoot, text.Key, text.Value.Content);
        }

        var cryptoFile = CryptoKeyWriter.Write(runRoot, _aesKeyHex);
        var forceCompanionStems = NormalizeCompanionTargets(forceCompanionAssets);
        var skipCompanionStems = NormalizeCompanionTargets(skipCompanionAssets);
        var shouldSeedCompanions = seedMissingCompanions || forceCompanionStems.Count > 0;

        var seededCompanions = 0;
        if (shouldSeedCompanions)
        {
            writeLog("Строю/загружаю индекс ассетов игры...");
            var pakIndex = PakIndexService.LoadOrBuild(_scum, _unrealPakPath, cryptoFile, writeLog);
            seededCompanions = SeedCompanionFiles(
                binaryMap.Keys.Concat(textMap.Keys),
                pakIndex,
                extractRoot,
                stageRoot,
                cryptoFile,
                seedMissingCompanions,
                forceCompanionStems,
                skipCompanionStems,
                warnings);
        }

        var buildsModsRoot = Path.Combine(_runtimePaths.BuildsRoot, "mods");
        Directory.CreateDirectory(buildsModsRoot);
        var finalPakName = ResolveChunkPakFileName(outputName, buildsModsRoot);
        var outputPakPath = Path.Combine(buildsModsRoot, finalPakName);
        var responseFilePath = BuildPakResponseFile(runRoot, stageRoot);

        writeLog("Собираю .pak...");
        var packResult = ProcessRunner.Run(
            _unrealPakPath,
            $"\"{outputPakPath}\" -Create=\"{responseFilePath}\" -compress",
            _runtimePaths.WorkspaceRoot,
            _ => { },
            timeoutMs: 30 * 60 * 1000);

        if (packResult.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"UnrealPak завершился с кодом {packResult.ExitCode}.\nSTDERR: {packResult.StdErrTail}\nSTDOUT: {packResult.StdOutTail}");
        }

        string? installedPakPath = null;
        if (installToGame)
        {
            var modsDir = Path.Combine(_scum.PaksPath, "mods");
            Directory.CreateDirectory(modsDir);
            installedPakPath = Path.Combine(modsDir, Path.GetFileName(outputPakPath));
            File.Copy(outputPakPath, installedPakPath, overwrite: true);
        }

        string? outputZipPath = null;
        if (createZip)
        {
            outputZipPath = Path.Combine(buildsModsRoot, $"{Path.GetFileNameWithoutExtension(outputPakPath)}.zip");
            if (File.Exists(outputZipPath))
            {
                File.Delete(outputZipPath);
            }

            using var archive = ZipFile.Open(outputZipPath, ZipArchiveMode.Create);
            archive.CreateEntryFromFile(outputPakPath, Path.GetFileName(outputPakPath), CompressionLevel.Optimal);
        }

        var totalFiles = binaryMap.Count + textMap.Count;
        return new BuildResult(
            outputPakPath,
            outputZipPath,
            installedPakPath,
            totalFiles,
            seededCompanions,
            overrideCount,
            warnings);
    }

    private string ResolveChunkPakFileName(string requestedName, string buildsModsRoot)
    {
        var trimmed = (requestedName ?? string.Empty).Trim();
        if (trimmed.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4];
        }

        if (ChunkPakNameRegex.IsMatch(trimmed))
        {
            return $"{trimmed}.pak";
        }

        var nextIndex = ResolveNextChunkIndex(buildsModsRoot);
        return $"pakchunk99-WindowsNoEditor_{nextIndex}_P.pak";
    }

    private int ResolveNextChunkIndex(string buildsModsRoot)
    {
        var nextIndex = 1;
        foreach (var root in EnumerateChunkSearchRoots(buildsModsRoot))
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var filePath in Directory.EnumerateFiles(root, "*.pak", SearchOption.TopDirectoryOnly))
            {
                var match = ChunkPakNameRegex.Match(Path.GetFileName(filePath));
                if (!match.Success)
                {
                    continue;
                }

                if (int.TryParse(match.Groups[1].Value, out var parsed))
                {
                    nextIndex = Math.Max(nextIndex, parsed + 1);
                }
            }
        }

        return nextIndex;
    }

    private IEnumerable<string> EnumerateChunkSearchRoots(string buildsModsRoot)
    {
        yield return buildsModsRoot;

        var gameModsRoot = Path.Combine(_scum.PaksPath, "mods");
        if (!string.Equals(gameModsRoot, buildsModsRoot, StringComparison.OrdinalIgnoreCase))
        {
            yield return gameModsRoot;
        }
    }

    public IReadOnlyList<StudioFileEntry> GetStudioFileEntries(IReadOnlyList<PresetDefinition> presets)
    {
        var result = new List<StudioFileEntry>(2048);
        foreach (var preset in presets)
        {
            foreach (var entry in EnumeratePresetFiles(preset))
            {
                result.Add(new StudioFileEntry(
                    preset.Id,
                    preset.DisplayName,
                    entry.SourcePath,
                    entry.TargetRelativePath,
                    ResolveSurfaceLabel(entry.TargetRelativePath)));
            }
        }

        return result
            .OrderBy(x => x.TargetRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IEnumerable<PresetFileEntry> EnumeratePresetFiles(PresetDefinition preset)
    {
        var presetRoot = Path.Combine(_runtimePaths.AppRoot, "presets", preset.FolderName);
        if (!Directory.Exists(presetRoot))
        {
            yield break;
        }

        string sourceRoot;
        string targetPrefix;
        switch (preset.RootMode)
        {
            case PresetRootMode.ScumRoot:
                sourceRoot = presetRoot;
                targetPrefix = string.Empty;
                break;
            case PresetRootMode.ContentRoot:
                sourceRoot = Path.Combine(presetRoot, "Content");
                targetPrefix = "SCUM/Content";
                break;
            case PresetRootMode.ConZFilesRoot:
                sourceRoot = presetRoot;
                targetPrefix = "SCUM/Content/ConZ_Files";
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (!Directory.Exists(sourceRoot))
        {
            yield break;
        }

        foreach (var sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            if (sourcePath.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rel = PathUtil.NormalizeRelative(Path.GetRelativePath(sourceRoot, sourcePath));
            if (string.IsNullOrWhiteSpace(rel) || rel.StartsWith("../", StringComparison.Ordinal))
            {
                continue;
            }

            var target = string.IsNullOrEmpty(targetPrefix)
                ? rel
                : PathUtil.NormalizeRelative($"{targetPrefix}/{rel}");

            if (preset.RootMode == PresetRootMode.ScumRoot)
            {
                var startsWithKnownRoot =
                    target.StartsWith("SCUM/", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith("Engine/", StringComparison.OrdinalIgnoreCase);
                if (!startsWithKnownRoot)
                {
                    continue;
                }
            }

            yield return new PresetFileEntry(preset, sourcePath, target);
        }
    }

    private static void CopyToStage(string sourcePath, string stageRoot, string targetRelativePath)
    {
        var targetAbs = Path.Combine(stageRoot, targetRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var targetDir = Path.GetDirectoryName(targetAbs);
        if (!string.IsNullOrWhiteSpace(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        File.Copy(sourcePath, targetAbs, overwrite: true);
    }

    private static void WriteTextToStage(string stageRoot, string targetRelativePath, string content)
    {
        var targetAbs = Path.Combine(stageRoot, targetRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var targetDir = Path.GetDirectoryName(targetAbs);
        if (!string.IsNullOrWhiteSpace(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        File.WriteAllText(targetAbs, content, new UTF8Encoding(false));
    }

    private int SeedCompanionFiles(
        IEnumerable<string> targetRelativePaths,
        PakIndex pakIndex,
        string extractRoot,
        string stageRoot,
        string cryptoPath,
        bool globalSeedEnabled,
        IReadOnlySet<string> forceCompanionStems,
        IReadOnlySet<string> skipCompanionStems,
        List<string> warnings)
    {
        var existing = new HashSet<string>(targetRelativePaths, StringComparer.OrdinalIgnoreCase);
        var stems = existing
            .Where(path => path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".umap", StringComparison.OrdinalIgnoreCase))
            .Select(path => PathUtil.NormalizeRelative(path[..^Path.GetExtension(path).Length]))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var extractedJobs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seeded = 0;

        foreach (var stem in stems)
        {
            var stemKey = stem.ToLowerInvariant();
            if (skipCompanionStems.Contains(stemKey))
            {
                continue;
            }

            if (!globalSeedEnabled && !forceCompanionStems.Contains(stemKey))
            {
                continue;
            }

            foreach (var ext in CompanionExtensions)
            {
                var companionRel = $"{stem}{ext}";
                if (existing.Contains(companionRel))
                {
                    continue;
                }

                if (!pakIndex.TryGetPakFor(companionRel, out var pakPath))
                {
                    continue;
                }

                var fileName = Path.GetFileName(companionRel);
                var extractJobKey = $"{pakPath}|{fileName}";
                if (!extractedJobs.Contains(extractJobKey))
                {
                    var extractResult = ProcessRunner.Run(
                        _unrealPakPath,
                        $"\"{pakPath}\" -Extract \"{extractRoot}\" -extracttomountpoint -Filter=\"*{fileName}\" -cryptokeys=\"{cryptoPath}\"",
                        _runtimePaths.WorkspaceRoot,
                        _ => { },
                        timeoutMs: 10 * 60 * 1000);

                    extractedJobs.Add(extractJobKey);
                    if (extractResult.ExitCode != 0)
                    {
                        warnings.Add($"Не удалось извлечь {fileName} из {Path.GetFileName(pakPath)}");
                        continue;
                    }
                }

                var expected = Path.Combine(extractRoot, companionRel.Replace('/', Path.DirectorySeparatorChar));
                var sourceCandidate = File.Exists(expected)
                    ? expected
                    : TryFindExtractedMatch(extractRoot, companionRel);

                if (sourceCandidate is null || !File.Exists(sourceCandidate))
                {
                    warnings.Add($"Companion не найден после извлечения: {companionRel}");
                    continue;
                }

                CopyToStage(sourceCandidate, stageRoot, companionRel);
                existing.Add(companionRel);
                seeded++;
            }
        }

        return seeded;
    }

    private static HashSet<string> NormalizeCompanionTargets(IReadOnlyCollection<string>? companionAssets)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (companionAssets is null || companionAssets.Count == 0)
        {
            return result;
        }

        foreach (var asset in companionAssets)
        {
            if (string.IsNullOrWhiteSpace(asset))
            {
                continue;
            }

            var normalized = PathUtil.NormalizeRelative(asset);
            var extension = Path.GetExtension(normalized);
            if (extension.Equals(".uasset", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".umap", StringComparison.OrdinalIgnoreCase))
            {
                var stem = normalized[..^extension.Length].ToLowerInvariant();
                result.Add(stem);
            }
        }

        return result;
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

    private static string BuildPakResponseFile(string runRoot, string stageRoot)
    {
        var responseFilePath = Path.Combine(runRoot, "pak-response.txt");
        var lines = Directory
            .EnumerateFiles(stageRoot, "*", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(file =>
            {
                var rel = PathUtil.NormalizeRelative(Path.GetRelativePath(stageRoot, file));
                var mount = $"../../../{rel}";
                return $"\"{file.Replace('\\', '/')}\" \"{mount}\"";
            });

        File.WriteAllLines(responseFilePath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return responseFilePath;
    }

    private static string ResolveSurfaceLabel(string targetRelativePath)
    {
        var path = targetRelativePath.ToLowerInvariant();
        if (path.Contains("/items/crafting/recipes/"))
        {
            return "Crafting Recipes";
        }

        if (path.Contains("/items/crafting/ingredients/"))
        {
            return "Crafting Ingredients";
        }

        if (path.Contains("/ui/"))
        {
            return "UI";
        }

        if (path.Contains("/economy/"))
        {
            return "Economy";
        }

        if (path.Contains("/quests/"))
        {
            return "Quests";
        }

        if (path.Contains("/spawnequipment/"))
        {
            return "Starter Equipment";
        }

        if (path.Contains("/encounters/"))
        {
            return "Encounters";
        }

        if (path.Contains("/npcs/"))
        {
            return "NPCs";
        }

        if (path.Contains("/radiation"))
        {
            return "Radiation";
        }

        return "General";
    }
}
