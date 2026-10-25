using System.Numerics;
using MemoryPack;
using Vantage.Shared.Protocol;
using Xunit;

namespace Vantage.Shared.Tests;

public sealed class MessageCodecTests
{
    [Fact]
    public void Input_envelope_round_trip_preserves_every_field_and_type_byte()
    {
        InputCommand original = new(73u, new Vector2(-0.75f, 0.5f), 1.25f,
            InputButtons.PrimaryFire | InputButtons.Grenade);

        byte[] packet = MessageCodec.EncodeInput(original);

        Assert.Equal(1, packet[0]);
        Assert.Equal(MemoryPackSerializer.Serialize(original), packet[1..]);
        Assert.True(MessageCodec.TryDecodeInput(packet, out InputCommand actual));
        Assert.Equal(73u, actual.ClientTick);
        Assert.Equal(new Vector2(-0.75f, 0.5f), actual.Move);
        Assert.Equal(1.25f, actual.AimAngleRadians);
        Assert.Equal(InputButtons.PrimaryFire | InputButtons.Grenade, actual.Buttons);
    }

    [Fact]
    public void Empty_packet_fails_without_throwing()
    {
        Assert.False(MessageCodec.TryDecodeInput([], out InputCommand actual));
        Assert.Equal(default, actual);
    }

    [Fact]
    public void Unknown_type_fails_without_throwing()
    {
        byte[] packet = MessageCodec.EncodeInput(new(1u, Vector2.Zero, 0f, InputButtons.None));
        packet[0] = 255;

        Assert.False(MessageCodec.TryDecodeInput(packet, out InputCommand actual));
        Assert.Equal(default, actual);
    }

    [Fact]
    public void Every_truncated_payload_fails_without_throwing()
    {
        byte[] packet = MessageCodec.EncodeInput(new(2u, Vector2.UnitX, -0.5f, InputButtons.Grenade));

        for (int length = 1; length < packet.Length; length++)
        {
            Assert.False(MessageCodec.TryDecodeInput(packet.AsSpan(0, length), out InputCommand actual));
            Assert.Equal(default, actual);
        }
    }

    [Fact]
    public void Corrupt_payload_fails_without_throwing()
    {
        // Random malformed data with a valid type marker, not an encoded command.
        byte[] packet = [1, 255, 23, 187, 99, 42];

        Assert.False(MessageCodec.TryDecodeInput(packet, out InputCommand actual));
        Assert.Equal(default, actual);
    }

    [Fact]
    public void Trailing_garbage_fails_without_throwing()
    {
        byte[] encoded = MessageCodec.EncodeInput(new(3u, Vector2.UnitY, 0.25f, InputButtons.PrimaryFire));
        byte[] packet = [.. encoded, 0xAB, 0xCD];

        Assert.False(MessageCodec.TryDecodeInput(packet, out InputCommand actual));
        Assert.Equal(default, actual);
    }
}
