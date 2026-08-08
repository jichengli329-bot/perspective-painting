#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Rendering;

namespace PerspectivePuzzle.EditorTools
{
    /// <summary>
    /// Builds a box with rounded edges and corners. Each of the six faces is subdivided
    /// into faceSegments x faceSegments quads and indexed independently (no vertices are
    /// shared between faces), with per-face 0..1 UVs.
    /// </summary>
    public static class RoundedBoxMeshFactory
    {
        private const int FaceCount = 6;

        /// <summary>
        /// Creates a rounded box mesh named "RoundedBox".
        /// </summary>
        /// <param name="size">Outer size of the box; every component must be positive.</param>
        /// <param name="radius">Rounding radius; must be &gt; 0 and &lt;= half the smallest size component.</param>
        /// <param name="faceSegments">Subdivisions per face edge; must be &gt;= 1.</param>
        public static Mesh Create(Vector3 size, float radius, int faceSegments)
        {
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f)
                throw new System.ArgumentException("All size components must be positive.", nameof(size));

            if (faceSegments < 1)
                throw new System.ArgumentOutOfRangeException(nameof(faceSegments), "faceSegments must be >= 1.");

            float halfSmallest = Mathf.Min(Mathf.Min(size.x, size.y), size.z) * 0.5f;
            if (radius <= 0f || radius > halfSmallest)
                throw new System.ArgumentOutOfRangeException(nameof(radius),
                    "radius must be > 0 and <= half the smallest size (" + halfSmallest + ").");

            Vector3 half = size * 0.5f;
            Vector3 inner = half - Vector3.one * radius; // half extents of the flat region

            int perEdge = faceSegments + 1;
            int vertexCount = FaceCount * perEdge * perEdge;
            int triangleCount = FaceCount * faceSegments * faceSegments * 2;

            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var triangles = new int[triangleCount * 3];

            // Per-face grid: origin corner, first grid axis (u), second grid axis (v).
            // Orientation is chosen so that u x v points out of the face, which makes the
            // quad triangulation below consistently outward-facing.
            var origins = new[]
            {
                new Vector3( half.x, -half.y, -half.z), // +X
                new Vector3(-half.x, -half.y,  half.z), // -X
                new Vector3(-half.x,  half.y,  half.z), // +Y
                new Vector3(-half.x, -half.y, -half.z), // -Y
                new Vector3(-half.x, -half.y,  half.z), // +Z
                new Vector3( half.x, -half.y, -half.z), // -Z
            };
            var uAxes = new[]
            {
                new Vector3(0f,   size.y, 0f),     // +X face
                new Vector3(0f,   size.y, 0f),     // -X face
                new Vector3(size.x, 0f, 0f),       // +Y face
                new Vector3(size.x, 0f, 0f),       // -Y face
                new Vector3(size.x, 0f, 0f),       // +Z face
                new Vector3(-size.x, 0f, 0f),      // -Z face
            };
            var vAxes = new[]
            {
                new Vector3(0f, 0f,   size.z),     // +X face
                new Vector3(0f, 0f,  -size.z),     // -X face
                new Vector3(0f, 0f,  -size.z),     // +Y face
                new Vector3(0f, 0f,   size.z),     // -Y face
                new Vector3(0f,   size.y, 0f),     // +Z face
                new Vector3(0f,   size.y, 0f),     // -Z face
            };

            int vertexIndex = 0;
            int triangleIndex = 0;

            for (int face = 0; face < FaceCount; face++)
            {
                Vector3 origin = origins[face];
                Vector3 uAxis = uAxes[face];
                Vector3 vAxis = vAxes[face];
                int faceBase = vertexIndex;

                for (int iu = 0; iu <= faceSegments; iu++)
                {
                    float u = (float)iu / faceSegments;
                    for (int iv = 0; iv <= faceSegments; iv++)
                    {
                        float v = (float)iv / faceSegments;

                        // p lies on the surface of the unrounded box; push it toward the
                        // rounded surface: clamp to the flat region, then extrude by the
                        // radius along the direction from the clamped point to p.
                        Vector3 p = origin + uAxis * u + vAxis * v;
                        Vector3 clamped = new Vector3(
                            Mathf.Clamp(p.x, -inner.x, inner.x),
                            Mathf.Clamp(p.y, -inner.y, inner.y),
                            Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 normal = (p - clamped).normalized; // never zero: p is outside the inner box

                        vertices[vertexIndex] = clamped + normal * radius;
                        normals[vertexIndex] = normal;
                        uvs[vertexIndex] = new Vector2(u, v);
                        vertexIndex++;
                    }
                }

                // Two triangles per quad, wound so the geometric normal matches u x v (outward).
                for (int iu = 0; iu < faceSegments; iu++)
                {
                    for (int iv = 0; iv < faceSegments; iv++)
                    {
                        int a = faceBase + iu * perEdge + iv; // (iu,     iv)
                        int b = a + perEdge;                  // (iu + 1, iv)
                        int c = a + 1;                        // (iu,     iv + 1)
                        int d = b + 1;                        // (iu + 1, iv + 1)
                        triangles[triangleIndex++] = a;
                        triangles[triangleIndex++] = b;
                        triangles[triangleIndex++] = c;
                        triangles[triangleIndex++] = b;
                        triangles[triangleIndex++] = d;
                        triangles[triangleIndex++] = c;
                    }
                }
            }

            var mesh = new Mesh();
            mesh.name = "RoundedBox";
            if (vertexCount > ushort.MaxValue)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
#endif
