namespace Vantage.Shared.Protocol;

public enum MessageType : byte
{
    Input = 1,
    Snapshot = 2,
    Welcome = 3,
    Hit = 4,
    DamageDealt = 5,
    Defeat = 6,
}
