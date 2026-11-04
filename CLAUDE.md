# How to Fish – Skin Collection Menu mod

Context handed over from a claude.ai chat (31 Oct 2026). Read this first.

## Goal

Build a **client-side BepInEx mod** for *How to Fish* (Dazed Games, Steam app 4001890) that adds a menu
showing which item skins the player **has** and **doesn't have**. It should look and feel like the game's
own **fish index** (the encyclopedia opened with **Tab**). Suggested hotkey: **K** (to avoid clashing with Tab).

Requirements:
- Read-only: never unlock, roll or change skins.
- Client-side only: reads the local player's own save. No networking and no host involvement.
  It must work in someone else's lobby.
- Covers every skinnable item, including the Bow added in the Islet update, plus the Boat.

## Game / tooling facts

- Unity, **Mono** backend. Networking uses **FishNet**.
- Current game version: **1.1.3**, Steam build **25604825**.
- Mod loader: **BepInEx 5** (Thunderstore dependency string `BepInEx-BepInExPack-5.4.2305`) + Harmony.
- Typical game folder: `C:\Program Files (x86)\Steam\steamapps\common\How to Fish\How to Fish\`
  - Game code: `How to Fish_Data\Managed\Assembly-CSharp.dll`
  - BepInEx core DLLs: `BepInEx\core\` (`BepInEx.dll`, `0Harmony.dll`)
- Reference DLLs other mods build against (copy from your own install, do not redistribute):
  `Assembly-CSharp.dll`, `FishNet.Runtime.dll`, `UnityEngine.CoreModule.dll`, `UnityEngine.dll`,
  `GameKit.Dependencies.dll`, `netstandard.dll` (all from `How to Fish_Data\Managed\`), plus `BepInEx.dll` and `0Harmony.dll`.
  UI work will also need `UnityEngine.UI.dll`, `Unity.TextMeshPro.dll`, `UnityEngine.InputLegacyModule.dll` or the Input System DLLs, depending on what the game uses.

## Status (31 Oct 2026)

- BepInEx 5.4.23.5 (win x64) installed in the game folder and verified loading. Console enabled in `BepInEx.cfg`.
  Game runs **Unity 6000.4.4**, Mono x64.
- Steps 1–2 done: reference DLLs are in `lib/` (gitignored). The metadata dump is in `decompiled/`.
  **`docs/game-api.md` supersedes the table below**: it confirms every row, fixes `Roll(..., byte rolled)`,
  names the skinnable-item list (`GameInfo.ItemWithSkinsforCommands`), the boat prefab and the fish index UI
  (`ThinkingUI` / `JournalSlot` / `PlayerThinking`). Both "Still unknown" notes below are now answered there.
- .NET SDK 10.0.403 installed at `C:\Program Files\dotnet`. `ilspycmd` 11.2 is a global tool; call it as
  `%USERPROFILE%\.dotnet\tools\ilspycmd.cmd`. Full decompile is in `decompiled/Assembly-CSharp/`.
- Step 3 done: `SkinCollection/` (netstandard2.1, GUID `avidkaos.howtofish.skincollection`). `dotnet build -c Release`
  copies the DLL to `BepInEx\plugins\SkinCollection\`.
- Step 4 done and **verified in-game**: `SkinCatalog.cs` finds 10 groups / 139 skins, matching the table below.
  It flags unobtainable skins (index ≠ 0 with `Rarity.Default`).
- Steps 5–6 **tested in-game and working** (v0.2.1). v0.2.2 adds: close hint shows the configured hotkey ("Close [K]")
  and the items are sorted in purchase order: Boat, Rod, Knuckles, Knife, Pistol, Shotgun, SMG, Sniper, AR, then Bow
  last (different currency). See `SkinCatalog.DisplayOrder`.
- v0.3.0 (not yet tested in-game) changes:
  - Skins are sorted Default → Common → Rare → Legendary → unobtainable.
  - Unobtainable skins are hidden by default and never count toward totals. An on-screen button (a copy of the close
    hint) and **U** toggle them, and the choice is saved in config `ShowUnobtainable`.
  - Fixed the "Local keyword … comes from a different shader" spam. `ApplyItemSkin(forBoat: true)` needs a material
    with `ShaderManager._boatShader`, so boat slots now use `Boat._skinRenderer`'s material. Items use the inventory
    material, checked against `_defaultShader`.
- v0.4.0 (not yet tested in-game) adds keybinds in the game's own Controls menu.
  - `ModInput.cs`: a separate `InputActionAsset` (the game's is never touched) with the actions `OpenSkinMenu`
    (K / no controller default, because every gamepad button is taken) and `ToggleUnobtainableSkins` (U / D-pad Up).
    D-pad Up is safe in-menu because WeaponInspect checks `BlockInputs`; Y/PlayerDrop is not, since `Boat.StopDrivingInput`
    ignores it. Bindings use the game's groups "Keyboard"/"Controller" and fixed binding GUIDs. Overrides are saved to
    config `Controls.BindingOverrides` on `BoundControlsChanged`. The old `Hotkey`/`ToggleUnobtainableKey` config is gone.
  - `ControlsMenu.cs`: a postfix on `RebindActionUI.OnEnable` spots the journal (`PlayerTab`) row and, next frame, clones
    it twice. It retargets each clone's `m_Action`/`m_BindingId` (keyboard vs controller decided from the template's binding
    group), relabels it and chains explicit controller navigation. The first injection logs the row hierarchy.
  - The game's rebinding data (from `sharedassets0.assets`) lists every Keyboard/Controller binding, if needed later.
- v0.4.1 (not yet tested in-game) changes:
  - Tested in v0.4.0: the rows appeared but overflowed. The Controls panel has fixed-height `LeftLayout`/`RightLayout`
    columns per scheme panel, and the journal row (`ThinkRebind`) is in the full left one. Our rows now go to the
    end of the sibling column with the fewest rows.
  - Controller default for the menu is **hold Select** (`hold(duration=0.5)`), chosen by the user. A tap opens the
    journal; holding switches journal → skins. `SkinMenu.Open` swaps in place when the journal is open
    (`HideJournalInstantly` cancels its LeanTween; firstpass is now referenced). A prefix on
    `PlayerThinking.ThinkingInput` makes Tab/Select in the skins switch back to the journal. The close hint shows
    Pause on a controller.
- v0.4.1 tested: the rows are in the right column and fit. Holding Select did nothing and logged NREs in
  `InputActionState.ProcessInteractions`. **BepInEx calls plugin `Awake` from inside `InputSystem`'s static constructor.**
  Enabling an action map there fails to create interactions ("Input System not yet initialized"). v0.4.2 creates
  `ModInput` in `Plugin.Start()`. It also appends "(Hold)" to our binding texts via a postfix on
  `RebindActionUI.UpdateBindingDisplay`.
- v0.4.3 (not yet tested in-game) changes:
  - "(Hold)" now goes on the row label, not inside the binding box.
  - Holding no longer triggers the button's game action. `ModInput.SyncTapOverrides` gives every game binding whose
    effectivePath matches one of our hold bindings an `overrideInteractions = "tap(duration=0.5)"`, keeping the player's
    overridePath. It re-syncs on any `BoundControlsChanged`. `RebindSaveLoad.Save` is patched to strip those taps first
    and mark them dirty after, so they never reach the game's PlayerPrefs; `Load` marks dirty. Limitation: game actions
    that poll the control (`IsPressed`/`ReadValue`) instead of using `performed` still react while held.
- v0.4.3 tested: the hold works and the logs are clean (only the game's own Steam-on-quit error). The user accepts
  that polled actions like the right trigger still fire while held. v0.4.4 adds `SkinMenu.CanUse()`, which blocks
  opening, and force-closes an open menu (unless paused), when:
  - `PlayerUI.UIDisabled` is set: cutscenes and credits; the intro crash cutscene also sets `MainMenuManager.IsInMenu = false`;
  - `EndGameUI`/`EndGameEffects.IsShowingEndGame` is set;
  - the game is loading (`LoadingManager.IsLoadingIn`, `IslandManager.IsLoading`);
  - `SelfDrivingBoat.IsDriving` (the menu/intro boat) is set.
  Dead is allowed, like the vanilla journal (`PlayerThinking.ThinkingInput` only checks pause). Tested: the menu doesn't
  open during cutscenes, even with the skip button.
- v0.5.0 (not yet tested in-game) adds `SkinHint.cs`, a one-time tip in the game's tutorial box.
  - Deliberately **not** inserted into the game's tutorial chain (`Tutorial.nextTutorial`). Its last step calls
    `Server.SendFinishedTutorial`, which is networked and saved by the host; inserting would also hide the tip from
    players who already finished.
  - It instantiates `PlayerTutorial._box` into `_tutorialHolder` itself, under an inactive staging parent so the
    LocalizeStringEvents can be disabled first.
  - It shows once `PlayerTutorial._tutorialToBox` has been empty for 5 s and `SkinMenu.CanUse()`. Use visible boxes,
    not `TutorialManager.ActiveTutorials`: the host's `SkipTutorial` RPC destroys the boxes but leaves the manager's set.
  - The text follows the current scheme ("Press [K]" / "Hold <Select icon>") via `onControlsChanged`.
  - `SkinMenu.Open` calls `Complete()`, which plays `TutorialBox.Finish()` and sets config `TutorialHint = false`.
- v0.5.0 tested: the tip shows once per install, as intended. Its finish animation played under the menu, which hides
  the HUD, so it was never seen. v0.5.1 sets the config at open but defers `Finish()` until the menu is closed and
  `PlayerThinking.IsThinking` is false.
- v0.5.1 tested and working visually, but it logged one NRE per frame in `TutorialBox.OnSizeShrink`. That method calls
  `LayoutRebuilder.ForceRebuildLayoutImmediate(_rectParent)`, and `_rectParent` is only set by `Initialize`, which we
  skip. v0.5.2 sets `_rectParent` to the holder via reflection. Tested: the logs are clean.
- v0.6.0 (not yet tested in-game) translates every mod text into the game's 16 locales.
  - The codes are en, zh-CN, zh-TW, fr, de, it, ja, ko, pl, pt-BR, ru, es-MX, es-ES, sv, tr, uk, read from `localization-locales`.
  - `Strings.cs` is a single table. `Strings.Get(key, args)` uses `LocalizationSettings.SelectedLocale`, falling back
    to the base language, then English. `Strings.LanguageChanged` refreshes the menu, the Controls labels and the tip.
  - Wording follows the game's official translations: "skin" as in "Change Weapon Skin" (pt-BR visual, es-MX
    aspecto, es-ES diseño, fr apparence, ru/uk образ, zh-TW 外觀, sv utseende, tr görünüm, pl skórka), and "Hold {0}" as
    in "Revive (Hold {0})". The boat name is `LocalizationManager.BoatLocalized`. Item names come from `Item.GetName()`
    (already localized). Skin names are plain strings in game data and are not localized by the game either.
  - CJK renders because `LocalizationManager` adds the CJK fonts as fallbacks of the default TMP font.
  - The tip follows the game's tutorial register per language (v0.6.1): fr "vous", ja casual 〜できる with no 。,
    ko "{0} 누르기로 … 있습니다" with no particle after the key, zh-TW "按下 {0}", sv "håll ner"; de/it/es/pt/pl/tr are
    informal, ru/uk use "вы".
  - v0.6.2 checked every other string against game references:
    - the counter copies the journal's "{n} / {total} Creatures" (noun capitalized, nominative);
    - `HoldTag` carries its own brackets and casing like the game's "(Hold {0})", with full-width （） for zh/ja;
    - control row labels follow the Input Table's verb form per language (uk uses nouns, de "anzeigen").
    - Note: the game's own "Close {0}" in zh-CN/zh-TW has no {0}, so the close hint shows no key there, same as the journal.
  - `tools/game_strings.py` (needs `pip install UnityPy`) prints the game's translations for English regexes from the
    string-table bundles in `StreamingAssets/aa/StandaloneWindows64`.
- Known game-side log errors, not caused by the mod: "Failed to load the Steam App List: 404" (the devs filter it in
  `ChatManager`) and "Steamworks is not initialized" from `SteamManager.LeaveLobby` when quitting. v0.2.0 cloned the `ThinkingUI` GameObject. That was
  wrong: it is `LocalUI`, which also holds VitalsUI/MoneyUI/WeaponUI/etc. The clones' startup code broke the HUD
  (enlarged health/hunger icons). **Never clone `LocalUI`.** `SkinMenu.cs` now clones only the visual parts,
  `ThinkingCanvas` (under `LocalCanvasHolder`) and `JournalCamera` (under `LocalPlayerHolder`). It retargets each cloned
  `JournalSlot._filter` to the camera clone and replicates the fade and cloud wobble itself, with its own RenderTexture. Each slot gets the item mesh plus
  `ShaderManager.ApplyItemSkin`, using the material from an inventory slot. While open it sets
  `PlayerThinking.IsThinking` via reflection, so the game blocks movement/camera/chat and Esc/Tab close it.
  `Patches.cs` reroutes `PlayerUI.ScrollThinkingPage` (A/D) to our pages. One page per item, split when skins > slots.
  Config: `Hotkey` (K), `MissingAsSilhouette`, `Debug.LogCatalogOnOpen`. The first open logs the clone's hierarchy.

### Step 8: packaging (done, 1.0.0)

- The version lives only in `<Version>` in `SkinCollection/SkinCollection.csproj`. A build target generates
  `PluginInfo.Version` for `Plugin.PluginVersion`. The package target fails if `thunderstore/manifest.json`'s
  `version_number` differs, so bump both.
- `dotnet build -c Release` (in `SkinCollection/`) also writes `dist/VanillaLikeSkinChecklist-<version>.zip`, which holds
  `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md` and `plugins/SkinCollection/SkinCollection.dll`. `dist/` is gitignored.
- `thunderstore/icon.png` is original art made by `tools/make_icon.py` (Pillow). Don't ship game art.
- **Published under the pseudonym `AviDKaos`. Never put the author's real name, personal GitHub or personal email in
  tracked files.** This repo has a repo-local git identity (`AviDKaos`, GitHub noreply email); the global identity is
  the author's personal one. To push, the remote URL must include the username
  (`https://AviDKaos@github.com/AviDKaos/<repo>.git`) so Git Credential Manager picks the right account; the personal
  account is the global default (`credential.https://github.com.username`).
- Thunderstore package name: **VanillaLikeSkinChecklist** (`ThunderstorePackageName` in the csproj, checked against the
  manifest). The in-game feature/menu title stays "Skin Collection" (translated), and the DLL and plugin folder stay
  `SkinCollection`. Repo: https://github.com/AviDKaos/VanillaLikeSkinChecklist (remote `origin` with the `AviDKaos@` URL).
  Published on Thunderstore as `AviDKaos-VanillaLikeSkinChecklist` 1.0.0 (2026-11-01). A first upload under a personal
  Thunderstore team is deprecated, with removal requested, and the personal GitHub repo was deleted.
- 1.1.0 (published 2026-11-01): per-skin island line from the slot machine pools (`SkinCatalog.SlotMachines`,
  see the slot machine section of `docs/game-api.md`), and `SkinMenu.SetText` shrinking labels that would wrap.
- The manual BepInEx install in the game folder was removed (2026-11-01); the user plays through r2modman. The Default
  profile holds released packages; test builds go into the **Dev** profile (shared with the other mods) by copying
  `bin/Release/SkinCollection.dll` over
  `%AppData%\r2modmanPlus-local\HowToFish\profiles\Dev\BepInEx\plugins\AviDKaos-VanillaLikeSkinChecklist\SkinCollection\`,
  and the user launches the Dev profile. Afterwards, reinstall the package in r2modman to go back to the release.
  `CopyToPlugins` only runs when the game folder has a `BepInEx` folder, so builds don't recreate it.
- Since 2026-11-03 each mod has its own folder and Claude session. This repo's folder was renamed from `HowToFishMod`;
  the other mod is `..\SlotMachineSkinNames`. Commits carry no Claude attribution lines (the trailer was stripped from
  the history on 2026-11-03).
- Upload is manual, by the user: thunderstore.io → How to Fish community → Upload, under their own team (the team
  name becomes the package namespace). Check that the dependency `BepInEx-BepInExPack-5.4.2305` is still the current
  pack there.

## Known game API

**Confirmed** = read from the compiled IL of the *Fresh Wheel Spins* mod (Estonia, v1.0.0), which calls these.
**Reported** = from another mod's README; not verified in IL.

| What | Name | Status |
|---|---|---|
| Local save object | `SaveManager._curLocalSave` (private static field, type `LocalSaveObject`). Read via `AccessTools.Field(typeof(SaveManager), "_curLocalSave").GetValue(null)` | Confirmed |
| Unlocked item skins | `LocalSaveObject.UnlockedItemSkins` – enumerable of `SavedItemSkin` | Confirmed |
| One item's unlocks | `SavedItemSkin.ID` (item id, a `byte`) and `SavedItemSkin.SkinsUnlocked` (collection of skin indexes; supports `Contains`) | Confirmed |
| Unlocked boat skins | `LocalSaveObject.UnlockedBoatSkins` (separate collection; supports `Contains`) | Confirmed |
| Item lookup | `GameInfo.IDToItem(byte id)` returns `Item` | Confirmed |
| Item fields | `Item.ID` (byte), `Item.SkinPreset` | Confirmed |
| Skin list | `SkinPreset.Skins` – indexable list of `ItemSkin` (a struct) | Confirmed |
| Skin rarity | `ItemSkin.Rarity` (enum `Rarity`) | Confirmed |
| Boat skins | `BoatManager.Boat.SkinPreset` | Confirmed |
| Slot machine | `SlotMachine.AvailableItems` (list of `Item` this machine can roll) | Confirmed |
| Roll | `SlotMachine.Roll(Player roller, byte[] itemIDs, byte[] itemSkins, int rolled)` (signature inferred from Harmony prefix parameter names) | Confirmed names |
| Local-player check | `roller.Owner.IsLocalClient` (`Player` is a FishNet `NetworkBehaviour`) | Confirmed |
| Unlock on finish | `SlotMachine.OnRollFinished()` → `SaveManager.UnlockSkin(itemID, skinIndex)` when `_playerWhoRolled.Owner.IsLocalClient` | Reported (FairShare README) |
| Save file | `local.txt` in the save folder, JSON-like: `"UnlockedItemSkins":[{"ID":57,"SkinsUnlocked":[1,2,3]}]` | Reported (guide) |

Notes:
- Skin index 0 appears to be the default skin. The guide says the default is not stored in `SkinsUnlocked`. Treat index 0 as always owned and verify.
- Community-reported item IDs: Fishing Rod 61, Knife 63, Pistol 66. Prefer enumerating from game data over hardcoding.
- **Still unknown:** how to enumerate *all* items that have a `SkinPreset`. Options: search `GameInfo` for an item list, or use `Resources.FindObjectsOfTypeAll<SkinPreset>()` / `<Item>()`. The *Skin Checklist* mod (PonyFisher) says it auto-discovers every item with a skin preset and merges internal prefabs with the same item name into one tab, so duplicates by name exist.
- **Still unknown:** the class and UI prefab for the Tab fish index. Decompile `Assembly-CSharp.dll` (e.g. `ilspycmd`, dnSpy or ILSpy) and search for "Index", "Encyclopedia", "Journal", "Catalog", "Tab".

## Expected skin counts (patch 1.1.3, totals include the default skin)

Use these to sanity-check what the mod discovers. Source: yueue's data-mined list, https://yuexr.github.io/howtofish/skins.

| Item | Skins | Not obtainable from slot machine |
|---|---|---|
| SMG | 16 | – |
| Sniper Rifle | 16 | – |
| Knife | 15 | – |
| Assault Rifle | 14 | Orange, Blue |
| Boat | 14 | Fire |
| Fishing Rod | 14 | Gold (removed from drops in 1.1.3) |
| Pistol | 14 | Missing |
| Shotgun | 13 | – |
| Bow | 12 | – (drops only at the Mini1 islet machine) |
| Brass Knuckles | 11 | – |

Total 139 = 10 defaults + 5 unobtainable + 124 winnable. Rarities: Common, Rare, Legendary (spin odds 70/20/10).
The menu could mark unobtainable skins differently so players don't chase them.

## Related mods (prior art)

- **Skin Checklist** (PonyFisher, Thunderstore): already does an owned/missing skin list from a gold button in the pause menu, with tabs, search, filters and apply buttons. Client-side. Our mod differs by matching the fish index style and opening from a hotkey.
- **Fresh Wheel Spins** (Estonia): Harmony prefix on `SlotMachine.Roll`; replaces duplicate rolls with locked skins, only when `roller.Owner.IsLocalClient`, using the local save. Most likely client-side (not 100% confirmed).
- **FairShare** (LicenseX): postfix on `SlotMachine.OnRollFinished`; shares unlocked skins with the lobby via a FishNet broadcast. Its README lists the reference DLLs above.

## Suggested plan

1. Ask the user for their game folder path. Copy the reference DLLs into `lib/` (gitignored).
2. Decompile `Assembly-CSharp.dll`. Confirm the API table above, find the full item list, and find the fish index UI classes.
3. Scaffold a BepInEx 5 plugin project (target `netstandard2.1` or whatever the game's Managed folder supports), with GUID like `yourname.howtofish.skincollection`.
4. Data layer: build `[{ item name, item id, skins: [{ index, name, rarity, owned }] }]` from `SkinPreset` + `_curLocalSave`, including the boat.
5. UI layer: clone or reuse the fish index's UI style (preferred) or fall back to a clean IMGUI/uGUI window. One tab per item, owned skins bright and missing ones greyed, with per-item and overall counts.
6. Hotkey K toggles the menu. Refresh the data each time it opens.
7. Test in single player and as a client in another lobby. Check `BepInEx/LogOutput.log`.
8. Package for Thunderstore: `manifest.json`, `README.md`, `icon.png` (256×256), `plugins/<ModName>/<ModName>.dll`.
