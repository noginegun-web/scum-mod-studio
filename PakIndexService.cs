using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ScumPakWizard;

internal sealed class PakIndex
{
    private readonly Dictionary<string, string> _fileToPak;

    public PakIndex(Dictionary<string, string> fileToPak)
    {
        _fileToPak = fileToPak;
    }

    public bool TryGetPakFor(string relativePath, out string pakPath)
    {
        return _fileToPak.TryGetValue(PathUtil.NormalizeRelative(relativePath).ToLowerInvariant(), out pakPath!);
    }

    public IReadOnlyCollection<string> GetAllRelativePaths()
    {
        return _fileToPak.Keys;
    }
}

internal static class PakIndexService
{
    private static readonly Regex MountPointRegex = new(@"Mount point (?<mount>.+)$", RegexOptions.Compiled);
    private static readonly Regex FileRegex = new(@"Display:\s+""(?<file>[^""]+)""\s+offset:", RegexOptions.Compiled);

    public static PakIndex LoadOrBuild(
        ScumInstallation scum,
        string unrealPakPath,
        string cryptoPath,
        Action<string>? log)
    {
        var cacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScumPakWizard",
            "cache");
        Directory.CreateDirectory(cacheRoot);
        var cachePath = Path.Combine(cacheRoot, "pak-index.json");

        var pakFiles = Directory
            .EnumerateFiles(scum.PaksPath, "*.pak", SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var currentStamps = pakFiles
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new PakFileStamp(path, info.Length, info.LastWriteTimeUtc.Ticks);
            })
            .ToList();

        var cached = TryLoadCache(cachePath);
        if (cached is not null && CacheMatches(cached, scum, currentStamps))
        {
            log?.Invoke("Индекс ассетов загружен из кэша.");
            return new PakIndex(cached.Files);
        }

        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pakPath in pakFiles)
        {
            log?.Invoke($"Индексирую: {Path.GetFileName(pakPath)}");
            var currentMount = string.Empty;

            var result = ProcessRunner.Run(
                unrealPakPath,
                $"\"{pakPath}\" -List -cryptokeys=\"{cryptoPath}\"",
                Path.GetDirectoryName(unrealPakPath),
                line =>
                {
                    var mountMatch = MountPointRegex.Match(line);
                    if (mountMatch.Success)
                    {
                        currentMount = NormalizeMountPoint(mountMatch.Groups["mount"].Value);
                        return;
                    }

                    var fileMatch = FileRegex.Match(line);
                    if (!fileMatch.Success || string.IsNullOrWhiteSpace(currentMount))
                    {
                        return;
                    }

                    var fileName = fileMatch.Groups["file"].Value;
                    var fullRel = PathUtil.NormalizeRelative($"{currentMount}/{fileName}").ToLowerInvariant();
                    if (!files.ContainsKey(fullRel))
                    {
                        files[fullRel] = pakPath;
                    }
                },
                timeoutMs: 30 * 60 * 1000);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Не удалось прочитать индекс из {Path.GetFileName(pakPath)}: {result.StdErrTail}");
            }
        }

        var toCache = new PakIndexCache
        {
            ScumRoot = scum.RootPath,
            BuildId = scum.BuildId,
            PakFiles = currentStamps,
            Files = files
        };
        SaveCache(cachePath, toCache);

        return new PakIndex(files);
    }

    private static string NormalizeMountPoint(string mountPoint)
    {
        var mount = mountPoint.Trim().Trim('"').Replace('\\', '/');
        while (mount.StartsWith("../", StringComparison.Ordinal))
        {
            mount = mount[3..];
        }

        return PathUtil.NormalizeRelative(mount);
    }

    private static PakIndexCache? TryLoadCache(string cachePath)
    {
        if (!File.Exists(cachePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(cachePath);
            return JsonSerializer.Deserialize<PakIndexCache>(json);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveCache(string cachePath, PakIndexCache cache)
    {
        var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = false });
        File.WriteAllText(cachePath, json, new UTF8Encoding(false));
    }

    private static bool CacheMatches(PakIndexCache cache, ScumInstallation scum, List<PakFileStamp> currentStamps)
    {
        if (!string.Equals(cache.ScumRoot, scum.RootPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(cache.BuildId ?? string.Empty, scum.BuildId ?? string.Empty, StringComparison.Ordinal))
        {
            return false;
        }

        if (cache.PakFiles.Count != currentStamps.Count)
        {
            return false;
        }

        for (var i = 0; i < currentStamps.Count; i++)
        {
            var a = cache.PakFiles[i];
            var b = currentStamps[i];
            if (!string.Equals(a.Path, b.Path, StringComparison.OrdinalIgnoreCase)
                || a.Length != b.Length
                || a.LastWriteUtcTicks != b.LastWriteUtcTicks)
            {
                return false;
            }
        }

        return true;
    }

    private sealed class PakIndexCache
    {
        public string ScumRoot { get; set; } = string.Empty;
        public string? BuildId { get; set; }
        public List<PakFileStamp> PakFiles { get; set; } = [];
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record PakFileStamp(string Path, long Length, long LastWriteUtcTicks);
}
