using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary><c>exit.json</c> (spec 028 §3.3): the valuation's multiple and its steps, the buyers' share and days, a rushed sale, the branches.</summary>
        public static ExitDef LoadExit(string json)
        {
            const string root = "exit";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "baseMultiple", "growthStep", "riskStep", "managerBonus", "minMultiple", "maxMultiple", "growthShare", "profitableSeasons", "buyerLow", "buyerHigh",
                "saleDaysMin", "saleDaysMax", "rushedDays", "rushedShare", "maxBranches", "buyers",
                "investorShares", "maxOutside", "buyBackSeasons", "investorValueMin", "partnerStake", "partnerProfit", "buyOutLow", "buyOutHigh", "buyOutHours", "buyOutBigHours", "buyOutBigShare",
                "buyOutGapSeasons", "offerHours", "offerChance", "investors");
            Fixed F(string n) { return Number(Required(o, root, n), Join(root, n)); }
            int I(string n) { return Int(Required(o, root, n), Join(root, n)); }
            var d = new ExitDef
            {
                BaseMultiple = F("baseMultiple"), GrowthStep = I("growthStep"), RiskStep = I("riskStep"), ManagerBonus = I("managerBonus"), MinMultiple = F("minMultiple"), MaxMultiple = F("maxMultiple"),
                GrowthShare = F("growthShare"), ProfitableSeasons = I("profitableSeasons"), BuyerLow = F("buyerLow"), BuyerHigh = F("buyerHigh"), SaleDaysMin = I("saleDaysMin"),
                SaleDaysMax = I("saleDaysMax"), RushedDays = I("rushedDays"), RushedShare = F("rushedShare"), MaxBranches = I("maxBranches"),
                MaxOutside = F("maxOutside"), BuyBackSeasons = I("buyBackSeasons"), InvestorValueMin = Money(Required(o, root, "investorValueMin"), Join(root, "investorValueMin")), PartnerStake = F("partnerStake"),
                PartnerProfit = F("partnerProfit"), BuyOutLow = F("buyOutLow"), BuyOutHigh = F("buyOutHigh"), BuyOutHours = I("buyOutHours"), BuyOutBigHours = I("buyOutBigHours"),
                BuyOutBigShare = F("buyOutBigShare"), BuyOutGapSeasons = I("buyOutGapSeasons"), OfferHours = I("offerHours"), OfferChance = F("offerChance"),
            };
            Range(d.MinMultiple, Fixed.One, d.MaxMultiple, Join(root, "minMultiple"));
            Range(d.BaseMultiple, d.MinMultiple, d.MaxMultiple, Join(root, "baseMultiple"));
            Range(d.GrowthShare, Fixed.Zero, Fixed.One, Join(root, "growthShare"));
            Range(d.ProfitableSeasons, 1, 4, Join(root, "profitableSeasons")); // a business keeps four reports
            Range(d.BuyerLow, Fixed.FromRatio(1, 2), d.BuyerHigh, Join(root, "buyerLow"));
            Range(d.BuyerHigh, d.BuyerLow, Fixed.FromInt(2), Join(root, "buyerHigh"));
            Range(d.SaleDaysMin, 1, d.SaleDaysMax, Join(root, "saleDaysMin"));
            Range(d.SaleDaysMax, d.SaleDaysMin, 30, Join(root, "saleDaysMax"));
            Range(d.RushedDays, 1, d.SaleDaysMin, Join(root, "rushedDays"));
            Range(d.RushedShare, Fixed.FromRatio(1, 2), d.BuyerLow, Join(root, "rushedShare"));
            Range(d.MaxBranches, 0, 5, Join(root, "maxBranches"));
            var buyers = new List<string>();
            var arr = Array(Required(o, root, "buyers"), Join(root, "buyers"));
            if (arr.Count == 0) throw new ContentException(Join(root, "buyers"), "at least one kind of buyer");
            for (int i = 0; i < arr.Count; i++) buyers.Add(String(arr[i], Index(Join(root, "buyers"), i)));
            d.Buyers = buyers;
            var shares = new List<Fixed>();
            var sh = Array(Required(o, root, "investorShares"), Join(root, "investorShares"));
            if (sh.Count == 0) throw new ContentException(Join(root, "investorShares"), "at least one share");
            for (int i = 0; i < sh.Count; i++)
            {
                Fixed v = Number(sh[i], Index(Join(root, "investorShares"), i));
                Range(v, Fixed.Zero, d.MaxOutside, Index(Join(root, "investorShares"), i));
                if (v <= Fixed.Zero || (i > 0 && v <= shares[i - 1])) throw new ContentException(Index(Join(root, "investorShares"), i), "above 0 and ascending");
                shares.Add(v);
            }
            d.InvestorShares = shares;
            Range(d.MaxOutside, Fixed.Zero, Fixed.One, Join(root, "maxOutside"));
            Range(d.BuyBackSeasons, 0, 40, Join(root, "buyBackSeasons"));
            Range(d.PartnerStake, Fixed.Zero, d.MaxOutside, Join(root, "partnerStake"));
            Range(d.PartnerProfit, Fixed.Zero, d.MaxOutside, Join(root, "partnerProfit"));
            Range(d.BuyOutLow, Fixed.FromRatio(1, 2), d.BuyOutHigh, Join(root, "buyOutLow"));
            Range(d.BuyOutHigh, d.BuyOutLow, Fixed.FromInt(2), Join(root, "buyOutHigh"));
            Range(d.BuyOutHours, 1, 96, Join(root, "buyOutHours"));
            Range(d.BuyOutBigHours, d.BuyOutHours, 96, Join(root, "buyOutBigHours"));
            Range(d.BuyOutBigShare, Fixed.Zero, Fixed.One, Join(root, "buyOutBigShare"));
            Range(d.BuyOutGapSeasons, 0, 40, Join(root, "buyOutGapSeasons"));
            Range(d.OfferHours, 1, 96, Join(root, "offerHours"));
            Range(d.OfferChance, Fixed.Zero, Fixed.One, Join(root, "offerChance"));
            var names = new List<string>();
            var inv = Array(Required(o, root, "investors"), Join(root, "investors"));
            if (inv.Count == 0) throw new ContentException(Join(root, "investors"), "at least one investor");
            for (int i = 0; i < inv.Count; i++) names.Add(String(inv[i], Index(Join(root, "investors"), i)));
            d.Investors = names;
            return d;
        }
    }
}
