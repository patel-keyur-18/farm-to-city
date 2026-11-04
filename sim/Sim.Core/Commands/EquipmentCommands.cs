using Sim.Core.Definitions;
using Sim.Core.Equipment;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Livestock;

namespace Sim.Core.Commands
{
    /// <summary>Shared checks for the equipment commands (spec 013 §3.2).</summary>
    internal static class Machines
    {
        /// <summary>The machine kind by id, unlocked and not already yours: its index, or a rejection.</summary>
        internal static RejectReason Kind(Simulation sim, string id, out int kind)
        {
            kind = -1;
            if (!EquipmentRules.On(sim)) return RejectReason.UnknownMachine;
            kind = EquipmentRules.Defs(sim).IndexOf(id);
            if (kind < 0) return RejectReason.UnknownMachine;
            if (!EquipmentRules.Unlocked(sim, EquipmentRules.Defs(sim).Machines[kind])) return RejectReason.EraLocked;
            return RejectReason.None;
        }

        /// <summary>The rest of this season's diesel for the fields a new machine of this kind would work.</summary>
        internal static long DieselNow(Simulation sim, int kind, MachineDef d, Machine added)
        {
            sim.State.Equipment.Machines.Add(added);
            long diesel = EquipmentRules.RestOfSeason(sim, EquipmentRules.Diesel(sim, d, EquipmentRules.Acres(sim, kind)));
            sim.State.Equipment.Machines.Remove(added);
            return diesel;
        }

        internal static void Pay(Simulation sim, string key, long machineId, params Posting[] costs)
        {
            long total = 0;
            var postings = new System.Collections.Generic.List<Posting>();
            foreach (Posting p in costs) if (p.Amount != 0) { postings.Add(p); total += p.Amount; }
            if (total == 0) return;
            postings.Add(new Posting(Account.Cash, -total));
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, key, "machine:" + machineId, postings.ToArray());
        }
    }

    /// <summary>
    /// Buys a machine (spec 013 §3.2): its price capitalised to 1610, the rest of this season's diesel, and for the ox plough its pair of
    /// bullocks as livestock (1750) into the second shed (ADR 0088). A tractor can be bought with a bank loan against it (0.70).
    /// </summary>
    public sealed class BuyMachine : TimedCommand
    {
        public string KindId { get; }
        public Financing Financing { get; }

        public BuyMachine(long at, string kindId, Financing financing = null) : base(at)
        {
            KindId = kindId;
            Financing = financing;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = Machines.Kind(sim, KindId, out int kind);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            if (EquipmentRules.OfKind(sim, kind) != null) return CommandResult.Reject(RejectReason.AlreadyBuilt);
            MachineDef d = EquipmentRules.Defs(sim).Machines[kind];
            if (d.Bullocks > 0 && LivestockRules.InShed(sim, 1) + d.Bullocks > LivestockRules.Stalls(sim, 1)) return CommandResult.Reject(RejectReason.NoShedRoom);
            long t = sim.State.Clock.SimTime, price = EquipmentRules.Price(sim, d);
            var m = new Machine { Id = sim.State.Equipment.NextMachineId, Kind = kind, Since = t, Cost = price };
            long diesel = Machines.DieselNow(sim, kind, d, m), bullocks = EquipmentRules.BullocksPrice(sim, d);
            int lender = -1;
            long loan = 0;
            if (Financing != null)
            {
                if (!d.Loan) return CommandResult.Reject(RejectReason.InvalidArgument);
                if (Financing.Amount > price) return CommandResult.Reject(RejectReason.InvalidAmount);
                r = CreditRules.Check(sim, Financing.LenderId, Financing.Amount, Financing.TermSeasons, 0, out lender, 0, 0, price);
                if (r != RejectReason.None) return CommandResult.Reject(r);
                loan = Financing.Amount;
            }
            if (!sim.State.Ledger.CanAfford(price + bullocks + diesel - loan)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            FarmSystem.Accrue(sim);
            if (Financing != null) m.LoanId = CreditSystem.Create(sim, lender, loan, Financing.TermSeasons).Id;
            sim.State.Equipment.NextMachineId++;
            Machines.Pay(sim, "ledger.buyMachine", m.Id, new Posting(Account.Equipment, price), new Posting(Account.Power, diesel));
            sim.State.Equipment.Machines.Add(m);
            LivestockState herd = sim.State.Livestock;
            int bullock = EquipmentRules.Defs(sim).Bullock;
            for (int i = 0; i < d.Bullocks; i++)
            {
                long cost = bullocks / d.Bullocks;
                var a = new Animal
                {
                    Id = herd.NextAnimalId++, Kind = bullock, BoughtAt = t, Cost = cost, Stage = AnimalStage.Milking, VaccinatedPeriod = sim.Period, Since = t, Shed = 1,
                };
                a.Name = BuyAnimal.PickName(sim, a.Id, bullock);
                sim.State.Ledger.Post(t, sim.Period, "ledger.buyBullock", "animal:" + a.Id, new Posting(Account.Livestock, cost), new Posting(Account.Cash, -cost));
                herd.Animals.Add(a);
            }
            FarmSystem.Refresh(sim);
            sim.Emit(new MachineBought(t, m.Id, d.Id, price + bullocks));
            if (d.Bullocks > 0) LivestockSystem.LowCheck(sim);
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Leases a machine (spec 013 §3.2): the rest of this season's rent and diesel now (Dr 5300, 5810), then a season's upfront at each
    /// close until you give it back or can't pay. A leased machine never breaks down on you.
    /// </summary>
    public sealed class LeaseMachine : TimedCommand
    {
        public string KindId { get; }

        public LeaseMachine(long at, string kindId) : base(at) { KindId = kindId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = Machines.Kind(sim, KindId, out int kind);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            MachineDef d = EquipmentRules.Defs(sim).Machines[kind];
            if (d.LeasePerSeason <= 0) return CommandResult.Reject(RejectReason.InvalidArgument);
            if (EquipmentRules.OfKind(sim, kind) != null) return CommandResult.Reject(RejectReason.AlreadyBuilt);
            long t = sim.State.Clock.SimTime;
            var m = new Machine { Id = sim.State.Equipment.NextMachineId, Kind = kind, Since = t, Leased = true };
            long lease = EquipmentRules.RestOfSeason(sim, EquipmentRules.Lease(sim, d)), diesel = Machines.DieselNow(sim, kind, d, m);
            if (!sim.State.Ledger.CanAfford(lease + diesel)) return CommandResult.Reject(RejectReason.InsufficientFunds);
            FarmSystem.Accrue(sim);
            sim.State.Equipment.NextMachineId++;
            Machines.Pay(sim, "ledger.machineLease", m.Id, new Posting(Account.Rent, lease), new Posting(Account.Power, diesel));
            sim.State.Equipment.Machines.Add(m);
            FarmSystem.Refresh(sim);
            sim.Emit(new MachineLeased(t, m.Id, d.Id, lease + diesel));
            return CommandResult.Ok;
        }
    }

    /// <summary>Hires a machine with its driver (or the ox plough with its owner) until midnight: it works all your fields today (spec 013 §3.2).</summary>
    public sealed class HireMachine : TimedCommand
    {
        public string KindId { get; }

        public HireMachine(long at, string kindId) : base(at) { KindId = kindId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = Machines.Kind(sim, KindId, out int kind);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            if (EquipmentRules.OfKind(sim, kind) != null || EquipmentRules.Hired(sim, kind)) return CommandResult.Reject(RejectReason.AlreadyBuilt);
            MachineDef d = EquipmentRules.Defs(sim).Machines[kind];
            long t = sim.State.Clock.SimTime, cost = EquipmentRules.Hire(sim, d);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);
            FarmSystem.Accrue(sim);
            sim.State.Ledger.Post(t, sim.Period, "ledger.machineHire", "hire:" + d.Id, new Posting(Account.Rent, cost), new Posting(Account.Cash, -cost));
            sim.State.Equipment.HiredUntil[kind] = (t / ClockDef.SecondsPerDay + 1) * ClockDef.SecondsPerDay;
            FarmSystem.Refresh(sim);
            sim.Emit(new MachineHired(t, d.Id, cost));
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Sells a bought machine at its resale value (book × 0.80), its bullocks with it at their fair price, repaying the bank first if
    /// it backs a loan; or gives a leased one back (its rent isn't refunded).
    /// </summary>
    public sealed class SellMachine : TimedCommand
    {
        public long MachineId { get; }

        public SellMachine(long at, long machineId) : base(at) { MachineId = machineId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Machine m = EquipmentRules.Find(sim, MachineId);
            if (m == null) return CommandResult.Reject(RejectReason.UnknownMachine);
            long t = sim.State.Clock.SimTime;
            string kind = EquipmentRules.Defs(sim).Machines[m.Kind].Id;
            if (m.Leased)
            {
                FarmSystem.Accrue(sim);
                sim.State.Equipment.Machines.Remove(m);
                FarmSystem.Refresh(sim);
                sim.Emit(new MachineReturned(t, m.Id, kind));
                return CommandResult.Ok;
            }
            long required = CreditSystem.RequiredOnSale(sim, -1, -1, m.Id);
            if (EquipmentRules.Resale(sim, m) < required) return CommandResult.Reject(RejectReason.LoanSecured);
            long raised = EquipmentRules.Sell(sim, m, "ledger.sellMachine", sim.Period);
            if (required > 0) CreditSystem.RepaySecured(sim, required);
            sim.Emit(new MachineSold(t, m.Id, kind, raised));
            return CommandResult.Ok;
        }
    }

    /// <summary>Pay a bought machine's maintenance at each close, or skip it (it breaks down more often; spec 013 §3.2).</summary>
    public sealed class SetMaintenance : TimedCommand
    {
        public long MachineId { get; }
        public bool On { get; }

        public SetMaintenance(long at, long machineId, bool on) : base(at)
        {
            MachineId = machineId;
            On = on;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Machine m = EquipmentRules.Find(sim, MachineId);
            if (m == null || m.Leased) return CommandResult.Reject(RejectReason.UnknownMachine);
            m.Maintain = On;
            return CommandResult.Ok;
        }
    }
}
