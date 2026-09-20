using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace GeosBeltTweaks
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
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

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            _harmony = null;
            BeltPreviewTiltPatch.Release();
            ToggleBeltSurfaceHeightPatch.ClearSavedHeight();
        }
    }
}
