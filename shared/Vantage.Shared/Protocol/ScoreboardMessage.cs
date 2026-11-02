using MemoryPack;

namespace Vantage.Shared.Protocol;

/// <summary>
/// The per-player scoreboard. Row order is the sender's order; an empty array is valid.
/// </summary>
[MemoryPackable]
public sealed partial record ScoreboardMessage(uint ServerTick, ScoreboardRow[] Rows);
