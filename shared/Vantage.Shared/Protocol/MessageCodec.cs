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
}
