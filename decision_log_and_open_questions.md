---
title: "Decision Log and Open Questions"
version: "1.0"
status: 
last_verified: "2026-07-01"
game_architect: "dweebz"
author: "Josh"
---

# Decision Log and Open Questions

## Approved Decisions

### Decision: Restructure — defer player life-state to M4
**Status:** Approved
**Owner:** Josh
**Context:** Core doc M3 lists local player health/death/respawn; this conflicts with server authority and creates throwaway work.
**Decision:** M3 dummies-only damage; player health/death/respawn enters server-authoritative in M4.
**Rationale:** Avoids rework; keeps life-state authority off the client.
**Affected Documents:** vantage_core_doc.md §8 (M3), §9 (pass sequence)
**Affected Implementation Areas:** M3 client combat passes, P20
**Blocks:** M3 scope finalization (P13–P14), P20 — now unblocked

### Decision: Protocol serialization library
**Status:** Approved
**Owner:** Josh
**Context:** AGENT.md/architecture.md state MemoryPack is "preferred unless a later pass changes it," but it was not formally decided. Blocked the protocol DTO pass.
**Decision:** Ratify MemoryPack for protocol DTOs.
**Rationale:** Zero-alloc, source-generated, greenfield C#; no non-.NET interop planned.
**Affected Documents:** architecture.md, networking.md
**Affected Implementation Areas:** shared/Vantage.Shared/Protocol
**Blocks:** P15 — now unblocked

### Decision: Real-time transport
**Status:** Approved
**Owner:** Josh
**Context:** Core doc §12 flagged ENet vs WebSocket as unconfirmed. Blocked the transport pass.
**Decision:** Ratify ENet (ENetMultiplayerPeer) for client↔server gameplay.
**Rationale:** Godot-native, UDP fits a shooter, DTLS path available later; Hetzner does not restrict UDP.
**Affected Documents:** networking.md
**Affected Implementation Areas:** server/headless/net, client/autoloads
**Blocks:** P17 — now unblocked

## Open Questions

## Decision History