using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-264: the mill's Today tab says what its atta did in the village, and its Why? says why only some sells.</summary>
    public class VillageAttaViewTests
    {
        [Test]
        public void Mill_Sheet_Says_What_Went_To_The_Village()
        {
            Simulation sim = Situations.Mill();
            Sim.Core.Business.Business b = sim.State.Businesses.Businesses.Last();
            var ctx = ViewKit2.Context(sim);
            SheetVm vm = BusinessSheet.Build(ctx, b.Id);
            LineVm row = vm.Lines.Single(l => !l.IsHeader && l.Label == "Atta sold in the village today");
            Assert.That(row.WhyKey, Is.EqualTo("businessVillage"));
            Assert.That(row.Value, Is.EqualTo(ctx.Fmt.Kg(b.Day == (ctx.Now - 3600) / 86_400 || b.Day == ctx.Now / 86_400 ? b.VillageToday : 0)));
            Assert.That(vm.Lines.Any(l => l.Label == "Bran sold in the village today"), Is.False, "only atta goes to the village");
            WhyVm why = WhyView.Build(ctx, row.WhyKey, row.WhyId);
            Assert.That(why.Rows.Select(r => r.Label), Has.Some.Contain("never sells below what the truck pays"));
        }
    }
}
