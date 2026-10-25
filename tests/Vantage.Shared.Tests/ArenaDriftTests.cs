using Xunit;

namespace Vantage.Shared.Tests;

public sealed class ArenaDriftTests
{
    [Fact]
    public void Server_physics_copy_matches_every_wall_setting_and_world_position()
    {
        var (client, server, sandbox) = ReadScenes();
        var expected = WallSettings(client, ArenaPosition(sandbox, "[node name=\"DevTilemapArena\""));
        var actual = WallSettings(server, ArenaPosition(server, "[node name=\"Arena01\""));
        Assert.True(Differences(expected, actual).Count == 0, string.Join("\n", Differences(expected, actual)));
        Assert.DoesNotContain("Texture", server);
        Assert.DoesNotContain("texture =", server);
    }

    [Theory]
    [InlineData("tile_map_data", "tile_map_data =", "tile_map_data = changed")]
    [InlineData("tile_size", "tile_size = Vector2i(32, 32)", "tile_size = Vector2i(64, 32)")]
    [InlineData("physics layers and collision layer/mask", "physics_layer_0/collision_layer = 1", "physics_layer_0/collision_layer = 2")]
    [InlineData("physics layers and collision layer/mask", "physics_layer_0/collision_layer = 1", "physics_layer_0/collision_layer = 1\nphysics_layer_0/collision_mask = 2")]
    [InlineData("collision polygon 1", "WallPolygon = PackedVector2Array(-16", "WallPolygon = PackedVector2Array(-15")]
    [InlineData("collision polygon 2", "ObstaclePolygon = PackedVector2Array(-16", "ObstaclePolygon = PackedVector2Array(-15")]
    [InlineData("arena world position", "position = Vector2(2, 1)", "position = Vector2(3, 1)")]
    public void Drift_diagnostic_names_each_changed_setting(string setting, string before, string after)
    {
        var (client, server, sandbox) = ReadScenes();
        Assert.Contains(before, server);
        string changed = server.Replace(before, after, StringComparison.Ordinal);
        var expected = WallSettings(client, ArenaPosition(sandbox, "[node name=\"DevTilemapArena\""));
        var actual = WallSettings(changed, ArenaPosition(changed, "[node name=\"Arena01\""));
        string difference = Assert.Single(Differences(expected, actual));
        Assert.StartsWith($"Arena drift: {setting} differs.", difference);
    }

    private static (string Client, string Server, string Sandbox) ReadScenes()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "vantage.sln"))) root = root.Parent;
        Assert.NotNull(root);
        return (File.ReadAllText(Path.Combine(root!.FullName, "client/scenes/dev/DevTilemapArena.tscn")),
            File.ReadAllText(Path.Combine(root.FullName, "server/headless/arenas/Arena01.tscn")),
            File.ReadAllText(Path.Combine(root.FullName, "client/scenes/dev/MovementSandbox.tscn")));
    }

    private static Dictionary<string, string> WallSettings(string scene, string position)
    {
        string[] lines = scene.Split('\n').Select(line => line.Trim()).ToArray();
        string Property(string prefix) => Assert.Single(lines, line => line.StartsWith(prefix + " =", StringComparison.Ordinal));
        string Polygon(string clientKey, string serverKey)
        {
            string line = Assert.Single(lines, l => l.StartsWith(clientKey + " =", StringComparison.Ordinal) || l.StartsWith(serverKey + " =", StringComparison.Ordinal));
            string prefix = clientKey[..clientKey.IndexOf("physics_layer_", StringComparison.Ordinal)] + "physics_layer_";
            string extras = string.Join("\n", lines.Where(l => l.StartsWith(prefix, StringComparison.Ordinal) && l != line).Order());
            return line.Split(" = ", 2)[1] + extras;
        }
        return new()
        {
            ["tile_map_data"] = Property("tile_map_data"),
            ["tile_size"] = Property("tile_size"),
            ["physics layers and collision layer/mask"] = string.Join("\n", lines.Where(l => l.StartsWith("physics_layer_", StringComparison.Ordinal)).Order()),
            ["collision polygon 1"] = Polygon("1:0/0/physics_layer_0/polygon_0/points", "WallPolygon"),
            ["collision polygon 2"] = Polygon("2:0/0/physics_layer_0/polygon_0/points", "ObstaclePolygon"),
            ["arena world position"] = position,
        };
    }

    private static string ArenaPosition(string scene, string node)
    {
        string section = scene[scene.IndexOf(node, StringComparison.Ordinal)..].Split("\n[node ")[0];
        return Assert.Single(section.Split('\n'), l => l.StartsWith("position =", StringComparison.Ordinal));
    }

    private static List<string> Differences(Dictionary<string, string> client, Dictionary<string, string> server) =>
        client.Where(pair => !server.TryGetValue(pair.Key, out string? actual) || pair.Value != actual)
            .Select(pair => $"Arena drift: {pair.Key} differs. Client: {pair.Value}; Server: {server.GetValueOrDefault(pair.Key)}").ToList();
}
