# M2 Manual Checklist

- Launch the Godot 4.7 .NET editor with `client/` as the project.
- Run the movement sandbox main scene.
- Verify the dev tilemap/test arena loads and is visible.
- Verify WASD moves the player left, right, up, and down.
- Verify diagonal movement is not faster than cardinal movement.
- Verify the camera follows the player or otherwise keeps the player visible.
- Verify the player moves freely across floor tiles.
- Verify boundary wall tiles block the player.
- Verify the interior obstacle tiles block the player.
- Verify placeholder tile visuals/resources are under dev-scoped paths and use `_placeholder` or `Dev...` naming.
- Verify no network, server, Nakama, protocol, combat, audio, or final asset behavior is involved.
