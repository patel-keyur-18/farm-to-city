using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Town
{
    public enum ClinicStatus
    {
        None = 0,
        Building = 1,
        Open = 2,
    }

    public enum QuartersStatus
    {
        None = 0,
        Building = 1,
        Built = 2,
    }

    public enum DoctorStatus
    {
        /// <summary>Not yet: the clinic and the quarters aren't both standing.</summary>
        None = 0,

        /// <summary>Both stand; he arrives at <see cref="ClinicState.ArrivesAt"/>.</summary>
        Coming = 1,
        InPost = 2,

        /// <summary>He left at the Township's third unpaid close (spec 023 §3.9); a new one comes when Health recovers.</summary>
        Left = 3,
    }

    /// <summary>
    /// The village clinic, the doctor's quarters and the doctor (spec 023 §3.1, §3.9; save schema 20). The district builds the clinic for your
    /// share (a gift); the quarters are yours, an asset. The doctor is one of your citizens with a household in the quarters.
    /// </summary>
    public sealed class ClinicState
    {
        public ClinicStatus Status { get; internal set; }
        public long StartedAt { get; internal set; }
        public long ReadyAt { get; internal set; }
        public long JobId { get; internal set; }

        /// <summary>The share given, indexed at the press.</summary>
        public long Share { get; internal set; }

        public QuartersStatus Quarters { get; internal set; }
        public long QuartersReadyAt { get; internal set; }
        public long QuartersJobId { get; internal set; }

        /// <summary>The cost, indexed at the press, and the building it became (0 until it stands).</summary>
        public long QuartersCost { get; internal set; }
        public long QuartersId { get; internal set; }

        public DoctorStatus Doctor { get; internal set; }

        /// <summary>When the doctor (or his successor) arrives.</summary>
        public long ArrivesAt { get; internal set; }
        public long ArrivalJobId { get; internal set; }

        /// <summary>The doctor's household while he is in post; 0 otherwise.</summary>
        public long HouseholdId { get; internal set; }

        /// <summary>The doctor's name (an index into the names, as a citizen's) kept after he leaves, for the sheet and the letter.</summary>
        public int DoctorName { get; internal set; }

        /// <summary>The season he arrived, and how many doctors have come.</summary>
        public long SinceSeason { get; internal set; }
        public int Doctors { get; internal set; }

        /// <summary>Season closes in a row at the Township with Health's condition under the treasury's <c>healthLeaveBelow</c> (spec 023 §3.9).</summary>
        public int UnderCloses { get; internal set; }

        /// <summary>The clinic's benefit, this season and last (spec 023 §3.2): each an exact count against what the same roll would have done without it.</summary>
        public int SickAvoidedThis { get; internal set; }
        public int SickAvoidedLast { get; internal set; }
        public int RegainedThis { get; internal set; }
        public int RegainedLast { get; internal set; }
        public int EldersWellThis { get; internal set; }
        public int EldersWellLast { get; internal set; }

        internal bool IsDefault { get { return Status == ClinicStatus.None && Quarters == QuartersStatus.None && Doctor == DoctorStatus.None; } }

        internal ClinicState Clone() { return (ClinicState)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt((int)Status);
            h.WriteLong(StartedAt);
            h.WriteLong(ReadyAt);
            h.WriteLong(JobId);
            h.WriteLong(Share);
            h.WriteInt((int)Quarters);
            h.WriteLong(QuartersReadyAt);
            h.WriteLong(QuartersJobId);
            h.WriteLong(QuartersCost);
            h.WriteLong(QuartersId);
            h.WriteInt((int)Doctor);
            h.WriteLong(ArrivesAt);
            h.WriteLong(ArrivalJobId);
            h.WriteLong(HouseholdId);
            h.WriteInt(DoctorName);
            h.WriteLong(SinceSeason);
            h.WriteInt(Doctors);
            h.WriteInt(UnderCloses);
            h.WriteInt(SickAvoidedThis); h.WriteInt(SickAvoidedLast);
            h.WriteInt(RegainedThis); h.WriteInt(RegainedLast);
            h.WriteInt(EldersWellThis); h.WriteInt(EldersWellLast);
        }

        internal static ClinicState ReadFrom(SaveReader r)
        {
            return new ClinicState
            {
                Status = (ClinicStatus)r.ReadInt(), StartedAt = r.ReadLong(), ReadyAt = r.ReadLong(), JobId = r.ReadLong(), Share = r.ReadLong(),
                Quarters = (QuartersStatus)r.ReadInt(), QuartersReadyAt = r.ReadLong(), QuartersJobId = r.ReadLong(), QuartersCost = r.ReadLong(), QuartersId = r.ReadLong(),
                Doctor = (DoctorStatus)r.ReadInt(), ArrivesAt = r.ReadLong(), ArrivalJobId = r.ReadLong(), HouseholdId = r.ReadLong(), DoctorName = r.ReadInt(),
                SinceSeason = r.ReadLong(), Doctors = r.ReadInt(), UnderCloses = r.ReadInt(),
                SickAvoidedThis = r.ReadInt(), SickAvoidedLast = r.ReadInt(), RegainedThis = r.ReadInt(), RegainedLast = r.ReadInt(),
                EldersWellThis = r.ReadInt(), EldersWellLast = r.ReadInt(),
            };
        }
    }
}
