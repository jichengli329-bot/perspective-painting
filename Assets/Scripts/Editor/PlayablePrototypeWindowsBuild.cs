#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PerspectivePuzzle.EditorTools
{
    /// <summary>
    /// Deterministic Windows development build of the playable prototype:
    /// builds only Assets/Scenes/PlayablePrototype.unity as x86_64 Standalone
    /// with the Mono scripting backend and the Development option into
    /// Builds/Windows/PerspectivePuzzle.exe. Creates the output directory,
    /// throws if the build report is anything but Succeeded, and logs the
    /// total size plus warning/error counts. Never falls back to IL2CPP: if
    /// the installed modules do not support Windows Mono, the exact
    /// platform/build error is logged and rethrown. Callable from the menu or
    /// via -executeMethod PerspectivePuzzle.EditorTools.PlayablePrototypeWindowsBuild.Build.
    /// </summary>
    public static class PlayablePrototypeWindowsBuild
    {
        private const string ScenePath = "Assets/Scenes/PlayablePrototype.unity";
        private const string OutputPath = "Builds/Windows/PerspectivePuzzle.exe";
        private const BuildTarget Target = BuildTarget.StandaloneWindows64;
        private const BuildTargetGroup TargetGroup = BuildTargetGroup.Standalone;

        [MenuItem("Tools/PerspectivePuzzle/Build Windows Development")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Windows build failed: scene not found at " + ScenePath);

            if (!BuildPipeline.IsBuildTargetSupported(TargetGroup, Target))
                throw new InvalidOperationException(
                    "Windows build failed: build target " + Target + " is not supported by this Unity installation "
                    + "(the Windows Standalone Support module is not installed). Install it via Unity Hub and rerun.");

            // Pin the Mono scripting backend explicitly; never fall back to IL2CPP.
            // (The Standalone group covers the Windows x86_64 standalone target.)
            var backendTarget = NamedBuildTarget.Standalone;
            PlayerSettings.SetScriptingBackend(backendTarget, ScriptingImplementation.Mono2x);
            if (PlayerSettings.GetScriptingBackend(backendTarget) != ScriptingImplementation.Mono2x)
                throw new InvalidOperationException(
                    "Windows build failed: could not set the Mono scripting backend for " + Target + ".");

            var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(OutputPath));
            if (outputDirectory != null)
                Directory.CreateDirectory(outputDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = Target,
                targetGroup = TargetGroup,
                options = BuildOptions.Development,
            };

            Debug.Log("[WindowsBuild] Starting " + Target + " Development build of " + ScenePath
                + " -> " + OutputPath + " (scripting backend: Mono).");

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            catch (Exception e)
            {
                // Surface the exact module/platform error without swallowing it.
                Debug.LogError("[WindowsBuild] BuildPlayer threw: " + e);
                throw;
            }

            var summary = report.summary;
            Debug.Log("[WindowsBuild] Result: " + summary.result
                + ", total size: " + summary.totalSize + " bytes ("
                + (summary.totalSize / (1024f * 1024f)).ToString("F2") + " MiB)"
                + ", warnings: " + summary.totalWarnings
                + ", errors: " + summary.totalErrors);

            if (summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("[WindowsBuild] Build failed with result " + summary.result
                    + " (" + summary.totalErrors + " errors, " + summary.totalWarnings + " warnings). See log for details.");

            Debug.Log("[WindowsBuild] Succeeded: " + Path.GetFullPath(OutputPath));
        }
    }
}
#endif
