# Networking

## Local Development Topology

The intended local topology is Nakama/Postgres in Docker, a Godot headless authoritative game server, and one or more Godot desktop clients.

## Server Authoritative Model

The server owns movement, combat, match state, damage, respawn, and score authority. Clients submit input commands and render the latest accepted server state.

## Fixed Tick

The gameplay simulation is planned around a 60 Hz Godot physics tick. Protocol messages should reference explicit tick numbers once the protocol exists.

## Input Commands

Clients will send compact input commands containing movement, aim, buttons, and client tick information. The server validates and applies inputs at the authoritative tick.

## State Snapshots

The server will broadcast authoritative snapshots containing the current tick and enough world state for clients to render. Snapshot rate may start lower than simulation rate to control bandwidth.

## Later Interpolation/Prediction Path

Snapshot interpolation is expected before broader playtesting. Client prediction, reconciliation, and lag compensation should wait until the base protocol and server simulation are stable.

## Nakama Auth/Matchmaking Boundary

Nakama handles identity, sessions, matchmaking, and future persistence. It does not run the real-time gameplay simulation.

## Security Notes

The server must validate tickets, clamp input values, reject stale or malformed inputs, and rate-limit any service boundary added later. Development defaults in Docker are local-only and must not be exposed as production credentials.
