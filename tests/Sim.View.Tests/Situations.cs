using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Time;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>
    /// Seeded Phase 1 situations for the views' sweeping tests (spec 008 AC-1, AC-15): a new game, a growing farm with people and
    /// trees, the same farm ready to harvest with goods on offer, and a later game with loans and a closed season.
    /// </summary>
    public static class Situations
    {
        private const long Hour = 3600, Day = 86400;

        public static Simulation Rich(ulong seed = 42)
        {
            return ViewKit.NewGame(seed: seed, edit: (f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000") : j);
        }

        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Preview(c).Reason); }

        private static long T(Simulation sim) { return sim.State.Clock.SimTime; }

        /// <summary>Potato on the well acres, a garden bed, a mango, a hut and a hand with an order.</summary>
        public static Simulation Growing()
        {
            Simulation sim = Rich();
            Ok(sim, new Sow(T(sim), 86, "potato", SeedSource.Certified, "compost"));
            Ok(sim, new Sow(T(sim), 102, "radish", SeedSource.Certified, "none"));
            GardenBed bed = sim.State.Farm.Beds[0];
            Ok(sim, new SowGarden(T(sim), bed.Id, "garden_spinach"));
            TreeSlot slot = sim.State.Land.Slots.First(s => s.Kind == SlotKind.Yard && s.TreeId == 0);
            Ok(sim, new PlantTree(T(sim), slot.Id, "mango"));
            Ok(sim, new BuildHandHut(T(sim), 119));
            for (int i = 0; i < 24 * 10 && sim.State.People.Hands.Count == 0; i++)
            {
                sim.AdvanceTo(T(sim) + Hour);
                if (sim.State.People.Applicants.Count > 0) Ok(sim, new OfferContract(T(sim), sim.State.People.Applicants[0].Id));
            }
            Assert.That(sim.State.People.Hands.Count, Is.EqualTo(1));
            Ok(sim, new SetLabourSplit(T(sim), 25));
            sim.AdvanceTo(T(sim) + 2 * Hour);
            return sim;
        }

        /// <summary>The growing farm a day and a half later: ready crops and goods on offer.</summary>
        public static Simulation Ready()
        {
            Simulation sim = Growing();
            sim.AdvanceTo(T(sim) + 30 * Hour);
            foreach (Crop c in sim.State.Farm.Crops.Where(c => c.State == CropState.Ready && c.TileId == 102).ToList())
                Ok(sim, new Harvest(T(sim), c.TileId));
            int radish = sim.Config.Game.Market.Goods.ToList().FindIndex(g => g.Id == "radish");
            if (sim.State.Market.InStore(radish) > 0) Ok(sim, new Offer(T(sim), "radish", sim.State.Market.InStore(radish) / 2));
            return sim;
        }

        /// <summary>A moneylender loan and a closed season (a report), with the events of the last away advance.</summary>
        public static Simulation Later(out IReadOnlyList<SimEvent> away)
        {
            Simulation sim = Ready();
            Ok(sim, new TakeLoan(T(sim), "moneylender", 20_000 * 100, 4));
            sim.DrainEvents();
            long next = (T(sim) / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason;
            sim.AdvanceTo(next + 2 * Hour, AdvanceMode.Away);
            away = sim.DrainEvents();
            Assert.That(sim.State.Finance.Reports.Count, Is.GreaterThan(0));
            return sim;
        }

        /// <summary>
        /// The Hamlet with a family house (spec 014 §7): the hand alone in his hut, and Manoj's family of five in the house (his mother,
        /// his wife from another village, a son and a daughter) with his late father kept in the tree.
        /// </summary>
        public static Simulation Town()
        {
            Simulation sim = Ready();
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "hamlet") sim.State.Era.Current = i;
            Ok(sim, new BuildFamilyHouse(T(sim), HouseTile(sim)));
            sim.AdvanceTo(T(sim) + 48 * Hour);
            Building house = sim.State.Land.Buildings.Single(b => HouseRules.IsHouse(sim, b));
            CitizensState town = sim.State.Citizens;
            var x = new Household { Id = town.NextHouseholdId++, HomeId = house.Id };
            Citizen Add(string name, int age, long father = 0, long mother = 0)
            {
                int n = sim.Config.Game.People.Names.ToList().FindIndex(d => d.Id == name);
                var c = new Citizen { Id = town.NextCitizenId++, Name = n, Female = sim.Config.Game.People.Names[n].Female, BornSeason = CitizenRules.BornFor(sim, age),
                    HouseholdId = x.Id, Health = Fixed.One, Father = father, Mother = mother, Skill = Fixed.FromInt(40), Village = 1 };
                town.Citizens.Add(c);
                return c;
            }
            Citizen grandfather = Add("mohan", 74), grandmother = Add("kamala", 66);
            grandfather.Gone = CitizenGone.Died;
            grandfather.GoneAt = T(sim);
            grandfather.Spouse = grandmother.Id;
            grandmother.Spouse = grandfather.Id;
            Citizen manoj = Add("manoj", 38, grandfather.Id, grandmother.Id), sita = Add("usha", 35);
            manoj.Spouse = sita.Id;
            sita.Spouse = manoj.Id;
            Citizen ravi = Add("ravi", 12, manoj.Id, sita.Id), asha = Add("asha", 9, manoj.Id, sita.Id);
            foreach (Citizen c in new[] { manoj, sita, ravi, asha, grandmother }) x.Members.Add(c.Id);
            town.Households.Add(x);
            HouseholdRules.SetBudget(sim, x);
            return sim;
        }

        /// <summary>An owned farm acre with nothing growing, for a family house.</summary>
        public static int HouseTile(Simulation sim)
        {
            return sim.State.Land.Tiles.First(t => t.Terrain == Terrain.Farm && sim.State.Land.Parcels[t.ParcelId].Owned && sim.State.Farm.CropOn(t.Id) == null).Id;
        }

        /// <summary>The town with a flour mill founded on an empty acre (hired worker), built, and a working day gone by (spec 016).</summary>
        public static Simulation Mill()
        {
            Simulation sim = Town();
            int tile = sim.State.Land.Tiles.First(t => Sim.Core.Business.BusinessRules.Place(sim, t.Id) == RejectReason.None).Id;
            Ok(sim, new FoundBusiness(T(sim), "flour_mill", tile, 150_000_00, false));
            sim.AdvanceTo((T(sim) / 86_400 + 2) * 86_400 + 12 * 3600);
            return sim;
        }

        /// <summary>The Village with the mill and a bakery beside it (hired baker), built, a morning round gone by (spec 016 §3.9c).</summary>
        public static Simulation Bakery()
        {
            Simulation sim = Mill();
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "village") sim.State.Era.Current = i;
            int tile = sim.State.Land.Tiles.First(t => Sim.Core.Business.BusinessRules.Place(sim, t.Id) == RejectReason.None).Id;
            Ok(sim, new FoundBusiness(T(sim), "bakery", tile, 200_000_00, false));
            sim.AdvanceTo((T(sim) / 86_400 + 3) * 86_400 + 12 * 3600);
            return sim;
        }

        /// <summary>The bakery's town with a cattle shed (a buffalo and a cow) and a dairy, a morning round gone by (spec 016 §3.9b).</summary>
        public static Simulation Dairy()
        {
            Simulation sim = Bakery();
            Ok(sim, new BuildShed(T(sim)));
            sim.AdvanceTo(T(sim) + 25 * Hour);
            Ok(sim, new BuyAnimal(T(sim), "buffalo"));
            Ok(sim, new BuyAnimal(T(sim), "cow"));
            int tile = sim.State.Land.Tiles.First(t => Sim.Core.Business.BusinessRules.Place(sim, t.Id) == RejectReason.None).Id;
            Ok(sim, new FoundBusiness(T(sim), "dairy", tile, 300_000_00, false));
            sim.AdvanceTo((T(sim) / 86_400 + 3) * 86_400 + 12 * 3600);
            return sim;
        }

        public static IEnumerable<Simulation> All()
        {
            yield return Rich();
            yield return Growing();
            yield return Ready();
            yield return Later(out _);
            yield return Town();
            yield return Mill();
            yield return Bakery();
            yield return Dairy();
        }
    }
}

namespace Sim.View.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Sim.Core;
    using Sim.Core.Definitions;
    using Sim.Core.Land;
    using Sim.View.Views;

    /// <summary>The Sprint 7 panels (ADR 0043) for the sweeping tests; each new sheet joins here.</summary>
    public static class Panels
    {
        private static KeyValuePair<string, SheetVm> N(string name, SheetVm s) { return new KeyValuePair<string, SheetVm>(name, s); }

        public static IEnumerable<KeyValuePair<string, SheetVm>> All(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            yield return N("menu", MenuSheet.Build(ctx));
            yield return N("lands", LandsSheet.Build(ctx));
            yield return N("money", MoneySheet.Build(ctx));
            foreach (var r in sim.State.Finance.Reports) yield return N("report " + r.Period, ReportSheet.Build(ctx, r.Period));
            for (int i = 0; i < sim.Config.Game.Credit.Lenders.Count; i++)
            {
                SheetVm amounts = BorrowSheet.Build(ctx, i, null);
                yield return N("borrow " + i, amounts);
                foreach (LineVm l in amounts.Lines) yield return N("borrow " + i + " " + l.Label, BorrowSheet.Build(ctx, i, l.Action.Opens.Value.Arg));
            }
            foreach (var loan in sim.State.Credit.Loans) if (loan.IsOpen) yield return N("repay " + loan.Id, Repay.Build(ctx, loan.Id));
            Tile unowned = sim.State.Land.Tiles.First(t => t.Terrain == Terrain.Farm && !sim.State.Land.Parcels[t.ParcelId].Owned);
            yield return N("land loan", LandLoanSheet.Build(ctx, unowned.Id));
            yield return N("founder", FounderSheet.Build(ctx));
            yield return N("progress", ProgressSheet.Build(ctx));
            yield return N("letter", Letter.List(ctx));
            for (int g = 0; g < sim.Config.Game.Market.Goods.Count; g++)
            {
                if (sim.State.Market.InStore(g) == 0) continue;
                yield return N("good " + g, GoodSheet.Build(ctx, g));
                SheetVm offer = GoodSheet.Offer(ctx, g, null);
                yield return N("offer " + g, offer);
                foreach (LineVm l in offer.Lines) yield return N("offer " + g + " " + l.Label, GoodSheet.Offer(ctx, g, l.Action.Opens.Value.Arg));
            }
            SheetVm inputs = BuyInputsSheet.Build(ctx);
            yield return N("inputs", inputs);
            foreach (LineVm l in inputs.Lines) yield return N("input " + l.Label, BuyInputsSheet.One(ctx, (int)l.Action.Opens.Value.Id));
            SheetVm mandi = MandiSheet.Build(ctx, null);
            yield return N("mandi", mandi);
            foreach (LineVm l in mandi.Lines.Where(x => x.Action != null))
            {
                SheetVm load = MandiSheet.Load(ctx, (int)l.Action.Opens.Value.Id, null);
                yield return N("mandi load " + l.Label, load);
                foreach (LineVm c in load.Lines) yield return N("mandi with " + l.Label + " " + c.Label, MandiSheet.Build(ctx, c.Action.Opens.Value.Arg));
            }
        }
    }
}
