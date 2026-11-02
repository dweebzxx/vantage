namespace Vantage.Shared.Game;

/// <summary>One player moved between teams by <see cref="TeamAssignment.Rebalance"/>.</summary>
public readonly record struct TeamMove(uint EntityId, Faction From, Faction To);
