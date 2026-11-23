using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.People
{
    /// <summary>What a manager is to be judged on (spec 028 §3.2): the season's profit, or its sales.</summary>
    public enum ManagerTarget
    {
        Profit = 0,
        Growth = 1,
    }

    /// <summary>
    /// What makes a <see cref="Worker"/> a manager (spec 028 §3.2, save schema 23): his style, the target you set, the days of this season
    /// he mis-set, how often he missed in a row, and the report of the last close. A manager is hired, paid, renewed and asked for a raise
    /// like any worker (spec 015); he runs the business where a worker works it.
    /// </summary>
    public sealed class ManagerRecord
    {
        /// <summary>Index into <see cref="Definitions.ManagersDef.Styles"/> (the list only ever grows).</summary>
        public int Style { get; internal set; }
        public ManagerTarget Target { get; internal set; }

        /// <summary>Misses in a row (two lower his motivation).</summary>
        public int Misses { get; internal set; }

        /// <summary>Days of this season he has run, and the ones he mis-set; whether today is one.</summary>
        public int Days { get; internal set; }
        public int MisDays { get; internal set; }
        public bool MisToday { get; internal set; }

        /// <summary>The day (t ÷ day) he last set things, so a catch-up never sets one day twice.</summary>
        public long SetDay { get; internal set; } = -1;

        // The last close's report (period 0: none yet).
        public long ReportPeriod { get; internal set; }
        public ManagerTarget ReportTarget { get; internal set; }
        public long ReportGoal { get; internal set; }
        public long ReportResult { get; internal set; }
        public bool ReportMet { get; internal set; }
        public int ReportDays { get; internal set; }
        public int ReportMisDays { get; internal set; }

        /// <summary>The profit share he was paid for that season, and what he asked for after it (a raise), 0 when none.</summary>
        public long ReportShare { get; internal set; }

        internal ManagerRecord Clone() { return (ManagerRecord)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Style); h.WriteInt((int)Target); h.WriteInt(Misses); h.WriteInt(Days); h.WriteInt(MisDays); h.WriteBool(MisToday); h.WriteLong(SetDay);
            h.WriteLong(ReportPeriod); h.WriteInt((int)ReportTarget); h.WriteLong(ReportGoal); h.WriteLong(ReportResult); h.WriteBool(ReportMet);
            h.WriteInt(ReportDays); h.WriteInt(ReportMisDays); h.WriteLong(ReportShare);
        }

        internal static ManagerRecord ReadFrom(SaveReader r)
        {
            return new ManagerRecord
            {
                Style = r.ReadInt(), Target = (ManagerTarget)r.ReadInt(), Misses = r.ReadInt(), Days = r.ReadInt(), MisDays = r.ReadInt(), MisToday = r.ReadBool(), SetDay = r.ReadLong(),
                ReportPeriod = r.ReadLong(), ReportTarget = (ManagerTarget)r.ReadInt(), ReportGoal = r.ReadLong(), ReportResult = r.ReadLong(), ReportMet = r.ReadBool(),
                ReportDays = r.ReadInt(), ReportMisDays = r.ReadInt(), ReportShare = r.ReadLong(),
            };
        }
    }
}
