using Godot;
using Vantage.Shared.Game;

/// <summary>
/// The one place the client keeps its placeholder team colors. They match the colors already in
/// Player.tscn (blue) and RemotePlayer.tscn (red). Faction.None has no color, so a body keeps its scene color.
/// </summary>
public static class TeamColors
{
    public static readonly Color DevBlue = new(0.15f, 0.65f, 0.95f);
    public static readonly Color DevRed = new(0.95f, 0.35f, 0.2f);

    public static bool TryGet(Faction faction, out Color color)
    {
        switch (faction)
        {
            case Faction.DevBlue:
                color = DevBlue;
                return true;
            case Faction.DevRed:
                color = DevRed;
                return true;
            default:
                color = default;
                return false;
        }
    }

    /// <summary>The upper-case word the HUD shows for a team, or null for Faction.None.</summary>
    public static string? Word(Faction faction) => faction switch
    {
        Faction.DevBlue => "BLUE",
        Faction.DevRed => "RED",
        _ => null,
    };
}
