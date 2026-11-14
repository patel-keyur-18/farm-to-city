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
            Add(ConfirmKind.Tap, typeof(BuyParcel), typeof(BuildFamilyHouse), typeof(BuildHandHut), typeof(BuildSchool), typeof(BuildShed), typeof(FoundBusiness),
                typeof(BuyMachine), typeof(LeaseMachine), typeof(HireMachine), typeof(InstallPump), typeof(DrillBorewell), typeof(BuildChannel), typeof(LayDrip),
                typeof(BuyGodown), typeof(RentWarehouse), typeof(PlantOrchard), typeof(PlantTree), typeof(BuyAnimal), typeof(TakeLoan), typeof(RestructureLoan),
                typeof(OfferContract), typeof(OfferTownsperson), typeof(RenewContract), typeof(SendOnCourse), typeof(StartAdultClass), typeof(SetSavingsCover), typeof(GiveToTreasury), typeof(CommissionWorks));
            Add(ConfirmKind.Hold, typeof(SellParcel), typeof(SellMachine), typeof(SellAnimal), typeof(SellOrchardOnTrees), typeof(CloseBusiness), typeof(ClearField),
                typeof(ClearOrchard), typeof(RemoveTree), typeof(DismissHand), typeof(LetWorkerGo));
            Add(ConfirmKind.Everyday, typeof(Sow), typeof(Harvest), typeof(Spray), typeof(PickFruit), typeof(PickGarden), typeof(SowGarden), typeof(PickOrchard),
                typeof(SellToTrader), typeof(DeliverOrder), typeof(GoToMandi), typeof(Buy), typeof(BuyMedicine), typeof(HireLabourers), typeof(Offer), typeof(Withdraw),
                typeof(SetBorewellAcres), typeof(SetBusiness), typeof(SetBuyGreenFodder), typeof(SetDietPlan), typeof(SetEatFromStore), typeof(SetLabourSplit),
                typeof(SetLongDays), typeof(SetMaintenance), typeof(SetMix), typeof(SetPumpAcres), typeof(SetShop), typeof(SetStallGoods), typeof(SetStandingOrder),
                typeof(SetWorkHours), typeof(ClearStandingOrder), typeof(PutCash), typeof(TakeCash), typeof(CheckReference), typeof(Interview), typeof(PostJob),
                typeof(RepayLoan), typeof(DepositSavings), typeof(WithdrawSavings), typeof(CoverFromSavings), typeof(TakeBackSavingsCover), typeof(LetGodown), typeof(RunBusinessYourself), typeof(AnswerHrEvent), typeof(AnswerFamilyRequest), typeof(RenewLease), typeof(SeeChapter),
                typeof(CompleteStep), typeof(SkipFirstSession), typeof(FindCodexCards), typeof(ReadCodexCard), typeof(HearAdvisors));
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
            Simulation sim = ctx.Sim;
            CommandResult result = sim.Preview(c, out Simulation after);
            if (!result.Accepted) return null;
            var s = ctx.Strings;
            string name = c.GetType().Name;
            long cash = after.State.Ledger.Cash - sim.State.Ledger.Cash;
            if (c is RentWarehouse && cash == 0) return null; // stopping or cutting the rent spends nothing: no card (ADR 0103)
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
            else if (c is CommissionWorks) Works(ctx, vm, sim, after);
            else if (kind == ConfirmKind.Hold) Receive(ctx, vm, c, cash, repaid); else Spend(ctx, vm, cash, taken, after, newest);
            vm.Warning = s.Has("confirm." + name + ".warn") ? s.Get("confirm." + name + ".warn", "quick", ctx.Fmt.Percent(sim.Config.Game.Land.QuickSaleFactor)) : kind == ConfirmKind.Hold ? s.Get("confirm.cantUndo") : null;
            string verb = s.Get("confirm." + name + ".verb");
            if (kind == ConfirmKind.Hold)
            {
                vm.SafeLabel = s.Has("confirm." + name + ".keep") ? s.Get("confirm." + name + ".keep") : s.Get("confirm.keep");
                vm.GoLabel = s.Get("confirm.hold", "verb", verb.ToLowerInvariant());
            }
            else
            {
                vm.SafeLabel = s.Get("confirm.notNow");
                vm.GoLabel = cash < 0 ? s.Get("confirm.verbFor", "verb", verb, "amount", ctx.Fmt.Coins(-cash)) : verb;
            }
            return vm;
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

        /// <summary>A gift to the town (spec 021 §3.8): what you give, the town's balance after, your cash after.</summary>
        private static void Gift(ViewContext ctx, ConfirmCardVm vm, GiveToTreasury gift, Simulation after)
        {
            var s = ctx.Strings;
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.GiveToTreasury.gift"), ctx.Fmt.Coins(gift.Amount)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.GiveToTreasury.town"), ctx.Fmt.Coins(after.State.Treasury.Cash)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.cashAfter"), ctx.Fmt.Coins(after.State.Ledger.Cash), total: true));
        }

        /// <summary>Capital works (spec 021 §3.7): the town pays a builder from its own money; yours isn't touched.</summary>
        private static void Works(ViewContext ctx, ConfirmCardVm vm, Simulation sim, Simulation after)
        {
            var s = ctx.Strings;
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.CommissionWorks.cost"), ctx.Fmt.Coins(sim.State.Treasury.Cash - after.State.Treasury.Cash)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("confirm.CommissionWorks.town"), ctx.Fmt.Coins(after.State.Treasury.Cash), total: true));
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
            string id = c is BuyParcel b ? b.ParcelId : c is SellParcel sp ? sp.ParcelId : null;
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
