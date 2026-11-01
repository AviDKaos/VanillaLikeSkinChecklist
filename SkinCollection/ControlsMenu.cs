using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Samples.RebindUI;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace SkinCollection
{
    /// <summary>Marks a row we added, and which game row it was cloned from.</summary>
    public class SkinCollectionRow : MonoBehaviour
    {
        public Transform Source;
    }

    /// <summary>
    /// Adds rows for the mod's actions to the game's Controls menu by cloning the journal (PlayerTab) row
    /// and pointing its RebindActionUI components at our actions. Rebinding, icons, the "waiting for input"
    /// overlay and reset all keep using the game's own code.
    /// </summary>
    public static class ControlsMenu
    {
        private const string TemplateAction = "PlayerTab";

        private static readonly FieldInfo ActionField = AccessTools.Field(typeof(RebindActionUI), "m_Action");
        private static readonly FieldInfo BindingIdField = AccessTools.Field(typeof(RebindActionUI), "m_BindingId");

        private static readonly HashSet<RebindActionUI> Pending = new HashSet<RebindActionUI>();
        // Row label and its Strings key, per cloned rebind control.
        private static readonly Dictionary<RebindActionUI, (TextMeshProUGUI Label, string Key)> Labels =
            new Dictionary<RebindActionUI, (TextMeshProUGUI, string)>();
        private static ModInput _input;
        private static bool _loggedLayout;

        private struct RowSpec
        {
            public string Label;
            public InputAction Action;
            public Guid KeyboardId;
            public Guid ControllerId;
        }

        public static void Init(ModInput input)
        {
            _input = input;
            Strings.LanguageChanged += OnLanguageChanged;
        }

        /// <summary>Called from RebindActionUI.OnEnable; the hierarchy can't be changed during activation, so defer.</summary>
        public static void Notice(RebindActionUI ui)
        {
            if (_input != null && ActionName(ui) == TemplateAction)
                Pending.Add(ui);
        }

        public static void ProcessPending()
        {
            if (Pending.Count == 0)
                return;
            var batch = Pending.ToList();
            Pending.Clear();

            foreach (var ui in batch)
            {
                try
                {
                    if (ui == null || ActionName(ui) != TemplateAction || ui.GetComponentInParent<SkinCollectionRow>(true) != null)
                        continue;
                    var row = FindRow(ui.transform);
                    if (row.parent == null)
                        continue;
                    var target = FindTargetColumn(row);
                    if (AlreadyInjected(target, row))
                        continue;
                    Inject(row, target);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Could not add Skin Collection rows to the Controls menu: {e}");
                }
            }
        }

        /// <summary>Largest ancestor of the template control that holds only template-action rebind buttons.</summary>
        private static Transform FindRow(Transform t)
        {
            var row = t;
            while (row.parent != null)
            {
                var uis = row.parent.GetComponentsInChildren<RebindActionUI>(true);
                if (uis.Any(u => ActionName(u) != TemplateAction))
                    break;
                row = row.parent;
            }
            return row;
        }

        /// <summary>
        /// The Controls panel splits rows into columns (LeftLayout / RightLayout) with a fixed height.
        /// Put our rows in the sibling column with the fewest rows, not necessarily the journal row's column.
        /// </summary>
        private static Transform FindTargetColumn(Transform row)
        {
            var column = row.parent;
            if (column.parent == null)
                return column;
            Transform best = column;
            int bestCount = RowCount(column);
            foreach (Transform sibling in column.parent)
            {
                if (sibling == column || sibling.GetComponent<LayoutGroup>() == null)
                    continue;
                int count = RowCount(sibling);
                if (count > 0 && count < bestCount)
                {
                    best = sibling;
                    bestCount = count;
                }
            }
            return best;
        }

        private static int RowCount(Transform column)
        {
            return column.Cast<Transform>().Count(c => c.GetComponentInChildren<RebindActionUI>(true) != null);
        }

        private static bool AlreadyInjected(Transform column, Transform row)
        {
            foreach (Transform child in column)
            {
                var marker = child.GetComponent<SkinCollectionRow>();
                if (marker != null && marker.Source == row)
                    return true;
            }
            return false;
        }

        private static void Inject(Transform row, Transform column)
        {
            if (!_loggedLayout)
            {
                _loggedLayout = true;
                LogHierarchy(row);
            }

            var specs = new[]
            {
                new RowSpec { Label = Strings.Title, Action = _input.Open, KeyboardId = ModInput.OpenKeyboardId, ControllerId = ModInput.OpenControllerId },
                new RowSpec { Label = Strings.ShowUnobtainableRow, Action = _input.ToggleUnobtainable, KeyboardId = ModInput.ToggleKeyboardId, ControllerId = ModInput.ToggleControllerId },
            };

            // Append after the column's last rebind row.
            var anchor = column.Cast<Transform>().LastOrDefault(c => c.GetComponentInChildren<RebindActionUI>(true) != null) ?? row;
            var clones = new List<Transform>();
            int index = anchor.parent == column ? anchor.GetSiblingIndex() : column.childCount - 1;
            foreach (var spec in specs)
            {
                var clone = UnityEngine.Object.Instantiate(row.gameObject, column, false);
                clone.name = "SkinCollection_" + spec.Action.name;
                clone.transform.SetSiblingIndex(++index);
                clone.AddComponent<SkinCollectionRow>().Source = row;
                Retarget(clone.transform, spec);
                clones.Add(clone.transform);
            }
            ChainNavigation(anchor, clones);
            Plugin.Log.LogInfo($"Added {clones.Count} Skin Collection rows to the Controls menu under '{column.name}'");
        }

        private static void Retarget(Transform clone, RowSpec spec)
        {
            var excluded = new HashSet<TextMeshProUGUI>();
            var overlays = new List<Transform>();
            TextMeshProUGUI label = null;
            foreach (var ui in clone.GetComponentsInChildren<RebindActionUI>(true))
            {
                // Decide keyboard vs controller from the binding it was showing for the journal.
                bool controller = ui.ResolveActionAndBinding(out var action, out int bindingIndex)
                                  && InputBinding.MaskByGroup(ModInput.ControllerGroup).Matches(action.bindings[bindingIndex]);
                ActionField.SetValue(ui, InputActionReference.Create(spec.Action));
                BindingIdField.SetValue(ui, (controller ? spec.ControllerId : spec.KeyboardId).ToString());
                ui.UpdateBindingDisplay();

                foreach (var text in new[] { ui.bindingText, ui.rebindPrompt, ui.rebindInfo })
                    if (text != null)
                        excluded.Add(text);
                if (ui.rebindOverlay != null)
                    overlays.Add(ui.rebindOverlay.transform);
                if (label == null && ui.actionLabel != null && ui.actionLabel.transform.IsChildOf(clone))
                    label = ui.actionLabel;
            }

            // Otherwise the first text in the row that isn't a binding/overlay text.
            if (label == null)
                label = clone.GetComponentsInChildren<TextMeshProUGUI>(true)
                    .FirstOrDefault(t => !excluded.Contains(t) && !overlays.Any(o => t.transform.IsChildOf(o)));
            if (label == null)
            {
                Plugin.Log.LogWarning($"No label found in the cloned Controls row for '{spec.Label}'");
                return;
            }
            var localize = label.GetComponent<LocalizeStringEvent>();
            if (localize != null)
                localize.enabled = false;
            label.text = Strings.Get(spec.Label);

            // Now that the label is known, let DecorateBinding add "(Hold)" to it where needed.
            foreach (var ui in clone.GetComponentsInChildren<RebindActionUI>(true))
            {
                Labels[ui] = (label, spec.Label);
                ui.UpdateBindingDisplay();
            }
        }

        /// <summary>
        /// Clones copy explicit navigation pointing at the template row's neighbours, which would make them
        /// unreachable with a controller. Chain anchor (the row above) → clones → anchor's old next row, and
        /// take left/right from the anchor so they lead to the right place in this column.
        /// </summary>
        private static void ChainNavigation(Transform anchor, List<Transform> clones)
        {
            var original = anchor.GetComponentsInChildren<Selectable>(true);
            var cloned = clones.Select(c => c.GetComponentsInChildren<Selectable>(true)).ToList();

            for (int i = 0; i < original.Length; i++)
            {
                var o = original[i];
                if (o.navigation.mode != Navigation.Mode.Explicit || cloned.Any(c => c.Length != original.Length))
                    continue;

                var oldDown = o.navigation.selectOnDown;
                Selectable previous = o;
                for (int k = 0; k < cloned.Count; k++)
                {
                    var current = cloned[k][i];
                    var nav = current.navigation;
                    nav.mode = Navigation.Mode.Explicit;
                    nav.selectOnUp = previous;
                    nav.selectOnDown = k + 1 < cloned.Count ? cloned[k + 1][i] : oldDown;
                    nav.selectOnLeft = MapWithinRow(o.navigation.selectOnLeft, original, cloned[k]);
                    nav.selectOnRight = MapWithinRow(o.navigation.selectOnRight, original, cloned[k]);
                    current.navigation = nav;

                    var prevNav = previous.navigation;
                    prevNav.selectOnDown = current;
                    previous.navigation = prevNav;
                    previous = current;
                }

                if (oldDown != null && oldDown.navigation.mode == Navigation.Mode.Explicit && oldDown.navigation.selectOnUp == o)
                {
                    var downNav = oldDown.navigation;
                    downNav.selectOnUp = previous;
                    oldDown.navigation = downNav;
                }
            }
        }

        private static Selectable MapWithinRow(Selectable target, Selectable[] original, Selectable[] clone)
        {
            int j = Array.IndexOf(original, target);
            return j >= 0 ? clone[j] : target;
        }

        /// <summary>Appends "(Hold)" to the row label when the row's binding has a hold interaction (controller default).</summary>
        public static void DecorateBinding(RebindActionUI ui)
        {
            if (_input == null || !Labels.TryGetValue(ui, out var entry) || entry.Label == null)
                return;
            var action = (ActionField.GetValue(ui) as InputActionReference)?.action;
            int index = action != null ? action.FindBindingById(ui.bindingId) : -1;
            bool hold = index >= 0 && ModInput.IsHold(action.bindings[index]);
            string text = Strings.Get(entry.Key);
            entry.Label.text = hold ? $"{text} <size=75%>{Strings.Get(Strings.HoldTag)}</size>" : text;
        }

        private static void OnLanguageChanged()
        {
            foreach (var ui in Labels.Keys.ToList())
            {
                if (ui == null)
                    Labels.Remove(ui);
                else
                    DecorateBinding(ui);
            }
        }

        private static string ActionName(RebindActionUI ui)
        {
            var reference = ActionField.GetValue(ui) as InputActionReference;
            return reference != null && reference.action != null ? reference.action.name : null;
        }

        private static void LogHierarchy(Transform row)
        {
            var sb = new StringBuilder($"Controls row template '{row.name}' (parent '{row.parent.name}'):\n");
            Append(sb, row, 0);
            Plugin.Log.LogInfo(sb.ToString());
        }

        private static void Append(StringBuilder sb, Transform t, int depth)
        {
            if (depth > 5)
                return;
            var comps = string.Join(", ", t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
            sb.Append(' ', depth * 2).Append(t.name).Append(" [").Append(comps).Append("]\n");
            foreach (Transform child in t)
                Append(sb, child, depth + 1);
        }
    }

    [HarmonyPatch(typeof(RebindActionUI), "OnEnable")]
    internal static class RebindActionUIOnEnablePatch
    {
        private static void Postfix(RebindActionUI __instance)
        {
            ControlsMenu.Notice(__instance);
        }
    }

    [HarmonyPatch(typeof(RebindActionUI), nameof(RebindActionUI.UpdateBindingDisplay))]
    internal static class RebindActionUIUpdateBindingDisplayPatch
    {
        private static void Postfix(RebindActionUI __instance)
        {
            ControlsMenu.DecorateBinding(__instance);
        }
    }
}
