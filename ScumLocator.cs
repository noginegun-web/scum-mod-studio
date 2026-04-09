using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ScumPakWizard;

internal static class ScumLocator
{
    private const string ScumAppId = "513710";

    public static ScumInstallation? Locate()
    {
        var envRoot = Environment.GetEnvironmentVariable("SCUM_PATH");
        var byEnv = TryBuildInstallation(envRoot);
        if (byEnv is not null)
        {
            return byEnv;
        }

        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in GetSteamLibraryCandidates())
        {
            libraries.Add(path);
        }

        foreach (var library in libraries)
        {
            var manifestPath = Path.Combine(library, "steamapps", $"appmanifest_{ScumAppId}.acf");
            var gameRoot = Path.Combine(library, "steamapps", "common", "SCUM");
            var installation = TryBuildInstallation(gameRoot, manifestPath);
            if (installation is not null)
            {
                return installation;
            }
        }

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            var steamRoot = Path.Combine(drive.RootDirectory.FullName, "SteamLibrary");
            var manifestPath = Path.Combine(steamRoot, "steamapps", $"appmanifest_{ScumAppId}.acf");
            var gameRoot = Path.Combine(steamRoot, "steamapps", "common", "SCUM");
            var installation = TryBuildInstallation(gameRoot, manifestPath);
            if (installation is not null)
            {
                return installation;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetSteamLibraryCandidates()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var steamPaths = new[]
        {
            TryReadSteamPath(Registry.CurrentUser, @"Software\Valve\Steam"),
            TryReadSteamPath(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam"),
            TryReadSteamPath(Registry.LocalMachine, @"SOFTWARE\Valve\Steam")
        };

        foreach (var steamPath in steamPaths)
        {
            if (string.IsNullOrWhiteSpace(steamPath))
            {
                continue;
            }

            candidates.Add(steamPath);
            var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            foreach (var parsed in ParseLibraryFolders(vdfPath))
            {
                candidates.Add(parsed);
            }
        }

        candidates.Add(@"C:\Program Files (x86)\Steam");
        candidates.Add(@"D:\SteamLibrary");
        candidates.Add(@"E:\SteamLibrary");
        candidates.Add(@"F:\SteamLibrary");

        return candidates.Where(Directory.Exists);
    }

    private static string? TryReadSteamPath(RegistryKey root, string subKey)
    {
        try
        {
            using var key = root.OpenSubKey(subKey, writable: false);
            var value = key?.GetValue("SteamPath") as string;
            return string.IsNullOrWhiteSpace(value) ? null : value.Replace('/', Path.DirectorySeparatorChar);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> ParseLibraryFolders(string vdfPath)
    {
        if (!File.Exists(vdfPath))
        {
            yield break;
        }

        var pattern = new Regex("\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        foreach (var line in File.ReadLines(vdfPath))
        {
            var match = pattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var raw = match.Groups[1].Value;
            var normalized = raw.Replace(@"\\", @"\").Replace('/', Path.DirectorySeparatorChar);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static ScumInstallation? TryBuildInstallation(string? gameRoot, string? manifestPath = null)
    {
        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            return null;
        }

        var scumExe = Path.Combine(gameRoot, "SCUM", "Binaries", "Win64", "SCUM.exe");
        var paksPath = Path.Combine(gameRoot, "SCUM", "Content", "Paks");
        if (!File.Exists(scumExe) || !Directory.Exists(paksPath))
        {
            return null;
        }

        string? buildId = null;
        if (!string.IsNullOrWhiteSpace(manifestPath) && File.Exists(manifestPath))
        {
            buildId = ParseBuildId(manifestPath);
        }

        return new ScumInstallation(
            Path.GetFullPath(gameRoot),
            scumExe,
            paksPath,
            manifestPath is not null && File.Exists(manifestPath) ? manifestPath : null,
            buildId);
    }

    private static string? ParseBuildId(string manifestPath)
    {
        try
        {
            var content = File.ReadAllText(manifestPath);
            var match = Regex.Match(content, "\"buildid\"\\s+\"(?<id>\\d+)\"", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["id"].Value : null;
        }
        catch
        {
            return null;
        }
    }
}
