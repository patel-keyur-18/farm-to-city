using System.Collections.Generic;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.People;
using Sim.Core.Time;

namespace Sim.Core.History
{
    /// <summary>
    /// The Town Almanac (spec 029 §3.4 and §3.5, ADR 0165, 0166 gap 12): what the year was, written at each Winter close and kept. Like the
    /// Chronicle it only reads: the year's crops and losses are gathered from events as they happen, nothing here changes any other state.
    /// </summary>
    public static class AlmanacRules
    {
        public static AlmanacState State(Simulation sim) { return ChronicleRules.On(sim) ? sim.State.Chronicle.Almanac : null; }

        /// <summary>A Winter close ends a year (spring is season 0).</summary>
        public static bool EndsYear(Simulation sim, long period) { return period % ClockDef.SeasonsPerYear == ClockDef.SeasonsPerYear - 1; }

        // ---- gathering (from the events, as they happen) ----

        internal static void Observe(Simulation sim, SimEvent e)
        {
            AlmanacState a = State(sim);
            if (a == null) return;
            switch (e)
            {
                case CropSown x:
                    for (int i = 0; i < sim.Config.Game.Farm.Crops.Count && i < a.CropCost.Length; i++)
                        if (sim.Config.Game.Farm.Crops[i].Id == x.CropId) { a.CropCost[i] += x.Cost; break; }
                    break;
                case CitizenLiterate x:
                    if (x.How == LiterateHow.School && a.Reader == 0) a.Reader = x.CitizenId; // the first child of the year
                    break;
                case FireRepaired x: Loss(a, AlmanacLoss.FireRepair, x.Cost, ""); break;
                case ParcelSeized x: Loss(a, AlmanacLoss.Seized, x.QuickSaleValue, x.ParcelId); break;
                case AnimalSeized x: Loss(a, AlmanacLoss.Seized, x.Amount, ""); break;
                case BusinessSold x:
                {
                    SaleRecord rec = sim.State.Township?.Exit.Sales.Find(r => r.Id == x.SaleId);
                    if (rec != null) Loss(a, AlmanacLoss.BusinessSold, rec.Held - rec.Price, rec.TypeId); // sold for less than it held
                    break;
                }
                case GoodsSpoiled x:
                    Spoiled(sim, a, x);
                    break;
            }
        }

        /// <summary>A loss on closing a business or selling a machine, posted where it is known.</summary>
        internal static void Loss(Simulation sim, AlmanacLoss kind, long amount, string id)
        {
            AlmanacState a = State(sim);
            if (a != null) Loss(a, kind, amount, id);
        }

        private static void Loss(AlmanacState a, AlmanacLoss kind, long amount, string id)
        {
            if (amount <= 0 || amount <= a.WorstAmount) return; // the earlier of two equal losses is kept
            a.WorstLoss = kind; a.WorstAmount = amount; a.WorstId = id;
        }

        /// <summary>A day's spoiled goods are one loss (valued at the trader's floor), added up until the day changes.</summary>
        private static void Spoiled(Simulation sim, AlmanacState a, GoodsSpoiled x)
        {
            long day = x.SimTime / ClockDef.SecondsPerDay + 1;
            if (a.SpoilDay != day) { FlushSpoiled(a); a.SpoilDay = day; }
            int g = sim.Config.Game.Market.IndexOf(x.GoodId);
            if (g >= 0) a.SpoilValue += MarketRules.Value(sim, g, x.Quantity, MarketRules.Floor(sim, g));
        }

        private static void FlushSpoiled(AlmanacState a)
        {
            Loss(a, AlmanacLoss.Spoiled, a.SpoilValue, "");
            a.SpoilDay = 0; a.SpoilValue = 0;
        }

        // ---- each close ----

        /// <summary>At every season close: the closed season's crop sales join the year, a business's losing season is a loss; a Winter close writes the Almanac.</summary>
        internal static void Closed(Simulation sim, SeasonClosed x)
        {
            AlmanacState a = State(sim);
            if (a == null) return;
            IReadOnlyList<CropDef> crops = sim.Config.Game.Farm.Crops;
            IReadOnlyList<GoodDef> goods = sim.Config.Game.Market.Goods;
            foreach (Sale s in sim.State.Finance.Sales.Last) // rolled just before this event: the season that closed
                for (int i = 0; i < crops.Count && i < a.CropIncome.Length; i++)
                    if ((uint)crops[i].OutputGood < (uint)goods.Count && goods[crops[i].OutputGood].Id == s.Good) { a.CropIncome[i] += s.Amount; break; }
            if (sim.State.Businesses != null)
                foreach (Business.Business b in sim.State.Businesses.Businesses)
                {
                    BusinessReport r = LastReport(b, x.Period);
                    if (r != null) Loss(a, AlmanacLoss.LosingSeason, -BusinessBooks.NetProfit(r), BusinessRules.TypeOf(sim, b).Id);
                }
            if (EndsYear(sim, x.Period)) Write(sim, a, x);
        }

        private static BusinessReport LastReport(Business.Business b, long period)
        {
            return b.Reports.Count > 0 && b.Reports[b.Reports.Count - 1].Period == period ? b.Reports[b.Reports.Count - 1] : null;
        }

        private static void Write(Simulation sim, AlmanacState a, SeasonClosed x)
        {
            FlushSpoiled(a);
            ChronicleDefs d = ChronicleRules.Defs(sim);
            long year = x.Period / ClockDef.SeasonsPerYear + 1;
            var e = new AlmanacEntry
            {
                Year = year, At = x.SimTime, Households = Progress.EraRules.Households(sim), People = sim.State.Citizens?.Population ?? 0, Village = Town.CohortRules.People(sim),
                NetWorth = x.Report.NetWorth.Total, Loss = a.WorstLoss, LossAmount = a.WorstAmount, LossId = a.WorstId,
            };
            SeasonReport before = sim.State.Finance.Reports.Find(r => r.Period == x.Period - ClockDef.SeasonsPerYear);
            if (before != null) { e.HadBefore = true; e.Before = before.NetWorth.Total; }
            Best(sim, a, e, x.Period, d.MinProfit);
            Spot(sim, a, e, x.Period);
            a.Entries.Add(e);
            while (a.Entries.Count > d.AlmanacKeep) a.Entries.RemoveAt(0);
            // The next year starts empty.
            System.Array.Clear(a.CropIncome, 0, a.CropIncome.Length);
            System.Array.Clear(a.CropCost, 0, a.CropCost.Length);
            a.WorstLoss = AlmanacLoss.None; a.WorstAmount = 0; a.WorstId = ""; a.Reader = 0;
            sim.Emit(new AlmanacWritten(x.SimTime, year));
        }

        // ---- the best return (§3.4) ----

        /// <summary>
        /// The business or crop with the biggest share of profit to money put in, provided its profit is at least <paramref name="minProfit"/>.
        /// A business's profit is its closed seasons of this year and its money put in is its capital still in it (a business that has taken its
        /// capital out has nothing at risk and isn't ranked); a crop's is its sales less its sowings. Shares are compared by cross-multiplying
        /// (no division); a tie goes to a business, then the lowest id.
        /// </summary>
        private static void Best(Simulation sim, AlmanacState a, AlmanacEntry e, long winter, long minProfit)
        {
            long bestProfit = 0, bestPut = 0;
            if (sim.State.Businesses != null)
                foreach (Business.Business b in sim.State.Businesses.Businesses)
                {
                    if (b.Status == BusinessStatus.Closed) continue;
                    long put = BusinessBooks.CapitalIn(b), profit = 0;
                    foreach (BusinessReport r in b.Reports) if (r.Period > winter - ClockDef.SeasonsPerYear && r.Period <= winter) profit += BusinessBooks.NetProfit(r);
                    if (put <= 0 || profit < minProfit) continue;
                    if (e.Best == AlmanacBest.None || (System.Numerics.BigInteger)profit * bestPut > (System.Numerics.BigInteger)bestProfit * put)
                    {
                        e.Best = AlmanacBest.Business; e.BestId = BusinessRules.TypeOf(sim, b).Id; e.BestRef = b.Id; bestProfit = profit; bestPut = put;
                    }
                }
            IReadOnlyList<CropDef> crops = sim.Config.Game.Farm.Crops;
            for (int i = 0; i < crops.Count && i < a.CropCost.Length; i++)
            {
                long put = a.CropCost[i], profit = a.CropIncome[i] - put;
                if (put <= 0 || profit < minProfit) continue;
                if (e.Best == AlmanacBest.None || (System.Numerics.BigInteger)profit * bestPut > (System.Numerics.BigInteger)bestProfit * put)
                {
                    e.Best = AlmanacBest.Crop; e.BestId = crops[i].Id; e.BestRef = 0; bestProfit = profit; bestPut = put;
                }
            }
            e.BestProfit = bestProfit; e.BestPut = bestPut;
        }

        // ---- the spotlight (§3.5) ----

        /// <summary>
        /// The first that fits of: an arc that moved this year (with the stories), the first child to read, the youngest baby, the longest-serving
        /// worker, the oldest elder; ties go to the lowest id. No randomness, so the card is the same whenever it is opened.
        /// </summary>
        private static void Spot(Simulation sim, AlmanacState a, AlmanacEntry e, long winter)
        {
            CitizensState cs = sim.State.Citizens;
            if (cs == null) return;
            long yearStart = (winter - ClockDef.SeasonsPerYear + 1) * sim.Clock.SecondsPerSeason;
            if (a.Reader != 0 && cs.Citizen(a.Reader) is Citizen reader && reader.Living) { e.Spot = AlmanacSpot.Reader; e.SpotCitizen = reader.Id; return; }
            Citizen baby = null; long babyAt = 0, workerAt = 0;
            Citizen worker = null, elder = null;
            int elderAge = 0;
            foreach (Citizen c in cs.Citizens) // in id order
            {
                if (!c.Living) continue;
                foreach (StoryLine s in c.Story)
                {
                    if (s.Kind == StoryKind.Born && s.At >= yearStart && (baby == null || s.At > babyAt)) { baby = c; babyAt = s.At; }
                    if (s.Kind == StoryKind.Hired && (c.Job == CitizenJob.Hand || c.Job == CitizenJob.Business) && (worker == null || s.At < workerAt)) { worker = c; workerAt = s.At; }
                }
                int age = CitizenRules.Age(sim, c);
                if (age >= sim.Config.Game.Population.RetireAge && age > elderAge) { elder = c; elderAge = age; }
            }
            if (baby != null) { e.Spot = AlmanacSpot.Baby; e.SpotCitizen = baby.Id; }
            else if (worker != null) { e.Spot = AlmanacSpot.Worker; e.SpotCitizen = worker.Id; }
            else if (elder != null) { e.Spot = AlmanacSpot.Elder; e.SpotCitizen = elder.Id; }
        }
    }
}
