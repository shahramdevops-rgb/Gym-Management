using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Gym.Application.Common.Security;

/// <summary>Creates refresh token values and the hashes that are stored instead of them.</summary>
/// <remarks>
/// <para>
/// 32 bytes from the operating system's cryptographic random number generator: 256 bits, far
/// beyond guessing. <see cref="Random"/> would not do; its output can be predicted.
/// </para>
/// <para>
/// A plain SHA-256 is enough here, unlike for passwords. Password hashes are slow on purpose
/// because people choose guessable passwords. A 256-bit random value has nothing to guess, so
/// a fast hash loses nothing, and every refresh can look a token up by its hash directly.
/// </para>
/// </remarks>
public static class RefreshTokenSecret
{
    private const int SizeInBytes = 32;

    /// <summary>A new token value, URL-safe so it needs no escaping in a cookie.</summary>
    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SizeInBytes));

    /// <summary>The value stored in <c>refresh_tokens.token_hash</c>: SHA-256 as lower-case hex.</summary>
    public static string Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
