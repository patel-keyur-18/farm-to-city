using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary><c>stories.json</c> (spec 029 §3.6): the four arcs' numbers.</summary>
        public static StoriesDefs LoadStories(string json)
        {
            const string root = "stories";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "stall", "reader", "migrant", "longLife");
            JObject stall = Object(Required(o, root, "stall"), Join(root, "stall"));
            string sp = Join(root, "stall");
            OnlyFields(stall, sp, "seasons", "savings", "motivation", "chance", "judgedAfter");
            var d = new StoriesDefs
            {
                StallSeasons = Int(Required(stall, sp, "seasons"), Join(sp, "seasons")),
                StallSavings = Int(Required(stall, sp, "savings"), Join(sp, "savings")),
                StallMotivation = Number(Required(stall, sp, "motivation"), Join(sp, "motivation")),
                StallChance = Number(Required(stall, sp, "chance"), Join(sp, "chance")),
                StallJudgedAfter = Int(Required(stall, sp, "judgedAfter"), Join(sp, "judgedAfter")),
            };
            Range(d.StallSeasons, 1, 40, Join(sp, "seasons"));
            Range((int)(d.StallSavings / 100), 0, 100000000, Join(sp, "savings"));
            Range(d.StallMotivation, Fixed.Zero, Fixed.FromInt(2), Join(sp, "motivation"));
            Range(d.StallChance, Fixed.Zero, Fixed.One, Join(sp, "chance"));
            Range(d.StallJudgedAfter, 1, 20, Join(sp, "judgedAfter"));
            JObject reader = Object(Required(o, root, "reader"), Join(root, "reader"));
            OnlyFields(reader, Join(root, "reader"), "adultAge");
            d.ReaderAdultAge = Int(Required(reader, Join(root, "reader"), "adultAge"), Join(root, "reader.adultAge"));
            Range(d.ReaderAdultAge, 8, 40, Join(root, "reader.adultAge"));
            JObject migrant = Object(Required(o, root, "migrant"), Join(root, "migrant"));
            OnlyFields(migrant, Join(root, "migrant"), "first", "second");
            d.MigrantFirst = Int(Required(migrant, Join(root, "migrant"), "first"), Join(root, "migrant.first"));
            d.MigrantSecond = Int(Required(migrant, Join(root, "migrant"), "second"), Join(root, "migrant.second"));
            Range(d.MigrantFirst, 1, 40, Join(root, "migrant.first"));
            Range(d.MigrantSecond, d.MigrantFirst + 1, 80, Join(root, "migrant.second"));
            JObject life = Object(Required(o, root, "longLife"), Join(root, "longLife"));
            OnlyFields(life, Join(root, "longLife"), "ages", "diedFrom");
            var ages = new List<int>();
            List<JToken> a = NonEmptyArray(life, Join(root, "longLife"), "ages");
            for (int i = 0; i < a.Count; i++)
            {
                ages.Add(Int(a[i], Index(Join(root, "longLife.ages"), i)));
                if (i > 0 && ages[i] <= ages[i - 1]) throw new ContentException(Index(Join(root, "longLife.ages"), i), "ages must rise");
            }
            if (ages.Count != 3) throw new ContentException(Join(root, "longLife.ages"), "expected three ages");
            d.LifeAges = ages.AsReadOnly();
            d.LifeDiedFrom = Int(Required(life, Join(root, "longLife"), "diedFrom"), Join(root, "longLife.diedFrom"));
            Range(d.LifeDiedFrom, 40, 120, Join(root, "longLife.diedFrom"));
            return d;
        }
    }
}
