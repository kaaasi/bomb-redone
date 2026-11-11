namespace BombRedone.Core.Economy;

/// <summary>A team as the team status screen sees it: number 1..10, name and members.</summary>
/// <remarks>
/// [ORIGINAL] Team records of 0x35 bytes at DS:4838 + 0x35 × number (number 1..10): <c>+00h</c> name (string[14],
/// O3:6F67 draws <c>[di+4838]</c>), <c>+0Fh</c> member count (Word, set by the team setup; O3:66C8 reads
/// <c>[di+4847]</c>). The sums the status screen fills follow at <c>+11h</c> (see <see cref="TeamStandings"/>).
/// </remarks>
public sealed record Team(int Number, string Name, IReadOnlyList<PlayerAccount> Members);

/// <summary>One line of the team status screen.</summary>
/// <param name="Position">Line on the screen (1-based, y = top + 10 + 10 × position).</param>
/// <param name="Rank">Rank; equal score and average wins share a rank.</param>
/// <param name="Team">The team.</param>
/// <param name="Score">Sum of the members' scores (Real48 in the original, exact for LongInt sums).</param>
/// <param name="WinsSum">Sum of the members' wins.</param>
public readonly record struct TeamStanding(int Position, int Rank, Team Team, long Score, int WinsSum)
{
    /// <summary>Average wins per member as the screen shows it (Real48 division in the original).</summary>
    public double AverageWins => (double)WinsSum / Team.Members.Count;
}

/// <summary>Result of <see cref="TeamStandings.Rank"/>.</summary>
/// <param name="Lines">Teams with members in screen order.</param>
/// <param name="Order">
/// All ten team slots in sorted order (index 0 = position 1, null = no team with that number); the digit keys of the
/// screen pick from this list, empty slots included.
/// </param>
/// <param name="Players">Number of players (DS:47DA): the sum of the members.</param>
/// <param name="CountedTeams">
/// Slots 1..players with members ([bp-128h], O3:66B8): the box height, the sorts by wins and the mouse lines use this
/// count. It equals the number of <paramref name="Lines"/> unless a team number is above the player count.
/// </param>
public sealed record TeamRanking(IReadOnlyList<TeamStanding> Lines, IReadOnlyList<Team?> Order, int Players,
    int CountedTeams);

/// <summary>Order of the teams on the team status screen between two games.</summary>
public static class TeamStandings
{
    /// <summary>Team slots of the original (team numbers 1..10).</summary>
    public const int Slots = 10;

    /// <summary>
    /// Sums and sorts the teams. [ORIGINAL] O3:6696 (BOMB.OVR 0x0F122):
    /// <list type="bullet">
    /// <item>O3:6713–6A83: for each slot 1..10 the score (+13h) and the words +68h..+70h of every player whose team
    /// (+66h) matches are added as Real48; +68h and the wins (+6Ah) are then divided by the member count (only the
    /// average wins are shown, the other sums belong to the statistics).</item>
    /// <item>O3:6A86–6AFD: 10 passes of a bubble sort over the slot indices 1..10 that swaps neighbours with fewer
    /// members, so the empty slots move to the end.</item>
    /// <item>O3:6B03–6BC7: n passes over the first n slots (n = slots 1..players with members, O3:66B8) that swap
    /// neighbours with lower average wins; O3:6BCA–6CD2: n passes that swap neighbours with equal average wins and a
    /// lower score.</item>
    /// <item>O3:6E89–7006: the rank starts at 1 and grows after a team whose score or average wins differ from the next
    /// slot.</item>
    /// </list>
    /// Averages are compared exactly here; with at most ten members and Word counts the Real48 quotients of different
    /// fractions never coincide, equal fractions give equal quotients.
    /// </summary>
    public static TeamRanking Rank(IEnumerable<Team> teams)
    {
        ArgumentNullException.ThrowIfNull(teams);
        var slots = new Team?[Slots + 1];
        foreach (var team in teams)
        {
            if (team.Number is < 1 or > Slots)
                throw new ArgumentOutOfRangeException(nameof(teams), team.Number, "Team numbers are 1 to 10.");
            if (slots[team.Number] is not null)
                throw new ArgumentException($"Team {team.Number} appears twice.", nameof(teams));
            slots[team.Number] = team;
        }

        var players = slots.Sum(t => t?.Members.Count ?? 0);
        int Count(int slot) => slots[slot]?.Members.Count ?? 0;
        long Score(int slot) => slots[slot]?.Members.Sum(m => (long)m.Score) ?? 0;
        int Wins(int slot) => slots[slot]?.Members.Sum(m => m.Wins) ?? 0;
        // Compares the averages wins(a)/count(a) and wins(b)/count(b); an empty slot averages 0 (never divided).
        int CompareWins(int a, int b)
        {
            long ca = Math.Max(1, Count(a)), cb = Math.Max(1, Count(b));
            return ((long)Wins(a) * cb).CompareTo((long)Wins(b) * ca);
        }

        var n = 0;
        for (var slot = 1; slot <= Math.Min(players, Slots); slot++)
            if (Count(slot) > 0)
                n++;

        // Index 1..10 as in the original array [bp-126h].
        var order = new int[Slots + 2];
        for (var i = 1; i <= Slots; i++)
            order[i] = i;
        order[Slots + 1] = 0;

        for (var pass = 1; pass <= Slots; pass++)
            for (var i = 1; i <= Slots - 1; i++)
                if (Count(order[i]) < Count(order[i + 1]))
                    (order[i], order[i + 1]) = (order[i + 1], order[i]);
        for (var pass = 1; pass <= n; pass++)
            for (var i = 1; i <= n - 1; i++)
                if (CompareWins(order[i], order[i + 1]) < 0)
                    (order[i], order[i + 1]) = (order[i + 1], order[i]);
        for (var pass = 1; pass <= n; pass++)
            for (var i = 1; i <= n - 1; i++)
                if (CompareWins(order[i], order[i + 1]) == 0 && Score(order[i]) < Score(order[i + 1]))
                    (order[i], order[i + 1]) = (order[i + 1], order[i]);

        var lines = new List<TeamStanding>();
        var rank = 1;
        for (var position = 1; position <= Math.Min(players, Slots); position++)
        {
            var slot = order[position];
            if (Count(slot) <= 0)
                continue;
            lines.Add(new TeamStanding(position, rank, slots[slot]!, Score(slot), Wins(slot)));
            if (position != Slots)
            {
                var next = order[position + 1];
                if (Score(slot) != Score(next) || CompareWins(slot, next) != 0)
                    rank++;
            }
        }

        return new TeamRanking(lines, order.Skip(1).Take(Slots).Select(s => slots[s]).ToList(), players, n);
    }
}
