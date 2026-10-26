# M4 Manual Checklist

This checklist tests the server-authoritative skeleton (milestone M4): a headless server, two game windows connected to it over your own computer, and the rules being decided by the server. Nothing here has been tried yet. Tick a box only after you have seen the thing happen. Each item ends with a short source in brackets. Reports are in `build-report-logs/reports/`; "vision doc" means the M4 section of `docs/00_project/game_vision_scope_and_roadmap.md`.

Do the section "Before You Start" first. If the GitHub Actions run is not green, stop and send the log to a developer; M4 is not closed until it is.

## How to run the game

All commands are typed in Terminal from the project folder:

```sh
cd /Volumes/etcetera/etceteraOS/ai-projects/code-playground/vantage
```

You need three Terminal windows for the connected checks. Start the server first, then the two clients. Port 8127 is used so that nothing already running on 7777 is disturbed. [P27 report, Manual Verification]

**Terminal 1: the server.**

```sh
godot-mono --headless --path server/headless -- --port 8127
```

**Terminal 2 and Terminal 3: one game window each.**

```sh
godot-mono --path client -- --connect --port 8127
```

**Offline game (no server).**

```sh
godot-mono --path client
```

[P26 and P27 reports, Manual Verification. The reports used port 7777 for the movement checks and 8127 for the combat checks; the port is only a choice, as long as server and clients use the same one.]

When you are done, stop the server with **Ctrl+C** in Terminal 1.

Controls: **W A S D** move, **mouse** aims, **left mouse button** shoots, **1** and **2** pick a gun, **G** throws a grenade, **F9** disconnects from the server and reconnects. [P25 and P26 reports; same keys as M3]

Notes, so you do not report these as bugs:
- Movement while connected looks steppy. There is no smoothing between server updates yet. [P26 D8; vision doc M4 acceptance: "No client interpolation yet"]
- The other player is drawn in orange. [P26 Manual Verification]
- Your own shots, projectiles and grenades never hurt you. [P27 D10]
- Grenade blasts go through walls. This is accepted. [P27 Manual Verification]
- There is no health bar yet; it comes in M7. In a connected game the instant-shot gun draws no red line. [P27 Manual Verification]

## Before You Start

- [ ] Verify the GitHub Actions run for `build.yml` finished green after you pushed. [P29 D7]
- [ ] Verify the shared unit tests pass on your machine: `dotnet build vantage.sln --configuration Debug --no-restore`, then `dotnet test tests/Vantage.Shared.Tests/Vantage.Shared.Tests.csproj --configuration Debug --no-build` reports 100 passed, 0 failed, 0 skipped. [P28 V4; P29 V4]
- [ ] Verify the server integration tests pass on your machine: after the build above, `dotnet test tests/Vantage.Server.IntegrationTests/Vantage.Server.IntegrationTests.csproj --configuration Debug --no-build` reports 16 passed, 0 failed, 0 skipped (it takes about 104 seconds), and `pgrep -fl godot` afterwards shows no Godot process left over from the tests. [P28 D2, D3, D8]

## Server Runs Cleanly at 60 Hz

- [ ] Verify the server starts and prints `port=8127` and `tick rate=60 Hz`. [P24 Manual Verification]
- [ ] Verify that after about 10 seconds you can press Ctrl+C and the last line reads `final tick=N; physics frames=N` with the two numbers equal (about 600 after 10 seconds), and `echo $?` right after prints `0`. [P24 D2, D8; vision doc M4 acceptance: "Server can run 60 Hz cleanly"]

## Two Players, Server In Charge (Movement)

- [ ] Verify both game windows show two players, yours and an orange one, and the same server is running. [P26 D5, D8; vision doc M4 manual validation]
- [ ] Verify that moving the player in one window moves its orange copy in the other window. [P26 D8]
- [ ] Verify both players stop at the outer walls and at each obstacle, in both windows. [P26 D8]
- [ ] Verify that while connected, pressing W A S D still moves your own player on screen, and it moves in the steppy way described above. Your own position on screen comes only from the server's updates, not from the game window deciding by itself. [P26 D6]
- [ ] Code check (ask a developer if you like): temporarily switch off the client's own movement code, start the server and one connected client, and confirm the server still moves the player from your key presses; the picture lags by about one update but moves correctly. Put the code back afterwards. [vision doc M4 manual review, "Cheat test"]
- [ ] Verify that a client cannot make up outcomes. The server only accepts input messages and ignores everything else. In the scripted bad-input run (`godot-mono --headless --path client -- --connect --port 8127 --net-smoke 600 --net-script illegal`, against a freshly started server), the server rejects the 200 bad inputs, nobody is damaged, no event appears, and nothing crashes. Also, no message exists that says "I killed you"; if a developer has to add one to test this, that is a design problem. [P27 D7; P26 D4; vision doc M4 manual review, "Cheat test 2"]

## Shooting Through the Server

For a clear shot between the two spawn points, move the player at the centre down to about y=96 before firing at the other spawn; the direct line between the spawns hits an obstacle corner. [P27 Manual Verification]

- [ ] Verify firing the instant-shot gun (key 1) at the other player flashes them, and four hits defeat them. After a defeat they reappear at their own spawn point with full health, and another hit works on them afterwards. [P27 D2, D10]
- [ ] Verify the defeat flash looks different from, and lasts longer than, the hit flash. [P27 Manual Verification]
- [ ] Verify the slow-bullet gun (key 2) shows an orange diamond in both windows, and a hit hurts only through the server. [P27 D4, D10]
- [ ] Verify a grenade (G) shows as a green diamond in both windows, explodes about 1.5 seconds after the throw, and hurts a player standing inside its radius. [P27 D5, D10]
- [ ] Verify walls stop instant shots and slow bullets: a shot or bullet with a wall or obstacle in the way does no damage. [P27 D3, D4, D10]
- [ ] Verify rapid clicking cannot fire faster than the gun allows (the instant-shot gun is limited to one shot per 15 server ticks, a quarter of a second), and holding the button down fires only once per press. [P27 D6, D10]
- [ ] Verify your own slow bullets do not hurt you. [P27 D10]
- [ ] Verify walking close to your own grenade blast gives you no damage flash and no defeat. [P27 D5, D10]

## Disconnect and Reconnect

- [ ] Verify pressing F9 in one window (click the window first so it has focus) removes that player's orange copy from the other window. [P26 D8; P27 D8, D10]
- [ ] Verify the window that pressed F9 gets local control back: it moves and shoots at the red dummies like in the offline game. [P26 D8; P27 D8]
- [ ] Verify the server and the other window keep running without errors or damage. [P27 D8]
- [ ] Verify pressing F9 again reconnects without restarting the game, and the two players see each other again. [P26 D8; P25 Manual Verification]
- [ ] Verify stopping the server with Ctrl+C while a client is connected makes the client report the loss and keep running, and that restarting the server and pressing F9 reconnects the same game window. [P25 Manual Verification]

## Offline Game and M3 Unchanged

- [ ] Verify the offline game (no `--connect`) moves, aims, shoots both guns, throws grenades, damages the red dummies, and resets them with R exactly like before. [P26 D8; P27 D8, D10]
- [ ] Verify every item in `tests/manual/checklist-m3.md` still behaves the same when run offline. [P27 D10]

## Optional

- [ ] OPTIONAL: Look at the bytes on the wire for one tick with a network sniffer such as Wireshark, watching the loopback traffic on the server's port while two clients are connected, and confirm there is no plaintext garbage. Skipping this does not block closing M4. [vision doc M4 manual review]

## Where the numbers come from

| Number in this list | Where it comes from |
|---|---|
| 60 Hz, final tick equals physics frames | P24 D2, D8 |
| 4 hits of 25 take a player to defeat, then back to full health (100) at the spawn | P27 D2 |
| 15 ticks between instant shots | P27 D6 |
| 1.5 s grenade fuse, 90 ticks | P27 D5 |
| 100 shared tests, 16 integration scenarios | P28 V4, V5 |
