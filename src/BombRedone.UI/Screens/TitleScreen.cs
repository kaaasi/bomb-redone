using BombRedone.Formats;

namespace BombRedone.UI.Screens;

/// <summary>
/// Title sequence of the original, procedure O1:0014 (BOMB.OVR 0x0001C; docs/analyse/re-grundlagen.md section 6,
/// requirements in docs/anforderungen/titel.md).
/// </summary>
/// <remarks>
/// Course [ORIGINAL, BOMB.OVR offsets]:
/// <list type="number">
/// <item>Without BOMB2.RAW nothing is shown and the sequence ends at once (0x002EF jumps to the end at 0x00501).</item>
/// <item>The palette part of BOMB1 is zeroed (0x002F9), the DAC set to it (0x0031F), the BOMB1 pixels copied to the screen
/// (0x00333) and the DAC set to the palette of BOMB2 (0x00349): BOMB1 appears with the colours of BOMB2. Without BOMB1.RAW
/// the base image is all index 0 (0x0029A).</item>
/// <item>Reveal: for colour 135..154, 16..20, 115..124 and then the pairs (155, 175)..(164, 184) every pixel of BOMB2 with
/// that index is copied over the screen (0x00369, 0x0039F, 0x003D4, 0x00409). Before the first pass and after every step
/// (0x0035D ff., 0x00458) holding both Shift keys or both mouse buttons ends the sequence.</item>
/// <item>The BIOS keyboard buffer is emptied (0x00464): keys pressed during the reveal do not count.</item>
/// <item>Colour cycling: step i = 1..20 (after 20 back to 1, 0x00484) sets DAC 135..154 from entries i-1..i+18 of a 40-entry
/// red table (0x00491), waits Delay(60) (0x004B2) and ends on a key (0x004BB) or the left or right mouse button
/// (0x004C9, 0x004D6).</item>
/// <item>At the end the DAC is restored to the palette saved at the start (0x00501); the screen content stays.</item>
/// </list>
/// </remarks>
public sealed class TitleScreen : IScreen
{
    /// <summary>
    /// [APPROX] Duration of one overlay pass. The original reveal has no delay; each pass is a loop over 64,000 bytes with
    /// four instructions per byte (BOMB.EXE 0x05E34), i.e. about 256,000 instructions, at DOSBox <c>cycles=3000</c> (R1-D1)
    /// about 85 ms. To be checked against a DOSBox recording (the longplay shows the lettering appear within roughly
    /// 4 seconds, LP 00000-00003).
    /// </summary>
    public static readonly TimeSpan OverlayPassDuration = TimeSpan.FromMilliseconds(85);

    /// <summary>[ORIGINAL] BOMB.OVR 0x004B2: Delay(60) per cycling step (milliseconds, re-grundlagen section 5).</summary>
    public static readonly TimeSpan CycleStepDuration = TimeSpan.FromMilliseconds(60);

    /// <summary>[ORIGINAL] BOMB.OVR 0x00491: first DAC entry of the colour cycling.</summary>
    public const int CycleFirstColor = 135;

    /// <summary>[ORIGINAL] BOMB.OVR 0x00491: number of DAC entries set per cycling step.</summary>
    public const int CycleColorCount = 20;

    /// <summary>[ORIGINAL] BOMB.OVR 0x00484: the step counter runs 1..20.</summary>
    public const int CycleSteps = 20;

    /// <summary>[ORIGINAL] BOMB.OVR 0x00041: the cycling table has 0x78 bytes = 40 entries.</summary>
    public const int CycleTableEntries = 40;

    /// <summary>
    /// [ORIGINAL] The palette routines take the number of entries as a byte, and the title passes 255 for the full palette
    /// (BOMB.OVR 0x0031F, 0x00349, 0x00501; BOMB.EXE 0x0EFB9 multiplies the byte by 3), so DAC entry 255 is never set here.
    /// </summary>
    public const int FullPaletteCount = 255;

    /// <summary>[APPROX] The VGA DAC keeps only the low 6 bits of each value written (hardware, not from the original files).</summary>
    private const byte DacMask = 0x3F;

    private const int PixelCount = RawImage.PixelCount;

    /// <summary>[ORIGINAL] Colour order of the reveal, see class remarks; inner arrays are the passes of one step.</summary>
    private static readonly int[][] RevealSteps =
    [
        .. Enumerable.Range(135, 20).Select(c => new[] { c }),
        .. Enumerable.Range(16, 5).Select(c => new[] { c }),
        .. Enumerable.Range(115, 10).Select(c => new[] { c }),
        .. Enumerable.Range(155, 10).Select(c => new[] { c, c + 20 }),
    ];

    private readonly byte[] _pixels = new byte[PixelCount];
    private readonly byte[] _dac = new byte[Palette.ByteLength];
    private readonly byte[] _savedDac;
    private readonly byte[] _overlay = new byte[PixelCount];
    private readonly byte[] _cycleTable = BuildCycleTable();
    private readonly Lazy<IScreen?> _next;

    private TitlePhase _phase;
    private bool _firstCheckDone;
    private int _revealStep;
    private int _cycleStep;
    private bool _keyBuffered;
    private TimeSpan _clock;

    /// <param name="images">The title files; see <see cref="TitleImages"/>.</param>
    /// <param name="next">Creates the screen that follows the title.</param>
    /// <param name="previousDac">
    /// The DAC (768 values of 6 bits) before the title, restored at its end. [APPROX] In the original this is the palette the
    /// main program set up before (BOMB.EXE 0x0EF40 reads it); default all black.
    /// </param>
    public TitleScreen(TitleImages images, Func<IScreen?> next, ReadOnlySpan<byte> previousDac = default)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(next);
        if (!previousDac.IsEmpty && previousDac.Length != Palette.ByteLength)
            throw new ArgumentException($"DAC needs {Palette.ByteLength} values.", nameof(previousDac));
        _next = new Lazy<IScreen?>(next);
        previousDac.CopyTo(_dac);
        _savedDac = (byte[])_dac.Clone();

        if (images.Second is null)
        {
            _phase = TitlePhase.Finished;
            return;
        }

        images.Second.AsSpan(0, PixelCount).CopyTo(_overlay);
        // The palette of BOMB1 is zeroed and set first; the pixels then appear with the palette of BOMB2.
        images.First?.AsSpan(0, PixelCount).CopyTo(_pixels);
        SetDac(0, FullPaletteCount, new byte[Palette.ByteLength]);
        SetDac(0, FullPaletteCount, images.Second.AsSpan(PixelCount));
        _phase = TitlePhase.Reveal;
    }

    /// <summary>Current part of the sequence.</summary>
    public TitlePhase Phase => _phase;

    /// <summary>Number of reveal steps done (0..<see cref="RevealStepCount"/>).</summary>
    public int RevealStepsDone => _revealStep;

    /// <summary>Number of reveal steps (45: 35 single colours and 10 pairs).</summary>
    public static int RevealStepCount => RevealSteps.Length;

    /// <summary>Overlay passes of the whole reveal (55).</summary>
    public static int RevealPassCount => RevealSteps.Sum(s => s.Length);

    /// <summary>Colours copied in reveal step <paramref name="step"/> (0-based).</summary>
    public static IReadOnlyList<int> RevealColors(int step) => RevealSteps[step];

    /// <summary>Current cycling step 1..20 (0 before the cycling starts).</summary>
    public int CycleStep => _cycleStep;

    /// <summary>The DAC as the sequence has set it (768 values of 6 bits, copy).</summary>
    public byte[] GetDac() => (byte[])_dac.Clone();

    /// <summary>
    /// The 40-entry cycling table (R, G, B of 6 bits as computed, before the DAC mask).
    /// [ORIGINAL] BOMB.OVR 0x000AB-0x00108: entry e has red = Trunc(((e + 1) mod 20) * 6.3) with 6.3 as the Real48 constant
    /// 83 9A 99 99 99 49; green and blue are 0 (FillChar). The values above 63 come from the original (mod 20 with factor
    /// 6.3); two further tables with mod 10 in blue (0x0013F) and green (0x001B1) are computed but never used.
    /// </summary>
    public static byte[] BuildCycleTable()
    {
        // Real48 83 9A 99 99 99 49: mantissa 0xC99999999A (hidden bit set) times 2^(0x83 - 129 - 39) = M / 2^37.
        // The product with a small integer is exact here, so Trunc is an integer shift.
        const long mantissa = 0xC99999999AL;
        var table = new byte[CycleTableEntries * 3];
        for (var i = 1; i <= CycleTableEntries; i++)
            table[(i - 1) * 3] = (byte)(((i % 20) * mantissa) >> 37);
        return table;
    }

    public IScreen? Update(TimeSpan elapsed, ScreenInput input)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Time cannot run backwards.");
        if (_phase == TitlePhase.Finished)
            return _next.Value;

        _clock += elapsed;
        if (_phase == TitlePhase.Reveal)
        {
            if (!_firstCheckDone)
            {
                _firstCheckDone = true;
                if (RevealAborted(input))
                    return Finish();
            }

            while (_revealStep < RevealSteps.Length)
            {
                var duration = OverlayPassDuration * RevealSteps[_revealStep].Length;
                if (_clock < duration)
                    return this;
                _clock -= duration;
                foreach (var color in RevealSteps[_revealStep])
                    Overlay((byte)color);
                _revealStep++;
                if (RevealAborted(input))
                    return Finish();
            }

            // 0x00458: one more check right after the last step, then the keyboard buffer is emptied (0x00464).
            if (RevealAborted(input))
                return Finish();
            // [APPROX] Key presses reported with the update that ends the reveal are counted as before the flush.
            _phase = TitlePhase.Cycle;
            _keyBuffered = false;
            NextCycleStep();
        }
        else
        {
            _keyBuffered |= input.KeyPressed;
        }

        while (_clock >= CycleStepDuration)
        {
            _clock -= CycleStepDuration;
            if (_keyBuffered || input.MouseHolds(MouseButtons.Left) || input.MouseHolds(MouseButtons.Right))
                return Finish();
            NextCycleStep();
        }
        return this;
    }

    public void Draw(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (surface.Width != RawImage.Width || surface.Height != RawImage.Height)
            throw new ArgumentException(
                $"The title needs a {RawImage.Width} x {RawImage.Height} surface, got {surface.Width} x {surface.Height}.",
                nameof(surface));
        _pixels.CopyTo(surface.Pixels, 0);
        for (var i = 0; i < Palette.Size; i++)
        {
            var r = Palette.Widen(_dac[i * 3]);
            var g = Palette.Widen(_dac[i * 3 + 1]);
            var b = Palette.Widen(_dac[i * 3 + 2]);
            surface.Palette[i] = 0xFF000000u | ((uint)b << 16) | ((uint)g << 8) | r;
        }
    }

    /// <summary>[ORIGINAL] BOMB.EXE 0x06379 (U431:09C9): both Shift keys (BIOS flags and 3 = 3) or mouse buttons 1 and 2.</summary>
    private static bool RevealAborted(ScreenInput input) =>
        input.BothShiftsDown || input.MouseHolds(MouseButtons.Left | MouseButtons.Right);

    /// <summary>
    /// [ORIGINAL] BOMB.EXE 0x05E20 (U431:0470): copies every byte equal to <paramref name="color"/> from the source to the
    /// screen, with the index running from 0xFA00 down to 1. Offset 0 (pixel 0, 0) is therefore never copied; offset 0xFA00
    /// lies outside the visible 64,000 bytes. [APPROX] Assumes both heap buffers start at offset 0 (the routine uses only
    /// their segments).
    /// </summary>
    private void Overlay(byte color)
    {
        for (var i = PixelCount - 1; i >= 1; i--)
        {
            if (_overlay[i] == color)
                _pixels[i] = color;
        }
    }

    private void NextCycleStep()
    {
        _cycleStep = _cycleStep % CycleSteps + 1;
        SetDac(CycleFirstColor, CycleColorCount, _cycleTable.AsSpan((_cycleStep - 1) * 3));
    }

    private IScreen? Finish()
    {
        _savedDac.AsSpan(0, FullPaletteCount * 3).CopyTo(_dac);
        _phase = TitlePhase.Finished;
        return _next.Value;
    }

    private void SetDac(int first, int count, ReadOnlySpan<byte> values)
    {
        for (var i = 0; i < count * 3; i++)
            _dac[first * 3 + i] = (byte)(values[i] & DacMask);
    }
}

/// <summary>Parts of the title sequence.</summary>
public enum TitlePhase
{
    /// <summary>BOMB2 colours are copied over the base image step by step.</summary>
    Reveal,

    /// <summary>Colours 135..154 cycle until a key or mouse button.</summary>
    Cycle,

    /// <summary>Done (or skipped without BOMB2.RAW).</summary>
    Finished,
}
