using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Finance;

namespace Sim.Core.Commands
{
    /// <summary>
    /// Changes the diet plan at once (spec 003 §3.11): the current prepayment is closed with a refund of the unearned part,
    /// and the new plan is paid for the rest of the season. Nutrition follows at once, time-weighted (ADR 0032).
    /// </summary>
    public sealed class SetDietPlan : TimedCommand
    {
        public string PlanId { get; }

        public SetDietPlan(long at, string planId) : base(at) { PlanId = planId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (sim.State.Farm == null) return CommandResult.Reject(RejectReason.UnknownPlan);
            FarmDefs farm = sim.Config.Game.Farm;
            int plan = farm.IndexOfDietPlan(PlanId);
            if (plan < 0) return CommandResult.Reject(RejectReason.UnknownPlan);
            if (PlanId == DietPlanDef.SubsistenceId) return CommandResult.Reject(RejectReason.NotSelectable); // automatic only (ADR 0034)
            FounderState s = sim.State.Farm.Founder;
            long t = sim.State.Clock.SimTime, S = sim.Clock.SecondsPerSeason;
            long end = (t / S + 1) * S;
            long cost = (long)((System.Numerics.BigInteger)Market.MarketRules.Indexed(sim, farm.DietPlans[plan].CostPerSeason) * (end - t) / S);
            long refund = s.LivingPrepaymentId == 0 ? 0 : Accrual.RefundIfClosed(sim, s.LivingPrepaymentId);
            if (sim.State.Ledger.Cash + refund < cost) return CommandResult.Reject(RejectReason.InsufficientFunds);

            FarmSystem.Accrue(sim);
            if (s.LivingPrepaymentId != 0) Accrual.Close(sim, s.LivingPrepaymentId);
            s.LivingPrepaymentId = cost == 0 ? 0 : Accrual.Open(sim, Account.LivingCosts, Account.PrepaidLivingCosts, cost, t, end, "founder:living").Id;
            s.AutoSubsistence = false;
            s.PreviousPlan = plan;
            FounderRules.ChangePlan(sim, plan, false);
            return CommandResult.Ok;
        }
    }

    /// <summary>Chooses the working-hours option; it applies from the next daily pass (spec 003 §5).</summary>
    public sealed class SetWorkHours : TimedCommand
    {
        public string OptionId { get; }

        public SetWorkHours(long at, string optionId) : base(at) { OptionId = optionId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            int option = sim.Config.Game == null ? -1 : sim.Config.Game.Farm.IndexOfWorkHours(OptionId);
            if (option < 0) return CommandResult.Reject(RejectReason.UnknownOption);
            sim.State.Farm.Founder.WorkHours = option;
            return CommandResult.Ok;
        }
    }

    /// <summary>Sets the share of the founder's labour sold as day labour: 0, 25, 50, 75 or 100 % (spec 003 §3.11).</summary>
    public sealed class SetLabourSplit : TimedCommand
    {
        public int SharePercent { get; }

        public SetLabourSplit(long at, int sharePercent) : base(at) { SharePercent = sharePercent; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (sim.State.Farm == null || SharePercent < 0 || SharePercent > 100 || SharePercent % 25 != 0)
                return CommandResult.Reject(RejectReason.InvalidShare);
            FarmSystem.Accrue(sim);
            sim.State.Farm.Founder.DayLabourShare = SharePercent;
            FarmSystem.Refresh(sim);
            return CommandResult.Ok;
        }
    }

    /// <summary>Turns eating from the store on or off (spec 003 §3.11); time-weighted like the other plan changes.</summary>
    public sealed class SetEatFromStore : TimedCommand
    {
        public bool On { get; }

        public SetEatFromStore(long at, bool on) : base(at) { On = on; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (sim.State.Farm == null) return CommandResult.Reject(RejectReason.InvalidArgument);
            FarmSystem.Accrue(sim);
            sim.State.Farm.Founder.EatFromStore = On;
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Buys medicine from the village chemist (spec 003 §3.11, ADR 0031): Dr 5250 Health costs, health +gain (at most 1),
    /// once a day. Today's hours are already fixed, so it lowers the chance of the next sick days.
    /// </summary>
    public sealed class BuyMedicine : TimedCommand
    {
        public BuyMedicine(long at) : base(at) { }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (sim.State.Farm == null) return CommandResult.Reject(RejectReason.InvalidArgument);
            FounderState s = sim.State.Farm.Founder;
            FounderDef def = sim.Config.Game.Farm.Founder;
            long day = sim.State.Clock.SimTime / ClockDef.SecondsPerDay;
            if (s.MedicineDay == day) return CommandResult.Reject(RejectReason.AlreadyTreatedToday);
            long cost = Market.MarketRules.Indexed(sim, def.MedicineCost);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);
            if (cost > 0)
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.medicine", "founder",
                    new Posting(Account.HealthCosts, cost), new Posting(Account.Cash, -cost));
            Maths.Fixed h = s.Health + def.MedicineHealthGain;
            s.Health = h > Maths.Fixed.One ? Maths.Fixed.One : h;
            s.MedicineDay = day;
            return CommandResult.Ok;
        }
    }
}
