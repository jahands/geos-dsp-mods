using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace GeosBeltTweaks
{
    [HarmonyPatch]
    internal static class ChainUpgradeAcrossTiersPatch
    {
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(BuildTool_Upgrade), nameof(BuildTool_Upgrade.DetermineMoreChainTargets))]
        private static IEnumerable<CodeInstruction> ChainTargetsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var beltSpeed = AccessTools.Field(typeof(PrefabDesc), nameof(PrefabDesc.beltSpeed));
            var stopsChain = AccessTools.Method(typeof(ChainUpgradeAcrossTiersPatch), nameof(StopsChain));
            int matches = 0;

            // Match the tier comparison, not the earlier read of the hovered belt's speed.
            for (int i = 0; i + 1 < codes.Count; i++)
            {
                if (
                    codes[i].LoadsField(beltSpeed)
                    && (codes[i + 1].opcode == OpCodes.Bne_Un || codes[i + 1].opcode == OpCodes.Bne_Un_S)
                )
                {
                    codes[i + 1].opcode = OpCodes.Brtrue;
                    codes.Insert(++i, new CodeInstruction(OpCodes.Call, stopsChain));
                    matches++;
                }
            }

            if (matches != 1)
            {
                throw new InvalidOperationException(
                    $"GeosBeltTweaks chain upgrade across tiers: expected 1 belt speed comparison in BuildTool_Upgrade.DetermineMoreChainTargets, found {matches}."
                );
            }

            return codes;
        }

        private static bool StopsChain(int hoveredBeltSpeed, int beltSpeed)
        {
            return !Plugin.ChainUpgradeAcrossTiers.Value && hoveredBeltSpeed != beltSpeed;
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(BuildTool_Upgrade), nameof(BuildTool_Upgrade.UpgradeAction))]
        private static IEnumerable<CodeInstruction> UpgradeActionTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var objId = AccessTools.Field(typeof(BuildPreview), nameof(BuildPreview.objId));
            var upgradeLevel = AccessTools.Field(typeof(BuildTool_Upgrade), nameof(BuildTool_Upgrade.upgradeLevel));
            var getChainGrade = AccessTools.Method(typeof(ChainUpgradeAcrossTiersPatch), nameof(GetChainGrade));
            int matches = 0;

            // Match the grade argument of each DoUpgradeObject call.
            for (int i = 0; i + 3 < codes.Count; i++)
            {
                if (
                    codes[i].LoadsField(objId)
                    && codes[i + 1].LoadsConstant(0)
                    && codes[i + 2].opcode == OpCodes.Ldarg_0
                    && codes[i + 3].LoadsField(upgradeLevel)
                )
                {
                    codes[i + 1].opcode = OpCodes.Ldarg_0;
                    codes[i + 1].operand = null;
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Call, getChainGrade));
                    matches++;
                }
            }

            if (matches != 2)
            {
                throw new InvalidOperationException(
                    $"GeosBeltTweaks chain upgrade across tiers: expected 2 DoUpgradeObject grade arguments in BuildTool_Upgrade.UpgradeAction, found {matches}."
                );
            }

            return codes;
        }

        private static int GetChainGrade(BuildTool_Upgrade tool)
        {
            if (!Plugin.ChainUpgradeAcrossTiers.Value || !tool.chainReaction || tool.cursorType != 0)
            {
                return 0;
            }

            // The hovered object is the first preview; its chain targets are all belts.
            var hovered = tool.buildPreviews[0];
            if (!hovered.desc.isBelt)
            {
                return 0;
            }

            // A grade of 0 falls back to the relative upgrade.
            return Math.Max(1, hovered.item.Grade + tool.upgradeLevel);
        }
    }
}
