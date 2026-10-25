using System.Numerics;
using MemoryPack;

namespace Vantage.Shared.Protocol;

[MemoryPackable]
public readonly partial record struct GrenadeState(uint EntityId, Vector2 Position);
