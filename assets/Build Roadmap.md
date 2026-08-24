# Finalized Build Roadmap
This document is mostly for planning purposes. Nothing is set in stone until implimented.

## Standing guardrails (apply to every CLI pass below unless noted)

- No edits to `.github/workflows/**`, `server/nakama/data/**`, `.env*`, `*secret*`, `*token*`, `*credential*`, `.git/**`, `AGENTS.md`/`CLAUDE.md`, or anything under `docs/` without a specific human go-ahead in that conversation.
- `Vantage.Shared` must not reference Godot assemblies or packages.
- No new NuGet packages, no target-framework changes, beyond what a pass explicitly authorizes (MemoryPack is the one pre-authorized exception, scoped to P18).
- One logical deliverable per pass. No mixing gameplay code with workflow/doc/CI changes.
- Plan-then-execute: for anything touching `Vantage.Shared` or `server/headless/`, require a stated plan/diff summary before file writes.

Standard verification block (adapt per pass):
```
dotnet build vantage.sln --configuration Debug --no-restore
dotnet test tests/Vantage.Shared.Tests/Vantage.Shared.Tests.csproj --configuration Debug --no-build
godot-mono --headless --path client --build-solutions --quit   # only if client .csproj/scene wiring changed
godot-mono --headless --path client --quit
```
Hard stop: if build or tests fail, resolve within scope before declaring the pass complete.

## Roadmap overview

```
GATE G0  -> Confirm P10 manual verification (Josh, no code)
PHASE 1  -> Close M3          : P13, P14, P15, P16
PHASE 2  -> M4 doc prereq     : P17
PHASE 3  -> M4 networking core: P18, P19, P20, P21, P22, P23, P24
PHASE 4  -> M5-M11 (coarse)
```

---

## Gate G0 — Confirm P10 manual verification

**Route:** Josh direct. No CLI agent.
**Goal:** Close the one open verification item from the P10 report before new combat code lands on top of unverified behavior.
**Milestones:** Run the P10 manual checklist in the Godot 4.7 editor (hitscan cadence, dummy damage/defeat, Shared-derived damage/range parity, no underflow, no networking).
**Exit criteria:** Checklist fully confirmed. If any item fails, route a scoped CLI fix pass before P13; do not proceed with new features on top of unverified P10 behavior.

---

## P13 — Local Projectile Weapon & Weapon Switching

**Route:** CLI (Codex). **Read-first:** `PlayerCombatController.cs`, `WeaponDef.cs`, `WeaponKind.cs`, `WeaponId.cs`, `DamageMath.cs`, `MovementSandbox.tscn`, `DevTargetDummy.cs`, `client/project.godot`.
**Goal & scope:** Add a second weapon (`WeaponDef.DevProjectile`) and the means to select it. Combined into one pass because switching is untestable without a second weapon, and a second weapon is untestable without a way to select it without throwaway hardcoding — treating them separately would create disposable work in between.
**Key milestones:**
1. `client/entities/combat/ProjectileView.cs` + `.tscn`: travels along aim direction at `WeaponDef.DevProjectile.ProjectileSpeed`, collides with walls/obstacles (despawn) and dummies (damage via `DamageMath`), expires on lifetime.
2. Weapon-select input action(s) in `project.godot`; active-weapon state in `PlayerCombatController.cs` driving fire kind, cadence, and damage from the selected `WeaponDef`.
3. Debug indicator of the currently active weapon (text/log is sufficient; no HUD dependency yet).
**Exit criteria:** Standard verification block green. Manual: switching toggles behavior correctly; hitscan still works unchanged; projectile fires, travels, collides, damages, and expires; damage/range/cadence values trace to `WeaponDef.DevProjectile`. No `Vantage.Shared` edits beyond consumption. No networking, no player life-state, no HUD.

## P14 — Local Grenade System & Radius Damage

**Route:** CLI (Codex). **Read-first:** `GrenadeDef.cs`, `DamageMath.cs` (radius path), `PlayerCombatController.cs` (post-P13), `CombatMathTests.cs`.
**Goal & scope:** Thrown grenade consuming `GrenadeDef.DevPlaceholder`: throw, fuse, explosion, radius falloff damage against dummies.
**Key milestones:**
1. `throw_grenade` input action; `client/entities/combat/GrenadeView.cs` + `.tscn` (toss physics, fuse timer).
2. Detonation: 2D overlap query within `GrenadeDef.RadiusUnits`, falloff damage via `DamageMath.RadiusDamage`.
3. Debug explosion visual (brief, primitive).
4. Add radius-falloff edge-case tests to `CombatMathTests.cs` if any combination (center/edge/outside) isn't already covered from P09.
**Exit criteria:** Standard verification block green with an increased/confirmed test count. Manual: grenade throws, fuses, explodes, damages within radius with visible falloff, leaves dummies outside radius unharmed, no health underflow.

## P15 — Local Combat HUD (view-only, code only)

**Route:** CLI (Codex). **Read-first:** current client scenes, `player_experience_and_ui_behavior_sot.md` (for intended element list — read only, not edited), combat controller for active-weapon source.
**Goal & scope:** View-only HUD: active-weapon indicator, reticle, static placeholder health bar. No document edits in this pass.
**Key milestones:**
1. HUD scene wired into the sandbox.
2. Weapon indicator bound to active weapon (post-P13 state); reticle tracks cursor.
3. Static placeholder health bar (reads nothing authoritative — life-state is still M4-deferred).
**Exit criteria:** Standard verification block green. Manual: HUD shows active weapon, reticle follows cursor, health bar renders as static placeholder. HUD decides no state (view-only confirmed by code inspection, not just visually).

## P16 — M3 SOT Drafting & M3 Gate Closure

**Route:** Cowork or Antigravity for the document edits (with explicit per-file human go-ahead, since `docs/` is AI-read-only without it), plus a Josh manual gate. **Read-first:** current `client/` combat code (post-P13/P14/P15), Shared `Game/*.cs`, Decision Log.
**Goal & scope:** Close all five items the documentation index requires before M3 is complete, not just two. Brownfield: transcribe from working code, don't invent.
**Key milestones:**
1. `combat_weapons_and_damage_sot.md` drafted from Shared + client behavior (hitscan, projectile, grenade, damage clamping, friendly-fire off), submitted for owner approval.
2. `gameplay_and_player_controls_sot.md` drafted from `PlayerController` (movement, aim, input semantics, 40x64 arena note), submitted for owner approval.
3. `technical_architecture_and_process_model.md` updated to reflect P13-P15 and promoted toward Approved Baseline.
4. `test_strategy_and_acceptance_criteria.md` drafted, reflecting the real test layers that exist.
5. `vantage_core_doc.md` §9 pass table corrected to match the executed ledger (see Part 2).
6. Full M3 manual checklist run in-editor by Josh.
**Exit criteria:** All five M3-gate documents exist per `core_operational_behavior_documents_list.md` §8, with the two SOTs owner-approved; M3 manual checklist passes; core doc pass table no longer collides with the real ledger. M3 declared complete only after this.

---

## P17 — M4 Documentation Prerequisite Gate

**Route:** Cowork or Antigravity (docs), Architecture approval required, per-file human go-ahead. **Read-first:** Decision Log (ENet/MemoryPack ratifications), `technical_architecture_and_process_model.md`, `server_authority_and_state_ownership_sot.md` and `networking_and_protocol_behavior_sot.md` placeholders.
**Goal & scope:** Satisfy the hard prerequisite in `core_operational_behavior_documents_list.md` §8 ("Before beginning authoritative M4 networking work") before any M4 code is written — not retroactively after the code exists.
**Key milestones:**
1. `networking_and_protocol_behavior_sot.md` drafted: ENet transport, server-authoritative model, 60 Hz tick, input/snapshot semantics, the four-rates distinction it already flags.
2. `server_authority_and_state_ownership_sot.md` drafted: ownership table for movement, aim/fire, health, death, score, teams, match state, projectiles, grenades, ticks.
3. `protocol_dto_and_serialization_spec.md` skeleton drafted against the required-sections table it already lists, ready to be filled as P18 lands.
**Exit criteria:** The two SOTs reach at least Pending Approval (ideally Architecture-approved before P18 starts); the spec skeleton exists. No code in this pass.

## P18 — Shared Protocol DTOs, MemoryPack, Tick Math

**Route:** CLI (Claude Code — high-trust path). **Read-first:** P17 outputs, `WeaponDef.cs`/`GrenadeDef.cs` (style precedent), Decision Log's MemoryPack ratification.
**Goal & scope:** Establish the wire contract in `Vantage.Shared`. This pass is the one authorized exception to the no-new-packages guardrail: add `MemoryPack` only.
**Key milestones:**
1. Add `MemoryPack` package reference to `Vantage.Shared.csproj`.
2. `shared/Vantage.Shared/Protocol/`: `InputCommand.cs` (tick, move vector clamped [-1,1], aim angle, button bitmask), `StateSnapshot.cs` (tick, acked client tick, player states, projectile/grenade states), `Events.cs` (hit/damage/defeat events).
3. `shared/Vantage.Shared/Net/TickMath.cs` (tick-to-time conversion, wraparound handling, tick delta).
4. Round-trip serialization tests in `tests/Vantage.Shared.Tests/`.
**Exit criteria:** Standard verification block green; round-trip tests pass with full field fidelity; `Vantage.Shared` remains otherwise Godot-free.

## P19 — Headless Server Simulation Shell & Tick Loop

**Route:** CLI (Claude Code). **Read-first:** P18 DTOs, `vantage_core_doc.md` §3.4/§3.5.
**Goal & scope:** Standalone headless server process with authoritative 60 Hz tick and a world-state ring buffer (built now per the core doc's own advice, even though lag comp is M11).
**Key milestones:** `ServerMain.cs` entry point with `--port` argument; `TickLoop.cs` driving `_PhysicsProcess` at 60 Hz with monotonic tick counter; `World.cs`; ring buffer storing recent tick history.
**Exit criteria:** `godot-mono --headless --path server/headless/ --quit` starts and shuts down cleanly; tick counter increments without drift; ring buffer round-trips a stored state by tick index; no rendering/audio nodes in the server project.

---

## Higher-level phases (coarse — will gain pass-level detail once M4 proves the direction)

**P20 — ENet transport + client connect + input send loop.** `ServerPeer.cs`, `client/autoloads/GameClient.cs`, hardcoded loopback IP, no auth. Exit: client streams 60 Hz `InputCommand` over loopback; server deserializes with zero drops; clean disconnect handling both directions.

**P21 — Authoritative MovementSystem + snapshot broadcast.** Server-side movement from `InputCommand`; `StateSnapshot` broadcast at 30 Hz; client becomes view-only for remote/local player position; input bounds validation server-side. Exit: two client instances see each other move via server snapshots only; disabling client-side movement doesn't stop the rendered player; illegal input deltas are clamped/ignored server-side.

**P22 — Authoritative WeaponSystem + player life-state.** Server-side hitscan/projectile/grenade resolution against the same arena geometry; player health, death, instant respawn (this is where the M3 deferral decision lands). Exit: client cannot declare its own damage or kills; damage/death/respawn only occur via server-broadcast events.

**P23 — Server integration test harness.** `tests/Vantage.Server.IntegrationTests/`; in-process boot, fake clients, deterministic assertions on movement/combat/life-state. Exit: harness green, deterministic across repeat runs.

**P24 — Minimal CI + M4 gate closure.** `.github/workflows/build.yml` (protected path, reviewed line by line, human-authorized); `dotnet build`/`test` on push. Exit: CI green; M4 complete — 1v1 loopback with authoritative movement/combat/life-state, MemoryPack protocol, ring buffer, integration harness, and CI all green.

**M5 — TDM & match flow.** Doc prereq first (`match_teams_scoring_and_life_state_sot.md`, `player_experience_and_ui_behavior_sot.md`). Then match state machine, factions, scoring, timer, FF-off filter, scoreboard, postgame/restart. Exit: a full match runs start to finish and resets cleanly.

**M6 — Nakama integration.** Doc prereq first (`identity_matchmaking_and_game_server_admission_sot.md`, `security_and_trust_boundaries_sot.md`, `nakama_integration_spec.md`). Device auth, matchmaker, ticket RPC, server-side ticket validation. Exit: two clients match into one session; unticketed connections rejected; replay rejected.

**M7 — Vertical slice.** Snapshot interpolation, kill feed, audio/music, HUD polish, 10-player LAN. Exit: 10 players complete a match on LAN without disconnects.

**M8-M11 (coarse).** Content/provenance; cross-platform exports + CI matrix; Hetzner private testing with runbook and security review gate; public-readiness hardening (prediction, reconciliation, lag comp, rate limits, DTLS, telemetry).

---