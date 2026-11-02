using System.Numerics;
using MemoryPack;
using Vantage.Shared.Game;
using Vantage.Shared.Protocol;
using Xunit;

namespace Vantage.Shared.Tests;

public sealed class ServerMessageCodecTests
{
    private static StateSnapshot Snapshot() => new(78, 43,
        [new(1, new(12.5f, -24), 1.5f, Faction.DevBlue), new(2, new(-160, 96), -0.5f, Faction.DevRed)], [new(7, new(4, 5))], [new(9, new(-2, 8))]);

    [Fact]
    public void Snapshot_envelope_preserves_type_and_every_field()
    {
        byte[] packet = MessageCodec.EncodeSnapshot(Snapshot());
        Assert.Equal(2, packet[0]);
        Assert.Equal(MemoryPackSerializer.Serialize(Snapshot()), packet[1..]);
        Assert.True(MessageCodec.TryDecodeSnapshot(packet, out var actual));
        Assert.NotNull(actual);
        Assert.Equal(78u, actual!.ServerTick);
        Assert.Equal(43u, actual.AcknowledgedClientTick);
        Assert.Equal(new PlayerState[] { new(1, new(12.5f, -24), 1.5f, Faction.DevBlue), new(2, new(-160, 96), -0.5f, Faction.DevRed) }, actual.Players);
        Assert.Equal(new ProjectileState[] { new(7, new(4, 5)) }, actual.Projectiles);
        Assert.Equal(new GrenadeState[] { new(9, new(-2, 8)) }, actual.Grenades);
    }

    [Fact]
    public void Welcome_envelope_preserves_type_entity_and_version()
    {
        byte[] packet = MessageCodec.EncodeWelcome(new(987u, 17));
        Assert.Equal(3, packet[0]);
        Assert.Equal(MemoryPackSerializer.Serialize(new WelcomeMessage(987u, 17)), packet[1..]);
        Assert.True(MessageCodec.TryDecodeWelcome(packet, out var actual));
        Assert.Equal(987u, actual.EntityId);
        Assert.Equal(17, actual.ProtocolVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Server_envelopes_reject_empty_unknown_wrong_type_truncated_corrupt_and_trailing_bytes(bool welcome)
    {
        byte[] valid = welcome ? MessageCodec.EncodeWelcome(new(8, 1)) : MessageCodec.EncodeSnapshot(Snapshot());
        List<byte[]> malformed = [ [], [255, .. valid[1..]], [(byte)(welcome ? 2 : 3), .. valid[1..]],
            [valid[0], 254, 23, 187, 99, 42], [.. valid, 0xAB, 0xCD] ];
        for (int length = 1; length < valid.Length; length++) malformed.Add(valid[..length]);
        foreach (byte[] packet in malformed)
        {
            if (welcome)
            {
                Assert.False(MessageCodec.TryDecodeWelcome(packet, out var actual));
                Assert.Equal(default, actual);
            }
            else
            {
                Assert.False(MessageCodec.TryDecodeSnapshot(packet, out var actual));
                Assert.Null(actual);
            }
        }
    }

    [Fact]
    public void Snapshot_envelope_rejects_null_snapshot_and_null_collections()
    {
        StateSnapshot?[] invalid = [null, new(1, 0, null!, [], []), new(1, 0, [], null!, []), new(1, 0, [], [], null!)];
        foreach (var snapshot in invalid)
        {
            byte[] packet = [(byte)MessageType.Snapshot, .. MemoryPackSerializer.Serialize(snapshot)];
            Assert.False(MessageCodec.TryDecodeSnapshot(packet, out var actual));
            Assert.Null(actual);
        }
    }
}
