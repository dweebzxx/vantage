using MemoryPack;
using Vantage.Shared.Game;

namespace Vantage.Shared.Protocol;

/// <summary>One player's line on the scoreboard.</summary>
[MemoryPackable]
public readonly partial record struct ScoreboardRow(uint EntityId, Faction Faction, int Kills, int Deaths);
