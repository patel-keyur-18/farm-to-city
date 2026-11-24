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
                if (k == ChronicleKind.Story) continue; // the arcs' own sentences come with US-159
                var e = new ChronicleEntry { At = 1, Kind = k, A = 2, B = 1, Id = k == ChronicleKind.LoanCleared ? "coop_bank" : k == ChronicleKind.RecordHarvest ? crop : k == ChronicleKind.FirstOrchard ? "mango" : biz };
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
    }
}
