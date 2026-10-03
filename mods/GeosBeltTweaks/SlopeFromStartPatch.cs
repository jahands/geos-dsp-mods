using HarmonyLib;

namespace GeosBeltTweaks
{
    // Let belts rise or fall from their first segment while toggled on.
    // The game keeps the first segment flat unless the belt starts at a building port.
    [HarmonyPatch(typeof(PlanetAuxData), nameof(PlanetAuxData.SnapLineNonAlloc))]
    internal static class SlopeFromStartPatch
    {
        internal static bool Enabled;

        [HarmonyPrefix]
        private static void Prefix(ref bool begin_flat)
        {
            begin_flat &= !Enabled;
        }
    }
}
