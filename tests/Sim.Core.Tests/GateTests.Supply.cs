using System.Collections.Concurrent;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Definitions;
using Tools.Balancer;

namespace Sim.Core.Tests
{
    /// <summary>
    /// The exploit check of US-268 (spec 030 §8 AC-16, Sprint 28): Manual supply and its caps are choices about where your own produce goes, not a way to money.
    /// On 200 Township towns, in zero time every random supply command moves no coin, and a town whose supply is flipped every morning for three days keeps exact books.
    /// What flipping earns against leaving it Automatic is measured and printed.
    /// </summary>
    public partial class GateTests
    {
        private static Command SupplyCommand(Simulation sim, long t, ulong r, System.Collections.Generic.List<Business.Business> own)
        {
            ulong Raw(int k) { return Rng.Next(r, (RngStream)(990 + k), 0, 0); }
            int Pick(int k, int n) { return n <= 0 ? 0 : (int)Rng.Below(Raw(k), (ulong)n); }
            Business.Business b = own[Pick(1, own.Count)];
            var goods = SupplyRules.CappedInputs(sim, BusinessRules.TypeOf(sim, b));
            if (Pick(2, 4) == 0 || goods.Count == 0) return new SetSupplyMode(t, b.Id, Pick(3, 2) == 0);
            string good = sim.Config.Game.Market.Goods[goods[Pick(4, goods.Count)]].Id;
            var steps = BusinessRules.Defs(sim).SupplySteps;
            int share = new[] { 0, 25, 50, 100 }[Pick(5, 4)];
            long grams = Pick(6, 3) == 0 ? steps[Pick(7, steps.Count)] : 0;
            return new SetSupplyCap(t, b.Id, good, share, grams);
        }

        // AC-16: supply caps and modes in zero time change no money (net worth moves by nothing), and flipping them every morning for three days leaves every ledger exact.
        [Test]
        public void SupplyExploits()
        {
            SimConfig config = LiveTownship();
            int seeds = P3Seeds;
            var failures = new ConcurrentBag<string>();
            long accepted = 0, flipped = 0;
            long gainSum = 0, towns = 0;
            object gate = new object();
            System.Threading.Tasks.Parallel.For(0, seeds, i =>
            {
                ulong seed = (ulong)(i + 1);
                Simulation sim = Economy.RunBot(config, new TownshipBot(false), 28 + i % 4, seed, null).Sim;
                var own = sim.State.Businesses.Businesses.Where(b => b.Status == BusinessStatus.Open && SupplyRules.HasMode(sim, BusinessRules.TypeOf(sim, b))).ToList();
                if (own.Count == 0) return;
                long t = sim.State.Clock.SimTime;
                long before = Reporting.NetWorth(sim).Total;
                int ok = 0;
                for (int c = 0; c < 30; c++) if (sim.Execute(SupplyCommand(sim, t, Rng.Next(seed, (RngStream)985, 0, (ulong)c), own)).Accepted) ok++;
                long gain = Reporting.NetWorth(sim).Total - before;
                if (gain != 0) failures.Add("seed " + seed + ": net worth moved by " + gain + " in zero time with " + ok + " supply commands");
                int flips = 0;
                for (int d = 0; d < 3; d++) // flipped every morning
                {
                    long day = (sim.State.Clock.SimTime / 86_400 + 1) * 86_400;
                    sim.AdvanceTo(day + 5 * 3600 + 1800);
                    for (int c = 0; c < 6; c++) if (sim.Execute(SupplyCommand(sim, sim.State.Clock.SimTime, Rng.Next(seed, (RngStream)986, (ulong)d, (ulong)c), own)).Accepted) flips++;
                    sim.AdvanceTo(day + 8 * 3600);
                }
                if (sim.State.Ledger.Reconcile() != null) failures.Add("seed " + seed + ": your ledger");
                foreach (Business.Business b in sim.State.Businesses.Businesses) if (b.Ledger.Reconcile() != null) failures.Add("seed " + seed + ": business " + b.Id);
                lock (gate) { accepted += ok; flipped += flips; gainSum += gain; towns++; }
            });
            TestContext.Out.WriteLine("SUPPLY " + towns + " of " + seeds + " towns had a business to set; " + accepted + " zero-time commands accepted, " + flipped + " morning flips; net worth moved " + gainSum + " in zero time");
            Assert.That(towns, Is.GreaterThan(seeds / 4), "the check really had businesses to set");
            Assert.That(accepted, Is.GreaterThan(towns), "the check really changed the supply");
            Assert.That(failures.OrderBy(f => f, System.StringComparer.Ordinal).ToList(), Is.Empty);
        }
    }
}
