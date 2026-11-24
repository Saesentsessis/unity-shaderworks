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
using UnityEditor;

namespace Saesentsessis.Shaders.Installer
{
    [InitializeOnLoad]
    public static partial class Installer
    {
        public const string PackageId = "com.saesentsessis.unity-shaderworks";
        public const string Version = "0.1.1";

        static Installer()
        {
#if !SAESENTSESSIS_INSTALLER_PROJECT
            AddScopedRegistryIfNeeded(ManifestPath);
#endif
        }
    }
}