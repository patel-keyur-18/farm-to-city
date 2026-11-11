using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Business;
using Sim.Core.Equipment;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.People;

namespace Sim.Core.Finance
{
    public enum DueKind { Loan, Living, Hand, Warehouse, Water, School, Machine }

    /// <summary>One charge the next season close takes from the founder's cash (ADR 0101 §1).</summary>
    public sealed class CloseDue
    {
        /// <summary>Stable id: <c>loan:7</c>, <c>living</c>, <c>hand:3</c>, <c>warehouse</c>, <c>water:pump</c>, <c>school</c>, <c>machine:2:lease</c>.</summary>
        public string Key { get; internal set; }
        public DueKind Kind { get; internal set; }

        /// <summary>The loan, hand or machine it is for; 0 otherwise.</summary>
        public long Id { get; internal set; }

        /// <summary>The cash the close takes, at today's prices.</summary>
        public long Amount { get; internal set; }

        /// <summary>A loan's interest and principal; a hand's wage before the house and an advance (<see cref="Housing"/>, <see cref="Advance"/>).</summary>
        public long Interest { get; internal set; }
        public long Principal { get; internal set; }
        public long Wage { get; internal set; }
        public long Housing { get; internal set; }
        public long Advance { get; internal set; }

        /// <summary>The machine's kind (its definition id), or the water source (<c>pump</c>, <c>borewell</c>, <c>canal</c>, <c>drip</c>).</summary>
        public string What { get; internal set; }
    }

    /// <summary>What the next close takes from one business's own cash (its loan's payment, its worker's wage, its pitch's rent), beside what it can spend (ADR 0101 §1).</summary>
    public sealed class BusinessDue
    {
        public long BusinessId { get; internal set; }
        public long Loan { get; internal set; }
        public long Wage { get; internal set; }
        public long Rent { get; internal set; }
        public long Total { get { return Loan + Wage + Rent; } }

        /// <summary>Its cash plus what's left of its overdraft.</summary>
        public long CanSpend { get; internal set; }
        public bool Covered { get { return CanSpend >= Total; } }
        public long Short { get { return Covered ? 0 : Total - CanSpend; } }
    }

    /// <summary>
    /// Everything the next season close takes from the founder's own cash, in the order the close takes it (ADR 0101 §1): loans, living, hands'
    /// renewals, warehouse space, water, the school, machines. Priced as the close prices it, at today's prices (the close's index step may
    /// add a little). A view of state: it changes nothing. A business's own charges are on its own books and aren't here.
    /// </summary>
    public static class CloseDues
    {
        public static List<CloseDue> Next(Simulation sim)
        {
            var list = new List<CloseDue>();
            if (sim.State.Credit != null)
                foreach (Loan loan in sim.State.Credit.Loans)
                {
                    if (!loan.IsOpen || loan.BusinessId != 0) continue; // a business pays its own loan (spec 016 §3.8)
                    long total = CreditRules.NextPayment(sim, loan, out long interest, out long principal);
                    if (total == 0) continue;
                    list.Add(new CloseDue { Key = "loan:" + loan.Id, Kind = DueKind.Loan, Id = loan.Id, Amount = total, Interest = interest, Principal = principal });
                }
            if (sim.State.Farm != null)
            {
                Farm.FounderState founder = sim.State.Farm.Founder;
                int plan = founder.AutoSubsistence ? founder.PreviousPlan : founder.DietPlan;
                long living = MarketRules.Indexed(sim, sim.Config.Game.Farm.DietPlans[plan].CostPerSeason);
                if (living > 0) list.Add(new CloseDue { Key = "living", Kind = DueKind.Living, Amount = living });
            }
            if (sim.State.People != null)
                foreach (Hand h in sim.State.People.Hands)
                {
                    if (h.Contract == ContractState.Lapsed || HrRules.Leaves(sim, h) || CitizenRules.Retiring(sim, h)) continue; // not renewed at the close
                    long wage = PeopleSystem.RenewalWage(sim, h, out long housing);
                    list.Add(new CloseDue
                    {
                        Key = "hand:" + h.Id, Kind = DueKind.Hand, Id = h.Id, Amount = System.Math.Max(0, wage - h.Hr.Advance),
                        Wage = wage + housing, Housing = housing, Advance = h.Hr.Advance,
                    });
                }
            long rent = GodownRules.NextCloseRent(sim);
            if (rent > 0) list.Add(new CloseDue { Key = "warehouse", Kind = DueKind.Warehouse, Amount = rent });
            if (IrrigationRules.On(sim))
            {
                Water(list, "pump", IrrigationRules.PumpPower(sim, 1));
                Water(list, "borewell", IrrigationRules.PumpPower(sim, 2));
                Water(list, "canal", IrrigationRules.CanalFee(sim));
                Water(list, "drip", IrrigationRules.DripPower(sim) + IrrigationRules.DripUpkeep(sim));
            }
            if (SchoolRules.IsOpen(sim) && SchoolRules.Upkeep(sim) > 0) list.Add(new CloseDue { Key = "school", Kind = DueKind.School, Amount = SchoolRules.Upkeep(sim) });
            if (EquipmentRules.On(sim))
            {
                foreach (Machine m in sim.State.Equipment.Machines)
                {
                    MachineDef d = EquipmentRules.Defs(sim).Machines[m.Kind];
                    long diesel = EquipmentRules.Diesel(sim, d, EquipmentRules.Acres(sim, m.Kind));
                    if (m.Leased)
                    {
                        long lease = EquipmentRules.Lease(sim, d) + diesel; // the close takes both or hands the machine back
                        if (lease > 0) list.Add(MachineDue(m, d, "lease", lease));
                        continue;
                    }
                    long upkeep = m.Maintain ? EquipmentRules.Maintenance(sim, m) : 0;
                    if (upkeep > 0) list.Add(MachineDue(m, d, "upkeep", upkeep));
                    if (diesel > 0) list.Add(MachineDue(m, d, "diesel", diesel));
                }
            }
            return list;
        }

        /// <summary>Each business's own charges at the next close: they come from its own cash, not the founder's, so they stand beside the founder's total, never in it.</summary>
        public static List<BusinessDue> Businesses(Simulation sim)
        {
            var list = new List<BusinessDue>();
            if (sim.State.Businesses == null) return list;
            foreach (Business.Business b in sim.State.Businesses.Businesses)
            {
                if (b.Status != BusinessStatus.Open && b.Status != BusinessStatus.Building) continue;
                var due = new BusinessDue { BusinessId = b.Id, CanSpend = BusinessBooks.CanSpend(sim, b) };
                Loan loan = b.LoanId == 0 || sim.State.Credit == null ? null : sim.State.Credit.Find(b.LoanId);
                if (loan != null && loan.IsOpen) due.Loan = CreditRules.NextPayment(sim, loan, out _, out _);
                Worker w = sim.State.People?.WorkerAt(b.Id);
                if (b.Status == BusinessStatus.Open && w != null && !HrRules.Leaves(sim, w)) due.Wage = System.Math.Max(0, w.Wage - w.Hr.Advance);
                if (b.Pitch >= 0 && sim.Config.Game.Shops != null) due.Rent = MarketRules.Indexed(sim, sim.Config.Game.Shops.PitchRent);
                if (due.Total > 0) list.Add(due);
            }
            return list;
        }

        private static void Water(List<CloseDue> list, string source, long cost)
        {
            if (cost > 0) list.Add(new CloseDue { Key = "water:" + source, Kind = DueKind.Water, What = source, Amount = cost });
        }

        private static CloseDue MachineDue(Machine m, MachineDef d, string part, long cost)
        {
            return new CloseDue { Key = "machine:" + m.Id + ":" + part, Kind = DueKind.Machine, Id = m.Id, What = d.Id, Amount = cost };
        }
    }
}
