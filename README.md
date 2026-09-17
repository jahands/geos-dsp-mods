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
compile references; builds do not require the game installed.

## Releases

Mod source and assets under `mods/` are synchronized from a private repository through pull
requests. This repository owns everything else.

Merging to `main` uploads versions missing from Thunderstore to the `Geostyx` namespace using the
`THUNDERSTORE_TOKEN` Actions secret. Bump both `.csproj` and `manifest.json` versions to release.
Locally, `bun run release` uses `TCLI_AUTH_TOKEN` with Thunderstore CLI 0.2.4 installed.
