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
        _serverPeer = new ServerPeer();
        Error error = _serverPeer.Start(Port);
        if (error != Error.Ok)
        {
            GD.PrintErr($"[Server] ENet startup failed: {error}");
            _tickLoop.SetPhysicsProcess(false);
            GetTree().Quit(1);
            return;
        }
        _tickLoop.Tick += _world.RecordTick;
        _tickLoop.Tick += _serverPeer.PollTick;
        Console.CancelKeyPress += OnCancelKeyPress;
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
        _tickLoop.Tick -= _world.RecordTick;
        if (_serverPeer is not null)
        {
            _tickLoop.Tick -= _serverPeer.PollTick;
            _serverPeer.Dispose();
        }
        GD.Print($"[Server] final tick={_tickLoop.CurrentTick}; physics frames={Engine.GetPhysicsFrames() - _initialPhysicsFrame}");
    }
}
