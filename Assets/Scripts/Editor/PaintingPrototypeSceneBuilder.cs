#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.EditorTools
{
    /// <summary>
    /// Deterministically builds the T-008A hidden solved diorama scene at
    /// Assets/Scenes/PaintingPrototype.unity: the original "Mist Valley
    /// Bridge" landscape as a physical miniature in the warm-ivory exhibition
    /// language. Exactly the eight movable scenery roots (Sun, Far Mountain,
    /// Middle Mountain, Tree Cluster Left, Tree Cluster Right, Pavilion, Arch
    /// Bridge, Foreground Rock) sit under "Solved Scenery" at deliberately
    /// surprising depths; from the tagged "Composition Camera" they read as
    /// one coherent celadon landscape (foreground bridge and rock overlap the
    /// middle ground, and perspective changes apparent size), while the
    /// "Build Camera" three-quarter view exposes the depth layers of the toy
    /// installation. Sky, mist, water and exhibition furniture (floor, plinth,
    /// framed Reference Frame) are static environment. T-010B additionally
    /// tags every piece root as manipulable: a fitted BoxCollider on each
    /// root, all roots on a dedicated PaintingPiece layer, and one
    /// PaintingManipulationController wired to the cameras. T-010C applies
    /// a deterministic unsolved start layout to the eight piece roots only
    /// after every authored transform and the controller wiring are final,
    /// so the saved scene opens visibly misplaced but readable and
    /// selectable from the Build Camera, while each authored transform
    /// stays the solved pose that ResetToAuthored restores.
    /// Callable from the menu or via
    /// -executeMethod PerspectivePuzzle.EditorTools.PaintingPrototypeSceneBuilder.BuildScene.
    /// </summary>
    public static class PaintingPrototypeSceneBuilder
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/PaintingPrototype.unity",
            "Assets/Scenes/PaintingMoonGarden.unity",
            "Assets/Scenes/PaintingRedCliffs.unity",
            "Assets/Scenes/PaintingTwinSeal.unity",
        };
        private static readonly string[] PaintingTitles = { "雾谷石桥", "月下庭园", "赤壁群峰", "双景印章" };
        private static readonly string[] PieceDisplayNames =
        {
            "日轮", "远山", "中山", "左侧树林", "右侧树林", "凉亭", "拱桥", "前景石"
        };
        private static readonly string[] ReferenceStems = { "MistValleyBridge", "MoonGarden", "RedCliffs", "TwinSeal" };
        private static int _activePainting;
        // One art-generation path keeps every gallery in the same collectible
        // celadon family; this is readonly (not const) so legacy fallback
        // branches remain compilable without unreachable-code warnings.
        private static readonly bool UseUnifiedHeroArt = true;
        private static string ScenePath => ScenePaths[_activePainting];
        private static readonly int[][] ActivePieceIndices =
        {
            new[] { 5, 6 },
            // Moon Garden is the depth tutorial: retain the two familiar
            // pieces and introduce only two mountains. Trees and the rock
            // remain solved background layers so the jump from gallery one
            // is two decisions, not four unrelated new problems.
            new[] { 1, 2, 5, 6 },
            new[] { 0, 1, 2, 3, 4, 5, 6, 7 },
            new[] { 0, 1, 2, 3, 4, 5, 6, 7 },
        };
        private static bool IsActivePiece(int index) => Array.IndexOf(ActivePieceIndices[_activePainting], index) >= 0;
        private const string MaterialsFolder = "Assets/Art/Materials";
        private const string MeshesFolder = "Assets/Art/Meshes";
        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private const string PaintingPieceLayerName = "PaintingPiece";
        private const string CompositionGuideLayerName = "CompositionGuide";

        /// <summary>Small local-space padding so the fitted collider fully covers the visible piece.</summary>
        private const float ColliderPadding = 0.02f;

        /// <summary>Controller serialized rotation configuration: Q/E step and symmetric yaw clamp in degrees.</summary>
        private const float RotationStepDegrees = 15f;
        private const float MaxRotationOffsetDegrees = 45f;

        /// <summary>Every root can traverse this shared normalized target-painting canvas.</summary>
        private static readonly Rect CompositionViewportBounds = new Rect(0.05f, 0.05f, 0.9f, 0.9f);

        /// <summary>Shared absolute distance interval in front of the Composition Camera.</summary>
        private static readonly Vector2 CompositionDepthRange = new Vector2(4.5f, 12f);

        /// <summary>
        /// T-010B conservative movement bounds containing the authored
        /// colliders of all eight pieces (Sun top at world y ~3.23, the tree
        /// clusters extend slightly past x=-3.2 after robust AABB fitting,
        /// Sun rearmost at z ~-2.6,
        /// Foreground Rock frontmost at z ~1.98) with room for visible
        /// horizontal/vertical/depth error, without reaching the cameras
        /// (Build Camera at (-8.8, 6.2, 8.4), Composition Camera at (0, 2.15, 7))
        /// or the environment (sky backdrop at z=-3.15, water top at y=0.68,
        /// plinth top at y=0.6).
        /// </summary>
        private static readonly Bounds ManipulationMovementBounds = new Bounds(
            Vector3.zero, Vector3.one * 100f);

        /// <summary>Water top surface; the base plane all scenery roots sit on.</summary>
        private const float WaterTopY = 0.68f;

        /// <summary>
        /// Shared physical placement area across the whole inner tray rather
        /// than only the coloured lake. The inset keeps every complete piece
        /// on the plinth while giving large mountains meaningful travel.
        /// </summary>
        private static readonly Rect PlacementRectangle = new Rect(-4.05f, -2.55f, 8.10f, 5.30f);
        private const float PlacementLiftHeight = 0.62f;
        private const float PlacementFollowSmoothTime = 0.09f;
        private const float PlacementSettleDuration = 0.28f;

        /// <summary>
        /// T-010C deterministic unsolved start layout, applied to the eight
        /// piece roots only after every authored transform and the controller
        /// wiring are final: one deliberate world-space translation per piece
        /// from its authored pose, plus a modest quantized yaw offset
        /// (multiple of 15 degrees, within +/-45) for the pieces where
        /// rotation reads clearly. The Sun gets no yaw: as a thin disc its
        /// yaw would turn it edge-on and it would vanish from both cameras.
        /// Every start pose lies inside the shared Composition Camera canvas
        /// and absolute depth range, stays inside the exhibition,
        /// and never piles pieces: the arrangement reads as clearly
        /// misplaced from the Composition Camera while every piece remains
        /// visible and selectable from the Build Camera.
        /// </summary>
        private static readonly Vector3[] UnsolvedStartOffsets =
        {
            new Vector3(-2.2f, 0f, 0.45f),      // Sun stand stays planted on the shared surface
            new Vector3(0.85f, 0f, 1.05f),      // Far Mountain: forward-right
            new Vector3(1.15f, 0f, 1.35f),      // Middle Mountain: forward-right, off the far ridge
            new Vector3(-0.95f, 0f, 0.9f),      // Tree Cluster Left: forward-left of center
            new Vector3(1.0f, 0f, 0.7f),        // Tree Cluster Right: forward-right of center
            new Vector3(-0.75f, 0f, 1.2f),      // Pavilion: forward-left
            new Vector3(0.9f, 0f, 0.75f),       // Arch Bridge: right, crossing the water diagonally
            new Vector3(0.85f, 0f, -0.95f),     // Foreground Rock: back toward the water center
        };

        /// <summary>Signed yaw offsets from each piece's authored rotation, in RequiredPieces order.</summary>
        private static readonly float[] UnsolvedStartYawOffsets =
        {
            0f,    // Sun
            15f,   // Far Mountain
            -30f,  // Middle Mountain
            30f,   // Tree Cluster Left
            -15f,  // Tree Cluster Right
            30f,   // Pavilion
            -30f,  // Arch Bridge
            30f,   // Foreground Rock
        };

        // Accepted warm-ivory exhibition palette plus the celadon/jade
        // landscape palette of T-008A (glazed ceramic mountains, moss trees,
        // porcelain architecture, muted blue-green water, one warm sun).
        private static readonly Color WarmIvory = new Color(0.75f, 0.55f, 0.34f);
        private static readonly Color SkyIvory = new Color(0.91f, 0.84f, 0.69f);
        private static readonly Color LightStone = new Color(0.86f, 0.77f, 0.59f);
        private static readonly Color FrameBlueGray = new Color(0.22f, 0.105f, 0.045f);
        private static readonly Color FrameSurface = new Color(0.961f, 0.949f, 0.925f);
        private static readonly Color CeladonWater = new Color(0.21f, 0.44f, 0.45f);
        private static readonly Color CeladonPale = new Color(0.46f, 0.65f, 0.55f);
        private static readonly Color CeladonJade = new Color(0.18f, 0.43f, 0.36f);
        private static readonly Color MossGreen = new Color(0.10f, 0.27f, 0.16f);
        private static readonly Color Porcelain = new Color(0.973f, 0.965f, 0.941f);
        private static readonly Color SunHalo = new Color(0.88f, 0.58f, 0.20f);
        private static readonly Color SunCore = new Color(0.76f, 0.24f, 0.12f);
        private static readonly Color CeladonStone = new Color(0.33f, 0.50f, 0.42f);
        private static readonly Color BridgeStone = new Color(0.37f, 0.57f, 0.49f);
        private static readonly Color MistWhite = new Color(0.980f, 0.972f, 0.945f, 0.32f);
        private static readonly Color DarkWalnut = new Color(0.16f, 0.07f, 0.025f);
        private static readonly Color WarmGold = new Color(0.68f, 0.43f, 0.13f);
        private static readonly Color BarkBrown = new Color(0.24f, 0.105f, 0.045f);
        private static readonly Color JadeHighlight = new Color(0.53f, 0.72f, 0.59f);

        // Camera poses: the composition camera is the "painting" viewpoint on
        // the +Z side, close enough that the diorama fills the frame (no
        // plinth, floor, frame or backdrop edge visible); the build camera is
        // a high unobstructed three-quarter view from the front-left showing
        // the whole installation and the depth spread of the pieces.
        private static readonly Vector3 BuildCameraPosition = new Vector3(-8.8f, 6.2f, 8.4f);
        private static readonly Vector3 BuildCameraTarget = new Vector3(0f, 1.0f, -0.1f);
        private static readonly Vector3 CompositionCameraPosition = new Vector3(0f, 2.15f, 7.0f);
        private static readonly Vector3 CompositionCameraTarget = new Vector3(0f, 1.42f, -0.35f);

        /// <summary>Exactly the required movable scenery roots of T-008A.</summary>
        private static readonly string[] RequiredPieces =
        {
            "Sun", "Far Mountain", "Middle Mountain", "Tree Cluster Left",
            "Tree Cluster Right", "Pavilion", "Arch Bridge", "Foreground Rock",
        };

        /// <summary>
        /// Stable packed 24-bit Object-ID colors in RequiredPieces order,
        /// matching the target capture (PaintingPrototypeCapture.ObjectIdColors):
        /// Sun #FF4040, Far Mountain #40FF40, Middle Mountain #4040FF, Tree
        /// Cluster Left #FFFF40, Tree Cluster Right #FF40FF, Pavilion #40FFFF,
        /// Arch Bridge #FF8040, Foreground Rock #8040FF.
        /// </summary>
        private static readonly int[] PieceIds =
        {
            0xFF4040, 0x40FF40, 0x4040FF, 0xFFFF40, 0xFF40FF, 0x40FFFF, 0xFF8040, 0x8040FF,
        };

        /// <summary>T-008B target Object-ID capture the evaluator scores against.</summary>
        private static string TargetObjectIdPath => "Assets/Content/PaintingPrototype/References/" + ReferenceStems[_activePainting] + "_ObjectId.png";
        private static string TargetBeautyPath => "Assets/Content/PaintingPrototype/References/" + ReferenceStems[_activePainting] + "_Beauty.png";

        [MenuItem("Tools/PerspectivePuzzle/Build Painting Prototype Scene")]
        public static void BuildScene()
        {
            BuildSceneAt(0);
        }

        [MenuItem("Tools/PerspectivePuzzle/Build All Painting Galleries")]
        public static void BuildAllScenes()
        {
            EnsureLevelReferencePlaceholders();
            for (int i = 0; i < ScenePaths.Length; i++) BuildSceneAt(i);
            Debug.Log("All four painting galleries built.");
        }

        private static void BuildSceneAt(int paintingIndex)
        {
            _activePainting = paintingIndex;
            EnsureFolder("Assets", "Art");
            EnsureFolder("Assets/Art", "Materials");
            EnsureFolder("Assets/Art", "Meshes");
            EnsureFolder("Assets", "Scenes");

            // Start from a clean empty scene so the build is reproducible.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("Painting Prototype").transform;
            BuildExhibition(root);
            BuildSky(root);
            BuildWater(root);
            BuildSolvedScenery(root);
            ApplyPaintingVariation(root.Find("Solved Scenery"), paintingIndex);
            BuildLights(root);
            BuildCameras(root);
            BuildPostProcessing(root);
            BuildEvaluation(root);
            BuildSecondaryEvaluationAndGoalGate(root);
            BuildManipulation(root);
            BuildGuidance(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureSceneInBuildSettings();

            ValidateScene();
            Debug.Log("Painting Prototype scene built at " + ScenePath);
        }

        private static void EnsureLevelReferencePlaceholders()
        {
            const string sourceBeauty = "Assets/Content/PaintingPrototype/References/MistValleyBridge_Beauty.png";
            const string sourceIds = "Assets/Content/PaintingPrototype/References/MistValleyBridge_ObjectId.png";
            for (int i = 1; i < ReferenceStems.Length; i++)
            {
                string beauty = "Assets/Content/PaintingPrototype/References/" + ReferenceStems[i] + "_Beauty.png";
                string ids = "Assets/Content/PaintingPrototype/References/" + ReferenceStems[i] + "_ObjectId.png";
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(beauty) == null) AssetDatabase.CopyAsset(sourceBeauty, beauty);
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(ids) == null) AssetDatabase.CopyAsset(sourceIds, ids);
            }
            AssetDatabase.Refresh();
            EnsureTwinSealSecondaryPlaceholder();
        }

        private static void EnsureTwinSealSecondaryPlaceholder()
        {
            const string path = "Assets/Content/PaintingPrototype/References/TwinSeal_SecondaryObjectId.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) return;
            var pixels = new uint[256 * 144];
            int[] allowed = { PieceIds[1], PieceIds[2], PieceIds[5], PieceIds[6] };
            // A small valid four-ID seed; the GPU capture pipeline replaces
            // it with the authored right-side target before final scene build.
            for (int i = 0; i < allowed.Length; i++)
            for (int y = 56; y < 88; y++)
            for (int x = 48 + i * 40; x < 68 + i * 40; x++) pixels[y * 256 + x] = (uint)allowed[i];
            var texture = new Texture2D(256, 144, TextureFormat.RGBA32, false, true);
            var colors = new Color32[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) colors[i] = new Color32(
                (byte)(pixels[i] >> 16), (byte)(pixels[i] >> 8), (byte)pixels[i], 255);
            texture.SetPixels32(colors); texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = true; importer.sRGBTexture = false; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void ApplyPaintingVariation(Transform scenery, int index)
        {
            if (scenery == null || index == 0) return;
            // Each chapter is a genuinely different occlusion puzzle while
            // retaining the same tactile collection of eight miniature props.
            Vector3[] offsets = index == 1
                ? new[] { new Vector3(0.75f,-0.35f,0.2f), new Vector3(-0.9f,0f,0.3f), new Vector3(0.55f,0f,-0.2f), new Vector3(0.55f,0f,-0.35f), new Vector3(-0.55f,0f,0.25f), new Vector3(0.55f,0f,-0.6f), new Vector3(-0.1f,0f,0.2f), new Vector3(-0.55f,0f,-0.5f) }
                : index == 2
                    ? new[] { new Vector3(-0.7f,0.2f,0.1f), new Vector3(0.5f,0f,-0.15f), new Vector3(-0.5f,0f,0.35f), new Vector3(0.55f,0f,-0.3f), new Vector3(-0.45f,0f,-0.15f), new Vector3(-0.55f,0f,-0.4f), new Vector3(0f,0f,0.2f), new Vector3(0.55f,0f,-0.25f) }
                    : new[] { new Vector3(0.55f,-0.15f,-0.1f), new Vector3(-0.65f,0f,0.45f), new Vector3(0.45f,0f,-0.45f), new Vector3(-0.35f,0f,0.3f), new Vector3(0.5f,0f,-0.25f), new Vector3(-0.25f,0f,0.35f), new Vector3(0.2f,0f,-0.2f), new Vector3(-0.45f,0f,-0.35f) };
            for (int i = 0; i < RequiredPieces.Length; i++)
            {
                Transform piece = scenery.Find(RequiredPieces[i]);
                piece.position += offsets[i];
            }
        }


        private static void EnsureSceneInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == ScenePath)
                {
                    if (!scenes[i].enabled)
                    {
                        scenes[i] = new EditorBuildSettingsScene(ScenePath, true);
                        EditorBuildSettings.scenes = scenes.ToArray();
                    }
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Static display furniture: warm-ivory floor, light-stone plinth and
        /// the framed Reference Frame standing on the plinth, facing the
        /// composition side. The frame carries a neutral placeholder surface;
        /// its pose is fixed so later work can display the beauty reference
        /// without touching the main composition.
        /// </summary>
        private static void BuildExhibition(Transform root)
        {
            var floorMat = GetOrCreateMaterial("Mat_WarmIvory", WarmIvory, 0.32f, 0f);
            var plinthMat = GetOrCreateMaterial("Mat_LightStone", LightStone, 0.30f, 0f);
            var frameBarMat = GetOrCreateMaterial("Mat_FrameBlueGray", FrameBlueGray, 0.35f, 0f);
            var frameSurfaceMat = GetOrCreateReferencePaintingMaterial();
            var walnutMat = GetOrCreateMaterial("Mat_DarkWalnut", DarkWalnut, 0.42f, 0f);
            var goldMat = GetOrCreateMaterial("Mat_WarmGold", WarmGold, 0.48f, 0.18f);

            var exhibition = new GameObject("Exhibition").transform;
            exhibition.SetParent(root, false);

            var floorMesh = GetOrCreateMesh("RoundedFloorCompact", new Vector3(19f, 0.5f, 14f), 0.12f, 6);
            CreateMeshRenderer(exhibition, "Floor", new Vector3(0f, -0.25f, 0f), floorMesh, floorMat, Quaternion.identity);

            var plinthMesh = GetOrCreateMesh("PaintingPlinth", new Vector3(8.6f, 0.6f, 5.8f), 0.16f, 10);
            CreateMeshRenderer(exhibition, "Plinth", new Vector3(0f, 0.3f, 0.1f), plinthMesh, plinthMat, Quaternion.identity);

            // A dark carved-looking rim turns the flat test slab into a
            // deliberate puzzle table and gives the hand a clear boundary.
            var rimLong = GetOrCreateMesh("PaintingTableRimLong", new Vector3(9.05f, 0.25f, 0.22f), 0.07f, 8);
            var rimShort = GetOrCreateMesh("PaintingTableRimShort", new Vector3(0.22f, 0.25f, 5.8f), 0.07f, 8);
            CreateMeshRenderer(exhibition, "Table Rim Front", new Vector3(0f, 0.72f, 3.0f), rimLong, walnutMat, Quaternion.identity);
            CreateMeshRenderer(exhibition, "Table Rim Back", new Vector3(0f, 0.72f, -2.8f), rimLong, walnutMat, Quaternion.identity);
            CreateMeshRenderer(exhibition, "Table Rim Left", new Vector3(-4.42f, 0.72f, 0.1f), rimShort, walnutMat, Quaternion.identity);
            CreateMeshRenderer(exhibition, "Table Rim Right", new Vector3(4.42f, 0.72f, 0.1f), rimShort, walnutMat, Quaternion.identity);
            var cornerMesh = GetOrCreateMesh("PaintingTableCornerMedallion", new Vector3(0.34f, 0.09f, 0.34f), 0.04f, 8);
            foreach (float x in new[] { -4.25f, 4.25f })
            foreach (float z in new[] { -2.65f, 2.85f })
                CreateMeshRenderer(exhibition, "Gold Corner", new Vector3(x, 0.88f, z), cornerMesh, goldMat, Quaternion.identity);

            // Physical framed placeholder standing on the plinth's dry front
            // strip, well outside the composition frustum (at the camera's
            // right side) so the beauty image never shows it. Its bottom edge
            // sits on the plinth top (y = 0.6) and it faces +Z, visible from
            // the build camera; the pose is fixed so later work can display
            // the beauty reference without touching the main composition.
            var frame = new GameObject("Reference Frame").transform;
            frame.SetParent(exhibition, false);
            frame.position = new Vector3(-7.2f, 1.125f, -2.0f);
            frame.rotation = Quaternion.Euler(6f, -15f, 0f);

            var surfaceMesh = GetOrCreateMesh("ReferenceSurface", new Vector3(1.5f, 1.05f, 0.06f), 0.025f, 8);
            var barHMesh = GetOrCreateMesh("ReferenceFrameBar_Horizontal", new Vector3(1.7f, 0.1f, 0.12f), 0.04f, 6);
            var barVMesh = GetOrCreateMesh("ReferenceFrameBar_Vertical", new Vector3(0.1f, 1.15f, 0.12f), 0.04f, 6);

            CreateMeshRenderer(frame, "Surface", Vector3.zero, surfaceMesh, frameSurfaceMat, Quaternion.identity);
            CreateMeshRenderer(frame, "Frame Top", new Vector3(0f, 0.525f, 0.05f), barHMesh, frameBarMat, Quaternion.identity);
            CreateMeshRenderer(frame, "Frame Bottom", new Vector3(0f, -0.525f, 0.05f), barHMesh, frameBarMat, Quaternion.identity);
            CreateMeshRenderer(frame, "Frame Left", new Vector3(-0.7f, 0f, 0.05f), barVMesh, frameBarMat, Quaternion.identity);
            CreateMeshRenderer(frame, "Frame Right", new Vector3(0.7f, 0f, 0.05f), barVMesh, frameBarMat, Quaternion.identity);
        }

        /// <summary>
        /// Static sky: one warm-ivory diorama backdrop standing on the plinth's
        /// back edge behind the farthest scenery. Its edges fall outside the
        /// composition frustum (or below the water line), so no room/table
        /// horizon is visible in the beauty image; the build view sees it as
        /// the diorama's back wall.
        /// </summary>
        private static void BuildSky(Transform root)
        {
            var skyMat = GetOrCreateMaterial("Mat_SkyIvory", SkyIvory, 0.20f, 0f);
            var frameMat = GetOrCreateMaterial("Mat_BackdropWalnut", DarkWalnut, 0.38f, 0f);
            var goldMat = GetOrCreateMaterial("Mat_BackdropGold", WarmGold, 0.5f, 0.2f);

            var sky = new GameObject("Sky").transform;
            sky.SetParent(root, false);

            var panelMesh = GetOrCreateMesh("SkyPanel_Cycle2Wide", new Vector3(11.5f, 5.0f, 0.12f), 0.05f, 8);
            var backdrop = CreateMeshRenderer(sky, "Backdrop", new Vector3(0f, 3.1f, -3.15f), panelMesh, skyMat, Quaternion.identity);
            var backdropRenderer = backdrop.GetComponent<MeshRenderer>();
            backdropRenderer.receiveShadows = false;
            backdropRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var horizontal = GetOrCreateMesh("BackdropFrameHorizontal", new Vector3(12.0f, 0.22f, 0.22f), 0.06f, 8);
            var vertical = GetOrCreateMesh("BackdropFrameVertical", new Vector3(0.22f, 5.35f, 0.22f), 0.06f, 8);
            CreateMeshRenderer(sky, "Backdrop Frame Top", new Vector3(0f, 5.62f, -3.05f), horizontal, frameMat, Quaternion.identity);
            CreateMeshRenderer(sky, "Backdrop Frame Bottom", new Vector3(0f, 0.58f, -3.05f), horizontal, frameMat, Quaternion.identity);
            CreateMeshRenderer(sky, "Backdrop Frame Left", new Vector3(-5.85f, 3.1f, -3.05f), vertical, frameMat, Quaternion.identity);
            CreateMeshRenderer(sky, "Backdrop Frame Right", new Vector3(5.85f, 3.1f, -3.05f), vertical, frameMat, Quaternion.identity);
            var crest = GetOrCreateMesh("BackdropGoldCrest", new Vector3(1.4f, 0.16f, 0.28f), 0.07f, 8);
            CreateMeshRenderer(sky, "Backdrop Gold Crest", new Vector3(0f, 5.72f, -2.92f), crest, goldMat, Quaternion.identity);

            // Shallow paper-cut distance layers make the composition camera
            // read as an illustrated landscape before any movable foreground
            // piece is aligned. They remain static and never affect scoring.
            var distantMat = GetOrCreateMaterial("Mat_DistantMountain", new Color(0.55f, 0.63f, 0.54f), 0.18f, 0f);
            var distantPaleMat = GetOrCreateMaterial("Mat_DistantMountainPale", new Color(0.70f, 0.72f, 0.62f), 0.16f, 0f);
            var distantMesh = GetOrCreateSilhouetteMesh("BackdropMountainV1", new[]
            {
                new Vector2(-0.75f, 0f), new Vector2(-0.48f, 0.32f), new Vector2(-0.20f, 0.92f),
                new Vector2(0.02f, 1.26f), new Vector2(0.20f, 0.78f), new Vector2(0.42f, 0.38f),
                new Vector2(0.72f, 0f),
            }, 0.035f);
            CreateBackdropDecoration(sky, "Distant Peak Left", new Vector3(-3.0f, 0.66f, -2.94f), distantMesh, distantPaleMat, 0.72f);
            CreateBackdropDecoration(sky, "Distant Peak Mid", new Vector3(2.15f, 0.66f, -2.93f), distantMesh, distantMat, 0.54f);
            CreateBackdropDecoration(sky, "Distant Peak Right", new Vector3(3.15f, 0.66f, -2.92f), distantMesh, distantPaleMat, 0.76f);
        }

        private static void CreateBackdropDecoration(Transform parent, string name, Vector3 position,
            Mesh mesh, Material material, float scale)
        {
            GameObject decoration = CreateMeshRenderer(parent, name, position, mesh, material,
                Quaternion.identity, Vector3.one * scale);
            var renderer = decoration.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Static water: one muted blue-green rounded slab on the plinth.</summary>
        private static void BuildWater(Transform root)
        {
            var waterMat = GetOrCreateMaterial("Mat_CeladonWater", CeladonWater, 0.45f, 0.05f);

            var water = new GameObject("Water").transform;
            water.SetParent(root, false);

            var waterMesh = GetOrCreateMesh("WaterSurface_Cycle2Wide", new Vector3(6.6f, 0.08f, 5.0f), 0.035f, 8);
            // Slab bottom flush with the plinth top; top surface at WaterTopY.
            CreateMeshRenderer(water, "Lake Surface", new Vector3(0f, 0.64f, 0.25f), waterMesh, waterMat, Quaternion.identity);
            // Keep the lake as one calm visual surface. Spatial coordinates
            // appear only after selection through PaintingPlacementLatticeView;
            // a permanent full grid made the diorama read as a level editor.
        }

        /// <summary>
        /// Five columns by three depth bands, visible only to the player's
        /// build camera. The target/composition cameras explicitly cull this
        /// layer, so spatial affordance never contaminates the painting or
        /// machine-readable Object-ID references.
        /// </summary>
        private static void BuildPhysicalCompositionBoard(Transform water)
        {
            int guideLayer = EnsureCompositionGuideLayer();
            var farMat = GetOrCreateMaterial("T026_BoardFar", new Color(0.30f, 0.55f, 0.54f), 0.34f, 0f);
            var middleMat = GetOrCreateMaterial("T026_BoardMiddle", new Color(0.34f, 0.61f, 0.59f), 0.36f, 0f);
            var nearMat = GetOrCreateMaterial("T026_BoardNear", new Color(0.38f, 0.66f, 0.63f), 0.38f, 0f);
            var goldMat = GetOrCreateMaterial("T026_BoardInlay", new Color(0.62f, 0.45f, 0.20f), 0.52f, 0.28f);

            var bandMesh = GetOrCreateMesh("T026_DepthBand", new Vector3(6.28f, 0.018f, 1.54f), 0.007f, 3);
            float[] bandCenters = { -1.39f, 0.25f, 1.89f };
            Material[] bandMaterials = { farMat, middleMat, nearMat };
            for (int i = 0; i < bandCenters.Length; i++)
            {
                GameObject band = CreateMeshRenderer(water, "Depth Band " + (i + 1),
                    new Vector3(0f, 0.689f, bandCenters[i]), bandMesh,
                    bandMaterials[i], Quaternion.identity);
                SetLayerRecursively(band, guideLayer);
            }

            var verticalInlay = GetOrCreateMesh("T026_ColumnInlay", new Vector3(0.025f, 0.026f, 4.78f), 0.01f, 4);
            foreach (float x in new[] { -1.89f, -0.63f, 0.63f, 1.89f })
            {
                GameObject line = CreateMeshRenderer(water, "Column Gold Inlay", new Vector3(x, 0.706f, 0.25f),
                    verticalInlay, goldMat, Quaternion.identity);
                SetLayerRecursively(line, guideLayer);
            }

            var horizontalInlay = GetOrCreateMesh("T026_DepthInlay", new Vector3(6.28f, 0.028f, 0.032f), 0.011f, 4);
            foreach (float z in new[] { -0.82f, 0.82f })
            {
                GameObject line = CreateMeshRenderer(water, "Depth Gold Inlay", new Vector3(0f, 0.707f, z),
                    horizontalInlay, goldMat, Quaternion.identity);
                SetLayerRecursively(line, guideLayer);
            }

            Camera composition = GameObject.Find("Composition Camera")?.GetComponent<Camera>();
            if (composition != null)
                composition.cullingMask &= ~(1 << guideLayer);
            Camera secondary = GameObject.Find("Secondary Composition Camera")?.GetComponent<Camera>();
            if (secondary != null)
                secondary.cullingMask &= ~(1 << guideLayer);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        /// <summary>
        /// Static mist: soft translucent rounded bands wrapping the far shore
        /// (washing out the water/backdrop seam), the mountain bases and the
        /// water in front of the bridge.
        /// </summary>
        private static void BuildMist(Transform root)
        {
            var mistMat = GetOrCreateMistMaterial();

            var mist = new GameObject("Mist").transform;
            mist.SetParent(root, false);

            var shoreBandMesh = GetOrCreateMesh("MistBandShore", new Vector3(7.6f, 0.55f, 0.5f), 0.07f, 8);
            var farBandMesh = GetOrCreateMesh("MistBandFar", new Vector3(6.4f, 0.22f, 0.9f), 0.07f, 8);
            var midBandMesh = GetOrCreateMesh("MistBandMid", new Vector3(3.4f, 0.16f, 0.6f), 0.06f, 8);
            var nearBandMesh = GetOrCreateMesh("MistBandNear", new Vector3(3.0f, 0.14f, 0.6f), 0.06f, 8);

            CreateMistBand(mist, "Band Shore", new Vector3(0f, 0.78f, -2.45f), shoreBandMesh, mistMat);
            CreateMistBand(mist, "Band Far", new Vector3(0.3f, 1.05f, -1.65f), farBandMesh, mistMat);
            CreateMistBand(mist, "Band Mid", new Vector3(-0.6f, 1.12f, -1.1f), midBandMesh, mistMat);
            CreateMistBand(mist, "Band Near", new Vector3(0.6f, 0.92f, 0.5f), nearBandMesh, mistMat);
        }

        private static void CreateMistBand(Transform parent, string name, Vector3 position, Mesh mesh, Material material)
        {
            var band = CreateMeshRenderer(parent, name, position, mesh, material, Quaternion.identity);
            var renderer = band.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// The hidden solved arrangement. Every piece root is a direct child of
        /// "Solved Scenery"; from the composition camera the pieces layer into
        /// a landscape (sun above the far ridge, mountains and pavilion in the
        /// middle ground, bridge and rock overlapping them in the foreground),
        /// while the side build view shows the surprising depth spread.
        /// </summary>
        private static void BuildSolvedScenery(Transform root)
        {
            var scenery = new GameObject("Solved Scenery").transform;
            scenery.SetParent(root, false);

            BuildSun(scenery);
            BuildFarMountain(scenery);
            BuildMiddleMountain(scenery);
            BuildTreeClusterLeft(scenery);
            BuildTreeClusterRight(scenery);
            BuildPavilion(scenery);
            BuildArchBridge(scenery);
            BuildForegroundRock(scenery);

            // T-009B2: tag every direct piece root with its packed Object-ID
            // so the composition evaluator can draw and score it.
            WirePieceIds(scenery);
        }

        /// <summary>
        /// Attaches exactly one <see cref="PaintingPieceId"/> to every direct
        /// piece root of "Solved Scenery" in RequiredPieces order and
        /// configures its packed Object-ID, so the machine-readable
        /// composition pass can draw the piece. Environment and renderer
        /// children are never tagged. Throws when a root is missing, already
        /// tagged, or has no renderer below it.
        /// </summary>
        private static void WirePieceIds(Transform scenery)
        {
            for (int i = 0; i < RequiredPieces.Length; i++)
            {
                var pieceRoot = scenery.Find(RequiredPieces[i]);
                if (pieceRoot == null)
                    throw new InvalidOperationException(
                        "PaintingPrototype wiring failed: Solved Scenery/" + RequiredPieces[i] + " is missing.");
                if (pieceRoot.GetComponent<PaintingPieceId>() != null)
                    throw new InvalidOperationException(
                        "PaintingPrototype wiring failed: Solved Scenery/" + RequiredPieces[i] + " already carries a PaintingPieceId.");
                var pieceId = pieceRoot.gameObject.AddComponent<PaintingPieceId>();
                // Configure caches the renderers below the root and throws
                // when none exist, so a miswired piece fails the build here.
                pieceId.Configure(PieceIds[i]);
            }
        }

        /// <summary>
        /// Adds the single "Composition Evaluator" object under "Painting
        /// Prototype" and serializes the evaluator's references to the
        /// Composition Camera, the readable target Object-ID texture, and the
        /// eight ordered piece IDs at 256x144 and 6 Hz. The policy weights and
        /// thresholds keep the T-009A defaults (0.40/0.45/0.15/0.93/0.80)
        /// already serialized on the component. Configure is intentionally not
        /// called here: the saved scene deserializes these references and the
        /// evaluator configures itself in PlayMode, where its full validation
        /// (target readability, ID coverage, shader) runs against the imported
        /// asset.
        /// </summary>
        private static void BuildEvaluation(Transform root)
        {
            var evaluation = new GameObject("Composition Evaluator").transform;
            evaluation.SetParent(root, false);
            var evaluator = evaluation.gameObject.AddComponent<PaintingCompositionEvaluator>();

            var camera = GameObject.Find("Composition Camera").GetComponent<Camera>();
            if (camera == null)
                throw new InvalidOperationException("PaintingPrototype wiring failed: Composition Camera missing.");

            var target = AssetDatabase.LoadAssetAtPath<Texture2D>(TargetObjectIdPath);
            if (target == null)
                throw new InvalidOperationException(
                    "PaintingPrototype wiring failed: target Object-ID texture not found at " + TargetObjectIdPath + ".");
            if (!target.isReadable)
                throw new InvalidOperationException(
                    "PaintingPrototype wiring failed: target Object-ID texture must be readable (Read/Write enabled); recapture the references with PaintingPrototypeCapture.");

            var scenery = GameObject.Find("Solved Scenery").transform;
            var pieces = new PaintingPieceId[RequiredPieces.Length];
            for (int i = 0; i < RequiredPieces.Length; i++)
            {
                pieces[i] = scenery.Find(RequiredPieces[i]).GetComponent<PaintingPieceId>();
                if (pieces[i] == null)
                    throw new InvalidOperationException(
                        "PaintingPrototype wiring failed: Solved Scenery/" + RequiredPieces[i] + " has no PaintingPieceId.");
            }

            var serialized = new SerializedObject(evaluator);
            serialized.FindProperty("_compositionCamera").objectReferenceValue = camera;
            serialized.FindProperty("_targetTexture").objectReferenceValue = target;
            Shader idShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/PaintingObjectId.shader");
            if (idShader == null)
                throw new InvalidOperationException("PaintingObjectId shader asset is missing.");
            serialized.FindProperty("_idShader").objectReferenceValue = idShader;
            var piecesProperty = serialized.FindProperty("_pieces");
            piecesProperty.arraySize = pieces.Length;
            for (int i = 0; i < pieces.Length; i++)
                piecesProperty.GetArrayElementAtIndex(i).objectReferenceValue = pieces[i];
            serialized.FindProperty("_width").intValue = 256;
            serialized.FindProperty("_height").intValue = 144;
            serialized.FindProperty("_frequencyHz").floatValue = 6f;
            // Moon Garden teaches depth bands, not pixel-perfect masking. Its
            // overall score may pass once the painting reads correctly, while
            // a low per-piece floor still prevents leaving an object entirely
            // outside its intended region. This avoids the near-full progress
            // bar deadlock seen when a mountain is plausibly occluded.
            serialized.FindProperty("_passThreshold").floatValue = _activePainting == 0 ? 0.84f : _activePainting == 1 ? 0.82f : 0.91f;
            serialized.FindProperty("_minimumCoverageThreshold").floatValue = _activePainting == 0 ? 0.62f : _activePainting == 1 ? 0.40f : 0.76f;
            serialized.ApplyModifiedProperties();
        }

        private static void BuildSecondaryEvaluationAndGoalGate(Transform root)
        {
            var primary = GameObject.Find("Composition Evaluator").GetComponent<PaintingCompositionEvaluator>();
            PaintingCompositionEvaluator secondary = null;
            if (_activePainting == 3)
            {
                var go = new GameObject("Secondary Composition Evaluator");
                go.transform.SetParent(root, false);
                secondary = go.AddComponent<PaintingCompositionEvaluator>();
                var scenery = GameObject.Find("Solved Scenery").transform;
                int[] indices = { 1, 2, 5, 6 };
                var ids = new PaintingPieceId[indices.Length];
                for (int i = 0; i < indices.Length; i++)
                    ids[i] = scenery.Find(RequiredPieces[indices[i]]).GetComponent<PaintingPieceId>();
                var serialized = new SerializedObject(secondary);
                serialized.FindProperty("_compositionCamera").objectReferenceValue = GameObject.Find("Secondary Composition Camera").GetComponent<Camera>();
                serialized.FindProperty("_targetTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Content/PaintingPrototype/References/TwinSeal_SecondaryObjectId.png");
                serialized.FindProperty("_idShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/PaintingObjectId.shader");
                var pieces = serialized.FindProperty("_pieces");
                pieces.arraySize = ids.Length;
                for (int i = 0; i < ids.Length; i++) pieces.GetArrayElementAtIndex(i).objectReferenceValue = ids[i];
                serialized.FindProperty("_width").intValue = 256;
                serialized.FindProperty("_height").intValue = 144;
                serialized.FindProperty("_frequencyHz").floatValue = 6f;
                serialized.FindProperty("_passThreshold").floatValue = 0.82f;
                serialized.FindProperty("_minimumCoverageThreshold").floatValue = 0.60f;
                serialized.ApplyModifiedProperties();
            }

            var gate = new GameObject("Painting Goal Gate");
            gate.transform.SetParent(root, false);
            var component = gate.AddComponent<PaintingGoalGate>();
            var gateSerialized = new SerializedObject(component);
            gateSerialized.FindProperty("_primary").objectReferenceValue = primary;
            var secondaries = gateSerialized.FindProperty("_secondary");
            secondaries.arraySize = secondary != null ? 1 : 0;
            if (secondary != null) secondaries.GetArrayElementAtIndex(0).objectReferenceValue = secondary;
            gateSerialized.FindProperty("_secondarySilhouetteThreshold").floatValue = 0.82f;
            gateSerialized.ApplyModifiedProperties();
        }

        /// <summary>
        /// T-010B: tags every piece root as manipulable. Each of the eight
        /// roots receives exactly one selection collider fitted from all
        /// child renderer world bounds converted into root-local space (no
        /// Rigidbody), a <see cref="PaintingManipulablePiece"/> captures the
        /// authored transform and validates the collider, and each root moves
        /// to the dedicated PaintingPiece layer (created deterministically on
        /// the first free user layer, only if missing). Only after every
        /// handle is created and configured, exactly one
        /// <see cref="PaintingManipulationController"/> on a clearly named
        /// child of "Painting Prototype" is wired to the Build Camera, the
        /// Composition Camera, the eight ordered pieces plus an explicit Arch
        /// Bridge compatibility reference, the layer mask, the conservative
        /// movement bounds and the rotation configuration. Child renderers
        /// stay on Default because raycasts hit the root collider. Rebuilding
        /// from a clean scene is deterministic.
        /// </summary>
        private static void BuildManipulation(Transform root)
        {
            int layer = EnsurePaintingPieceLayer();

            var scenery = GameObject.Find("Solved Scenery");
            if (scenery == null)
                throw new InvalidOperationException(
                    "PaintingPrototype wiring failed: Solved Scenery is missing.");

            var allPieces = new PaintingManipulablePiece[RequiredPieces.Length];
            for (int i = 0; i < RequiredPieces.Length; i++)
            {
                var pieceRoot = scenery.transform.Find(RequiredPieces[i]);
                if (pieceRoot == null)
                    throw new InvalidOperationException(
                        "PaintingPrototype wiring failed: Solved Scenery/" + RequiredPieces[i] + " is missing.");
                if (pieceRoot.GetComponent<PaintingManipulablePiece>() != null)
                    throw new InvalidOperationException(
                        "PaintingPrototype wiring failed: Solved Scenery/" + RequiredPieces[i]
                        + " already carries a PaintingManipulablePiece.");
                if (pieceRoot.GetComponent<Collider>() != null)
                    throw new InvalidOperationException(
                        "PaintingPrototype wiring failed: Solved Scenery/" + RequiredPieces[i]
                        + " already carries a collider.");

                var piece = pieceRoot.gameObject.AddComponent<PaintingManipulablePiece>();
                FitPieceCollider(pieceRoot);
                // Captures the authored transform exactly once, after the fitted
                // collider and all child renderers are final; throws on missing
                // renderers or a collider count other than one.
                piece.Configure();
                allPieces[i] = piece;
                bool active = IsActivePiece(i);
                piece.SelectionCollider.enabled = active;
                pieceRoot.gameObject.layer = active ? layer : 0;
            }

            var pieces = new PaintingManipulablePiece[ActivePieceIndices[_activePainting].Length];
            for (int i = 0; i < pieces.Length; i++) pieces[i] = allPieces[ActivePieceIndices[_activePainting][i]];

            // Explicit compatibility reference: the Arch Bridge is resolved
            // by name rather than by array index, keeping the Bridge helper
            // and the parameterless SelectPiece for the bridge-focused tests.
            var bridge = scenery.transform.Find("Arch Bridge").GetComponent<PaintingManipulablePiece>();

            var controllerGo = new GameObject("Manipulation Controller");
            controllerGo.transform.SetParent(root, false);
            var controller = controllerGo.AddComponent<PaintingManipulationController>();

            var buildCamera = GameObject.Find("Build Camera").GetComponent<Camera>();
            var compositionCamera = GameObject.Find("Composition Camera").GetComponent<Camera>();
            if (buildCamera == null || compositionCamera == null)
                throw new InvalidOperationException(
                    "PaintingPrototype wiring failed: Build Camera or Composition Camera missing for the Manipulation Controller.");

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("_buildCamera").objectReferenceValue = buildCamera;
            serialized.FindProperty("_compositionCamera").objectReferenceValue = compositionCamera;
            var piecesProperty = serialized.FindProperty("_pieces");
            piecesProperty.arraySize = pieces.Length;
            for (int i = 0; i < pieces.Length; i++)
                piecesProperty.GetArrayElementAtIndex(i).objectReferenceValue = pieces[i];
            serialized.FindProperty("_bridge").objectReferenceValue = bridge;
            serialized.FindProperty("_selectionMask").intValue = 1 << layer;
            serialized.FindProperty("_movementBounds").boundsValue = ManipulationMovementBounds;
            serialized.FindProperty("_compositionViewportBounds").rectValue = CompositionViewportBounds;
            serialized.FindProperty("_compositionDepthRange").vector2Value = CompositionDepthRange;
            serialized.FindProperty("_wheelSensitivity").floatValue = 0.25f;
            serialized.FindProperty("_wheelBurstWindowSeconds").floatValue = 0.45f;
            serialized.FindProperty("_rotationStepDegrees").floatValue = RotationStepDegrees;
            serialized.FindProperty("_maxRotationOffsetDegrees").floatValue = MaxRotationOffsetDegrees;
            serialized.FindProperty("_allowDepthAdjustment").boolValue = _activePainting >= 1;
            // Moon Garden's authored unsolved layout includes quantized yaw
            // offsets for all four curriculum pieces. Locking rotation there
            // made the visible answer unreachable without AssistPlace.
            // Every shipped starting layout contains deliberate yaw error, including
            // Mist Valley's bridge and pavilion. Rotation therefore has to be part
            // of the player's toolset from the first gallery onward.
            serialized.FindProperty("_allowRotation").boolValue = true;
            serialized.FindProperty("_placementRectangle").rectValue = PlacementRectangle;
            serialized.FindProperty("_surfaceY").floatValue = WaterTopY;
            serialized.FindProperty("_liftHeight").floatValue = PlacementLiftHeight;
            serialized.FindProperty("_followSmoothTime").floatValue = PlacementFollowSmoothTime;
            serialized.FindProperty("_settleDuration").floatValue = PlacementSettleDuration;
            serialized.FindProperty("_validPreviewMaterial").objectReferenceValue = GetOrCreatePreviewMaterial(
                "Mat_PlacementValid", new Color(0.30f, 0.92f, 0.76f, 0.42f));
            serialized.FindProperty("_invalidPreviewMaterial").objectReferenceValue = GetOrCreatePreviewMaterial(
                "Mat_PlacementInvalid", new Color(1.00f, 0.33f, 0.27f, 0.48f));
            serialized.FindProperty("_latticeColumns").intValue = _activePainting <= 1 ? 5 : 7;
            serialized.FindProperty("_latticeDepthRows").intValue = _activePainting <= 1 ? 3 : 4;
            serialized.FindProperty("_latticeColumnSpacing").floatValue = _activePainting == 0 ? 0.90f : _activePainting == 1 ? 0.75f : 0.60f;
            serialized.FindProperty("_latticeDepthSpacing").floatValue = _activePainting == 0 ? 0.85f : _activePainting == 1 ? 0.70f : 0.55f;
            serialized.ApplyModifiedProperties();

            if (_activePainting == 0)
            {
                var evaluator = GameObject.Find("Composition Evaluator")?.GetComponent<PaintingCompositionEvaluator>();
                if (evaluator == null)
                    throw new InvalidOperationException("Mist Valley tutorial requires Composition Evaluator.");
                var sequence = controllerGo.AddComponent<PaintingTutorialSequence>();
                var sequenceSerialized = new SerializedObject(sequence);
                sequenceSerialized.FindProperty("_evaluator").objectReferenceValue = evaluator;
                sequenceSerialized.FindProperty("_manipulation").objectReferenceValue = controller;
                sequenceSerialized.FindProperty("_bridge").objectReferenceValue = allPieces[6];
                sequenceSerialized.FindProperty("_pavilion").objectReferenceValue = allPieces[5];
                sequenceSerialized.FindProperty("_bridgeEvaluatorIndex").intValue = 6;
                sequenceSerialized.ApplyModifiedProperties();
            }
            else if (_activePainting == 1)
            {
                var evaluator = GameObject.Find("Composition Evaluator")?.GetComponent<PaintingCompositionEvaluator>();
                if (evaluator == null)
                    throw new InvalidOperationException("Moon Garden depth tutorial requires Composition Evaluator.");
                var sequence = controllerGo.AddComponent<PaintingDepthTutorialSequence>();
                var sequenceSerialized = new SerializedObject(sequence);
                sequenceSerialized.FindProperty("_evaluator").objectReferenceValue = evaluator;
                sequenceSerialized.FindProperty("_manipulation").objectReferenceValue = controller;
                sequenceSerialized.FindProperty("_bridge").objectReferenceValue = allPieces[6];
                sequenceSerialized.FindProperty("_farMountain").objectReferenceValue = allPieces[1];
                sequenceSerialized.FindProperty("_middleMountain").objectReferenceValue = allPieces[2];
                sequenceSerialized.FindProperty("_pavilion").objectReferenceValue = allPieces[5];
                sequenceSerialized.ApplyModifiedProperties();
            }

            if (_activePainting == 0)
            {
                var inspection = controllerGo.AddComponent<PaintingInspectionCamera>();
                inspection.Configure(buildCamera, controller, new Vector3(-1.2f, 8.4f, 5.4f),
                    new Vector3(0f, WaterTopY, 0.25f), 0.24f);
            }

            var latticeView = controllerGo.AddComponent<PaintingPlacementLatticeView>();
            var latticeSerialized = new SerializedObject(latticeView);
            latticeSerialized.FindProperty("_controller").objectReferenceValue = controller;
            latticeSerialized.FindProperty("_lineMaterial").objectReferenceValue = GetOrCreatePreviewMaterial(
                "Mat_LatticeGuide", new Color(0.78f, 0.88f, 0.82f, 0.20f));
            latticeSerialized.FindProperty("_lineColor").colorValue =
                new Color(0.78f, 0.88f, 0.82f, 0.20f);
            latticeSerialized.FindProperty("_lineWidth").floatValue = 0.010f;
            latticeSerialized.ApplyModifiedProperties();

            // T-010C: only after every authored transform is captured and the
            // controller is fully wired, apply the deterministic unsolved start
            // layout to the eight piece roots. ValidateScene then verifies the
            // start poses stay inside the shared composition canvas and depth range.
            ApplyUnsolvedStartLayout(scenery.transform);
        }

        /// <summary>
        /// T-011 product guidance rail: target beauty, live Composition Camera
        /// beauty, calm score line and one worst-piece hint. It deliberately
        /// exposes neither the evaluator's Object-ID colours nor a numeric
        /// percentage/grid.
        /// </summary>
        private static void BuildGuidance(Transform root)
        {
            Texture2D targetBeauty = AssetDatabase.LoadAssetAtPath<Texture2D>(TargetBeautyPath);
            if (targetBeauty == null)
                throw new InvalidOperationException("PaintingPrototype guidance target is missing at " + TargetBeautyPath + ".");
            var evaluator = GameObject.Find("Composition Evaluator")?.GetComponent<PaintingCompositionEvaluator>();
            var compositionCamera = GameObject.Find("Composition Camera")?.GetComponent<Camera>();
            if (evaluator == null || compositionCamera == null)
                throw new InvalidOperationException("PaintingPrototype guidance requires the evaluator and Composition Camera.");

            var canvasGo = new GameObject("Guidance Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(root, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            // Geometric compromise: the rail remains close to 29% on 16:9,
            // but does not balloon on 4:3 or become vertically oversized on
            // ultrawide displays.
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<PaintingChineseFontInstaller>();

            Image panel = CreateUiImage(canvasGo.transform, "Curator Rail",
                new Color(0.965f, 0.948f, 0.912f, 0.94f));
            CanvasGroup guidanceGroup = panel.gameObject.AddComponent<CanvasGroup>();
            SetRect(panel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-370f, 0f), Vector2.zero);

            Text title = CreateUiText(panel.transform, "Title", "重构这幅画", 18,
                FontStyle.Bold, new Color(0.19f, 0.23f, 0.24f), TextAnchor.MiddleLeft);
            SetTopRect(title.rectTransform, 24f, 322f, 30f);

            Text targetLabel = CreateUiText(panel.transform, "Target Label", "目标画面", 12,
                FontStyle.Bold, new Color(0.35f, 0.42f, 0.41f), TextAnchor.MiddleLeft);
            SetTopRect(targetLabel.rectTransform, 66f, 322f, 22f);
            RawImage targetImage = CreateUiRawImage(panel.transform, "Target Painting", targetBeauty);
            SetTopRect(targetImage.rectTransform, 91f, 322f, 181f);
            RawImage targetOutline = CreateUiRawImage(panel.transform, "Selected Target Outline", null);
            SetTopRect(targetOutline.rectTransform, 91f, 322f, 181f);
            targetOutline.enabled = false;

            Text liveLabel = CreateUiText(panel.transform, "Live Label", "当前画面", 12,
                FontStyle.Bold, new Color(0.35f, 0.42f, 0.41f), TextAnchor.MiddleLeft);
            SetTopRect(liveLabel.rectTransform, 290f, 322f, 22f);
            RawImage liveImage = CreateUiRawImage(panel.transform, "Live Composition", null);
            SetTopRect(liveImage.rectTransform, 315f, 322f, 181f);

            Text status = CreateUiText(panel.transform, "Status", "调整景物", 20,
                FontStyle.Normal, new Color(0.19f, 0.23f, 0.24f), TextAnchor.MiddleLeft);
            SetTopRect(status.rectTransform, 520f, 322f, 30f);

            Image track = CreateUiImage(panel.transform, "Progress Track", new Color(0.76f, 0.78f, 0.73f, 0.65f));
            SetTopRect(track.rectTransform, 562f, 322f, 7f);
            Image fill = CreateUiImage(track.transform, "Progress Fill", new Color(0.36f, 0.66f, 0.57f, 1f));
            SetRect(fill.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);

            Text focus = CreateUiText(panel.transform, "Focus", "让当前画面接近目标", 15,
                FontStyle.Italic, new Color(0.42f, 0.46f, 0.43f), TextAnchor.MiddleLeft);
            SetTopRect(focus.rectTransform, 582f, 322f, 32f);

            Text secondaryStatus = null;
            if (_activePainting == 3)
            {
                secondaryStatus = CreateUiText(panel.transform, "Secondary Status", "侧面印章  •  调整中", 12,
                    FontStyle.Bold, new Color(0.52f, 0.34f, 0.15f), TextAnchor.MiddleLeft);
                SetTopRect(secondaryStatus.rectTransform, 626f, 322f, 22f);
                Texture2D seal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Content/PaintingPrototype/References/TwinSeal_SecondarySilhouette.png");
                if (seal == null) seal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Content/PaintingPrototype/References/TwinSeal_SecondaryObjectId.png");
                RawImage sealImage = CreateUiRawImage(panel.transform, "Secondary Seal Target", seal);
                SetTopRect(sealImage.rectTransform, 650f, 120f, 60f);
            }

            Image comparisonOverlay = CreateUiImage(canvasGo.transform, "构图对照放大",
                new Color(0.075f, 0.055f, 0.035f, 0.94f));
            SetRect(comparisonOverlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CanvasGroup comparisonGroup = comparisonOverlay.gameObject.AddComponent<CanvasGroup>();
            comparisonGroup.alpha = 0f;
            Text comparisonTitle = CreateUiText(comparisonOverlay.transform, "对照标题",
                "构图对照  •  松开 Tab 返回摆放", 20, FontStyle.Bold,
                new Color(0.95f, 0.84f, 0.61f), TextAnchor.MiddleCenter);
            SetRect(comparisonTitle.rectTransform, new Vector2(0.15f, 0.82f), new Vector2(0.85f, 0.91f), Vector2.zero, Vector2.zero);
            Text largeTargetLabel = CreateUiText(comparisonOverlay.transform, "放大目标标签", "目标画面", 16,
                FontStyle.Bold, new Color(0.94f, 0.91f, 0.83f), TextAnchor.MiddleCenter);
            SetRect(largeTargetLabel.rectTransform, new Vector2(0.05f, 0.72f), new Vector2(0.47f, 0.80f), Vector2.zero, Vector2.zero);
            RawImage largeTarget = CreateUiRawImage(comparisonOverlay.transform, "放大目标画面", targetBeauty);
            SetRect(largeTarget.rectTransform, new Vector2(0.05f, 0.22f), new Vector2(0.47f, 0.72f), Vector2.zero, Vector2.zero);
            Text largeLiveLabel = CreateUiText(comparisonOverlay.transform, "放大当前标签", "当前画面", 16,
                FontStyle.Bold, new Color(0.94f, 0.91f, 0.83f), TextAnchor.MiddleCenter);
            SetRect(largeLiveLabel.rectTransform, new Vector2(0.53f, 0.72f), new Vector2(0.95f, 0.80f), Vector2.zero, Vector2.zero);
            RawImage largeLive = CreateUiRawImage(comparisonOverlay.transform, "放大当前画面", null);
            SetRect(largeLive.rectTransform, new Vector2(0.53f, 0.22f), new Vector2(0.95f, 0.72f), Vector2.zero, Vector2.zero);

            var presenter = canvasGo.AddComponent<PaintingGuidancePresenter>();
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("_evaluator").objectReferenceValue = evaluator;
            serialized.FindProperty("_manipulation").objectReferenceValue = GameObject.Find("Manipulation Controller").GetComponent<PaintingManipulationController>();
            serialized.FindProperty("_goalGate").objectReferenceValue = GameObject.Find("Painting Goal Gate").GetComponent<PaintingGoalGate>();
            serialized.FindProperty("_compositionCamera").objectReferenceValue = compositionCamera;
            serialized.FindProperty("_targetImage").objectReferenceValue = targetImage;
            serialized.FindProperty("_targetPieceOutline").objectReferenceValue = targetOutline;
            serialized.FindProperty("_liveImage").objectReferenceValue = liveImage;
            serialized.FindProperty("_progressFill").objectReferenceValue = fill.rectTransform;
            serialized.FindProperty("_statusText").objectReferenceValue = status;
            serialized.FindProperty("_focusText").objectReferenceValue = focus;
            serialized.FindProperty("_secondaryStatusText").objectReferenceValue = secondaryStatus;
            serialized.FindProperty("_comparisonGroup").objectReferenceValue = comparisonGroup;
            serialized.FindProperty("_comparisonTarget").objectReferenceValue = largeTarget;
            serialized.FindProperty("_comparisonLive").objectReferenceValue = largeLive;
            serialized.FindProperty("_tutorialMode").boolValue = _activePainting == 0;
            if (_activePainting == 0)
                serialized.FindProperty("_tutorialSequence").objectReferenceValue =
                    GameObject.Find("Manipulation Controller").GetComponent<PaintingTutorialSequence>();
            else if (_activePainting == 1)
                serialized.FindProperty("_depthTutorialSequence").objectReferenceValue =
                    GameObject.Find("Manipulation Controller").GetComponent<PaintingDepthTutorialSequence>();
            var names = serialized.FindProperty("_pieceNames");
            names.arraySize = RequiredPieces.Length;
            for (int i = 0; i < RequiredPieces.Length; i++)
                names.GetArrayElementAtIndex(i).stringValue = PieceDisplayNames[i];
            var eligible = serialized.FindProperty("_hintEligible");
            eligible.arraySize = RequiredPieces.Length;
            for (int i = 0; i < RequiredPieces.Length; i++)
                eligible.GetArrayElementAtIndex(i).boolValue = IsActivePiece(i);
            serialized.ApplyModifiedProperties();

            Image revealOverlay = CreateUiImage(canvasGo.transform, "Reveal Overlay",
                new Color(0.965f, 0.948f, 0.912f, 0.40f));
            SetRect(revealOverlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CanvasGroup revealGroup = revealOverlay.gameObject.AddComponent<CanvasGroup>();
            revealGroup.alpha = 0f;
            revealGroup.blocksRaycasts = false;
            Text revealText = CreateUiText(revealOverlay.transform, "Reveal Message", "画面重合", 32,
                FontStyle.Bold, new Color(0.16f, 0.23f, 0.22f), TextAnchor.MiddleCenter);
            SetRect(revealText.rectTransform, new Vector2(0.18f, 0.42f), new Vector2(0.82f, 0.58f),
                Vector2.zero, Vector2.zero);

            var manipulation = GameObject.Find("Manipulation Controller")?.GetComponent<PaintingManipulationController>();
            var buildCamera = GameObject.Find("Build Camera")?.GetComponent<Camera>();
            if (manipulation == null || buildCamera == null)
                throw new InvalidOperationException("Painting completion reveal requires manipulation and Build Camera.");
            var reveal = canvasGo.AddComponent<PaintingCompletionReveal>();
            var revealSerialized = new SerializedObject(reveal);
            revealSerialized.FindProperty("_evaluator").objectReferenceValue = evaluator;
            revealSerialized.FindProperty("_goalGate").objectReferenceValue = GameObject.Find("Painting Goal Gate").GetComponent<PaintingGoalGate>();
            if (_activePainting == 0)
                revealSerialized.FindProperty("_tutorialSequence").objectReferenceValue =
                    GameObject.Find("Manipulation Controller").GetComponent<PaintingTutorialSequence>();
            else if (_activePainting == 1)
                revealSerialized.FindProperty("_depthTutorialSequence").objectReferenceValue =
                    GameObject.Find("Manipulation Controller").GetComponent<PaintingDepthTutorialSequence>();
            revealSerialized.FindProperty("_manipulation").objectReferenceValue = manipulation;
            revealSerialized.FindProperty("_buildCamera").objectReferenceValue = buildCamera;
            revealSerialized.FindProperty("_compositionCamera").objectReferenceValue = compositionCamera;
            if (_activePainting == 3)
                revealSerialized.FindProperty("_secondaryRevealCamera").objectReferenceValue = GameObject.Find("Secondary Composition Camera").GetComponent<Camera>();
            revealSerialized.FindProperty("_guidanceGroup").objectReferenceValue = guidanceGroup;
            revealSerialized.FindProperty("_revealGroup").objectReferenceValue = revealGroup;
            revealSerialized.FindProperty("_revealText").objectReferenceValue = revealText;
            revealSerialized.ApplyModifiedProperties();

            if (_activePainting == 0)
            {
                var metrics = canvasGo.AddComponent<PaintingSessionMetrics>();
                var metricsSerialized = new SerializedObject(metrics);
                metricsSerialized.FindProperty("_manipulation").objectReferenceValue = manipulation;
                metricsSerialized.FindProperty("_reveal").objectReferenceValue = reveal;
                metricsSerialized.FindProperty("_gallery").stringValue = "Mist Valley";
                metricsSerialized.ApplyModifiedProperties();
            }

            // A quiet museum-style opening replaces an abrupt drop into an
            // unexplained editor-looking scene. It also owns the universal
            // reset/undo shortcuts and the post-reveal gallery hand-off.
            Image intro = CreateUiImage(canvasGo.transform, "Exhibition Introduction",
                new Color(0.075f, 0.045f, 0.025f, 0.88f));
            SetRect(intro.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CanvasGroup introGroup = intro.gameObject.AddComponent<CanvasGroup>();
            Text chapter = CreateUiText(intro.transform, "Chapter", "", 30, FontStyle.Bold,
                new Color(0.95f, 0.84f, 0.61f), TextAnchor.MiddleCenter);
            SetRect(chapter.rectTransform, new Vector2(0.18f, 0.49f), new Vector2(0.82f, 0.66f),
                Vector2.zero, Vector2.zero);
            Text premise = CreateUiText(intro.transform, "Premise",
                IntroInstructions(),
                18, FontStyle.Normal, new Color(0.94f, 0.91f, 0.83f), TextAnchor.UpperCenter);
            SetRect(premise.rectTransform, new Vector2(0.18f, 0.26f), new Vector2(0.82f, 0.49f),
                Vector2.zero, Vector2.zero);

            Image continuePanel = CreateUiImage(canvasGo.transform, "Gallery Continue",
                new Color(0.075f, 0.045f, 0.025f, 0.86f));
            SetRect(continuePanel.rectTransform, new Vector2(0.20f, 0.38f), new Vector2(0.80f, 0.62f),
                Vector2.zero, Vector2.zero);
            CanvasGroup continueGroup = continuePanel.gameObject.AddComponent<CanvasGroup>();
            Text continueText = CreateUiText(continuePanel.transform, "Continue Message", "", 23,
                FontStyle.Bold, new Color(0.95f, 0.84f, 0.61f), TextAnchor.MiddleCenter);
            SetRect(continueText.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 18f),
                new Vector2(-24f, -18f));

            var flow = canvasGo.AddComponent<PaintingLevelFlow>();
            var flowSerialized = new SerializedObject(flow);
            flowSerialized.FindProperty("_reveal").objectReferenceValue = reveal;
            flowSerialized.FindProperty("_manipulation").objectReferenceValue = manipulation;
            flowSerialized.FindProperty("_introGroup").objectReferenceValue = introGroup;
            flowSerialized.FindProperty("_continueGroup").objectReferenceValue = continueGroup;
            flowSerialized.FindProperty("_chapterText").objectReferenceValue = chapter;
            flowSerialized.FindProperty("_continueText").objectReferenceValue = continueText;
            flowSerialized.FindProperty("_paintingTitle").stringValue = PaintingTitles[_activePainting];
            flowSerialized.FindProperty("_paintingNumber").intValue = _activePainting + 1;
            flowSerialized.FindProperty("_paintingCount").intValue = ScenePaths.Length;
            flowSerialized.FindProperty("_nextScene").stringValue = _activePainting + 1 < ScenePaths.Length
                ? System.IO.Path.GetFileNameWithoutExtension(ScenePaths[_activePainting + 1])
                : string.Empty;
            flowSerialized.ApplyModifiedProperties();

            Image pausePanel = CreateUiImage(canvasGo.transform, "Pause Help",
                new Color(0.075f, 0.045f, 0.025f, 0.93f));
            SetRect(pausePanel.rectTransform, new Vector2(0.24f, 0.25f), new Vector2(0.76f, 0.75f),
                Vector2.zero, Vector2.zero);
            CanvasGroup pauseGroup = pausePanel.gameObject.AddComponent<CanvasGroup>();
            string pauseControls = _activePainting switch
            {
                0 => "拖动：移动并切换远近格  •  Q / E：旋转景物  •  空格：俯看棋盘",
                1 => "滚轮：切换远近层  •  旋转：本关未开放",
                _ => "滚轮：切换远近层  •  Q / E：旋转景物",
            };
            if (_activePainting == 1)
                pauseControls = "滚轮：切换远近层  •  Q / E：旋转景物";
            Text pauseText = CreateUiText(pausePanel.transform, "Pause Instructions",
                "暂停\n\n拖动景物，松手后吸附到金色构图格\n" + pauseControls + "\nTab：放大对照  •  H：标出目标  •  G：辅助摆放\nR：重置  •  Ctrl + Z：撤销\n\n按 Esc 继续",
                20, FontStyle.Normal, new Color(0.95f, 0.91f, 0.83f), TextAnchor.MiddleCenter);
            SetRect(pauseText.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(30f, 24f), new Vector2(-30f, -24f));
            var pauseMenu = canvasGo.AddComponent<PaintingPauseMenu>();
            var pauseSerialized = new SerializedObject(pauseMenu);
            pauseSerialized.FindProperty("_flow").objectReferenceValue = flow;
            pauseSerialized.FindProperty("_manipulation").objectReferenceValue = manipulation;
            pauseSerialized.FindProperty("_panel").objectReferenceValue = pauseGroup;
            pauseSerialized.ApplyModifiedProperties();
        }

        private static string IntroInstructions()
        {
            string controls = _activePainting switch
            {
                0 => "拖动桥和亭子，按 Q / E 调整朝向；按住空格可以俯看棋盘。",
                1 => "拖动：摆放景物  •  滚轮：调整前后",
                _ => "拖动：摆放景物  •  滚轮：调整前后  •  Q / E：旋转"
            };
            if (_activePainting == 1)
                controls = "拖动：摆放景物  •  滚轮：调整前后  •  Q / E：旋转";
            string assist = _activePainting == 0
                ? "金色格线会在选中后出现，靠近正确位置会自动吸附  •  Tab：放大对照"
                : "Tab：放大对照  •  H：标出目标区域  •  G：辅助摆好当前景物";
            return "从唯一正确的视角，还原这幅山水画。\n" + controls
                + "\n" + assist
                + "\n\n点击或按空格键进入展厅";
        }

        private static Image CreateUiImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RawImage CreateUiRawImage(Transform parent, string name, Texture texture)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateUiText(Transform parent, string name, string value, int size,
            FontStyle style, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static void SetTopRect(RectTransform rect, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        /// <summary>
        /// T-010C: applies the deterministic unsolved start layout
        /// (translation plus quantized yaw) to the eight piece roots, after
        /// their authored transforms are captured and the controller wiring
        /// is final. Only the root position and root yaw rotation change:
        /// children, scales, materials, cameras, target textures, piece IDs,
        /// scorer policy and every authored serialized field stay exactly as
        /// built, and the Arch Bridge compatibility API is untouched.
        /// </summary>
        private static void ApplyUnsolvedStartLayout(Transform scenery)
        {
            for (int i = 0; i < RequiredPieces.Length; i++)
            {
                var pieceRoot = scenery.Find(RequiredPieces[i]);
                if (pieceRoot == null)
                    throw new InvalidOperationException(
                        "PaintingPrototype wiring failed: Solved Scenery/" + RequiredPieces[i]
                        + " is missing for the start layout.");
                var handle = pieceRoot.GetComponent<PaintingManipulablePiece>();
                if (handle == null || !handle.IsConfigured)
                    throw new InvalidOperationException(
                        "PaintingPrototype wiring failed: Solved Scenery/" + RequiredPieces[i]
                        + " must be configured before the start layout is applied.");

                pieceRoot.position = handle.AuthoredPosition + (IsActivePiece(i) ? StartOffsetFor(i) : Vector3.zero);
                if (IsActivePiece(i) && UnsolvedStartYawOffsets[i] != 0f)
                    pieceRoot.rotation = Quaternion.Euler(0f, UnsolvedStartYawOffsets[i], 0f) * handle.AuthoredRotation;
                else
                    pieceRoot.rotation = handle.AuthoredRotation;
            }
        }

        private static Vector3 StartOffsetFor(int pieceIndex)
        {
            if (_activePainting == 1)
            {
                if (pieceIndex == 1) return new Vector3(-1.15f, 0f, 0.90f);
                if (pieceIndex == 2) return new Vector3(1.35f, 0f, 1.30f);
            }
            return UnsolvedStartOffsets[pieceIndex];
        }

        /// <summary>
        /// Fits exactly one <see cref="BoxCollider"/> onto the piece root
        /// from every child renderer's world bounds: all eight corners of
        /// each renderer world AABB are converted into root-local space and
        /// encapsulated, so rotated roots (the Sun disc) are covered
        /// correctly, with a small constant padding. No Rigidbody is added.
        /// </summary>
        private static BoxCollider FitPieceCollider(Transform pieceRoot)
        {
            Renderer[] renderers = pieceRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException(
                    "PaintingPrototype wiring failed: Solved Scenery/" + pieceRoot.name
                    + " has no renderer to fit the selection collider.");

            Vector3 min = Vector3.positiveInfinity;
            Vector3 max = Vector3.negativeInfinity;
            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds world = renderers[i].bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 localCorner = pieceRoot.InverseTransformPoint(WorldCorner(world, corner));
                    min = Vector3.Min(min, localCorner);
                    max = Vector3.Max(max, localCorner);
                }
            }

            Vector3 minLocal = min - Vector3.one * ColliderPadding;
            Vector3 maxLocal = max + Vector3.one * ColliderPadding;

            var collider = pieceRoot.gameObject.AddComponent<BoxCollider>();
            collider.center = (minLocal + maxLocal) * 0.5f;
            collider.size = maxLocal - minLocal;
            return collider;
        }

        /// <summary>Returns the corner of an axis-aligned world bounds selected by its bit pattern.</summary>
        private static Vector3 WorldCorner(Bounds bounds, int corner)
        {
            return new Vector3(
                (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
        }

        /// <summary>
        /// The index of the "PaintingPiece" user layer, or -1 when missing.
        /// LayerMask.NameToLayer is checked first; the TagManager asset is
        /// scanned as a fallback so validation works right after a fresh
        /// creation without an asset refresh.
        /// </summary>
        private static int GetPaintingPieceLayerIndex()
        {
            int index = LayerMask.NameToLayer(PaintingPieceLayerName);
            if (index >= 0)
                return index;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == PaintingPieceLayerName)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Returns the existing "PaintingPiece" layer, or creates it
        /// deterministically on the first free user layer index (6..31) by
        /// editing the TagManager only when missing. Throws when no free user
        /// layer exists.
        /// </summary>
        private static int EnsurePaintingPieceLayer()
        {
            int existing = GetPaintingPieceLayerIndex();
            if (existing >= 0)
                return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            int index = -1;
            for (int i = 6; i < layers.arraySize; i++)
            {
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
                throw new InvalidOperationException(
                    "PaintingPrototype wiring failed: no free user layer available for 'PaintingPiece'.");

            layers.GetArrayElementAtIndex(index).stringValue = PaintingPieceLayerName;
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return index;
        }

        private static int EnsureCompositionGuideLayer()
        {
            int existing = LayerMask.NameToLayer(CompositionGuideLayerName);
            if (existing >= 0)
                return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == CompositionGuideLayerName)
                    return i;

            for (int i = 6; i < layers.arraySize; i++)
            {
                if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                    continue;
                layers.GetArrayElementAtIndex(i).stringValue = CompositionGuideLayerName;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                return i;
            }
            throw new InvalidOperationException(
                "PaintingPrototype wiring failed: no free user layer available for 'CompositionGuide'.");
        }

        /// <summary>
        /// Sun: a physical tabletop ornament. Its root and weighted base rest
        /// on the shared lake surface; a slim post supports the warm disc in
        /// the upper-left of the solved composition. Only the disc pivot faces
        /// the Composition Camera, so pickup and placement read as moving an
        /// object rather than dragging a detached sky decal.
        /// </summary>
        private static void BuildSun(Transform scenery)
        {
            var sunMat = GetOrCreateMaterial("Mat_SunHalo", SunHalo, 0.30f, 0f);
            var coreMat = GetOrCreateMaterial("Mat_SunCore", SunCore, 0.30f, 0f);

            var sun = new GameObject("Sun").transform;
            sun.SetParent(scenery, false);
            sun.position = new Vector3(2.12f, WaterTopY, -2.25f);

            // The support belongs to the physical build view but visually
            // dissolves into parchment/mist from the target camera.
            var standMat = GetOrCreateMaterial("Mat_SunStand", SkyIvory, 0.34f, 0f);
            var baseMesh = GetOrCreateMesh("SunStandBase", new Vector3(0.52f, 0.12f, 0.38f), 0.05f, 4);
            var postMesh = GetOrCreateMesh("SunStandPost", new Vector3(0.10f, 1.82f, 0.10f), 0.035f, 3);
            CreateMeshRenderer(sun, "Weighted Base", new Vector3(0f, 0.06f, 0f), baseMesh, standMat, Quaternion.identity);
            CreateMeshRenderer(sun, "Support Post", new Vector3(0f, 0.97f, 0f), postMesh, standMat, Quaternion.identity);

            var discPivot = new GameObject("Sun Disc Pivot").transform;
            discPivot.SetParent(sun, false);
            discPivot.localPosition = new Vector3(0f, 1.97f, 0f);
            Vector3 discWorldPosition = sun.position + discPivot.localPosition;
            var towardComposition = (CompositionCameraPosition - discWorldPosition).normalized;
            discPivot.rotation = Quaternion.FromToRotation(Vector3.up, towardComposition);

            var haloMesh = GetOrCreateConeMesh("SunDisc", 0.42f, 0.42f, 0.09f, 40);
            var coreMesh = GetOrCreateConeMesh("SunCoreDisc", 0.28f, 0.28f, 0.06f, 32);
            CreateMeshRenderer(discPivot, "Halo", Vector3.zero, haloMesh, sunMat, Quaternion.identity);
            CreateMeshRenderer(discPivot, "Core", new Vector3(0f, 0.055f, 0f), coreMesh, coreMat, Quaternion.identity);
        }

        /// <summary>
        /// Pale celadon ridge at the far shore, left of center: the
        /// subordinate silhouette of the rear mountain line.
        /// </summary>
        private static void BuildFarMountain(Transform scenery)
        {
            var paleMat = GetOrCreateMaterial("Mat_CeladonPale", CeladonPale, 0.35f, 0f);
            var shadowMat = GetOrCreateMaterial("Mat_MountainShadow", CeladonJade, 0.28f, 0f);
            var goldMat = GetOrCreateMaterial("Mat_MountainGold", WarmGold, 0.35f, 0.08f);

            var far = new GameObject("Far Mountain").transform;
            far.SetParent(scenery, false);
            far.position = new Vector3(0.65f, WaterTopY, -1.85f);

            if (UseUnifiedHeroArt)
            {
                paleMat = GetOrCreateCeladonMaterial("T026_CeladonPale", new Color(0.48f, 0.68f, 0.57f),
                    new Color(0.17f, 0.30f, 0.29f), new Color(0.82f, 0.91f, 0.78f), 0.42f, 0.10f);
                shadowMat = GetOrCreateCeladonMaterial("T026_CeladonShadow", new Color(0.24f, 0.48f, 0.41f),
                    new Color(0.09f, 0.22f, 0.22f), new Color(0.62f, 0.82f, 0.68f), 0.46f, 0.12f);
                var heroMain = GetOrCreateHeroMountainMesh("T026_FarMountain_Main", 1.85f, 0.82f, 1.34f, 18, 17);
                var heroShoulder = GetOrCreateHeroMountainMesh("T026_FarMountain_Shoulder", 0.82f, 0.55f, 0.72f, 16, 31);
                var heroFace = GetOrCreateHeroMountainMesh("T026_FarMountain_Face", 0.88f, 0.42f, 1.02f, 16, 73);
                CreateMeshRenderer(far, "Sculpted Far Peak", Vector3.zero, heroMain, paleMat,
                    Quaternion.Euler(0f, -5f, 0f));
                CreateMeshRenderer(far, "Sculpted Shoulder", new Vector3(0.58f, 0f, 0.16f),
                    heroShoulder, shadowMat, Quaternion.Euler(0f, 11f, 0f));
                CreateMeshRenderer(far, "Pale Glaze Face", new Vector3(-0.18f, 0f, 0.34f),
                    heroFace, paleMat, Quaternion.Euler(0f, -7f, 0f));
                var accent = GetOrCreateMesh("T026_FarMountain_GoldVein", new Vector3(0.035f, 0.56f, 0.035f), 0.014f, 4);
                CreateMeshRenderer(far, "Gold Ridge Inlay", new Vector3(-0.20f, 0.72f, 0.38f),
                    accent, goldMat, Quaternion.Euler(7f, 0f, -17f));
                return;
            }

            var mainMesh = GetOrCreateSilhouetteMesh("MountainReliefFarV1", new[]
            {
                new Vector2(-0.95f, 0f), new Vector2(-0.78f, 0.26f), new Vector2(-0.60f, 0.40f),
                new Vector2(-0.35f, 0.84f), new Vector2(-0.12f, 1.16f), new Vector2(0.06f, 0.78f),
                new Vector2(0.28f, 0.48f), new Vector2(0.52f, 0.72f), new Vector2(0.68f, 0.34f),
                new Vector2(0.93f, 0f),
            }, 0.30f);
            CreateMeshRenderer(far, "Painted Ridge", Vector3.zero, mainMesh, paleMat, Quaternion.identity);
            var washMesh = GetOrCreateSilhouetteMesh("MountainReliefFarWashV1", new[]
            {
                new Vector2(-0.55f, 0f), new Vector2(-0.36f, 0.32f), new Vector2(-0.12f, 0.82f),
                new Vector2(0.06f, 0.54f), new Vector2(0.30f, 0.20f), new Vector2(0.50f, 0f),
            }, 0.05f);
            CreateMeshRenderer(far, "Ink Wash", new Vector3(-0.13f, 0.02f, 0.18f), washMesh, shadowMat, Quaternion.identity);
            var accentMesh = GetOrCreateMesh("MountainGoldAccent", new Vector3(0.045f, 0.48f, 0.04f), 0.018f, 4);
            CreateMeshRenderer(far, "Gold Vein", new Vector3(-0.18f, 0.52f, 0.23f), accentMesh, goldMat, Quaternion.Euler(0f, 0f, -20f));
        }

        /// <summary>
        /// Deeper jade mass right of center, partly overlapping the far ridge:
        /// the dominant silhouette of the rear mountain line.
        /// </summary>
        private static void BuildMiddleMountain(Transform scenery)
        {
            var jadeMat = GetOrCreateMaterial("Mat_CeladonJade", CeladonJade, 0.35f, 0f);
            var highlightMat = GetOrCreateMaterial("Mat_JadeHighlight", JadeHighlight, 0.38f, 0f);
            var inkMat = GetOrCreateMaterial("Mat_InkGreen", MossGreen, 0.25f, 0f);

            var middle = new GameObject("Middle Mountain").transform;
            middle.SetParent(scenery, false);
            middle.position = new Vector3(-0.45f, WaterTopY, -1.2f);

            if (UseUnifiedHeroArt)
            {
                jadeMat = GetOrCreateCeladonMaterial("T026_CeladonMain", new Color(0.25f, 0.52f, 0.44f),
                    new Color(0.08f, 0.20f, 0.21f), new Color(0.63f, 0.85f, 0.70f), 0.50f, 0.13f);
                highlightMat = GetOrCreateCeladonMaterial("T026_CeladonHighlight", new Color(0.46f, 0.68f, 0.56f),
                    new Color(0.16f, 0.31f, 0.29f), new Color(0.84f, 0.94f, 0.80f), 0.52f, 0.11f);
                inkMat = GetOrCreateCeladonMaterial("T026_CeladonInk", new Color(0.15f, 0.36f, 0.31f),
                    new Color(0.05f, 0.15f, 0.17f), new Color(0.50f, 0.72f, 0.60f), 0.38f, 0.10f);
                var heroMain = GetOrCreateHeroMountainMesh("T029_MiddleMountain_MainV3", 2.05f, 1.12f, 2.10f, 24, 107);
                var heroLeft = GetOrCreateHeroMountainMesh("T029_MiddleMountain_LeftV3", 1.02f, 0.76f, 1.16f, 18, 143);
                var heroRight = GetOrCreateHeroMountainMesh("T029_MiddleMountain_RightV3", 0.86f, 0.68f, 0.94f, 18, 159);
                var heroFace = GetOrCreateHeroMountainMesh("T029_MiddleMountain_FaceV3", 0.98f, 0.52f, 1.58f, 20, 189);
                var rearSpire = GetOrCreateHeroMountainMesh("T029_MiddleMountain_RearV3", 0.64f, 0.54f, 1.28f, 16, 211);
                var foothill = GetOrCreateHeroMountainMesh("T029_MiddleMountain_FoothillV3", 0.72f, 0.62f, 0.58f, 16, 227);
                CreateMeshRenderer(middle, "Sculpted Main Peak", Vector3.zero, heroMain, jadeMat,
                    Quaternion.Euler(0f, 4f, 0f));
                CreateMeshRenderer(middle, "Left Ridge", new Vector3(-0.76f, 0f, 0.16f), heroLeft,
                    inkMat, Quaternion.Euler(0f, -12f, 0f));
                CreateMeshRenderer(middle, "Right Ridge", new Vector3(0.68f, 0f, 0.10f), heroRight,
                    highlightMat, Quaternion.Euler(0f, 13f, 0f));
                CreateMeshRenderer(middle, "Jade Glaze Face", new Vector3(-0.06f, 0f, 0.48f), heroFace,
                    highlightMat, Quaternion.Euler(0f, -5f, 0f));
                CreateMeshRenderer(middle, "Rear Needle Peak", new Vector3(0.34f, 0f, -0.28f), rearSpire,
                    inkMat, Quaternion.Euler(0f, 18f, 0f));
                CreateMeshRenderer(middle, "Front Foothill", new Vector3(0.78f, 0f, 0.42f), foothill,
                    jadeMat, Quaternion.Euler(0f, -15f, 0f));
                var goldMat = GetOrCreateMaterial("Mat_MountainGold", WarmGold, 0.35f, 0.08f);
                var longVein = GetOrCreateMesh("T029_MountainVeinLongV3", new Vector3(0.026f, 0.60f, 0.026f), 0.010f, 5);
                var shortVein = GetOrCreateMesh("T029_MountainVeinShortV3", new Vector3(0.024f, 0.36f, 0.024f), 0.009f, 5);
                CreateMeshRenderer(middle, "Gold Vein Left", new Vector3(-0.42f, 1.05f, 0.55f), longVein,
                    goldMat, Quaternion.Euler(8f, 0f, -21f));
                CreateMeshRenderer(middle, "Gold Vein Crown", new Vector3(0.08f, 1.55f, 0.52f), shortVein,
                    goldMat, Quaternion.Euler(6f, 0f, 13f));
                CreateMeshRenderer(middle, "Gold Vein Right", new Vector3(0.48f, 0.70f, 0.49f), shortVein,
                    goldMat, Quaternion.Euler(-4f, 0f, 24f));
                return;
            }

            var mainMesh = GetOrCreateSilhouetteMesh("MountainReliefMainV1", new[]
            {
                new Vector2(-1.18f, 0f), new Vector2(-0.98f, 0.30f), new Vector2(-0.72f, 0.62f),
                new Vector2(-0.50f, 1.16f), new Vector2(-0.24f, 1.84f), new Vector2(0.02f, 2.12f),
                new Vector2(0.22f, 1.58f), new Vector2(0.43f, 0.96f), new Vector2(0.62f, 1.18f),
                new Vector2(0.79f, 0.72f), new Vector2(1.18f, 0f),
            }, 0.38f);
            CreateMeshRenderer(middle, "Main Painted Peak", Vector3.zero, mainMesh, jadeMat, Quaternion.identity);
            var highlightMesh = GetOrCreateSilhouetteMesh("MountainReliefMainHighlightV1", new[]
            {
                new Vector2(-0.45f, 0f), new Vector2(-0.30f, 0.44f), new Vector2(-0.06f, 1.45f),
                new Vector2(0.10f, 1.78f), new Vector2(0.22f, 1.12f), new Vector2(0.48f, 0f),
            }, 0.05f);
            var inkMesh = GetOrCreateSilhouetteMesh("MountainReliefMainInkV1", new[]
            {
                new Vector2(-0.44f, 0f), new Vector2(-0.20f, 0.62f), new Vector2(0.02f, 1.08f),
                new Vector2(0.19f, 0.55f), new Vector2(0.43f, 0f),
            }, 0.05f);
            CreateMeshRenderer(middle, "Glaze Highlight", new Vector3(0.03f, 0.02f, 0.23f), highlightMesh, highlightMat, Quaternion.identity);
            CreateMeshRenderer(middle, "Ink Ridge", new Vector3(-0.50f, 0.01f, 0.25f), inkMesh, inkMat, Quaternion.identity);
        }

        /// <summary>
        /// Deep moss green toy trees framing the composition's left edge with
        /// varied canopy sizes, kept short so they never hide the sun, the
        /// pavilion or the mountains.
        /// </summary>
        private static void BuildTreeClusterLeft(Transform scenery)
        {
            var cluster = new GameObject("Tree Cluster Left").transform;
            cluster.SetParent(scenery, false);
            cluster.position = new Vector3(2.25f, WaterTopY, -0.8f);
            BuildTree(cluster, "Tree A", new Vector3(-0.22f, 0.02f), 1.0f, 12f);
            BuildTree(cluster, "Tree B", new Vector3(0.06f, -0.15f), 0.8f, -18f);
            BuildTree(cluster, "Tree C", new Vector3(0.30f, 0.05f), 0.62f, 8f);
        }

        /// <summary>Smaller moss green pair framing the composition's right edge.</summary>
        private static void BuildTreeClusterRight(Transform scenery)
        {
            var cluster = new GameObject("Tree Cluster Right").transform;
            cluster.SetParent(scenery, false);
            cluster.position = new Vector3(-2.45f, WaterTopY, -1.0f);
            BuildTree(cluster, "Tree A", new Vector3(-0.16f, 0.05f), 0.9f, -10f);
            BuildTree(cluster, "Tree B", new Vector3(0.18f, -0.10f), 0.7f, 14f);
        }

        private static void BuildTree(Transform cluster, string name, Vector3 localPosition, float scale, float yaw)
        {
            var treeMat = GetOrCreateMaterial("Mat_MossGreen", MossGreen, 0.25f, 0f);
            var leafLightMat = GetOrCreateMaterial("Mat_LeafHighlight", JadeHighlight * 0.72f, 0.28f, 0f);
            var trunkMat = GetOrCreateMaterial("Mat_BarkBrown", BarkBrown, 0.32f, 0f);
            var trunkMesh = GetOrCreateMesh("TreeTrunk", new Vector3(0.10f, 0.56f, 0.10f), 0.04f, 6);
            var canopyMesh = GetOrCreateMesh("TreeCanopyLayered", new Vector3(0.78f, 0.48f, 0.58f), 0.20f, 8);

            var tree = new GameObject(name).transform;
            tree.SetParent(cluster, false);
            tree.localPosition = localPosition;
            tree.localRotation = Quaternion.Euler(0f, yaw, 0f);

            if (UseUnifiedHeroArt)
            {
                treeMat = GetOrCreateCeladonMaterial("T026_PineDark", new Color(0.12f, 0.31f, 0.23f),
                    new Color(0.035f, 0.11f, 0.10f), new Color(0.38f, 0.58f, 0.42f), 0.30f, 0.07f);
                leafLightMat = GetOrCreateCeladonMaterial("T026_PineLight", new Color(0.28f, 0.49f, 0.36f),
                    new Color(0.08f, 0.19f, 0.16f), new Color(0.58f, 0.75f, 0.56f), 0.34f, 0.08f);
                var trunkV3 = GetOrCreateCurvedBranchMesh("T029_PineTrunkV3", new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(-0.04f, 0.30f, 0.01f),
                    new Vector3(0.06f, 0.60f, -0.015f), new Vector3(0.02f, 0.86f, 0.02f),
                    new Vector3(0.14f, 1.10f, 0f),
                }, 0.075f, 0.035f, 9);
                var branchLeftV3 = GetOrCreateCurvedBranchMesh("T029_PineBranchLeftV3", new[]
                {
                    new Vector3(0.02f, 0.57f, 0f), new Vector3(-0.18f, 0.70f, 0.01f),
                    new Vector3(-0.42f, 0.74f, 0.02f),
                }, 0.045f, 0.018f, 8);
                var branchRightV3 = GetOrCreateCurvedBranchMesh("T029_PineBranchRightV3", new[]
                {
                    new Vector3(0.05f, 0.73f, 0f), new Vector3(0.28f, 0.82f, -0.02f),
                    new Vector3(0.48f, 0.78f, -0.03f),
                }, 0.041f, 0.016f, 8);
                var heroCanopy = GetOrCreateCloudCanopyMesh("T029_CloudPineCanopyV3", 0.76f, 0.52f, 0.22f, 18);
                CreateMeshRenderer(tree, "Continuous Curved Trunk", Vector3.zero, trunkV3,
                    trunkMat, Quaternion.identity, Vector3.one * scale);
                CreateMeshRenderer(tree, "Connected Left Branch", Vector3.zero, branchLeftV3,
                    trunkMat, Quaternion.identity, Vector3.one * scale);
                CreateMeshRenderer(tree, "Connected Right Branch", Vector3.zero, branchRightV3,
                    trunkMat, Quaternion.identity, Vector3.one * scale);
                CreateMeshRenderer(tree, "Cloud Crown", new Vector3(0.14f * scale, 1.08f * scale, 0f),
                    heroCanopy, treeMat, Quaternion.Euler(0f, -8f, 1f), Vector3.one * scale);
                CreateMeshRenderer(tree, "Cloud Left", new Vector3(-0.38f * scale, 0.75f * scale, 0.02f),
                    heroCanopy, leafLightMat, Quaternion.Euler(0f, 13f, -2f), Vector3.one * (scale * 0.76f));
                CreateMeshRenderer(tree, "Cloud Right", new Vector3(0.46f * scale, 0.80f * scale, -0.03f),
                    heroCanopy, treeMat, Quaternion.Euler(0f, -17f, 2f), Vector3.one * (scale * 0.70f));
                CreateMeshRenderer(tree, "Cloud Top", new Vector3(0.02f * scale, 1.25f * scale, 0.01f),
                    heroCanopy, leafLightMat, Quaternion.Euler(0f, 20f, 0f), Vector3.one * (scale * 0.48f));
                return;
            }
            CreateMeshRenderer(tree, "Trunk", new Vector3(0f, 0.28f * scale, 0f),
                trunkMesh, trunkMat, Quaternion.identity, Vector3.one * scale);
            CreateMeshRenderer(tree, "Canopy Crown", new Vector3(0f, 0.94f * scale, 0f),
                canopyMesh, treeMat, Quaternion.identity, Vector3.one * scale);
            CreateMeshRenderer(tree, "Canopy Left", new Vector3(-0.30f * scale, 0.76f * scale, 0.02f),
                canopyMesh, treeMat, Quaternion.Euler(0f, -12f, 4f), Vector3.one * (scale * 0.74f));
            CreateMeshRenderer(tree, "Canopy Right", new Vector3(0.30f * scale, 0.80f * scale, -0.02f),
                canopyMesh, leafLightMat, Quaternion.Euler(0f, 14f, -3f), Vector3.one * (scale * 0.68f));
        }

        /// <summary>Porcelain-white toy pavilion in the composition's right-middle third, on the water in front of the middle mountain.</summary>
        private static void BuildPavilion(Transform scenery)
        {
            var porcelainMat = GetOrCreateMaterial("Mat_Porcelain", Porcelain, 0.45f, 0f);
            var roofMat = GetOrCreateMaterial("Mat_PavilionRoofJade", CeladonJade, 0.52f, 0.04f);
            var columnMat = GetOrCreateMaterial("Mat_PavilionWood", BarkBrown, 0.38f, 0f);
            var goldMat = GetOrCreateMaterial("Mat_PavilionGold", WarmGold, 0.55f, 0.22f);

            var pavilion = new GameObject("Pavilion").transform;
            pavilion.SetParent(scenery, false);
            pavilion.position = new Vector3(-0.85f, WaterTopY, -0.55f);
            // Keep the pavilion readable behind the unified hero bridge in
            // every target view. The former 0.78 scale was tuned for the old
            // shallow roof and became completely occluded after conversion.
            pavilion.localScale = Vector3.one * 0.94f;

            if (UseUnifiedHeroArt)
            {
                porcelainMat = GetOrCreateCeladonMaterial("T026_PorcelainIvory", new Color(0.86f, 0.84f, 0.74f),
                    new Color(0.30f, 0.31f, 0.29f), Color.white, 0.48f, 0.07f);
                roofMat = GetOrCreateCeladonMaterial("T026_RoofJade", new Color(0.22f, 0.52f, 0.46f),
                    new Color(0.06f, 0.18f, 0.20f), new Color(0.66f, 0.86f, 0.72f), 0.56f, 0.14f);
            }

            var baseMesh = GetOrCreateMesh("PavilionBase", new Vector3(0.95f, 0.12f, 0.8f), 0.04f, 8);
            var columnMesh = GetOrCreateMesh("PavilionColumn", new Vector3(0.09f, 0.5f, 0.09f), 0.03f, 6);
            var roofMesh = GetOrCreateHeroRoofMesh(
                "T029_PavilionRoofV3b", 1.24f, 1.02f, 0.34f, 0.075f, 20);
            var finialMesh = GetOrCreateMesh("PavilionFinial", new Vector3(0.11f, 0.11f, 0.11f), 0.045f, 6);
            var lintelMesh = GetOrCreateMesh("PavilionLintel", new Vector3(0.86f, 0.07f, 0.07f), 0.025f, 5);

            CreateMeshRenderer(pavilion, "Base", new Vector3(0f, 0.06f, 0f), baseMesh, porcelainMat, Quaternion.identity);
            int columnIndex = 0;
            foreach (float sideX in new[] { -1f, 1f })
            {
                foreach (float sideZ in new[] { -1f, 1f })
                {
                    CreateMeshRenderer(pavilion, "Column " + (++columnIndex),
                        new Vector3(0.38f * sideX, 0.37f, 0.3f * sideZ),
                        columnMesh, columnMat, Quaternion.identity);
                }
            }
            CreateMeshRenderer(pavilion, "Front Lintel", new Vector3(0f, 0.62f, 0.31f), lintelMesh, columnMat, Quaternion.identity);
            CreateMeshRenderer(pavilion, "Back Lintel", new Vector3(0f, 0.62f, -0.31f), lintelMesh, columnMat, Quaternion.identity);
            if (UseUnifiedHeroArt)
            {
                var stepMesh = GetOrCreateMesh("T026_PavilionStep", new Vector3(0.52f, 0.09f, 0.22f), 0.025f, 5);
                var sideLintelMesh = GetOrCreateMesh("T029_PavilionSideLintelV3", new Vector3(0.07f, 0.07f, 0.68f), 0.025f, 5);
                var bracketMesh = GetOrCreateMesh("T029_PavilionBracketV3", new Vector3(0.16f, 0.12f, 0.12f), 0.035f, 6);
                var railMeshV3 = GetOrCreateMesh("T029_PavilionRailV3", new Vector3(0.72f, 0.10f, 0.07f), 0.026f, 5);
                var eaveShadowMesh = GetOrCreateMesh("T029_PavilionEaveShadowV3", new Vector3(1.02f, 0.08f, 0.80f), 0.025f, 5);
                CreateMeshRenderer(pavilion, "Front Step", new Vector3(0f, 0.055f, 0.47f), stepMesh, porcelainMat, Quaternion.identity);
                CreateMeshRenderer(pavilion, "Left Lintel", new Vector3(-0.38f, 0.62f, 0f), sideLintelMesh, columnMat, Quaternion.identity);
                CreateMeshRenderer(pavilion, "Right Lintel", new Vector3(0.38f, 0.62f, 0f), sideLintelMesh, columnMat, Quaternion.identity);
                foreach (float x in new[] { -0.38f, 0.38f })
                foreach (float z in new[] { -0.30f, 0.30f })
                    CreateMeshRenderer(pavilion, "Connected Eave Bracket", new Vector3(x, 0.67f, z),
                        bracketMesh, goldMat, Quaternion.Euler(0f, x * z > 0f ? 35f : -35f, 0f));
                CreateMeshRenderer(pavilion, "Rear Safety Rail", new Vector3(0f, 0.23f, -0.35f),
                    railMeshV3, roofMat, Quaternion.identity);
                CreateMeshRenderer(pavilion, "Connected Eave Shadow", new Vector3(0f, 0.655f, 0f),
                    eaveShadowMesh, columnMat, Quaternion.identity);
                CreateMeshRenderer(pavilion, "Single Upturned Roof", new Vector3(0f, 0.68f, 0f), roofMesh, roofMat, Quaternion.identity);
                CreateMeshRenderer(pavilion, "Finial", new Vector3(0f, 1.03f, 0f), finialMesh, goldMat, Quaternion.identity,
                    new Vector3(0.85f, 1.45f, 0.85f));
            }
            else
            {
                CreateMeshRenderer(pavilion, "Roof", new Vector3(0f, 0.62f, 0f), roofMesh, roofMat, Quaternion.identity);
                CreateMeshRenderer(pavilion, "Finial", new Vector3(0f, 0.90f, 0f), finialMesh, goldMat, Quaternion.identity);
            }
        }

        /// <summary>
        /// Stone arch bridge crossing the water in the lower-middle third, in
        /// front of the pavilion and mountain: a low parabolic arch whose
        /// underside stays clear above the water so the lake reads through the
        /// opening, and whose right end tucks behind the foreground rock.
        /// </summary>
        private static void BuildArchBridge(Transform scenery)
        {
            var bridgeMat = GetOrCreateMaterial("Mat_BridgeStone", BridgeStone, 0.30f, 0f);
            var railMat = GetOrCreateMaterial("Mat_BridgeRail", JadeHighlight, 0.42f, 0f);
            var goldMat = GetOrCreateMaterial("Mat_BridgeGold", WarmGold, 0.48f, 0.16f);

            var bridge = new GameObject("Arch Bridge").transform;
            bridge.SetParent(scenery, false);
            bridge.position = new Vector3(0f, WaterTopY, _activePainting == 0 ? 0.55f : 0.95f);
            bridge.localScale = Vector3.one * (_activePainting == 0 ? 0.84f : 1f);

            if (UseUnifiedHeroArt)
            {
                bridgeMat = GetOrCreateCeladonMaterial("T026_BridgeIvory", new Color(0.72f, 0.76f, 0.66f),
                    new Color(0.24f, 0.28f, 0.27f), new Color(0.92f, 0.94f, 0.80f), 0.40f, 0.06f);
                railMat = GetOrCreateCeladonMaterial("T026_BridgeJade", new Color(0.40f, 0.62f, 0.51f),
                    new Color(0.12f, 0.25f, 0.24f), new Color(0.72f, 0.88f, 0.70f), 0.46f, 0.10f);
            }

            float bridgeSpan = _activePainting == 0 ? 1.94f : 1.85f;
            float bridgeWidth = _activePainting == 0 ? 0.48f : 0.42f;
            float bridgeApex = _activePainting == 0 ? 0.35f : 0.34f;
            var bridgeMesh = GetOrCreateArchMesh("T029_ArchBridgeBodyV3",
                bridgeSpan, bridgeWidth, bridgeApex, 0.15f, 14);
            CreateMeshRenderer(bridge, "Deck", Vector3.zero, bridgeMesh, bridgeMat, Quaternion.identity);
            var postMesh = GetOrCreateMesh("BridgeRailPost", new Vector3(0.055f, 0.28f, 0.055f), 0.018f, 4);
            var railMesh = GetOrCreateMesh("BridgeRailBar", new Vector3(1.72f, 0.055f, 0.055f), 0.018f, 4);
            if (UseUnifiedHeroArt)
            {
                BuildHeroBridgeRails(bridge, bridgeSpan, bridgeWidth, bridgeApex, bridgeMat, railMat, goldMat, postMesh);
                var landingMesh = GetOrCreateMesh("T029_BridgeLandingV3", new Vector3(0.38f, 0.13f, 0.64f), 0.055f, 7);
                var landingStep = GetOrCreateMesh("T029_BridgeLandingStepV3", new Vector3(0.22f, 0.07f, 0.54f), 0.025f, 5);
                var archTrim = GetOrCreateArchMesh("T029_BridgeArchTrimV3", bridgeSpan * 0.96f, 0.045f,
                    bridgeApex + 0.025f, 0.045f, 18);
                CreateMeshRenderer(bridge, "Front Arch Glaze Trim", new Vector3(0f, 0.015f, bridgeWidth * 0.52f),
                    archTrim, railMat, Quaternion.identity);
                CreateMeshRenderer(bridge, "Back Arch Glaze Trim", new Vector3(0f, 0.015f, -bridgeWidth * 0.52f),
                    archTrim, railMat, Quaternion.identity);
                foreach (float side in new[] { -1f, 1f })
                {
                    CreateMeshRenderer(bridge, "Connected Landing", new Vector3(side * bridgeSpan * 0.52f, 0.065f, 0f),
                        landingMesh, bridgeMat, Quaternion.identity);
                    CreateMeshRenderer(bridge, "Landing Step", new Vector3(side * bridgeSpan * 0.66f, 0.035f, 0f),
                        landingStep, bridgeMat, Quaternion.identity);
                }
                return;
            }
            foreach (float z in new[] { -0.19f, 0.19f })
            {
                CreateMeshRenderer(bridge, "Rail", new Vector3(0f, 0.35f, z), railMesh, railMat, Quaternion.identity);
                foreach (float x in new[] { -0.78f, -0.39f, 0f, 0.39f, 0.78f })
                    CreateMeshRenderer(bridge, "Rail Post", new Vector3(x, 0.24f + 0.11f * (1f - Mathf.Abs(x) / 0.78f), z),
                        postMesh, x == 0f ? goldMat : railMat, Quaternion.identity);
            }
        }

        /// <summary>
        /// Foreground rock: a compact stack of three rounded celadon stones
        /// anchoring the composition's lower-right corner, small enough that
        /// it frames the bridge end instead of covering the center.
        /// </summary>
        private static void BuildForegroundRock(Transform scenery)
        {
            var stoneMat = GetOrCreateMaterial("Mat_CeladonStone", CeladonStone, 0.30f, 0f);

            var rock = new GameObject("Foreground Rock").transform;
            rock.SetParent(scenery, false);
            rock.position = new Vector3(-1.45f, WaterTopY, 1.65f);

            var bigMesh = GetOrCreateMesh("RockStone_Big", new Vector3(0.78f, 0.46f, 0.62f), 0.16f, 8);
            var midMesh = GetOrCreateMesh("RockStone_Mid", new Vector3(0.44f, 0.34f, 0.4f), 0.12f, 8);
            var smallMesh = GetOrCreateMesh("RockStone_Small", new Vector3(0.28f, 0.2f, 0.26f), 0.07f, 6);

            CreateMeshRenderer(rock, "Big Stone", new Vector3(0f, 0.23f, 0f), bigMesh, stoneMat, Quaternion.Euler(0f, 15f, 0f));
            CreateMeshRenderer(rock, "Mid Stone", new Vector3(0.16f, 0.5f, 0.08f), midMesh, stoneMat, Quaternion.Euler(0f, -25f, 0f));
            CreateMeshRenderer(rock, "Small Stone", new Vector3(-0.08f, 0.72f, -0.04f), smallMesh, stoneMat, Quaternion.Euler(0f, 40f, 0f));
        }

        /// <summary>
        /// T-026 rails follow the bridge parabola instead of floating as one
        /// straight bar. Each short rail segment joins the tops of adjacent
        /// posts, so the structure reads as manufactured and remains intact
        /// from both the build and composition cameras.
        /// </summary>
        private static void BuildHeroBridgeRails(Transform bridge, float span, float width, float apex,
            Material bridgeMaterial, Material railMaterial, Material goldMaterial, Mesh postMesh)
        {
            const int stationCount = 7;
            const float deckThickness = 0.15f;
            const float postHeight = 0.235f;
            var points = new Vector2[stationCount];
            for (int i = 0; i < stationCount; i++)
            {
                float t = (float)i / (stationCount - 1);
                float x = -span * 0.5f + span * t;
                float parabola = (2f * t - 1f) * (2f * t - 1f);
                float deckTop = apex * (1f - parabola) + deckThickness;
                points[i] = new Vector2(x, deckTop + postHeight);
            }

            foreach (float z in new[] { -width * 0.44f, width * 0.44f })
            {
                for (int i = 0; i < stationCount; i++)
                {
                    Vector2 point = points[i];
                    float deckTop = point.y - postHeight;
                    CreateMeshRenderer(bridge, "Sculpted Rail Post", new Vector3(point.x, deckTop + postHeight * 0.5f, z),
                        postMesh, i == stationCount / 2 ? goldMaterial : railMaterial, Quaternion.identity,
                        new Vector3(1.08f, 1.18f, 1.08f));
                    var capMesh = GetOrCreateMesh("T029_BridgePostCapV3", new Vector3(0.09f, 0.075f, 0.09f), 0.030f, 7);
                    CreateMeshRenderer(bridge, "Gold Post Cap", new Vector3(point.x, deckTop + postHeight + 0.025f, z),
                        capMesh, goldMaterial, Quaternion.identity);
                }

                for (int i = 0; i < stationCount - 1; i++)
                {
                    Vector2 a = points[i];
                    Vector2 b = points[i + 1];
                    Vector2 delta = b - a;
                    float length = delta.magnitude;
                    float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                    var railMesh = GetOrCreateMesh("T029_BridgeRailSegmentV3_" + i,
                        new Vector3(length + 0.045f, 0.065f, 0.065f), 0.021f, 5);
                    CreateMeshRenderer(bridge, "Curved Rail Segment", new Vector3((a.x + b.x) * 0.5f,
                        (a.y + b.y) * 0.5f, z), railMesh, railMaterial, Quaternion.Euler(0f, 0f, angle));
                }
            }

            var treadMesh = GetOrCreateMesh("T029_BridgeTreadV3", new Vector3(0.18f, 0.035f, width * 0.86f), 0.012f, 4);
            for (int i = 0; i < 11; i++)
            {
                float t = (float)i / 10f;
                float x = -span * 0.46f + span * 0.92f * t;
                float parabola = (2f * t - 1f) * (2f * t - 1f);
                float deckTop = apex * (1f - parabola) + deckThickness;
                float slope = -4f * apex * (2f * t - 1f) / span;
                float angle = Mathf.Atan(slope) * Mathf.Rad2Deg;
                CreateMeshRenderer(bridge, "Stone Bridge Tread", new Vector3(x, deckTop + 0.015f, 0f),
                    treadMesh, bridgeMaterial, Quaternion.Euler(0f, 0f, angle));
            }
        }

        private static void BuildLights(Transform root)
        {
            var lights = new GameObject("Lights").transform;
            lights.SetParent(root, false);

            // Soft key from behind-right: gentle contact shadows only, no
            // harsh black cast in the composition; shadows fall away from the
            // composition camera (toward -Z) so they stay subtle.
            var keyGo = new GameObject("Key Light (Directional)");
            keyGo.transform.SetParent(lights, false);
            keyGo.transform.rotation = Quaternion.Euler(50f, -145f, 0f);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.05f;
            key.color = new Color(1f, 0.965f, 0.91f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.42f;

            // Low-intensity cool fill from the build-camera side keeps the
            // back-facing surfaces of the diorama readable.
            var fillGo = new GameObject("Fill Light (Directional)");
            fillGo.transform.SetParent(lights, false);
            fillGo.transform.rotation = Quaternion.Euler(15f, 40f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.34f;
            fill.color = new Color(0.82f, 0.9f, 1f);
            fill.shadows = LightShadows.None;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.29f, 0.27f, 0.24f);
            RenderSettings.fog = false;
        }

        private static void BuildCameras(Transform root)
        {
            BuildCamera(root, "Build Camera", BuildCameraPosition, BuildCameraTarget,
                _activePainting == 0 ? 34f : 42f, true);
            Camera compositionCamera = BuildCamera(
                root, "Composition Camera", CompositionCameraPosition, CompositionCameraTarget, 24f, false);
            // The player manipulates scenery through the Build Camera. The
            // Composition Camera is a hidden scoring sensor: its matrices are
            // used by PaintingCompositionEvaluator and CaptureAll can still
            // call Camera.Render() explicitly while the component is disabled.
            compositionCamera.enabled = false;
            int guideLayer = LayerMask.NameToLayer(CompositionGuideLayerName);
            if (guideLayer >= 0)
                compositionCamera.cullingMask &= ~(1 << guideLayer);
            if (_activePainting == 3)
            {
                Camera secondary = BuildCamera(root, "Secondary Composition Camera",
                    new Vector3(7f, 2.15f, 0f), new Vector3(0f, 1.35f, 0f), 24f, false);
                secondary.enabled = false;
                if (guideLayer >= 0)
                    secondary.cullingMask &= ~(1 << guideLayer);
            }
        }

        private static Camera BuildCamera(Transform root, string name, Vector3 position, Vector3 target, float fov, bool tagged)
        {
            var camGo = new GameObject(name);
            if (tagged)
                camGo.tag = "MainCamera";
            camGo.transform.SetParent(root, false);
            camGo.transform.position = position;
            camGo.transform.rotation = Quaternion.LookRotation((target - position).normalized);

            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = fov;
            cam.aspect = 16f / 9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = WarmIvory;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.allowHDR = true;

            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            return cam;
        }

        private static void BuildPostProcessing(Transform root)
        {
            var profile = GetOrCreateProfile();

            if (!profile.TryGet<Tonemapping>(out var tonemapping))
                tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.Neutral;

            if (!profile.TryGet<ColorAdjustments>(out var colorAdjustments))
                colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.value = _activePainting == 0 ? -0.04f : -0.1f;
            colorAdjustments.contrast.value = _activePainting == 0 ? 8f : 5f;
            colorAdjustments.saturation.value = _activePainting == 0 ? 3f : 18f;
            colorAdjustments.hueShift.value = 0f;
            colorAdjustments.colorFilter.value = new Color(1f, 0.985f, 0.96f);

            if (!profile.TryGet<Vignette>(out var vignette))
                vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = _activePainting == 0 ? 0.10f : 0.18f;
            vignette.smoothness.value = 0.45f;
            vignette.rounded.value = true;
            vignette.color.value = new Color(0.13f, 0.15f, 0.17f);

            var volumeGo = new GameObject("Post-Process Volume");
            volumeGo.transform.SetParent(root, false);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static VolumeProfile GetOrCreateProfile()
        {
            string path = MaterialsFolder + (_activePainting == 0
                ? "/T026_MistValleyProfile.asset"
                : "/PaintingPrototypeProfile.asset");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null)
                return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        /// <summary>
        /// Contract requirement: the freshly built scene must contain both
        /// cameras, "Solved Scenery" with exactly the eight required piece
        /// roots, and the "Reference Frame", or the build fails loudly.
        /// </summary>
        private static void ValidateScene()
        {
            var missing = new List<string>();
            if (GameObject.Find("Build Camera") == null
                || GameObject.Find("Build Camera").GetComponent<Camera>() == null)
                missing.Add("Build Camera");
            if (GameObject.Find("Composition Camera") == null
                || GameObject.Find("Composition Camera").GetComponent<Camera>() == null)
                missing.Add("Composition Camera");

            var scenery = GameObject.Find("Solved Scenery");
            if (scenery == null)
            {
                missing.Add("Solved Scenery");
            }
            else
            {
                if (scenery.transform.childCount != RequiredPieces.Length)
                    missing.Add("Solved Scenery must contain exactly " + RequiredPieces.Length + " piece roots");
                foreach (var pieceName in RequiredPieces)
                {
                    if (scenery.transform.Find(pieceName) == null)
                        missing.Add("Solved Scenery/" + pieceName);
                }
            }

            if (GameObject.Find("Reference Frame") == null)
                missing.Add("Reference Frame");

            ValidateCompositionEvaluation(missing);
            ValidateManipulation(missing);
            ValidateGuidance(missing);

            if (missing.Count > 0)
                throw new InvalidOperationException(
                    "PaintingPrototype validation failed: " + string.Join(", ", missing) + ".");

            Debug.Log("PaintingPrototype validation passed: both cameras, Solved Scenery with all "
                + RequiredPieces.Length + " piece roots, Reference Frame, evaluator and guidance rail wiring present.");
        }

        private static void ValidateGuidance(List<string> missing)
        {
            var canvas = GameObject.Find("Guidance Canvas");
            var presenter = canvas != null ? canvas.GetComponent<PaintingGuidancePresenter>() : null;
            if (canvas == null || presenter == null)
            {
                missing.Add("Guidance Canvas with PaintingGuidancePresenter");
                return;
            }
            var serialized = new SerializedObject(presenter);
            if (serialized.FindProperty("_evaluator").objectReferenceValue == null)
                missing.Add("Guidance presenter evaluator reference");
            if (serialized.FindProperty("_compositionCamera").objectReferenceValue == null)
                missing.Add("Guidance presenter Composition Camera reference");
            if (serialized.FindProperty("_targetImage").objectReferenceValue == null
                || serialized.FindProperty("_liveImage").objectReferenceValue == null)
                missing.Add("Guidance presenter target/live image references");
            if (serialized.FindProperty("_pieceNames").arraySize != RequiredPieces.Length)
                missing.Add("Guidance presenter ordered piece names");
            var reveal = canvas.GetComponent<PaintingCompletionReveal>();
            if (reveal == null)
            {
                missing.Add("Guidance Canvas PaintingCompletionReveal");
                return;
            }
            var revealSerialized = new SerializedObject(reveal);
            if (revealSerialized.FindProperty("_manipulation").objectReferenceValue == null
                || revealSerialized.FindProperty("_buildCamera").objectReferenceValue == null
                || revealSerialized.FindProperty("_compositionCamera").objectReferenceValue == null)
                missing.Add("Painting completion reveal camera/manipulation wiring");
            if (revealSerialized.FindProperty("_guidanceGroup").objectReferenceValue == null
                || revealSerialized.FindProperty("_revealGroup").objectReferenceValue == null
                || revealSerialized.FindProperty("_revealText").objectReferenceValue == null)
                missing.Add("Painting completion reveal UI wiring");
        }

        /// <summary>
        /// T-009B2 contract: the evaluator object with its component, the
        /// readable target Object-ID texture, the eight ordered piece IDs
        /// (exactly one per root, in the required order with the required
        /// colors) and the serialized evaluator wiring must all be correct, or
        /// the build fails loudly.
        /// </summary>
        private static void ValidateCompositionEvaluation(List<string> missing)
        {
            var evaluation = GameObject.Find("Composition Evaluator");
            var evaluator = evaluation != null ? evaluation.GetComponent<PaintingCompositionEvaluator>() : null;
            if (evaluation == null || evaluator == null)
            {
                missing.Add("Composition Evaluator (with PaintingCompositionEvaluator)");
                return;
            }

            var target = AssetDatabase.LoadAssetAtPath<Texture2D>(TargetObjectIdPath);
            if (target == null)
            {
                missing.Add("target Object-ID texture " + TargetObjectIdPath);
            }
            else if (!target.isReadable)
            {
                missing.Add("target Object-ID texture must be readable (Read/Write enabled); recapture the references with PaintingPrototypeCapture");
            }

            var scenery = GameObject.Find("Solved Scenery");
            if (scenery == null)
            {
                missing.Add("Solved Scenery");
                return;
            }

            for (int i = 0; i < RequiredPieces.Length; i++)
            {
                var pieceRoot = scenery.transform.Find(RequiredPieces[i]);
                if (pieceRoot == null)
                {
                    missing.Add("Solved Scenery/" + RequiredPieces[i]);
                    continue;
                }
                var pieceIds = pieceRoot.GetComponents<PaintingPieceId>();
                if (pieceIds.Length != 1)
                {
                    missing.Add("Solved Scenery/" + RequiredPieces[i] + " must carry exactly one PaintingPieceId");
                }
                else if ((int)pieceIds[0].Id != PieceIds[i])
                {
                    missing.Add("Solved Scenery/" + RequiredPieces[i] + " PaintingPieceId must be 0x"
                        + PieceIds[i].ToString("X6") + ", got 0x" + ((int)pieceIds[0].Id).ToString("X6"));
                }
            }

            var serialized = new SerializedObject(evaluator);
            var cameraReference = serialized.FindProperty("_compositionCamera").objectReferenceValue as Camera;
            if (cameraReference == null || cameraReference.name != "Composition Camera")
                missing.Add("Composition Evaluator must reference the Composition Camera");
            if (serialized.FindProperty("_targetTexture").objectReferenceValue == null)
                missing.Add("Composition Evaluator must reference the target Object-ID texture");
            if (serialized.FindProperty("_idShader").objectReferenceValue == null)
                missing.Add("Composition Evaluator must explicitly reference PaintingObjectId shader for player builds");
            var piecesProperty = serialized.FindProperty("_pieces");
            if (piecesProperty.arraySize != RequiredPieces.Length)
            {
                missing.Add("Composition Evaluator must reference exactly " + RequiredPieces.Length
                    + " ordered PaintingPieceId components, got " + piecesProperty.arraySize);
            }
            else
            {
                for (int i = 0; i < RequiredPieces.Length; i++)
                {
                    var reference = piecesProperty.GetArrayElementAtIndex(i).objectReferenceValue as PaintingPieceId;
                    var expected = scenery.transform.Find(RequiredPieces[i]).GetComponent<PaintingPieceId>();
                    if (reference == null || reference != expected)
                        missing.Add("Composition Evaluator piece " + i + " must be Solved Scenery/" + RequiredPieces[i] + " PaintingPieceId");
                }
                int width = serialized.FindProperty("_width").intValue;
                int height = serialized.FindProperty("_height").intValue;
                float frequency = serialized.FindProperty("_frequencyHz").floatValue;
                if (width != 256 || height != 144)
                    missing.Add("Composition Evaluator must render 256x144, got " + width + "x" + height);
                if (Mathf.Abs(frequency - 6f) > 0.001f)
                    missing.Add("Composition Evaluator must sample at 6 Hz, got " + frequency);
            }
        }

        /// <summary>
        /// T-010B/T-010C contract: exactly eight PaintingManipulablePiece
        /// handles and eight enabled selection colliders, one per piece root
        /// in RequiredPieces order, each with the authored transform captured
        /// from the solved pose, every root on the PaintingPiece layer with
        /// no Rigidbody, and the scene saved with the deterministic unsolved
        /// start layout on top of the authored poses — or the build fails
        /// loudly.
        /// </summary>
        private static void ValidateManipulation(List<string> missing)
        {
            var scenery = GameObject.Find("Solved Scenery");
            if (scenery == null)
            {
                missing.Add("Solved Scenery");
                return;
            }

            int layer = GetPaintingPieceLayerIndex();
            if (layer < 0)
                missing.Add("project layer 'PaintingPiece'");

            for (int i = 0; i < RequiredPieces.Length; i++)
            {
                var pieceRoot = scenery.transform.Find(RequiredPieces[i]);
                if (pieceRoot == null)
                {
                    missing.Add("Solved Scenery/" + RequiredPieces[i]);
                    continue;
                }

                var handles = pieceRoot.GetComponents<PaintingManipulablePiece>();
                if (handles.Length != 1)
                {
                    missing.Add("Solved Scenery/" + RequiredPieces[i]
                        + " must carry exactly one PaintingManipulablePiece, got " + handles.Length);
                }
                else
                {
                    if (!handles[0].IsConfigured)
                    {
                        missing.Add("Solved Scenery/" + RequiredPieces[i] + " PaintingManipulablePiece must be configured");
                    }
                    else
                    {
                        ValidateAuthoredStartPose(missing, pieceRoot, handles[0], i);
                    }
                    int expectedLayer = IsActivePiece(i) ? layer : 0;
                    if (layer >= 0 && pieceRoot.gameObject.layer != expectedLayer)
                        missing.Add("Solved Scenery/" + RequiredPieces[i] + " must use its authored interaction layer");
                    if (pieceRoot.GetComponent<Rigidbody>() != null)
                        missing.Add("Solved Scenery/" + RequiredPieces[i] + " must have no Rigidbody");
                }

                var colliders = pieceRoot.GetComponents<Collider>();
                if (colliders.Length != 1)
                {
                    missing.Add("Solved Scenery/" + RequiredPieces[i]
                        + " must carry exactly one selection collider, got " + colliders.Length);
                }
                else
                {
                    if (!(colliders[0] is BoxCollider))
                        missing.Add("Solved Scenery/" + RequiredPieces[i] + " selection collider must be a BoxCollider");
                    if (colliders[0].enabled != IsActivePiece(i))
                        missing.Add("Solved Scenery/" + RequiredPieces[i] + " collider eligibility must match the level rules");
                    if (colliders[0] is BoxCollider boxCollider
                        && !ColliderCoversRenderers(pieceRoot, boxCollider))
                        missing.Add("Solved Scenery/" + RequiredPieces[i]
                            + " selection collider must cover the piece renderers");
                }
            }

            ValidateManipulationController(missing, layer);
        }

        private static bool ColliderCoversRenderers(Transform pieceRoot, BoxCollider collider)
        {
            Bounds localCollider = new Bounds(collider.center, collider.size + Vector3.one * 0.0001f);
            foreach (Renderer renderer in pieceRoot.GetComponentsInChildren<Renderer>(true))
            {
                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;
                Bounds meshBounds = meshFilter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 rendererLocal = WorldCorner(meshBounds, corner);
                    Vector3 local = pieceRoot.InverseTransformPoint(
                        renderer.transform.TransformPoint(rendererLocal));
                    if (!localCollider.Contains(local))
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// T-010C contract: the authored transform captured at build time is
        /// the solved pose and is never re-captured, and the scene must save
        /// exactly the deterministic unsolved start layout on top of it:
        /// every root at its authored position plus the authored offset with
        /// the authored yaw offset, the local scale unchanged, the start
        /// collider inside the movement bounds, and the start pose within
        /// +/-<see cref="MaxDepthOffset"/> Composition Camera depth of its
        /// authored pose — or the build fails loudly.
        /// </summary>
        private static void ValidateAuthoredStartPose(
            List<string> missing, Transform pieceRoot, PaintingManipulablePiece handle, int pieceIndex)
        {
            string label = "Solved Scenery/" + pieceRoot.name;

            if (Vector3.Distance(handle.AuthoredLocalScale, pieceRoot.localScale) > 0.0001f)
                missing.Add(label + " start layout must never change the authored local scale");

            Vector3 expectedStart = handle.AuthoredPosition + (IsActivePiece(pieceIndex) ? StartOffsetFor(pieceIndex) : Vector3.zero);
            if (Vector3.Distance(pieceRoot.position, expectedStart) > 0.001f)
                missing.Add(label + " must start at its authored unsolved position");
            float expectedYaw = IsActivePiece(pieceIndex) ? UnsolvedStartYawOffsets[pieceIndex] : 0f;
            if (Mathf.Abs(handle.AuthoredSignedYawOffset(pieceRoot.rotation) - expectedYaw) > 0.01f)
                missing.Add(label + " must start at yaw " + expectedYaw + " degrees from its authored rotation");

            // Locked scenery is already part of the solved painting and may
            // deliberately extend beyond the interactive canvas; only pieces
            // the player can move need the reachability constraints below.
            if (!IsActivePiece(pieceIndex))
                return;

            var startCollider = pieceRoot.GetComponent<Collider>();
            if (startCollider != null
                && (!ManipulationMovementBounds.Contains(startCollider.bounds.min)
                    || !ManipulationMovementBounds.Contains(startCollider.bounds.max)))
                missing.Add(label + " start pose must stay inside the manipulation movement bounds");

            var compositionCameraGo = GameObject.Find("Composition Camera");
            var compositionCamera = compositionCameraGo != null ? compositionCameraGo.GetComponent<Camera>() : null;
            if (compositionCamera == null)
            {
                missing.Add("Composition Camera");
                return;
            }
            Vector3 viewport = compositionCamera.WorldToViewportPoint(pieceRoot.position);
            if (viewport.x < CompositionViewportBounds.xMin - 0.001f
                || viewport.x > CompositionViewportBounds.xMax + 0.001f
                || viewport.y < CompositionViewportBounds.yMin - 0.001f
                || viewport.y > CompositionViewportBounds.yMax + 0.001f)
                missing.Add(label + " start pose must stay inside the shared composition viewport");
            if (viewport.z < CompositionDepthRange.x - 0.001f
                || viewport.z > CompositionDepthRange.y + 0.001f)
                missing.Add(label + " start pose must stay inside the shared composition depth range");
        }

        /// <summary>
        /// T-010A contract: the single "Manipulation Controller" child of
        /// "Painting Prototype" with its serialized references to the Build
        /// Camera, the Composition Camera, the Arch Bridge piece, exactly the
        /// PaintingPiece layer mask, movement bounds containing the bridge
        /// (authored and unsolved start pose), and positive
        /// depth/sensitivity/window values — or the build fails loudly.
        /// </summary>
        private static void ValidateManipulationController(List<string> missing, int layer)
        {
            var prototype = GameObject.Find("Painting Prototype");
            var controllerGo = prototype != null ? prototype.transform.Find("Manipulation Controller") : null;
            var controller = controllerGo != null ? controllerGo.GetComponent<PaintingManipulationController>() : null;
            if (controllerGo == null || controller == null)
            {
                missing.Add("Manipulation Controller (with PaintingManipulationController)");
                return;
            }

            var serialized = new SerializedObject(controller);
            var buildCamera = serialized.FindProperty("_buildCamera").objectReferenceValue as Camera;
            if (buildCamera == null || buildCamera.name != "Build Camera")
                missing.Add("Manipulation Controller must reference the Build Camera");
            var compositionCamera = serialized.FindProperty("_compositionCamera").objectReferenceValue as Camera;
            if (compositionCamera == null || compositionCamera.name != "Composition Camera")
                missing.Add("Manipulation Controller must reference the Composition Camera");

            var scenery = GameObject.Find("Solved Scenery");
            var expectedBridge = scenery != null && scenery.transform.Find("Arch Bridge") != null
                ? scenery.transform.Find("Arch Bridge").GetComponent<PaintingManipulablePiece>()
                : null;
            var bridge = serialized.FindProperty("_bridge").objectReferenceValue as PaintingManipulablePiece;
            if (bridge == null || bridge != expectedBridge)
                missing.Add("Manipulation Controller must reference Solved Scenery/Arch Bridge PaintingManipulablePiece");

            int mask = serialized.FindProperty("_selectionMask").intValue;
            if (layer < 0 || mask != (1 << layer))
                missing.Add("Manipulation Controller mask must be exactly the 'PaintingPiece' layer");
            if (layer >= 0 && bridge != null && (mask & (1 << bridge.gameObject.layer)) == 0)
                missing.Add("Manipulation Controller mask must include the Arch Bridge layer");

            var bounds = serialized.FindProperty("_movementBounds").boundsValue;
            if (expectedBridge != null)
            {
                var colliders = expectedBridge.GetComponents<Collider>();
                if (colliders.Length == 1
                    && (!bounds.Contains(colliders[0].bounds.min) || !bounds.Contains(colliders[0].bounds.max)))
                    missing.Add("Manipulation Controller bounds must contain the Arch Bridge start pose");
            }

            if (serialized.FindProperty("_compositionViewportBounds").rectValue != CompositionViewportBounds)
                missing.Add("Manipulation Controller must use the shared composition viewport bounds");
            if (serialized.FindProperty("_compositionDepthRange").vector2Value != CompositionDepthRange)
                missing.Add("Manipulation Controller must use the shared composition depth range");
            if (serialized.FindProperty("_wheelSensitivity").floatValue <= 0f)
                missing.Add("Manipulation Controller wheel sensitivity must be positive");
            if (serialized.FindProperty("_wheelBurstWindowSeconds").floatValue <= 0f)
                missing.Add("Manipulation Controller wheel burst window must be positive");
            if (serialized.FindProperty("_placementRectangle").rectValue != PlacementRectangle)
                missing.Add("Manipulation Controller must use the shared lake placement rectangle");
            if (!Mathf.Approximately(serialized.FindProperty("_surfaceY").floatValue, WaterTopY))
                missing.Add("Manipulation Controller placement surface must match the lake top");
            if (serialized.FindProperty("_validPreviewMaterial").objectReferenceValue == null
                || serialized.FindProperty("_invalidPreviewMaterial").objectReferenceValue == null)
                missing.Add("Manipulation Controller must reference both placement preview materials");
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

        private static Mesh GetOrCreateHeroMountainMesh(string assetName, float width, float depth,
            float height, int radialSegments, int seed)
        {
            string path = MeshesFolder + "/" + assetName + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;
            mesh = CeladonHeroMeshFactory.CreateSculptedMountain(width, depth, height, radialSegments, seed);
            mesh.name = assetName;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Mesh GetOrCreateHeroRoofMesh(string assetName, float width, float depth,
            float rise, float thickness, int segments)
        {
            string path = MeshesFolder + "/" + assetName + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;
            mesh = CeladonHeroMeshFactory.CreateUpturnedPagodaRoof(width, depth, rise, thickness, segments);
            mesh.name = assetName;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Mesh GetOrCreateCurvedBranchMesh(string assetName, Vector3[] path,
            float startRadius, float endRadius, int radialSegments)
        {
            string assetPath = MeshesFolder + "/" + assetName + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh != null) return mesh;
            mesh = CeladonHeroMeshFactory.CreateCurvedBranch(path, startRadius, endRadius, radialSegments);
            mesh.name = assetName;
            AssetDatabase.CreateAsset(mesh, assetPath);
            return mesh;
        }

        private static Mesh GetOrCreateCloudCanopyMesh(string assetName, float width,
            float depth, float height, int lobes)
        {
            string path = MeshesFolder + "/" + assetName + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = CeladonHeroMeshFactory.CreateCloudCanopy(width, depth, height, lobes);
            mesh.name = assetName;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Mesh GetOrCreateConeMesh(string assetName, float baseRadius, float topRadius,
            float height, int segments)
        {
            string path = MeshesFolder + "/" + assetName + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;
            mesh = CreateConeMesh(baseRadius, topRadius, height, segments);
            mesh.name = assetName;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Mesh GetOrCreateArchMesh(string assetName, float span, float width, float apex,
            float thickness, int segments)
        {
            string path = MeshesFolder + "/" + assetName + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;
            mesh = CreateArchBridgeMesh(span, width, apex, thickness, segments);
            mesh.name = assetName;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Mesh GetOrCreateSilhouetteMesh(string assetName, Vector2[] outline, float depth)
        {
            string path = MeshesFolder + "/" + assetName + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;
            mesh = CreateExtrudedSilhouetteMesh(outline, depth);
            mesh.name = assetName;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>
        /// Material helper. Existing materials are reused without re-shading so
        /// the other prototype scenes are never altered; new materials are
        /// created with URP Lit.
        /// </summary>
        private static Material GetOrCreateMaterial(string assetName, Color color, float smoothness, float metallic)
        {
            string path = MaterialsFolder + "/" + assetName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(LitShaderName);
            if (shader == null)
                throw new InvalidOperationException("Required URP shader was not found: " + LitShaderName);

            if (material == null)
            {
                material = new Material(shader) { name = assetName };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                throw new InvalidOperationException(
                    "PaintingPrototype would alter existing material " + assetName + "; it uses "
                    + material.shader.name + " instead of " + LitShaderName + ".");
            }

            material.color = color;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateCeladonMaterial(string assetName, Color baseColor,
            Color shadowColor, Color highlightColor, float smoothness, float rimStrength)
        {
            string path = MaterialsFolder + "/" + assetName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("PerspectivePuzzle/CeladonLit");
            if (shader == null)
                throw new InvalidOperationException("Required celadon shader was not found.");
            if (material == null)
            {
                material = new Material(shader) { name = assetName };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_ShadowColor", shadowColor);
            material.SetColor("_HighlightColor", highlightColor);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_RimStrength", rimStrength);
            material.SetFloat("_TopLight", 0.10f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Opaque URP material carrying the captured target beauty on the physical reference frame.</summary>
        private static Material GetOrCreateReferencePaintingMaterial()
        {
            const string assetName = "Mat_ReferencePainting";
            string path = MaterialsFolder + "/" + assetName + ".mat";
            Texture2D target = AssetDatabase.LoadAssetAtPath<Texture2D>(TargetBeautyPath);
            if (target == null)
                throw new InvalidOperationException("Reference painting texture is missing at " + TargetBeautyPath + ".");
            Shader shader = Shader.Find(LitShaderName);
            if (shader == null)
                throw new InvalidOperationException("Required URP shader was not found: " + LitShaderName);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = assetName };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                throw new InvalidOperationException(assetName + " must use " + LitShaderName + ".");
            }
            material.SetTexture("_BaseMap", target);
            material.mainTexture = target;
            material.SetColor("_BaseColor", Color.white);
            material.color = Color.white;
            material.SetFloat("_Smoothness", 0.18f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Transparent unlit landing ghost used by the placement transaction.</summary>
        private static Material GetOrCreatePreviewMaterial(string assetName, Color color)
        {
            string path = MaterialsFolder + "/" + assetName + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                throw new InvalidOperationException("Required URP Unlit shader was not found.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = assetName };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                throw new InvalidOperationException(
                    "PaintingPrototype would alter existing material " + assetName + "; it uses "
                    + material.shader.name + " instead of the URP Unlit shader.");
            }

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetColor("_BaseColor", color);
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Soft translucent white mist (URP Lit alpha blend).</summary>
        private static Material GetOrCreateMistMaterial()
        {
            const string path = MaterialsFolder + "/Mat_MistWhite.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(LitShaderName);
            if (shader == null)
                throw new InvalidOperationException("Required URP shader was not found: " + LitShaderName);

            if (material == null)
            {
                material = new Material(shader) { name = "Mat_MistWhite" };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                throw new InvalidOperationException(
                    "PaintingPrototype would alter existing material Mat_MistWhite; it uses "
                    + material.shader.name + " instead of " + LitShaderName + ".");
            }

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Smoothness", 0.1f);
            material.SetFloat("_Metallic", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetColor("_BaseColor", MistWhite);
            material.color = MistWhite;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateMeshRenderer(Transform parent, string name, Vector3 localPosition,
            Mesh mesh, Material material, Quaternion localRotation)
        {
            return CreateMeshRenderer(parent, name, localPosition, mesh, material, localRotation, Vector3.one);
        }

        private static GameObject CreateMeshRenderer(Transform parent, string name, Vector3 localPosition,
            Mesh mesh, Material material, Quaternion localRotation, Vector3 localScale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return go;
        }

        /// <summary>
        /// Extrudes a clockwise XY outline into a shallow relief. The broad
        /// painted face points toward +Z (the Composition Camera), while the
        /// thickness remains obvious from the Build Camera.
        /// </summary>
        private static Mesh CreateExtrudedSilhouetteMesh(Vector2[] outline, float depth)
        {
            if (outline == null || outline.Length < 3)
                throw new ArgumentException("A silhouette needs at least three outline points.", nameof(outline));
            if (depth <= 0f)
                throw new ArgumentOutOfRangeException(nameof(depth));

            int count = outline.Length;
            var vertices = new List<Vector3>(count * 4 + 2);
            var triangles = new List<int>(count * 12);
            Vector2 center = Vector2.zero;
            for (int i = 0; i < count; i++)
                center += outline[i];
            center /= count;

            int frontCenter = vertices.Count;
            vertices.Add(new Vector3(center.x, center.y, depth * 0.5f));
            int frontStart = vertices.Count;
            for (int i = 0; i < count; i++)
                vertices.Add(new Vector3(outline[i].x, outline[i].y, depth * 0.5f));
            int backCenter = vertices.Count;
            vertices.Add(new Vector3(center.x, center.y, -depth * 0.5f));
            int backStart = vertices.Count;
            for (int i = 0; i < count; i++)
                vertices.Add(new Vector3(outline[i].x, outline[i].y, -depth * 0.5f));

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                // Input is clockwise, so reverse the front fan for +Z.
                triangles.Add(frontCenter); triangles.Add(frontStart + next); triangles.Add(frontStart + i);
                triangles.Add(backCenter); triangles.Add(backStart + i); triangles.Add(backStart + next);

                int side = vertices.Count;
                vertices.Add(vertices[frontStart + i]);
                vertices.Add(vertices[frontStart + next]);
                vertices.Add(vertices[backStart + next]);
                vertices.Add(vertices[backStart + i]);
                triangles.Add(side); triangles.Add(side + 1); triangles.Add(side + 2);
                triangles.Add(side); triangles.Add(side + 2); triangles.Add(side + 3);
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Truncated cone mesh (also used for discs by passing equal radii),
        /// with both caps and flat per-face normals: the stylized toy mountain
        /// and sun shape.
        /// </summary>
        private static Mesh CreateConeMesh(float baseRadius, float topRadius, float height, int segments)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            for (int i = 0; i < segments; i++)
            {
                float a0 = (float)i / segments * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / segments * Mathf.PI * 2f;
                var b0 = new Vector3(Mathf.Cos(a0) * baseRadius, 0f, Mathf.Sin(a0) * baseRadius);
                var b1 = new Vector3(Mathf.Cos(a1) * baseRadius, 0f, Mathf.Sin(a1) * baseRadius);
                var t0 = new Vector3(Mathf.Cos(a0) * topRadius, height, Mathf.Sin(a0) * topRadius);
                var t1 = new Vector3(Mathf.Cos(a1) * topRadius, height, Mathf.Sin(a1) * topRadius);
                var radial = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));

                AddQuad(vertices, normals, triangles, b0, b1, t1, t0, radial);
                AddTri(vertices, normals, triangles, Vector3.zero, b0, b1, Vector3.down);
                AddTri(vertices, normals, triangles, new Vector3(0f, height, 0f), t1, t0, Vector3.up);
            }

            return BuildMeshFromParts(vertices, normals, triangles, "TruncatedCone");
        }

        /// <summary>
        /// Arch bridge mesh: the deck top and underside follow a parabola from
        /// the two water-contact ends to a central apex, with a constant
        /// thickness, plus front/back faces and flat end caps.
        /// </summary>
        private static Mesh CreateArchBridgeMesh(float span, float width, float apex, float thickness, int segments)
        {
            var stations = new BridgeStation[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float x = -span * 0.5f + span * t;
                float parabola = (2f * t - 1f) * (2f * t - 1f);
                float bottom = apex * (1f - parabola);
                float top = bottom + thickness;
                stations[i] = new BridgeStation(
                    new Vector3(x, bottom, width * 0.5f),
                    new Vector3(x, bottom, -width * 0.5f),
                    new Vector3(x, top, width * 0.5f),
                    new Vector3(x, top, -width * 0.5f));
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            for (int i = 0; i < segments; i++)
            {
                var s0 = stations[i];
                var s1 = stations[i + 1];
                AddQuad(vertices, normals, triangles, s0.TopFront, s1.TopFront, s1.TopBack, s0.TopBack, Vector3.up);
                AddQuad(vertices, normals, triangles, s0.BottomBack, s1.BottomBack, s1.BottomFront, s0.BottomFront, Vector3.down);
                AddQuad(vertices, normals, triangles, s0.BottomFront, s1.BottomFront, s1.TopFront, s0.TopFront, Vector3.forward);
                AddQuad(vertices, normals, triangles, s0.TopBack, s1.TopBack, s1.BottomBack, s0.BottomBack, Vector3.back);
            }

            var left = stations[0];
            var right = stations[segments];
            AddQuad(vertices, normals, triangles, left.BottomBack, left.BottomFront, left.TopFront, left.TopBack, Vector3.left);
            AddQuad(vertices, normals, triangles, right.BottomFront, right.BottomBack, right.TopBack, right.TopFront, Vector3.right);

            return BuildMeshFromParts(vertices, normals, triangles, "ArchBridge");
        }

        private readonly struct BridgeStation
        {
            public readonly Vector3 BottomFront;
            public readonly Vector3 BottomBack;
            public readonly Vector3 TopFront;
            public readonly Vector3 TopBack;

            public BridgeStation(Vector3 bottomFront, Vector3 bottomBack, Vector3 topFront, Vector3 topBack)
            {
                BottomFront = bottomFront;
                BottomBack = bottomBack;
                TopFront = topFront;
                TopBack = topBack;
            }
        }

        private static Mesh BuildMeshFromParts(List<Vector3> vertices, List<Vector3> normals,
            List<int> triangles, string meshName)
        {
            var mesh = new Mesh();
            mesh.name = meshName;
            if (vertices.Count > ushort.MaxValue)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTri(List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 desiredNormal)
        {
            int baseIndex = vertices.Count;
            vertices.Add(p0);
            vertices.Add(p1);
            vertices.Add(p2);
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
            if (Vector3.Dot(n, desiredNormal) < 0f)
            {
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex);
                n = -n;
            }
            else
            {
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
            }
            n.Normalize();
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);
        }

        private static void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3, Vector3 desiredNormal)
        {
            // Single flat normal per quad, flipped toward the desired side;
            // the two triangles share it so curved faces stay faceted cleanly.
            Vector3 n = Vector3.Cross(c2 - c0, c3 - c1);
            if (Vector3.Dot(n, desiredNormal) < 0f)
                n = -n;
            n.Normalize();

            int baseIndex = vertices.Count;
            vertices.Add(c0);
            vertices.Add(c1);
            vertices.Add(c2);
            vertices.Add(c3);
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);

            if (Vector3.Dot(Vector3.Cross(c1 - c0, c2 - c0), n) < 0f)
            {
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 2);
            }
            else
            {
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
            }

            if (Vector3.Dot(Vector3.Cross(c2 - c0, c3 - c0), n) < 0f)
            {
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 3);
            }
            else
            {
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 3);
            }
        }
    }
}
#endif
