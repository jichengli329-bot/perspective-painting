using System;

namespace PerspectivePuzzle.Domain
{
    /// <summary>
    /// Immutable 2D buffer of packed 24-bit RGB piece IDs produced from a
    /// rendered composition. Each pixel holds one piece color
    /// (<c>0xRRGGBB</c>); black (<c>0x000000</c>) is the background. The
    /// internal storage is never exposed and cannot change after
    /// construction.
    /// </summary>
    public sealed class CompositionIdBuffer
    {
        private const uint MaxRgbValue = 0x00FFFFFF;

        private readonly uint[] _pixels;

        /// <summary>Extent along the X axis. Always positive.</summary>
        public int Width { get; }

        /// <summary>Extent along the Y axis. Always positive.</summary>
        public int Height { get; }

        /// <summary>Total number of pixels (width x height).</summary>
        public int PixelCount => _pixels.Length;

        /// <summary>
        /// Creates an immutable buffer from row-major X/Y pixel data. The
        /// input is copied, so later changes to the caller's array cannot
        /// change the buffer.
        /// </summary>
        public static CompositionIdBuffer FromPixels(int width, int height, uint[] pixels)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Buffer width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Buffer height must be positive.");
            if (pixels == null)
                throw new ArgumentNullException(nameof(pixels));
            if (pixels.Length != width * height)
                throw new ArgumentException("Pixel array length must equal width x height.", nameof(pixels));

            var copy = new uint[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i] > MaxRgbValue)
                    throw new ArgumentOutOfRangeException(nameof(pixels), pixels[i], "Pixel value must fit in 24-bit RGB (0x000000..0xFFFFFF).");
                copy[i] = pixels[i];
            }

            return new CompositionIdBuffer(width, height, copy);
        }

        private CompositionIdBuffer(int width, int height, uint[] pixels)
        {
            Width = width;
            Height = height;
            _pixels = pixels;
        }

        /// <summary>
        /// Reads the packed RGB ID at the given coordinates (0 is
        /// background). Out-of-bounds coordinates throw.
        /// </summary>
        public uint GetPixel(int x, int y)
        {
            if (x < 0 || x >= Width)
                throw new ArgumentOutOfRangeException(nameof(x), x, "Pixel X coordinate must be within the buffer width.");
            if (y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException(nameof(y), y, "Pixel Y coordinate must be within the buffer height.");

            return _pixels[y * Width + x];
        }

        /// <summary>
        /// Reads the packed RGB ID at the given flat row-major index. Only
        /// <see cref="CompositionScorer"/> uses this accessor.
        /// </summary>
        internal uint GetPixelAt(int index)
        {
            if (index < 0 || index >= _pixels.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Pixel index must be within the buffer.");

            return _pixels[index];
        }
    }
}
