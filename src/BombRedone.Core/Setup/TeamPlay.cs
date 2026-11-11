using System.Globalization;

namespace BombRedone.Core.Setup;

/// <summary>A team as the game starts with it.</summary>
/// <param name="Number">Team number 1..player count.</param>
/// <param name="Name">Name, at most <see cref="TeamRecord.MaxNameLength"/> characters.</param>
/// <param name="Members">Player numbers of the team in order.</param>
public sealed record TeamSetup(int Number, string Name, IReadOnlyList<int> Members);

/// <summary>
/// Entry of the team table DS:4838 + 53 · t (O2:35CF, BOMB.OVR 0x03BB7): name String[13] at +0, "all computers" at
/// +0Eh, member count at +0Fh.
/// </summary>
public sealed class TeamRecord
{
    /// <summary>[ORIGINAL] The name is a String[13] (O2:35CF copies 13 characters, O2:31A8 limits the entry to 13).</summary>
    public const int MaxNameLength = 13;

    private string _name = string.Empty;

    internal TeamRecord(int number) => Number = number;

    /// <summary>Team number 1..player count.</summary>
    public int Number { get; }

    /// <summary>Name, at most <see cref="MaxNameLength"/> characters.</summary>
    public string Name
    {
        get => _name;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _name = value.Length > MaxNameLength ? value[..MaxNameLength] : value;
        }
    }

    /// <summary>
    /// [ORIGINAL] Byte +0Eh: 1 unless a member is not a computer (DS:E5A6 + 2p ≠ 3); also 1 for teams without members.
    /// </summary>
    public bool AllComputers { get; internal set; } = true;

    /// <summary>Player numbers of the members in order (the count is word +0Fh).</summary>
    public IReadOnlyList<int> Members => MemberList;

    internal List<int> MemberList { get; } = [];
}

/// <summary>
/// Rules of team play: the check at the end of the team setup O2:2749 (BOMB.OVR 0x02D31) and the team table with the
/// names of O2:35CF (BOMB.OVR 0x03BB7); docs/anforderungen/spielerauswahl.md §7–9.
/// </summary>
public static class TeamPlay
{
    /// <summary>
    /// [ORIGINAL] Names an all-computer team of two or more players draws from (O2:38F1–3A80, Random(11)); Pascal
    /// strings at O2:355D … O2:35C5 (BOMB.OVR 0x03B45 …).
    /// </summary>
    public static IReadOnlyList<string> RandomNames { get; } =
    [
        "Bushwackers", "Crazy CPUs", "Tank Trashers", "Studs", "Human Mashers", "Crackshots", "Dungheaps", "Ugly Ducks",
        "Martians", "Gravy", "Boneheads",
    ];

    /// <summary>
    /// [ORIGINAL] O2:2CA8–2D63: team play is valid if some player of 1..count is not in the team of player 1 and some
    /// team 1..count has at least two players.
    /// </summary>
    public static bool IsValid(PlayerRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);
        var players = roster.Players;
        var split = false;
        for (var p = 1; p <= players; p++)
        {
            if (roster[p].Team != roster[1].Team)
                split = true;
        }

        var largest = 0;
        for (var t = 1; t <= players; t++)
        {
            var count = 0;
            for (var p = 1; p <= players; p++)
            {
                if (roster[p].Team == t)
                    count++;
            }

            largest = Math.Max(largest, count);
        }

        return split && largest >= 2;
    }

    /// <summary>[ORIGINAL] O2:2E89–2EB0: team play is switched off (DS:E8BC := 0) and every record n gets team n again.</summary>
    public static void Reset(PlayerRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);
        foreach (var slot in roster.Slots)
            slot.Team = slot.Number;
    }

    /// <summary>
    /// [ORIGINAL] O2:35D6–374C: the table is cleared (FillChar 247h bytes); for team t = 1..count the members are
    /// counted and "all computers" set; a team with members gets the name "Team " + Str(k), k = number of teams with
    /// members up to t, and a team of exactly one player the name of that player.
    /// </summary>
    /// <returns>Records of the teams 1..player count (index 0 = team 1), with and without members.</returns>
    public static IReadOnlyList<TeamRecord> CreateTeams(PlayerRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);
        var players = roster.Players;
        var teams = new List<TeamRecord>(players);
        var named = 0;
        for (var t = 1; t <= players; t++)
        {
            var team = new TeamRecord(t);
            for (var p = 1; p <= players; p++)
            {
                if (roster[p].Team != t)
                    continue;
                team.MemberList.Add(p);
                if (roster[p].Control != PlayerControl.Computer)
                    team.AllComputers = false;
            }

            if (team.Members.Count != 0)
                named++;
            team.Name = "Team " + named.ToString(CultureInfo.InvariantCulture);
            if (team.Members.Count == 1)
                team.Name = roster[team.Members[0]].Name;
            teams.Add(team);
        }

        return teams;
    }

    /// <summary>
    /// [ORIGINAL] O2:38F1–3ADD: repeat name := RandomNames[Random(11)] until no other team 1..count has that name.
    /// </summary>
    /// <param name="teams">Result of <see cref="CreateTeams"/>.</param>
    /// <param name="number">Team that gets the name.</param>
    /// <param name="random">Turbo Pascal Random(n); called with n = 11 once per try.</param>
    public static void ChooseRandomName(IReadOnlyList<TeamRecord> teams, int number, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(teams);
        ArgumentNullException.ThrowIfNull(random);
        var team = teams[number - 1];
        bool unique;
        do
        {
            unique = true;
            var pick = random(RandomNames.Count);
            if (pick is < 0 or > 10)
                throw new InvalidOperationException($"Random(11) returned {pick}.");
            team.Name = RandomNames[pick];
            foreach (var other in teams)
            {
                if (other.Number != number && string.Equals(other.Name, team.Name, StringComparison.Ordinal))
                    unique = false;
            }
        }
        while (!unique);
    }

    /// <summary>The setup with the team numbers of the roster and the named teams that have members.</summary>
    public static GameSetup Apply(GameSetup setup, PlayerRoster roster, IReadOnlyList<TeamRecord> teams)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(teams);
        return new GameSetup([.. setup.Players.Select(p => p with { Team = roster[p.Number].Team })], setup.AllComputers)
        {
            Teams = [.. teams.Where(t => t.Members.Count > 0).Select(t => new TeamSetup(t.Number, t.Name, [.. t.Members]))],
        };
    }
}
