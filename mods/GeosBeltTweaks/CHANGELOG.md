# @repo/dsp-mod-geosbelttweaks

## 0.2.0

### Minor Changes

- 92956fb: feat: tilt belt preview caps to match the belt's slope and tilt
- fb0479d: feat: remember belt free-angle mode when reopening the belt tool
  
  Add a default-on RememberBeltFreeAngleMode setting that preserves free-angle mode between belt-building sessions, without per-frame hooks or allocations.
- 5b21ffb: feat: scaffold Geo's Belt Tweaks DSP mod
- 6c2c5f7: feat: default new belt runs to the starting belt's height and tier
  
  Add independently configurable, default-on tweaks that select the starting belt's height and tier when extending a built or unbuilt belt. Preserve manual height and tier changes afterward and the current selection when starting on empty ground.
- 91d5616: feat: match starting splitter height when building belts
  
  Extend MatchStartingBeltHeight to built and unbuilt splitters while keeping the selected belt tier.
- 820d5a1: feat: toggle between surface and previous belt height with the reset-height hotkey
  
  Add a default-on ToggleBeltSurfaceHeight setting. Remember the height before resetting to the surface and restore it on the next press, preserving the vanilla tilt reset and clearing the remembered height when the belt tool closes. Store the height without allocating or looking up per-tool state, and only run toggle logic on reset-height key presses.

### Patch Changes

- b93fa03: feat: version DSP mods from package.json
- 5abebdc: chore: align Geo's Belt Tweaks scaffold with DSP modding practices
  
  - Pin build dependencies and the C# language version, and lock NuGet resolution.
  - Keep game and loader assemblies out of runtime outputs.
  - Retain Harmony ownership during startup and roll back patches on initialization failure.
  - Exclude generated build files from formatting checks.
  - Validate release archives automatically and replace the manual archive checklist with the validation command.
  - Document the reference baseline and runtime release checks without claiming in-game validation.
- 5b21ffb: chore: move Geo's Belt Tweaks into the geos-dsp-mods workspace
- 5cc9efe: fix: keep queued belt preview caps aligned with the belt's slope and tilt
- 7b5833d: docs: rewrite the Geo's Belt Tweaks README for Thunderstore
- 5b21ffb: chore: rebrand the mod as Geo's Belt Tweaks
- d15620b: feat: prepare Geo's Belt Tweaks for public releases under the Geostyx namespace
