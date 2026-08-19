using System;
using NUnit.Framework;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.EditMode.Tests
{
    public sealed class PaintingGoalGateTests
    {
        [TestCase(false, 1f, false)]
        [TestCase(true, 0.81f, false)]
        [TestCase(true, 0.82f, true)]
        [TestCase(true, 1f, true)]
        public void CompoundGoalRequiresBothViews(bool primary, float secondary, bool expected)
            => Assert.AreEqual(expected, PaintingGoalGate.AreGoalsSatisfied(primary, new[] { secondary }, 0.82f));

        [Test]
        public void SingleGoalMirrorsPrimary()
        {
            Assert.IsTrue(PaintingGoalGate.AreGoalsSatisfied(true, Array.Empty<float>(), 0.82f));
            Assert.IsFalse(PaintingGoalGate.AreGoalsSatisfied(false, Array.Empty<float>(), 0.82f));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => PaintingGoalGate.AreGoalsSatisfied(true, null, 0.82f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PaintingGoalGate.AreGoalsSatisfied(true, Array.Empty<float>(), -0.1f));
        }
    }
}
