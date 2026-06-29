# Architecture

## Overview

vantage is planned as a Godot 4.7 .NET C# top-down multiplayer shooter using a Godot .NET desktop client, a Godot .NET headless authoritative server, and Nakama for auth, matchmaking, and non-gameplay services.

## Process Model

Local development will eventually run three cooperating processes: Nakama with Postgres, a Godot headless game server, and one or more Godot desktop clients.

## Client

The client renders presentation, collects player input, talks to Nakama through `nakama-dotnet`, and connects to the game server for real-time play. Gameplay authority must not live in the client.

## Godot Headless Authoritative Server

The headless server owns gameplay state, validates client inputs, runs the fixed simulation, and sends authoritative snapshots. It must not delegate gameplay simulation to Nakama.

## Nakama Role

Nakama is reserved for authentication, matchmaking, non-gameplay RPCs, persistence, and service boundaries. Nakama modules are out of scope for P01.

## Shared Library Plan

`Vantage.Shared` will be created in a later pass as a plain .NET library with no Godot dependency. Protocol DTOs, shared gameplay math, enums, and serialization helpers should live there once that project exists.

## Fixed Tick And Snapshots

The authoritative simulation targets a 60 Hz Godot physics tick. Clients send input commands; the server sends authoritative snapshots. Snapshot interpolation and prediction are later milestones.

## Serialization

MemoryPack is the preferred serialization choice for protocol DTOs unless a later explicit architecture pass changes this decision.

## Non-Goals Before P02

P01 does not create Godot projects, C# projects, solution files, gameplay code, CI workflows, Nakama modules, networking code, or assets. P02 should create the empty buildable project skeleton.
