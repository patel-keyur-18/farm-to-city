using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.History;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-157 (spec 029 §3.1, §7, AC-13, AC-14, Preview 19): the Menu's Chronicle sheet and its Why? lines.</summary>
    public class ChronicleViewTests
    {
        private static Simulation Town(out ViewContext ctx)
        {
            Simulation sim = ViewKit.NewGame();
            ctx = ViewKit2.Context(sim);
            return sim;
        }

        // The Timeline: newest first, a header for each game year, one sentence a line with its date and icon; the founding is the oldest line.
        [Test]
        public void Timeline_Newest_First_By_Year()
        {
            Simulation sim = Town(out ViewContext ctx);
            long season = sim.Clock.SecondsPerSeason, t = sim.State.Clock.SimTime;
            sim.Emit(new SchoolOpened(t + 2 * season));
            sim.Emit(new RoadBuilt(t + 5 * season)); // year 2
            SheetVm vm = ChronicleSheet.Build(ctx);
            Assert.That(vm.Tabs.Select(a => a.Label), Is.EqualTo(new[] { "Timeline", "Almanacs" }));
            var lines = vm.Lines.Select(l => l.IsHeader ? "# " + l.Label : l.Label).ToList();
            Assert.That(lines, Is.EqualTo(new[] { "# Year 2", "The road to the district was paved.", "# Year 1", "The school opened its doors.", "You came to the valley with " + ctx.Fmt.Integer(sim.State.Chronicle.Entries[0].A) + " acres and a plan." }));
            Assert.That(vm.Lines[1].Note, Is.EqualTo("Summer, year 2"), "its date");
            Assert.That(vm.Lines[1].Icon, Is.EqualTo("sign"), "its kind's icon");
        }

        // Every kind has its sentence in words, made from numbers and names the line keeps.
        [Test]
        public void Every_Kind_Reads_As_A_Sentence()
        {
            Simulation sim = Town(out ViewContext ctx);
            ChronicleDefs d = sim.Config.Game.Chronicle;
            string biz = sim.Config.Game.Businesses.Types[0].Id, crop = sim.Config.Game.Farm.Crops[0].Id;
            foreach (ChronicleKind k in System.Enum.GetValues(typeof(ChronicleKind)))
            {
                var e = new ChronicleEntry { At = 1, Kind = k, A = 2, B = k == ChronicleKind.Story ? StoriesRules.Code(StoryArc.Stall, 1, 0) : 1, Id = k == ChronicleKind.Story ? "veg" : k == ChronicleKind.LoanCleared ? "coop_bank" : k == ChronicleKind.RecordHarvest ? crop : k == ChronicleKind.FirstOrchard ? "mango" : biz };
                string text = ChronicleWords.Line(ctx, e);
                Assert.That(text, Is.Not.Empty, k.ToString());
                Assert.That(text, Does.Not.Contain("{").And.Not.Contain("chronicle."), k + ": " + text);
            }
            Assert.That(d.Kinds.Count, Is.EqualTo(ChronicleDefs.KindIds.Length));
        }

        // AC-14: a line with numbers has a ? that answers in words, with those numbers and no factors; the Menu lists the Chronicle.
        [Test]
        public void Lines_With_Numbers_Have_A_Why()
        {
            Simulation sim = Town(out ViewContext ctx);
            long t = sim.State.Clock.SimTime;
            sim.Emit(new CropHarvested(t, 1, sim.Config.Game.Farm.Crops[0].Id, 1_200_000, null));
            sim.Emit(new SeasonClosed(t, 0, null));
            sim.Emit(new CropHarvested(t + sim.Clock.SecondsPerSeason, 1, sim.Config.Game.Farm.Crops[0].Id, 1_500_000, null));
            sim.Emit(new SeasonClosed(t + sim.Clock.SecondsPerSeason, 1, null));
            sim.Emit(new FireRepaired(t, 1, false, 5_000));
            sim.Emit(new LoanCleared(t, 1, "coop_bank", 40_000));
            sim.Emit(new EpidemicEnded(t, 20));
            SheetVm vm = ChronicleSheet.Build(ctx);
            var withWhy = vm.Lines.Where(l => l.WhyKey != null).ToList();
            Assert.That(withWhy.Count, Is.GreaterThanOrEqualTo(5), "founding, a record, a fire, a loan, an epidemic");
            foreach (LineVm l in withWhy)
            {
                Assert.That(WhyView.Has(l.WhyKey), Is.True, l.Label);
                WhyTests.AssertWords(l.Label, WhyView.Build(ctx, l.WhyKey, l.WhyId));
            }
            LineVm record = withWhy.Single(l => l.Label.StartsWith("A record"));
            WhyVm why = WhyView.Build(ctx, record.WhyKey, record.WhyId);
            Assert.That(why.Rows.Select(r => r.Value), Does.Contain(ctx.Fmt.Kg(1_500_000)).And.Contain(ctx.Fmt.Kg(1_200_000)), "this season's harvest and the old best");
            Assert.That(MenuSheet.Build(ctx).Lines.Any(l => l.Action != null && l.Action.Opens.Value.Kind == ChronicleSheet.Kind), Is.True, "the Menu opens it");
        }

        [Test]
        public void Almanacs_Tab_Is_Empty_Until_A_Winter_Closes()
        {
            Simulation sim = Town(out ViewContext ctx);
            SheetVm vm = ChronicleSheet.Build(ctx, ChronicleSheet.Almanacs);
            Assert.That(vm.Tab, Is.EqualTo("tab:almanacs"));
            Assert.That(vm.Lines.Count, Is.EqualTo(1));
        }

        private static AlmanacEntry Entry(long year, bool quiet)
        {
            return new AlmanacEntry
            {
                Year = year, Households = 26, People = 112, Village = 184, NetWorth = 18_64_000_00, Before = 15_54_000_00, HadBefore = year > 1,
                Best = quiet ? AlmanacBest.None : AlmanacBest.Business, BestId = "bakery", BestRef = 1, BestProfit = 1_52_000_00, BestPut = 4_00_000_00,
                Loss = quiet ? AlmanacLoss.None : AlmanacLoss.FireRepair, LossAmount = quiet ? 0 : 18_400_00,
            };
        }

        // US-158 (spec 029 §7, Preview 19): the card says the year's people, net worth, best return, costliest loss and a person; each number has a ?.
        [Test]
        public void Almanac_Card_Reads_And_Explains()
        {
            Simulation sim = Town(out ViewContext ctx);
            var citizen = new Sim.Core.People.Citizen { Id = 99, Name = 0, Female = true, HouseholdId = 0, Health = Sim.Core.Maths.Fixed.One };
            sim.State.Citizens.Citizens.Add(citizen);
            AlmanacEntry e = Entry(6, quiet: false);
            e.Spot = AlmanacSpot.Reader; e.SpotCitizen = 99;
            sim.State.Chronicle.Almanac.Entries.Add(e);
            SheetVm vm = AlmanacSheet.Build(ctx, 6);
            Assert.That(vm.Title, Is.EqualTo("Year 6 in the valley"));
            Assert.That(vm.Lines.Select(l => l.Label), Is.EqualTo(new[] { "Your people", "Net worth", "Best return", "Costliest loss", Sim.View.Views.PeopleWords.Name(ctx, 0) }));
            Assert.That(vm.Lines[0].Note, Does.Contain("26 households").And.Contain("184"));
            Assert.That(vm.Lines[1].Note, Does.StartWith("Up "));
            Assert.That(vm.Lines[2].Note, Is.EqualTo("Your bakery: it earned 38 coins for every 100 you put in"));
            Assert.That(vm.Lines[3].Note, Does.StartWith("A fire"));
            Assert.That(vm.Actions.Select(a => a.Id), Is.EqualTo(new[] { "almanacChronicle", AlmanacSheet.CloseId }));
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null))
            {
                Assert.That(WhyView.Has(l.WhyKey), Is.True, l.Label);
                WhyTests.AssertWords(l.Label, WhyView.Build(ctx, l.WhyKey, l.WhyId));
            }
            Assert.That(vm.Lines.Count(l => l.WhyKey != null), Is.EqualTo(5), "people, worth, best, loss and the person");
            WhyVm best = WhyView.Build(ctx, vm.Lines[2].WhyKey, vm.Lines[2].WhyId);
            Assert.That(best.Rows.Select(r => r.Value), Does.Contain(ctx.Fmt.Coins(1_52_000_00)).And.Contain(ctx.Fmt.Coins(4_00_000_00)), "the profit and the money put in");
        }

        [Test]
        public void Almanac_Quiet_Year()
        {
            Simulation sim = Town(out ViewContext ctx);
            sim.State.Chronicle.Almanac.Entries.Add(Entry(1, quiet: true));
            SheetVm vm = AlmanacSheet.Build(ctx, 1);
            Assert.That(vm.Lines[1].Note, Is.EqualTo("Your first year in the valley"));
            Assert.That(vm.Lines[2].Value, Is.EqualTo("a quiet year"));
            Assert.That(vm.Lines[3].Value, Is.EqualTo("none"));
            Assert.That(vm.Lines[4].Label, Is.EqualTo("A quiet year for the people"));
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null)) WhyTests.AssertWords(l.Label, WhyView.Build(ctx, l.WhyKey, l.WhyId));
        }

        // The Almanacs tab: newest first, each year's net worth and best return, an Open for its card.
        [Test]
        public void Almanacs_Tab_Lists_The_Years()
        {
            Simulation sim = Town(out ViewContext ctx);
            sim.State.Chronicle.Almanac.Entries.Add(Entry(1, quiet: true));
            sim.State.Chronicle.Almanac.Entries.Add(Entry(2, quiet: false));
            SheetVm vm = ChronicleSheet.Build(ctx, ChronicleSheet.Almanacs);
            Assert.That(vm.Lines.Select(l => l.Label), Is.EqualTo(new[] { "Year 2 in the valley", "Year 1 in the valley" }));
            Assert.That(vm.Lines[0].Note, Does.Contain("Best: Bakery, 38 for every 100"));
            Assert.That(vm.Lines[1].Note, Does.Contain("a quiet year"));
            Assert.That(vm.Lines[0].Action.Opens.Value, Is.EqualTo(new SheetRequest(AlmanacSheet.Kind, 2)));
        }

        // US-159 (spec 029 §3.6, §7): every step of every arc reads as a sentence with the person's name, in the Chronicle, the toast and the Why?.
        [Test]
        public void Story_Steps_Read_And_Explain()
        {
            Simulation sim = Town(out ViewContext ctx);
            var c = new Sim.Core.People.Citizen { Id = 7, Name = 0, Female = true, Health = Sim.Core.Maths.Fixed.One };
            sim.State.Citizens.Citizens.Add(c);
            string name = PeopleWords.Name(ctx, 0);
            var steps = new (StoryArc arc, int step, int n, string kind, string expect)[]
            {
                (StoryArc.Stall, 1, 0, "veg", "has put aside enough for a stall of their own."),
                (StoryArc.Stall, 2, 0, "chai", "has gone to start a tea stall."),
                (StoryArc.Stall, 3, 1, "sweets", "sweet shop is doing well."),
                (StoryArc.Stall, 3, 0, "veg", "stall closed; they would work for wages again."),
                (StoryArc.Reader, 1, 0, "", "nobody in the family can read yet."),
                (StoryArc.Reader, 2, 0, "", "the first in the family."),
                (StoryArc.Reader, 3, 0, "", "taken a paid job."),
                (StoryArc.Migrant, 1, 0, "", "two seasons"),
                (StoryArc.Migrant, 2, 0, "", "four seasons"),
                (StoryArc.Migrant, 3, 0, "", "A child was born"),
                (StoryArc.LongLife, 2, 70, "", "is 70 and still part of the daily life"),
                (StoryArc.LongLife, 4, 72_005, "", "died at 72, remembered by 5 family members."),
            };
            foreach (var x in steps)
            {
                var e = new ChronicleEntry { At = 10, Kind = ChronicleKind.Story, A = 7, B = StoriesRules.Code(x.arc, x.step, x.n), Id = x.kind };
                string line = ChronicleWords.Line(ctx, e);
                Assert.That(line, Does.Contain(x.expect), x.arc + " " + x.step);
                Assert.That(line, Does.Contain(name).And.Not.Contain("{"));
                sim.State.Chronicle.Entries.Add(e);
            }
            SheetVm vm = ChronicleSheet.Build(ctx);
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null && l.Label.Contains(name)))
            {
                Assert.That(WhyView.Has(l.WhyKey), Is.True);
                WhyTests.AssertWords(l.Label, WhyView.Build(ctx, l.WhyKey, l.WhyId));
            }
            var toast = StoryToastVm.Build(ctx, new Sim.Core.Events.StoryStepped(10, (int)StoryArc.Stall, 1, 7, 0, "veg"));
            Assert.That((toast.Kicker, toast.Text), Is.EqualTo(("A story from your valley · they will leave at the close", name + " has put aside enough for a stall of their own.")));
            Assert.That(toast.Portrait, Does.StartWith("portrait_"));
        }

        // Review of Sprint 26 (spec 029 §5): a story step taken while you were away, a worker who left to start a stall among them, is a line on the away card.
        [Test]
        public void Away_Card_Says_A_Stall_Opened()
        {
            Simulation sim = ViewKit.NewGame();
            ViewContext ctx = ViewKit2.Context(sim);
            long now = sim.State.Clock.SimTime;
            long who = 1; // nobody by that id yet: the line says "someone"
            var events = new List<SimEvent>
            {
                new Sim.Core.Events.StoryStepped(now, (int)StoryArc.Stall, 2, who, 0, "veg"),
                new AwayAdvanceCompleted(now, now - 30 * 3600, now, 30 * 3600, 30 * 3600, false),
            };
            AwayCard.Vm vm = AwayCard.Build(ctx, events);
            Assert.That(vm, Is.Not.Null);
            Assert.That(vm.Lines.Concat(vm.More).Any(l => l.Value.Contains("stall") && !l.Value.Contains("{")), Is.True, "the away card says someone has gone to start a stall");
        }
    }
}
