using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeosBeltTweaks
{
    // Rotate belt preview caps to show each segment's slope and tilt.
    // Split the caps from their vertical guides so they can rotate independently.
    // The game draws the guides; a separate batch draws the caps, including endpoint rings.
    [HarmonyPatch]
    internal static class BeltPreviewTiltPatch
    {
        private static CapRenderer? _capRenderer;
        private static bool _initializationFailed;
        private static ConnGizmoRenderer? _queuedRotationOwner;

        // Match queued belts to draw records by position; draw records contain no object IDs.
        private static readonly Dictionary<Vector3, Quaternion> QueuedCapRotations = new();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ConnGizmoRenderer), nameof(ConnGizmoRenderer.Update))]
        private static void BeginMarkerUpdate(ConnGizmoRenderer __instance, out bool __state)
        {
            __state = false;

            if (
                !Plugin.ShowBeltPreviewTilt.Value || _initializationFailed
                || __instance.factory == null || __instance.factoryModel == null
                || GameMain.localPlanet?.factory != __instance.factory
            )
            {
                if (_queuedRotationOwner == __instance)
                {
                    _queuedRotationOwner = null;
                    QueuedCapRotations.Clear();
                }

                return;
            }

            if (_queuedRotationOwner != __instance)
            {
                _queuedRotationOwner = __instance;
                __instance.factoryModel.needRefreshConnGizmo012 = true;
            }

            // Rebuild cap rotations with the game's marker batches. Keep the cached
            // rotations and GPU data when the construction markers have not changed.
            __state = __instance.factoryModel.needRefreshConnGizmo012 || __instance.cursorGizmoGraph.pointCount > 0;

            if (__state)
            {
                QueuedCapRotations.Clear();
            }
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(ConnGizmoRenderer), nameof(ConnGizmoRenderer.Update))]
        private static IEnumerable<CodeInstruction> InjectQueuedRotationCapture(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var rotation = AccessTools.Field(typeof(PrebuildData), nameof(PrebuildData.rot));
            var markerRotation = AccessTools.Field(typeof(ConnGizmoObj), nameof(ConnGizmoObj.rot));
            int match = -1;

            for (int i = 1; i + 1 < codes.Count; i++)
            {
                if (
                    codes[i - 1].opcode == OpCodes.Ldelema && Equals(codes[i - 1].operand, typeof(PrebuildData))
                    && codes[i].LoadsField(rotation) && codes[i + 1].StoresField(markerRotation)
                )
                {
                    if (match >= 0)
                    {
                        throw new InvalidOperationException("GeosBeltTweaks queued belt tilt: multiple marker rotation assignments in ConnGizmoRenderer.Update.");
                    }

                    match = i;
                }
            }

            if (match < 0)
            {
                throw new InvalidOperationException("GeosBeltTweaks queued belt tilt: marker rotation assignment not found in ConnGizmoRenderer.Update.");
            }

            // Capture each queued belt as the game visits it, avoiding another factory scan.
            // Leave its original rotation in the game's buffer for the vertical guide.
            codes[match].opcode = OpCodes.Ldarg_0;
            codes[match].operand = null;
            codes.Insert(match + 1, new CodeInstruction(
                OpCodes.Call, AccessTools.Method(typeof(BeltPreviewTiltPatch), nameof(CaptureQueuedCapRotation))
            ));

            return codes;
        }

        // Save the tilted cap rotation separately, returning the original rotation for the guide.
        private static Quaternion CaptureQueuedCapRotation(ref PrebuildData belt, ConnGizmoRenderer renderer)
        {
            if (_queuedRotationOwner != renderer)
            {
                return belt.rot;
            }

            var factory = renderer.factory;
            var forward = belt.rot * Vector3.forward;

            // Follow the output connection, or the incoming segment at a run's end.
            // Positive connection IDs are built objects; negative IDs are queued objects.
            for (int slot = 0; slot < 4; slot++)
            {
                factory.ReadObjectConn(-belt.id, slot, out bool isOutput, out int otherId, out _);

                if (otherId == 0)
                {
                    continue;
                }

                var neighbor = otherId > 0 ? factory.entityPool[otherId].pos : factory.prebuildPool[-otherId].pos;
                forward = isOutput ? neighbor - belt.pos : belt.pos - neighbor;
                break;
            }

            QueuedCapRotations[belt.pos] = CalculateCapRotation(belt.rot, belt.pos, forward, belt.tilt);
            return belt.rot;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ConnGizmoRenderer), nameof(ConnGizmoRenderer.Update))]
        private static void UpdateCapBatches(
            ConnGizmoRenderer __instance,
            List<ConnGizmoObj> ___objs_0,
            List<ConnGizmoObj> ___objs_1,
            Mesh ___mesh_0,
            Mesh ___mesh_1,
            Material ___mat_0,
            Material ___mat_1,
            bool __state
        )
        {
            bool markersRebuilt = __state;

            if (!markersRebuilt && _queuedRotationOwner == __instance && _capRenderer?.Renderer == __instance)
            {
                return;
            }

            if (_capRenderer?.Renderer == __instance)
            {
                _capRenderer.Enabled = false;
            }

            if (!Plugin.ShowBeltPreviewTilt.Value || _initializationFailed || _queuedRotationOwner != __instance)
            {
                return;
            }

            var build = GameMain.mainPlayer?.controller?.actionBuild;
            var tool = build?.pathTool;
            var graph = __instance.cursorGizmoGraph;

            bool hasCursor = build != null && build.active && tool != null && tool.active
                && build.model.connRenderer == __instance && tool.buildPreviews.Count == graph.pointCount;
            int cursorCount = hasCursor ? graph.pointCount : 0;

            int cursorMajorCount = 0;
            int cursorMinorCount = 0;

            for (int i = 0; i < cursorCount; i++)
            {
                var item = tool!.buildPreviews[i];

                if (!item.isConnNode || item.desc == null || !item.desc.isBelt)
                {
                    return;
                }

                if (graph.pointsCurrent[i] == Vector3.zero)
                {
                    continue;
                }

                if (i == 0 || i == graph.pointCount - 1)
                {
                    cursorMajorCount++;
                }
                else
                {
                    cursorMinorCount++;
                }
            }

            if (
                ___objs_0.Count + ___objs_1.Count == 0
                || cursorMajorCount > ___objs_0.Count || cursorMinorCount > ___objs_1.Count
            )
            {
                return;
            }

            if (_capRenderer?.Renderer != __instance)
            {
                _capRenderer?.Dispose();
                _capRenderer = null;

                try
                {
                    _capRenderer = new CapRenderer(__instance, ___mesh_0, ___mesh_1, ___mat_0, ___mat_1);
                }
                catch (Exception err)
                {
                    _initializationFailed = true;
                    Plugin.Log.LogError($"Belt preview tilt disabled until restart: could not split the marker meshes. {err}");
                    return;
                }
            }

            var capRenderer = _capRenderer!;

            // Apply saved belt rotations to the queued construction markers.
            capRenderer.Major.CopyWithQueuedRotations(___objs_0);
            capRenderer.Minor.CopyWithQueuedRotations(___objs_1);

            // Update the active preview at the end of each batch using the current build path.
            // The game omits zero-position cursor points, so skip them when advancing indices.
            int cursorMajorIndex = ___objs_0.Count - cursorMajorCount;
            int cursorMinorIndex = ___objs_1.Count - cursorMinorCount;

            for (int i = 0; i < cursorCount; i++)
            {
                if (graph.pointsCurrent[i] == Vector3.zero)
                {
                    continue;
                }

                bool major = i == 0 || i == graph.pointCount - 1;
                var batch = major ? capRenderer.Major : capRenderer.Minor;
                int index = major ? cursorMajorIndex++ : cursorMinorIndex++;
                var marker = batch.CapInstances[index];

                marker.rot = CursorCapRotation(marker.rot, tool!, i);
                batch.CapInstances[index] = marker;
            }

            capRenderer.Major.UploadCaps();
            capRenderer.Minor.UploadCaps();
            capRenderer.Enabled = true;
        }

        private static Quaternion CursorCapRotation(Quaternion rotation, BuildTool_Path tool, int index)
        {
            var preview = tool.buildPreviews[index];
            Vector3 forward;

            if (index + 1 < tool.buildPreviews.Count)
            {
                forward = tool.buildPreviews[index + 1].lpos - preview.lpos;
            }
            else if (index > 0)
            {
                forward = preview.lpos - tool.buildPreviews[index - 1].lpos;
            }
            else
            {
                if (preview.tilt == 0f)
                {
                    return rotation;
                }

                forward = rotation * Vector3.forward;
            }

            return CalculateCapRotation(rotation, preview.lpos, forward, preview.tilt);
        }

        private static Quaternion CalculateCapRotation(Quaternion rotation, Vector3 position, Vector3 forward, float tilt)
        {
            // Align the cap with the belt's slope, then roll it by the belt's tilt.
            // Planet-relative positions give local vertical; projecting that vector
            // perpendicular to the belt direction gives the cap's unbanked up direction.
            var up = Vector3.ProjectOnPlane(position, forward);

            if (Vector3.Dot(forward, forward) < 1e-8f || up.sqrMagnitude < 1e-8f)
            {
                return rotation;
            }

            var pathRotation = Quaternion.LookRotation(forward, up);
            return tilt == 0f
                ? pathRotation
                : Quaternion.AngleAxis(tilt, forward) * pathRotation;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ConnGizmoRenderer), nameof(ConnGizmoRenderer.Draw))]
        private static void UseGuideMeshes(
            ConnGizmoRenderer __instance, ref Mesh ___mesh_0, ref Mesh ___mesh_1, out CapRenderer? __state
        )
        {
            __state = _capRenderer;

            if (__state == null || !__state.Enabled || __state.Renderer != __instance)
            {
                __state = null;
                return;
            }

            // Draw upright guides using the game's original instance rotations.
            ___mesh_0 = __state.Major.GuideMesh;
            ___mesh_1 = __state.Minor.GuideMesh;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ConnGizmoRenderer), nameof(ConnGizmoRenderer.Draw))]
        private static void DrawRotatedCaps(ref Mesh ___mesh_0, ref Mesh ___mesh_1, CapRenderer? __state)
        {
            if (__state == null)
            {
                return;
            }

            ___mesh_0 = __state.Major.OriginalMesh;
            ___mesh_1 = __state.Minor.OriginalMesh;

            // Draw the caps separately using the rotated instance data.
            __state.Major.DrawCaps();
            __state.Minor.DrawCaps();
        }

        // Restore the full marker meshes even if drawing throws an exception.
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(ConnGizmoRenderer), nameof(ConnGizmoRenderer.Draw))]
        private static void RestoreMeshes(ref Mesh ___mesh_0, ref Mesh ___mesh_1, CapRenderer? __state)
        {
            if (__state != null)
            {
                ___mesh_0 = __state.Major.OriginalMesh;
                ___mesh_1 = __state.Minor.OriginalMesh;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ConnGizmoRenderer), nameof(ConnGizmoRenderer.Destroy))]
        private static void ReleaseRenderer(ConnGizmoRenderer __instance)
        {
            if (_capRenderer?.Renderer == __instance || _queuedRotationOwner == __instance)
            {
                Release();
            }
        }

        internal static void Release()
        {
            _capRenderer?.Dispose();
            _capRenderer = null;
            _initializationFailed = false;
            _queuedRotationOwner = null;
            QueuedCapRotations.Clear();
        }

        private sealed class CapRenderer : IDisposable
        {
            internal readonly ConnGizmoRenderer Renderer;

            // Keep the game's two marker batches: ringed markers (0) and plain markers (1).
            internal readonly CapBatch Major;
            internal readonly CapBatch Minor;
            internal bool Enabled;

            internal CapRenderer(ConnGizmoRenderer renderer, Mesh major, Mesh minor, Material majorMat, Material minorMat)
            {
                Renderer = renderer;
                Major = new CapBatch(major, majorMat);

                try
                {
                    Minor = new CapBatch(minor, minorMat);
                }
                catch
                {
                    Major.Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                Major.Dispose();
                Minor.Dispose();
            }
        }

        private sealed class CapBatch : IDisposable
        {
            internal readonly Mesh OriginalMesh;
            internal Mesh GuideMesh = null!;
            internal ConnGizmoObj[] CapInstances = Array.Empty<ConnGizmoObj>();

            private Mesh _capMesh = null!;
            private Material _capMaterial = null!;
            private ComputeBuffer? _instanceBuffer;
            private ComputeBuffer? _drawArgsBuffer;
            private readonly uint[] _drawArgs = new uint[5];
            private int _instanceCount;

            // Reuse split cap and guide meshes across marker updates.
            internal CapBatch(Mesh original, Material material)
            {
                OriginalMesh = original;

                try
                {
                    var vertices = original.vertices;
                    var triangles = original.triangles;
                    var capTriangles = new List<int>();
                    var guideTriangles = new List<int>();

                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        // Separate cap triangles from the guide: the cap is at y >= 0,
                        // while the stem and elevation ticks extend below it.
                        var targetTriangles = vertices[triangles[i]].y >= -0.001f
                            && vertices[triangles[i + 1]].y >= -0.001f
                            && vertices[triangles[i + 2]].y >= -0.001f ? capTriangles : guideTriangles;

                        targetTriangles.Add(triangles[i]);
                        targetTriangles.Add(triangles[i + 1]);
                        targetTriangles.Add(triangles[i + 2]);
                    }

                    if (capTriangles.Count == 0 || guideTriangles.Count == 0)
                    {
                        throw new InvalidOperationException("GeosBeltTweaks: belt preview mesh has no separate cap and stem.");
                    }

                    _capMesh = UnityEngine.Object.Instantiate(original);
                    var capVertices = new List<Vector3>(vertices);
                    var capNormals = new List<Vector3>(original.normals);
                    var capTangents = new List<Vector4>(original.tangents);
                    var capColors = new List<Color32>(original.colors32);
                    var bottomIndices = new Dictionary<int, int>();
                    int capIndexCount = capTriangles.Count;

                    for (int i = 0; i < capIndexCount; i += 3)
                    {
                        if (
                            capNormals[capTriangles[i]].y < 0.99f
                            || capNormals[capTriangles[i + 1]].y < 0.99f
                            || capNormals[capTriangles[i + 2]].y < 0.99f
                        )
                        {
                            continue;
                        }

                        // Close the underside by projecting top faces onto the base,
                        // preserving the endpoint ring's opening. Reverse the vertex order
                        // so these new faces are visible from below.
                        for (int corner = 2; corner >= 0; corner--)
                        {
                            int topIndex = capTriangles[i + corner];

                            if (!bottomIndices.TryGetValue(topIndex, out int bottomIndex))
                            {
                                bottomIndex = capVertices.Count;
                                bottomIndices.Add(topIndex, bottomIndex);

                                var vertex = vertices[topIndex];
                                vertex.y = 0f;
                                capVertices.Add(vertex);
                                capNormals.Add(Vector3.down);

                                if (capTangents.Count > 0)
                                {
                                    var tangent = capTangents[topIndex];
                                    tangent.w = -tangent.w;
                                    capTangents.Add(tangent);
                                }

                                if (capColors.Count > 0)
                                {
                                    capColors.Add(capColors[topIndex]);
                                }
                            }

                            capTriangles.Add(bottomIndex);
                        }
                    }

                    _capMesh.SetVertices(capVertices);
                    _capMesh.SetNormals(capNormals);

                    if (capTangents.Count > 0)
                    {
                        _capMesh.SetTangents(capTangents);
                    }

                    if (capColors.Count > 0)
                    {
                        _capMesh.SetColors(capColors);
                    }

                    _capMesh.SetTriangles(capTriangles, 0, false);

                    GuideMesh = UnityEngine.Object.Instantiate(original);
                    GuideMesh.SetTriangles(guideTriangles, 0, false);

                    _capMaterial = new Material(material);
                    _drawArgsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);

                    // Describe one instanced draw: index count, instance count, index start,
                    // base vertex, and starting instance. The instance count changes on upload.
                    _drawArgs[0] = _capMesh.GetIndexCount(0);
                    _drawArgs[2] = _capMesh.GetIndexStart(0);
                    _drawArgs[3] = _capMesh.GetBaseVertex(0);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            internal void CopyWithQueuedRotations(List<ConnGizmoObj> source)
            {
                _instanceCount = source.Count;

                if (_instanceCount > CapInstances.Length)
                {
                    int capacity = Mathf.NextPowerOfTwo(_instanceCount);
                    CapInstances = new ConnGizmoObj[capacity];
                    _instanceBuffer?.Release();
                    _instanceBuffer = new ComputeBuffer(capacity, 36);
                    _capMaterial.SetBuffer("_ObjBuffer", _instanceBuffer);
                }

                for (int i = 0; i < _instanceCount; i++)
                {
                    var marker = source[i];

                    if (QueuedCapRotations.TryGetValue(marker.pos, out var rotation))
                    {
                        marker.rot = rotation;
                    }

                    CapInstances[i] = marker;
                }
            }

            internal void UploadCaps()
            {
                if (_instanceCount == 0)
                {
                    return;
                }

                _instanceBuffer!.SetData(CapInstances, 0, 0, _instanceCount);

                if (_drawArgs[1] != (uint)_instanceCount)
                {
                    _drawArgs[1] = (uint)_instanceCount;
                    _drawArgsBuffer!.SetData(_drawArgs);
                }
            }

            internal void DrawCaps()
            {
                if (_instanceCount > 0)
                {
                    Graphics.DrawMeshInstancedIndirect(
                        _capMesh, 0, _capMaterial,
                        new Bounds(Vector3.zero, new Vector3(10000f, 10000f, 10000f)),
                        _drawArgsBuffer, 0, null, ShadowCastingMode.Off, false
                    );
                }
            }

            public void Dispose()
            {
                _instanceBuffer?.Release();
                _drawArgsBuffer?.Release();

                UnityEngine.Object.Destroy(_capMesh);
                UnityEngine.Object.Destroy(GuideMesh);
                UnityEngine.Object.Destroy(_capMaterial);
            }
        }
    }
}
