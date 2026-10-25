using MemoryPack;

namespace Vantage.Shared.Protocol;

[MemoryPackable]
public readonly partial record struct DamageDealtEvent(uint ServerTick, uint SourceEntityId, uint TargetEntityId, int Amount);
