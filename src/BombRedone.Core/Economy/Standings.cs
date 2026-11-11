namespace BombRedone.Core.Economy;

/// <summary>One line of the status screen: a player and its rank (equal wins and score share a rank).</summary>
public readonly record struct Standing(int Rank, PlayerAccount Player);

/// <summary>Order of the players on the status screen between two games.</summary>
public static class Standings
{
    /// <summary>
    /// Sorts the players and numbers the ranks. [ORIGINAL] O3:5B61–5C6D (BOMB.OVR 0x0E5ED): n passes of a bubble sort
    /// that swaps neighbours with fewer wins, then n passes that swap neighbours with equal wins and a lower score
    /// (swap: O3:0000, whole player records). O3:5E44–5F62: the rank starts at 1 and grows after a player whose score or
    /// wins differ from the next one.
    /// </summary>
    public static IReadOnlyList<Standing> Rank(IEnumerable<PlayerAccount> players)
    {
        var list = players.ToList();
        var n = list.Count;
        for (var pass = 0; pass < n; pass++)
            for (var i = 0; i < n - 1; i++)
                if (list[i].Wins < list[i + 1].Wins)
                    (list[i], list[i + 1]) = (list[i + 1], list[i]);
        for (var pass = 0; pass < n; pass++)
            for (var i = 0; i < n - 1; i++)
                if (list[i].Wins == list[i + 1].Wins && list[i].Score < list[i + 1].Score)
                    (list[i], list[i + 1]) = (list[i + 1], list[i]);

        var result = new List<Standing>(n);
        var rank = 1;
        for (var i = 0; i < n; i++)
        {
            result.Add(new Standing(rank, list[i]));
            if (i + 1 < n && (list[i].Score != list[i + 1].Score || list[i].Wins != list[i + 1].Wins))
                rank++;
        }

        return result;
    }
}
