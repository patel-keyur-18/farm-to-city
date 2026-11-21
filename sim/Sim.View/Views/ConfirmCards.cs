using System;
using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>How a player command asks first (spec 008 §3.15, ADR 0101 §12), as Hay Day: everyday play is one tap.</summary>
    public enum ConfirmKind
    {
        /// <summary>One tap, no card.</summary>
        Everyday = 0,

        /// <summary>Card B: spending on something that lasts; a normal tap on the card.</summary>
        Tap = 1,

        /// <summary>Card C: selling, closing, destroying or ending; the red button is held.</summary>
        Hold = 2,

        /// <summary>Not a player's action: the clock, a new game, the developer panel.</summary>
        System = 3,
    }

    /// <summary>Every command sorted into everyday, tap or hold. <c>ConfirmTests.EveryCommand</c> fails when a command is left out.</summary>
    public static class ConfirmRules
    {
        private static readonly Dictionary<Type, ConfirmKind> Kinds = Build();

        private static Dictionary<Type, ConfirmKind> Build()
        {
            var k = new Dictionary<Type, ConfirmKind>();
            void Add(ConfirmKind kind, params Type[] types) { foreach (Type t in types) k[t] = kind; }
            Add(ConfirmKind.Tap, typeof(BuyParcel), typeof(BuildFamilyHouse), typeof(UpgradeHouse), typeof(BuildHandHut), typeof(BuildSchool), typeof(BuildShed), typeof(FoundBusiness),
                typeof(BuyMachine), typeof(LeaseMachine), typeof(HireMachine), typeof(InstallPump), typeof(DrillBorewell), typeof(BuildChannel), typeof(LayDrip),
                typeof(BuildSilagePit), typeof(BuyChaffCutter), typeof(FitSolarPanel), typeof(BuyGodown), typeof(RentWarehouse), typeof(RentColdStore), typeof(PlantOrchard), typeof(PlantTree), typeof(BuyAnimal), typeof(TakeLoan), typeof(RestructureLoan),
                typeof(OfferContract), typeof(OfferTownsperson), typeof(RenewContract), typeof(SendOnCourse), typeof(StartAdultClass), typeof(SetSavingsCover), typeof(GiveToTreasury), typeof(ListParcel), typeof(CancelSale), typeof(ZoneParcel), typeof(LeaseOutLand), typeof(EndLease), typeof(CommissionWorks), typeof(AnswerEvent), typeof(PetitionRoad), typeof(BuildClinic), typeof(BuildDoctorQuarters), typeof(EnactPolicy), typeof(AnswerPetition), typeof(ScheduleFestival));
            Add(ConfirmKind.Hold, typeof(SellParcel), typeof(SellMachine), typeof(SellAnimal), typeof(SellOrchardOnTrees), typeof(CloseBusiness), typeof(ClearField),
                typeof(ClearOrchard), typeof(RemoveTree), typeof(DismissHand), typeof(LetWorkerGo), typeof(RepealPolicy));
            Add(ConfirmKind.Everyday, typeof(Sow), typeof(Harvest), typeof(Spray), typeof(PickFruit), typeof(PickGarden), typeof(SowGarden), typeof(PickOrchard),
                typeof(SellToTrader), typeof(DeliverOrder), typeof(GoToMandi), typeof(Buy), typeof(BuyMedicine), typeof(HireLabourers), typeof(Offer), typeof(Withdraw),
                typeof(SetBorewellAcres), typeof(SetBusiness), typeof(SetBuyGreenFodder), typeof(SetKeepHeifers), typeof(SetHireOut), typeof(SetDietPlan), typeof(SetEatFromStore), typeof(SetLabourSplit),
                typeof(SetLongDays), typeof(SetMaintenance), typeof(SetMix), typeof(SetPumpAcres), typeof(SetShop), typeof(SetStallGoods), typeof(SetStandingOrder),
                typeof(SetWorkHours), typeof(ClearStandingOrder), typeof(UnzoneParcel), typeof(PutCash), typeof(TakeCash), typeof(CheckReference), typeof(Interview), typeof(PostJob),
                typeof(RepayLoan), typeof(DepositSavings), typeof(WithdrawSavings), typeof(CoverFromSavings), typeof(TakeBackSavingsCover), typeof(LetGodown), typeof(RunBusinessYourself), typeof(AnswerHrEvent), typeof(AnswerFamilyRequest), typeof(RenewLease), typeof(PostCivicJob), typeof(DismissStaff), typeof(SetBudget), typeof(AcceptBudget), typeof(SetTaxRate), typeof(UndoTaxRate), typeof(SeeTaxTour), typeof(SeePolicyTour), typeof(SeeChapter),
                typeof(CompleteStep), typeof(SkipFirstSession), typeof(FindCodexCards), typeof(ReadCodexCard), typeof(HearAdvisors), typeof(SetCropInsurance), typeof(SetPropertyInsurance), typeof(SetBusinessInsurance));
            Add(ConfirmKind.System, typeof(AdvanceTo), typeof(DebugAdvance), typeof(DebugNextEra), typeof(LiveRun), typeof(NewGame), typeof(Sync));
            return k;
        }

        /// <summary>The kind a command type is sorted as; <see cref="ConfirmKind.System"/> only for the clock and the developer's.</summary>
        public static bool TryKind(Type commandType, out ConfirmKind kind) { return Kinds.TryGetValue(commandType, out kind); }

        public static ConfirmKind Of(Command c) { return Kinds.TryGetValue(c.GetType(), out ConfirmKind k) ? k : ConfirmKind.Everyday; }

        public static IEnumerable<Type> Sorted { get { return Kinds.Keys; } }
    }

    /// <summary>One row of a confirm card: what it is, its coins, and whether it is the bold total.</summary>
    public sealed class ConfirmRowVm
    {
        public string Label { get; }
        public string Value { get; }
        public bool Total { get; }

        internal ConfirmRowVm(string label, string value, bool total = false)
        {
            Label = label;
            Value = value;
            Total = total;
        }
    }

    /// <summary>
    /// A confirm card (spec 008 §3.15): a question, its numbers, a safe button and the action. A hold card's action is held for
    /// <see cref="HoldMillis"/>; a tap card's is a normal tap.
    /// </summary>
    public sealed class ConfirmCardVm
    {
        public string Title { get; internal set; }
        public string Subtitle { get; internal set; }
        public List<ConfirmRowVm> Rows { get; } = new List<ConfirmRowVm>();
        public string Warning { get; internal set; }

        /// <summary>The note is good news in green, not a warning (the budget card's "nothing breaks").</summary>
        public bool Reassure { get; internal set; }

        /// <summary>The big safe button ("Not now", "Keep my land").</summary>
        public string SafeLabel { get; internal set; }

        /// <summary>The action's button ("Buy for 4,80,000", "Hold to sell").</summary>
        public string GoLabel { get; internal set; }
        public bool Hold { get; internal set; }
        public int HoldMillis { get; internal set; }

        /// <summary>What the command changes, as the sim itself computes it on a copy: cash paid (negative) or received, and the loan taken or repaid.</summary>
        public long CashChange { get; internal set; }
        public long LoanTaken { get; internal set; }
        public long LoanRepaid { get; internal set; }
    }

    /// <summary>Builds a command's confirm card from what the command would really do, run on a copy of the state (so the numbers are the sim's own).</summary>
    public static class ConfirmCards
    {
        /// <summary>The card for a command, or null when it is everyday or the sim would refuse it anyway (the refusal then shows as it does today).</summary>
        public static ConfirmCardVm For(ViewContext ctx, Command c)
        {
            ConfirmKind kind = ConfirmRules.Of(c);
            if (kind != ConfirmKind.Tap && kind != ConfirmKind.Hold) return null;
            if (c is AnswerPetition promise && promise.Closes != 0) return null; // a promise costs nothing and ends nothing: it is its own choice sheet
            if (c is RepealPolicy undo && ctx.Sim.State.Treasury != null && ctx.Sim.State.Treasury.Policies[(int)undo.Policy].PaidCloses == 0) return null; // never paid: taking it back costs nothing and ends at once
            if (c is EndLease cancel && Sim.Core.Town.LandLeaseRules.StatusOf(ctx.Sim.State.Land.Parcels[ctx.Sim.Config.Game.Map.IndexOfParcel(cancel.ParcelId)]) == Sim.Core.Land.LeaseStatus.Starting) return null; // not started: cancelling costs nothing and ends at once
            if (c is EnactPolicy keep && ctx.Sim.State.Treasury != null && ctx.Sim.State.Treasury.Policies[(int)keep.Policy].Repealing) return null; // keeping a policy that is ending costs nothing new
            Simulation sim = ctx.Sim;
            CommandResult result = sim.Preview(c, out Simulation after);
            if (!result.Accepted) return null;
            var s = ctx.Strings;
            string name = c.GetType().Name;
            long cash = after.State.Ledger.Cash - sim.State.Ledger.Cash;
            if (c is AnswerEvent && cash == 0) return null; // a choice that costs nothing is just answered (spec 027 §3.1)
            if ((c is RentWarehouse || c is RentColdStore) && cash == 0) return null; // stopping or cutting the rent spends nothing: no card (ADR 0103)
            if (c is SetSavingsCover cover && !cover.Always) return null; // back to Ask me: nothing can move, so nothing to ask
            long owedBefore = Owed(sim), owedAfter = Owed(after);
            long taken = NewLoans(sim, after, out Loan newest);
            long repaid = System.Math.Max(0, owedBefore - owedAfter);
            var vm = new ConfirmCardVm
            {
                Title = s.Get("confirm." + name + ".title"), Subtitle = Subject(ctx, c), Hold = kind == ConfirmKind.Hold, HoldMillis = ctx.Ui.ConfirmHoldMillis,
                CashChange = cash, LoanTaken = taken, LoanRepaid = repaid,
            };
            if (c is SetSavingsCover) Standing(ctx, vm);
            else if (c is GiveToTreasury gift) Gift(ctx, vm, gift, after);
            else if (c is BuildFamilyHouse house && house.Financing == null && Sim.Core.Town.ConstructionRules.On(sim)) BuildingSheet.CardRows(ctx, vm, Sim.Core.Town.ConstructionRules.Quote(sim, "family_house", Sim.Core.Town.BuildProject.Yours, 1), "build.cardNowYours", "build.cardAfterYours", sim.State.Ledger.Cash, after.State.Ledger.Cash);
            else if (c is UpgradeHouse upgrade) UpgradeSheet.Card(ctx, vm, sim, after, upgrade);
            else if (c is CommissionWorks works) Works(ctx, vm, sim, after, works);
            else if (c is EnactPolicy enact) Enacting(ctx, vm, sim, after, enact);
            else if (c is PetitionRoad || c is BuildClinic || c is BuildDoctorQuarters) Road(ctx, vm, cash, taken, after, newest, name);
            else if (c is AnswerPetition) Declining(ctx, vm);
            else if (c is AnswerEvent insured && Sim.Core.Town.EventRules.State(sim)?.Waiting.Find(x => x.Id == insured.EventId) is Sim.Core.Town.PendingEvent repair && Sim.Core.Town.InsuranceRules.RepairPays(sim, repair, insured.Choice) > 0) InsuredRepair(ctx, vm, sim, after, repair, insured.Choice); // US-155: an insured fire's bill, the refund and what you pay
            else if (c is ScheduleFestival fest) Festival(ctx, vm, sim, after, fest);
            else if (c is RepealPolicy repeal) Repealing(ctx, vm, sim, repeal);
            else if (c is ListParcel || c is CancelSale) Listing(ctx, vm, c);
            else if (c is ZoneParcel zp) Zoning(ctx, vm, zp);
            else if (c is LeaseOutLand || c is EndLease) Leasing(ctx, vm, c);
            else if (kind == ConfirmKind.Hold) Receive(ctx, vm, c, cash, repaid); else Spend(ctx, vm, cash, taken, after, newest);
            vm.Warning = s.Has("confirm." + name + ".warn") ? s.Get("confirm." + name + (c is BuyParcel && Sim.Core.Town.LandValueRules.On(sim) ? ".warnTownship" : ".warn"), "quick", ctx.Fmt.Percent(sim.Config.Game.Land.QuickSaleFactor)) : kind == ConfirmKind.Hold ? s.Get("confirm.cantUndo") : null;
            if (c is ScheduleFestival fw) vm.Warning = FestivalWarning(ctx, after, fw);
            if (c is CommissionWorks cw) { if (Sim.Core.Town.LaneRules.OfWork(sim, cw.WorkId) >= 0) LaneSheet.Guard(ctx, vm, after); else if (Sim.Core.Town.UtilityRules.OfWork(sim, cw.WorkId) >= 0) UtilitySheet.Guard(ctx, vm, after, cw.WorkId); else { CivicSheet.Guard(ctx, vm, cw.WorkId); if (Sim.Core.Town.ConstructionRules.On(sim) && Sim.Core.Town.ConstructionRules.Def(sim).KindOfWork(cw.WorkId) != null) vm.Warning = BuildingSheet.Timing(ctx, after, false) + " " + vm.Warning; } }
            if (c is FoundBusiness kiln && kiln.Pitch < 0 && sim.Config.Game.Businesses.IndexOfType(kiln.TypeId) >= 0 && sim.Config.Game.Businesses.Types[sim.Config.Game.Businesses.IndexOfType(kiln.TypeId)].IsKiln)
                vm.Warning = KilnSheet.Smoke(ctx, sim.Config.Game.Businesses.Types[sim.Config.Game.Businesses.IndexOfType(kiln.TypeId)], kiln.TileId); // US-243: the smoke's footprint, counted
            if (c is BuildFamilyHouse bf && Sim.Core.Town.ConstructionRules.On(sim)) vm.Warning = BuildingSheet.Timing(ctx, after, true) + BuildingSheet.KilnSaving(ctx, Sim.Core.Town.ConstructionRules.Quote(sim, "family_house", Sim.Core.Town.BuildProject.Yours, 1)); // spec 023 §3.7: the budget guard replaces the generic line for a civic building
            if (c is UpgradeHouse uh) vm.Warning = UpgradeSheet.Warning(ctx, sim, after, uh); // US-152: when it is ready and what the rent does
            string verb = s.Get("confirm." + name + ".verb");
            if (kind == ConfirmKind.Hold)
            {
                vm.SafeLabel = s.Has("confirm." + name + ".keep") ? s.Get("confirm." + name + ".keep") : s.Get("confirm.keep");
                vm.GoLabel = s.Get("confirm.hold", "verb", verb.ToLowerInvariant());
            }
            else
            {
                vm.SafeLabel = s.Has("confirm." + name + ".keep") ? s.Get("confirm." + name + ".keep") : s.Get("confirm.notNow");
                vm.GoLabel = cash < 0 ? s.Get("confirm.verbFor", "verb", verb, "amount", ctx.Fmt.Coins(-cash)) : verb;
            }
            if (c is CommissionWorks bw && Sim.Core.Town.ConstructionRules.On(sim) && Sim.Core.Town.ConstructionRules.Def(sim).KindOfWork(bw.WorkId) != null) vm.GoLabel = s.Get("lane.cardGo", "amount", ctx.Fmt.Coins(sim.State.Treasury.Cash - after.State.Treasury.Cash));
            if (c is CommissionWorks uw && Sim.Core.Town.UtilityRules.OfWork(sim, uw.WorkId) >= 0)
            {
                long paid = sim.State.Treasury.Cash - after.State.Treasury.Cash + uw.Gift;
                vm.GoLabel = uw.Gift > 0 ? s.Get("utility.cardGoGift", "gift", ctx.Fmt.Coins(uw.Gift)) : s.Get("lane.cardGo", "amount", ctx.Fmt.Coins(paid));
            }
            if (c is CommissionWorks lw && Sim.Core.Town.LaneRules.OfWork(sim, lw.WorkId) >= 0) vm.GoLabel = s.Get("lane.cardGo", "amount", ctx.Fmt.Coins(sim.State.Treasury.Cash - after.State.Treasury.Cash));
            return vm;
        }

        /// <summary>An insured fire's repair (spec 027 §3.6): the bill, what the policy pays back, what you pay, the cash after.</summary>
        private static void InsuredRepair(ViewContext ctx, ConfirmCardVm vm, Simulation sim, Simulation after, Sim.Core.Town.PendingEvent repair, int choice)
        {
            var s = ctx.Strings;
            long bill = Sim.Core.Town.EventRules.ChoiceCost(sim, repair.Def, choice, repair), back = Sim.Core.Town.InsuranceRules.RepairPays(sim, repair, choice);
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.insuredBill"), ctx.Fmt.Coins(bill)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.insuredBack"), ctx.Fmt.Coins(back)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.insuredYouPay"), ctx.Fmt.Coins(System.Math.Max(0, sim.State.Ledger.Cash - after.State.Ledger.Cash))));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.cashAfter"), ctx.Fmt.Coins(after.State.Ledger.Cash), total: true));
        }

        /// <summary>What you pay and what is borrowed: the cost, the loan, what you pay now, the first payment, the cash after.</summary>
        private static void Spend(ViewContext ctx, ConfirmCardVm vm, long cash, long taken, Simulation after, Loan newest)
        {
            var s = ctx.Strings;
            long pay = System.Math.Max(0, -cash);
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.cost"), ctx.Fmt.Coins(pay + taken)));
            if (taken > 0)
            {
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.loan"), ctx.Fmt.Coins(taken)));
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.payNow"), ctx.Fmt.Coins(pay)));
                if (newest != null)
                {
                    long first = CreditRules.NextPayment(after, newest, out _, out _);
                    if (first > 0) vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.firstPayment"), ctx.Fmt.Coins(first)));
                }
            }
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.cashAfter"), ctx.Fmt.Coins(after.State.Ledger.Cash), total: true));
        }

        /// <summary>A slow sale's card (spec 024 §3.4): the land value now, the wait, what comes then; or the listing taken off. A tap, and good news: nothing is lost either way.</summary>
        private static void Listing(ViewContext ctx, ConfirmCardVm vm, Command c)
        {
            var s = ctx.Strings;
            vm.Reassure = true;
            if (c is ListParcel lp)
            {
                Parcel parcel = ctx.Sim.State.Land.Parcels[ctx.Sim.Config.Game.Map.IndexOfParcel(lp.ParcelId)];
                long value = LandRules.ParcelPrice(ctx.Sim, parcel);
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.ListParcel.value"), ctx.Fmt.Coins(value)));
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.ListParcel.wait"), LandValueViews.Days(ctx, Sim.Core.Town.LandValueRules.Def(ctx.Sim).SlowSaleSeconds)));
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.ListParcel.receive"), ctx.Fmt.Coins(value), total: true));
            }
            else
            {
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.CancelSale.back"), s.Get("confirm.CancelSale.backValue")));
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.CancelSale.cost"), s.Get("confirm.CancelSale.costValue"), total: true));
            }
        }

        /// <summary>Offering a parcel to builders (spec 024 §3.3): its land value, the families a builder waits for, what you receive; nothing is paid by you and it can be taken back.</summary>
        /// <summary>Declining a petition (spec 026 §3.2): approval falls and the families don't raise it again for a while.</summary>
        private static void Declining(ViewContext ctx, ConfirmCardVm vm)
        {
            var s = ctx.Strings;
            Sim.Core.Definitions.PetitionDef d = Sim.Core.Town.PetitionRules.Def(ctx.Sim);
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.AnswerPetition.approval"), TownLifeSheet.Signed(ctx, d.Declined)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.AnswerPetition.again"), s.Get("confirm.AnswerPetition.againValue", "n", ctx.Fmt.Integer(d.DeclinedQuietCloses)), total: true));
        }

        /// <summary>Holding a festival (spec 026 §3.1): where, which day, the cost, who pays, what is left, and whether the town would still pay its bills.</summary>
        private static void Festival(ViewContext ctx, ConfirmCardVm vm, Simulation sim, Simulation after, ScheduleFestival f)
        {
            var s = ctx.Strings;
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.ScheduleFestival.where"), FestivalSheet.VenueName(ctx, f.Venue)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.ScheduleFestival.day"), FestivalSheet.DayName(ctx, f.DaysAhead)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.ScheduleFestival.cost"), ctx.Fmt.Coins(Sim.Core.Town.TownLifeRules.State(after).FestivalCost)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.ScheduleFestival.paidBy"), s.Get(f.Payer == Sim.Core.Definitions.FestivalDef.Town ? "festival.theTown" : "festival.youGift")));
            if (f.Payer == Sim.Core.Definitions.FestivalDef.Town)
            {
                long shortBy = Sim.Core.Town.TreasuryRules.Forecast(after).Short;
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.ScheduleFestival.guard"), shortBy > 0 ? s.Get("confirm.ScheduleFestival.guardShort", "short", ctx.Fmt.Coins(shortBy)) : s.Get("confirm.ScheduleFestival.guardOk")));
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.ScheduleFestival.treasuryAfter"), ctx.Fmt.Coins(after.State.Treasury.Cash), total: true));
            }
            else vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.cashAfter"), ctx.Fmt.Coins(after.State.Ledger.Cash), total: true));
        }

        /// <summary>The line under a festival card: whether the town would still pay its bills (or that a gift is outside farm profit), and that it can't be undone.</summary>
        private static string FestivalWarning(ViewContext ctx, Simulation after, ScheduleFestival f)
        {
            var s = ctx.Strings;
            string first = f.Payer == Sim.Core.Definitions.FestivalDef.Town ? null : s.Get("confirm.ScheduleFestival.giftNote");
            return first == null ? s.Get("confirm.ScheduleFestival.warn") : first + " " + s.Get("confirm.ScheduleFestival.warn");
        }

        private static void Zoning(ViewContext ctx, ConfirmCardVm vm, ZoneParcel c)
        {
            var s = ctx.Strings;
            vm.Reassure = true;
            int id = ctx.Sim.Config.Game.Map.IndexOfParcel(c.ParcelId);
            long value = LandRules.ParcelPrice(ctx.Sim, ctx.Sim.State.Land.Parcels[id]);
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.ZoneParcel.value"), ctx.Fmt.Coins(value)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.ZoneParcel.when"), s.Plural("confirm.ZoneParcel.families", Sim.Core.Town.ZoningRules.Need(ctx.Sim, id))));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.ZoneParcel.receive"), ctx.Fmt.Coins(value), total: true));
        }

        /// <summary>Leasing a parcel out (spec 024 §3.6): its land value, the rent, when the first rent comes; or ending it at the close. Nothing is paid by you.</summary>
        private static void Leasing(ViewContext ctx, ConfirmCardVm vm, Command c)
        {
            var s = ctx.Strings;
            Simulation sim = ctx.Sim;
            vm.Reassure = c is LeaseOutLand;
            string left = ctx.Fmt.Duration(System.Math.Max(0, Sim.Core.Town.LandLeaseRules.NextClose(sim) - ctx.Now));
            if (c is LeaseOutLand lo)
            {
                Parcel parcel = sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(lo.ParcelId)];
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.LeaseOutLand.value"), ctx.Fmt.Coins(LandRules.ParcelPrice(sim, parcel))));
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.LeaseOutLand.rent"), ctx.Fmt.Coins(Sim.Core.Town.LandLeaseRules.Rent(sim, parcel))));
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.LeaseOutLand.first"), s.Get("confirm.LeaseOutLand.firstValue", "left", left), total: true));
            }
            else
            {
                Parcel parcel = sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(((EndLease)c).ParcelId)];
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.EndLease.paid"), s.Get("confirm.EndLease.paidValue", "rent", ctx.Fmt.Coins(Sim.Core.Town.LandLeaseRules.Rent(sim, parcel)))));
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.EndLease.leaves"), s.Get("confirm.EndLease.leavesValue", "left", left)));
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.EndLease.after"), s.Get("confirm.EndLease.afterValue"), total: true));
            }
        }

        /// <summary>The petition for the road (spec 023 §3.3): your cash now, the share, any loan, the days it takes, your cash after; the road is not yours and the share is not repaid.</summary>
        private static void Road(ViewContext ctx, ConfirmCardVm vm, long cash, long taken, Simulation after, Loan newest, string name)
        {
            var s = ctx.Strings;
            long pay = System.Math.Max(0, -cash);
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm." + name + ".now"), ctx.Fmt.Coins(after.State.Ledger.Cash + pay)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm." + name + ".share"), "− " + ctx.Fmt.Coins(pay + taken)));
            if (taken > 0)
            {
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.loan"), ctx.Fmt.Coins(taken)));
                if (newest != null)
                {
                    long first = CreditRules.NextPayment(after, newest, out _, out _);
                    if (first > 0) vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.firstPayment"), ctx.Fmt.Coins(first)));
                }
            }
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm." + name + ".days"), s.Plural("road.daysValue", Days(after, name))));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.cashAfter"), ctx.Fmt.Coins(after.State.Ledger.Cash), total: true));
        }

        /// <summary>Days the district or the builders take for what a card starts: the road's, or the clinic's and the quarters' hours rounded up to days.</summary>
        private static int Days(Simulation after, string name)
        {
            if (name == "BuildClinic") return (Sim.Core.Town.ClinicRules.Def(after).BuildHours + 23) / 24;
            if (name == "BuildDoctorQuarters") return (Sim.Core.Town.ClinicRules.QuartersDef(after).BuildHours + 23) / 24;
            return Sim.Core.Town.RoadRules.Def(after).Days;
        }

        /// <summary>A gift to the town (spec 021 §3.8): what you give, the town's balance after, your cash after.</summary>
        private static void Gift(ViewContext ctx, ConfirmCardVm vm, GiveToTreasury gift, Simulation after)
        {
            var s = ctx.Strings;
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.GiveToTreasury.gift"), ctx.Fmt.Coins(gift.Amount)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.GiveToTreasury.town"), ctx.Fmt.Coins(after.State.Treasury.Cash)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.cashAfter"), ctx.Fmt.Coins(after.State.Ledger.Cash), total: true));
        }

        /// <summary>Capital works (spec 021 §3.7): the town pays a builder from its own money; yours isn't touched.</summary>
        private static void Works(ViewContext ctx, ConfirmCardVm vm, Simulation sim, Simulation after, CommissionWorks works)
        {
            var s = ctx.Strings;
            int lane = Sim.Core.Town.LaneRules.OfWork(sim, works.WorkId);
            if (lane >= 0) { LaneSheet.Card(ctx, vm, after, lane); return; }
            int utility = Sim.Core.Town.UtilityRules.OfWork(sim, works.WorkId);
            if (utility >= 0) { UtilitySheet.Card(ctx, vm, sim, after, works, utility); return; } // spec 025 §3.1
            int civic = Sim.Core.Town.CivicRules.OfWork(sim, works.WorkId);
            Sim.Core.Definitions.BuildKindDef made = civic >= 0 && Sim.Core.Town.ConstructionRules.On(sim) ? Sim.Core.Town.ConstructionRules.Def(sim).KindOfWork(works.WorkId) : null;
            if (made != null) // from the Township a civic building takes materials and masons (spec 025 §3.2)
            {
                string cid = CivicSheet.Ids[civic];
                vm.Title = s.Get("civic." + cid + ".cardTitle");
                vm.Subtitle = s.Get("civic.cardSub");
                BuildingSheet.CardRows(ctx, vm, Sim.Core.Town.ConstructionRules.Quote(sim, made.Id, Sim.Core.Town.BuildProject.Town, 1), "civic.cardNow", "civic.cardAfter", sim.State.Treasury.Cash, after.State.Treasury.Cash);
                return;
            }
            if (civic >= 0) // a civic building (spec 023 §3.7): the town's cash now and after, and the budget guard
            {
                string id = CivicSheet.Ids[civic];
                vm.Title = s.Get("civic." + id + ".cardTitle");
                vm.Subtitle = s.Get("civic.cardSub");
                vm.Rows.Add(new ConfirmRowVm(s.Get("civic.cardNow"), ctx.Fmt.Coins(sim.State.Treasury.Cash)));
                vm.Rows.Add(new ConfirmRowVm(s.Get("civic." + id + ".cardRow"), "− " + ctx.Fmt.Coins(sim.State.Treasury.Cash - after.State.Treasury.Cash)));
                vm.Rows.Add(new ConfirmRowVm(s.Get("civic.cardAfter"), ctx.Fmt.Coins(after.State.Treasury.Cash), total: true));
                return;
            }
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.CommissionWorks.cost"), ctx.Fmt.Coins(sim.State.Treasury.Cash - after.State.Treasury.Cash)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.CommissionWorks.town"), ctx.Fmt.Coins(after.State.Treasury.Cash), total: true));
        }

        /// <summary>Enacting a policy (spec 022 §3.6): what it costs the town a season, when it is paid and starts, and the town's bill at the close before and after.</summary>
        private static void Enacting(ViewContext ctx, ConfirmCardVm vm, Simulation sim, Simulation after, EnactPolicy c)
        {
            var s = ctx.Strings;
            var p = c.Policy;
            long cost = Sim.Core.Town.PolicyRules.Cost(sim, p);
            int left = Sim.Core.Town.PolicyRules.SettingUp(after, p);
            vm.Subtitle = PolicySheet.Name(ctx, (int)p);
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.EnactPolicy.cost"), PolicySheet.CostText(ctx, p, cost)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.EnactPolicy.paid"), FieldSheets.SeasonName(ctx, sim.Period)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.EnactPolicy.starts"), FieldSheets.SeasonName(ctx, sim.Period + left)));
            Sim.Core.Town.TreasuryRules.CloseForecast before = Sim.Core.Town.TreasuryRules.Forecast(sim), now = Sim.Core.Town.TreasuryRules.Forecast(after);
            long raise = p == Sim.Core.Town.PolicyId.MinWage ? Sim.Core.Town.PolicyRules.ClerkRaise(sim) : 0;
            if (raise > 0) vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.EnactPolicy.clerk"), "+" + ctx.Fmt.Coins(raise))); // the town's own clerk is paid the minimum too
            long due = now.Due + raise;
            long income = Sim.Core.Town.TreasuryRules.ExpectedIncome(sim), balance = sim.State.Treasury.Cash;
            string status = TreasurySheet.CloseStatus(ctx, income, due, balance, out Tone tone);
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.EnactPolicy.bill"), ctx.Fmt.Coins(before.Due) + " → " + ctx.Fmt.Coins(due), total: true));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.EnactPolicy.status"), status));
            vm.Warning = tone == Tone.Warn ? s.Get("confirm.EnactPolicy.short") : null;
            vm.Reassure = false;
        }

        /// <summary>Repealing a policy (spec 022 §3.6): what stops, and anything that stays behind (the subsidy's dip, wages and rents until each renewal).</summary>
        private static void Repealing(ViewContext ctx, ConfirmCardVm vm, Simulation sim, RepealPolicy c)
        {
            var s = ctx.Strings;
            vm.Subtitle = PolicySheet.Name(ctx, (int)c.Policy);
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.RepealPolicy.stops"), s.Get("confirm.RepealPolicy.stopsValue")));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.RepealPolicy.saves"), PolicySheet.CostText(ctx, c.Policy, Sim.Core.Town.PolicyRules.Cost(sim, c.Policy))));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.RepealPolicy.after"), s.Get("policy." + PolicySheet.Key((int)c.Policy) + ".afterRepeal"), total: true));
        }

        /// <summary>The standing consent (Sprint 18 review): your savings, and what the next close would take from them under it.</summary>
        private static void Standing(ViewContext ctx, ConfirmCardVm vm)
        {
            var s = ctx.Strings;
            long deposit = SavingsRules.Balance(ctx.Sim);
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.SetSavingsCover.savings"), ctx.Fmt.Coins(deposit)));
            long next = System.Math.Min(deposit, SavingsRules.Shortfall(ctx.Sim, out _));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.SetSavingsCover.next"), ctx.Fmt.Coins(next), total: true));
        }

        /// <summary>What you give up and get: for land, its price at market, the quick sale, what goes with it, the loan repaid first, the cash.</summary>
        private static void Receive(ViewContext ctx, ConfirmCardVm vm, Command c, long cash, long repaid)
        {
            var s = ctx.Strings;
            if (c is SellParcel sp)
            {
                GameDefs defs = ctx.Sim.Config.Game;
                Parcel parcel = ctx.Sim.State.Land.Parcels[defs.Map.IndexOfParcel(sp.ParcelId)];
                long market = LandRules.ParcelPrice(ctx.Sim, parcel);
                long quick = Money.RoundToCoin(Money.MulRate(market, defs.Land.QuickSaleFactor));
                long extra = cash + repaid - quick; // channels, drip, a borewell and orchards go with the land, at book value
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.market"), ctx.Fmt.Coins(market)));
                vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.quick", "percent", ctx.Fmt.Percent(defs.Land.QuickSaleFactor)), ctx.Fmt.Coins(quick)));
                if (extra > 0) vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.goesWith"), "+" + ctx.Fmt.Coins(extra)));
            }
            if (repaid > 0) vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.loanFirst"), "−" + ctx.Fmt.Coins(repaid)));
            vm.Rows.Add(new ConfirmRowVm(s.Get(cash >= 0 ? "confirm.toCash" : "confirm.fromCash"), ctx.Fmt.Coins(System.Math.Abs(cash)), total: true));
        }

        /// <summary>A parcel's soil and acres; empty for commands with no named subject.</summary>
        private static string Subject(ViewContext ctx, Command c)
        {
            GameDefs defs = ctx.Sim.Config.Game;
            if (c is DismissHand dh)
            {
                Sim.Core.People.Hand hand = ctx.Sim.State.People?.Hand(dh.HandId);
                return hand == null ? null : Sim.View.Views.PeopleWords.Name(ctx, hand.Name);
            }
            if (c is AnswerPetition pa)
            {
                Sim.Core.Town.TownPetition pet = Sim.Core.Town.TownLifeRules.State(ctx.Sim)?.Petitions.Find(x => x.Id == pa.PetitionId);
                return pet == null ? null : TownLifeSheet.Text(ctx, pet);
            }
            if (c is FitSolarPanel fs) return ctx.Strings.Get(fs.Source == 1 ? "water.pump" : "water.borewell"); // which source the panel is for
            string id = c is BuyParcel b ? b.ParcelId : c is SellParcel sp ? sp.ParcelId : c is ListParcel lp ? lp.ParcelId : c is CancelSale cs ? cs.ParcelId : c is ZoneParcel zp2 ? zp2.ParcelId : c is LeaseOutLand lo ? lo.ParcelId : c is EndLease el ? el.ParcelId : null;
            int p = id == null ? -1 : defs.Map.IndexOfParcel(id);
            if (p < 0) return null;
            ParcelDef d = defs.Map.Parcels[p];
            string soil = ctx.Strings.Get(defs.Soils[ctx.Sim.State.Land.Tiles[d.Tiles[0]].Soil].NameKey);
            return ctx.Strings.Get("lands.parcel", "soil", soil, "acres", ctx.Strings.Plural("land.acres", d.Tiles.Count));
        }

        /// <summary>What you owe on your own loans (a business's are on its books): principal and interest.</summary>
        private static long Owed(Simulation sim)
        {
            long owed = 0;
            if (sim.State.Credit == null) return 0;
            foreach (Loan l in sim.State.Credit.Loans)
                if (l.IsOpen && l.BusinessId == 0) owed += l.Principal + l.ArrearsInterest + l.InterestAccrued;
            return owed;
        }

        /// <summary>Principal of loans the command opened (ids the copy has and the state hasn't), and the newest.</summary>
        private static long NewLoans(Simulation before, Simulation after, out Loan newest)
        {
            newest = null;
            long taken = 0;
            if (after.State.Credit == null) return 0;
            foreach (Loan l in after.State.Credit.Loans)
            {
                if (l.BusinessId != 0 || before.State.Credit.Find(l.Id) != null) continue;
                taken += l.Principal;
                newest = l;
            }
            return taken;
        }
    }
}
