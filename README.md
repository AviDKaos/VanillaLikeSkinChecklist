# Skin Collection

See which weapon and boat skins you already own and which ones you're still missing, in a menu that looks and feels like the game's own fish journal.

- **Open it with `K`**, or **hold Select** on a controller.
- One page per item: Boat, Fishing Rod, Brass Knuckles, Knife, Pistol, Shotgun, SMG, Sniper Rifle, Assault Rifle and Bow.
- Every skin is shown on the item's 3D model. Skins you own are in full color, and missing ones are black silhouettes, like fish you haven't caught yet.
- Skins are sorted Default → Common → Rare → Legendary, with rarity shown in the game's own colors.
- Per-item and overall counters turn orange once complete.
- Skins the slot machine can't give are hidden by default and **never count toward your totals**. Toggle them with `U` / D-pad Up or the on-screen button.

## Controls

| Action | Keyboard | Controller |
|---|---|---|
| Open / close the skin menu | `K` | Hold **Select** |
| Next / previous page | `A` / `D` or the on-screen arrows | Left stick |
| Show / hide unobtainable skins | `U` or the on-screen button | D-pad Up |
| Close | `Esc` | Start |

Both bindings can be changed in the game's own **Options → Controls** menu, where they appear as extra rows next to the vanilla ones.

On a controller, a tap on Select still opens the fish journal. Holding it switches to the skins instead, without triggering the journal or the button's normal action. From the skin menu, `Tab` / Select goes back to the journal.

## Safe to use anywhere

- **Read-only:** it only reads your local save. It never unlocks, rolls or changes skins.
- **Client-side:** there's no networking and nothing is required from the host, so it works in anyone's lobby, modded or not.
- **Leaves no trace:** your rebinds are stored in the mod's own config, not in the game's settings, so uninstalling restores everything as it was.
- **Plays by the game's rules:** it can't be opened during cutscenes, credits or loading screens, or wherever the fish journal is unavailable.

## Languages

All of the mod's text follows the game's language setting and is available in every language the game supports: English, Français, Deutsch, Italiano, Español (España / México), Português (Brasil), Polski, Русский, Українська, Svenska, Türkçe, 日本語, 한국어, 简体中文 and 繁體中文. Wording follows the game's own translations, so a skin is a "visual" in Brazilian Portuguese and an "apparence" in French, for example.

## First-time tip

The first time you play with the mod, a tip in the game's tutorial style explains how to open the menu. It appears once the vanilla tutorial is out of the way, even on saves where you finished it long ago. It completes the first time you open the menu and never shows up again.

## Configuration

`BepInEx/config/avidkaos.howtofish.skincollection.cfg`:

| Setting | Default | What it does |
|---|---|---|
| `MissingAsSilhouette` | `true` | Missing skins as black silhouettes, or dimmed when `false`. |
| `ShowUnobtainable` | `false` | Remembered from the in-menu toggle. |
| `TutorialHint` | `true` | Set back to `true` to see the first-time tip again. |
| `BindingOverrides` | *(empty)* | Your rebinds from the Controls menu. Clear it to reset them. |

## Compatibility

Built for How to Fish **1.1.3** with BepInExPack 5.4.2305.