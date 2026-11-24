/*
┌────────────────────────────────────────────────────────────────────────────┐
│  Author: Saesentsessis (https://github.com/Saesentsessis)                  │
│  Repository: GitHub (https://github.com/Saesentsessis/unity-shaderworks)   │
│  Copyright (c) 2025–2026 Saesentsessis                                     │
│  Licensed under the MIT License.                                           │
│  See the LICENSE file in the project root for more information.            │
└────────────────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Saesentsessis.Shaders.Installer
{
    public static class PackageExporter
    {
        public static void ExportPackage()
        {
            var packagePath = "Assets/ShaderWorks Installer";
            var outputPath = "build/ShaderWorks-Installer.unitypackage";

            // Ensure build directory exists
            var buildDir = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(buildDir))
            {
                Directory.CreateDirectory(buildDir);
            }

            // Export the package
            AssetDatabase.ExportPackage(packagePath, outputPath, ExportPackageOptions.Recurse);

            Debug.Log($"Package exported to: {outputPath}");
        }
    }
}