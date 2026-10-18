using System;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.Tests
{
    public partial class MarketTests
    {
        private static void Rejects(Simulation sim, Command cmd, RejectReason reason)
        {
            ulong before = sim.State.ComputeHash();
            Assert.That(sim.Execute(cmd).Reason, Is.EqualTo(reason), cmd.GetType().Name);
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(before), reason + " changed the state");
        }

        // Spec 004 AC-7: random offer and withdraw sequences never leave the rail, and replay identically.
        [Test]
        public void Rail_SupplyNeverZero_Deterministic()
        {
            MarketDef def = TestContent.Game().Market.Market;
            string[] produce = { "radish", "wheat", "rice", "potato", "lentils", "banana", "papaya", "guava", "mango", "spinach", "coriander" };
            ulong RunSequence(ulong seed, bool check)
            {
                Simulation sim = TestSim.NewFarm(seed, game: TestSim.Quiet());
                foreach (string id in produce) sim.State.Market.Stock[Good(sim, id)] += 1_000_000; // 1 t of each to play with
                for (int step = 0; step < 24; step++)
                {
                    long t = (step + 1) * H;
                    ulong r = Rng.Next(seed, (RngStream)901, 0, (ulong)step);
                    string id = produce[(int)Rng.Below(r, (ulong)produce.Length)];
                    long q = (long)Rng.Below(r >> 7, 400_000) + 1;
                    Fixed? min = (r & 3) == 0 ? (Fixed?)MarketRules.RegionalPrice(sim, Good(sim, id)) : null;
                    if ((r & 4) == 0) sim.Execute(new Offer(sim.State.Clock.SimTime, id, q, min));
                    else sim.Execute(new Withdraw(sim.State.Clock.SimTime, id, Math.Min(q, sim.State.Market.OnOffer(Good(sim, id)))));
                    sim.AdvanceTo(t);
                    if (!check) continue;
                    foreach (string g in produce)
                    {
                        int i = Good(sim, g);
                        Fixed reg = MarketRules.RegionalPrice(sim, i), p = sim.State.Market.MarketPrice(i);
                        Assert.That(p >= def.RailMin * reg && p <= def.RailMax * reg, g + " left the rail: " + p);
                    }
                }
                return sim.State.ComputeHash();
            }
            for (ulong seed = 1; seed <= 1000; seed++)
            {
                ulong a = RunSequence(seed, true);
                if (seed % 50 == 0) Assert.That(RunSequence(seed, false), Is.EqualTo(a), "same seed, same prices and sales");
            }
        }

        // Spec 004 AC-10.
        [Test]
        public void SellToTrader()
        {
            Simulation sim = TestSim.NewFarm(game: TestSim.Quiet());
            int potato = Good(sim, "potato");
            sim.State.Market.Stock[potato] = 3_500_000;
            Assert.That(sim.Execute(new Offer(0, "potato", 1_000_000)).Accepted, Is.True);
            long cash = sim.State.Ledger.Cash;

            Rejects(sim, new SellToTrader(0, "potato", 2_600_000), RejectReason.InsufficientStock); // only 2,500 kg isn't on offer
            Rejects(sim, new SellToTrader(0, "potatoes", 1), RejectReason.UnknownGood);
            Rejects(sim, new SellToTrader(0, "seed_potato", 1), RejectReason.NotProduce);
            Rejects(sim, new SellToTrader(0, "potato", 0), RejectReason.InvalidQuantity);

            Assert.That(sim.Execute(new SellToTrader(0, "potato", 2_000_000)).Accepted, Is.True);
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(Money.Value(2_000_000, MarketRules.Floor(sim, potato), 1000)));
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(10_600_00)); // 2,000 kg × 5.30
            Assert.That(MarketRules.TraderCapacityLeft(sim, potato), Is.EqualTo(1_000_000));
            Assert.That(sim.State.Market.OnOffer(potato), Is.EqualTo(1_000_000), "the offer is untouched");
            Rejects(sim, new SellToTrader(0, "potato", 1_000_001), RejectReason.InsufficientStock);
            Assert.That(sim.Execute(new Withdraw(0, "potato", 1_000_000)).Accepted, Is.True);
            Rejects(sim, new SellToTrader(0, "potato", 1_000_001), RejectReason.TraderFull);
        }

        // Spec 004 AC-11: saplings only at planting; Buy rejects saplings and produce.
        [Test]
        public void Saplings_AtPlantingOnly()
        {
            Simulation sim = TestSim.NewFarm(game: TestSim.RichGame());
            Rejects(sim, new Buy(0, "sapling_mango", 1), RejectReason.NotAnInput);
            Rejects(sim, new Buy(0, "potato", 1_000), RejectReason.NotAnInput);
            long cash = sim.State.Ledger.Cash;
            long slot = sim.State.Land.Slots.First(s => s.TreeId == 0).Id;
            Assert.That(sim.Execute(new PlantTree(0, slot, "mango")).Accepted, Is.True);
            int sapling = Good(sim, "sapling_mango");
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(Money.Value(1, MarketRules.RegionalPrice(sim, sapling), 1)));
            Assert.That(sim.State.Ledger.Balance(Account.Trees), Is.EqualTo(350_00));
        }

        // Spec 004 §5: Offer and Withdraw validation.
        [Test]
        public void Offer_Withdraw_Rejections()
        {
            Simulation sim = TestSim.NewFarm(game: TestSim.Quiet());
            Rejects(sim, new Offer(0, "wheat", 75_001), RejectReason.InsufficientStock); // 75 kg inherited
            Rejects(sim, new Offer(0, "seed_wheat", 1), RejectReason.NotProduce);
            Rejects(sim, new Offer(0, "wheat", -5), RejectReason.InvalidQuantity);
            Rejects(sim, new Offer(0, "wheat", 5, Fixed.Zero), RejectReason.InvalidArgument);
            Assert.That(sim.Execute(new Offer(0, "wheat", 50_000, Fixed.FromInt(30))).Accepted, Is.True);
            Assert.That(sim.State.Market.OfferMinPrice(Good(sim, "wheat")), Is.EqualTo(Fixed.FromInt(30)));
            Rejects(sim, new Offer(0, "wheat", 25_001), RejectReason.InsufficientStock);
            Rejects(sim, new Withdraw(0, "wheat", 50_001), RejectReason.NotOffered);
            Assert.That(sim.Execute(new Withdraw(0, "wheat", 50_000)).Accepted, Is.True);
            Assert.That(sim.State.Market.OnOffer(Good(sim, "wheat")), Is.EqualTo(0));
            Assert.That(sim.State.Market.OfferMinPrice(Good(sim, "wheat")), Is.Null);
        }
    }
}
