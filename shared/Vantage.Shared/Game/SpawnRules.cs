using System.Numerics;

namespace Vantage.Shared.Game;

/// <summary>Pure spawn choice for a join or a respawn.</summary>
public static class SpawnRules
{
    /// <summary>Side of the square player body, in world units.</summary>
    public const float BodySize = 32f;

    /// <summary>
    /// Returns the index of the first spawn in <paramref name="teamSpawns"/> that no position in
    /// <paramref name="otherPositions"/> occupies, or 0 when every spawn is occupied. A spawn is occupied when
    /// another body centre is strictly inside a <see cref="BodySize"/> by <see cref="BodySize"/> box around it
    /// (<c>|dx| &lt; BodySize</c> and <c>|dy| &lt; BodySize</c>).
    /// </summary>
    public static int ChooseSpawn(IReadOnlyList<Vector2> teamSpawns, IEnumerable<Vector2> otherPositions)
    {
        ArgumentNullException.ThrowIfNull(teamSpawns);
        ArgumentNullException.ThrowIfNull(otherPositions);
        if (teamSpawns.Count == 0)
            throw new ArgumentException("At least one spawn is required.", nameof(teamSpawns));

        Vector2[] others = otherPositions.ToArray();
        for (int i = 0; i < teamSpawns.Count; i++)
        {
            if (!IsOccupied(teamSpawns[i], others))
                return i;
        }

        return 0;
    }

    private static bool IsOccupied(Vector2 spawn, Vector2[] others)
    {
        foreach (Vector2 other in others)
        {
            if (MathF.Abs(other.X - spawn.X) < BodySize && MathF.Abs(other.Y - spawn.Y) < BodySize)
                return true;
        }

        return false;
    }
}
