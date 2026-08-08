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
        private const string ScenePath = "Assets/Scenes/PaintingPrototype.unity";
        private const string MaterialsFolder = "Assets/Art/Materials";
        private const string MeshesFolder = "Assets/Art/Meshes";
        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private const string PaintingPieceLayerName = "PaintingPiece";

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
        private static readonly Color WarmIvory = new Color(0.949f, 0.933f, 0.902f);
        private static readonly Color SkyIvory = new Color(0.965f, 0.948f, 0.912f);
        private static readonly Color LightStone = new Color(0.871f, 0.843f, 0.792f);
        private static readonly Color FrameBlueGray = new Color(0.243f, 0.290f, 0.322f);
        private static readonly Color FrameSurface = new Color(0.961f, 0.949f, 0.925f);
        private static readonly Color CeladonWater = new Color(0.365f, 0.529f, 0.502f);
        private static readonly Color CeladonPale = new Color(0.710f, 0.780f, 0.690f);
        private static readonly Color CeladonJade = new Color(0.545f, 0.690f, 0.585f);
        private static readonly Color MossGreen = new Color(0.216f, 0.380f, 0.243f);
        private static readonly Color Porcelain = new Color(0.973f, 0.965f, 0.941f);
        private static readonly Color SunHalo = new Color(0.933f, 0.718f, 0.380f);
        private static readonly Color SunCore = new Color(0.980f, 0.624f, 0.263f);
        private static readonly Color CeladonStone = new Color(0.596f, 0.655f, 0.600f);
        private static readonly Color BridgeStone = new Color(0.730f, 0.710f, 0.665f);
        private static readonly Color MistWhite = new Color(0.980f, 0.972f, 0.945f, 0.32f);

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
        private const string TargetObjectIdPath = "Assets/Content/PaintingPrototype/References/MistValleyBridge_ObjectId.png";
        private const string TargetBeautyPath = "Assets/Content/PaintingPrototype/References/MistValleyBridge_Beauty.png";

        [MenuItem("Tools/PerspectivePuzzle/Build Painting Prototype Scene")]
        public static void BuildScene()
        {
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
            BuildLights(root);
            BuildCameras(root);
            BuildPostProcessing(root);
            BuildEvaluation(root);
            BuildManipulation(root);
            BuildGuidance(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureSceneInBuildSettings();

            ValidateScene();
            Debug.Log("Painting Prototype scene built at " + ScenePath);
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
            var floorMat = GetOrCreateMaterial("Mat_WarmIvory", WarmIvory, 0.25f, 0f);
            var plinthMat = GetOrCreateMaterial("Mat_LightStone", LightStone, 0.30f, 0f);
            var frameBarMat = GetOrCreateMaterial("Mat_FrameBlueGray", FrameBlueGray, 0.35f, 0f);
            var frameSurfaceMat = GetOrCreateReferencePaintingMaterial();

            var exhibition = new GameObject("Exhibition").transform;
            exhibition.SetParent(root, false);

            var floorMesh = GetOrCreateMesh("RoundedFloorCompact", new Vector3(19f, 0.5f, 14f), 0.12f, 6);
            CreateMeshRenderer(exhibition, "Floor", new Vector3(0f, -0.25f, 0f), floorMesh, floorMat, Quaternion.identity);

            var plinthMesh = GetOrCreateMesh("PaintingPlinth", new Vector3(8.6f, 0.6f, 5.8f), 0.16f, 10);
            CreateMeshRenderer(exhibition, "Plinth", new Vector3(0f, 0.3f, 0.1f), plinthMesh, plinthMat, Quaternion.identity);

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

            var sky = new GameObject("Sky").transform;
            sky.SetParent(root, false);

            var panelMesh = GetOrCreateMesh("SkyPanel_Cycle2Wide", new Vector3(11.5f, 5.0f, 0.12f), 0.05f, 8);
            var backdrop = CreateMeshRenderer(sky, "Backdrop", new Vector3(0f, 3.1f, -3.15f), panelMesh, skyMat, Quaternion.identity);
            var backdropRenderer = backdrop.GetComponent<MeshRenderer>();
            backdropRenderer.receiveShadows = false;
            backdropRenderer.shadowCastingMode = ShadowCastingMode.Off;
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
            var piecesProperty = serialized.FindProperty("_pieces");
            piecesProperty.arraySize = pieces.Length;
            for (int i = 0; i < pieces.Length; i++)
                piecesProperty.GetArrayElementAtIndex(i).objectReferenceValue = pieces[i];
            serialized.FindProperty("_width").intValue = 256;
            serialized.FindProperty("_height").intValue = 144;
            serialized.FindProperty("_frequencyHz").floatValue = 6f;
            serialized.ApplyModifiedProperties();
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

            var pieces = new PaintingManipulablePiece[RequiredPieces.Length];
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
                pieceRoot.gameObject.layer = layer;
                pieces[i] = piece;
            }

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
            serialized.FindProperty("_placementRectangle").rectValue = PlacementRectangle;
            serialized.FindProperty("_surfaceY").floatValue = WaterTopY;
            serialized.FindProperty("_liftHeight").floatValue = PlacementLiftHeight;
            serialized.FindProperty("_followSmoothTime").floatValue = PlacementFollowSmoothTime;
            serialized.FindProperty("_settleDuration").floatValue = PlacementSettleDuration;
            serialized.FindProperty("_validPreviewMaterial").objectReferenceValue = GetOrCreatePreviewMaterial(
                "Mat_PlacementValid", new Color(0.30f, 0.92f, 0.76f, 0.42f));
            serialized.FindProperty("_invalidPreviewMaterial").objectReferenceValue = GetOrCreatePreviewMaterial(
                "Mat_PlacementInvalid", new Color(1.00f, 0.33f, 0.27f, 0.48f));
            serialized.ApplyModifiedProperties();

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
            scaler.matchWidthOrHeight = 1f;

            Image panel = CreateUiImage(canvasGo.transform, "Curator Rail",
                new Color(0.965f, 0.948f, 0.912f, 0.94f));
            SetRect(panel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-370f, 0f), Vector2.zero);

            Text title = CreateUiText(panel.transform, "Title", "RECONSTRUCT THE PAINTING", 18,
                FontStyle.Bold, new Color(0.19f, 0.23f, 0.24f), TextAnchor.MiddleLeft);
            SetTopRect(title.rectTransform, 24f, 322f, 30f);

            Text targetLabel = CreateUiText(panel.transform, "Target Label", "TARGET", 12,
                FontStyle.Bold, new Color(0.35f, 0.42f, 0.41f), TextAnchor.MiddleLeft);
            SetTopRect(targetLabel.rectTransform, 66f, 322f, 22f);
            RawImage targetImage = CreateUiRawImage(panel.transform, "Target Painting", targetBeauty);
            SetTopRect(targetImage.rectTransform, 91f, 322f, 181f);

            Text liveLabel = CreateUiText(panel.transform, "Live Label", "YOUR VIEW", 12,
                FontStyle.Bold, new Color(0.35f, 0.42f, 0.41f), TextAnchor.MiddleLeft);
            SetTopRect(liveLabel.rectTransform, 290f, 322f, 22f);
            RawImage liveImage = CreateUiRawImage(panel.transform, "Live Composition", null);
            SetTopRect(liveImage.rectTransform, 315f, 322f, 181f);

            Text status = CreateUiText(panel.transform, "Status", "Arrange the scene", 20,
                FontStyle.Normal, new Color(0.19f, 0.23f, 0.24f), TextAnchor.MiddleLeft);
            SetTopRect(status.rectTransform, 520f, 322f, 30f);

            Image track = CreateUiImage(panel.transform, "Progress Track", new Color(0.76f, 0.78f, 0.73f, 0.65f));
            SetTopRect(track.rectTransform, 562f, 322f, 7f);
            Image fill = CreateUiImage(track.transform, "Progress Fill", new Color(0.36f, 0.66f, 0.57f, 1f));
            SetRect(fill.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);

            Text focus = CreateUiText(panel.transform, "Focus", "Match the target painting", 15,
                FontStyle.Italic, new Color(0.42f, 0.46f, 0.43f), TextAnchor.MiddleLeft);
            SetTopRect(focus.rectTransform, 582f, 322f, 32f);

            var presenter = canvasGo.AddComponent<PaintingGuidancePresenter>();
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("_evaluator").objectReferenceValue = evaluator;
            serialized.FindProperty("_compositionCamera").objectReferenceValue = compositionCamera;
            serialized.FindProperty("_targetImage").objectReferenceValue = targetImage;
            serialized.FindProperty("_liveImage").objectReferenceValue = liveImage;
            serialized.FindProperty("_progressFill").objectReferenceValue = fill.rectTransform;
            serialized.FindProperty("_statusText").objectReferenceValue = status;
            serialized.FindProperty("_focusText").objectReferenceValue = focus;
            var names = serialized.FindProperty("_pieceNames");
            names.arraySize = RequiredPieces.Length;
            for (int i = 0; i < RequiredPieces.Length; i++)
                names.GetArrayElementAtIndex(i).stringValue = RequiredPieces[i];
            serialized.ApplyModifiedProperties();
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

                pieceRoot.position = handle.AuthoredPosition + UnsolvedStartOffsets[i];
                if (UnsolvedStartYawOffsets[i] != 0f)
                    pieceRoot.rotation = Quaternion.Euler(0f, UnsolvedStartYawOffsets[i], 0f) * handle.AuthoredRotation;
            }
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
            sun.position = new Vector3(1.55f, WaterTopY, -2.25f);

            var standMat = GetOrCreateMaterial("Mat_SunStand", LightStone, 0.42f, 0f);
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

            var far = new GameObject("Far Mountain").transform;
            far.SetParent(scenery, false);
            far.position = new Vector3(0.65f, WaterTopY, -1.85f);

            var mainMesh = GetOrCreateConeMesh("MountainCone_FarSharp", 0.85f, 0.06f, 0.95f, 20);
            var sideMesh = GetOrCreateConeMesh("MountainCone_FarSideSharp", 0.45f, 0.04f, 0.5f, 16);
            CreateMeshRenderer(far, "Main Peak", Vector3.zero, mainMesh, paleMat, Quaternion.identity);
            CreateMeshRenderer(far, "Side Ridge", new Vector3(0.55f, 0f, -0.05f), sideMesh, paleMat, Quaternion.identity);
        }

        /// <summary>
        /// Deeper jade mass right of center, partly overlapping the far ridge:
        /// the dominant silhouette of the rear mountain line.
        /// </summary>
        private static void BuildMiddleMountain(Transform scenery)
        {
            var jadeMat = GetOrCreateMaterial("Mat_CeladonJade", CeladonJade, 0.35f, 0f);

            var middle = new GameObject("Middle Mountain").transform;
            middle.SetParent(scenery, false);
            middle.position = new Vector3(-0.45f, WaterTopY, -1.2f);

            var mainMesh = GetOrCreateConeMesh("MountainCone_MainSharp", 1.0f, 0.07f, 1.55f, 24);
            var shoulderMesh = GetOrCreateConeMesh("MountainCone_ShoulderSharp", 0.55f, 0.05f, 0.9f, 20);
            CreateMeshRenderer(middle, "Main Peak", Vector3.zero, mainMesh, jadeMat, Quaternion.identity);
            CreateMeshRenderer(middle, "Shoulder", new Vector3(-0.5f, 0f, -0.15f), shoulderMesh, jadeMat, Quaternion.identity);
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
            var trunkMesh = GetOrCreateMesh("TreeTrunk", new Vector3(0.10f, 0.56f, 0.10f), 0.04f, 6);
            var canopyMesh = GetOrCreateMesh("TreeCanopy", new Vector3(1.0f, 0.85f, 1.0f), 0.30f, 8);

            var tree = new GameObject(name).transform;
            tree.SetParent(cluster, false);
            tree.localPosition = localPosition;
            tree.localRotation = Quaternion.Euler(0f, yaw, 0f);
            CreateMeshRenderer(tree, "Trunk", new Vector3(0f, 0.28f * scale, 0f),
                trunkMesh, treeMat, Quaternion.identity, Vector3.one * scale);
            CreateMeshRenderer(tree, "Canopy", new Vector3(0f, 0.985f * scale, 0f),
                canopyMesh, treeMat, Quaternion.identity, Vector3.one * scale);
        }

        /// <summary>Porcelain-white toy pavilion in the composition's right-middle third, on the water in front of the middle mountain.</summary>
        private static void BuildPavilion(Transform scenery)
        {
            var porcelainMat = GetOrCreateMaterial("Mat_Porcelain", Porcelain, 0.45f, 0f);

            var pavilion = new GameObject("Pavilion").transform;
            pavilion.SetParent(scenery, false);
            pavilion.position = new Vector3(-0.85f, WaterTopY, -0.55f);
            pavilion.localScale = Vector3.one * 0.78f;

            var baseMesh = GetOrCreateMesh("PavilionBase", new Vector3(0.95f, 0.12f, 0.8f), 0.04f, 8);
            var columnMesh = GetOrCreateMesh("PavilionColumn", new Vector3(0.09f, 0.5f, 0.09f), 0.03f, 6);
            var roofMesh = GetOrCreateConeMesh("PavilionRoof", 0.72f, 0.06f, 0.4f, 24);
            var finialMesh = GetOrCreateMesh("PavilionFinial", new Vector3(0.11f, 0.11f, 0.11f), 0.045f, 6);

            CreateMeshRenderer(pavilion, "Base", new Vector3(0f, 0.06f, 0f), baseMesh, porcelainMat, Quaternion.identity);
            int columnIndex = 0;
            foreach (float sideX in new[] { -1f, 1f })
            {
                foreach (float sideZ in new[] { -1f, 1f })
                {
                    CreateMeshRenderer(pavilion, "Column " + (++columnIndex),
                        new Vector3(0.38f * sideX, 0.37f, 0.3f * sideZ),
                        columnMesh, porcelainMat, Quaternion.identity);
                }
            }
            // Column tops are at y=0.62. The roof mesh starts at local y=0,
            // so placing it at 0.62 joins the structure without a visible gap;
            // its top then reaches y=1.02, exactly the finial's lower face.
            CreateMeshRenderer(pavilion, "Roof", new Vector3(0f, 0.62f, 0f), roofMesh, porcelainMat, Quaternion.identity);
            CreateMeshRenderer(pavilion, "Finial", new Vector3(0f, 1.075f, 0f), finialMesh, porcelainMat, Quaternion.identity);
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

            var bridge = new GameObject("Arch Bridge").transform;
            bridge.SetParent(scenery, false);
            bridge.position = new Vector3(0f, WaterTopY, 0.95f);

            var bridgeMesh = GetOrCreateArchMesh("ArchBridgeDeck_Cycle2Small", 1.85f, 0.42f, 0.34f, 0.14f, 14);
            CreateMeshRenderer(bridge, "Deck", Vector3.zero, bridgeMesh, bridgeMat, Quaternion.identity);
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
            key.intensity = 0.72f;
            key.color = new Color(1f, 0.965f, 0.91f);
            key.shadows = LightShadows.None;

            // Low-intensity cool fill from the build-camera side keeps the
            // back-facing surfaces of the diorama readable.
            var fillGo = new GameObject("Fill Light (Directional)");
            fillGo.transform.SetParent(lights, false);
            fillGo.transform.rotation = Quaternion.Euler(15f, 40f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.24f;
            fill.color = new Color(0.82f, 0.9f, 1f);
            fill.shadows = LightShadows.None;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.38f, 0.37f, 0.35f);
            RenderSettings.fog = false;
        }

        private static void BuildCameras(Transform root)
        {
            BuildCamera(root, "Build Camera", BuildCameraPosition, BuildCameraTarget, 42f, true);
            Camera compositionCamera = BuildCamera(
                root, "Composition Camera", CompositionCameraPosition, CompositionCameraTarget, 24f, false);
            // The player manipulates scenery through the Build Camera. The
            // Composition Camera is a hidden scoring sensor: its matrices are
            // used by PaintingCompositionEvaluator and CaptureAll can still
            // call Camera.Render() explicitly while the component is disabled.
            compositionCamera.enabled = false;
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
            colorAdjustments.postExposure.value = -0.1f;
            colorAdjustments.contrast.value = 5f;
            colorAdjustments.saturation.value = 18f;
            colorAdjustments.hueShift.value = 0f;
            colorAdjustments.colorFilter.value = new Color(1f, 0.985f, 0.96f);

            if (!profile.TryGet<Vignette>(out var vignette))
                vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.18f;
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
            const string path = MaterialsFolder + "/PaintingPrototypeProfile.asset";
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
                    if (layer >= 0 && pieceRoot.gameObject.layer != layer)
                        missing.Add("Solved Scenery/" + RequiredPieces[i] + " must be on the 'PaintingPiece' layer");
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
                    if (!colliders[0].enabled)
                        missing.Add("Solved Scenery/" + RequiredPieces[i] + " selection collider must be enabled");
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

            Vector3 expectedStart = handle.AuthoredPosition + UnsolvedStartOffsets[pieceIndex];
            if (Vector3.Distance(pieceRoot.position, expectedStart) > 0.001f)
                missing.Add(label + " must start at its authored unsolved position");
            float expectedYaw = UnsolvedStartYawOffsets[pieceIndex];
            if (Mathf.Abs(handle.AuthoredSignedYawOffset(pieceRoot.rotation) - expectedYaw) > 0.01f)
                missing.Add(label + " must start at yaw " + expectedYaw + " degrees from its authored rotation");

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
