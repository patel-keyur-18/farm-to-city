using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary><c>invoices.json</c> (spec 028 §3.1 and §4): the terms, the late rule and the village buyers. Every good and rival must exist.</summary>
        public static InvoicesDef LoadInvoices(string json, MarketDefs market, ShopDefs shops)
        {
            const string root = "invoices";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "terms", "defaultTerm", "lateTerms", "buyers");
            var arr = Array(Required(o, root, "terms"), Join(root, "terms"));
            if (arr.Count < 2 || arr.Count > 6) throw new ContentException(Join(root, "terms"), "between 2 and 6 terms");
            var terms = new List<InvoiceTermDef>();
            for (int i = 0; i < arr.Count; i++)
            {
                string at = Index(Join(root, "terms"), i);
                JObject t = Object(arr[i], at);
                OnlyFields(t, at, "id", "nameKey", "days");
                var term = new InvoiceTermDef { Id = String(Required(t, at, "id"), Join(at, "id")), NameKey = String(Required(t, at, "nameKey"), Join(at, "nameKey")), Days = BoundedInt(t, at, "days", 0, 60) };
                foreach (InvoiceTermDef x in terms) if (x.Id == term.Id) throw new ContentException(Join(at, "id"), "'" + term.Id + "' is listed twice");
                if (i == 0 && term.Days != 0) throw new ContentException(Join(at, "days"), "the first term is cash: 0 days");
                if (i > 0 && term.Days <= terms[i - 1].Days) throw new ContentException(Join(at, "days"), "terms must grow longer");
                terms.Add(term);
            }
            var def = new InvoicesDef
            {
                Terms = terms, LateTerms = BoundedInt(o, root, "lateTerms", 0, 4),
            };
            def.DefaultTerm = def.TermIndex(String(Required(o, root, "defaultTerm"), Join(root, "defaultTerm")));
            if (def.DefaultTerm < 0) throw new ContentException(Join(root, "defaultTerm"), "not one of the terms");
            var buyers = new List<WholesaleBuyerDef>();
            var barr = Array(Required(o, root, "buyers"), Join(root, "buyers"));
            for (int i = 0; i < barr.Count; i++)
            {
                string at = Index(Join(root, "buyers"), i);
                JObject b = Object(barr[i], at);
                OnlyFields(b, at, "rival", "goods");
                string rival = String(Required(b, at, "rival"), Join(at, "rival"));
                if (!System.Linq.Enumerable.Any(shops.Rivals, r => r.Id == rival)) throw new ContentException(Join(at, "rival"), "'" + rival + "' is not a rival in shops.json");
                var goods = new List<string>();
                var garr = Array(Required(b, at, "goods"), Join(at, "goods"));
                if (garr.Count == 0) throw new ContentException(Join(at, "goods"), "at least one good");
                for (int k = 0; k < garr.Count; k++)
                {
                    string id = String(garr[k], Index(Join(at, "goods"), k));
                    if (market.IndexOf(id) < 0) throw new ContentException(Index(Join(at, "goods"), k), "'" + id + "' is not a good");
                    goods.Add(id);
                }
                buyers.Add(new WholesaleBuyerDef { Rival = rival, Goods = goods });
            }
            def.Buyers = buyers;
            return def;
        }
    }
}
