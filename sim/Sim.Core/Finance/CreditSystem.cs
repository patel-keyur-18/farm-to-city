using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.Finance
{
    /// <summary>What the season-close settlement did to the loans (spec 005 §3.4 step 4), for the score and the report.</summary>
    internal sealed class SettlementStats
    {
        public bool AnyDue, AnyMissed;
        public long InterestPaid, PrincipalPaid, Unpaid;
    }

    /// <summary>
    /// Loans, season-close settlement, defaults and foreclosure (spec 005 §3.6–3.9, ADR 0038). Called by the finance
    /// system's passes and by the credit commands.
    /// </summary>
    internal static class CreditSystem
    {
        /// <summary>The books a loan's entries go to: a business's for its business loan, else yours.</summary>
        internal static Ledger LedgerOf(Simulation sim, Loan loan)
        {
            return loan.BusinessId == 0 ? sim.State.Ledger : sim.State.Businesses.Find(loan.BusinessId).Ledger;
        }

        // ---- taking a loan ----

        /// <summary>
        /// Creates the loan and pays the amount into Cash: yours, or the business's for a business loan. The caller has checked
        /// <see cref="CreditRules.Check"/> (or the business loan's own rules).
        /// </summary>
        internal static Loan Create(Simulation sim, int lender, long amount, int term, long businessId = 0)
        {
            CreditDefs defs = sim.Config.Game.Credit;
            CreditState credit = sim.State.Credit;
            LenderDef def = defs.Lenders[lender];
            long t = sim.State.Clock.SimTime;
            var loan = new Loan
            {
                Id = credit.NextLoanId++, Lender = lender, OriginalPrincipal = amount, Principal = amount,
                Rate = CreditRules.Rate(def, credit.Score), PenaltyRate = def.PenaltyRate, Schedule = def.Schedule, Term = term,
                TakenAt = t, Remaining = term, InstalmentBase = amount / term, FirstClosePartial = t % sim.Clock.SecondsPerSeason != 0,
                SegmentStart = t, State = LoanState.Active, BusinessId = businessId,
            };
            LedgerOf(sim, loan).Post(t, sim.Period, "ledger.loanTaken", "loan:" + loan.Id,
                new Posting(Account.Cash, amount), new Posting(def.LoanAccount, -amount));
            credit.Loans.Add(loan);
            credit.LoanSeenThisSeason = true;
            sim.Emit(new LoanTaken(t, loan.Id, def.Id, amount, loan.Rate, term));
            return loan;
        }

        // ---- interest ----

        /// <summary>Posts the interest and penalty accrued since the segment start (Dr 5400 / 5410, Cr 2300) and starts a new segment.</summary>
        internal static void Accrue(Simulation sim, Loan loan, long period)
        {
            long now = sim.State.Clock.SimTime;
            if (!loan.IsOpen) return;
            CreditRules.AccruedSince(sim, loan, now, out long interest, out long penalty);
            loan.SegmentStart = now;
            if (interest + penalty == 0) return;
            var postings = new List<Posting>();
            if (interest > 0) postings.Add(new Posting(Account.Interest, interest));
            if (penalty > 0) postings.Add(new Posting(Account.PenaltyInterest, penalty));
            postings.Add(new Posting(Account.InterestPayable, -(interest + penalty)));
            LedgerOf(sim, loan).Post(now, period, "ledger.interest", "loan:" + loan.Id, postings.ToArray());
            loan.InterestAccrued += interest + penalty;
        }

        internal static void AccrueAll(Simulation sim, long period)
        {
            foreach (Loan l in sim.State.Credit.Loans) Accrue(sim, l, period);
        }

        // ---- paying ----

        /// <summary>
        /// Pays up to <paramref name="amount"/> of a loan from Cash: interest (arrears first), then principal arrears, then
        /// scheduled principal (spec 005 §3.6). The caller has accrued interest to now. Returns what was paid.
        /// </summary>
        internal static long Pay(Simulation sim, Loan loan, long amount, string key, out long interest, out long arrears, out long principal)
        {
            Ledger ledger = LedgerOf(sim, loan);
            long take = System.Math.Min(amount, ledger.Cash);
            long left = take;
            interest = arrears = principal = 0;
            long fromArrearsInterest = System.Math.Min(left, loan.ArrearsInterest);
            loan.ArrearsInterest -= fromArrearsInterest;
            left -= fromArrearsInterest;
            long fromAccrued = System.Math.Min(left, loan.InterestAccrued);
            loan.InterestAccrued -= fromAccrued;
            left -= fromAccrued;
            interest = fromArrearsInterest + fromAccrued;
            arrears = System.Math.Min(left, loan.ArrearsPrincipal);
            loan.ArrearsPrincipal -= arrears;
            left -= arrears;
            long scheduled = System.Math.Min(left, loan.Principal - arrears - loan.ArrearsPrincipal);
            principal = arrears + scheduled;
            loan.Principal -= principal;
            take = interest + principal;
            if (take > 0)
            {
                var postings = new List<Posting>();
                if (interest > 0) postings.Add(new Posting(Account.InterestPayable, interest));
                if (principal > 0) postings.Add(new Posting(sim.Config.Game.Credit.Lenders[loan.Lender].LoanAccount, principal));
                postings.Add(new Posting(Account.Cash, -take));
                ledger.Post(sim.State.Clock.SimTime, sim.Period, key, "loan:" + loan.Id, postings.ToArray());
                sim.Emit(new LoanRepayment(sim.State.Clock.SimTime, loan.Id, interest, arrears, principal - arrears));
            }
            if (!loan.HasArrears) loan.ArrearsCloses = 0;
            if (loan.Principal == 0 && loan.ArrearsInterest + loan.InterestAccrued == 0) loan.State = LoanState.Repaid;
            return take;
        }

        // ---- the season close (spec 005 §3.4 steps 2, 4, 8) ----

        /// <summary>Step 4: loan payments due, in loan id order, taking whatever cash there is; a shortfall becomes arrears.</summary>
        internal static SettlementStats Settle(Simulation sim)
        {
            CreditState credit = sim.State.Credit;
            var stats = new SettlementStats();
            long t = sim.State.Clock.SimTime;
            foreach (Loan loan in credit.Loans)
            {
                if (loan.State != LoanState.Active) continue; // a defaulted loan is on the foreclosure clock, not the schedule
                long scheduled = 0;
                if (loan.FirstClosePartial) loan.FirstClosePartial = false; // interest only
                else if (loan.Remaining > 0)
                {
                    loan.Remaining--;
                    long outstanding = loan.Principal - loan.ArrearsPrincipal;
                    scheduled = loan.Remaining == 0 ? outstanding
                        : loan.Schedule == LoanSchedule.Bullet ? 0 : System.Math.Min(loan.InstalmentBase, outstanding);
                }
                long dueInterest = loan.ArrearsInterest + loan.InterestAccrued;
                long dueArrears = loan.ArrearsPrincipal;
                long due = dueInterest + dueArrears + scheduled;
                if (due == 0) { if (loan.Principal == 0) loan.State = LoanState.Repaid; continue; }
                stats.AnyDue = true;

                // Interest and arrears come first, then the instalment; the rest of the instalment becomes arrears.
                loan.ArrearsInterest = dueInterest;
                loan.InterestAccrued = 0;
                loan.ArrearsPrincipal += scheduled; // due now; what is paid comes off below
                if (loan.BusinessId != 0) Business.BusinessBooks.Draw(sim, sim.State.Businesses.Find(loan.BusinessId), due); // its overdraft first (spec 016 §3.10)
                long paid = Pay(sim, loan, due, "ledger.loanPayment", out long i, out long a, out long p);
                stats.InterestPaid += i;
                stats.PrincipalPaid += p;
                long unpaid = due - paid;
                if (unpaid > 0)
                {
                    stats.AnyMissed = true;
                    stats.Unpaid += unpaid;
                    loan.ArrearsCloses++;
                    sim.Emit(new LoanPaymentMissed(t, loan.Id, unpaid, loan.ArrearsCloses));
                }
                else loan.ArrearsCloses = 0;
            }
            return stats;
        }

        /// <summary>Step 8: the score for the season, then default detection and the final notice (spec 005 §3.7, §3.8; ADR 0038).</summary>
        internal static CreditSummary Outcomes(Simulation sim, SettlementStats stats)
        {
            CreditState credit = sim.State.Credit;
            CreditScoreDef rules = sim.Config.Game.Credit.Score;
            int before = credit.Score;

            if (stats.AnyMissed) ChangeScore(sim, -rules.MissedPenalty, "score.missed");
            else if (stats.AnyDue) ChangeScore(sim, rules.OnTimeGain, "score.onTime");
            else if (!credit.LoanSeenThisSeason && !credit.Loans.Exists(l => l.IsOpen && l.HasArrears)) ChangeScore(sim, rules.CleanSeasonGain, "score.clean");

            var defaulted = new List<long>();
            foreach (Loan loan in credit.Loans)
            {
                if (loan.State != LoanState.Active || loan.ArrearsCloses <= sim.Config.Game.Credit.Lenders[loan.Lender].GraceCloses) continue;
                loan.State = LoanState.Defaulted;
                defaulted.Add(loan.Id);
                sim.Emit(new LoanDefaulted(sim.State.Clock.SimTime, loan.Id));
                ChangeScore(sim, -rules.DefaultPenalty, "score.default");
            }
            if (defaulted.Count > 0) RaiseNotice(sim);

            credit.LoanSeenThisSeason = credit.Loans.Exists(l => l.IsOpen);
            long principal = 0;
            foreach (Loan l in credit.Loans) if (l.IsOpen) principal += l.Principal;
            return new CreditSummary
            {
                Score = credit.Score, ScoreChange = credit.Score - before, PrincipalOutstanding = principal,
                InterestPaid = stats.InterestPaid, PrincipalPaid = stats.PrincipalPaid, Missed = stats.Unpaid,
            };
        }

        internal static void ChangeScore(Simulation sim, int delta, string reason)
        {
            CreditState credit = sim.State.Credit;
            int old = credit.Score;
            credit.Score = sim.Config.Game.Credit.Score.Clamp(old + delta);
            credit.History.Add(new ScoreChange(sim.State.Clock.SimTime, credit.Score - old, credit.Score, reason));
            sim.Emit(new CreditScoreChanged(sim.State.Clock.SimTime, old, credit.Score, reason));
        }

        // ---- the final notice and foreclosure (ADR 0038) ----

        private static List<long> DefaultedIds(CreditState credit)
        {
            var ids = new List<long>();
            foreach (Loan l in credit.Loans) if (l.State == LoanState.Defaulted) ids.Add(l.Id);
            return ids;
        }

        private static void RaiseNotice(Simulation sim)
        {
            CreditState credit = sim.State.Credit;
            int hours = sim.Config.Game.Credit.Score.FinalNoticeLiveHours;
            if (!credit.PendingForeclosure)
            {
                credit.PendingForeclosure = true;
                credit.NoticeLiveSeconds = 0;
            }
            credit.NoticeAnnounced = !sim.State.Clock.IsAway;
            sim.Emit(new FinalNotice(sim.State.Clock.SimTime, DefaultedIds(credit), (int)(hours - credit.NoticeLiveSeconds / 3600)));
        }

        /// <summary>On return from an away advance, a notice raised while away is shown again (before AwayAdvanceCompleted).</summary>
        internal static void AnnounceAfterAway(Simulation sim)
        {
            CreditState credit = sim.State.Credit;
            if (credit == null || !credit.PendingForeclosure || credit.NoticeAnnounced) return;
            credit.NoticeAnnounced = true;
            int hours = sim.Config.Game.Credit.Score.FinalNoticeLiveHours;
            sim.Emit(new FinalNotice(sim.State.Clock.SimTime, DefaultedIds(credit), (int)(hours - credit.NoticeLiveSeconds / 3600)));
        }

        /// <summary>A defaulted loan whose arrears are paid is active again; with none left the notice is cancelled.</summary>
        internal static void CheckCure(Simulation sim)
        {
            CreditState credit = sim.State.Credit;
            if (credit == null || !credit.PendingForeclosure) return;
            foreach (Loan l in credit.Loans)
                if (l.State == LoanState.Defaulted && !l.HasArrears) { l.State = LoanState.Active; l.ArrearsCloses = 0; }
            if (credit.Loans.Exists(l => l.State == LoanState.Defaulted)) return;
            credit.PendingForeclosure = false;
            credit.NoticeLiveSeconds = 0;
        }

        /// <summary>Hourly: the notice clock runs on live hours only; at its end foreclosure runs unless cured.</summary>
        internal static void Hourly(Simulation sim)
        {
            CreditState credit = sim.State.Credit;
            if (credit == null || !credit.PendingForeclosure) return;
            CheckCure(sim);
            if (!credit.PendingForeclosure || sim.State.Clock.IsAway) return;
            credit.NoticeLiveSeconds += 3600;
            if (credit.NoticeLiveSeconds >= sim.Config.Game.Credit.Score.FinalNoticeLiveHours * 3600L) Foreclose(sim);
        }

        /// <summary>
        /// Foreclosure (spec 005 §3.9): bought parcels with everything on them, then stored produce at the Floor until the debt is
        /// covered; the bank is repaid first, then the others pro rata; the rest is written off; the score is set low.
        /// </summary>
        internal static void Foreclose(Simulation sim)
        {
            CreditState credit = sim.State.Credit;
            Ledger ledger = sim.State.Ledger;
            GameDefs defs = sim.Config.Game;
            long t = sim.State.Clock.SimTime;
            long period = sim.Period;

            // 0. A business on bought land, or whose own loan has defaulted, is closed (spec 016 §3.10): its stock and equipment sold,
            // its debts repaid, what's still owed becoming your guaranteed loan; what it hands back joins the pot.
            long pot = 0;
            if (sim.State.Businesses != null)
                foreach (Business.Business b in sim.State.Businesses.Businesses.ToArray())
                {
                    if (b.Status == Business.BusinessStatus.Closed) continue;
                    Loan own = b.LoanId == 0 ? null : credit.Find(b.LoanId);
                    bool bought = !sim.State.Land.Parcels[sim.State.Land.Tiles[b.TileId].ParcelId].Protected;
                    if (!bought && (own == null || own.State != LoanState.Defaulted)) continue;
                    long before = ledger.Cash;
                    Business.BusinessBooks.CloseDown(sim, b);
                    pot += ledger.Cash - before;
                }

            AccrueAll(sim, period);
            long debt = 0;
            foreach (Loan l in credit.Loans) if (l.IsOpen && l.BusinessId == 0) debt += l.Principal + l.ArrearsInterest + l.InterestAccrued;

            // 1. Bought parcels, in id order, with their crops and trees.
            var parcels = new List<string>();
            FarmSystem.Accrue(sim);
            foreach (Parcel parcel in sim.State.Land.Parcels)
            {
                if (!parcel.Owned || parcel.Protected) continue;
                foreach (int tile in defs.Map.Parcels[parcel.Id].Tiles)
                {
                    Crop crop = sim.State.Farm.CropOn(tile);
                    if (crop != null)
                    {
                        if (crop.State == CropState.Growing) sim.CompleteJob(sim.State.Jobs.Find(crop.JobId));
                        if (crop.PestJobId != 0) sim.CompleteJob(sim.State.Jobs.Find(crop.PestJobId));
                        sim.State.Farm.Crops.Remove(crop);
                        LandSystem.SetOccupied(sim, tile, false);
                    }
                }
                foreach (Tree tree in sim.State.Farm.Trees.ToArray())
                {
                    TreeSlot slot = sim.State.Land.Slots.Find(s => s.Id == tree.SlotId);
                    if (slot != null && sim.State.Land.Tiles[slot.TileId].ParcelId == parcel.Id) FarmSystem.Dispose(sim, tree);
                }
                pot += People.HouseRules.Seize(sim, parcel.Id, period); // a family house goes with its land, at book value (spec 014 §3.13)
                long value = Money.RoundToCoin(Money.MulRate(LandRules.ParcelPrice(sim, parcel), defs.Land.QuickSaleFactor));
                long basis = parcel.CostBasis, gain = value - basis;
                ledger.Post(t, period, "ledger.parcelSeized", "parcel:" + defs.Map.Parcels[parcel.Id].Id,
                    new Posting(Account.Cash, value), new Posting(Account.Land, -basis),
                    gain >= 0 ? new Posting(Account.GainOnAssetSales, -gain) : new Posting(Account.LossOnAssetSales, -gain));
                LandSystem.Release(sim, parcel);
                pot += value;
                parcels.Add(defs.Map.Parcels[parcel.Id].Id);
                sim.Emit(new ParcelSeized(t, defs.Map.Parcels[parcel.Id].Id, value));
            }
            FarmSystem.Refresh(sim);

            // 1b. Animals, in id order, at their fair price, only until the debt is covered (spec 012 §3.12). The shed stays (the yard).
            if (sim.State.Livestock != null)
                foreach (Livestock.Animal a in sim.State.Livestock.Animals.ToArray())
                {
                    if (debt <= pot) break;
                    long value = Livestock.LivestockRules.SellPrice(sim, a);
                    long gain = value - a.Cost;
                    ledger.Post(t, period, "ledger.animalSeized", "animal:" + a.Id, new Posting(Account.Cash, value), new Posting(Account.Livestock, -a.Cost),
                        gain >= 0 ? new Posting(Account.GainOnAssetSales, -gain) : new Posting(Account.LossOnAssetSales, -gain));
                    sim.State.Livestock.Animals.Remove(a);
                    pot += value;
                    sim.Emit(new AnimalSeized(t, a.Id, a.Name, value));
                }
            FarmSystem.Refresh(sim);

            // 2. Stored produce at the Floor, highest value first, only until the debt is covered.
            var produce = new List<KeyValuePair<string, long>>();
            if (debt > pot)
            {
                var goods = defs.Market.Goods;
                var order = new List<int>();
                for (int g = 0; g < goods.Count; g++)
                    if (goods[g].Kind == GoodKind.Produce && sim.State.Market.Stock[g] > 0) order.Add(g);
                order.Sort((a, b) =>
                {
                    int c = StockValue(sim, b).CompareTo(StockValue(sim, a));
                    return c != 0 ? c : a.CompareTo(b);
                });
                foreach (int g in order)
                {
                    long need = debt - pot;
                    if (need <= 0) break;
                    Fixed floor = MarketRules.Floor(sim, g);
                    long stock = sim.State.Market.Stock[g], lo = 1, hi = stock;
                    if (MarketRules.Value(sim, g, stock, floor) > need)
                        while (lo < hi)
                        {
                            long mid = lo + (hi - lo) / 2;
                            if (MarketRules.Value(sim, g, mid, floor) >= need) hi = mid; else lo = mid + 1;
                        }
                    long q = MarketRules.Value(sim, g, stock, floor) > need ? lo : stock;
                    long amount = MarketRules.Value(sim, g, q, floor);
                    Store.Take(sim, g, q);
                    if (amount > 0)
                        ledger.Post(t, period, "ledger.produceSeized", "good:" + goods[g].Id,
                            new Posting(Account.Cash, amount), new Posting(Store.SalesAccount(sim, g), -amount));
                    pot += amount;
                    produce.Add(new KeyValuePair<string, long>(goods[g].Id, q));
                }
            }

            // 3. Repay: the secured lender first, then the others in proportion to what each is owed.
            long raised = pot;
            // A standing business's loan is its own debt, on its own books: not foreclosed with yours.
            foreach (Loan l in credit.Loans)
                if (l.IsOpen && l.BusinessId == 0 && defs.Credit.Lenders[l.Lender].Secured) pot -= Pay(sim, l, pot, "ledger.foreclosurePayment", out _, out _, out _) ;
            var unsecured = credit.Loans.FindAll(l => l.IsOpen && l.BusinessId == 0 && !defs.Credit.Lenders[l.Lender].Secured);
            long totalOwed = 0;
            foreach (Loan l in unsecured) totalOwed += l.Principal + l.ArrearsInterest + l.InterestAccrued;
            if (totalOwed > 0 && pot > 0)
            {
                var shares = new long[unsecured.Count];
                long given = 0;
                for (int i = 0; i < unsecured.Count; i++)
                {
                    long owed = unsecured[i].Principal + unsecured[i].ArrearsInterest + unsecured[i].InterestAccrued;
                    shares[i] = pot >= totalOwed ? owed : (long)((System.Numerics.BigInteger)pot * owed / totalOwed);
                    given += shares[i];
                }
                long remainder = pot >= totalOwed ? 0 : pot - given;
                if (shares.Length > 0) shares[0] += remainder; // the remainder goes to the lowest loan id (Pay caps it at what is owed)
                for (int i = 0; i < unsecured.Count; i++) Pay(sim, unsecured[i], shares[i], "ledger.foreclosurePayment", out _, out _, out _);
            }

            // 4. Write off what is still owed; every loan is closed.
            long writtenOff = 0;
            foreach (Loan l in credit.Loans)
            {
                if (!l.IsOpen || l.BusinessId != 0) continue;
                long interest = l.ArrearsInterest + l.InterestAccrued, principal = l.Principal, total = interest + principal;
                if (total > 0)
                {
                    var postings = new List<Posting>();
                    if (interest > 0) postings.Add(new Posting(Account.InterestPayable, interest));
                    if (principal > 0) postings.Add(new Posting(defs.Credit.Lenders[l.Lender].LoanAccount, principal));
                    postings.Add(new Posting(Account.DebtWrittenOff, -total));
                    ledger.Post(t, period, "ledger.debtWrittenOff", "loan:" + l.Id, postings.ToArray());
                    writtenOff += total;
                }
                l.Principal = l.ArrearsPrincipal = l.ArrearsInterest = l.InterestAccrued = 0;
                l.ArrearsCloses = 0;
                l.State = total > 0 ? LoanState.WrittenOff : LoanState.Repaid;
            }

            // 5. Credit damage; the clock stops.
            int old = credit.Score;
            credit.Score = defs.Credit.Score.ForeclosureScore;
            credit.History.Add(new ScoreChange(t, credit.Score - old, credit.Score, "score.foreclosure"));
            sim.Emit(new CreditScoreChanged(t, old, credit.Score, "score.foreclosure"));
            credit.Foreclosures++;
            credit.PendingForeclosure = false;
            credit.NoticeLiveSeconds = 0;
            credit.LoanSeenThisSeason = false;
            sim.Emit(new ForeclosureCompleted(t, parcels, produce, raised, writtenOff, credit.Score));
        }

        private static long StockValue(Simulation sim, int good)
        {
            return MarketRules.Value(sim, good, sim.State.Market.Stock[good], MarketRules.Floor(sim, good));
        }

        // ---- selling collateral (spec 005 §3.6, ADR 0038) ----

        /// <summary>
        /// What a sale must repay to the secured lenders: <c>max(0, bankOwed − ltv × remaining collateral)</c>, where the remaining
        /// collateral excludes the parcel being sold.
        /// </summary>
        internal static long RequiredOnSale(Simulation sim, int parcelId, long animalId = -1)
        {
            if (sim.State.Credit == null) return 0;
            CreditDefs defs = sim.Config.Game.Credit;
            long owed = 0;
            Fixed ltv = Fixed.Zero, animalLtv = Fixed.Zero, houseLtv = Fixed.Zero;
            foreach (Loan l in sim.State.Credit.Loans)
            {
                if (!l.IsOpen || l.BusinessId != 0 || !defs.Lenders[l.Lender].Secured) continue; // a business loan is secured on the business
                owed += CreditRules.OwedNow(sim, l);
                if (defs.Lenders[l.Lender].CollateralLtv > ltv) ltv = defs.Lenders[l.Lender].CollateralLtv;
                if (defs.Lenders[l.Lender].LivestockLtv > animalLtv) animalLtv = defs.Lenders[l.Lender].LivestockLtv;
                if (defs.Lenders[l.Lender].HouseLtv > houseLtv) houseLtv = defs.Lenders[l.Lender].HouseLtv;
            }
            if (owed == 0) return 0;
            // What stays behind the loans once this parcel or animal is sold (spec 005 §3.6, spec 012 §3.12).
            long supported = Money.MulRate(CreditRules.Collateral(sim, 0, parcelId), ltv) + Money.MulRate(CreditRules.LivestockCollateral(sim, 0, animalId), animalLtv)
                + Money.MulRate(CreditRules.HouseCollateral(sim), houseLtv);
            return System.Math.Max(0, owed - supported);
        }

        /// <summary>Repays <paramref name="required"/> to the secured lenders' loans in id order (interest, arrears, principal).</summary>
        internal static void RepaySecured(Simulation sim, long required)
        {
            CreditDefs defs = sim.Config.Game.Credit;
            long left = required;
            foreach (Loan l in sim.State.Credit.Loans)
            {
                if (left <= 0) break;
                if (!l.IsOpen || l.BusinessId != 0 || !defs.Lenders[l.Lender].Secured) continue;
                Accrue(sim, l, sim.Period);
                left -= Pay(sim, l, left, "ledger.loanPayment", out _, out _, out _);
            }
            CheckCure(sim);
        }
    }
}
