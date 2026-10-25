using Godot;
using Vantage.Shared.Net;
using Vantage.Shared.Protocol;

namespace Vantage.Server;

// Raw ENet packets only; deliberately not assigned to SceneMultiplayer.
public sealed class ServerPeer(World world) : IDisposable
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

    public void SendSnapshots(uint tick)
    {
        if (tick % TransportConstants.SnapshotIntervalTicks != 0) return;
        PlayerState[] players = world.CapturePlayers();
        foreach (var (id, counters) in _clients)
        {
            // 0 is the no-accepted-input sentinel; first client input tick is 1.
            var snapshot = new StateSnapshot(tick, counters.LatestAccepted?.ClientTick ?? 0u, players, world.CaptureProjectiles(), world.CaptureGrenades());
            Send(id, MessageCodec.EncodeSnapshot(snapshot), TransportConstants.SnapshotChannel,
                MultiplayerPeer.TransferModeEnum.Unreliable);
        }
        _peer.Host.Flush();
    }

    public void SendEvents()
    {
        foreach (byte[] packet in world.Events)
            foreach (int id in _clients.Keys)
                Send(id, packet, TransportConstants.EventChannel, MultiplayerPeer.TransferModeEnum.Reliable);
        if (world.Events.Count > 0) _peer.Host.Flush();
    }

    private void Send(int id, byte[] packet, int channel, MultiplayerPeer.TransferModeEnum mode)
    {
        _peer.SetTargetPeer(id);
        _peer.TransferChannel = channel;
        _peer.TransferMode = mode;
        Error error = _peer.PutPacket(packet);
        if (error != Error.Ok) GD.PrintErr($"[NetServer] send failed peer={id}: {error}");
    }

    private void OnConnected(long id)
    {
        var counters = new PeerInputCounters((int)id);
        _clients[(int)id] = counters;
        uint entityId = world.AddPlayer((int)id, counters);
        Send((int)id, MessageCodec.EncodeWelcome(new(entityId, ProtocolConstants.ProtocolVersion)),
            TransportConstants.WelcomeChannel, MultiplayerPeer.TransferModeEnum.Reliable);
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
            world.RemovePlayer(id);
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
