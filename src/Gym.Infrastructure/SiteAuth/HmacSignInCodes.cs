using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Gym.Application.Common.Security;
using Gym.Domain.SiteAuth;

using Microsoft.Extensions.Options;

namespace Gym.Infrastructure.SiteAuth;

/// <summary>
/// The code is HMAC-SHA256 of the phone and the seed under <c>SiteAuth:CodeSecret</c>, cut to 5
/// digits (BUSINESS_RULES.md §15).
/// </summary>
/// <remarks>
/// <para>
/// HMAC rather than a plain hash: without the secret, a seed read from a backup gives nothing. The
/// phone is part of the input, so a seed copied onto another phone's row gives a different code.
/// </para>
/// <para>
/// The first 8 bytes of the HMAC as a number, modulo 100 000. A 64-bit number spread evenly makes
/// the remainders even too, to well under one part in a hundred million million.
/// </para>
/// </remarks>
public sealed class HmacSignInCodes(IOptions<SiteAuthOptions> options) : ISignInCodes
{
    private static readonly ulong Modulus = (ulong)Math.Pow(10, SignInCode.Digits);

    public string CodeFor(string phoneNumber, string codeSeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeSeed);

        var key = Encoding.UTF8.GetBytes(options.Value.CodeSecret);
        var input = Encoding.UTF8.GetBytes($"{phoneNumber}:{codeSeed}");
        var mac = HMACSHA256.HashData(key, input);

        var number = BinaryPrimitives.ReadUInt64BigEndian(mac) % Modulus;

        return number.ToString(CultureInfo.InvariantCulture).PadLeft(SignInCode.Digits, '0');
    }
}
