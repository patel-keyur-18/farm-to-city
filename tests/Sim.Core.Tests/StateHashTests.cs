using NUnit.Framework;
using Sim.Core.Maths;

namespace Sim.Core.Tests
{
    public class StateHashTests
    {
        // Reference values from the FNV-1a 64 definition, computed independently in Python.
        [Test]
        public void Fnv1a_MatchesReferenceValues()
        {
            Assert.That(new StateHasher().Value, Is.EqualTo(0xCBF29CE484222325UL));

            var a = new StateHasher();
            a.WriteByte((byte)'a');
            Assert.That(a.Value, Is.EqualTo(0xAF63DC4C8601EC8CUL));

            var foobar = new StateHasher();
            foreach (char c in "foobar") foobar.WriteByte((byte)c);
            Assert.That(foobar.Value, Is.EqualTo(0x85944171F73967E8UL));

            var minusTwo = new StateHasher();
            minusTwo.WriteLong(-2); // little-endian bytes FE FF FF FF FF FF FF FF
            Assert.That(minusTwo.Value, Is.EqualTo(0xFC1A35225397861CUL));
        }

        [Test]
        public void GameState_HashCoversClockAndSeed_AndCloneMatches()
        {
            var s = new GameState { Seed = 42 };
            s.Clock.SimTime = 100;
            s.Clock.LastWallUtc = 1_800_000_000;
            ulong h = s.ComputeHash();

            GameState copy = s.Clone();
            Assert.That(copy.ComputeHash(), Is.EqualTo(h));

            copy.Clock.SimTime = 101;
            Assert.That(copy.ComputeHash(), Is.Not.EqualTo(h));
            Assert.That(s.ComputeHash(), Is.EqualTo(h), "the clone is a deep copy");

            var other = s.Clone();
            other.Seed = 43;
            Assert.That(other.ComputeHash(), Is.Not.EqualTo(h));

            var away = s.Clone();
            away.Clock.IsAway = true;
            Assert.That(away.ComputeHash(), Is.Not.EqualTo(h));
        }
    }
}
