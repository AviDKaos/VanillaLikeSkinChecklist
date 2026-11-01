using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace SkinCollection
{
    /// <summary>
    /// Skin collection menu drawn with runtime clones of the fish journal's visual parts
    /// (ThinkingCanvas + JournalCamera). ThinkingUI itself lives on "LocalUI" next to every other
    /// HUD script, so it is never cloned; its behaviour (fade, cloud wobble, paging) is replicated here.
    /// While open the menu reuses the journal's "thinking" state, so movement/camera/chat are blocked
    /// and Esc/Tab close it exactly like the journal.
    /// </summary>
    public class SkinMenu : MonoBehaviour
    {
        private const float FadeTime = 0.5f;

        public static SkinMenu Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private static readonly FieldInfo PlayerUIInstanceField = AccessTools.Field(typeof(PlayerUI), "_instance");
        private static readonly FieldInfo ThinkingUIField = AccessTools.Field(typeof(PlayerUI), "_thinkingUI");
        private static readonly MethodInfo IsThinkingSetter = AccessTools.PropertySetter(typeof(PlayerThinking), "IsThinking");
        private static readonly FieldInfo ItemSlotsField = AccessTools.Field(typeof(PlayerInventory), "_itemSlots");
        private static readonly FieldInfo InventorySlotRendererField = AccessTools.Field(typeof(InventorySlot), "_renderer");
        private static readonly FieldInfo BoatSkinRendererField = AccessTools.Field(typeof(Boat), "_skinRenderer");
        private static readonly FieldInfo ShaderManagerInstanceField = AccessTools.Field(typeof(ShaderManager), "_instance");
        private static readonly FieldInfo ThinkingAlphaField = AccessTools.Field(typeof(ThinkingUI), "_thinkingAlpha");

        public bool MissingAsSilhouette = true;
        public ModInput Input;
        public ConfigEntry<bool> ShowUnobtainable;

        private ThinkingUI _sourceUI;
        private GameObject _canvasRoot;
        private GameObject _cameraRoot;
        private CanvasGroup _canvas;
        private CanvasGroup _holder;
        private Camera _camera;
        private RenderTexture _texture;
        private TextMeshProUGUI _progressText;
        private TextMeshProUGUI _totalText;
        private TextMeshProUGUI _pageText;
        private TextMeshProUGUI _closeText;
        private TextMeshProUGUI _toggleText;
        private TextMeshProUGUI _titleText;
        private float _slotAnimDelay = 0.1f;
        private readonly List<SlotView> _slots = new List<SlotView>();
        private bool _buildFailed;

        // ShaderManager.ApplyItemSkin uses keywords of the item shader, or of the boat shader when forBoat;
        // the slot's material must use the matching shader or Unity logs "comes from a different shader".
        private Material _itemMaterial;
        private Material _boatMaterial;

        // Cloud wobble, copied from ThinkingUI.CloudyAnimations.
        private readonly List<Transform> _cloudy = new List<Transform>();
        private AnimationCurve _cloudyCurveX;
        private AnimationCurve _cloudyCurveY;
        private float _animSpeed = 1f;
        private float _offsetStrength = 1f;
        private float _animStrength = 1f;
        private float _animTime;

        private List<SkinGroup> _groups = new List<SkinGroup>();
        private List<Page> _pages = new List<Page>();
        private int _curPage;
        private int _ownedTotal;
        private int _skinTotal;
        private float _alpha;
        private Coroutine _showRoutine;

        private sealed class Page
        {
            public SkinGroup Group;
            public List<SkinEntry> Visible;
            public int Start;
            public int PageInGroup;
            public int PagesInGroup;
        }

        private sealed class SlotView
        {
            public JournalSlot Slot;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Worth;
            public TextMeshProUGUI Bait;
            public MeshFilter Filter;
            public Renderer Renderer;
            public RawImage Image;
            public DripText Drip;
            public Material BaseMaterial;
        }

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
            DestroyClones();
        }

        private void OnLanguageChanged()
        {
            if (_titleText != null)
                _titleText.text = Strings.Get(Strings.Title);
            if (IsOpen)
            {
                UpdateCloseText();
                ShowPage();
            }
        }

        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        /// <summary>
        /// False whenever the game is in a state where the journal can't be used: cutscenes and credits hide the
        /// HUD via PlayerUI.UIDisabled (the intro cutscene even clears MainMenuManager.IsInMenu), loading, and the
        /// self-driving menu/intro boat. Dying is allowed, like the journal.
        /// </summary>
        public static bool CanUse()
        {
            if (Player.LocalPlayer == null || MainMenuManager.IsInMenu || PauseManager.IsPaused || ChatManager.IsTyping)
                return false;
            if (PlayerUI.UIDisabled || EndGameUI.IsShowingEndGame || EndGameEffects.IsShowingEndGame)
                return false;
            if (LoadingManager.IsLoadingIn || IslandManager.IsLoading || SelfDrivingBoat.IsDriving)
                return false;
            return true;
        }

        private void Open()
        {
            if (!CanUse())
                return;

            // While "thinking" without our menu, the journal is open: swap it for the skins in place.
            bool fromJournal = PlayerThinking.IsThinking;

            if (!EnsureBuilt())
                return;

            _groups = SkinCatalog.Build();
            if (_groups.Count == 0)
            {
                Plugin.Log.LogWarning("No skinnable items found; not opening the menu");
                return;
            }
            BuildPages();

            IsOpen = true;
            if (fromJournal)
            {
                HideJournalInstantly();
                // The thinking background is already fully shown; don't fade it from zero.
                _alpha = 1f;
                _holder.alpha = 1f;
                ShaderManager.SetThinkingAlpha(1f);
            }
            else
            {
                IsThinkingSetter.Invoke(null, new object[] { true });
                PlayerCamera.ToggleMouse(true);
                PlayerUI.ToggleMainCanvas(false);
                PlayerUI.ToggleFXCanvas(false);
                PlayerUI.ToggleBossCanvas(false);
            }

            _canvasRoot.SetActive(true);
            _cameraRoot.SetActive(true);
            _canvas.alpha = 1f;
            UpdateCloseText();
            ShowPage();
            SkinHint.Instance?.Complete();
        }

        /// <summary>Tab/Select while the skins are open: show the journal instead, staying in "thinking".</summary>
        public void SwitchToJournal()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            if (_showRoutine != null)
                StopCoroutine(_showRoutine);
            _showRoutine = null;
            _alpha = 0f;
            _holder.alpha = 0f;
            _canvas.alpha = 0f;
            _canvasRoot.SetActive(false);
            _cameraRoot.SetActive(false);

            // Start the journal's fade at 1 so the thinking background doesn't flicker.
            ThinkingAlphaField?.SetValue(_sourceUI, 1f);
            PlayerUI.OnToggleThinking();
        }

        private void HideJournalInstantly()
        {
            var canvas = Get<CanvasGroup>(_sourceUI, "_thinkingCanvas");
            var holder = Get<CanvasGroup>(_sourceUI, "_thinkingHolderGroup");
            if (canvas != null)
            {
                LeanTween.cancel(canvas.gameObject);
                canvas.alpha = 0f;
            }
            if (holder != null)
                holder.alpha = 0f;
            ThinkingAlphaField?.SetValue(_sourceUI, 0f);
        }

        private bool ShowingUnobtainable => ShowUnobtainable != null && ShowUnobtainable.Value;

        public void ToggleUnobtainable()
        {
            if (!IsOpen || ShowUnobtainable == null)
                return;
            ShowUnobtainable.Value = !ShowUnobtainable.Value;
            BuildPages();
            ShowPage();
        }

        private void UpdateToggleText()
        {
            if (_toggleText == null)
                return;
            string key = Input != null ? ModInput.Display(Input.ToggleUnobtainable) : "";
            _toggleText.text = Strings.Get(ShowingUnobtainable ? Strings.HideUnobtainable : Strings.ShowUnobtainable, key).TrimEnd();
        }

        /// <summary>"Close [K]" or a gamepad icon, built like ThinkingUI.SetupCloseInputText but with our action.</summary>
        private void UpdateCloseText()
        {
            if (_closeText == null)
                return;
            // On a controller our button (Select by default) switches to the journal rather than closing,
            // so show Pause there, like the journal does.
            bool controller = GameInfo.Input != null && GameInfo.Input.currentControlScheme == ModInput.ControllerGroup;
            string key = Input != null && !controller ? ModInput.Display(Input.Open) : "";
            if (string.IsNullOrEmpty(key))
            {
                var pause = GameInfo.Input != null ? GameInfo.Input.actions.FindAction("Pause") : null;
                key = pause != null ? ModInput.Display(pause) : "";
            }
            string text = null;
            try
            {
                text = LocalizationManager.CloseLocalized?.GetLocalizedString(key);
            }
            catch (Exception)
            {
                // Fall back to English below.
            }
            _closeText.text = string.IsNullOrEmpty(text) ? "Close " + key : text;
        }

        private void Close()
        {
            if (!IsOpen)
                return;
            IsOpen = false;

            // Leave the shared "thinking" state the same way PlayerThinking.ToggleThinking(false) does,
            // minus PlayerUI.OnToggleThinking (that would toggle the real journal).
            if (PlayerThinking.IsThinking)
            {
                IsThinkingSetter.Invoke(null, new object[] { false });
                PlayerCamera.ToggleMouse(false);
                PlayerUI.ToggleMainCanvas(true);
                PlayerUI.ToggleFXCanvas(true);
                PlayerUI.ToggleBossCanvas(true);
            }
        }

        public void Scroll(int direction)
        {
            if (!IsOpen || _pages.Count == 0)
                return;
            _curPage = (_curPage + direction + _pages.Count) % _pages.Count;
            ShowPage();
        }

        private void Update()
        {
            // Esc / Tab go through PlayerThinking and clear IsThinking; follow it.
            if (IsOpen && !PlayerThinking.IsThinking)
                Close();

            // A cutscene, the credits or a loading screen started while the menu was open. Pause is excluded:
            // pausing keeps the menu open underneath, like the journal.
            if (IsOpen && !PauseManager.IsPaused && !CanUse())
                Close();

            if (_canvasRoot == null)
            {
                IsOpen = false;
                return;
            }

            if (IsOpen)
                CloudyAnimations();

            float target = IsOpen ? 1f : 0f;
            if (!Mathf.Approximately(_alpha, target))
            {
                _alpha = Mathf.MoveTowards(_alpha, target, Time.unscaledDeltaTime / FadeTime);
                float eased = IsOpen ? Mathf.Sin(_alpha * Mathf.PI / 2f) : 1f - Mathf.Cos(_alpha * Mathf.PI / 2f);
                _holder.alpha = eased;
                ShaderManager.SetThinkingAlpha(eased);

                if (!IsOpen && _alpha <= 0f)
                {
                    _canvas.alpha = 0f;
                    _canvasRoot.SetActive(false);
                    _cameraRoot.SetActive(false);
                }
            }
        }

        private void CloudyAnimations()
        {
            if (_cloudyCurveX == null || _cloudyCurveY == null)
                return;
            foreach (var t in _cloudy)
            {
                if (t == null)
                    continue;
                Vector3 position = t.parent.position;
                float time = Mathf.Repeat(_animTime + (position.x + position.y) * _offsetStrength, 1f);
                t.localPosition = new Vector3(_cloudyCurveX.Evaluate(time), _cloudyCurveY.Evaluate(time), 0f) * _animStrength;
            }
            _animTime += Time.deltaTime * _animSpeed;
        }

        private void BuildPages()
        {
            var previous = _pages.Count > 0 ? _pages[_curPage] : null;
            int perPage = _slots.Count;
            _pages = new List<Page>();
            _ownedTotal = 0;
            _skinTotal = 0;

            foreach (var group in _groups)
            {
                _ownedTotal += group.OwnedCount;
                _skinTotal += group.TotalCount;
                var visible = ShowingUnobtainable ? group.Skins : group.Skins.Where(s => !s.Unobtainable).ToList();
                int count = Mathf.Max(1, Mathf.CeilToInt(visible.Count / (float)perPage));
                for (int p = 0; p < count; p++)
                    _pages.Add(new Page { Group = group, Visible = visible, Start = p * perPage, PageInGroup = p, PagesInGroup = count });
            }

            // Keep the player on the same item page across reopenings and toggles.
            _curPage = 0;
            if (previous != null)
            {
                int same = _pages.FindIndex(p => p.Group.ItemId == previous.Group.ItemId
                                                 && p.PageInGroup == Mathf.Min(previous.PageInGroup, p.PagesInGroup - 1));
                if (same >= 0)
                    _curPage = same;
            }
        }

        private void ShowPage()
        {
            var page = _pages[_curPage];
            var group = page.Group;
            var skinSource = GetSkinSource(group);

            for (int i = 0; i < _slots.Count; i++)
            {
                int index = page.Start + i;
                FillSlot(_slots[i], index < page.Visible.Count ? page.Visible[index] : null, group, skinSource);
            }

            RenderSlots();

            string groupPage = page.PagesInGroup > 1 ? $" ({page.PageInGroup + 1}/{page.PagesInGroup})" : "";
            _progressText.text = $"{group.Name}{groupPage}: {group.OwnedCount} / {group.TotalCount}";
            _progressText.color = group.OwnedCount == group.TotalCount ? GameInfo.OrangeColor : Color.white;
            _totalText.text = Strings.Get(Strings.TotalSkins, _ownedTotal, _skinTotal);
            _totalText.color = _ownedTotal == _skinTotal ? GameInfo.OrangeColor : Color.white;
            _pageText.text = Strings.Get(Strings.Page, _curPage + 1, _pages.Count);
            UpdateToggleText();

            if (_showRoutine != null)
                StopCoroutine(_showRoutine);
            _showRoutine = StartCoroutine(ShowSlots());
        }

        private IEnumerator ShowSlots()
        {
            foreach (var view in _slots)
            {
                view.Slot.ShowSlot();
                yield return new WaitForSecondsRealtime(_slotAnimDelay);
            }
            _showRoutine = null;
        }

        private struct SkinSource
        {
            public Mesh Mesh;
            public Vector3 Pos;
            public Vector3 Rot;
            public float Scale;
            public SkinPreset Preset;
        }

        private static SkinSource GetSkinSource(SkinGroup group)
        {
            if (group.IsBoat)
            {
                var boat = SkinCatalog.FindBoat();
                return new SkinSource { Mesh = boat.Mesh, Pos = boat.SkinMeshPos, Rot = boat.SkinMeshRot, Scale = boat.SkinMeshScale, Preset = boat.SkinPreset };
            }
            var item = GameInfo.IDToItem(group.ItemId);
            return new SkinSource { Mesh = item.Mesh, Pos = item.InventoryMeshPos, Rot = item.InventoryMeshRot, Scale = item.InventoryMeshScale, Preset = item.SkinPreset };
        }

        private void FillSlot(SlotView view, SkinEntry entry, SkinGroup group, SkinSource source)
        {
            // Clears texts/mesh and collapses the slot, ready for ShowSlot().
            view.Slot.SetCreature(null);
            if (view.Drip != null)
                view.Drip.SetDrip(false);
            if (entry == null)
                return;

            view.Name.gameObject.SetActive(true);
            view.Name.text = entry.Name;
            view.Name.color = entry.Owned ? Color.white : new Color(1f, 1f, 1f, 0.5f);

            view.Worth.text = RarityLabel(entry);
            view.Worth.color = RarityColor(entry);

            if (view.Bait != null)
                view.Bait.text = "";

            view.Image.color = entry.Owned ? Color.white : (MissingAsSilhouette ? Color.black : new Color(0.35f, 0.35f, 0.35f, 1f));

            view.Filter.mesh = source.Mesh;
            view.Filter.transform.localPosition = source.Pos;
            view.Filter.transform.localEulerAngles = source.Rot;
            view.Filter.transform.localScale = Vector3.one * source.Scale;
            if (view.Renderer == null || source.Preset == null || entry.Index >= source.Preset.Skins.Count)
                return;

            bool forBoat = group.IsBoat && _boatMaterial != null;
            var material = forBoat ? _boatMaterial : _itemMaterial;
            if (material == null)
                return;
            if (view.BaseMaterial != material)
            {
                // ApplyItemSkin edits rend.material, an instance owned by this renderer; drop the old one.
                if (view.BaseMaterial != null && view.Renderer.sharedMaterial != view.BaseMaterial)
                    Destroy(view.Renderer.sharedMaterial);
                view.Renderer.sharedMaterial = material;
                view.BaseMaterial = material;
            }
            ShaderManager.ApplyItemSkin(source.Preset.Skins[entry.Index], view.Renderer, forBoat);
        }

        private static string RarityLabel(SkinEntry entry)
        {
            if (entry.Index == 0)
                return Strings.Get(Strings.Default);
            if (entry.Unobtainable)
                return Strings.Get(Strings.Unobtainable);
            switch (entry.Rarity)
            {
                case Rarity.Common: return Strings.Get(Strings.Common);
                case Rarity.Rare: return Strings.Get(Strings.Rare);
                case Rarity.Legendary: return Strings.Get(Strings.Legendary);
                default: return entry.Rarity.ToString();
            }
        }

        private static Color RarityColor(SkinEntry entry)
        {
            switch (entry.Rarity)
            {
                case Rarity.Common: return GameInfo.CommonColor;
                case Rarity.Rare: return GameInfo.RareColor;
                case Rarity.Legendary: return GameInfo.LegendaryColor;
                default: return new Color(1f, 1f, 1f, 0.6f);
            }
        }

        private void RenderSlots()
        {
            // The real journal's meshes share our camera's position and layers; hide them for our render.
            var hidden = new List<Renderer>();
            foreach (var slot in Get<List<JournalSlot>>(_sourceUI, "_slots") ?? new List<JournalSlot>())
            {
                var filter = Get<MeshFilter>(slot, "_filter");
                var rend = filter != null ? filter.GetComponent<Renderer>() : null;
                if (rend != null && rend.enabled)
                {
                    rend.enabled = false;
                    hidden.Add(rend);
                }
            }
            try
            {
                _camera.Render();
            }
            finally
            {
                foreach (var rend in hidden)
                    rend.enabled = true;
            }
        }

        private bool EnsureBuilt()
        {
            if (_canvasRoot != null && _cameraRoot != null)
                return true;
            if (_buildFailed)
                return false;

            // The clones live under the local PlayerHolder, so they vanish with it (e.g. leaving a lobby).
            DestroyClones();

            var playerUI = PlayerUIInstanceField.GetValue(null) as PlayerUI;
            _sourceUI = playerUI != null ? ThinkingUIField.GetValue(playerUI) as ThinkingUI : null;
            if (_sourceUI == null)
            {
                Plugin.Log.LogWarning("PlayerUI/ThinkingUI not ready yet");
                return false;
            }

            try
            {
                var srcCanvas = Get<CanvasGroup>(_sourceUI, "_thinkingCanvas");
                var srcCamera = Get<Camera>(_sourceUI, "_journalCamera");
                if (srcCanvas == null || srcCamera == null)
                    return Fail("ThinkingUI has no _thinkingCanvas or _journalCamera");

                // Only clone objects that carry nothing but visuals.
                if (srcCanvas.GetComponent<ThinkingUI>() != null || srcCamera.GetComponent<ThinkingUI>() != null)
                    return Fail("Journal canvas/camera share a GameObject with ThinkingUI");

                var srcCanvasT = srcCanvas.transform;
                var srcCameraT = srcCamera.transform;

                _cameraRoot = Instantiate(srcCamera.gameObject, srcCameraT.parent, false);
                _cameraRoot.name = "SkinCollectionCamera";
                _cameraRoot.SetActive(false);
                _camera = _cameraRoot.GetComponent<Camera>();
                _camera.enabled = false;

                _canvasRoot = Instantiate(srcCanvas.gameObject, srcCanvasT.parent, false);
                _canvasRoot.name = "SkinCollectionCanvas";
                _canvasRoot.SetActive(false);
                _canvasRoot.transform.SetAsLastSibling();
                _canvas = _canvasRoot.GetComponent<CanvasGroup>();

                Transform InCanvas(Component c) => MapPath(c != null ? c.transform : null, srcCanvasT, _canvasRoot.transform);

                _holder = InCanvas(Get<CanvasGroup>(_sourceUI, "_thinkingHolderGroup"))?.GetComponent<CanvasGroup>();
                _progressText = InCanvas(Get<TextMeshProUGUI>(_sourceUI, "_progressText"))?.GetComponent<TextMeshProUGUI>();
                _totalText = InCanvas(Get<TextMeshProUGUI>(_sourceUI, "_dripProgressText"))?.GetComponent<TextMeshProUGUI>();
                _pageText = InCanvas(Get<TextMeshProUGUI>(_sourceUI, "_dripChanceText"))?.GetComponent<TextMeshProUGUI>();
                _closeText = InCanvas(Get<LocalizeStringEvent>(_sourceUI, "_closeEvent"))?.GetComponent<TextMeshProUGUI>();
                _slotAnimDelay = Get<float>(_sourceUI, "_slotAnimDelay");
                if (_holder == null || _progressText == null || _totalText == null || _pageText == null)
                    return Fail("Journal holder/progress texts are not inside ThinkingCanvas");

                _cloudyCurveX = Get<AnimationCurve>(_sourceUI, "_cloudyCurveX");
                _cloudyCurveY = Get<AnimationCurve>(_sourceUI, "_cloudyCurveY");
                _animSpeed = Get<float>(_sourceUI, "_animSpeed");
                _offsetStrength = Get<float>(_sourceUI, "_offsetStrength");
                _animStrength = Get<float>(_sourceUI, "_animStrength");
                foreach (var t in Get<List<Transform>>(_sourceUI, "_cloudlyTransforms") ?? new List<Transform>())
                {
                    var mapped = MapPath(t, srcCanvasT, _canvasRoot.transform);
                    if (mapped != null)
                        _cloudy.Add(mapped);
                }

                FindSkinMaterials();
                foreach (var srcSlot in Get<List<JournalSlot>>(_sourceUI, "_slots") ?? new List<JournalSlot>())
                {
                    var slot = InCanvas(srcSlot)?.GetComponent<JournalSlot>();
                    if (slot == null)
                        return Fail("A JournalSlot is not inside ThinkingCanvas");

                    // _filter points at a mesh under the real JournalCamera; retarget it to our camera clone,
                    // otherwise SetCreature(null) would wipe the real journal's meshes.
                    var srcFilter = Get<MeshFilter>(srcSlot, "_filter");
                    var filter = MapPath(srcFilter != null ? srcFilter.transform : null, srcCameraT, _cameraRoot.transform)?.GetComponent<MeshFilter>();
                    if (filter == null)
                        return Fail("JournalSlot._filter is not under JournalCamera");
                    AccessTools.Field(typeof(JournalSlot), "_filter").SetValue(slot, filter);

                    var view = new SlotView
                    {
                        Slot = slot,
                        Name = Get<TextMeshProUGUI>(slot, "_nameText"),
                        Worth = Get<TextMeshProUGUI>(slot, "_worthText"),
                        Bait = Get<TextMeshProUGUI>(slot, "_baitText"),
                        Filter = filter,
                        Renderer = filter.GetComponent<Renderer>(),
                        Image = Get<RawImage>(slot, "_image"),
                        Drip = Get<DripText>(slot, "_dripText"),
                    };
                    if (view.Name == null || view.Worth == null || view.Image == null
                        || !view.Name.transform.IsChildOf(_canvasRoot.transform) || !view.Image.transform.IsChildOf(_canvasRoot.transform))
                        return Fail("JournalSlot texts/image are not inside ThinkingCanvas");
                    _slots.Add(view);
                }
                if (_slots.Count == 0)
                    return Fail("ThinkingUI has no journal slots");

                // Own render target so we never draw over the real journal's texture.
                var source = srcCamera.targetTexture != null ? srcCamera.targetTexture : GameInfo.JournalTexture;
                _texture = new RenderTexture(source.descriptor) { name = "SkinCollectionTexture" };
                _texture.Create();
                _camera.targetTexture = _texture;
                foreach (var image in _canvasRoot.GetComponentsInChildren<RawImage>(true))
                    if (image.texture == source)
                        image.texture = _texture;

                SetupStaticTexts();
                SetupArrows();
                CreateToggleButton();

                _canvas.alpha = 0f;
                _holder.alpha = 0f;
                Plugin.Log.LogInfo($"Skin menu built ({_slots.Count} slots per page, {_cloudy.Count} cloud parts, "
                                   + $"item material: {(_itemMaterial != null ? _itemMaterial.name : "none")}, boat material: {(_boatMaterial != null ? _boatMaterial.name : "none")})");
                return true;
            }
            catch (Exception e)
            {
                return Fail(e.ToString());
            }
        }

        private void SetupStaticTexts()
        {
            // Localized texts would overwrite ours (and the close hint's runtime arguments aren't cloned),
            // so freeze them with whatever the real journal currently shows.
            foreach (var localize in _canvasRoot.GetComponentsInChildren<LocalizeStringEvent>(true))
                localize.enabled = false;

            _titleText = _canvasRoot.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == "ThinkingText");
            if (_titleText != null)
                _titleText.text = Strings.Get(Strings.Title);
        }

        private void SetupArrows()
        {
            foreach (var button in _canvasRoot.GetComponentsInChildren<Button>(true))
            {
                int direction = button.name.StartsWith("Prev") ? -1 : button.name.StartsWith("Next") ? 1 : 0;
                if (direction == 0)
                    continue;
                // Replace the persistent listeners, which target the real ThinkingUI.
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => Scroll(direction));
            }
        }

        /// <summary>A clickable copy of the close hint, placed just above it.</summary>
        private void CreateToggleButton()
        {
            if (_closeText == null)
                return;
            var go = Instantiate(_closeText.gameObject, _closeText.transform.parent, false);
            go.name = "UnobtainableToggle";
            _toggleText = go.GetComponent<TextMeshProUGUI>();
            _toggleText.raycastTarget = true;
            var rect = _toggleText.rectTransform;
            rect.anchoredPosition += new Vector2(0f, _closeText.rectTransform.rect.height * 1.1f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = _toggleText;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(ToggleUnobtainable);
        }

        private bool Fail(string reason)
        {
            Plugin.Log.LogError($"Could not build the skin menu: {reason}");
            if (_sourceUI != null)
            {
                var canvas = Get<CanvasGroup>(_sourceUI, "_thinkingCanvas");
                if (canvas != null)
                    LogHierarchy(canvas.transform);
            }
            DestroyClones();
            _buildFailed = true;
            return false;
        }

        private void DestroyClones()
        {
            if (_canvasRoot != null)
                Destroy(_canvasRoot);
            if (_cameraRoot != null)
                Destroy(_cameraRoot);
            if (_texture != null)
                _texture.Release();
            _canvasRoot = null;
            _cameraRoot = null;
            _texture = null;
            _slots.Clear();
            _cloudy.Clear();
        }

        /// <summary>Finds the clone of <paramref name="original"/> by walking the same sibling indices.</summary>
        private static Transform MapPath(Transform original, Transform originalRoot, Transform cloneRoot)
        {
            if (original == null)
                return null;
            var indices = new List<int>();
            for (var t = original; t != originalRoot; t = t.parent)
            {
                if (t == null)
                    return null;
                indices.Add(t.GetSiblingIndex());
            }
            var mapped = cloneRoot;
            for (int i = indices.Count - 1; i >= 0; i--)
            {
                if (indices[i] >= mapped.childCount)
                    return null;
                mapped = mapped.GetChild(indices[i]);
            }
            return mapped;
        }

        /// <summary>
        /// Item material: the inventory slots' (item skin shader). Boat material: the boat's own skin renderer
        /// (boat skin shader). Both are checked against the shaders ShaderManager built its keywords from.
        /// </summary>
        private void FindSkinMaterials()
        {
            var shaderManager = ShaderManagerInstanceField?.GetValue(null);
            var itemShader = shaderManager != null ? Get<Shader>(shaderManager, "_defaultShader") : null;
            var boatShader = shaderManager != null ? Get<Shader>(shaderManager, "_boatShader") : null;

            _itemMaterial = null;
            var inventory = Player.LocalPlayer != null ? Player.LocalPlayer.Inventory : null;
            if (inventory != null && ItemSlotsField?.GetValue(inventory) is List<InventorySlot> slots)
            {
                foreach (var slot in slots)
                {
                    var mat = (InventorySlotRendererField?.GetValue(slot) as Renderer)?.sharedMaterial;
                    if (mat != null && (itemShader != null ? mat.shader == itemShader : mat.HasProperty(ShaderManager.UseSkinID)))
                    {
                        _itemMaterial = mat;
                        break;
                    }
                }
            }
            if (_itemMaterial == null && itemShader != null)
                _itemMaterial = new Material(itemShader) { name = "SkinCollectionItem" };

            _boatMaterial = null;
            var boat = SkinCatalog.FindBoat();
            var boatMat = boat != null ? (BoatSkinRendererField?.GetValue(boat) as Renderer)?.sharedMaterial : null;
            if (boatMat != null && (boatShader == null || boatMat.shader == boatShader))
                _boatMaterial = boatMat;
            else if (boatShader != null)
                _boatMaterial = new Material(boatShader) { name = "SkinCollectionBoat" };

            if (_itemMaterial == null)
                Plugin.Log.LogWarning("No item skin material found; skins will not render");
        }

        private static T Get<T>(object obj, string field)
        {
            var info = AccessTools.Field(obj.GetType(), field);
            var value = info?.GetValue(obj);
            return value is T t ? t : default;
        }

        private static void LogHierarchy(Transform root)
        {
            var sb = new StringBuilder("Hierarchy of " + root.name + ":\n");
            Append(sb, root, 0);
            Plugin.Log.LogInfo(sb.ToString());
        }

        private static void Append(StringBuilder sb, Transform t, int depth)
        {
            if (depth > 8)
                return;
            var comps = string.Join(", ", t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
            sb.Append(' ', depth * 2).Append(t.name).Append(" [").Append(comps).Append("]\n");
            foreach (Transform child in t)
                Append(sb, child, depth + 1);
        }
    }
}
