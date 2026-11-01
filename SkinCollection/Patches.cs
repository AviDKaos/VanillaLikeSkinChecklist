using HarmonyLib;
using UnityEngine.InputSystem.Samples.RebindUI;

namespace SkinCollection
{
    /// <summary>
    /// The game saves every override on its asset to PlayerPrefs. Strip our temporary tap interactions first
    /// and restore them afterwards, so the game's saved bindings stay untouched (also if the mod is removed).
    /// </summary>
    [HarmonyPatch(typeof(RebindSaveLoad), nameof(RebindSaveLoad.Save))]
    internal static class RebindSaveLoadSavePatch
    {
        private static void Prefix(RebindSaveLoad __instance)
        {
            ModInput.Instance?.StripTapOverrides(__instance.actions);
        }

        private static void Postfix()
        {
            ModInput.Instance?.MarkDirty();
        }
    }

    [HarmonyPatch(typeof(RebindSaveLoad), nameof(RebindSaveLoad.Load))]
    internal static class RebindSaveLoadLoadPatch
    {
        // Loading replaces all overrides, ours included; put the taps back next frame.
        private static void Postfix()
        {
            ModInput.Instance?.MarkDirty();
        }
    }

    [HarmonyPatch(typeof(PlayerUI), nameof(PlayerUI.ScrollThinkingPage))]
    internal static class ScrollThinkingPagePatch
    {
        // A/D (PlayerMove) pages the journal while "thinking"; send it to our menu instead when it is open.
        private static bool Prefix(int input)
        {
            if (!SkinMenu.IsOpen || SkinMenu.Instance == null)
                return true;
            SkinMenu.Instance.Scroll(input);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerThinking), "ThinkingInput")]
    internal static class ThinkingInputPatch
    {
        // Tab / Select while the skins are open goes to the journal instead of closing everything.
        private static bool Prefix()
        {
            if (!SkinMenu.IsOpen || SkinMenu.Instance == null || PauseManager.IsPaused)
                return true;
            SkinMenu.Instance.SwitchToJournal();
            return false;
        }
    }
}
