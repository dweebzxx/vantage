using System.Numerics;
using MemoryPack;
using Vantage.Shared.Protocol;
using Xunit;
namespace Vantage.Shared.Tests;

public sealed class CombatMessageCodecTests
{
    [Fact]
    public void Weapon_select_bits_preserve_input_layout_and_exact_values()
    {
        Assert.Equal(8, (byte)InputButtons.SelectSlot1);
        Assert.Equal(16, (byte)InputButtons.SelectSlot2);
        foreach (var selection in new[] { InputButtons.SelectSlot1, InputButtons.SelectSlot2 })
        {
            var input = new InputCommand(27, new Vector2(-0.5f, 1), -1.25f, selection | InputButtons.PrimaryFire | InputButtons.Grenade);
            var bytes = MessageCodec.EncodeInput(input);
            Assert.Equal(MemoryPackSerializer.Serialize(input), bytes[1..]);
            Assert.True(MessageCodec.TryDecodeInput(bytes, out var actual));
            Assert.Equal(input, actual);
        }
    }

    [Fact]
    public void Event_envelopes_preserve_type_bytes_and_every_field()
    {
        var hit = MessageCodec.EncodeHit(new(73, 11, 22));
        Assert.Equal(4, hit[0]);
        Assert.Equal(MemoryPackSerializer.Serialize(new HitEvent(73, 11, 22)), hit[1..]);
        Assert.True(MessageCodec.TryDecodeHit(hit, out var h));
        Assert.Equal(new HitEvent(73, 11, 22), h);
        var damage = MessageCodec.EncodeDamage(new(74, 12, 23, 40));
        Assert.Equal(5, damage[0]);
        Assert.Equal(MemoryPackSerializer.Serialize(new DamageDealtEvent(74, 12, 23, 40)), damage[1..]);
        Assert.True(MessageCodec.TryDecodeDamage(damage, out var d));
        Assert.Equal(new DamageDealtEvent(74, 12, 23, 40), d);
        var defeat = MessageCodec.EncodeDefeat(new(75, 13, 24));
        Assert.Equal(6, defeat[0]);
        Assert.Equal(MemoryPackSerializer.Serialize(new DefeatEvent(75, 13, 24)), defeat[1..]);
        Assert.True(MessageCodec.TryDecodeDefeat(defeat, out var f));
        Assert.Equal(new DefeatEvent(75, 13, 24), f);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Every_event_rejects_empty_unknown_wrong_type_truncated_corrupt_and_trailing_bytes(byte type)
    {
        byte[] valid = type switch { 4 => MessageCodec.EncodeHit(new(7, 8, 9)), 5 => MessageCodec.EncodeDamage(new(7, 8, 9, 25)), _ => MessageCodec.EncodeDefeat(new(7, 8, 9)) };
        List<byte[]> invalid = [[], [255, .. valid[1..]], [(byte)MessageType.Input, .. valid[1..]], [type, 254, 23, 187, 99, 42], [.. valid, 0xAB]];
        for (int n = 1; n < valid.Length; n++) invalid.Add(valid[..n]);
        foreach (var packet in invalid)
        {
            if (type == 4) { Assert.False(MessageCodec.TryDecodeHit(packet, out var actual)); Assert.Equal(default, actual); }
            else if (type == 5) { Assert.False(MessageCodec.TryDecodeDamage(packet, out var actual)); Assert.Equal(default, actual); }
            else { Assert.False(MessageCodec.TryDecodeDefeat(packet, out var actual)); Assert.Equal(default, actual); }
        }
    }
}
