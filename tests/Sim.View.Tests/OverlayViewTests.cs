using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-153 (spec 024 §3.8, AC-11, Preview 6): the overlays change nothing, every acre's number is the sim's, every number has a Why?, the smoke is hatched, crime is grey.</summary>
    public class OverlayViewTests
    {
        private const long Hour = 3600;
        private static readonly OverlayKind[] All = { OverlayKind.Fertility, OverlayKind.LandValue, OverlayKind.Coverage, OverlayKind.Happiness, OverlayKind.Crime };

        private static Simulation WithGenerator()
        {
            Simulation sim = Situations.Township();
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, 3_000_000 * 100L), new Posting(Account.GiftsFromFounder, -3_000_000 * 100L));
            Situations.SetCash(sim, 5_000_000_00);
            Assert.That(sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, UtilityRules.Def(sim, UtilityDef.Generator).Work)).Accepted, Is.True);
            for (int i = 0; i < 400 && !UtilityRules.Built(sim, UtilityDef.Generator); i++) sim.AdvanceTo(sim.State.Clock.SimTime + 6 * Hour);
            Assert.That(SmokeRules.Sources(sim).Count, Is.GreaterThan(0), "the generator smokes");
            return sim;
        }

        [Test]
        public void ButtonAppearsFromTheTownship()
        {
            Assert.That(OverlayView.Available(Situations.Town()), Is.False, "before the Township there is no land value to show");
            Assert.That(OverlayView.Available(Situations.Township()), Is.True);
        }

        [Test]
        public void Chooser_HasFiveAndCrimeIsMuted()
        {
            Simulation sim = Situations.Township();
            sim.State.Township.TownLife = new Sim.Core.Town.TownLifeState(); // the town not yet read
            var c = OverlayView.Choices(ViewKit2.Context(sim));
            Assert.That(c.Select(x => x.Kind), Is.EqualTo(All));
            Assert.That(c.Where(x => x.Muted).Select(x => x.Kind), Is.EqualTo(new[] { OverlayKind.Crime }), "before the first reading crime is drawn grey; it still opens, with No crime yet");
            Assert.That(c.All(x => x.Name.Length > 0 && x.Sub.Length > 0), Is.True);
        }

        // An overlay changes nothing and costs no time (spec 024 §3.8).
        [Test]
        public void Overlays_ChangeNothing()
        {
            Simulation sim = WithGenerator();
            ViewContext ctx = ViewKit2.Context(sim);
            ulong hash = sim.State.ComputeHash();
            long now = sim.State.Clock.SimTime;
            foreach (OverlayKind k in All)
            {
                OverlayView.Build(ctx, k);
                foreach (Tile t in sim.State.Land.Tiles) OverlayView.Card(ctx, k, t.Id);
            }
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash));
            Assert.That(sim.State.Clock.SimTime, Is.EqualTo(now));
        }

        [Test]
        public void Keys_HaveFiveStepsAndWords()
        {
            ViewContext ctx = ViewKit2.Context(Situations.Township());
            foreach (OverlayKind k in All)
            {
                OverlayVm vm = OverlayView.Build(ctx, k);
                Assert.That(vm.Title, Is.Not.Empty, k.ToString());
                Assert.That(vm.Note, Is.Not.Empty, k.ToString());
                Assert.That(vm.Labels.Count, Is.EqualTo(5), k.ToString()); // crime too, from the first reading
                Assert.That(vm.Labels.All(l => l.Length > 0 && !l.Contains("{")), Is.True, k + ": " + string.Join(" | ", vm.Labels));
            }
            Assert.That(OverlayView.Build(ctx, OverlayKind.Fertility).Labels, Is.EqualTo(new[] { "under 20", "20–39", "40–59", "60–79", "80+" }));
            Assert.That(OverlayView.Build(ctx, OverlayKind.LandValue).Labels, Is.EqualTo(new[] { "under ×1.00", "×1.00–1.09", "×1.10–1.14", "×1.15–1.24", "×1.25+" }), "the fourth step starts at the pucca offer (ADR 0131)");
            Assert.That(OverlayView.Build(ctx, OverlayKind.Happiness).Labels, Is.EqualTo(new[] { "under 50", "50–59", "60–69", "70–79", "80+" }));
        }

        // AC-11: each acre's number equals the sim's.
        [Test]
        public void EveryAcre_ShowsTheSimsNumber()
        {
            Simulation sim = WithGenerator();
            ViewContext ctx = ViewKit2.Context(sim);
            OverlayVm fert = OverlayView.Build(ctx, OverlayKind.Fertility), lv = OverlayView.Build(ctx, OverlayKind.LandValue), cov = OverlayView.Build(ctx, OverlayKind.Coverage), hap = OverlayView.Build(ctx, OverlayKind.Happiness);
            Fixed edge = sim.Config.Game.Township.Upgrade.PuccaMinLandValue;
            int farm = 0, land = 0, homes = 0;
            foreach (Tile t in sim.State.Land.Tiles)
            {
                bool isFarm = t.Terrain == Terrain.Farm && !LandRules.Locked(sim, t.ParcelId);
                Assert.That(fert.Steps[t.Id] >= 0, Is.EqualTo(isFarm), "fertility tints farm acres only (acre " + t.Id + ")");
                if (isFarm)
                {
                    farm++;
                    long f = t.Fertility.RoundToLong();
                    Assert.That(fert.Steps[t.Id], Is.EqualTo(System.Math.Min(4, (int)(t.Fertility.FloorToLong() / 20))));
                    Assert.That(OverlayView.Card(ctx, OverlayKind.Fertility, t.Id).Value, Is.EqualTo(f.ToString()));
                }
                bool isLand = OverlayView.IsLand(sim, t);
                Assert.That(lv.Steps[t.Id] >= 0 && cov.Steps[t.Id] >= 0, Is.EqualTo(isLand), "land value and coverage tint the same acres (acre " + t.Id + ")");
                if (!isLand) continue;
                land++;
                Fixed v = LandValueRules.Of(sim, t.Id);
                Assert.That(lv.Steps[t.Id] == 3 || lv.Steps[t.Id] == 4, Is.EqualTo(v >= edge), "the fourth step is where a pucca house is offered (acre " + t.Id + ")");
                Assert.That(OverlayView.Card(ctx, OverlayKind.LandValue, t.Id).Value, Is.EqualTo(LandValueViews.Mult(ctx, v)));
                CoverageAt c = OverlayView.Services(sim, t.Id);
                int count = (SchoolRules.IsOpen(sim) && CoverageRules.Within(sim, t.Id, sim.Config.Game.School.Tile, sim.Config.Game.School.Reach) ? 1 : 0)
                    + (ClinicRules.IsOpen(sim) && CoverageRules.Within(sim, t.Id, ClinicRules.Def(sim).Tile, ClinicRules.Def(sim).Reach) ? 1 : 0)
                    + (CivicRules.Staffed(sim, CivicDef.Watchman) && CivicRules.Covered(sim, CivicDef.Watchman, t.Id) ? 1 : 0)
                    + (UtilityRules.HasWater(sim, t.Id) ? 1 : 0);
                Assert.That(c.Count, Is.EqualTo(count), "coverage counts the four services (acre " + t.Id + ")");
                Assert.That(cov.Steps[t.Id], Is.EqualTo(count));
                Assert.That(OverlayView.Card(ctx, OverlayKind.Coverage, t.Id).Value, Is.EqualTo(count + " of 4"));
            }
            foreach (Household x in sim.State.Citizens.Households)
            {
                Building home = sim.State.Land.Buildings.First(b => b.Id == x.HomeId);
                Assert.That(hap.Steps[home.TileId], Is.GreaterThanOrEqualTo(0), "a household's acre is tinted");
                homes++;
            }
            Assert.That(hap.Steps.Count(s => s >= 0), Is.GreaterThan(0).And.LessThanOrEqualTo(homes), "happiness tints only acres with one of your households");
            Assert.That(farm, Is.GreaterThan(0));
            Assert.That(land, Is.GreaterThan(farm / 2));
        }

        [Test]
        public void Happiness_IsTheMeanOfTheHouseholdsOnTheAcre()
        {
            Simulation sim = Situations.Township();
            ViewContext ctx = ViewKit2.Context(sim);
            Household x = sim.State.Citizens.Households[0];
            int tile = sim.State.Land.Buildings.First(b => b.Id == x.HomeId).TileId;
            var here = sim.State.Citizens.Households.Where(h => sim.State.Land.Buildings.First(b => b.Id == h.HomeId).TileId == tile).ToList();
            foreach (Household h in here) h.Happiness = Fixed.FromInt(63);
            OverlayCardVm card = OverlayView.Card(ctx, OverlayKind.Happiness, tile);
            Assert.That(card.Value, Is.EqualTo("63"), "the mean of " + here.Count + " household(s)");
            Assert.That(OverlayView.Build(ctx, OverlayKind.Happiness).Steps[tile], Is.EqualTo(2), "60–69 is the third step");
            here[0].Happiness = Fixed.FromInt(41);
            if (here.Count > 1) Assert.That(OverlayView.Card(ctx, OverlayKind.Happiness, tile).Value, Is.Not.EqualTo("63"), "the mean moves with each household");
        }

        // The smoke hatch (spec 024 §3.8, G7): the footprint of every smoking building, and nothing else.
        [Test]
        public void Smoke_IsTheFootprintOfEverySource()
        {
            Simulation sim = WithGenerator();
            ViewContext ctx = ViewKit2.Context(sim);
            OverlayVm cov = OverlayView.Build(ctx, OverlayKind.Coverage);
            var expect = new System.Collections.Generic.HashSet<int>();
            foreach (SmokeRules.Source s in SmokeRules.Sources(sim)) foreach (int t in SmokeRules.Footprint(sim, s.Tile, s.Def)) expect.Add(t);
            Assert.That(expect.Count, Is.GreaterThan(1), "the generator reaches more than its own acre");
            for (int t = 0; t < cov.Smoke.Length; t++) Assert.That(cov.Smoke[t], Is.EqualTo(expect.Contains(t)), "acre " + t);
            Assert.That(OverlayView.Build(ctx, OverlayKind.LandValue).Smoke.Any(b => b), Is.False, "only the coverage overlay hatches smoke");
            int smoky = expect.First(t => OverlayView.IsLand(sim, sim.State.Land.Tiles[t]));
            OverlayCardVm card = OverlayView.Card(ctx, OverlayKind.Coverage, smoky);
            Assert.That(card.Line, Does.Contain("Smoke reaches here"));
            WhyVm why = WhyView.Build(ctx, card.WhyKey, card.WhyId);
            Assert.That(why.Rows.Select(r => r.Label), Does.Contain("The smoke"));
        }

        [Test]
        public void Smoke_StopsWhenTheGridReplacesTheGenerator()
        {
            Simulation sim = WithGenerator();
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, 3_000_000 * 100L), new Posting(Account.GiftsFromFounder, -3_000_000 * 100L));
            Assert.That(sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, UtilityRules.Def(sim, UtilityDef.Grid).Work)).Accepted, Is.True);
            sim.AdvanceTo(sim.State.Clock.SimTime + 6 * Hour);
            Assert.That(OverlayView.Build(ViewKit2.Context(sim), OverlayKind.Coverage).Smoke.Any(b => b), Is.False);
        }

        [Test]
        public void Crime_IsGreyWithNoCrimeYet()
        {
            Simulation sim = Situations.Township();
            sim.State.Township.TownLife = new Sim.Core.Town.TownLifeState(); // not yet read: Sprint 24's reading lights the overlay up (OverlayTests in TownLifeViewTests)
            ViewContext ctx = ViewKit2.Context(sim);
            OverlayVm vm = OverlayView.Build(ctx, OverlayKind.Crime);
            Assert.That(vm.Steps.All(s => s == OverlayVm.NoStep || s == 0), Is.True, "one grey step");
            Assert.That(vm.Steps.Count(s => s == 0), Is.EqualTo(OverlayView.Build(ctx, OverlayKind.LandValue).Steps.Count(s => s >= 0)), "on the same land as the others");
            OverlayCardVm card = OverlayView.Card(ctx, OverlayKind.Crime, 0);
            Assert.That(card.Value, Is.EqualTo("No crime yet"));
            Assert.That(card.WhyKey, Is.Null, "no number, so no Why?");
        }

        // AC-11: every number has a Why?: each acre's card, in every overlay, names a source that exists and builds in words.
        [Test]
        public void EveryNumberHasWhy()
        {
            Simulation sim = WithGenerator();
            ViewContext ctx = ViewKit2.Context(sim);
            int withWhy = 0;
            foreach (OverlayKind k in All)
                foreach (Tile t in sim.State.Land.Tiles)
                {
                    OverlayCardVm card = OverlayView.Card(ctx, k, t.Id);
                    Assert.That(card.Title, Is.Not.Empty);
                    Assert.That(card.Line, Is.Not.Empty, k + " acre " + t.Id);
                    Assert.That(card.OpenLabel, Is.EqualTo("Open the acre"));
                    Assert.That(card.Line + card.Title + card.Value, Does.Not.Contain("{"), k + " acre " + t.Id);
                    if (card.Value == null) { Assert.That(card.WhyKey, Is.Null, k + " acre " + t.Id + ": an acre with no number has no ?"); continue; }
                    Assert.That(card.WhyKey, Is.Not.Null, k + " acre " + t.Id + " shows " + card.Value + " and needs a ?");
                    Assert.That(WhyView.Has(card.WhyKey), Is.True, card.WhyKey);
                    WhyVm why = WhyView.Build(ctx, card.WhyKey, card.WhyId);
                    Assert.That(why.Rows.Count, Is.GreaterThan(0), card.WhyKey);
                    Assert.That(why.Footnote, Is.Not.Empty, card.WhyKey);
                    foreach (WhyRowVm r in why.Rows) Assert.That((r.Label ?? "") + (r.Value ?? "") + (r.Note ?? ""), Does.Not.Contain("{").And.Not.Contain("×1.00 ×"), card.WhyKey);
                    withWhy++;
                }
            Assert.That(withWhy, Is.GreaterThan(100));
        }

        [Test]
        public void CoverageWhy_ListsEachServiceWithItsDistance()
        {
            Simulation sim = Situations.TownshipBuilt();
            ViewContext ctx = ViewKit2.Context(sim);
            int tile = sim.State.Land.Tiles.First(t => OverlayView.IsLand(sim, t) && OverlayView.Services(sim, t.Id).Count >= 2).Id;
            WhyVm why = WhyView.Build(ctx, OverlayView.CoverageWhyKey, tile);
            Assert.That(why.Rows.Take(4).Select(r => r.Label), Is.EqualTo(new[] { "School", "Clinic", "Watchman", "Clean water" }));
            Assert.That(why.Rows.Take(3).All(r => r.Value.StartsWith("Yes · ") || r.Value.StartsWith("No · ")), Is.True, string.Join(" | ", why.Rows.Select(r => r.Value)));
            Assert.That(why.Value, Is.EqualTo(OverlayView.Services(sim, tile).Count + " of 4"));
        }
    }
}
