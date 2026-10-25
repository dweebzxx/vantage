using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Vantage.Server.IntegrationTests;

internal sealed class ProcessRunner : IAsyncDisposable
{
    private readonly Process _process;
    private readonly object _gate = new();
    private readonly List<string> _lines = [];
    private readonly List<string> _transcript = [];
    private TaskCompletionSource _changed = NewSignal();
    private readonly Task _stdout;
    private readonly Task _stderr;
    public string Name { get; }
    public int Id => _process.Id;
    public bool HasExited => _process.HasExited;
    public string[] Lines { get { lock (_gate) return _lines.ToArray(); } }
    public string Transcript { get { lock (_gate) return string.Join(Environment.NewLine, _transcript); } }

    public ProcessRunner(string name, string executable, string root, params string[] arguments)
    {
        Name = name;
        var info = new ProcessStartInfo(executable)
        {
            WorkingDirectory = root, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        _process = new Process { StartInfo = info };
        try { _process.Start(); }
        catch { _process.Dispose(); throw; }
        Add("harness", $"started pid={Id}: {executable} {string.Join(' ', arguments)}", false);
        _stdout = Read(_process.StandardOutput, "stdout");
        _stderr = Read(_process.StandardError, "stderr");
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private void Add(string stream, string line, bool program = true)
    {
        lock (_gate)
        {
            if (program) _lines.Add(line);
            _transcript.Add($"{DateTimeOffset.UtcNow:O} [{Name}/{stream}] {line}");
            var old = _changed;
            _changed = NewSignal();
            old.TrySetResult();
        }
    }
    private async Task Read(StreamReader reader, string stream)
    {
        while (await reader.ReadLineAsync() is { } line) Add(stream, line);
    }

    public async Task WaitFor(Func<string, bool> predicate, string description, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        Task exited = _process.WaitForExitAsync();
        try
        {
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                Task signal;
                lock (_gate)
                {
                    if (_lines.Any(predicate)) return;
                    signal = _changed.Task;
                }
                if (_process.HasExited)
                {
                    await Drain();
                    if (Lines.Any(predicate)) return;
                    throw new InvalidOperationException($"{Name} exited before {description}.\n{Transcript}");
                }
                // Exit is also an observable change, even if the program prints nothing.
                await Task.WhenAny(signal, exited).WaitAsync(deadline.Token);
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"{Name}: timeout {timeout} waiting for {description}.\n{Transcript}");
        }
    }

    public async Task<int> Exit(TimeSpan timeout)
    {
        try
        {
            await _process.WaitForExitAsync().WaitAsync(timeout);
            await Drain();
            return _process.ExitCode;
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"{Name}: timeout {timeout} waiting for exit.\n{Transcript}");
        }
    }

    public void Interrupt()
    {
        if (HasExited) return;
        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("SIGINT requires a Unix host for this Godot harness.");
        if (Kill(Id, 2) != 0)
            throw new InvalidOperationException($"SIGINT failed for {Name} pid={Id}: errno={Marshal.GetLastPInvokeError()}.\n{Transcript}");
        Add("harness", "sent SIGINT", false);
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);

    private async Task Drain()
    {
        try { await Task.WhenAll(_stdout, _stderr).WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (TimeoutException) { throw new TimeoutException($"{Name}: output drain timed out.\n{Transcript}"); }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!HasExited)
            {
                Add("harness", $"forced harness stop pid={Id}", false);
                _process.Kill(entireProcessTree: true);
            }
            await Exit(TimeSpan.FromSeconds(10));
            Add("harness", $"reaped pid={Id}; exit={_process.ExitCode}", false);
        }
        finally { _process.Dispose(); }
    }
}
