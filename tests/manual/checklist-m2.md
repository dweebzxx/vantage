# M2 Manual Checklist

P03-P07 baseline below is marked complete from the prompt handoff: P07 manual verification passed. P08 reset and multi-dummy checks remain manual verification required until run in the Godot editor.

## User-Confirmed P03-P07 Baseline

- [x] Launch the Godot 4.7 .NET editor with `client/` as the project.
- [x] Run the movement sandbox main scene.
- [x] Verify the dev tilemap/test arena loads and is visible.
- [x] Verify WASD moves the player left, right, up, and down.
- [x] Verify diagonal movement is not faster than cardinal movement.
- [x] Verify the camera follows the player or otherwise keeps the player visible.
- [x] Verify the player moves freely across floor tiles.
- [x] Verify boundary wall tiles block the player.
- [x] Verify the interior obstacle tiles block the player.
- [x] Verify mouse aim direction follows the cursor.
- [x] Verify the aim visualization remains visible while moving.
- [x] Verify left mouse fires one local debug shot line.
- [x] Verify the debug shot follows the current mouse aim direction.
- [x] Verify a dev target dummy can be hit by the debug shot.
- [x] Verify left mouse reduces the dev target dummy health/damage counter by the current local placeholder damage value.
- [x] Verify the dev target dummy reaches a clear defeated/inert state after max health is exhausted.
- [x] Verify additional shots do not underflow the dummy below 0 health or show negative health.
- [x] Verify no real player health/death/respawn/score/ammo/inventory/weapon switching behavior exists.
- [x] Verify no network, server, Nakama, or protocol behavior is involved in firing or hit feedback.
- [x] Verify aim visuals are clearly dev/debug scoped with `DevDebug...` or `DevPlaceholder...` naming.
- [x] Verify combat visuals and target dummy visuals are clearly dev/debug scoped with `Dev...` or `DevPlaceholder...` naming.
- [x] Verify placeholder tile visuals/resources are under dev-scoped paths and use `_placeholder` or `Dev...` naming.
- [x] Verify no real gameplay combat systems, audio, or final asset behavior is involved.

## P08 Manual Gate

- [ ] Verify `dev_reset` is bound to R and works in the movement sandbox.
- [ ] Verify two dev target dummies are visible and reachable in the movement sandbox.
- [ ] Verify each dummy can be hit by the local debug shot.
- [ ] Verify hitting one dummy reduces only that dummy's health/damage counter.
- [ ] Verify the other dummy remains unchanged until hit.
- [ ] Verify a dummy reaches a clear defeated/inert local state at 0 health.
- [ ] Verify additional shots do not underflow health below 0.
- [ ] Verify pressing R restores dummy HP to max.
- [ ] Verify pressing R clears each dummy's inert state.
- [ ] Verify pressing R resets both dummies.
- [ ] Verify movement, mouse aim, tile collision, and debug shot-line behavior are unaffected before and after reset.
- [ ] Verify no real player health/death/respawn/score/ammo/inventory/weapon switching behavior exists.
- [ ] Verify no networking/server/Nakama/protocol behavior occurs.
- [ ] Verify all combat visuals and target dummy visuals remain dev/debug scoped.

## P10 Shared Combat Source Gate

- [ ] Verify local hitscan damage, range, and fire cadence derive from `Vantage.Shared.Game.WeaponDef.DevHitscan`.
- [ ] Verify local hitscan damage application derives from `Vantage.Shared.Game.DamageMath`.
- [ ] Verify each valid dummy hit applies damage equal to the P09 placeholder `WeaponDef.DevHitscan.Damage` value.
- [ ] Verify range behavior matches the P09 placeholder `WeaponDef.DevHitscan.RangeUnits` value.
- [ ] Verify the dummy health counter never underflows below 0.
- [ ] Verify damage remains local and dummies-only; M3 player health/death/respawn remains deferred.
- [ ] Verify no player score/ammo/inventory/weapon switching/network/server/Nakama behavior is present.
