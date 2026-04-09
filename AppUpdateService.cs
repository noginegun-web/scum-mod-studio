using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace ScumPakWizard;

internal sealed class AppUpdateService
{
    private readonly AppUpdateSettings _settings;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly UpdateManager? _manager;
    private readonly string[] _restartArgs;
    private readonly object _stateSync = new();
    private VelopackAsset? _availableAsset;
    private VelopackAsset? _pendingAsset;
    private bool _startupCheckQueued;
    private bool _isChecking;
    private bool _isDownloading;
    private bool _isInstalling;
    private int _downloadProgress;
    private string? _statusTitle;
    private string? _statusMessage;
    private string? _releaseNotesMarkdown;
    private string? _lastError;
    private DateTimeOffset? _lastCheckedUtc;

    private AppUpdateService(AppUpdateSettings settings, string[] restartArgs)
    {
        _settings = settings;
        _restartArgs = restartArgs;

        if (settings.Enabled && !string.IsNullOrWhiteSpace(settings.RepoUrl))
        {
            var source = new GithubSource(settings.RepoUrl, string.Empty, settings.AllowPrerelease, null);
            var options = new UpdateOptions
            {
                ExplicitChannel = string.IsNullOrWhiteSpace(settings.Channel) ? null : settings.Channel
            };

            _manager = new UpdateManager(source, options);
            RefreshPendingAsset();
        }
        else
        {
            _statusTitle = "Автообновление пока не настроено";
            _statusMessage = "Когда будет подключён GitHub-источник, программа сможет предлагать и ставить новые версии сама.";
        }
    }

    public static AppUpdateService Create(IConfiguration configuration, string[] args)
    {
        var settings = AppUpdateSettings.FromConfiguration(configuration);
        return new AppUpdateService(settings, args);
    }

    public AppUpdateStatusDto GetStatus()
    {
        lock (_stateSync)
        {
            var manager = _manager;
            var isInstalled = manager?.IsInstalled ?? false;
            var pendingAsset = _pendingAsset ?? manager?.UpdatePendingRestart;
            var availableAsset = pendingAsset is null ? _availableAsset : null;
            var currentVersion = manager?.CurrentVersion?.ToString();

            var statusTitle = _statusTitle;
            var statusMessage = _statusMessage;

            if (!string.IsNullOrWhiteSpace(_lastError))
            {
                statusTitle ??= "Не удалось проверить обновления";
                statusMessage ??= _lastError;
            }
            else if (pendingAsset is not null)
            {
                statusTitle ??= $"Обновление {pendingAsset.Version} готово";
                statusMessage ??= "Можно закрыть программу, установить новую версию и сразу вернуться в работу.";
            }
            else if (_isDownloading)
            {
                statusTitle ??= $"Скачивается обновление {availableAsset?.Version ?? _availableAsset?.Version}";
                statusMessage ??= "Файлы новой версии уже загружаются. После этого останется перезапустить программу.";
            }
            else if (availableAsset is not null)
            {
                statusTitle ??= $"Доступно обновление {availableAsset.Version}";
                statusMessage ??= "Новая версия уже найдена. Её можно скачать и установить прямо из программы.";
            }
            else if (_isChecking)
            {
                statusTitle ??= "Проверяем обновления";
                statusMessage ??= "Смотрим, появилась ли новая версия программы на GitHub.";
            }

            return new AppUpdateStatusDto(
                Enabled: _settings.Enabled,
                Configured: !string.IsNullOrWhiteSpace(_settings.RepoUrl),
                IsInstalled: isInstalled,
                IsPortable: manager?.IsPortable ?? false,
                IsChecking: _isChecking,
                IsDownloading: _isDownloading,
                IsInstalling: _isInstalling,
                PendingRestart: pendingAsset is not null,
                UpdateAvailable: availableAsset is not null,
                CanCheck: manager is not null && isInstalled && !_isChecking && !_isDownloading && !_isInstalling,
                CanDownload: manager is not null && isInstalled && availableAsset is not null && !_isDownloading && !_isInstalling,
                CanInstall: manager is not null && isInstalled && pendingAsset is not null && !_isInstalling,
                CurrentVersion: currentVersion,
                AvailableVersion: availableAsset?.Version?.ToString(),
                PendingVersion: pendingAsset?.Version?.ToString(),
                DownloadProgress: _downloadProgress,
                RepoUrl: _settings.RepoUrl,
                StatusTitle: statusTitle,
                StatusMessage: statusMessage,
                ReleaseNotesMarkdown: _releaseNotesMarkdown,
                LastError: _lastError,
                LastCheckedUtc: _lastCheckedUtc?.ToUniversalTime().ToString("O"));
        }
    }

    public void QueueStartupCheck()
    {
        if (_manager is null || !_settings.CheckOnStartup)
        {
            return;
        }

        lock (_stateSync)
        {
            if (_startupCheckQueued)
            {
                return;
            }

            _startupCheckQueued = true;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                if (_settings.StartupCheckDelaySeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_settings.StartupCheckDelaySeconds));
                }

                await CheckForUpdatesAsync();
            }
            catch
            {
                // ignore startup failures, status endpoint already stores details
            }
        });
    }

    public async Task<AppUpdateCommandResultDto> CheckForUpdatesAsync()
    {
        var manager = _manager;
        if (manager is null)
        {
            return Fail("Источник обновлений ещё не настроен.");
        }

        if (!manager.IsInstalled)
        {
            return Fail("Автообновление работает только у установленной версии программы, а не у обычного файла из папки сборки.");
        }

        await _gate.WaitAsync();
        try
        {
            SetBusy(checking: true, downloading: false, installing: false, progress: 0);
            SetStatus("Проверяем обновления", "Смотрим, появилась ли новая версия программы на GitHub.", clearError: true);

            RefreshPendingAsset();
            var update = await manager.CheckForUpdatesAsync();

            lock (_stateSync)
            {
                _availableAsset = update?.TargetFullRelease;
                _releaseNotesMarkdown = update?.TargetFullRelease?.NotesMarkdown;
                _lastCheckedUtc = DateTimeOffset.UtcNow;
                _lastError = null;

                if (_availableAsset is null)
                {
                    _statusTitle = "Установлена последняя версия";
                    _statusMessage = "Новых обновлений пока нет.";
                }
                else
                {
                    _statusTitle = $"Доступно обновление {_availableAsset.Version}";
                    _statusMessage = "Новая версия найдена и готова к скачиванию.";
                }
            }

            return new AppUpdateCommandResultDto(
                Ok: true,
                Message: update?.TargetFullRelease is null
                    ? "Новых обновлений пока нет."
                    : $"Найдена новая версия {update.TargetFullRelease.Version}.",
                Error: null);
        }
        catch (Exception ex) when (ex is NotInstalledException or HttpRequestException)
        {
            RememberError(ex);
            return Fail(ex.Message);
        }
        finally
        {
            SetBusy(checking: false, downloading: false, installing: false, progress: _downloadProgress);
            _gate.Release();
        }
    }

    public async Task<AppUpdateCommandResultDto> DownloadUpdateAsync()
    {
        var manager = _manager;
        if (manager is null)
        {
            return Fail("Источник обновлений ещё не настроен.");
        }

        if (!manager.IsInstalled)
        {
            return Fail("Скачивание обновлений работает только у установленной версии программы.");
        }

        await _gate.WaitAsync();
        try
        {
            var update = await manager.CheckForUpdatesAsync();
            if (update?.TargetFullRelease is null)
            {
                lock (_stateSync)
                {
                    _availableAsset = null;
                    _releaseNotesMarkdown = null;
                    _statusTitle = "Установлена последняя версия";
                    _statusMessage = "Новых обновлений пока нет.";
                    _lastCheckedUtc = DateTimeOffset.UtcNow;
                }

                return new AppUpdateCommandResultDto(true, "Новых обновлений пока нет.", null);
            }

            lock (_stateSync)
            {
                _availableAsset = update.TargetFullRelease;
                _releaseNotesMarkdown = update.TargetFullRelease.NotesMarkdown;
            }

            SetBusy(checking: false, downloading: true, installing: false, progress: 0);
            SetStatus($"Скачивается обновление {update.TargetFullRelease.Version}", "Загружаем новую версию программы.", clearError: true);

            await manager.DownloadUpdatesAsync(update, progress =>
            {
                lock (_stateSync)
                {
                    _downloadProgress = progress;
                    _statusTitle = $"Скачивается обновление {update.TargetFullRelease.Version}";
                    _statusMessage = $"Загрузка завершена на {progress}%.";
                }
            }, CancellationToken.None);

            RefreshPendingAsset();

            lock (_stateSync)
            {
                _lastCheckedUtc = DateTimeOffset.UtcNow;
                _statusTitle = $"Обновление {_pendingAsset?.Version ?? update.TargetFullRelease.Version} готово";
                _statusMessage = "Новая версия уже скачана. Осталось установить её и перезапустить программу.";
                _downloadProgress = 100;
                _lastError = null;
            }

            return new AppUpdateCommandResultDto(true, "Обновление скачано. Можно устанавливать.", null);
        }
        catch (Exception ex) when (ex is NotInstalledException or AcquireLockFailedException or HttpRequestException)
        {
            RememberError(ex);
            return Fail(ex.Message);
        }
        finally
        {
            SetBusy(checking: false, downloading: false, installing: false, progress: _downloadProgress);
            _gate.Release();
        }
    }

    public async Task<AppUpdateCommandResultDto> PrepareInstallAsync()
    {
        var manager = _manager;
        if (manager is null)
        {
            return Fail("Источник обновлений ещё не настроен.");
        }

        if (!manager.IsInstalled)
        {
            return Fail("Установка обновлений работает только у установленной версии программы.");
        }

        await _gate.WaitAsync();
        try
        {
            RefreshPendingAsset();
            var assetToApply = _pendingAsset ?? manager.UpdatePendingRestart;
            if (assetToApply is null)
            {
                return Fail("Сначала нужно скачать новую версию.");
            }

            SetBusy(checking: false, downloading: false, installing: true, progress: 100);
            SetStatus($"Устанавливаем обновление {assetToApply.Version}", "Программа сейчас закроется, поставит новую версию и запустится снова.", clearError: true);

            manager.WaitExitThenApplyUpdates(assetToApply, false, true, _restartArgs);

            return new AppUpdateCommandResultDto(
                Ok: true,
                Message: "Программа закрывается для установки обновления.",
                Error: null,
                ShouldShutdown: true);
        }
        catch (Exception ex) when (ex is NotInstalledException or AcquireLockFailedException or IOException)
        {
            RememberError(ex);
            return Fail(ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    public string RewritePublishedAppSettings(string originalJson, string repoUrl)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(originalJson) ? "{}" : originalJson);

        var root = document.RootElement.Clone();
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();

        var wroteAppUpdate = false;
        foreach (var property in root.EnumerateObject())
        {
            if (property.NameEquals("AppUpdate"))
            {
                WriteAppUpdateSection(writer, property.Value, repoUrl);
                wroteAppUpdate = true;
                continue;
            }

            property.WriteTo(writer);
        }

        if (!wroteAppUpdate)
        {
            WriteAppUpdateSection(writer, default, repoUrl);
        }

        writer.WriteEndObject();
        writer.Flush();

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private void WriteAppUpdateSection(Utf8JsonWriter writer, JsonElement source, string repoUrl)
    {
        writer.WritePropertyName("AppUpdate");
        writer.WriteStartObject();

        var wroteEnabled = false;
        var wroteRepoUrl = false;

        if (source.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in source.EnumerateObject())
            {
                if (property.NameEquals("Enabled"))
                {
                    writer.WriteBoolean("Enabled", true);
                    wroteEnabled = true;
                    continue;
                }

                if (property.NameEquals("RepoUrl"))
                {
                    writer.WriteString("RepoUrl", repoUrl);
                    wroteRepoUrl = true;
                    continue;
                }

                property.WriteTo(writer);
            }
        }

        if (!wroteEnabled)
        {
            writer.WriteBoolean("Enabled", true);
        }

        if (!wroteRepoUrl)
        {
            writer.WriteString("RepoUrl", repoUrl);
        }

        writer.WriteEndObject();
    }

    private void RefreshPendingAsset()
    {
        lock (_stateSync)
        {
            _pendingAsset = _manager?.UpdatePendingRestart;
        }
    }

    private void SetBusy(bool checking, bool downloading, bool installing, int progress)
    {
        lock (_stateSync)
        {
            _isChecking = checking;
            _isDownloading = downloading;
            _isInstalling = installing;
            _downloadProgress = progress;
        }
    }

    private void SetStatus(string title, string message, bool clearError)
    {
        lock (_stateSync)
        {
            _statusTitle = title;
            _statusMessage = message;
            if (clearError)
            {
                _lastError = null;
            }
        }
    }

    private void RememberError(Exception ex)
    {
        lock (_stateSync)
        {
            _lastError = ex.Message;
            _statusTitle = "Не удалось проверить обновления";
            _statusMessage = ex.Message;
            _lastCheckedUtc = DateTimeOffset.UtcNow;
        }
    }

    private AppUpdateCommandResultDto Fail(string error) =>
        new(false, null, error);
}

internal sealed record AppUpdateSettings(
    bool Enabled,
    string RepoUrl,
    string Channel,
    bool AllowPrerelease,
    bool CheckOnStartup,
    int StartupCheckDelaySeconds)
{
    public static AppUpdateSettings FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("AppUpdate");
        var repoUrl = Environment.GetEnvironmentVariable("SCUM_STUDIO_UPDATE_REPO_URL")
            ?? section["RepoUrl"]
            ?? string.Empty;

        var channel = Environment.GetEnvironmentVariable("SCUM_STUDIO_UPDATE_CHANNEL")
            ?? section["Channel"]
            ?? "win";

        return new AppUpdateSettings(
            Enabled: ReadBool(section["Enabled"], true),
            RepoUrl: repoUrl.Trim(),
            Channel: channel.Trim(),
            AllowPrerelease: ReadBool(section["AllowPrerelease"], false),
            CheckOnStartup: ReadBool(section["CheckOnStartup"], true),
            StartupCheckDelaySeconds: ReadInt(section["StartupCheckDelaySeconds"], 2));
    }

    private static bool ReadBool(string? value, bool fallback) =>
        bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static int ReadInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed >= 0 ? parsed : fallback;
}
