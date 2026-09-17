using HarmonyLib;
using UnityEngine;

namespace GeosBeltTweaks
{
    [HarmonyPatch(typeof(BuildTool_Path), nameof(BuildTool_Path.ConfirmOperation))]
    internal static class MatchStartingBeltPatch
    {
        [HarmonyPrefix]
        private static void Prefix(BuildTool_Path __instance, out int __state)
        {
            __state = __instance.controller.cmd.stage;
        }

        [HarmonyPostfix]
        private static void Postfix(BuildTool_Path __instance, int __state)
        {
            if (__state != 0 || __instance.controller.cmd.stage != 1)
            {
                return;
            }

            var item = __instance.GetItemProto(__instance.startObjectId);
            if (item == null || (!item.prefabDesc.isBelt && !item.prefabDesc.isSplitter))
            {
                return;
            }

            if (Plugin.MatchStartingBeltHeight.Value)
            {
                float height = __instance.startTarget.magnitude - __instance.planet.realRadius - 0.2f;
                __instance.altitude = Mathf.Clamp(Mathf.RoundToInt(height / 1.3333333f), 0, 60);
            }

            if (
                item.prefabDesc.isBelt
                && Plugin.MatchStartingBeltTier.Value
                && item.ID != __instance.controller.cmd.refId
            )
            {
                __instance.player.SetHandItems(item.ID, 0);
                // Keep the active path when the selected hand item changes.
                __instance.controller.cmd.refId = item.ID;
                __instance.UpdateHandItem();
            }
        }
    }
}
