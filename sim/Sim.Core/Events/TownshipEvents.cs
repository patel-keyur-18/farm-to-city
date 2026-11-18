namespace Sim.Core.Events
{
    /// <summary>The founder gave the district the road's share (spec 023 §3.3); it builds the road by <see cref="ReadyAt"/>.</summary>
    public sealed class RoadPetitioned : SimEvent
    {
        public long Share { get; }
        public long ReadyAt { get; }

        public RoadPetitioned(long simTime, long share, long readyAt) : base(simTime)
        {
            Share = share;
            ReadyAt = readyAt;
        }

        protected override string Describe() { return "RoadPetitioned(share=" + Share + ", ready=" + ReadyAt + ")"; }
    }

    /// <summary>The paved link road is finished: the road tier is better for every price (spec 023 §3.3).</summary>
    public sealed class RoadBuilt : SimEvent
    {
        public RoadBuilt(long simTime) : base(simTime) { }

        protected override string Describe() { return "RoadBuilt"; }
    }

    /// <summary>You gave the district the clinic's share (spec 023 §3.1); it stands by <see cref="ReadyAt"/>.</summary>
    public sealed class ClinicStarted : SimEvent
    {
        public long Share { get; }
        public long ReadyAt { get; }

        public ClinicStarted(long simTime, long share, long readyAt) : base(simTime)
        {
            Share = share;
            ReadyAt = readyAt;
        }

        protected override string Describe() { return "ClinicStarted(share=" + Share + ", ready=" + ReadyAt + ")"; }
    }

    /// <summary>The clinic stands (spec 023 §3.1). With the quarters built the doctor is on his way.</summary>
    public sealed class ClinicBuilt : SimEvent
    {
        public ClinicBuilt(long simTime) : base(simTime) { }

        protected override string Describe() { return "ClinicBuilt"; }
    }

    /// <summary>You started the doctor's quarters (spec 023 §3.1), an asset of yours at <see cref="Cost"/>.</summary>
    public sealed class QuartersStarted : SimEvent
    {
        public long Cost { get; }
        public long ReadyAt { get; }

        public QuartersStarted(long simTime, long cost, long readyAt) : base(simTime)
        {
            Cost = cost;
            ReadyAt = readyAt;
        }

        protected override string Describe() { return "QuartersStarted(cost=" + Cost + ", ready=" + ReadyAt + ")"; }
    }

    /// <summary>The doctor's quarters stand (spec 023 §3.1).</summary>
    public sealed class QuartersBuilt : SimEvent
    {
        public QuartersBuilt(long simTime) : base(simTime) { }

        protected override string Describe() { return "QuartersBuilt"; }
    }

    /// <summary>The clinic and the quarters both stand: the doctor arrives by <see cref="ArrivesAt"/> (a migrant, or his successor).</summary>
    public sealed class DoctorComing : SimEvent
    {
        public long ArrivesAt { get; }

        public DoctorComing(long simTime, long arrivesAt) : base(simTime) { ArrivesAt = arrivesAt; }

        protected override string Describe() { return "DoctorComing(at=" + ArrivesAt + ")"; }
    }

    /// <summary>The doctor has moved into the quarters and the clinic opens its doors (spec 023 §3.1).</summary>
    public sealed class DoctorArrived : SimEvent
    {
        public long CitizenId { get; }
        public int People { get; }
        public bool Returned { get; }

        public DoctorArrived(long simTime, long citizenId, int people, bool returned) : base(simTime)
        {
            CitizenId = citizenId;
            People = people;
            Returned = returned;
        }

        protected override string Describe() { return "DoctorArrived(citizen=" + CitizenId + ", people=" + People + (Returned ? ", returned" : "") + ")"; }
    }

    /// <summary>Health's allowance went unpaid at <see cref="Closes"/> closes: at the last he leaves (spec 023 §3.9).</summary>
    public sealed class DoctorUnpaid : SimEvent
    {
        public int Closes { get; }
        public int Of { get; }

        public DoctorUnpaid(long simTime, int closes, int of) : base(simTime)
        {
            Closes = closes;
            Of = of;
        }

        protected override string Describe() { return "DoctorUnpaid(" + Closes + " of " + Of + ")"; }
    }

    /// <summary>The doctor left at the Township's unpaid close; the clinic covers nobody and the quarters stay reserved (spec 023 §3.9).</summary>
    public sealed class DoctorLeft : SimEvent
    {
        public DoctorLeft(long simTime) : base(simTime) { }

        protected override string Describe() { return "DoctorLeft"; }
    }

    /// <summary>One of the Township's gate goals is met for good (spec 023 §3.4): <see cref="Goal"/> is a <c>TownshipGoal</c> bit.</summary>
    public sealed class GateGoalMet : SimEvent
    {
        public int Goal { get; }

        public GateGoalMet(long simTime, int goal) : base(simTime) { Goal = goal; }

        protected override string Describe() { return "GateGoalMet(" + Goal + ")"; }
    }

    /// <summary>The old village joined the town at the Township's unlock (spec 023 §3.5).</summary>
    public sealed class CohortJoined : SimEvent
    {
        public int Households { get; }
        public int People { get; }

        public CohortJoined(long simTime, int households, int people) : base(simTime)
        {
            Households = households;
            People = people;
        }

        protected override string Describe() { return "CohortJoined(" + Households + " households, " + People + " people)"; }
    }

    /// <summary>A household of the old village left after two closes under 35 (spec 023 §3.5).</summary>
    public sealed class CohortHouseholdLeft : SimEvent
    {
        public int Households { get; }

        public CohortHouseholdLeft(long simTime, int households) : base(simTime) { Households = households; }

        protected override string Describe() { return "CohortHouseholdLeft(" + Households + " left)"; }
    }

    /// <summary>The treasury commissioned a civic building (spec 023 §3.7): <see cref="Civic"/> is an index of <c>CivicDef</c>.</summary>
    public sealed class CivicBuilt : SimEvent
    {
        public int Civic { get; }

        public CivicBuilt(long simTime, int civic) : base(simTime) { Civic = civic; }

        protected override string Describe() { return "CivicBuilt " + Civic; }
    }

    /// <summary>A post was advertised; a villager takes it at <see cref="At"/> (spec 023 §3.7).</summary>
    public sealed class PostAdvertised : SimEvent
    {
        public int Civic { get; }
        public long At { get; }

        public PostAdvertised(long simTime, int civic, long at) : base(simTime) { Civic = civic; At = at; }

        protected override string Describe() { return "PostAdvertised " + Civic + " " + At; }
    }

    /// <summary>A villager took a civic post (spec 023 §3.7).</summary>
    public sealed class PostFilled : SimEvent
    {
        public int Civic { get; }
        public int Name { get; }

        public PostFilled(long simTime, int civic, int name) : base(simTime) { Civic = civic; Name = name; }

        protected override string Describe() { return "PostFilled " + Civic + " " + Name; }
    }

    /// <summary>A civic post's wage went unpaid in full at a close; he leaves at the limit (spec 023 §3.7).</summary>
    public sealed class PostUnpaid : SimEvent
    {
        public int Civic { get; }
        public int Closes { get; }
        public int Limit { get; }

        public PostUnpaid(long simTime, int civic, int closes, int limit) : base(simTime) { Civic = civic; Closes = closes; Limit = limit; }

        protected override string Describe() { return "PostUnpaid " + Civic + " " + Closes + "/" + Limit; }
    }

    /// <summary>A civic post fell vacant (spec 023 §3.7): the villager left unpaid, or was let go.</summary>
    public sealed class PostVacated : SimEvent
    {
        public int Civic { get; }
        public int Name { get; }
        public bool Unpaid { get; }

        public PostVacated(long simTime, int civic, int name, bool unpaid) : base(simTime) { Civic = civic; Name = name; Unpaid = unpaid; }

        protected override string Describe() { return "PostVacated " + Civic + (Unpaid ? " unpaid" : " let go"); }
    }
}
