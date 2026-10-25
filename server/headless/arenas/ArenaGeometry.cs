using Godot;

namespace Vantage.Server;

// Physics-only copy: exact source cells/settings, without an atlas or texture.
// ArenaDriftTests guards the copied data, polygons, physics settings and position.
public partial class ArenaGeometry : Node2D
{
    [Export] public Vector2[] WallPolygon { get; set; } = [];
    [Export] public Vector2[] ObstaclePolygon { get; set; } = [];

    public override void _Ready()
    {
        TileMapLayer cells = GetNode<TileMapLayer>("Cells");
        foreach (Vector2I cell in cells.GetUsedCells())
        {
            Vector2I atlas = cells.GetCellAtlasCoords(cell);
            if (atlas.X is not (1 or 2)) continue; // atlas 0 is the collision-free floor.
            var body = new StaticBody2D
            {
                Position = cells.MapToLocal(cell),
                CollisionLayer = cells.TileSet.GetPhysicsLayerCollisionLayer(0),
                CollisionMask = cells.TileSet.GetPhysicsLayerCollisionMask(0),
            };
            body.AddChild(new CollisionPolygon2D { Polygon = atlas.X == 1 ? WallPolygon : ObstaclePolygon });
            AddChild(body);
        }
    }
}
