using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Tools.Balancer
{
    /// <summary>
    /// US-099 (ADR 0016, ADR 0107 §11): the three archetypes of the balance rule, each the competent founder (homes first, a hand, orders) who
    /// puts what he earns into a different kind of town. The agri-hub grows the farm, the merchant town founds businesses, the education town
    /// builds the school and its adult classes first.
    /// </summary>
    public sealed class AgriHubBot : FarmToolBot
    {
        private readonly bool _land;

        public AgriHubBot(bool land = true, params string[] tools) : base(tools.Length > 0 ? tools : new[] { "pump", "canal", "drip", "mango", "granary", "tractor-hire" }) { _land = land; }

        public override string Name { get { return "agri-hub" + (_land ? "" : "-no-land"); } }

        /// <summary>The land bought so far, to keep a reserve for the close.</summary>
        public int Parcels { get; private set; }

        public override void Play(Simulation sim, long t)
        {
            base.Play(sim, t);
            if (_land) Land(sim, t);
        }

        /// <summary>
        /// Land: with cash beyond the next close's costs, the cheapest parcel for sale (no loan: a farm that grows on what it earns); its fields
        /// are harvested and sown with the season's best crop while the founder's hours (and his hand's) cover the demand.
        /// </summary>
        private void Land(Simulation sim, long t)
        {
            if (t % 86_400 / 3600 == 9 && sim.State.Ledger.Cash > 150_000 * Money.Coin)
            {
                foreach (Parcel p in sim.State.Land.Parcels)
                    if (!p.Owned && Ok(sim, new BuyParcel(t, sim.Config.Game.Map.Parcels[p.Id].Id))) { Parcels++; break; }
            }
            Fixed supply = FounderRules.LabourRate(sim) + FounderRules.MandiCut(sim) + PeopleRules.HiredHours(sim);
            foreach (Tile tile in sim.State.Land.Tiles)
            {
                Parcel parcel = sim.State.Land.Parcels[tile.ParcelId];
                if (!parcel.Owned || parcel.Protected || Array.IndexOf(AcresOf(sim), tile.Id) >= 0) continue;
                Crop c = sim.State.Farm.CropOn(tile.Id);
                if (c != null) { if (c.State == CropState.Ready) Ok(sim, new Harvest(t, tile.Id, Overflow.SellRestToTrader)); continue; }
                if (sim.State.Farm.Founder.AutoSubsistence) continue;
                Sim.Core.Definitions.CropDef crop = sim.Config.Game.Farm.Crops[sim.Config.Game.Farm.IndexOfCrop(HandCrop[Season(sim, t)])];
                if (FarmRules.LabourDemand(sim) + crop.LabourPerAcrePerDay > supply) break;
                Ok(sim, new Sow(t, tile.Id, crop.Id, SeedSource.Certified, Fertiliser(sim, tile.Id)));
            }
        }
    }

    /// <summary>The one investment every founder makes: a mango orchard on the last inherited acre once 30,000 coins are spare (ADR 0090).</summary>
    public static class Orchard
    {
        public static void Mango(Simulation sim, long t)
        {
            int last = Home.Acres(sim.Config.Game)[Home.Acres(sim.Config.Game).Length - 1];
            if (OrchardRules.Of(sim, last) != null)
            {
                // What the store and the trader take each hour; at 21:00 what is still ripe is sold on the trees (as the farm-tool bot does).
                sim.Execute(new PickOrchard(t, last, Overflow.SellRestToTrader));
                if (t % 86_400 / 3600 == 21) sim.Execute(new SellOrchardOnTrees(t, last));
                return;
            }
            if (sim.State.Ledger.Cash < 30_000 * Money.Coin) return;
            Crop c = sim.State.Farm.CropOn(last);
            if (c != null && c.State == CropState.Ready) sim.Execute(new Harvest(t, last, Overflow.SellRestToTrader));
            if (sim.State.Farm.CropOn(last) == null) sim.Execute(new PlantOrchard(t, last, "mango"));
        }
    }

    /// <summary>The merchant town (every archetype plants the mango orchard: see <see cref="Orchard"/>): homes first (the Village), then hired businesses on their loans as their eras come: the mill, the bakery, the dairy, the shops.</summary>
    public sealed class MerchantBot : BusinessBot
    {
        private readonly bool _mango;

        public MerchantBot(bool mango = true) : base(false, true, "flour_mill", "bakery", "dairy", "veg_stall", "tea_stall", "sweet_shop") { _mango = mango; }

        public override string Name { get { return "merchant" + (_mango ? "+mango" : ""); } }

        public override void Play(Simulation sim, long t)
        {
            base.Play(sim, t);
            if (_mango) Orchard.Mango(sim, t);
        }
    }

    /// <summary>The education town: homes, then the school as soon as the Hamlet allows it, its adult classes whenever a class can start, and the mill after.</summary>
    public sealed class EducationBot : VillageBot
    {
        private readonly bool _mango;

        public EducationBot(bool mango = true) : base(false, "HSM") { _mango = mango; }

        public override string Name { get { return "education" + (_mango ? "+mango" : ""); } }

        public override void Play(Simulation sim, long t)
        {
            base.Play(sim, t);
            if (_mango) Orchard.Mango(sim, t);
            if (SchoolRules.On(sim) && SchoolRules.State(sim).Status == SchoolStatus.Open && t % 86_400 / 3600 == 10) Ok(sim, new StartAdultClass(t));
        }
    }

    /// <summary>US-099's rule measured: medians of net worth (at year-0 prices) and town happiness at the end of a run, per bot.</summary>
    public static class Archetypes
    {
        public sealed class Row
        {
            public string Bot;
            public int Seeds;
            public List<double> NetWorth = new List<double>(), Happiness = new List<double>();

            public double MedianWorth { get { return Median(NetWorth); } }
            public double MedianHappiness { get { return Median(Happiness); } }
        }

        public static double Median(List<double> v)
        {
            if (v.Count == 0) return double.NaN;
            double[] a = v.OrderBy(x => x).ToArray();
            return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2;
        }

        public static Row Measure(SimConfig config, Func<Bot> make, int seeds, int seasons)
        {
            var worth = new double[seeds];
            var happy = new double[seeds];
            string name = make().Name;
            Parallel.For(0, seeds, i =>
            {
                Simulation sim = Economy.RunBot(config, make(), seasons, (ulong)(i + 1), null).Sim;
                worth[i] = (double)Reporting.NetWorth(sim).Total / 100.0 / ((double)sim.State.Market.PriceIndex.Raw / 4294967296.0);
                happy[i] = sim.State.Citizens == null || sim.State.Citizens.Population == 0 ? 0 : (double)HappinessRules.Town(sim).Raw / 4294967296.0;
            });
            return new Row { Bot = name, Seeds = seeds, NetWorth = worth.ToList(), Happiness = happy.ToList() };
        }
    }
}
