using System.Security.Cryptography;
using BombRedone.Core.Numerics;

namespace BombRedone.Core.Tests.Numerics;

/// <summary>
/// Mirror of the case generator in tools/reverse-engineering/real48.py (functions gen_real, gen_case,
/// result_record, op_hash). The probe runs the original runtime routines of BOMB.EXE in an 8086 emulator
/// and prints one SHA-256 per operation; <see cref="Hash"/> computes the same over the remake.
/// Any change here must be made in the probe as well.
/// </summary>
internal static class Real48ProbeCases
{
    private static readonly byte[] SpecialExponents =
        [0, 1, 2, 0x7F, 0x80, 0x81, 0x82, 0x9E, 0x9F, 0xA0, 0xA1, 0xFE, 0xFF];

    private static readonly Dictionary<string, ulong> Seeds = new()
    {
        ["add"] = 1, ["sub"] = 2, ["sqr"] = 3, ["mul"] = 4, ["div"] = 5, ["cmp"] = 6,
        ["float"] = 7, ["trunc"] = 8, ["round"] = 9, ["random"] = 10,
    };

    internal sealed class SplitMix64(ulong seed)
    {
        private ulong _state = seed;

        public ulong Next()
        {
            _state = unchecked(_state + 0x9E3779B97F4A7C15UL);
            ulong z = _state;
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            return z ^ (z >> 31);
        }
    }

    public static byte[] GenerateReal(SplitMix64 g, int? near = null)
    {
        ulong r = g.Next();
        ulong mode = r & 7;
        ulong mantissa = (r >> 8) & ((1UL << 40) - 1);
        int exponent;
        switch (mode)
        {
            case 0:
                exponent = (int)((r >> 48) & 0xFF);
                break;
            case 1:
                exponent = SpecialExponents[(int)(((r >> 48) & 0xFF) % (ulong)SpecialExponents.Length)];
                break;
            case 2:
                if (((r >> 60) & 1) != 0)
                {
                    mantissa &= ~0xFFFFFFUL;
                }

                exponent = 0x61 + (int)((r >> 48) & 0x3F);
                break;
            case 3 when near is not null:
                exponent = Math.Clamp(near.Value + (int)((r >> 48) & 0x7F) - 0x3F, 0, 255);
                break;
            case 4:
                mantissa = (((r >> 8) & 0xFF) << 32) | (((r >> 16) & 1) << 39);
                exponent = 0x81 + (int)((r >> 48) & 0x1F);
                break;
            case 5:
                return new byte[Real48.Size];
            default:
                exponent = 0x61 + (int)((r >> 48) & 0x3F);
                break;
        }

        var bytes = new byte[Real48.Size];
        bytes[0] = (byte)exponent;
        for (int i = 0; i < 5; i++)
        {
            bytes[1 + i] = (byte)(mantissa >> (8 * i));
        }

        return bytes;
    }

    /// <summary>Generated operands: one or two 6-byte reals, or a 32-bit integer for float/random.</summary>
    public static (byte[]? A, byte[]? B, uint Integer) GenerateCase(string op, SplitMix64 g)
    {
        if (op == "float")
        {
            ulong r = g.Next();
            ulong mode = r & 3;
            uint v = (uint)((r >> 32) & 0xFFFFFFFF);
            if (mode == 1)
            {
                v = unchecked((uint)((1UL << (int)((r >> 8) & 31)) + ((r >> 16) & 3) - 1));
                if (((r >> 24) & 1) != 0)
                {
                    v = unchecked(0u - v);
                }
            }

            return (null, null, v);
        }

        if (op == "random")
        {
            return (null, null, (uint)((g.Next() >> 16) & 0xFFFFFFFF));
        }

        var a = GenerateReal(g);
        if (op is "sqr" or "trunc" or "round")
        {
            return (a, null, 0);
        }

        return (a, GenerateReal(g, a[0]), 0);
    }

    /// <summary>Result of one case as (error code, result bytes), like run() in the probe.</summary>
    public static (int Error, byte[] Result) Run(string op, (byte[]? A, byte[]? B, uint Integer) c)
    {
        try
        {
            return (0, op switch
            {
                "add" => (Real(c.A) + Real(c.B)).ToBytes(),
                "sub" => (Real(c.A) - Real(c.B)).ToBytes(),
                "cmp" => [(byte)(sbyte)Real48.Compare(Real(c.A), Real(c.B))],
                "float" => Real48.FromInt32(unchecked((int)c.Integer)).ToBytes(),
                _ => RunExtended(op, c),
            });
        }
        catch (RunErrorException e)
        {
            return (e.Code, []);
        }
    }

    private static byte[] RunExtended(string op, (byte[]? A, byte[]? B, uint Integer) c) => op switch
    {
        "mul" => (Real(c.A) * Real(c.B)).ToBytes(),
        "div" => (Real(c.A) / Real(c.B)).ToBytes(),
        "sqr" => Real48.Square(Real(c.A)).ToBytes(),
        "trunc" => LittleEndian(Real(c.A).Trunc()),
        "round" => LittleEndian(Real(c.A).Round()),
        _ => throw new NotSupportedException(op),
    };

    private static byte[] LittleEndian(int value)
    {
        var bytes = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static Real48 Real(byte[]? bytes) => Real48.FromBytes(bytes!);

    public static string Hash(string op, int count)
    {
        var g = new SplitMix64(Seeds[op]);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (int i = 0; i < count; i++)
        {
            var (error, result) = Run(op, GenerateCase(op, g));
            sha.AppendData([(byte)error, (byte)result.Length]);
            sha.AppendData(result);
        }

        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }
}
