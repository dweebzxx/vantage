using System.Numerics;
using MemoryPack;
using Vantage.Shared.Protocol;
using Xunit;

namespace Vantage.Shared.Tests;

public sealed class ProtocolSerializationTests
{
    [Fact]
    public void InputCommand_round_trip_preserves_every_field()
    {
        InputCommand original = new InputCommand(17u, new Vector2(-0.75f, 0.5f), 1.25f, InputButtons.PrimaryFire | InputButtons.Grenade);

        InputCommand actual = MemoryPackSerializer.Deserialize<InputCommand>(MemoryPackSerializer.Serialize(original));

        Assert.Equal(17u, actual.ClientTick);
        Assert.Equal(new Vector2(-0.75f, 0.5f), actual.Move);
        Assert.Equal(1.25f, actual.AimAngleRadians);
        Assert.Equal(InputButtons.PrimaryFire | InputButtons.Grenade, actual.Buttons);
    }

    [Fact]
    public void PlayerState_round_trip_preserves_every_field()
    {
        PlayerState original = new PlayerState(23u, new Vector2(-10.5f, 40.25f), -2.5f);

        PlayerState actual = MemoryPackSerializer.Deserialize<PlayerState>(MemoryPackSerializer.Serialize(original));

        Assert.Equal(23u, actual.EntityId);
        Assert.Equal(new Vector2(-10.5f, 40.25f), actual.Position);
        Assert.Equal(-2.5f, actual.AimAngleRadians);
    }

    [Fact]
    public void ProjectileState_round_trip_preserves_every_field()
    {
        ProjectileState original = new ProjectileState(31u, new Vector2(12.5f, -8.25f));

        ProjectileState actual = MemoryPackSerializer.Deserialize<ProjectileState>(MemoryPackSerializer.Serialize(original));

        Assert.Equal(31u, actual.EntityId);
        Assert.Equal(new Vector2(12.5f, -8.25f), actual.Position);
    }

    [Fact]
    public void GrenadeState_round_trip_preserves_every_field()
    {
        GrenadeState original = new GrenadeState(41u, new Vector2(-3.5f, 9.25f));

        GrenadeState actual = MemoryPackSerializer.Deserialize<GrenadeState>(MemoryPackSerializer.Serialize(original));

        Assert.Equal(41u, actual.EntityId);
        Assert.Equal(new Vector2(-3.5f, 9.25f), actual.Position);
    }

    [Fact]
    public void HitEvent_round_trip_preserves_every_field()
    {
        HitEvent original = new HitEvent(101u, 23u, 47u);

        HitEvent actual = MemoryPackSerializer.Deserialize<HitEvent>(MemoryPackSerializer.Serialize(original));

        Assert.Equal(101u, actual.ServerTick);
        Assert.Equal(23u, actual.SourceEntityId);
        Assert.Equal(47u, actual.TargetEntityId);
    }

    [Fact]
    public void DamageDealtEvent_round_trip_preserves_every_field()
    {
        DamageDealtEvent original = new DamageDealtEvent(102u, 29u, 53u, 40);

        DamageDealtEvent actual = MemoryPackSerializer.Deserialize<DamageDealtEvent>(MemoryPackSerializer.Serialize(original));

        Assert.Equal(102u, actual.ServerTick);
        Assert.Equal(29u, actual.SourceEntityId);
        Assert.Equal(53u, actual.TargetEntityId);
        Assert.Equal(40, actual.Amount);
    }

    [Fact]
    public void DefeatEvent_round_trip_preserves_every_field()
    {
        DefeatEvent original = new DefeatEvent(103u, 37u, 59u);

        DefeatEvent actual = MemoryPackSerializer.Deserialize<DefeatEvent>(MemoryPackSerializer.Serialize(original));

        Assert.Equal(103u, actual.ServerTick);
        Assert.Equal(37u, actual.SourceEntityId);
        Assert.Equal(59u, actual.TargetEntityId);
    }

    [Fact]
    public void StateSnapshot_round_trip_preserves_populated_arrays_and_every_field()
    {
        StateSnapshot original = new(200u, 190u,
            [new(23u, new(-10.5f, 40.25f), -2.5f), new(24u, new(50.0f, -7.0f), 0.5f)],
            [new(31u, new(12.5f, -8.25f)), new(32u, new(6.0f, 7.0f))],
            [new(41u, new(-3.5f, 9.25f)), new(42u, new(8.0f, 9.0f))]);

        StateSnapshot? actual = MemoryPackSerializer.Deserialize<StateSnapshot>(MemoryPackSerializer.Serialize(original));

        Assert.NotNull(actual);
        Assert.Equal(200u, actual.ServerTick);
        Assert.Equal(190u, actual.AcknowledgedClientTick);
        Assert.Equal(new PlayerState[] { new(23u, new(-10.5f, 40.25f), -2.5f), new(24u, new(50.0f, -7.0f), 0.5f) }, actual.Players);
        Assert.Equal(new ProjectileState[] { new(31u, new(12.5f, -8.25f)), new(32u, new(6.0f, 7.0f)) }, actual.Projectiles);
        Assert.Equal(new GrenadeState[] { new(41u, new(-3.5f, 9.25f)), new(42u, new(8.0f, 9.0f)) }, actual.Grenades);
    }

    [Fact]
    public void StateSnapshot_round_trip_preserves_empty_arrays()
    {
        StateSnapshot original = new(uint.MaxValue, 0u, [], [], []);

        StateSnapshot? actual = MemoryPackSerializer.Deserialize<StateSnapshot>(MemoryPackSerializer.Serialize(original));

        Assert.NotNull(actual);
        Assert.Equal(uint.MaxValue, actual.ServerTick);
        Assert.Equal(0u, actual.AcknowledgedClientTick);
        Assert.NotNull(actual.Players);
        Assert.NotNull(actual.Projectiles);
        Assert.NotNull(actual.Grenades);
        Assert.Empty(actual.Players);
        Assert.Empty(actual.Projectiles);
        Assert.Empty(actual.Grenades);
    }
}
