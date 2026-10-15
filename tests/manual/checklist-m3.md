# M3 Manual Checklist

This checklist tests the shooting and grenade features (milestone M3) by playing the game yourself. Nothing here has been tried yet. Tick a box only after you have seen the thing happen.

## How to open the game

Pick one of these two ways.

**Way 1: from Terminal (quickest).**
1. Open the Terminal app.
2. Type `cd /Volumes/etcetera/etceteraOS/ai-projects/code-playground/vantage` and press Return. This takes you to the project folder.
3. Type `godot-mono --editor --path client` and press Return. The Godot editor opens with the game loaded.

**Way 2: from the Godot app.**
1. Open the Godot .NET (Mono) app, the version 4.7 one.
2. In the project list, click **Import**.
3. Go into the project folder, open the `client` folder, choose the file `project.godot`, and click **Open**.
4. Click **Import & Edit**.

**Then start the game.** Press the **Play** button (the triangle at the top right of Godot) or press **F5** (on a Mac, **Cmd+B** also works). The test arena should open in a game window. If Godot asks you to pick a main scene, the right one is `MovementSandbox`. Close the game window when you are done.

Notes:
- If Godot shows a build or "C#" error the first time, click the hammer icon (**Build**) at the top, then press Play again.
- When this list mentions "wait time", it means the short pause after a shot or throw before you can do it again.
- The red boxes are the practice targets ("dummies"). Each one has 75 health.

Controls used below: **W A S D** to move, **mouse** to aim, **left mouse button** to shoot, **1** and **2** to pick a gun, **G** to throw a grenade, **R** to reset the targets.

## Basics Still Work

- [ ] Verify the game opens in the test arena with the player and two red target dummies visible.
- [ ] Verify W A S D moves the player, the camera keeps the player in view, and walls and obstacles stop the player.
- [ ] Verify the aim marker follows the mouse and stays visible while you move.
- [ ] Verify you can walk up to both dummies.
- [ ] Verify pressing R puts every dummy back to full health (its label reads `HP 75/75 Dmg 0/75`) and its normal red color.

## Switching Guns

- [ ] Verify the game starts with the instant-shot gun selected (bottom-left text reads `Weapon: DevHitscan`).
- [ ] Verify pressing 1 selects the instant-shot gun and pressing 2 selects the slow-bullet gun.
- [ ] Verify the bottom-left text changes right away to `Weapon: DevHitscan` or `Weapon: DevProjectile`.
- [ ] Verify the Output panel at the bottom of the Godot editor shows `[DevCombat] Active weapon switched to DevHitscan` or `... DevProjectile` when you really switch, and shows nothing new when you press the key for the gun you already hold.

## Instant-Shot Gun (DevHitscan)

- [ ] Verify a left click draws one red line from the player toward the mouse, and the line fades away quickly.
- [ ] Verify holding the button down fires only once. Each click is one shot.
- [ ] Verify clicking again within 0.25 seconds does nothing, and clicking after that fires.
- [ ] Verify a hit takes 25 health off a dummy and its label updates.
- [ ] Verify a dummy farther than 320 units away takes no damage, and the red line stops at 320 units.
- [ ] Verify the red line stops at a wall or obstacle.
- [ ] Verify hitting one dummy changes only that dummy. The other stays the same.

## Slow-Bullet Gun (DevProjectile)

- [ ] Verify a left click sends an orange diamond toward the mouse.
- [ ] Verify the orange diamond flies at a visible speed (560 units per second), not instantly.
- [ ] Verify clicking again within 0.6 seconds does nothing, and clicking after that fires.
- [ ] Verify a hit takes 40 health off a dummy and the orange diamond disappears.
- [ ] Verify the orange diamond disappears when it hits a wall or obstacle.  (works as intended)
- [ ] Verify an orange diamond that hits nothing disappears by itself after about 0.86 seconds, having flown about 480 units. (works as intended)
- [ ] Verify the orange diamond does not disappear on the player who fired it, and it does not hurt the player.

## Grenade

- [ ] Verify pressing G throws a green diamond toward the mouse. It flies a short way, then stops and waits.
- [ ] Verify there is no countdown number floating above the grenade.
- [ ] Verify the grenade blows up 1.5 seconds after you throw it, and then disappears.
- [ ] Verify a dummy within 96 units of the blast (measured to the middle of the dummy) takes damage, and a dummy farther away takes none.
- [ ] Verify the damage is biggest at the blast center (up to 60) and gets smaller toward the edge of the 96 units.
- [ ] Verify a blast near both dummies hurts each one by its own distance.
- [ ] Verify the player takes no damage and no other effect from their own grenade.
- [ ] Verify a second throw within 1.5 seconds of the last one does nothing, and a throw after that works.
- [ ] Verify throwing a grenade does not depend on which gun you hold, and shooting does not delay your next grenade.
- [ ] Verify a dummy's health never goes below 0 from any gun or grenade, and never shows a minus sign.

## Bottom-Left Display (HUD)

- [ ] Verify the display sits at the bottom-left of the game window and does not move when the player or camera moves.
- [ ] Verify it shows the gun name (`Weapon: DevHitscan` or `Weapon: DevProjectile`), a `Fire:` row, and a `Grenade:` row, each with a bar and a text value.
- [ ] Verify both text values say `READY` and both bars are empty when the game starts.
- [ ] Verify that after you shoot, the `Fire:` bar jumps to full and then drains while the number counts down (for example `0.18`) to `READY`. It should take 0.25 seconds with the instant-shot gun and 0.6 seconds with the slow-bullet gun.
- [ ] Verify that after you throw a grenade, the `Grenade:` bar jumps to full and then drains while the number counts down to `READY` over 1.5 seconds.
- [ ] Verify the display is only for looking. It has no buttons and changes nothing about how shooting works.
- [ ] Verify there is no gun-name label floating above the player.

## M3 Pass/Fail Check

- [ ] Verify you can knock out a dummy with the instant-shot gun (3 hits of 25 take it from 75 to 0).
- [ ] Verify you can knock out a dummy with the slow-bullet gun (2 hits of 40 take it to 0).
- [ ] Verify you can knock out a dummy with grenades (one grenade does at most 60, so you need two close ones).
- [ ] Verify a knocked-out dummy turns grey and its label reads `Inert 0/75 Dmg 75/75`, and pressing R brings it back.
- [ ] Verify the grenade blows up after 1.5 seconds and only hurts dummies within 96 units.
- [ ] Code check (ask a developer if you like): open `WeaponDef.cs` and `GrenadeDef.cs` and confirm the game takes its gun and grenade numbers from them, with no gun or grenade numbers typed directly into the other scripts. Known exceptions to decide on: grenade fly speed 420 and fly distance 220 (in `DevGrenadeView`), and the 28-unit gap in front of the player where shots and grenades appear (in `PlayerCombatController`) are typed into the game scripts instead. Write down whether you accept them; do not tick this box silently.

## Things That Must Not Exist Yet

- [ ] Verify the player has no health, cannot die, and does not respawn. Nothing damages the player, and no health number or death screen appears.
- [ ] Verify nothing needs the internet, a server, or an online account to shoot, throw, or see hit results.
- [ ] Verify all the temporary shapes (red line, orange and green diamonds, dummies, aim marker) are the plain test versions, and the bottom-left display shows only the gun name and the two wait times.
- [ ] Verify there is no sound, ammo count, inventory, score, or finished artwork.

## Where the numbers come from

For anyone checking the numbers against the code. Gun and grenade values are in `shared/Vantage.Shared/Game/`.

| Number in this list | Where it is defined |
|---|---|
| 320 units range, 25 damage, 0.25 s wait | `WeaponDef.DevHitscan` (`RangeUnits`, `Damage`, `FireIntervalSeconds`) |
| 480 units range, 40 damage, 0.6 s wait, 560 units per second | `WeaponDef.DevProjectile` (`RangeUnits`, `Damage`, `FireIntervalSeconds`, `ProjectileSpeedUnitsPerSecond`) |
| About 0.86 s flight time | Range 480 divided by speed 560 (`DevProjectileView.cs`) |
| 1.5 s fuse and throw wait, 96 units radius, 60 damage, falloff 1.0 | `GrenadeDef.DevPlaceholder` (`FuseSeconds`, `RadiusUnits`, `Damage`, `Falloff`) |
| 75 dummy health | `DevTargetDummy.MaxDebugHealth`, which is `WeaponDef.DevHitscan.Damage * 3` |
| 420 fly speed, 220 fly distance | `DevGrenadeView.ThrowSpeedUnitsPerSecond`, `DevGrenadeView.ThrowDistanceUnits` |
| 28-unit gap | `PlayerCombatController.DevMuzzleOffsetUnits` |
| Keys R, 1, 2, G, left mouse | `dev_reset`, `weapon_slot_1`, `weapon_slot_2`, `grenade_throw`, `primary_fire` in `client/project.godot` |


---

All manual checks done. If nothing is in the square it means passed. If something is in it, it means idk what it means or it didnt pass.