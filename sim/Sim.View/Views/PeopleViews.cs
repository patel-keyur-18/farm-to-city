using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Time;

namespace Sim.View.Views
{
    /// <summary>Skill and hours in words and stars, never raw factors (ADR 0039 §7); names of people and places.</summary>
    public static class PeopleWords
    {
        /// <summary>Stars for a skill range: solid to the low end, faded to the high end (both rounded to the nearest star).</summary>
        public static StarsVm Stars(ViewContext ctx, int low, int high)
        {
            int per = ctx.Ui.SkillStarPoints;
            int solid = System.Math.Min(5, (low + per / 2) / per);
            int upto = System.Math.Min(5, System.Math.Max(solid, (high + per / 2) / per));
            return new StarsVm(solid, upto - solid);
        }

        /// <summary>Beginner, Capable, Skilled or Expert (thresholds in ui.json).</summary>
        public static string Band(ViewContext ctx, int skill)
        {
            string[] keys = { "skill.beginner", "skill.capable", "skill.skilled", "skill.expert" };
            int i = 0;
            while (i < 3 && skill >= ctx.Ui.SkillBands[i]) i++;
            return ctx.Strings.Get(keys[i]);
        }

        /// <summary>"Capable" or "Capable to Skilled".</summary>
        public static string Range(ViewContext ctx, int low, int high)
        {
            string a = Band(ctx, low), b = Band(ctx, high);
            return a == b ? a : ctx.Strings.Get("skill.range", "low", a, "high", b);
        }

        /// <summary>"Tends about 2½ acres": hours a day ÷ a reference acre's hours, to the nearest half.</summary>
        public static string Tends(ViewContext ctx, Fixed hours)
        {
            long halves = (hours * Fixed.FromInt(2) / Fixed.FromInt(ctx.Ui.ReferenceAcreHours)).RoundToLong();
            string acres = halves / 2 == 0 ? "½" : (halves / 2) + (halves % 2 == 1 ? "½" : "");
            return ctx.Strings.Get("people.tends", "acres", acres);
        }

        public static string Name(ViewContext ctx, int name) { return ctx.Strings.Get(ctx.Sim.Config.Game.People.Names[name].NameKey); }

        public static bool Female(ViewContext ctx, int name) { return ctx.Sim.Config.Game.People.Names[name].Female; }

        public static string Trait(ViewContext ctx, int trait) { return ctx.Strings.Get(ctx.Sim.Config.Game.People.Traits[trait].NameKey); }

        /// <summary>"Field 3": the player's farm acres numbered in map order.</summary>
        public static string Field(ViewContext ctx, int tileId)
        {
            int n = 0;
            foreach (Tile t in ctx.Sim.State.Land.Tiles)
            {
                if (t.Terrain != Terrain.Farm || !ctx.Sim.State.Land.Parcels[t.ParcelId].Owned) continue;
                n++;
                if (t.Id == tileId) break;
            }
            return ctx.Strings.Get("place.field", "n", ctx.Fmt.Integer(n));
        }

        public static string Target(ViewContext ctx, OrderTargetKind kind, long target)
        {
            if (kind == OrderTargetKind.Field) return Field(ctx, (int)target);
            Tree tree = ctx.Sim.State.Farm.TreeIn(target);
            return tree == null ? ctx.Strings.Get("place.treeSlot") : ctx.Strings.Get(ctx.Sim.Config.Game.Farm.Trees[tree.Def].NameKey);
        }

        /// <summary>"Keep wheat · sell at the market, not below 26" or "Pick · sell to the trader".</summary>
        public static string Order(ViewContext ctx, StandingOrder o)
        {
            string sell = o.Sell == SellRule.Store ? ctx.Strings.Get("order.sellStore")
                : o.Sell == SellRule.Trader ? ctx.Strings.Get("order.sellTrader")
                : ctx.Strings.Get("order.sellOffer", "min", ctx.Fmt.Integer(o.MinPrice.RoundToLong()));
            string what = o.Kind == OrderTargetKind.Field
                ? ctx.Strings.Get("order.keep", "crop", ctx.Strings.Get(ctx.Sim.Config.Game.Farm.Crops[o.Crop].NameKey))
                : ctx.Strings.Get("order.pick");
            return what + " · " + sell + " · " + ctx.Strings.Get(o.Once ? "order.once" : "order.every");
        }

        /// <summary>"Last: harvested 1,200 kg, 3 h ago", or the waiting reason, or null before the first action.</summary>
        public static string Last(ViewContext ctx, StandingOrder o)
        {
            if (o.Waiting != OrderWait.None) return ctx.Strings.Get("order.wait." + o.Waiting);
            if (o.LastAction == OrderAction.None) return null;
            string ago = ctx.Fmt.Duration(ctx.Now - o.LastAt);
            return o.LastAction == OrderAction.Sowed ? ctx.Strings.Get("order.lastSowed", "ago", ago)
                : ctx.Strings.Get(o.LastAction == OrderAction.Picked ? "order.lastPicked" : "order.lastHarvested", "qty", ctx.Fmt.Kg(o.LastQuantity), "ago", ago);
        }

        /// <summary>The hand who takes orders for a target: the one holding it, else the first hand.</summary>
        public static Hand OrderHand(Simulation sim, OrderTargetKind kind, long target)
        {
            PeopleState p = sim.State.People;
            if (p == null || p.Hands.Count == 0) return null;
            StandingOrder o = p.OrderFor(kind, target);
            return o != null ? p.Hand(o.HandId) : p.Hands[0];
        }

        /// <summary>Care today as a word, with <paramref name="extraHours"/> more hours a day in the pool.</summary>
        internal static string CareWith(ViewContext ctx, Fixed extraHours)
        {
            Simulation sim = ctx.Sim;
            Fixed demand = FarmSystem.LabourDemand(sim);
            Fixed cap = sim.Config.Game.Farm.Farm.CareCap;
            Fixed ratio = demand == Fixed.Zero ? cap : (PeopleRules.CareSupply(sim) + extraHours) / demand;
            return ctx.Rating(ratio > cap ? cap : ratio);
        }
    }

    /// <summary>The People sheet (ADR 0039 §8): day labourers, hands, applicants, huts and sending word. One row per choice.</summary>
    public static class PeopleSheet
    {
        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            PeopleState p = sim.State.People;
            PeopleDefs defs = sim.Config.Game.People;
            var vm = new SheetVm
            {
                Icon = "people", Title = ctx.Strings.Get("people.title"),
                Subtitle = ctx.Strings.Get("people.subtitle", "population", ctx.Fmt.Integer(p.Population), "huts", ctx.Fmt.Integer(PeopleRules.HutCount(sim)),
                    "max", ctx.Fmt.Integer(defs.Hand.MaxHandHuts)),
            };

            // Day labourers.
            vm.Lines.Add(LineVm.Header(ctx.Strings.Get("people.dayLabour")));
            int free = PeopleRules.FreeNow(sim), working = 0;
            foreach (Neighbour n in p.Roster) if (n.HiredToday) working++;
            Season season = Calendar.SeasonAt(ctx.Now, sim.Clock);
            vm.Lines.Add(new LineVm(ctx.Strings.Get("people.freeToday"),
                ctx.Strings.Get("people.freeTodayValue", "n", ctx.Fmt.Integer(free), "season", ctx.Strings.Get("season." + season.ToString().ToLowerInvariant()))));
            if (working > 0) vm.Lines.Add(new LineVm(ctx.Strings.Get("people.workingToday"), ctx.Strings.Get("people.workingTodayValue", "n", ctx.Fmt.Integer(working))));
            bool growing = FarmSystem.LabourDemand(sim) > Fixed.Zero;
            string now = PeopleWords.CareWith(ctx, Fixed.Zero);
            for (int k = 1; k <= free; k++)
            {
                int count = k;
                string care = growing
                    ? ctx.Strings.Get("people.carePreview", "from", now, "to", PeopleWords.CareWith(ctx, defs.Pool.HoursPerDay.MulRatio(count, 1)))
                    : ctx.Strings.Get("people.noCrops");
                var line = new LineVm(ctx.Strings.Plural("people.hireN", count), care, "care");
                line.Action = ActionVm.Checked(ctx, "hire:" + count, ctx.Strings.Get("action.hire"), ctx.Fmt.Coins(PeopleRules.DayLabourCost(sim, count)), ActionKind.Primary,
                    at => new HireDayLabourers(at, count));
                vm.Lines.Add(line);
            }

            // Hands.
            vm.Lines.Add(LineVm.Header(ctx.Strings.Get("people.hands")));
            if (p.Hands.Count == 0) vm.Lines.Add(new LineVm(ctx.Strings.Get("people.noHands"), ctx.Strings.Get(PeopleRules.HutCount(sim) == 0 ? "people.noHandsHut" : p.Applicants.Count > 0 ? "people.noHandsChoose" : "people.noHandsWait")));
            foreach (Hand h in p.Hands)
            {
                Hand hand = h;
                int skill = (int)h.Skill.RoundToLong();
                string value = h.Contract == ContractState.Lapsed ? ctx.Strings.Get("people.unpaid")
                    : PeopleWords.Band(ctx, skill) + " · " + PeopleWords.Tends(ctx, PeopleRules.HandHours(sim, h));
                var line = new LineVm(PeopleWords.Name(ctx, h.Name), value) { Stars = PeopleWords.Stars(ctx, skill, skill) };
                line.Action = h.Contract == ContractState.Lapsed
                    ? ActionVm.Checked(ctx, "renew:" + h.Id, ctx.Strings.Get("action.renew"), ctx.Fmt.Coins(h.Wage), ActionKind.Primary, at => new RenewContract(at, hand.Id))
                    : ActionVm.Open("hand:" + h.Id, ctx.Strings.Get("action.see"), new SheetRequest("Hand", h.Id));
                vm.Lines.Add(line);
            }

            // Applicants.
            if (PeopleRules.HutCount(sim) > 0)
            {
                vm.Lines.Add(LineVm.Header(ctx.Strings.Get("people.applicants")));
                if (p.Applicants.Count == 0)
                    vm.Lines.Add(new LineVm(ctx.Strings.Get("people.nobody"), p.NextApplicantAt > ctx.Now
                        ? ctx.Strings.Get("people.nextIn", "time", ctx.Fmt.Duration(p.NextApplicantAt - ctx.Now))
                        : ctx.Strings.Get(PeopleRules.EmptyHut(sim) == null ? "people.waitHut" : "people.nextSoon")));
                foreach (Applicant a in p.Applicants)
                {
                    Applicant ap = a;
                    PeopleRules.SkillRange(sim, a, out int lo, out int hi);
                    string traits = PeopleWords.Trait(ctx, a.Trait1) + " · " + (a.SecondTraitKnown ? PeopleWords.Trait(ctx, a.Trait2) : "?");
                    long wage = PeopleRules.AskedWage(sim, Fixed.FromInt(a.Skill), a.AboveMarket);
                    var line = new LineVm(PeopleWords.Name(ctx, a.Name), PeopleWords.Range(ctx, lo, hi) + " · " + traits) { Stars = PeopleWords.Stars(ctx, lo, hi) };
                    line.Action = ActionVm.Checked(ctx, "offer:" + a.Id, ctx.Strings.Get("action.offer"),
                        ctx.Strings.Get("people.offerDetail", "wage", ctx.Fmt.Coins(wage), "now", ctx.Fmt.Coins(PeopleRules.RestOfSeason(sim, wage))), ActionKind.Primary,
                        at => new OfferContract(at, ap.Id));
                    vm.Lines.Add(line);
                    if (!a.SecondTraitKnown)
                    {
                        var talk = new LineVm(ctx.Strings.Get("people.talkTo", "name", PeopleWords.Name(ctx, a.Name)), ctx.Strings.Get("people.talkWhy"));
                        talk.Action = ActionVm.Checked(ctx, "interview:" + a.Id, ctx.Strings.Get("action.interview"), null, ActionKind.Secondary, at => new Interview(at, ap.Id));
                        vm.Lines.Add(talk);
                    }
                }
            }

            // Huts.
            vm.Lines.Add(LineVm.Header(ctx.Strings.Get("people.huts")));
            foreach (Building b in sim.State.Land.Buildings)
            {
                if (!PeopleRules.IsHandHut(sim, b)) continue;
                Hand who = p.Hands.Find(h => h.HutId == b.Id);
                vm.Lines.Add(new LineVm(PeopleWords.Field(ctx, b.TileId),
                    who == null ? ctx.Strings.Get("people.hutEmpty") : ctx.Strings.Get("people.hutLives", "name", PeopleWords.Name(ctx, who.Name))));
            }
            foreach (Construction c in p.Constructions)
                vm.Lines.Add(new LineVm(PeopleWords.Field(ctx, c.TileId), ctx.Strings.Get("people.hutReadyIn", "time", ctx.Fmt.Duration(c.ReadyAt - ctx.Now))));
            long hutCost = MarketRules.Indexed(sim, sim.Config.Game.Buildings[defs.HandHut].Value);
            bool room = PeopleRules.HutCount(sim) < defs.Hand.MaxHandHuts;
            bool cash = sim.State.Ledger.CanAfford(hutCost);
            var build = new LineVm(ctx.Strings.Get("people.buildHut"), ctx.Strings.Get("people.buildHutWhy"), "hut");
            build.Action = new ActionVm("buildHut", ctx.Strings.Get("action.place"), ctx.Fmt.Coins(hutCost), ActionKind.Open, room && cash,
                !room ? ctx.Reject(RejectReason.TooManyHuts) : !cash ? ctx.Reject(RejectReason.InsufficientFunds) : null, null, new SheetRequest("HutPlacement", 0));
            vm.Lines.Add(build);

            // Sending word.
            vm.Lines.Add(LineVm.Header(ctx.Strings.Get("people.word")));
            if (p.Post != null)
                vm.Lines.Add(new LineVm(ctx.Strings.Get(p.Post.AboveMarket ? "people.postAbove" : "people.postMarket"),
                    ctx.Strings.Get("people.postActive", "time", ctx.Fmt.Duration(p.Post.ExpiresAt - ctx.Now))));
            else
                foreach (bool above in new[] { false, true })
                {
                    bool am = above;
                    var line = new LineVm(ctx.Strings.Get(am ? "people.postAbove" : "people.postMarket"), ctx.Strings.Get(am ? "people.postAboveWhy" : "people.postMarketWhy"));
                    line.Action = ActionVm.Checked(ctx, am ? "postAbove" : "postMarket", ctx.Strings.Get("action.post"),
                        ctx.Fmt.Coins(MarketRules.Indexed(sim, defs.JobPost.Cost)), ActionKind.Secondary, at => new PostJob(at, am));
                    vm.Lines.Add(line);
                }
            return vm;
        }
    }

    /// <summary>One hand (spec 006 §7, ADR 0039 §8): skill, work, traits, contract, orders; Renew or Let go.</summary>
    public static class HandSheet
    {
        public static SheetVm Build(ViewContext ctx, long handId)
        {
            Simulation sim = ctx.Sim;
            Hand h = sim.State.People.Hand(handId);
            if (h == null) return new SheetVm { Icon = "people", Title = ctx.Reject(RejectReason.NoHand) };
            int skill = (int)h.Skill.RoundToLong();
            var vm = new SheetVm
            {
                Icon = PeopleWords.Female(ctx, h.Name) ? "person_hand_f" : "person_hand_m", Title = PeopleWords.Name(ctx, h.Name),
                Subtitle = ctx.Strings.Get("hand.subtitle", "time", ctx.Fmt.Duration(ctx.Now - h.HiredAt)),
            };
            vm.Lines.Add(new LineVm(ctx.Strings.Get("hand.skill"), PeopleWords.Band(ctx, skill), "hand.skill") { Stars = PeopleWords.Stars(ctx, skill, skill) });
            vm.Lines.Add(new LineVm(ctx.Strings.Get("hand.work"), PeopleWords.Tends(ctx, PeopleRules.HandHours(sim, h)), "hand.hours"));
            foreach (int t in new[] { h.Trait1, h.Trait2 })
                vm.Lines.Add(new LineVm(PeopleWords.Trait(ctx, t), ctx.Strings.Get(sim.Config.Game.People.Traits[t].NameKey + ".why")));
            if (h.Contract == ContractState.Active)
            {
                long left = Sim.Core.Finance.Accrual.RefundIfClosed(sim, h.PrepaymentId);
                vm.Lines.Add(new LineVm(ctx.Strings.Get("hand.contract"), ctx.Strings.Get("hand.contractPaid", "wage", ctx.Fmt.Coins(h.Wage)), "hand.wage"));
                vm.Lines.Add(new LineVm(ctx.Strings.Get("hand.prepaid"), ctx.Fmt.Coins(left)));
            }
            else
            {
                long grace = sim.Config.Game.People.Hand.LapseGraceDays * ClockDef.SecondsPerDay - h.LapsedLiveSeconds;
                vm.Lines.Add(new LineVm(ctx.Strings.Get("hand.contract"), ctx.Strings.Get("hand.contractUnpaid", "time", ctx.Fmt.Duration(grace), "wage", ctx.Fmt.Coins(h.Wage)), "hand.lapse"));
            }

            vm.Lines.Add(LineVm.Header(ctx.Strings.Get("hand.orders")));
            bool any = false;
            foreach (StandingOrder o in sim.State.People.Orders)
            {
                if (o.HandId != h.Id) continue;
                any = true;
                StandingOrder order = o;
                string last = PeopleWords.Last(ctx, o);
                var line = new LineVm(PeopleWords.Target(ctx, o.Kind, o.Target), PeopleWords.Order(ctx, o) + (last == null ? "" : "\n" + last));
                line.Action = ActionVm.Checked(ctx, "clear:" + o.Kind + ":" + o.Target, ctx.Strings.Get("action.remove"), null, ActionKind.Secondary,
                    at => new ClearStandingOrder(at, h.Id, order.Kind, order.Target));
                vm.Lines.Add(line);
            }
            if (!any) vm.Lines.Add(new LineVm(ctx.Strings.Get("hand.noOrders"), ctx.Strings.Get("hand.noOrdersHint")));

            if (h.Contract == ContractState.Lapsed)
                vm.Actions.Add(ActionVm.Checked(ctx, "renew", ctx.Strings.Get("action.renew"), ctx.Fmt.Coins(h.Wage), ActionKind.Primary, at => new RenewContract(at, handId)));
            long refund = h.PrepaymentId == 0 ? 0 : Sim.Core.Finance.Accrual.RefundIfClosed(sim, h.PrepaymentId);
            long severance = h.SeasonsWorked == 0 ? 0 : System.Math.Min(refund, Sim.Core.Finance.Money.MulRate(h.Wage, sim.Config.Game.People.Hand.SeveranceShare));
            ActionVm letGo = ActionVm.Checked(ctx, "letGo", ctx.Strings.Get("action.letGo"),
                ctx.Strings.Get("hand.letGoDetail", "refund", ctx.Fmt.Coins(refund - severance)), ActionKind.Danger, at => new DismissHand(at, handId));
            letGo.Confirm = new ConfirmVm(ctx.Strings.Get("hand.letGoQ", "name", PeopleWords.Name(ctx, h.Name)), ctx.Strings.Get("action.letGo"),
                ctx.Strings.Get("hand.keep"), at => new Command[] { new DismissHand(at, handId) }, noSends: false) { YesIsDanger = true };
            vm.Actions.Add(letGo);
            return vm;
        }
    }

    /// <summary>
    /// Giving a hand an order from the map (ADR 0039 §8): a field's crop first (Arg null), then how to sell (Arg = crop, or
    /// "crop|minimum" while the minimum is being stepped). Trees go straight to selling (Arg "tree").
    /// </summary>
    public static class OrderSheet
    {
        public static SheetVm Build(ViewContext ctx, OrderTargetKind kind, long target, string arg)
        {
            Simulation sim = ctx.Sim;
            GameDefs defs = sim.Config.Game;
            Hand h = PeopleWords.OrderHand(sim, kind, target);
            var vm = new SheetVm { Icon = "people" };
            if (h == null)
            {
                vm.Title = ctx.Strings.Get("order.noHand");
                return vm;
            }
            string name = PeopleWords.Name(ctx, h.Name);
            vm.Title = ctx.Strings.Get("order.title", "name", name, "place", PeopleWords.Target(ctx, kind, target));
            StandingOrder existing = sim.State.People.OrderFor(kind, target);
            if (existing != null) vm.Subtitle = PeopleWords.Order(ctx, existing);

            // Arg: the steps chosen so far, "crop|once|26" (a tree: "tree|every|40"). Crop, then how long, then how to sell.
            string[] parts = string.IsNullOrEmpty(arg) ? new string[0] : arg.Split('|');
            string crop = kind == OrderTargetKind.Field && parts.Length > 0 ? parts[0] : null;
            string head = kind == OrderTargetKind.Field ? crop : "tree";
            if (kind == OrderTargetKind.Field && crop == null)
            {
                Season season = Calendar.SeasonAt(ctx.Now, sim.Clock);
                foreach (CropDef c in defs.Farm.Crops)
                {
                    bool now = c.SowSeasons[(int)season];
                    string cname = ctx.Strings.Get(c.NameKey);
                    var line = new LineVm(cname, now ? ctx.Strings.Get("order.sowsNow")
                        : ctx.Strings.Get("order.waitsFor", "seasons", FieldSheets.SeasonsText(ctx, c.SowSeasons)));
                    line.Action = ActionVm.Open("crop:" + c.Id, ctx.Strings.Get("action.choose"), new SheetRequest("Order", target, c.Id));
                    vm.Lines.Add(line);
                }
                AddClear(ctx, vm, h, existing, kind, target);
                return vm;
            }
            if (crop != null) vm.Subtitle = ctx.Strings.Get("order.keep", "crop", ctx.Strings.Get(defs.Farm.Crops[defs.Farm.IndexOfCrop(crop)].NameKey));
            if (parts.Length < 2)
            {
                // How long: this season only (the order clears after its harvest) or every season (a standing order).
                foreach (string how in new[] { "once", "every" })
                {
                    var line = new LineVm(ctx.Strings.Get("order." + how), ctx.Strings.Get("order." + how + "Why"));
                    line.Action = ActionVm.Open("how:" + how, ctx.Strings.Get("action.choose"), new SheetRequest("Order", target, head + "|" + how));
                    vm.Lines.Add(line);
                }
                AddClear(ctx, vm, h, existing, kind, target);
                return vm;
            }
            bool once = parts[1] == "once";
            Fixed? min = parts.Length > 2 ? Fixed.FromInt(int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)) : (Fixed?)null;
            vm.Subtitle = (vm.Subtitle == null ? "" : vm.Subtitle + " · ") + ctx.Strings.Get(once ? "order.once" : "order.every");

            int good = kind == OrderTargetKind.Field ? defs.Farm.Crops[defs.Farm.IndexOfCrop(crop)].OutputGood
                : sim.State.Farm.TreeIn(target) == null ? -1 : defs.Farm.Trees[sim.State.Farm.TreeIn(target).Def].FruitGood;
            long today = good < 0 ? 1 : System.Math.Max(1, sim.State.Market.MarketPrice(good).FloorToLong()); // never above today's price, so the offer can sell
            long minimum = min.HasValue ? min.Value.RoundToLong() : today;
            OrderSpec Spec(SellRule rule, long m)
            {
                OrderSpec spec = crop != null ? OrderSpec.KeepCrop((int)target, crop, SeedSource.Certified, "none", rule, Fixed.FromInt((int)m)) : OrderSpec.PickTree(target, rule, Fixed.FromInt((int)m));
                return once ? spec.ThisSeasonOnly() : spec;
            }

            var store = new LineVm(ctx.Strings.Get("order.sellStore"), ctx.Strings.Get("order.sellStoreWhy"));
            store.Action = ActionVm.Checked(ctx, "set:store", ctx.Strings.Get("action.setOrder"), null, ActionKind.Primary, at => new SetStandingOrder(at, h.Id, Spec(SellRule.Store, 0)));
            vm.Lines.Add(store);
            var offer = new LineVm(ctx.Strings.Get("order.sellOffer", "min", ctx.Fmt.Integer(minimum)),
                ctx.Strings.Get("order.sellOfferWhy", "today", ctx.Fmt.Integer(today)), "minPrice");
            offer.Action = ActionVm.Checked(ctx, "set:offer", ctx.Strings.Get("action.setOrder"), null, ActionKind.Primary, at => new SetStandingOrder(at, h.Id, Spec(SellRule.Offer, minimum)));
            vm.Lines.Add(offer);
            string prefix = head + "|" + parts[1] + "|";
            var lower = new LineVm(ctx.Strings.Get("order.lower"), ctx.Strings.Get("order.minNow", "min", ctx.Fmt.Integer(minimum)));
            lower.Action = new ActionVm("min:down", "−", null, ActionKind.Open, minimum > 1, minimum > 1 ? null : ctx.Reject(RejectReason.InvalidArgument), null,
                new SheetRequest("Order", target, prefix + (minimum - 1)));
            vm.Lines.Add(lower);
            var raise = new LineVm(ctx.Strings.Get("order.raise"), ctx.Strings.Get("order.minNow", "min", ctx.Fmt.Integer(minimum)));
            raise.Action = ActionVm.Open("min:up", "+", new SheetRequest("Order", target, prefix + (minimum + 1)));
            vm.Lines.Add(raise);
            var trader = new LineVm(ctx.Strings.Get("order.sellTrader"), ctx.Strings.Get("order.sellTraderWhy"));
            trader.Action = ActionVm.Checked(ctx, "set:trader", ctx.Strings.Get("action.setOrder"), null, ActionKind.Primary, at => new SetStandingOrder(at, h.Id, Spec(SellRule.Trader, 0)));
            vm.Lines.Add(trader);
            AddClear(ctx, vm, h, existing, kind, target);
            return vm;
        }

        private static void AddClear(ViewContext ctx, SheetVm vm, Hand h, StandingOrder existing, OrderTargetKind kind, long target)
        {
            if (existing == null) return;
            long holder = existing.HandId;
            vm.Actions.Add(ActionVm.Checked(ctx, "clearOrder", ctx.Strings.Get("action.removeOrder"), null, ActionKind.Danger, at => new ClearStandingOrder(at, holder, kind, target)));
        }
    }

    /// <summary>Where a hand's hut can go (ADR 0039 §2): each inherited farm acre, green or red with the reason.</summary>
    public sealed class HutPlaceVm
    {
        public int TileId { get; internal set; }
        public bool Allowed { get; internal set; }
        public string Reason { get; internal set; }
    }

    public static class HutPlacement
    {
        public static List<HutPlaceVm> Places(ViewContext ctx)
        {
            var list = new List<HutPlaceVm>();
            Simulation sim = ctx.Sim;
            foreach (Tile t in sim.State.Land.Tiles)
            {
                if (t.Terrain != Terrain.Quarters || !sim.State.Land.Parcels[t.ParcelId].Protected) continue;
                CommandResult r = sim.Preview(new BuildHandHut(ctx.Now, t.Id));
                list.Add(new HutPlaceVm { TileId = t.Id, Allowed = r.Accepted, Reason = r.Accepted ? null : ctx.Reject(r.Reason) });
            }
            return list;
        }

        /// <summary>Why a hut can't go on any tile (the ghost over a tile that isn't in <see cref="Places"/>).</summary>
        public static string ReasonFor(ViewContext ctx, int tileId)
        {
            CommandResult r = ctx.Sim.Preview(new BuildHandHut(ctx.Now, tileId));
            return r.Accepted ? null : ctx.Reject(r.Reason);
        }

        /// <summary>The Confirm card's button: build here, with the cost.</summary>
        public static ActionVm Confirm(ViewContext ctx, int tileId)
        {
            long cost = MarketRules.Indexed(ctx.Sim, ctx.Sim.Config.Game.Buildings[ctx.Sim.Config.Game.People.HandHut].Value);
            return ActionVm.Checked(ctx, "buildHere", ctx.Strings.Get("action.buildHere"), ctx.Fmt.Coins(cost), ActionKind.Primary, at => new BuildHandHut(at, tileId));
        }
    }

    /// <summary>The people part of While you were away (US-182, spec 006 AC-21): what each hand did, day labour, contracts.</summary>
    public sealed class AwayVm
    {
        public string Title { get; internal set; }
        public string Subtitle { get; internal set; }
        public List<LineVm> Lines { get; } = new List<LineVm>();

        /// <summary>Totals over the events, for tests and the card's footer: sales by the hands' orders and inputs they bought.</summary>
        public long OrderSales { get; internal set; }
        public long OrderInputs { get; internal set; }
        public long DayLabourPaid { get; internal set; }
    }

    public static class AwayView
    {
        /// <summary>Builds the card from an away advance's events (the sim's LastAwayReport), or null when no people events happened.</summary>
        public static AwayVm People(ViewContext ctx, IReadOnlyList<SimEvent> events)
        {
            Simulation sim = ctx.Sim;
            if (sim.State.People == null) return null;
            var vm = new AwayVm { Title = ctx.Strings.Get("away.title") };
            var hands = new List<long>();
            long away = 0;
            foreach (SimEvent e in events)
            {
                if (e is AwayAdvanceCompleted done) away = done.AppliedSeconds;
                if (e is OrderExecuted o && !hands.Contains(o.HandId)) hands.Add(o.HandId);
            }
            vm.Subtitle = ctx.Strings.Get("away.subtitle", "time", ctx.Fmt.Duration(away));
            foreach (long id in hands)
            {
                Hand h = sim.State.People.Hand(id);
                string name = h == null ? ctx.Strings.Get("away.formerHand") : PeopleWords.Name(ctx, h.Name);
                int harvests = 0, sowings = 0, picks = 0;
                long grams = 0, sales = 0, inputs = 0;
                foreach (SimEvent e in events)
                {
                    if (!(e is OrderExecuted o) || o.HandId != id) continue;
                    if (o.Action == OrderAction.Sowed) { sowings++; inputs -= o.Amount; }
                    else
                    {
                        if (o.Action == OrderAction.Picked) picks++; else harvests++;
                        grams += o.Quantity;
                        sales += o.Amount;
                    }
                }
                vm.OrderSales += sales;
                vm.OrderInputs += inputs;
                if (harvests + picks > 0)
                    vm.Lines.Add(new LineVm(name, ctx.Strings.Get("away.collected", "n", ctx.Fmt.Integer(harvests + picks), "qty", ctx.Fmt.Kg(grams))));
                if (sales > 0) vm.Lines.Add(new LineVm(name, ctx.Strings.Get("away.sold", "amount", ctx.Fmt.Coins(sales))));
                if (sowings > 0) vm.Lines.Add(new LineVm(name, ctx.Strings.Get("away.sowed", "n", ctx.Fmt.Integer(sowings), "cost", ctx.Fmt.Coins(inputs))));
            }
            var waits = new List<string>();
            foreach (SimEvent e in events)
            {
                if (e is OrderWaiting w)
                {
                    string text = ctx.Strings.Get("away.waiting", "place", PeopleWords.Target(ctx, w.Kind, w.Target), "reason", ctx.Strings.Get("order.wait." + w.Reason));
                    if (!waits.Contains(text)) waits.Add(text);
                }
                if (e is DayLabourPaid d) vm.DayLabourPaid += d.Amount;
                if (e is ContractRenewed r) vm.Lines.Add(new LineVm(HandName(ctx, r.HandId), ctx.Strings.Get("away.renewed", "amount", ctx.Fmt.Coins(r.Paid))));
                if (e is ContractLapsed l) vm.Lines.Add(new LineVm(HandName(ctx, l.HandId), ctx.Strings.Get("away.lapsed", "amount", ctx.Fmt.Coins(l.Needed))));
                if (e is HandLeft) vm.Lines.Add(new LineVm(ctx.Strings.Get("away.formerHand"), ctx.Strings.Get("away.left")));
            }
            foreach (string w in waits) vm.Lines.Add(new LineVm(ctx.Strings.Get("away.order"), w));
            if (vm.DayLabourPaid > 0) vm.Lines.Add(new LineVm(ctx.Strings.Get("away.dayLabour"), ctx.Fmt.Coins(vm.DayLabourPaid)));
            if (vm.Lines.Count > ctx.Ui.AwayRowsMax) vm.Lines.RemoveRange(ctx.Ui.AwayRowsMax, vm.Lines.Count - ctx.Ui.AwayRowsMax);
            return vm.Lines.Count == 0 ? null : vm;
        }

        private static string HandName(ViewContext ctx, long handId)
        {
            Hand h = ctx.Sim.State.People.Hand(handId);
            return h == null ? ctx.Strings.Get("away.formerHand") : PeopleWords.Name(ctx, h.Name);
        }
    }
}
