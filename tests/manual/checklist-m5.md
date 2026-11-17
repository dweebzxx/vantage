# M5 Manual Checklist

This checklist tests team deathmatch and match flow (milestone M5): teams, a match timer, scoring, friendly fire off, the scoreboard, the end-of-match screen, and the automatic start of the next match. Tick a box only after you have seen the thing happen. Each item ends with a short source in brackets. Reports are in `build-report-logs/reports/`; "vision doc" means the M5 section of `docs/00_project/game_vision_scope_and_roadmap.md`.

Everything in M5 is drawn with stand-in art (flat colors and plain text). Final art is a separate, later step, so do not judge looks here beyond "can I read it". [M5 pass sequence, decision D9]

Do the section "Before You Start" first.

## How to run the game

All commands are typed in Terminal from the project folder:

```sh
cd /Volumes/etcetera/etceteraOS/ai-projects/code-playground/vantage
```

Start the server first, then the game windows. The server accepts at most 4 players. [P36 report]

**Terminal 1: the server, with a short match.**

```sh
godot-mono --headless --path server/headless -- --port 8127 --match-ticks 600,1800,600
```

The three numbers are the lengths of warmup, play, and the end-of-match pause, in server ticks (60 ticks are one second). `600,1800,600` gives a 10 second warmup, 30 seconds of play, and a 10 second pause. Leave `--match-ticks` out to get the real lengths: 10 second warmup, 5 minutes of play, 15 second pause. [P34 report; `MatchRules.cs`]

**One more Terminal per game window (2 to 4 of them).**

```sh
godot-mono --path client -- --connect --port 8127
```

**Offline game (no server).**

```sh
godot-mono --path client
```

When you are done, stop the server with **Ctrl+C** in Terminal 1.

Controls: **W A S D** move, **mouse** aims, **left mouse button** shoots, **1** and **2** pick a gun, **G** throws a grenade, hold **Tab** for the scoreboard, **F9** disconnects and reconnects. [M4 checklist; P38 report]

Who is on which team: the first player to join is Blue, the second Red, the third Blue, the fourth Red. Each new player goes to the team with fewer players, and to Blue when the teams are equal. [P32, P36 reports]

Notes, so you do not report these as bugs:
- Movement looks steppy and there is no health bar. Both arrive in M7. [M4 checklist]
- The instant-shot gun (key 1) draws no line when connected. A hit shows as a yellow flash on the player who was hit. [P27 Manual Verification]
- Shooting works in every phase, including warmup and the end-of-match pause. Only kills during play count. [P36 report, ruling of 2026-11-02]
- The match keeps running with one player, and with one team empty. It stops only when the last player leaves. [P32 report]
- For a clear shot between the first Blue and first Red spawn with the slow-bullet gun (key 2), walk down a little first; the straight line clips an obstacle corner. The instant-shot gun reaches directly. [M4 checklist; P39 report]

## Before You Start

- [ ] Verify the build and the shared unit tests pass on your machine: `dotnet build vantage.sln --configuration Debug --no-restore`, then `dotnet test tests/Vantage.Shared.Tests/Vantage.Shared.Tests.csproj --configuration Debug --no-build` reports 338 passed, 0 failed, 0 skipped. [P39 V3, V4]
- [ ] Verify the server integration tests pass on your machine: after the build above, `dotnet test tests/Vantage.Server.IntegrationTests/Vantage.Server.IntegrationTests.csproj --configuration Debug --no-build` reports 39 passed, 0 failed, 0 skipped (it takes about six and a half minutes), and `pgrep -fl godot` afterwards shows no Godot process left over from the tests. [P39 V8]
- [ ] Verify the GitHub Actions run for `build.yml` is green after you push the M5 work. [P29 D7]

## The Match Has a Beginning, Middle, and End

Start the short-match server and two game windows.

- [ ] Verify each window shows, at the top, a countdown, the line `BLUE 0 : 0 RED`, and `YOU: BLUE` in the first window and `YOU: RED` in the second. [P37 D8]
- [ ] Verify the word `WARMUP` shows for the first 10 seconds, counting down to `0:00`, and then disappears while the countdown restarts at `0:30`. [P37 D8]
- [ ] Verify the server Terminal prints a line starting `[Match] phase` each time the phase changes: to `Warmup`, to `Playing`, to `Postgame`, then to `Warmup` again, at the times above. [P34 D9]
- [ ] Verify that when the 30 seconds run out, the end-of-match screen appears by itself in both windows (see "End of Match" below), and after 10 more seconds a new warmup starts without anyone doing anything. [P38 D8; vision doc acceptance: "Server transitions cleanly to next match"]
- [ ] Verify that a second and third match in a row behave the same as the first. [vision doc acceptance: "A complete match flows"]
- [ ] Verify that closing every game window makes the server print a phase change to `Idle`, and that a window joining afterwards starts a fresh warmup at `0 : 0`. [P34 report]

## Teams and Colors

- [ ] Verify each player's body is drawn in its team color, blue or red-orange, in every window, your own body included. [P37 D8]
- [ ] Verify the yellow hit flash and the magenta defeat flash still show on top of the team color, and the body returns to the team color afterwards. [P37 D8]
- [ ] With three windows, verify the server prints teams Blue, Red, Blue in its `[Match] join` lines and all three players stand in different places. [P36 D10]
- [ ] With four windows, verify there are two Blue and two Red players, all in different places. [P36 D10]

## Scoring

- [ ] Verify a kill during play raises the killer's team score by one in every window, and the server prints `[Match] kill` with `scored=true`. [P34 D9; P37 D8]
- [ ] Verify a kill during warmup does not change the score, and the server prints `scored=false`. [P34 D9; P37 D8]
- [ ] Verify a kill during the end-of-match pause does not change the final score shown on the end-of-match screen. [P39 scenario MatchKillInPostgame]
- [ ] Verify a kill with each weapon counts the same: the instant-shot gun, the slow-bullet gun, and a grenade each add one point. [M5 pass sequence, decision D5]
- [ ] Verify a defeated player reappears at once, with no waiting, at one of their own team's two spawn points, and never on top of a teammate. [Decision Log, instant respawn; P36 D10]
- [ ] Make ten kills in one match (use the real match length or a longer `--match-ticks`, for example `600,7200,600`). Verify the two team scores add up to ten, and the kills and deaths on the scoreboard add up to ten each. [vision doc manual review: "Scoreboard sums correctly with 10 simulated kills"; P39 scenario TenKills]

## Friendly Fire Is Off

Use three or four windows so that one team has two players.

- [ ] Verify that shooting your teammate with the instant-shot gun gives them no yellow flash, however often you hit them. [vision doc manual review: "Friendly fire is genuinely off"; P39 scenario FriendlyFireShots]
- [ ] Verify the slow bullet stops at your teammate and does them no harm. [P39 scenario FriendlyFireShots]
- [ ] Verify a grenade exploding next to your teammate does them no harm, while the same grenade does harm an enemy standing inside the blast. [P39 scenario FriendlyFireGrenade]
- [ ] Verify a teammate can never be defeated by you, and your team's score never changes from shooting a teammate. [P39 scenario FriendlyFireShots]

## Scoreboard

- [ ] Verify holding **Tab** shows a panel with a Blue block and a Red block, each with its team score and one line per player with kills and deaths; releasing Tab hides it. [P38 D8]
- [ ] Verify your own line is brighter and ends with `(YOU)`. [P38 D8]
- [ ] Verify that after a kill during play, the killer's line shows one more kill and the victim's line one more death, in every window. [P38 D8]
- [ ] Verify the player with the most kills is at the top of their team's block. [P38 report, R2]
- [ ] Verify pressing Tab does nothing else: no focus box appears, and moving and shooting still work while the panel is up. [P38 D8]
- [ ] Verify the panel fits on screen and is readable with four players. [P38 Manual Verification]

## End of Match

- [ ] Verify the end-of-match screen names the winner: `BLUE WINS` or `RED WINS` for the team with more points, `DRAW` when the scores are equal. [P38 D8]
- [ ] Verify it shows the final scores, the scoreboard, and `Next match in` with a countdown that reaches `0:00` as the next warmup starts, and that the screen then goes away by itself. [P38 D8]
- [ ] Verify the `MATCH OVER` banner at the top is not shown while the end-of-match screen is up. [P38 D8]

## The Next Match Starts Clean

- [ ] Verify that when the new warmup starts, both team scores are back to `0 : 0` and every line on the scoreboard shows 0 kills and 0 deaths. [P36 D10; P39 scenario FullMatch]
- [ ] Verify every player is back at a spawn point of their team with full health at the start of the new warmup, and again when play starts. A player who was hurt in warmup takes four instant shots to defeat once play starts. [P36 D10]
- [ ] Verify a grenade thrown just before play starts never explodes, and you can throw another one right away. [P39 scenario ResetClearsAttacks]
- [ ] Team rebalance: join three windows (Blue, Red, Blue), close the Red one, and wait for the next match. Verify the server prints `[Match] team-move`, the later Blue player turns red in every window, and that window's line changes to `YOU: RED`. [P36 D10; P37 D8]

## Joining and Leaving Mid-Match

- [ ] Verify a window that joins during play shows the current score and countdown at once, not `0 : 0`, and its scoreboard lists the players already there with their kills and deaths. [P39 scenario LateJoin]
- [ ] Verify a player who leaves disappears from the other windows and from the scoreboard, and their team's score stays as it was. [P32 report]
- [ ] Verify **F9** (disconnect) removes the timer, scores, team line, and scoreboard from that window and returns the player to its normal offline color; **F9** again brings them back. [P37, P38 reports]
- [ ] Verify a fifth window cannot join while four are connected, and the four keep playing. [P36 report]

## A Real Five-Minute Match

- [ ] Start the server without `--match-ticks` and play one whole match with two or more windows. Verify warmup lasts 10 seconds, play lasts 5 minutes (the countdown starts at `5:00`), the end-of-match screen stays for 15 seconds, and the next warmup starts by itself. [vision doc deliverable: "5-min timer"; `MatchRules.cs`]

## Offline Game and M4 Unchanged

- [ ] Verify the offline game (no `--connect`) shows no timer, no scores, no team line, and no scoreboard on Tab, and plays exactly like before. [P37 D8; P38 D8]
- [ ] Verify moving, shooting, grenades, defeat, and respawn in a connected game feel the same as in M4. [P34 D9; P36 D10]
- [ ] Verify the items in `tests/manual/checklist-m4.md` under "Shooting Through the Server" and "Disconnect and Reconnect" still pass. Two M4 details changed on purpose: other players are drawn in their team color instead of orange, and a defeated player returns to a team spawn point instead of the place they joined. [P36, P37 reports]

## Where the numbers come from

| Number in this list | Where it comes from |
|---|---|
| 60 ticks per second | P24 D2 |
| Warmup 10 s, play 5 min, end-of-match pause 15 s | `shared/Vantage.Shared/Game/MatchRules.cs` |
| At most 4 players; 2 spawn points per team | P36 report |
| 4 hits of 25 defeat a player; full health is 100 | P27 D2 |
| 338 shared tests, 39 integration scenarios | P39 V4, V8 |
