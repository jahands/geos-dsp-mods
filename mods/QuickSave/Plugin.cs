using System;
using System.IO;
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
        private ConfigEntry<int> _quickSaveCount = null!;

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
                "Saves the game over the oldest quicksave."
            );
            _quickLoadKey = Config.Bind(
                "Hotkeys",
                "QuickLoadKey",
                new KeyboardShortcut(KeyCode.F9),
                "Loads the newest quicksave."
            );
            _quickSaveCount = Config.Bind(
                "Saves",
                "QuickSaveCount",
                5,
                new ConfigDescription(
                    "Number of quicksaves to keep.",
                    new AcceptableValueRange<int>(1, 20)
                )
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

            // The build menu uses F1-F10 to pick items and filters while a category is open.
            if (UIRoot.instance.uiGame.buildMenu.currentCategory != 0)
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
            bool saved = loaded && SaveGame(SlotName(OldestSlot()));
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

        private bool SaveGame(string name)
        {
            if (_useCompressSave == null || !(bool)_enableCompress!.GetValue(null))
            {
                return GameSave.SaveCurrentGame(name);
            }

            _useCompressSave.SetValue(null, true);
            _useCommonSaveCompressionType!.Invoke(null, null);
            try
            {
                return GameSave.SaveCurrentGame(name);
            }
            finally
            {
                // CompressSave clears the flag only after a successful write.
                _useCompressSave.SetValue(null, false);
            }
        }

        private int OldestSlot()
        {
            int oldest = 1;
            for (int slot = 2; slot <= _quickSaveCount.Value; slot++)
            {
                if (SaveTime(slot) < SaveTime(oldest))
                {
                    oldest = slot;
                }
            }

            return oldest;
        }

        private int NewestSlot()
        {
            int newest = 1;
            for (int slot = 2; slot <= _quickSaveCount.Value; slot++)
            {
                if (SaveTime(slot) > SaveTime(newest))
                {
                    newest = slot;
                }
            }

            return newest;
        }

        private static string SlotName(int slot) => $"{SaveName}_{slot}";

        private static DateTime SaveTime(int slot)
        {
            string path = GameSave.SavePath(SlotName(slot));
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }

        private void QuickLoad()
        {
            string name = SlotName(NewestSlot());
            if (!GameSave.SaveExist(name))
            {
                UIRealtimeTip.Popup("No quicksave");
                return;
            }

            GameDesc? desc = GameSave.LoadGameDesc(name);
            if (desc == null)
            {
                UIRealtimeTip.Popup("无效存档".Translate());
                return;
            }

            DSPGame.StartGame(name);
            DSPGame.WillEnableSandboxTools = desc.isSandboxMode;
            DSPGame.LoadWithGoalLevel = desc.goalLevel;
            DSPGame.RecordPlayVersion();
        }
    }
}
