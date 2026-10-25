using Godot;
using Vantage.Shared.Net;
using Vantage.Shared.Protocol;

namespace Vantage.Server;

// Raw ENet packets only; deliberately not assigned to SceneMultiplayer.
public sealed class ServerPeer : IDisposable
{
    private readonly ENetMultiplayerPeer _peer = new();
    private readonly Dictionary<int, PeerInputCounters> _clients = new();
    private readonly List<int> _departed = new();
    private bool _started;

    public Error Start(int port)
    {
        _peer.SetBindIP(TransportConstants.LoopbackAddress);
        _peer.PeerConnected += OnConnected;
        _peer.PeerDisconnected += OnDisconnected;
        Error error = _peer.CreateServer(port, TransportConstants.MaxClients);
        _started = error == Error.Ok;
        if (_started)
        {
            GD.Print($"[NetServer] listening {TransportConstants.LoopbackAddress}:{port}; max clients={TransportConstants.MaxClients}");
        }

        return error;
    }

    public void PollTick(uint tick)
    {
        _peer.Poll();
        while (_peer.GetAvailablePacketCount() > 0)
        {
            int sender = _peer.GetPacketPeer();
            byte[] packet = _peer.GetPacket();
            if (_clients.TryGetValue(sender, out PeerInputCounters? counters))
            {
                counters.Receive(packet);
            }
        }

        // Poll can report departure in the same batch as the final input packets.
        foreach (int id in _departed)
        {
            RemoveClient(id, "client left");
        }
        _departed.Clear();

        if (tick % TickMath.SimulationRateHz == 0)
        {
            foreach (PeerInputCounters counters in _clients.Values)
            {
                GD.Print($"[NetServer] summary {counters}");
            }
        }
    }

    private void OnConnected(long id)
    {
        _clients[(int)id] = new PeerInputCounters((int)id);
        GD.Print($"[NetServer] connected peer={id}");
        if (_clients.Count == TransportConstants.MaxClients)
        {
            GD.Print("[NetServer] capacity reached; ENet refuses further clients; refused attempts are not exposed as peer signals.");
        }
    }

    private void OnDisconnected(long id) => _departed.Add((int)id);

    private void RemoveClient(int id, string reason)
    {
        if (_clients.Remove(id, out PeerInputCounters? counters))
        {
            GD.Print($"[NetServer] disconnected ({reason}); final {counters}");
        }
    }

    public void Dispose()
    {
        if (_started)
        {
            foreach (int id in _clients.Keys.ToArray())
            {
                // Send ENet's departure notification before destroying the host.
                _peer.GetPeer(id).PeerDisconnectNow();
                RemoveClient(id, "server shutdown");
            }
            _peer.Host.Flush();
            _peer.Close();
            _started = false;
        }

        _peer.PeerConnected -= OnConnected;
        _peer.PeerDisconnected -= OnDisconnected;
        _peer.Dispose();
    }
}
