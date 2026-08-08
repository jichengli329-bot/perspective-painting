#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PerspectivePuzzle.EditorTools
{
    /// <summary>
    /// Renders the playable prototype scene with the real GPU and writes a
    /// deterministic 1280x720 review PNG to Logs/VisualReviews/PlayablePrototype.png.
    /// Must run without -nographics. Callable from the menu or via
    /// -executeMethod PerspectivePuzzle.EditorTools.PlayablePrototypeCapture.Capture.
    /// </summary>
    public static class PlayablePrototypeCapture
    {
        private const string ScenePath = "Assets/Scenes/PlayablePrototype.unity";
        private const string OutputPath = "Logs/VisualReviews/PlayablePrototype.png";

        [MenuItem("Tools/PerspectivePuzzle/Capture Playable Prototype")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var camera = Camera.main;
            if (camera == null)
                throw new System.InvalidOperationException("PlayablePrototype has no Main Camera.");

            const int width = 1280;
            const int height = 720;
            var renderTexture = new RenderTexture(
                width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;

            try
            {
                if (!renderTexture.Create())
                    throw new System.InvalidOperationException("Could not create the capture RenderTexture.");
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();

                Directory.CreateDirectory("Logs/VisualReviews");
                File.WriteAllBytes(OutputPath, image.EncodeToPNG());
                Debug.Log("Playable prototype screenshot saved to " + OutputPath);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(image);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }
        }
    }
}
#endif
