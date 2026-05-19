using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Velopack;

namespace ScumPakWizard;

internal static class Program
{
    private const int StudioPort = 49321;

    private static int Main(string[] args)
    {
        if (TryRunReplacementContract(args, out var replacementContractExitCode))
        {
            return replacementContractExitCode;
        }

        if (TryRunWeaponContract(args, out var contractExitCode))
        {
            return contractExitCode;
        }

        if (TryRunFieldDiscovery(args, out var discoveryExitCode))
        {
            return discoveryExitCode;
        }

        try
        {
            VelopackApp
                .Build()
                .SetArgs(args)
                .SetAutoApplyOnStartup(false)
                .Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Предупреждение Velopack:");
            Console.WriteLine(ex.Message);
        }

        return MainCoreAsync(args).GetAwaiter().GetResult();
    }

    private static async Task<int> MainCoreAsync(string[] args)
    {
        var openBrowser = !args.Any(x => x.Equals("--no-browser", StringComparison.OrdinalIgnoreCase));
        var diagnosticsEnabled = IsDiagnosticsEnabled(args);
        var vehicleAdapterEnabled = IsVehicleAdapterEnabled(args);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });
        var appUpdate = AppUpdateService.Create(builder.Configuration, args);

        StudioRuntime studio;
        try
        {
            studio = StudioRuntime.Create();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка инициализации студии модов:");
            Console.WriteLine(ex.Message);
            return 2;
        }

        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 2L * 1024 * 1024 * 1024;
            options.ValueLengthLimit = int.MaxValue;
            options.MultipartHeadersLengthLimit = int.MaxValue;
        });
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenLocalhost(StudioPort);
            options.Limits.MaxRequestBodySize = 2L * 1024 * 1024 * 1024;
        });
        var app = builder.Build();

        var uiRoot = Path.Combine(AppContext.BaseDirectory, "ui");
        if (Directory.Exists(uiRoot))
        {
            var fileProvider = new PhysicalFileProvider(uiRoot);
            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = fileProvider,
                RequestPath = string.Empty
            });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = fileProvider,
                ContentTypeProvider = new FileExtensionContentTypeProvider()
            });
        }

        app.MapGet("/api/status", () => Results.Ok(studio.GetStatus()));
        app.MapPost("/api/toolchain/open-tools-folder", () =>
        {
            studio.OpenToolsFolder();
            return Results.Ok(new { ok = true });
        });
        app.MapGet("/api/assets", (string? search, string? presetId, string? scope, int? page, int? pageSize) =>
            Results.Ok(studio.GetAssets(search, presetId, scope, page ?? 1, pageSize ?? 120)));
        app.MapGet("/api/modding/categories", () =>
            Results.Ok(studio.GetModdingCategories()));
        app.MapGet("/api/modding/assets", (string? categoryId, string? search, int? page, int? pageSize) =>
            Results.Ok(studio.GetModdingAssets(categoryId, search, page ?? 1, pageSize ?? 120)));
        app.MapGet("/api/modding/schema", (string assetId) =>
            Results.Ok(studio.GetModdingAssetSchema(assetId)));
        app.MapGet("/api/modding/replacement-contract", (string assetId) =>
            Results.Ok(studio.GetReplacementContract(assetId)));
        app.MapGet("/api/modding/weapon-contract", (string assetId) =>
            Results.Ok(studio.GetWeaponContract(assetId)));
        app.MapGet("/api/modding/vehicle-profile", (string assetId) =>
            vehicleAdapterEnabled
                ? Results.Ok(studio.GetVehicleProfile(assetId))
                : Results.NotFound());
        app.MapGet("/api/modding/vehicle-module-plan", (string assetId, string rawSourceRelativePath) =>
            vehicleAdapterEnabled
                ? Results.Ok(studio.GetVehicleModulePlan(assetId, rawSourceRelativePath))
                : Results.NotFound());
        app.MapPost("/api/modding/vehicle-module-cook", (StudioVehicleModuleCookRequestDto request) =>
            vehicleAdapterEnabled
                ? Results.Ok(studio.CookVehicleModuleRawModel(request))
                : Results.NotFound());
        app.MapPost("/api/modding/vehicle-module-cook-batch", (StudioVehicleModuleCookBatchRequestDto request) =>
            vehicleAdapterEnabled
                ? Results.Ok(studio.CookVehicleModulePlanRawModels(request))
                : Results.NotFound());
        app.MapPost("/api/modding/vehicle-full-replacement", (StudioVehicleFullReplacementRequestDto request) =>
            vehicleAdapterEnabled
                ? Results.Ok(studio.CookAndBuildVehicleFullReplacement(request))
                : Results.NotFound());
        app.MapGet("/api/modding/armor-set-plan", (string rawSourceRelativePath) =>
            Results.Ok(studio.GetArmorSetPlan(rawSourceRelativePath)));
        app.MapPost("/api/modding/armor-set-cook", (StudioArmorSetCookRequestDto request) =>
            Results.Ok(studio.CookArmorSetRawModel(request)));
        app.MapPost("/api/modding/armor-set-cook-batch", (StudioArmorSetCookBatchRequestDto request) =>
            Results.Ok(studio.CookArmorSetPlanRawModels(request)));
        app.MapPost("/api/modding/schema-preview", (StudioSchemaPreviewRequestDto request) =>
            Results.Ok(studio.PreviewModdingAssetSchema(request)));
        app.MapGet("/api/modding/reference-options", (string pickerKind, string? term, int? limit) =>
            Results.Ok(studio.GetModdingReferenceOptions(pickerKind, term, limit ?? 24)));
        app.MapGet("/api/custom-visual-assets", (string? kind) =>
            Results.Ok(studio.GetCustomVisualAssets(kind)));
        app.MapPost("/api/custom-visual-assets/import", async (HttpRequest request) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new StudioCustomVisualImportResultDto(
                    false,
                    "Нужна multipart form-data форма с cooked UE-файлами или raw-моделью.",
                    0,
                    [],
                    []));
            }

            var form = await request.ReadFormAsync();
            return Results.Ok(await studio.ImportCustomVisualAssetsAsync(form.Files));
        });
        app.MapPost("/api/custom-visual-assets/cook-raw", (StudioRawModelCookRequestDto request) =>
            Results.Ok(studio.CookRawModelAsset(request)));
        app.MapGet("/api/research/mod-pattern", (string assetPath, bool? includeImportDiff, int? maxItems) =>
            Results.Ok(studio.InspectResearchModPattern(assetPath, includeImportDiff ?? true, maxItems ?? 12)));
        if (diagnosticsEnabled)
        {
            app.MapGet("/api/dev/field-candidates", (string assetId, int? limit, bool? hiddenOnly) =>
                Results.Ok(studio.GetFieldDiscoveryReport(assetId, limit ?? 600, hiddenOnly ?? false)));
        }

        app.MapGet("/api/catalog", () => Results.Ok(studio.GetItemCatalog()));
        app.MapGet("/api/catalog/search", async (string? term, int? limit) =>
            Results.Ok(await studio.SearchItemCatalogAsync(term, limit ?? 160)));
        app.MapGet("/api/icon", (string itemId) =>
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return Results.BadRequest(new { error = "itemId is required" });
            }

            return studio.TryGetItemIcon(itemId, out var pngBytes)
                ? Results.File(pngBytes, "image/png")
                : Results.NotFound();
        });
        app.MapPost("/api/build", (StudioBuildRequestDto request) =>
            Results.Ok(studio.Build(request)));
        app.MapGet("/api/app-update/status", () =>
            Results.Ok(appUpdate.GetStatus()));
        app.MapPost("/api/app-update/check", async () =>
            Results.Ok(await appUpdate.CheckForUpdatesAsync()));
        app.MapPost("/api/app-update/download", async () =>
            Results.Ok(await appUpdate.DownloadUpdateAsync()));
        app.MapPost("/api/app-update/install", async (IHostApplicationLifetime lifetime) =>
        {
            var result = await appUpdate.PrepareInstallAsync();
            if (result.ShouldShutdown)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(650);
                    lifetime.StopApplication();
                });
            }

            return Results.Ok(result);
        });

        app.MapGet("/health", () => Results.Ok(new { ok = true }));

        await app.StartAsync();
        appUpdate.QueueStartupCheck();
        var url = $"http://127.0.0.1:{StudioPort}";

        Console.WriteLine("SCUM Mod Studio запущена.");
        Console.WriteLine($"Открой в браузере: {url}");
        Console.WriteLine("Для остановки нажми Ctrl+C.");

        if (openBrowser)
        {
            TryOpenBrowser(url);
        }

        await app.WaitForShutdownAsync();
        return 0;
    }

    private static void TryOpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // ignore
        }
    }

    private static bool IsDiagnosticsEnabled(string[] args)
    {
        if (args.Any(x => x.Equals("--diagnostics", StringComparison.OrdinalIgnoreCase)
            || x.Equals("--dev-diagnostics", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var value = Environment.GetEnvironmentVariable("SCUM_STUDIO_DIAGNOSTICS")
                    ?? Environment.GetEnvironmentVariable("SCUM_STUDIO_DEV_DIAGNOSTICS");
        return value is not null
            && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsVehicleAdapterEnabled(string[] args)
    {
        if (args.Any(x => x.Equals("--enable-vehicle-adapter", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var value = Environment.GetEnvironmentVariable("SCUM_MOD_STUDIO_ENABLE_VEHICLE_ADAPTER");
        return value is not null
            && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryRunWeaponContract(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (!TryGetOptionValue(args, "--weapon-contract", out var assetId)
            && !TryGetOptionValue(args, "--scan-weapon-contract", out assetId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(assetId))
        {
            Console.Error.WriteLine("Для --weapon-contract нужен assetId, например game::scum/content/conz_files/items/weapons/...");
            exitCode = 2;
            return true;
        }

        try
        {
            var studio = StudioRuntime.Create();
            var report = studio.GetWeaponContract(assetId);
            var json = JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            if (TryGetOptionValue(args, "--output", out var outputPath)
                && !string.IsNullOrWhiteSpace(outputPath))
            {
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(outputPath, json);
            }
            else
            {
                Console.WriteLine(json);
            }

            exitCode = report.Ok ? 0 : 2;
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Ошибка scanner-контракта оружия:");
            Console.Error.WriteLine(ex.Message);
            exitCode = 2;
            return true;
        }
    }

    private static bool TryRunReplacementContract(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (!TryGetOptionValue(args, "--replacement-contract", out var assetId)
            && !TryGetOptionValue(args, "--scan-replacement-contract", out assetId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(assetId))
        {
            Console.Error.WriteLine("Для --replacement-contract нужен assetId, например game::scum/content/...");
            exitCode = 2;
            return true;
        }

        try
        {
            var studio = StudioRuntime.Create();
            var report = studio.GetReplacementContract(assetId);
            var json = JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            if (TryGetOptionValue(args, "--output", out var outputPath)
                && !string.IsNullOrWhiteSpace(outputPath))
            {
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(outputPath, json);
            }
            else
            {
                Console.WriteLine(json);
            }

            exitCode = report.Ok ? 0 : 2;
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Ошибка scanner-контракта замены модели:");
            Console.Error.WriteLine(ex.Message);
            exitCode = 2;
            return true;
        }
    }

    private static bool TryRunFieldDiscovery(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (!TryGetOptionValue(args, "--field-discovery", out var assetId)
            && !TryGetOptionValue(args, "--discover-fields", out assetId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(assetId))
        {
            Console.Error.WriteLine("Для --field-discovery нужен assetId, например game::scum/content/...");
            exitCode = 2;
            return true;
        }

        var limit = 600;
        if (TryGetOptionValue(args, "--limit", out var limitText)
            && int.TryParse(limitText, out var parsedLimit))
        {
            limit = parsedLimit;
        }

        var hiddenOnly = HasFlag(args, "--hidden-only");
        try
        {
            var studio = StudioRuntime.Create();
            var report = studio.GetFieldDiscoveryReport(assetId, limit, hiddenOnly);
            var json = JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            if (TryGetOptionValue(args, "--output", out var outputPath)
                && !string.IsNullOrWhiteSpace(outputPath))
            {
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(outputPath, json);
            }
            else
            {
                Console.WriteLine(json);
            }

            exitCode = report.Warnings.Any(warning => warning.Contains("Не удалось", StringComparison.OrdinalIgnoreCase))
                ? 2
                : 0;
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Ошибка диагностики полей:");
            Console.Error.WriteLine(ex.Message);
            exitCode = 2;
            return true;
        }
    }

    private static bool TryGetOptionValue(string[] args, string optionName, out string? value)
    {
        value = null;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals(optionName, StringComparison.OrdinalIgnoreCase))
            {
                value = i + 1 < args.Length ? args[i + 1] : null;
                return true;
            }

            var prefix = optionName + "=";
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = arg[prefix.Length..];
                return true;
            }
        }

        return false;
    }

    private static bool HasFlag(string[] args, string flagName)
    {
        return args.Any(arg => arg.Equals(flagName, StringComparison.OrdinalIgnoreCase));
    }
}
