#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.EditorTools
{
    /// <summary>
    /// Renders the painting prototype scene with the real GPU and writes
    /// deterministic PNGs: the two review images
    /// (Logs/VisualReviews/PaintingPrototype_Build.png from the Build Camera
    /// and PaintingPrototype_Composition.png from the Composition Camera)
    /// plus the three T-008B machine-readable target artifacts below
    /// Assets/Content/PaintingPrototype/References (beauty render, object-ID
    /// color mask and white silhouette, all from the Composition Camera).
    /// Must run without -nographics. Callable from the menu or via
    /// -executeMethod PerspectivePuzzle.EditorTools.PaintingPrototypeCapture.CaptureAll.
    /// </summary>
    public static class PaintingPrototypeCapture
    {
        private const string ScenePath = "Assets/Scenes/PaintingPrototype.unity";
        private const string BuildOutputPath = "Logs/VisualReviews/PaintingPrototype_Build.png";
        private const string CompositionOutputPath = "Logs/VisualReviews/PaintingPrototype_Composition.png";

        // T-008B target artifacts, all captured from the Composition Camera.
        private const string BeautyOutputPath =
            "Assets/Content/PaintingPrototype/References/MistValleyBridge_Beauty.png";
        private const string ObjectIdOutputPath =
            "Assets/Content/PaintingPrototype/References/MistValleyBridge_ObjectId.png";
        private const string SilhouetteOutputPath =
            "Assets/Content/PaintingPrototype/References/MistValleyBridge_Silhouette.png";

        /// <summary>Exact movable piece roots below "Solved Scenery" (T-008A), in capture order.</summary>
        private static readonly string[] RequiredPieces =
        {
            "Sun", "Far Mountain", "Middle Mountain", "Tree Cluster Left",
            "Tree Cluster Right", "Pavilion", "Arch Bridge", "Foreground Rock",
        };

        /// <summary>
        /// Stable opaque 24-bit object-ID colors, indexed by RequiredPieces:
        /// Sun #FF4040, Far Mountain #40FF40, Middle Mountain #4040FF,
        /// Tree Cluster Left #FFFF40, Tree Cluster Right #FF40FF, Pavilion
        /// #40FFFF, Arch Bridge #FF8040, Foreground Rock #8040FF.
        /// </summary>
        private static readonly Color[] ObjectIdColors =
        {
            new Color(1f, 0x40 / 255f, 0x40 / 255f),
            new Color(0x40 / 255f, 1f, 0x40 / 255f),
            new Color(0x40 / 255f, 0x40 / 255f, 1f),
            new Color(1f, 1f, 0x40 / 255f),
            new Color(1f, 0x40 / 255f, 1f),
            new Color(0x40 / 255f, 1f, 1f),
            new Color(1f, 0x80 / 255f, 0x40 / 255f),
            new Color(0x80 / 255f, 0x40 / 255f, 1f),
        };

        private static readonly Color SilhouetteWhite = Color.white;

        [MenuItem("Tools/PerspectivePuzzle/Capture Painting Prototype")]
        public static void CaptureAll()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // T-010C saves the playable scene deliberately unsolved. Reference
            // and machine-target captures must always depict the hidden
            // authored answer; these temporary restores are never saved.
            RestoreAuthoredSolutionForCapture();

            var buildCamera = FindCameraByName("Build Camera");
            var compositionCamera = FindCameraByName("Composition Camera");
            if (buildCamera == null)
                throw new InvalidOperationException("PaintingPrototype capture failed: Build Camera missing.");
            if (compositionCamera == null)
                throw new InvalidOperationException("PaintingPrototype capture failed: Composition Camera missing.");

            // Unchanged deterministic 1280x720 review outputs.
            Capture(buildCamera, BuildOutputPath);
            Capture(compositionCamera, CompositionOutputPath);

            // T-008B machine-readable target artifacts from the Composition Camera.
            var scenery = ValidateTargets();
            Capture(compositionCamera, BeautyOutputPath);
            CaptureTarget(compositionCamera, scenery, ObjectIdOutputPath, objectId: true);
            CaptureTarget(compositionCamera, scenery, SilhouetteOutputPath, objectId: false);

            AssetDatabase.Refresh();
            // T-009B2: the evaluator and PlayMode tests read the Object-ID
            // (and silhouette) masks back, so those imports must be readable;
            // the beauty reference does not need to be.
            ConfigureTextureImport(BeautyOutputPath, pointFilter: false, sRGB: true, readable: false);
            ConfigureTextureImport(ObjectIdOutputPath, pointFilter: true, sRGB: false, readable: true);
            ConfigureTextureImport(SilhouetteOutputPath, pointFilter: true, sRGB: false, readable: true);
        }

        private static void RestoreAuthoredSolutionForCapture()
        {
            var scenery = GameObject.Find("Solved Scenery");
            if (scenery == null)
                throw new InvalidOperationException("PaintingPrototype capture failed: Solved Scenery missing.");

            PaintingManipulablePiece[] pieces = scenery.GetComponentsInChildren<PaintingManipulablePiece>(true);
            if (pieces.Length != ObjectIdColors.Length)
                throw new InvalidOperationException(
                    "PaintingPrototype capture requires exactly " + ObjectIdColors.Length
                    + " configured manipulable pieces, found " + pieces.Length + ".");
            foreach (PaintingManipulablePiece piece in pieces)
            {
                if (!piece.IsConfigured)
                    throw new InvalidOperationException(
                        "PaintingPrototype capture found an unconfigured piece: " + piece.name + ".");
                piece.RestoreAuthored();
            }
        }

        private static Camera FindCameraByName(string name)
        {
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (camera.name == name)
                    return camera;
            }
            return null;
        }

        private static void Capture(Camera camera, string outputPath)
        {
            RenderToPng(camera, 1280, 720, outputPath);
        }

        private static void RenderToPng(Camera camera, int width, int height, string outputPath)
        {
            var renderTexture = new RenderTexture(
                width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;

            try
            {
                if (!renderTexture.Create())
                    throw new InvalidOperationException(
                        "Could not create the capture RenderTexture for " + camera.name + ".");
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
                Debug.Log("Painting prototype screenshot saved to " + outputPath);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        /// <summary>
        /// T-008B contract: the Composition Camera, "Solved Scenery" and all
        /// eight direct piece roots must exist, each root with at least one
        /// renderer, or the capture fails loudly before writing anything.
        /// </summary>
        private static Transform ValidateTargets()
        {
            var scenery = GameObject.Find("Solved Scenery");
            if (scenery == null)
                throw new InvalidOperationException("PaintingPrototype capture failed: Solved Scenery missing.");

            var missing = new List<string>();
            foreach (var pieceName in RequiredPieces)
            {
                if (scenery.transform.Find(pieceName) == null)
                    missing.Add(pieceName);
            }
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    "PaintingPrototype capture failed: missing piece roots below Solved Scenery: "
                    + string.Join(", ", missing) + ".");

            var empty = new List<string>();
            foreach (var pieceName in RequiredPieces)
            {
                if (scenery.transform.Find(pieceName).GetComponentsInChildren<Renderer>(true).Length == 0)
                    empty.Add(pieceName);
            }
            if (empty.Count > 0)
                throw new InvalidOperationException(
                    "PaintingPrototype capture failed: piece roots without renderers: "
                    + string.Join(", ", empty) + ".");

            return scenery.transform;
        }

        /// <summary>
        /// Captures the object-ID (per-piece flat colors) or white silhouette
        /// mask at 256x144 from the given camera. Only renderers below "Solved
        /// Scenery" are visible: every other scene renderer is temporarily
        /// disabled, the camera clears to solid black without post-processing,
        /// and each piece root's renderers get one stable opaque URP Unlit
        /// material. All scene state is restored in finally blocks.
        /// </summary>
        private static void CaptureTarget(Camera camera, Transform scenery, string outputPath, bool objectId)
        {
            const int width = 256;
            const int height = 144;

            var allRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var savedEnabled = new Dictionary<Renderer, bool>(allRenderers.Length);
            var savedMaterials = new Dictionary<Renderer, Material[]>();
            var tempMaterials = new List<Material>();
            var previousClearFlags = camera.clearFlags;
            var previousBackground = camera.backgroundColor;
            var previousPostProcessing = camera.GetUniversalAdditionalCameraData().renderPostProcessing;

            try
            {
                // Only renderers below the solved scenery root may contribute.
                foreach (var renderer in allRenderers)
                {
                    savedEnabled[renderer] = renderer.enabled;
                    if (!renderer.transform.IsChildOf(scenery))
                        renderer.enabled = false;
                }

                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

                for (int i = 0; i < RequiredPieces.Length; i++)
                {
                    var color = objectId ? ObjectIdColors[i] : SilhouetteWhite;
                    var material = CreateTemporaryUnlitMaterial(color, RequiredPieces[i]);
                    tempMaterials.Add(material);
                    foreach (var renderer in scenery.Find(RequiredPieces[i]).GetComponentsInChildren<Renderer>(true))
                    {
                        savedMaterials[renderer] = renderer.sharedMaterials;
                        renderer.sharedMaterials = new[] { material };
                    }
                }

                RenderToPng(camera, width, height, outputPath);
            }
            finally
            {
                // Restore renderer material arrays before destroying the
                // temporary materials, and restore everything else even if a
                // restore step itself fails.
                try
                {
                    foreach (var pair in savedMaterials)
                        pair.Key.sharedMaterials = pair.Value;
                }
                finally
                {
                    foreach (var material in tempMaterials)
                        UnityEngine.Object.DestroyImmediate(material);
                }

                foreach (var pair in savedEnabled)
                    pair.Key.enabled = pair.Value;
                camera.clearFlags = previousClearFlags;
                camera.backgroundColor = previousBackground;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = previousPostProcessing;
            }
        }

        /// <summary>
        /// Stable opaque URP Unlit material with an exact flat color, used only
        /// for the object-ID and silhouette passes. Never saved to disk.
        /// </summary>
        private static Material CreateTemporaryUnlitMaterial(Color color, string pieceName)
        {
            const string unlitShaderName = "Universal Render Pipeline/Unlit";
            var shader = Shader.Find(unlitShaderName);
            if (shader == null)
                throw new InvalidOperationException(
                    "PaintingPrototype capture failed: required URP shader not found: " + unlitShaderName);

            var material = new Material(shader) { name = "TempCapture_" + pieceName };
            material.SetColor("_BaseColor", color);
            material.color = color;
            material.SetOverrideTag("RenderType", "Opaque");
            return material;
        }

        /// <summary>
        /// Deterministic import settings for the three target PNGs: no
        /// mipmaps, clamp wrap, uncompressed so the exact bytes survive.
        /// Beauty uses bilinear filtering in sRGB; the masks use point
        /// filtering and non-sRGB so the raw 24-bit ID colors come back
        /// unmodified. Only the machine-readable masks are imported readable
        /// (<paramref name="readable"/>), since the evaluator and PlayMode
        /// tests read them back; the beauty reference is not.
        /// </summary>
        private static void ConfigureTextureImport(string path, bool pointFilter, bool sRGB, bool readable)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException(
                    "PaintingPrototype capture failed: could not load the texture importer for " + path);

            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = pointFilter ? FilterMode.Point : FilterMode.Bilinear;
            importer.sRGBTexture = sRGB;
            importer.isReadable = readable;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }
}
#endif
