# Changelog

## [0.1.1] - 2026-11-24

### Fixed

- The installer `.unitypackage` added the package template's OpenUPM scopes instead of ShaderWorks', so the package it
  added couldn't be resolved. It now adds exactly `com.saesentsessis.unity-shaderworks`.

## [0.1.0] - 2026-11-24

### Added

- **Shader variant stripping** (`Saesentsessis.Shaders.Stripping.Editor`), first used in Fuluma 0.1.0: keyword schemas
  with stable ids and former ids for renames, `IShaderStrippingSource` for packages, Automatic and Custom modes,
  excluded combinations, shader variant collections, a Burst variant count preview, a summary log line per shader and
  a JSON report per source in `Logs/ShaderStripping/`. Unrecognized variants are kept.
- Requires Unity 2022.3 or newer; dependencies on `com.unity.burst` 1.8.25, `com.unity.collections` 2.6.2 and
  `com.unity.mathematics` 1.3.3.
