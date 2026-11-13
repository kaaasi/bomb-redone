using System.Security.Cryptography;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

namespace BombRedone.Core.Tests.Playfield;

/// <summary>
/// The playfield set-up against the original code run in the probe (docs/analyse/feld.md section 2, messpunkte A and S).
/// Expected values from <c>python3 -I tools/reverse-engineering/feld.py hashes</c> (analyse/feld/hashes.tsv): SHA-256
/// of the screen without the BGI texts (column screen_core), of the save buffer and of the written DAC entries; RandSeed
/// and the number of Random calls at A; wind and start player at S.
/// </summary>
public sealed class PlayfieldProbeTests
{
    public sealed record ProbeCase(
        string Seed, int Players, int Wall, int Background, int Mask, int Wind, bool Invisible,
        string SeedAtA, int StepsAtA, int WallOfRound, int BackgroundDrawn, string Tanks,
        string Screen, string SaveBuffer, string DacSet,
        string SeedAtS, int WindLevel, int WindValue, int StartPlayer, int Pass)
    {
        public override string ToString() => $"seed {Seed}, {Players} players, wall {Wall}, background {Background}";
    }

    public static readonly TheoryData<ProbeCase> Cases =
    [
        new("12345678", 2, 0, 0, 0xFFFF, 2, false, "9829CECB", 127, 0, 0, "33,123 300,107", "3f380b2a28676f29336edb0801ba23e50848cd868ae3eec929790cc9a53cf19c", "ce41d8b95ee50c843a9c2a819320834ef980a15d0ccf8e5a83746b778411b949", "803b83ea4d76aed4e030e6906f14730741575a3a781314963de713518bebaec9", "FE45D765", 2, -44, 2, 1),
        new("00000001", 3, 1, 1, 0xFFFF, 0, false, "85CDA90B", 1094, 1, 1, "40,47 168,143 296,175", "210116ef11e0ef77b3d8d620ef4b453abcb630d80860e19cc066daa833f0b750", "2df4a02d7ff378f7b990af021317de17cfd84eb31eccf7513a1362ee1bfe6098", "4b9910ad20b76229ea8b3a8f715ad5ebbec3c034ba4d5a7cf1bc36d265940aef", "5637C3A5", 0, 0, 1, 1),
        new("9E3779B9", 4, 2, 2, 0xFFFF, 1, false, "799096A1", 184, 2, 2, "29,167 117,142 205,164 293,151", "cd496773a8aa61b5f0c9b7b166f34c86813c797c0c3962ee111c118193854de1", "a0b16375293eae27d5e886bc20d195b98c88975c0440fe68ff29283e8a737a28", "a51208597ca9c73f956f711365d2b81f8705cbdc02a46de10fa17e0839d273b9", "255A2BEB", 1, -3, 3, 1),
        new("DEADBEEF", 5, 3, 3, 0xFFFF, 3, false, "C985D189", 166, 3, 3, "34,112 98,146 162,182 226,170 292,156", "3ec1d40aa8b87323a11bef1892bc8d1f3be2d9002b411f400f55eb8342dc73b1", "d81809b24b85e077561352c10578c73bdbaff6010cdc94e9a4ecfe83665c86ba", "05154ae9ae8423c892f5ce10fc23a3fd08b541eef69bbb7044ae48e06075b674", "FECB4613", 3, 91, 3, 1),
        new("0BADF00D", 6, 4, 4, 0xFFFF, 4, false, "2978AA49", 212, 4, 4, "43,151 94,163 145,174 196,122 247,88 301,95", "766c7dbff89161cf0a633aa155d58e7bc33f5e5dcb92bef7aabe0ff55eef8ceb", "06183d892a75ab1dedbf81c42257ab8e67aeb3319ff15b016ab2b147e9e767b7", "28e39ff668ddff3306dcca17afe216da61dd3e517521b9949d1fa1a2705df43f", "4034A4D3", 4, 672, 3, 1),
        new("7FFFFFFF", 7, 5, 5, 0xFFFF, 5, false, "2DCA8E83", 204, 1, 5, "35,155 77,143 119,86 161,62 203,46 245,65 287,60", "b69ee6d495651e0b20e6ee46d7f9740cfa37a9f264250921dcd96da30ee7e34a", "52da9304f833e54ebc92cbd1b4a88fb4f6cda98e1a3962d7dda36ad329a64327", "dcc6dfc902df73d45977cd4399c7465af2ef70639074b9bbe5c945d291d2d79d", "D87E5452", 2, 140, 4, 1),
        new("80000000", 8, 1, 6, 0xFFFF, 2, true, "BAA891C5", 47789, 1, 6, "21,42 59,49 97,97 135,135 173,132 211,105 249,133 289,138", "a81a69b4c294b6ec6a0177fb1f84800d2629c5a2dc891706aaab742d99733cf2", "e16dd10d7674ed7df9ec192c083ea6552dd60405ddee25968a1d77fc72a4a578", "34c3ed8c787f79a3de6732f7f9dc4b347aa013f08b192c0340b572b8bb0bb29c", "7D40742F", 2, -26, 1, 1),
        new("CAFEBABE", 9, 2, 7, 0xFFFF, 2, false, "C22D2485", 227, 2, 7, "36,69 68,90 100,100 132,111 164,121 196,117 228,100 260,54 296,37", "1f1a6b9356c9b1726ffc0eeced4cafde33cb3894a895fe48a7baf598e73edf97", "303c56d886319b9f17c700163edd176db8b070c346057dd3bd624503f0d5fd63", "bf722c4d49d31b97a6decc4fb5c1821c57e85946f08dbd4c9224b379a9928683", "F64B5CEF", 2, 20, 7, 1),
        new("13579BDF", 10, 3, 8, 0xFFFF, 4, false, "D91B91C9", 278, 3, 8, "22,178 50,186 78,175 106,119 134,82 162,76 190,91 218,110 246,115 278,101", "55cab700c62e79931f03b8be8da15a2c1aa4676b7e95a9e6c0b7b30f89ecbe2f", "506242eaf8df9bc2bc5985b67a56b9df991238fba0778c500fbd2f2f6ad5ec5c", "c537d0693bb85d1feeeede813eb6cfae327996dc7ca9a9a80f45aafeec7dba60", "F4DC4853", 4, -1197, 5, 1),
        new("2468ACE0", 2, 5, 9, 0xFFFF, 5, false, "EC043743", 143, 3, 5, "41,170 281,109", "7153dc6f5f58a9b12259d51e9182b97ad2223da2bf727af33106b30be2ed2523", "52da9304f833e54ebc92cbd1b4a88fb4f6cda98e1a3962d7dda36ad329a64327", "f0ed77523f735511749ae3c9f63e193dc4f652168ec0c0e7c18b0827eab19e66", "E76ABE12", 0, 0, 1, 1),
        new("FEDCBA98", 3, 5, 10, 0x0124, 5, false, "B03FCACF", 171, 0, 8, "18,101 156,52 294,66", "9f49cab962e9a574e9241c72f9c2bb6222a23dee2123db5f68a6c964cbc5a049", "506242eaf8df9bc2bc5985b67a56b9df991238fba0778c500fbd2f2f6ad5ec5c", "69c2e1a64dc774fd7852bbc41dcdcf396026d67822d6d3e24c51ae04caeee598", "8745E604", 4, 6652, 1, 1),
        new("00015446", 2, 0, 0, 0xFFFF, 4, false, "AD12BB05", 171, 0, 0, "38,83 295,130", "06fb1a3b731dc76cad34fd7e2296703b02d0e8bd02965a1887d99864379dfd4d", "ce41d8b95ee50c843a9c2a819320834ef980a15d0ccf8e5a83746b778411b949", "803b83ea4d76aed4e030e6906f14730741575a3a781314963de713518bebaec9", "AA86276F", 4, 30000, 1, 1),
        new("002B423B", 2, 0, 0, 0xFFFF, 4, false, "A48E539C", 173, 0, 0, "23,45 291,177", "1ead9f774b4ad71383dd9fd11cbb9973d23473fa1d482e59bd258f993c59db6f", "ce41d8b95ee50c843a9c2a819320834ef980a15d0ccf8e5a83746b778411b949", "803b83ea4d76aed4e030e6906f14730741575a3a781314963de713518bebaec9", "C210B594", 4, 3008, 1, 1),
    ];

    private static Core.Playfield.Playfield Build(ProbeCase c, out TurboPascalRandom random)
    {
        random = new TurboPascalRandom(Convert.ToUInt32(c.Seed, 16));
        return Core.Playfield.Playfield.Build(
            new PlayfieldOptions(c.Players, c.Wall, c.Background, c.Mask, c.Wind, c.Invisible), random);
    }

    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    [Theory]
    [MemberData(nameof(Cases))]
    public void State_at_A_matches_the_original(ProbeCase c)
    {
        var field = Build(c, out var random);

        Assert.Equal(c.SeedAtA, random.Seed.ToString("X8"));
        Assert.Equal(c.StepsAtA, RandomSteps.Between(Convert.ToUInt32(c.Seed, 16), random.Seed));
        Assert.Equal(c.WallOfRound, (int)field.Wall);
        Assert.Equal(c.BackgroundDrawn, field.BackgroundNumber);
        Assert.Equal(c.Tanks, string.Join(" ", field.Tanks.Select(t => $"{t.X},{t.Y}")));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Save_buffer_at_A_matches_the_original(ProbeCase c)
    {
        var field = Build(c, out _);

        Assert.Equal(c.SaveBuffer, Sha(field.Background.Pixels));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Screen_at_A_matches_the_original_without_texts(ProbeCase c)
    {
        var field = Build(c, out _);

        Assert.Equal(c.Screen, Sha(field.Screen.Pixels));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Written_DAC_entries_at_A_match_the_original(ProbeCase c)
    {
        var field = Build(c, out _);

        Assert.Equal(c.DacSet, Sha(field.Palette.WrittenEntries()));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Wind_and_start_player_at_S_match_the_original(ProbeCase c)
    {
        Build(c, out var random);

        var wind = Wind.Choose(c.Wind, random);
        var rounds = new int[c.Players];
        var start = StartPlayer.Choose(rounds, Enumerable.Repeat(true, c.Players).ToArray(), random);

        Assert.Equal((c.WindLevel, c.WindValue), (wind.Level, wind.Value));
        Assert.Equal((c.StartPlayer, c.Pass), (start.Player, start.Pass));
        Assert.Equal(1, rounds[c.StartPlayer - 1]);
        Assert.Equal(c.SeedAtS, random.Seed.ToString("X8"));
    }
}
