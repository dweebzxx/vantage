namespace Vantage.Shared.Game;

public readonly record struct PlayerMatchStats(uint EntityId, Faction Team, int Kills, int Deaths);
