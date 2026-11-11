using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Finance;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary><em>Your wealth</em>'s hub (spec 019 §3.2, ADR 0101 §6): net worth's trend in today's coins, what your wealth is made of, and three ratios.</summary>
    public static class HubSheet
    {
        public const int TrendRows = 8;

        /// <summary>The hub rows' <em>Why?</em> ids.</summary>
        public const int Trend = 0, Made = 1, Debt = 2, Emergency = 3, Passive = 4;

        /// <summary>The asset classes of the composition, in order: (key, amount).</summary>
        public static List<KeyValuePair<string, long>> Classes(NetWorthSnapshot n)
        {
            var list = new List<KeyValuePair<string, long>>();
            void Add(string k, long v) { if (v > 0) list.Add(new KeyValuePair<string, long>(k, v)); }
            Add("land", n.LandAtMarket);
            Add("buildings", n.BuildingsAtBook + n.EquipmentAtBook);
            Add("livestock", n.TreesAtBook + n.LivestockAtBook);
            Add("goods", n.StoredGoods + n.Prepaid);
            Add("cash", n.Cash + n.Deposit);
            Add("businesses", n.Businesses);
            Add("owed", n.OwedByBuyers);
            return list;
        }

        /// <summary>Net worth's points for the chart: the closes, then now, all in today's coins.</summary>
        public static List<long> TrendValues(Simulation sim, NetWorthSnapshot now)
        {
            var values = new List<long>();
            foreach (TrendPoint p in Hub.Trend(sim, TrendRows)) values.Add(p.InTodaysCoins);
            values.Add(now.Total);
            return values;
        }

        public static string Pct(ViewContext ctx, long n) { return ctx.Fmt.Integer(n) + "%"; }

        public static string Tenths(ViewContext ctx, long tenths) { return ctx.Fmt.Integer(tenths / 10) + "." + ctx.Fmt.Integer(tenths % 10); }

        /// <summary>The hub's rows: the trend, the composition, the three ratios.</summary>
        public static void Add(ViewContext ctx, SheetVm vm, NetWorthSnapshot worth)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            vm.Lines.Add(LineVm.Header(s.Get("hub.wealth")));

            List<long> values = TrendValues(sim, worth);
            var trend = new LineVm(s.Get("hub.trend"), ctx.Fmt.Coins(worth.Total), StatementSheet.HubWhy, Trend);
            if (values.Count >= 2)
            {
                var t = new TrendVm { FirstCaption = ReportSheet.PeriodName(ctx, sim.State.Finance.Reports[System.Math.Max(0, sim.State.Finance.Reports.Count - TrendRows)].Period), LastCaption = s.Get("hub.why.trendNow") };
                t.Values.AddRange(values);
                trend.Trend = t;
            }
            else trend.Note = s.Get("hub.trendFirst");
            vm.Lines.Add(trend);

            long own = Hub.Owned(worth);
            var classes = Classes(worth);
            var pie = new List<PieSliceVm>();
            foreach (KeyValuePair<string, long> c in classes)
                pie.Add(new PieSliceVm(s.Get("hub.class." + c.Key), (int)(new System.Numerics.BigInteger(c.Value) * 100 / System.Math.Max(1, own)), ctx.Fmt.Coins(c.Value), c.Key));
            vm.Lines.Add(new LineVm(s.Get("hub.made"), s.Get("hub.madeValue", "amount", ctx.Fmt.Coins(own)), StatementSheet.HubWhy, Made) { Pie = pie });

            DebtBand band = Hub.Band(sim, worth);
            var debt = new LineVm(s.Get("hub.debt"), band == DebtBand.None ? s.Get("hub.debtNone") : s.Get("hub.debtValue", "pct", ctx.Fmt.Integer(Hub.DebtPercent(worth))), StatementSheet.HubWhy, Debt) { Icon = "bank" };
            if (band != DebtBand.None)
            {
                debt.Note = s.Get("hub.debtNote", "owed", ctx.Fmt.Coins(Hub.Owed(worth)), "own", ctx.Fmt.Coins(own), "band", s.Get("hub.band." + band));
                debt.NoteTone = band == DebtBand.Risky ? Tone.Warn : band == DebtBand.Comfortable ? Tone.Good : Tone.Plain;
            }
            vm.Lines.Add(debt);

            long tenths = Hub.EmergencyTenths(sim);
            var emergency = new LineVm(s.Get("hub.emergency"), tenths < 0 ? s.Get("hub.emergencyNothing") : s.Get("hub.emergencyValue", "n", Tenths(ctx, tenths)), StatementSheet.HubWhy, Emergency) { Icon = "coin" };
            emergency.Note = tenths >= 0 && tenths < 10 ? s.Get("hub.emergencyLow") : s.Get("hub.emergencyNote");
            emergency.NoteTone = tenths >= 0 && tenths < 10 ? Tone.Warn : Tone.Plain;
            vm.Lines.Add(emergency);

            int passive = Hub.PassivePercent(sim);
            vm.Lines.Add(new LineVm(s.Get("hub.passive"), s.Get("hub.passiveValue", "pct", ctx.Fmt.Integer(passive)), StatementSheet.HubWhy, Passive)
                { Icon = "land", Note = passive >= 100 ? s.Get("hub.passiveCovers") : s.Get("hub.passiveNote"), NoteTone = passive >= 100 ? Tone.Good : Tone.Plain });
        }
    }

    /// <summary>Borrowing's front door (US-135): the lenders, each with its limit and rate and a Borrow button.</summary>
    public static class LendersSheet
    {
        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "bank", Title = s.Get("lenders.title"), Subtitle = s.Get("lenders.subtitle") };
            if (sim.State.Credit == null) return vm;
            for (int i = 0; i < sim.Config.Game.Credit.Lenders.Count; i++)
            {
                Sim.Core.Definitions.LenderDef l = sim.Config.Game.Credit.Lenders[i];
                CreditLimit c = CreditRules.Limit(sim, i);
                bool can = !c.ScoreTooLow && c.Limit >= l.MinAmount;
                string why = c.ScoreTooLow ? ctx.Reject(RejectReason.ScoreTooLow) : can ? null : s.Get("money.cantBorrow", "min", ctx.Fmt.Coins(l.MinAmount));
                vm.Lines.Add(new LineVm(s.Get(l.NameKey), s.Get("money.lenderValue", "limit", ctx.Fmt.Coins(c.Limit), "rate", ctx.Fmt.Percent(CreditRules.Rate(l, sim.State.Credit.Score))), "creditLimit", i)
                {
                    Icon = "bank",
                    Action = new ActionVm("borrow:" + l.Id, s.Get("money.borrowButton"), null, ActionKind.Open, can, why, null, new SheetRequest("Borrow", i)),
                });
            }
            return vm;
        }

        /// <summary>The most you could borrow now from any lender, or 0.</summary>
        public static long Best(Simulation sim)
        {
            long best = 0;
            if (sim.State.Credit == null) return 0;
            for (int i = 0; i < sim.Config.Game.Credit.Lenders.Count; i++)
            {
                CreditLimit c = CreditRules.Limit(sim, i);
                if (!c.ScoreTooLow && c.Limit >= sim.Config.Game.Credit.Lenders[i].MinAmount && c.Limit > best) best = c.Limit;
            }
            return best;
        }
    }
}
