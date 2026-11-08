# @repo/dsp-mod-quicksave

## 0.2.0

### Minor Changes

- 2d38536: feat: keep the last 5 quicksaves
  
  F5 saves to `quicksave_1` through `quicksave_5`, overwriting the oldest, and F9 loads the newest of them. `QuickSaveCount` (1-20, default 5) sets the number of quicksaves. F9 no longer loads the old `quicksave` save.

### Patch Changes

- 4ce7409: fix: ignore the quick-save and quick-load hotkeys while a build menu category is open
- 576873e: chore: name the mod QuickSave in the README title and plugin name, and match the description to the README
- 576873e: docs: link CameraMemory from the README

## 0.1.0

### Minor Changes

- 64994ae: feat: compress quicksaves when CompressSave is installed
- b3aaa2b: feat: add Quick Save with quick-save (F5) and quick-load (F9) hotkeys
