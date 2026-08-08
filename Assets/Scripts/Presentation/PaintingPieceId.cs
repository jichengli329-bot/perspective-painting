using System;
using System.Collections.Generic;
using UnityEngine;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Tags one direct scenery-piece root with a packed 24-bit RGB ID for
    /// <see cref="PaintingCompositionEvaluator"/>'s machine-readable ID
    /// rendering. The ID is serialized as an int, exposed as a uint in
    /// 1..0xFFFFFF (black is the background color and never a valid ID), and
    /// <see cref="Configure"/> caches every <see cref="Renderer"/> below the
    /// root — including inactive children — so sampling never searches the
    /// scene. The deterministic editor scene builder attaches one of these
    /// to each piece root and calls <see cref="Configure"/>.
    /// </summary>
    public sealed class PaintingPieceId : MonoBehaviour
    {
        [SerializeField, Range(1, 0xFFFFFF)] private int _id = 1;

        private Renderer[] _renderers = Array.Empty<Renderer>();

        /// <summary>Packed 24-bit RGB ID in 1..0xFFFFFF; never black.</summary>
        public uint Id => (uint)_id;

        /// <summary>Renderers below this root, including inactive ones; empty until configured.</summary>
        public IReadOnlyList<Renderer> Renderers => _renderers;

        private void Awake()
        {
            Configure(_id);
        }

        /// <summary>
        /// Sets the packed RGB ID and caches every renderer below this root,
        /// including inactive children. Throws when the ID is out of range or
        /// when no renderer exists anywhere below the root, so a miswired
        /// piece fails loudly at configuration time instead of producing
        /// blank ID pixels at runtime.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is outside 1..0xFFFFFF.</exception>
        /// <exception cref="InvalidOperationException">No renderer exists below this root.</exception>
        public void Configure(int id)
        {
            if (id < 1 || id > 0x00FFFFFF)
                throw new ArgumentOutOfRangeException(nameof(id), id, "Piece ID must be within 1..0xFFFFFF; black (0) is the background color.");

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException($"PaintingPieceId on '{name}' has no Renderer below its root, so it cannot be drawn in the machine-readable ID view.");

            _id = id;
            _renderers = renderers;
        }
    }
}
