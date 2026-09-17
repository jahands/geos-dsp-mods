# Geo's Belt Tweaks

Geo's belt tweaks for Dyson Sphere Program.

## Tweaks

- **Show belt preview tilt:** Belt previews show the belt's slope and tilt.
- **Match starting belt height:** Match the height of the belt or splitter you start from, rounded to the nearest build level. Splitters with ports at multiple heights use the base level; raise the belt height to use an upper port.
- **Match starting belt tier:** Extending a belt selects its tier.
- **Toggle belt surface height:** Press numpad 0 (or your rebound reset-height hotkey) to reset to the surface, then press it again to restore the previous height. Closing the belt tool clears the remembered height. Tilt still resets on each press.
- **Remember belt free-angle mode:** Keep free-angle mode selected with R when closing and reopening the belt tool.

All tweaks are enabled by default. Set `ShowBeltPreviewTilt`, `MatchStartingBeltHeight`, `MatchStartingBeltTier`, `ToggleBeltSurfaceHeight`, or `RememberBeltFreeAngleMode` to `false` under `[Building]` in `BepInEx/config/com.geostyx.dsp.geosbelttweaks.cfg` to disable a tweak. In-game configuration changes apply immediately to previews and the surface-height toggle, to the next belt you start for height and tier, and when reopening the belt tool for free-angle mode. After editing the file, reload the configuration or restart the game.

## Setting up a development environment

Install the .NET 10 SDK. The mod targets .NET Framework 4.7.2. References restore from NuGet, so no game install is needed to build.

```sh
dotnet build GeosBeltTweaks/GeosBeltTweaks.csproj -c Release
```

Run this from the public repository root. The DLL is written to `GeosBeltTweaks/bin/Release/net472/GeosBeltTweaks.dll`. To build and validate Thunderstore ZIPs, run `python3 scripts/release.py` from the repository root, then import `GeosBeltTweaks/dist/GeosBeltTweaks.zip` into a dedicated r2modman profile.

NuGet restores use the committed `packages.lock.json` in locked mode. When intentionally updating a package reference, regenerate the lock from the mod directory with `dotnet restore -p:RestoreLockedMode=false`, review both files, then rebuild.

## Compatibility and verification

The compile-time baseline is BepInEx 5.4.17, `DysonSphereProgram.GameLibs` 0.10.34.28347-r.0, and Unity 2022.3.62. In-game behavior is untested.

Before releasing gameplay changes:

- Verify the patched methods against the intended game's assemblies; compile-time reference packages do not establish runtime behavior.
- Load in the test profile and check BepInEx logs for successful startup and patch failures. Exercise each tweak on fresh and existing saves, save/reload, return-to-menu/load-another-save, configuration toggles, and overlapping mods as applicable.
- Record the exact tested game, loader, and overlapping mod versions and results in the release notes.
