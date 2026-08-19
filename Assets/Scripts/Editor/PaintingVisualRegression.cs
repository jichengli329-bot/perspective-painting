#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PerspectivePuzzle.EditorTools
{
    /// <summary>
    /// Fixed-camera visual regression gate. Baselines are explicit reviewed
    /// artifacts; current captures and heatmaps remain in ignored Logs.
    /// This detects composition drift without turning art judgment into a
    /// brittle exact-byte test.
    /// </summary>
    public static class PaintingVisualRegression
    {
        public const string BaselineRoot = "docs/visual-regression/baselines";
        public const string CaptureRoot = "Logs/VisualReviews";
        public const string ResultRoot = "Logs/VisualRegression";
        public const float MeanAbsoluteErrorLimit = 0.012f;
        public const float ChangedPixelRatioLimit = 0.06f;
        private const float PixelChangeThreshold = 12f / 255f;

        private static readonly string[] ReviewedFrames =
        {
            "PaintingPrototype_Build.png",
            "PaintingPrototype_Composition.png",
            "PaintingMoonGarden_Build.png",
            "PaintingMoonGarden_Composition.png",
            "PaintingRedCliffs_Build.png",
            "PaintingRedCliffs_Composition.png",
            "PaintingTwinSeal_Build.png",
            "PaintingTwinSeal_Composition.png",
            "PaintingTwinSeal_Secondary.png",
        };

        public readonly struct Comparison
        {
            public Comparison(string frame, float meanError, float changedRatio, bool passes)
            {
                Frame = frame;
                MeanError = meanError;
                ChangedRatio = changedRatio;
                Passes = passes;
            }

            public string Frame { get; }
            public float MeanError { get; }
            public float ChangedRatio { get; }
            public bool Passes { get; }
        }

        [MenuItem("Tools/PerspectivePuzzle/Visual Regression/Capture And Compare All")]
        public static void RunAll()
        {
            PaintingPrototypeCapture.CaptureAllGalleries();
            IReadOnlyList<Comparison> results = CompareReviewedFrames();
            foreach (Comparison result in results)
                if (!result.Passes)
                    throw new InvalidOperationException(
                        "Visual regression failed. See " + ResultRoot + "/report.md");
            Debug.Log("[PaintingVisualRegression] All " + results.Count + " reviewed frames passed.");
        }

        [MenuItem("Tools/PerspectivePuzzle/Visual Regression/Compare Existing Captures")]
        public static void CompareExistingMenu()
        {
            IReadOnlyList<Comparison> results = CompareReviewedFrames();
            int failed = 0;
            foreach (Comparison result in results)
                if (!result.Passes) failed++;
            if (failed > 0)
                throw new InvalidOperationException(
                    failed + " visual frame(s) exceeded thresholds. See " + ResultRoot + "/report.md");
            Debug.Log("[PaintingVisualRegression] Existing captures passed.");
        }

        public static IReadOnlyList<Comparison> CompareReviewedFrames()
        {
            Directory.CreateDirectory(ResultRoot);
            var results = new List<Comparison>(ReviewedFrames.Length);
            foreach (string frame in ReviewedFrames)
                results.Add(CompareFrame(frame));
            WriteReport(results);
            AssetDatabase.Refresh();
            return results;
        }

        public static Comparison CompareFrame(string frame)
        {
            if (string.IsNullOrWhiteSpace(frame) || Path.GetFileName(frame) != frame)
                throw new ArgumentException("Frame must be a plain file name.", nameof(frame));

            string baselinePath = Path.Combine(BaselineRoot, frame);
            string capturePath = Path.Combine(CaptureRoot, frame);
            if (!File.Exists(baselinePath))
                throw new FileNotFoundException("Reviewed visual baseline missing.", baselinePath);
            if (!File.Exists(capturePath))
                throw new FileNotFoundException("Current visual capture missing.", capturePath);

            Texture2D baseline = LoadPng(baselinePath);
            Texture2D current = LoadPng(capturePath);
            Texture2D heatmap = null;
            try
            {
                if (baseline.width != current.width || baseline.height != current.height)
                    throw new InvalidOperationException(frame + " dimensions differ from its baseline.");

                Color32[] expected = baseline.GetPixels32();
                Color32[] actual = current.GetPixels32();
                var diff = new Color32[expected.Length];
                double total = 0d;
                int changed = 0;
                for (int i = 0; i < expected.Length; i++)
                {
                    float dr = Mathf.Abs(expected[i].r - actual[i].r) / 255f;
                    float dg = Mathf.Abs(expected[i].g - actual[i].g) / 255f;
                    float db = Mathf.Abs(expected[i].b - actual[i].b) / 255f;
                    float error = (dr + dg + db) / 3f;
                    total += error;
                    if (Mathf.Max(dr, Mathf.Max(dg, db)) > PixelChangeThreshold) changed++;
                    byte intensity = (byte)Mathf.Clamp(Mathf.RoundToInt(error * 510f), 0, 255);
                    diff[i] = new Color32(intensity, (byte)(intensity / 8), 0, 255);
                }

                float mean = (float)(total / expected.Length);
                float ratio = (float)changed / expected.Length;
                heatmap = new Texture2D(baseline.width, baseline.height, TextureFormat.RGB24, false);
                heatmap.SetPixels32(diff);
                // EncodeToPNG still needs CPU-side pixels. The texture is
                // destroyed immediately after writing, so making it
                // non-readable here would save nothing and break encoding.
                heatmap.Apply(false, false);
                File.WriteAllBytes(Path.Combine(ResultRoot,
                    Path.GetFileNameWithoutExtension(frame) + "_Diff.png"), heatmap.EncodeToPNG());
                return new Comparison(frame, mean, ratio,
                    mean <= MeanAbsoluteErrorLimit && ratio <= ChangedPixelRatioLimit);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baseline);
                UnityEngine.Object.DestroyImmediate(current);
                if (heatmap != null) UnityEngine.Object.DestroyImmediate(heatmap);
            }
        }

        private static Texture2D LoadPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!texture.LoadImage(File.ReadAllBytes(path), false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidOperationException("Could not decode PNG: " + path);
            }
            return texture;
        }

        private static void WriteReport(IReadOnlyList<Comparison> results)
        {
            var report = new StringBuilder();
            report.AppendLine("# Painting visual regression");
            report.AppendLine();
            report.AppendLine("Thresholds: mean absolute RGB error <= "
                + MeanAbsoluteErrorLimit.ToString("0.000", CultureInfo.InvariantCulture)
                + ", changed-pixel ratio <= "
                + ChangedPixelRatioLimit.ToString("P1", CultureInfo.InvariantCulture) + ".");
            report.AppendLine();
            report.AppendLine("| Frame | Mean error | Changed pixels | Result |");
            report.AppendLine("|---|---:|---:|---|");
            foreach (Comparison result in results)
                report.AppendLine("| " + result.Frame + " | "
                    + result.MeanError.ToString("0.000000", CultureInfo.InvariantCulture) + " | "
                    + result.ChangedRatio.ToString("P2", CultureInfo.InvariantCulture) + " | "
                    + (result.Passes ? "PASS" : "FAIL") + " |");
            File.WriteAllText(Path.Combine(ResultRoot, "report.md"), report.ToString());
        }
    }
}
#endif
