using System.Globalization;
using Godot;
using Vantage.Shared.Protocol;
using Vantage.Shared.Net;
using NumericsVector2 = System.Numerics.Vector2;

public partial class GameClient : Node
{
    private static readonly StringName MoveLeft = "move_left";
    private static readonly StringName MoveRight = "move_right";
    private static readonly StringName MoveUp = "move_up";
    private static readonly StringName MoveDown = "move_down";
    private static readonly StringName PrimaryFire = "primary_fire";
    private static readonly StringName GrenadeThrow = "grenade_throw";

    private ENetMultiplayerPeer? _peer;
    private PlayerController? _player;
    private int _port = TransportConstants.DefaultPort;
    private uint _clientTick;
    private ulong _sent;
    private uint? _smokeTicks;
    private bool _connected;
    private bool _disconnecting;
    private bool _quitAfterDisconnect;
    private bool _loggedMissingPlayer;
    private bool _smokeFinished;

    private string _smokeScript = "default";
    private uint? _entityId;
    private uint? _lastSnapshotTick;
    private ulong _snapshotsApplied;
    private ulong _snapshotsDropped;
    private ulong _badSent;
    private int _mostOthersSeen;
    private StateSnapshot? _lastSnapshot;
    private readonly Dictionary<uint, Node2D> _remotePlayers = new();
    private PackedScene? _remoteScene;

    public override void _Ready()
    {
        SetPhysicsProcess(false);
        GetTree().AutoAcceptQuit = false;
        bool connect = false;
        string[] arguments = OS.GetCmdlineUserArgs();
        for (int i = 0; i < arguments.Length; i++)
        {
            switch (arguments[i])
            {
                case "--connect":
                    connect = true;
                    break;
                case "--port":
                    if (++i >= arguments.Length ||
                        !int.TryParse(arguments[i], NumberStyles.None, CultureInfo.InvariantCulture, out int port) ||
                        port is < 1 or > 65535)
                    {
                        GD.PrintErr("[GameClient] error: --port requires an integer from 1 to 65535.");
                        GetTree().Quit(1);
                        return;
                    }
                    _port = port;
                    break;
                case "--net-script":
                    if (++i >= arguments.Length || arguments[i] is not ("east" or "illegal"))
                    {
                        GD.PrintErr("[GameClient] error: --net-script requires east or illegal.");
                        GetTree().Quit(1);
                        return;
                    }
                    _smokeScript = arguments[i];
                    break;
                case "--net-smoke":
                    if (++i >= arguments.Length ||
                        !uint.TryParse(arguments[i], NumberStyles.None, CultureInfo.InvariantCulture, out uint ticks) || ticks == 0)
                    {
                        GD.PrintErr("[GameClient] error: --net-smoke requires a positive uint tick count.");
                        GetTree().Quit(1);
                        return;
                    }
                    _smokeTicks = ticks;
                    break;
            }
        }

        if (_smokeTicks.HasValue && !connect)
        {
            GD.PrintErr("[GameClient] error: --net-smoke requires --connect.");
            GetTree().Quit(1);
            return;
        }
        if (connect)
        {
            Connect();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F9 })
        {
            if (_peer is null)
            {
                Connect();
            }
            else
            {
                BeginDisconnect(false);
            }
        }
    }

    private void Connect()
    {
        _peer = new ENetMultiplayerPeer();
        Error error = _peer.CreateClient(TransportConstants.LoopbackAddress, _port);
        if (error != Error.Ok)
        {
            ConnectionFailed(error.ToString());
            return;
        }
        _peer.TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered;
        _peer.TransferChannel = TransportConstants.InputChannel;
        _peer.SetTargetPeer(1);
        _entityId = null;
        _lastSnapshotTick = null;
        _lastSnapshot = null;
        _snapshotsApplied = _snapshotsDropped = _badSent = 0;
        _mostOthersSeen = 0;
        _clientTick = 0;
        _sent = 0;
        _loggedMissingPlayer = false;
        SetPhysicsProcess(true);
        GD.Print($"[GameClient] connecting {TransportConstants.LoopbackAddress}:{_port}");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_peer is null)
        {
            return;
        }

        _peer.Poll();
        MultiplayerPeer.ConnectionStatus status = _peer.GetConnectionStatus();
        if (status == MultiplayerPeer.ConnectionStatus.Disconnected)
        {
            if (_disconnecting)
            {
                bool quit = _quitAfterDisconnect;
                GD.Print($"[GameClient] disconnected; sent={_sent}");
                ClearPeer();
                if (quit) GetTree().Quit();
            }
            else if (_connected)
            {
                GD.Print($"[GameClient] server disconnected; sent={_sent}; local play continues; F9 reconnects");
                ClearPeer();
            }
            else
            {
                ConnectionFailed("ENet connection refused or failed");
            }
            return;
        }

        if (status != MultiplayerPeer.ConnectionStatus.Connected || _disconnecting)
        {
            return;
        }
        if (!_connected)
        {
            _connected = true;
            GD.Print($"[GameClient] connected peer={_peer.GetUniqueId()}; input rate={TransportConstants.InputSendRateHz} Hz; first client tick=1");
        }
        FindPlayer();
        ReceiveMessages();
        if (_peer is null || _disconnecting) return;
        if (_smokeFinished)
        {
            // Give the final flushed input a separate polling turn from the
            // departure. ENet removes queued incoming packets on disconnect.
            BeginDisconnect(true);
            return;
        }

        // Reset on each connection; first input is tick 1, then uint wraps naturally.
        _clientTick = unchecked(_clientTick + 1u);
        InputCommand input = _smokeTicks.HasValue ? ScriptedInput(_clientTick) : ReadLocalInput(_clientTick);
        Error sendError = _peer.PutPacket(MessageCodec.EncodeInput(input));
        if (sendError != Error.Ok)
        {
            GD.PrintErr($"[GameClient] input send failed: {sendError}");
            ClearPeer();
            if (_smokeTicks.HasValue) GetTree().Quit(1);
            return;
        }
        _sent++;
        if (!input.IsInRange()) _badSent++;
        if (_clientTick % TickMath.SimulationRateHz == 0)
            GD.Print($"[GameClient] snapshots applied={_snapshotsApplied}; dropped={_snapshotsDropped}; displayed={_player?.GlobalPosition}; view-only={_player?.ViewOnly}");
        // Submit this physics tick's input now rather than batching it with the
        // next tick during Poll. This also drains the final smoke input.
        _peer.Host.Flush();
        if (_smokeTicks.HasValue && _sent == _smokeTicks.Value)
        {
            _smokeFinished = true;
        }
    }

    private InputCommand ReadLocalInput(uint tick)
    {
        Vector2 move = Input.GetVector(MoveLeft, MoveRight, MoveUp, MoveDown);
        if (move.LengthSquared() > 1f) move = move.Normalized();
        InputButtons buttons = InputButtons.None;
        if (Input.IsActionPressed(PrimaryFire)) buttons |= InputButtons.PrimaryFire;
        if (Input.IsActionPressed(GrenadeThrow)) buttons |= InputButtons.Grenade;

        FindPlayer();
        if (_player is null && !_loggedMissingPlayer)
        {
            GD.Print("[GameClient] no player in current scene; sending aim=0");
            _loggedMissingPlayer = true;
        }

        // The existing read-only property uses mouse minus player global position,
        // normalized then Angle(), and retains the last angle at zero distance.
        return new(tick, new NumericsVector2(move.X, move.Y), _player?.AimAngleRadians ?? 0f, buttons);
    }

    private InputCommand ScriptedInput(uint tick)
    {
        if (_smokeScript == "east") return new(tick, NumericsVector2.UnitX, 0f, InputButtons.None);
        if (_smokeScript == "illegal")
        {
            uint third = _smokeTicks!.Value / 3;
            NumericsVector2 move = tick <= third ? new(0.1f, 0f) :
                tick <= third * 2 ? (tick % 3) switch
                {
                    0 => new(100f, -100f),
                    1 => new(float.NaN, 0f),
                    _ => new(0f, float.PositiveInfinity),
                } : new(-0.1f, 0f);
            return new(tick, move, 0f, InputButtons.None);
        }
        return DefaultScriptedInput(tick);
    }

    private static InputCommand DefaultScriptedInput(uint tick) => new(tick,
        (tick % 4u) switch
        {
            0u => new NumericsVector2(1f, 0f),
            1u => new NumericsVector2(0f, -1f),
            2u => new NumericsVector2(-0.5f, 0.5f),
            _ => NumericsVector2.Zero,
        }, (tick % 32u) * 0.1f,
        (tick % 2u == 0 ? InputButtons.PrimaryFire : InputButtons.None) |
        (tick % 7u == 0 ? InputButtons.Grenade : InputButtons.None));

    private void FindPlayer()
    {
        Node? scene = GetTree().CurrentScene;
        if (!IsInstanceValid(_player) || scene is null || !scene.IsAncestorOf(_player))
        {
            _player = scene?.FindChildren("*", "", true, false).OfType<PlayerController>().FirstOrDefault();
            if (_player is not null) _player.ViewOnly = _entityId.HasValue;
        }
    }

    private void ReceiveMessages()
    {
        while (_peer is not null && _peer.GetAvailablePacketCount() > 0)
        {
            byte[] packet = _peer.GetPacket();
            if (MessageCodec.TryDecodeWelcome(packet, out WelcomeMessage welcome))
            {
                if (welcome.ProtocolVersion != ProtocolConstants.ProtocolVersion)
                {
                    GD.PrintErr($"[GameClient] protocol mismatch: server={welcome.ProtocolVersion}; client={ProtocolConstants.ProtocolVersion}");
                    BeginDisconnect(false);
                    return;
                }
                _entityId = welcome.EntityId;
                if (_player is not null) _player.ViewOnly = true;
                GD.Print($"[GameClient] welcome entity={_entityId}; protocol={welcome.ProtocolVersion}");
            }
            else if (MessageCodec.TryDecodeSnapshot(packet, out StateSnapshot? snapshot))
            {
                if (!_entityId.HasValue || (_lastSnapshotTick.HasValue &&
                    !TickMath.IsNewerThan(snapshot!.ServerTick, _lastSnapshotTick.Value)))
                {
                    _snapshotsDropped++;
                    continue;
                }
                ApplySnapshot(snapshot!);
            }
        }
    }

    private void ApplySnapshot(StateSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        _lastSnapshotTick = snapshot.ServerTick;
        _snapshotsApplied++;
        var present = new HashSet<uint>();
        foreach (PlayerState player in snapshot.Players)
        {
            Vector2 position = new(player.Position.X, player.Position.Y);
            if (player.EntityId == _entityId)
            {
                _player?.ApplySnapshot(position, player.AimAngleRadians);
                continue;
            }
            present.Add(player.EntityId);
            if (!_remotePlayers.TryGetValue(player.EntityId, out Node2D? view))
            {
                if (_player is null) continue;
                _remoteScene ??= GD.Load<PackedScene>("res://entities/player/RemotePlayer.tscn");
                view = _remoteScene.Instantiate<Node2D>();
                _player.GetParent().AddChild(view);
                _remotePlayers.Add(player.EntityId, view);
            }
            view.GlobalPosition = position;
            view.GetNode<Node2D>("DevDebugAimPivot").Rotation = player.AimAngleRadians;
        }
        foreach (uint id in _remotePlayers.Keys.Where(id => !present.Contains(id)).ToArray())
        {
            if (IsInstanceValid(_remotePlayers[id])) _remotePlayers[id].QueueFree();
            _remotePlayers.Remove(id);
        }
        _mostOthersSeen = Math.Max(_mostOthersSeen, present.Count);
    }

    private void LogSmokeResult()
    {
        if (!_smokeTicks.HasValue) return;
        GD.Print($"[GameClient] smoke complete; sent={_sent}; bad-sent={_badSent}; last snapshot tick={_lastSnapshotTick}; entity={_entityId}; others-seen={_mostOthersSeen}; displayed={_player?.GlobalPosition}; view-only={_player?.ViewOnly}");
        if (_lastSnapshot is not null)
            foreach (PlayerState player in _lastSnapshot.Players)
                GD.Print($"[GameClient] last snapshot entity={player.EntityId}; position={player.Position}");
    }

    private void BeginDisconnect(bool quit)
    {
        if (_disconnecting)
        {
            _quitAfterDisconnect |= quit;
            return;
        }
        if (_peer is null || !_connected)
        {
            ClearPeer();
            if (quit) GetTree().Quit();
            return;
        }

        LogSmokeResult();
        _disconnecting = true;
        _quitAfterDisconnect = quit;
        // DisconnectLater only waits for reliable packets. Flush the last
        // unreliable input first so ENet does not discard it during teardown.
        // Keep polling asynchronously until ENet reports the departure.
        _peer!.Host.Flush();
        _peer.GetPeer(1).PeerDisconnectLater();
        _peer.Host.Flush();
    }

    private void ConnectionFailed(string reason)
    {
        GD.PrintErr($"[GameClient] connection failed: {reason}");
        ClearPeer();
        if (_smokeTicks.HasValue) GetTree().Quit(1);
    }

    private void ClearPeer()
    {
        if (IsInstanceValid(_player)) _player!.ViewOnly = false;
        foreach (Node2D view in _remotePlayers.Values)
            if (IsInstanceValid(view)) view.QueueFree();
        _remotePlayers.Clear();
        _entityId = null;
        _lastSnapshotTick = null;
        _lastSnapshot = null;
        _peer?.Close();
        _peer?.Dispose();
        _peer = null;
        _connected = false;
        _disconnecting = false;
        _quitAfterDisconnect = false;
        _smokeFinished = false;
        SetPhysicsProcess(false);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) BeginDisconnect(true);
    }

    public override void _ExitTree()
    {
        if (_peer is not null && _connected && !_disconnecting)
        {
            _peer.GetPeer(1).PeerDisconnectNow();
            _peer.Host.Flush();
        }
        ClearPeer();
    }
}
