using Sim.Core.Maths;

namespace Sim.Core.Events
{
    /// <summary>The next era's progress changed (spec 009 §5): after a field harvest, or a season close with farm profit.</summary>
    public sealed class EraProgress : SimEvent
    {
        public int Acres { get; }
        public Fixed MeanCare { get; }
        public int HutsBuilt { get; }
        public int ProfitableSeasons { get; }

        public EraProgress(long simTime, int acres, Fixed meanCare, int hutsBuilt, int profitableSeasons) : base(simTime)
        {
            Acres = acres;
            MeanCare = meanCare;
            HutsBuilt = hutsBuilt;
            ProfitableSeasons = profitableSeasons;
        }

        protected override string Describe() { return "EraProgress(acres=" + Acres + ", care=" + MeanCare + ", huts=" + HutsBuilt + ", profitable=" + ProfitableSeasons + ")"; }
    }

    /// <summary>The town reached a new era (spec 009 §3.6): the celebration and the play-log milestone.</summary>
    public sealed class EraUnlocked : SimEvent
    {
        public string EraId { get; }

        public EraUnlocked(long simTime, string eraId) : base(simTime) { EraId = eraId; }

        protected override string Describe() { return "EraUnlocked(" + EraId + ")"; }
    }
}
