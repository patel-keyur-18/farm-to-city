using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// The <em>Town life</em> tab of the town hall's sheet (US-151, spec 026 §7): approval, the five scores of your town's name (each a ? with its parts), what the name
    /// does, crime and inequality. A view: it reads the state and changes nothing.
    /// </summary>
    public static class TownLifeSheet
    {
        public const string Tab = "life";

        public static void Rows(ViewContext ctx, SheetVm vm)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            if (!TownLifeRules.Known(sim))
            {
                vm.Lines.Add(new LineVm(s.Get("life.notYet"), null) { Dim = true });
                return;
            }
            TownLifeState st = TownLifeRules.State(sim);

            vm.Lines.Add(LineVm.Header(s.Get("life.approvalHeader")));
            vm.Lines.Add(new LineVm(s.Get("life.approval"), ctx.Fmt.Integer(st.Approval.RoundToLong()), "lifeApproval", 0) { Note = s.Get("life.approvalNote") });

            vm.Lines.Add(LineVm.Header(s.Get("life.nameHeader")));
            for (int i = 0; i < TownLifeDef.Scores; i++)
            {
                int score = (int)st.Reputation[i].RoundToLong();
                vm.Lines.Add(new LineVm(s.Get("life.score." + i), ctx.Fmt.Integer(score), "lifeScore", i) { Meter = score });
            }
            vm.Lines.Add(new LineVm(PullLine(ctx), null, "lifePull", 0) { Dim = true });

            vm.Lines.Add(LineVm.Header(s.Get("life.fairHeader")));
            vm.Lines.Add(new LineVm(s.Get("life.crime"), ctx.Fmt.Integer(st.Crime.RoundToLong()), "lifeCrime", 0) { Note = s.Get("life.crimeNote") });
            if (st.RatioKnown) vm.Lines.Add(new LineVm(s.Get("life.ratio", "n", ctx.Fmt.Decimal1(st.Ratio)), null, "lifeRatio", 0) { Note = s.Get("life.ratioNote") });
            else vm.Lines.Add(new LineVm(s.Get("life.ratioFew"), null, "lifeRatio", 0) { Dim = true });
        }

        /// <summary>"Families come about 13% faster and the district's grants are 3.6% higher": what the name does (spec 026 §3.4, §3.7).</summary>
        public static string PullLine(ViewContext ctx)
        {
            var s = ctx.Strings;
            Fixed pull = TownLifeRules.Pull(ctx.Sim);
            long diff = (pull - Fixed.FromInt(100)).RoundToLong();
            Fixed grants = (TownLifeRules.GrantFactor(ctx.Sim) - Fixed.One) * Fixed.FromInt(100);
            string families = diff == 0 ? s.Get("life.pull.same") : diff > 0 ? s.Get("life.pull.faster", "n", ctx.Fmt.Integer(diff)) : s.Get("life.pull.slower", "n", ctx.Fmt.Integer(-diff));
            return grants > Fixed.Zero ? s.Get("life.pull.withGrants", "families", families, "g", ctx.Fmt.Decimal1(grants)) : families;
        }
    }
}
