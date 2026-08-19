#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PerspectivePuzzle.EditorTools
{
    /// <summary>
    /// Deterministic production mesh primitives for the T-026 celadon hero
    /// kit. These are deliberately authored as sculptural game forms rather
    /// than generic cones: every mountain has an offset spine, elliptical
    /// shelves and controlled hand-cut irregularity; pavilion roofs use a
    /// closed, upturned multi-ring shell. The meshes contain no gameplay
    /// state and remain safe to regenerate as editor assets.
    /// </summary>
    public static class CeladonHeroMeshFactory
    {
        public static Mesh CreateSculptedMountain(
            float width, float depth, float height, int radialSegments, int seed)
        {
            if (width <= 0f || depth <= 0f || height <= 0f)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (radialSegments < 8)
                throw new ArgumentOutOfRangeException(nameof(radialSegments));

            // Broad foot, two readable geological shoulders, narrow crown and
            // an off-axis summit. The asymmetric centerline is what stops the
            // result reading as a decorated cone from the composition camera.
            float[] heights = { 0f, 0.16f, 0.38f, 0.62f, 0.82f, 0.95f };
            float[] radii = { 1f, 0.91f, 0.74f, 0.53f, 0.31f, 0.12f };
            Vector2[] spine =
            {
                Vector2.zero,
                new Vector2(0.015f, -0.01f),
                new Vector2(-0.055f, 0.025f),
                new Vector2(0.035f, -0.015f),
                new Vector2(-0.04f, 0.018f),
                new Vector2(0.06f, -0.025f),
            };

            int ringCount = heights.Length;
            var vertices = new List<Vector3>(ringCount * radialSegments + 2);
            var triangles = new List<int>((ringCount - 1) * radialSegments * 6 + radialSegments * 6);

            for (int ring = 0; ring < ringCount; ring++)
            {
                for (int segment = 0; segment < radialSegments; segment++)
                {
                    float t = (float)segment / radialSegments;
                    float angle = t * Mathf.PI * 2f;
                    float irregular = 1f
                        + 0.075f * Mathf.Sin(angle * 3f + seed * 0.71f)
                        + 0.035f * Mathf.Sin(angle * 7f + seed * 1.37f + ring * 0.8f);
                    // Slight front/back compression retains a strong painted
                    // silhouette while remaining unmistakably three-dimensional.
                    float x = spine[ring].x * width
                        + Mathf.Cos(angle) * width * 0.5f * radii[ring] * irregular;
                    float z = spine[ring].y * depth
                        + Mathf.Sin(angle) * depth * 0.5f * radii[ring]
                        * (1f + 0.045f * Mathf.Cos(angle * 5f + seed));
                    vertices.Add(new Vector3(x, heights[ring] * height, z));
                }
            }

            for (int ring = 0; ring < ringCount - 1; ring++)
            {
                int current = ring * radialSegments;
                int next = (ring + 1) * radialSegments;
                for (int segment = 0; segment < radialSegments; segment++)
                {
                    int n = (segment + 1) % radialSegments;
                    triangles.Add(current + segment);
                    triangles.Add(next + segment);
                    triangles.Add(next + n);
                    triangles.Add(current + segment);
                    triangles.Add(next + n);
                    triangles.Add(current + n);
                }
            }

            int summit = vertices.Count;
            Vector2 summitOffset = spine[ringCount - 1];
            vertices.Add(new Vector3(summitOffset.x * width + width * 0.035f,
                height, summitOffset.y * depth - depth * 0.02f));
            int topRing = (ringCount - 1) * radialSegments;
            for (int segment = 0; segment < radialSegments; segment++)
            {
                int n = (segment + 1) % radialSegments;
                triangles.Add(topRing + segment);
                triangles.Add(summit);
                triangles.Add(topRing + n);
            }

            int bottomCenter = vertices.Count;
            vertices.Add(Vector3.zero);
            for (int segment = 0; segment < radialSegments; segment++)
            {
                int n = (segment + 1) % radialSegments;
                triangles.Add(bottomCenter);
                triangles.Add(n);
                triangles.Add(segment);
            }

            return Build("CeladonSculptedMountain", vertices, triangles);
        }

        public static Mesh CreateUpturnedPagodaRoof(
            float width, float depth, float rise, float thickness, int segments)
        {
            if (width <= 0f || depth <= 0f || rise <= 0f || thickness <= 0f)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (segments < 8 || segments % 4 != 0)
                throw new ArgumentOutOfRangeException(nameof(segments));

            const int perimeterCount = 8;
            var perimeter = new[]
            {
                new Vector2(0f, -0.5f), new Vector2(0.5f, -0.5f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(-0.5f, 0.5f),
                new Vector2(-0.5f, 0f), new Vector2(-0.5f, -0.5f),
            };
            var vertices = new List<Vector3>(perimeterCount * 4 + 2);
            var triangles = new List<int>(perimeterCount * 18);

            for (int i = 0; i < perimeterCount; i++)
            {
                bool corner = (i & 1) == 1;
                float cornerLift = corner ? rise * 0.20f : 0f;
                float ox = perimeter[i].x * width;
                float oz = perimeter[i].y * depth;
                float ix = ox * 0.40f;
                float iz = oz * 0.40f;
                vertices.Add(new Vector3(ox, cornerLift, oz));
                vertices.Add(new Vector3(ix, rise * 0.58f, iz));
                vertices.Add(new Vector3(ox, cornerLift - thickness, oz));
                vertices.Add(new Vector3(ix, rise * 0.58f - thickness, iz));
            }

            // The additions above are interleaved; reorder indices through a
            // local accessor rather than allocating a second vertex buffer.
            int V(int ring, int i) => i * 4 + ring;
            for (int i = 0; i < perimeterCount; i++)
            {
                int n = (i + 1) % perimeterCount;
                AddQuad(triangles, V(0, i), V(1, i), V(1, n), V(0, n));
                AddQuad(triangles, V(2, n), V(3, n), V(3, i), V(2, i));
                AddQuad(triangles, V(0, n), V(2, n), V(2, i), V(0, i));
            }

            int topCenter = vertices.Count;
            vertices.Add(new Vector3(0f, rise, 0f));
            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, rise - thickness, 0f));
            for (int i = 0; i < perimeterCount; i++)
            {
                int n = (i + 1) % perimeterCount;
                triangles.Add(V(1, i)); triangles.Add(topCenter); triangles.Add(V(1, n));
                triangles.Add(V(3, n)); triangles.Add(bottomCenter); triangles.Add(V(3, i));
            }

            return Build("CeladonUpturnedPagodaRoof", vertices, triangles);
        }

        /// <summary>A continuous tapered branch following an authored local-space curve.</summary>
        public static Mesh CreateCurvedBranch(IReadOnlyList<Vector3> path,
            float startRadius, float endRadius, int radialSegments)
        {
            if (path == null || path.Count < 2) throw new ArgumentException("A branch needs two path points.", nameof(path));
            if (startRadius <= 0f || endRadius <= 0f) throw new ArgumentOutOfRangeException(nameof(startRadius));
            if (radialSegments < 6) throw new ArgumentOutOfRangeException(nameof(radialSegments));

            var vertices = new List<Vector3>(path.Count * radialSegments + 2);
            var triangles = new List<int>((path.Count - 1) * radialSegments * 6 + radialSegments * 6);
            for (int i = 0; i < path.Count; i++)
            {
                float t = (float)i / (path.Count - 1);
                Vector3 tangent = i == 0 ? path[1] - path[0]
                    : i == path.Count - 1 ? path[i] - path[i - 1]
                    : path[i + 1] - path[i - 1];
                tangent.Normalize();
                Vector3 side = Vector3.Cross(tangent, Mathf.Abs(Vector3.Dot(tangent, Vector3.forward)) > 0.92f
                    ? Vector3.right : Vector3.forward).normalized;
                Vector3 up = Vector3.Cross(side, tangent).normalized;
                float radius = Mathf.Lerp(startRadius, endRadius, t);
                for (int s = 0; s < radialSegments; s++)
                {
                    float angle = s * Mathf.PI * 2f / radialSegments;
                    vertices.Add(path[i] + (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius);
                }
            }

            for (int i = 0; i < path.Count - 1; i++)
            for (int s = 0; s < radialSegments; s++)
            {
                int n = (s + 1) % radialSegments;
                AddQuad(triangles, i * radialSegments + s, (i + 1) * radialSegments + s,
                    (i + 1) * radialSegments + n, i * radialSegments + n);
            }

            int bottom = vertices.Count;
            vertices.Add(path[0]);
            int top = vertices.Count;
            vertices.Add(path[path.Count - 1]);
            for (int s = 0; s < radialSegments; s++)
            {
                int n = (s + 1) % radialSegments;
                triangles.Add(bottom); triangles.Add(n); triangles.Add(s);
                int last = (path.Count - 1) * radialSegments;
                triangles.Add(top); triangles.Add(last + s); triangles.Add(last + n);
            }
            return Build("CeladonCurvedBranch", vertices, triangles);
        }

        /// <summary>Low scalloped cloud crown with an intentional pine-like edge.</summary>
        public static Mesh CreateCloudCanopy(float width, float depth, float height, int lobes)
        {
            if (width <= 0f || depth <= 0f || height <= 0f) throw new ArgumentOutOfRangeException(nameof(width));
            if (lobes < 8) throw new ArgumentOutOfRangeException(nameof(lobes));
            int rings = 4;
            var vertices = new List<Vector3>(rings * lobes + 2);
            var triangles = new List<int>();
            float[] ys = { 0f, height * 0.28f, height * 0.72f, height };
            float[] rs = { 0.72f, 1f, 0.82f, 0.34f };
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < lobes; s++)
            {
                float a = s * Mathf.PI * 2f / lobes;
                float scallop = 1f + 0.10f * Mathf.Sin(a * 5f) + 0.04f * Mathf.Sin(a * 9f + 0.7f);
                vertices.Add(new Vector3(Mathf.Cos(a) * width * 0.5f * rs[r] * scallop,
                    ys[r], Mathf.Sin(a) * depth * 0.5f * rs[r] * scallop));
            }
            for (int r = 0; r < rings - 1; r++)
            for (int s = 0; s < lobes; s++)
            {
                int n = (s + 1) % lobes;
                AddQuad(triangles, r * lobes + s, (r + 1) * lobes + s,
                    (r + 1) * lobes + n, r * lobes + n);
            }
            int top = vertices.Count; vertices.Add(new Vector3(0f, height * 1.08f, 0f));
            int lastRing = (rings - 1) * lobes;
            for (int s = 0; s < lobes; s++)
            {
                int n = (s + 1) % lobes;
                triangles.Add(lastRing + s); triangles.Add(top); triangles.Add(lastRing + n);
            }
            int bottom = vertices.Count; vertices.Add(Vector3.zero);
            for (int s = 0; s < lobes; s++)
            {
                int n = (s + 1) % lobes;
                triangles.Add(bottom); triangles.Add(n); triangles.Add(s);
            }
            return Build("CeladonCloudCanopy", vertices, triangles);
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        private static Mesh Build(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > ushort.MaxValue)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
#endif
