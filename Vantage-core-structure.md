> From: https://chatgpt.com/g/g-p-6a42318042cc8191a643dde9b79d8658/c/6a765a1a-9efc-83ea-a189-e84d47d717e9

# you asked

message time: 2026-08-07 17:20:51

I want to create a core_operational_behavior_documents_list.md structure for this project. Here is an example. Obviously it has to be tailored to the specific project, 'vantage'.

---

# chatgpt response

I’ll use the example’s role—master index, hierarchy, per-document boundaries—but reshape it around Vantage’s actual architecture, gameplay milestones, server authority, networking, testing, assets, and operations.

The important difference from the fulfillment-app example is that Vantage needs its highest-authority documents split by **game rules, simulation authority, protocol/network behavior, and match lifecycle**, not by business workflow. The existing `architecture.md`, `networking.md`, decision log, ADR, and core document already give us the backbone.

Yes. The SpicedAnime file is a good structural model because it separates the **master index** from the actual specifications, defines precedence, and gives every document explicit boundaries. 

For Vantage, I would make one major adjustment: the hierarchy needs to distinguish **gameplay rules**, **server authority**, **network/protocol behavior**, **implementation specifications**, and **workflow controls**. Vantage already establishes the Godot client/headless-server/Nakama architecture and shared-library boundary, while the decision log records later corrections such as deferring player life-state to the authoritative server.   The existing M2–M4 plan also shows why combat, protocol, authoritative simulation, and verification need separate specifications instead of one giant architecture document. 

I would start with this as **v0.1 of the master structure**:

**Document Title: **

---
title: "Vantage: Core Operational Behavior Documents List"
version: "0.1"
status: "Draft Structure"
last_verified:
project: "vantage"
game_architect: "dweebz"
author: "Josh"
primary_systems:
  - Godot .NET Desktop Client
  - Godot .NET Headless Authoritative Server
  - Vantage.Shared
  - Nakama
  - PostgreSQL
  - ENet Gameplay Transport
  - MemoryPack Protocol Serialization
core_runtime_domains:
  - Player Input
  - Movement
  - Combat
  - Damage
  - Player Life State
  - Match State
  - Team State
  - Scoring
  - Respawn
  - Real-Time Networking
  - Authentication
  - Matchmaking
  - Game-Server Admission
---

# Vantage: Core Operational Behavior Documents List

This document is the master index for Vantage's core operational and behavioral documentation.

It defines which documents establish authoritative game behavior, architecture, networking rules, runtime ownership, testing expectations, content rules, and operational procedures. It also defines the precedence between those documents and the boundaries of each document.

This document does **not** contain the complete specifications themselves. Each referenced document is a separate deliverable and source of authority for its assigned domain.

The objective is to ensure that implementation work does not invent behavior, place authority in the wrong process, duplicate protocol contracts, or rely on obsolete milestone-planning material.

---

# 1. Documentation Tree

Target documentation structure:

```text
vantage/
├── vantage_core_doc.md
├── decision_log_and_open_questions.md
│
├── docs/
│   ├── core_operational_behavior_documents_list.md
│   │
│   ├── architecture.md
│   ├── networking.md
│   ├── gameplay.md
│   ├── combat.md
│   ├── match_lifecycle.md
│   ├── protocol.md
│   ├── nakama.md
│   ├── security.md
│   ├── assets.md
│   ├── testing.md
│   ├── operations.md
│   ├── ai-workflow.md
│   │
│   └── adr/
│       ├── 0001-godot-net.md
│       └── ...
│
├── tests/
│   ├── Vantage.Shared.Tests/
│   ├── Vantage.Server.IntegrationTests/
│   └── manual/
│       ├── checklist-m2.md
│       ├── checklist-m3.md
│       ├── checklist-m4.md
│       └── ...
│
└── assets-source/
    └── provenance/
        └── manifest.json
```

This is the target logical structure. Existing authoritative files should not be relocated solely to satisfy this tree unless a dedicated documentation-maintenance pass explicitly authorizes the move.

---

# 2. Document Authority Classes

| Authority Class | Purpose |
| :--- | :--- |
| **Decision Control** | Records explicit human-approved decisions and unresolved questions. Overrides older planning material when a later decision changes it. |
| **Behavioral Source of Truth** | Defines observable gameplay and match behavior. Implementation must conform exactly. |
| **Technical Source of Truth** | Defines architecture, authority boundaries, networking invariants, protocol behavior, and system ownership. |
| **Architecture Decision Record** | Records an accepted architectural decision, its context, and consequences. |
| **Implementation Spec** | Defines how an approved behavioral or technical rule is represented in code or data. It must not independently invent behavior. |
| **Test Spec** | Defines how approved behavior and technical invariants are verified. |
| **Operations Runbook** | Defines how systems are launched, configured, observed, deployed, and recovered. It does not define gameplay behavior. |
| **Planning Reference** | Describes roadmap, milestones, sequencing, risks, and historical planning. It is not automatically runtime authority. |
| **Workflow Control** | Governs AI-assisted development and repository workflow. It does not define game behavior. |

---

# 3. Source-of-Truth Hierarchy

When documents overlap, use the following precedence.

1. **Approved Decision Log**
2. **Accepted Architecture Decision Records**
3. **Behavioral and Technical Source-of-Truth documents**
4. **Implementation specifications**
5. **Test specifications and acceptance criteria**
6. **Operations documentation**
7. **Planning references and milestone plans**

Workflow-control documents such as `AGENTS.md`, `docs/ai-workflow.md`, the workflow guide, and metadata schema govern **how development work is performed**, not what the game does at runtime.

## Conflict Rules

If an Implementation Spec conflicts with a Behavioral or Technical SOT, the SOT wins.

If two SOT documents conflict, implementation stops until the conflict is resolved through `decision_log_and_open_questions.md`.

If a later approved decision changes behavior described in `vantage_core_doc.md`, the approved decision wins and the affected domain document must be updated.

If an accepted ADR conflicts with an older architectural recommendation in `vantage_core_doc.md`, the ADR wins.

Tests must never become an independent source of gameplay behavior. A failing test caused by an intentional approved rule change must be updated to match the governing specification.

---

# 4. Approval Status Key

| Status | Meaning |
| :--- | :--- |
| **Owner approval required** | Defines player-visible gameplay, game rules, balance semantics, or a product-level behavioral decision. Implementation must not treat an unresolved draft as authoritative. |
| **Architecture approval required** | Defines process boundaries, network authority, protocol contracts, security boundaries, persistence ownership, or another difficult-to-reverse architectural decision. |
| **Technical approval required** | Defines implementation behavior derived from already-approved architecture or gameplay rules. |
| **Owner approval not required** | Operational, testing, diagnostic, or workflow documentation that does not independently change game or architecture behavior. |

Suggested document lifecycle states:

| State | Meaning |
| :--- | :--- |
| `Draft` | Structure or content is incomplete. |
| `Pending Approval` | Content is complete enough for its required reviewer. |
| `Approved Baseline` | Authoritative for implementation. |
| `Changes Requested` | Review identified required changes. |
| `Superseded` | Replaced by a newer authoritative document or decision. |

---

# 5. Core Project-Control Documents

## `decision_log_and_open_questions.md`

**Approval Status:** Owner approval required

**Source Type:** Decision Control

### Primary Purpose

Provides the canonical record of explicit project decisions and unresolved questions.

This document resolves ambiguities without forcing domain specifications to preserve the history of every prior alternative.

### Must Cover

- Approved gameplay decisions.
- Approved architecture decisions.
- Approved networking decisions.
- Scope changes affecting milestone behavior.
- Deferred behavior.
- Open questions that block implementation.
- Documents and implementation areas affected by each decision.

### Explicitly Excludes

- Full gameplay specifications.
- Full architecture specifications.
- Protocol field definitions.
- Detailed implementation instructions.
- Pass-by-pass execution history.

### Required Sections

| Section | Required Content |
| :--- | :--- |
| Approved Decisions | Every active approved decision with owner, context, resolution, and affected domains. |
| Open Questions | Unresolved questions and what they block. |
| Decision History | Resolved or superseded questions and decisions. |

---

## `vantage_core_doc.md`

**Approval Status:** Planning reference

**Source Type:** Foundational Planning Reference

### Primary Purpose

Preserves the original high-level architecture, milestone roadmap, development strategy, asset strategy, testing strategy, and project direction.

### Authority Boundary

This file is **not** the final source of truth for individual runtime behaviors once a dedicated domain SOT exists.

Later approved decisions, ADRs, and domain SOTs override conflicting material in this document.

### Must Cover

- Project vision.
- High-level architecture.
- Milestone roadmap.
- Broad development sequencing.
- Asset strategy.
- Testing strategy.
- Deployment direction.
- Long-term networking evolution.

### Explicitly Excludes

As authoritative definitions:

- Exact protocol contracts.
- Exact combat rules.
- Match state-machine definitions.
- Exact player life-state behavior.
- Exact authentication/ticket behavior.
- Final security policy.

Those belong in dedicated documents.

---

# 6. Core Behavioral Sources of Truth

## `docs/gameplay.md`

**Approval Status:** Owner approval required

**Source Type:** Behavioral Source of Truth

### Primary Purpose

Defines the canonical gameplay model for Vantage independent of client/server implementation details.

### Must Cover

- Game mode objectives.
- Player capabilities.
- Movement semantics.
- Aiming semantics.
- Weapon interaction rules.
- Grenade interaction rules.
- Team/faction semantics.
- Friendly-fire rule.
- Player health model.
- Death behavior.
- Respawn behavior.
- Win/loss conditions.
- Player-visible rules shared across game modes.

### Must Consider

Rules must remain valid regardless of whether the game is currently running locally or through the authoritative networked architecture.

### Explicitly Excludes

- ENet implementation.
- Serialization.
- Snapshot formats.
- Nakama authentication.
- Godot scene hierarchy.
- Asset file formats.
- Exact network timing.

### Required Sections

| Section | Required Content |
| :--- | :--- |
| Game Overview | Short description of the player experience. |
| Player Controls | Canonical input semantics. |
| Movement Rules | Allowed player movement and collision behavior. |
| Combat Overview | High-level combat interaction rules. |
| Teams and Factions | Gameplay meaning of faction membership. |
| Health and Damage | Player-visible health semantics. |
| Death and Respawn | Canonical life-cycle behavior. |
| Game-Mode Rules | Rules shared by or specific to supported modes. |
| Deferred Gameplay | Explicit features not currently implemented. |

---

## `docs/combat.md`

**Approval Status:** Owner approval required

**Source Type:** Behavioral Source of Truth

### Primary Purpose

Defines the complete combat behavior consumed by both local prototypes and the authoritative server implementation.

### Must Cover

- Weapon roster.
- Weapon categories.
- Hitscan rules.
- Projectile rules.
- Grenade rules.
- Damage semantics.
- Damage falloff.
- Fire cadence.
- Range.
- Magazine/ammunition behavior when introduced.
- Friendly-fire behavior.
- Damage ownership.
- Kill attribution.
- Combat-event semantics.

### Must Consider

Shared weapon definitions and pure damage calculations belong in `Vantage.Shared`.

The server ultimately owns authoritative combat outcomes.

### Explicitly Excludes

- Client rendering effects.
- Audio asset selection.
- ENet packet construction.
- Match scoring behavior beyond kill-event output.
- Unapproved balance values.

### Required Sections

| Section | Required Content |
| :--- | :--- |
| Combat Authority | Which system determines combat outcomes. |
| Weapon Definitions | Canonical roster and gameplay parameters. |
| Hitscan Rules | Raycast and range semantics. |
| Projectile Rules | Travel, collision, and lifetime semantics. |
| Grenade Rules | Throw, fuse, radius, and falloff semantics. |
| Damage Rules | Point/radius damage and clamping behavior. |
| Friendly Fire | Exact allow/deny rule. |
| Death Event | Conditions producing a valid kill/death event. |
| Balance Governance | How values become approved and how changes are recorded. |

---

## `docs/match_lifecycle.md`

**Approval Status:** Owner approval required

**Source Type:** Behavioral Source of Truth

### Primary Purpose

Defines the state machines controlling a complete multiplayer match.

### Must Cover

- Match creation.
- Player admission.
- Team assignment.
- Warmup if applicable.
- Match start.
- Active-play state.
- Match timer.
- Scoring.
- Death.
- Respawn.
- Disconnect handling.
- End-of-match condition.
- Postgame state.
- Restart/reset behavior.

### Required State Machines

At minimum:

- Match lifecycle.
- Player match lifecycle.
- Player life-state lifecycle.
- Team-score lifecycle.

### Explicitly Excludes

- ENet packet fields.
- Nakama matchmaking algorithm.
- UI layout.
- Server process allocation.

---

# 7. Technical Sources of Truth

## `docs/architecture.md`

**Approval Status:** Architecture approval required

**Source Type:** Technical Source of Truth

### Primary Purpose

Defines process boundaries and which system owns each category of state and behavior.

### Must Cover

- Godot .NET desktop client.
- Godot .NET headless authoritative server.
- `Vantage.Shared`.
- Nakama.
- PostgreSQL.
- Process topology.
- Ownership boundaries.
- Dependency direction.
- Shared-library restrictions.
- Runtime authority model.
- Architectural non-goals.

### Required Hard Invariants

- Gameplay authority does not live in the client.
- Gameplay simulation does not live in Nakama.
- The Godot headless server owns authoritative gameplay state.
- `Vantage.Shared` remains free of Godot dependencies.
- Shared protocol contracts and common gameplay math are not independently duplicated by client and server.

### Explicitly Excludes

- Complete packet schemas.
- Complete gameplay rules.
- Exact Nakama RPC API.
- Asset-production procedures.
- AI workflow mechanics.

---

## `docs/networking.md`

**Approval Status:** Architecture approval required

**Source Type:** Technical Source of Truth

### Primary Purpose

Defines authoritative real-time networking behavior.

### Must Cover

- ENet client/server transport.
- Server-authoritative model.
- 60 Hz simulation tick.
- Client input submission.
- Input validation.
- Tick numbering.
- Stale/out-of-order input handling.
- State snapshot production.
- Snapshot broadcast rate.
- Client snapshot consumption.
- Connection lifecycle.
- Disconnect behavior.
- Evolution path for interpolation, prediction, reconciliation, and lag compensation.

### Must Consider

The document should clearly distinguish:

1. simulation rate;
2. network input rate;
3. snapshot rate;
4. render rate.

### Explicitly Excludes

- Nakama matchmaking rules.
- Authentication credential storage.
- Weapon balance.
- UI design.

---

## `docs/protocol.md`

**Approval Status:** Architecture approval required

**Source Type:** Technical Source of Truth / Implementation Contract

### Primary Purpose

Defines the wire-level contract shared between Vantage clients and game servers.

### Must Cover

- Protocol versioning.
- MemoryPack use.
- `InputCommand`.
- `StateSnapshot`.
- Player-state DTOs.
- Projectile-state DTOs.
- Event DTOs.
- Tick representation.
- Button/input flags.
- Serialization rules.
- Message direction.
- Validation requirements.
- Compatibility policy.

### Required Sections

| Section | Required Content |
| :--- | :--- |
| Protocol Principles | Versioning, authority, compatibility. |
| Serialization | MemoryPack rules and restrictions. |
| Client-to-Server Messages | Complete message inventory. |
| Server-to-Client Messages | Complete message inventory. |
| DTO Definitions | Canonical fields and semantics. |
| Tick Semantics | Ordering and wraparound rules. |
| Validation | Malformed, stale, duplicate, and out-of-range messages. |
| Evolution Policy | Rules for protocol-changing passes. |

### Explicitly Excludes

- Godot scene implementation.
- Visual interpolation algorithm.
- Matchmaking.
- Weapon balancing.

---

## `docs/nakama.md`

**Approval Status:** Architecture approval required

**Source Type:** Technical Source of Truth

### Primary Purpose

Defines the boundary between Nakama and the authoritative gameplay server.

### Must Cover

- Device authentication.
- Session ownership.
- Matchmaking responsibility.
- Match result / persistence responsibility when introduced.
- Game-server ticket issuance.
- Ticket format and claims.
- Ticket expiry.
- Ticket validation boundary.
- Replay policy.
- Nakama-to-game-server trust assumptions.
- Server endpoint discovery.

### Required Hard Invariant

Nakama does not execute authoritative real-time gameplay simulation.

### Explicitly Excludes

- ENet simulation protocol.
- Movement.
- Damage calculation.
- Match tick execution.

---

# 8. Security and Trust Specification

## `docs/security.md`

**Approval Status:** Architecture approval required

**Source Type:** Technical Source of Truth

### Primary Purpose

Defines the security and anti-cheat trust model appropriate to the current project stage.

### Must Cover

- Client trust boundary.
- Server authority.
- Input bounds validation.
- Fire-rate validation.
- Movement validation.
- Ticket validation.
- Ticket expiration.
- Replay handling.
- Secret handling.
- Nakama RPC rate limiting.
- Debug-feature restrictions.
- Development versus public-server exposure.
- Later transport encryption requirements.
- Logging restrictions for secrets and tokens.

### Explicitly Excludes

- Credential values.
- Secrets.
- Production environment contents.
- Unapproved third-party anti-cheat systems.

---

# 9. Content and Presentation Specification

## `docs/assets.md`

**Approval Status:** Owner approval required for style; technical approval for pipeline rules

**Source Type:** Behavioral / Implementation Spec

### Primary Purpose

Defines visual, audio, and provenance rules for game content.

### Must Cover

- Visual style.
- Pixel dimensions.
- Color palette.
- Player and faction visual rules.
- Weapon art.
- Arena tiles.
- Effects.
- UI asset rules.
- Audio categories.
- Music categories.
- Placeholder conventions.
- Source versus engine-ready assets.
- Naming rules.
- Provenance requirements.
- Commercial-clearance tracking.

### Required Supporting Artifact

```text
assets-source/provenance/manifest.json
```

### Explicitly Excludes

- Gameplay damage values.
- Network behavior.
- Match state.
- AI workflow rules.

---

# 10. Testing and Acceptance Specification

## `docs/testing.md`

**Approval Status:** Owner approval not required

**Source Type:** Test Spec

### Primary Purpose

Defines how behavioral and architectural requirements are proven.

### Must Cover

- Shared-library unit tests.
- Server integration tests.
- Godot headless smoke tests.
- Manual milestone verification.
- Multiplayer dual-instance tests.
- Authority/cheat tests.
- Network-condition tests when introduced.
- Performance acceptance thresholds when introduced.
- Regression-test expectations.
- Test ownership by milestone.

### Test Layers

| Layer | Purpose |
| :--- | :--- |
| Shared unit tests | Pure combat math, tick math, serialization, ticket logic. |
| Server integration tests | Authoritative simulation behavior. |
| Godot headless checks | Scene/project loading and engine integration. |
| Manual gameplay checks | Input feel, rendering, collision, UI, audio. |
| Multiplayer manual checks | Cross-client visibility and authoritative behavior. |
| Adversarial authority checks | Confirm clients cannot declare position, damage, kills, score, or other server-owned outcomes. |

### Supporting Files

```text
tests/manual/checklist-m2.md
tests/manual/checklist-m3.md
tests/manual/checklist-m4.md
...
```

Manual checklists derive from the governing SOTs. They do not establish new behavior.

---

# 11. Operations Specification

## `docs/operations.md`

**Approval Status:** Owner approval not required

**Source Type:** Operations Runbook

### Primary Purpose

Defines how Vantage is run and diagnosed in development, testing, and later hosted environments.

### Must Cover

- Local development startup.
- Nakama/Postgres startup.
- Headless game-server startup.
- Client startup.
- Port configuration.
- Log locations.
- Debug overlays.
- Shutdown.
- Resetting local development state.
- Build procedures.
- Export procedures when introduced.
- CI behavior.
- VPS/server deployment when introduced.
- Backup/recovery requirements when persistence is introduced.
- Health checks.
- Common failure diagnosis.

### Explicitly Excludes

- Gameplay rules.
- Protocol redesign.
- Secret values.
- Balance decisions.

---

# 12. Architecture Decision Records

## `docs/adr/`

**Approval Status:** Architecture approval required

**Source Type:** Architecture Decision Record

### Primary Purpose

Preserves important technical decisions that should not be silently reversed by implementation passes.

Existing:

```text
docs/adr/0001-godot-net.md
```

Future ADRs should be created for decisions that are:

- expensive to reverse;
- architecture-wide;
- security-sensitive;
- protocol-wide;
- dependency-defining;
- likely to be questioned later.

### Example Future ADR Subjects

- Protocol serialization selection.
- ENet transport selection.
- Snapshot-frequency policy if made architectural.
- Client prediction architecture.
- Server rewind / lag-compensation architecture.
- Game-server allocation model.
- Persistence ownership.
- Production hosting model.

Routine implementation choices do not require ADRs.

---

# 13. Workflow Documentation

## `docs/ai-workflow.md`

**Approval Status:** Owner approval not required

**Source Type:** Workflow Control Reference

### Primary Purpose

Documents project-specific AI development workflow orientation.

### Authority Boundary

This document does not define gameplay, networking, protocol, or runtime behavior.

The current workflow suite and `AGENTS.md` remain authoritative for pass mechanics and agent constraints.

### Must Cover

- Relationship to the active workflow suite.
- Pass artifact locations.
- High-level local workflow orientation.
- References to `AGENTS.md` rather than duplicate standing instructions.

---

# 14. Core Behavior Traceability

Every implementation-relevant rule should be traceable through this chain:

```text
Approved Decision
      ↓
Behavioral or Technical SOT
      ↓
Implementation Contract
      ↓
Code
      ↓
Automated Test and/or Manual Acceptance Test
```

Example:

```text
Decision: gameplay authority lives on headless server
      ↓
docs/architecture.md
      ↓
docs/networking.md + docs/protocol.md
      ↓
server/headless/*
      ↓
server integration test + dual-client authority test
```

Another example:

```text
Decision: player life-state becomes authoritative in M4
      ↓
docs/gameplay.md + docs/match_lifecycle.md
      ↓
server authoritative player-state implementation
      ↓
integration tests + M4 manual checklist
```

---

# 15. Document Dependency Matrix

| Document | Depends On | Primary Consumers |
| :--- | :--- | :--- |
| `decision_log_and_open_questions.md` | Human decisions | All documents |
| `vantage_core_doc.md` | Original project planning | Roadmap and planning |
| `docs/gameplay.md` | Decision Log | Client, server, tests |
| `docs/combat.md` | Gameplay, Decision Log | Shared, server, client views, tests |
| `docs/match_lifecycle.md` | Gameplay, Decision Log | Server, UI, tests |
| `docs/architecture.md` | Decision Log, ADRs | All technical implementation |
| `docs/networking.md` | Architecture, ADRs | Client, server, protocol |
| `docs/protocol.md` | Architecture, Networking | Shared, client, server, tests |
| `docs/nakama.md` | Architecture, Security | Client service layer, Nakama modules, server admission |
| `docs/security.md` | Architecture, Networking, Nakama | Server, services, operations |
| `docs/assets.md` | Gameplay, owner art decisions | Client, asset pipeline |
| `docs/testing.md` | All behavioral/technical SOTs | Unit/integration/manual tests |
| `docs/operations.md` | Architecture, Networking, Security | Development and deployment |
| `docs/adr/*` | Architecture decisions | Architecture and future maintenance |
| `docs/ai-workflow.md` | Active AI workflow suite | AI-assisted development |

---

# 16. Documents That Must Not Become Runtime Sources of Truth

The following artifacts are useful but must not independently define game behavior:

- AI pass prompts.
- AI pass reports.
- AI metadata JSON.
- Build logs.
- Git commit messages.
- Manual test checklists.
- CI configuration.
- `AGENTS.md`.
- `docs/ai-workflow.md`.
- Historical milestone plans.
- Comments in implementation code.
- Temporary debug scenes.
- Prototype-only configuration values.

If one of these contains a new behavioral rule that does not exist in an approved SOT, the rule must be promoted into the proper SOT before it is treated as intentional permanent behavior.

---

# 17. Minimum Documentation Gate by Milestone

## Before completing M3

Required authoritative documents:

- `decision_log_and_open_questions.md`
- `docs/architecture.md`
- `docs/gameplay.md`
- `docs/combat.md`
- `docs/testing.md`

## Before beginning authoritative M4 networking work

Additionally required:

- `docs/networking.md`
- `docs/protocol.md`
- accepted architecture decisions covering transport and serialization

## Before Nakama integration

Additionally required:

- `docs/nakama.md`
- `docs/security.md`

## Before vertical-slice/content production

Additionally required:

- `docs/match_lifecycle.md`
- `docs/assets.md`

## Before external/private hosted testing

Additionally required:

- `docs/operations.md`
- current security review
- multiplayer/manual acceptance checklist
- deployment-specific operational procedures

---

# 18. Initial Document-Creation Priority

Recommended order:

1. `core_operational_behavior_documents_list.md`
2. Normalize `decision_log_and_open_questions.md`
3. Promote `docs/architecture.md` to a complete Technical SOT
4. Create `docs/gameplay.md`
5. Create `docs/combat.md`
6. Promote `docs/networking.md` to a complete Technical SOT
7. Create `docs/protocol.md`
8. Create `docs/testing.md`
9. Create `docs/match_lifecycle.md`
10. Create `docs/nakama.md`
11. Create `docs/security.md`
12. Create `docs/assets.md`
13. Create `docs/operations.md`

This order establishes game and authority semantics before adding dependent implementation and operational specifications.

---

# 19. Completion Definition for This Documentation Set

The core operational documentation set is considered complete enough for implementation when:

- Every permanent gameplay behavior has exactly one authoritative home.
- Every authoritative runtime state has a declared owner.
- Client/server/Nakama responsibility boundaries are explicit.
- Every network message has defined semantics and direction.
- Match and player lifecycle transitions are explicit.
- Combat calculations and ownership rules are explicit.
- Open questions that block implementation are visible in the Decision Log.
- Tests trace back to documented requirements.
- Operations documentation does not define gameplay behavior.
- Historical planning material cannot silently override later approved decisions.
- AI agents can determine where to look for a rule without inferring or inventing it.

This is the structure I would use rather than copying the fulfillment app’s categories literally. In particular, I would keep `architecture.md` and `networking.md` as the existing canonical paths and expand them instead of creating replacement documents with new names. The current architecture already defines the client/headless-server/Nakama separation, and the existing networking document already establishes server authority, 60 Hz simulation, input commands, snapshots, and the later interpolation/prediction path.  

I would also explicitly demote `vantage_core_doc.md` to a **foundational planning reference** once these domain documents exist. It remains extremely useful, but the project already has at least one case where a later approved decision intentionally modifies its original milestone behavior, so treating the core document itself as permanent behavioral authority would eventually create conflicts. 

The next useful step would be to turn this structure into a **more SpicedAnime-like master spec**, where each of the 10–12 Vantage documents gets its full “Primary Purpose / Thick Boundaries / Required Sections / Required Hardcoded Content / Cross-References” definition before we actually write any of those individual documents.

---

# you asked

message time: 2026-08-07 17:27:56

why not make these folders then **gameplay rules**, **server authority**, **network/protocol behavior**, **implementation specifications**, and **workflow controls**

instead if just dumping most things into a docs folder?
this is already the progress

---

# chatgpt response

Yes. I think that is the better structure for Vantage.

My previous layout was too conservative because I was preserving the existing `docs/architecture.md`, `docs/networking.md`, etc. at the top level. For a project that is going to accumulate substantial behavioral and architectural specification, organizing by **authority/domain** is cleaner than treating `docs/` as one flat bucket.

The pass history reinforces that. Vantage has already moved beyond a simple prototype: M0/M1 are established, M2 was closed through P08, Shared combat contracts were introduced in P09, and the client hitscan path was refactored to consume those Shared contracts in P10.    The current architecture also explicitly separates client, headless server, Shared, and Nakama responsibilities. 

## Recommended structure

I would change the target to something like:

```text
docs/
├── core_operational_behavior_documents_list.md
│
├── 00_project_control/
│   ├── decision_log_and_open_questions.md
│   ├── roadmap_and_milestones.md
│   └── architecture_decision_index.md
│
├── 01_gameplay_rules/
│   ├── gameplay_overview.md
│   ├── player_movement_and_input.md
│   ├── combat_rules.md
│   ├── weapons_and_damage.md
│   ├── grenades_and_area_damage.md
│   ├── player_life_state.md
│   ├── teams_factions_and_friendly_fire.md
│   └── match_lifecycle_and_scoring.md
│
├── 02_server_authority/
│   ├── authority_model.md
│   ├── authoritative_world_state.md
│   ├── authoritative_movement.md
│   ├── authoritative_combat.md
│   ├── player_state_and_respawn.md
│   ├── input_validation.md
│   └── world_state_history.md
│
├── 03_network_protocol/
│   ├── networking_architecture.md
│   ├── protocol_contract.md
│   ├── tick_and_timing_model.md
│   ├── input_command_contract.md
│   ├── state_snapshot_contract.md
│   ├── gameplay_event_contract.md
│   ├── connection_lifecycle.md
│   └── interpolation_prediction_and_lag_compensation.md
│
├── 04_implementation_specs/
│   ├── client_architecture.md
│   ├── headless_server_architecture.md
│   ├── shared_library_contract.md
│   ├── nakama_integration.md
│   ├── security_and_trust_model.md
│   ├── assets_and_provenance.md
│   └── logging_and_diagnostics.md
│
├── 05_testing/
│   ├── testing_strategy.md
│   ├── shared_unit_testing.md
│   ├── server_integration_testing.md
│   ├── multiplayer_authority_testing.md
│   └── manual_acceptance_testing.md
│
├── 06_operations/
│   ├── local_development.md
│   ├── server_operations.md
│   ├── nakama_operations.md
│   ├── builds_and_exports.md
│   └── deployment_and_recovery.md
│
├── 07_workflow_controls/
│   └── ai-workflow.md
│
└── adr/
    ├── 0001-godot-net.md
    └── ...
```

## Why this is stronger

The biggest advantage is that the filesystem itself communicates the authority boundary.

For example, **gameplay rules** answer:

> What is supposed to happen?

`01_gameplay_rules/weapons_and_damage.md` would define damage, range, fire cadence semantics, grenade behavior, and so forth.

**Server authority** answers:

> Who is allowed to decide that it happened?

That distinction matters enormously in Vantage. The project explicitly decided that player life-state belongs in M4 authoritative server work rather than remaining local client behavior.  A separate `02_server_authority/` makes that boundary impossible to miss.

**Network/protocol** answers:

> How does authoritative information cross the process boundary?

That is different from server authority itself. The current networking specification already establishes input commands, authoritative snapshots, a 60 Hz simulation target, and later interpolation/prediction. 

And **implementation specs** answer:

> How is that approved behavior represented in the actual software?

That keeps things like Godot scene organization, Shared-library structure, Nakama integration, MemoryPack usage, logging, and asset pipelines from becoming accidental gameplay SOTs.

## I would make one adjustment to your five categories

I would **not** put testing and operations under `implementation specifications`.

They deserve independent categories:

```text
01_gameplay_rules/
02_server_authority/
03_network_protocol/
04_implementation_specs/
05_testing/
06_operations/
07_workflow_controls/
```

Testing is derived evidence. It should never define behavior.

Operations is how we run the implementation. It should never define behavior either.

That separation gives us a very clean precedence model:

```text
Approved Decisions / ADRs
        ↓
Gameplay Rules
        ↓
Server Authority
        ↓
Network / Protocol
        ↓
Implementation Specs
        ↓
Testing
        ↓
Operations
```

Not every rule literally flows through every tier, but the direction of authority is obvious.

## Existing documents

I would migrate the current documents rather than discard them.

| Current | Target |
|---|---|
| `decision_log_and_open_questions.md` | `docs/00_project_control/decision_log_and_open_questions.md` |
| `docs/architecture.md` | split primarily into `02_server_authority/authority_model.md` and relevant `04_implementation_specs/*` |
| `docs/networking.md` | seed `03_network_protocol/networking_architecture.md` |
| `docs/ai-workflow.md` | `07_workflow_controls/ai-workflow.md` |
| `docs/adr/0001-godot-net.md` | keep under `docs/adr/0001-godot-net.md` |
| `vantage_core_doc.md` | retain as historical/foundational planning reference, not behavioral SOT |

The existing ADR should remain because it captures the foundational Godot .NET client + Godot .NET headless authoritative-server decision. 

One workflow consideration: `docs/`, `vantage_core_doc.md`, and `decision_log_and_open_questions.md` are currently explicitly declared project-specific Tier 2 paths under `.private/`. The v2.4.0 migration preserved that arrangement.  So this should be treated as an intentional **documentation restructuring pass**, not casual file shuffling during gameplay implementation.

So yes: **I recommend switching to the domain-folder model.** It is more scalable, makes source-of-truth boundaries visible, and fits Vantage much better than a flat `docs/` directory.

---

# you asked

message time: 2026-08-07 17:30:15

actually i like the 

```
00_project/
01_source_of_truth/
...
```

structure better

---

# chatgpt response

Agreed. The numbered hierarchy is cleaner for this project because it communicates both **document role** and **authority order** without over-fragmenting the repository.

I’d keep that model and tailor the contents to Vantage like this:

```text
docs/
├── core_operational_behavior_documents_list.md
│
├── 00_project/
│   ├── Project_Scope_and_Roadmap.md
│   ├── Decision_Log_and_Open_Questions.md
│   └── Coding_AI_Context.md
│
├── 01_source_of_truth/
│   ├── Gameplay_Rules_SOT.md
│   ├── Combat_Weapons_and_Damage_SOT.md
│   ├── Player_Life_State_SOT.md
│   ├── Match_Lifecycle_and_Scoring_SOT.md
│   ├── Server_Authority_and_State_Ownership_SOT.md
│   └── Networking_and_Protocol_Behavior_SOT.md
│
├── 02_implementation_specs/
│   ├── Technical_Architecture_and_Process_Model.md
│   ├── Vantage_Shared_Contract.md
│   ├── Client_Implementation_Spec.md
│   ├── Headless_Server_Implementation_Spec.md
│   ├── Protocol_DTO_and_Serialization_Spec.md
│   ├── Nakama_Integration_and_Game_Server_Admission_Spec.md
│   ├── Security_and_Trust_Model.md
│   ├── Logging_and_Diagnostics_Spec.md
│   └── Asset_Pipeline_and_Provenance_Spec.md
│
├── 03_testing/
│   ├── Test_Plan_and_Acceptance_Criteria.md
│   ├── Shared_Unit_Test_Spec.md
│   ├── Server_Integration_Test_Spec.md
│   ├── Multiplayer_Authority_Test_Spec.md
│   └── Manual_Verification_Guide.md
│
├── 04_operations/
│   ├── Local_Development_Runbook.md
│   ├── Nakama_and_Postgres_Runbook.md
│   ├── Game_Server_Operations_Runbook.md
│   ├── Build_and_Export_Runbook.md
│   └── Deployment_and_Recovery_Runbook.md
│
├── 05_workflow/
│   └── AI_Workflow_Context.md
│
└── adr/
    ├── 0001-godot-net.md
    └── ...
```

## Why this is better

The key distinction would be:

- `00_project/` — project intent, roadmap, decisions, orientation.
- `01_source_of_truth/` — **what Vantage must do and who owns runtime truth**.
- `02_implementation_specs/` — **how those approved rules are implemented**.
- `03_testing/` — how conformance is proven.
- `04_operations/` — how the finished systems are run.
- `05_workflow/` — AI/development workflow only.
- `adr/` — durable architecture decisions.

That keeps the strongest part of your original example: someone can inspect the folder tree and immediately understand which files are authoritative versus derived.

I also prefer putting **server authority and networking behavior inside `01_source_of_truth/`**, rather than making them top-level folder categories. For Vantage, “the server owns player health” and “clients submit inputs rather than positions” are operational truths of the game architecture, not merely implementation details.

I would also keep `01_source_of_truth/` fairly small. Six strong SOTs are better than twenty tiny ones. Implementation details can be split more aggressively under `02_implementation_specs/`.

The next version of `core_operational_behavior_documents_list.md` should use this hierarchy and then define each document using the same strong format from your example: **Approval Status, Source Type, Primary Purpose, Thick Boundaries, Required Sections, Required Hardcoded Content, and Cross-References.**

---

# you asked

message time: 2026-08-07 17:36:31

05_workflow/ isnt needed. is this the best way to organize the core docs files? is that all the core docs we will need (roughly)

---

# chatgpt response

Yes — dropping `05_workflow/` makes sense. I’m checking the current architecture, decision log, networking scope, and pass history against the proposed hierarchy so the answer reflects what Vantage actually needs rather than a generic docs taxonomy.

Yes. With `05_workflow/` removed, I think the **numbered four-layer structure is the best fit for Vantage**, but I would refine the actual document list before locking it. The earlier version had a little too much splitting in some places and was missing one important behavioral domain: **identity/matchmaking/game-server admission**.

Vantage already has clear boundaries between client, authoritative game server, Shared, and Nakama, with the server owning movement/combat/match state/damage/respawn/score and Nakama owning identity/matchmaking rather than gameplay.  The roadmap then expands into TDM match flow, Nakama integration, assets/provenance, exports, hosted testing, and public-readiness/security work.  So I would design the core-doc set around those actual domains.

## Recommended final structure

```text
docs/
├── core_operational_behavior_documents_list.md
│
├── 00_project/
│   ├── Project_Scope_and_Roadmap.md
│   ├── Decision_Log_and_Open_Questions.md
│   └── adr/
│       ├── 0001-godot-net.md
│       └── ...
│
├── 01_source_of_truth/
│   ├── Gameplay_Rules_SOT.md
│   ├── Combat_Weapons_and_Damage_SOT.md
│   ├── Match_Lifecycle_Teams_and_Scoring_SOT.md
│   ├── Server_Authority_and_State_Ownership_SOT.md
│   ├── Networking_and_Protocol_Behavior_SOT.md
│   └── Identity_Matchmaking_and_Game_Server_Admission_SOT.md
│
├── 02_implementation_specs/
│   ├── Technical_Architecture_and_Process_Model.md
│   ├── Client_Implementation_Spec.md
│   ├── Headless_Server_Implementation_Spec.md
│   ├── Vantage_Shared_Contract.md
│   ├── Protocol_DTO_and_Serialization_Spec.md
│   ├── Nakama_Integration_Spec.md
│   ├── Security_and_Trust_Model.md
│   ├── Logging_Debugging_and_Observability_Spec.md
│   └── Asset_Pipeline_and_Provenance_Spec.md
│
├── 03_testing/
│   └── Test_Strategy_and_Acceptance_Criteria.md
│
└── 04_operations/
    ├── Local_Development_Runbook.md
    ├── Build_Export_and_Deployment_Runbook.md
    └── Hosted_Server_Operations_Runbook.md
```

That is about **21 core documents including the master index and ADR 0001**. I would consider that a very reasonable eventual size for Vantage.

### Why I changed a few things

I would **not** create separate source-of-truth files for `Player_Life_State`, `Grenades`, `Friendly_Fire`, etc. Those are real domains, but not necessarily separate documents.

For example:

- health / alive / dead / respawning → `Match_Lifecycle_Teams_and_Scoring_SOT.md`
- hitscan / projectile / grenades / damage / weapon rules → `Combat_Weapons_and_Damage_SOT.md`
- movement / aiming / player capabilities → `Gameplay_Rules_SOT.md`

That avoids ending up with 40 tiny SOT files.

The approved project direction makes this grouping particularly sensible because player life-state was explicitly moved out of the local M3 prototype and into server-authoritative M4 work. The later authoritative combat design has `World` owning player health while the server handles weapon/damage/death events. 

## One SOT I think we were missing

I strongly recommend:

`Identity_Matchmaking_and_Game_Server_Admission_SOT.md`

This isn't just a Nakama implementation detail.

There are actual behavioral rules here:

- what identifies a player;
- what an authenticated session means;
- how players enter matchmaking;
- what constitutes a match assignment;
- who may connect to a game server;
- what a game-server ticket represents;
- expiration/reuse/rejection semantics;
- what happens when admission fails;
- reconnection policy;
- later persistence/account relationships.

The existing project design already gives Nakama responsibility for identity, sessions, matchmaking, and future persistence, while the Godot server separately validates admission before accepting gameplay connections.  The core architecture describes the intended ticket handoff from Nakama to the game server as well. 

So that deserves a behavioral SOT, while `Nakama_Integration_Spec.md` describes **how the code implements those rules**.

## Testing should probably start as one core document

I would simplify the earlier `03_testing/` substantially.

You already have concrete testing layers:

- Shared xUnit tests;
- server integration testing;
- Godot/headless checks;
- milestone manual checklists. 

But those don't need four core specification files today.

Start with:

```text
03_testing/
└── Test_Strategy_and_Acceptance_Criteria.md
```

Inside it, define all of those test classes and point to actual executable/checklist artifacts:

```text
tests/
├── Vantage.Shared.Tests/
├── Vantage.Server.IntegrationTests/
└── manual/
    ├── checklist-m2.md
    ├── checklist-m3.md
    └── ...
```

If that testing document eventually becomes unwieldy, split it then.

Same philosophy for operations.

## What I would *not* add yet

There are several conceivable documents that I would **not create until the feature actually becomes substantial**:

```text
Persistence_and_Player_Data_SOT.md
Progression_and_Cosmetics_SOT.md
UI_and_UX_Spec.md
Audio_Implementation_Spec.md
Performance_Budgets.md
Telemetry_and_Analytics_Spec.md
Moderation_and_Player_Safety_SOT.md
Live_Service_Operations.md
Release_Management.md
```

Some may eventually become necessary. But the current Vantage material doesn't justify making them core documents yet.

For example, persistence is explicitly future-facing in the current architecture rather than part of the present gameplay loop.  Likewise, cross-platform exports, hosted private testing, and public hardening occur much later in M9–M11. 

The master index can have a **“Future Core Documents / Creation Triggers”** section listing those possibilities without creating empty files.

## The hierarchy itself

I think the hierarchy is strong because every level answers a different question:

| Folder | Question |
|---|---|
| `00_project/` | **What are we building, and what have we decided?** |
| `01_source_of_truth/` | **Exactly how must Vantage behave?** |
| `02_implementation_specs/` | **How must the software realize that behavior?** |
| `03_testing/` | **How do we prove it conforms?** |
| `04_operations/` | **How do we run it correctly?** |

And ADRs fit naturally under `00_project/adr/`, because they record **why durable project-level architectural decisions were made**, rather than defining day-to-day runtime behavior. The existing ADR 0001 is exactly that kind of document: it records the accepted Godot .NET client + Godot .NET headless server architecture and the Nakama boundary. 

### One important principle

I would **not try to predict every document Vantage will ever need now**.

The target should instead be:

> Every important current rule has exactly one authoritative home, and the directory structure has obvious places for new domains when they become real.

With the structure above, I think we achieve that. **Roughly, yes: this is the complete core-document set I would expect Vantage to need through the planned M0–M11 arc, with perhaps 3–6 additional domain documents emerging later rather than dozens being created upfront.**

---

# you asked

message time: 2026-08-07 17:41:58

Agrees, now perform a verification run on youre recommended structure. Remember to go back to the core of what vantage is and what type of game I am wanting to build. Then give your final recommendeds for structure. @Web search

---

# chatgpt response

I’ll verify the structure against two things: the actual Vantage vision/roadmap in your project materials, and the responsibilities implied by the current technical stack. Then I’ll give you one final structure, including anything I think should be merged, renamed, added, or deferred.

The verification is converging on one important refinement: the four-folder hierarchy is sound, but the document inventory should be organized around **Vantage’s actual game loop**—a small-session top-down TDM shooter—not around every technical subsystem that happens to exist. That means a few earlier specs should merge, while a couple of player-facing domains deserve clearer homes.

I ran the proposed hierarchy back against the Vantage core document, architecture/networking decisions, approved decision log, M2→M4 execution plan, completed P01–P10 product work, and the current v2.4.0 repository state. I also checked the structure against the official Godot, Nakama, MemoryPack, and Diátaxis documentation.

My conclusion is: **the `00_project / 01_source_of_truth / 02_implementation_specs / 03_testing / 04_operations` model is the right long-term structure for Vantage.** I would keep it, with several refinements to the actual document inventory.

## 1. Re-grounding in what Vantage actually is

Vantage is not primarily “a Godot networking project.” It is a **top-down multiplayer shooter whose first complete game mode is small-session team deathmatch**. The target vertical slice is a 10-player TDM match with two visually distinct factions, a five-minute match, instant respawning, friendly fire disabled, scoring/scoreboard, multiple weapon types including hitscan/projectile/grenade behavior, and a polished arena/HUD/audio presentation.  

The intended presentation is also fairly specific: pixel-art-oriented visuals, a constrained palette, faction variants, weapon overlays, arena tiles, HUD/scoreboard/kill feed, combat SFX, music, and later asset provenance suitable for commercial distribution if that path is taken.  

The networking architecture exists to serve that game. A Godot .NET headless server owns authoritative gameplay; the client submits inputs and renders authoritative results; `Vantage.Shared` owns shared gameplay contracts/math and protocol contracts; Nakama handles authentication, matchmaking, persistence/service functions, and game-server admission rather than real-time gameplay simulation. 

The current product implementation has already validated this direction. M2 was closed, Shared combat contracts were established in P09, and P10 moved client hitscan damage/range/cadence onto `WeaponDef` and `DamageMath`. P10 built and tested successfully, although its report still leaves the final Godot manual check outstanding.  

That is the lens I used for the verification.

## 2. Verification results

| Test | Result | Finding |
|---|---|---|
| Represents the actual game rather than the technology | **PASS after refinement** | Gameplay, combat, TDM, player experience, identity/admission all need explicit homes. |
| Makes server authority unambiguous | **PASS** | Dedicated authority SOT is warranted. |
| Separates behavioral rules from C#/Godot implementation | **PASS** | One of the strongest features of the structure. |
| Handles ENet/MemoryPack protocol growth | **PASS** | Behavioral networking SOT + exact protocol implementation spec is the right split. |
| Handles Nakama without letting it become “the game server” | **PASS** | Identity/matchmaking/admission SOT + Nakama implementation spec cleanly preserves the boundary. |
| Handles TDM lifecycle completely | **PASS after refinement** | Match, teams, scoring **and player life-state** should remain together. |
| Covers player-facing HUD/lobby/scoreboard/kill-feed behavior | **PARTIAL in previous version** | Add a player-experience/UI behavioral SOT. |
| Covers security/public-play readiness | **PARTIAL in previous version** | Security/trust belongs in Source of Truth, not merely Implementation Specs. |
| Covers visual/audio asset production | **PASS** | One presentation/assets/provenance implementation spec is appropriate. |
| Testing structure appropriately sized | **PASS** | One central testing document is enough initially. |
| Operations structure appropriately sized | **PASS** | Three runbooks are enough; don't over-split them yet. |
| Avoids documentation explosion | **PASS after refinement** | Keep broad domain docs and split only when a document becomes genuinely unwieldy. |
| Scales through M11 | **PASS** | Every planned milestone has an obvious documentation home. |

The external technical check also supports the architectural separations already chosen. Godot officially supports dedicated-server exports/headless operation, and `ENetMultiplayerPeer` is its ENet-backed multiplayer peer. ([Godot Engine documentation](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_dedicated_servers.html?utm_source=chatgpt.com)) Nakama explicitly treats authentication sessions and matchmaking as separate backend concerns, which supports keeping identity/matchmaking/admission distinct from the authoritative gameplay simulation. ([Heroic Labs](https://heroiclabs.com/docs/nakama/concepts/authentication/?utm_source=chatgpt.com))

MemoryPack itself uses code generation as part of its architecture, which is another reason the exact serialized DTO/wire contract deserves a technical implementation document distinct from higher-level networking behavior. ([GitHub](https://github.com/Cysharp/MemoryPack?utm_source=chatgpt.com))

There is also a useful documentation-design validation here: Diátaxis explicitly distinguishes technical/reference information from procedural how-to documentation. It isn't a prescription for this exact folder tree, but it reinforces the decision **not to mix operational runbooks with reference/source-of-truth material**. ([Diátaxis](https://diataxis.fr/?utm_source=chatgpt.com))

## 3. Final recommended structure

I would lock the target structure as:

```text
docs/
├── core_operational_behavior_documents_list.md
│
├── 00_project/
│   ├── game_vision_scope_and_roadmap.md
│   ├── decision_log_and_open_questions.md
│   └── adr/
│       ├── 0001-godot-net.md
│       └── ...
│
├── 01_source_of_truth/
│   ├── gameplay_and_player_controls_sot.md
│   ├── combat_weapons_and_damage_sot.md
│   ├── match_teams_scoring_and_life_state_sot.md
│   ├── server_authority_and_state_ownership_sot.md
│   ├── networking_and_protocol_behavior_sot.md
│   ├── identity_matchmaking_and_game_server_admission_sot.md
│   ├── player_experience_and_ui_behavior_sot.md
│   └── security_and_trust_boundaries_sot.md
│
├── 02_implementation_specs/
│   ├── technical_architecture_and_process_model.md
│   ├── vantage_shared_contract.md
│   ├── client_implementation_spec.md
│   ├── headless_server_implementation_spec.md
│   ├── protocol_dto_and_serialization_spec.md
│   ├── nakama_integration_spec.md
│   └── presentation_assets_and_provenance_spec.md
│
├── 03_testing/
│   └── test_strategy_and_acceptance_criteria.md
│
└── 04_operations/
    ├── local_development_runbook.md
    ├── build_export_and_ci_runbook.md
    └── hosted_server_and_services_runbook.md
```

I recommend lowercase filenames because that already matches the repository's existing documentation convention.

## 4. The important refinements

### `game_vision_scope_and_roadmap.md`

This should be the successor to the useful parts of `vantage_core_doc.md`.

It answers:

**What is Vantage? What experience are we trying to build? What is in/out? Where are we going?**

It should contain the concise product vision, target game mode, target player count, platform scope, high-level visual direction, major milestones, non-goals, and major product constraints.

It should **not** contain detailed networking protocol structures, exact code architecture, or temporary pass numbering.

That prevents the problem the existing core document already experienced: implementation sequencing changed after the document was written, such as the approved decision to move player life-state out of local M3 and into authoritative M4. 

### `gameplay_and_player_controls_sot.md`

This is the general physical rules of playing Vantage:

movement, aiming, collision expectations, input semantics, weapon selection interaction, grenade input, player capabilities, world interaction, and similar rules.

It should **not** absorb detailed damage formulas or match lifecycle rules.

### `combat_weapons_and_damage_sot.md`

This becomes especially important because Shared combat contracts already exist.

It owns the intended semantics of:

hitscan, projectiles, grenades, fire cadence, range, magazine/ammo rules when finalized, damage, falloff, friendly-fire behavior, hit/death attribution, weapon switching, and final weapon roster/balance once approved.

P09/P10 are already early implementation evidence for this domain. 

### `match_teams_scoring_and_life_state_sot.md`

I would **keep these together rather than creating separate files**.

Alive/dead/respawning, faction assignment, friendly-fire rules, kills, scores, timer, warmup, active match, postgame, restart/reset all participate in one match state machine.

The core document itself warns against letting match state grow ad hoc. 

### `server_authority_and_state_ownership_sot.md`

This is one of the most important Vantage documents.

It answers for every significant piece of state:

**Who owns truth? Who may request a change? Who validates it? What may the client display but never decide?**

Movement, position, aim/fire requests, health, death, score, teams, match state, projectiles, grenades, timestamps/ticks, etc.

The M4 plan demonstrates why this deserves its own SOT: player life-state was deliberately moved to the authoritative server specifically to avoid client-authority drift and throwaway implementation. 

### `networking_and_protocol_behavior_sot.md`

This should describe network **semantics**, not exact C# layouts:

60 Hz authoritative simulation, input flow, snapshot behavior, stale/out-of-order policy, connection/disconnection behavior, interpolation, later prediction/reconciliation/lag compensation, protocol compatibility expectations, event semantics, and similar behavioral rules.

The existing architecture already establishes input-command → server simulation → authoritative snapshot as the fundamental loop. 

### `identity_matchmaking_and_game_server_admission_sot.md`

I still strongly recommend this one.

M6 is an actual player lifecycle:

authenticate → acquire session → enter matchmaking → receive match → obtain game-server endpoint/ticket → server validates → enter gameplay.

Those are **game/service behaviors**, not just details of `NakamaService.cs`. The existing plan explicitly requires valid ticket admission and rejection without a ticket. 

Official Nakama documentation likewise treats authentication/session issuance and matchmaking as independent service capabilities. ([Heroic Labs](https://heroiclabs.com/docs/nakama/concepts/authentication/?utm_source=chatgpt.com))

### `player_experience_and_ui_behavior_sot.md`

This is the main addition resulting from the verification.

Without it, behavioral requirements for the HUD, reticle, health/ammo display, weapon indicator, scoreboard, kill feed, lobby, matchmaking states, postgame screen, connection errors, and optional minimap would drift into `client_implementation_spec.md`.

That would be wrong.

The SOT answers **what the player must see and when**.

The client spec answers **how Godot implements it**.

For a competitive shooter, that distinction matters.

### `security_and_trust_boundaries_sot.md`

I would move this from `02_implementation_specs` into `01_source_of_truth`.

Security rules such as:

client is untrusted, server is authoritative, inputs are bounded, invalid/stale data is rejected, admission tickets expire, replay is rejected, secrets never enter clients, rate limits exist at exposed service boundaries, and debug interfaces must not survive inappropriate deployment contexts

are **system truths**, not merely implementation suggestions.

The code mechanisms that enforce those truths live in the server, protocol, and Nakama implementation specs.

## 5. Implementation specs are now leaner

I would stop at seven central implementation specs.

`technical_architecture_and_process_model.md` describes the big physical system: client, Shared, headless server, Nakama/Postgres, process/dependency boundaries.

`vantage_shared_contract.md` describes what is permitted in `Vantage.Shared`, including its Godot-free boundary and ownership of reusable gameplay math/contracts.

`client_implementation_spec.md` owns Godot client scenes, autoloads, views, input collection, UI composition, snapshot rendering, interpolation, audio presentation, etc.

`headless_server_implementation_spec.md` owns `MatchHost`, world state, tick loop, authoritative systems, history/ring buffer, server scene organization, and related internals.

`protocol_dto_and_serialization_spec.md` owns exact DTO fields, MemoryPack annotations/versioning, message IDs/types, serialization compatibility and wire representation.

`nakama_integration_spec.md` owns the concrete client SDK usage, RPC/module implementation and admission-ticket mechanics.

`presentation_assets_and_provenance_spec.md` owns pixel-art constraints, palette, source/engine asset flow, naming/import standards, audio asset rules, and provenance.

I **removed a standalone logging/observability implementation document from the core set**. For the expected scale of Vantage, those requirements can initially live across architecture, testing, and hosted operations. If logging/telemetry becomes complex during M10/M11, it can graduate into its own document.

## 6. Testing and operations passed the simplification check

I would keep only:

```text
03_testing/
└── test_strategy_and_acceptance_criteria.md
```

Actual test artifacts remain where they belong in the repository:

```text
tests/
├── Vantage.Shared.Tests/
├── Vantage.Server.IntegrationTests/
└── manual/
```

The core test document explains the test taxonomy, required authority/cheat tests, milestone acceptance gates, latency/network testing, regression expectations, and which SOT each test category validates.

Likewise, three operational runbooks are enough:

```text
04_operations/
├── local_development_runbook.md
├── build_export_and_ci_runbook.md
└── hosted_server_and_services_runbook.md
```

Don't create separate Postgres, Nakama, game-server, backup, log, recovery, and deployment files until those runbooks actually become too large.

## 7. Documents I would **not** create yet

I would not pre-create separate core files for persistence/player data, progression, cosmetics, individual weapons, grenades, individual arenas, audio, performance budgets, anti-cheat implementation, moderation, analytics, release management, Steam/store operations, or live-service management.

Some could eventually become necessary.

The master index should instead contain **document creation triggers**. For example, if Vantage gains multiple substantially different modes, split `match_teams_scoring_and_life_state_sot.md` into mode-independent lifecycle plus per-mode SOTs. If persistence becomes a meaningful player-facing system, create a persistence SOT then. If multiple arenas gain formal spawn/layout rules, create an arena-design SOT then.

This follows a useful general documentation principle as well: structure should grow because the content requires it rather than because every conceivable category was created in advance. Diátaxis makes a similar point about allowing useful documentation structure to emerge from actual needs. ([Diátaxis](https://diataxis.fr/how-to-use-diataxis/?utm_source=chatgpt.com))

## Final recommendation

**I would now consider the hierarchy settled.**

The final target is approximately **22–24 core documents including the index and ADRs**, but they should **not all be written immediately**. The structure should define their eventual homes and creation gates.

The most important improvement over the previous iteration is this:

> `01_source_of_truth/` is not a bag of technical architecture documents. It is the canonical specification of **how Vantage behaves as a game and online system**.

That means its eight domains—gameplay, combat, match/life-state, authority, networking, identity/admission, player-facing UI behavior, and security/trust—directly correspond to the major behaviors that make Vantage the particular top-down multiplayer TDM shooter you are building.

Everything under `02_implementation_specs/` then exists to implement those truths, `03_testing/` proves them, and `04_operations/` explains how to run the resulting system.

One operational note for the eventual reorganization: the current v2.4.0 controls explicitly recognize `docs/`, `vantage_core_doc.md`, and `decision_log_and_open_questions.md` as project-specific Tier 2 paths.  Moving the latter two into this new `docs/00_project/` layout should therefore be done as an intentional documentation/control-maintenance pass rather than mixed into the next gameplay implementation pass.