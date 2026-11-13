using System.Security.Cryptography;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>
/// Playfield screen against the original code run in the probe (docs/analyse/feld.md section 2): whole screen at A (with
/// wall label and name) and at K (header and weapon bar), written DAC entries at K. Expected SHA-256 from
/// <c>python3 -I tools/reverse-engineering/feld.py hashes</c> (columns screen at o8 and header, dac_set at header).
/// Needs the original files (SmallFont) and the reference 8 x 8 font of DOSBox (DefaultFont of the probe).
/// </summary>
public sealed class PlayfieldScreenOriginalTests
{
    public sealed record ProbeCase(string Seed, int Players, int Wall, int Background, int Mask, int Wind, bool Invisible,
        string ScreenAtA, string ScreenAtK, string DacAtK)
    {
        public override string ToString() => $"seed {Seed}, {Players} players, wall {Wall}, background {Background}";
    }

    public static readonly IEnumerable<object[]> Cases = new ProbeCase[]
    {
        new("12345678", 2, 0, 0, 0xFFFF, 2, false, "8c97c63ad67398ba15d0b6d87a237123a1759dd86e548ddc48c279f0a50d23f1", "18a579ffacda86a3a6e8c5c35b40877f8ae72351ee1d6473d83411d539826164", "9a825c4b8868b3f5d7805d08da6ace9c2107f3ce982b549ebd613f2bbcb338ee"),
        new("00000001", 3, 1, 1, 0xFFFF, 0, false, "90707e26a846d19b165627364f1536ad13094066604be9764a1cfaef7de9d462", "c132681043b1c73f287be0f4282b3b500cc1af55885c960f00f1a07addc193f6", "ef04d8d8160eafc074fe1a371d98b41c414713f6b2beedc78421e91b58ac331a"),
        new("9E3779B9", 4, 2, 2, 0xFFFF, 1, false, "527547f58f6c71bb0fa6b39c55b414c540ab8b2a14737c3bb1a731732fefa91f", "b97f2a4c91dfda960a9078d60c481ddf254552e2ec06d8482bdafbbf49688740", "76ca0e5ab75b1e2ab32bb30f10b3799fe07885e41a830ece58b7d6714f34e701"),
        new("DEADBEEF", 5, 3, 3, 0xFFFF, 3, false, "7df9f94a799565b6a72e864146c426f120c9063439ba6388659ddc6a261b57fc", "564bd208ad155dfa9101e83262cc00919fa67121fb0cda9604dd1b621f49186b", "a7c38eef91c020182b31c5b2fbfa996ac8da08d51c4c5c4dfe0173f5de7d8f63"),
        new("0BADF00D", 6, 4, 4, 0xFFFF, 4, false, "836a8f9367fd4a7cc009182c7c9263eb1c56f8f402625ae49c6f48f3f0b6757d", "0a1bd2d3f68b8d4189030c466400470625b6c433fd5eb8abcc70af8141a807b7", "3f8a05404b7b599fa262241a0a6b7e140f80a99d0980876c32cbd7da8c6b63b7"),
        new("7FFFFFFF", 7, 5, 5, 0xFFFF, 5, false, "70a6023dd10831fa60a82196d3fa7f1053b98ecb0bc27001964ed80ffe6545e2", "da1d3be8458f7cafa518d90336d4692bbadf55d7fef46ccf73322defc9b50751", "5da6c1fdcc580aeb7f05a6c00402ae3bb3cfc035c50ef218051d0963671c23c3"),
        new("80000000", 8, 1, 6, 0xFFFF, 2, true, "3aa80be60712e2b5623ad184383e899fcff4220c1bc854af5d0085167d5835c6", "5f547471016174f9d3e77d03d92526c0597d1088a59aca48eb71de22724a6e8b", "598d6490d0b86405d8c0392e7f679a04ab1961c63f5728cd94c46f7ae1884240"),
        new("CAFEBABE", 9, 2, 7, 0xFFFF, 2, false, "f64efbf882e4f2fcf1575ce5ac136dbee0f522feca45fb2fafd2deecd763f186", "d6075e6e12436adcafc8053152f589bb24f9ca8a7dd8f8c72fbbbb9dfeea5b3f", "db74582cd293bf626d2c416270816050427cbf15b5e5412cb8d489dddb902584"),
        new("13579BDF", 10, 3, 8, 0xFFFF, 4, false, "5c0384e9fc89c0188a2cae6cfb4d8f78c511866640ca664159024e30b13e2a69", "3f960e5a40bf2b8c86773abeae3588882be2f06daa0dd2bd3ba532254be5ca38", "9fc264772c67d2a4b289d55c25268e990e0d07d4c4680c8cdd652040373acecd"),
        new("2468ACE0", 2, 5, 9, 0xFFFF, 5, false, "5465a870a7dbaf4277ad5a560bf8bb40c59ee1f1d0279b65dd338ce4e6299e7f", "4c29402d003eae4a0976f8dce01978a3eb1e9256e86abfc6207998c10889d0f6", "2718b522264ac023899668b03854478c51627336a6862a3a633ee23004bd11bb"),
        new("FEDCBA98", 3, 5, 10, 0x0124, 5, false, "a1895085062d31029361bb15079dee41d19ce6d8229892287b709f0af1af5ed3", "d21182804cb2f1a6ace199ed89d580a488be4df31d31947c8f17244e3d18d5b2", "bbd79bd4f2a13c8de26831e61721efc74fad2184115d4a04fef19d3ffa3ebd9f"),
        new("00015446", 2, 0, 0, 0xFFFF, 4, false, "a44cec86de7b98f5a8015bdcf3552ad52112c339167aa46188e501228239f69f", "f078247e4b0ae3859a24c16eccde9db8c4fe71dc85572028d8e93226754b177e", "9a825c4b8868b3f5d7805d08da6ace9c2107f3ce982b549ebd613f2bbcb338ee"),
        new("002B423B", 2, 0, 0, 0xFFFF, 4, false, "2c2898c7f7466846cda363d144a5877aa7911c4631222c3652ccbf58a1c363cf", "708299987c4e06bc2111ba40e10e078bef3d8fe68208e08e11ff9133854b0f06", "9a825c4b8868b3f5d7805d08da6ace9c2107f3ce982b549ebd613f2bbcb338ee"),
    }.Select(c => new object[] { c });

    private static PlayfieldScreen Create(ProbeCase c)
    {
        var fontPath = BitmapFontSource.FindReference(BitmapFontSource.DefaultSearchDirectories())
            ?? throw new InvalidOperationException(
                "Reference font missing (tools/claude/fetch-reference.sh fetches schriften/ to ~/referenz).");
        var fonts = MenuFonts.Load(TestGameData.Load(), BitmapFontSource.ReadReference(fontPath));
        var random = new TurboPascalRandom(Convert.ToUInt32(c.Seed, 16));
        var field = Playfield.Build(new PlayfieldOptions(c.Players, c.Wall, c.Background, c.Mask, c.Wind, c.Invisible), random);
        var wind = Wind.Choose(c.Wind, random);
        var names = Enumerable.Range(1, c.Players).Select(n => "Player " + n).ToArray();
        return new PlayfieldScreen(fonts, field, wind, names);
    }

    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    [GameDataTheory]
    [MemberData(nameof(Cases))]
    public void Screen_after_the_setup_matches_the_original(ProbeCase c) =>
        Assert.Equal(c.ScreenAtA, Sha(Create(c).AfterSetup.Pixels));

    [GameDataTheory]
    [MemberData(nameof(Cases))]
    public void Screen_with_header_and_weapon_bar_matches_the_original(ProbeCase c) =>
        Assert.Equal(c.ScreenAtK, Sha(Create(c).Screen.Pixels));

    [GameDataTheory]
    [MemberData(nameof(Cases))]
    public void Written_DAC_entries_match_the_original(ProbeCase c) =>
        Assert.Equal(c.DacAtK, Sha(Create(c).WrittenDacEntries()));
}
