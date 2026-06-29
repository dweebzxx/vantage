# ADR 0001: Use Godot .NET Client With Godot .NET Headless Authoritative Server

## Status

Accepted

## Date

2026-06-29

## Context

vantage is a C#-first top-down multiplayer shooter targeting Godot 4.7 .NET. The project needs a path that keeps gameplay authority off the client while still using Godot scenes, physics, and tooling for both development and runtime simulation.

Nakama is useful for auth, matchmaking, sessions, and persistence, but its role should not include real-time gameplay simulation for this architecture.

## Decision

Use a Godot .NET desktop client with a separate Godot .NET headless authoritative server. Use Nakama for auth, matchmaking, and non-gameplay services only.

## Consequences

The client and server can share protocol DTOs and gameplay math through a later `Vantage.Shared` plain .NET library. The server can use Godot physics and scene data while remaining authoritative. The architecture requires maintaining separate client and headless server Godot projects after P02.

Nakama modules may be added later for service boundaries, but Nakama is not used for gameplay simulation.
