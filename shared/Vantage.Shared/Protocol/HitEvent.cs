using MemoryPack;

namespace Vantage.Shared.Protocol;

[MemoryPackable]
public readonly partial record struct HitEvent(uint ServerTick, uint SourceEntityId, uint TargetEntityId);
