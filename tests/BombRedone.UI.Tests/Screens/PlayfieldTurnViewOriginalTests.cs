using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Setup;
using BombRedone.Core.Turn;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>
/// The whole screen of a turn at every poll against the probe (docs/analyse/zug.md): header fields, tank info window,
/// target bar and MIRV dialog over the field of Core. Expected values: column poll_screens of
/// <c>python3 -I tools/reverse-engineering/zug.py hashes</c> (first 24 hex digits of the SHA-256 of the 64000 screen
/// bytes at each poll site) and the screen at the end. Needs the original files (fonts, item names via the catalog) and
/// the reference 8 x 8 font of DOSBox.
/// </summary>
public sealed class PlayfieldTurnViewOriginalTests
{
    public sealed record TurnScreens(
        int Nr, string Seed, int Players, int Wall, int Background, int WindOption, int Shooter, int Control, string Sets,
        string Steps, string Options, string PollScreens, string EndScreen)
    {
        public override string ToString() => $"turn {Nr}";
    }

    private static readonly TurnScreens[] All =
    [
        new(0, "12345678", 2, 0, 0, 2, 1, 1, "-", "Left;Left;Left;Space", "-", "a19715ff542c1d8dc5002d1b 6859b91ab26d4677b8f5efc6 f40bdcd9381cc82ccf46bec6 b48f77117313655203322f5a", "b48f77117313655203322f5a"),
        new(1, "12345678", 2, 0, 0, 2, 1, 1, "-", "Alt+Left;Alt+Left;Space", "-", "a19715ff542c1d8dc5002d1b 6859b91ab26d4677b8f5efc6 f40bdcd9381cc82ccf46bec6", "f40bdcd9381cc82ccf46bec6"),
        new(2, "12345678", 2, 0, 0, 2, 1, 1, "-", "Right;KpRight;Alt+Right;RAlt+KpRight;Space", "-", "a19715ff542c1d8dc5002d1b f52efcbef69e6e7192c7f16e 34c28e37250e27179b6f1f32 89c7cc5ccfa3f3547a4a13e4 a7a9cbe6a18b162c085a37d4", "a7a9cbe6a18b162c085a37d4"),
        new(3, "12345678", 2, 0, 0, 2, 1, 1, "-", "Down;Down;Alt+Down;Up;Alt+KpUp;KpDown;Space", "-", "a19715ff542c1d8dc5002d1b c7b2d9a33f95d7e2b8a90d37 1a81755f138670b1c67d6235 71b3b9f4e5d331cf9ed1bbdd 1a81755f138670b1c67d6235 c7b2d9a33f95d7e2b8a90d37 1a81755f138670b1c67d6235", "1a81755f138670b1c67d6235"),
        new(4, "12345678", 2, 0, 0, 2, 1, 1, "-", "PgUp;PgUp;PgDn;Alt+KpPgUp;Alt+PgDn;Space", "-", "a19715ff542c1d8dc5002d1b 9a507be636f5a5383e30263d 0ea7ecd87d0cc7b3536d6469 9a507be636f5a5383e30263d 0ea7ecd87d0cc7b3536d6469 9a507be636f5a5383e30263d", "9a507be636f5a5383e30263d"),
        new(5, "12345678", 2, 0, 0, 2, 1, 1, "power=995", "Up;Up;Up;Up;Up;Up;PgUp;Space", "-", "ca8ebdee92a1772842c84244 f8693893b9c15be762d33dbd 71fe5877842822fcfd5f4ed1 9af802fd2ad33b73fc091bc4 96c644766beea1d26cfbe6e1 31c840e37a7ca6c294a913c6 31c840e37a7ca6c294a913c6 31c840e37a7ca6c294a913c6", "31c840e37a7ca6c294a913c6"),
        new(6, "12345678", 2, 0, 0, 2, 1, 1, "power=50", "PgDn;Down;Up;Space", "-", "f0314e3f5cbc111f799c9e23 510d3486f317b448a7d1d624 510d3486f317b448a7d1d624 e535fe5093a612c8a186b904", "e535fe5093a612c8a186b904"),
        new(7, "12345678", 2, 0, 0, 2, 1, 1, "power=2000", "Space", "-", "31c840e37a7ca6c294a913c6", "31c840e37a7ca6c294a913c6"),
        new(8, "12345678", 2, 0, 0, 2, 1, 1, "angle=88 dir=1", "Left;Left;Left;Left;Right;Space", "-", "79ed966c096d117a0a60fe7c a9a4ec91fec06ef1a58c2aef 84a681d13849ab0fca5ef2c6 016501ca0c9ca741e5612023 a1583ff69f9002f134199933 016501ca0c9ca741e5612023", "016501ca0c9ca741e5612023"),
        new(9, "12345678", 2, 0, 0, 2, 1, 1, "angle=1 dir=-1", "Left;Left;Left;Right;Right;Space", "-", "eee42f4e72a447230111a46f 7590cea978846d57865a3531 c810153f1cf2d982c46195ab 1f24aaf05833ff38d35b394a c810153f1cf2d982c46195ab 94fd49586f7991ff5dd4ac45", "94fd49586f7991ff5dd4ac45"),
        new(10, "12345678", 2, 0, 0, 2, 1, 1, "-", "I;I;I;Space", "-", "a19715ff542c1d8dc5002d1b c78e583145de86c01ef2f7b7 a19715ff542c1d8dc5002d1b c78e583145de86c01ef2f7b7", "c78e583145de86c01ef2f7b7"),
        new(11, "12345678", 2, 0, 0, 2, 1, 1, "stock0=5 stock1=5 stock2=5 stock3=5 stock4=5 stock5=5 stock6=5 stock7=5 stock8=5 stock9=5 stock10=5 stock11=5 stock12=5 stock13=5 stock14=5 stock15=5 stock16=5 stock17=5", "Tab;Tab;Tab;LShift+Tab;RShift+Tab;Space", "-", "a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b", "a19715ff542c1d8dc5002d1b"),
        new(12, "12345678", 2, 0, 0, 2, 1, 1, "weapon=17 stock0=5 stock1=5 stock2=5 stock3=5 stock4=5 stock5=5 stock6=5 stock7=5 stock8=5 stock9=5 stock10=5 stock11=5 stock12=5 stock13=5 stock14=5 stock15=5 stock16=5 stock17=5", "Tab;LShift+Tab;LShift+Tab;Space", "-", "a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b", "a19715ff542c1d8dc5002d1b"),
        new(13, "12345678", 2, 0, 0, 2, 1, 1, "weapon=3 stock3=0", "Space", "-", "a19715ff542c1d8dc5002d1b", "a19715ff542c1d8dc5002d1b"),
        new(14, "12345678", 2, 0, 0, 2, 1, 1, "weapon=2 stock2=1 stock4=2", "Tab;Tab;LShift+Tab;Space", "-", "a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b", "a19715ff542c1d8dc5002d1b"),
        new(15, "12345678", 2, 0, 0, 2, 1, 1, "weapon=5 stock5=100", "-;Space", "-", "a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b", "a19715ff542c1d8dc5002d1b"),
        new(16, "12345678", 2, 0, 0, 2, 1, 1, "weapon=5 stock5=150 power=140", "Up;Up;Up;Up;Up;Up;Up;Up;Up;Up;Up;Space", "-", "6cb889454df15087073f3938 0903abcff20a535930209dd3 ae990ff22dd61948c583f238 50fa27ea461df881f0f43a73 0f3e0cde4f2fe3400c2516f7 9b17baf04ed9873708631c2c 66344d69c102dff2b44a55f7 c8d6e2fd63d5ebb853f3d7cf da27de620aa0a88d223a22b7 0c76d3aed4ea3aab6ed74f88 25f30686766ceeef2e5aecee 25f30686766ceeef2e5aecee", "25f30686766ceeef2e5aecee"),
        new(17, "12345678", 2, 0, 0, 2, 1, 1, "herb=1 vick=1", "H;V;H;Space;b:Enter", "-", "a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b 1534f630bbd57d9d47641bea", "a19715ff542c1d8dc5002d1b"),
        new(18, "12345678", 2, 0, 0, 2, 1, 1, "-", "H;V;Space", "-", "a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b", "a19715ff542c1d8dc5002d1b"),
        new(19, "12345678", 2, 0, 0, 2, 1, 1, "weapon=5 stock5=0", "Tab;Space", "-", "a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b", "a19715ff542c1d8dc5002d1b"),
        new(20, "12345678", 2, 0, 0, 2, 1, 1, "-", "KpIns+KpDel;W;G;D;C;Space;Space", "-", "a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b 2cf68fa62ab53f8c9d3b02a8 2cf68fa62ab53f8c9d3b02a8", "2cf68fa62ab53f8c9d3b02a8"),
        new(21, "12345678", 2, 0, 0, 2, 1, 1, "-", "KpIns+KpDel;W;KpIns+KpDel;Space", "-", "a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b", "a19715ff542c1d8dc5002d1b"),
        new(22, "12345678", 2, 0, 0, 2, 1, 1, "-", "Ins+Del;Space", "-", "a19715ff542c1d8dc5002d1b a19715ff542c1d8dc5002d1b", "a19715ff542c1d8dc5002d1b"),
        new(23, "12345678", 2, 0, 0, 2, 1, 1, "-", "1;b:Space;2;b:Enter;Space", "-", "a19715ff542c1d8dc5002d1b d82a1eccc9e9d3ef7c599815 a19715ff542c1d8dc5002d1b 6e0b82ad7a58b9709f32d373 a19715ff542c1d8dc5002d1b", "a19715ff542c1d8dc5002d1b"),
        new(24, "9E3779B9", 4, 2, 2, 1, 3, 1, "herb=3 vick=2 stock0=5 stock1=5 stock2=5 stock3=5 stock4=5 stock5=5 stock6=5 stock7=5 stock8=5 stock9=5 stock10=5 stock11=5 stock12=5 stock13=5 stock14=5 stock15=5 stock16=5 stock17=5", "3;b:Space;Space", "-", "776eae0f257b2e6368fafe74 42d14f5752a03636b70f4752 776eae0f257b2e6368fafe74", "776eae0f257b2e6368fafe74"),
        new(25, "9E3779B9", 4, 2, 2, 1, 3, 1, "2:alive=0 4:shield=120", "2;b:Space;4;b:Space;Space", "-", "776eae0f257b2e6368fafe74 647d89c54090e1b5777a90d3 776eae0f257b2e6368fafe74 6005cae982018f87d8a25fb1 776eae0f257b2e6368fafe74", "776eae0f257b2e6368fafe74"),
        new(26, "9E3779B9", 4, 2, 2, 1, 3, 1, "-", "0;b:Space;Space", "-", "776eae0f257b2e6368fafe74 776eae0f257b2e6368fafe74 776eae0f257b2e6368fafe74", "776eae0f257b2e6368fafe74"),
        new(27, "9E3779B9", 4, 2, 2, 1, 3, 1, "herb=2", "H;Space;b:Right;b:Right;b:Enter", "-", "776eae0f257b2e6368fafe74 776eae0f257b2e6368fafe74 97cb4ae2bc324c1460d440ba 32dfabc802e115222e70f196 612b57b8cf23658a77370261", "776eae0f257b2e6368fafe74"),
        new(28, "9E3779B9", 4, 2, 2, 1, 3, 1, "vick=2 4:alive=0", "V;Enter;b:Left;b:Left;b:Left;b:X;b:Space", "-", "776eae0f257b2e6368fafe74 776eae0f257b2e6368fafe74 42f2eb2f9da8cc2e7fddd7b4 a6d1c9382ab904b0ea3d4f35 42f2eb2f9da8cc2e7fddd7b4 a6d1c9382ab904b0ea3d4f35 a6d1c9382ab904b0ea3d4f35", "776eae0f257b2e6368fafe74"),
        new(29, "9E3779B9", 4, 2, 2, 1, 3, 1, "herb=2 vick=2", "H;V;KpEnter;b:Right;b:Enter", "-", "776eae0f257b2e6368fafe74 776eae0f257b2e6368fafe74 776eae0f257b2e6368fafe74 2082650a9af255ae510fe21e 00d097740d6fa80ecd710215", "776eae0f257b2e6368fafe74"),
        new(30, "9E3779B9", 4, 2, 2, 1, 3, 1, "weapon=7 stock7=3", "Space;Down;Down;Enter;Down;Space", "mirv", "776eae0f257b2e6368fafe74 c9454542d66e9d4a61c9c8a0 c9454542d66e9d4a61c9c8a0 c9454542d66e9d4a61c9c8a0 2e2a49a51d0f3b97a2a09d04 2e2a49a51d0f3b97a2a09d04", "2e2a49a51d0f3b97a2a09d04"),
        new(31, "9E3779B9", 4, 2, 2, 1, 3, 1, "weapon=7 stock7=3", "Space;Down;Down;Down;Down;Down;Up;Space;Down;Down;Down;Enter", "mirv", "776eae0f257b2e6368fafe74 c9454542d66e9d4a61c9c8a0 c9454542d66e9d4a61c9c8a0 c9454542d66e9d4a61c9c8a0 c9454542d66e9d4a61c9c8a0 c9454542d66e9d4a61c9c8a0 c9454542d66e9d4a61c9c8a0 c9454542d66e9d4a61c9c8a0 2e2a49a51d0f3b97a2a09d04 2e2a49a51d0f3b97a2a09d04 2e2a49a51d0f3b97a2a09d04 2e2a49a51d0f3b97a2a09d04", "2e2a49a51d0f3b97a2a09d04"),
        new(32, "9E3779B9", 4, 2, 2, 1, 3, 1, "weapon=7 stock7=3", "Space;Down;Enter;Esc;Up;Enter;Enter", "mirv", "776eae0f257b2e6368fafe74 c9454542d66e9d4a61c9c8a0 c9454542d66e9d4a61c9c8a0 2e2a49a51d0f3b97a2a09d04 c9454542d66e9d4a61c9c8a0 c9454542d66e9d4a61c9c8a0 2e2a49a51d0f3b97a2a09d04", "2e2a49a51d0f3b97a2a09d04"),
        new(33, "9E3779B9", 4, 2, 2, 1, 3, 2, "-", "m:60,4,1;m:60,4,2;m:70,0,1;m:71,0,1;m:60,12,1;m:86,17,2;m:56,10,1;m:20,12,1;m:54,17,2;m:30,9,1;m:299,4,1;m:0,0,3", "-", "776eae0f257b2e6368fafe74 5af0373dbd34b44bcf3929bb a3583ddd6c31e667a5fc8fa5 5af0373dbd34b44bcf3929bb 5af0373dbd34b44bcf3929bb f92f34788b2973962a12165c 5af0373dbd34b44bcf3929bb f92f34788b2973962a12165c 3cd204ee57fe04817c2c85cd f92f34788b2973962a12165c f92f34788b2973962a12165c 0d0b2b70949bcc26ab877637", "0d0b2b70949bcc26ab877637"),
        new(34, "9E3779B9", 4, 2, 2, 1, 3, 2, "stock0=5 stock1=5 stock2=5 stock3=5 stock4=5 stock5=5 stock6=5 stock7=5 stock8=5 stock9=5 stock10=5 stock11=5 stock12=5 stock13=5 stock14=5 stock15=5 stock16=5 stock17=5", "m:10,18,1;m:100,27,2;m:279,20,2;m:280,20,1;m:9,20,1;m:150,28,1;m:0,0,3", "-", "776eae0f257b2e6368fafe74 776eae0f257b2e6368fafe74 776eae0f257b2e6368fafe74 776eae0f257b2e6368fafe74 776eae0f257b2e6368fafe74 776eae0f257b2e6368fafe74 776eae0f257b2e6368fafe74", "776eae0f257b2e6368fafe74"),
        new(35, "9E3779B9", 4, 2, 2, 1, 3, 2, "herb=2 vick=2", "m:280,20,1;m:295,20,1;m:300,22,1;m:0,0,3;m:121,142,1", "-", "776eae0f257b2e6368fafe74 776eae0f257b2e6368fafe74 776eae0f257b2e6368fafe74 776eae0f257b2e6368fafe74 0b4b843369687f6a5d682fd4", "776eae0f257b2e6368fafe74"),
        new(36, "9E3779B9", 4, 2, 2, 1, 3, 2, "-", "m:121,142,1;m:0,0,0;m:0,0,1;m:297,151,2;m:5,5,2;m:0,0,3", "-", "776eae0f257b2e6368fafe74 647d89c54090e1b5777a90d3 647d89c54090e1b5777a90d3 776eae0f257b2e6368fafe74 cde452325b1d6b001cebd1cd 776eae0f257b2e6368fafe74", "776eae0f257b2e6368fafe74"),
        new(37, "9E3779B9", 4, 2, 2, 1, 3, 2, "herb=2", "m:280,20,1;m:0,0,3;m:5,5,1;m:209,164,1;m:150,100,0;m:121,142,1", "-", "776eae0f257b2e6368fafe74 776eae0f257b2e6368fafe74 0b4b843369687f6a5d682fd4 0b4b843369687f6a5d682fd4", "776eae0f257b2e6368fafe74"),
        new(38, "9E3779B9", 4, 2, 2, 1, 3, 2, "weapon=7 stock7=3", "m:0,0,3;m:5,90,0;m:5,100,0;m:5,130,1;m:5,100,0;m:5,92,2", "mirv", "776eae0f257b2e6368fafe74 c9454542d66e9d4a61c9c8a0 c9454542d66e9d4a61c9c8a0 c9454542d66e9d4a61c9c8a0 2e2a49a51d0f3b97a2a09d04 2e2a49a51d0f3b97a2a09d04", "2e2a49a51d0f3b97a2a09d04"),
        new(39, "12345678", 2, 0, 0, 2, 1, 1, "-", "Left;Alt+Right;I;Space", "sound", "a19715ff542c1d8dc5002d1b 6859b91ab26d4677b8f5efc6 a19715ff542c1d8dc5002d1b c78e583145de86c01ef2f7b7", "c78e583145de86c01ef2f7b7"),
    ];

    public static TheoryData<TurnScreens> Cases
    {
        get
        {
            var data = new TheoryData<TurnScreens>();
            foreach (var c in All)
                data.Add(c);
            return data;
        }
    }

    [GameDataTheory]
    [MemberData(nameof(Cases))]
    public void Screen_at_every_poll_matches_the_probe(TurnScreens c)
    {
        var (polls, end) = PlayfieldTurnHarness.Run(c);
        Assert.Equal(c.PollScreens, polls);

        // The probe stops the MIRV cases before the dialog restores the screen (O4:1AB9).
        if (!c.Options.Contains("mirv", StringComparison.Ordinal))
            Assert.Equal(c.EndScreen, end);
    }
}

/// <summary>
/// Drives Core's <see cref="TurnSession"/> with <see cref="PlayfieldTurnView"/> and the input model of the probe
/// (docs/analyse/zug.md section 2; the same steps as TurnProbeTests in BombRedone.Core.Tests).
/// </summary>
internal static class PlayfieldTurnHarness
{
    private static readonly Dictionary<string, int> Keys = new()
    {
        ["Esc"] = Scancode.Esc, ["0"] = Scancode.Digit0, ["Tab"] = Scancode.Tab, ["W"] = Scancode.W,
        ["Enter"] = Scancode.Enter, ["D"] = Scancode.D, ["G"] = Scancode.G, ["H"] = Scancode.H, ["I"] = Scancode.I,
        ["LShift"] = Scancode.LeftShift, ["C"] = Scancode.C, ["V"] = Scancode.V, ["RShift"] = Scancode.RightShift,
        ["Alt"] = Scancode.Alt, ["Space"] = Scancode.Space, ["KpUp"] = Scancode.PadUp, ["KpPgUp"] = Scancode.PadPageUp,
        ["KpLeft"] = Scancode.PadLeft, ["KpRight"] = Scancode.PadRight, ["KpDown"] = Scancode.PadDown,
        ["KpPgDn"] = Scancode.PadPageDown, ["KpIns"] = Scancode.PadInsert, ["KpDel"] = Scancode.PadDelete,
        ["KpEnter"] = Scancode.PadEnter, ["RAlt"] = Scancode.RightAlt, ["Up"] = Scancode.Up, ["PgUp"] = Scancode.PageUp,
        ["Left"] = Scancode.Left, ["Right"] = Scancode.Right, ["Down"] = Scancode.Down, ["PgDn"] = Scancode.PageDown,
        ["Ins"] = Scancode.Insert, ["Del"] = Scancode.Delete,
    };

    private static readonly Dictionary<string, (byte Ascii, byte Scan)> Bios = new()
    {
        ["Enter"] = (13, 0x1C), ["Space"] = (32, 0x39), ["Esc"] = (27, 0x01), ["Right"] = (0, 0x4D), ["Left"] = (0, 0x4B),
        ["Up"] = (0, 0x48), ["Down"] = (0, 0x50), ["A"] = ((byte)'a', 0x1E), ["X"] = ((byte)'x', 0x2D),
    };

    private static MenuFonts? _fonts;

    static PlayfieldTurnHarness()
    {
        for (var n = 1; n <= 9; n++)
            Keys[n.ToString(CultureInfo.InvariantCulture)] = Scancode.Digit1 + n - 1;
    }

    private static MenuFonts Fonts()
    {
        if (_fonts is not null)
            return _fonts;
        var fontPath = BitmapFontSource.FindReference(BitmapFontSource.DefaultSearchDirectories())
            ?? throw new InvalidOperationException(
                "Reference font missing (tools/claude/fetch-reference.sh fetches schriften/ to ~/referenz).");
        _fonts = MenuFonts.Load(TestGameData.Load(), BitmapFontSource.ReadReference(fontPath));
        return _fonts;
    }

    private static string Sha24(byte[] pixels) => Convert.ToHexStringLower(SHA256.HashData(pixels))[..24];

    public static (string Polls, string End) Run(PlayfieldTurnViewOriginalTests.TurnScreens c)
    {
        var random = new TurboPascalRandom(Convert.ToUInt32(c.Seed, 16));
        var field = Playfield.Build(new PlayfieldOptions(c.Players, c.Wall, c.Background, 0xFFFF, c.WindOption, false),
            random);
        var wind = Wind.Choose(c.WindOption, random);
        StartPlayer.Choose(new int[c.Players], Enumerable.Repeat(true, c.Players).ToArray(), random);
        var names = Enumerable.Range(1, c.Players).Select(n => "Player " + n).ToArray();
        var screen = new PlayfieldScreen(Fonts(), field, wind, names);
        var view = new PlayfieldTurnView(screen, Fonts(), wind);

        var players = field.Tanks.Select(t => new TurnPlayer(t)).ToList();
        foreach (var p in players)
            p.SetDefaultStocks();
        foreach (var set in c.Sets == "-" ? [] : c.Sets.Split(' '))
            Apply(players, c.Shooter, set);

        var control = (PlayerControl)c.Control;
        var input = new TurnInput { MousePresent = control == PlayerControl.Mouse, MouseX = 160, MouseY = 100 };
        var session = new TurnSession(field, players, input,
            new TurnSettings(Sound: c.Options.Contains("sound", StringComparison.Ordinal)), view);
        var steps = c.Steps.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var used = 0;
        var polls = new List<string>();

        session.Begin(c.Shooter);
        var waits = session.Play(c.Shooter, control);
        if (c.Options.Contains("mirv", StringComparison.Ordinal))
            waits = waits.Concat(session.MirvSetup(c.Shooter, control));
        foreach (var w in waits)
        {
            if (w.Kind != TurnWaitKind.Poll)
                continue;
            view.Refresh();
            polls.Add(Sha24(screen.Screen.Pixels));
            if (used >= steps.Length)
                break;
            ApplyStep(input, steps[used++]);
        }

        view.Refresh();
        return (string.Join(" ", polls), Sha24(screen.Screen.Pixels));
    }

    private static void ApplyStep(TurnInput input, string step)
    {
        input.ReleaseAll();
        var mouse = false;
        foreach (var part in step.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "-")
                continue;
            if (part.StartsWith("m:", StringComparison.Ordinal))
            {
                var v = part[2..].Split(',').Select(int.Parse).ToArray();
                (input.MouseX, input.MouseY, input.MouseButtons) = (v[0], v[1], v[2]);
                mouse = true;
            }
            else if (part.StartsWith("b:", StringComparison.Ordinal))
            {
                var (a, s) = Bios[part[2..]];
                input.TypeKey(a, s);
            }
            else
            {
                foreach (var key in part.Split('+'))
                    input[Keys[key]] = true;
            }
        }

        if (!mouse)
            input.MouseButtons = 0;
    }

    private static void Apply(List<TurnPlayer> players, int shooter, string set)
    {
        var colon = set.IndexOf(':', StringComparison.Ordinal);
        var who = colon >= 0 ? int.Parse(set[..colon], CultureInfo.InvariantCulture) : shooter;
        var rest = colon >= 0 ? set[(colon + 1)..] : set;
        var eq = rest.IndexOf('=', StringComparison.Ordinal);
        var name = rest[..eq];
        var value = int.Parse(rest[(eq + 1)..], CultureInfo.InvariantCulture);
        var p = players[who - 1];
        var t = p.Tank;
        switch (name)
        {
            case "power": SetInternal(t, nameof(t.Power), value); break;
            case "angle": SetInternal(t, nameof(t.Angle), value); break;
            case "dir": SetInternal(t, nameof(t.Direction), value); break;
            case "energy": SetInternal(t, nameof(t.Energy), value); break;
            case "alive": SetInternal(t, nameof(t.Active), value != 0); break;
            case "weapon": t.Weapon = value; break;
            case "herb": t.Herbs = unchecked((short)value); break;
            case "vick": t.Vicks = unchecked((short)value); break;
            case "shield": t.Defense[0] = value; break;
            default: t.Stock[int.Parse(name[5..], CultureInfo.InvariantCulture)] = unchecked((short)value); break;
        }
    }

    private static void SetInternal(PlayfieldTank tank, string property, object value) =>
        typeof(PlayfieldTank).GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!
            .GetSetMethod(nonPublic: true)!.Invoke(tank, [value]);
}
