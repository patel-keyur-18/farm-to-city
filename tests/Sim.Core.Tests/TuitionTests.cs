using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>US-244 (spec 028 §3.5, ADR 0161): the tuition centre's demand from the schooling budget, its capacity and fee, the skill a season of tuition adds, and its stars.</summary>
    public class TuitionTests
    {
        private static Business.Business Centre(out Simulation sim)
        {
            sim = InvoiceTests.Township();
            return InvoiceTests.Found(sim, "tuition");
        }

        private static TuitionDef D(Simulation sim) { return sim.Config.Game.Businesses.Types.Single(t => t.IsTuition).Tuition; }

        /// <summary>A family of yours with children of the given ages and a schooling budget of <paramref name="places"/> fees.</summary>
        private static Household Family(Simulation sim, int places, params int[] kids)
        {
            var ages = new int[kids.Length + 2];
            ages[0] = 36; ages[1] = 33;
            kids.CopyTo(ages, 2);
            Household x = AddFamily(sim, 0, ages);
            x.Savings = 1_000_000 * Coin;
            x.Schooling = places * TuitionRules.FeeAt(sim, D(sim), Fixed.One);
            return x;
        }

        private static bool Near(Fixed a, Fixed b) { Fixed e = Fixed.FromRatio(1, 1000); return a - b < e && b - a < e; }

        private static void Full(Business.Business b) { b.Stars = Fixed.FromInt(5); } // take-up of every child a family can pay for

        private static void ToHour(Simulation sim, int hour)
        {
            long t = sim.State.Clock.SimTime / 86400 * 86400 + hour * 3600;
            if (t <= sim.State.Clock.SimTime) t += 86400;
            sim.AdvanceTo(t);
        }

        private static void Reconciles(Simulation sim)
        {
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            foreach (Business.Business b in sim.State.Businesses.Businesses) Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // The type loads with its numbers: a Township business that teaches, not a shop, a kiln or a maker.
        [Test]
        public void Content_Is_Checked()
        {
            Simulation sim = InvoiceTests.Township();
            BusinessTypeDef t = sim.Config.Game.Businesses.Types.Single(x => x.IsTuition);
            Assert.That(t.IsShop || t.IsKiln || t.ByRecipe, Is.False);
            Assert.That((t.Tuition.Capacity, t.Tuition.Fee, t.OpenHour, t.CloseHour, t.Tuition.GainMax), Is.EqualTo((40, 800L * 100, 15, 19, 10)));
            Assert.Throws<Sim.Content.ContentException>(() => Game((f, j) => f == "businesses.json" ? j.Replace("\"batches\": 2", "\"batches\": 3") : j), "4 hours can't split into 3 batches");
            Assert.Throws<Sim.Content.ContentException>(() => Game((f, j) => f == "businesses.json" ? j.Replace("\"gainMax\": 10", "\"gainMax\": 0") : j), "at least one season's gain");
        }

        // AC-14 Demand: a household's schooling budget buys floor(budget ÷ fee) places, never more than its children of 7 to 17; your children come before the old village's.
        [Test]
        public void Demand()
        {
            Business.Business b = Centre(out Simulation sim);
            Household few = Family(sim, 1, 9, 12);            // pays for one of two
            Household many = Family(sim, 5, 8, 17, 18, 6);    // two of age (18 and 6 are out), pays for both
            Household poor = Family(sim, 0, 10);              // pays for none
            Full(b);
            TuitionRules.Enrol(sim, b);
            var mine = b.Pupils.Select(id => sim.State.Citizens.Citizen(id)).ToList();
            Assert.That(mine.Count(c => c.HouseholdId == few.Id), Is.EqualTo(1), "floor(budget / fee) places");
            Assert.That(mine.Count(c => c.HouseholdId == many.Id), Is.EqualTo(2), "the children of age, not the 18 or the 6");
            Assert.That(mine.Count(c => c.HouseholdId == poor.Id), Is.EqualTo(0), "no budget, no place");
            Assert.That(mine.All(c => { int a = CitizenRules.Age(sim, c); return a >= 7 && a <= 17; }), Is.True);
            Assert.That(b.CohortPupils, Is.EqualTo(TuitionRules.CohortAfford(sim, D(sim), TuitionRules.FeeAt(sim, D(sim), Fixed.One))).And.GreaterThan(0), "the old village fills the rest, as a head-count");
            Assert.That(TuitionRules.Pupils(b), Is.LessThanOrEqualTo(D(sim).Capacity));
        }

        // Stars set take-up: none send 40% of those who can pay, five send them all.
        [Test]
        public void Stars_Set_Take_Up()
        {
            Business.Business b = Centre(out Simulation sim);
            TuitionDef d = D(sim);
            Assert.That(TuitionRules.TakeUp(d, Fixed.Zero), Is.EqualTo(Fixed.FromRatio(2, 5)));
            Assert.That(TuitionRules.TakeUp(d, Fixed.FromInt(5)), Is.EqualTo(Fixed.One));
            Assert.That(TuitionRules.TakeUp(d, Fixed.FromRatio(5, 2)), Is.EqualTo(Fixed.FromRatio(7, 10)), "halfway");
            Family(sim, 10, 8, 9, 10, 11, 12, 13, 14, 15);
            b.Stars = Fixed.FromInt(5);
            TuitionRules.Enrol(sim, b);
            int high = TuitionRules.Pupils(b);
            b.Stars = Fixed.Zero;
            TuitionRules.Enrol(sim, b);
            Assert.That(TuitionRules.Pupils(b), Is.LessThan(high), "fewer come to a centre with no stars");
            Assert.That(TuitionRules.Pupils(b), Is.GreaterThan(0));
        }

        // AC-14 Capacity_Fee: 20 a batch, two batches a evening, 40 in all, whoever is over waits; a fee presets from the reference.
        [Test]
        public void Capacity_Fee()
        {
            Business.Business b = Centre(out Simulation sim);
            TuitionDef d = D(sim);
            Assert.That(d.Capacity, Is.EqualTo(40));
            for (int i = 0; i < 12; i++) Family(sim, 5, 8, 10, 12, 14);
            Full(b);
            TuitionRules.Enrol(sim, b);
            Assert.That(TuitionRules.Pupils(b), Is.EqualTo(40), "never more than the places");
            Assert.That((TuitionRules.InBatch(b, d, 0), TuitionRules.InBatch(b, d, 1), TuitionRules.InBatch(b, d, 2)), Is.EqualTo((20, 20, 0)));
            long reference = TuitionRules.FeeAt(sim, d, Fixed.One);
            Assert.That(d.Presets.Select(p => TuitionRules.FeeAt(sim, d, p)), Is.EqualTo(new[] { reference, reference * 9 / 10, reference * 11 / 10, reference * 12 / 10 }));
            Ok(sim, new SetTuitionFee(sim.State.Clock.SimTime, b.Id, 3));
            Assert.That(b.PriceFactor, Is.EqualTo(Fixed.FromRatio(12, 10)));
            Assert.That(b.Fee, Is.EqualTo(Fixed.One), "the pupils in keep the fee they came at");
            Assert.That(sim.Execute(new SetTuitionFee(sim.State.Clock.SimTime, b.Id, 9)).Reason, Is.EqualTo(RejectReason.InvalidArgument));
            TuitionRules.Enrol(sim, b);
            Assert.That(b.Fee, Is.EqualTo(Fixed.FromRatio(12, 10)), "a new season's pupils pay the new fee");
        }

        // A dearer fee fits fewer children into the same budget.
        [Test]
        public void Dear_Fee_Fits_Fewer()
        {
            Business.Business b = Centre(out Simulation sim);
            Family(sim, 5, 8, 10, 12, 14, 16); // five fees of budget
            Full(b);
            long fee = TuitionRules.FeeAt(sim, D(sim), Fixed.One);
            sim.State.Citizens.Households.Last().Schooling = 4 * fee + fee / 2;
            TuitionRules.Enrol(sim, b);
            int own = b.Pupils.Count;
            Assert.That(own, Is.EqualTo(4), "four and a half fees buy four");
            Ok(sim, new SetTuitionFee(sim.State.Clock.SimTime, b.Id, 3));
            TuitionRules.Enrol(sim, b);
            Assert.That(b.Pupils.Count, Is.EqualTo(3), "at +20% the same budget pays for three");
        }

        // The evening: the batches' pupils pay the day's fee (the season's over its days) and use the day's books; a day shut earns nothing; the books balance.
        [Test]
        public void Fees_By_The_Day()
        {
            Business.Business b = Centre(out Simulation sim);
            Family(sim, 3, 8, 10, 12);
            Full(b);
            TuitionRules.Enrol(sim, b);
            int pupils = TuitionRules.Pupils(b);
            Assert.That(pupils, Is.GreaterThan(3));
            long cash = b.Ledger.Cash;
            ToHour(sim, 15);
            ToHour(sim, 19);
            long day = TuitionRules.DayFee(sim, b), supplies = TuitionRules.DaySupplies(sim, b);
            Assert.That(b.TuitionFees, Is.EqualTo(day * pupils), "every pupil paid a day's fee");
            long wage = 0; // the worker is paid upfront, not by the day
            Assert.That(b.Ledger.Cash - cash + wage, Is.EqualTo(day * pupils - supplies * pupils), "fees in, books out");
            Reconciles(sim);
        }

        // Nobody comes the evening nobody teaches: no worker, no fee.
        [Test]
        public void Shut_Day_Earns_Nothing()
        {
            Business.Business b = Centre(out Simulation sim);
            Family(sim, 3, 8, 10, 12);
            Full(b);
            TuitionRules.Enrol(sim, b);
            b.Worker = WorkerKind.None; // no teacher
            ToHour(sim, 15);
            ToHour(sim, 19);
            Assert.That(b.TuitionFees, Is.EqualTo(0));
        }

        // AC-14 Skill_Gain: a child of yours who sat a season gains a point of farming and of crafting, up to ten in all; the old village's children are a head-count and gain nothing named.
        [Test]
        public void Skill_Gain()
        {
            Business.Business b = Centre(out Simulation sim);
            Household x = Family(sim, 3, 9, 12);
            Full(b);
            TuitionRules.Enrol(sim, b);
            Citizen c = sim.State.Citizens.Citizen(b.Pupils[0]);
            c.Skill = Fixed.FromInt(20);
            c.Crafting = Fixed.FromInt(30);
            ToHour(sim, 15);
            ToHour(sim, 19); // a season's days have run for this pupil: the fees say so
            TuitionRules.AtClose(sim);
            Assert.That((c.Skill, c.Crafting, c.TuitionSeasons), Is.EqualTo((Fixed.FromInt(21), Fixed.FromInt(31), 1)), "+1 each, a season of tuition");
            for (int i = 0; i < 12; i++)
            {
                x.Schooling = 3 * TuitionRules.FeeAt(sim, D(sim), Fixed.One);
                Full(b);
                TuitionRules.Enrol(sim, b);
                b.TuitionFees = 1; // the centre ran
                TuitionRules.AtClose(sim);
            }
            Assert.That(c.TuitionSeasons, Is.EqualTo(10), "ten seasons is the most a child can sit");
            Assert.That(c.Skill, Is.EqualTo(Fixed.FromInt(30)));
            x.Schooling = 3 * TuitionRules.FeeAt(sim, D(sim), Fixed.One);
            Full(b);
            TuitionRules.Enrol(sim, b);
            Assert.That(b.Pupils, Does.Not.Contain(c.Id), "a child at the cap isn't enrolled again");
            Fixed cap = Fixed.FromInt(100);
            c.Skill = cap;
            c.TuitionSeasons = 0;
            b.Pupils.Clear(); b.Pupils.Add(c.Id); b.TuitionFees = 1;
            TuitionRules.AtClose(sim);
            Assert.That(c.Skill, Is.LessThanOrEqualTo(cap), "never past 100");
        }

        // A season with no evening run (a centre that was shut all season) adds nothing.
        [Test]
        public void No_Run_No_Gain()
        {
            Business.Business b = Centre(out Simulation sim);
            Family(sim, 3, 9, 12);
            Full(b);
            TuitionRules.Enrol(sim, b);
            Citizen c = sim.State.Citizens.Citizen(b.Pupils[0]);
            Fixed skill = c.Skill;
            TuitionRules.AtClose(sim);
            Assert.That((c.Skill, c.TuitionSeasons), Is.EqualTo((skill, 0)));
        }

        // Stars move a tenth of the way a day to their target: full batches and pupils who stayed.
        [Test]
        public void Stars_Follow_The_Batches()
        {
            Business.Business b = Centre(out Simulation sim);
            for (int i = 0; i < 12; i++) Family(sim, 5, 8, 10, 12, 14);
            Full(b);
            TuitionRules.Enrol(sim, b);
            Fixed target = TuitionRules.Target(sim, b);
            Assert.That(Near(target, Fixed.FromInt(3)), Is.True, "full, nobody yet stayed: 5 × 0.6");
            foreach (long id in b.Pupils) sim.State.Citizens.Citizen(id).TuitionSeasons = 1;
            Assert.That(Near(TuitionRules.Target(sim, b), Fixed.FromInt(5)), Is.True, "full and all stayed");
            b.Stars = Fixed.FromInt(2);
            TuitionRules.Morning(sim);
            Assert.That(Near(b.Stars, Fixed.FromRatio(23, 10)), Is.True, "a tenth of the way from 2 to 5");
        }

        // A closing centre lets its pupils go; the others' pupils are not touched.
        [Test]
        public void Closing_Releases_Pupils()
        {
            Business.Business b = Centre(out Simulation sim);
            Family(sim, 3, 8, 10, 12);
            Full(b);
            TuitionRules.Enrol(sim, b);
            Assert.That(TuitionRules.Pupils(b), Is.GreaterThan(0));
            Ok(sim, new CloseBusiness(sim.State.Clock.SimTime, b.Id));
            Assert.That((b.Pupils.Count, b.CohortPupils), Is.EqualTo((0, 0)));
            Reconciles(sim);
        }

        // Two centres share the children: a child is in one only, and a household's budget pays for each fee it commits.
        [Test]
        public void Two_Centres_Share_Children()
        {
            Business.Business a = Centre(out Simulation sim);
            Business.Business b2 = InvoiceTests.Found(sim, "tuition", a.TileId);
            Family(sim, 2, 8, 10, 12, 14);
            Full(a); Full(b2);
            TuitionRules.Enrol(sim, a);
            TuitionRules.Enrol(sim, b2);
            Assert.That(a.Pupils.Intersect(b2.Pupils), Is.Empty, "no child in two centres");
            Assert.That(a.Pupils.Count + b2.Pupils.Count, Is.EqualTo(2), "the family's two fees of budget, shared");
        }

        // The plan reckons on the pupils the town would send now, against the wage, books, repairs and wear; run by you it has no wage.
        [Test]
        public void Plan()
        {
            Simulation sim = InvoiceTests.Township();
            Family(sim, 3, 8, 10, 12);
            BusinessTypeDef t = sim.Config.Game.Businesses.Types.Single(x => x.IsTuition);
            BusinessRules.Projection hired = BusinessRules.Projected(sim, t, false, 0, Fixed.Zero), mine = BusinessRules.Projected(sim, t, true, 0, Fixed.Zero);
            Assert.That(hired.OwnGrams, Is.EqualTo(TuitionRules.Expected(sim, t.Tuition, Fixed.One)).And.GreaterThan(0));
            Assert.That(hired.Sales, Is.EqualTo(hired.OwnGrams * TuitionRules.FeeAt(sim, t.Tuition, Fixed.One)));
            Assert.That(mine.Profit - hired.Profit, Is.EqualTo(hired.Wage), "run by you there is no wage");
            Assert.That(hired.Profit, Is.EqualTo(hired.Revenue - hired.Costs));
        }

        // The save keeps a centre's pupils and a child's seasons, round-trips to the same hash, and the command round-trips.
        [Test]
        public void Saves_And_Commands()
        {
            Business.Business b = Centre(out Simulation sim);
            Family(sim, 3, 8, 10, 12);
            Full(b);
            TuitionRules.Enrol(sim, b);
            sim.State.Citizens.Citizen(b.Pupils[0]).TuitionSeasons = 4;
            SimConfig config = sim.Config;
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, config, TestSim.Wall0), config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Business.Business c = r.State.Businesses.Find(b.Id);
            Assert.That((c.Pupils.Count, c.CohortPupils), Is.EqualTo((b.Pupils.Count, b.CohortPupils)));
            Assert.That(r.State.Citizens.Citizen(b.Pupils[0]).TuitionSeasons, Is.EqualTo(4));
        }

        [Test]
        public void Before_The_Township_Nothing()
        {
            Simulation sim = Start();
            Assert.That(sim.Execute(new SetTuitionFee(sim.State.Clock.SimTime, 1, 0)).Reason, Is.Not.EqualTo(RejectReason.None));
        }
    }
}
