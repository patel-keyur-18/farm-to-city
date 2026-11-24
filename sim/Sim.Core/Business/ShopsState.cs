using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Business
{
    /// <summary>
    /// A rival shop's running figures (spec 017 §3.7): its price, last hour's customers for its queue, today's and this season's. The
    /// village's own from the content, or a newcomer (Def −1, schema 15, ADR 0095 §1–2) on a pitch or as the cart by the square.
    /// </summary>
    public sealed class Rival
    {
        /// <summary>Index into <see cref="Definitions.ShopDefs.Rivals"/>, or −1 for a newcomer.</summary>
        public int Def { get; internal set; }

        public bool Newcomer { get { return Def < 0; } }

        /// <summary>A newcomer's kind, its pitch (−1: the cart by the square), when it came and its seasons under the exit share.</summary>
        public Definitions.ShopKind Kind { get; internal set; }
        public int Pitch { get; internal set; } = -1;
        public long OpenedAt { get; internal set; }
        public int LowSeasons { get; internal set; }

        /// <summary>A newcomer drawn by customers nobody served (a gap), else by your prices above the reference.</summary>
        public bool Gap { get; internal set; }

        /// <summary>The citizen whose stall it is (spec 029 §3.6, a hand who left to start one), or 0 for an ordinary newcomer (schema 24).</summary>
        public long Owner { get; internal set; }

        /// <summary>Last season's share of its kind's demand (schema 15): the grocer and tea stall react to its change (ADR 0095 §3).</summary>
        public Fixed LastShare { get; internal set; }

        /// <summary>When it last changed its price (schema 15, 0 never): the alert shows that season.</summary>
        public long ChangedAt { get; internal set; }

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
            if (h.Hashing ? LastShare != Fixed.Zero || ChangedAt != 0 : !(h is SaveWriter w && w.Schema < 15)) // hashed only once used
            {
                h.WriteFixed(LastShare);
                h.WriteLong(ChangedAt);
            }
        }

        /// <summary>A newcomer's own terms after the running figures (schema 15).</summary>
        internal void WriteNewcomer(IStateWriter h)
        {
            WriteTo(h);
            h.WriteInt((int)Kind);
            h.WriteInt(Pitch);
            h.WriteLong(OpenedAt);
            h.WriteInt(LowSeasons);
            h.WriteBool(Gap);
            if (h.Hashing ? Owner != 0 : !(h is SaveWriter w24 && w24.Schema < 24)) h.WriteLong(Owner); // hashed only once used
        }

        internal static Rival ReadNewcomer(SaveReader r, SimConfig config)
        {
            Rival x = ReadFrom(r, config);
            x.Def = -1;
            x.Kind = (Definitions.ShopKind)r.ReadInt();
            x.Pitch = r.ReadInt();
            x.OpenedAt = r.ReadLong();
            x.LowSeasons = r.ReadInt();
            x.Gap = r.ReadBool();
            if (r.Schema >= 24) x.Owner = r.ReadLong();
            return x;
        }

        internal static Rival ReadFrom(SaveReader r, SimConfig config)
        {
            string id = r.ReadString();
            int def = -1;
            for (int i = 0; i < config.Game.Shops.Rivals.Count; i++) if (config.Game.Shops.Rivals[i].Id == id) def = i;
            var x = new Rival { Def = def, Id = id, PriceFactor = r.ReadFixed(), Stars = r.ReadFixed(), LastArrivals = r.ReadLong(), CustomersToday = r.ReadLong(), SeasonServed = r.ReadLong() };
            if (def >= 0) x.Kind = config.Game.Shops.Rivals[def].Kind;
            if (r.Schema >= 15)
            {
                x.LastShare = r.ReadFixed();
                x.ChangedAt = r.ReadLong();
            }
            return x;
        }
    }

    /// <summary>
    /// Customer choice (spec 017 §4; save schema 14's last section): the rivals, today's customers no shop served by kind, and what your
    /// town bought at your stalls yesterday (it comes off the town's buying at the market, §3.2).
    /// </summary>
    public sealed class ShopsState
    {
        /// <summary>Slots in the by-kind counts: none, vegetables, chai, sweets, meals and groceries. The first four are the older layout; meals are saved with the restaurant's own section (schema 23), groceries with the kirana's (schema 24).</summary>
        internal const int Kinds = 6, OlderKinds = 4;

        public List<Rival> Rivals { get; internal set; } = new List<Rival>();

        /// <summary>Newcomers (schema 15), at most one of each kind, in the order they came.</summary>
        public List<Rival> Newcomers { get; internal set; } = new List<Rival>();

        /// <summary>This season's customers no shop served by <see cref="Definitions.ShopKind"/> (schema 15): a gap draws a newcomer.</summary>
        public long[] LostSeason { get; internal set; } = new long[Kinds];

        /// <summary>The village's rivals, then the newcomers.</summary>
        public List<Rival> All()
        {
            var all = new List<Rival>(Rivals);
            all.AddRange(Newcomers);
            return all;
        }

        /// <summary>The newcomer on a pitch, or null.</summary>
        public Rival OnPitch(int pitch) { return Newcomers.Find(x => x.Pitch == pitch && pitch >= 0); }

        /// <summary>The day (t ÷ day) the today figures are for.</summary>
        public long Day { get; internal set; }

        /// <summary>Customers lost today by <see cref="Definitions.ShopKind"/> (every shop closed or full; cups or grams).</summary>
        public long[] LostToday { get; internal set; } = new long[Kinds];

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
            for (int i = 0; i < rivals.Count; i++) s.Rivals.Add(new Rival { Def = i, Id = rivals[i].Id, Kind = rivals[i].Kind, PriceFactor = rivals[i].PriceFactor, Stars = rivals[i].Stars });
            return s;
        }

        internal ShopsState Clone()
        {
            var c = (ShopsState)MemberwiseClone();
            c.Rivals = Rivals.ConvertAll(x => x.Clone());
            c.Newcomers = Newcomers.ConvertAll(x => x.Clone());
            c.LostSeason = (long[])LostSeason.Clone();
            c.LostToday = (long[])LostToday.Clone();
            c.TownToday = (long[])TownToday.Clone();
            c.TownYesterday = (long[])TownYesterday.Clone();
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Rivals.FindAll(x => Older(x.Kind)).Count);
            foreach (Rival x in Rivals) if (Older(x.Kind)) x.WriteTo(h);
            h.WriteLong(Day);
            for (int k = 0; k < OlderKinds; k++) h.WriteLong(LostToday[k]);
            h.WriteInt(TownToday.Length);
            for (int i = 0; i < TownToday.Length; i++)
            {
                h.WriteLong(TownToday[i]);
                h.WriteLong(TownYesterday[i]);
            }
            // Schema 15 (ADR 0095): newcomers and the season's lost customers; hashed only once there's any, so older replays keep their hash.
            bool any = Newcomers.Count > 0 || System.Array.FindIndex(LostSeason, 0, OlderKinds, v => v != 0) >= 0;
            if (h.Hashing ? any : !(h is SaveWriter w && w.Schema < 15))
            {
                h.WriteInt(Newcomers.Count);
                foreach (Rival x in Newcomers) x.WriteNewcomer(h);
                for (int k = 0; k < OlderKinds; k++) h.WriteLong(LostSeason[k]);
            }
            // Schema 23 (spec 028 §3.7): the restaurant's diners (the town dhaba's figures and the customers nobody fed); hashed only once the restaurant has opened.
            bool meals = LostToday[(int)Definitions.ShopKind.Meals] != 0 || LostSeason[(int)Definitions.ShopKind.Meals] != 0
                || Rivals.Exists(x => x.Kind == Definitions.ShopKind.Meals && (x.LastArrivals != 0 || x.CustomersToday != 0 || x.SeasonServed != 0 || x.LastShare != Fixed.Zero || x.ChangedAt != 0));
            if (h.Hashing ? meals : !(h is SaveWriter m23 && m23.Schema < Town.TownshipState.GrowthSchema))
            {
                h.WriteLong(LostToday[(int)Definitions.ShopKind.Meals]);
                h.WriteLong(LostSeason[(int)Definitions.ShopKind.Meals]);
                List<Rival> dining = Rivals.FindAll(x => x.Kind == Definitions.ShopKind.Meals);
                h.WriteInt(dining.Count);
                foreach (Rival x in dining) x.WriteTo(h);
            }
            // Schema 24 (spec 028 §3.10): the kirana's customers (the village grocer's staples and the customers nobody served); hashed only once a kirana has been open.
            bool grocery = LostToday[(int)Definitions.ShopKind.Grocery] != 0 || LostSeason[(int)Definitions.ShopKind.Grocery] != 0 || Rivals.Exists(x => x.Kind == Definitions.ShopKind.Grocery && Moved(x));
            if (h.Hashing ? grocery : !(h is SaveWriter g24 && g24.Schema < 24))
            {
                h.WriteLong(LostToday[(int)Definitions.ShopKind.Grocery]);
                h.WriteLong(LostSeason[(int)Definitions.ShopKind.Grocery]);
                List<Rival> staples = Rivals.FindAll(x => x.Kind == Definitions.ShopKind.Grocery);
                h.WriteInt(staples.Count);
                foreach (Rival x in staples) x.WriteTo(h);
            }
        }

        private static bool Older(Definitions.ShopKind k) { return k != Definitions.ShopKind.Meals && k != Definitions.ShopKind.Grocery; }

        private static bool Moved(Rival x) { return x.LastArrivals != 0 || x.CustomersToday != 0 || x.SeasonServed != 0 || x.LastShare != Fixed.Zero || x.ChangedAt != 0; }

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
            for (int k = 0; k < OlderKinds; k++) s.LostToday[k] = r.ReadLong();
            int goods = r.ReadCount();
            for (int i = 0; i < goods; i++)
            {
                int at = r.Slot(ContentTable.Good, i);
                s.TownToday[at] = r.ReadLong();
                s.TownYesterday[at] = r.ReadLong();
            }
            if (r.Schema < 15) return s; // schema 14: no newcomers yet
            int m = r.ReadCount();
            for (int i = 0; i < m; i++) s.Newcomers.Add(Rival.ReadNewcomer(r, config));
            for (int k = 0; k < OlderKinds; k++) s.LostSeason[k] = r.ReadLong();
            if (r.Schema < Town.TownshipState.GrowthSchema) return s;
            s.LostToday[(int)Definitions.ShopKind.Meals] = r.ReadLong();
            s.LostSeason[(int)Definitions.ShopKind.Meals] = r.ReadLong();
            int dining = r.ReadCount();
            for (int i = 0; i < dining; i++)
            {
                Rival x = Rival.ReadFrom(r, config);
                if (x.Def >= 0) s.Rivals[x.Def] = x;
            }
            if (r.Schema < 24) return s;
            s.LostToday[(int)Definitions.ShopKind.Grocery] = r.ReadLong();
            s.LostSeason[(int)Definitions.ShopKind.Grocery] = r.ReadLong();
            int staples = r.ReadCount();
            for (int i = 0; i < staples; i++)
            {
                Rival x = Rival.ReadFrom(r, config);
                if (x.Def >= 0) s.Rivals[x.Def] = x;
            }
            return s;
        }
    }
}
