using System;
using System.Collections;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Components;

namespace SkinCollection
{
    /// <summary>
    /// One-time tip shown in the game's own tutorial box. It is not part of the game's tutorial chain
    /// (whose last step reports completion to the host's save over the network); it only borrows
    /// PlayerTutorial's box prefab and holder, appears once no game tutorial box is on screen, and finishes
    /// with the game's completion animation the first time the skin menu opens.
    /// </summary>
    public class SkinHint : MonoBehaviour
    {
        // Boxes can appear and then be removed by the host's SkipTutorial RPC shortly after spawning.
        private const float QuietTime = 5f;

        private static readonly FieldInfo TutorialToBoxField = AccessTools.Field(typeof(PlayerTutorial), "_tutorialToBox");
        private static readonly FieldInfo BoxPrefabField = AccessTools.Field(typeof(PlayerTutorial), "_box");
        private static readonly FieldInfo HolderField = AccessTools.Field(typeof(PlayerTutorial), "_tutorialHolder");
        private static readonly FieldInfo RectParentField = AccessTools.Field(typeof(TutorialBox), "_rectParent");

        public static SkinHint Instance { get; private set; }

        public ConfigEntry<bool> Enabled;
        public ModInput Input;

        private TutorialBox _box;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _description;
        private PlayerInput _subscribedInput;
        private float _quietSince = -1f;
        private bool _failed;
        private bool _finishPending;
        private float _finishAt = -1f;
        private const float FinishedHoldTime = 0.4f;

        private void Awake()
        {
            Instance = this;
            Strings.LanguageChanged += OnLanguageChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            Strings.LanguageChanged -= OnLanguageChanged;
            Unsubscribe();
        }

        private void OnLanguageChanged()
        {
            if (_box != null && !_finishPending)
                RefreshText();
        }

        /// <summary>
        /// Called when the skin menu opens: the player knows the button now. The menu hides the HUD (and the
        /// tutorial box with it), so the completion animation is deferred until the HUD is back.
        /// </summary>
        public void Complete()
        {
            if (Enabled != null && Enabled.Value)
                Enabled.Value = false;
            if (_box != null)
                _finishPending = true;
        }

        private void Update()
        {
            if (_finishPending)
            {
                if (_box == null)
                    _finishPending = false; // destroyed with the scene
                else if (SkinMenu.IsOpen || PlayerThinking.IsThinking)
                    _finishAt = -1f;
                else if (_finishAt < 0f)
                {
                    // Turn green now and hold it briefly before the game's shrink animation, so it's noticeable.
                    var image = Get<UnityEngine.UI.Image>(_box, "_image");
                    if (image != null)
                        image.color = Get<Color>(_box, "_finishedColor");
                    _finishAt = Time.unscaledTime + FinishedHoldTime;
                }
                else if (Time.unscaledTime >= _finishAt)
                {
                    _finishPending = false;
                    _finishAt = -1f;
                    Unsubscribe();
                    _box.Finish();
                    _box = null;
                }
                return;
            }

            if (_failed || Enabled == null || !Enabled.Value || Input == null || _box != null)
                return;

            var tutorial = PlayerTutorial.Instance;
            if (tutorial == null || PlayerThinking.IsThinking || !SkinMenu.CanUse()
                || !(TutorialToBoxField?.GetValue(tutorial) is IDictionary boxes) || boxes.Count > 0)
            {
                _quietSince = -1f;
                return;
            }
            if (_quietSince < 0f)
                _quietSince = Time.unscaledTime;
            if (Time.unscaledTime - _quietSince >= QuietTime)
                Show(tutorial);
        }

        private void Show(PlayerTutorial tutorial)
        {
            try
            {
                var prefab = BoxPrefabField?.GetValue(tutorial) as TutorialBox;
                var holder = HolderField?.GetValue(tutorial) as RectTransform;
                if (prefab == null || holder == null)
                    throw new InvalidOperationException("PlayerTutorial has no box prefab or holder");

                // Build it under an inactive parent so its localized texts never refresh over ours,
                // then move it into the holder, which runs TutorialBox.OnEnable (the grow animation).
                var staging = new GameObject("SkinHintStaging");
                staging.SetActive(false);
                staging.transform.SetParent(holder, false);
                _box = Instantiate(prefab, staging.transform);
                _box.name = "SkinCollectionHint";
                foreach (var localize in _box.GetComponentsInChildren<LocalizeStringEvent>(true))
                    localize.enabled = false;
                _title = Get<TextMeshProUGUI>(_box, "_titleText");
                _description = Get<TextMeshProUGUI>(_box, "_descriptionText");
                // TutorialBox.Initialize would set this, but it also applies a game Tutorial's localized texts.
                // The finish animation rebuilds this layout every frame and throws if it is missing.
                RectParentField?.SetValue(_box, holder);
                RefreshText();
                _box.transform.SetParent(holder, false);
                Destroy(staging);

                _subscribedInput = GameInfo.Input;
                if (_subscribedInput != null)
                    _subscribedInput.onControlsChanged += OnControlsChanged;
            }
            catch (Exception e)
            {
                // Don't retry every frame; the menu works fine without the tip.
                _failed = true;
                if (_box != null)
                    Destroy(_box.gameObject);
                _box = null;
                Plugin.Log.LogWarning($"Could not show the Skin Collection tip: {e.Message}");
            }
        }

        private void OnControlsChanged(PlayerInput input)
        {
            if (_box == null)
            {
                Unsubscribe();
                return;
            }
            RefreshText();
        }

        private void RefreshText()
        {
            if (_title != null)
                _title.text = Strings.Get(Strings.Title);
            if (_description == null)
                return;

            string key = ModInput.Display(Input.Open);
            if (string.IsNullOrEmpty(key))
            {
                _description.text = Strings.Get(Strings.HintUnbound, Strings.Get(Strings.Title));
                return;
            }
            _description.text = Strings.Get(CurrentBindingIsHold() ? Strings.HintHold : Strings.HintPress, key);
        }

        private bool CurrentBindingIsHold()
        {
            string scheme = GameInfo.Input != null ? GameInfo.Input.currentControlScheme : null;
            if (string.IsNullOrEmpty(scheme))
                return false;
            var mask = InputBinding.MaskByGroup(scheme);
            foreach (var binding in Input.Open.bindings)
                if (mask.Matches(binding))
                    return ModInput.IsHold(binding);
            return false;
        }

        private void Unsubscribe()
        {
            if (_subscribedInput != null)
                _subscribedInput.onControlsChanged -= OnControlsChanged;
            _subscribedInput = null;
        }

        private static T Get<T>(object obj, string field)
        {
            var value = AccessTools.Field(obj.GetType(), field)?.GetValue(obj);
            return value is T t ? t : default;
        }
    }
}
