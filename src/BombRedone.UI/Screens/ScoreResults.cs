using System.Globalization;
using System.Text;
using BombRedone.Core.Economy;

namespace BombRedone.UI.Screens;

/// <summary>One line of text-mode output with the text colour of every character (Crt.TextColor, 0..15).</summary>
/// <param name="Text">Characters of the line (code page 437 as Unicode).</param>
/// <param name="Colors">Text colour per character.</param>
public sealed record TextLine(string Text, IReadOnlyList<byte> Colors);

/// <summary>
/// End result after the last game, printed in text mode when the program ends: O3:7566 (BOMB.OVR 0x0FFF2), called from
/// the main program as O3[1]; docs/anforderungen/wertung-einkauf.md §6.
/// </summary>
public static class ScoreResults
{
    /// <summary>[ORIGINAL] O3:7639–7681: the heading lines (strings O3:750A, O3:7520, O3:7540; 0xC4 = '─').</summary>
    public const string ResultsHeading = "             Results ";

    /// <inheritdoc cref="ResultsHeading"/>
    public const string ColumnHeading = "   Name            Score   Wins";

    /// <summary>[ORIGINAL] O3:7540: a space and 32 × character 0xC4 of code page 437.</summary>
    public static readonly string Rule = " " + new string('─', 32);

    /// <summary>
    /// Lines after ClrScr (white on black). [ORIGINAL] O3:7599–7634 in colour 6: games played − 1 with " Games " (above 1)
    /// or " Game " and "Played", "   And just in case you missed them:", an empty line; O3:7639–7681 in colour 15 the
    /// headings; O3:7686–780A in colour 10 per player " r." (rank, leading space below 10), " " + name, GotoXY(17) the
    /// score in 8 columns, GotoXY(28) the wins in 4 columns; then two empty lines.
    /// </summary>
    /// <param name="players">Players; ranked like the status screen (O3:7566 uses the record order it left).</param>
    /// <param name="gamesCounter">DS:47E2 when the battle ends (one more than the games played).</param>
    public static IReadOnlyList<TextLine> Build(IEnumerable<PlayerAccount> players, int gamesCounter)
    {
        var played = gamesCounter - 1;
        var lines = new List<TextLine>
        {
            Line(Number(played) + (gamesCounter > 2 ? " Games " : " Game ") + "Played", 6),
            Line("   And just in case you missed them:", 6),
            Line("", 6),
            Line(ResultsHeading, 15),
            Line(ColumnHeading, 15),
            Line(Rule, 15),
        };

        foreach (var standing in Standings.Rank(players))
        {
            var text = new StringBuilder();
            Write(text, 1, ScoreScreen.RankText(standing.Rank) + " " + standing.Player.Name);
            Write(text, 17, Number(standing.Player.Score).PadLeft(8));
            Write(text, 28, Number((short)standing.Player.Wins).PadLeft(4));
            lines.Add(Line(text.ToString(), 10));
        }

        lines.Add(Line("", 10));
        lines.Add(Line("", 10));
        return lines;
    }

    /// <summary>
    /// Crt.GotoXY(column, WhereY) and Write: output overwrites characters already on the line, gaps stay blank.
    /// [APPROX] Lines longer than 80 columns (wrap of the text window) are not modelled.
    /// </summary>
    private static int Write(StringBuilder text, int column, string value)
    {
        var index = column - 1;
        if (text.Length < index)
            text.Append(' ', index - text.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (index + i < text.Length)
                text[index + i] = value[i];
            else
                text.Append(value[i]);
        }

        return index + value.Length + 1;
    }

    private static TextLine Line(string text, byte color) => new(text, Enumerable.Repeat(color, text.Length).ToArray());

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
