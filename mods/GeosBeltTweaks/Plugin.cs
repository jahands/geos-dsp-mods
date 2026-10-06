using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems.ModLocalization;
using crecheng.DSPModSave;
using HarmonyLib;
using UnityEngine;

namespace GeosBeltTweaks
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(CommonAPIPlugin.GUID)]
    [BepInDependency(DSPModSavePlugin.MODGUID)]
    [CommonAPISubmoduleDependency(nameof(LocalizationModule))]
    public class Plugin : BaseUnityPlugin, IModCanSave
    {
        private const int SaveVersion = 2;

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
        internal static ConfigEntry<KeyboardShortcut> ToggleBattleBaseConstructionKey = null!;

        private void Awake()
        {
            Log = Logger;
            MatchStartingBeltHeight = Config.Bind(
                "Building",
                "MatchStartingBeltHeight",
                true,
                "Start new belts at the height of the belt or splitter you drag from."
            );
            MatchStartingBeltTier = Config.Bind(
                "Building",
                "MatchStartingBeltTier",
                true,
                "Select the starting belt's tier when extending it."
            );
            ShowBeltPreviewTilt = Config.Bind(
                "Building",
                "ShowBeltPreviewTilt",
                true,
                "Show slope and tilt in belt previews."
            );
            ToggleBeltSurfaceHeight = Config.Bind(
                "Building",
                "ToggleBeltSurfaceHeight",
                true,
                "The reset-height key toggles between the surface and the previous height."
            );
            RememberBeltFreeAngleMode = Config.Bind(
                "Building",
                "RememberBeltFreeAngleMode",
                true,
                "Keep free-angle mode on when reopening the belt tool."
            );
            UnlimitedChainUpgradeRange = Config.Bind(
                "Building",
                "UnlimitedChainUpgradeRange",
                true,
                "Chain upgrades reach the whole belt, ignoring build range."
            );
            ChainUpgradeAcrossTiers = Config.Bind(
                "Building",
                "ChainUpgradeAcrossTiers",
                true,
                "Chain upgrades continue through tier changes."
            );
            ToggleHalfGridSnapKey = Config.Bind(
                "Building",
                "ToggleHalfGridSnapKey",
                new KeyboardShortcut(KeyCode.BackQuote),
                "Toggles half-grid snapping in the belt tool."
            );
            ToggleSlopeFromStartKey = Config.Bind(
                "Building",
                "ToggleSlopeFromStartKey",
                new KeyboardShortcut(KeyCode.Keypad1),
                "Toggles sloping from the first segment in the belt tool."
            );
            ToggleBattleBaseConstructionKey = Config.Bind(
                "Building",
                "ToggleBattleBaseConstructionKey",
                new KeyboardShortcut(KeyCode.Keypad2),
                "Toggles building by Battlefield Analysis Bases on the planet while the belt tool is open."
            );
            ToggleHalfGridSnapKey.SettingChanged += ToggleKeyTip.HalfGrid.OnKeyChanged;
            ToggleSlopeFromStartKey.SettingChanged += ToggleKeyTip.SlopeFromStart.OnKeyChanged;
            ToggleBattleBaseConstructionKey.SettingChanged += ToggleKeyTip.BattleBaseConstruction.OnKeyChanged;
            LocalizationModule.RegisterTranslation(ToggleKeyTip.HalfGrid.OnText, "Half-grid: on", "半格：开", "");
            LocalizationModule.RegisterTranslation(ToggleKeyTip.HalfGrid.OffText, "Half-grid: off", "半格：关", "");
            LocalizationModule.RegisterTranslation(ToggleKeyTip.SlopeFromStart.OnText, "Slope from start: on", "起点起坡：开", "");
            LocalizationModule.RegisterTranslation(ToggleKeyTip.SlopeFromStart.OffText, "Slope from start: off", "起点起坡：关", "");
            LocalizationModule.RegisterTranslation(ToggleKeyTip.BattleBaseConstruction.OnText, "BAB construction: on", "战场分析基站建设：开", "");
            LocalizationModule.RegisterTranslation(ToggleKeyTip.BattleBaseConstruction.OffText, "BAB construction: off", "战场分析基站建设：关", "");
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
            PauseBattleBaseBuildingPatch.Update();
            if (VFInput.inputing || GameMain.mainPlayer?.controller.actionBuild.activeTool is not BuildTool_Path)
            {
                return;
            }

            if (ToggleHalfGridSnapKey.Value.IsDown())
            {
                HalfGridBeltSnapPatch.Enabled = !HalfGridBeltSnapPatch.Enabled;
                HalfGridBeltSnapPatch.HalfStep = false;
            }

            if (ToggleSlopeFromStartKey.Value.IsDown())
            {
                SlopeFromStartPatch.Enabled = !SlopeFromStartPatch.Enabled;
            }

            if (ToggleBattleBaseConstructionKey.Value.IsDown())
            {
                PauseBattleBaseBuildingPatch.Enabled = !PauseBattleBaseBuildingPatch.Enabled;
            }
        }

        private void OnDestroy()
        {
            ToggleHalfGridSnapKey.SettingChanged -= ToggleKeyTip.HalfGrid.OnKeyChanged;
            ToggleSlopeFromStartKey.SettingChanged -= ToggleKeyTip.SlopeFromStart.OnKeyChanged;
            ToggleBattleBaseConstructionKey.SettingChanged -= ToggleKeyTip.BattleBaseConstruction.OnKeyChanged;
            _harmony?.UnpatchSelf();
            _harmony = null;
            BeltPreviewTiltPatch.Release();
            ToggleBeltSurfaceHeightPatch.ClearSavedHeight();
            ToggleKeyTip.HalfGrid.Destroy();
            ToggleKeyTip.SlopeFromStart.Destroy();
            ToggleKeyTip.BattleBaseConstruction.Destroy();
        }

        public void Export(BinaryWriter w)
        {
            w.Write(SaveVersion);
            w.Write(HalfGridBeltSnapPatch.Enabled);
            w.Write(SlopeFromStartPatch.Enabled);
            w.Write(GameMain.mainPlayer?.controller.actionBuild.pathTool.geodesic ?? false);
            w.Write(PauseBattleBaseBuildingPatch.Enabled);
        }

        public void Import(BinaryReader r)
        {
            int version = r.ReadInt32();
            if (version > SaveVersion)
            {
                Log.LogWarning($"Ignoring belt tool modes saved by a newer version (save data version {version})");
                IntoOtherSave();
                return;
            }

            HalfGridBeltSnapPatch.Enabled = r.ReadBoolean();
            HalfGridBeltSnapPatch.HalfStep = false;
            SlopeFromStartPatch.Enabled = r.ReadBoolean();
            bool freeAngle = r.ReadBoolean();
            PauseBattleBaseBuildingPatch.Enabled = version >= 2 && r.ReadBoolean();
            if (GameMain.mainPlayer != null)
            {
                GameMain.mainPlayer.controller.actionBuild.pathTool.geodesic = freeAngle;
            }
        }

        public void IntoOtherSave()
        {
            HalfGridBeltSnapPatch.Enabled = false;
            HalfGridBeltSnapPatch.HalfStep = false;
            SlopeFromStartPatch.Enabled = false;
            PauseBattleBaseBuildingPatch.Enabled = false;
        }
    }
}
