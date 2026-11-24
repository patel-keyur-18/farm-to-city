using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary>
        /// <c>chronicle.json</c> (spec 029 §4): the entries kept, the record harvest's floor and rise, and the kinds in the order of
        /// <see cref="ChronicleKind"/> (each with its id and icon; an id out of place is refused).
        /// </summary>
        public static ChronicleDefs LoadChronicle(string json)
        {
            const string root = "chronicle";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "keep", "recordMinGrams", "recordRise", "almanacKeep", "minProfit", "kinds");
            int keep = Int(Required(o, root, "keep"), Join(root, "keep"));
            Range(keep, 100, 2000, Join(root, "keep"));
            int minGrams = Int(Required(o, root, "recordMinGrams"), Join(root, "recordMinGrams"));
            Range(minGrams, 1, 100000000, Join(root, "recordMinGrams"));
            Fixed rise = Number(Required(o, root, "recordRise"), Join(root, "recordRise"));
            Range(rise, Fixed.Zero, Fixed.One, Join(root, "recordRise"));
            int almanacKeep = Int(Required(o, root, "almanacKeep"), Join(root, "almanacKeep"));
            Range(almanacKeep, 1, 40, Join(root, "almanacKeep"));
            int minProfit = Int(Required(o, root, "minProfit"), Join(root, "minProfit"));
            Range(minProfit, 0, 1000000000, Join(root, "minProfit"));
            var kinds = new List<ChronicleKindDef>();
            List<JToken> k = NonEmptyArray(o, root, "kinds");
            if (k.Count != ChronicleDefs.KindIds.Length) throw new ContentException(Join(root, "kinds"), "expected " + ChronicleDefs.KindIds.Length + " kinds, found " + k.Count);
            for (int i = 0; i < k.Count; i++)
            {
                string p = Index(Join(root, "kinds"), i);
                JObject x = Object(k[i], p);
                OnlyFields(x, p, "id", "icon");
                string id = String(Required(x, p, "id"), Join(p, "id"));
                if (id != ChronicleDefs.KindIds[i]) throw new ContentException(Join(p, "id"), "kind " + i + " must be '" + ChronicleDefs.KindIds[i] + "'");
                kinds.Add(new ChronicleKindDef { Id = id, Icon = String(Required(x, p, "icon"), Join(p, "icon")) });
            }
            return new ChronicleDefs { Keep = keep, RecordMinGrams = minGrams, RecordRise = rise, AlmanacKeep = almanacKeep, MinProfit = minProfit, Kinds = kinds.AsReadOnly() };
        }
    }
}
