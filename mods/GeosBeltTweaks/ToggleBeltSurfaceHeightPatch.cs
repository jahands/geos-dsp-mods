using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace GeosBeltTweaks
{
    [HarmonyPatch]
    internal static class ToggleBeltSurfaceHeightPatch
    {
        // Only one belt tool is active; its open/close lifecycle bounds the remembered height.
        private static int _savedAltitude;

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(BuildTool_Path), nameof(BuildTool_Path.DeterminePreviews))]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var zeroKey = AccessTools.PropertyGetter(typeof(VFInput), nameof(VFInput._beltsZeroKey));
            var altitude = AccessTools.Field(typeof(BuildTool_Path), nameof(BuildTool_Path.altitude));
            int match = -1;

            // Match the hotkey's altitude reset, not the later clamp to ground level.
            for (int i = 0; i + 4 < codes.Count; i++)
            {
                if (
                    codes[i].Calls(zeroKey)
                    && (codes[i + 1].opcode == OpCodes.Brfalse || codes[i + 1].opcode == OpCodes.Brfalse_S)
                    && codes[i + 2].opcode == OpCodes.Ldarg_0
                    && codes[i + 3].LoadsConstant(0)
                    && codes[i + 4].StoresField(altitude)
                )
                {
                    if (match >= 0)
                    {
                        throw new InvalidOperationException("GeosBeltTweaks belt surface toggle: multiple reset-height assignments in BuildTool_Path.DeterminePreviews.");
                    }

                    match = i + 3;
                }
            }

            if (match < 0)
            {
                throw new InvalidOperationException("GeosBeltTweaks belt surface toggle: reset-height assignment not found in BuildTool_Path.DeterminePreviews.");
            }

            // Replace only the reset value, keeping its labels and the vanilla tilt reset.
            // Staying inside the existing key branch avoids extra work on other frames.
            codes[match].opcode = OpCodes.Ldarg_0;
            codes[match].operand = null;
            codes.Insert(match + 1, new CodeInstruction(
                OpCodes.Call, AccessTools.Method(typeof(ToggleBeltSurfaceHeightPatch), nameof(GetResetHeight))
            ));
            return codes;
        }

        private static int GetResetHeight(BuildTool_Path tool)
        {
            if (!Plugin.ToggleBeltSurfaceHeight.Value)
            {
                _savedAltitude = 0;
                return 0;
            }

            if (tool.altitude == 0)
            {
                return _savedAltitude;
            }

            // Height keys run before the reset, so simultaneous input can exceed the build limits.
            _savedAltitude = Math.Max(0, Math.Min(60, tool.altitude));
            return 0;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildTool_Path), "_OnOpen")]
        private static void OnOpen()
        {
            _savedAltitude = 0;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildTool_Path), "_OnClose")]
        internal static void ClearSavedHeight()
        {
            _savedAltitude = 0;
        }
    }
}
