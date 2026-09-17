# Geo's DSP Mods

Mods for Dyson Sphere Program, released under the [MIT license](LICENSE).

- [Geo's Belt Tweaks](mods/GeosBeltTweaks/README.md)

## Development

Install Bun, pnpm, the .NET 10 SDK, and `zip`/`unzip`, then run:

```sh
pnpm install
bun turbo build check:types test
```

Mods live in `mods/`; release commands and archive validation live in
`packages/tools`. Each mod's validated Thunderstore ZIP is written to
`mods/<mod>/dist/<mod>.zip`. Assets are committed in each mod's `img/` directory.
NuGet supplies compile references; builds do not require the game installed.

## Releases

The author develops mod source privately and synchronizes it here through pull
requests. Only mod source and assets are synchronized. This repository maintains
its own workspace manifests, tooling, workflows, and documentation.

Pull requests build and validate packages. Merging to `main` uploads versions
missing from Thunderstore. Update both `.csproj` and `manifest.json` versions for
new releases. Published versions are skipped; source or asset changes without a
version bump cannot replace an existing upload. Rerun the Release workflow to
retry interrupted uploads.

Publishing requires the Actions variable `THUNDERSTORE_NAMESPACE` and secret
`THUNDERSTORE_TOKEN`. Locally, `bun run release` uses `THUNDERSTORE_NAMESPACE`
and `TCLI_AUTH_TOKEN`, with Thunderstore CLI 0.2.4 installed.
