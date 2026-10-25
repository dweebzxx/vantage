using MemoryPack;

namespace Vantage.Shared.Protocol;

[MemoryPackable]
public readonly partial record struct WelcomeMessage(uint EntityId, int ProtocolVersion);
