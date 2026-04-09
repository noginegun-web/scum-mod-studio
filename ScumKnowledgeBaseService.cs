using System.Net;
using System.Text.Json;

namespace ScumPakWizard;

internal sealed class ScumKnowledgeBaseService : IDisposable
{
    private const string ApiBase = "https://scum-db.com";
    private readonly HttpClient _httpClient;
    private readonly object _sync = new();
    private readonly Dictionary<string, CacheEntry> _searchCache = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _blockedUntilUtc = DateTimeOffset.MinValue;

    public ScumKnowledgeBaseService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ScumPakWizard/1.0");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public async Task<List<StudioItemDto>> SearchItemsAsync(
        string? term,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedTerm = (term ?? string.Empty).Trim();
        limit = Math.Clamp(limit, 20, 400);
        var cacheKey = $"{limit}|{normalizedTerm}";

        if (TryGetCache(cacheKey, out var cached))
        {
            return cached;
        }

        if (DateTimeOffset.UtcNow < _blockedUntilUtc)
        {
            return [];
        }

        var requestUrl = $"{ApiBase}/api/search?query={Uri.EscapeDataString(normalizedTerm)}&limit={limit}&offset=0&_t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            {
                BlockFor(TimeSpan.FromMinutes(1));
                return [];
            }

            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!document.RootElement.TryGetProperty("items", out var itemsElement)
                || itemsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var parsed = ParseItems(itemsElement, limit);
            SetCache(cacheKey, parsed);
            return parsed;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            BlockFor(TimeSpan.FromSeconds(20));
            return [];
        }
        catch
        {
            BlockFor(TimeSpan.FromMinutes(1));
            return [];
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static List<StudioItemDto> ParseItems(JsonElement itemsElement, int limit)
    {
        var result = new List<StudioItemDto>(Math.Min(limit, itemsElement.GetArrayLength()));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in itemsElement.EnumerateArray())
        {
            var itemId = ReadString(item, "game_id");
            if (string.IsNullOrWhiteSpace(itemId) || !seen.Add(itemId))
            {
                continue;
            }

            var itemName = ReadString(item, "name");
            if (string.IsNullOrWhiteSpace(itemName))
            {
                itemName = itemId;
            }

            var fileDirectory = ReadString(item, "file_directory");
            var iconHash = FirstNonEmpty(
                ReadString(item, "vicinity_icon_hash"),
                ReadString(item, "inventory_icon_hash"),
                ReadString(item, "inhands_icon_hash"),
                ReadString(item, "trader_icon_hash"),
                ReadString(item, "icon_path_hash"));

            var iconUrl = BuildIconUrl(iconHash);
            var relativePath = string.IsNullOrWhiteSpace(fileDirectory)
                ? $"kb/{itemId}"
                : $"kb/{fileDirectory}/{itemId}";

            result.Add(new StudioItemDto(itemId, itemName, relativePath, iconUrl));
            if (result.Count >= limit)
            {
                break;
            }
        }

        return result;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? BuildIconUrl(string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return null;
        }

        return $"{ApiBase}/textures/hash/{Uri.EscapeDataString(hash)}";
    }

    private bool TryGetCache(string key, out List<StudioItemDto> items)
    {
        lock (_sync)
        {
            if (_searchCache.TryGetValue(key, out var entry) && entry.ExpiresAtUtc > DateTimeOffset.UtcNow)
            {
                items = entry.Items;
                return true;
            }
        }

        items = [];
        return false;
    }

    private void SetCache(string key, List<StudioItemDto> items)
    {
        lock (_sync)
        {
            _searchCache[key] = new CacheEntry(DateTimeOffset.UtcNow.AddMinutes(3), items);
        }
    }

    private void BlockFor(TimeSpan period)
    {
        lock (_sync)
        {
            var until = DateTimeOffset.UtcNow.Add(period);
            if (until > _blockedUntilUtc)
            {
                _blockedUntilUtc = until;
            }
        }
    }

    private sealed record CacheEntry(DateTimeOffset ExpiresAtUtc, List<StudioItemDto> Items);
}
