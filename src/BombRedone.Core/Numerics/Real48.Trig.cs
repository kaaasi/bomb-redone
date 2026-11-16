namespace BombRedone.Core.Numerics;

/// <summary>
/// Sin, Cos, Sqrt, Int and Frac of the TP 6.0 runtime and the degree/distance helpers of the game unit 0DBD
/// (docs/analyse/feld.md, section "Schuss"). Every step uses the Real48 operations of the runtime in the same
/// order and with the same operand order as the original, so the results are bit-identical (Real48TrigProbeTests).
/// </summary>
public readonly partial struct Real48
{
    /// <summary>Pi as used by the game. [ORIGINAL] BOMB.EXE 0x0F365 (0DBD:00F5, operand of Rad), also 2 pi / 2 in Sin.</summary>
    public static Real48 Pi => new(0x2182, 0xDAA2, 0x490F);

    /// <summary>[ORIGINAL] 12CE:1305 (BOMB.EXE 0x15685): 2 pi.</summary>
    private static Real48 TwoPi => new(0x2183, 0xDAA2, 0x490F);

    /// <summary>[ORIGINAL] 12CE:1331 / 12CE:133B: exponent of 2 pi decremented twice.</summary>
    private static Real48 HalfPi => new(0x2181, 0xDAA2, 0x490F);

    /// <summary>[ORIGINAL] 12CE:12EE (BOMB.EXE 0x1566E): -pi / 2.</summary>
    private static Real48 MinusHalfPi => new(0x2181, 0xDAA2, 0xC90F);

    /// <summary>[ORIGINAL] 0DBD:00E8 (BOMB.EXE 0x0F358): 180.</summary>
    private static Real48 Degrees180 => new(0x0088, 0x0000, 0x3400);

    /// <summary>
    /// Coefficients of the Sin polynomial in x², highest power first.
    /// [ORIGINAL] table 12CE:1362 (BOMB.EXE 0x156E2), 7 entries (alternating 1/(2k+1)! values).
    /// </summary>
    private static readonly Real48[] SinCoefficients =
    [
        FromHex("589D399F3FD7"),
        FromHex("60439D309230"),
        FromHex("67AA3F2832D7"),
        FromHex("6EB62A1DEF38"),
        FromHex("740DD0000DD0"),
        FromHex("7A8888888808"),
        FromHex("7EABAAAAAAAA"),
    ];

    /// <summary>
    /// Int: integer part (fraction bits cleared, sign kept). [ORIGINAL] 12CE:122A (BOMB.EXE 0x155AA): exponent at
    /// least A8h returns the value unchanged, at most 80h returns zero.
    /// </summary>
    public static Real48 Int(Real48 value)
    {
        byte exponent = (byte)value._ax;
        if (exponent >= 0xA8)
        {
            return value;
        }

        if (exponent <= 0x80)
        {
            return Zero;
        }

        int keep = exponent - 0x80; // sign + integer bits below the hidden bit, from the top of DX:BX:AH
        ulong bits = ((ulong)value._dx << 24) | ((ulong)value._bx << 8) | (uint)(value._ax >> 8);
        ulong mask = ((1UL << keep) - 1) << (40 - keep);
        bits &= mask;
        return new((ushort)((((uint)bits & 0xFF) << 8) | exponent), (ushort)(bits >> 8), (ushort)(bits >> 24));
    }

    /// <summary>Frac = value - Int(value). [ORIGINAL] 12CE:127B (BOMB.EXE 0x155FB), Sub 12CE:0EAE.</summary>
    public static Real48 Frac(Real48 value) => Subtract(value, Int(value));

    /// <summary>
    /// Sin (radians). [ORIGINAL] 12CE:1301 (BOMB.EXE 0x15681): exponent below 6Ch returns the argument; reduction
    /// to [0, 2 pi) with Frac(x / 2 pi) · 2 pi only for |x| ≥ 2 pi, then folding to [0, pi/2] and the polynomial
    /// 12CE:160D with table 12CE:1362 (exponent below 6Ch skips it again).
    /// </summary>
    public static Real48 Sin(Real48 value)
    {
        var v = value;
        if ((byte)v._ax < 0x6C)
        {
            return v;
        }

        var magnitude = new Real48(v._ax, v._bx, (ushort)(v._dx & 0x7FFF));
        if (Compare(magnitude, TwoPi) >= 0)
        {
            v = Multiply(Frac(DivideCore(v._ax, v._bx, v._dx, TwoPi._ax, TwoPi._bx, TwoPi._dx)), TwoPi);
        }

        if (v.IsNegative)
        {
            v = Add(v, TwoPi);
        }

        bool belowPi = Compare(v, Pi) < 0;
        if (!belowPi)
        {
            v = Subtract(v, Pi);
        }

        if (Compare(v, HalfPi) >= 0)
        {
            v = Add(new Real48(v._ax, v._bx, (ushort)(v._dx | 0x8000)), Pi);
        }

        if ((byte)v._ax >= 0x6C)
        {
            v = Polynomial(v, SinCoefficients);
        }

        if (!belowPi && (byte)v._ax != 0)
        {
            v = -v;
        }

        return v;
    }

    /// <summary>
    /// Cos = Sin(pi/2 - x). [ORIGINAL] 12CE:12EE (BOMB.EXE 0x1566E): x + (-pi/2), sign flipped unless zero, then Sin.
    /// </summary>
    public static Real48 Cos(Real48 value)
    {
        var v = Add(value, MinusHalfPi);
        if ((byte)v._ax != 0)
        {
            v = -v;
        }

        return Sin(v);
    }

    /// <summary>
    /// Sqrt by Newton steps. [ORIGINAL] 12CE:128F (BOMB.EXE 0x1560F): zero (exponent 0) returns the argument,
    /// negative is runtime error 207; start value is the argument with halved exponent; step g := (x / g + g) / 2
    /// (division 12CE:107A, halving by decrementing the exponent byte) until the exponent of the change is below the
    /// start exponent - 14h (byte compare).
    /// </summary>
    public static Real48 Sqrt(Real48 value)
    {
        byte exponent = (byte)value._ax;
        if (exponent == 0)
        {
            return value;
        }

        if (value.IsNegative)
        {
            throw new RunErrorException(RunErrorException.InvalidFloatingPointOperation);
        }

        byte start = unchecked((byte)(((sbyte)unchecked((byte)(exponent + 0x80)) >> 1) + 0x80));
        byte threshold = unchecked((byte)(start - 0x14));
        var guess = new Real48((ushort)((value._ax & 0xFF00) | start), value._bx, value._dx);
        while (true)
        {
            var next = Add(DivideCore(value._ax, value._bx, value._dx, guess._ax, guess._bx, guess._dx), guess);
            next = new Real48((ushort)((next._ax & 0xFF00) | unchecked((byte)((byte)next._ax - 1))), next._bx, next._dx);
            var change = Subtract(next, guess);
            guess = next;
            if ((byte)change._ax < threshold)
            {
                return guess;
            }
        }
    }

    /// <summary>
    /// Degrees to radians: x / 180 · pi. [ORIGINAL] 0DBD:00D9 (BOMB.EXE 0x0F349), Div 12CE:11CE and Mul 12CE:11C8.
    /// </summary>
    public static Real48 DegreesToRadians(Real48 degrees) => Multiply(Divide(degrees, Degrees180), Pi);

    /// <summary>
    /// Sqrt(a² + b²). [ORIGINAL] 0DBD:008F (BOMB.EXE 0x0F2FF): Sqr of the first argument plus Sqr of the second
    /// (Add with the first square as left operand), then Sqrt 12CE:128F.
    /// </summary>
    public static Real48 Distance(Real48 first, Real48 second) => Sqrt(Add(Square(first), Square(second)));

    /// <summary>
    /// Coefficients of the ArcTan series in x², highest power first: −1/11, 1/9, −1/7, 1/5, −1/3.
    /// [ORIGINAL] table 12CE:15E9 (BOMB.EXE 0x15969), 5 entries, used through 12CE:1607.
    /// </summary>
    private static readonly Real48[] ArcTanCoefficients =
    [
        FromHex("7DE8A28B2EBA"),
        FromHex("7D8EE3388E63"),
        FromHex("7E4992244992"),
        FromHex("7ECDCCCCCC4C"),
        FromHex("7FABAAAAAAAA"),
    ];

    // [ORIGINAL] 12CE:1509: below this ArcTan uses the series directly (0.1371456616).
    private static readonly Real48 ArcTanSeriesLimit = FromHex("7E4A8EE96F0C");

    // [ORIGINAL] table 12CE:15B9 (BOMB.EXE 0x15939): per step limit, tangent and angle (tan 22.5°: 15°; 0.7673: 30°),
    // then tangent 1 and angle pi/4 for the rest.
    private static readonly (Real48 Limit, Real48 Tangent, Real48 Angle)[] ArcTanSteps =
    [
        (FromHex("7FE7CFCC1354"), FromHex("7FF6F4A23009"), FromHex("7F6AC1910A06")),
        (FromHex("80B59E8A6F44"), FromHex("80822C3ACD13"), FromHex("806AC1910A06")),
    ];

    private static readonly Real48 ArcTanLastTangent = FromHex("810000000000");
    private static readonly Real48 QuarterPi = FromHex("8021A2DA0F49");

    /// <summary>
    /// ArcTan (radians). [ORIGINAL] 12CE:14D8 (BOMB.EXE 0x15858): zero returns the argument; the sign is taken off;
    /// x ≥ 1 becomes 1 / x (12CE:107A); below 0.137 the series 12CE:1607 alone, otherwise the first step k with x &lt;
    /// limit_k (else tangent 1, angle pi/4): series((x − t) / (x · t + 1)) + angle; then pi/2 − result for an inverted
    /// argument and the sign back. Operand order as in the original (Sub 0EAE, Mul 0F75, Add 0EB2).
    /// </summary>
    public static Real48 ArcTan(Real48 value)
    {
        if ((byte)value._ax == 0)
        {
            return value;
        }

        var negative = value.IsNegative;
        var x = new Real48(value._ax, value._bx, (ushort)(value._dx & 0x7FFF));
        var inverted = false;
        if (Compare(x, One) >= 0)
        {
            x = DivideCore(One._ax, One._bx, One._dx, x._ax, x._bx, x._dx);
            inverted = true;
        }

        Real48 result;
        if (Compare(x, ArcTanSeriesLimit) < 0)
        {
            result = Polynomial(x, ArcTanCoefficients);
        }
        else
        {
            var tangent = ArcTanLastTangent;
            var angle = QuarterPi;
            foreach (var step in ArcTanSteps)
            {
                if (Compare(x, step.Limit) < 0)
                {
                    tangent = step.Tangent;
                    angle = step.Angle;
                    break;
                }
            }

            var numerator = Subtract(x, tangent);
            var denominator = Add(Multiply(x, tangent), One);
            var q = DivideCore(numerator._ax, numerator._bx, numerator._dx, denominator._ax, denominator._bx, denominator._dx);
            result = Add(Polynomial(q, ArcTanCoefficients), angle);
        }

        if (inverted)
        {
            result = Subtract(HalfPi, result);
        }

        if (negative)
        {
            result = new Real48(result._ax, result._bx, (ushort)(result._dx | 0x8000));
        }

        return result;
    }

    private static readonly Lazy<Real48[]> DegreeSines = new(() =>
    {
        var table = new Real48[91];
        for (int a = 0; a <= 90; a++)
        {
            table[a] = Sin(Divide(Multiply(FromInt32(a), Pi), Degrees180));
        }

        return table;
    });

    /// <summary>
    /// Sine of whole degrees from the table of the original. [ORIGINAL] 0B25:0000 (BOMB.EXE 0x0C8F0): sign off for
    /// a &lt; 0, a − 180 with sign flip while a &gt; 180, 180 − a above 90, then entry a of the 91 Real48 values at
    /// 0B25:00F9 with the sign bit flipped (also for 0). The entries are Sin(a · pi / 180) of the runtime (multiply first,
    /// then divide; checked against the table in the test with the original data).
    /// </summary>
    public static Real48 SinDegrees(int degrees)
    {
        var a = (short)degrees;
        ushort sign = 0;
        if (a < 0)
        {
            a = (short)-a;
            sign = 0x8000;
        }

        while (a > 180)
        {
            sign ^= 0x8000;
            a -= 180;
        }

        if ((ushort)a > 90)
        {
            a = (short)(180 - a);
        }

        var v = DegreeSines.Value[a];
        return new Real48(v._ax, v._bx, (ushort)(v._dx ^ sign));
    }

    /// <summary>Cosine of whole degrees. [ORIGINAL] 0B25:005A (BOMB.EXE 0x0C94A): <see cref="SinDegrees"/>(90 − a).</summary>
    public static Real48 CosDegrees(int degrees) => SinDegrees((short)(90 - degrees));

    /// <summary>
    /// Radians to degrees: x / pi · 180. [ORIGINAL] 0DBD:011B (BOMB.EXE 0x0F38B), Div 12CE:11CE and Mul 12CE:11C8.
    /// </summary>
    public static Real48 RadiansToDegrees(Real48 radians) => Multiply(Divide(radians, Pi), Degrees180);

    /// <summary>
    /// [ORIGINAL] 12CE:160D (BOMB.EXE 0x1598D): t = x · x; s = c0 · t; s = (s + c_k) · t for k = 1..n-1;
    /// result (s + 1) · x. Operand order as in 12CE:1626.
    /// </summary>
    private static Real48 Polynomial(Real48 x, Real48[] coefficients)
    {
        var t = Multiply(x, x);
        var s = Multiply(coefficients[0], t);
        for (int k = 1; k < coefficients.Length; k++)
        {
            s = Multiply(Add(s, coefficients[k]), t);
        }

        return Multiply(Add(s, One), x);
    }
}
