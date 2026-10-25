using MemoryPack;

namespace Vantage.Shared.Protocol;

[MemoryPackable]
public readonly partial record struct DefeatEvent(uint ServerTick, uint SourceEntityId, uint TargetEntityId);
