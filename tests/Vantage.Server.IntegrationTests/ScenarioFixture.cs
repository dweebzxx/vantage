using System.Net;
using System.Net.Sockets;
using Xunit;
using Xunit.Abstractions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Vantage.Server.IntegrationTests;

public sealed class ScenarioFixture
{
    internal const string MissingGodot = "Godot .NET could not be started or --version failed. Install godot-mono on PATH or set VANTAGE_GODOT to a working Godot .NET executable.";
    internal string Root { get; } = FindRoot();
    internal string Executable { get; } = Environment.GetEnvironmentVariable("VANTAGE_GODOT") ?? "godot-mono";
    private Task<string?>? _validation;

    internal async Task RequireGodot()
    {
        string? error = await (_validation ??= Validate());
        Assert.True(error is null, error);
    }

    private async Task<string?> Validate()
    {
        try
        {
            await using var version = new ProcessRunner("version", Executable, Root, "--version");
            return await version.Exit(TimeSpan.FromSeconds(10)) == 0 ? null : MissingGodot;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or TimeoutException or InvalidOperationException)
        {
            return MissingGodot;
        }
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "vantage.sln"))) return directory.FullName;
        throw new InvalidOperationException("Cannot locate repository root: walk up to vantage.sln failed.");
    }
}

internal sealed class Scenario(ScenarioFixture fixture, ITestOutputHelper output) : IAsyncDisposable
{
    private readonly List<ProcessRunner> _processes = [];
    public int Port { get; } = FreeUdpPort();
    public ProcessRunner Server { get; private set; } = null!;
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);

    // ENet is UDP. Release just before Godot binds; a bind race is a clear startup failure.
    private static int FreeUdpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private ProcessRunner Start(string name, string path, params string[] arguments)
    {
        var process = new ProcessRunner(name, fixture.Executable, fixture.Root,
            ["--headless", "--path", path, "--", "--port", Port.ToString(System.Globalization.CultureInfo.InvariantCulture), .. arguments]);
        _processes.Add(process);
        return process;
    }

    public async Task Boot(params string[] serverArguments)
    {
        // P36: Warmup>Playing now resets every player. The default 10 s warmup would land inside scenarios that
        // run 10 s, so a scenario that does not choose its own timing gets a warmup too long to end.
        if (!serverArguments.Contains("--match-ticks")) serverArguments = [.. serverArguments, "--match-ticks", "3000,3000,3000"];
        Server = Start("server", "server/headless", serverArguments);
        await Wait(Server, "listening");
        // Admission is polled only after arena physics registration (P26).
        await Wait(Server, "arena");
    }

    public ProcessRunner InvalidPort() => Start("invalid-port", "server/headless", "--port", "abc");
    public ProcessRunner InvalidMatchTicks(string value) => Start($"invalid-match-ticks-{value}", "server/headless", "--match-ticks", value);

    // extra: further smoke options for the scripted client, e.g. "--net-target", "teammate" (P39).
    public async Task<ProcessRunner> Client(string name, string script, int ticks, params string[] extra)
    {
        var process = Start(name, "client", ["--connect", "--net-smoke", ticks.ToString(System.Globalization.CultureInfo.InvariantCulture), "--net-script", script, .. extra]);
        await Wait(process, "welcome");
        return process;
    }

    public Task Wait(ProcessRunner process, string kind) => process.WaitFor(line => Patterns.Is(line, kind), kind, ReadyTimeout);
    public async Task Finish(ProcessRunner client)
    {
        Assert.Equal(0, await client.Exit(ClientTimeout));
        long peer = Assert.Single(Patterns.Read(client, "connected")).Number("peer");
        await Server.WaitFor(line => Patterns.Is(line, "counters") && line.Contains($"peer={peer};", StringComparison.Ordinal),
            $"final departure counters for peer={peer}", ReadyTimeout);
    }

    public async Task StopServer()
    {
        Server.Interrupt();
        Assert.Equal(0, await Server.Exit(StopTimeout));
    }

    public async ValueTask DisposeAsync()
    {
        // Attempt cleanup for every process even if an earlier cleanup fails.
        var errors = new List<Exception>();
        foreach (var process in _processes.AsEnumerable().Reverse())
        {
            try { await process.DisposeAsync(); }
            catch (Exception error) { errors.Add(error); }
            finally { output.WriteLine(process.Transcript); }
        }
        if (errors.Count > 0) throw new AggregateException("Scenario process cleanup failed; all owned processes were attempted.", errors);
    }
}
