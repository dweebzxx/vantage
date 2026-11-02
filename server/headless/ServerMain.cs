using System.Globalization;
using Godot;
using Vantage.Shared.Game;
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
        MatchTiming matchTiming = MatchTiming.Default;
        bool suppliedMatchTicks = false;
        string[] arguments = OS.GetCmdlineUserArgs();
        for (int i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] == "--match-ticks")
            {
                if (++i >= arguments.Length || !TryParseMatchTicks(arguments[i], out MatchTiming? parsed))
                {
                    GD.PrintErr("[Server] error: --match-ticks requires three positive integers separated by commas: warmup,playing,postgame.");
                    _tickLoop.SetPhysicsProcess(false);
                    GetTree().Quit(1);
                    return;
                }

                matchTiming = parsed;
                suppliedMatchTicks = true;
                continue;
            }

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
        _world.Match = new MatchHost(matchTiming);
        GD.Print($"[Match] timing warmup={matchTiming.WarmupTicks}; playing={matchTiming.PlayingTicks}; postgame={matchTiming.PostgameTicks}; source={(suppliedMatchTicks ? "option" : "default")}");
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

    // Exactly three positive uint values; NumberStyles.None rejects signs and whitespace.
    private static bool TryParseMatchTicks(string text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out MatchTiming? timing)
    {
        timing = null;
        string[] parts = text.Split(',');
        if (parts.Length != 3) return false;
        var values = new uint[3];
        for (int i = 0; i < 3; i++)
            if (!uint.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out values[i]) || values[i] == 0) return false;
        timing = new MatchTiming(values[0], values[1], values[2]);
        return true;
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
        // Fixed authoritative tick order (R10): accepted input, match advance
        // (MatchHost.Advance), team moves, player reset (Postgame>Warmup and Warmup>Playing),
        // selection, movement, hitscan/new attacks, projectile sweeps, grenade
        // timers/detonation, damage/life-state/respawn, match broadcast (MatchHost messages
        // appended to events), reliable events, due snapshots, history.
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
