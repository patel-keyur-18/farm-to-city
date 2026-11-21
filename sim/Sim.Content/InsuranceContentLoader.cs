using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary><c>insurance.json</c> (spec 027 §3.5–§3.6 and §4): the three policies' numbers.</summary>
        public static InsuranceDef LoadInsurance(string json)
        {
            const string root = "insurance";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "crop", "property", "business", "keepClaims");

            string cp = Join(root, "crop");
            JObject c = Object(Required(o, root, "crop"), cp);
            OnlyFields(c, cp, "threshold", "pestShortfall", "eventPart", "loading", "wetShare");
            var wetArr = Array(Required(c, cp, "wetShare"), Join(cp, "wetShare"));
            if (wetArr.Count != 4) throw new ContentException(Join(cp, "wetShare"), "four numbers: Spring, Summer, Monsoon, Winter");
            var wet = new Fixed[4];
            for (int i = 0; i < 4; i++)
            {
                wet[i] = Number(wetArr[i], Index(Join(cp, "wetShare"), i));
                Range(wet[i], Fixed.Zero, Fixed.One, Index(Join(cp, "wetShare"), i));
            }
            var crop = new CropInsuranceDef
            {
                Threshold = Bounded(c, cp, "threshold", "0.1", "1"), PestShortfall = Bounded(c, cp, "pestShortfall", "0", "1"),
                EventPart = Bounded(c, cp, "eventPart", "0", "0.5"), Loading = Bounded(c, cp, "loading", "1", "3"), WetShare = wet,
            };

            string pp = Join(root, "property");
            JObject p = Object(Required(o, root, "property"), pp);
            OnlyFields(p, pp, "rate", "excess");
            var property = new PropertyInsuranceDef { Rate = Bounded(p, pp, "rate", "0", "0.1"), Excess = Bounded(p, pp, "excess", "0", "0.9") };

            string bp = Join(root, "business");
            JObject b = Object(Required(o, root, "business"), bp);
            OnlyFields(b, bp, "rate", "profitShare", "excess", "dailyProfitShare");
            var business = new BusinessInsuranceDef
            {
                Rate = Bounded(b, bp, "rate", "0", "0.1"), ProfitShare = Bounded(b, bp, "profitShare", "0", "0.5"),
                Excess = Bounded(b, bp, "excess", "0", "0.9"), DailyProfitShare = Bounded(b, bp, "dailyProfitShare", "0", "1"),
            };
            return new InsuranceDef { Crop = crop, Property = property, Business = business, KeepClaims = BoundedInt(o, root, "keepClaims", 1, 20) };
        }
    }
}
