#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace PerspectivePuzzle.EditorTools
{
    /// <summary>
    /// Builds the visual-foundation prototype scene described in docs/VISUAL_FOUNDATION.md:
    /// a warm ivory stage with a raised light-stone plinth, an asymmetrical cluster of
    /// muted-teal rounded blocks, one coral accent, and a framed near-white projection
    /// board, lit and graded for a first screenshot. Callable from the menu or via
    /// -executeMethod PerspectivePuzzle.EditorTools.VisualPrototypeSceneBuilder.BuildScene.
    /// </summary>
    public static class VisualPrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/VisualPrototype.unity";
        private const string MaterialsFolder = "Assets/Art/Materials";
        private const string MeshesFolder = "Assets/Art/Meshes";
        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private const string ToyShaderName = "PerspectivePuzzle/ToyLit";

        /// <summary>Camera pose that frames the 16:9 three-quarter view.</summary>
        private static readonly Vector3 CameraPosition = new Vector3(-9f, 8.5f, -10.5f);
        private static readonly Vector3 CameraTarget = new Vector3(0f, 0.9f, 0.5f);

        /// <summary>Palette from docs/VISUAL_FOUNDATION.md, tuned for URP Lit.</summary>
        private static readonly Color WarmIvory = new Color(0.949f, 0.933f, 0.902f);   // #F2EEE6
        private static readonly Color LightStone = new Color(0.871f, 0.843f, 0.792f);  // #DED7CA
        private static readonly Color MutedTeal = new Color(0.298f, 0.604f, 0.573f);   // #4C9A92
        private static readonly Color SoftCoral = new Color(0.898f, 0.541f, 0.396f);   // #E58A65
        private static readonly Color BoardWhite = new Color(0.969f, 0.957f, 0.933f);  // near-white surface
        private static readonly Color FrameBlueGray = new Color(0.243f, 0.290f, 0.322f);

        [MenuItem("Tools/PerspectivePuzzle/Build Visual Prototype Scene")]
        public static void BuildScene()
        {
            EnsureFolder("Assets", "Art");
            EnsureFolder("Assets/Art", "Materials");
            EnsureFolder("Assets/Art", "Meshes");
            EnsureFolder("Assets", "Scenes");

            // Start from a clean empty scene so the build is reproducible.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("Visual Prototype").transform;
            BuildStage(root);
            BuildLights(root);
            BuildCamera(root);
            BuildPostProcessing(root);

            // VisualPrototype is the first (and only) enabled build scene.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Visual Prototype scene built at " + ScenePath);
        }

        private static void BuildStage(Transform root)
        {
            var floorMat = GetOrCreateMaterial("Mat_WarmIvory", WarmIvory, 0.25f, 0f);
            var plinthMat = GetOrCreateMaterial("Mat_LightStone", LightStone, 0.30f, 0f);
            var tealMat = GetOrCreateMaterial("Mat_MutedTeal", MutedTeal, 0.55f, 0.02f);
            var coralMat = GetOrCreateMaterial("Mat_Coral", SoftCoral, 0.45f, 0f);
            var boardMat = GetOrCreateMaterial("Mat_BoardWhite", BoardWhite, 0.50f, 0f);
            var frameMat = GetOrCreateMaterial("Mat_FrameBlueGray", FrameBlueGray, 0.35f, 0f);

            // Large warm backdrop/floor; its color matches the camera background so the
            // horizon seam disappears.
            var floorMesh = GetOrCreateMesh("RoundedFloorCompact", new Vector3(19f, 0.5f, 14f), 0.12f, 6);
            CreateMeshRenderer(root, "Floor", new Vector3(0f, -0.25f, 0f), floorMesh, floorMat, Quaternion.identity);

            // Raised light-stone plinth holding the composition.
            var plinthMesh = GetOrCreateMesh("RoundedPlinth", new Vector3(7.4f, 0.6f, 4.8f), 0.15f, 10);
            CreateMeshRenderer(root, "Plinth", new Vector3(-1.2f, 0.3f, 0.2f), plinthMesh, plinthMat, Quaternion.identity);

            // Deliberately asymmetrical cluster of muted-teal rounded blocks.
            var cluster = new[]
            {
                new BlockPlacement("TealBlock_Tall",  new Vector3(1.9f, 1.0f, 1.2f), 0.16f, new Vector3(-2.3f, 1.1f, 1.4f), 8f),
                new BlockPlacement("TealBlock_Low",   new Vector3(1.0f, 0.85f, 1.0f), 0.14f, new Vector3(-1.2f, 1.025f, 0.4f), 16f),
                new BlockPlacement("TealBlock_Wide",  new Vector3(1.3f, 0.7f, 1.6f), 0.13f, new Vector3(0.15f, 0.95f, 1.5f), -12f),
                new BlockPlacement("TealBlock_Tower", new Vector3(0.75f, 1.5f, 0.75f), 0.13f, new Vector3(1.5f, 1.35f, 0.7f), 6f),
                new BlockPlacement("TealBlock_Stub",  new Vector3(0.7f, 0.55f, 0.7f), 0.11f, new Vector3(-2.3f, 1.875f, 1.4f), 24f),
                new BlockPlacement("TealBlock_Cap",   new Vector3(0.6f, 0.5f, 0.6f), 0.10f, new Vector3(0.15f, 1.55f, 1.5f), -20f),
            };
            foreach (var block in cluster)
            {
                var mesh = GetOrCreateMesh(block.MeshName, block.Size, block.Radius, 10);
                CreateMeshRenderer(root, block.MeshName, block.Position, mesh, tealMat, Quaternion.Euler(0f, block.Yaw, 0f));
            }

            // Single restrained coral accent at the plinth's front edge.
            var accentMesh = GetOrCreateMesh("CoralAccent", new Vector3(0.85f, 0.5f, 0.6f), 0.10f, 8);
            CreateMeshRenderer(root, "CoralAccent", new Vector3(2.0f, 0.85f, -1.3f), accentMesh, coralMat, Quaternion.Euler(0f, -18f, 0f));

            BuildProjectionBoard(root, boardMat, frameMat, tealMat, coralMat);
        }

        /// <summary>
        /// Near-white projection board with a dark blue-gray rounded frame and two rear
        /// support legs, standing in the right-rear third of the frame.
        /// </summary>
        private static void BuildProjectionBoard(Transform root, Material boardMaterial, Material frameMaterial,
            Material targetMaterial, Material targetAccentMaterial)
        {
            var surfaceMesh = GetOrCreateMesh("BoardSurface", new Vector3(2.9f, 2.1f, 0.12f), 0.05f, 8);
            var barHMesh = GetOrCreateMesh("FrameBar_Horizontal", new Vector3(3.1f, 0.14f, 0.18f), 0.05f, 6);
            var barVMesh = GetOrCreateMesh("FrameBar_Vertical", new Vector3(0.14f, 2.24f, 0.18f), 0.05f, 6);
            var legMesh = GetOrCreateMesh("BoardLeg", new Vector3(0.14f, 2.1f, 0.14f), 0.05f, 6);

            var board = new GameObject("Projection Board").transform;
            board.SetParent(root, false);
            board.position = new Vector3(-5.4f, 0f, 4.4f);

            // Face the camera (horizontal yaw only), then tilt the top back slightly.
            var towardCamera = CameraPosition - board.position;
            towardCamera.y = 0f;
            board.rotation = Quaternion.LookRotation(towardCamera.normalized) * Quaternion.Euler(6f, 0f, 0f);

            CreateMeshRenderer(board, "Surface", Vector3.zero, surfaceMesh, boardMaterial, Quaternion.identity);
            CreateMeshRenderer(board, "Frame Top", new Vector3(0f, 1.06f, 0.08f), barHMesh, frameMaterial, Quaternion.identity);
            CreateMeshRenderer(board, "Frame Bottom", new Vector3(0f, -1.06f, 0.08f), barHMesh, frameMaterial, Quaternion.identity);
            CreateMeshRenderer(board, "Frame Left", new Vector3(-1.38f, 0f, 0.08f), barVMesh, frameMaterial, Quaternion.identity);
            CreateMeshRenderer(board, "Frame Right", new Vector3(1.38f, 0f, 0.08f), barVMesh, frameMaterial, Quaternion.identity);

            // A compact five-cell target makes the board communicate the puzzle premise
            // even before gameplay and UI are present.
            var targetTileMesh = GetOrCreateMesh("ProjectionTargetTile", new Vector3(0.38f, 0.38f, 0.06f), 0.03f, 6);
            var targetCells = new[]
            {
                new Vector2(0f, 0.42f),
                new Vector2(-0.42f, 0f),
                Vector2.zero,
                new Vector2(0.42f, 0f),
                new Vector2(0f, -0.42f),
            };
            for (int i = 0; i < targetCells.Length; i++)
            {
                var cell = targetCells[i];
                CreateMeshRenderer(board, "Target Cell " + i,
                    new Vector3(cell.x, cell.y, 0.095f), targetTileMesh,
                    i == 2 ? targetAccentMaterial : targetMaterial, Quaternion.identity);
            }

            // Two rear legs run from the lower corners down-and-back to the floor.
            float legLength = 2.1f;
            var legAxis = new Vector3(0f, -Mathf.Cos(33f * Mathf.Deg2Rad), -Mathf.Sin(33f * Mathf.Deg2Rad)).normalized;
            var legRotation = Quaternion.FromToRotation(Vector3.up, legAxis);
            var attach = new Vector3(1.38f, -1.03f, 0.06f);

            // Raise the board so both leg bottoms land exactly on the floor (y = 0).
            var bottomLocal = attach + legAxis * legLength;
            board.position = new Vector3(-5.4f, -(board.rotation * bottomLocal).y, 4.4f);

            foreach (float side in new[] { -1f, 1f })
            {
                attach.x = 1.38f * side;
                var centerLocal = attach + legAxis * (legLength * 0.5f);
                CreateMeshRenderer(board, "Support Leg " + (side < 0f ? "Left" : "Right"), centerLocal, legMesh, frameMaterial, legRotation);
            }
        }

        private static void BuildLights(Transform root)
        {
            // Soft shadow-casting key from behind-right so contact shadows fall toward
            // the camera and read across the plinth.
            var keyGo = new GameObject("Key Light (Directional)");
            keyGo.transform.SetParent(root, false);
            keyGo.transform.rotation = Quaternion.Euler(50f, -145f, 0f);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 0.72f;
            key.color = new Color(1f, 0.965f, 0.91f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.8f;

            // Low-intensity cool fill from the camera side keeps teal readable in shadow.
            var fillGo = new GameObject("Fill Light (Directional)");
            fillGo.transform.SetParent(root, false);
            fillGo.transform.rotation = Quaternion.Euler(15f, 40f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.12f;
            fill.color = new Color(0.82f, 0.9f, 1f);
            fill.shadows = LightShadows.None;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.21f, 0.20f);
        }

        private static void BuildCamera(Transform root)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(root, false);
            camGo.transform.position = CameraPosition;
            camGo.transform.rotation = Quaternion.LookRotation((CameraTarget - CameraPosition).normalized);

            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f; // 12 world units tall; at 16:9 roughly 21.3 wide
            cam.aspect = 16f / 9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = WarmIvory;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.allowHDR = true;

            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        private static void BuildPostProcessing(Transform root)
        {
            var profile = GetOrCreateProfile();

            if (!profile.TryGet<Tonemapping>(out var tonemapping))
                tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.Neutral;

            if (!profile.TryGet<ColorAdjustments>(out var colorAdjustments))
                colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.value = -0.55f;
            colorAdjustments.contrast.value = 4f;
            colorAdjustments.saturation.value = 12f;
            colorAdjustments.hueShift.value = 0f;
            colorAdjustments.colorFilter.value = Color.white;

            if (!profile.TryGet<Vignette>(out var vignette))
                vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.22f;
            vignette.smoothness.value = 0.4f;
            vignette.rounded.value = true;
            vignette.color.value = new Color(0.12f, 0.14f, 0.16f);

            var volumeGo = new GameObject("Post-Process Volume");
            volumeGo.transform.SetParent(root, false);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static VolumeProfile GetOrCreateProfile()
        {
            const string path = MaterialsFolder + "/VisualPrototypeProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null)
                return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static void EnsureFolder(string parentFolder, string childName)
        {
            string path = parentFolder + "/" + childName;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parentFolder, childName);
        }

        private static Mesh GetOrCreateMesh(string assetName, Vector3 size, float radius, int segments)
        {
            string path = MeshesFolder + "/" + assetName + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;
            mesh = RoundedBoxMeshFactory.Create(size, radius, segments);
            mesh.name = assetName;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Material GetOrCreateMaterial(string assetName, Color color, float smoothness, float metallic)
        {
            string path = MaterialsFolder + "/" + assetName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool preserveToyColor = assetName == "Mat_MutedTeal" || assetName == "Mat_Coral" ||
                                    assetName == "Mat_BoardWhite";
            string shaderName = preserveToyColor ? ToyShaderName : LitShaderName;
            var desiredShader = Shader.Find(shaderName);
            if (desiredShader == null)
                throw new System.InvalidOperationException("Required URP shader was not found: " + shaderName);

            if (material == null)
            {
                material = new Material(desiredShader) { name = assetName };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = desiredShader;
            material.color = color;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", preserveToyColor ? 0.18f : smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateMeshRenderer(Transform parent, string name, Vector3 localPosition,
            Mesh mesh, Material material, Quaternion localRotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return go;
        }

        /// <summary>A rounded block on the plinth: size, corner radius, center and yaw.</summary>
        private readonly struct BlockPlacement
        {
            public readonly string MeshName;
            public readonly Vector3 Size;
            public readonly float Radius;
            public readonly Vector3 Position;
            public readonly float Yaw;

            public BlockPlacement(string meshName, Vector3 size, float radius, Vector3 position, float yaw)
            {
                MeshName = meshName;
                Size = size;
                Radius = radius;
                Position = position;
                Yaw = yaw;
            }
        }
    }
}
#endif
