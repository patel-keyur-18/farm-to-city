using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>The Town life <em>Why?</em>s (spec 026 §7): approval, each reputation score by its parts and weights, what the name does, crime and inequality. Words, never a factor (ADR 0043).</summary>
    public static partial class WhyView
    {
        private static WhyVm LifeApproval(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            TownLifeState st = TownLifeRules.State(sim);
            PetitionDef d = PetitionRules.Def(sim);
            WhyVm vm = New(ctx, "why.life.approval.title", Num(ctx, st.Approval));
            Row(vm, ctx, "why.life.approval.start", ctx.Fmt.Integer(TownLifeRules.Def(sim).ApprovalStart));
            Row(vm, ctx, "why.life.approval.happiness", Num(ctx, Sim.Core.People.HappinessRules.Town(sim)));
            Row(vm, ctx, "why.life.approval.step", TownLifeSheet.Signed(ctx, (int)PetitionRules.Step(d, Sim.Core.People.HappinessRules.Town(sim))), "max", ctx.Fmt.Integer(d.StepMax));
            Row(vm, ctx, "why.life.approval.endings", ctx.Strings.Get("why.life.approval.endingsValue", "done", TownLifeSheet.Signed(ctx, d.Done), "kept", TownLifeSheet.Signed(ctx, d.Kept), "declined", TownLifeSheet.Signed(ctx, d.Declined), "ignored", TownLifeSheet.Signed(ctx, d.Ignored), "broken", TownLifeSheet.Signed(ctx, d.Broken)));
            Text(vm, ctx, "why.life.approval.text");
            return vm;
        }

        /// <summary>A petition's <em>Why?</em> (spec 026 §3.2): how many families, how many it takes, and what answers it.</summary>
        private static WhyVm LifePetition(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            TownLifeState st = TownLifeRules.State(sim);
            PetitionDef d = PetitionRules.Def(sim);
            TownPetition p = st.Petitions.Find(x => x.Id == id);
            if (p == null) return New(ctx, "why.life.petition.title", "");
            WhyVm vm = New(ctx, "why.life.petition.title", ctx.Strings.Plural("petition.families", p.Households));
            Row(vm, ctx, "why.life.petition.count", ctx.Fmt.Integer(p.Households));
            Row(vm, ctx, "why.life.petition.needed", ctx.Fmt.Integer(PetitionRules.Needed(sim, d, TownLifeRules.Yours(sim).Count)));
            Row(vm, ctx, "why.life.petition.answer", ctx.Strings.Get("why.life.petition.answerValue"));
            Text(vm, ctx, "why.life.petition.text." + p.Topic);
            return vm;
        }

        /// <summary>What a festival costs and does (spec 026 §3.1).</summary>
        private static WhyVm Festival(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            FestivalDef d = FestivalRules.Def(sim);
            int kind = (int)id;
            WhyVm vm = New(ctx, "why.festival.title", Money(ctx, FestivalRules.Cost(sim, kind)));
            Row(vm, ctx, "why.festival.perPerson", Money(ctx, d.Kinds[kind].PerPerson * Sim.Core.Finance.Money.Coin));
            Row(vm, ctx, "why.festival.people", ctx.Fmt.Integer(FestivalRules.People(sim)));
            Row(vm, ctx, "why.festival.lift", ctx.Strings.Get("why.festival.liftValue", "in", ctx.Fmt.Integer(d.InReach), "out", ctx.Fmt.Integer(d.Outside)));
            Row(vm, ctx, "why.festival.shops", ctx.Strings.Get("why.festival.shopsValue", "chai", Pct(ctx, d.ShopChai), "veg", Pct(ctx, d.ShopVeg)));
            Text(vm, ctx, "why.festival.text");
            return vm;
        }

        private static WhyVm LifeScore(ViewContext ctx, long id)
        {
            int i = (int)id;
            TownLifeDef d = TownLifeRules.Def(ctx.Sim);
            TownLifeState st = TownLifeRules.State(ctx.Sim);
            WhyVm vm = New(ctx, "why.life.score.title", Num(ctx, st.Reputation[i]), "name", ctx.Strings.Get("life.score." + i));
            for (int k = 0; k < TownLifeDef.PartsEach; k++)
            {
                Fixed part = st.Parts[i * TownLifeDef.PartsEach + k];
                string label = ctx.Strings.Get("life.part." + i + "." + k, "v", Num(ctx, part)) + " · " + ctx.Strings.Get("why.life.weight", "w", ctx.Fmt.Integer(d.Weights[i][k]));
                vm.Rows.Add(new WhyRowVm(label, ctx.Strings.Get("why.life.contrib", "n", ctx.Fmt.Decimal1(part.MulRatio(d.Weights[i][k], 100)))));
            }
            vm.Footnote = ctx.Strings.Get("why.life.score.text." + i);
            return vm;
        }

        private static WhyVm LifePull(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            TownLifeState st = TownLifeRules.State(sim);
            Fixed pull = TownLifeRules.Pull(sim);
            WhyVm vm = New(ctx, "why.life.pull.title", Num(ctx, pull));
            Row(vm, ctx, "why.life.pull.approval", Num(ctx, st.Approval));
            Row(vm, ctx, "why.life.pull.mean", Num(ctx, TownLifeRules.Mean(st)));
            long hours = sim.Config.Game.Population.FamilyEveryHours;
            Row(vm, ctx, "why.life.pull.wait", ctx.Strings.Get("why.life.hours", "n", ctx.Fmt.Integer((Fixed.FromInt(hours * 100) / pull).RoundToLong()), "base", ctx.Fmt.Integer(hours)));
            Row(vm, ctx, "why.life.pull.grants", ctx.Fmt.Decimal1((TownLifeRules.GrantFactor(sim) - Fixed.One) * Fixed.FromInt(100)) + "%");
            Text(vm, ctx, "why.life.pull.text");
            return vm;
        }

        private static WhyVm LifeCrime(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            TownLifeDef d = TownLifeRules.Def(sim);
            TownLifeState st = TownLifeRules.State(sim);
            WhyVm vm = New(ctx, "why.life.crime.title", Num(ctx, st.Crime));
            Row(vm, ctx, "why.life.crime.base", ctx.Fmt.Integer(d.CrimeBase));
            Row(vm, ctx, "why.life.crime.unemployed", Num(ctx, d.CrimePerUnemployed * Fixed.FromRatio(st.UnemployedPermille, 10)), "n", ctx.Fmt.Decimal1(Fixed.FromRatio(st.UnemployedPermille, 10)));
            Row(vm, ctx, "why.life.crime.inequality", Num(ctx, d.CrimeInequalityShare * st.Score), "n", Num(ctx, st.Score));
            Fixed watch = st.Crime - TownLifeRules.CrimeTown(sim);
            if (watch > Fixed.Zero) Row(vm, ctx, "why.life.crime.watchman", ctx.Strings.Get("why.life.less", "n", Num(ctx, watch)));
            Text(vm, ctx, "why.life.crime.text");
            return vm;
        }

        private static WhyVm LifeRatio(ViewContext ctx, long id)
        {
            TownLifeDef d = TownLifeRules.Def(ctx.Sim);
            TownLifeState st = TownLifeRules.State(ctx.Sim);
            if (!st.RatioKnown)
            {
                WhyVm few = New(ctx, "why.life.ratio.title", ctx.Strings.Get("life.ratioFewValue"));
                Text(few, ctx, "why.life.ratio.few", "n", ctx.Fmt.Integer(d.NeedsHouseholds));
                return few;
            }
            WhyVm vm = New(ctx, "why.life.ratio.title", ctx.Strings.Get("life.ratioValue", "n", ctx.Fmt.Decimal1(st.Ratio)));
            Row(vm, ctx, "why.life.ratio.rich", Money(ctx, st.RichestMean));
            Row(vm, ctx, "why.life.ratio.poor", Money(ctx, st.PoorestMean));
            Row(vm, ctx, "why.life.ratio.felt", ctx.Strings.Get("why.life.points", "n", ctx.Fmt.Integer(System.Math.Max(0, System.Math.Min(d.MaxPoints, (int)st.Ratio.FloorToLong() - d.HappinessAbove)))));
            Text(vm, ctx, "why.life.ratio.text", "n", ctx.Fmt.Integer(d.HappinessAbove), "max", ctx.Fmt.Integer(d.PushOutAt));
            return vm;
        }

        /// <summary>The crime overlay's card: the acre's crime, the town's, and what the watchman takes off here.</summary>
        internal static WhyVm LifeCrimeAcre(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            TownLifeState st = TownLifeRules.State(sim);
            Fixed here = TownLifeRules.CrimeAtTile(sim, (int)id);
            WhyVm vm = New(ctx, "why.life.acre.title", Num(ctx, here));
            Row(vm, ctx, "why.life.acre.town", Num(ctx, st.Crime));
            Row(vm, ctx, "why.life.acre.watchman", ctx.Strings.Get("why.life.less", "n", Num(ctx, st.Crime - here)));
            Row(vm, ctx, "why.life.acre.value", ctx.Fmt.Decimal2(TownLifeRules.LandValueTerm(sim, (int)id)));
            Text(vm, ctx, "why.life.acre.text");
            return vm;
        }
    }
}
