using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// The clinic plot's and the doctor's quarters plot's sheets (spec 023 §3.1, §3.2, §3.9, US-148), as previewed to the owner (Preview 3): the
    /// share and the quarters from cash or "With a loan", the build's countdown, then the doctor, who pays, the service, the reach and capacity,
    /// what the clinic saved this season and the five effects. Every row has a *Why?* (the <c>clinic</c> source).
    /// </summary>
    public static class ClinicSheet
    {
        public const string ClinicLoanKind = "ClinicLoan", QuartersLoanKind = "QuartersLoan";

        /// <summary>A row's *Why?* id (<see cref="LineVm.WhyId"/>).</summary>
        public const int Share = 0, Days = 1, Quarters = 2, Arrives = 3, Doctor = 4, Pays = 5, Service = 6, Reach = 7, Capacity = 8, Avoided = 9, Regained = 10, ElderWell = 11,
            EffSick = 12, EffLoss = 13, EffGain = 14, EffDeath = 15, EffCare = 16, QCost = 17, QDays = 18, QRent = 19, QBook = 20, QHousehold = 21, Status = 22;

        private const string Why = "clinic";

        private static LineVm Row(ViewContext ctx, string key, string value, int id) { return new LineVm(ctx.Strings.Get(key), value, Why, id); }

        // ---- the clinic plot ----

        internal static void Fill(ViewContext ctx, SheetVm vm)
        {
            Simulation sim = ctx.Sim;
            if (!ClinicRules.On(sim)) return;
            var s = ctx.Strings;
            ClinicState st = ClinicRules.State(sim);
            ClinicDef d = ClinicRules.Def(sim);
            long price = ClinicRules.Price(sim);
            if (st.Status == ClinicStatus.None)
            {
                vm.Subtitle = s.Get(ClinicRules.Unlocked(sim) ? "place.clinic_plot.line" : "clinic.lockedLine");
                if (!ClinicRules.Unlocked(sim))
                {
                    vm.Lines.Add(Row(ctx, "clinic.status", s.Get("clinic.notYet"), Status));
                    vm.Lines.Add(new LineVm(s.Get("clinic.lockedNote"), null));
                }
                else
                {
                    vm.Lines.Add(LineVm.Header(s.Get("clinic.hdrClinic")));
                    vm.Lines.Add(Row(ctx, "clinic.share", ctx.Fmt.Coins(price), Share));
                    vm.Lines.Add(Row(ctx, "clinic.builtIn", ctx.Fmt.Duration(d.BuildHours * 3600L), Days));
                    vm.Lines.Add(LineVm.Header(s.Get("clinic.hdrDoctor")));
                    vm.Lines.Add(QuartersRow(ctx));
                    if (st.Quarters == QuartersStatus.None) vm.Lines.Add(new LineVm(s.Get("clinic.quartersNoteNone"), null));
                    vm.Lines.Add(Row(ctx, "clinic.arrives", s.Get("clinic.arrivesValue", "h", ctx.Fmt.Integer(d.DoctorArrivesHours)), Arrives));
                    Effects(ctx, vm, Fixed.Zero, false);
                    vm.Lines.Add(new LineVm(s.Get("clinic.note"), null));
                }
                vm.Actions.Add(ActionVm.Checked(ctx, "buildClinic", s.Get("clinic.buildCash"), ctx.Fmt.Coins(price), ActionKind.Primary, at => new BuildClinic(at)));
                if (ClinicRules.Unlocked(sim)) vm.Actions.Add(WithLoan(ctx, "clinicLoan", s.Get("clinic.buildLoan"), price, ClinicLoanKind, ClinicRules.CanBuildClinic(sim)));
                if (ClinicRules.Unlocked(sim) && st.Quarters == QuartersStatus.None)
                    vm.Actions.Add(ActionVm.Open("quarters", s.Get("clinic.toQuarters"), new SheetRequest("Tile", ClinicRules.QuartersDef(sim).Tile), s.Get("clinic.toQuartersDetail")));
                return;
            }
            if (st.Status == ClinicStatus.Building)
            {
                vm.Icon = "clinic_building";
                vm.Title = s.Get("clinic.buildingTitle");
                vm.Subtitle = s.Get("clinic.buildingLine");
                vm.Lines.Add(Row(ctx, "clinic.readyIn", ctx.Fmt.Duration(st.ReadyAt - ctx.Now), Days));
                vm.Lines.Add(Row(ctx, "clinic.shareGave", ctx.Fmt.Coins(st.Share), Share));
                vm.Lines.Add(QuartersRow(ctx));
                if (st.Quarters == QuartersStatus.None) vm.Lines.Add(new LineVm(s.Get("clinic.quartersNoteNone"), null));
                vm.Lines.Add(new LineVm(s.Get("clinic.buildingNote"), null));
                return;
            }
            vm.Icon = "clinic";
            vm.Title = s.Get("clinic.title");
            bool inPost = st.Doctor == DoctorStatus.InPost;
            Citizen doc = ClinicRules.Doctor(sim);
            vm.Subtitle = inPost && doc != null ? s.Get("clinic.lineDoctor", "name", TownWords.Name(ctx, doc)) : s.Get(st.Doctor == DoctorStatus.Left ? "clinic.lineLeft" : "clinic.lineNone");
            vm.Lines.Add(LineVm.Header(s.Get("clinic.hdrDoctor")));
            vm.Lines.Add(Row(ctx, "clinic.doctor", DoctorText(ctx, st, doc), Doctor));
            if (!inPost)
            {
                vm.Lines.Add(QuartersRow(ctx));
                if (st.Quarters == QuartersStatus.None) vm.Lines.Add(new LineVm(s.Get("clinic.quartersNoteNone"), null));
            }
            else vm.Lines.Add(Row(ctx, "clinic.pays", PaysText(ctx), Pays));
            Fixed condition = ClinicRules.Condition(sim);
            bool unpaid = inPost && TreasuryRules.IsOpen(sim) && TreasuryRules.State(sim).Unpaid[1] > 0;
            if (unpaid) vm.Lines.Add(new LineVm(s.Get("clinic.unpaidNote"), null) { Banner = true, NoteTone = Tone.Warn });
            if (inPost)
            {
                LineVm service = Row(ctx, "clinic.service", ctx.Fmt.Percent(condition), Service);
                if (condition < Fixed.One) { service.Note = s.Get("clinic.serviceNote"); service.NoteTone = condition < TreasuryRules.Def(sim).HealthLeaveBelow ? Tone.Warn : Tone.Plain; }
                vm.Lines.Add(service);
                vm.Lines.Add(Row(ctx, "clinic.reach", s.Get("clinic.reachValue", "m", ctx.Fmt.Integer(d.Reach), "in", ctx.Fmt.Integer(HomesInReach(sim)), "of", ctx.Fmt.Integer(sim.State.Citizens.Households.Count)), Reach));
                vm.Lines.Add(Row(ctx, "clinic.capacity", s.Get("clinic.capacityValue", "cap", ctx.Fmt.Integer(d.Capacity), "n", ctx.Fmt.Integer(ClinicRules.Served(sim))), Capacity));
                vm.Lines.Add(LineVm.Header(s.Get("clinic.hdrSaved")));
                vm.Lines.Add(Row(ctx, "clinic.avoided", ctx.Fmt.Integer(st.SickAvoidedThis), Avoided));
                vm.Lines.Add(Row(ctx, "clinic.regained", ctx.Fmt.Integer(st.RegainedThis), Regained));
                vm.Lines.Add(Row(ctx, "clinic.elders", ctx.Fmt.Integer(st.EldersWellThis), ElderWell));
            }
            else vm.Lines.Add(new LineVm(s.Get(st.Doctor == DoctorStatus.Left ? "clinic.leftNote" : "clinic.waitNote"), null));
            Effects(ctx, vm, inPost ? Fixed.One * condition * ClinicRules.Scale(sim) : Fixed.Zero, inPost);
            if (st.Quarters == QuartersStatus.None) vm.Actions.Add(ActionVm.Open("quarters", s.Get("clinic.toQuarters"), new SheetRequest("Tile", ClinicRules.QuartersDef(sim).Tile), s.Get("clinic.toQuartersDetail")));
            if (TreasuryRules.IsOpen(sim) && (unpaid || st.Doctor == DoctorStatus.Left))
            {
                vm.Actions.Add(ActionVm.Open("budget", s.Get("clinic.openBudget"), new SheetRequest(TreasurySheet.Kind, 0, TreasurySheet.BudgetTab)));
                vm.Actions.Add(ActionVm.Open("give", s.Get("clinic.give"), new SheetRequest(TreasurySheet.GiveKind, 0), s.Get("treasury.giveDetail")));
            }
        }

        /// <summary>Homes of your households within the clinic's reach.</summary>
        private static int HomesInReach(Simulation sim)
        {
            int n = 0;
            foreach (Household x in sim.State.Citizens.Households)
            {
                Building b = sim.State.Land.Buildings.Find(y => y.Id == x.HomeId);
                if (b == null || ClinicRules.Covered(sim, b.TileId)) n++;
            }
            return n;
        }

        private static LineVm QuartersRow(ViewContext ctx)
        {
            var s = ctx.Strings;
            ClinicState st = ClinicRules.State(ctx.Sim);
            string v = st.Quarters == QuartersStatus.Built ? s.Get("clinic.quartersBuilt") : st.Quarters == QuartersStatus.Building
                ? s.Get("clinic.quartersBuilding", "time", ctx.Fmt.Duration(st.QuartersReadyAt - ctx.Now)) : s.Get("clinic.quartersNone");
            return Row(ctx, "clinic.quarters", v, Quarters);
        }

        private static string DoctorText(ViewContext ctx, ClinicState st, Citizen doc)
        {
            var s = ctx.Strings;
            if (st.Doctor == DoctorStatus.InPost && doc != null)
            {
                Household x = ctx.Sim.State.Citizens.Household(st.HouseholdId);
                return s.Get("clinic.doctorValue", "name", TownWords.Name(ctx, doc), "people", s.Plural("clinic.people", x == null ? 1 : x.Members.Count), "season", FieldSheets.SeasonName(ctx, st.SinceSeason));
            }
            if (st.Doctor == DoctorStatus.Coming) return s.Get("clinic.doctorComing", "time", ctx.Fmt.Duration(st.ArrivesAt - ctx.Now));
            if (st.Doctor == DoctorStatus.Left) return s.Get("clinic.doctorLeft");
            return s.Get(ClinicRules.QuartersStand(ctx.Sim) ? "clinic.doctorSoon" : "clinic.doctorWaiting");
        }

        /// <summary>Who pays him: the district before the Township, then the Health budget's allowance and the clinic's upkeep.</summary>
        private static string PaysText(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            if (!TreasuryRules.TakenOver(sim)) return ctx.Strings.Get("clinic.paysDistrict");
            return ctx.Strings.Get("clinic.paysBudget", "amount", ctx.Fmt.Coins(PaysAmount(sim)));
        }

        internal static long PaysAmount(Simulation sim)
        {
            return TreasuryRules.LineAmount(sim, LineById(sim, "doctor")) + TreasuryRules.LineAmount(sim, LineById(sim, "clinic"));
        }

        private static LineDef LineById(Simulation sim, string id)
        {
            foreach (LineDef l in TreasuryRules.Def(sim).Lines) if (l.Id == id) return l;
            return null;
        }

        /// <summary>The five effects at the clinic's effect <paramref name="e"/> (spec 023 §3.2): percents, or "—" until a doctor is in post.</summary>
        private static void Effects(ViewContext ctx, SheetVm vm, Fixed e, bool on)
        {
            var s = ctx.Strings;
            ClinicDef d = ClinicRules.Def(ctx.Sim);
            string dash = s.Get("clinic.eNone");
            vm.Lines.Add(LineVm.Header(s.Get(on ? "clinic.hdrEffects" : "clinic.hdrEffectsWill")));
            vm.Lines.Add(Row(ctx, "clinic.eSick", on ? s.Get("clinic.eFewer", "pct", ctx.Fmt.Percent(d.SickCut * e)) : dash, EffSick));
            vm.Lines.Add(Row(ctx, "clinic.eLoss", on ? s.Get("clinic.eLess", "pct", ctx.Fmt.Percent(d.LossCut * e)) : dash, EffLoss));
            vm.Lines.Add(Row(ctx, "clinic.eGain", on ? s.Get("clinic.eFaster", "pct", ctx.Fmt.Percent(d.GainBoost * e)) : dash, EffGain));
            vm.Lines.Add(Row(ctx, "clinic.eDeath", on ? s.Get("clinic.eLess", "pct", ctx.Fmt.Percent(d.DeathCut * e)) : dash, EffDeath));
            Fixed chemist = ctx.Sim.Config.Game.Population.Chemist;
            vm.Lines.Add(Row(ctx, "clinic.eCare", on ? s.Get("clinic.eMet", "pct", ctx.Fmt.Percent(chemist + (Fixed.One - chemist) * e)) : s.Get("clinic.eCareNone", "pct", ctx.Fmt.Percent(chemist)), EffCare));
        }

        // ---- the quarters plot ----

        internal static void FillQuarters(ViewContext ctx, SheetVm vm)
        {
            Simulation sim = ctx.Sim;
            if (!ClinicRules.On(sim)) return;
            var s = ctx.Strings;
            ClinicState st = ClinicRules.State(sim);
            QuartersDef q = ClinicRules.QuartersDef(sim);
            long price = ClinicRules.QuartersPrice(sim);
            if (st.Quarters == QuartersStatus.None)
            {
                vm.Subtitle = s.Get(ClinicRules.Unlocked(sim) ? "place.quarters_plot.line" : "clinic.quartersLockedLine");
                if (ClinicRules.Unlocked(sim))
                {
                    vm.Lines.Add(Row(ctx, "quarters.cost", ctx.Fmt.Coins(price), QCost));
                    vm.Lines.Add(Row(ctx, "quarters.builtIn", ctx.Fmt.Duration(q.BuildHours * 3600L), QDays));
                    vm.Lines.Add(Row(ctx, "quarters.rent", s.Get("quarters.rentValue", "amount", ctx.Fmt.Coins(ClinicRules.Rent(sim))), QRent));
                    vm.Lines.Add(Row(ctx, "quarters.pays", s.Get("quarters.paysValue"), Pays));
                    vm.Lines.Add(Row(ctx, "quarters.clinic", s.Get(st.Status == ClinicStatus.None ? "clinic.notBuilt" : st.Status == ClinicStatus.Building ? "clinic.building" : "clinic.stands"), Status));
                    vm.Lines.Add(new LineVm(s.Get("quarters.note"), null));
                }
                else vm.Lines.Add(new LineVm(s.Get("clinic.lockedNote"), null));
                vm.Actions.Add(ActionVm.Checked(ctx, "buildQuarters", s.Get("quarters.build"), ctx.Fmt.Coins(price), ActionKind.Primary, at => new BuildDoctorQuarters(at)));
                if (ClinicRules.Unlocked(sim)) vm.Actions.Add(WithLoan(ctx, "quartersLoan", s.Get("quarters.buildLoan"), price, QuartersLoanKind, ClinicRules.CanBuildQuarters(sim)));
                return;
            }
            if (st.Quarters == QuartersStatus.Building)
            {
                vm.Icon = "doctor_quarters_building";
                vm.Title = s.Get("quarters.buildingTitle");
                vm.Subtitle = s.Get("quarters.buildingLine");
                vm.Lines.Add(Row(ctx, "clinic.readyIn", ctx.Fmt.Duration(st.QuartersReadyAt - ctx.Now), QDays));
                vm.Lines.Add(Row(ctx, "quarters.costGave", ctx.Fmt.Coins(st.QuartersCost), QCost));
                vm.Lines.Add(new LineVm(s.Get("quarters.buildingNote"), null));
                return;
            }
            vm.Icon = "doctor_quarters";
            vm.Title = s.Get("quarters.doctorTitle");
            Household x = ClinicRules.DoctorHousehold(sim);
            Citizen doc = ClinicRules.Doctor(sim);
            vm.Subtitle = doc != null ? s.Get("quarters.lineDoctor", "name", TownWords.Name(ctx, doc)) : s.Get("quarters.lineEmpty");
            vm.Lines.Add(Row(ctx, "quarters.household", x == null ? s.Get("quarters.householdNone") : s.Plural("quarters.householdValue", x.Members.Count), QHousehold));
            vm.Lines.Add(Row(ctx, "quarters.rent", x == null ? s.Get("quarters.rentNone") : s.Get("quarters.rentValue", "amount", ctx.Fmt.Coins(ClinicRules.Rent(sim))), QRent));
            if (x != null) vm.Lines.Add(new LineVm(s.Get("quarters.rentPaid"), s.Get("quarters.rentPaidValue")));
            Building b = sim.State.Land.Buildings.Find(y => y.Id == st.QuartersId);
            if (b != null) vm.Lines.Add(Row(ctx, "quarters.book", ctx.Fmt.Coins(b.BookValue), QBook));
            vm.Lines.Add(new LineVm(s.Get(x == null ? "quarters.builtNoteEmpty" : "quarters.builtNote"), null));
        }

        // ---- loans (the school's and the road's, for a share or a house) ----

        /// <summary>Most the co-op bank lends toward <paramref name="price"/>: its room on what you already have, in whole thousands, at most the price.</summary>
        public static long Loan(Simulation sim, long price)
        {
            if (sim.State.Credit == null) return 0;
            int bank = LandLoanSheet.Bank(sim);
            if (bank < 0) return 0;
            long loan = System.Math.Min(price, CreditRules.Limit(sim, bank).Limit) / (1000 * Money.Coin) * (1000 * Money.Coin);
            return loan < sim.Config.Game.Credit.Lenders[bank].MinAmount ? 0 : loan;
        }

        private static ActionVm WithLoan(ViewContext ctx, string id, string label, long price, string kind, RejectReason can)
        {
            Simulation sim = ctx.Sim;
            long loan = Loan(sim, price);
            string reason = can != RejectReason.None ? ctx.Reject(can) : loan == 0 ? ctx.Strings.Get("road.noLoan")
                : !sim.State.Ledger.CanAfford(price - loan) ? ctx.Reject(RejectReason.InsufficientFunds) : null;
            return new ActionVm(id, label, loan == 0 ? null : ctx.Strings.Get("houseLoan.down", "amount", ctx.Fmt.Coins(price - loan)), ActionKind.Open, reason == null, reason, null, new SheetRequest(kind, 0));
        }

        /// <summary>The share or the quarters with a co-op loan: what it lends, the down payment, one row per term.</summary>
        public static SheetVm LoanSheet(ViewContext ctx, bool quarters)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long price = quarters ? ClinicRules.QuartersPrice(sim) : ClinicRules.Price(sim), loan = Loan(sim, price);
            int bank = LandLoanSheet.Bank(sim);
            var vm = new SheetVm
            {
                Icon = quarters ? "place_quarters_plot" : "place_clinic_plot", Title = s.Get(quarters ? "quarters.loanTitle" : "clinic.loanTitle"),
                Subtitle = s.Get("landLoan.subtitle", "price", ctx.Fmt.Coins(price), "loan", ctx.Fmt.Coins(loan), "down", ctx.Fmt.Coins(price - loan)),
            };
            vm.Lines.Add(new LineVm(s.Get(quarters ? "quarters.loanWhy" : "clinic.loanWhy"), null));
            if (loan == 0)
            {
                vm.Lines.Add(new LineVm(s.Get("road.noLoan"), null));
                return vm;
            }
            LenderDef l = sim.Config.Game.Credit.Lenders[bank];
            foreach (int term in BorrowSheet.Terms(l))
            {
                int tm = term;
                LoanPlanVm plan = BorrowSheet.Plan(sim, bank, loan, term);
                vm.Lines.Add(new LineVm(s.Plural("borrow.over", term), s.Get("borrow.termValue", "first", ctx.Fmt.Coins(plan.Payments[0]),
                    "last", ctx.Fmt.Coins(plan.Payments[plan.Payments.Count - 1]), "interest", ctx.Fmt.Coins(plan.TotalInterest)), "borrow", bank * 1000 + term)
                {
                    Action = ActionVm.Checked(ctx, (quarters ? "quartersLoan:" : "clinicLoan:") + term, s.Get(quarters ? "quarters.build" : "clinic.buildCash"), ctx.Fmt.Coins(price - loan), ActionKind.Primary,
                        at => quarters ? (Command)new BuildDoctorQuarters(at, new Financing(l.Id, loan, tm)) : new BuildClinic(at, new Financing(l.Id, loan, tm))),
                });
            }
            return vm;
        }
    }

    public static partial class WhyView
    {
        private static WhyVm ClinicWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            ClinicDef d = ClinicRules.Def(sim);
            ClinicState st = ClinicRules.State(sim);
            Fixed condition = ClinicRules.Condition(sim);
            WhyVm vm;
            switch ((int)id)
            {
                case ClinicSheet.Share:
                    vm = New(ctx, "why.clinic.share.title", Money(ctx, st.Status == ClinicStatus.None ? ClinicRules.Price(sim) : st.Share));
                    Row(vm, ctx, "why.clinic.share.base", Money(ctx, d.Share));
                    Row(vm, ctx, "why.price.index", Effect(ctx, sim.State.Market.PriceIndex));
                    Text(vm, ctx, "why.clinic.share.text");
                    vm.Footnote = s.Get("why.clinic.lesson");
                    return vm;
                case ClinicSheet.Days:
                    vm = New(ctx, "why.clinic.days.title", ctx.Fmt.Duration(d.BuildHours * 3600L));
                    if (st.Status == ClinicStatus.Building) Row(vm, ctx, "why.clinic.days.left", ctx.Fmt.Duration(st.ReadyAt - ctx.Now));
                    Text(vm, ctx, "why.clinic.days.text");
                    return vm;
                case ClinicSheet.Quarters:
                    vm = New(ctx, "why.clinic.quarters.title", s.Get(st.Quarters == QuartersStatus.Built ? "clinic.quartersBuilt" : st.Quarters == QuartersStatus.Building ? "clinic.quartersBuilding" : "clinic.quartersNone", "time", ctx.Fmt.Duration(System.Math.Max(0, st.QuartersReadyAt - ctx.Now))));
                    Text(vm, ctx, "why.clinic.quarters.text");
                    return vm;
                case ClinicSheet.Arrives:
                    vm = New(ctx, "why.clinic.arrives.title", s.Get("clinic.arrivesValue", "h", ctx.Fmt.Integer(d.DoctorArrivesHours)));
                    Text(vm, ctx, "why.clinic.arrives.text");
                    return vm;
                case ClinicSheet.Doctor:
                    vm = New(ctx, "why.clinic.doctor.title", st.Doctor == DoctorStatus.InPost ? s.Get("clinic.inPost") : s.Get("clinic.noDoctor"));
                    Text(vm, ctx, "why.clinic.doctor.text");
                    if (st.Doctor == DoctorStatus.Left) Text(vm, ctx, "why.clinic.doctor.left", "pct", ctx.Fmt.Percent(ClinicRules.DoctorDef(sim).ReturnCondition));
                    return vm;
                case ClinicSheet.Pays:
                    vm = New(ctx, "why.clinic.pays.title", TreasuryRules.TakenOver(sim) ? Money(ctx, ClinicSheet.PaysAmount(sim)) : s.Get("clinic.paysDistrict"));
                    Text(vm, ctx, TreasuryRules.TakenOver(sim) ? "why.clinic.pays.budget" : "why.clinic.pays.district");
                    return vm;
                case ClinicSheet.Service:
                    vm = New(ctx, "why.clinic.service.title", Pct(ctx, condition));
                    Row(vm, ctx, "why.clinic.service.now", Pct(ctx, condition));
                    if (TreasuryRules.IsOpen(sim)) Row(vm, ctx, "why.clinic.service.leave", Pct(ctx, TreasuryRules.Def(sim).HealthLeaveBelow));
                    Text(vm, ctx, "why.clinic.service.text");
                    return vm;
                case ClinicSheet.Reach:
                    vm = New(ctx, "why.clinic.reach.title", s.Get("clinic.reachShort", "m", ctx.Fmt.Integer(d.Reach)));
                    Row(vm, ctx, "why.clinic.reach.reach", s.Get("clinic.reachShort", "m", ctx.Fmt.Integer(d.Reach)));
                    Text(vm, ctx, "why.clinic.reach.text");
                    return vm;
                case ClinicSheet.Capacity:
                    vm = New(ctx, "why.clinic.capacity.title", s.Get("clinic.capacityValue", "cap", ctx.Fmt.Integer(d.Capacity), "n", ctx.Fmt.Integer(ClinicRules.Served(sim))));
                    Row(vm, ctx, "why.clinic.capacity.scale", Pct(ctx, ClinicRules.Scale(sim)));
                    Text(vm, ctx, "why.clinic.capacity.text");
                    return vm;
                case ClinicSheet.Avoided:
                    vm = New(ctx, "why.clinic.avoided.title", ctx.Fmt.Integer(st.SickAvoidedThis));
                    Row(vm, ctx, "why.clinic.this", ctx.Fmt.Integer(st.SickAvoidedThis));
                    Row(vm, ctx, "why.clinic.last", ctx.Fmt.Integer(st.SickAvoidedLast));
                    Text(vm, ctx, "why.clinic.avoided.text", "pct", Pct(ctx, d.SickCut));
                    return vm;
                case ClinicSheet.Regained:
                    vm = New(ctx, "why.clinic.regained.title", ctx.Fmt.Integer(st.RegainedThis));
                    Row(vm, ctx, "why.clinic.this", ctx.Fmt.Integer(st.RegainedThis));
                    Row(vm, ctx, "why.clinic.last", ctx.Fmt.Integer(st.RegainedLast));
                    Text(vm, ctx, "why.clinic.regained.text");
                    return vm;
                case ClinicSheet.ElderWell:
                    vm = New(ctx, "why.clinic.elders.title", ctx.Fmt.Integer(st.EldersWellThis));
                    Row(vm, ctx, "why.clinic.this", ctx.Fmt.Integer(st.EldersWellThis));
                    Row(vm, ctx, "why.clinic.last", ctx.Fmt.Integer(st.EldersWellLast));
                    Text(vm, ctx, "why.clinic.elders.text", "pct", Pct(ctx, d.DeathCut));
                    return vm;
                case ClinicSheet.EffSick: return Effect5(ctx, "sick", d.SickCut);
                case ClinicSheet.EffLoss: return Effect5(ctx, "loss", d.LossCut);
                case ClinicSheet.EffGain: return Effect5(ctx, "gain", d.GainBoost);
                case ClinicSheet.EffDeath: return Effect5(ctx, "death", d.DeathCut);
                case ClinicSheet.EffCare:
                    vm = Effect5(ctx, "care", Fixed.One - ctx.Sim.Config.Game.Population.Chemist);
                    return vm;
                case ClinicSheet.QCost:
                    vm = New(ctx, "why.quarters.cost.title", Money(ctx, st.Quarters == QuartersStatus.None ? ClinicRules.QuartersPrice(sim) : st.QuartersCost));
                    Row(vm, ctx, "why.quarters.cost.base", Money(ctx, sim.Config.Game.Buildings[ClinicRules.QuartersDef(sim).Building].Value));
                    Row(vm, ctx, "why.price.index", Effect(ctx, sim.State.Market.PriceIndex));
                    Text(vm, ctx, "why.quarters.cost.text");
                    return vm;
                case ClinicSheet.QDays:
                    vm = New(ctx, "why.quarters.days.title", ctx.Fmt.Duration(ClinicRules.QuartersDef(sim).BuildHours * 3600L));
                    Text(vm, ctx, "why.quarters.days.text");
                    return vm;
                case ClinicSheet.QRent:
                    vm = New(ctx, "why.quarters.rent.title", Money(ctx, ClinicRules.Rent(sim)));
                    Row(vm, ctx, "why.quarters.rent.base", Money(ctx, ClinicRules.QuartersDef(sim).Rent));
                    Row(vm, ctx, "why.price.index", Effect(ctx, sim.State.Market.PriceIndex));
                    Text(vm, ctx, "why.quarters.rent.text");
                    return vm;
                case ClinicSheet.QBook:
                    vm = New(ctx, "why.quarters.book.title", s.Get("clinic.quartersBuilt"));
                    Text(vm, ctx, "why.quarters.book.text");
                    return vm;
                case ClinicSheet.QHousehold:
                    vm = New(ctx, "why.quarters.household.title", s.Get("clinic.inPost"));
                    Text(vm, ctx, "why.quarters.household.text");
                    return vm;
                default:
                    vm = New(ctx, "why.clinic.status.title", s.Get(ClinicRules.Unlocked(sim) ? "clinic.notBuilt" : "clinic.notYet"));
                    Text(vm, ctx, "why.clinic.status.text");
                    return vm;
            }
        }

        private static WhyVm Effect5(ViewContext ctx, string key, Fixed cut)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Fixed condition = ClinicRules.Condition(sim), scale = ClinicRules.Scale(sim);
            WhyVm vm = New(ctx, "why.clinic.e." + key + ".title", (key == "care" ? "+" : "") + Pct(ctx, cut * condition * scale));
            Row(vm, ctx, "why.clinic.e.full", Pct(ctx, cut));
            Row(vm, ctx, "why.clinic.e.service", Pct(ctx, condition));
            Row(vm, ctx, "why.clinic.e.scale", Pct(ctx, scale));
            Text(vm, ctx, "why.clinic.e." + key + ".text");
            return vm;
        }
    }
}
