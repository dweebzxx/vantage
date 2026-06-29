# vantage Core Document

## 0. External verification (current as of June 2026)

| Component | Your version | Current stable | Notes |
|---|---|---|---|
| Godot Mono | 4.7.stable | 4.7 (released 2026-06-18) | You are on the current stable. Good. |
| .NET SDK | 10.0.301 | .NET 10 LTS | Godot 4.7 requires .NET 8+. .NET 10 LTS works but Godot pins via `global.json`; pin yours explicitly. |
| Godot.NET.Sdk NuGet | n/a | 4.7.0 | Match Godot editor version. |
| Nakama image | 3.22.0 | ~3.30.x (May 2026 release) | Yours works; bump in a later pass. |
| Nakama Godot SDK | n/a | GDScript only (4.x) | Path B + C# means use `nakama-dotnet` NuGet on the client instead. See §3. |
| Docker Desktop (aarch64) | 29.5.3 | current | Fine on Apple Silicon. |
| Nakama tick cap | n/a | 30 Hz match handler | Irrelevant for Path B; your 60 Hz tick lives in the Godot headless server. |

Sources: godotengine.org downloads, github.com/godotengine/godot/releases, heroiclabs.com release notes, github.com/heroiclabs/nakama-godot README.

One important architectural callout from verification: the official Nakama Godot client is GDScript only. Heroic Labs explicitly notes the `nakama-dotnet` client works fine in Godot desktop builds. Since your project is C# and you have no HTML5 target, use `nakama-dotnet` directly. This avoids a GDScript/C# split in your client code.

---

## 1. Review of current state

### Must fix now (block before P01 bootstrap)

1. **`.gitignore` is missing the Nakama Postgres data dir.** `server/nakama/data/postgres/` will be created on first `docker compose up` and will fill the repo with binary database files. Add `server/nakama/data/` to `.gitignore`. Also add `*.user`, `*.userprefs`, `.vs/`, `.vscode/` (or commit a curated `.vscode/`), and `.idea/` while you are there.
2. **`.gitignore` does not cover Godot's properties.godot.cfg or export build outputs.** Add `.godot/` (you have this), and add `export_presets.cfg` decision: usually committed, but the build outputs from exports must be ignored. Plan to add `build/`, `exports/`, `dist/`, and `*.pck`.
3. **No `global.json` pinning .NET SDK.** With .NET 10 SDK installed and Godot.NET.Sdk pinned to 4.7.0 expecting .NET 8 LTS by default, you will eventually hit silent roll-forward bugs. Pin during P01.
4. **No `AGENT.md`, `metadata-schema.json`, or workflow scripts.** Required for your AI workflow. P01 adoption-bootstrap pass.
5. **No Godot project yet.** `client/` is empty. Without a Godot project, agents cannot work on client code. P02 should create the Godot project.

### Should fix soon (before any networked work)

6. **Nakama docker-compose values are dev defaults.** `console.username admin / password password` is fine locally, but document this and rotate before any LAN/VPS exposure.
7. **Postgres port 5432 is bound to host.** Not a security issue on localhost-only, but unnecessary; bind to `127.0.0.1:5432` explicitly to avoid surprise exposure if you turn off the macOS firewall.
8. **No `docs/` content yet.** You need a short `docs/architecture.md` and `docs/networking.md` before any networking pass so agents have authoritative references.
9. **No shared protocol library.** With Path B and C#, you want a `Vantage.Shared` C# class library referenced by both client and server projects. Create this before any RPC/state code.
10. **No CI.** Not required pre-MVP, but a single GitHub Actions workflow that does `dotnet build` on push catches a class of agent errors cheaply. Add by milestone M3.

### Can defer

11. CockroachDB vs Postgres choice. Postgres is fine for solo dev. Heroic Cloud uses CockroachDB; only relevant if you go to managed hosting.
12. Nakama TypeScript/Go server modules. Defer until you actually need server-side logic (matchmaking customization, ticket issuance, persistence rules). MVP only needs an RPC for game-server ticket issuance and that can be one small TS module.
13. Fleet Manager API for game-server allocation. Overkill for local single-server MVP.
14. Cross-platform export pipeline. Build for macOS arm64 only until M4. Add Windows/Linux exports in M5 once gameplay is stable.
15. Anti-cheat hardening. Authoritative server already gives you 90 percent of what an indie shooter needs. Add specific mitigations (rate limits, input sanity bounds, simple movement validators) in M6.

### Things that are fine

- Path B (Godot headless authoritative server + Nakama for non-gameplay) is the correct call for a C# shooter targeting eventual VPS hosting.
- Godot 4.7 .NET on Apple Silicon: known-good. ARM64 native binaries ship.
- Docker Desktop for Nakama: standard practice.
- Private GitHub repo with `gh` CLI: clean.
- Folder skeleton (`client/`, `server/`, `docs/`, `tools/`, `scripts/`, `build-report-logs/`): roughly right. I propose minor expansions in §5.

---

## 2. High-level development plan

Twelve phases, M0 through M11. Hours assume your 28 hr/week.

| # | Phase | Goal | Approx. weeks |
|---|---|---|---|
| M0 | Adoption bootstrap | AI workflow files, `global.json`, `.gitignore` fixes, `AGENT.md`, scripts, no gameplay code | 0.5 |
| M1 | Godot project + project skeleton | Empty Godot 4.7 .NET project, solution structure, Shared class library, builds clean | 0.5 |
| M2 | Local single-player core | Player movement, top-down aim, tile map test scene, placeholder sprites, fixed tick simulation in isolation | 2 |
| M3 | Local combat | Hitscan or projectile guns, grenades, damage, death, respawn, all single-player against dummy bots | 2 |
| M4 | Server-authoritative skeleton (no Nakama yet) | Standalone Godot headless server. Direct ENet connect. Client sends inputs, server simulates, broadcasts state. No prediction. 1v1 local. | 3 |
| M5 | TDM mode + match flow | 5 min TDM, two factions (cosmetic), instant respawn, FF off, scoreboard, round end, scoreboard reset | 2 |
| M6 | Nakama integration | Device auth, matchmaker, RPC issues game-server ticket, client connects to Godot server with ticket validated | 2 |
| M7 | Vertical slice | One polished arena, both factions, all weapons, sound, UI, music, win/lose flow, 10-player TDM stable on LAN | 3 |
| M8 | Content production + provenance | Final sprites, tilesets, SFX, music. Asset provenance schema enforced. Style guide locked. | 2 |
| M9 | Cross-platform exports + CI | Windows, Linux, macOS exports. GitHub Actions builds on push. Headless server export for Linux ARM64. | 1.5 |
| M10 | Private testing | Closed playtest with 4-10 friends. Server on Hetzner ARM. Log collection. Bug fixes. | 2 |
| M11 | Public-test readiness | Latency hardening (here is where snapshot interpolation and prediction become non-optional), rate limits, basic anti-cheat, store/marketing prep if commercial. | open-ended |

Practical sequencing notes:

- Do **not** wire Nakama before M4. Get the Godot client/server loop working first against a fixed local IP. Nakama is plumbing; gameplay is the risk.
- **Snapshot interpolation** belongs at M7 (vertical slice), not M4. Even on LAN at 60 Hz, you will see jitter when you exceed two clients. Interpolation is small and worth the early investment.
- **Client prediction and reconciliation** belong at M11. Do not touch them until your server simulation is rock solid and your protocol is stable. Adding prediction to an unstable protocol triples the debugging surface.
- **Lag compensation** (server-side rewind for hit registration) is M11+ and only if you ship publicly. For LAN and localhost it is unnecessary.

---

## 3. Recommended architecture

### 3.1 Top-level model

```
[Client (Godot .NET)] <--ENet--> [Godot Headless Server (.NET)]
        |
        +----WebSocket/HTTP----> [Nakama (Docker)] <-- Postgres
```

Three processes. One persistent Nakama. One Godot headless server per match (in MVP, one fixed instance handles one match at a time). N clients per match (1 to 10).

**Nakama's role:**
- Device-ID authentication, session tokens.
- Matchmaking (clients enter pool, get matched into a party).
- An RPC (`get_game_server_ticket`) that returns the game server endpoint plus a short-lived signed ticket (HMAC over user_id + match_id + expiry, shared secret with the server).
- Future: persistence (stats, XP, cosmetics).

**Godot headless server's role:**
- Owns gameplay state.
- Listens for ENet connections.
- Validates the Nakama ticket on connect.
- Runs the fixed-tick simulation at 60 Hz.
- Broadcasts state snapshots.
- Logs to `build-report-logs/server/`.

**Client's role:**
- Renders.
- Captures input every frame.
- Sends input commands to server at fixed tick rate.
- Receives authoritative state and renders it (initially with no interpolation, later with snapshot interpolation, eventually with prediction).
- Talks to Nakama via `nakama-dotnet` for auth and matchmaking only.

### 3.2 C# solution layout

```
vantage.sln
├── client/Vantage.Client.csproj         # references Godot.NET.Sdk; the Godot project
├── server/headless/Vantage.Server.csproj # references Godot.NET.Sdk; headless export target
└── shared/Vantage.Shared.csproj         # plain .NET class library; no Godot deps
```

Shared library contains: command and state DTOs, serialization helpers (e.g. `MemoryPack` or `MessagePack-CSharp`), enums (`WeaponId`, `Faction`, `MatchState`), tick math, deterministic-friendly types where possible. Both client and server reference Shared.

This is the single most important architectural decision for avoiding a rewrite later. Do not let weapon stats, damage formulas, or protocol structs live in `client/` only.

### 3.3 Godot client structure

```
client/
├── project.godot
├── scenes/
│   ├── Boot.tscn               # entry, autoloads, version check
│   ├── MainMenu.tscn
│   ├── Lobby.tscn              # Nakama matchmaking UI
│   ├── Match.tscn              # in-game scene, instances Arena.tscn
│   └── arenas/Arena01.tscn
├── autoloads/
│   ├── GameClient.cs           # owns ENet peer + connection lifecycle
│   ├── NakamaService.cs        # owns Nakama client + session
│   └── InputBus.cs             # central input capture
├── entities/
│   ├── PlayerView.tscn         # purely visual; no authoritative state
│   ├── BulletView.tscn
│   └── GrenadeView.tscn
├── ui/
└── debug/                      # net stats overlay, tick visualizer
```

The Godot scene tree on the client is **view only** after M4. Logic lives in C# classes that consume server snapshots.

### 3.4 Godot headless server structure

```
server/headless/
├── project.godot               # separate Godot project, no graphics
├── ServerMain.cs               # entry point invoked via --headless
├── match/
│   ├── MatchHost.cs            # the per-match simulator
│   ├── TickLoop.cs             # 60 Hz fixed loop, uses _PhysicsProcess at physics_fps=60
│   ├── World.cs                # authoritative world state
│   └── systems/
│       ├── MovementSystem.cs
│       ├── WeaponSystem.cs
│       ├── GrenadeSystem.cs
│       └── DamageSystem.cs
├── net/
│   ├── ServerPeer.cs           # ENetMultiplayerPeer wrapper
│   ├── TicketValidator.cs      # validates Nakama-issued HMAC ticket
│   └── Protocol.cs             # uses Vantage.Shared
└── arenas/Arena01.tscn         # same arena scene as client (shared via symlink or duplicate)
```

The server runs Godot in headless mode (`godot --headless`). You get physics, collision shapes, scenes, TileMapLayer queries, and a render-free process. That is the whole reason to use Godot headless instead of a from-scratch C# server.

### 3.5 Fixed-tick simulation

- `physics_fps = 60` in both client and server `project.godot`.
- Server `_PhysicsProcess(delta)` is the authoritative tick.
- Each tick has a monotonically increasing `uint` tick number. All protocol messages reference this tick number.
- Client sends `InputCommand { tickClientPredicted, moveAxis, aimAngle, buttons }` at the same 60 Hz. Server processes the latest input per player per tick. Out-of-order or stale inputs are dropped.
- Server broadcasts `StateSnapshot { tick, players[], projectiles[], events[] }` at 30 Hz initially (every other tick) to halve bandwidth. Bump to 60 Hz later if needed.

### 3.6 Protocol evolution path

| Phase | Client | Server |
|---|---|---|
| M4 | Send input → wait for state → render last known state | Receive input → simulate → broadcast state |
| M7 | Same + snapshot interpolation (render at `serverTime - 100ms`) | Send timestamps with snapshots |
| M11 | Add input prediction + reconciliation on the client | Add lag-compensated hitscan; keep recent state history |

Designing for this from M4: use a `WorldState` ring buffer on the server even before you do lag comp. It costs nothing and means M11 is a feature add, not a rewrite.

### 3.7 Serialization

Use **MemoryPack** (zero-allocation, source-generated) over MessagePack for greenfield C# 12 code. Both client and server reference the Shared library where DTOs are decorated with `[MemoryPackable]`. Avoid Godot's `Variant` for protocol; it is convenient but locks you to Godot and is slow.

### 3.8 Local development server setup

Three terminals for local dev:
1. `docker compose up` from `server/nakama/`.
2. `godot --headless --path server/headless/ -- --port 7777` for the game server.
3. Godot editor open on `client/` for the client.

A `scripts/dev/up.zsh` wraps this. A `scripts/dev/down.zsh` tears it down. Both pre-authorized for the AI workflow.

### 3.9 Cross-platform export strategy

| Target | Use | Built when |
|---|---|---|
| macOS arm64 (.NET) | Your dev box client | Every M2+ |
| Linux ARM64 headless (.NET) | VPS server | M9+ |
| Linux x64 (.NET) | Friends without Macs | M9+ |
| Windows x64 (.NET) | Friends with Windows | M9+ |
| Linux ARM64 (.NET, client) | Only if anyone playtests on Pi-class hardware | defer |

Godot 4.7 .NET ships Linux ARM64 templates. Confirmed via the official download page.

### 3.10 CI/CD strategy

- M0–M3: no CI. Local builds only.
- M4: GitHub Actions workflow that runs `dotnet build vantage.sln` on push to `main`. Fails fast on broken code from AI passes. About 30 lines of YAML.
- M9: matrix build to all five export targets, artifacts uploaded.
- M10: tag-based release builds.

GitHub Actions has macOS arm64, Linux x64, and Windows runners free for private repos within quota. Linux ARM64 is available on `ubuntu-24.04-arm` runners.

### 3.11 Testing strategy

- **Unit tests** in `tests/Vantage.Shared.Tests/` (xUnit). Cover damage math, weapon stats, tick math, ticket signing/validation. These run in CI from M4.
- **Headless server integration tests**: a small harness that boots `Vantage.Server` in-process, connects fake clients, runs a deterministic match, asserts final state. Start in M5.
- **Manual smoke tests**: a `tests/manual/checklist-mN.md` per milestone. Replaces unit tests for engine-side concerns (animation, audio, input).

### 3.12 Logging and debugging

- Server logs to `build-report-logs/server/YYYY-MM-DD/server-<pid>.log`.
- Each match logs a per-match JSON line stream (one line per tick, fields configurable) at `build-report-logs/matches/<match-id>.jsonl`. Off by default; flag-enabled. Invaluable for "what happened on tick 1247" debugging.
- Client has a debug overlay (F3): RTT, last server tick, jitter, dropped inputs, current tick, fps. Always-on in dev builds.
- Nakama logs go to its own container; `docker compose logs -f nakama`.

### 3.13 Security and anti-cheat (indie scope)

For private testing (M10), minimum bar:
1. Server is authoritative for movement, damage, weapon state, ammo, faction membership. Client never tells the server "I dealt X damage."
2. Input bounds validation: aim angle is wrapped, movement axis clamped to [-1, 1], fire rate clamped at server.
3. Ticket validation on connect (HMAC, 60 second expiry).
4. Rate limit per-IP and per-user-id on Nakama RPCs.
5. Strip debug RPCs from non-dev builds via a `#if DEBUG_NET` guard.

For public play (M11+), add:
6. Encrypted transport (ENet supports DTLS; Godot wraps this).
7. Server-side movement validators (max speed, max teleport delta per tick).
8. Crash and disconnect telemetry.

What you do **not** do as a solo indie:
- No EAC, BattlEye, or kernel anti-cheat. Wrong scale.
- No client integrity checks beyond build hash.

---

## 4. AI CLI agent usage strategy

### 4.1 Tool allocation

| Agent | Primary use | Why |
|---|---|---|
| Claude Opus (web/chat) | This kind of planning, architecture review, prompt generation, pass design, code review of finished passes | Strongest on structured reasoning across long context; you trust the chain. |
| Claude Code | Trusted implementation passes that touch protocol, server core, shared library, security-relevant code | Highest trust; reserve for code that is hard to undo. |
| Codex | Bulk implementation: UI scenes, audio glue, content wiring, refactors with clear scope | Usage-limit relief for high-volume but lower-risk work. |
| Antigravity | Review passes, test-writing passes, documentation passes, fix-the-failing-CI passes | Good for tasks where a fresh perspective helps, and where the diff is small. |

### 4.2 Pass discipline

Per your workflow guide v6.2:
- **One logical unit per pass.** Examples of one unit: "add `Vantage.Shared` and define `InputCommand`," "implement `MovementSystem` server-side and its unit tests," "wire Nakama device-auth and surface it in `Lobby.tscn`."
- **Never combine** bootstrap + implementation, or maintenance + implementation. Per the workflow guide.
- **Architecture changes are deliberate.** If a pass would change a protocol DTO, the prompt explicitly authorizes it, and the report calls it out.

### 4.3 Routing rules

Send to **Claude Code** when:
- Protocol changes (anything in `Vantage.Shared`).
- Anything in `server/headless/net/` or `match/`.
- Ticket validation and crypto.
- Anything that touches `AGENT.md`.

Send to **Codex** when:
- UI scenes (`.tscn` files plus their C# scripts).
- Asset import pipeline.
- Audio glue.
- Routine refactors with a defined target shape.

Send to **Antigravity** when:
- Writing or fixing tests.
- Documentation passes.
- Reviewing a Claude Code pass for missed cases (a review-only pass that produces a markdown report, no code edits).
- Fixing CI failures with bounded scope.

Send to **manual you** when:
- Picking weapon balance numbers (you, not an agent, decides what fun feels like).
- Choosing art direction.
- Approving any protocol change.
- Resolving merge conflicts between two agent branches.

### 4.4 Architectural consistency across agents

The hard problem with multi-agent work: agent A renames a method, agent B keeps the old name. Defenses:

1. **`AGENT.md` is the contract.** Every pass starts by reading `AGENT.md`. It lists naming rules, the protocol DTO source of truth (`Vantage.Shared`), the tick rate, the serialization library, what files are off-limits.
2. **Source of truth files.** `docs/architecture.md` and `docs/networking.md` are read-only references the agents are told to consult. They are updated by you, not by agents, except in explicit doc passes.
3. **Build-on-pass is mandatory.** Every pass ends with `dotnet build vantage.sln` clean. If it does not build, the pass failed.
4. **Diff review.** Every pass produces a clean diff in the report. You read it.
5. **No simultaneous overlapping passes.** Wait for pass N to be reviewed and committed before starting N+1. Even with multiple CLI tools available, do not parallelize on the same branch.

### 4.5 Prompt hygiene

Every implementation prompt must include:
- The exact scope (what file(s) to touch, what to leave alone).
- The relevant section of `docs/architecture.md`.
- The exact acceptance criteria (build clean, test names, manual verification steps).
- An explicit "do not change protocol DTOs unless authorized" guard.
- An explicit "do not edit `AGENT.md`" guard unless the pass is a maintenance pass.

The workflow guide's three-block format (Save Prompt / Save Launcher / Run Launcher) enforces this.

### 4.6 Handling usage limits

When Claude Code is rate-limited:
- Switch the next pass to Codex, but only if it is in the Codex category from §4.3.
- Never demote a Claude-Code-category task to Codex just because Claude is rate-limited. Wait, or do something else from the backlog instead.

When Codex is rate-limited:
- Antigravity for review, test-writing, and doc passes is the usual relief.
- For UI work specifically, you can also do it manually in the editor and have an agent write only the code-behind.

### 4.7 What never gets delegated blindly

- Initial protocol design.
- Security primitives (ticket signing, HMAC verification).
- Balance numbers.
- Anything that touches `server/nakama/modules/` server-side code that runs untrusted client RPCs.
- Asset licensing decisions.
- Anything in `.github/workflows/` on the first write (review every line of the YAML).

---

## 5. Recommended directory structure

```
vantage/
├── AGENT.md                              # AI workflow contract
├── README.md
├── LICENSE                               # add when you decide license
├── .gitignore
├── .gitattributes                        # LFS rules for binary assets
├── global.json                           # pin .NET SDK
├── vantage.sln
│
├── client/                               # Godot client project
│   ├── project.godot
│   ├── Vantage.Client.csproj
│   ├── scenes/
│   ├── autoloads/
│   ├── entities/
│   ├── ui/
│   ├── debug/
│   ├── assets/
│   │   ├── sprites/                      # final, optimized sprites
│   │   ├── tiles/
│   │   ├── audio/
│   │   ├── music/
│   │   └── fonts/
│   └── export_presets.cfg
│
├── server/
│   ├── headless/                         # Godot headless authoritative server
│   │   ├── project.godot
│   │   ├── Vantage.Server.csproj
│   │   ├── ServerMain.cs
│   │   ├── match/
│   │   ├── net/
│   │   ├── arenas/                       # arena .tscn (kept in sync with client)
│   │   └── export_presets.cfg
│   └── nakama/
│       ├── docker-compose.yml
│       ├── data/                         # gitignored
│       └── modules/
│           ├── package.json              # for TS runtime later
│           └── (ts modules)
│
├── shared/
│   └── Vantage.Shared/
│       ├── Vantage.Shared.csproj
│       ├── Protocol/
│       │   ├── InputCommand.cs
│       │   ├── StateSnapshot.cs
│       │   └── Events.cs
│       ├── Game/
│       │   ├── WeaponDef.cs
│       │   ├── Faction.cs
│       │   └── DamageMath.cs
│       └── Net/
│           ├── TickMath.cs
│           └── Ticket.cs                 # signing + validation
│
├── tests/
│   ├── Vantage.Shared.Tests/
│   ├── Vantage.Server.IntegrationTests/
│   └── manual/
│       ├── checklist-m2.md
│       └── checklist-m3.md
│
├── tools/                                # asset pipeline, scripts unrelated to runtime
│   ├── asset-import/
│   └── provenance/
│       └── provenance-schema.json
│
├── scripts/
│   ├── ai-post-pass.zsh
│   ├── ai-tool-preflight.zsh
│   ├── dev/
│   │   ├── up.zsh
│   │   └── down.zsh
│   └── exports/
│       ├── export-mac.zsh
│       ├── export-linux.zsh
│       ├── export-linux-arm64.zsh
│       └── export-server-linux-arm64.zsh
│
├── assets-source/                        # raw, non-imported source assets
│   ├── sprites-raw/
│   ├── tiles-raw/
│   ├── audio-raw/
│   ├── music-raw/
│   └── provenance/
│       └── manifest.json                 # per-asset provenance entries
│
├── docs/
│   ├── architecture.md
│   ├── networking.md
│   ├── gameplay.md
│   ├── assets.md
│   ├── ai-workflow.md
│   └── adr/                              # architecture decision records
│       └── 0001-godot-net.md
│
├── build-report-logs/                    # gitignored, per workflow guide
│   ├── passes/
│   ├── server/
│   └── matches/
│
├── prompts/                              # saved prompts, per workflow guide
│   ├── p01-bootstrap/
│   └── pNN-.../
│
├── launchers/                            # saved launchers, per workflow guide
│   └── pNN-.../
│
└── .github/
    └── workflows/
        ├── build.yml
        └── release.yml
```

Notes:

- `shared/Vantage.Shared/` is a plain .NET class library, **not** a Godot project. Both client and server `csproj` files have `<ProjectReference Include="..\..\shared\Vantage.Shared\Vantage.Shared.csproj" />`.
- `assets-source/` versus `client/assets/`: the former is the raw generation output (Midjourney PNGs at original resolution, Suno WAVs at 44.1k, etc.) with provenance metadata. The latter is the imported, optimized, in-engine version. Both are committed for an indie project at your scale; switch to Git LFS for `assets-source/` if it grows past 200 MB.
- `arenas/` exists in both `client/` and `server/headless/`. Keep them identical. Symlink is the cleanest mechanism on macOS, but it complicates Windows checkout. I recommend duplicating the file and adding a `tools/sync-arenas.zsh` script that verifies they match. Diff fails the CI build if they diverge.

---

## 6. Asset and materials plan

Asset categories with their phase-of-need, format, source, and provenance treatment.

### 6.1 Placeholder phase (M2 to M4)

Goal: gray boxes that move. No art investment.

| Asset | What | Format | Source |
|---|---|---|---|
| Player sprite (placeholder) | 32x32 colored square with direction indicator | PNG | Hand-drawn in any tool |
| Bullet | 4x4 dot | PNG | Same |
| Grenade | 8x8 circle | PNG | Same |
| Tilemap test | 16-tile grid, two terrain types | PNG, 16x16 tiles | Same |
| HUD | Programmer-art labels in Godot's default font | Godot Theme | Same |
| SFX | None (silent) | n/a | n/a |

No provenance burden in placeholder phase. Mark all placeholder assets with filename suffix `_placeholder.png`. A grep for `_placeholder` tells you what is real and what is fake.

### 6.2 Functional art phase (M5 to M7)

Goal: looks like a game, not a finished one.

| Asset | What | Format | Source |
|---|---|---|---|
| Player base sprite | 8-direction or rotational base, 64x64 | PNG, transparent | Midjourney / Perchance, then cleaned in Aseprite or Photopea |
| Weapon overlays | Held-weapon overlays for each gun, per-direction | PNG | Same |
| Two faction tints | Color palette swap rules | Godot ShaderMaterial | You |
| Grenade sprite + arc | 16x16 sprite, particle trail | PNG + Godot particles | AI + engine |
| Bullet projectile | 8x8 sprite, small tracer | PNG | AI |
| Explosion | 64x64 spritesheet, 8 frames | PNG | AI generation, then frame-aligned in Aseprite |
| Hit flash | Simple ShaderMaterial | shader code | You or AI |
| Death | 32x32 spritesheet, 6 frames | PNG | AI |
| Tileset | 32x32 tiles, one arena's worth, 60 to 100 tiles | PNG tilesheet | AI generation, then arranged into a `TileSet` resource manually |
| Collision tiles | Defined as part of the `TileSet` resource | Godot resource | You |
| Environment props | Crates, barrels, doors (decorative), 20 pieces | PNG | AI |
| UI / HUD | Health bar, ammo counter, scoreboard, kill feed, faction indicator, minimap (optional) | PNG + Godot theme | You |
| Menus | Main menu background, button styles | PNG + Godot theme | AI for backgrounds |
| Font | One pixel UI font + one fallback for body text | TTF or BMFont | Free licensed font (e.g. m5x7, m6x11 by Daniel Linssen) or AI generation is not practical for fonts |
| Color palette | Locked palette (16 or 32 colors) | Hex list in `docs/assets.md` | You |
| Sound effects | Gunshots (per weapon), grenade prime, grenade explode, footsteps, hit flesh, hit wall, reload, death, round win/lose, UI clicks | WAV 44.1k mono | Gemini for SFX, then cleaned in Audacity |
| Music | Menu theme, in-match loop (one track), victory sting, defeat sting | OGG | Suno / Udio |

### 6.3 Polish phase (M8)

| Asset | What |
|---|---|
| Player walk/run cycles | Proper 4 or 8-direction walk cycles, 6 to 8 frames each |
| Weapon-specific muzzle flash | Per-weapon flash overlays |
| Tile variants | Damage variants, edge variants, decals |
| Better explosion | Multi-layer with screen shake hook |
| HUD polish | Animations on state changes (hit flash, score increment) |
| Final music pass | Mastered to consistent LUFS |
| Trailer music | Separate track |

### 6.4 Marketing assets (M11 prep, only if commercial)

Capsule art (Steam sizes if you choose Steam), trailer (1-2 min), screenshots (1920x1080, no UI clutter), gif loops for social, store page copy, key art (the marquee image), favicon if you have a website. None of this is needed for private playtests.

### 6.5 AI generation workflow

A practical pipeline that prevents asset chaos:

1. **Prompt template per category.** `assets-source/prompt-templates/sprites.md`, `tiles.md`, `sfx.md`, `music.md`. Each defines the canonical style description (pixel art, palette reference, 32x32 base, no shading, etc.). Every generation prompt starts with this template.
2. **Generate in batches.** Generate 10 to 20 variants per asset. Pick the best. Record what won and why.
3. **Per-asset manifest entry.** Every accepted asset gets an entry in `assets-source/provenance/manifest.json` (see §6.6).
4. **Engine-side import.** Run the source asset through a small import script that resizes, palette-snaps, and outputs to `client/assets/`. Keep the source file unchanged in `assets-source/`.
5. **Naming convention.** `<category>_<subject>_<variant>_v<n>.<ext>`, lowercase, underscore-separated. Example: `sprite_player_base_v3.png`, `sfx_gunshot_rifle_v2.wav`. Version on substantive change, not on minor edits.

### 6.6 Provenance schema (hobby-grade, hardening-ready)

`assets-source/provenance/manifest.json` entries shape:

```json
{
  "asset_path": "assets-source/sprites-raw/sprite_player_base_v3.png",
  "category": "sprite",
  "tool": "midjourney",
  "tool_version": "v7",
  "prompt": "...",
  "negative_prompt": null,
  "seed": null,
  "generated_at": "2026-07-15T14:33:00Z",
  "post_processing": ["aseprite_cleanup", "palette_snap_16"],
  "license_review_status": "hobby_unreviewed",
  "commercial_clearance": false,
  "engine_path": "client/assets/sprites/sprite_player_base.png"
}
```

For hobby phase you do not need every field perfectly filled. The minimum is: `asset_path`, `tool`, `prompt`, `generated_at`, `engine_path`, `commercial_clearance: false`. The hardening pass later (between M11 and any commercial release) walks the manifest and fills the rest, regenerates assets where the tool's commercial license is unclear, and flips `commercial_clearance: true` per asset.

This protects you from a worst case: you decide to commercialize and have no records of which Midjourney plan an asset was generated under. Per-asset metadata is cheaper to maintain than to reconstruct.

### 6.7 Asset versioning rules

- Bump the `v<n>` suffix when you accept a new generation that replaces the previous.
- Keep prior versions in `assets-source/` (cheap, useful for going back).
- The `client/assets/` filename is stable (no `_v<n>` suffix). Only the source carries the version.
- When importing, the import script overwrites `client/assets/<name>.png` and updates `engine_path` in the manifest.

### 6.8 Font and palette

- Pick one pixel font and stick with it. m5x7 (free, OFL) or similar.
- Lock a 16- or 32-color palette before M5. Reference it in every art prompt. This is the single highest-leverage decision for art coherence.

---

## 7. Materials needed by milestone

| Asset | Why | Milestone needed | Placeholder OK? | Format | Source | Risk if late |
|---|---|---|---|---|---|---|
| Placeholder player sprite | Test movement and aim | M2 | Yes (gray box) | PNG 32x32 | Manual | None |
| Test tilemap | Validate `TileMapLayer` and collisions | M2 | Yes | PNG 16x16 tiles | Manual | None |
| Placeholder bullet/grenade | Combat prototype | M3 | Yes | PNG dots | Manual | None |
| Placeholder HUD | Show health/ammo | M3 | Yes | Godot default theme | Manual | None |
| Final player sprite (both factions) | Visual identity | M5 | No by end of M5 | PNG | Midjourney + Aseprite | Blocks M7 vertical slice |
| Final weapon overlays | Gun feel | M5 | No | PNG | Midjourney + Aseprite | Blocks M7 |
| Tileset for Arena01 | Real arena | M5 | Partially (placeholder until end of M5) | PNG tileset + `.tres` | Midjourney + manual `TileSet` build | Blocks M7 |
| Environment props | Cover and decoration | M6 | Yes (boxes) | PNG | Midjourney | Hurts feel, not function |
| Gunshot SFX (per weapon) | Combat feel | M5 | Silent OK until end of M5 | WAV mono | Gemini SFX | Blocks M7 |
| Grenade SFX (prime, explode) | Critical for grenade reads | M5 | Same | WAV mono | Gemini | Blocks M7 |
| Hit/death SFX | Feedback | M5 | Same | WAV mono | Gemini | Blocks M7 |
| Footstep SFX | Audio cues | M6 | Yes | WAV | Gemini | Quality only |
| UI SFX (clicks, score) | Polish | M7 | Yes | WAV | Gemini | Polish only |
| Match music | Atmosphere | M7 | Silent OK | OGG | Suno/Udio | Polish only |
| Menu music | Atmosphere | M7 | Silent OK | OGG | Suno/Udio | Polish only |
| Pixel font | UI legibility | M5 | Godot default | TTF or BMFont | Daniel Linssen or similar | None |
| Color palette doc | Coherence | M5 (lock by start) | n/a | Hex list in docs | You | Cohesion debt if late |
| Explosion sheet | Grenade visual | M5 | Yes (single sprite) | PNG sheet | AI + Aseprite | Blocks M7 |
| Hit flash | Damage feedback | M5 | Yes | Godot shader | You/AI | Polish only |
| Net debug overlay | Dev tool | M4 | n/a | Code only | You | Blocks M4 |
| Provenance manifest | Commercial readiness | M5 onward | n/a | JSON | You + script | Critical if commercial; trivial if you keep up with it |
| Marketing screenshots | Store/press | M11 | n/a | PNG 1920x1080 | Game capture | Only if commercial |
| Trailer | Steam/press | M11 | n/a | MP4 | Game capture + edit | Only if commercial |

---

## 8. Technical milestones

Each milestone has: goal, deliverables, code systems, assets, validation, acceptance, risks, AI pass ideas, manual review checklist.

### M0: Adoption bootstrap (0.5 weeks)

- **Goal:** AI workflow in place, no gameplay code yet.
- **Deliverables:** `AGENT.md`, `metadata-schema.json`, `scripts/ai-post-pass.zsh`, `scripts/ai-tool-preflight.zsh`, `build-report-logs/` directory, `global.json`, fixed `.gitignore`, `docs/architecture.md` skeleton, `docs/networking.md` skeleton, `docs/ai-workflow.md`.
- **Code systems:** none.
- **Assets:** none.
- **Validation:** `dotnet --info` shows .NET 10 LTS in use. Workflow scripts run without error.
- **Acceptance:** Next pass can be generated by your workflow agent using the v6.2 process.
- **Risks:** Scope creep (don't add gameplay).
- **AI pass:** P01 single pass. Claude Code, since this touches `AGENT.md` which is the contract.
- **Manual review:** Read `AGENT.md` line by line. Verify `.gitignore` excludes `server/nakama/data/`. Test `scripts/ai-post-pass.zsh` end-to-end on a fake report.

### M1: Project skeleton (0.5 weeks)

- **Goal:** Empty but buildable solution.
- **Deliverables:** `vantage.sln`, `client/` (empty Godot 4.7 .NET project), `server/headless/` (empty Godot 4.7 .NET project), `shared/Vantage.Shared/`, `tests/Vantage.Shared.Tests/` (xUnit), build passes, one trivial unit test passes.
- **Code systems:** Project references, build configuration.
- **Assets:** none.
- **Validation:** `dotnet build vantage.sln` clean. `dotnet test` runs with one passing test.
- **Acceptance:** You can open the client project in Godot editor and it loads with no errors.
- **Risks:** Godot.NET.Sdk version mismatch with .NET 10. Pin via `global.json` and `<TargetFramework>net10.0</TargetFramework>` in csproj.
- **AI pass:** P02 (Claude Code).
- **Manual review:** Project loads in Godot. Shared library compiles independently. No Godot reference leaks into Shared.

### M2: Single-player core (2 weeks)

- **Goal:** Top-down movement, aiming, and a test arena run locally.
- **Deliverables:** `Boot.tscn`, `Arena01.tscn` (test), `PlayerView.tscn`, movement with keyboard, aim with mouse, camera follow, working `TileMapLayer` with collisions.
- **Code systems:** Input capture, fixed `_PhysicsProcess` movement, collision, camera. Placeholder weapon mount point.
- **Assets:** Placeholder player sprite, test tilemap (per §6.1).
- **Validation:** Manual playtest checklist `tests/manual/checklist-m2.md`.
- **Acceptance:** You can walk around the arena with WASD, aim with mouse, collide with walls. 60 FPS on M3.
- **Risks:** Over-investing in animation polish. Keep visual minimal.
- **AI passes:** P03 (input + movement, Claude Code), P04 (tilemap test scene, Codex), P05 (camera + aim, Codex).
- **Manual review:** Movement feels right (you, not an agent, judge). No physics jitter at 60 Hz.

### M3: Local combat (2 weeks)

- **Goal:** Single-player can shoot a dummy.
- **Deliverables:** Hitscan and projectile guns (one of each), grenade with arc and timer, damage, health, death, respawn.
- **Code systems:** Weapon definitions in Shared, weapon firing logic on a single "world" object (still single-player), grenade physics, damage application.
- **Assets:** Placeholder bullet, grenade, explosion, HUD with health/ammo.
- **Validation:** Damage math unit tests in `Vantage.Shared.Tests`. Manual combat smoke test.
- **Acceptance:** Player can kill a stationary dummy with each weapon. Grenade explodes after 2 s and damages within radius.
- **Risks:** Building weapon logic into Godot scene scripts instead of into Shared. Avoid. Weapon definitions and damage math live in Shared.
- **AI passes:** P06 (WeaponDef + DamageMath in Shared, Claude Code), P07 (hitscan + projectile firing on world, Claude Code), P08 (grenade, Codex), P09 (HUD wiring, Codex), P10 (damage tests, Antigravity).
- **Manual review:** Weapon stats in Shared are the ones used at runtime. Open `WeaponDef.cs` and confirm. No magic numbers in scene scripts.

### M4: Server-authoritative skeleton (3 weeks)

This is the milestone that determines whether the project lives or dies.

- **Goal:** 1v1 over loopback with the architecture from §3. No Nakama yet.
- **Deliverables:** `Vantage.Server` headless project, ENet server peer, `InputCommand`/`StateSnapshot` DTOs in Shared, client sends inputs, server simulates, client renders authoritative state. Hardcoded IP, no auth.
- **Code systems:** `ServerMain.cs`, `MatchHost`, `TickLoop`, `World`, `MovementSystem`, `WeaponSystem` (server-side), `ServerPeer`, `Protocol`. Client `GameClient` autoload. MemoryPack serialization in Shared.
- **Assets:** none new.
- **Validation:** Integration test that boots the server in-process, connects two fake clients, sends inputs, asserts world state. Manual: two Godot editor instances on the same machine, both connect, both see each other move.
- **Acceptance:** 1v1 over loopback. State is authoritative (try cheating client-side, server ignores). Server can run 60 Hz cleanly. No client interpolation yet, jitter visible at WAN but fine at loopback.
- **Risks:**
  - Building movement logic that lives only on the client and "echoing" to server. Wrong. Build it in Shared (math) and run it on the server (authoritative).
  - Choosing custom serialization. Don't. Use MemoryPack.
  - Skipping the ring buffer of past world states. Add it now even though you don't use it yet; M11 reconciliation depends on it.
- **AI passes:** P11 to P16. Mostly Claude Code (this is the trusted path).
  - P11 protocol DTOs in Shared.
  - P12 server `MatchHost` + `TickLoop`.
  - P13 server `MovementSystem` consuming `InputCommand`.
  - P14 client `GameClient` autoload + ENet connect + send input loop.
  - P15 client snapshot consumption + render binding.
  - P16 server `WeaponSystem` + damage application.
- **Manual review:**
  - Cheat test: comment out client-side movement, server still moves the player based on inputs. The render lags one tick but moves correctly.
  - Cheat test 2: modify client to send fake "I killed you" message (if such a message exists) and verify server ignores it. If you have to add such a message to test, that is a design red flag.
  - Look at the protocol bytes-on-wire with a network sniffer (Wireshark) for one tick. Confirm no plaintext garbage.

### M5: TDM + match flow (2 weeks)

- **Goal:** Match has a beginning, middle, and end.
- **Deliverables:** Two factions (cosmetic), team assignment, scoring, 5-min timer, instant respawn, friendly-fire off, scoreboard, end-of-match scoreboard screen, restart flow.
- **Code systems:** `MatchState` (warmup, playing, postgame), team allocation, score accumulator, FF filtering in `DamageSystem`.
- **Assets:** Final player sprites (both faction tints), real tileset, real arena, gun/grenade/explosion SFX, hit/death SFX, final HUD elements.
- **Validation:** Run a full match start to finish solo against bots (add dummy bots if needed; could be a 'cpu fills empty slot' placeholder). Manual checklist.
- **Acceptance:** A complete match flows. Scoreboard shows team scores. Match ends. Server transitions cleanly to next match.
- **Risks:** Match state machine grows ad hoc. Define it as an enum in Shared with explicit transitions.
- **AI passes:** P17 (`MatchState` + transitions, Claude Code), P18 (team assignment + FF filter, Claude Code), P19 (HUD scoreboard, Codex), P20 (assets wiring, Codex), P21 (postgame screen, Codex).
- **Manual review:** Friendly fire is genuinely off (verified in damage tests). Scoreboard sums correctly with 10 simulated kills.

### M6: Nakama integration (2 weeks)

- **Goal:** Players authenticate via Nakama, get matched, get a ticket, connect to the game server.
- **Deliverables:** `NakamaService` autoload (uses `nakama-dotnet`), device-ID auth, lobby UI, matchmaker call, RPC `get_game_server_ticket` returns server endpoint + signed ticket, server `TicketValidator` validates, server accepts only ticketed connections.
- **Code systems:** Client `NakamaService`. One TypeScript module in `server/nakama/modules/` implementing the RPC. Shared `Ticket` (sign/validate, HMAC-SHA256, shared secret).
- **Assets:** Lobby UI mockup, faction select if you want (cosmetic-only is fine).
- **Validation:** Unit tests for ticket signing/validation. Manual: full flow from clean app launch to in-match.
- **Acceptance:** Two clients can match and end up in the same game-server match. Server rejects connections without a valid ticket.
- **Risks:**
  - Shared secret leakage. Server pulls from env var, never committed. Same secret on Nakama side via Nakama config or env.
  - Forgetting `nakama-dotnet` desktop-only caveat (irrelevant here, but document it).
- **AI passes:** P22 (`Ticket` in Shared + tests, Claude Code), P23 (Nakama TS RPC module, Claude Code), P24 (`TicketValidator` on server, Claude Code), P25 (`NakamaService` autoload + lobby UI, Codex).
- **Manual review:** Confirm secret is not in git history. Test ticket expiry. Test replay-attack rejection (same ticket twice should fail; track used jti or single-use nonce).

### M7: Vertical slice (3 weeks)

- **Goal:** 10-player TDM that feels good on LAN.
- **Deliverables:** Snapshot interpolation on client, kill feed, minimap optional, audio mixer, music wiring, polished HUD.
- **Code systems:** Client `InterpolationBuffer` (render at serverTime - 100 ms), interpolation tuning, audio bus structure.
- **Assets:** All M5 assets polished, all M6 UI polished, music tracks, full SFX library.
- **Validation:** 10-player LAN test session. Latency overlay shows reasonable numbers.
- **Acceptance:** 10 friends on a LAN can play a full match without disconnects. Hit registration feels fair. Audio cues are legible.
- **Risks:** Interpolation introduces input feel lag. Mitigated by keeping your own player view at zero buffer (local-input prediction for movement only, M11 territory if you want it earlier). For M7, accept the 100 ms input-to-display lag; it is fine on LAN.
- **AI passes:** P26 (`InterpolationBuffer`, Claude Code), P27 (audio bus + music, Codex), P28 (kill feed, Codex), P29 (HUD polish, Codex), P30 (LAN session smoke fixes, Antigravity for review-then-fix).
- **Manual review:** Real friend in same room playtest. Note exactly what feels bad and prioritize.

### M8: Content production + provenance (2 weeks)

- **Goal:** Replace remaining placeholder assets, enforce provenance discipline.
- **Deliverables:** Final art for everything, provenance manifest covers every committed asset, style guide locked in `docs/assets.md`.
- **AI passes:** Mostly manual creative work plus tooling passes. P31 (provenance lint script, Codex), P32 (asset import script, Codex), P33 (style guide draft from existing decisions, Antigravity).
- **Manual review:** Run the provenance lint. Should report 100 percent coverage.

### M9: Exports + CI (1.5 weeks)

- **Goal:** Push-button builds, push-on-merge CI.
- **Deliverables:** Five export presets (mac arm64 client, linux x64 client, linux arm64 client, windows x64 client, linux arm64 headless server). GitHub Actions: build, test, optional matrix export.
- **AI passes:** P34 (export-mac.zsh + verify, Claude Code), P35 (linux x64 + arm64 client exports, Codex), P36 (windows export, Codex), P37 (headless server export, Claude Code), P38 (`.github/workflows/build.yml`, Claude Code), P39 (release workflow with tags, Codex).
- **Manual review:** Read every line of the YAML before merging. CI runs in under 10 minutes.

### M10: Private testing (2 weeks)

- **Goal:** Closed playtest on a real VPS.
- **Deliverables:** Hetzner ARM (or AWS Graviton) Nakama + headless server deployment, deploy script in `scripts/deploy/`, runbook in `docs/runbook.md`, log collection.
- **AI passes:** P40 to P45 deployment glue. Mix of Claude Code (deploy scripts) and Codex (docs, monitoring).
- **Manual review:** You deploy by hand once before scripting it. Document the exact commands.

### M11: Public-test readiness (open)

- **Goal:** Stable enough that strangers can play.
- **Deliverables:** Client prediction + reconciliation, lag compensation for hitscan, rate limits, anti-cheat hardening, encrypted transport, telemetry, support runbook.
- **AI passes:** P46+. Heavy Claude Code for prediction/reconciliation and lag comp (high-risk code). Codex/Antigravity for telemetry and docs.
- **Manual review:** Prediction is the part of the project most likely to ship subtly broken. Plan a dedicated week of just prediction testing with at least three latency-injection scenarios (50 ms, 100 ms, 200 ms one-way).

---

## 9. First 10 implementation passes

These come after the planning conversation we are in now. P01 is the adoption bootstrap.

| # | Name | Purpose | Likely files | Agent does | You verify | Agent |
|---|---|---|---|---|---|---|
| P01 | Adoption bootstrap | Stand up AI workflow files | `AGENT.md`, `metadata-schema.json`, `scripts/ai-post-pass.zsh`, `scripts/ai-tool-preflight.zsh`, `.gitignore` update, `global.json`, `docs/{architecture,networking,ai-workflow}.md` skeletons | Per workflow guide §24 bootstrap; do not touch gameplay | All scripts run, `.gitignore` updated, `global.json` pins .NET 10 SDK, no Godot project changes | Claude Code |
| P02 | Project skeleton | Solution + three csproj + first unit test | `vantage.sln`, `client/Vantage.Client.csproj`, `client/project.godot` (empty), `server/headless/Vantage.Server.csproj`, `server/headless/project.godot` (empty), `shared/Vantage.Shared/Vantage.Shared.csproj`, `tests/Vantage.Shared.Tests/*` | Create empty buildable solution, one trivial test | `dotnet build` clean, `dotnet test` passes, Godot opens client without errors | Claude Code |
| P03 | Input + player movement (single-player) | Local control of a placeholder player | `client/autoloads/InputBus.cs`, `client/entities/PlayerView.tscn` + script, `client/scenes/Boot.tscn`, `client/scenes/arenas/Arena01.tscn` | Add `InputBus` autoload, build PlayerView with `_PhysicsProcess` movement at 60 Hz, test arena scene | Manual: WASD moves player, mouse aims, 60 FPS solid | Claude Code |
| P04 | Test tilemap with collisions | Validate `TileMapLayer` and physics | `client/assets/tiles/test_tileset.png`, `client/scenes/arenas/Arena01.tscn` update, `client/resources/test_tileset.tres` | Build a small `TileMapLayer` with two tile types (walkable, wall), apply collision | Manual: player collides with walls, walks on floor | Codex |
| P05 | Camera follow + aim line | Visual polish for prototype | `client/entities/PlayerView.tscn`, `client/scenes/arenas/Arena01.tscn`, possibly `client/autoloads/CameraRig.cs` | Smoothed camera following player, debug aim line from player to mouse | Manual: camera does not jitter, aim line correct under cursor | Codex |
| P06 | WeaponDef + DamageMath in Shared | Move weapon stats into Shared library | `shared/Vantage.Shared/Game/WeaponDef.cs`, `shared/Vantage.Shared/Game/DamageMath.cs`, `tests/Vantage.Shared.Tests/DamageMathTests.cs`, `tests/Vantage.Shared.Tests/WeaponDefTests.cs` | Define `WeaponId`, `WeaponDef` (range, dmg, fire rate, magazine), pure `DamageMath` functions, unit tests | `dotnet test` passes, no Godot deps in Shared | Claude Code |
| P07 | Single-player hitscan + projectile firing | Wire weapons in client-only world | `client/entities/PlayerView.tscn`, `client/match/LocalWorld.cs` (a temporary single-player world), `client/entities/BulletView.tscn` | Implement firing using `WeaponDef` from Shared, hitscan via `PhysicsRayQueryParameters2D`, projectile gun via `RigidBody2D` or kinematic | Manual: each weapon fires at correct rate, hitscan kills dummy, projectile travels and impacts | Claude Code |
| P08 | Grenade in single-player | Grenades + explosion | `client/entities/GrenadeView.tscn`, `client/match/LocalWorld.cs` update, `shared/Vantage.Shared/Game/GrenadeDef.cs` | Grenade arc (parabolic or physics), 2 s fuse, radius damage using `DamageMath` | Manual: grenade arcs realistically, explodes on time, damages within radius | Codex |
| P09 | HUD: health, ammo, weapon icon | Player feedback | `client/ui/Hud.tscn`, `client/ui/Hud.cs` | Wire HUD to current player state from `LocalWorld` (still single-player), placeholder graphics | Manual: HUD updates as you fire/take damage | Codex |
| P10 | Damage and weapon-system tests | Lock in damage math correctness | `tests/Vantage.Shared.Tests/DamageMathTests.cs` (expand), `tests/Vantage.Shared.Tests/WeaponDefTests.cs` (expand), CI workflow file `.github/workflows/build.yml` | Add coverage for edge cases (zero distance, max range, headshot multiplier if any, friendly fire boolean), add basic GH Actions YAML running `dotnet test` | `dotnet test` passes, CI runs green on push | Antigravity |

Why these ten:
- P01 to P02 are workflow plumbing. No gameplay risk.
- P03 to P05 prove the Godot project works and you can move a thing around.
- P06 introduces the Shared library before any networking. This is the single most important early decision: weapon stats live in Shared from day one.
- P07 to P09 give you single-player combat against a dummy, with stats from Shared. This is the M3 boundary.
- P10 cements correctness before networking. The temptation will be to skip it. Don't.

After P10, the next high-stakes pass is **P11: protocol DTOs in Shared** (`InputCommand`, `StateSnapshot`, `Events`, MemoryPack annotations). This is the gateway to M4. Treat it like P06: Claude Code only, careful design, exhaustive tests.

---

## 10. Bootstrap strategy

### 10.1 Classification

This is an **adoption bootstrap**, not an initial bootstrap. Your repo exists with one commit. P01's job is to add the AI workflow scaffolding, not to create the repo or the Godot project.

### 10.2 What P01 creates

1. `AGENT.md`: the contract. Project identity, naming rules, off-limits files, build verification step, current architecture state (mostly "empty" at P01), how to read prior pass artifacts.
2. `metadata-schema.json`: schema per workflow guide v6.2.
3. `scripts/ai-post-pass.zsh`: per workflow guide §24.
4. `scripts/ai-tool-preflight.zsh`: per workflow guide §24.
5. `global.json`: pins .NET SDK to current 10.x with `latestFeature` roll-forward.
6. `.gitignore`: adds `server/nakama/data/`, `build/`, `dist/`, `*.pck`, `.idea/`, `*.user`, `.DS_Store` (already), Godot caches (already).
7. `docs/architecture.md`: skeleton with sections that match this plan's §3.
8. `docs/networking.md`: skeleton with sections that match §3.5–§3.7.
9. `docs/ai-workflow.md`: how this project uses the workflow guide, which agents do what (mirrors §4).
10. `docs/adr/0001-godot-net.md`: first ADR recording the Godot .NET + Path B decision.
11. `build-report-logs/.gitkeep`: directory placeholder; `build-report-logs/` itself stays ignored.
12. `prompts/.gitkeep`, `launchers/.gitkeep`: same pattern.

### 10.3 What P01 does NOT create

- No Godot projects (`client/` and `server/headless/` stay empty until P02).
- No C# code.
- No solution file.
- No CI workflow.
- No Nakama modules.
- No assets.

The cardinal rule from the workflow guide: do not combine bootstrap with implementation. P01 is workflow infrastructure only.

### 10.4 What gets committed vs local-only

| Path | Committed? | Notes |
|---|---|---|
| `AGENT.md` | Yes | The contract. |
| `metadata-schema.json` | Yes | Schema reference. |
| `scripts/ai-post-pass.zsh` | Yes | Reproducible. |
| `scripts/ai-tool-preflight.zsh` | Yes | Reproducible. |
| `global.json` | Yes | Pins SDK. |
| `.gitignore` | Yes | |
| `docs/**` | Yes | |
| `prompts/<pass-id>/` | Yes for public-safe content | Per workflow guide, prompts are saved per pass. Commit them so any agent can see prior context. |
| `launchers/<pass-id>/` | Yes | Same. |
| `build-report-logs/**` | **No** (private repo: optional; public repo: required no per workflow guide). | Per workflow guide. Since your repo is private, you can choose. I recommend **gitignored** anyway: report logs can contain absolute paths and machine info you do not want in history. |
| `server/nakama/data/**` | **No** | DB binary files. |

For a **private** repo specifically: you can be looser on `build-report-logs/`, but the workflow guide treats them as workflow-internal. Stay aligned with the guide.

### 10.5 P01 prompt characteristics

When you ask your prompt agent (a future Claude conversation) to generate P01:

- Pass objective: "P01 adoption bootstrap per workflow guide v6.2 §24."
- The bootstrap Save Prompt block embeds the full bootstrap script from workflow guide §24 (replacing `<<AI_ASSISTANT: ...>>` placeholders).
- The launcher follows workflow guide §15c (bootstrap variant).
- The pass touches nothing in `client/` or `server/headless/` beyond ensuring the directories exist with `.gitkeep`.

### 10.6 Initial commit reconciliation

Your repo already has one commit. The bootstrap will produce a second commit. Use a meaningful commit message:
```
P01: adopt AI workflow v6.2
```
Branch model from M0 onward: feature branches per pass, PR to `main`, squash merge, tag with the pass id. Even working solo, this gives you cherry-pick recovery and a clean history. Without PRs, you can still tag commits with the pass id.

---

## 11. Next exact steps

After you read this plan, do these five things in order, before you start any AI passes.

1. **Decide on the three things I flagged as uncertain in §12.** Specifically the serialization library (MemoryPack vs MessagePack), the audio toolchain final pick (Gemini for SFX is decided, but mastering tool needs a pick: I'd say Audacity for free), and the font (m5x7 is the easy answer).

2. **Fix `.gitignore` manually right now** (before any AI pass). One-line update to add `server/nakama/data/`. Commit as a small "chore: gitignore nakama data dir" commit. Do not let the first AI pass run with the data dir uningored, because the moment you `docker compose up` it will fill with binary files.

3. **Create `global.json` manually right now.** Pin .NET SDK. A six-line file. Commit as "chore: pin .NET 10 SDK." This means agents are not guessing at SDK versions.

4. **Start a fresh conversation with your workflow prompt agent** (the project instructions you uploaded) for P01. The pass objective: "Adoption bootstrap per workflow guide v6.2 §24. Repo already has one initial-scaffold commit and a chore `.gitignore` commit. Do not touch `server/nakama/docker-compose.yml`. Do not create any Godot projects."

5. **Run P01 through the workflow.** Save Prompt, Save Launcher, Run Launcher. Review the report. Commit. Then start the P02 conversation.

Do not start P02 until P01 is committed and you have read `AGENT.md` end to end. The contract is only as good as your understanding of it.

---

## 12. Assumptions, uncertainty, and verification notes

Things I assumed and where they could be wrong:

- **MemoryPack over MessagePack-CSharp.** I assumed MemoryPack because it is faster, zero-alloc, and source-generated. MessagePack-CSharp is more mature on .NET and has better cross-platform tooling. Either works. If you want to ever interop with a non-.NET service, MessagePack is the safer pick.
- **`nakama-dotnet` on Godot mobile/web is not in scope.** Confirmed by Heroic Labs docs: works on desktop, not HTML5. You said no HTML5 target. If that changes, the answer changes too; you would then use the GDScript Nakama client + a thin C#/GDScript bridge, or wait for an official .NET client to support web.
- **ENet vs WebSocket transport.** I assumed ENet because Godot's `MultiplayerAPI` defaults to ENet, it has built-in DTLS for later, and UDP is right for a shooter. If your VPS has UDP restrictions, WebSocket fallback is real work to add. Hetzner does not restrict UDP for normal customers.
- **One server process per match (not per arena).** Fine at MVP scale (10 friends, one match). For commercial scale you would want a process-per-match model with a fleet manager. That is M11+.
- **Heroic Cloud is not the host.** I assumed self-hosted Hetzner ARM / AWS Graviton based on your answers. If you switch to Heroic Cloud later, Nakama deploy gets easier; the Godot headless server still needs separate hosting.
- **Snapshot interpolation at 100 ms buffer.** Industry standard, but the exact number depends on your tick rate and content. Tune in M7.
- **Anti-cheat scope.** I assumed indie scope through M11. If the project ever moves toward competitive ladder play, the anti-cheat plan needs a full rethink.
- **License decision deferred.** You did not specify a code license. For a project that may go commercial, "All Rights Reserved" by omission is the default in most jurisdictions; consider adding an explicit `LICENSE` file when the commercial decision is made. The `assets-source/provenance/manifest.json` handles asset licensing per-asset.
- **`global.json` pinning to .NET 10.** Verified via Godot docs: 4.7 requires .NET 8 minimum, supports newer. .NET 10 is the current LTS (released Nov 2025). The known issue from .NET 9 RC days (issue #98334 in godot/godot) was specific to RC pre-releases. .NET 10 stable should be fine. If you hit roll-forward issues, fall back to `<TargetFramework>net8.0</TargetFramework>` and use the .NET 10 SDK to build it.
- **`server/nakama/data/postgres/` is the only Nakama-generated path.** True for the docker-compose you wrote. If you later add a CockroachDB store-locally option or a Nakama config file with its own data dirs, update `.gitignore`.
- **Workflow document version.** I read v6.2 of both your uploaded guides. If a v6.3 changes the bootstrap or maintenance protocol, this plan's §10 needs a revision.

Things I did **not** verify and would want to confirm at the time of running:
- Whether Godot.NET.Sdk 4.7.0 has any specific incompatibility with .NET 10 SDK feature-band 301. Check the Godot 4.7 release notes and a fresh `dotnet build` of an empty Godot.NET project before P02.
- The exact format of `registry.heroiclabs.com` Nakama image arm64 manifest. The Docker image you pulled may have a multi-arch manifest; `docker image inspect heroiclabs/nakama:3.22.0` on your machine will confirm arm64 is selected.
- Whether your installed `gh` CLI is current. Not critical, but a stale `gh` has fewer ergonomics.

If any of these turn out wrong in practice, the architecture and milestone plan do not change. The specific commands and version pins in the relevant pass change.

---