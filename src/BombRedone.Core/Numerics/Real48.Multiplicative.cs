namespace BombRedone.Core.Numerics;

/// <summary>Mul, Sqr, Div, Trunc and Round of the TP 6.0 runtime (docs/analyse/real48.md).</summary>
public readonly partial struct Real48
{
    public static Real48 operator *(Real48 left, Real48 right) => Multiply(left, right);

    public static Real48 operator /(Real48 left, Real48 right) => Divide(left, right);

    /// <summary>
    /// Multiplication. [ORIGINAL] BOMB.EXE 0x15548 (12CE:11C8) calling 12CE:0F75; overflow is runtime error 205.
    /// The product is formed from word products like the original (not always the correctly rounded product).
    /// </summary>
    public static Real48 Multiply(Real48 left, Real48 right) =>
        MultiplyCore(left._ax, left._bx, left._dx, right._ax, right._bx, right._dx);

    /// <summary>Sqr. [ORIGINAL] BOMB.EXE 0x15542 (12CE:11C2): copies the operand and multiplies.</summary>
    public static Real48 Square(Real48 value) =>
        MultiplyCore(value._ax, value._bx, value._dx, value._ax, value._bx, value._dx);

    /// <summary>
    /// Division left / right. [ORIGINAL] BOMB.EXE 0x1554E (12CE:11CE): divisor exponent 0 is runtime error 200,
    /// then 12CE:107A; overflow is runtime error 205.
    /// </summary>
    public static Real48 Divide(Real48 left, Real48 right)
    {
        if ((byte)right._ax == 0)
        {
            throw new RunErrorException(RunErrorException.DivisionByZero);
        }

        return DivideCore(left._ax, left._bx, left._dx, right._ax, right._bx, right._dx);
    }

    /// <summary>
    /// Trunc. [ORIGINAL] BOMB.EXE 0x15560 (12CE:11E0) calling 12CE:115A with CH = 0; runtime error 207 above
    /// exponent 9Fh.
    /// </summary>
    public int Trunc() => ToInteger(round: false);

    /// <summary>
    /// Round (half away from zero). [ORIGINAL] BOMB.EXE 0x15568 (12CE:11E8) calling 12CE:115A with CH = 1;
    /// runtime error 207 above exponent 9Fh or when rounding reaches 2^31.
    /// </summary>
    public int Round() => ToInteger(round: true);

    /// <summary>Translation of 12CE:0F75 (BOMB.EXE 0x152F5). First factor DX:BX:AX, second CX:SI:DI.</summary>
    private static Real48 MultiplyCore(ushort ax, ushort bx, ushort dx, ushort cx, ushort si, ushort di)
    {
        if ((byte)ax == 0 || (byte)cx == 0)
        {
            return Zero;
        }

        ushort bp = dx;
        // DX: sign in bit 15, exponent sum in DL with carry into DH (0F80-0F8A).
        uint exponentSum = (uint)(byte)ax + (byte)cx;
        ushort exponentWord = (ushort)(((dx ^ di) & 0x8000) + exponentSum);
        ax &= 0xFF00;
        cx &= 0xFF00;
        bp |= 0x8000;
        di |= 0x8000;

        ushort low, mid, high;
        bool firstShort = (ax & 0xFF00) == 0 && bx == 0;
        bool secondShort = (cx & 0xFF00) == 0 && si == 0;
        if (firstShort || secondShort)
        {
            if (!firstShort)
            {
                (ax, cx) = (cx, ax);
                (bx, si) = (si, bx);
                (bp, di) = (di, bp);
            }

            // 0FAC-0FC5: 16-bit factor BP times the 40-bit factor DI:SI:CH.
            uint lowest = (uint)(bp >> 8) * (uint)(cx >> 8);
            uint p = (uint)bp * si;
            uint t = (lowest & 0xFFFF) + (p & 0xFFFF);
            ushort b = (ushort)t;
            ushort carryWord = (ushort)((p >> 16) + (t >> 16));
            uint q = (uint)bp * di;
            uint u = (q & 0xFFFF) + carryWord;
            ushort a = (ushort)u;
            ushort d = (ushort)((q >> 16) + (u >> 16));
            (high, mid, low) = (d, a, b);
        }
        else
        {
            (high, mid, low) = FullProduct(ax, bx, bp, cx, si, di);
        }

        return MultiplyDivideTail(low, mid, high, exponentWord, normalizeAdjust: 0x8081);
    }

    /// <summary>0FCB-1043: the nine word products on the stack frame, accumulated like the original.</summary>
    private static (ushort High, ushort Mid, ushort Low) FullProduct(
        ushort w0, ushort w2, ushort w4, ushort w6, ushort w8, ushort w10)
    {
        ushort si, di, bx, ax, dx;

        // Accumulates a 32-bit product into (r0, r1) and the carry into r2, like add/adc/adc.
        static void Accumulate(uint product, ref ushort r0, ref ushort r1, ref ushort r2)
        {
            uint s0 = (uint)r0 + (ushort)product;
            r0 = (ushort)s0;
            uint s1 = (uint)r1 + (ushort)(product >> 16) + (s0 >> 16);
            r1 = (ushort)s1;
            r2 = (ushort)(r2 + (s1 >> 16));
        }

        si = (ushort)((w0 >> 8) * (w6 >> 8));
        di = 0;
        bx = 0;
        Accumulate((uint)w0 * w8, ref si, ref di, ref bx);
        Accumulate((uint)w2 * w6, ref si, ref di, ref bx);
        si = 0;
        Accumulate((uint)w0 * w10, ref di, ref bx, ref si);
        Accumulate((uint)w2 * w8, ref di, ref bx, ref si);
        Accumulate((uint)w4 * w6, ref di, ref bx, ref si);
        di = 0;
        Accumulate((uint)w2 * w10, ref bx, ref si, ref di);
        Accumulate((uint)w4 * w8, ref bx, ref si, ref di);
        uint top = (uint)w4 * w10;
        uint t0 = (ushort)top + (uint)si;
        ax = (ushort)t0;
        dx = (ushort)((top >> 16) + di + (t0 >> 16));
        return (dx, ax, bx);
    }

    /// <summary>
    /// 1046-1072 (shared by Mul and Div from 1058): normalise one step, remove the bias, round on the guard
    /// byte, then underflow (bit 6 of the high exponent byte) gives 0 and bit 0 is the overflow error.
    /// </summary>
    private static Real48 MultiplyDivideTail(ushort ax, ushort bx, ushort dx, ushort cx, ushort normalizeAdjust)
    {
        if (normalizeAdjust == 0x8081)
        {
            if ((dx & 0x8000) == 0)
            {
                Shl48(ref ax, ref bx, ref dx);
                cx--;
            }

            cx = (ushort)(cx - 0x8081);
        }

        uint s0 = (uint)ax + 0x80;
        ax = (ushort)s0;
        uint s1 = (uint)bx + (s0 >> 16);
        bx = (ushort)s1;
        uint s2 = (uint)dx + (s1 >> 16);
        dx = (ushort)s2;
        if ((s2 >> 16) != 0)
        {
            dx = (ushort)((dx >> 1) | 0x8000);
            cx++;
        }

        byte ch = (byte)(cx >> 8);
        if ((ch & 0x40) != 0)
        {
            return Zero;
        }

        cx++;
        ch = (byte)(cx >> 8);
        ax = (ushort)((ax & 0xFF00) | (byte)cx);
        dx = (ushort)(dx ^ (ch << 8));
        if ((ch & 1) != 0)
        {
            throw new RunErrorException(RunErrorException.FloatingPointOverflow);
        }

        return new Real48(ax, bx, dx);
    }

    private static void Shl48(ref ushort ax, ref ushort bx, ref ushort dx)
    {
        dx = (ushort)((dx << 1) | (bx >> 15));
        bx = (ushort)((bx << 1) | (ax >> 15));
        ax = (ushort)(ax << 1);
    }

    /// <summary>Translation of 12CE:107A (BOMB.EXE 0x153FA). Dividend DX:BX:AX, divisor CX:SI:DI (exponent not 0).</summary>
    private static Real48 DivideCore(ushort ax, ushort bx, ushort dx, ushort cx, ushort si, ushort di)
    {
        if ((byte)ax == 0)
        {
            return Zero;
        }

        // DX: sign in bit 15 and dividend exponent, minus the divisor exponent with borrow (1081-1093).
        ushort exponentWord = (ushort)((((dx ^ di) & 0x8000) | (byte)ax) - (byte)cx);
        ushort bp = (ushort)(dx | 0x8000);
        di |= 0x8000;
        byte ah = (byte)(ax >> 8);
        byte ch = (byte)(cx >> 8);

        // Remainder BP:BX:AH, divisor DI:SI:CH; quotient bits are collected inverted (1 = no subtraction).
        var words = new ushort[3];
        int word = 0;
        uint acc = 1; // sentinel
        bool skipCompare = false;
        bool carry = false;
        while (true)
        {
            if (!skipCompare)
            {
                bool less = bp != di ? bp < di : bx != si ? bx < si : ah < ch;
                if (!less)
                {
                    Subtract40(ref ah, ref bx, ref bp, ch, si, di);
                }

                carry = less;
            }

            skipCompare = false;
            acc = (acc << 1) | (carry ? 1u : 0u);
            if ((acc & 0x10000) != 0)
            {
                words[word++] = (ushort)acc;
                if (word == 3)
                {
                    break;
                }

                acc = word == 2 ? 0x40u : 1u;
            }

            // 10B1: shift the remainder left; a bit out of BP means it is larger than the divisor.
            bool outBit = (bp & 0x8000) != 0;
            bp = (ushort)((bp << 1) | (bx >> 15));
            bx = (ushort)((bx << 1) | (ah >> 7));
            ah = (byte)(ah << 1);
            if (outBit)
            {
                Subtract40(ref ah, ref bx, ref bp, ch, si, di);
                carry = false;
                skipCompare = true;
            }
        }

        // 10D0-10EE: third word holds 10 bits; invert, normalise one step, add the bias.
        ushort qa = (ushort)~(ushort)(words[2] << 6);
        ushort qb = (ushort)~words[1];
        ushort qd = (ushort)~words[0];
        if ((qd & 0x8000) == 0)
        {
            Shl48(ref qa, ref qb, ref qd);
            exponentWord--;
        }

        exponentWord = (ushort)(exponentWord + 0x8080);
        return MultiplyDivideTail(qa, qb, qd, exponentWord, normalizeAdjust: 0);
    }

    private static void Subtract40(ref byte ah, ref ushort bx, ref ushort bp, byte ch, ushort si, ushort di)
    {
        int s0 = ah - ch;
        ah = (byte)s0;
        int s1 = bx - si - (s0 < 0 ? 1 : 0);
        bx = (ushort)s1;
        bp = (ushort)(bp - di - (s1 < 0 ? 1 : 0));
    }

    /// <summary>Translation of 12CE:115A (BOMB.EXE 0x154DA).</summary>
    private int ToInteger(bool round)
    {
        byte exponent = (byte)_ax;
        if (exponent > 0x9F)
        {
            throw new RunErrorException(RunErrorException.InvalidFloatingPointOperation);
        }

        int shift = 0x9F - exponent;
        if (shift > 0x1F)
        {
            return 0;
        }

        shift++;
        // The upper 32 mantissa bits DX:BX with the hidden bit; AH is not used.
        uint magnitude = ((uint)(_dx | 0x8000) << 16) | _bx;
        bool half = ((magnitude >> (shift - 1)) & 1) != 0;
        magnitude = shift == 32 ? 0 : magnitude >> shift;
        if (half && round)
        {
            magnitude++;
            if ((magnitude & 0x8000_0000) != 0)
            {
                throw new RunErrorException(RunErrorException.InvalidFloatingPointOperation);
            }
        }

        return IsNegative ? unchecked(-(int)magnitude) : (int)magnitude;
    }
}
