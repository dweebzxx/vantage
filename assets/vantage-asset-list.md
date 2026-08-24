## Assets to build for M5–M7

### Lock before M5 starts
| Asset | Spec | Format |
|---|---|---|
| Color palette | 16 or 32 colors, locked, referenced in every art prompt | Hex list in `docs/assets.md` |
| Pixel font | One pixel UI font + one fallback for body text | TTF or BMFont |

### Sprites and visual assets
| Asset | Spec | Format |
|---|---|---|
| Player base sprite | 8-direction or rotational base, 64x64, transparent | PNG |
| Two faction tints | Color palette swap rules, applied via shader | Godot ShaderMaterial |
| Weapon overlays | Held-weapon overlays, per gun, per direction | PNG |
| Bullet projectile | 8x8 sprite, small tracer | PNG |
| Grenade sprite + arc | 16x16 sprite, particle trail | PNG + Godot particles |
| Explosion sheet | 64x64 spritesheet, 8 frames | PNG |
| Hit flash | Simple damage-feedback effect | ShaderMaterial |
| Death | 32x32 spritesheet, 6 frames | PNG |
| Tileset for Arena01 | 32x32 tiles, 60–100 tiles for one arena's worth | PNG tilesheet, arranged into a `TileSet` resource manually |
| Collision tiles | Defined as part of the `TileSet` resource | Godot resource |
| Environment props | Crates, barrels, doors (decorative), 20 pieces | PNG |

### UI / menus
| Asset | Spec | Format |
|---|---|---|
| UI / HUD | Health bar, ammo counter, scoreboard, kill feed, faction indicator, minimap (optional) | PNG + Godot theme |
| Menus | Main menu background, button styles | PNG + Godot theme |

### Audio
| Asset | Spec | Format |
|---|---|---|
| Gunshot SFX | Per weapon | WAV, 44.1k mono |
| Grenade SFX | Prime and explode | WAV, 44.1k mono |
| Hit/death SFX | Hit flesh, hit wall, death | WAV, 44.1k mono |
| Footstep SFX | Movement audio cues | WAV, 44.1k mono |
| Reload SFX | Per weapon reload | WAV, 44.1k mono |
| Round win/lose SFX | Match-end stings | WAV, 44.1k mono |
| UI SFX | Clicks, score | WAV, 44.1k mono |
| Menu theme | Music | OGG |
| In-match loop | One track | OGG |
| Victory sting | Music | OGG |
| Defeat sting | Music | OGG |

### Infrastructure
| Item | Spec |
|---|---|
| Provenance manifest | `assets-source/provenance/manifest.json`, minimum fields: `asset_path`, `tool`, `prompt`, `generated_at`, `engine_path`, `commercial_clearance: false` |

### Hard blockers if late (must be real, not placeholder, by end of M5)
- Final player sprite (both factions)
- Final weapon overlays
- Tileset for Arena01
- Gunshot SFX (per weapon)
- Grenade SFX (prime, explode)
- Hit/death SFX
- Explosion sheet