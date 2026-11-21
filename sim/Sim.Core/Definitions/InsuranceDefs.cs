using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>Crop insurance's numbers (spec 027 §3.5): luck below <see cref="Threshold"/> pays; the premium is the pest part and the event part, loaded.</summary>
    public sealed class CropInsuranceDef
    {
        /// <summary>The insured yield index under which a claim is paid (0.80: a 20% excess).</summary>
        public Fixed Threshold { get; internal set; }

        /// <summary>The average shortfall in an outbreak (95% × 0.20 + 5% × 0.80): the pest risk times this is the expected pest loss.</summary>
        public Fixed PestShortfall { get; internal set; }

        /// <summary>A flat share of the sum insured for hail, drought and waterlogging (a placeholder for the balance pass).</summary>
        public Fixed EventPart { get; internal set; }
        public Fixed Loading { get; internal set; }

        /// <summary>The season's usual wet share (Spring, Summer, Monsoon, Winter), the pest risk's wet input at sowing.</summary>
        public Fixed[] WetShare { get; internal set; }
    }

    /// <summary>Property insurance: a share of the insured book value a season, and the share of a repair bill the owner carries.</summary>
    public sealed class PropertyInsuranceDef
    {
        public Fixed Rate { get; internal set; }
        public Fixed Excess { get; internal set; }
    }

    /// <summary>Business insurance: the same, plus a share of last season's profit, and how much of the lost profit a day shut pays back.</summary>
    public sealed class BusinessInsuranceDef
    {
        public Fixed Rate { get; internal set; }
        public Fixed ProfitShare { get; internal set; }
        public Fixed Excess { get; internal set; }
        public Fixed DailyProfitShare { get; internal set; }
    }

    /// <summary>Insurance (spec 027 §3.5–§3.6, <c>content/insurance.json</c>), from the Township's content, or null.</summary>
    public sealed class InsuranceDef
    {
        public CropInsuranceDef Crop { get; internal set; }
        public PropertyInsuranceDef Property { get; internal set; }
        public BusinessInsuranceDef Business { get; internal set; }

        /// <summary>How many claims the Money page keeps.</summary>
        public int KeepClaims { get; internal set; }
    }
}
