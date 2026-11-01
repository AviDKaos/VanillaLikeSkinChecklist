using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SkinCollection
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "avidkaos.howtofish.skincollection";
        public const string PluginName = "Skin Collection";
        // Generated from <Version> in SkinCollection.csproj.
        public const string PluginVersion = PluginInfo.Version;

        internal static ManualLogSource Log;

        private ConfigEntry<bool> _missingAsSilhouette;
        private ConfigEntry<bool> _logOnOpen;
        private ConfigEntry<string> _bindings;
        private ModInput _input;
        private SkinMenu _menu;
        private SkinHint _hint;

        private void Awake()
        {
            Log = Logger;
            _missingAsSilhouette = Config.Bind("General", "MissingAsSilhouette", true,
                "Draw skins you don't own as black silhouettes, like undiscovered fish in the journal. If false they are dimmed instead.");
            var showUnobtainable = Config.Bind("General", "ShowUnobtainable", false,
                "Show skins the slot machine can't give. They never count toward the totals. Remembered from the in-menu toggle.");
            _bindings = Config.Bind("Controls", "BindingOverrides", "",
                "Keybinds changed in the game's Controls menu (Skin collection rows). Managed by the game menu; clear to reset.");
            var tutorialHint = Config.Bind("General", "TutorialHint", true,
                "Show a one-time tip in the game's tutorial style explaining how to open the skin menu. "
                + "Turns itself off the first time you open the menu; set back to true to see it again.");
            _logOnOpen = Config.Bind("Debug", "LogCatalogOnOpen", false, "Also write the full skin list to the log when the menu opens.");

            _menu = gameObject.AddComponent<SkinMenu>();
            _menu.ShowUnobtainable = showUnobtainable;
            _hint = gameObject.AddComponent<SkinHint>();
            _hint.Enabled = tutorialHint;

            new Harmony(PluginGuid).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void Start()
        {
            // BepInEx runs Awake from inside the Input System's static constructor. Actions resolved that early
            // lose their interactions ("Input System not yet initialized"), so the hold on Select never fired.
            _input = new ModInput(_bindings);
            _input.Open.performed += _ => ToggleMenu();
            _input.ToggleUnobtainable.performed += _ => _menu.ToggleUnobtainable();
            _menu.Input = _input;
            _hint.Input = _input;
            ControlsMenu.Init(_input);
        }

        private void ToggleMenu()
        {
            _menu.MissingAsSilhouette = _missingAsSilhouette.Value;
            if (_logOnOpen.Value && !SkinMenu.IsOpen)
                SkinCatalog.LogSummary(Log);
            _menu.Toggle();
        }

        private void Update()
        {
            _input?.Update();
            ControlsMenu.ProcessPending();
        }
    }
}
