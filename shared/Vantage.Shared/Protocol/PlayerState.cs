using System.Numerics;
using MemoryPack;
using Vantage.Shared.Game;

namespace Vantage.Shared.Protocol;

[MemoryPackable]
public readonly partial record struct PlayerState(uint EntityId, Vector2 Position, float AimAngleRadians, Faction Faction);
