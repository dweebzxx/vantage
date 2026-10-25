using System.Globalization;
using Godot;
using Vantage.Shared.Protocol;

namespace Vantage.Server;

public partial class ServerMain : Node
{
    public const int DefaultPort = TransportConstants.DefaultPort;
    public int Port { get; private set; } = DefaultPort;

    private TickLoop _tickLoop = null!;
    private World _world = null!;
    private ulong _initialPhysicsFrame;
    private int _shutdownRequested;
    private ServerPeer? _serverPeer;
    private bool _arenaVerified;
    private bool _wallProbe;

    public override void _Ready()
    {
        _tickLoop = GetNode<TickLoop>("TickLoop");
        _world = GetNode<World>("World");
        _initialPhysicsFrame = Engine.GetPhysicsFrames();

        bool suppliedPort = false;
        string[] arguments = OS.GetCmdlineUserArgs();
        for (int i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] != "--port")
            {
                continue;
            }

            if (++i >= arguments.Length ||
                !int.TryParse(arguments[i], NumberStyles.None, CultureInfo.InvariantCulture, out int port) ||
                port is < 1 or > 65535)
            {
                GD.PrintErr("[Server] error: --port requires an integer from 1 to 65535.");
                _tickLoop.SetPhysicsProcess(false);
                GetTree().Quit(1);
                return;
            }

            Port = port;
            suppliedPort = true;
        }

        GD.Print($"[Server] port={Port} ({(suppliedPort ? "--port" : "default")})");
        GD.Print($"[Server] tick rate={Engine.PhysicsTicksPerSecond} Hz; initial tick=0");
        _wallProbe = arguments.Contains("--wall-probe");
        _world.Initialize();
        _serverPeer = new ServerPeer(_world);
        Error error = _serverPeer.Start(Port);
        if (error != Error.Ok)
        {
            GD.PrintErr($"[Server] ENet startup failed: {error}");
            _tickLoop.SetPhysicsProcess(false);
            GetTree().Quit(1);
            return;
        }
        _tickLoop.Tick += RunTick;
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    private void RunTick(uint tick)
    {
        // TileMap physics registration needs a completed physics frame.
        if (!_arenaVerified)
        {
            if (tick < 3) return;
            if (!_world.VerifyArena(_wallProbe))
            {
                GD.PrintErr("[Server] arena verification failed; stopping before simulation.");
                _tickLoop.SetPhysicsProcess(false);
                GetTree().Quit(1);
                return;
            }
            _arenaVerified = true;
        }
        // Fixed authoritative tick order (R10): accepted input/selection, movement,
        // hitscan/new attacks, projectile sweeps, grenade timers/detonation,
        // damage/life-state/respawn, reliable events, due snapshots, history.
        _serverPeer!.PollTick(tick);
        _world.SimulateTick(tick);
        _serverPeer.SendEvents();
        _serverPeer.SendSnapshots(tick);
        _world.RecordTick(tick);
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        // The console callback can run off-thread. Only request shutdown here;
        // the physics callback below performs Godot work on the main thread.
        System.Threading.Interlocked.Exchange(ref _shutdownRequested, 1);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (System.Threading.Volatile.Read(ref _shutdownRequested) != 0)
        {
            GetTree().Quit();
        }
    }

    public override void _ExitTree()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        _tickLoop.Tick -= RunTick;
        if (_serverPeer is not null)
        {
            _serverPeer.Dispose();
        }
        GD.Print($"[Server] final tick={_tickLoop.CurrentTick}; physics frames={Engine.GetPhysicsFrames() - _initialPhysicsFrame}");
    }
}
