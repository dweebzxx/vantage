using System.Globalization;
using Godot;
using Vantage.Shared.Game;
using Vantage.Shared.Protocol;

/// <summary>
/// Placeholder match HUD at the top center of the screen: phase banner, time left, team scores, own team.
/// It only displays what the server sent. The client creates it on the welcome and frees it on disconnect.
/// </summary>
public partial class MatchHud : CanvasLayer
{
    private Label? _banner;
    private Label? _time;
    private Label? _blue;
    private Label? _colon;
    private Label? _red;
    private Label? _team;
    private (MatchPhase Phase, int Blue, int Red, Faction Team)? _lastLogged;

    public override void _Ready()
    {
        _banner = GetNode<Label>("Root/Column/BannerLabel");
        _time = GetNode<Label>("Root/Column/TimeLabel");
        _blue = GetNode<Label>("Root/Column/ScoreRow/BlueLabel");
        _colon = GetNode<Label>("Root/Column/ScoreRow/ColonLabel");
        _red = GetNode<Label>("Root/Column/ScoreRow/RedLabel");
        _team = GetNode<Label>("Root/Column/TeamLabel");
        _blue.AddThemeColorOverride("font_color", TeamColors.DevBlue);
        _red.AddThemeColorOverride("font_color", TeamColors.DevRed);
        Visible = false;
    }

    /// <summary>
    /// Shows the stored match state. <paramref name="newestTick"/> is the newest server tick the client knows,
    /// and <paramref name="ownFaction"/> is the faction the server sent for the player's own entity
    /// (null before the first snapshot). With no match state yet, the HUD stays hidden.
    /// </summary>
    public void Refresh(MatchStateMessage? state, uint newestTick, Faction? ownFaction)
    {
        if (state is not MatchStateMessage match || _banner is null)
        {
            Visible = false;
            return;
        }

        Visible = true;
        string? bannerText = match.Phase switch
        {
            MatchPhase.Idle => "WAITING FOR PLAYERS",
            MatchPhase.Warmup => "WARMUP",
            MatchPhase.Postgame => "MATCH OVER",
            _ => null,
        };
        _banner.Text = bannerText ?? string.Empty;
        // P38: the end-of-match screen replaces the banner in Postgame.
        _banner.Visible = bannerText is not null && match.Phase != MatchPhase.Postgame;

        int seconds = MatchClock.SecondsLeft(match.PhaseEndTick, newestTick);
        _time!.Text = MatchClock.Format(seconds);
        _time.Visible = match.Phase != MatchPhase.Idle;

        _blue!.Text = string.Create(CultureInfo.InvariantCulture, $"BLUE {match.BlueScore}");
        _colon!.Text = " : ";
        _red!.Text = string.Create(CultureInfo.InvariantCulture, $"{match.RedScore} RED");

        Faction team = ownFaction ?? Faction.None;
        string? word = TeamColors.Word(team);
        _team!.Visible = word is not null;
        if (word is not null)
        {
            _team.Text = $"YOU: {word}";
            if (TeamColors.TryGet(team, out Color color)) _team.AddThemeColorOverride("font_color", color);
        }

        var shown = (match.Phase, match.BlueScore, match.RedScore, team);
        if (_lastLogged != shown)
        {
            _lastLogged = shown;
            GD.Print($"[GameClient] hud phase={match.Phase}; seconds-left={seconds}; blue={match.BlueScore}; red={match.RedScore}; team={team}");
        }
    }
}
