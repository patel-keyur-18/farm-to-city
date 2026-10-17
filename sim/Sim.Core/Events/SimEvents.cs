using Sim.Core.Time;

namespace Sim.Core.Events
{
    /// <summary>
    /// A domain event emitted by the simulation (spec 001 §5). The UI reacts to events and reads state;
    /// it never changes state directly.
    /// </summary>
    public abstract class SimEvent
    {
        /// <summary>The sim time the event happened at.</summary>
        public long SimTime { get; }

        protected SimEvent(long simTime) { SimTime = simTime; }

        /// <summary>The event name and payload, e.g. <c>DayStarted(day=1, ...)</c>.</summary>
        protected abstract string Describe();

        /// <summary>Canonical text form, used by determinism tests to compare event lists.</summary>
        public override string ToString() { return SimTime + " " + Describe(); }
    }

    /// <summary>At every hour boundary. Internal to systems; not shown in the UI.</summary>
    public sealed class HourStarted : SimEvent
    {
        public HourStarted(long simTime) : base(simTime) { }
        protected override string Describe() { return "HourStarted"; }
    }

    public sealed class DayStarted : SimEvent
    {
        public long Day { get; }
        public int DayOfSeason { get; }
        public Season Season { get; }
        public long Year { get; }

        public DayStarted(long simTime, long day, int dayOfSeason, Season season, long year) : base(simTime)
        {
            Day = day;
            DayOfSeason = dayOfSeason;
            Season = season;
            Year = year;
        }

        protected override string Describe()
        {
            return "DayStarted(day=" + Day + ", dayOfSeason=" + DayOfSeason + ", season=" + Season + ", year=" + Year + ")";
        }
    }

    public sealed class SeasonStarted : SimEvent
    {
        public Season Season { get; }
        public long Year { get; }

        public SeasonStarted(long simTime, Season season, long year) : base(simTime)
        {
            Season = season;
            Year = year;
        }

        protected override string Describe() { return "SeasonStarted(season=" + Season + ", year=" + Year + ")"; }
    }

    public sealed class YearStarted : SimEvent
    {
        public long Year { get; }

        public YearStarted(long simTime, long year) : base(simTime) { Year = year; }

        protected override string Describe() { return "YearStarted(year=" + Year + ")"; }
    }

    public sealed class WeatherSpellStarted : SimEvent
    {
        public long SpellIndex { get; }
        public string WeatherId { get; }

        public WeatherSpellStarted(long simTime, long spellIndex, string weatherId) : base(simTime)
        {
            SpellIndex = spellIndex;
            WeatherId = weatherId;
        }

        protected override string Describe() { return "WeatherSpellStarted(spell=" + SpellIndex + ", weather=" + WeatherId + ")"; }
    }

    /// <summary>A spell moved to a nearer forecast bucket and its shown weather changed.</summary>
    public sealed class ForecastRevised : SimEvent
    {
        public long SpellIndex { get; }
        public string OldWeatherId { get; }
        public string NewWeatherId { get; }

        public ForecastRevised(long simTime, long spellIndex, string oldWeatherId, string newWeatherId) : base(simTime)
        {
            SpellIndex = spellIndex;
            OldWeatherId = oldWeatherId;
            NewWeatherId = newWeatherId;
        }

        protected override string Describe()
        {
            return "ForecastRevised(spell=" + SpellIndex + ", old=" + OldWeatherId + ", new=" + NewWeatherId + ")";
        }
    }

    /// <summary>The end of an away advance; the source of the While-you-were-away report.</summary>
    public sealed class AwayAdvanceCompleted : SimEvent
    {
        public long FromSimTime { get; }
        public long ToSimTime { get; }
        public long RequestedSeconds { get; }
        public long AppliedSeconds { get; }
        public bool Capped { get; }

        public AwayAdvanceCompleted(long simTime, long fromSimTime, long toSimTime, long requestedSeconds, long appliedSeconds, bool capped)
            : base(simTime)
        {
            FromSimTime = fromSimTime;
            ToSimTime = toSimTime;
            RequestedSeconds = requestedSeconds;
            AppliedSeconds = appliedSeconds;
            Capped = capped;
        }

        protected override string Describe()
        {
            return "AwayAdvanceCompleted(from=" + FromSimTime + ", to=" + ToSimTime + ", requested=" + RequestedSeconds +
                ", applied=" + AppliedSeconds + ", capped=" + (Capped ? "true" : "false") + ")";
        }
    }

    /// <summary><c>Sync</c> saw the wall clock go backwards. No time is granted.</summary>
    public sealed class ClockAnomalyDetected : SimEvent
    {
        public long LastWallUtc { get; }
        public long NowWallUtc { get; }

        public ClockAnomalyDetected(long simTime, long lastWallUtc, long nowWallUtc) : base(simTime)
        {
            LastWallUtc = lastWallUtc;
            NowWallUtc = nowWallUtc;
        }

        protected override string Describe() { return "ClockAnomalyDetected(lastWall=" + LastWallUtc + ", nowWall=" + NowWallUtc + ")"; }
    }
}
