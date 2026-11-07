using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Equipment;
using Sim.Core.Finance;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>
    /// Menu → Equipment (spec 013 §7): each machine with what it does; yours with its state, book and resale value, maintenance and
    /// Sell; otherwise Buy (the tractor with a loan too), Lease and Hire a day as picture buttons, refusals in words.
    /// </summary>
    public static class EquipmentSheet
    {
        public const string Kind = "Equipment";

        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "tractor", Title = s.Get("equipment.title"), Subtitle = s.Get("equipment.subtitle") };
            if (!EquipmentRules.On(sim)) return vm;
            EquipmentDefs defs = EquipmentRules.Defs(sim);
            for (int k = 0; k < defs.Machines.Count; k++)
            {
                MachineDef d = defs.Machines[k];
                vm.Lines.Add(LineVm.Header(s.Get(d.NameKey)));
                vm.Lines.Add(new LineVm(s.Get("equipment.does"), Does(ctx, d), "machine", k) { Icon = Icon(d) });
                Machine m = EquipmentRules.OfKind(sim, k);
                if (m != null) Yours(ctx, vm, m, d);
                else Offers(ctx, vm, k, d);
            }
            return vm;
        }

        internal static string Icon(MachineDef d) { return d.Bullocks > 0 ? "plough_team" : d.Id; }

        /// <summary>"Works 6 fields · labour −15% · Tools ×1.03 · carts 500 kg".</summary>
        internal static string Does(ViewContext ctx, MachineDef d)
        {
            return ctx.Strings.Get("equipment.doesValue", "acres", ctx.Fmt.Integer(d.Acres), "cut", ctx.Fmt.Percent(d.LabourCut), "tools", WhyView.Effect(ctx, d.Tools),
                "cart", ctx.Fmt.Kg(d.CartKg * 1000));
        }

        private static void Yours(ViewContext ctx, SheetVm vm, Machine m, MachineDef d)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            int acres = 0;
            foreach (int c in EquipmentRules.Cover(sim)) if (c == m.Kind) acres++;
            string state = EquipmentRules.Broken(sim, m) ? (m.RepairDue > 0 ? s.Get("equipment.waitsRepair", "cost", ctx.Fmt.Coins(m.RepairDue))
                    : s.Get("equipment.broken", "time", ctx.Fmt.Duration(m.BrokenUntil - ctx.Now)))
                : m.Unpaid ? s.Get(m.Leased ? "equipment.unpaidLease" : "equipment.unpaid")
                : d.Bullocks > 0 && EquipmentRules.Bullocks(sim) < d.Bullocks ? s.Get("equipment.noBullocks")
                : s.Plural("equipment.working", acres);
            vm.Lines.Add(new LineVm(s.Get(m.Leased ? "equipment.leased" : "equipment.bought"), state, "machine", m.Kind) { Icon = Icon(d) });
            if (!m.Leased)
            {
                vm.Lines.Add(new LineVm(s.Get("equipment.value"), s.Get("equipment.valueValue", "book", ctx.Fmt.Coins(EquipmentRules.Book(m)), "market", ctx.Fmt.Coins(EquipmentRules.Resale(sim, m))),
                    "machineValue", m.Id));
                long upkeep = EquipmentRules.Maintenance(sim, m);
                if (upkeep > 0)
                {
                    long id = m.Id;
                    bool on = m.Maintain;
                    vm.Lines.Add(new LineVm(s.Get("equipment.upkeep"), on ? s.Get("equipment.upkeepOn", "cost", ctx.Fmt.Coins(upkeep)) : s.Get("equipment.upkeepOff"), "machineValue", m.Id)
                    {
                        NoteTone = on ? Tone.Plain : Tone.Warn,
                        Action = ActionVm.Checked(ctx, "upkeep:" + id, s.Get(on ? "equipment.skip" : "equipment.pay"), null, ActionKind.Secondary, at => new SetMaintenance(at, id, !on)),
                    });
                }
            }
            long mid = m.Id;
            vm.Lines.Add(new LineVm(s.Get(m.Leased ? "equipment.giveBack" : "equipment.sell"), null)
            {
                Note = s.Get(m.Leased ? "equipment.giveBackNote" : "equipment.sellNote"),
                Action = ActionVm.Checked(ctx, "sell:" + mid, s.Get(m.Leased ? "equipment.giveBackButton" : "equipment.sellButton"),
                    m.Leased ? null : d.Bullocks > 0 ? s.Get("equipment.withPair", "price", ctx.Fmt.Coins(EquipmentRules.SaleValue(sim, m))) : ctx.Fmt.Coins(EquipmentRules.SaleValue(sim, m)),
                    ActionKind.Secondary, at => new SellMachine(at, mid)),
            });
        }

        private static void Offers(ViewContext ctx, SheetVm vm, int k, MachineDef d)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            string id = d.Id;
            var buttons = new List<ActionVm>();
            long diesel = DieselNow(sim, k, d), price = EquipmentRules.Price(sim, d) + EquipmentRules.BullocksPrice(sim, d) + diesel;
            buttons.Add(Act(ctx, "buy:" + id, s.Get("equipment.buy"), d.Bullocks > 0 ? s.Get("equipment.withPair", "price", ctx.Fmt.Coins(price)) : ctx.Fmt.Coins(price), Icon(d), d,
                at => new BuyMachine(at, id)));
            int bank = LandLoanSheet.Bank(sim);
            if (d.Loan && bank >= 0)
            {
                long loan = MachineLoanSheet.Loan(sim, bank, EquipmentRules.Price(sim, d)); // price includes this season's diesel: what you pay now
                ActionVm cash = buttons[0];
                bool locked = !EquipmentRules.Unlocked(sim, d);
                bool lends = loan >= sim.Config.Game.Credit.Lenders[bank].MinAmount, shortOfCash = lends && !sim.State.Ledger.CanAfford(price - loan), can = !locked && lends && !shortOfCash;
                buttons.Add(new ActionVm("buyLoan:" + id, s.Get("equipment.buyLoan"), can ? s.Get("land.buyLoanDetail", "down", ctx.Fmt.Coins(price - loan)) : null, ActionKind.Open,
                    can, can ? null : locked ? cash.DisabledReason : shortOfCash ? s.Get("equipment.short", "cost", s.Get("land.buyLoanDetail", "down", ctx.Fmt.Coins(price - loan))) : s.Get("equipment.noLoan"), null, new SheetRequest(MachineLoanSheet.Kind, k)) { Icon = "coin" });
            }
            if (d.LeasePerSeason > 0)
                buttons.Add(Act(ctx, "lease:" + id, s.Get("equipment.lease"), s.Get("equipment.leaseValue", "cost", ctx.Fmt.Coins(EquipmentRules.Lease(sim, d))), Icon(d), d,
                    at => new LeaseMachine(at, id)));
            bool hired = EquipmentRules.Hired(sim, k);
            buttons.Add(Act(ctx, "hire:" + id, s.Get(hired ? "equipment.hiredToday" : "equipment.hire"), s.Get("equipment.hireValue", "cost", ctx.Fmt.Coins(EquipmentRules.Hire(sim, d))),
                Icon(d), d, at => new HireMachine(at, id)));
            for (int i = 0; i < buttons.Count; i += 2) vm.Lines.Add(new LineVm(null, null) { Buttons = buttons.GetRange(i, System.Math.Min(2, buttons.Count - i)) }); // two a row: four were cramped
        }

        /// <summary>The rest of this season's diesel a new machine of this kind would be charged now (0 for the ox plough).</summary>
        internal static long DieselNow(Simulation sim, int kind, MachineDef d) { return Machines.DieselNow(sim, kind, d, new Machine { Kind = kind }); }

        /// <summary>A picture button checked against the sim, its refusal in words ("Comes once you're a Village", "Hired until midnight").</summary>
        private static ActionVm Act(ViewContext ctx, string id, string label, string detail, string icon, MachineDef d, System.Func<long, Command> make)
        {
            CommandResult r = ctx.Sim.Preview(make(ctx.Now));
            string reason = null;
            if (!r.Accepted)
                reason = r.Reason == RejectReason.EraLocked ? ctx.Strings.Get("equipment.locked." + ctx.Sim.Config.Game.Onboarding.Eras[d.UnlockEra].Id)
                    : r.Reason == RejectReason.AlreadyBuilt && id.StartsWith("hire:", System.StringComparison.Ordinal) ? ctx.Strings.Get("equipment.hiredUntil")
                    : r.Reason == RejectReason.InsufficientFunds ? ctx.Strings.Get("equipment.short", "cost", detail)
                    : ctx.Reject(r.Reason);
            return new ActionVm(id, label, detail, ActionKind.Primary, r.Accepted, reason, make) { Icon = icon };
        }

        /// <summary>The field card's "Worked by" row (spec 013 §3.2): the machine working this field today, or hand tools.</summary>
        internal static void AddRow(ViewContext ctx, SheetVm vm, int tileId)
        {
            Simulation sim = ctx.Sim;
            if (!EquipmentRules.On(sim) || !EquipmentRules.Fields(sim).Contains(tileId)) return;
            int k = EquipmentRules.Cover(sim)[tileId];
            vm.Lines.Add(new LineVm(ctx.Strings.Get("field.workedBy"), k < 0 ? ctx.Strings.Get("field.handTools") : ctx.Strings.Get(EquipmentRules.Defs(sim).Machines[k].NameKey),
                "fieldTools", tileId));
        }
    }

    /// <summary>Buying a machine with the bank's loan against it (spec 013 §3.2, at <c>equipmentLtv</c>): what it lends, the rest now, one row per term.</summary>
    public static class MachineLoanSheet
    {
        public const string Kind = "MachineLoan";

        /// <summary>The most the bank lends towards it (its limit with the machine counted), in whole thousands, at most its price.</summary>
        internal static long Loan(Simulation sim, int bank, long price)
        {
            long limit = CreditRules.Limit(sim, bank, 0, 0, 0, price).Limit;
            return System.Math.Min(price, limit) / (1000 * Money.Coin) * (1000 * Money.Coin);
        }

        public static SheetVm Build(ViewContext ctx, int kind)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            MachineDef d = EquipmentRules.Defs(sim).Machines[kind];
            long price = EquipmentRules.Price(sim, d);
            int bank = LandLoanSheet.Bank(sim);
            LenderDef l = sim.Config.Game.Credit.Lenders[bank];
            long loan = Loan(sim, bank, price), diesel = EquipmentSheet.DieselNow(sim, kind, d);
            var vm = new SheetVm
            {
                Icon = EquipmentSheet.Icon(d), Title = s.Get("machineLoan.title", "machine", s.Get(d.NameKey).ToLowerInvariant()),
                Subtitle = s.Get("landLoan.subtitle", "price", ctx.Fmt.Coins(price), "loan", ctx.Fmt.Coins(loan), "down", ctx.Fmt.Coins(price - loan)),
            };
            vm.Lines.Add(new LineVm(s.Get("machineLoan.why"), null));
            if (diesel > 0) vm.Lines.Add(new LineVm(s.Get("machineLoan.diesel"), ctx.Fmt.Coins(diesel), "machine", kind));
            string id = d.Id;
            foreach (int term in BorrowSheet.Terms(l))
            {
                int tm = term;
                LoanPlanVm plan = BorrowSheet.Plan(sim, bank, loan, term);
                vm.Lines.Add(new LineVm(s.Plural("borrow.over", term), s.Get("borrow.termValue", "first", ctx.Fmt.Coins(plan.Payments[0]),
                    "last", ctx.Fmt.Coins(plan.Payments[plan.Payments.Count - 1]), "interest", ctx.Fmt.Coins(plan.TotalInterest)), "borrow", bank * 1000 + term)
                {
                    Action = ActionVm.Checked(ctx, "buyLoan:" + term, s.Get("equipment.buy"), ctx.Fmt.Coins(price - loan + diesel), ActionKind.Primary,
                        at => new BuyMachine(at, id, new Financing(l.Id, loan, tm))),
                });
            }
            return vm;
        }
    }
}
