# How to Fish – game API findings

Source: metadata of `Assembly-CSharp.dll` (game 1.1.3, Unity **6000.4.4**, Mono x64), dumped with
`tools/dump_meta.py` on 31 Oct 2026. This covers type, field and method **signatures** only, not method
bodies. The full decompiled source (ilspycmd 11.2) is in `decompiled/Assembly-CSharp/` and is gitignored.
Regenerate it with:
`ilspycmd -p -o decompiled/Assembly-CSharp -r "<game>/How to Fish_Data/Managed" lib/Assembly-CSharp.dll`.

Regenerate the dump:

```bash
python -m venv .venv && .venv/Scripts/python -m pip install dnfile
.venv/Scripts/python -I tools/dump_meta.py lib/Assembly-CSharp.dll decompiled/acs.txt
```

## Save data (all confirmed)

```csharp
class SaveManager : MonoBehaviour {
    private static LocalSaveObject _curLocalSave;        // nested type: SaveManager+LocalSaveObject
    public static bool HasLocalSave { get; }
    public static void UnlockSkin(byte itemID, byte skinIndex);   // never call (read-only mod)
    public static bool HasSkin(byte itemID);
    public static byte GetSkin(byte itemID, byte curSkin, bool right);
    public static void LockAllSkins();                            // never call
}
class SaveManager.LocalSaveObject {
    public List<SavedItemSkin> UnlockedItemSkins;
    public List<byte> UnlockedBoatSkins;
    // also: LocalHat/Outfit/Accessory colors+indexes, SeenHats/Outfits/Accesories, Creatures, SavedLanguage
}
class SavedItemSkin { public byte ID; public List<byte> SkinsUnlocked; }
```

Read with `AccessTools.Field(typeof(SaveManager), "_curLocalSave").GetValue(null)`. The nested type is
public, so it can be referenced as `SaveManager.LocalSaveObject` directly.

## Items and skins (all confirmed)

```csharp
class GameInfo : MonoBehaviour {
    public static Item[] ItemWithSkinsforCommands { get; }   // <- the list of skinnable items (field _itemsWithSkinsForCommands)
    private static Dictionary<byte, Item> _allItems;
    public static Item IDToItem(byte id);
    public static Color CommonColor, RareColor, LegendaryColor { get; }  // the game's own rarity colors
    public static RenderTexture JournalTexture { get; }
}
class Item : FishNet.Object.NetworkBehaviour {
    public byte ID { get; }
    public SkinPreset SkinPreset { get; }
    public byte CurSkin { get; }
    public virtual string GetName();          // localized (field LocalizedString _nameLocalized)
}
class SkinPreset : ScriptableObject { public IReadOnlyList<ItemSkin> Skins { get; } }
struct ItemSkin { public string Name; public Rarity Rarity; public bool UseSkin; public bool IsRainbowSkin; /* + shader params */ }
enum Rarity { Default, Common, Rare, Legendary }   // Default is presumably skin index 0
```

**Verified in-game (31 Oct 2026):** `ItemWithSkinsforCommands` returns exactly the 9 skinnable items with no
duplicates: Assault Rifle 54, Brass Knuckles 57, Fishing Rod 61, Knife 63, Pistol 66, Shotgun 68, SMG 69,
Sniper Rifle 70, Bow 86. Add the Boat (255) and the total is 139 skins, matching the expected table.
- Index 0 has `Rarity.Default` and is never stored in `SkinsUnlocked`.
- **Unobtainable skins are `Rarity.Default` at index ≠ 0.** That is exactly the five known ones: AR Orange/Blue,
  Rod Gold, Pistol Missing, Boat Fire. No hardcoded list needed.

## Boat (confirmed)

```csharp
class Boat : NetworkBehaviour { public SkinPreset SkinPreset { get; } public byte CurSkin { get; } }
class BoatManager : NetworkBehaviour { public static Boat Boat { get; } }   // current spawned boat; can be null
class SpawnManager : MonoBehaviour { private Boat _boatPrefab; }           // no static instance
```

In `SaveManager.UnlockSkin`/`HasSkin` the boat is item id **255** (`byte.MaxValue`). Its skins go into `UnlockedBoatSkins`.

`BoatManager.Boat` is null before a boat is spawned. As a fallback use `SpawnManager._boatPrefab` (via
`FindObjectOfType<SpawnManager>()` and reflection) or `Resources.FindObjectsOfTypeAll<Boat>()`.

## Slot machine (corrections vs CLAUDE.md)

```csharp
class SlotMachine : MonoBehaviour {
    public static Item[] AvailableItems { get; }      // Item[], not List
    public static bool ExcludeBoat { get; }
    public static bool IsRolling { get; }
    public static void Roll(Player roller, byte[] itemIDs, byte[] itemSkins, byte rolled);   // rolled is byte, not int
    private void OnRollFinished();
    private static Player _playerWhoRolled;
}
```

## Fish index UI = "Thinking" / Journal

The Tab fish index is a thought-bubble journal:

```csharp
class PlayerThinking : MonoBehaviour {        // input side, one per local player
    public static bool IsThinking { get; }
    static event Action OnThinking;
    private void ThinkingInput(CallbackContext);  ExitThinkingInput(...);  ScrollJournalInput(...);
    private void ToggleThinking(bool to);
}
class PlayerUI : MonoBehaviour {
    private static PlayerUI _instance;
    private ThinkingUI _thinkingUI;
    public static void OnToggleThinking();
    public static void ScrollThinkingPage(int input);
    public static bool UIDisabled { get; }
}
class ThinkingUI : MonoBehaviour {            // the visual journal
    CanvasGroup _thinkingCanvas, _thinkingHolderGroup;
    TextMeshProUGUI _progressText, _dripProgressText, _dripChanceText;
    Camera _journalCamera;                    // renders 3D creature meshes into GameInfo.JournalTexture
    List<JournalSlot> _slots;                 // fixed number of slots per page; _curPage paginates
    List<Transform> _cloudlyTransforms;       // cloud-bubble wobble animation (AnimationCurves)
    LocalizeStringEvent _closeEvent; InputActionReference _closeInput;
    public void ToggleThinking(); public void ScrollThinkingPage(int input);
}
class JournalSlot : MonoBehaviour {
    TextMeshProUGUI _nameText, _worthText, _baitText;
    MeshFilter _filter;  RawImage _image;  GameObject _slotToScale;  DripText _dripText;
    public void SetCreature(Creature c); public void ShowSlot();
}
```

UI approach to try: at runtime, `Instantiate` the `ThinkingUI` GameObject (from `PlayerUI._instance._thinkingUI`),
strip its `ThinkingUI` component, and reuse its canvas, cloud background, fonts and `JournalSlot` layout.
Fill the slots with item meshes plus skin names and rarity colors. A slot shows a 3D mesh through
`_journalCamera`, so showing a skin means applying the skin to that mesh (see `Item.ApplySkin(ItemSkin)`,
which is private).

## Input

The game uses the **new Input System** (`PlayerInput`, `InputActionReference`). For the K hotkey use
`UnityEngine.InputSystem.Keyboard.current.kKey.wasPressedThisFrame` rather than legacy `UnityEngine.Input`,
which may be disabled. Skip the hotkey while `PlayerThinking.IsThinking`, while the game is paused or while
a text field has focus.

## Slot machines (where skins drop)

Each island scene has one `SlotMachine` whose serialized `_availableItems` (Item prefabs) and `_excludeBoat`
make up its prize pool. `SlotMachineManager.RollRandom` fills 10 slots, each with a random collection from
that pool (the boat counts as one collection unless excluded). Seven slots get `GetRandomSkinIndex(Common)`, two
get `Rare` and one gets `Legendary`, and then a random slot wins. That makes 70 / 20 / 10 % per rarity, then an
even split across collections and across skins of that rarity. Skins tagged `Rarity.Default` never drop.

Only the current island's scene is loaded, so the pools are hard-coded in `SkinCatalog.SlotMachines`.
Re-extract them with `tools/slot_pools.py` after game updates. Build order: 0 Game, 1–5 Island1–5,
6 DevIsland, 7–11 Mini1–5.

| Scene | Prizes | Boat |
|---|---|---|
| Island1 | Brass Knuckles, Knife | no |
| Island2 | Knife, Pistol, Fishing Rod, Shotgun | yes |
| Island3 | Pistol, SMG, Fishing Rod, Shotgun | yes |
| Island4 | Fishing Rod, SMG, Sniper Rifle | yes |
| Island5 | Fishing Rod, Assault Rifle, Sniper Rifle | yes |
| Mini1 (sells the Bow) | Bow | no |
| DevIsland (not reachable) | Brass Knuckles, Knife, Pistol | yes |
