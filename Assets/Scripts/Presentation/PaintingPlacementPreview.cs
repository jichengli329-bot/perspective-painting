using System;
using System.Collections.Generic;
using UnityEngine;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// T-010G1A: hidden runtime placement preview for one
    /// <see cref="PaintingManipulablePiece"/>, built by <see cref="Create"/>
    /// from every source descendant MeshFilter's sharedMesh plus its
    /// associated MeshRenderer. Each clone keeps its position, rotation and
    /// scale relative to the source root, so showing the preview at the
    /// source's pose makes the meshes coincide exactly; the source
    /// hierarchy, its materials and its property blocks are never mutated.
    /// Every preview renderer draws with exactly the configured shared
    /// preview material, switched between the valid and invalid materials by
    /// <see cref="SetValid(bool)"/>. Preview objects live on the Default
    /// layer, carry no collider or Rigidbody, stay inactive until
    /// <see cref="Show"/> and are destroyed by <see cref="Dispose"/>.
    /// </summary>
    public sealed class PaintingPlacementPreview : MonoBehaviour
    {
        private const string PreviewRootName = "PaintingPlacementPreview (Runtime)";
        private const int DefaultLayer = 0;

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private Material _validMaterial;
        private Material _invalidMaterial;
        private bool _visible;
        private bool _disposed;

        /// <summary>True while the preview root is active; false after <see cref="Hide"/> or <see cref="Dispose"/>.</summary>
        public bool IsVisible => _visible;

        /// <summary>Material state last applied by <see cref="SetValid(bool)"/> or <see cref="Show"/>.</summary>
        public bool IsValid { get; private set; }

        /// <summary>Root transform of the preview; its world pose is the placement pose.</summary>
        public Transform Root => transform;

        /// <summary>
        /// Builds one hidden runtime preview root: for every MeshFilter below
        /// <paramref name="source"/>, clones its sharedMesh into a new node
        /// with an associated MeshRenderer and copies the mesh's pose
        /// relative to the source root, including relative position, rotation
        /// and scale. Scripts, colliders and source material instances are
        /// never cloned; all preview renderers start on
        /// <paramref name="validMaterial"/>. The preview root is inactive
        /// until <see cref="Show"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Null source or preview material.</exception>
        /// <exception cref="InvalidOperationException">The source has no MeshFilter below its root.</exception>
        public static PaintingPlacementPreview Create(
            PaintingManipulablePiece source,
            Material validMaterial,
            Material invalidMaterial)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (validMaterial == null)
                throw new ArgumentNullException(nameof(validMaterial));
            if (invalidMaterial == null)
                throw new ArgumentNullException(nameof(invalidMaterial));

            MeshFilter[] filters = source.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0)
                throw new InvalidOperationException(
                    $"PaintingPlacementPreview: '{source.name}' has no MeshFilter below its root.");

            var root = new GameObject(PreviewRootName) { layer = DefaultLayer };
            var preview = root.AddComponent<PaintingPlacementPreview>();
            preview._validMaterial = validMaterial;
            preview._invalidMaterial = invalidMaterial;

            Transform rootTransform = root.transform;
            Matrix4x4 sourceWorldToLocal = source.transform.worldToLocalMatrix;
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                MeshRenderer meshRenderer = filter.GetComponent<MeshRenderer>();
                if (meshRenderer == null || filter.sharedMesh == null)
                    continue;

                var node = new GameObject(filter.name + " (Preview)") { layer = DefaultLayer };
                node.transform.SetParent(rootTransform, false);

                // The source mesh's pose expressed in the source root's local
                // space; the preview root starts at identity, so the clone
                // node keeps it 1:1 and Show reproduces it exactly.
                Matrix4x4 relative = sourceWorldToLocal * filter.transform.localToWorldMatrix;
                node.transform.localPosition = relative.GetPosition();
                node.transform.localRotation = relative.rotation;
                node.transform.localScale = relative.lossyScale;

                var clonedFilter = node.AddComponent<MeshFilter>();
                clonedFilter.sharedMesh = filter.sharedMesh;
                var clonedRenderer = node.AddComponent<MeshRenderer>();
                clonedRenderer.sharedMaterial = validMaterial;

                preview._renderers.Add(clonedRenderer);
            }

            preview.IsValid = true;
            root.SetActive(false);
            return preview;
        }

        /// <summary>
        /// Applies the given world pose to the preview root, switches every
        /// renderer to the valid or invalid preview material according to
        /// <paramref name="valid"/>, and activates the preview.
        /// </summary>
        /// <exception cref="ObjectDisposedException">After <see cref="Dispose"/>.</exception>
        public void Show(Vector3 worldPosition, Quaternion worldRotation, Vector3 localScale, bool valid)
        {
            EnsureNotDisposed();
            transform.position = worldPosition;
            transform.rotation = worldRotation;
            transform.localScale = localScale;
            SetValid(valid);
            gameObject.SetActive(true);
            _visible = true;
        }

        /// <summary>Deactivates the preview root. Idempotent.</summary>
        public void Hide()
        {
            if (_disposed)
                return;
            gameObject.SetActive(false);
            _visible = false;
        }

        /// <summary>
        /// Switches every preview renderer between the configured valid and
        /// invalid shared preview materials.
        /// </summary>
        /// <exception cref="ObjectDisposedException">After <see cref="Dispose"/>.</exception>
        public void SetValid(bool valid)
        {
            EnsureNotDisposed();
            Material material = valid ? _validMaterial : _invalidMaterial;
            for (int i = 0; i < _renderers.Count; i++)
                _renderers[i].sharedMaterial = material;
            IsValid = valid;
        }

        /// <summary>
        /// Destroys the preview root and everything below it — deferred in
        /// play mode, immediate in editor mode — so the preview never
        /// survives disposal. No-op after the first call; idempotent.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _visible = false;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(gameObject);
                return;
            }
#endif
            Destroy(gameObject);
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PaintingPlacementPreview));
        }
    }
}
