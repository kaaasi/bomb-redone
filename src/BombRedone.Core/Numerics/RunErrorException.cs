namespace BombRedone.Core.Numerics;

/// <summary>
/// Runtime error of the Turbo Pascal 6.0 runtime (RunError). The original program halts with
/// "Runtime error <code>"; the remake raises this exception instead.
/// [ORIGINAL] BOMB.EXE 0x15570 (12CE:11F0): 207 invalid floating point operation (Trunc/Round),
/// 12CE:11F6: 205 floating point overflow (Add/Sub/Mul/Div), 12CE:11FC: 200 division by zero.
/// </summary>
public sealed class RunErrorException(int code)
    : Exception($"Runtime error {code} ({Describe(code)})")
{
    public const int DivisionByZero = 200;
    public const int FloatingPointOverflow = 205;
    public const int InvalidFloatingPointOperation = 207;

    public int Code { get; } = code;

    private static string Describe(int code) => code switch
    {
        DivisionByZero => "division by zero",
        FloatingPointOverflow => "floating point overflow",
        InvalidFloatingPointOperation => "invalid floating point operation",
        _ => "runtime error",
    };
}
