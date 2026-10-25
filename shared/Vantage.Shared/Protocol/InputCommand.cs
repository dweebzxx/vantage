using System.Numerics;
using MemoryPack;

namespace Vantage.Shared.Protocol;

[Flags]
public enum InputButtons : byte
{
    None = 0,
    PrimaryFire = 1,
    SecondaryFire = 2,
    Grenade = 4,
}

[MemoryPackable]
public readonly partial record struct InputCommand(
    uint ClientTick,
    Vector2 Move,
    float AimAngleRadians,
    InputButtons Buttons)
{
    public bool IsInRange() =>
        float.IsFinite(Move.X) && float.IsFinite(Move.Y) &&
        Move.X >= -1.0f && Move.X <= 1.0f &&
        Move.Y >= -1.0f && Move.Y <= 1.0f &&
        float.IsFinite(AimAngleRadians);

    /// <summary>
    /// Clamps movement components independently, replacing NaN with zero.
    /// Nonfinite aim becomes zero; finite angles, tick, and button bits are preserved.
    /// This helper does not decide whether a server accepts an input.
    /// </summary>
    public InputCommand Clamped() => this with
    {
        Move = new Vector2(ClampComponent(Move.X), ClampComponent(Move.Y)),
        AimAngleRadians = float.IsFinite(AimAngleRadians) ? AimAngleRadians : 0.0f,
    };

    private static float ClampComponent(float value) =>
        float.IsNaN(value) ? 0.0f : Math.Clamp(value, -1.0f, 1.0f);
}
