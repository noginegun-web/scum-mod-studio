using System.Diagnostics;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Velopack;

namespace ScumPakWizard;

internal static class Program
{
    private const int StudioPort = 49321;

    private static int Main(string[] args)
    {
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

        builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(StudioPort));
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
        app.MapGet("/api/assets", (string? search, string? presetId, string? scope, int? page, int? pageSize) =>
            Results.Ok(studio.GetAssets(search, presetId, scope, page ?? 1, pageSize ?? 120)));
        app.MapGet("/api/modding/categories", () =>
            Results.Ok(studio.GetModdingCategories()));
        app.MapGet("/api/modding/assets", (string? categoryId, string? search, int? page, int? pageSize) =>
            Results.Ok(studio.GetModdingAssets(categoryId, search, page ?? 1, pageSize ?? 120)));
        app.MapGet("/api/modding/schema", (string assetId) =>
            Results.Ok(studio.GetModdingAssetSchema(assetId)));
        app.MapPost("/api/modding/schema-preview", (StudioSchemaPreviewRequestDto request) =>
            Results.Ok(studio.PreviewModdingAssetSchema(request)));
        app.MapGet("/api/modding/reference-options", (string pickerKind, string? term, int? limit) =>
            Results.Ok(studio.GetModdingReferenceOptions(pickerKind, term, limit ?? 24)));
        app.MapGet("/api/research/mod-pattern", (string assetPath, bool? includeImportDiff, int? maxItems) =>
            Results.Ok(studio.InspectResearchModPattern(assetPath, includeImportDiff ?? true, maxItems ?? 12)));
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
}
