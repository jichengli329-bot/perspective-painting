using System;
using NUnit.Framework;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Tests
{
    public sealed class PlacementLatticeTests
    {
        [Test]
        public void SnapClampsOvershootAndUsesNearestCell()
        {
            var lattice = new PlacementLattice(5, 4, -4f, 4f, -3f, 3f);
            PlacementLatticePoint low = lattice.Snap(-100f, -100f);
            PlacementLatticePoint middle = lattice.Snap(0.8f, 1.2f);
            PlacementLatticePoint high = lattice.Snap(100f, 100f);

            Assert.AreEqual(0, low.Column);
            Assert.AreEqual(0, low.DepthRow);
            Assert.AreEqual(2, middle.Column);
            Assert.AreEqual(2, middle.DepthRow);
            Assert.AreEqual(4, high.Column);
            Assert.AreEqual(3, high.DepthRow);
        }

        [Test]
        public void AtSpansInclusiveTableBounds()
        {
            var lattice = new PlacementLattice(3, 3, -2f, 2f, -6f, 0f);
            Assert.AreEqual(-2f, lattice.At(0, 0).X);
            Assert.AreEqual(-6f, lattice.At(0, 0).Z);
            Assert.AreEqual(0f, lattice.At(1, 1).X);
            Assert.AreEqual(-3f, lattice.At(1, 1).Z);
            Assert.AreEqual(2f, lattice.At(2, 2).X);
            Assert.AreEqual(0f, lattice.At(2, 2).Z);
        }

        [Test]
        public void DepthSteppingMovesExactlyOneRowAndStopsAtEdges()
        {
            var lattice = new PlacementLattice(4, 3, -1f, 1f, -1f, 1f);
            Assert.AreEqual(1, lattice.StepDepthRow(0, 1));
            Assert.AreEqual(2, lattice.StepDepthRow(1, 99));
            Assert.AreEqual(2, lattice.StepDepthRow(2, 1));
            Assert.AreEqual(0, lattice.StepDepthRow(0, -1));
            Assert.AreEqual(1, lattice.StepDepthRow(2, -1));
        }

        [Test]
        public void PieceAnchoredSnapAlwaysKeepsAuthoredSolutionOnGrid()
        {
            var lattice = new PlacementLattice(5, 3, -4f, 4f, -3f, 3f);
            PlacementLatticePoint authored = lattice.SnapAround(0.37f, -0.42f, 0.37f, -0.42f);
            PlacementLatticePoint neighbor = lattice.SnapAround(0.37f, -0.42f, 2.2f, 2.7f);
            Assert.AreEqual(0.37f, authored.X, 0.0001f);
            Assert.AreEqual(-0.42f, authored.Z, 0.0001f);
            Assert.AreEqual(2.37f, neighbor.X, 0.0001f);
            Assert.AreEqual(2.58f, neighbor.Z, 0.0001f);
        }

        [Test]
        public void InvalidLayoutsFailLoudly()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlacementLattice(1, 3, 0f, 1f, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlacementLattice(3, 1, 0f, 1f, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlacementLattice(3, 3, 1f, 1f, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlacementLattice(3, 3, 0f, 1f, 2f, 1f));
        }
    }
}
