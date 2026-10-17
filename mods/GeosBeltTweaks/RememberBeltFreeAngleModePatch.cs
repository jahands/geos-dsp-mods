using HarmonyLib;

namespace GeosBeltTweaks
{
    [HarmonyPatch(typeof(BuildTool_Path))]
    internal static class RememberBeltFreeAngleModePatch
    {
        // Free-angle mode from the loaded save. The game initializes the belt tool after DSPModSave imports.
        internal static bool LoadedFreeAngle;

        [HarmonyPostfix]
        [HarmonyPatch("_OnInit")]
        private static void InitPostfix(BuildTool_Path __instance)
        {
            __instance.geodesic = LoadedFreeAngle;
        }

        [HarmonyPrefix]
        [HarmonyPatch("_OnOpen")]
        private static void OpenPrefix(BuildTool_Path __instance, out bool __state)
        {
            // Preserve free-angle mode, which the game turns off when reopening the belt tool.
            __state = Plugin.RememberBeltFreeAngleMode.Value && __instance.geodesic;
        }

        [HarmonyPostfix]
        [HarmonyPatch("_OnOpen")]
        private static void OpenPostfix(BuildTool_Path __instance, bool __state)
        {
            if (__state)
            {
                __instance.geodesic = true;
            }
        }
    }
}
