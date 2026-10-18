using Sim.Core.Maths;

namespace Game.Presentation.Phase0
{
    /// <summary>
    /// A stand-in for a later phase's hourly citizen pass (US-021): 2,000 citizens with three needs in Fixed maths, a job
    /// choice and a path request each. Plain struct arrays, as the GDD plans for agents. Throwaway: spike only.
    /// </summary>
    public sealed class CitizenStub
    {
        private struct Citizen
        {
            public Fixed Food, Rest, Joy;
            public int Job, TargetX, TargetY;
        }

        private readonly Citizen[] _c;
        private readonly int _mapSize;
        private ulong _tick;

        public CitizenStub(int count, int mapSize)
        {
            _c = new Citizen[count];
            _mapSize = mapSize;
            for (int i = 0; i < count; i++) _c[i] = new Citizen { Food = Fixed.One, Rest = Fixed.One, Joy = Fixed.One };
        }

        /// <summary>One hourly pass over every citizen.</summary>
        public void Hour()
        {
            Fixed decay = Fixed.FromRatio(3, 100), gain = Fixed.FromRatio(1, 10);
            for (int i = 0; i < _c.Length; i++)
            {
                ref Citizen c = ref _c[i];
                ulong r = Rng.Next(7, (RngStream)990, (ulong)i, _tick);
                c.Food -= decay; c.Rest -= decay.MulRatio(2, 3); c.Joy -= decay.MulRatio(1, 2);
                // The neediest need picks the job: eat, sleep or play, each refilling its need.
                if (c.Food <= c.Rest && c.Food <= c.Joy) { c.Job = 0; c.Food += gain * Fixed.Pow(Fixed.One + c.Food, Fixed.FromRatio(1, 2)); }
                else if (c.Rest <= c.Joy) { c.Job = 1; c.Rest += gain; }
                else { c.Job = 2; c.Joy += gain; }
                // A path request: a target tile for the job.
                c.TargetX = (int)Rng.Below(r, (ulong)_mapSize);
                c.TargetY = (int)Rng.Below(r * 31 + 17, (ulong)_mapSize);
            }
            _tick++;
        }
    }
}
