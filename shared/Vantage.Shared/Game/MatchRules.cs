namespace Vantage.Shared.Game;

public static class MatchRules
{
    // Human rulings H1 to H3 (2026-11-01).
    public const int WarmupSeconds = 10;
    public const int MatchSeconds = 300;
    public const int PostgameSeconds = 15;

    public static bool IsValidTransition(MatchPhase from, MatchPhase to) => (from, to) switch
    {
        (MatchPhase.Idle, MatchPhase.Warmup) => true,
        (MatchPhase.Warmup, MatchPhase.Playing) => true,
        (MatchPhase.Playing, MatchPhase.Postgame) => true,
        (MatchPhase.Postgame, MatchPhase.Warmup) => true,
        (MatchPhase.Warmup, MatchPhase.Idle) => true,
        (MatchPhase.Playing, MatchPhase.Idle) => true,
        (MatchPhase.Postgame, MatchPhase.Idle) => true,
        _ => false,
    };

    /// <summary>True for the two teams of a match: <see cref="Faction.DevBlue"/> and <see cref="Faction.DevRed"/>.</summary>
    public static bool IsTeam(Faction faction) => faction is Faction.DevBlue or Faction.DevRed;

    /// <summary>
    /// The outcome of a match from the two team scores: the higher score wins and equal scores are a draw
    /// (Human ruling H4, 2026-11-01). Scores are ordinary integers; nothing here rejects a negative one.
    /// </summary>
    public static MatchOutcome OutcomeOf(int blueScore, int redScore) =>
        blueScore > redScore ? MatchOutcome.BlueWins :
        redScore > blueScore ? MatchOutcome.RedWins :
        MatchOutcome.Draw;
}
