using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>Names of your people and their homes (spec 014 §7): "Manoj's family", "Family house 2", a portrait, an age and stage.</summary>
    public static class TownWords
    {
        public static string Name(ViewContext ctx, Citizen c) { return PeopleWords.Name(ctx, c.Name); }

        /// <summary>"Manoj" for one person, "Manoj's family" for more: named after its first member.</summary>
        public static string Household(ViewContext ctx, Household x)
        {
            Citizen head = ctx.Sim.State.Citizens.Citizen(x.Members[0]);
            return x.Members.Count == 1 ? Name(ctx, head) : ctx.Strings.Get("town.family", "name", Name(ctx, head));
        }

        /// <summary>Who a home's people are, for a *Lives in* row: "alone" for one person, else the household's name.</summary>
        public static string LivesWith(ViewContext ctx, Household x)
        {
            return x.Members.Count == 1 ? ctx.Strings.Get("hand.homeAlone") : Household(ctx, x);
        }

        /// <summary>"Hut 1" or "Family house 2": numbered as the People sheet numbers them, in building order.</summary>
        public static string Home(ViewContext ctx, long buildingId)
        {
            Simulation sim = ctx.Sim;
            Building home = sim.State.Land.Buildings.Find(b => b.Id == buildingId);
            if (home == null) return ctx.Strings.Get("town.noHome");
            bool house = HouseRules.IsHouse(sim, home);
            int n = 0;
            foreach (Building b in sim.State.Land.Buildings)
            {
                if (house ? !HouseRules.IsHouse(sim, b) : !PeopleRules.IsHandHut(sim, b)) continue;
                n++;
                if (b.Id == buildingId) break;
            }
            return ctx.Strings.Get(house ? "town.houseN" : "people.hutN", "n", ctx.Fmt.Integer(n));
        }

        /// <summary>A card portrait: a hand's own, else by stage and gender (the map's looks, ADR 0067).</summary>
        public static string Portrait(ViewContext ctx, Citizen c)
        {
            if (c.Job == CitizenJob.Hand) return c.Female ? "portrait_hand_f" : "portrait_hand_m";
            return "portrait_" + MapView.Look(ctx.Sim, c);
        }

        /// <summary>"child", "adult" or "elder".</summary>
        public static string Stage(ViewContext ctx, Citizen c)
        {
            return ctx.Strings.Get(HouseholdRules.IsChild(ctx.Sim, c) ? "town.child" : HouseholdRules.IsElder(ctx.Sim, c) ? "town.elder" : "town.adult");
        }

        /// <summary>"Your hand", "Works outside", "Retired", "At home" (a child or an elder).</summary>
        public static string Job(ViewContext ctx, Citizen c)
        {
            Simulation sim = ctx.Sim;
            if (c.Job == CitizenJob.Hand) return ctx.Strings.Get("town.job.hand");
            if (c.Job == CitizenJob.Retired) return ctx.Strings.Get("town.job.retired");
            if (HouseholdRules.WorksOutside(sim, c)) return ctx.Strings.Get(c.Labouring(ctx.Now) ? "town.job.labouring" : "town.job.outside");
            return ctx.Strings.Get(HouseholdRules.IsChild(sim, c) ? "town.job.child" : "town.job.home");
        }

        /// <summary>"Manoj (38)".</summary>
        public static string NameAge(ViewContext ctx, Citizen c)
        {
            return ctx.Strings.Get("town.nameAge", "name", Name(ctx, c), "age", ctx.Fmt.Integer(CitizenRules.Age(ctx.Sim, c)));
        }

        /// <summary>A bride who married into the town (spec 014 §3.3, ADR 0070): her parents are unknown, her home village is shown.</summary>
        public static bool MarriedIn(Citizen c) { return c.Village >= 0 && c.Father == 0 && c.Mother == 0 && c.Story.Exists(l => l.Kind == StoryKind.Married); }

        /// <summary>"Usha (35, from Sitapur)" for a bride who married in, else "Usha (35)".</summary>
        public static string NameAgeFrom(ViewContext ctx, Citizen c)
        {
            if (!MarriedIn(c)) return NameAge(ctx, c);
            return ctx.Strings.Get("town.nameAgeFrom", "name", Name(ctx, c), "age", ctx.Fmt.Integer(CitizenRules.Age(ctx.Sim, c)), "village", Village(ctx, c));
        }

        public static string Village(ViewContext ctx, Citizen c)
        {
            return c.Village < 0 ? null : ctx.Strings.Get("village." + ctx.Sim.Config.Game.Population.Villages[c.Village]);
        }
    }

    /// <summary>
    /// Births, marriages and deaths in words (spec 014 §3.9, ADR 0070): gentle, as information, for the away card and the Town tab's
    /// "This season". Never a push or a sound.
    /// </summary>
    public static class TownNews
    {
        /// <summary>"A baby girl, Asha, was born to Manoj's family".</summary>
        public static string Born(ViewContext ctx, int name, bool female, long householdId)
        {
            Household x = ctx.Sim.State.Citizens.Household(householdId);
            return ctx.Strings.Get(female ? "news.bornGirl" : "news.bornBoy", "name", PeopleWords.Name(ctx, name),
                "family", x == null ? ctx.Strings.Get("away.aFamily") : TownWords.Household(ctx, x));
        }

        /// <summary>"Ravi married Sita from Sitapur" (and where they live, when they moved into a house of their own), or "Asha married and moved to Rampur".</summary>
        public static string Married(ViewContext ctx, int name, bool movedAway, long spouseId, int village, long newHouseholdId)
        {
            var s = ctx.Strings;
            string place = s.Get("village." + ctx.Sim.Config.Game.Population.Villages[village]);
            if (movedAway) return s.Get("news.marriedAway", "name", PeopleWords.Name(ctx, name), "village", place);
            Citizen spouse = ctx.Sim.State.Citizens.Citizen(spouseId);
            Household home = newHouseholdId == 0 ? null : ctx.Sim.State.Citizens.Household(newHouseholdId);
            return s.Get(home == null ? "news.married" : "news.marriedHouse", "name", PeopleWords.Name(ctx, name),
                "spouse", spouse == null ? "" : TownWords.Name(ctx, spouse), "village", place, "home", home == null ? "" : TownWords.Home(ctx, home.HomeId));
        }

        /// <summary>"Kamla (77), Manoj's mother, passed away peacefully. Her family mourns this season."</summary>
        public static string Died(ViewContext ctx, long citizenId, int name, bool female, int age)
        {
            var s = ctx.Strings;
            Citizen child = ctx.Sim.State.Citizens.Citizens.Find(c => c.Living && (c.Father == citizenId || c.Mother == citizenId));
            string who = PeopleWords.Name(ctx, name) + " (" + ctx.Fmt.Integer(age) + ")";
            if (child != null) who = s.Get("news.relation", "who", who, "name", TownWords.Name(ctx, child), "relation", s.Get(female ? "kin.mother" : "kin.father"));
            return s.Get(female ? "news.diedF" : "news.diedM", "who", who);
        }

        /// <summary>This season's births, marriages and deaths, from the story lines of those still on record (the Town tab).</summary>
        public static List<string> ThisSeason(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            long season = CitizenRules.SeasonOf(sim, ctx.Now);
            var lines = new List<string>();
            foreach (Citizen c in sim.State.Citizens.Citizens)
                foreach (StoryLine l in c.Story)
                {
                    if (CitizenRules.SeasonOf(sim, l.At) != season) continue;
                    if (l.Kind == StoryKind.Born) lines.Add(Born(ctx, c.Name, c.Female, c.HouseholdId));
                    else if (l.Kind == StoryKind.MarriedAway) lines.Add(Married(ctx, c.Name, true, 0, c.Village, 0));
                    else if (l.Kind == StoryKind.Married && !c.Female)
                    {
                        Citizen bride = sim.State.Citizens.Citizen(l.Other);
                        if (bride != null && bride.Village >= 0) lines.Add(Married(ctx, c.Name, false, bride.Id, bride.Village, 0));
                    }
                    else if (l.Kind == StoryKind.Died) lines.Add(Died(ctx, c.Id, c.Name, c.Female, CitizenRules.Age(sim, c)));
                }
            return lines;
        }
    }

    /// <summary>
    /// Who is family to whom (spec 014 §7): parents, spouse, siblings, children and grandchildren, found from the parents' and spouse's
    /// ids. Never a whole-town tree.
    /// </summary>
    public static class Kin
    {
        public static bool IsChildOf(Citizen child, long parent) { return parent != 0 && (child.Father == parent || child.Mother == parent); }

        public static bool Siblings(Citizen a, Citizen b)
        {
            return a.Id != b.Id && ((a.Father != 0 && a.Father == b.Father) || (a.Mother != 0 && a.Mother == b.Mother));
        }

        /// <summary>Close family in the order the card shows them, each with its relation key ("father", "son"…).</summary>
        public static List<KeyValuePair<string, Citizen>> Close(Simulation sim, Citizen c)
        {
            var list = new List<KeyValuePair<string, Citizen>>();
            List<Citizen> all = sim.State.Citizens.Citizens;
            void Add(string relation, Citizen x) { if (x != null && x.Id != c.Id && !list.Exists(p => p.Value.Id == x.Id)) list.Add(new KeyValuePair<string, Citizen>(relation, x)); }
            Add("father", sim.State.Citizens.Citizen(c.Father));
            Add("mother", sim.State.Citizens.Citizen(c.Mother));
            Citizen spouse = sim.State.Citizens.Citizen(c.Spouse);
            if (spouse != null) Add(spouse.Female ? "wife" : "husband", spouse);
            foreach (Citizen x in all) if (Siblings(c, x)) Add(x.Female ? "sister" : "brother", x);
            foreach (Citizen x in all) if (IsChildOf(x, c.Id)) Add(x.Female ? "daughter" : "son", x);
            foreach (Citizen x in all)
                if (sim.State.Citizens.Citizen(x.Father) is Citizen f && IsChildOf(f, c.Id) || sim.State.Citizens.Citizen(x.Mother) is Citizen m && IsChildOf(m, c.Id))
                    Add(x.Female ? "granddaughter" : "grandson", x);
            return list;
        }

        /// <summary>Other households a household is family to, by marriage or birth (the Town tab's "related").</summary>
        public static List<Household> Related(Simulation sim, Household x)
        {
            var related = new List<Household>();
            foreach (long id in x.Members)
                foreach (KeyValuePair<string, Citizen> k in Close(sim, sim.State.Citizens.Citizen(id)))
                {
                    if (!k.Value.Living || k.Value.HouseholdId == x.Id) continue;
                    Household other = sim.State.Citizens.Household(k.Value.HouseholdId);
                    if (other != null && !related.Contains(other)) related.Add(other);
                }
            return related;
        }
    }

    /// <summary>
    /// The People sheet's Town tab (spec 014 §7): how many live here and how happy, who works in town, one row per household (sortable),
    /// and Build a family house.
    /// </summary>
    public static class TownSheet
    {
        public const string ByHappiness = "happiness", BySize = "size", ByJob = "job";

        /// <summary>Workers and Town, over both tabs of the People sheet.</summary>
        internal static void Tabs(ViewContext ctx, SheetVm vm, string tab)
        {
            if (ctx.Sim.State.Citizens == null || ctx.Sim.Config.Game.Population == null) return;
            vm.Tab = tab;
            vm.Tabs = new List<ActionVm>
            {
                ActionVm.Open("tab:workers", ctx.Strings.Get("town.tabWorkers"), new SheetRequest("People", 0)),
                ActionVm.Open("tab:town", ctx.Strings.Get("town.tabTown"), new SheetRequest("Town", 0)),
            };
        }

        public static SheetVm Build(ViewContext ctx, string sort = null)
        {
            Simulation sim = ctx.Sim;
            CitizensState town = sim.State.Citizens;
            var s = ctx.Strings;
            sort = sort ?? ByHappiness;
            var vm = new SheetVm { Icon = "population", Title = s.Get("people.title") };
            Tabs(ctx, vm, "tab:town");
            int households = town.Households.Count;
            vm.Subtitle = s.Get("town.subtitle", "people", s.Plural("town.people", town.Population), "households", s.Plural("town.households", households));
            if (households > 0)
            {
                vm.Lines.Add(new LineVm(s.Get("town.happiness"), ctx.Fmt.Integer(HappinessRules.Town(sim).RoundToLong()), "townHappiness") { Icon = "happiness" });
                int inTown = 0, adults = 0;
                foreach (Household x in town.Households)
                {
                    HappinessRules.Jobs(sim, x, out int i, out int a);
                    inTown += i;
                    adults += a;
                }
                vm.Lines.Add(new LineVm(s.Get("town.work"), s.Get("town.workValue", "n", ctx.Fmt.Integer(inTown), "of", ctx.Fmt.Integer(adults)), "employment"));
            }

            vm.Lines.Add(new LineVm(null, null) { Buttons = new List<ActionVm> { BuildButton(ctx) } });

            List<string> news = TownNews.ThisSeason(ctx);
            if (news.Count > 0)
            {
                vm.Lines.Add(LineVm.Header(s.Get("town.thisSeason")));
                foreach (string n in news) vm.Lines.Add(new LineVm(n, null) { Icon = "population" });
            }

            // Households, then the empty houses and the ones being built.
            vm.Lines.Add(LineVm.Header(s.Get("town.householdsHeader")));
            if (households == 0) vm.Lines.Add(new LineVm(s.Get("town.nobody"), s.Get("town.nobodyValue")));
            else
            {
                string next = sort == ByHappiness ? BySize : sort == BySize ? ByJob : ByHappiness;
                var order = new LineVm(s.Get("town.sorted." + sort), null);
                order.Action = ActionVm.Open("sort:" + next, s.Get("town.sortBy." + next), new SheetRequest("Town", 0, next));
                vm.Lines.Add(order);
            }
            foreach (Household x in Sorted(sim, sort)) vm.Lines.Add(Row(ctx, x));
            foreach (Building b in sim.State.Land.Buildings)
            {
                if (!HouseRules.IsHouse(sim, b) || town.Households.Exists(x => x.HomeId == b.Id)) continue;
                vm.Lines.Add(new LineVm(TownWords.Home(ctx, b.Id), s.Get("town.houseEmpty")) { Icon = "family_house", Note = s.Get("town.houseEmptyNote") });
            }
            foreach (Construction c in town.Constructions)
                vm.Lines.Add(new LineVm(s.Get("town.houseBuilding"), s.Get("people.hutReadyIn", "time", ctx.Fmt.Duration(c.ReadyAt - ctx.Now))) { Icon = "family_house_building" });
            return vm;
        }

        /// <summary>The picture button: Build a family house (60,000), greyed with the reason before the Hamlet or without the money.</summary>
        internal static ActionVm BuildButton(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long cost = HouseCost(sim);
            bool unlocked = HouseRules.Unlocked(sim), cash = sim.State.Ledger.CanAfford(cost);
            string reason = !unlocked ? s.Get("town.houseLocked") : !cash ? ctx.Reject(RejectReason.InsufficientFunds) : null;
            var a = new ActionVm("buildHouse", s.Get("town.buildHouse"), s.Get("town.buildHouseDetail", "cost", ctx.Fmt.Coins(cost), "rent", ctx.Fmt.Coins(MarketRules.Indexed(sim, sim.Config.Game.Population.RentPerSeason))),
                ActionKind.Open, reason == null, reason, null, new SheetRequest("HousePlacement", 0));
            a.Icon = "family_house";
            return a;
        }

        public static long HouseCost(Simulation sim) { return MarketRules.Indexed(sim, sim.Config.Game.Buildings[sim.Config.Game.Population.House].Value); }

        private static List<Household> Sorted(Simulation sim, string sort)
        {
            var list = new List<Household>(sim.State.Citizens.Households);
            // Least happy first (who needs you), the biggest first, or the most at work in town first; ties by id.
            System.Comparison<Household> by = sort == BySize ? (a, b) => b.Members.Count.CompareTo(a.Members.Count)
                : sort == ByJob ? (a, b) => HappinessRules.Job(sim, b).CompareTo(HappinessRules.Job(sim, a))
                : (System.Comparison<Household>)((a, b) => a.Happiness.CompareTo(b.Happiness));
            list.Sort((a, b) => by(a, b) != 0 ? by(a, b) : a.Id.CompareTo(b.Id));
            return list;
        }

        private static LineVm Row(ViewContext ctx, Household x)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Citizen head = sim.State.Citizens.Citizen(x.Members[0]);
            string label = x.Members.Count == 1 ? TownWords.Household(ctx, x) : s.Get("town.familyN", "name", TownWords.Household(ctx, x), "n", ctx.Fmt.Integer(x.Members.Count));
            var note = new List<string> { s.Get("town.rowHappiness", "n", ctx.Fmt.Integer(x.Happiness.RoundToLong())) };
            if (x.RentBehind > 0) note.Add(s.Plural("needs.rentBehind", x.RentBehind));
            var related = new List<string>();
            foreach (Household r in Kin.Related(sim, x)) related.Add(TownWords.Household(ctx, r));
            if (related.Count > 0) note.Add(s.Get("town.related", "list", string.Join(", ", related)));
            var line = new LineVm(label, s.Get("town.rowValue", "home", TownWords.Home(ctx, x.HomeId), "income", ctx.Fmt.Coins(x.Income)), "household.income", x.Id)
            {
                Icon = TownWords.Portrait(ctx, head), Note = string.Join(" · ", note),
                NoteTone = x.Happiness >= Fixed.FromInt(ctx.Ui.HappyAbove) && x.RentBehind == 0 ? Tone.Good : Tone.Warn,
                Opens = new SheetRequest("Household", x.Id),
            };
            line.Action = ActionVm.Open("household:" + x.Id, s.Get("action.see"), new SheetRequest("Household", x.Id));
            return line;
        }
    }

    /// <summary>
    /// A household's card (spec 014 §7): its family as a small tree, each member, the season's budget as a pie with Engel's Why?, savings,
    /// rent, and its needs with what would help.
    /// </summary>
    public static class HouseholdSheet
    {
        public static SheetVm Build(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Household x = sim.State.Citizens?.Household(id);
            var s = ctx.Strings;
            if (x == null) return new SheetVm { Icon = "population", Title = s.Get("town.gone") };
            var vm = new SheetVm
            {
                Icon = TownWords.Portrait(ctx, sim.State.Citizens.Citizen(x.Members[0])), Title = TownWords.Household(ctx, x),
                Subtitle = s.Get("household.subtitle", "home", TownWords.Home(ctx, x.HomeId), "people", s.Plural("town.people", x.Members.Count)),
            };
            if (x.Members.Count > 1) vm.Lines.Add(new LineVm(null, null) { Tree = Tree(ctx, x) });

            vm.Lines.Add(LineVm.Header(s.Get("household.members")));
            foreach (long m in x.Members)
            {
                Citizen c = sim.State.Citizens.Citizen(m);
                var line = new LineVm(TownWords.Name(ctx, c), s.Get("household.memberValue", "age", ctx.Fmt.Integer(CitizenRules.Age(sim, c)), "stage", TownWords.Stage(ctx, c)), "citizen.age", c.Id)
                    { Icon = TownWords.Portrait(ctx, c), Note = TownWords.Job(ctx, c), Opens = new SheetRequest("Citizen", c.Id) };
                line.Action = ActionVm.Open("citizen:" + c.Id, s.Get("action.see"), new SheetRequest("Citizen", c.Id));
                vm.Lines.Add(line);
            }

            vm.Lines.Add(LineVm.Header(s.Get("household.money")));
            vm.Lines.Add(new LineVm(s.Get("household.income"), ctx.Fmt.Coins(x.Income), "household.income", x.Id));
            vm.Lines.Add(new LineVm(s.Get("household.budget"), s.Get("household.budgetValue", "pct", ctx.Fmt.Percent(Share(x.Food, x.Income))), "household.budget", x.Id) { Pie = Pie(ctx, x) });
            vm.Lines.Add(new LineVm(s.Get("household.savings"), ctx.Fmt.Coins(x.Savings), "household.savings", x.Id));
            vm.Lines.Add(new LineVm(s.Get("household.rent"), RentText(ctx, x), "household.rent", x.Id));

            vm.Lines.Add(LineVm.Header(s.Get("household.needs")));
            vm.Lines.Add(new LineVm(s.Get("household.happiness"), ctx.Fmt.Integer(x.Happiness.RoundToLong()), "household.happiness", x.Id) { Icon = "happiness" });
            vm.Lines.AddRange(NeedsView.Rows(ctx, x));
            return vm;
        }

        private static Fixed Share(long part, long whole) { return whole <= 0 ? Fixed.Zero : Fixed.FromRatio(part, whole); }

        internal static string RentText(ViewContext ctx, Household x)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            if (HouseholdRules.InHut(sim, x)) return s.Get("household.rentHut");
            if (x.Rent == 0) return s.Get("household.rentInKind");
            return x.RentBehind > 0 ? s.Plural("needs.rentBehind", x.RentBehind) : s.Get("household.rentPaid", "rent", ctx.Fmt.Coins(x.Rent));
        }

        /// <summary>The season's budget (§3.5) as slices: food, rent, clothing, health, schooling, leisure, saved; empty ones left out.</summary>
        internal static List<PieSliceVm> Pie(ViewContext ctx, Household x)
        {
            var slices = new List<PieSliceVm>();
            long total = x.Food + x.Rent + x.Clothing + x.HealthCare + x.Schooling + x.Leisure + x.Saved;
            void Add(string key, long amount)
            {
                if (amount <= 0 || total <= 0) return;
                slices.Add(new PieSliceVm(ctx.Strings.Get("budget." + key), (int)((amount * 200 + total) / (total * 2)), ctx.Fmt.Coins(amount), key)); // rounded, as the Budget row
            }
            Add("food", x.Food);
            Add("rent", x.Rent);
            Add("clothing", x.Clothing);
            Add("health", x.HealthCare);
            Add("schooling", x.Schooling);
            Add("leisure", x.Leisure);
            Add("saved", x.Saved);
            return slices;
        }

        /// <summary>The family by generation: whoever has a parent here sits below them, a spouse beside, oldest first in a row.</summary>
        internal static FamilyTreeVm Tree(ViewContext ctx, Household x)
        {
            Simulation sim = ctx.Sim;
            var members = new List<Citizen>();
            foreach (long m in x.Members) members.Add(sim.State.Citizens.Citizen(m));
            var depth = new Dictionary<long, int>(); // keyed lookups only
            int Depth(Citizen c)
            {
                if (depth.TryGetValue(c.Id, out int d)) return d;
                depth[c.Id] = 0; // a guard against a loop in bad data
                Citizen parent = members.Find(p => Kin.IsChildOf(c, p.Id));
                Citizen spouse = parent == null ? members.Find(p => p.Id == c.Spouse) : null;
                d = parent != null ? Depth(parent) + 1 : spouse != null && members.Exists(p => Kin.IsChildOf(spouse, p.Id)) ? Depth(spouse) : 0;
                depth[c.Id] = d;
                return d;
            }
            var tree = new FamilyTreeVm();
            var sorted = new List<Citizen>(members);
            sorted.Sort((a, b) => a.BornSeason != b.BornSeason ? a.BornSeason.CompareTo(b.BornSeason) : a.Id.CompareTo(b.Id));
            foreach (Citizen c in sorted)
            {
                int d = Depth(c);
                while (tree.Generations.Count <= d) tree.Generations.Add(new List<TreePersonVm>());
                List<TreePersonVm> row = tree.Generations[d];
                if (row.Exists(p => p.CitizenId == c.Id)) continue;
                row.Add(new TreePersonVm(TownWords.NameAgeFrom(ctx, c), TownWords.Portrait(ctx, c), c.Id));
                Citizen spouse = members.Find(p => p.Id == c.Spouse);
                if (spouse != null && !row.Exists(p => p.CitizenId == spouse.Id))
                    row.Add(new TreePersonVm(TownWords.NameAgeFrom(ctx, spouse), TownWords.Portrait(ctx, spouse), spouse.Id) { SpouseOfPrevious = true });
            }
            tree.Generations.RemoveAll(g => g.Count == 0);
            return tree;
        }
    }

    /// <summary>
    /// A citizen's card (spec 014 §7): portrait, age and stage, job, skill, traits, health, story, and close family with links; the
    /// departed fold into one line.
    /// </summary>
    public static class CitizenSheet
    {
        public static SheetVm Build(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Citizen c = sim.State.Citizens?.Citizen(id);
            if (c == null) return new SheetVm { Icon = "population", Title = s.Get("town.gone") };
            int age = CitizenRules.Age(sim, c);
            var vm = new SheetVm { Icon = TownWords.Portrait(ctx, c), Title = TownWords.Name(ctx, c), Subtitle = Gone(ctx, c) ?? TownWords.Job(ctx, c) };
            vm.Lines.Add(new LineVm(s.Get("citizen.age"), s.Get("citizen.ageValue", "age", ctx.Fmt.Integer(age), "stage", TownWords.Stage(ctx, c)), "citizen.age", c.Id));
            if (c.Living)
            {
                Household home = sim.State.Citizens.Household(c.HouseholdId);
                if (home != null)
                    vm.Lines.Add(new LineVm(s.Get("citizen.home"), TownWords.Home(ctx, home.HomeId) + " · " + TownWords.LivesWith(ctx, home), "household.home", home.Id)
                        { Opens = new SheetRequest("Household", home.Id) });
                var job = new LineVm(s.Get("citizen.job"), TownWords.Job(ctx, c), c.Job == CitizenJob.Hand ? null : "citizen.job", c.Id);
                if (c.Job == CitizenJob.Hand) job.Action = ActionVm.Open("hand:" + c.HandId, s.Get("action.see"), new SheetRequest("Hand", c.HandId));
                vm.Lines.Add(job);
            }
            string from = TownWords.Village(ctx, c);
            if (from != null) vm.Lines.Add(new LineVm(s.Get("citizen.from"), from));
            if (!HouseholdRules.IsChild(sim, c))
            {
                int skill = (int)c.Skill.RoundToLong();
                vm.Lines.Add(new LineVm(s.Get("hand.skill"), PeopleWords.Band(ctx, skill)) { Stars = PeopleWords.Stars(ctx, skill, skill) });
                foreach (int t in new[] { c.Trait1, c.Trait2 })
                    vm.Lines.Add(new LineVm(PeopleWords.Trait(ctx, t), s.Get(sim.Config.Game.People.Traits[t].NameKey + ".why")));
            }
            vm.Lines.Add(new LineVm(s.Get("citizen.health"), ctx.Rating(c.Health), "citizen.health", c.Id));
            vm.Lines.Add(new LineVm(s.Get("citizen.school"), s.Get(c.Education > 0 ? "citizen.schoolPrimary" : "citizen.schoolNone")));

            if (c.Story.Count > 0)
            {
                vm.Lines.Add(LineVm.Header(s.Get("citizen.story")));
                foreach (StoryLine l in c.Story)
                    vm.Lines.Add(new LineVm(s.Get("story." + l.Kind, "name", TownWords.Name(ctx, c), "other", Other(ctx, l)), ReportSheet.PeriodName(ctx, CitizenRules.SeasonOf(sim, l.At)), "calendar"));
            }

            List<KeyValuePair<string, Citizen>> family = Kin.Close(sim, c);
            Household own = c.Living ? sim.State.Citizens.Household(c.HouseholdId) : null;
            bool away = c.Job == CitizenJob.Hand && own != null && HouseholdRules.InHut(sim, own); // §3.3: a hand's family stays home
            if (family.Count > 0 || away)
            {
                vm.Lines.Add(LineVm.Header(s.Get("citizen.family")));
                if (away) vm.Lines.Add(new LineVm(s.Get("citizen.familyHome"), TownWords.Village(ctx, c) ?? s.Get("citizen.elsewhere"), "hand.home", c.HandId));
                int departed = 0;
                foreach (KeyValuePair<string, Citizen> k in family)
                {
                    if (!k.Value.Living) { departed++; continue; }
                    vm.Lines.Add(Relative(ctx, c, k.Key, k.Value));
                }
                if (departed > 0)
                {
                    var fold = new LineVm(s.Plural("citizen.departed", departed), null) { Opens = new SheetRequest("Departed", c.Id) };
                    fold.Action = ActionVm.Open("departed", s.Get("action.see"), new SheetRequest("Departed", c.Id));
                    vm.Lines.Add(fold);
                }
            }
            return vm;
        }

        /// <summary>Those of a citizen's close family who have passed away or moved away, greyed (opened from the folded line).</summary>
        public static SheetVm Departed(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Citizen c = sim.State.Citizens?.Citizen(id);
            var vm = new SheetVm { Icon = "population", Title = ctx.Strings.Get("citizen.departedTitle", "name", c == null ? "" : TownWords.Name(ctx, c)) };
            if (c == null) return vm;
            foreach (KeyValuePair<string, Citizen> k in Kin.Close(sim, c))
                if (!k.Value.Living)
                    vm.Lines.Add(new LineVm(TownWords.Name(ctx, k.Value) + " · " + ctx.Strings.Get("kin." + k.Key), Gone(ctx, k.Value), "citizen.age", k.Value.Id) { Icon = TownWords.Portrait(ctx, k.Value), NoteTone = Tone.Plain });
            return vm;
        }

        /// <summary>"passed away at 70", "moved to Rampur" or "married, lives in Rampur"; null while here.</summary>
        private static string Gone(ViewContext ctx, Citizen c)
        {
            if (c.Living) return null;
            if (c.Gone == CitizenGone.Died) return ctx.Strings.Get("citizen.died", "age", ctx.Fmt.Integer(CitizenRules.Age(ctx.Sim, c)));
            return ctx.Strings.Get(c.Gone == CitizenGone.MarriedAway ? "citizen.marriedAway" : "citizen.left", "village", TownWords.Village(ctx, c) ?? ctx.Strings.Get("citizen.elsewhere"));
        }

        private static LineVm Relative(ViewContext ctx, Citizen me, string relation, Citizen k)
        {
            var s = ctx.Strings;
            string where = k.HouseholdId == me.HouseholdId ? s.Get("kin.livesHere")
                : s.Get("kin.livesIn", "home", TownWords.Home(ctx, ctx.Sim.State.Citizens.Household(k.HouseholdId)?.HomeId ?? 0));
            string value = s.Get("kin.value", "relation", s.Get("kin." + relation), "age", ctx.Fmt.Integer(CitizenRules.Age(ctx.Sim, k)));
            if (TownWords.MarriedIn(k)) value = s.Get("kin.from", "value", value, "village", TownWords.Village(ctx, k));
            var line = new LineVm(TownWords.Name(ctx, k), value, "citizen.age", k.Id)
                { Icon = TownWords.Portrait(ctx, k), Note = TownWords.Job(ctx, k) + " · " + where, Opens = new SheetRequest("Citizen", k.Id) };
            line.Action = ActionVm.Open("citizen:" + k.Id, s.Get("action.see"), new SheetRequest("Citizen", k.Id));
            return line;
        }

        private static string Other(ViewContext ctx, StoryLine l)
        {
            Citizen o = l.Other == 0 ? null : ctx.Sim.State.Citizens.Citizen(l.Other);
            return o == null ? "" : TownWords.Name(ctx, o);
        }
    }

    /// <summary>Where a family house can go (spec 014 §3.13): every owned farm acre and the quarters, lit where it may, the reason where not.</summary>
    public static class HousePlacement
    {
        public static List<HutPlaceVm> Places(ViewContext ctx)
        {
            var list = new List<HutPlaceVm>();
            Simulation sim = ctx.Sim;
            foreach (Tile t in sim.State.Land.Tiles)
            {
                if (t.Terrain != Terrain.Farm && t.Terrain != Terrain.Quarters || !sim.State.Land.Parcels[t.ParcelId].Owned) continue;
                CommandResult r = sim.Preview(new BuildFamilyHouse(ctx.Now, t.Id));
                list.Add(new HutPlaceVm { TileId = t.Id, Allowed = r.Accepted, Reason = r.Accepted ? null : Reason(ctx, t.Id, r.Reason) });
            }
            return list;
        }

        public static string ReasonFor(ViewContext ctx, int tileId)
        {
            CommandResult r = ctx.Sim.Preview(new BuildFamilyHouse(ctx.Now, tileId));
            return r.Accepted ? null : Reason(ctx, tileId, r.Reason);
        }

        /// <summary>The refusal in words: "Something is growing here", "This acre has 2 houses".</summary>
        private static string Reason(ViewContext ctx, int tileId, RejectReason r)
        {
            if (r == RejectReason.TileOccupied) return ctx.Strings.Get("house.growing");
            if (r == RejectReason.NoRoom && ctx.Sim.State.Land.Tiles[tileId].Terrain == Terrain.Farm)
                return ctx.Strings.Get("house.full", "n", ctx.Fmt.Integer(ctx.Sim.Config.Game.Population.HousesPerAcre));
            return ctx.Reject(r);
        }

        public static ActionVm Confirm(ViewContext ctx, int tileId)
        {
            return ActionVm.Checked(ctx, "buildHere", ctx.Strings.Get("action.buildHere"), ctx.Fmt.Coins(TownSheet.HouseCost(ctx.Sim)), ActionKind.Primary, at => new BuildFamilyHouse(at, tileId));
        }
    }

    /// <summary>An acre with family houses (spec 014 §7): its houses and who lives in each, and a place for another.</summary>
    public static class HomesteadSheet
    {
        public static SheetVm Build(ViewContext ctx, Tile tile)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "family_house", Title = s.Get("homestead.title"), Subtitle = s.Get("homestead.subtitle", "n", ctx.Fmt.Integer(sim.Config.Game.Population.HousesPerAcre)) };
            AddHouses(ctx, vm, tile.Id);
            int free = sim.Config.Game.Population.HousesPerAcre - HouseRules.HousesOn(sim, tile.Id);
            if (free > 0) vm.Lines.Add(new LineVm(s.Plural("homestead.free", free), null));
            if (free > 0) vm.Actions.Add(BuildHere(ctx, tile.Id));
            return vm;
        }

        /// <summary>One row per house on the tile: who lives there (opens the household), empty, or being built.</summary>
        internal static void AddHouses(ViewContext ctx, SheetVm vm, int tileId)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            foreach (Building b in sim.State.Land.Buildings)
            {
                if (!HouseRules.IsHouse(sim, b) || b.TileId != tileId) continue;
                Household x = sim.State.Citizens.Households.Find(h => h.HomeId == b.Id);
                var line = new LineVm(TownWords.Home(ctx, b.Id), x == null ? s.Get("town.houseEmpty") : TownWords.Household(ctx, x)) { Icon = "family_house" };
                if (x != null) line.Action = ActionVm.Open("household:" + x.Id, s.Get("action.see"), new SheetRequest("Household", x.Id));
                else line.Note = s.Get("town.houseEmptyNote");
                vm.Lines.Add(line);
            }
            foreach (Construction c in sim.State.Citizens.Constructions)
                if (c.TileId == tileId)
                    vm.Lines.Add(new LineVm(s.Get("town.houseBuilding"), s.Get("people.hutReadyIn", "time", ctx.Fmt.Duration(c.ReadyAt - ctx.Now))) { Icon = "family_house_building" });
        }

        /// <summary>Build a family house on this acre (an empty field's sheet and the homestead's): the cost, or the sim's reason.</summary>
        internal static ActionVm BuildHere(ViewContext ctx, int tileId)
        {
            return ActionVm.Checked(ctx, "buildHouse", ctx.Strings.Get("town.buildHouse"), ctx.Fmt.Coins(TownSheet.HouseCost(ctx.Sim)), ActionKind.Secondary,
                at => new BuildFamilyHouse(at, tileId));
        }
    }
}
