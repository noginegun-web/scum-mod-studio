using System.Globalization;

namespace ScumPakWizard;

internal static class CliUi
{
    public static void PrintBanner(
        ScumInstallation scum,
        string unrealPakPath,
        IReadOnlyList<PresetDefinition> presets)
    {
        Console.WriteLine("SCUM Mod Factory (CLI)");
        Console.WriteLine("----------------------");
        Console.WriteLine($"SCUM: {scum.RootPath}");
        Console.WriteLine($"Paks: {scum.PaksPath}");
        if (!string.IsNullOrWhiteSpace(scum.BuildId))
        {
            Console.WriteLine($"BuildId: {scum.BuildId}");
        }

        Console.WriteLine($"UnrealPak: {unrealPakPath}");
        Console.WriteLine();
        Console.WriteLine("Проверенные пресеты:");
        for (var i = 0; i < presets.Count; i++)
        {
            var preset = presets[i];
            Console.WriteLine($"{i + 1}. {preset.DisplayName} [{preset.Id}]");
            Console.WriteLine($"   {preset.Description}");
            Console.WriteLine($"   Что меняет: {string.Join(", ", preset.EditableSurfaces)}");
        }
    }

    public static List<PresetDefinition> PromptPresetSelection(IReadOnlyList<PresetDefinition> presets)
    {
        Console.WriteLine();
        Console.Write("Выбери пресеты по номерам через запятую (например: 1,3,5): ");
        var raw = Console.ReadLine() ?? string.Empty;
        var selected = new List<PresetDefinition>();
        var seen = new HashSet<int>();

        foreach (var token in raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
            {
                continue;
            }

            var zeroBased = idx - 1;
            if (zeroBased < 0 || zeroBased >= presets.Count || !seen.Add(zeroBased))
            {
                continue;
            }

            selected.Add(presets[zeroBased]);
        }

        return selected;
    }

    public static string PromptOutputName()
    {
        var defaultName = $"pakchunk99-scum-mod-{DateTime.Now:yyyyMMdd-HHmmss}-WindowsNoEditor";
        Console.Write($"Имя выходного .pak [{defaultName}]: ");
        var input = (Console.ReadLine() ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(input) ? defaultName : PathUtil.SanitizeFileName(input);
    }

    public static bool PromptYesNo(string prompt, bool defaultValue)
    {
        Console.Write(prompt);
        var input = (Console.ReadLine() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        return input.StartsWith("y", StringComparison.OrdinalIgnoreCase)
            || input.StartsWith("д", StringComparison.OrdinalIgnoreCase);
    }
}
