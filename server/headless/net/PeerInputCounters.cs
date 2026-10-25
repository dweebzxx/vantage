using Vantage.Shared.Net;
using Vantage.Shared.Protocol;

namespace Vantage.Server;

public sealed class PeerInputCounters(int peerId)
{
    public int PeerId { get; } = peerId;
    public ulong Received { get; private set; }
    public ulong Decoded { get; private set; }
    public ulong Accepted { get; private set; }
    public ulong Rejected { get; private set; }
    public ulong OutOfOrder { get; private set; }
    public InputCommand? LatestAccepted { get; private set; }

    public void Receive(byte[] packet)
    {
        Received++;
        if (!MessageCodec.TryDecodeInput(packet, out InputCommand input))
        {
            Rejected++;
            return;
        }

        Decoded++;
        if (!input.IsInRange())
        {
            Rejected++;
            return;
        }

        if (LatestAccepted is InputCommand latest && !TickMath.IsNewerThan(input.ClientTick, latest.ClientTick))
        {
            OutOfOrder++;
            return;
        }

        LatestAccepted = input;
        Accepted++;
    }

    public override string ToString() =>
        $"peer={PeerId}; received={Received}; decoded={Decoded}; accepted={Accepted}; rejected={Rejected}; out-of-order={OutOfOrder}";
}
