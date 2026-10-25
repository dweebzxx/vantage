using Godot;
using Vantage.Shared.Net;

namespace Vantage.Server;

// Immutable placeholder: entity IDs only; no entity or gameplay state exists yet.
public sealed record WorldState(uint Tick)
{
    public IReadOnlyList<uint> Entities { get; } = Array.Empty<uint>();
}

public partial class World : Node
{
    // 64 ticks retain about 1.07 seconds at 60 Hz, ready for later history consumers.
    public const int HistoryCapacity = TickStateRingBuffer<WorldState>.DefaultCapacity;

    public WorldState CurrentState { get; private set; } = new(0u);
    public TickStateRingBuffer<WorldState> History { get; } = new(HistoryCapacity);

    public void RecordTick(uint tick)
    {
        CurrentState = new WorldState(tick);
        History.Add(tick, CurrentState);
    }
}
