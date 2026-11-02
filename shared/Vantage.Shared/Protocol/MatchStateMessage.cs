using MemoryPack;
using Vantage.Shared.Game;

namespace Vantage.Shared.Protocol;

/// <summary>
/// The match phase, its timer, and the team scores, sent by the server.
/// </summary>
/// <param name="ServerTick">The server tick this message describes.</param>
/// <param name="Phase">The current match phase.</param>
/// <param name="PhaseEndTick">
/// The server tick at which the current phase ends. Equal to <paramref name="ServerTick"/> when the phase is
/// <see cref="MatchPhase.Idle"/>. A client computes the time left as
/// <c>TickMath.Delta(PhaseEndTick, &lt;latest snapshot tick&gt;)</c>.
/// </param>
/// <param name="BlueScore">The <see cref="Faction.DevBlue"/> team score.</param>
/// <param name="RedScore">The <see cref="Faction.DevRed"/> team score.</param>
[MemoryPackable]
public readonly partial record struct MatchStateMessage(
    uint ServerTick,
    MatchPhase Phase,
    uint PhaseEndTick,
    int BlueScore,
    int RedScore);
