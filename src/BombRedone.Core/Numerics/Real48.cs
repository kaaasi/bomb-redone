using System.Buffers.Binary;
using System.Globalization;

namespace BombRedone.Core.Numerics;

/// <summary>
/// The 6-byte <c>Real</c> of Turbo Pascal 6.0 with the arithmetic of the runtime linked into BOMB.EXE
/// (software floating point, <c>$N-</c>; docs/analyse/real48.md).
/// Memory layout: byte 0 exponent (bias 129, 0 means zero), bytes 1..5 mantissa little endian without the
/// hidden bit, bit 7 of byte 5 is the sign. The runtime holds a value in three registers DX:BX:AX
/// (AL exponent, AH lowest mantissa byte); the fields mirror that.
/// Every operation is a step-by-step translation of the runtime routine (12CE:0EAE-1201), including its
/// truncating alignment, rounding on the guard byte and its treatment of non-canonical zeros (exponent 0
/// with mantissa bits), so results are bit-identical with the original.
/// <see cref="Equals(Real48)"/> and <c>==</c> compare the bits; the Pascal comparison is <see cref="Compare"/>.
/// </summary>
public readonly partial struct Real48 : IEquatable<Real48>
{
    public const int Size = 6;

    private readonly ushort _ax;
    private readonly ushort _bx;
    private readonly ushort _dx;

    private Real48(ushort ax, ushort bx, ushort dx)
    {
        _ax = ax;
        _bx = bx;
        _dx = dx;
    }

    public static Real48 Zero => default;

    /// <summary>1.0 (exponent 81h, mantissa 0), as loaded by the runtime itself (12CE:1667).</summary>
    public static Real48 One => new(0x0081, 0, 0);

    public byte Exponent => (byte)_ax;

    public bool IsNegative => (_dx & 0x8000) != 0;

    /// <summary>Exponent 0 means zero for every routine, whatever the mantissa bits are.</summary>
    public bool IsZero => Exponent == 0;

    public static Real48 FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Size)
        {
            throw new ArgumentException($"A Real48 needs {Size} bytes.", nameof(bytes));
        }

        return new Real48(
            BinaryPrimitives.ReadUInt16LittleEndian(bytes),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]));
    }

    public void WriteBytes(Span<byte> destination)
    {
        if (destination.Length < Size)
        {
            throw new ArgumentException($"A Real48 needs {Size} bytes.", nameof(destination));
        }

        BinaryPrimitives.WriteUInt16LittleEndian(destination, _ax);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], _bx);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[4..], _dx);
    }

    public byte[] ToBytes()
    {
        var bytes = new byte[Size];
        WriteBytes(bytes);
        return bytes;
    }

    /// <summary>Parses 12 hex digits in memory byte order (as the probe and the analysis notes print them).</summary>
    public static Real48 FromHex(string hex) => FromBytes(Convert.FromHexString(hex));

    public string ToHex() => Convert.ToHexString(ToBytes());

    /// <summary>
    /// LongInt to Real. [ORIGINAL] BOMB.EXE 0x1549B (12CE:111B, called via 12CE:11DC): exact, the magnitude
    /// is normalised from exponent A0h downwards, the mantissa byte AH stays 0.
    /// </summary>
    public static Real48 FromInt32(int value)
    {
        uint magnitude = (uint)value;
        if (magnitude == 0)
        {
            return Zero;
        }

        bool negative = value < 0;
        if (negative)
        {
            magnitude = unchecked(0u - magnitude);
        }

        ushort dx = (ushort)(magnitude >> 16);
        ushort bx = (ushort)magnitude;
        byte exponent = 0xA0;
        if (dx == 0)
        {
            dx = bx;
            bx = 0;
            exponent = 0x90;
            if ((dx & 0xFF00) == 0)
            {
                dx = (ushort)(dx << 8);
                exponent = 0x88;
            }
        }

        while ((dx & 0x8000) == 0)
        {
            exponent--;
            uint shifted = ((uint)dx << 17) | ((uint)bx << 1);
            dx = (ushort)(shifted >> 16);
            bx = (ushort)shifted;
        }

        if (!negative)
        {
            dx &= 0x7FFF;
        }

        return new Real48(exponent, bx, dx);
    }

    /// <summary>
    /// Value as <see cref="double"/> for display and diagnostics only; the game logic must stay in Real48.
    /// [APPROX] Not part of the original; exact because 40 mantissa bits fit into a double.
    /// </summary>
    public double ToDouble()
    {
        if (IsZero)
        {
            return 0.0;
        }

        ulong mantissa = Mantissa40;
        double value = Math.ScaleB(mantissa, Exponent - 129 - 39);
        return IsNegative ? -value : value;
    }

    public override string ToString() => ToDouble().ToString("R", CultureInfo.InvariantCulture);

    /// <summary>Mantissa with the hidden bit as bit 39.</summary>
    private ulong Mantissa40 => (1UL << 39) | ((ulong)(_dx & 0x7FFF) << 24) | ((ulong)_bx << 8) | (uint)(_ax >> 8);

    public static Real48 operator +(Real48 left, Real48 right) => Add(left, right);

    public static Real48 operator -(Real48 left, Real48 right) => Subtract(left, right);

    /// <summary>
    /// Negation flips the sign bit only (also of a zero). [APPROX] Turbo Pascal compiles unary minus inline;
    /// the code sites in BOMB.EXE/BOMB.OVR are not checked yet.
    /// </summary>
    public static Real48 operator -(Real48 value) => new(value._ax, value._bx, (ushort)(value._dx ^ 0x8000));

    /// <summary>
    /// Addition. [ORIGINAL] BOMB.EXE 0x15536 (12CE:11B6) calling 12CE:0EB2; overflow is runtime error 205.
    /// </summary>
    public static Real48 Add(Real48 left, Real48 right) =>
        AddCore(left._ax, left._bx, left._dx, right._ax, right._bx, right._dx);

    /// <summary>
    /// Subtraction left - right. [ORIGINAL] BOMB.EXE 0x1553C (12CE:11BC): flips the sign of the right operand
    /// (12CE:0EAE) and continues with the addition.
    /// </summary>
    public static Real48 Subtract(Real48 left, Real48 right) =>
        AddCore(left._ax, left._bx, left._dx, right._ax, right._bx, (ushort)(right._dx ^ 0x8000));

    /// <summary>
    /// Translation of 12CE:0EB2 (BOMB.EXE 0x15232). First operand DX:BX:AX, second CX:SI:DI.
    /// </summary>
    private static Real48 AddCore(ushort ax, ushort bx, ushort dx, ushort cx, ushort si, ushort di)
    {
        if ((byte)cx == 0)
        {
            return new Real48(ax, bx, dx);
        }

        if ((byte)ax == 0)
        {
            return new Real48(cx, si, di);
        }

        if ((byte)ax > (byte)cx)
        {
            (ax, cx) = (cx, ax);
            (bx, si) = (si, bx);
            (dx, di) = (di, dx);
        }

        // Now DX:BX:AX has the smaller (or equal) exponent.
        int shift = (byte)cx - (byte)ax;
        if (shift >= 40)
        {
            return new Real48(cx, si, di);
        }

        byte exponent = (byte)cx;
        byte smallerSign = (byte)((dx >> 8) & 0x80);
        bool subtract = ((dx ^ di) & 0x8000) != 0;

        // 48-bit mantissas: hidden bit set, the low byte (AL / CL) is the guard byte, initially 0.
        ulong small = ((ulong)(dx | 0x8000) << 32) | ((ulong)bx << 16) | (uint)(ax & 0xFF00);
        ulong large = ((ulong)(di | 0x8000) << 32) | ((ulong)si << 16) | (uint)(cx & 0xFF00);
        small >>= shift; // byte and bit shifts without sticky bits (0EE3-0F05)

        byte sign;
        ulong sum;
        if (!subtract)
        {
            sign = smallerSign;
            sum = small + large;
            if ((sum >> 48) != 0)
            {
                sum = (sum >> 1) & Mask48;
                sum |= 1UL << 47;
                exponent++;
                if (exponent == 0)
                {
                    throw new RunErrorException(RunErrorException.FloatingPointOverflow);
                }
            }
        }
        else
        {
            sign = smallerSign;
            if (small >= large)
            {
                sum = small - large;
            }
            else
            {
                sum = large - small;
                sign ^= 0x80;
            }

            if (sum == 0)
            {
                return Zero;
            }

            while ((sum & (1UL << 47)) == 0)
            {
                sum <<= 1;
                exponent--;
                if (exponent == 0)
                {
                    return Zero;
                }
            }
        }

        return RoundAndPack(sum, exponent, sign);
    }

    /// <summary>12CE:0F1F-0F31: round on the guard byte (half up in magnitude), then exponent and sign.</summary>
    private static Real48 RoundAndPack(ulong mantissa48, byte exponent, byte sign)
    {
        mantissa48 += 0x80;
        if ((mantissa48 >> 48) != 0)
        {
            // Only DX is shifted (0F39); BX and AH are 0 after the carry ran through them.
            mantissa48 = (mantissa48 & 0xFFFF_FFFF) | ((((mantissa48 >> 32) & 0xFFFF) >> 1 | 0x8000) << 32);
            exponent++;
            if (exponent == 0)
            {
                throw new RunErrorException(RunErrorException.FloatingPointOverflow);
            }
        }

        ushort ax = (ushort)((mantissa48 & 0xFF00) | exponent);
        ushort bx = (ushort)(mantissa48 >> 16);
        ushort dx = (ushort)(((mantissa48 >> 32) & 0x7FFF) | ((uint)sign << 8));
        return new Real48(ax, bx, dx);
    }

    private const ulong Mask48 = (1UL << 48) - 1;

    /// <summary>
    /// Pascal comparison: negative, zero or positive like <paramref name="left"/> - <paramref name="right"/>.
    /// [ORIGINAL] BOMB.EXE 0x15471 (12CE:10F1, called via 12CE:11D8): different signs decide by the sign bit
    /// alone (so +0 &gt; -x and -0 &lt; +0 for a zero with sign bit); equal signs compare exponent, then the
    /// mantissa words; two numbers with exponent 0 and the same sign are equal.
    /// </summary>
    public static int Compare(Real48 left, Real48 right)
    {
        if (((left._dx ^ right._dx) & 0x8000) != 0)
        {
            return left.IsNegative ? -1 : 1;
        }

        int magnitude = CompareMagnitude(left, right);
        return left.IsNegative ? -magnitude : magnitude;
    }

    /// <summary>12CE:1108: exponent, then DX, BX, AH unsigned; stops at equal exponent 0.</summary>
    private static int CompareMagnitude(Real48 left, Real48 right)
    {
        int c = ((byte)left._ax).CompareTo((byte)right._ax);
        if (c != 0 || (byte)left._ax == 0)
        {
            return Math.Sign(c);
        }

        c = left._dx.CompareTo(right._dx);
        if (c == 0)
        {
            c = left._bx.CompareTo(right._bx);
        }

        if (c == 0)
        {
            c = (left._ax >> 8).CompareTo(right._ax >> 8);
        }

        return Math.Sign(c);
    }

    public static bool operator <(Real48 left, Real48 right) => Compare(left, right) < 0;

    public static bool operator >(Real48 left, Real48 right) => Compare(left, right) > 0;

    public static bool operator <=(Real48 left, Real48 right) => Compare(left, right) <= 0;

    public static bool operator >=(Real48 left, Real48 right) => Compare(left, right) >= 0;

    public bool Equals(Real48 other) => _ax == other._ax && _bx == other._bx && _dx == other._dx;

    public override bool Equals(object? obj) => obj is Real48 other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_ax, _bx, _dx);

    public static bool operator ==(Real48 left, Real48 right) => left.Equals(right);

    public static bool operator !=(Real48 left, Real48 right) => !left.Equals(right);
}
