using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Maths;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 017 §3.7 (US-122, ADR 0095 §1–3): newcomers coming and going, the cart by the square, the grocer's and tea stall's prices.</summary>
    public class RivalTests
    {
        private static long NextClose(Simulation sim) { return (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason; }

        /// <summary>The content with a newcomer's chance (and its exit share) set, so a test needn't hunt for a seed.</summary>
        private static Simulation Town(string chance, string exitShare = "0.1")
        {
            return Start(Game((f, j) => f != "shops.json" ? j : j.Replace("\"chance\": 0.5", "\"chance\": " + chance).Replace("\"exitShare\": 0.1", "\"exitShare\": " + exitShare)));
        }

        private static Rival Newcomer(Simulation sim, ShopKind kind) { return sim.State.Shops.Newcomers.SingleOrDefault(r => r.Kind == kind); }

        // AC-4: your shop at the usual price draws nobody; a season above it draws a newcomer (the seeded chance, here 1) on the first free
        // pitch, at the usual price, standard, 2.5 stars; at most one of a kind; it keeps its place in a save; under the exit share two
        // seasons running it packs up.
        [Test]
        public void Entry_Exit()
        {
            Simulation sim = Town("1");
            Business.Business tea = ShopTests.Shop(sim, "tea_stall", 0);
            sim.AdvanceTo(NextClose(sim));
            Assert.That(sim.State.Shops.Newcomers, Is.Empty, "at the usual price, no gap: nobody comes");

            Ok(sim, new SetShop(sim.State.Clock.SimTime, tea.Id, Fixed.Parse("1.1")));
            sim.AdvanceTo(NextClose(sim) - 3600);
            Assert.That(tea.SeasonAtPrice, Is.GreaterThan(tea.SeasonServed), "sold the season above the reference");
            Assert.That(ShopRules.RichPrices(sim, ShopKind.Chai), Is.True);
            sim.DrainEvents();
            sim.AdvanceTo(NextClose(sim));
            Rival n = Newcomer(sim, ShopKind.Chai);
            Assert.That(n, Is.Not.Null, "your prices drew a newcomer");
            Assert.That((n.Pitch, n.PriceFactor, n.Stars, ShopRules.RivalQuality(sim, n)), Is.EqualTo((1, Fixed.One, Fixed.FromRatio(5, 2), 1)), "pitch II, the usual price, standard, 2.5 stars");
            RivalOpened e = sim.DrainEvents().OfType<RivalOpened>().Single();
            Assert.That((e.Kind, e.Pitch, e.Gap), Is.EqualTo((ShopKind.Chai, 1, false)));
            Assert.That(sim.Execute(new FoundBusiness(sim.State.Clock.SimTime, "veg_stall", 0, 200_000_00, false, null, 1)).Reason, Is.EqualTo(RejectReason.PitchTaken), "its pitch is taken");

            sim.AdvanceTo(NextClose(sim) + 12 * 3600); // midday: the shops open from 8:00
            Assert.That(sim.State.Shops.Newcomers.Count(r => r.Kind == ShopKind.Chai), Is.EqualTo(1), "at most one of a kind");
            Assert.That(n.SeasonServed, Is.GreaterThan(0), "it serves customers");

            LoadResult again = SaveCodec.Read(SaveCodec.Write(sim.State, sim.Config, Wall0, maxLogEntries: 0), sim.Config, 3); // no history: the test set its cash
            Assert.That(again.Error, Is.EqualTo(SaveError.None), again.Detail);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), "the newcomer round-trips");
            Assert.That(again.State.Shops.Newcomers.Single().Pitch, Is.EqualTo(1));

            // Under the exit share for two seasons running, it packs up.
            Simulation quiet = Town("1", "0.99");
            Business.Business dear = ShopTests.Shop(quiet, "tea_stall", 0);
            Ok(quiet, new SetShop(quiet.State.Clock.SimTime, dear.Id, Fixed.Parse("1.2")));
            quiet.AdvanceTo(NextClose(quiet));
            Rival q = Newcomer(quiet, ShopKind.Chai);
            Assert.That(q, Is.Not.Null);
            Ok(quiet, new SetShop(quiet.State.Clock.SimTime, dear.Id, Fixed.One));
            quiet.AdvanceTo(NextClose(quiet));
            Assert.That(q.LowSeasons, Is.EqualTo(1), "one season under the exit share");
            quiet.DrainEvents();
            quiet.AdvanceTo(NextClose(quiet));
            Assert.That(Newcomer(quiet, ShopKind.Chai), Is.Null, "two seasons: it packed up");
            Assert.That(quiet.DrainEvents().OfType<RivalClosed>().Single().Kind, Is.EqualTo(ShopKind.Chai));
        }

        // AC-4 (ADR 0095 §2): every pitch taken, the newcomer is the cart by the square, 100 m further for the village's customers.
        [Test]
        public void Cart()
        {
            Simulation sim = Town("1");
            ShopTests.Shop(sim, "veg_stall", 0);
            ShopTests.Shop(sim, "sweet_shop", 1, rich: false);
            Business.Business tea = ShopTests.Shop(sim, "tea_stall", 2, rich: false);
            Ok(sim, new SetShop(sim.State.Clock.SimTime, tea.Id, Fixed.Parse("1.1")));
            sim.AdvanceTo(NextClose(sim));
            sim.AdvanceTo(NextClose(sim));
            Rival n = Newcomer(sim, ShopKind.Chai);
            Assert.That(n, Is.Not.Null);
            Assert.That(n.Pitch, Is.EqualTo(-1), "no pitch free: the cart");
            Assert.That(ShopRules.RivalMetres(sim, n, ShopGroup.Village), Is.EqualTo(100), "100 m from the square");
            Assert.That(ShopRules.RivalMetres(sim, n, ShopGroup.Village) - ShopRules.Metres(sim, ShopGroup.Village, sim.Config.Game.Shops.SquareTile), Is.EqualTo(100));
            Assert.That(ShopRules.FreePitch(sim), Is.EqualTo(-1));
        }

        // AC-4 (ADR 0095 §3): the grocer and the tea stall cut a step (5% of the reference) when their share falls by more than a third,
        // never under 0.85; they go back a step when it rises by more than a third, never above their own price; the halwai never moves.
        [Test]
        public void PriceCut()
        {
            Simulation sim = Start();
            Rival tea = sim.State.Shops.Rivals.Single(r => r.Id == "village_tea"), halwai = sim.State.Shops.Rivals.Single(r => r.Id == "town_halwai");
            void Season(Rival r, Fixed last, long served, long lost)
            {
                foreach (Rival x in sim.State.Shops.All()) x.SeasonServed = 0;
                System.Array.Clear(sim.State.Shops.LostSeason, 0, 4);
                r.LastShare = last;
                r.SeasonServed = served;
                sim.State.Shops.LostSeason[(int)r.Kind] = lost;
                ShopRules.Rivals(sim);
            }
            Fixed Pf(string v) { return Fixed.FromRatio((long)(decimal.Parse(v, System.Globalization.CultureInfo.InvariantCulture) * 100), 100); }

            Season(tea, Fixed.Parse("0.9"), 700, 300);
            Assert.That(tea.PriceFactor, Is.EqualTo(Fixed.One), "0.9 to 0.7: under a third, no cut");
            Season(tea, Pf("0.9"), 500, 500);
            Assert.That(tea.PriceFactor, Is.EqualTo(Pf("0.95")), "0.9 to 0.5: a third gone, 5% off");
            RivalPriceCut cut = sim.DrainEvents().OfType<RivalPriceCut>().Single();
            Assert.That((cut.RivalId, cut.PriceFactor, cut.Raised), Is.EqualTo(("village_tea", Pf("0.95"), false)));
            Assert.That(tea.LastShare, Is.EqualTo(Fixed.FromRatio(1, 2)), "the season's share kept for the next close");
            Season(tea, Pf("0.9"), 100, 900);
            Season(tea, Pf("0.9"), 100, 900);
            Season(tea, Pf("0.9"), 100, 900);
            Assert.That(tea.PriceFactor, Is.EqualTo(Pf("0.85")), "never under the floor");
            Season(tea, Pf("0.1"), 900, 100);
            Assert.That(tea.PriceFactor, Is.EqualTo(Pf("0.90")), "customers back: a step up");
            Assert.That(sim.DrainEvents().OfType<RivalPriceCut>().Last().Raised, Is.True);
            Season(tea, Pf("0.1"), 900, 100);
            Season(tea, Pf("0.1"), 900, 100);
            Season(tea, Pf("0.1"), 900, 100);
            Assert.That(tea.PriceFactor, Is.EqualTo(Fixed.One), "never above its own price");
            Assert.That(tea.ChangedAt, Is.EqualTo(sim.State.Clock.SimTime));

            Season(halwai, Pf("0.9"), 100, 900);
            Assert.That(halwai.PriceFactor, Is.EqualTo(Fixed.One), "the town's halwai keeps its price");
            Rival grocer = sim.State.Shops.Rivals.Single(r => r.Id == "grocer");
            Season(grocer, Pf("0.9"), 100, 900);
            Assert.That(grocer.PriceFactor, Is.EqualTo(Pf("0.95")), "the grocer reacts too");
        }
    }
}
