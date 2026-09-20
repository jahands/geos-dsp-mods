using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace GeosBeltTweaks
{
    [HarmonyPatch(typeof(BuildTool_Upgrade), nameof(BuildTool_Upgrade.DetermineMoreChainTargets))]
    internal static class UnlimitedChainUpgradeRangePatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var buildArea = AccessTools.Field(typeof(Mecha), nameof(Mecha.buildArea));
            var getChainBuildArea = AccessTools.Method(typeof(UnlimitedChainUpgradeRangePatch), nameof(GetChainBuildArea));
            int matches = 0;

            // The only build area reads are the two sides of the squared range check.
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(buildArea))
                {
                    codes.Insert(++i, new CodeInstruction(OpCodes.Call, getChainBuildArea));
                    matches++;
                }
            }

            if (matches != 2)
            {
                throw new InvalidOperationException(
                    $"GeosBeltTweaks unlimited chain upgrade range: expected 2 build area reads in BuildTool_Upgrade.DetermineMoreChainTargets, found {matches}."
                );
            }

            return codes;
        }

        private static float GetChainBuildArea(float buildArea)
        {
            return Plugin.UnlimitedChainUpgradeRange.Value ? float.PositiveInfinity : buildArea;
        }
    }
}
