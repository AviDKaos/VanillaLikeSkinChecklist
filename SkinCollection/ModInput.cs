using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SkinCollection
{
    /// <summary>
    /// The mod's own input actions, kept in a separate asset so the game's PlayerInput asset is never modified.
    /// Bindings use the game's control scheme groups ("Keyboard" / "Controller") so the game's rebinding rows
    /// and SpriteManager icons treat them like native actions. Overrides are stored in the mod's config.
    /// </summary>
    public sealed class ModInput
    {
        public const string KeyboardGroup = "Keyboard";
        public const string ControllerGroup = "Controller";

        // Fixed ids so saved overrides keep matching the same bindings across sessions.
        public static readonly Guid OpenKeyboardId = new Guid("5b1f0c1e-6a43-4f7e-9d0a-1c2e3f405161");
        public static readonly Guid OpenControllerId = new Guid("5b1f0c1e-6a43-4f7e-9d0a-1c2e3f405162");
        public static readonly Guid ToggleKeyboardId = new Guid("5b1f0c1e-6a43-4f7e-9d0a-1c2e3f405163");
        public static readonly Guid ToggleControllerId = new Guid("5b1f0c1e-6a43-4f7e-9d0a-1c2e3f405164");

        public InputAction Open { get; }
        public InputAction ToggleUnobtainable { get; }

        private const string HoldInteraction = "hold(duration=0.5)";
        // Given to game bindings on the same control as one of our holds: the game action then only fires
        // on a quick release, so holding opens the skins without also doing the button's normal action.
        private const string TapInteraction = "tap(duration=0.5)";

        public static ModInput Instance { get; private set; }

        private readonly InputActionAsset _asset;
        private readonly InputActionMap _map;
        private readonly ConfigEntry<string> _overrides;
        private bool _tapDirty = true;

        public ModInput(ConfigEntry<string> overrides)
        {
            Instance = this;
            _overrides = overrides;
            _asset = ScriptableObject.CreateInstance<InputActionAsset>();
            _asset.name = "SkinCollectionInput";
            UnityEngine.Object.DontDestroyOnLoad(_asset);
            _map = new InputActionMap("SkinCollection");
            _asset.AddActionMap(_map);

            Open = _map.AddAction("OpenSkinMenu", InputActionType.Button);
            AddBinding(Open, OpenKeyboardId, "<Keyboard>/k", KeyboardGroup);
            // Every gamepad button already has a game action. Tapping Select opens the journal; holding it
            // switches from the journal to the skins. Rebinding keeps the hold (only the path is overridden).
            AddBinding(Open, OpenControllerId, "<Gamepad>/select", ControllerGroup, HoldInteraction);

            ToggleUnobtainable = _map.AddAction("ToggleUnobtainableSkins", InputActionType.Button);
            AddBinding(ToggleUnobtainable, ToggleKeyboardId, "<Keyboard>/u", KeyboardGroup);
            // Only used inside the menu, where the game blocks its own D-pad Up action (weapon inspect).
            AddBinding(ToggleUnobtainable, ToggleControllerId, "<Gamepad>/dpad/up", ControllerGroup);

            Load();
            InputSystem.onActionChange += OnActionChange;
            _map.Enable();
        }

        private void AddBinding(InputAction action, Guid id, string path, string group, string interactions = null)
        {
            _map.AddBinding(new InputBinding { id = id, path = path, groups = group, action = action.name, interactions = interactions });
        }

        /// <summary>The binding for the current control scheme as the game would show it ("[K]" or a gamepad icon).</summary>
        public static string Display(InputAction action)
        {
            try
            {
                return SpriteManager.InputToSpriteName(action) ?? "";
            }
            catch (Exception)
            {
                return action.GetBindingDisplayString();
            }
        }

        private void Load()
        {
            if (string.IsNullOrEmpty(_overrides.Value))
                return;
            try
            {
                _asset.LoadBindingOverridesFromJson(_overrides.Value);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Ignoring invalid saved bindings: {e.Message}");
            }
        }

        private void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.BoundControlsChanged)
                return;
            // Any rebind (ours or the game's) may change which game bindings share a control with our holds.
            // Re-sync next frame rather than from inside the Input System callback.
            _tapDirty = true;

            var map = (obj as InputAction)?.actionMap ?? obj as InputActionMap;
            if (map != _map && !ReferenceEquals(obj, _asset))
                return;

            string json = _asset.SaveBindingOverridesAsJson();
            if (json != _overrides.Value)
                _overrides.Value = json;
        }

        public static bool IsHold(InputBinding binding)
        {
            var interactions = binding.effectiveInteractions;
            return !string.IsNullOrEmpty(interactions) && interactions.Contains("hold");
        }

        public void MarkDirty()
        {
            _tapDirty = true;
        }

        public void Update()
        {
            if (_tapDirty)
                SyncTapOverrides();
        }

        private static InputActionAsset GameAsset()
        {
            try
            {
                return GameInfo.Input != null ? GameInfo.Input.actions : null;
            }
            catch (Exception)
            {
                return null; // GameInfo not loaded yet
            }
        }

        /// <summary>Adds the tap interaction to game bindings that share a control with one of our hold bindings, and removes it elsewhere.</summary>
        private void SyncTapOverrides()
        {
            var game = GameAsset();
            if (game == null)
                return;
            _tapDirty = false;

            var holdPaths = new System.Collections.Generic.HashSet<string>();
            foreach (var binding in _map.bindings)
                if (IsHold(binding) && !string.IsNullOrEmpty(binding.effectivePath))
                    holdPaths.Add(binding.effectivePath);

            foreach (var action in game)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var b = action.bindings[i];
                    if (b.isComposite || b.isPartOfComposite)
                        continue;
                    bool want = holdPaths.Contains(b.effectivePath);
                    bool has = b.overrideInteractions == TapInteraction;
                    if (want != has)
                        SetTap(action, i, want);
                }
            }
        }

        /// <summary>Removes our tap overrides so the game never saves them into its own bindings (see RebindSaveLoadPatch).</summary>
        public void StripTapOverrides(InputActionAsset asset)
        {
            if (asset == null)
                return;
            foreach (var action in asset)
                for (int i = 0; i < action.bindings.Count; i++)
                    if (action.bindings[i].overrideInteractions == TapInteraction)
                        SetTap(action, i, false);
            _tapDirty = true;
        }

        private static void SetTap(InputAction action, int index, bool on)
        {
            // Keep whatever path/processors the player rebound; only the interactions are ours.
            var b = action.bindings[index];
            action.ApplyBindingOverride(index, new InputBinding
            {
                overridePath = b.overridePath,
                overrideProcessors = b.overrideProcessors,
                overrideInteractions = on ? TapInteraction : null,
            });
        }
    }
}
