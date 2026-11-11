using System.Globalization;
using BombRedone.Core.Setup;
using BombRedone.Formats;
using BombRedone.UI.Text;

namespace BombRedone.UI.Screens;

/// <summary>
/// Team names of the original: O2:35CF (BOMB.OVR 0x03BB7) with the name entry O2:31A8 and its cells O2:2EBD / O2:300A,
/// called by O2:3D49 after the team setup; docs/anforderungen/spielerauswahl.md §8–9.
/// </summary>
/// <remarks>
/// [ORIGINAL] O2:3F2C–3F88 restores the whole screen from the save buffer DS:E5BC (UCC8:0AC0 along 520 lines) before
/// O2:35CF; the screen starts on the buffer. [APPROX] The wipe itself is not animated. Teams of computers are named
/// without input; for every other team the name entry waits for keys (one key per pass).
/// </remarks>
public sealed class PlayerSelectTeamNamesScreen : IScreen
{
    /// <summary>[ORIGINAL] O2:2EBD / O2:300A: characters of the team name entry at x = 143 + 6 · (k − 1), y = 104.</summary>
    public const int NameFirstX = 143, NamePitch = 6, NameY = 104;

    /// <summary>[ORIGINAL] O2:3B62–3B90: at the end the columns 103..250 come back from the save buffer.</summary>
    public const int RestoreLeft = 103, RestoreRight = 250;

    /// <summary>O2 offsets of <see cref="TeamPlay.RandomNames"/> (O2:38F1–3A80).</summary>
    private static readonly int[] RandomNameOffsets =
        [0x355D, 0x3569, 0x3574, 0x3582, 0x3588, 0x3596, 0x35A1, 0x35AB, 0x35B6, 0x35BF, 0x35C5];

    /// <summary>Texts of the team names with their O2 offsets (BOMB.OVR 0x005E8 + offset, length byte).</summary>
    public static IReadOnlyList<(int FileOffset, string Text)> OriginalTexts { get; } =
    [
        .. new (int Offset, string Text)[]
        {
            (0x3116, "New Team Name:"), (0x354E, "Team "), (0x3554, "Team #"), (0x355B, ":"),
        }.Select(t => (PlayerSelectScreen.OverlayO2FileOffset + t.Offset, t.Text)),
        .. TeamPlay.RandomNames.Select((name, i) => (PlayerSelectScreen.OverlayO2FileOffset + RandomNameOffsets[i], name)),
    ];

    private readonly MenuScreen _menu;
    private readonly PlayerRoster _roster;
    private readonly GameSetup _setup;
    private readonly Func<int, int> _random;
    private readonly Func<GameSetup, IScreen?> _start;
    private readonly GraphText _text;
    private readonly Queue<ushort> _keys = new();
    private readonly IReadOnlyList<TeamRecord> _teams;
    private int _team;
    private bool _entering, _finished;

    // O2:31A8: characters 1..14 (index 0 unused; 14 is the byte behind the String[13], the flag +0Eh = 0 of a team with
    // a human), length, cursor [bp-2], new cursor [bp-4], changed [bp-C], finished [bp-B].
    private readonly char[] _chars = new char[TeamRecord.MaxNameLength + 2];
    private int _length, _cursor, _newCursor;
    private bool _changed, _done;

    /// <param name="menu">Main menu: save buffer, palette, fonts.</param>
    /// <param name="roster">Player records with their team numbers.</param>
    /// <param name="setup">Players as the player selection ended.</param>
    /// <param name="random">Turbo Pascal Random(n); Random(11) per try for the name of an all-computer team.</param>
    /// <param name="start">Called with the setup including the teams; returns the next screen.</param>
    public PlayerSelectTeamNamesScreen(MenuScreen menu, PlayerRoster roster, GameSetup setup, Func<int, int> random,
        Func<GameSetup, IScreen?> start)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(start);
        _menu = menu;
        _roster = roster;
        _setup = setup;
        _random = random;
        _start = start;
        _text = new GraphText(menu.Fonts.Default);
        menu.Saved.Pixels.CopyTo(Screen.Pixels, 0);
        _teams = TeamPlay.CreateTeams(roster);
        Advance();
    }

    /// <summary>Video memory as the original leaves it (palette indices).</summary>
    public Surface Screen { get; } = new();

    /// <summary>The teams 1..player count with their names so far.</summary>
    public IReadOnlyList<TeamRecord> Teams => _teams;

    /// <summary>Team whose name is being entered, or 0.</summary>
    public int EnteringTeam => _entering ? _team : 0;

    /// <summary>Name as typed so far, without the cursor space (for tests).</summary>
    public string NameBeingEntered => new(_chars, 1, Math.Max(0, Math.Min(_length, TeamRecord.MaxNameLength)));

    /// <summary>[ORIGINAL] O2:3867–3887: member k (1-based within the team) has the button (20, 52 + 15 k)–(120, 62 + 15 k).</summary>
    public static (int X1, int Y1, int X2, int Y2) MemberBox(int k) => (20, 52 + 15 * k, 120, 62 + 15 * k);

    public IScreen? Update(TimeSpan elapsed, ScreenInput input)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Time cannot run backwards.");
        _menu.RunPaletteRotation(elapsed);
        foreach (var key in input.Keys)
            _keys.Enqueue(key);
        while (_entering && _keys.Count > 0)
            NamePass(_keys.Dequeue());
        if (!_finished)
            return this;
        return _start(TeamPlay.Apply(_setup, _roster, _teams));
    }

    public void Draw(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (surface.Width != Screen.Width || surface.Height != Screen.Height)
            throw new ArgumentException("The team names draw into a 320 x 200 surface only.", nameof(surface));
        Screen.Pixels.CopyTo(surface.Pixels, 0);
        Span<byte> dac = stackalloc byte[Palette.ByteLength];
        PlayerSelectTeamScreen.PlayerDac(_menu, dac);
        MenuGraphics.ApplyDac(dac, surface);
    }

    /// <summary>
    /// [ORIGINAL] O2:3762–3B5F for the teams from <see cref="_team"/> + 1 on: a team with members is shown, a team with a
    /// human gets the name entry, an all-computer team of two or more a random name; then the member buttons are
    /// restored from the save buffer. At the end O2:3B62.
    /// </summary>
    private void Advance()
    {
        while (++_team <= _teams.Count)
        {
            var team = _teams[_team - 1];
            if (team.Members.Count == 0)
                continue;
            DrawTeam(team);
            if (!team.AllComputers)
            {
                OpenName(team);
                return;
            }

            if (team.Members.Count > 1)
                TeamPlay.ChooseRandomName(_teams, team.Number, _random);
            RestoreMembers(team);
        }

        MenuGraphics.CopyRect(_menu.Saved, Screen, RestoreLeft, 0, RestoreRight, Screen.Height - 1);
        _finished = true;
    }

    /// <summary>
    /// [ORIGINAL] O2:3777–38C3: button (10, 50)–(110, 60); SmallFont size 4 with SetUserCharSize(1, 1, 1, 1), left/top;
    /// "Team #" + Str(t) + ":" brown (6) at (15, 50); member k (player p) on the button <see cref="MemberBox"/>, the name
    /// in colour 19 + p at (25, 51 + 15 k).
    /// </summary>
    private void DrawTeam(TeamRecord team)
    {
        MenuGraphics.Frame(Screen, 10, 50, 110, 60, MenuGraphics.FrameFace, 15, MenuGraphics.FrameDark);
        _text.SetTextStyle(_menu.Fonts.Small, TextDirection.Horizontal, 4);
        _text.SetUserCharSize(1, 1, 1, 1);
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        _text.Color = 6;
        _text.OutTextXY(Screen, 15, 50, "Team #" + team.Number.ToString(CultureInfo.InvariantCulture) + ":");
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        for (var k = 1; k <= team.Members.Count; k++)
        {
            var p = team.Members[k - 1];
            _text.Color = (byte)(PlayerSelectScreen.PlayerColorBase + p);
            var (x1, y1, x2, y2) = MemberBox(k);
            MenuGraphics.Frame(Screen, x1, y1, x2, y2, MenuGraphics.FrameFace, 15, MenuGraphics.FrameDark);
            _text.OutTextXY(Screen, 25, 51 + 15 * k, _roster[p].Name);
        }
    }

    /// <summary>[ORIGINAL] O2:3AE0–3B55: (18, 50 + 15 k)–(122, 64 + 15 k) per member from the save buffer (UCC8:00CC).</summary>
    private void RestoreMembers(TeamRecord team)
    {
        for (var k = 1; k <= team.Members.Count; k++)
        {
            var (x1, y1, x2, y2) = MemberBox(k);
            MenuGraphics.CopyRect(_menu.Saved, Screen, x1 - 2, y1 - 2, x2 + 2, y2 + 2);
        }
    }

    /// <summary>
    /// [ORIGINAL] O2:31A8–3316 (BOMB.OVR 0x03790): pad the name with spaces up to 13 (the length stays); button
    /// (135, 85)–(235, 115); "New Team Name:" magenta (5) left/top at (138, 85); sunken field (140, 100)–(230, 110); the
    /// characters black, left/centre, one by one; the length grows by one and the cursor stands there. The box is not
    /// saved: it stays until the columns are restored at the end.
    /// </summary>
    private void OpenName(TeamRecord team)
    {
        _entering = true;
        Array.Fill(_chars, ' ');
        _chars[TeamRecord.MaxNameLength + 1] = '\0';
        team.Name.CopyTo(0, _chars, 1, team.Name.Length);
        _length = team.Name.Length;
        MenuGraphics.Frame(Screen, 135, 85, 235, 115, MenuGraphics.FrameFace, 15, MenuGraphics.FrameDark);
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        _text.SetUserCharSize(1, 1, 1, 1);
        _text.Color = 5;
        _text.OutTextXY(Screen, 138, 85, "New Team Name:");
        MenuGraphics.Frame(Screen, 140, 100, 230, 110, MenuGraphics.FrameFace, MenuGraphics.FrameDark, 15);
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Center);
        _text.Color = 0;
        for (var k = 1; k <= _length; k++)
            _text.OutTextXY(Screen, NameX(k), NameY, _chars[k].ToString());
        _length++;
        _cursor = _length;
        _done = false;
        DrawCell(_cursor, cursor: true);
    }

    private static int NameX(int position) => NameFirstX + NamePitch * (position - 1);

    /// <summary>
    /// [ORIGINAL] O2:2EBD (with cursor) / O2:300A: three nested rectangles in the face colour clear the cell
    /// (x − 1, 100)–(x + 4, 110), the character is drawn black at (x, 104); O2:2EBD also copies (x − 1, 109)–(x + 4, 110)
    /// from the save buffer as the cursor.
    /// </summary>
    private void DrawCell(int position, bool cursor)
    {
        var x = NameX(position);
        MenuGraphics.Rectangle(Screen, x - 1, 100, x + 4, 110, MenuGraphics.FrameFace);
        MenuGraphics.Rectangle(Screen, x, 101, x + 3, 109, MenuGraphics.FrameFace);
        MenuGraphics.Rectangle(Screen, x + 1, 102, x + 2, 108, MenuGraphics.FrameFace);
        _text.Color = 0;
        _text.OutTextXY(Screen, x, NameY, _chars[position].ToString());
        if (cursor)
            MenuGraphics.CopyRect(_menu.Saved, Screen, x - 1, 109, x + 4, 110);
    }

    /// <summary>
    /// [ORIGINAL] One key of the loop O2:331A–34FD (ReadKey without UpCase): Backspace (set O2:3125) clears before the
    /// cursor; Enter and Esc (O2:3145) finish; 32..125 (O2:3165) are written at the cursor; extended: Home clears the
    /// name (name := "  ", no redraw), Del clears at the cursor, End goes to the end, Left/Right (DS:3719/3718) move.
    /// </summary>
    private void NamePass(ushort word)
    {
        _changed = false;
        var character = (byte)(word & 0xFF);
        if (character == 8 && _cursor > 1)
        {
            _newCursor = _cursor - 1;
            _chars[_newCursor] = ' ';
            _changed = true;
        }

        if (character is 13 or 27)
        {
            _done = true;
            _changed = true;
        }

        if (character is >= 32 and <= 125)
        {
            _chars[_cursor] = (char)character;
            _changed = true;
            _newCursor = _cursor + 1;
            if (_length < _newCursor)
                _length++;
        }

        if (character == 0)
        {
            var scan = (byte)(word >> 8);
            if (scan == 0x47)
            {
                // [ORIGINAL] O2:33D7–3437.
                if (_length >= 1)
                {
                    for (_cursor = _length; ; _cursor--)
                    {
                        _chars[_cursor] = ' ';
                        DrawCell(_cursor, cursor: true);
                        DrawCell(_cursor, cursor: false);
                        if (_cursor == 1)
                            break;
                    }
                }

                _chars[1] = ' ';
                _chars[2] = ' ';
                _length = 2;
                _newCursor = 1;
            }

            if (scan == 0x53)
            {
                _chars[_cursor] = ' ';
                _changed = true;
                _newCursor = _cursor;
            }

            if (scan == 0x4F)
            {
                _changed = true;
                _newCursor = _length;
            }

            if (scan == 0x4B && _cursor > 1)
            {
                _newCursor = _cursor - 1;
                _changed = true;
            }

            if (scan == 0x4D && _length > _cursor)
            {
                _newCursor = _cursor + 1;
                _changed = true;
            }
        }

        if (!_changed)
            return;

        // [ORIGINAL] O2:34C5–34FD.
        _newCursor = Math.Min(_newCursor, TeamRecord.MaxNameLength);
        _length = Math.Min(_length, TeamRecord.MaxNameLength);
        DrawCell(_cursor, cursor: false);
        _cursor = _newCursor;
        if (!_done)
        {
            DrawCell(_cursor, cursor: true);
            return;
        }

        // [ORIGINAL] O2:3500–3546: drop trailing characters of the set O2:3188 = {0, 32}.
        while (_length > 0 && _chars[_length] is ' ' or '\0')
            _length--;
        var team = _teams[_team - 1];
        team.Name = new string(_chars, 1, _length);
        _entering = false;
        RestoreMembers(team);
        Advance();
    }
}
