using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// Forward contracts on the Store sheet (spec 027 §7, US-156): the trader's offers with Accept (a tap card that says it is binding), your contracts (what is delivered,
    /// what is left, the days, against the market) and the last settled. Everything here reads the state.
    /// </summary>
    public static class ContractSheet
    {
        public const string OfferWhy = "conOffer", OpenWhy = "conOpen", DoneWhy = "conDone", DueWhy = "conDue";
        private const long Day = Sim.Core.Definitions.ClockDef.SecondsPerDay;

        /// <summary>The offers, your contracts and the last two settled, under the trader's sale rows. Nothing before the Township.</summary>
        public static void Add(ViewContext ctx, SheetVm vm)
        {
            Simulation sim = ctx.Sim;
            if (!ContractRules.Available(sim)) return;
            var s = ctx.Strings;
            ContractsState st = ContractRules.State(sim);
            ContractsDef d = ContractRules.Def(sim);

            vm.Lines.Add(LineVm.Header(s.Get("contract.offersTitle")));
            if (st.Offers.Count == 0) vm.Lines.Add(new LineVm(s.Get("contract.noOffers", "kg", ctx.Fmt.Integer(d.MinKg)), null) { Dim = true });
            foreach (Contract o in st.Offers)
            {
                long id = o.Id;
                GoodDef good = Good(sim, o);
                vm.Lines.Add(new LineVm(s.Get(good.NameKey), s.Get("contract.offerValue", "qty", ctx.Fmt.Kg(o.Quantity), "price", ctx.Fmt.PricePerKg(o.Price)), OfferWhy, o.Id)
                {
                    Icon = good.Id, Note = s.Get("contract.offerNote", "expected", ctx.Fmt.PricePerKg(o.Expected), "season", FieldSheets.SeasonName(ctx, o.Delivery)),
                    Action = ActionVm.Checked(ctx, "contract:" + id, s.Get("contract.accept"), null, ActionKind.Primary, at => new AcceptContract(at, id)),
                });
            }

            if (st.Open.Count > 0) vm.Lines.Add(LineVm.Header(s.Get("contract.yoursTitle")));
            foreach (Contract c in st.Open)
            {
                GoodDef good = Good(sim, c);
                bool running = c.Delivery == sim.Period;
                var row = new LineVm(s.Get(good.NameKey), running ? s.Get("contract.runValue", "filled", ctx.Fmt.Kg(c.Filled), "qty", ctx.Fmt.Kg(c.Quantity)) : s.Get("contract.waitValue", "qty", ctx.Fmt.Kg(c.Quantity), "price", ctx.Fmt.PricePerKg(c.Price)), OpenWhy, c.Id) { Icon = good.Id };
                row.Note = running ? s.Get("contract.runNote", "left", ctx.Fmt.Kg(c.Left), "days", Days(ctx, (sim.Period + 1) * sim.Clock.SecondsPerSeason - sim.State.Clock.SimTime), "price", ctx.Fmt.PricePerKg(c.Price), "against", Against(ctx, ContractRules.Against(sim, c)))
                    : s.Get("contract.waitNote", "season", FieldSheets.SeasonName(ctx, c.Delivery), "days", Days(ctx, c.Delivery * sim.Clock.SecondsPerSeason - sim.State.Clock.SimTime));
                row.NoteTone = running ? Tone(ContractRules.Against(sim, c)) : Sim.View.Views.Tone.Plain;
                vm.Lines.Add(row);
            }

            int shown = 0;
            for (int i = st.History.Count - 1; i >= 0 && shown < 2; i--, shown++)
            {
                if (shown == 0) vm.Lines.Add(LineVm.Header(s.Get("contract.doneTitle")));
                ContractRecord r = st.History[i];
                GoodDef good = sim.Config.Game.Market.Goods[sim.Config.Game.Market.IndexOf(r.Good)];
                vm.Lines.Add(new LineVm(s.Get(good.NameKey), s.Get("contract.doneValue", "filled", ctx.Fmt.Kg(r.Filled), "qty", ctx.Fmt.Kg(r.Quantity)), DoneWhy, i)
                {
                    Icon = good.Id, Note = (r.Shortfall > 0 ? s.Get("contract.doneShort", "amount", ctx.Fmt.Coins(r.Shortfall)) + " · " : "") + Against(ctx, r.Against), NoteTone = Tone(r.Against),
                });
            }
        }

        /// <summary>One alert while the trader has an offer you have not answered: it names the crops and opens the Store.</summary>
        public static void Alerts(ViewContext ctx, System.Collections.Generic.List<AlertVm> list)
        {
            Simulation sim = ctx.Sim;
            if (!ContractRules.Available(sim)) return;
            ContractsState st = ContractRules.State(sim);
            if (st.Offers.Count == 0) return;
            var s = ctx.Strings;
            var names = new System.Collections.Generic.List<string>();
            foreach (Contract o in st.Offers) names.Add(s.Get(Good(sim, o).NameKey).ToLowerInvariant());
            list.Add(new AlertVm
            {
                Level = AlertLevel.Info, Text = s.Get("alert.contractOffers", "crops", string.Join(", ", names)),
                Action = ActionVm.Open("contracts", s.Get("action.openStore"), new SheetRequest("Store", 0)),
            });
        }

        private static GoodDef Good(Simulation sim, Contract c) { return sim.Config.Game.Market.Goods[sim.Config.Game.Market.IndexOf(c.Good)]; }

        private static Sim.View.Views.Tone Tone(long against) { return against > 0 ? Sim.View.Views.Tone.Good : against < 0 ? Sim.View.Views.Tone.Warn : Sim.View.Views.Tone.Plain; }

        /// <summary>"3 days" or "1 day", rounded up.</summary>
        public static string Days(ViewContext ctx, long seconds) { return ctx.Strings.Plural("contract.days", System.Math.Max(1, (seconds + Day - 1) / Day)); }

        /// <summary>"against the market: you gained 1,200", "…lost 400", or "even with the market".</summary>
        public static string Against(ViewContext ctx, long against)
        {
            var s = ctx.Strings;
            return against == 0 ? s.Get("contract.againstEven") : against > 0 ? s.Get("contract.againstGain", "amount", ctx.Fmt.Coins(against)) : s.Get("contract.againstLoss", "amount", ctx.Fmt.Coins(-against));
        }
    }
}
