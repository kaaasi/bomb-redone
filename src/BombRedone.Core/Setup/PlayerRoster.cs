using System.Globalization;

namespace BombRedone.Core.Setup;

/// <summary>One of the ten player records while the game is being set up (name and control).</summary>
public sealed class PlayerSlot
{
    /// <summary>[ORIGINAL] The name is a String[13] at DS:402F + 116 · n (O2:42E8 limits it to 13, O2:481B copies 13).</summary>
    public const int MaxNameLength = 13;

    private string _name;

    internal PlayerSlot(int number)
    {
        Number = number;
        _name = DefaultName(number);
    }

    /// <summary>Player number 1..10 (byte 0 of the record, O10:0161).</summary>
    public int Number { get; }

    /// <summary>Name, at most <see cref="MaxNameLength"/> characters.</summary>
    public string Name
    {
        get => _name;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length > MaxNameLength)
                throw new ArgumentException($"A name has at most {MaxNameLength} characters.", nameof(value));
            _name = value;
        }
    }

    /// <summary>Who controls the player.</summary>
    public PlayerControl Control { get; set; } = PlayerControl.Keyboard;

    /// <summary>[ORIGINAL] O2:0216 (BOMB.OVR 0x007FE): "Player " + Str(n).</summary>
    public static string DefaultName(int number) => "Player " + number.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// The ten player records the main menu and the player selection work on, and the player count of the main menu
/// (DS:47DA). Turned into a <see cref="GameSetup"/> when the player selection ends.
/// </summary>
public sealed class PlayerRoster
{
    private readonly PlayerSlot[] _slots;
    private int _players = 2;

    /// <summary>
    /// [ORIGINAL] O2:3D49 calls O2:0216 for n = 1..10 before the main menu: name "Player n", control keyboard
    /// (BOMB.OVR 0x00A7E).
    /// </summary>
    public PlayerRoster()
    {
        _slots = new PlayerSlot[GameLimits.MaxPlayers];
        for (var i = 0; i < _slots.Length; i++)
            _slots[i] = new PlayerSlot(i + 1);
    }

    /// <summary>Number of players taking part (2..10 from the main menu).</summary>
    public int Players
    {
        get => _players;
        set
        {
            if (!GameLimits.IsValidPlayerCount(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            _players = value;
        }
    }

    /// <summary>All ten records, index 0 = player 1.</summary>
    public IReadOnlyList<PlayerSlot> Slots => _slots;

    /// <summary>Record of player <paramref name="number"/> (1..10).</summary>
    public PlayerSlot this[int number] =>
        number is >= 1 and <= GameLimits.MaxPlayers
            ? _slots[number - 1]
            : throw new ArgumentOutOfRangeException(nameof(number));

    /// <summary>
    /// End of the player selection, O2:5A2C–5AC9 (BOMB.OVR 0x06014): the table of intelligences DS:3BE8 (11 far
    /// pointers) is cleared; computer players called "Random" get the name of intelligence Random(7) + 1 (in player
    /// order, one call each); O2:0078 installs the intelligence of each name; DS:4812 says whether all players are
    /// computers. The records keep the resolved names, as in the original.
    /// </summary>
    /// <param name="random">Turbo Pascal Random(n) of the game; called with n = 7 once per "Random" computer.</param>
    public GameSetup Start(Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var players = new List<PlayerSetup>(_players);
        var allComputers = true;
        for (var n = 1; n <= _players; n++)
        {
            var slot = this[n];
            if (slot.Control == PlayerControl.Computer && slot.Name == ComputerOpponents.Names[ComputerOpponents.Random])
            {
                var pick = random(ComputerOpponents.IntelligenceCount);
                if (pick is < 0 or >= ComputerOpponents.IntelligenceCount)
                    throw new InvalidOperationException($"Random(7) returned {pick}.");
                slot.Name = ComputerOpponents.Names[pick + 1];
            }

            // [ORIGINAL] O2:0078 compares the name with all seven intelligences, whatever the control.
            var intelligence = ComputerOpponents.Find(slot.Name);
            players.Add(new PlayerSetup(n, slot.Name, slot.Control, intelligence is > 0 ? intelligence : null, Team: 0));
            if (slot.Control != PlayerControl.Computer)
                allComputers = false;
        }

        return new GameSetup(players, allComputers);
    }
}

/// <summary>A player as the game starts with it.</summary>
/// <param name="Number">Player number 1..10.</param>
/// <param name="Name">Name; for a computer the name of its intelligence.</param>
/// <param name="Control">Who controls the player.</param>
/// <param name="Intelligence">
/// Index 1..7 into <see cref="ComputerOpponents.Names"/> if the name is the name of an intelligence (O2:0078 installs it
/// for that player whatever the control), otherwise null.
/// </param>
/// <param name="Team">
/// Team number; 0 = none. [APPROX] Always 0 here: teams come from the team setup O2:2749 (BOMB.OVR 0x02D31), which
/// O2:3D49 calls after the selection only with "Team Play" on and which is not part of this screen yet.
/// </param>
public sealed record PlayerSetup(int Number, string Name, PlayerControl Control, int? Intelligence, int Team);

/// <summary>Players of a game as the player selection leaves them.</summary>
/// <param name="Players">Players 1..count in order.</param>
/// <param name="AllComputers">[ORIGINAL] DS:4812: every player is a computer (O2:5A2C, O2:5ABE).</param>
public sealed record GameSetup(IReadOnlyList<PlayerSetup> Players, bool AllComputers);
