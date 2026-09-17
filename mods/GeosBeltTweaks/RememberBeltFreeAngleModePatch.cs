using HarmonyLib;

namespace GeosBeltTweaks
{
    [HarmonyPatch(typeof(BuildTool_Path), "_OnOpen")]
    internal static class RememberBeltFreeAngleModePatch
    {
        [HarmonyPrefix]
        private static void Prefix(BuildTool_Path __instance, out bool __state)
        {
            // Preserve free-angle mode, which the game turns off when reopening the belt tool.
            __state = Plugin.RememberBeltFreeAngleMode.Value && __instance.geodesic;
        }

        [HarmonyPostfix]
        private static void Postfix(BuildTool_Path __instance, bool __state)
        {
            if (__state)
            {
                __instance.geodesic = true;
            }
        }
    }
}
