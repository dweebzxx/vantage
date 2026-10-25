using Godot;

namespace Vantage.Server;

public partial class TickLoop : Node
{
    // Zero means no simulation tick has run. The first callback emits tick 1.
    // uint ticks wrap modulo 2^32, as required by the shared TickMath contract.
    public uint CurrentTick { get; private set; }
    public event Action<uint>? Tick;

    public override void _PhysicsProcess(double delta)
    {
        CurrentTick = unchecked(CurrentTick + 1u);
        Tick?.Invoke(CurrentTick);
    }
}
