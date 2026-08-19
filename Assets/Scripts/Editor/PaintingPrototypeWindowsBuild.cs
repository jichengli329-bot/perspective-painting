#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PerspectivePuzzle.EditorTools
{
    /// <summary>Deterministic Windows development build of the painting-reconstruction vertical slice.</summary>
    public static class PaintingPrototypeWindowsBuild
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/PaintingPrototype.unity",
            "Assets/Scenes/PaintingMoonGarden.unity",
            "Assets/Scenes/PaintingRedCliffs.unity",
            "Assets/Scenes/PaintingTwinSeal.unity",
        };
        private const string OutputPath = "Builds/WindowsPainting/PerspectivePainting.exe";
        private const BuildTarget Target = BuildTarget.StandaloneWindows64;

        [MenuItem("Tools/PerspectivePuzzle/Build Painting Windows Development")]
        public static void Build()
        {
            foreach (string scenePath in ScenePaths)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                    throw new InvalidOperationException("Painting Windows build scene is missing: " + scenePath);
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, Target))
                throw new InvalidOperationException("Windows Standalone Support is not installed for " + Target + ".");

            var namedTarget = NamedBuildTarget.Standalone;
            PlayerSettings.SetScriptingBackend(namedTarget, ScriptingImplementation.Mono2x);
            PlayerSettings.productName = "Perspective Painting";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            string absoluteOutput = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutput));
            var options = new BuildPlayerOptions
            {
                scenes = ScenePaths,
                locationPathName = OutputPath,
                target = Target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.Development,
            };

            Debug.Log("[PaintingWindowsBuild] Starting: " + absoluteOutput);
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log("[PaintingWindowsBuild] Result=" + summary.result
                + ", size=" + summary.totalSize + ", warnings=" + summary.totalWarnings
                + ", errors=" + summary.totalErrors);
            if (summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Painting Windows build failed: " + summary.result);

            string dataDirectory = Path.Combine(Path.GetDirectoryName(absoluteOutput),
                Path.GetFileNameWithoutExtension(absoluteOutput) + "_Data");
            if (!File.Exists(absoluteOutput))
                throw new BuildFailedException("Build reported success but EXE is missing: " + absoluteOutput);
            if (!Directory.Exists(dataDirectory))
                throw new BuildFailedException("Build reported success but data directory is missing: " + dataDirectory);
            Debug.Log("[PaintingWindowsBuild] VERIFIED: " + absoluteOutput);
        }
    }
}
#endif
