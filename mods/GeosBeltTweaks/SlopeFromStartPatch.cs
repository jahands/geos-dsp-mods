using HarmonyLib;
using UnityEngine;

namespace GeosBeltTweaks
{
    // Let belts rise or fall from their first segment while toggled on.
    // The game keeps the first segment flat unless the belt starts at a building port.
    [HarmonyPatch(typeof(PlanetAuxData), nameof(PlanetAuxData.SnapLineNonAlloc))]
    internal static class SlopeFromStartPatch
    {
        internal static bool Enabled;

        private static bool _snappingFlatStart;

        [HarmonyPrefix]
        private static void Prefix(ref bool begin_flat, out bool __state)
        {
            __state = begin_flat && Enabled && !_snappingFlatStart;
            begin_flat &= !__state;
        }

        // Keep the flat first segment where the game would reject a sloped start.
        [HarmonyPostfix]
        private static void Postfix(
            PlanetAuxData __instance,
            Vector3 begin,
            Vector3 end,
            int path,
            bool geodesic,
            Vector3[] snaps,
            bool forceVertical,
            ref float maxGridSlope,
            bool useOldPath,
            ref int __result,
            bool __state
        )
        {
            if (!__state || __result < 2 || CanSlopeFromStart(begin, snaps[1]))
            {
                return;
            }

            if (HalfGridBeltSnapPatch.Enabled && __result < snaps.Length - 10)
            {
                // Run flat for half a cell, then keep the slope by moving each later point to
                // the average height of itself and its predecessor.
                Vector3 start = snaps[0];
                Vector3 halfCell = Vector3.Slerp(start.normalized, snaps[1].normalized, 0.5f) * start.magnitude;

                // DeterminePreviews merges points closer than this, which would drop the flat point.
                if ((halfCell - start).sqrMagnitude >= 0.28f)
                {
                    for (int i = __result; i >= 2; i--)
                    {
                        snaps[i] = snaps[i - 1].normalized * ((snaps[i - 1].magnitude + snaps[i - 2].magnitude) / 2f);
                    }

                    snaps[1] = halfCell;
                    __result++;
                    return;
                }
            }

            _snappingFlatStart = true;
            try
            {
                __result = __instance.SnapLineNonAlloc(begin, end, path, geodesic, true, snaps, forceVertical, ref maxGridSlope, useOldPath);
            }
            finally
            {
                _snappingFlatStart = false;
            }
        }

        // Matches BuildTool_Path.CheckBuildConditions: a sloped first segment needs to continue
        // an existing belt whose input is at least 2.5 radians from the new segment.
        private static bool CanSlopeFromStart(Vector3 start, Vector3 next)
        {
            if (Mathf.Abs(Maths.SphericalSlopeRatio(start, next)) <= 0.1f)
            {
                return true;
            }

            var tool = GameMain.mainPlayer?.controller?.actionBuild?.pathTool;
            if (tool == null || !tool.ObjectIsBelt(tool.startObjectId))
            {
                return false;
            }

            var factory = tool.factory;
            for (int slot = 1; slot < 4; slot++)
            {
                factory.ReadObjectConn(tool.startObjectId, slot, out _, out int inputId, out _);
                if (inputId == 0)
                {
                    continue;
                }

                var input = inputId > 0 ? factory.entityPool[inputId].pos : factory.prebuildPool[-inputId].pos;
                if (Maths.SphericalAngleAOBInRAD(start, next, input) >= 2.5f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
