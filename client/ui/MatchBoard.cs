using System.Globalization;
using Godot;
using Vantage.Shared.Game;
using Vantage.Shared.Protocol;

/// <summary>
/// Placeholder scoreboard and end-of-match screen, centered on the screen. It only displays the newest server
/// match-state and scoreboard messages the client stored: the scoreboard while the scoreboard key is held, and
/// the end-of-match screen (result, final scores, the same table, countdown) while the phase is Postgame.
/// The result word comes from the shared <see cref="MatchRules.OutcomeOf"/> rule and the row order from
/// <see cref="ScoreboardOrder"/>; the client decides nothing itself. It is created on the welcome and freed on disconnect.
/// </summary>
public partial class MatchBoard : CanvasLayer
{
    private const string ViewKey = "key";
    private const string ViewPostgame = "postgame";
    private const int RowFontSize = 18;

    private Control? _panel;
    private Label? _result;
    private Control? _scoreRow;
    private Label? _blueScore;
    private Label? _redScore;
    private Label? _blueHeader;
    private Label? _redHeader;
    private Container? _blueRows;
    private Container? _redRows;
    private Label? _next;

    // What is shown now, so a line is printed only when it changes.
    private string? _shownView;
    private string? _shownRows;
    private string? _builtKey;
    private bool _resultLogged;

    public override void _Ready()
    {
        _panel = GetNode<Control>("Root/Panel");
        _result = GetNode<Label>("Root/Panel/Column/ResultLabel");
        _scoreRow = GetNode<Control>("Root/Panel/Column/ScoreRow");
        _blueScore = GetNode<Label>("Root/Panel/Column/ScoreRow/BlueScoreLabel");
        _redScore = GetNode<Label>("Root/Panel/Column/ScoreRow/RedScoreLabel");
        _blueHeader = GetNode<Label>("Root/Panel/Column/BlueHeader");
        _redHeader = GetNode<Label>("Root/Panel/Column/RedHeader");
        _blueRows = GetNode<Container>("Root/Panel/Column/BlueRows");
        _redRows = GetNode<Container>("Root/Panel/Column/RedRows");
        _next = GetNode<Label>("Root/Panel/Column/NextLabel");
        Visible = false;
    }

    /// <summary>
    /// Shows the stored server messages. <paramref name="keyHeld"/> is whether the scoreboard key is down,
    /// <paramref name="newestTick"/> is the newest server tick the client knows, and <paramref name="ownEntityId"/>
    /// is the entity id from the welcome. With no match state or no scoreboard yet, nothing is shown.
    /// </summary>
    public void Refresh(MatchStateMessage? state, ScoreboardMessage? board, uint newestTick, uint? ownEntityId, bool keyHeld)
    {
        if (_panel is null || state is not MatchStateMessage match || board is null)
        {
            HideAll();
            return;
        }

        bool postgame = match.Phase == MatchPhase.Postgame;
        string? view = postgame ? ViewPostgame : keyHeld ? ViewKey : null;
        if (view is null)
        {
            HideAll();
            return;
        }

        var stats = board.Rows.Select(r => new PlayerMatchStats(r.EntityId, r.Faction, r.Kills, r.Deaths)).ToArray();
        var blue = ScoreboardOrder.ForTeam(stats, Faction.DevBlue);
        var red = ScoreboardOrder.ForTeam(stats, Faction.DevRed);
        string rowsText = string.Join(",", blue.Concat(red).Select(r =>
            string.Create(CultureInfo.InvariantCulture, $"{r.Team}:{r.EntityId}:{r.Kills}:{r.Deaths}")));

        int seconds = MatchClock.SecondsLeft(match.PhaseEndTick, newestTick);
        if (postgame && !_resultLogged)
        {
            _resultLogged = true;
            GD.Print($"[GameClient] result outcome={MatchRules.OutcomeOf(match.BlueScore, match.RedScore)}; blue={match.BlueScore}; red={match.RedScore}; next-in={seconds}");
        }

        // The board line describes the view and the rows; the rest of the display (scores, own line) is rebuilt below.
        if (_shownView != view || _shownRows != rowsText)
        {
            _shownView = view;
            _shownRows = rowsText;
            GD.Print($"[GameClient] board view={view}; rows={rowsText}");
        }

        string builtKey = string.Create(CultureInfo.InvariantCulture,
            $"{view}|{match.BlueScore}|{match.RedScore}|{ownEntityId}|{rowsText}");
        if (_builtKey != builtKey)
        {
            _builtKey = builtKey;
            Build(view, match, blue, red, ownEntityId);
        }

        if (postgame) _next!.Text = $"Next match in {MatchClock.Format(seconds)}";
        Visible = true;
    }

    /// <summary>Hides both views and forgets what was shown, so a reconnect starts clean.</summary>
    public void Clear() => HideAll();

    private void HideAll()
    {
        if (_shownView is not null) GD.Print("[GameClient] board view=hidden");
        _shownView = null;
        _shownRows = null;
        _builtKey = null;
        _resultLogged = false;
        Visible = false;
    }

    private void Build(string view, MatchStateMessage match, IReadOnlyList<PlayerMatchStats> blue,
        IReadOnlyList<PlayerMatchStats> red, uint? ownEntityId)
    {
        bool postgame = view == ViewPostgame;
        _result!.Visible = postgame;
        _scoreRow!.Visible = postgame;
        _next!.Visible = postgame;
        if (postgame)
        {
            MatchOutcome outcome = MatchRules.OutcomeOf(match.BlueScore, match.RedScore);
            (string text, Color color) = outcome switch
            {
                MatchOutcome.BlueWins => ("BLUE WINS", TeamColors.DevBlue),
                MatchOutcome.RedWins => ("RED WINS", TeamColors.DevRed),
                _ => ("DRAW", Colors.White),
            };
            _result!.Text = text;
            _result.AddThemeColorOverride("font_color", color);
            _blueScore!.Text = match.BlueScore.ToString(CultureInfo.InvariantCulture);
            _redScore!.Text = match.RedScore.ToString(CultureInfo.InvariantCulture);
            _blueScore.AddThemeColorOverride("font_color", TeamColors.DevBlue);
            _redScore.AddThemeColorOverride("font_color", TeamColors.DevRed);
        }

        BuildTeam(_blueHeader!, _blueRows!, Faction.DevBlue, match.BlueScore, blue, ownEntityId);
        BuildTeam(_redHeader!, _redRows!, Faction.DevRed, match.RedScore, red, ownEntityId);
    }

    private static void BuildTeam(Label header, Container rows, Faction team, int teamScore,
        IReadOnlyList<PlayerMatchStats> players, uint? ownEntityId)
    {
        TeamColors.TryGet(team, out Color color);
        header.Text = string.Create(CultureInfo.InvariantCulture, $"{TeamColors.Word(team)}  {teamScore}");
        header.AddThemeColorOverride("font_color", color);

        foreach (Node child in rows.GetChildren())
        {
            rows.RemoveChild(child);
            child.QueueFree();
        }

        if (players.Count == 0)
        {
            rows.AddChild(MakeLabel("no players", new Color(0.7f, 0.7f, 0.7f), HorizontalAlignment.Left));
            return;
        }

        var grid = new GridContainer { Columns = 3, MouseFilter = Control.MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", 28);
        grid.AddThemeConstantOverride("v_separation", 0);
        var caption = new Color(0.65f, 0.65f, 0.65f);
        grid.AddChild(MakeLabel("PLAYER", caption, HorizontalAlignment.Left, expand: true));
        grid.AddChild(MakeLabel("KILLS", caption, HorizontalAlignment.Right));
        grid.AddChild(MakeLabel("DEATHS", caption, HorizontalAlignment.Right));
        foreach (PlayerMatchStats row in players)
        {
            bool own = ownEntityId == row.EntityId;
            // Own line: the team color pushed toward white. Others: the team color slightly dimmed.
            Color lineColor = own ? color.Lerp(Colors.White, 0.55f) : color.Darkened(0.2f);
            string name = string.Create(CultureInfo.InvariantCulture, $"Player {row.EntityId}") + (own ? "  (YOU)" : string.Empty);
            grid.AddChild(MakeLabel(name, lineColor, HorizontalAlignment.Left, expand: true));
            grid.AddChild(MakeLabel(row.Kills.ToString(CultureInfo.InvariantCulture), lineColor, HorizontalAlignment.Right));
            grid.AddChild(MakeLabel(row.Deaths.ToString(CultureInfo.InvariantCulture), lineColor, HorizontalAlignment.Right));
        }

        rows.AddChild(grid);
    }

    private static Label MakeLabel(string text, Color color, HorizontalAlignment align, bool expand = false)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = align,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
            CustomMinimumSize = new Vector2(expand ? 190 : 60, 0),
        };
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 4);
        label.AddThemeFontSizeOverride("font_size", RowFontSize);
        return label;
    }
}
