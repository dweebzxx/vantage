using Vantage.Shared.Protocol;

namespace Vantage.Shared.Net;

// Server counters already range/order-check; retain the same defensive gate here.
// The first accepted command establishes
// the baseline: a held bit without an observed released-to-pressed edge is not a press.
public sealed class PendingCombatPresses
{
    private InputCommand? _previous;
    private float? _fireAim;
    private float? _grenadeAim;

    public void Accept(InputCommand input)
    {
        if (!input.IsInRange() || (_previous is InputCommand latest && !TickMath.IsNewerThan(input.ClientTick, latest.ClientTick))) return;
        if (_previous is InputCommand previous)
        {
            InputButtons rising = input.Buttons & ~previous.Buttons;
            if ((rising & InputButtons.PrimaryFire) != 0) _fireAim ??= input.AimAngleRadians;
            if ((rising & InputButtons.Grenade) != 0) _grenadeAim ??= input.AimAngleRadians;
        }
        _previous = input;
    }

    public (float? FireAim, float? GrenadeAim) Consume()
    {
        var result = (_fireAim, _grenadeAim);
        _fireAim = _grenadeAim = null;
        return result;
    }

    public void ClearPending() => _fireAim = _grenadeAim = null;
}
