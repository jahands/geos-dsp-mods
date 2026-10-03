# Geo's DSP Mods

Mods for Dyson Sphere Program, released under the [MIT license](LICENSE).

- [Geo's Belt Tweaks](mods/GeosBeltTweaks/README.md)

## Development

Install Bun, pnpm, the .NET 10 SDK, and `zip`, then run:

```sh
pnpm install
bun run check
bun turbo build
```

Each mod's validated Thunderstore ZIP is written to `mods/<mod>/dist/<mod>.zip`. NuGet supplies
compile references; builds do not require the game installed. `DysonSphereProgram.GameLibs` comes
from a private GitHub Packages feed, so restoring requires `NUGET_GITHUB_TOKEN` set to a classic PAT
with `read:packages` access to the `uuid-rocks` packages.

## Releases

Mod source and assets under `mods/` are synchronized from a private repository through pull
requests. This repository owns everything else.

CI reads `NUGET_GITHUB_TOKEN` and `THUNDERSTORE_TOKEN` from the `geos-dsp-mods` Infisical project
through GitHub OIDC; only `main` can read `THUNDERSTORE_TOKEN`. Merging to `main` uploads versions
missing from Thunderstore to the `Geostyx` namespace. Bump both `.csproj` and `manifest.json` versions to release.
Locally, `bun run release` uses `TCLI_AUTH_TOKEN` with Thunderstore CLI 0.2.4 installed.
