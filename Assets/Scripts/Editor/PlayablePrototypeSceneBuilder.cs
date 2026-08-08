#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using PerspectivePuzzle.Domain;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.EditorTools
{
    /// <summary>
    /// Deterministically builds the T-007 playable scene at
    /// Assets/Scenes/PlayablePrototype.unity: the accepted tabletop stage with
    /// a readable 5x5 placement surface on a raycast layer, a rounded teal piece
    /// prefab, translucent snapped preview, in-world 1/2/3 layer indicator, the
    /// physical 5x5 projection board whose cells distinguish
    /// Missing/Extra/Matched/Empty, the restrained three-step progress
    /// indicator, and a three-puzzle session wired into the input controller
    /// with input locking, a camera reveal on exact match, per-puzzle R reset
    /// and Space advancement through the ordered <see cref="PuzzleContent"/>.
    /// Callable from the menu or via
    /// -executeMethod PerspectivePuzzle.EditorTools.PlayablePrototypeSceneBuilder.BuildScene.
    /// </summary>
    public static class PlayablePrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/PlayablePrototype.unity";
        private const string VisualPrototypeScenePath = "Assets/Scenes/VisualPrototype.unity";
        private const string PiecePrefabPath = "Assets/Prefabs/Piece_RoundedTeal.prefab";
        private const string MaterialsFolder = "Assets/Art/Materials";
        private const string MeshesFolder = "Assets/Art/Meshes";
        private const string ProfilePath = "Assets/Art/Materials/VisualPrototypeProfile.asset";
        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private const string ToyShaderName = "PerspectivePuzzle/ToyLit";
        private const int SurfaceLayerIndex = 3; // named "PlacementSurface" below

        // Accepted palette from docs/VISUAL_FOUNDATION.md and T-004.
        private static readonly Color WarmIvory = new Color(0.949f, 0.933f, 0.902f);
        private static readonly Color LightStone = new Color(0.871f, 0.843f, 0.792f);
        private static readonly Color MutedTeal = new Color(0.298f, 0.604f, 0.573f);
        private static readonly Color SoftCoral = new Color(0.898f, 0.541f, 0.396f);
        private static readonly Color BoardWhite = new Color(0.969f, 0.957f, 0.933f);
        private static readonly Color FrameBlueGray = new Color(0.243f, 0.290f, 0.322f);

        // Camera pose framing the 16:9 three-quarter construction view.
        private static readonly Vector3 CameraPosition = new Vector3(-9.2f, 8.0f, -10.2f);
        private static readonly Vector3 CameraTarget = new Vector3(0f, 1.2f, 0.6f);

        // 5x5x3 layout: cell (0,0,0) center at Origin, grid Y -> world Z,
        // grid Z (layer) -> world Y. Piece height 0.56, so cell centers sit
        // half a piece (0.28) above the slab top (y = 1.15).
        private static readonly Vector3 GridOrigin = new Vector3(-1.32f, 1.43f, -1.32f);
        private const float GridSpacingX = 0.66f;
        private const float GridSpacingY = 0.66f;
        private const float GridLayerHeight = 0.62f;
        private static readonly Vector3 PieceSize = new Vector3(0.56f, 0.56f, 0.56f);

        // Puzzle one of the project-owned three-puzzle slice: the smiley. The
        // scene serializes exactly this pattern so the board reads correctly
        // before play; the runtime progression coordinator owns all three.
        private static readonly Vector2Int[] TargetCells = PuzzleContent.Puzzles[0];

        private static readonly Vector3 BoardPosition = new Vector3(-5.8f, 0f, 4.5f);
        private static readonly Vector3 RevealPosition = new Vector3(-6.7f, 2.0f, 0.6f);

        [MenuItem("Tools/PerspectivePuzzle/Build Playable Prototype Scene")]
        public static void BuildScene()
        {
            EnsureFolder("Assets", "Art");
            EnsureFolder("Assets/Art", "Materials");
            EnsureFolder("Assets/Art", "Meshes");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets", "Scenes");
            EnsureLayerName();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("Playable Prototype").transform;
            BuildStage(root);
            BuildPiecePrefab();
            BuildPlacementPreview(root);
            BuildLayerIndicator(root);
            BuildControlLegend(root);
            BuildProgressIndicator(root);
            BuildProjectionBoard(root);
            BuildCamera(root);
            BuildLights(root);
            BuildPostProcessing(root);
            BuildPuzzle(root);

            var buildScenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(VisualPrototypeScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };
            EditorBuildSettings.scenes = buildScenes.ToArray();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveScene(scene, ScenePath);

            ValidateScene();
            Debug.Log("Playable Prototype scene built at " + ScenePath);
        }

        private static void BuildStage(Transform root)
        {
            var floorMat = GetOrCreateMaterial("Mat_WarmIvory", WarmIvory, 0.25f, 0f, false);
            var plinthMat = GetOrCreateMaterial("Mat_LightStone", LightStone, 0.30f, 0f, false);
            var tealMat = GetOrCreateMaterial("Mat_MutedTeal", MutedTeal, 0.55f, 0.02f, true);
            var boardMat = GetOrCreateMaterial("Mat_BoardWhite", BoardWhite, 0.50f, 0f, true);
            var tileMat = GetOrCreateMaterial("Mat_LightStone", LightStone, 0.30f, 0f, false);

            var floorMesh = GetOrCreateMesh("RoundedFloorCompact", new Vector3(19f, 0.5f, 14f), 0.12f, 6);
            CreateMeshRenderer(root, "Floor", new Vector3(0f, -0.25f, 0f), floorMesh, floorMat, Quaternion.identity);

            var plinthMesh = GetOrCreateMesh("RoundedPlinth", new Vector3(7.4f, 0.6f, 4.8f), 0.15f, 10);
            CreateMeshRenderer(root, "Plinth", new Vector3(-1.2f, 0.3f, 0.2f), plinthMesh, plinthMat, Quaternion.identity);

            // Raised near-white slab on the plinth; the 5x5 placement surface.
            // Slab top at y = 1.15.
            var slabMesh = GetOrCreateMesh("PuzzleSlab", new Vector3(3.7f, 0.55f, 3.7f), 0.14f, 10);
            var slab = CreateMeshRenderer(root, "Puzzle Slab", new Vector3(0f, 0.875f, 0f), slabMesh, boardMat, Quaternion.identity);
            slab.layer = SurfaceLayerIndex;
            var collider = slab.AddComponent<BoxCollider>();
            collider.size = new Vector3(3.7f, 0.55f, 3.7f);

            // 25 light-stone cell tiles on the white slab: the readable grid.
            var cellTileMesh = GetOrCreateMesh("PuzzleCellTile", new Vector3(0.58f, 0.02f, 0.58f), 0.008f, 6);
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    var center = GridOrigin + new Vector3(x * GridSpacingX, 0f, y * GridSpacingY);
                    var tile = CreateMeshRenderer(root, "Cell Tile " + x + "," + y,
                        new Vector3(center.x, 1.16f, center.z), cellTileMesh, tileMat, Quaternion.identity);
                    tile.layer = SurfaceLayerIndex;
                }
            }

            // Empty root for spawned piece views.
            var pieces = new GameObject("Pieces");
            pieces.transform.SetParent(root, false);
            pieces.transform.position = new Vector3(0f, 1.15f, 0f);
        }

        /// <summary>
        /// Saves the rounded teal piece (PieceView + rounded mesh + teal
        /// material) as a reusable prefab the controller instantiates.
        /// </summary>
        private static void BuildPiecePrefab()
        {
            var pieceMesh = GetOrCreateMesh("RoundedPiece", PieceSize, 0.10f, 8);
            var tealMat = GetOrCreateMaterial("Mat_MutedTeal", MutedTeal, 0.55f, 0.02f, true);

            var go = new GameObject("Piece_RoundedTeal");
            go.AddComponent<PieceView>();
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = pieceMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = tealMat;

            PrefabUtility.SaveAsPrefabAsset(go, PiecePrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void BuildPlacementPreview(Transform root)
        {
            var pieceMesh = GetOrCreateMesh("RoundedPiece", PieceSize, 0.10f, 8);

            var preview = new GameObject("Placement Preview");
            preview.transform.SetParent(root, false);
            var controller = preview.AddComponent<PlacementPreview>();

            var body = new GameObject("Preview Body");
            body.transform.SetParent(preview.transform, false);
            var filter = body.AddComponent<MeshFilter>();
            filter.sharedMesh = pieceMesh;
            var renderer = body.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetOrCreatePreviewMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            SetSerialized(controller, "previewRenderer", renderer);
        }

        private static void BuildLayerIndicator(Transform root)
        {
            var coralMat = GetOrCreateMaterial("Mat_Coral", SoftCoral, 0.45f, 0f, true);
            var frameMat = GetOrCreateMaterial("Mat_FrameBlueGray", FrameBlueGray, 0.35f, 0f, false);
            var markerMesh = GetOrCreateMesh("LayerMarker", new Vector3(0.52f, 0.07f, 0.52f), 0.03f, 6);
            var stemMesh = GetOrCreateMesh("LayerStem", new Vector3(0.09f, 1.35f, 0.09f), 0.03f, 6);

            var indicator = new GameObject("Layer Indicator");
            indicator.transform.SetParent(root, false);
            indicator.transform.position = new Vector3(-2.15f, 0.6f, 1.9f);
            var component = indicator.AddComponent<LayerIndicator>();

            // The stem is fixed; the marker moves up by layer height.
            var stem = new GameObject("Stem");
            stem.transform.SetParent(indicator.transform, false);
            stem.transform.localPosition = new Vector3(0f, 0.675f, 0f);
            var stemFilter = stem.AddComponent<MeshFilter>();
            stemFilter.sharedMesh = stemMesh;
            var stemRenderer = stem.AddComponent<MeshRenderer>();
            stemRenderer.sharedMaterial = frameMat;

            var marker = new GameObject("Marker");
            marker.transform.SetParent(indicator.transform, false);
            var markerFilter = marker.AddComponent<MeshFilter>();
            markerFilter.sharedMesh = markerMesh;
            var markerRenderer = marker.AddComponent<MeshRenderer>();
            markerRenderer.sharedMaterial = coralMat;

            // Numeric 1/2/3 label riding on the marker, facing the camera, so
            // the active layer is unmistakable at a glance.
            var label = new GameObject("Layer Label");
            label.transform.SetParent(marker.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.11f, 0f);
            var labelText = label.AddComponent<TextMesh>();
            labelText.font = GetBuiltinFont();
            labelText.text = "1";
            labelText.anchor = TextAnchor.MiddleCenter;
            labelText.alignment = TextAlignment.Center;
            labelText.characterSize = 0.05f;
            labelText.fontSize = 48;
            labelText.color = BoardWhite;
            label.transform.rotation = FacingCameraRotation(indicator.transform.position);

            SetSerialized(component, "marker", marker.transform);
            SetSerialized(component, "label", labelText);
            SetSerialized(component, "layerHeight", GridLayerHeight);
        }

        /// <summary>
        /// The restrained in-world control legend: a TextMesh lying on the
        /// floor beside the stage, facing the camera, listing the interactions
        /// without covering the puzzle or the projection board. A second,
        /// smaller coral TextMesh below the main text mentions Space — Next
        /// Puzzle; the runtime shows it only while a later puzzle exists.
        /// </summary>
        private static void BuildControlLegend(Transform root)
        {
            var legendGo = new GameObject("Control Legend");
            legendGo.transform.SetParent(root, false);
            legendGo.transform.position = new Vector3(3.25f, 0.9f, -1.75f);
            legendGo.transform.rotation = FacingCameraRotation(legendGo.transform.position);

            var plaqueMesh = GetOrCreateMesh("ControlLegendPlaqueTall", new Vector3(3.7f, 1.25f, 0.12f), 0.05f, 8);
            var plaqueMaterial = GetOrCreateMaterial("Mat_BoardWhite", BoardWhite, 0.25f, 0f, true);
            CreateMeshRenderer(legendGo.transform, "Plaque", Vector3.zero,
                plaqueMesh, plaqueMaterial, Quaternion.identity);

            var textGo = new GameObject("Control Text");
            textGo.transform.SetParent(legendGo.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0.10f, 0.07f);
            // Legacy TextMesh renders its readable front toward local -Z.
            textGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var text = textGo.AddComponent<TextMesh>();
            text.font = GetBuiltinFont();
            text.text = "Left Click Place    Right Click Remove\n1 / 2 / 3 Layer    Z Undo    R Reset";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.052f;
            text.fontSize = 40;
            text.color = FrameBlueGray;

            // The Space-next hint: coral accent line below the main text,
            // hidden by the input controller once the final puzzle is reached.
            var hintGo = new GameObject("Next Hint");
            hintGo.transform.SetParent(legendGo.transform, false);
            hintGo.transform.localPosition = new Vector3(0f, -0.32f, 0.07f);
            hintGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var hint = hintGo.AddComponent<TextMesh>();
            hint.font = GetBuiltinFont();
            hint.text = "Space — Next Puzzle";
            hint.anchor = TextAnchor.MiddleCenter;
            hint.alignment = TextAlignment.Center;
            hint.characterSize = 0.045f;
            hint.fontSize = 40;
            hint.color = SoftCoral;

            var component = legendGo.AddComponent<ControlLegend>();
            SetSerialized(component, "textMesh", text);
            SetSerialized(component, "nextHintMesh", hint);
        }

        /// <summary>
        /// The restrained physical three-step progress indicator: three rounded
        /// pips on a small plaque on the plinth in front of the puzzle slab.
        /// Step one is painted coral at build time; the input controller
        /// repaints after every puzzle transition (done teal, current coral,
        /// upcoming near-white).
        /// </summary>
        private static void BuildProgressIndicator(Transform root)
        {
            var plaqueMat = GetOrCreateMaterial("Mat_LightStone", LightStone, 0.30f, 0f, false);
            var coralMat = GetOrCreateMaterial("Mat_Coral", SoftCoral, 0.45f, 0f, true);
            var whiteMat = GetOrCreateMaterial("Mat_BoardWhite", BoardWhite, 0.50f, 0f, true);

            var indicator = new GameObject("Progress Indicator");
            indicator.transform.SetParent(root, false);
            indicator.transform.position = new Vector3(0f, 0.62f, -2.02f);
            indicator.transform.rotation = Quaternion.identity;

            var plaqueMesh = GetOrCreateMesh("ProgressPlaque", new Vector3(1.9f, 0.05f, 0.34f), 0.02f, 8);
            CreateMeshRenderer(indicator.transform, "Plaque", Vector3.zero,
                plaqueMesh, plaqueMat, Quaternion.identity);

            var pipMesh = GetOrCreateMesh("ProgressPip", new Vector3(0.26f, 0.045f, 0.26f), 0.02f, 8);
            var pips = new List<MeshRenderer>(3);
            for (int i = 0; i < 3; i++)
            {
                var pip = CreateMeshRenderer(indicator.transform, "Step " + (i + 1),
                    new Vector3(-0.5f + i * 0.5f, 0.05f, 0f), pipMesh,
                    i == 0 ? coralMat : whiteMat, Quaternion.identity);
                pips.Add(pip.GetComponent<MeshRenderer>());
            }

            var component = indicator.AddComponent<ProgressIndicator>();
            var serialized = new SerializedObject(component);
            var pipsProperty = serialized.FindProperty("pips");
            pipsProperty.arraySize = 3;
            for (int i = 0; i < 3; i++)
                pipsProperty.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Rotation that turns an in-world object toward the fixed review camera (flattened to the horizontal).</summary>
        private static Quaternion FacingCameraRotation(Vector3 worldPosition)
        {
            var towardCamera = CameraPosition - worldPosition;
            towardCamera.y = 0f;
            return Quaternion.LookRotation(towardCamera.normalized);
        }

        private static Font GetBuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
                throw new InvalidOperationException("No built-in font found for the in-world legend.");
            return font;
        }

        /// <summary>
        /// Physical projection board with the 5x5 state cells. The board view
        /// computes Missing/Extra/Matched/Empty per cell after every action and
        /// the cell renderer repaints the tiles with the accepted palette.
        /// </summary>
        private static void BuildProjectionBoard(Transform root)
        {
            var boardMat = GetOrCreateMaterial("Mat_BoardWhite", BoardWhite, 0.50f, 0f, true);
            var frameMat = GetOrCreateMaterial("Mat_FrameBlueGray", FrameBlueGray, 0.35f, 0f, false);
            var cellMat = GetOrCreateMaterial("Mat_BoardCell", BoardWhite, 0.30f, 0f, true);
            var missingMat = GetOrCreateMaterial("Mat_BoardCellMissing", SoftCoral, 0.30f, 0f, true);

            var surfaceMesh = GetOrCreateMesh("BoardSurface", new Vector3(2.9f, 2.1f, 0.12f), 0.05f, 8);
            var barHMesh = GetOrCreateMesh("FrameBar_Horizontal", new Vector3(3.1f, 0.14f, 0.18f), 0.05f, 6);
            var barVMesh = GetOrCreateMesh("FrameBar_Vertical", new Vector3(0.14f, 2.24f, 0.18f), 0.05f, 6);
            var legMesh = GetOrCreateMesh("BoardLeg", new Vector3(0.14f, 2.1f, 0.14f), 0.05f, 6);
            var cellTileMesh = GetOrCreateMesh("ProjectionBoardTile", new Vector3(0.34f, 0.05f, 0.34f), 0.02f, 6);

            var board = new GameObject("Projection Board").transform;
            board.SetParent(root, false);
            board.position = BoardPosition;

            var towardCamera = CameraPosition - board.position;
            towardCamera.y = 0f;
            board.rotation = Quaternion.LookRotation(towardCamera.normalized) * Quaternion.Euler(6f, 0f, 0f);

            CreateMeshRenderer(board, "Surface", Vector3.zero, surfaceMesh, boardMat, Quaternion.identity);
            CreateMeshRenderer(board, "Frame Top", new Vector3(0f, 1.06f, 0.08f), barHMesh, frameMat, Quaternion.identity);
            CreateMeshRenderer(board, "Frame Bottom", new Vector3(0f, -1.06f, 0.08f), barHMesh, frameMat, Quaternion.identity);
            CreateMeshRenderer(board, "Frame Left", new Vector3(-1.38f, 0f, 0.08f), barVMesh, frameMat, Quaternion.identity);
            CreateMeshRenderer(board, "Frame Right", new Vector3(1.38f, 0f, 0.08f), barVMesh, frameMat, Quaternion.identity);

            // 5x5 state cells, row-major in y so index = y * width + x matches
            // ProjectionBoardView.StateAt indexing. Grid row y = 0 (the row
            // nearest the player) sits at the bottom of the board, row y = 4 at
            // the top, so the board reads as the front view of the grid. The
            // target cells are painted in the Missing color at build time: the
            // runtime repaint produces the identical initial state, and the
            // scene also reads correctly before play.
            const float pitch = 0.37f;
            var cells = new List<MeshRenderer>(25);
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    bool isTarget = Array.IndexOf(TargetCells, new Vector2Int(x, y)) >= 0;
                    // The board faces the camera, so its local +X points to the
                    // left of the frame; placing grid x from right to left makes
                    // the board image match the camera's view of the grid.
                    var cell = CreateMeshRenderer(board, "Cell " + y + "," + x,
                        new Vector3(0.74f - x * pitch, -0.74f + y * pitch, 0.10f),
                        cellTileMesh, isTarget ? missingMat : cellMat, Quaternion.identity);
                    cells.Add(cell.GetComponent<MeshRenderer>());
                }
            }

            var view = board.gameObject.AddComponent<ProjectionBoardView>();
            var cellRenderer = board.gameObject.AddComponent<ProjectionBoardCellRenderer>();
            var cellRendererSerialized = new SerializedObject(cellRenderer);
            cellRendererSerialized.FindProperty("board").objectReferenceValue = view;
            var cellsProperty = cellRendererSerialized.FindProperty("cells");
            cellsProperty.arraySize = 25;
            for (int i = 0; i < 25; i++)
                cellsProperty.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
            cellRendererSerialized.ApplyModifiedPropertiesWithoutUndo();

            // Two rear legs running from the lower corners down-and-back to the floor.
            float legLength = 2.1f;
            var legAxis = new Vector3(0f, -Mathf.Cos(33f * Mathf.Deg2Rad), -Mathf.Sin(33f * Mathf.Deg2Rad)).normalized;
            var legRotation = Quaternion.FromToRotation(Vector3.up, legAxis);
            var attach = new Vector3(1.38f, -1.03f, 0.06f);
            var bottomLocal = attach + legAxis * legLength;
            board.position = new Vector3(BoardPosition.x, -(board.rotation * bottomLocal).y, BoardPosition.z);

            foreach (float side in new[] { -1f, 1f })
            {
                attach.x = 1.38f * side;
                var centerLocal = attach + legAxis * (legLength * 0.5f);
                CreateMeshRenderer(board, "Support Leg " + (side < 0f ? "Left" : "Right"), centerLocal, legMesh, frameMat, legRotation);
            }
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
            cam.orthographicSize = 5.8f;
            cam.aspect = 16f / 9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = WarmIvory;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.allowHDR = true;

            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        private static void BuildLights(Transform root)
        {
            var keyGo = new GameObject("Key Light (Directional)");
            keyGo.transform.SetParent(root, false);
            keyGo.transform.rotation = Quaternion.Euler(50f, -145f, 0f);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 0.72f;
            key.color = new Color(1f, 0.965f, 0.91f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.8f;

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

        private static void BuildPostProcessing(Transform root)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);

                var tonemapping = profile.Add<Tonemapping>(true);
                tonemapping.mode.value = TonemappingMode.Neutral;
                var colorAdjustments = profile.Add<ColorAdjustments>(true);
                colorAdjustments.postExposure.value = -0.55f;
                colorAdjustments.contrast.value = 4f;
                colorAdjustments.saturation.value = 12f;
                var vignette = profile.Add<Vignette>(true);
                vignette.intensity.value = 0.22f;
                vignette.smoothness.value = 0.4f;
                vignette.rounded.value = true;
                vignette.color.value = new Color(0.12f, 0.14f, 0.16f);
            }

            var volumeGo = new GameObject("Post-Process Volume");
            volumeGo.transform.SetParent(root, false);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static void BuildPuzzle(Transform root)
        {
            var camera = GameObject.Find("Main Camera").GetComponent<Camera>();

            var sourceGo = new GameObject("Puzzle Session Source");
            sourceGo.transform.SetParent(root, false);
            var source = sourceGo.AddComponent<PuzzleSessionSource>();

            var controllerGo = new GameObject("Input Controller");
            controllerGo.transform.SetParent(root, false);
            var controller = controllerGo.AddComponent<PuzzleInputController>();

            var revealGo = new GameObject("Match Reveal");
            revealGo.transform.SetParent(root, false);
            var reveal = revealGo.AddComponent<MatchReveal>();

            var preview = GameObject.Find("Placement Preview").GetComponent<PlacementPreview>();
            var layerIndicator = GameObject.Find("Layer Indicator").GetComponent<LayerIndicator>();
            var progressIndicator = GameObject.Find("Progress Indicator").GetComponent<ProgressIndicator>();
            var legend = GameObject.Find("Control Legend").GetComponent<ControlLegend>();
            var board = GameObject.Find("Projection Board").GetComponent<ProjectionBoardView>();
            var pieces = GameObject.Find("Pieces").transform;
            var piecePrefab = AssetDatabase.LoadAssetAtPath<PieceView>(PiecePrefabPath);

            var sourceSerialized = new SerializedObject(source);
            sourceSerialized.FindProperty("controller").objectReferenceValue = controller;
            var targetProperty = sourceSerialized.FindProperty("targetCells");
            targetProperty.arraySize = TargetCells.Length;
            for (int i = 0; i < TargetCells.Length; i++)
                targetProperty.GetArrayElementAtIndex(i).vector2IntValue = TargetCells[i];
            sourceSerialized.FindProperty("origin").vector3Value = GridOrigin;
            sourceSerialized.FindProperty("spacingX").floatValue = GridSpacingX;
            sourceSerialized.FindProperty("spacingY").floatValue = GridSpacingY;
            sourceSerialized.FindProperty("layerHeight").floatValue = GridLayerHeight;
            sourceSerialized.ApplyModifiedPropertiesWithoutUndo();

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("pointerCamera").objectReferenceValue = camera;
            controllerSerialized.FindProperty("preview").objectReferenceValue = preview;
            controllerSerialized.FindProperty("board").objectReferenceValue = board;
            controllerSerialized.FindProperty("layerIndicator").objectReferenceValue = layerIndicator;
            controllerSerialized.FindProperty("sessionSource").objectReferenceValue = source;
            controllerSerialized.FindProperty("reveal").objectReferenceValue = reveal;
            controllerSerialized.FindProperty("pieceRoot").objectReferenceValue = pieces;
            controllerSerialized.FindProperty("piecePrefab").objectReferenceValue = piecePrefab;
            controllerSerialized.FindProperty("progressIndicator").objectReferenceValue = progressIndicator;
            controllerSerialized.FindProperty("legend").objectReferenceValue = legend;
            controllerSerialized.FindProperty("surfaceMask").intValue = 1 << SurfaceLayerIndex;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var revealSerialized = new SerializedObject(reveal);
            revealSerialized.FindProperty("controller").objectReferenceValue = controller;
            revealSerialized.FindProperty("targetCamera").objectReferenceValue = camera;
            revealSerialized.FindProperty("boardTarget").objectReferenceValue = board.transform;
            revealSerialized.FindProperty("revealPosition").vector3Value = RevealPosition;
            revealSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateScene()
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<PuzzleInputController>();
            if (controller == null)
                throw new InvalidOperationException("PlayablePrototype validation failed: PuzzleInputController missing.");

            var serialized = new SerializedObject(controller);
            if (serialized.FindProperty("pointerCamera").objectReferenceValue == null
                || serialized.FindProperty("preview").objectReferenceValue == null
                || serialized.FindProperty("board").objectReferenceValue == null
                || serialized.FindProperty("layerIndicator").objectReferenceValue == null
                || serialized.FindProperty("pieceRoot").objectReferenceValue == null
                || serialized.FindProperty("piecePrefab").objectReferenceValue == null)
                throw new InvalidOperationException("PlayablePrototype validation failed: input controller has missing references.");
            if (serialized.FindProperty("surfaceMask").intValue != (1 << SurfaceLayerIndex))
                throw new InvalidOperationException("PlayablePrototype validation failed: surface raycast layer not set.");
            if (serialized.FindProperty("sessionSource").objectReferenceValue == null
                || serialized.FindProperty("reveal").objectReferenceValue == null
                || serialized.FindProperty("progressIndicator").objectReferenceValue == null
                || serialized.FindProperty("legend").objectReferenceValue == null)
                throw new InvalidOperationException("PlayablePrototype validation failed: input controller reset/progression wiring missing.");

            var legend = UnityEngine.Object.FindFirstObjectByType<ControlLegend>();
            if (legend == null || string.IsNullOrEmpty(legend.Text))
                throw new InvalidOperationException("PlayablePrototype validation failed: in-world control legend missing or empty.");
            if (!legend.NextHintVisible)
                throw new InvalidOperationException("PlayablePrototype validation failed: legend Space-next hint not visible on puzzle one.");

            var progressIndicator = UnityEngine.Object.FindFirstObjectByType<ProgressIndicator>();
            if (progressIndicator == null)
                throw new InvalidOperationException("PlayablePrototype validation failed: progress indicator missing.");
            if (new SerializedObject(progressIndicator).FindProperty("pips").arraySize != 3)
                throw new InvalidOperationException("PlayablePrototype validation failed: progress indicator must paint exactly three pips.");

            var layerIndicator = UnityEngine.Object.FindFirstObjectByType<LayerIndicator>();
            if (layerIndicator == null)
                throw new InvalidOperationException("PlayablePrototype validation failed: layer indicator missing.");
            if (new SerializedObject(layerIndicator).FindProperty("label").objectReferenceValue == null)
                throw new InvalidOperationException("PlayablePrototype validation failed: layer indicator label missing.");

            var source = UnityEngine.Object.FindFirstObjectByType<PuzzleSessionSource>();
            if (source == null)
                throw new InvalidOperationException("PlayablePrototype validation failed: PuzzleSessionSource missing.");
            var targetProperty = new SerializedObject(source).FindProperty("targetCells");
            if (targetProperty.arraySize != TargetCells.Length)
                throw new InvalidOperationException("PlayablePrototype validation failed: target cells not serialized.");
            for (int i = 0; i < TargetCells.Length; i++)
            {
                if (targetProperty.GetArrayElementAtIndex(i).vector2IntValue != TargetCells[i])
                    throw new InvalidOperationException("PlayablePrototype validation failed: serialized puzzle-one target diverges from PuzzleContent.");
            }

            var board = UnityEngine.Object.FindFirstObjectByType<ProjectionBoardCellRenderer>();
            if (board == null || new SerializedObject(board).FindProperty("cells").arraySize != 25)
                throw new InvalidOperationException("PlayablePrototype validation failed: board cell renderer incomplete.");

            if (UnityEngine.Object.FindFirstObjectByType<MatchReveal>() == null)
                throw new InvalidOperationException("PlayablePrototype validation failed: MatchReveal missing.");

            // Content validity: exactly three distinct, non-empty, in-bounds
            // 5x5 targets. The coordinator constructor throws on any violation.
            var progression = new PuzzleProgression(PuzzleContent.Puzzles);
            if (!progression.HasNext)
                throw new InvalidOperationException("PlayablePrototype validation failed: progression must have a next puzzle at start.");

            bool playableInBuild = false;
            foreach (var buildScene in EditorBuildSettings.scenes)
                if (buildScene.path == ScenePath)
                    playableInBuild = true;
            if (!playableInBuild)
                throw new InvalidOperationException("PlayablePrototype validation failed: scene not in build settings.");

            Debug.Log("PlayablePrototype scene validation passed: controller, session source, 25 board cells, "
                + TargetCells.Length + " puzzle-one cells, three distinct content targets, legend with Space hint, "
                + "three-pip progress indicator, layer label, reveal, reset and raycast layer all wired.");
        }

        private static void EnsureLayerName()
        {
            var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManager == null || tagManager.Length == 0)
                return;

            var serialized = new SerializedObject(tagManager);
            var layers = serialized.FindProperty("layers");
            if (layers == null || layers.arraySize <= SurfaceLayerIndex)
                return;
            if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(SurfaceLayerIndex).stringValue))
            {
                layers.GetArrayElementAtIndex(SurfaceLayerIndex).stringValue = "PlacementSurface";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
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

        private static Material GetOrCreateMaterial(string assetName, Color color, float smoothness, float metallic, bool toy)
        {
            string path = MaterialsFolder + "/" + assetName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            string shaderName = toy ? ToyShaderName : LitShaderName;
            var desiredShader = Shader.Find(shaderName);
            if (desiredShader == null)
                throw new InvalidOperationException("Required URP shader was not found: " + shaderName);

            if (material == null)
            {
                material = new Material(desiredShader) { name = assetName };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = desiredShader;
            material.color = color;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", toy ? 0.18f : smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Translucent teal ghost material for the placement preview (URP Lit
        /// alpha blend; ToyLit has no transparency pass).
        /// </summary>
        private static Material GetOrCreatePreviewMaterial()
        {
            const string path = MaterialsFolder + "/Mat_PreviewGhost.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(LitShaderName);
            if (shader == null)
                throw new InvalidOperationException("Required URP shader was not found: " + LitShaderName);

            if (material == null)
            {
                material = new Material(shader) { name = "Mat_PreviewGhost" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Smoothness", 0.18f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetColor("_BaseColor", new Color(MutedTeal.r, MutedTeal.g, MutedTeal.b, 0.5f));
            material.color = new Color(MutedTeal.r, MutedTeal.g, MutedTeal.b, 0.5f);
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

        private static void SetSerialized(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerialized(UnityEngine.Object target, string propertyName, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
