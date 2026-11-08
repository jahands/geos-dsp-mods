using System;
using System.IO;
using BepInEx;
using crecheng.DSPModSave;
using HarmonyLib;
using UnityEngine;

namespace CameraMemory
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(DSPModSavePlugin.MODGUID)]
    [HarmonyPatch]
    public class Plugin : BaseUnityPlugin, IModCanSave
    {
        private Harmony? _harmony;

        // Camera state from the last imported save; null when it has none.
        private static CameraState? _savedCamera;

        [Serializable]
        internal class CameraState
        {
            public float rtsYaw;
            public float rtsPitchCoef;
            public float rtsDistCoef;
            public Quaternion sailRotation;
            public float sailDistCoef;
        }

        private void Awake()
        {
            _harmony = Harmony.CreateAndPatchAll(typeof(Plugin), MyPluginInfo.PLUGIN_GUID);
            Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            _harmony = null;
        }

        [HarmonyPatch(typeof(DSPGame), nameof(DSPGame.StartGame), typeof(string))]
        [HarmonyPostfix]
        private static void StartGamePostfix()
        {
            // StartGame creates the GameLoader, which raises onLoadComplete before the first game frame renders.
            FindObjectOfType<GameLoader>().onLoadComplete += RestoreCamera;
        }

        private static void RestoreCamera()
        {
            if (_savedCamera == null)
            {
                return;
            }

            GameCamera camera = GameCamera.instance;
            SailPoser sail = camera.sailPoser;
            sail.targetURotWanted = _savedCamera.sailRotation;
            sail.distCoefWanted = _savedCamera.sailDistCoef;
            sail.ToWanted();

            RTSPoser rts = camera.rtsPoser;
            rts.yawWanted = _savedCamera.rtsYaw;
            rts.pitchCoefWanted = _savedCamera.rtsPitchCoef;
            rts.distCoefWanted = _savedCamera.rtsDistCoef;
            rts.ToWanted();
            if (!GameMain.mainPlayer.sailing)
            {
                // GameCamera.Logic offsets the RTS target by the pitch of the previous frame's camera.
                rts.cameraPose.ApplyToCamera(GameCamera.main);
            }
        }

        public void Export(BinaryWriter w)
        {
            GameCamera camera = GameCamera.instance;
            var state = new CameraState
            {
                rtsYaw = camera.rtsPoser.yawWanted,
                rtsPitchCoef = camera.rtsPoser.pitchCoefWanted,
                rtsDistCoef = camera.rtsPoser.distCoefWanted,
                sailRotation = camera.sailPoser.targetURotWanted,
                sailDistCoef = camera.sailPoser.distCoefWanted,
            };
            w.Write(JsonUtility.ToJson(state));
        }

        public void Import(BinaryReader r)
        {
            try
            {
                _savedCamera = JsonUtility.FromJson<CameraState>(r.ReadString());
            }
            catch (Exception e)
            {
                _savedCamera = null;
                Logger.LogWarning($"Ignoring unreadable camera state: {e.Message}");
            }
        }

        public void IntoOtherSave()
        {
            _savedCamera = null;
        }
    }
}
