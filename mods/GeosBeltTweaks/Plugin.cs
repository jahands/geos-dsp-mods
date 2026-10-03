using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems.ModLocalization;
using HarmonyLib;
using UnityEngine;

namespace GeosBeltTweaks
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(CommonAPIPlugin.GUID)]
    [CommonAPISubmoduleDependency(nameof(LocalizationModule))]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony? _harmony;
        internal static ManualLogSource Log = null!;
        internal static ConfigEntry<bool> MatchStartingBeltTier = null!;
        internal static ConfigEntry<bool> MatchStartingBeltHeight = null!;
        internal static ConfigEntry<bool> ShowBeltPreviewTilt = null!;
        internal static ConfigEntry<bool> ToggleBeltSurfaceHeight = null!;
        internal static ConfigEntry<bool> RememberBeltFreeAngleMode = null!;
        internal static ConfigEntry<bool> UnlimitedChainUpgradeRange = null!;
        internal static ConfigEntry<bool> ChainUpgradeAcrossTiers = null!;
        internal static ConfigEntry<KeyboardShortcut> ToggleHalfGridSnapKey = null!;
        internal static ConfigEntry<KeyboardShortcut> ToggleSlopeFromStartKey = null!;

        private void Awake()
        {
            Log = Logger;
            MatchStartingBeltHeight = Config.Bind(
                "Building",
                "MatchStartingBeltHeight",
                true,
                "Match the height of the belt or splitter you start from, rounded to the nearest build level. Applies to the next belt you start."
            );
            MatchStartingBeltTier = Config.Bind(
                "Building",
                "MatchStartingBeltTier",
                true,
                "Use the starting belt's tier when extending it. Applies to the next belt you start."
            );
            ShowBeltPreviewTilt = Config.Bind(
                "Building",
                "ShowBeltPreviewTilt",
                true,
                "Show the belt's slope and tilt in belt previews. Applies immediately."
            );
            ToggleBeltSurfaceHeight = Config.Bind(
                "Building",
                "ToggleBeltSurfaceHeight",
                true,
                "Toggle between the surface and the previous belt height with the reset-height hotkey (numpad 0 by default). Applies immediately."
            );
            RememberBeltFreeAngleMode = Config.Bind(
                "Building",
                "RememberBeltFreeAngleMode",
                true,
                "Keep free-angle mode selected when reopening the belt tool."
            );
            UnlimitedChainUpgradeRange = Config.Bind(
                "Building",
                "UnlimitedChainUpgradeRange",
                true,
                "Chain upgrading or downgrading a belt reaches the whole belt instead of stopping at the mecha's build range. Applies immediately."
            );
            ChainUpgradeAcrossTiers = Config.Bind(
                "Building",
                "ChainUpgradeAcrossTiers",
                true,
                "Chain upgrading or downgrading a belt continues through tier changes and brings every segment to the tier the hovered segment ends up at. Applies immediately."
            );
            ToggleHalfGridSnapKey = Config.Bind(
                "Building",
                "ToggleHalfGridSnapKey",
                new KeyboardShortcut(KeyCode.BackQuote),
                "Toggle half-grid snapping while the belt tool is open. Belts snap to half grid cells, and the height keys move half a level. Applies immediately."
            );
            ToggleSlopeFromStartKey = Config.Bind(
                "Building",
                "ToggleSlopeFromStartKey",
                new KeyboardShortcut(KeyCode.Keypad1),
                "Toggle sloping from the start while the belt tool is open. Belts rise or fall from their first segment instead of running one segment flat first. Works while holding other keys, such as Shift. Applies immediately."
            );
            ToggleHalfGridSnapKey.SettingChanged += ToggleKeyTip.HalfGrid.OnKeyChanged;
            ToggleSlopeFromStartKey.SettingChanged += ToggleKeyTip.SlopeFromStart.OnKeyChanged;
            LocalizationModule.RegisterTranslation(ToggleKeyTip.HalfGrid.OnText, "Half-grid: on", "半格：开", "");
            LocalizationModule.RegisterTranslation(ToggleKeyTip.HalfGrid.OffText, "Half-grid: off", "半格：关", "");
            LocalizationModule.RegisterTranslation(ToggleKeyTip.SlopeFromStart.OnText, "Slope from start: on", "起点起坡：开", "");
            LocalizationModule.RegisterTranslation(ToggleKeyTip.SlopeFromStart.OffText, "Slope from start: off", "起点起坡：关", "");
            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            try
            {
                _harmony.PatchAll(typeof(Plugin).Assembly);
            }
            catch
            {
                _harmony.UnpatchSelf();
                _harmony = null;
                throw;
            }
            Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded");
        }

        private void Update()
        {
            if (VFInput.inputing || GameMain.mainPlayer?.controller.actionBuild.activeTool is not BuildTool_Path)
            {
                return;
            }

            if (ToggleHalfGridSnapKey.Value.IsDown())
            {
                HalfGridBeltSnapPatch.Enabled = !HalfGridBeltSnapPatch.Enabled;
                HalfGridBeltSnapPatch.HalfStep = false;
            }

            // Unlike KeyboardShortcut.IsDown, ignore other held keys such as Shift.
            KeyboardShortcut slopeKey = ToggleSlopeFromStartKey.Value;
            if (Input.GetKeyDown(slopeKey.MainKey) && slopeKey.Modifiers.All(Input.GetKey))
            {
                SlopeFromStartPatch.Enabled = !SlopeFromStartPatch.Enabled;
            }
        }

        private void OnDestroy()
        {
            ToggleHalfGridSnapKey.SettingChanged -= ToggleKeyTip.HalfGrid.OnKeyChanged;
            ToggleSlopeFromStartKey.SettingChanged -= ToggleKeyTip.SlopeFromStart.OnKeyChanged;
            _harmony?.UnpatchSelf();
            _harmony = null;
            BeltPreviewTiltPatch.Release();
            ToggleBeltSurfaceHeightPatch.ClearSavedHeight();
            ToggleKeyTip.HalfGrid.Destroy();
            ToggleKeyTip.SlopeFromStart.Destroy();
        }
    }
}
