using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Business
{
    /// <summary>A rival shop's running figures (spec 017 §3.7): its price, last hour's customers for its queue, today's and this season's.</summary>
    public sealed class Rival
    {
        /// <summary>Index into <see cref="Definitions.ShopDefs.Rivals"/>.</summary>
        public int Def { get; internal set; }

        /// <summary>Its content id, as saved (the content's order may change).</summary>
        public string Id { get; internal set; }
        public Fixed PriceFactor { get; internal set; }
        public Fixed Stars { get; internal set; }
        public long LastArrivals { get; internal set; }
        public long CustomersToday { get; internal set; }
        public long SeasonServed { get; internal set; }

        internal Rival Clone() { return (Rival)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteString(Id);
            h.WriteFixed(PriceFactor);
            h.WriteFixed(Stars);
            h.WriteLong(LastArrivals);
            h.WriteLong(CustomersToday);
            h.WriteLong(SeasonServed);
        }

        internal static Rival ReadFrom(SaveReader r, SimConfig config)
        {
            string id = r.ReadString();
            int def = -1;
            for (int i = 0; i < config.Game.Shops.Rivals.Count; i++) if (config.Game.Shops.Rivals[i].Id == id) def = i;
            return new Rival { Def = def, Id = id, PriceFactor = r.ReadFixed(), Stars = r.ReadFixed(), LastArrivals = r.ReadLong(), CustomersToday = r.ReadLong(), SeasonServed = r.ReadLong() };
        }
    }

    /// <summary>
    /// Customer choice (spec 017 §4; save schema 14's last section): the rivals, today's customers no shop served by kind, and what your
    /// town bought at your stalls yesterday (it comes off the town's buying at the market, §3.2).
    /// </summary>
    public sealed class ShopsState
    {
        public List<Rival> Rivals { get; internal set; } = new List<Rival>();

        /// <summary>The day (t ÷ day) the today figures are for.</summary>
        public long Day { get; internal set; }

        /// <summary>Customers lost today by <see cref="Definitions.ShopKind"/> (every shop closed or full; cups or grams).</summary>
        public long[] LostToday { get; internal set; } = new long[4];

        /// <summary>Grams by good your town bought at your stalls today and yesterday.</summary>
        public long[] TownToday { get; internal set; }
        public long[] TownYesterday { get; internal set; }

        // The middle of your houses, worked out once an hour (derived, never saved or hashed): an hour's shares ask for it often.
        internal long CentreAt = -1, CentreHomes = -1;
        internal int CentreTile;

        /// <summary>A new town's: the content's rivals on their own terms.</summary>
        internal static ShopsState New(SimConfig config)
        {
            var s = new ShopsState { TownToday = new long[config.Game.Market.Goods.Count], TownYesterday = new long[config.Game.Market.Goods.Count] };
            var rivals = config.Game.Shops.Rivals;
            for (int i = 0; i < rivals.Count; i++) s.Rivals.Add(new Rival { Def = i, Id = rivals[i].Id, PriceFactor = rivals[i].PriceFactor, Stars = rivals[i].Stars });
            return s;
        }

        internal ShopsState Clone()
        {
            var c = (ShopsState)MemberwiseClone();
            c.Rivals = Rivals.ConvertAll(x => x.Clone());
            c.LostToday = (long[])LostToday.Clone();
            c.TownToday = (long[])TownToday.Clone();
            c.TownYesterday = (long[])TownYesterday.Clone();
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Rivals.Count);
            foreach (Rival x in Rivals) x.WriteTo(h);
            h.WriteLong(Day);
            foreach (long v in LostToday) h.WriteLong(v);
            h.WriteInt(TownToday.Length);
            for (int i = 0; i < TownToday.Length; i++)
            {
                h.WriteLong(TownToday[i]);
                h.WriteLong(TownYesterday[i]);
            }
        }

        /// <summary>Reads the section; the content's rivals keep their place by id order by id (a rival the content dropped is dropped, a new one starts on its terms).</summary>
        internal static ShopsState ReadFrom(SaveReader r, SimConfig config)
        {
            var s = New(config);
            int n = r.ReadCount();
            for (int i = 0; i < n; i++)
            {
                Rival x = Rival.ReadFrom(r, config);
                if (x.Def >= 0) s.Rivals[x.Def] = x;
            }
            s.Day = r.ReadLong();
            for (int k = 0; k < s.LostToday.Length; k++) s.LostToday[k] = r.ReadLong();
            int goods = r.ReadCount();
            for (int i = 0; i < goods; i++)
            {
                int at = r.Slot(ContentTable.Good, i);
                s.TownToday[at] = r.ReadLong();
                s.TownYesterday[at] = r.ReadLong();
            }
            return s;
        }
    }
}
