using MemoryPack;

namespace Vantage.Shared.Protocol;

[MemoryPackable]
public sealed partial record StateSnapshot(
    uint ServerTick,
    uint AcknowledgedClientTick,
    PlayerState[] Players,
    ProjectileState[] Projectiles,
    GrenadeState[] Grenades);
