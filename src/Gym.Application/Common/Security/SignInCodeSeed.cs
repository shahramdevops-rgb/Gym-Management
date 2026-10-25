using System.Buffers.Text;
using System.Security.Cryptography;

namespace Gym.Application.Common.Security;

/// <summary>A new seed for a one-time code: 16 random bytes, URL-safe Base64 (22 characters).</summary>
/// <remarks>
/// From the operating system's cryptographic generator, like <see cref="RefreshTokenSecret"/>:
/// a seed anyone could predict would make the code predictable, secret or not.
/// </remarks>
public static class SignInCodeSeed
{
    public const int Length = 22;

    private const int SizeInBytes = 16;

    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SizeInBytes));
}
