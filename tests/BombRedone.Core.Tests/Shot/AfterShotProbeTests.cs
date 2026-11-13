using System.Security.Cryptography;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Shot;

namespace BombRedone.Core.Tests.Shot;

/// <summary>
/// After the shot (O4:49CB): all tanks, landslide O7:03C1 / U431:0496 and falling O7:0020 against the probe. Expected values
/// from <c>python3 -I tools/reverse-engineering/feld_schuss.py post</c> (analyse/feld/schuss-post-hashes.tsv; the shot is
/// the one of <see cref="ShotProbeData"/> with that number). Compared: RandSeed, frame and field of the screen, background
/// buffer, DAC entries written after the shot, energy, inertia and position of every record.
/// </summary>
public sealed class AfterShotProbeTests
{
    public sealed record PostCase(int Nr, int Shot, int Crumble, bool FallDown, string SeedAfter, string ScreenField,
        string BackgroundBuffer, string DacSet, string Records)
    {
        public override string ToString() => $"post {Nr} (shot {Shot}, crumble {Crumble}, fall {FallDown})";
    }

    public static readonly TheoryData<PostCase> Cases =
    [
        new(0, 3, 0, true, "D526863D", "c3c9935cf213c426b71cbaabd4dc21d1d2e3eb4c40519f8119410265692e3136", "ce41d8b95ee50c843a9c2a819320834ef980a15d0ccf8e5a83746b778411b949", "6b2f545258dc4170bd07ae295412996df449341b68bf71d1314242b66fb00652", "100,0,33,123 0,0,297,114"),
        new(1, 3, 100, true, "20E01332", "c3c9935cf213c426b71cbaabd4dc21d1d2e3eb4c40519f8119410265692e3136", "ce41d8b95ee50c843a9c2a819320834ef980a15d0ccf8e5a83746b778411b949", "6b2f545258dc4170bd07ae295412996df449341b68bf71d1314242b66fb00652", "100,0,33,123 0,0,297,114"),
        new(2, 2, 100, true, "655B54E3", "29e34c61e68eb00a703f1388f88cfc44bfaec64bce1ba556b55d98c3bbc61731", "ce41d8b95ee50c843a9c2a819320834ef980a15d0ccf8e5a83746b778411b949", "bb7dd54bd346fd96fc3ca80dd7b9c67ea809217a2906abaaac0104f2f5c43de0", "100,0,33,123 100,0,300,107"),
        new(3, 18, 50, true, "B46A85F9", "3dea66d2ef1773bf453ad69384e33c2045ea8af80e3d568a56ac1963e3cfbcb1", "a0b16375293eae27d5e886bc20d195b98c88975c0440fe68ff29283e8a737a28", "d6054b1623410bf241f361df25b30deb96c629c485be059fb22d61e36899f044", "100,0,29,167 100,0,117,142 100,0,205,164 100,0,293,151"),
        new(4, 3, 100, false, "655B54E3", "74b08f84e732044ba2dbc65b357486d9f2c14fff3b5aa5b27e80b9c8415971af", "ce41d8b95ee50c843a9c2a819320834ef980a15d0ccf8e5a83746b778411b949", "6b2f545258dc4170bd07ae295412996df449341b68bf71d1314242b66fb00652", "100,0,33,123 0,0,300,107"),
        new(5, 13, 100, true, "655B54E3", "77f08ba3438d9b24a2c8fc542e3a4cdf2913b714f83ad78b0fbdb69126a1d831", "4aa722e7f01cb8d4297b44f890610118434c2d538e77cf8860d198d679d74060", "bb7dd54bd346fd96fc3ca80dd7b9c67ea809217a2906abaaac0104f2f5c43de0", "100,0,33,123 100,0,300,107"),
        new(6, 23, 100, true, "655B54E3", "30e70d69cd2abeb28ed4ad91c5c0a256873c8235a0c3862f8571e48769748e14", "ce41d8b95ee50c843a9c2a819320834ef980a15d0ccf8e5a83746b778411b949", "bb7dd54bd346fd96fc3ca80dd7b9c67ea809217a2906abaaac0104f2f5c43de0", "100,0,33,123 100,0,300,107"),
        new(7, 24, 100, true, "CA991697", "e8479bf3cc85c3e3ff6cc2ce1af149880a5401f0aaa1ab7f36b192c49eb02a76", "ce41d8b95ee50c843a9c2a819320834ef980a15d0ccf8e5a83746b778411b949", "32ce11506d1d601302f2d014f86bb68fa6c19d89467193eb9fe6c2d5e72ac5f9", "100,0,33,123 41,0,293,135"),
        new(8, 24, 0, true, "DD9548FA", "2d6a25674e73e096261f8904ca48445a711c0d9906d6d86a91109a7f4eeb02f5", "ce41d8b95ee50c843a9c2a819320834ef980a15d0ccf8e5a83746b778411b949", "bb7dd54bd346fd96fc3ca80dd7b9c67ea809217a2906abaaac0104f2f5c43de0", "100,0,33,123 100,0,300,107"),
    ];

    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    [Theory]
    [MemberData(nameof(Cases))]
    public void Landslide_and_falling_match_the_original(PostCase p)
    {
        var c = ShotProbeData.All.Single(s => s.Nr == p.Shot);
        var (field, random) = ShotProbeTests.FireForTest(c);
        field.Palette.ClearWritten();

        AfterShot.Run(field, p.Crumble, p.FallDown, random);

        Assert.Equal(p.SeedAfter, random.Seed.ToString("X8"));
        Assert.Equal(p.Records, string.Join(" ", field.Tanks.Select(t => $"{t.Energy},{t.Defense[Damage.Inertia]},{t.X},{t.Y}")));
        Assert.Equal(p.BackgroundBuffer, Sha(field.Background.Pixels));
        Assert.Equal(p.DacSet, Sha(field.Palette.WrittenEntries()));
        Assert.Equal(p.ScreenField, ShotProbeTests.FieldHash(field));
    }
}
