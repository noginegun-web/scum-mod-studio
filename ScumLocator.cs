using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ScumPakWizard;

internal static class ScumLocator
{
    private const string ScumAppId = "513710";
    private static readonly EnumerationOptions SafeDirectoryEnumeration = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint
    };

    private static readonly HashSet<string> SearchSkipDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$Recycle.Bin",
        "System Volume Information",
        "Windows",
        "WinSxS",
        "ProgramData",
        "AppData",
        "node_modules",
        ".git"
    };

    public static ScumInstallation? Locate()
    {
        var envRoot = Environment.GetEnvironmentVariable("SCUM_PATH");
        var byEnv = TryBuildInstallation(envRoot);
        if (byEnv is not null)
        {
            return byEnv;
        }

        foreach (var manifestPath in GetSteamAppManifestCandidates())
        {
            var byManifest = TryBuildInstallationFromManifest(manifestPath);
            if (byManifest is not null)
            {
                return byManifest;
            }
        }

        foreach (var gameRoot in GetScumRootCandidates())
        {
            var installation = TryBuildInstallation(gameRoot);
            if (installation is not null)
            {
                return installation;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetSteamAppManifestCandidates()
    {
        var manifests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var library in GetSteamLibraryCandidates())
        {
            AddManifestCandidate(manifests, Path.Combine(library, "steamapps", $"appmanifest_{ScumAppId}.acf"));
        }

        foreach (var steamAppsPath in GetSteamAppsFolderCandidates())
        {
            AddManifestCandidate(manifests, Path.Combine(steamAppsPath, $"appmanifest_{ScumAppId}.acf"));
        }

        return manifests;
    }

    private static void AddManifestCandidate(HashSet<string> manifests, string manifestPath)
    {
        if (File.Exists(manifestPath))
        {
            manifests.Add(manifestPath);
        }
    }

    private static IEnumerable<string> GetScumRootCandidates()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var library in GetSteamLibraryCandidates())
        {
            candidates.Add(Path.Combine(library, "steamapps", "common", "SCUM"));
        }

        foreach (var steamAppsPath in GetSteamAppsFolderCandidates())
        {
            candidates.Add(Path.Combine(steamAppsPath, "common", "SCUM"));
        }

        foreach (var drive in GetFixedDrives())
        {
            foreach (var scumRoot in FindDirectoriesByName(drive.RootDirectory.FullName, "SCUM", maxDepth: 5))
            {
                candidates.Add(scumRoot);
            }
        }

        return candidates;
    }

    private static IEnumerable<string> GetSteamAppsFolderCandidates()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var drive in GetFixedDrives())
        {
            var root = drive.RootDirectory.FullName;
            var quickCandidates = new[]
            {
                Path.Combine(root, "SteamLibrary", "steamapps"),
                Path.Combine(root, "Steam", "steamapps"),
                Path.Combine(root, "SteamGames", "steamapps"),
                Path.Combine(root, "Games", "SteamLibrary", "steamapps"),
                Path.Combine(root, "Games", "Steam", "steamapps"),
                Path.Combine(root, "Program Files", "Steam", "steamapps"),
                Path.Combine(root, "Program Files (x86)", "Steam", "steamapps")
            };

            foreach (var candidate in quickCandidates)
            {
                if (Directory.Exists(candidate))
                {
                    candidates.Add(candidate);
                }
            }
        }

        foreach (var drive in GetFixedDrives())
        {
            foreach (var steamAppsPath in FindDirectoriesByName(drive.RootDirectory.FullName, "steamapps", maxDepth: 4))
            {
                candidates.Add(steamAppsPath);
            }
        }

        return candidates;
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

    private static IEnumerable<DriveInfo> GetFixedDrives()
    {
        return DriveInfo.GetDrives()
            .Where(drive =>
            {
                try
                {
                    return drive.IsReady && drive.DriveType == DriveType.Fixed;
                }
                catch
                {
                    return false;
                }
            });
    }

    private static IEnumerable<string> FindDirectoriesByName(string root, string directoryName, int maxDepth)
    {
        if (maxDepth < 0 || string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            yield break;
        }

        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(current, "*", SafeDirectoryEnumeration);
            }
            catch
            {
                continue;
            }

            foreach (var child in children)
            {
                var name = Path.GetFileName(child.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (SearchSkipDirectoryNames.Contains(name))
                {
                    continue;
                }

                if (name.Equals(directoryName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return child;
                }

                if (depth < maxDepth)
                {
                    queue.Enqueue((child, depth + 1));
                }
            }
        }
    }

    private static string? TryReadSteamPath(RegistryKey root, string subKey)
    {
        try
        {
            using var key = root.OpenSubKey(subKey, writable: false);
            var value = key?.GetValue("SteamPath") as string
                ?? key?.GetValue("InstallPath") as string;
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

    private static ScumInstallation? TryBuildInstallationFromManifest(string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            return null;
        }

        var steamAppsPath = Path.GetDirectoryName(manifestPath);
        if (string.IsNullOrWhiteSpace(steamAppsPath))
        {
            return null;
        }

        var installDir = ParseInstallDir(manifestPath);
        var gameRoot = Path.Combine(steamAppsPath, "common", string.IsNullOrWhiteSpace(installDir) ? "SCUM" : installDir);
        return TryBuildInstallation(gameRoot, manifestPath);
    }

    private static ScumInstallation? TryBuildInstallation(string? gameRoot, string? manifestPath = null)
    {
        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            return null;
        }

        var scumExe = Path.Combine(gameRoot, "SCUM", "Binaries", "Win64", "SCUM.exe");
        var scumLauncherExe = Path.Combine(gameRoot, "SCUM_Launcher.exe");
        var paksPath = Path.Combine(gameRoot, "SCUM", "Content", "Paks");
        if (!Directory.Exists(paksPath))
        {
            return null;
        }

        var exePath = File.Exists(scumExe)
            ? scumExe
            : File.Exists(scumLauncherExe)
                ? scumLauncherExe
                : string.Empty;

        string? buildId = null;
        if (!string.IsNullOrWhiteSpace(manifestPath) && File.Exists(manifestPath))
        {
            buildId = ParseBuildId(manifestPath);
        }

        return new ScumInstallation(
            Path.GetFullPath(gameRoot),
            exePath,
            paksPath,
            manifestPath is not null && File.Exists(manifestPath) ? manifestPath : null,
            buildId);
    }

    private static string? ParseInstallDir(string manifestPath)
    {
        try
        {
            var content = File.ReadAllText(manifestPath);
            var match = Regex.Match(content, "\"installdir\"\\s+\"(?<dir>[^\"]+)\"", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["dir"].Value : null;
        }
        catch
        {
            return null;
        }
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
