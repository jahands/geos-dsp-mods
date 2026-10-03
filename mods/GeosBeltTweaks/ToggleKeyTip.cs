using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace GeosBeltTweaks
{
    // Belt tool key tips showing each toggle key and whether its mode is on.
    [HarmonyPatch]
    internal sealed class ToggleKeyTip(string onText, string offText)
    {
        internal static readonly ToggleKeyTip HalfGrid = new(
            "GeosBeltTweaks half-grid snapping on", "GeosBeltTweaks half-grid snapping off"
        );

        internal static readonly ToggleKeyTip SlopeFromStart = new(
            "GeosBeltTweaks slope from start on", "GeosBeltTweaks slope from start off"
        );

        internal readonly string OnText = onText;
        internal readonly string OffText = offText;

        private UIKeyTipNode? _keyTip;
        private string _onTip = "";
        private string _offTip = "";
        private bool _keyLabelStale;
        private bool _hasKey;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIKeyTips), "RegisterAllTips")]
        private static void RegisterKeyTips(UIKeyTips __instance)
        {
            HalfGrid.Register(__instance);
            SlopeFromStart.Register(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIKeyTips), "UpdateTipDesiredState")]
        private static void UpdateKeyTips(UIKeyTips __instance)
        {
            HalfGrid.Update(__instance, Plugin.ToggleHalfGridSnapKey.Value, HalfGridBeltSnapPatch.Enabled);
            SlopeFromStart.Update(__instance, Plugin.ToggleSlopeFromStartKey.Value, SlopeFromStartPatch.Enabled);
        }

        private void Register(UIKeyTips tips)
        {
            // The game re-registers tips when the language changes.
            _onTip = OnText.Translate();
            _offTip = OffText.Translate();
            _keyTip = tips.RegisterTip("", _offTip);
            _keyLabelStale = true;
        }

        private void Update(UIKeyTips tips, KeyboardShortcut shortcut, bool on)
        {
            if (_keyTip == null)
            {
                return;
            }

            if (_keyLabelStale)
            {
                _keyLabelStale = false;
                _hasKey = shortcut.MainKey != KeyCode.None;
                byte modifier = 0;
                foreach (KeyCode key in shortcut.Modifiers)
                {
                    modifier |= key switch
                    {
                        KeyCode.LeftShift or KeyCode.RightShift => 1,
                        KeyCode.LeftControl or KeyCode.RightControl => 2,
                        KeyCode.LeftAlt or KeyCode.RightAlt => 4,
                        _ => 0,
                    };
                }

                // Label the key the way the game labels its own key bindings.
                _keyTip.SetKeyTip(new CombineKey((int)shortcut.MainKey, modifier, ECombineKeyAction.OnceClick, false).ToTokenString(0), _offTip);
            }

            // Show alongside the belt tool's reset-height tip.
            _keyTip.desired = _hasKey && tips.zeroKeyInBuildMode2.desired;
            _keyTip.tipTextComp.text = on ? _onTip : _offTip;
        }

        internal void OnKeyChanged(object sender, EventArgs e)
        {
            _keyLabelStale = true;
        }

        internal void Destroy()
        {
            if (_keyTip != null)
            {
                _keyTip._Destroy();
            }

            _keyTip = null;
        }
    }
}
