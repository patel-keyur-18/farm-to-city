namespace Sim.Core.Time
{
    /// <summary>
    /// Keeps the game's time of day equal to the device's (spec 001 §3.2: a new game starts at the device's local time of day, and sim time then runs one second for each wall second).
    /// A town that was made somewhere else (a bot's playtest save, an import, a restored snapshot) can start at another time of day; this says by how much.
    /// </summary>
    public static class ClockAlign
    {
        /// <summary>A difference up to this many seconds is the whole-second rounding of the two clocks, not a drift.</summary>
        public const long ToleranceSeconds = 30;

        /// <summary>The seconds to move the sim forward to reach the device's time of day: 0 to 86 399.</summary>
        public static long Forward(long simTime, long localSecondOfDay)
        {
            long d = (localSecondOfDay - simTime % Day) % Day;
            return d < 0 ? d + Day : d;
        }

        /// <summary>
        /// The nearest way to match: positive is the sim behind the device (it moves forward that many seconds, like an away advance), negative is the sim ahead
        /// (it waits that many seconds, since time never goes backwards). Within <see cref="ToleranceSeconds"/> it is 0.
        /// </summary>
        public static long Offset(long simTime, long localSecondOfDay)
        {
            long f = Forward(simTime, localSecondOfDay);
            if (f > Day / 2) f -= Day;
            return f <= ToleranceSeconds && f >= -ToleranceSeconds ? 0 : f;
        }

        private const long Day = Definitions.ClockDef.SecondsPerDay;
    }
}
