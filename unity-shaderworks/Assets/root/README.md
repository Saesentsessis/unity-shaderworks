# ShaderWorks

[![OpenUPM](https://img.shields.io/npm/v/com.saesentsessis.unity-shaderworks?label=OpenUPM&registry_uri=https://package.openupm.com&labelColor=333A41 'OpenUPM package')](https://openupm.com/packages/com.saesentsessis.unity-shaderworks/)
[![Releases](https://img.shields.io/github/release/Saesentsessis/unity-shaderworks.svg)](https://github.com/Saesentsessis/unity-shaderworks/releases)
[![License](https://img.shields.io/github/license/Saesentsessis/unity-shaderworks?label=License&labelColor=333A41)](https://github.com/Saesentsessis/unity-shaderworks/blob/main/LICENSE)

Editor tools for Unity shaders. Each tool is its own assembly, so a package takes only what it uses.

## Tools

### Shader variant stripping

Assembly `Saesentsessis.Shaders.Stripping.Editor`, namespace `Saesentsessis.Shaders.Stripping`.

Strips a package's shader variants in player builds, driven by keyword schemas the package describes. It is a
framework for package authors: a package implements `IShaderStrippingSource` (its shaders' keyword schemas, its
settings, and what its features use), and the stripper only ever touches the shaders of those schemas.

- **Automatic** mode keeps the variants the build's features use (the source reports one keep pattern per feature),
  plus every variant listed in shader variant collections. A source that reports nothing keeps everything.
- **Custom** mode keeps the ticked values of every keyword group.
- **Excluded combinations** apply in both modes: every variant with all of an entry's picked values is stripped.
- **Preview**: how many variants a build for the active target keeps, without building (a Burst job counts them).
- **Logs and reports**: one summary line per shader per build, and a JSON report per source in
  `Logs/ShaderStripping/<source id>.json`.
- Settings (`ShaderStrippingSettings`) are serializable and drawn by `ShaderStrippingSettingsGui`, so a package can
  put them on its own Project Settings page.

Stripping only affects player and asset bundle builds; the Editor always has every variant. Anything the stripper
doesn't recognize is kept (it fails open).

Used by [Fuluma](https://github.com/Saesentsessis/Fuluma) (Project Settings > Fuluma).

## Requirements

- Unity **2022.3** or newer
- [`com.unity.burst`](https://docs.unity3d.com/Packages/com.unity.burst@latest) **1.8.25** or newer
- [`com.unity.collections`](https://docs.unity3d.com/Packages/com.unity.collections@latest) **2.6.2** or newer
- [`com.unity.mathematics`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest) **1.3.3** or newer

## Installation

### OpenUPM

```
openupm add com.saesentsessis.unity-shaderworks
```

Or add the registry to `Packages/manifest.json` by hand:

```json
{
  "dependencies": {
    "com.saesentsessis.unity-shaderworks": "0.1.1"
  },
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.saesentsessis"
      ]
    }
  ]
}
```

### Git URL

In the Package Manager, **+ > Add package from git URL...**:

```
https://github.com/Saesentsessis/unity-shaderworks.git?path=unity-shaderworks/Assets/root#0.1.1
```

## License

MIT, see [LICENSE](LICENSE).
