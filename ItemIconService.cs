using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using UAssetAPI;
using UAssetAPI.UnrealTypes;

namespace ScumPakWizard;

internal sealed class ItemIconService
{
    private const string RelativeConzPrefix = "scum/content/conz_files/";

    private readonly object _sync = new();
    private readonly RuntimePaths _runtimePaths;
    private readonly ScumInstallation _scum;
    private readonly string _unrealPakPath;
    private readonly string _aesKeyHex;
    private readonly string _iconCacheRoot;
    private readonly string _extractRoot;

    private readonly HashSet<string> _extractJobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _iconByItemId = new(StringComparer.OrdinalIgnoreCase);

    private PakIndex? _pakIndex;
    private string? _cryptoPath;

    public ItemIconService(
        RuntimePaths runtimePaths,
        ScumInstallation scum,
        string unrealPakPath,
        string aesKeyHex)
    {
        _runtimePaths = runtimePaths;
        _scum = scum;
        _unrealPakPath = unrealPakPath;
        _aesKeyHex = aesKeyHex;
        _iconCacheRoot = Path.Combine(runtimePaths.TempRoot, "icon-cache");
        _extractRoot = Path.Combine(runtimePaths.TempRoot, "icon-extract");
    }

    public void RebuildMappings(IReadOnlyList<StudioItemDto> items, PakIndex pakIndex)
    {
        var iconPaths = pakIndex.GetAllRelativePaths()
            .Where(IsIconAssetPath)
            .ToList();

        var byFileName = iconPaths
            .GroupBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key.ToLowerInvariant(), group => group.First(), StringComparer.OrdinalIgnoreCase);

        var byNormalized = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var byDirectory = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var iconPath in iconPaths)
        {
            var normKey = NormalizeComparable(Path.GetFileNameWithoutExtension(iconPath));
            if (!string.IsNullOrWhiteSpace(normKey))
            {
                if (!byNormalized.TryGetValue(normKey, out var list))
                {
                    list = [];
                    byNormalized[normKey] = list;
                }

                list.Add(iconPath);
            }

            var directory = GetDirectoryLower(iconPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            if (!byDirectory.TryGetValue(directory, out var dirList))
            {
                dirList = [];
                byDirectory[directory] = dirList;
            }

            dirList.Add(iconPath);
        }

        var nextMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var match = ResolveIconForItem(item, byFileName, byNormalized, byDirectory);
            if (!string.IsNullOrWhiteSpace(match))
            {
                nextMap[item.ItemId] = match;
            }
        }

        lock (_sync)
        {
            _pakIndex = pakIndex;
            _iconByItemId.Clear();
            foreach (var pair in nextMap)
            {
                _iconByItemId[pair.Key] = pair.Value;
            }
        }
    }

    public bool HasIconForItem(string itemId)
    {
        lock (_sync)
        {
            return _iconByItemId.ContainsKey(itemId);
        }
    }

    public bool TryGetIconPng(string itemId, out byte[] pngBytes)
    {
        pngBytes = [];
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        string iconRelativePath;
        var cachePath = Path.Combine(_iconCacheRoot, $"{NormalizeCacheFileName(itemId)}.png");
        lock (_sync)
        {
            if (!_iconByItemId.TryGetValue(itemId, out iconRelativePath!))
            {
                return false;
            }
        }

        if (File.Exists(cachePath))
        {
            pngBytes = File.ReadAllBytes(cachePath);
            return pngBytes.Length > 0;
        }

        if (!TryResolveIconUasset(iconRelativePath, out var sourceUassetPath))
        {
            return false;
        }

        if (!TryDecodeIconPng(sourceUassetPath, out pngBytes))
        {
            return false;
        }

        Directory.CreateDirectory(_iconCacheRoot);
        File.WriteAllBytes(cachePath, pngBytes);
        return true;
    }

    private string? ResolveIconForItem(
        StudioItemDto item,
        IReadOnlyDictionary<string, string> byFileName,
        IReadOnlyDictionary<string, List<string>> byNormalized,
        IReadOnlyDictionary<string, List<string>> byDirectory)
    {
        var aliases = GenerateAliasCandidates(item.ItemId).ToList();
        foreach (var alias in aliases)
        {
            foreach (var suffix in new[] { "", "_inventory", "_inv", "_inhands", "_vicinity" })
            {
                var candidateFile = $"ico_{alias}{suffix}.uasset";
                if (byFileName.TryGetValue(candidateFile, out var path))
                {
                    return path;
                }
            }
        }

        var itemDirectory = GetDirectoryLower(item.RelativePath);
        if (!string.IsNullOrWhiteSpace(itemDirectory) && byDirectory.TryGetValue(itemDirectory, out var sameDirectoryIcons))
        {
            var local = PickBestIcon(sameDirectoryIcons, aliases);
            if (!string.IsNullOrWhiteSpace(local))
            {
                return local;
            }
        }

        foreach (var alias in aliases)
        {
            var key = NormalizeComparable(alias);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (byNormalized.TryGetValue(key, out var matches) && matches.Count > 0)
            {
                return PickPreferredIcon(matches);
            }
        }

        return null;
    }

    private static string PickPreferredIcon(IReadOnlyList<string> matches)
    {
        return matches
            .OrderByDescending(path => path.Contains("/item_icons/", StringComparison.OrdinalIgnoreCase))
            .ThenBy(path => Path.GetFileName(path).Length)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static string? PickBestIcon(IReadOnlyList<string> candidates, IReadOnlyList<string> aliases)
    {
        var bestScore = 0;
        string? bestPath = null;
        var normalizedAliases = aliases
            .Select(NormalizeComparable)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in candidates)
        {
            var iconName = Path.GetFileNameWithoutExtension(path);
            var normIcon = NormalizeComparable(iconName);
            var score = 0;

            foreach (var alias in normalizedAliases)
            {
                if (normIcon.Equals(alias, StringComparison.OrdinalIgnoreCase))
                {
                    score += 100;
                    break;
                }

                if (normIcon.Contains(alias, StringComparison.OrdinalIgnoreCase))
                {
                    score += 60;
                }
                else if (alias.Contains(normIcon, StringComparison.OrdinalIgnoreCase))
                {
                    score += 40;
                }
            }

            if (iconName.Contains("_inventory", StringComparison.OrdinalIgnoreCase))
            {
                score += 8;
            }
            else if (iconName.Contains("_inv", StringComparison.OrdinalIgnoreCase))
            {
                score += 5;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestPath = path;
            }
        }

        return bestScore > 0 ? bestPath : null;
    }

    private bool TryResolveIconUasset(string iconRelativePath, out string sourceUassetPath)
    {
        var normalized = PathUtil.NormalizeRelative(iconRelativePath);

        if (TryResolveFromAnalysisMirror(normalized, out sourceUassetPath))
        {
            return true;
        }

        if (TryResolveFromExtractRoot(normalized, out sourceUassetPath))
        {
            return true;
        }

        if (!TryExtractFromPak(normalized))
        {
            sourceUassetPath = string.Empty;
            return false;
        }

        return TryResolveFromExtractRoot(normalized, out sourceUassetPath);
    }

    private bool TryResolveFromAnalysisMirror(string normalizedRelativePath, out string sourceUassetPath)
    {
        sourceUassetPath = string.Empty;
        if (!normalizedRelativePath.StartsWith(RelativeConzPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var conzRelative = normalizedRelativePath[RelativeConzPrefix.Length..];
        var allMirror = Path.Combine(
            _runtimePaths.WorkspaceRoot,
            "analysis",
            "uasset_extract_all",
            "SCUM",
            "Content",
            "ConZ_Files",
            conzRelative.Replace('/', Path.DirectorySeparatorChar));

        if (File.Exists(allMirror))
        {
            sourceUassetPath = allMirror;
            return true;
        }

        var compactMirror = Path.Combine(
            _runtimePaths.WorkspaceRoot,
            "analysis",
            "uasset_extract",
            conzRelative.Replace('/', Path.DirectorySeparatorChar));

        if (File.Exists(compactMirror))
        {
            sourceUassetPath = compactMirror;
            return true;
        }

        return false;
    }

    private bool TryResolveFromExtractRoot(string normalizedRelativePath, out string sourceUassetPath)
    {
        sourceUassetPath = Path.Combine(_extractRoot, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(sourceUassetPath))
        {
            return true;
        }

        sourceUassetPath = TryFindExtractedMatch(_extractRoot, normalizedRelativePath) ?? string.Empty;
        return File.Exists(sourceUassetPath);
    }

    private bool TryExtractFromPak(string normalizedRelativePath)
    {
        PakIndex? index;
        lock (_sync)
        {
            index = _pakIndex;
        }

        if (index is null || !index.TryGetPakFor(normalizedRelativePath, out var pakPath))
        {
            return false;
        }

        var fileName = Path.GetFileName(normalizedRelativePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var cryptoPath = GetOrCreateCryptoPath();
        var extractedAsset = ExtractSingleFileFromPak(pakPath, fileName, cryptoPath);
        var companionName = $"{Path.GetFileNameWithoutExtension(fileName)}.uexp";
        ExtractSingleFileFromPak(pakPath, companionName, cryptoPath);
        return extractedAsset;
    }

    private bool ExtractSingleFileFromPak(string pakPath, string fileName, string cryptoPath)
    {
        var jobKey = $"{pakPath}|{fileName}".ToLowerInvariant();
        lock (_sync)
        {
            if (_extractJobs.Contains(jobKey))
            {
                return true;
            }
        }

        Directory.CreateDirectory(_extractRoot);
        var result = ProcessRunner.Run(
            _unrealPakPath,
            $"\"{pakPath}\" -Extract \"{_extractRoot}\" -extracttomountpoint -Filter=\"*{fileName}\" -cryptokeys=\"{cryptoPath}\"",
            _runtimePaths.WorkspaceRoot,
            _ => { },
            timeoutMs: 8 * 60 * 1000);

        lock (_sync)
        {
            if (result.ExitCode == 0)
            {
                _extractJobs.Add(jobKey);
                return true;
            }
        }

        return false;
    }

    private string GetOrCreateCryptoPath()
    {
        lock (_sync)
        {
            if (!string.IsNullOrWhiteSpace(_cryptoPath) && File.Exists(_cryptoPath))
            {
                return _cryptoPath;
            }

            var cryptoRoot = Path.Combine(_runtimePaths.TempRoot, "icon-crypto");
            Directory.CreateDirectory(cryptoRoot);
            _cryptoPath = CryptoKeyWriter.Write(cryptoRoot, _aesKeyHex);
            return _cryptoPath;
        }
    }

    private static bool TryDecodeIconPng(string sourceUassetPath, out byte[] pngBytes)
    {
        pngBytes = [];
        try
        {
            var asset = new UAsset(sourceUassetPath, EngineVersion.VER_UE4_27, null, CustomSerializationFlags.None);
            if (asset.Exports.Count == 0)
            {
                return false;
            }

            var extras = asset.Exports[0].Extras;
            if (extras is null || extras.Length < 64)
            {
                return false;
            }

            var width = BitConverter.ToInt32(extras, 24);
            var height = BitConverter.ToInt32(extras, 28);
            if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
            {
                return false;
            }

            var pixelFormat = ReadPixelFormat(extras);
            byte[] bgra;
            switch (pixelFormat)
            {
                case "PF_B8G8R8A8":
                {
                    var dataLength = checked(width * height * 4);
                    var offset = extras.Length - dataLength;
                    if (offset < 0)
                    {
                        return false;
                    }

                    bgra = new byte[dataLength];
                    Buffer.BlockCopy(extras, offset, bgra, 0, dataLength);
                    break;
                }
                case "PF_DXT5":
                {
                    var blockWidth = (width + 3) / 4;
                    var blockHeight = (height + 3) / 4;
                    var compressedLength = checked(blockWidth * blockHeight * 16);
                    var offset = extras.Length - compressedLength;
                    if (offset < 0)
                    {
                        return false;
                    }

                    bgra = DecodeDxt5ToBgra(extras.AsSpan(offset, compressedLength), width, height);
                    break;
                }
                default:
                    return false;
            }

            pngBytes = EncodePng(width, height, bgra);
            return pngBytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] EncodePng(int width, int height, byte[] bgra)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, width, height);
        var bmpData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var rowBytes = width * 4;
            for (var y = 0; y < height; y++)
            {
                var srcOffset = y * rowBytes;
                var dstPtr = IntPtr.Add(bmpData.Scan0, y * bmpData.Stride);
                Marshal.Copy(bgra, srcOffset, dstPtr, rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static byte[] DecodeDxt5ToBgra(ReadOnlySpan<byte> source, int width, int height)
    {
        var output = new byte[width * height * 4];
        var blockWidth = (width + 3) / 4;
        var blockHeight = (height + 3) / 4;

        for (var by = 0; by < blockHeight; by++)
        {
            for (var bx = 0; bx < blockWidth; bx++)
            {
                var blockIndex = ((by * blockWidth) + bx) * 16;
                var block = source.Slice(blockIndex, 16);

                var a0 = block[0];
                var a1 = block[1];

                ulong alphaBits = 0;
                for (var i = 0; i < 6; i++)
                {
                    alphaBits |= ((ulong)block[2 + i]) << (8 * i);
                }

                var c0 = BinaryPrimitives.ReadUInt16LittleEndian(block.Slice(8, 2));
                var c1 = BinaryPrimitives.ReadUInt16LittleEndian(block.Slice(10, 2));
                var colorBits = BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(12, 4));

                var alphaPalette = BuildDxt5AlphaPalette(a0, a1);
                var colorPalette = BuildDxtColorPalette(c0, c1);

                for (var py = 0; py < 4; py++)
                {
                    var y = (by * 4) + py;
                    if (y >= height)
                    {
                        continue;
                    }

                    for (var px = 0; px < 4; px++)
                    {
                        var x = (bx * 4) + px;
                        if (x >= width)
                        {
                            continue;
                        }

                        var pixel = (py * 4) + px;
                        var alphaIndex = (int)((alphaBits >> (3 * pixel)) & 0x7);
                        var colorIndex = (int)((colorBits >> (2 * pixel)) & 0x3);

                        var dst = ((y * width) + x) * 4;
                        var color = colorPalette[colorIndex];
                        output[dst] = color.B;
                        output[dst + 1] = color.G;
                        output[dst + 2] = color.R;
                        output[dst + 3] = alphaPalette[alphaIndex];
                    }
                }
            }
        }

        return output;
    }

    private static byte[] BuildDxt5AlphaPalette(byte a0, byte a1)
    {
        var palette = new byte[8];
        palette[0] = a0;
        palette[1] = a1;
        if (a0 > a1)
        {
            palette[2] = (byte)((6 * a0 + a1) / 7);
            palette[3] = (byte)((5 * a0 + 2 * a1) / 7);
            palette[4] = (byte)((4 * a0 + 3 * a1) / 7);
            palette[5] = (byte)((3 * a0 + 4 * a1) / 7);
            palette[6] = (byte)((2 * a0 + 5 * a1) / 7);
            palette[7] = (byte)((a0 + 6 * a1) / 7);
        }
        else
        {
            palette[2] = (byte)((4 * a0 + a1) / 5);
            palette[3] = (byte)((3 * a0 + 2 * a1) / 5);
            palette[4] = (byte)((2 * a0 + 3 * a1) / 5);
            palette[5] = (byte)((a0 + 4 * a1) / 5);
            palette[6] = 0;
            palette[7] = 255;
        }

        return palette;
    }

    private static (byte R, byte G, byte B)[] BuildDxtColorPalette(ushort c0, ushort c1)
    {
        var color0 = ExpandRgb565(c0);
        var color1 = ExpandRgb565(c1);

        var palette = new (byte R, byte G, byte B)[4];
        palette[0] = color0;
        palette[1] = color1;

        if (c0 > c1)
        {
            palette[2] = (
                (byte)((2 * color0.R + color1.R) / 3),
                (byte)((2 * color0.G + color1.G) / 3),
                (byte)((2 * color0.B + color1.B) / 3));
            palette[3] = (
                (byte)((color0.R + 2 * color1.R) / 3),
                (byte)((color0.G + 2 * color1.G) / 3),
                (byte)((color0.B + 2 * color1.B) / 3));
        }
        else
        {
            palette[2] = (
                (byte)((color0.R + color1.R) / 2),
                (byte)((color0.G + color1.G) / 2),
                (byte)((color0.B + color1.B) / 2));
            palette[3] = (0, 0, 0);
        }

        return palette;
    }

    private static (byte R, byte G, byte B) ExpandRgb565(ushort value)
    {
        var r = (value >> 11) & 0x1F;
        var g = (value >> 5) & 0x3F;
        var b = value & 0x1F;

        return (
            (byte)((r * 255 + 15) / 31),
            (byte)((g * 255 + 31) / 63),
            (byte)((b * 255 + 15) / 31));
    }

    private static string ReadPixelFormat(byte[] extras)
    {
        if (extras.Length < 48)
        {
            return string.Empty;
        }

        var declaredLength = BitConverter.ToInt32(extras, 36);
        if (declaredLength > 0 && declaredLength < 32 && (40 + declaredLength) <= extras.Length)
        {
            return Encoding.ASCII.GetString(extras, 40, declaredLength).TrimEnd('\0');
        }

        var maxLength = Math.Min(24, extras.Length - 40);
        return Encoding.ASCII.GetString(extras, 40, maxLength).Split('\0')[0];
    }

    private static IEnumerable<string> GenerateAliasCandidates(string itemId)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string value)
        {
            var normalized = value.Trim().Trim('_').ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                result.Add(normalized);
            }
        }

        Add(itemId);
        Add(StripPrefix(itemId, "weapon_"));
        Add(StripPrefix(itemId, "item_"));
        Add(StripPrefix(itemId, "bp_"));
        Add(StripSuffix(itemId, "_es"));
        Add(StripSuffix(itemId, "_rw"));

        var tokens = itemId.Split(['_', '-', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 1)
        {
            Add(string.Join('_', tokens.Skip(1)));
            Add(string.Join('_', tokens.Take(tokens.Length - 1)));
        }

        return result;
    }

    private static string StripPrefix(string value, string prefix)
    {
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..]
            : value;
    }

    private static string StripSuffix(string value, string suffix)
    {
        return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? value[..^suffix.Length]
            : value;
    }

    private static string NormalizeComparable(string value)
    {
        var key = value.ToLowerInvariant();
        if (key.StartsWith("ico_", StringComparison.OrdinalIgnoreCase))
        {
            key = key[4..];
        }

        key = key
            .Replace("_inventory", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_inhands", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_vicinity", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_inv", string.Empty, StringComparison.OrdinalIgnoreCase);

        var chars = key.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars);
    }

    private static string GetDirectoryLower(string relativePath)
    {
        var normalized = PathUtil.NormalizeRelative(relativePath);
        var directory = Path.GetDirectoryName(normalized.Replace('/', Path.DirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(directory)
            ? string.Empty
            : PathUtil.NormalizeRelative(directory).ToLowerInvariant();
    }

    private static bool IsIconAssetPath(string relativePath)
    {
        if (!relativePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(relativePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        return fileName.StartsWith("ico_", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("_ico.uasset", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCacheFileName(string itemId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = itemId.Trim().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        var safe = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(safe)
            ? "item-icon"
            : safe.ToLowerInvariant();
    }

    private static string? TryFindExtractedMatch(string extractRoot, string targetRelativePath)
    {
        if (!Directory.Exists(extractRoot))
        {
            return null;
        }

        var expected = PathUtil.NormalizeRelative(targetRelativePath);
        var fileName = Path.GetFileName(expected);
        foreach (var candidate in Directory.EnumerateFiles(extractRoot, fileName, SearchOption.AllDirectories))
        {
            var rel = PathUtil.NormalizeRelative(Path.GetRelativePath(extractRoot, candidate));
            if (rel.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }
}
