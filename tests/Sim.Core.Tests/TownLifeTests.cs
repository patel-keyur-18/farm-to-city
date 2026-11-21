using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-151 (spec 026 §3.4 to §3.7, ADR 0127): reputation, crime, inequality, the pull on migration and the grants' bonus.</summary>
    public class TownLifeTests
    {
        /// <summary>A Township with the old village in and <paramref name="families"/> families of yours, every working-age adult in a town job.</summary>
        internal static Simulation Township(int families = 12, System.Func<string, string, string> edit = null)
        {
            Simulation sim = TownshipTests.Start(cohort: true, (f, j) => { j = f == "township.json" ? j.Replace("\"townSize\": 40", "\"townSize\": 100000") : j; return edit == null ? j : edit(f, j); });
            TownshipTests.Families(sim, families);
            TreasuryTests.Township(sim);
            foreach (Household x in sim.State.Citizens.Households)
                foreach (long id in x.Members) sim.State.Citizens.Citizen(id).Job = CitizenJob.Hand;
            sim.DrainEvents();
            return sim;
        }

        private static void SetIncomes(Simulation sim, params long[] incomes)
        {
            var hs = sim.State.Citizens.Households;
            for (int i = 0; i < hs.Count; i++) hs[i].Income = (i < incomes.Length ? incomes[i] : incomes[incomes.Length - 1]) * Coin * hs[i].Members.Count; // a person's income, for every household
        }

        private static void Pay(Household x, long perPerson) { x.Income = perPerson * Coin * x.Members.Count; }

        // AC-7: nothing is read before the Township; the first close reads the town and the five scores equal their weighted parts.
        [Test]
        public void NotBeforeTownship()
        {
            Simulation village = TownshipTests.Start(cohort: true);
            Assert.That(TownLifeRules.On(village), Is.False, "the Village has no town life");
            Assert.That(TownLifeRules.Known(village), Is.False);
            Assert.That(TownLifeRules.Pull(village), Is.EqualTo(Fixed.FromInt(100)), "no pull before the Township");
            Assert.That(TownLifeRules.GrantFactor(village), Is.EqualTo(Fixed.One));
            Assert.That(village.State.Township.TownLife.IsDefault, Is.True);
        }

        [Test]
        public void Parts()
        {
            Simulation sim = Township();
            Assert.That(TownLifeRules.Known(sim), Is.False, "on, but not yet read");
            long step = PetitionRules.Step(PetitionRules.Def(sim), HappinessRules.Town(sim));
            TownLifeRules.AtClose(sim);
            TownLifeState s = TownLifeRules.State(sim);
            TownLifeDef d = TownLifeRules.Def(sim);
            Assert.That(s.Read, Is.True);
            Assert.That(s.Approval, Is.EqualTo(Fixed.FromInt((int)(50 + step))), "approval starts at 50 and the first close moves it by happiness alone");
            for (int i = 0; i < TownLifeDef.Scores; i++)
            {
                Fixed sum = Fixed.Zero;
                for (int k = 0; k < TownLifeDef.PartsEach; k++)
                {
                    Fixed part = s.Parts[i * TownLifeDef.PartsEach + k];
                    Assert.That(part, Is.InRange(Fixed.Zero, Fixed.FromInt(100)), "part " + i + "." + k);
                    sum += part.MulRatio(d.Weights[i][k], 100);
                }
                Assert.That(s.Reputation[i], Is.EqualTo(sum), "score " + i + " is its weighted parts");
            }
            // Nothing built yet: no drains, no collection post, no clinic; a track, no paved road; the pond and wells water the homes.
            Assert.That(s.Parts[13], Is.EqualTo(Fixed.Zero), "no drains");
            Assert.That(s.Parts[14], Is.EqualTo(Fixed.Zero), "no collection post");
            Assert.That(s.Parts[5], Is.EqualTo(Fixed.FromInt(d.RoadTrack)), "a track");
            Assert.That(s.Parts[12], Is.EqualTo(Fixed.FromInt(100)), "no kiln: no smoke on any home");
            Assert.That(s.Parts[4], Is.EqualTo(Fixed.FromInt(50)), "no business open: neither good nor bad");
        }

        // AC-11: the ratio is the richest tenth over the poorest tenth, from 10 households, capped; the score follows it.
        [Test]
        public void Ratio()
        {
            Simulation sim = Township(families: 20);
            int n = TownLifeRules.Yours(sim).Count;
            Assert.That(n, Is.GreaterThanOrEqualTo(20));
            // Everyone earns 10,000, the poorest tenth 5,000 and the richest tenth 20,000: a ratio of 4.
            SetIncomes(sim, 10_000);
            var hs = TownLifeRules.Yours(sim);
            int tenth = n / 10;
            for (int i = 0; i < tenth; i++) { Pay(hs[i], 5_000); Pay(hs[n - 1 - i], 20_000); }
            TownLifeRules.AtClose(sim);
            TownLifeState s = TownLifeRules.State(sim);
            Assert.That(s.RatioKnown, Is.True);
            Assert.That(s.Ratio, Is.EqualTo(Fixed.FromInt(4)));
            Assert.That(s.Score, Is.EqualTo(Fixed.FromInt(3).MulRatio(100, 9)), "score = (ratio − 1) × 100 ÷ 9");
            // A retired couple earns nothing and is not an earner: the ratio ignores them.
            Household retired = AddFamily(sim, 0, 70, 68);
            retired.Income = 0;
            TownLifeRules.AtClose(sim);
            Assert.That(s.Ratio, Is.EqualTo(Fixed.FromInt(4)), "a household with nobody of working age is left out");
            Assert.That(TownLifeRules.InequalityPoints(sim, retired), Is.EqualTo(0), "and feels nothing of it");
            // A cap: a poorest tenth with nothing.
            for (int i = 0; i < tenth; i++) Pay(hs[i], 0);
            TownLifeRules.AtClose(sim);
            Assert.That(s.Ratio, Is.EqualTo(Fixed.FromInt(10)), "capped at 10");
            Assert.That(s.Score, Is.EqualTo(Fixed.FromInt(100)));
            // Too few families to compare.
            Simulation few = Township(families: 3);
            TownLifeRules.AtClose(few);
            Assert.That(TownLifeRules.State(few).RatioKnown, Is.False, "too few families to compare");
            Assert.That(TownLifeRules.State(few).Score, Is.EqualTo(Fixed.Zero));
        }

        // AC-11: below the median a household loses a point for each whole point of the ratio above 4, at most 6; the poorest leave a season sooner at 6.
        [Test]
        public void InequalityEffects()
        {
            Simulation sim = Township(families: 20);
            var hs = TownLifeRules.Yours(sim);
            int n = hs.Count, tenth = n / 10;
            SetIncomes(sim, 10_000);
            for (int i = 0; i < tenth; i++) { Pay(hs[i], 2_000); Pay(hs[n - 1 - i], 14_000); } // a ratio of 7
            TownLifeRules.AtClose(sim);
            TownLifeState s = TownLifeRules.State(sim);
            Assert.That(s.Ratio, Is.EqualTo(Fixed.FromInt(7)));
            Assert.That(TownLifeRules.InequalityPoints(sim, hs[0]), Is.EqualTo(3), "7 − 4 whole points, below the median");
            Assert.That(TownLifeRules.InequalityPoints(sim, hs[n - 1]), Is.EqualTo(0), "above the median: nothing");
            Assert.That(TownLifeRules.InequalityPoints(sim, hs[tenth]), Is.EqualTo(0), "at the median: nothing");
            Assert.That(TownLifeRules.PushedOut(sim, hs[0]), Is.True, "the poorest tenth, at a ratio of 6 or more");
            Assert.That(TownLifeRules.PushedOut(sim, hs[tenth]), Is.False);
            Assert.That(LeaseRules.Patience(sim, hs[0]), Is.EqualTo(LeaseRules.Patience(sim, hs[tenth]) - 1), "a season sooner");

            for (int i = 0; i < tenth; i++) Pay(hs[i], 0); // the ratio hits the cap of 10: 6 points, not 6+
            TownLifeRules.AtClose(sim);
            Assert.That(TownLifeRules.InequalityPoints(sim, hs[0]), Is.EqualTo(6), "at most 6 points");

            // The happiness itself: the same household, with and without the town read.
            Fixed before = HappinessOf(sim, hs[0], inequality: false);
            Fixed after = HappinessOf(sim, hs[0], inequality: true);
            Assert.That(before - after, Is.EqualTo(Fixed.FromInt(6)).Or.EqualTo(before), "6 points lower (or to nothing)");
        }

        private static Fixed HappinessOf(Simulation sim, Household x, bool inequality)
        {
            TownLifeState s = TownLifeRules.State(sim);
            bool known = s.RatioKnown;
            s.RatioKnown = inequality && known;
            HappinessRules.Update(sim, x);
            Fixed h = x.Happiness;
            s.RatioKnown = known;
            return h;
        }

        // AC-9: crime is the base plus two a percent of unemployment plus three-tenths of the inequality score.
        [Test]
        public void Formula()
        {
            Simulation sim = Township(families: 12);
            SetIncomes(sim, 10_000);
            TownLifeRules.AtClose(sim);
            TownLifeState s = TownLifeRules.State(sim);
            Assert.That(s.RatioKnown && s.Ratio == Fixed.One, Is.True, "equal incomes: a ratio of 1");
            Assert.That(s.UnemployedPermille, Is.EqualTo(0), "everybody has a town job");
            Assert.That(s.Crime, Is.EqualTo(Fixed.FromInt(20)), "20 + 0 + 0");

            // A tenth of the working-age adults without work, and a ratio of 4 (score 33.3).
            var adults = sim.State.Citizens.Citizens.Where(c => c.Living && !HouseholdRules.IsChild(sim, c) && !HouseholdRules.IsElder(sim, c)).ToList();
            int idle = 0;
            foreach (Citizen c in adults.Take(adults.Count / 10)) { c.Job = CitizenJob.None; idle++; }
            var hs = TownLifeRules.Yours(sim);
            Pay(hs[0], 5_000); Pay(hs[hs.Count - 1], 20_000);
            TownLifeRules.AtClose(sim);
            Assert.That(s.UnemployedPermille, Is.GreaterThan(0));
            Fixed expected = Fixed.FromInt(20) + Fixed.FromInt(2) * Fixed.FromRatio(s.UnemployedPermille, 10) + Fixed.FromRatio(3, 10) * s.Score;
            Assert.That(s.Crime, Is.EqualTo(expected));
            Assert.That(s.Crime, Is.InRange(Fixed.Zero, Fixed.FromInt(100)));
            Assert.That(idle, Is.GreaterThan(0));
        }

        // AC-9: at crime 50 the Safety part is the one it always was; a watchman lowers crime in his reach only.
        [Test]
        public void SafetyPart()
        {
            Simulation sim = Township(families: 3);
            TownLifeRules.AtClose(sim);
            TownLifeState s = TownLifeRules.State(sim);
            s.Crime = Fixed.FromInt(50);
            Household x = sim.State.Citizens.Households[0];
            Assert.That(CivicRules.Safety(sim, x), Is.EqualTo(Fixed.FromRatio(1, 2)), "crime 50: today's 0.5");
            s.Crime = Fixed.FromInt(80);
            Assert.That(CivicRules.Safety(sim, x), Is.EqualTo(Fixed.FromRatio(1, 5)), "1 − 80 ÷ 100");
            s.Crime = Fixed.Zero;
            Assert.That(CivicRules.Safety(sim, x), Is.EqualTo(Fixed.One));
            s.Crime = Fixed.FromInt(100);
            Assert.That(CivicRules.Safety(sim, x), Is.EqualTo(Fixed.Zero));
            Assert.That(TownLifeRules.CrimeAtTile(sim, 0), Is.EqualTo(Fixed.FromInt(100)), "no watchman, no gain");
        }

        // AC-9 (reach): with a watchman in post, crime falls by his gain in reach and not outside it.
        [Test]
        public void WatchmanReach()
        {
            Simulation sim = Township(families: 3, edit: null);
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, 1_000_000 * Coin), new Posting(Account.GiftsFromFounder, -1_000_000 * Coin));
            Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, "watchman_post"));
            for (int i = 0; i < 400 && !CivicRules.Built(sim, CivicDef.Watchman); i++) sim.AdvanceTo(sim.State.Clock.SimTime + 6 * Hour);
            Ok(sim, new PostCivicJob(sim.State.Clock.SimTime, CivicDef.Watchman));
            sim.AdvanceTo(sim.State.Clock.SimTime + 24 * Hour);
            Assert.That(CivicRules.Staffed(sim, CivicDef.Watchman), Is.True);
            TownLifeRules.AtClose(sim);
            TownLifeState s = TownLifeRules.State(sim);
            s.Crime = Fixed.FromInt(60);
            CivicDef w = CivicRules.Def(sim, CivicDef.Watchman);
            int near = w.Tile, far = -1;
            for (int t = 0; t < sim.State.Land.Tiles.Count && far < 0; t++) if (!CivicRules.Covered(sim, CivicDef.Watchman, t)) far = t;
            Assert.That(far, Is.GreaterThanOrEqualTo(0), "an acre out of his reach exists");
            Fixed full = CivicRules.Condition(sim) * CivicRules.Scale(sim, CivicDef.Watchman);
            Assert.That(TownLifeRules.CrimeAtTile(sim, near), Is.EqualTo(Fixed.FromInt(60) - w.Gain * full * Fixed.FromInt(100)), "his gain in reach");
            Assert.That(TownLifeRules.CrimeAtTile(sim, far), Is.EqualTo(Fixed.FromInt(60)), "none outside it");
        }

        // AC-8: the pull is 100 at approval 50 and a mean of 50, between 50 and 150, and shortens or lengthens a family's wait.
        [Test]
        public void Pull()
        {
            Simulation sim = Township(families: 3);
            TownLifeRules.AtClose(sim);
            TownLifeState s = TownLifeRules.State(sim);
            void Set(int approval, int mean) { s.Approval = Fixed.FromInt(approval); for (int i = 0; i < s.Reputation.Length; i++) s.Reputation[i] = Fixed.FromInt(mean); }
            Set(50, 50);
            Assert.That(TownLifeRules.Pull(sim), Is.EqualTo(Fixed.FromInt(100)), "nothing changes at 50 and 50");
            long t = sim.State.Clock.SimTime;
            long normal = MigrationRules.Wait(sim, t);
            Set(100, 100);
            Assert.That(TownLifeRules.Pull(sim), Is.EqualTo(Fixed.FromInt(150)));
            Assert.That(MigrationRules.Wait(sim, t), Is.EqualTo(normal * 100 / 150).Within(1), "18 hours become 12");
            Set(0, 0);
            Assert.That(TownLifeRules.Pull(sim), Is.EqualTo(Fixed.FromInt(50)));
            Assert.That(MigrationRules.Wait(sim, t), Is.EqualTo(normal * 2).Within(1), "18 hours become 36");
            Set(100, 0);
            Assert.That(TownLifeRules.Pull(sim), Is.EqualTo(Fixed.FromInt(100)), "approval and reputation pull against each other");
        }

        // AC-8: the grants rise by up to 10% above a mean of 50 and are never lowered.
        [Test]
        public void Grants()
        {
            Simulation sim = Township(families: 3);
            TownLifeRules.AtClose(sim);
            TownLifeState s = TownLifeRules.State(sim);
            void Mean(int mean) { for (int i = 0; i < s.Reputation.Length; i++) s.Reputation[i] = Fixed.FromInt(mean); }
            Mean(50);
            TreasuryRules.GrantsNow(sim, out long u50, out long t50, out _);
            Mean(100);
            TreasuryRules.GrantsNow(sim, out long u100, out long t100, out _);
            Mean(0);
            TreasuryRules.GrantsNow(sim, out long u0, out long t0, out _);
            Assert.That(u100, Is.EqualTo(u50 + u50 / 10).Within(u50 / 1000 + 100), "10% more at a mean of 100");
            Assert.That(u0, Is.EqualTo(u50), "never lowered");
            Assert.That(t0, Is.EqualTo(t50));
            Assert.That(t100, Is.GreaterThanOrEqualTo(t50));
            Mean(75);
            Assert.That(TownLifeRules.GrantFactor(sim), Is.EqualTo(Fixed.One + Fixed.FromRatio(25, 500)), "(75 − 50) ÷ 5 percent");
        }

        // AC-10: the land value takes the crime term: plus 0.02 for every 10 points under 50, minus over, at most ±0.10.
        [Test]
        public void LandValueCrime()
        {
            Simulation sim = Township(families: 3);
            TownLifeRules.AtClose(sim);
            TownLifeState s = TownLifeRules.State(sim);
            int tile = sim.State.Land.Tiles.Count / 2;
            void Crime(int c) { s.Crime = Fixed.FromInt(c); }
            Crime(50); Assert.That(TownLifeRules.LandValueTerm(sim, tile), Is.EqualTo(Fixed.Zero));
            Crime(30); Assert.That(TownLifeRules.LandValueTerm(sim, tile), Is.EqualTo(Fixed.FromRatio(4, 100)));
            Crime(70); Assert.That(TownLifeRules.LandValueTerm(sim, tile), Is.EqualTo(Fixed.Zero - Fixed.FromRatio(4, 100)));
            Crime(0); Assert.That(TownLifeRules.LandValueTerm(sim, tile), Is.EqualTo(Fixed.FromRatio(1, 10)), "capped at +0.10");
            Crime(100); Assert.That(TownLifeRules.LandValueTerm(sim, tile), Is.EqualTo(Fixed.Zero - Fixed.FromRatio(1, 10)), "capped at −0.10");
            Crime(20);
            Assert.That(LandValueRules.Parts(sim, tile).Any(p => p.Key == "crime" && p.Value == Fixed.FromRatio(6, 100)), Is.True, "the part is listed with the *Why?* key");
        }

        // AC-15: a town left alone for a year still works: it levels, nothing goes negative.
        [Test]
        public void LeftAlone()
        {
            Simulation sim = Township(families: 12);
            long t = sim.State.Clock.SimTime;
            for (int i = 0; i < 4; i++) sim.AdvanceTo(t + (i + 1) * sim.Clock.SecondsPerSeason);
            TownLifeState s = TownLifeRules.State(sim);
            Assert.That(s.Read, Is.True, "a close read the town");
            Assert.That(s.Crime, Is.InRange(Fixed.Zero, Fixed.FromInt(100)));
            foreach (Fixed r in s.Reputation) Assert.That(r, Is.InRange(Fixed.Zero, Fixed.FromInt(100)));
            Assert.That(TownLifeRules.Pull(sim), Is.InRange(Fixed.FromInt(50), Fixed.FromInt(150)));
            Assert.That(sim.State.Treasury.Ledger.Reconcile(), Is.Null);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }
    }
}
