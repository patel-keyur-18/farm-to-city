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
}
