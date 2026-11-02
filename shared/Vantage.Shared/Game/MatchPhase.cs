namespace Vantage.Shared.Game;

/// <summary>The phases of a team deathmatch match. Transitions are listed in <see cref="MatchRules.IsValidTransition"/>.</summary>
public enum MatchPhase : byte
{
    /// <summary>No players are connected.</summary>
    Idle = 0,

    /// <summary>Free play before a match; nothing scores.</summary>
    Warmup = 1,

    /// <summary>The timed match; kills score.</summary>
    Playing = 2,

    /// <summary>The post-match period before the next warmup.</summary>
    Postgame = 3,
}
