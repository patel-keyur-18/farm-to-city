using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 008 AC-1, AC-14: views are read only and the HUD equals the state.</summary>
    public class ViewTests
    {
        private const long Hour = 3600, Day = 86400;

        /// <summary>Every view builder over the current state, as the presentation would build them after a refresh.</summary>
        private static void BuildEverything(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            HudView.Build(ctx);
            MapView.Build(ctx);
            CollectAll.Commands(sim, ctx.Now);
            StoreSheet.Build(ctx);
            foreach (Tile t in sim.State.Land.Tiles)
            {
                FieldSheets.ForTile(ctx, t.Id);
                if (t.Terrain == Terrain.Farm) SowSheet.Build(ctx, t.Id);
            }
            foreach (TreeSlot s in sim.State.Land.Slots) FieldSheets.ForSlot(ctx, s.Id);
            foreach (GardenBed b in sim.State.Farm.Beds) FieldSheets.ForBed(ctx, b.Id);
        }

        // AC-1: building every view in every Phase 1 situation leaves the state hash unchanged.
        [Test]
        public void ReadOnly()
        {
            Simulation sim = ViewKit.NewGame(seed: 3);
            ViewContext ctx = ViewKit2.Context(sim);
            ulong hash = sim.State.ComputeHash();
            BuildEverything(ctx);
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "a new game");

            long t = sim.State.Clock.SimTime;
            sim.Execute(new Sow(t, 20, "potato", SeedSource.Certified, "none"));
            sim.Execute(new Sow(t, 28, "radish", SeedSource.Certified, "compost"));
            sim.Execute(new SowGarden(t, 1, "garden_radish"));
            sim.Execute(new PlantTree(t, sim.State.Land.Slots.First(s => s.Kind == SlotKind.Yard).Id, "banana"));
            foreach (long step in new[] { Hour, 2 * Hour, 10 * Hour, Day, 2 * Day, 4 * Day })
            {
                sim.AdvanceTo(sim.State.Clock.SimTime + step);
                ulong before = sim.State.ComputeHash();
                BuildEverything(ctx);
                Assert.That(sim.State.ComputeHash(), Is.EqualTo(before), "after +" + step + " s");
            }
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * Day); // crops wait ready, then spoil
            ulong end = sim.State.ComputeHash();
            BuildEverything(ctx);
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(end), "with crops waiting past their grace");
        }

        // AC-14: the HUD's cash, store %, ready count equal the state's in a seeded game at 10 sample times.
        [Test]
        public void Hud()
        {
            Simulation sim = ViewKit.NewGame(seed: 11);
            ViewContext ctx = ViewKit2.Context(sim);
            sim.Execute(new Sow(sim.State.Clock.SimTime, 20, "radish", SeedSource.Certified, "none"));
            sim.Execute(new Sow(sim.State.Clock.SimTime, 28, "potato", SeedSource.Certified, "none"));
            sim.Execute(new SowGarden(sim.State.Clock.SimTime, 1, "garden_radish"));
            sim.Execute(new SowGarden(sim.State.Clock.SimTime, 2, "garden_spinach"));
            var seen = new HashSet<int>();
            for (int i = 0; i < 10; i++)
            {
                sim.AdvanceTo(sim.State.Clock.SimTime + (i + 1) * 5400L);
                HudVm hud = HudView.Build(ctx);
                Assert.That(hud.Cash, Is.EqualTo(sim.State.Ledger.Cash));
                long used = Store.UsedGrams(sim), capacity = Store.CapacityGrams(sim);
                Assert.That(hud.StorePercent, Is.EqualTo((int)(used * 100 / capacity)));
                int ready = sim.State.Farm.Crops.Count(c => c.State == CropState.Ready) + sim.State.Farm.Beds.Count(b => b.State == BedState.Ready)
                    + sim.State.Farm.Trees.Count(x => x.RipeGrams > 0);
                Assert.That(hud.ReadyCount, Is.EqualTo(ready));
                seen.Add(ready);
                if (ready == 0) Assert.That(hud.CollectText, Is.EqualTo("Collect all · 0"));
                // Keep things growing so the ready count varies.
                foreach (GardenBed b in sim.State.Farm.Beds.ToList()) if (b.State == BedState.Empty) sim.Execute(new SowGarden(sim.State.Clock.SimTime, b.Id, "garden_radish"));
            }
            Assert.That(seen.Count, Is.GreaterThan(1), "the ready count changed during the run");
        }

        [Test]
        public void Hud_Text()
        {
            Simulation sim = ViewKit.NewGame(seed: 1, localSeconds: 14 * Hour + 5 * 60);
            HudVm hud = HudView.Build(ViewKit2.Context(sim));
            Assert.That(hud.ClockText, Is.EqualTo("14:05"));
            Assert.That(hud.DateText, Does.StartWith("Spring · Day 1 of 4 · Year 1"));
            Assert.That(hud.CashText, Is.EqualTo("2,000").Or.EqualTo(hud.CashFullText.Replace(" coins", "")));
            Assert.That(hud.CashFullText, Does.EndWith("coins"));
            Assert.That(HudView.Build(ViewKit2.Context(sim, Sim.View.Text.NumberFormat.Western)).CashFullText, Does.EndWith("coins"));
        }

        [Test]
        public void CollectAll_NearestToSpoilingFirst_AndStoreOnly()
        {
            Simulation sim = ViewKit.NewGame(seed: 5);
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new Sow(t, 20, "radish", SeedSource.Certified, "none")).Accepted, Is.True);   // ready at +2 h
            Assert.That(sim.Execute(new SowGarden(t, 1, "garden_radish")).Accepted, Is.True);                    // ready at +10 min
            sim.AdvanceTo(t + Hour);
            Assert.That(sim.Execute(new Sow(sim.State.Clock.SimTime, 28, "radish", SeedSource.Certified, "none")).Accepted, Is.True); // ready at +3 h
            sim.AdvanceTo(t + 30 * Hour);
            Assert.That(CollectAll.ReadyCount(sim), Is.EqualTo(3));
            List<Command> cmds = CollectAll.Commands(sim, sim.State.Clock.SimTime);
            Assert.That(cmds.Select(c => c.GetType().Name), Is.EqualTo(new[] { "PickGarden", "Harvest", "Harvest" }), "the garden was ready first, then the field sown first, then the second");
            Assert.That(((Harvest)cmds[1]).TileId, Is.EqualTo(20));
            Assert.That(((Harvest)cmds[2]).TileId, Is.EqualTo(28));
            foreach (Command c in cmds) { CommandResult r = sim.Execute(c); Assert.That(r.Accepted, Is.True, c.GetType().Name + " " + r); }
            Assert.That(CollectAll.ReadyCount(sim), Is.EqualTo(0));

            // A nearly full store takes what fits and leaves the rest where it is.
            Simulation full = ViewKit.NewGame(seed: 5);
            long t2 = full.State.Clock.SimTime;
            full.Execute(new Sow(t2, 20, "radish", SeedSource.Certified, "none"));
            full.Execute(new Sow(t2, 28, "radish", SeedSource.Certified, "none"));
            full.AdvanceTo(t2 + 30 * Hour);
            full.State.Market.Stock[full.Config.Game.Market.IndexOf("wheat")] += Store.CapacityGrams(full) - Store.UsedGrams(full) - 30_000;
            int accepted = CollectAll.Commands(full, full.State.Clock.SimTime).Count(c => full.Execute(c).Accepted);
            Assert.That(accepted, Is.LessThan(2), "something stays in the field");
            Assert.That(CollectAll.ReadyCount(full), Is.GreaterThan(0));
        }
    }
}
