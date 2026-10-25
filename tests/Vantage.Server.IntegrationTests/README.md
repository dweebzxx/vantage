# Real-server integration tests

Run from the repository root with Godot .NET installed as `godot-mono` on PATH.
Alternatively set `VANTAGE_GODOT` to its executable path. Missing/broken Godot
fails every scenario; it never silently skips tests. SIGINT shutdown currently
requires a Unix host (macOS or Linux).

After adding/restoring this project, build the solution before running tests:

```sh
dotnet restore vantage.sln
dotnet build vantage.sln --configuration Debug --no-restore
dotnet test tests/Vantage.Server.IntegrationTests/Vantage.Server.IntegrationTests.csproj --configuration Debug --no-build
```

List each result by name, or select one scenario:

```sh
dotnet test tests/Vantage.Server.IntegrationTests/Vantage.Server.IntegrationTests.csproj --configuration Debug --no-build --logger 'console;verbosity=normal'
dotnet test tests/Vantage.Server.IntegrationTests/Vantage.Server.IntegrationTests.csproj --configuration Debug --no-build --filter 'Scenario=Boot'
```

All scenarios have `Category=ServerIntegration`; each also has a `Scenario`
trait matching its name in `ServerScenarios.cs`. Tests run sequentially, each
with a separate server and OS-assigned UDP loopback port. The helper never
builds/imports projects or invokes `--build-solutions`. Godot itself may create
ignored cache files, so post-run git status is part of pass verification.

Readiness, client completion, and shutdown bounds are 15, 30, and 10 seconds.
The bounds only detect hangs; gameplay assertions use ticks and logged outcomes.
Cleanup kills/reaps owned processes even when assertions fail, and failure output
includes timestamped stdout/stderr and process IDs for every scenario process.
Server shutdown is SIGINT; any surviving client is an explicitly logged harness
stop, not a successful smoke-client exit.

Once connected, scripted smoke clients (`--net-smoke`) call
`_peer.GetPeer(1).ThrottleConfigure(5000, 2, 0)` exactly once per connection.
The interval (5000 ms) and acceleration (2) retain ENet defaults; only throttle
deceleration changes from 2 to 0. The configure command also updates the server's
peer entry for that client, so both outgoing client inputs and server snapshots
to smoke clients avoid adaptive throttle reduction. Smoke runs therefore **do
not exercise default throttling in either direction**. Normal clients, manual
play, and F9 connections retain ENet defaults. The input carrier remains
unreliable-ordered; channels, rates, protocol and game rules are unchanged.

Default throttling can discard normal clients' queued inputs, and the server's
snapshots, when acknowledgement latency varies (including CPU stalls). This
normal-play concern remains open for later netcode design, such as redundantly
sending recent inputs. This pass changes only smoke-run configuration.

Input delivery may lose packets. Tests require at least 80% arrival, no more
arrivals than sends, exact counter accounting, and zero reordered inputs.
They never retry a scenario or require lossless delivery. Combat scripts repeat
hitscan/projectile requests until defeat. The three grenade smoke scripts repeat
throw presses every 20 client ticks until an authoritative snapshot shows a
grenade, then stop pressing. The owner-immunity setup moves only after that
observation. This is scripted input within one scenario, not a scenario rerun.

Assertion comments cite the P24–P27 reports under `build-report-logs/reports/`.
The existing Shared suite remains a separate command and is unchanged.
