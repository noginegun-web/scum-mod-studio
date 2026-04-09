namespace ScumPakWizard;

internal static class UnrealPakLocator
{
    public static string? Locate(string appRoot)
    {
        var envPath = Environment.GetEnvironmentVariable("UNREALPAK_PATH");
        if (IsValidUnrealPak(envPath))
        {
            return Path.GetFullPath(envPath!);
        }

        var bundledCandidates = new[]
        {
            Path.Combine(appRoot, "Engine", "Binaries", "Win64", "UnrealPak.exe"),
            Path.Combine(appRoot, "tools", "UnrealPak", "UnrealPak.exe"),
            Path.Combine(appRoot, "tools", "UnrealPak.exe"),
            Path.Combine(appRoot, "UnrealPak", "UnrealPak.exe"),
            Path.Combine(appRoot, "UnrealPak.exe")
        };

        foreach (var candidate in bundledCandidates)
        {
            if (IsValidUnrealPak(candidate))
            {
                return candidate;
            }
        }

        var epicRoot = @"C:\Program Files\Epic Games";
        if (Directory.Exists(epicRoot))
        {
            var ueDirs = Directory.GetDirectories(epicRoot, "UE_*")
                .OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase);

            foreach (var ueDir in ueDirs)
            {
                var candidate = Path.Combine(ueDir, "Engine", "Binaries", "Win64", "UnrealPak.exe");
                if (IsValidUnrealPak(candidate))
                {
                    return candidate;
                }
            }
        }

        var fallback = @"C:\Program Files\Epic Games\UE_4.27\Engine\Binaries\Win64\UnrealPak.exe";
        return IsValidUnrealPak(fallback) ? fallback : null;
    }

    private static bool IsValidUnrealPak(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }
}
