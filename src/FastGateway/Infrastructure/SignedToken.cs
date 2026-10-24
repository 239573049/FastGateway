using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace FastGateway.Infrastructure;

/// <summary>
///     HMAC-SHA256 签名的 JSON 令牌：<c>base64url(payload).base64url(signature)</c>，
///     用于挑战状态与 clearance cookie。只防篡改，不加密，payload 不得包含机密。
/// </summary>
public static class SignedToken
{
    public static string Sign<T>(T value, JsonTypeInfo<T> typeInfo, byte[] key)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
        var signature = HMACSHA256.HashData(key, payload);
        return $"{Base64UrlEncode(payload)}.{Base64UrlEncode(signature)}";
    }

    public static bool TryRead<T>(string? encoded, JsonTypeInfo<T> typeInfo, byte[] key, out T value)
    {
        value = default!;
        if (string.IsNullOrWhiteSpace(encoded)) return false;

        var separator = encoded.IndexOf('.');
        if (separator <= 0 || separator == encoded.Length - 1) return false;

        try
        {
            var payload = Base64UrlDecode(encoded[..separator]);
            var signature = Base64UrlDecode(encoded[(separator + 1)..]);
            var expected = HMACSHA256.HashData(key, payload);
            if (!CryptographicOperations.FixedTimeEquals(signature, expected)) return false;

            var deserialized = JsonSerializer.Deserialize(payload, typeInfo);
            if (deserialized == null) return false;

            value = deserialized;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string CreateNonce()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    public static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static byte[] Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
        return Convert.FromBase64String(normalized);
    }
}
