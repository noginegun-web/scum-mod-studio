using System.Text;
using System.Text.RegularExpressions;

namespace ScumPakWizard;

internal static class CryptoKeyWriter
{
    public static string Write(string outputRoot, string aesKeyHex)
    {
        var normalized = NormalizeHex(aesKeyHex);
        var keyBytes = Convert.FromHexString(normalized);
        var keyBase64 = Convert.ToBase64String(keyBytes);

        var cryptoPath = Path.Combine(outputRoot, "scum_crypto.json");
        var json = $$"""
                     {
                       "$types": {
                         "UnrealBuildTool.EncryptionAndSigning+CryptoSettings, UnrealBuildTool, Version=4.0.0.0, Culture=neutral, PublicKeyToken=null": "1",
                         "UnrealBuildTool.EncryptionAndSigning+EncryptionKey, UnrealBuildTool, Version=4.0.0.0, Culture=neutral, PublicKeyToken=null": "2"
                       },
                       "$type": "1",
                       "EncryptionKey": {
                         "$type": "2",
                         "Name": "key",
                         "Guid": "00000000000000000000000000000000",
                         "Key": "{{keyBase64}}"
                       },
                       "SigningKey": null,
                       "bEnablePakSigning": false,
                       "bEnablePakIndexEncryption": true,
                       "bEnablePakIniEncryption": true,
                       "bEnablePakUAssetEncryption": true,
                       "bEnablePakFullAssetEncryption": false,
                       "bDataCryptoRequired": true,
                       "PakEncryptionRequired": true,
                       "PakSigningRequired": false,
                       "SecondaryEncryptionKeys": []
                     }
                     """;
        File.WriteAllText(cryptoPath, json, new UTF8Encoding(false));
        return cryptoPath;
    }

    private static string NormalizeHex(string value)
    {
        var cleaned = value.Trim();
        if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[2..];
        }

        cleaned = cleaned.Replace(" ", string.Empty);
        if (cleaned.Length != 64 || !Regex.IsMatch(cleaned, "^[0-9A-Fa-f]{64}$"))
        {
            throw new InvalidOperationException("Некорректный AES-ключ. Ожидается 32 байта в hex.");
        }

        return cleaned.ToUpperInvariant();
    }
}
