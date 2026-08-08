#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PerspectivePuzzle.EditorTools
{
    /// <summary>
    /// One-time project bootstrap: creates the URP pipeline asset, assigns it as the
    /// active render pipeline, and creates the minimal Bootstrap scene described in
    /// docs/ARCHITECTURE.md. Idempotent: safe to run again from the menu.
    /// </summary>
    public static class ProjectBootstrap
    {
        private const string RendererDataPath = "Assets/Settings/URP/UniversalRendererData.asset";
        private const string PipelineAssetPath = "Assets/Settings/URP/UniversalRenderPipelineAsset.asset";
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";

        [MenuItem("Tools/Perspective Puzzle/Run Bootstrap")]
        public static void RunBootstrap()
        {
            EnsureTopLevelFolders();
            CreateUrpPipeline();
            CreateBootstrapScene();
            AssetDatabase.SaveAssets();
            Debug.Log("PerspectivePuzzle bootstrap complete.");
        }

        [MenuItem("Tools/Perspective Puzzle/Verify Setup")]
        public static void VerifySetup()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            Debug.Log("Default render pipeline: " + (pipeline != null ? pipeline.name + " (" + pipeline.GetType().Name + ")" : "NULL"));
            Debug.Log("Active render pipeline:  " + (GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "NULL"));

            var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            Debug.Log("Opened scene: " + scene.path + " | rootCount=" + scene.rootCount);

            var gameRoot = GameObject.Find("GameRoot");
            var buildCamera = GameObject.Find("BuildCamera");
            var dirLight = GameObject.Find("Directional Light");
            Debug.Log("GameRoot=" + (gameRoot != null) + " BuildCamera=" + (buildCamera != null) + " DirectionalLight=" + (dirLight != null));

            // No MonoBehaviours exist yet, so a missing-script scan is a formality for the future.
            int missing = 0;
            foreach (var go in scene.GetRootGameObjects())
                missing += CountMissingScripts(go);
            Debug.Log("Missing script references: " + missing);
        }

        private static int CountMissingScripts(GameObject go)
        {
            int count = 0;
            foreach (var component in go.GetComponentsInChildren<Component>(true))
                if (component == null)
                    count++;
            return count;
        }

        private static void EnsureTopLevelFolders()
        {
            foreach (var folder in new[] { "Art", "Audio", "Content", "Prefabs", "Scenes", "Scripts", "Settings", "Tests" })
                if (!AssetDatabase.IsValidFolder("Assets/" + folder))
                    AssetDatabase.CreateFolder("Assets", folder);

            if (!AssetDatabase.IsValidFolder("Assets/Settings/URP"))
                AssetDatabase.CreateFolder("Assets/Settings", "URP");
        }

        private static void CreateUrpPipeline()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererDataPath);
            }

            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipelineAsset == null)
            {
                pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
            }

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            QualitySettings.renderPipeline = pipelineAsset;
            EditorUtility.SetDirty(pipelineAsset);
            Debug.Log("URP asset assigned: " + PipelineAssetPath);
        }

        private static void CreateBootstrapScene()
        {
            if (File.Exists(BootstrapScenePath))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var gameRoot = new GameObject("GameRoot");
            CreateChild("PuzzleSession", gameRoot);

            var gridStage = CreateChild("GridStage", gameRoot);
            CreateChild("PlacementSurface", gridStage);
            CreateChild("PieceRoot", gridStage);
            CreateChild("PreviewRoot", gridStage);

            CreateChild("ProjectionBoard", gameRoot);

            var cameras = CreateChild("Cameras", gameRoot);
            var mainCamera = GameObject.Find("Main Camera");
            if (mainCamera != null)
            {
                mainCamera.name = "BuildCamera";
                mainCamera.transform.SetParent(cameras.transform, false);
            }
            CreateChild("RevealTarget", cameras);

            var lighting = CreateChild("Lighting", gameRoot);
            var dirLight = GameObject.Find("Directional Light");
            if (dirLight != null)
                dirLight.transform.SetParent(lighting.transform, false);

            CreateChild("UI", gameRoot);

            if (!Directory.Exists("Assets/Scenes"))
                Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, BootstrapScenePath);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == BootstrapScenePath))
                scenes.Add(new EditorBuildSettingsScene(BootstrapScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("Bootstrap scene created: " + BootstrapScenePath);
        }

        private static GameObject CreateChild(string name, GameObject parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }
    }
}
#endif
