# ShaderWorks Installer

Editor scripts of the `ShaderWorks-Installer.unitypackage` installer. When the `.unitypackage` is imported, `Installer` adds the OpenUPM
scoped registry (`https://package.openupm.com`) with the scopes `com.saesentsessis.unity-shaderworks` to the host project's
`Packages/manifest.json`, then adds `com.saesentsessis.unity-shaderworks` at `Installer.Version` (it never downgrades a newer version).
Packages from Unity's registry need no scope.

This project is not the UPM package (`unity-shaderworks/Assets/root`). `SAESENTSESSIS_INSTALLER_PROJECT` (Player settings, scripting define
symbols) keeps the installer from editing this project's own manifest.

## CI export

The release workflow calls `Saesentsessis.Shaders.Installer.PackageExporter.ExportPackage` through GameCI `unity-builder` to produce
`ShaderWorks-Installer.unitypackage`. `commands/bump-version.ps1` keeps `Installer.Version` in step with `package.json`.
