using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>The map overlays (US-153, spec 024 §3.8): one at a time, from the Township. An overlay reads the sim and changes nothing.</summary>
    public enum OverlayKind
    {
        None = 0,
        Fertility = 1,
        LandValue = 2,
        Coverage = 3,
        Happiness = 4,

        /// <summary>Grey with "No crime yet" until the Town life update (spec 026, Sprint 24).</summary>
        Crime = 5,
    }

    /// <summary>One line of the chooser: the overlay's name and what it shows.</summary>
    public sealed class OverlayChoiceVm
    {
        public OverlayKind Kind { get; internal set; }
        public string Name { get; internal set; }
        public string Sub { get; internal set; }

        /// <summary>Crime is drawn greyed (nothing to show yet) but opens: its key says "No crime yet".</summary>
        public bool Muted { get; internal set; }
    }

    /// <summary>
    /// An overlay as the map draws it: for every acre a step 0..4 of one hue (the presentation picks the hue), or <see cref="NoStep"/> for an acre
    /// the overlay does not tint, and the smoke's footprint. <see cref="Labels"/> are the key's five numbers.
    /// </summary>
    public sealed class OverlayVm
    {
        public const int NoStep = -1;

        public OverlayKind Kind { get; internal set; }
        public string Title { get; internal set; }
        public string Note { get; internal set; }

        /// <summary>The key's five steps, lightest first; empty for crime (one grey step on the land, no key to read).</summary>
        public List<string> Labels { get; } = new List<string>();

        /// <summary>By tile id: 0..4 or <see cref="NoStep"/>.</summary>
        public int[] Steps { get; internal set; }

        /// <summary>By tile id: a kiln's or the generator's smoke reaches the acre (the coverage overlay hatches these red).</summary>
        public bool[] Smoke { get; internal set; }
    }

    /// <summary>The small card a tap opens while an overlay is up (spec 024 §3.8): the acre's number, a ? for its Why?, <b>Open the acre</b>.</summary>
    public sealed class OverlayCardVm
    {
        public int Tile { get; internal set; }
        public string Title { get; internal set; }
        public string Value { get; internal set; }
        public string Line { get; internal set; }
        public string OpenLabel { get; internal set; }

        /// <summary>The Why? source and id behind the ?, or null when the acre has no number (not tinted, or no crime yet).</summary>
        public string WhyKey { get; internal set; }
        public long WhyId { get; internal set; }
    }

    /// <summary>Which of the four services reach an acre, and how far each is (metres), for the coverage overlay and its Why?.</summary>
    public sealed class CoverageAt
    {
        public bool School, Clinic, Watchman, Water;
        public int SchoolM, SchoolReach, ClinicM, ClinicReach, WatchmanM, WatchmanReach;

        /// <summary>What gives the water: <c>pond</c>, <c>well</c>, <c>tower</c>, or null.</summary>
        public string WaterFrom;

        public int Count { get { return (School ? 1 : 0) + (Clinic ? 1 : 0) + (Watchman ? 1 : 0) + (Water ? 1 : 0); } }
    }

    public static class OverlayView
    {
        public const string CoverageWhyKey = "overlayCoverage", HappinessWhyKey = "overlayHappiness"; // Why? sources, not string rows

        // The key's edges are display scales, not balance: the land value's fourth step starts where a pucca house is offered (ADR 0131).
        private static readonly string[] CoverageSteps = { "overlay.coverage.step0", "overlay.coverage.step1", "overlay.coverage.step2", "overlay.coverage.step3", "overlay.coverage.step4" };
        private static readonly Fixed[] FertilityEdges = { Fixed.FromInt(20), Fixed.FromInt(40), Fixed.FromInt(60), Fixed.FromInt(80) };
        private static readonly Fixed[] HappinessEdges = { Fixed.FromInt(50), Fixed.FromInt(60), Fixed.FromInt(70), Fixed.FromInt(80) };

        /// <summary>The button is there from the Township, when land value exists to be read.</summary>
        public static bool Available(Simulation sim) { return LandValueRules.On(sim); }

        private static string Key(OverlayKind k)
        {
            switch (k)
            {
                case OverlayKind.Fertility: return "fertility";
                case OverlayKind.LandValue: return "landValue";
                case OverlayKind.Coverage: return "coverage";
                case OverlayKind.Happiness: return "happiness";
                default: return "crime";
            }
        }

        public static List<OverlayChoiceVm> Choices(ViewContext ctx)
        {
            var list = new List<OverlayChoiceVm>();
            foreach (OverlayKind k in new[] { OverlayKind.Fertility, OverlayKind.LandValue, OverlayKind.Coverage, OverlayKind.Happiness, OverlayKind.Crime })
                list.Add(new OverlayChoiceVm { Kind = k, Name = ctx.Strings.Get("overlay." + Key(k) + ".name"), Sub = ctx.Strings.Get("overlay." + Key(k) + ".sub"), Muted = k == OverlayKind.Crime });
            return list;
        }

        /// <summary>The acres the overlays tint at all: land that can be farmed, built on or sold (not roads, the river, hills, the highway or a closed parcel).</summary>
        public static bool IsLand(Simulation sim, Tile t)
        {
            switch (t.Terrain)
            {
                case Terrain.Farm: case Terrain.Yard: case Terrain.Quarters: case Terrain.Village: case Terrain.Commons: case Terrain.Bank: case Terrain.Cattle:
                    return !LandRules.Locked(sim, t.ParcelId);
                default:
                    return false;
            }
        }

        private static int StepOf(Fixed v, Fixed[] edges)
        {
            int n = 0;
            foreach (Fixed e in edges) if (v >= e) n++;
            return n;
        }

        private static Fixed[] LandValueEdges(Simulation sim)
        {
            return new[] { Fixed.One, Fixed.FromRatio(110, 100), sim.Config.Game.Township.Upgrade.PuccaMinLandValue, Fixed.FromRatio(125, 100) };
        }

        public static OverlayVm Build(ViewContext ctx, OverlayKind kind)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            LandState land = sim.State.Land;
            int n = land.Tiles.Count;
            var vm = new OverlayVm { Kind = kind, Title = s.Get("overlay." + Key(kind) + ".title"), Note = kind == OverlayKind.LandValue ? null : s.Get("overlay." + Key(kind) + ".note"), Steps = new int[n], Smoke = new bool[n] };
            for (int i = 0; i < n; i++) vm.Steps[i] = OverlayVm.NoStep;
            switch (kind)
            {
                case OverlayKind.Fertility:
                    foreach (Tile t in land.Tiles) if (t.Terrain == Terrain.Farm && !LandRules.Locked(sim, t.ParcelId)) vm.Steps[t.Id] = StepOf(t.Fertility, FertilityEdges);
                    Ranges(ctx, vm, FertilityEdges);
                    break;
                case OverlayKind.LandValue:
                    Fixed[] lv = LandValueEdges(sim);
                    foreach (Tile t in land.Tiles) if (IsLand(sim, t)) vm.Steps[t.Id] = StepOf(LandValueRules.Of(sim, t.Id), lv);
                    vm.Labels.Add(s.Get("overlay.landValue.under", "n", ctx.Fmt.Decimal2(lv[0])));
                    for (int i = 0; i < 3; i++) vm.Labels.Add(s.Get("overlay.landValue.range", "a", ctx.Fmt.Decimal2(lv[i]), "b", ctx.Fmt.Decimal2(lv[i + 1] - Fixed.FromRatio(1, 100))));
                    vm.Labels.Add(s.Get("overlay.landValue.over", "n", ctx.Fmt.Decimal2(lv[3])));
                    vm.Note = s.Get("overlay.landValue.note", "edge", ctx.Fmt.Decimal2(lv[2]));
                    break;
                case OverlayKind.Coverage:
                    foreach (Tile t in land.Tiles) if (IsLand(sim, t)) vm.Steps[t.Id] = Services(sim, t.Id).Count;
                    foreach (string step in CoverageSteps) vm.Labels.Add(s.Get(step));
                    foreach (SmokeRules.Source src in SmokeRules.Sources(sim))
                        foreach (int t in SmokeRules.Footprint(sim, src.Tile, src.Def)) vm.Smoke[t] = true;
                    break;
                case OverlayKind.Crime: // one grey step on every acre of land, with no number yet
                    foreach (Tile t in land.Tiles) if (IsLand(sim, t)) vm.Steps[t.Id] = 0;
                    break;
                case OverlayKind.Happiness:
                    var sum = new Fixed[n];
                    var count = new int[n];
                    foreach (Household x in sim.State.Citizens.Households)
                    {
                        Building home = land.Buildings.Find(b => b.Id == x.HomeId);
                        if (home == null || home.TileId < 0) continue;
                        sum[home.TileId] += x.Happiness;
                        count[home.TileId]++;
                    }
                    for (int i = 0; i < n; i++) if (count[i] > 0) vm.Steps[i] = StepOf(sum[i] / Fixed.FromInt(count[i]), HappinessEdges);
                    Ranges(ctx, vm, HappinessEdges);
                    break;
            }
            return vm;
        }

        /// <summary>"under 20", "20–39", "40–59", "60–79", "80+" for edges 20, 40, 60, 80.</summary>
        private static void Ranges(ViewContext ctx, OverlayVm vm, Fixed[] edges)
        {
            var s = ctx.Strings;
            vm.Labels.Add(s.Get("overlay.under", "n", ctx.Fmt.Integer(edges[0].RoundToLong())));
            for (int i = 0; i < edges.Length - 1; i++) vm.Labels.Add(s.Get("overlay.range", "a", ctx.Fmt.Integer(edges[i].RoundToLong()), "b", ctx.Fmt.Integer(edges[i + 1].RoundToLong() - 1)));
            vm.Labels.Add(s.Get("overlay.over", "n", ctx.Fmt.Integer(edges[edges.Length - 1].RoundToLong())));
        }

        /// <summary>The four services at an acre: the school, the clinic, the watchman and clean water (spec 024 §3.8). The same rules the land value and the households use.</summary>
        public static CoverageAt Services(Simulation sim, int tile)
        {
            var c = new CoverageAt();
            SchoolDef school = SchoolRules.Def(sim);
            if (school != null)
            {
                c.SchoolM = CoverageRules.Metres(sim, tile, school.Tile);
                c.SchoolReach = school.Reach;
                c.School = SchoolRules.IsOpen(sim) && c.SchoolM <= school.Reach;
            }
            ClinicDef clinic = ClinicRules.Def(sim);
            if (clinic != null)
            {
                c.ClinicM = CoverageRules.Metres(sim, tile, clinic.Tile);
                c.ClinicReach = clinic.Reach;
                c.Clinic = ClinicRules.IsOpen(sim) && c.ClinicM <= clinic.Reach;
            }
            if (CivicRules.On(sim))
            {
                CivicDef w = CivicRules.Def(sim, CivicDef.Watchman);
                c.WatchmanM = CoverageRules.Metres(sim, tile, w.Tile);
                c.WatchmanReach = w.Reach;
                c.Watchman = CivicRules.Staffed(sim, CivicDef.Watchman) && CivicRules.Covered(sim, CivicDef.Watchman, tile);
            }
            LandValueDef d = LandValueRules.Def(sim);
            if (CoverageRules.Within(sim, tile, d.WaterTile, d.WaterReach)) c.WaterFrom = "pond";
            else if (LandValueRules.PondOrWell(sim, tile)) c.WaterFrom = "well";
            else if (UtilityRules.HasWater(sim, tile)) c.WaterFrom = "tower";
            c.Water = c.WaterFrom != null;
            return c;
        }

        private static List<Household> HouseholdsOn(Simulation sim, int tile)
        {
            var list = new List<Household>();
            foreach (Household x in sim.State.Citizens.Households)
            {
                Building home = sim.State.Land.Buildings.Find(b => b.Id == x.HomeId);
                if (home != null && home.TileId == tile) list.Add(x);
            }
            return list;
        }

        private static Fixed Mean(List<Household> hh)
        {
            Fixed sum = Fixed.Zero;
            foreach (Household x in hh) sum += x.Happiness;
            return sum / Fixed.FromInt(hh.Count);
        }

        /// <summary>The card a tap opens on an acre while <paramref name="kind"/> is up: its number, a line in words, and where the ? goes.</summary>
        public static OverlayCardVm Card(ViewContext ctx, OverlayKind kind, int tile)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Tile t = sim.State.Land.Tiles[tile];
            string key = Key(kind);
            var card = new OverlayCardVm { Tile = tile, Title = s.Get("overlay." + key + ".card", "n", ctx.Fmt.Integer(tile)), OpenLabel = s.Get("overlay.open"), Line = s.Get("overlay." + key + ".none") };
            switch (kind)
            {
                case OverlayKind.Crime:
                    card.Value = s.Get("overlay.crime.value");
                    break;
                case OverlayKind.Fertility:
                    if (t.Terrain != Terrain.Farm || LandRules.Locked(sim, t.ParcelId)) break;
                    card.Value = ctx.Fmt.Integer(t.Fertility.RoundToLong());
                    card.Line = s.Get("overlay.fertility.line", "soil", s.Get(sim.Config.Game.Soils[t.Soil].NameKey));
                    card.WhyKey = "fertility"; card.WhyId = tile;
                    break;
                case OverlayKind.LandValue:
                    if (!IsLand(sim, t)) break;
                    Fixed lv = LandValueRules.Of(sim, tile), edge = sim.Config.Game.Township.Upgrade.PuccaMinLandValue;
                    card.Value = LandValueViews.Mult(ctx, lv);
                    card.Line = s.Get(lv >= edge ? "overlay.landValue.lineHigh" : "overlay.landValue.lineLow", "edge", ctx.Fmt.Decimal2(edge));
                    card.WhyKey = "landValue"; card.WhyId = tile;
                    break;
                case OverlayKind.Coverage:
                    if (!IsLand(sim, t)) break;
                    CoverageAt c = Services(sim, tile);
                    card.Value = s.Get("overlay.coverage.value", "n", ctx.Fmt.Integer(c.Count));
                    card.Line = CoverageLine(ctx, c, tile);
                    card.WhyKey = CoverageWhyKey; card.WhyId = tile;
                    break;
                case OverlayKind.Happiness:
                    List<Household> hh = HouseholdsOn(sim, tile);
                    if (hh.Count == 0) break;
                    card.Value = ctx.Fmt.Integer(Mean(hh).RoundToLong());
                    card.Line = hh.Count == 1 ? s.Get("overlay.happiness.lineOne", "name", TownWords.Household(ctx, hh[0])) : s.Get("overlay.happiness.lineMany", "n", ctx.Fmt.Integer(hh.Count));
                    card.WhyKey = HappinessWhyKey; card.WhyId = tile;
                    break;
            }
            return card;
        }

        private static string CoverageLine(ViewContext ctx, CoverageAt c, int tile)
        {
            var s = ctx.Strings;
            var yes = new List<string>();
            var no = new List<string>();
            (c.School ? yes : no).Add(s.Get("overlay.coverage.school"));
            (c.Clinic ? yes : no).Add(s.Get("overlay.coverage.clinic"));
            (c.Watchman ? yes : no).Add(s.Get("overlay.coverage.watchman"));
            (c.Water ? yes : no).Add(s.Get("overlay.coverage.water"));
            string line = yes.Count == 0 ? s.Get("overlay.coverage.lineNone") : no.Count == 0 ? s.Get("overlay.coverage.lineAll") : s.Get("overlay.coverage.line", "yes", string.Join(", ", yes), "no", string.Join(", ", no));
            SmokeLine(ctx, tile, out string smoke);
            return smoke == null ? line : line + " " + smoke;
        }

        private static void SmokeLine(ViewContext ctx, int tile, out string line)
        {
            line = null;
            Fixed lv = SmokeRules.LandValue(ctx.Sim, tile, out int count);
            if (count == 0) return;
            line = ctx.Strings.Get("overlay.coverage.smoke", "lv", Points(ctx, lv), "health", Points(ctx, SmokeHealth(ctx.Sim, tile)));
        }

        private static Fixed SmokeHealth(Simulation sim, int tile)
        {
            Fixed total = Fixed.Zero;
            foreach (SmokeRules.Source src in SmokeRules.Reaching(sim, tile)) total += src.Def.Health;
            return total;
        }

        private static string Points(ViewContext ctx, Fixed share) { return ctx.Fmt.Integer((share * Fixed.FromInt(100)).RoundToLong()); }

        // ---- the Why? of a card's number ----

        /// <summary>Why? of the coverage overlay: each service with Yes or No and its distance, then the smoke with its cost (words, never a factor: ADR 0043).</summary>
        internal static WhyVm CoverageWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            int tile = (int)id;
            CoverageAt c = Services(sim, tile);
            var vm = new WhyVm { Title = s.Get("why.coverage.title"), Value = s.Get("overlay.coverage.value", "n", ctx.Fmt.Integer(c.Count)) };
            vm.Rows.Add(new WhyRowVm(s.Get("overlay.coverage.school"), Reach(ctx, c.School, c.SchoolM, c.SchoolReach, SchoolRules.Def(sim) != null && SchoolRules.IsOpen(sim))));
            vm.Rows.Add(new WhyRowVm(s.Get("overlay.coverage.clinic"), Reach(ctx, c.Clinic, c.ClinicM, c.ClinicReach, ClinicRules.Def(sim) != null && ClinicRules.IsOpen(sim))));
            vm.Rows.Add(new WhyRowVm(s.Get("overlay.coverage.watchman"), Reach(ctx, c.Watchman, c.WatchmanM, c.WatchmanReach, CivicRules.Staffed(sim, CivicDef.Watchman))));
            vm.Rows.Add(new WhyRowVm(s.Get("overlay.coverage.water"), c.Water ? s.Get("why.coverage.waterYes", "from", s.Get("why.coverage.from." + c.WaterFrom)) : s.Get("why.coverage.waterNo")));
            Fixed lv = SmokeRules.LandValue(sim, tile, out int smokers);
            if (smokers > 0) vm.Rows.Add(new WhyRowVm(s.Get("why.coverage.smoke"), s.Get("why.coverage.smokeValue", "lv", Points(ctx, lv), "health", Points(ctx, SmokeHealth(sim, tile)))));
            vm.Rows.Add(new WhyRowVm(s.Get("why.coverage.how"), null));
            vm.Footnote = s.Get("why.coverage.lesson");
            return vm;
        }

        private static string Reach(ViewContext ctx, bool covered, int metres, int reach, bool open)
        {
            var s = ctx.Strings;
            if (!open) return s.Get("why.coverage.notOpen");
            return covered ? s.Get("why.coverage.yes", "m", ctx.Fmt.Integer(metres), "reach", ctx.Fmt.Integer(reach)) : s.Get("why.coverage.no", "m", ctx.Fmt.Integer(metres), "reach", ctx.Fmt.Integer(reach));
        }

        /// <summary>Why? of an acre's happiness on the overlay: each household there with its happiness, and the mean.</summary>
        internal static WhyVm HappinessWhy(ViewContext ctx, long id)
        {
            var s = ctx.Strings;
            List<Household> hh = HouseholdsOn(ctx.Sim, (int)id);
            var vm = new WhyVm { Title = s.Get("why.acreHappiness.title"), Value = hh.Count == 0 ? s.Get("why.acreHappiness.none") : ctx.Fmt.Integer(Mean(hh).RoundToLong()) };
            foreach (Household x in hh) vm.Rows.Add(new WhyRowVm(TownWords.Household(ctx, x), ctx.Fmt.Integer(x.Happiness.RoundToLong())));
            vm.Rows.Add(new WhyRowVm(s.Get("why.acreHappiness.how"), null));
            vm.Footnote = s.Get("why.acreHappiness.lesson");
            return vm;
        }
    }
}
