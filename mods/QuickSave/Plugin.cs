using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace QuickSave
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(CompressSaveGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private const string SaveName = "quicksave";
        private const string CompressSaveGuid = "org.soardev.compresssave";

        private ConfigEntry<KeyboardShortcut> _quickSaveKey = null!;
        private ConfigEntry<KeyboardShortcut> _quickLoadKey = null!;

        // CompressSave.PatchSave members that its compressed-save button sets before a manual save.
        private FieldInfo? _useCompressSave;
        private FieldInfo? _enableCompress;
        private MethodInfo? _useCommonSaveCompressionType;

        private void Awake()
        {
            _quickSaveKey = Config.Bind(
                "Hotkeys",
                "QuickSaveKey",
                new KeyboardShortcut(KeyCode.F5),
                $"Saves the game as \"{SaveName}\"."
            );
            _quickLoadKey = Config.Bind(
                "Hotkeys",
                "QuickLoadKey",
                new KeyboardShortcut(KeyCode.F9),
                $"Loads the \"{SaveName}\" save."
            );
            if (Chainloader.PluginInfos.TryGetValue(CompressSaveGuid, out PluginInfo compressSave))
            {
                Type? patchSave = compressSave.Instance.GetType().Assembly.GetType("CompressSave.PatchSave");
                _useCompressSave = patchSave?.GetField("UseCompressSave");
                _enableCompress = patchSave?.GetField("EnableCompress");
                _useCommonSaveCompressionType = patchSave?.GetMethod("UseCommonSaveCompressionType");
                if (_useCompressSave == null || _enableCompress == null || _useCommonSaveCompressionType == null)
                {
                    Logger.LogWarning("CompressSave.PatchSave members not found; quicksaves will not be compressed");
                    _useCompressSave = null;
                }
            }

            Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded");
        }

        private void Update()
        {
            // GameMain is not running while a game loads.
            if (VFInput.inputing || !GameMain.isRunning)
            {
                return;
            }

            // The game blocks its Esc menu, which holds Save and Load, in these states.
            if (!GameMain.instance.isMenuDemo && (GameMain.inOtherScene || GameMain.mainPlayer.respawning))
            {
                return;
            }

            if (_quickSaveKey.Value.IsDown())
            {
                QuickSave();
            }
            else if (_quickLoadKey.Value.IsDown())
            {
                QuickLoad();
            }
        }

        private void QuickSave()
        {
            if (GameMain.instance.isMenuDemo || GameMain.data.guideRunning)
            {
                return;
            }

            // The game's autosave waits until the local star and planet finish loading.
            bool loaded =
                (GameMain.localStar == null || GameMain.localStar.loaded)
                && (GameMain.localPlanet == null || (GameMain.localPlanet.loaded && GameMain.localPlanet.factoryLoaded));
            bool saved = loaded && SaveGame();
            if (saved)
            {
                UIAutoSave.lastSaveTick = GameMain.gameTick;
            }
            else
            {
                Logger.LogWarning(loaded ? "Quicksave failed" : "Quicksave skipped while the local star or planet loads");
            }

            UIRealtimeTip.Popup((saved ? "保存成功" : "保存失败").Translate(), sound: !saved);
        }

        private bool SaveGame()
        {
            if (_useCompressSave == null || !(bool)_enableCompress!.GetValue(null))
            {
                return GameSave.SaveCurrentGame(SaveName);
            }

            _useCompressSave.SetValue(null, true);
            _useCommonSaveCompressionType!.Invoke(null, null);
            try
            {
                return GameSave.SaveCurrentGame(SaveName);
            }
            finally
            {
                // CompressSave clears the flag only after a successful write.
                _useCompressSave.SetValue(null, false);
            }
        }

        private void QuickLoad()
        {
            if (!GameSave.SaveExist(SaveName))
            {
                UIRealtimeTip.Popup("No quicksave");
                return;
            }

            GameDesc? desc = GameSave.LoadGameDesc(SaveName);
            if (desc == null)
            {
                UIRealtimeTip.Popup("无效存档".Translate());
                return;
            }

            DSPGame.StartGame(SaveName);
            DSPGame.WillEnableSandboxTools = desc.isSandboxMode;
            DSPGame.LoadWithGoalLevel = desc.goalLevel;
            DSPGame.RecordPlayVersion();
        }
    }
}
