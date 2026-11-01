using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SkinCollection
{
    public sealed class SkinEntry
    {
        public byte Index;
        public string Name;
        public Rarity Rarity;
        public bool Owned;

        /// <summary>Non-default skins tagged Rarity.Default can't be won from the slot machine (verified for 1.1.3).</summary>
        public bool Unobtainable => Index != 0 && Rarity == Rarity.Default;
    }

    public sealed class SkinGroup
    {
        public string Name;
        public byte ItemId;
        public bool IsBoat;
        /// <summary>Default first, then Common, Rare, Legendary, then unobtainable; game index within each.</summary>
        public List<SkinEntry> Skins = new List<SkinEntry>();

        // Unobtainable skins never count toward progress, even when shown.
        public int OwnedCount => Skins.Count(s => s.Owned && !s.Unobtainable);
        public int TotalCount => Skins.Count(s => !s.Unobtainable);
    }

    /// <summary>Read-only view of which skins the local player owns. Never writes to the save.</summary>
    public static class SkinCatalog
    {
        /// <summary>SaveManager.UnlockSkin treats item id 255 as the boat.</summary>
        public const byte BoatItemId = byte.MaxValue;

        /// <summary>
        /// Menu order: the order items are bought in-game (by price), Bow last since it costs a different currency.
        /// Items not listed here (e.g. added by a future update) go at the end.
        /// </summary>
        private static readonly byte[] DisplayOrder =
        {
            BoatItemId, // Boat
            61,         // Fishing Rod
            57,         // Brass Knuckles
            63,         // Knife
            66,         // Pistol
            68,         // Shotgun
            69,         // SMG
            70,         // Sniper Rifle
            54,         // Assault Rifle
            86,         // Bow
        };

        private static readonly FieldInfo CurLocalSaveField = AccessTools.Field(typeof(SaveManager), "_curLocalSave");
        private static readonly FieldInfo BoatPrefabField = AccessTools.Field(typeof(SpawnManager), "_boatPrefab");

        public static SaveManager.LocalSaveObject GetLocalSave()
        {
            return CurLocalSaveField?.GetValue(null) as SaveManager.LocalSaveObject;
        }

        public static List<SkinGroup> Build()
        {
            var save = GetLocalSave();
            var groups = new List<SkinGroup>();

            Item[] items = null;
            try
            {
                items = GameInfo.ItemWithSkinsforCommands;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"GameInfo.ItemWithSkinsforCommands unavailable: {e.Message}");
            }

            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null || item.SkinPreset == null)
                        continue;

                    var unlocked = save?.UnlockedItemSkins?.FirstOrDefault(s => s.ID == item.ID)?.SkinsUnlocked;
                    groups.Add(MakeGroup(GetItemName(item), item.ID, false, item.SkinPreset, unlocked));
                }
            }

            var boat = FindBoat();
            if (boat != null && boat.SkinPreset != null)
                groups.Add(MakeGroup(GetBoatName(), BoatItemId, true, boat.SkinPreset, save?.UnlockedBoatSkins));

            // OrderBy is stable, so unlisted items keep the game's order after the listed ones.
            return groups.OrderBy(g =>
            {
                int i = Array.IndexOf(DisplayOrder, g.ItemId);
                return i < 0 ? int.MaxValue : i;
            }).ToList();
        }

        private static SkinGroup MakeGroup(string name, byte itemId, bool isBoat, SkinPreset preset, ICollection<byte> unlocked)
        {
            var group = new SkinGroup { Name = name, ItemId = itemId, IsBoat = isBoat };
            var skins = preset.Skins;
            for (int i = 0; i < skins.Count; i++)
            {
                var skin = skins[i];
                group.Skins.Add(new SkinEntry
                {
                    Index = (byte)i,
                    Name = skin.Name,
                    Rarity = skin.Rarity,
                    // Index 0 is the default skin and is not stored in the save.
                    Owned = i == 0 || (unlocked != null && unlocked.Contains((byte)i)),
                });
            }
            group.Skins = group.Skins.OrderBy(RarityOrder).ThenBy(s => s.Index).ToList();
            return group;
        }

        private static int RarityOrder(SkinEntry skin)
        {
            if (skin.Index == 0)
                return 0;
            switch (skin.Rarity)
            {
                case Rarity.Common: return 1;
                case Rarity.Rare: return 2;
                case Rarity.Legendary: return 3;
                default: return 4; // unobtainable
            }
        }

        private static string GetItemName(Item item)
        {
            try
            {
                var name = item.GetName();
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
            catch (Exception)
            {
                // Localization may not be ready yet; fall back to the prefab name.
            }
            return item.name;
        }

        /// <summary>The game's own localized "Boat" (as listed in the slot machine's prizes).</summary>
        private static string GetBoatName()
        {
            try
            {
                var name = LocalizationManager.BoatLocalized?.GetLocalizedString();
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
            catch (Exception)
            {
                // Localization not ready
            }
            return "Boat";
        }

        public static Boat FindBoat()
        {
            if (BoatManager.Boat != null)
                return BoatManager.Boat;

            var spawnManager = UnityEngine.Object.FindAnyObjectByType<SpawnManager>();
            if (spawnManager != null && BoatPrefabField?.GetValue(spawnManager) is Boat prefab && prefab != null)
                return prefab;

            return Resources.FindObjectsOfTypeAll<Boat>().FirstOrDefault();
        }

        public static void LogSummary(ManualLogSource log)
        {
            var save = GetLocalSave();
            if (save == null)
                log.LogWarning("Local save not loaded (SaveManager._curLocalSave is null)");
            else
                log.LogInfo("Raw save: " + string.Join(" | ", save.UnlockedItemSkins.Select(s => $"{s.ID}:[{string.Join(",", s.SkinsUnlocked)}]"))
                            + $" | boat:[{string.Join(",", save.UnlockedBoatSkins)}]");

            var groups = Build();
            int owned = 0, total = 0;
            foreach (var g in groups)
            {
                owned += g.OwnedCount;
                total += g.TotalCount;
                log.LogInfo($"{g.Name} (id {g.ItemId}{(g.IsBoat ? ", boat" : "")}): {g.OwnedCount}/{g.TotalCount}");
                foreach (var s in g.Skins)
                    log.LogInfo($"    [{(s.Owned ? "x" : " ")}] {s.Index,2} {s.Name} ({s.Rarity}){(s.Unobtainable ? " - unobtainable" : "")}");
            }
            log.LogInfo($"Total: {owned}/{total} obtainable skins in {groups.Count} groups");
        }
    }
}
