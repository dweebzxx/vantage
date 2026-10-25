using MemoryPack;

namespace Vantage.Shared.Protocol;

public static class MessageCodec
{
    public static byte[] EncodeInput(InputCommand input)
    {
        byte[] payload = MemoryPackSerializer.Serialize(input);
        byte[] packet = new byte[payload.Length + 1];
        packet[0] = (byte)MessageType.Input;
        payload.CopyTo(packet, 1);
        return packet;
    }

    // Structural decoding only. Numerical range validation belongs to the receiver.
    // MemoryPack is not a checksum: changes that form another valid payload are
    // indistinguishable from intentionally sent values.
    public static bool TryDecodeInput(ReadOnlySpan<byte> packet, out InputCommand input)
    {
        input = default;
        if (packet.IsEmpty || packet[0] != (byte)MessageType.Input)
        {
            return false;
        }

        try
        {
            InputCommand decoded = default;
            int consumed = MemoryPackSerializer.Deserialize(packet[1..], ref decoded);
            if (consumed != packet.Length - 1)
            {
                return false;
            }

            input = decoded;
            return true;
        }
        catch (MemoryPackSerializationException)
        {
            return false;
        }
    }

    public static byte[] EncodeSnapshot(StateSnapshot snapshot) => Encode(MessageType.Snapshot, snapshot);
    public static byte[] EncodeWelcome(WelcomeMessage welcome) => Encode(MessageType.Welcome, welcome);

    public static bool TryDecodeSnapshot(ReadOnlySpan<byte> packet, out StateSnapshot? snapshot)
    {
        if (TryDecode(packet, MessageType.Snapshot, out snapshot) && snapshot is not null &&
            snapshot.Players is not null && snapshot.Projectiles is not null && snapshot.Grenades is not null)
            return true;
        snapshot = null;
        return false;
    }

    public static bool TryDecodeWelcome(ReadOnlySpan<byte> packet, out WelcomeMessage welcome) =>
        TryDecode(packet, MessageType.Welcome, out welcome);

    private static byte[] Encode<T>(MessageType type, T message)
    {
        byte[] payload = MemoryPackSerializer.Serialize(message);
        return [(byte)type, .. payload];
    }

    private static bool TryDecode<T>(ReadOnlySpan<byte> packet, MessageType type, out T? message)
    {
        message = default;
        if (packet.IsEmpty || packet[0] != (byte)type) return false;
        try
        {
            T? decoded = default;
            int consumed = MemoryPackSerializer.Deserialize(packet[1..], ref decoded);
            if (consumed != packet.Length - 1) return false;
            message = decoded;
            return true;
        }
        catch (MemoryPackSerializationException)
        {
            return false;
        }
    }
}
