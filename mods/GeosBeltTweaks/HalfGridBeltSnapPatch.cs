using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace GeosBeltTweaks
{
    // Snap belts to half grid cells and half height steps while toggled on.
    // Grid rows and columns are 0.2 latitude/longitude segment units apart; half cells use 0.1.
    [HarmonyPatch]
    internal static class HalfGridBeltSnapPatch
    {
        private const float TwoPi = Mathf.PI * 2f;

        internal static bool Enabled;

        // Raises belts half a height step above the belt tool's altitude.
        internal static bool HalfStep;

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(BuildTool_Path), nameof(BuildTool_Path.UpdateRaycast))]
        private static IEnumerable<CodeInstruction> SnapCursor(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var snap = AccessTools.Method(typeof(PlanetAuxData), nameof(PlanetAuxData.Snap), [typeof(Vector3), typeof(bool)]);
            var snapCalls = new List<int>();
            var objectSnapRadii = new List<int>();

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(snap))
                {
                    snapCalls.Add(i);
                }
                else if (codes[i].opcode == OpCodes.Ldc_R4 && Equals(codes[i].operand, 0.6f))
                {
                    objectSnapRadii.Add(i);
                }
            }

            if (snapCalls.Count != 2 || objectSnapRadii.Count != 2)
            {
                throw new InvalidOperationException(
                    $"GeosBeltTweaks half-grid snapping: expected 2 PlanetAuxData.Snap calls and 2 object snap radii in BuildTool_Path.UpdateRaycast, found {snapCalls.Count} and {objectSnapRadii.Count}."
                );
            }

            foreach (int i in snapCalls)
            {
                codes[i].opcode = OpCodes.Call;
                codes[i].operand = AccessTools.Method(typeof(HalfGridBeltSnapPatch), nameof(Snap));
            }

            // A half cell is 0.5-0.76 m wide, so the vanilla 0.6 m radius would pull the cursor onto neighboring belts.
            foreach (int i in objectSnapRadii)
            {
                codes[i].opcode = OpCodes.Call;
                codes[i].operand = AccessTools.Method(typeof(HalfGridBeltSnapPatch), nameof(GetObjectSnapRadius));
            }

            return codes;
        }

        private static Vector3 Snap(PlanetAuxData aux, Vector3 pos, bool onTerrain)
        {
            Vector3 snapped = aux.Snap(pos, onTerrain);
            PlanetGrid? grid = aux.activeGrid;
            if (!Enabled || grid == null)
            {
                return snapped;
            }

            // Harmony003 reports reads of Vector3 parameters as writes.
#pragma warning disable Harmony003
            Vector3 direction = pos.normalized;
#pragma warning restore Harmony003
            int segment = grid.segment;
            float row = Mathf.Round(Mathf.Asin(direction.y) / TwoPi * segment * 10f) / 10f;
            float columns = GetColumnCount(row, segment);
            float column = Mathf.Round(Mathf.Atan2(direction.x, -direction.z) / TwoPi * columns * 10f) / 10f;
            float height = snapped.magnitude + (HalfStep ? PlanetGrid.kAltGrid / 2f : 0f);
            return GetDirection(row, column, segment, columns) * height;
        }

        private static float GetObjectSnapRadius()
        {
            return Enabled ? 0.3f : 0.6f;
        }

        // Replaces the belt route walker, which re-snaps every point to full cells.
        // Each leg steps one full cell from its start and ends with a half cell when the end needs it.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlanetGrid), nameof(PlanetGrid.SnapLineNonAlloc))]
        private static bool SnapLine(PlanetGrid __instance, Vector3 begin, Vector3 end, int path, Vector3[] snaps, out int swerveIndex, ref int __result)
        {
            swerveIndex = 0;
            if (!Enabled)
            {
                return true;
            }

            int segment = __instance.segment;
            int capacity = snaps.Length - 10;
            int count = 0;
            __result = 0;
            if (capacity <= 0)
            {
                return false;
            }

            // Pole and over-the-pole handling matches the vanilla walker.
#pragma warning disable Harmony003
            Vector3 from = begin.normalized;
            Vector3 to = end.normalized;
#pragma warning restore Harmony003
            float beginLatitude = Mathf.Asin(from.y);
            float beginLongitude = Mathf.Atan2(from.x, -from.z);
            float endLatitude = Mathf.Asin(to.y);
            float endLongitude = Mathf.Atan2(to.x, -to.z);
            bool beginAtPole = (from.x * from.x + from.z * from.z) * segment * segment < 1f;
            bool endAtPole = (to.x * to.x + to.z * to.z) * segment * segment < 1f;
            if (beginAtPole && endAtPole && from.y * to.y >= 0f)
            {
                snaps[0] = new Vector3(0f, from.y >= 0f ? 1f : -1f, 0f);
                __result = 1;
                return false;
            }

            if (beginAtPole && endAtPole)
            {
                beginLongitude = endLongitude = 0f;
                beginLatitude = Mathf.Sign(beginLatitude) * Mathf.PI * 0.5f;
                endLatitude = Mathf.Sign(endLatitude) * Mathf.PI * 0.5f;
            }
            else if (beginAtPole)
            {
                beginLongitude = endLongitude;
                beginLatitude = Mathf.Sign(beginLatitude) * Mathf.PI * 0.5f;
            }
            else if (endAtPole)
            {
                endLongitude = beginLongitude;
                endLatitude = Mathf.Sign(endLatitude) * Mathf.PI * 0.5f;
            }

            float turn = Mathf.Repeat(endLongitude - beginLongitude, TwoPi);
            if (turn > Mathf.PI)
            {
                turn -= TwoPi;
            }

            if (turn > 2f || turn < -2f)
            {
                turn -= Mathf.Sign(turn) * Mathf.PI;
                endLatitude = (endLatitude >= 0f ? Mathf.PI : -Mathf.PI) - endLatitude;
            }

            endLongitude = beginLongitude + turn;
            int beginRow = Mathf.RoundToInt(beginLatitude / TwoPi * segment * 10f);
            int endRow = Mathf.RoundToInt(endLatitude / TwoPi * segment * 10f);

            int beginParity = Mathf.RoundToInt(GetHalfColumn(beginRow, beginLongitude, out _, out _)) & 1;
            int endParity = Mathf.RoundToInt(GetHalfColumn(endRow, endLongitude, out _, out _)) & 1;

            if (path == 1)
            {
                AddParallel(beginRow, beginLongitude, beginParity, endLongitude, endParity);
                swerveIndex = count;
                AddMeridian(endLongitude, endParity, beginRow, endRow);
            }
            else
            {
                AddMeridian(beginLongitude, beginParity, beginRow, endRow);
                swerveIndex = count;
                AddParallel(endRow, beginLongitude, beginParity, endLongitude, endParity);
            }

            __result = count;
            return false;

            // Adds points along a row between two longitudes, one full cell apart. Rows and columns are
            // counted in half cells; a column taken from an end point's longitude keeps that point's
            // alignment with the full grid in other latitude bands.
            void AddParallel(int halfRow, float fromLongitude, int fromParity, float toLongitude, int toParity)
            {
                int from = Align(GetHalfColumn(halfRow, fromLongitude, out float row, out float columns), fromParity);
                int to = Align(GetHalfColumn(halfRow, toLongitude, out _, out _), toParity);
                int step = to >= from ? 2 : -2;
                for (int halfColumn = from; (to - halfColumn) * step > 0 && count < capacity; halfColumn += step)
                {
                    Add(GetDirection(row, halfColumn / 10f, segment, columns));
                }

                Add(GetDirection(row, to / 10f, segment, columns));
            }

            void AddMeridian(float longitude, int parity, int fromHalfRow, int toHalfRow)
            {
                int step = toHalfRow >= fromHalfRow ? 2 : -2;
                for (int halfRow = fromHalfRow; (toHalfRow - halfRow) * step > 0 && count < capacity; halfRow += step)
                {
                    AddOnMeridian(halfRow, longitude, parity);
                }

                AddOnMeridian(toHalfRow, longitude, parity);
            }

            void AddOnMeridian(int halfRow, float longitude, int parity)
            {
                int halfColumn = Align(GetHalfColumn(halfRow, longitude, out float row, out float columns), parity);
                Add(GetDirection(row, halfColumn / 10f, segment, columns));
            }

            static int Align(float halfColumn, int parity)
            {
                return parity + 2 * Mathf.RoundToInt((halfColumn - parity) / 2f);
            }

            // Position of a longitude in the row's band, in half columns. Rows past a pole are mirrored
            // onto the opposite meridian, like the vanilla walker.
            float GetHalfColumn(int halfRow, float longitude, out float row, out float columns)
            {
                row = halfRow / 10f;
                if (Mathf.Abs(row) > segment / 4f)
                {
                    row = Mathf.Sign(row) * segment / 2f - row;
                    longitude += Mathf.PI;
                }

                columns = GetColumnCount(row, segment);
                return longitude / TwoPi * columns * 10f;
            }

            // Appends a point, skipping repeated corners and columns that converge near the poles.
            void Add(Vector3 point)
            {
                float minGap = 0.25f / segment;
                if (count < capacity && (count == 0 || (point - snaps[count - 1]).sqrMagnitude > minGap * minGap))
                {
                    snaps[count++] = point;
                }
            }
        }

        // Longitude column count of the latitude band containing the row. The game picks the band with
        // |row| - 0.1 for its 0.2 row spacing; 0.05 keeps full rows in the same bands and puts each half
        // row in the band of the next full row away from the equator.
        private static float GetColumnCount(float row, int segment)
        {
            return PlanetGrid.DetermineLongitudeSegmentCount(Mathf.FloorToInt(Mathf.Max(0f, Mathf.Abs(row) - 0.05f)), segment);
        }

        private static Vector3 GetDirection(float row, float column, int segment, float columns)
        {
            float latitude = row / segment * TwoPi;
            float longitude = column / columns * TwoPi;
            float cosLatitude = Mathf.Cos(latitude);
            return new Vector3(cosLatitude * Mathf.Sin(longitude), Mathf.Sin(latitude), -cosLatitude * Mathf.Cos(longitude));
        }

        // Turn the height keys into half steps; vanilla applies its ±1 (±5 with Shift) after this prefix.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(BuildTool_Path), nameof(BuildTool_Path.DeterminePreviews))]
        private static void StepHalfHeight(BuildTool_Path __instance)
        {
            if (!Enabled)
            {
                return;
            }

            if (VFInput._notSnapBuild)
            {
                // Shift moves 4 half steps, so it keeps the half step unless it reaches a height limit.
                if (VFInput._liftBeltsHeight)
                {
                    HalfStep &= __instance.altitude + 2 < 60;
                    __instance.altitude -= 3;
                }

                if (VFInput._reduceBeltsHeight)
                {
                    HalfStep &= __instance.altitude >= 2;
                    __instance.altitude += 3;
                }

                return;
            }

            if (VFInput._liftBeltsHeight)
            {
                if (HalfStep)
                {
                    HalfStep = false;
                }
                else if (__instance.altitude < 60)
                {
                    HalfStep = true;
                    __instance.altitude--;
                }
            }

            if (VFInput._reduceBeltsHeight)
            {
                if (HalfStep)
                {
                    HalfStep = false;
                    __instance.altitude++;
                }
                else if (__instance.altitude > 0)
                {
                    HalfStep = true;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildTool_Path), "_OnClose")]
        private static void ClearHalfStep()
        {
            HalfStep = false;
        }

        // Move the elevated build grid to the half step so the cursor lands where the belt is drawn.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIBuildingGrid), "Update")]
        private static void RaiseAltitudeGrid(UIBuildingGrid __instance)
        {
            PlanetData? planet = GameMain.localPlanet;
            Player? player = GameMain.mainPlayer;
            if (!HalfStep || !__instance.altGridRnd.enabled || planet == null || player == null)
            {
                return;
            }

            float scale = (planet.realRadius + 0.2f + (player.controller.actionBuild.pathTool.altitude + 0.5f) * PlanetGrid.kAltGrid) * 2f;
            __instance.altGridRnd.transform.localScale = new Vector3(scale, scale, scale);
        }
    }
}
