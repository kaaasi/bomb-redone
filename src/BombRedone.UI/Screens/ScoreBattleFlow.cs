using BombRedone.Core.Battle;
using BombRedone.Core.Economy;

namespace BombRedone.UI.Screens;

/// <summary>
/// Screens of the game sequence between two rounds (O3:7847, docs/analyse/runde.md section 1): for every step of the
/// <see cref="BattleSequence"/> the status screen, the team status screen or the shop of a record; the round itself is
/// the <see cref="IBattleRound"/> of the sequence.
/// </summary>
/// <remarks>
/// [APPROX] "Add" (dialog "# TO ADD:" O3:5717) and the statistics (O3:2508, O3:2C8B) are not part of this package: the
/// screen stays. "Save" continues without saving (round 4). The wipe O3:79B2 is not animated.
/// </remarks>
public sealed class ScoreBattleFlow
{
    private readonly ShopFonts _fonts;
    private readonly BattleSequence _battle;
    private readonly ItemCatalog _catalog;
    private readonly bool _allComputers;
    private readonly Func<BattlePlayer, (int Red, int Green, int Blue)> _color;
    private readonly Func<int, string> _teamName;
    private readonly Func<IScreen?> _finished;
    private ScoreScreen? _rating;

    /// <param name="fonts">Fonts from BOMB.EXE.</param>
    /// <param name="battle">The sequence, not started yet.</param>
    /// <param name="catalog">Items of the shop.</param>
    /// <param name="allComputers">DS:4812: no buttons and no input on the status screens.</param>
    /// <param name="color">6-bit colour of a player (O10:01AC, from the record).</param>
    /// <param name="teamName">Name of a team number (team records DS:4838, Team Play only).</param>
    /// <param name="finished">Screen after the last game (end result O3[1]); null quits.</param>
    public ScoreBattleFlow(ShopFonts fonts, BattleSequence battle, ItemCatalog catalog, bool allComputers,
        Func<BattlePlayer, (int Red, int Green, int Blue)> color, Func<int, string> teamName, Func<IScreen?> finished)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(battle);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(teamName);
        ArgumentNullException.ThrowIfNull(finished);
        _fonts = fonts;
        _battle = battle;
        _catalog = catalog;
        _allComputers = allComputers;
        _color = color;
        _teamName = teamName;
        _finished = finished;
    }

    /// <summary>Plays the first round and returns its status screen.</summary>
    public IScreen? Start() => ScreenFor(_battle.Start());

    private IScreen? Next() => ScreenFor(_battle.Continue());

    private IScreen? ScreenFor(BattleStep step)
    {
        var records = _battle.Records;
        switch (step.Kind)
        {
            case BattleStepKind.Rating:
                // [ORIGINAL] O3:5AFD: after the last game the status screen always has its buttons.
                _rating = new ScoreScreen(_fonts, [.. records.InOrder.Select(r => new ScoreLine(r.Account, _color(r)))],
                    _battle.Game, _battle.Games, !_allComputers || _battle.Game == _battle.Games, Choose);
                return _rating;
            case BattleStepKind.TeamRating:
                var teams = records.InOrder.GroupBy(r => r.Team)
                    .Select(g => new Team(g.Key, _teamName(g.Key), [.. g.Select(r => r.Account)]));
                return new ScoreTeamScreen(_fonts, Rating.Background, Rating.Dac, teams, _battle.Game, _battle.Games,
                    !_allComputers, (choice, _, screen) => choice is ScoreChoice.Continue or ScoreChoice.Save ? Next() : screen);
            case BattleStepKind.Shop:
                var record = records[step.Record];
                return new ShopScreen(_fonts, Rating.Background, Rating.Dac, new Shop(record.Account, _catalog),
                    (ShopControl)record.Control, _color(record), Next, step.Record);
            case BattleStepKind.Wipe:
                return Next();
            case BattleStepKind.Finished:
                return _finished();
            default:
                throw new InvalidOperationException(step.Kind.ToString());
        }
    }

    private ScoreScreen Rating => _rating ?? throw new InvalidOperationException("No status screen yet.");

    private IScreen? Choose(ScoreChoice choice, int line, ScoreScreen screen) =>
        choice is ScoreChoice.Continue or ScoreChoice.Save ? Next() : screen;
}
