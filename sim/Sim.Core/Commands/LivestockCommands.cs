using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Livestock;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.Commands
{
    /// <summary>Builds the cattle shed on the yard (spec 012 §3.3): from the Hamlet, one shed, capitalised to 1600, ready in its build hours.</summary>
    public sealed class BuildShed : TimedCommand
    {
        public BuildShed(long at) : base(at) { }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            LivestockState s = sim.State.Livestock;
            if (s == null) return CommandResult.Reject(RejectReason.UnknownTile);
            if (!LivestockRules.Unlocked(sim)) return CommandResult.Reject(RejectReason.EraLocked);
            if (s.ShedReadyAt > 0 || LivestockRules.Shed(sim) != null) return CommandResult.Reject(RejectReason.AlreadyBuilt);
            BuildingDef def = sim.Config.Game.Buildings[LivestockRules.Defs(sim).Shed];
            long cost = MarketRules.Indexed(sim, def.Value);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            long t = sim.State.Clock.SimTime;
            s.ShedCost = cost;
            s.ShedReadyAt = t + def.BuildHours * 3600L;
            sim.State.Ledger.Post(t, sim.Period, "ledger.buildShed", "shed", new Posting(Account.Buildings, cost), new Posting(Account.Cash, -cost));
            sim.ScheduleJob(LivestockSystem.ShedJob, 0, s.ShedReadyAt);
            sim.Emit(new ShedStarted(t, cost, s.ShedReadyAt));
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Buys a buffalo or a cow at the village cattle fair (spec 012 §3.4): price × PriceIndex, capitalised to 1750. She walks into a
    /// free stall just calved, vaccinated for this season, healthy and fed for today. A bank loan can fund her (§3.12, US-202).
    /// </summary>
    public sealed class BuyAnimal : TimedCommand
    {
        public string KindId { get; }
        public Financing Financing { get; }

        public BuyAnimal(long at, string kindId, Financing financing = null) : base(at)
        {
            KindId = kindId;
            Financing = financing;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            LivestockState s = sim.State.Livestock;
            if (s == null) return CommandResult.Reject(RejectReason.UnknownAnimal);
            int kind = LivestockRules.Defs(sim).IndexOfAnimal(KindId);
            if (kind < 0) return CommandResult.Reject(RejectReason.UnknownAnimal);
            if (LivestockRules.Shed(sim) == null) return CommandResult.Reject(RejectReason.NoShed);
            if (s.Animals.Count >= LivestockRules.Stalls(sim)) return CommandResult.Reject(RejectReason.ShedFull);
            if (Financing != null) return CommandResult.Reject(RejectReason.InvalidArgument); // the dairy loan comes with US-202
            long cost = MarketRules.Indexed(sim, LivestockRules.Defs(sim).Animals[kind].Price);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            long t = sim.State.Clock.SimTime;
            FarmSystem.Accrue(sim); // her labour joins the Care demand from now (§3.9)
            var a = new Animal
            {
                Id = s.NextAnimalId++, Kind = kind, Name = PickName(sim, s.NextAnimalId - 1), BoughtAt = t, Cost = cost, Stage = AnimalStage.Milking,
                VaccinatedPeriod = sim.Period, Since = t,
            };
            sim.State.Ledger.Post(t, sim.Period, "ledger.buyAnimal", "animal:" + a.Id, new Posting(Account.Livestock, cost), new Posting(Account.Cash, -cost));
            s.Animals.Add(a);
            FarmSystem.Refresh(sim);
            sim.Emit(new AnimalBought(t, a.Id, KindId, a.Name, cost));
            return CommandResult.Ok;
        }

        /// <summary>A seeded name (stream Livestock, counter = animal id), the next one along if it's already in the shed.</summary>
        private static string PickName(Simulation sim, long id)
        {
            IReadOnlyList<string> names = LivestockRules.Defs(sim).Livestock.Names;
            int i = (int)Rng.Below(Rng.Next(sim.State.Seed, RngStream.Livestock, 0, (ulong)id), (ulong)names.Count);
            for (int k = 0; k < names.Count; k++)
            {
                string n = names[(i + k) % names.Count];
                if (!sim.State.Livestock.Animals.Exists(a => a.Name == n)) return n;
            }
            return names[i];
        }
    }

    /// <summary>
    /// Sells an animal at the fair (spec 012 §3.4): price × index × sellShare × health; Dr Cash, Cr 1750 at cost, the difference to
    /// 4800 or 5600. Her milk since the morning goes to the co-op first.
    /// </summary>
    public sealed class SellAnimal : TimedCommand
    {
        public long AnimalId { get; }

        public SellAnimal(long at, long animalId) : base(at) { AnimalId = animalId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Animal a = LivestockRules.Find(sim, AnimalId);
            if (a == null) return CommandResult.Reject(RejectReason.UnknownAnimal);
            long t = sim.State.Clock.SimTime;
            LivestockSystem.Collect(sim, new List<Animal> { a }, false);
            long proceeds = LivestockRules.SellPrice(sim, a);
            var postings = new List<Posting> { new Posting(Account.Cash, proceeds), new Posting(Account.Livestock, -a.Cost) };
            long diff = proceeds - a.Cost;
            if (diff > 0) postings.Add(new Posting(Account.GainOnAssetSales, -diff));
            if (diff < 0) postings.Add(new Posting(Account.LossOnAssetSales, -diff));
            sim.State.Ledger.Post(t, sim.Period, "ledger.sellAnimal", "animal:" + a.Id, postings.ToArray());
            FarmSystem.Accrue(sim);
            sim.State.Livestock.Animals.Remove(a);
            FarmSystem.Refresh(sim);
            sim.Emit(new AnimalSold(t, a.Id, a.Name, proceeds));
            return CommandResult.Ok;
        }
    }
}
