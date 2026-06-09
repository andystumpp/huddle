using System;
using System.Security.Cryptography;

namespace Huddle.Vision;

/// <summary>
/// Minimal ULID generator. 48 bits of millisecond timestamp + 80 bits of randomness,
/// Crockford base-32 encoded to 26 chars.
/// </summary>
internal static class UlidGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Generate()
    {
        long timeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Span<byte> random = stackalloc byte[10];
        RandomNumberGenerator.Fill(random);

        Span<char> output = stackalloc char[26];
        // First 10 chars encode the 48-bit timestamp.
        for (int i = 9; i >= 0; i--)
        {
            output[i] = Alphabet[(int)(timeMs & 0x1F)];
            timeMs >>= 5;
        }
        // Remaining 16 chars encode the 80 bits of randomness.
        // Pack the 10 bytes into 5-bit groups MSB-first.
        ulong hi = ((ulong)random[0] << 32) | ((ulong)random[1] << 24)
                 | ((ulong)random[2] << 16) | ((ulong)random[3] << 8) | random[4];
        ulong lo = ((ulong)random[5] << 32) | ((ulong)random[6] << 24)
                 | ((ulong)random[7] << 16) | ((ulong)random[8] << 8) | random[9];
        for (int i = 7; i >= 0; i--)
        {
            output[10 + i] = Alphabet[(int)(hi & 0x1F)];
            hi >>= 5;
        }
        for (int i = 7; i >= 0; i--)
        {
            output[18 + i] = Alphabet[(int)(lo & 0x1F)];
            lo >>= 5;
        }
        return new string(output);
    }
}
