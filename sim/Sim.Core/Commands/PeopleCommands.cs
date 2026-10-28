using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.Core.Commands
{
    /// <summary>
    /// Hires labourers till the crops growing now are in (spec 006 §3.2, ADR 0049 §3): they stay until the last one's readyAt
    /// rounded up to the hour, paid upfront pro rata (Dr 5100), harvesting each field without a hand's order as it ripens. The town's
    /// outside workers come first, then neighbours (spec 014 §3.6); the event lists the neighbours.
    /// </summary>
    public sealed class HireLabourers : TimedCommand
    {
        public int Count { get; }

        public HireLabourers(long at, int count) : base(at) { Count = count; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (sim.State.People == null || Count <= 0) return CommandResult.Reject(RejectReason.InvalidArgument);
            long until = PeopleRules.LabourUntil(sim);
            if (until == 0) return CommandResult.Reject(RejectReason.NotGrowing);
            if (PeopleRules.FreeNow(sim) < Count) return CommandResult.Reject(RejectReason.NoneAvailable);
            long cost = PeopleRules.LabourCost(sim, Count, until);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            long t = sim.State.Clock.SimTime;
            FarmSystem.Accrue(sim);
            var ids = new List<long>();
            // The town's outside workers first (spec 014 §3.6): their household earns the day wage in place of that day's outside work.
            long each = PeopleRules.LabourCost(sim, 1, until);
            int town = 0;
            foreach (Citizen c in PeopleRules.TownLabourers(sim))
            {
                if (town == Count) break;
                if (c.Labouring(t)) continue;
                c.LabourUntil = until;
                sim.State.Citizens.Household(c.HouseholdId).Earned += each;
                town++;
            }
            foreach (Neighbour n in sim.State.People.Roster)
            {
                if (ids.Count + town == Count) break;
                if (!n.FreeToday || n.Hired) continue;
                n.Tile = -1;
                n.Until = until;
                n.SecondsWorked += until - t;
                ids.Add(n.Id);
            }
            if (cost > 0)
                sim.State.Ledger.Post(t, sim.Period, "ledger.dayLabourers", "people",
                    new Posting(Account.HiredLabour, cost), new Posting(Account.Cash, -cost));
            FarmSystem.Refresh(sim);
            sim.Emit(new LabourersHired(t, ids.AsReadOnly(), cost, until));
            return CommandResult.Ok;
        }
    }

    /// <summary>Builds a hand's hut on the edge of an inherited farm acre (spec 006 §3.4, ADR 0039): paid upfront, capitalised.</summary>
    public sealed class BuildHandHut : TimedCommand
    {
        public int TileId { get; }

        public BuildHandHut(long at, int tileId) : base(at) { TileId = tileId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (sim.State.People == null) return CommandResult.Reject(RejectReason.UnknownTile);
            RejectReason place = PeopleRules.HutPlace(sim, TileId);
            if (place != RejectReason.None) return CommandResult.Reject(place);
            PeopleDefs p = sim.Config.Game.People;
            BuildingDef def = sim.Config.Game.Buildings[p.HandHut];
            long cost = MarketRules.Indexed(sim, def.Value);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            long t = sim.State.Clock.SimTime;
            PeopleState s = sim.State.People;
            var c = new Construction { Id = s.NextConstructionId++, TileId = TileId, Cost = cost, ReadyAt = t + def.BuildHours * 3600L };
            sim.State.Ledger.Post(t, sim.Period, "ledger.buildHut", "construction:" + c.Id,
                new Posting(Account.Buildings, cost), new Posting(Account.Cash, -cost));
            c.JobId = sim.ScheduleJob(PeopleSystem.HutJob, c.Id, c.ReadyAt).Id;
            s.Constructions.Add(c);
            sim.Emit(new ConstructionStarted(t, c.Id, TileId, cost, c.ReadyAt));
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Posts a job on the hiring board (spec 006 §3.5, spec 015 §3.1): for a hand (place 0) or a business's worker (its id). Your
    /// townspeople without a town job apply at once; migrants come at the post's pace, above the market better and surer ones.
    /// </summary>
    public sealed class PostJob : TimedCommand
    {
        public long Place { get; }
        public bool AboveMarket { get; }

        public PostJob(long at, bool aboveMarket) : this(at, 0, aboveMarket) { }

        public PostJob(long at, long place, bool aboveMarket) : base(at)
        {
            Place = place;
            AboveMarket = aboveMarket;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            PeopleState s = sim.State.People;
            if (s == null) return CommandResult.Reject(RejectReason.NoHut);
            if (Place == 0)
            {
                if (PeopleRules.EmptyHut(sim) == null && s.Constructions.Count == 0 && HiringRules.TownCandidates(sim, 0).Count == 0)
                    return CommandResult.Reject(RejectReason.NoHut);
            }
            else
            {
                RejectReason r = HiringRules.BusinessPlace(sim, Place);
                if (r != RejectReason.None) return CommandResult.Reject(r);
            }
            if (s.PostAt(Place) != null) return CommandResult.Reject(RejectReason.AlreadyPosted);
            JobPostDef def = sim.Config.Game.People.JobPost;
            long cost = MarketRules.Indexed(sim, def.Cost);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            long t = sim.State.Clock.SimTime;
            sim.State.Ledger.Post(t, sim.Period, "ledger.jobPost", "people",
                new Posting(Account.TransportAndSelling, cost), new Posting(Account.Cash, -cost));
            var post = new JobPost { Place = Place, AboveMarket = AboveMarket, PostedAt = t, ExpiresAt = t + def.Days * ClockDef.SecondsPerDay };
            HiringRules.AddPost(s, post);
            sim.Emit(new JobPosted(t, AboveMarket, cost, post.ExpiresAt, Place));
            HiringRules.TownApply(sim, Place);
            if (Place == 0) PeopleSystem.ScheduleApplicant(sim);
            else HiringRules.BusinessArrivals(sim);
            return CommandResult.Ok;
        }
    }

    /// <summary>Asks a migrant applicant's last employer (spec 015 §3.2): 300 coins, back in 12 hours, the skill to ±5 and how they left.</summary>
    public sealed class CheckReference : TimedCommand
    {
        public long ApplicantId { get; }

        public CheckReference(long at, long applicantId) : base(at) { ApplicantId = applicantId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Applicant a = sim.State.People?.Applicant(ApplicantId);
            if (a == null) return CommandResult.Reject(RejectReason.UnknownApplicant);
            if (a.SkillKnown || a.ReferenceAt != 0) return CommandResult.Reject(RejectReason.AlreadyChecked);
            HiringDef h = sim.Config.Game.People.Hiring;
            long cost = MarketRules.Indexed(sim, h.ReferenceCost);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);
            long t = sim.State.Clock.SimTime;
            if (cost > 0)
                sim.State.Ledger.Post(t, sim.Period, "ledger.reference", "people", new Posting(Account.TransportAndSelling, cost), new Posting(Account.Cash, -cost));
            a.ReferenceAt = t + h.ReferenceHours * 3600L;
            return CommandResult.Ok;
        }
    }

    /// <summary>Interviews an applicant: reveals their second trait (spec 006 §3.5). Free, once.</summary>
    public sealed class Interview : TimedCommand
    {
        public long ApplicantId { get; }

        public Interview(long at, long applicantId) : base(at) { ApplicantId = applicantId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Applicant a = sim.State.People?.Applicant(ApplicantId);
            if (a == null) return CommandResult.Reject(RejectReason.UnknownApplicant);
            if (a.SecondTraitKnown) return CommandResult.Reject(RejectReason.AlreadyInterviewed);
            a.SecondTraitKnown = true;
            sim.Emit(new ApplicantInterviewed(sim.State.Clock.SimTime, a.Id, sim.Config.Game.People.Traits[a.Trait2].Id));
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Offers the season contract (spec 006 §3.5, spec 015 §3.3): at the asked wage, 10% less or 10% more. Taken (seeded) → hired,
    /// the rest of the season prepaid (a hand's from your cash, a worker's from the business's); a low offer refused the first time
    /// becomes a counter at the asked wage; any other refusal and they leave. Cash is checked first: a short offer is rejected and they wait.
    /// </summary>
    public sealed class OfferContract : TimedCommand
    {
        public long ApplicantId { get; }
        public OfferLevel Level { get; }

        public OfferContract(long at, long applicantId, OfferLevel level = OfferLevel.Asked) : base(at)
        {
            ApplicantId = applicantId;
            Level = level;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Applicant a = sim.State.People?.Applicant(ApplicantId);
            if (a == null) return CommandResult.Reject(RejectReason.UnknownApplicant);
            long wage = HiringRules.OfferWage(sim, a, Level);
            long prepay = PeopleRules.RestOfSeason(sim, wage);
            if (a.Place == 0)
            {
                if (a.CitizenId == 0 && PeopleRules.EmptyHut(sim) == null) return CommandResult.Reject(RejectReason.NoEmptyHut);
                if (!sim.State.Ledger.CanAfford(prepay)) return CommandResult.Reject(RejectReason.InsufficientFunds);
            }
            else
            {
                RejectReason r = HiringRules.BusinessPlace(sim, a.Place);
                if (r != RejectReason.None) return CommandResult.Reject(r);
                if (Business.BusinessBooks.CanSpend(sim, sim.State.Businesses.Find(a.Place)) < prepay) return CommandResult.Reject(RejectReason.InsufficientFunds);
            }
            if (a.CitizenId != 0 && !HouseholdRules.WorksOutside(sim, sim.State.Citizens.Citizen(a.CitizenId))) return CommandResult.Reject(RejectReason.UnknownApplicant);
            HiringRules.Offer(sim, a, Level, wage);
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Renews a lapsed contract within the grace (spec 006 §3.6, ADR 0039): the whole season's wage, the waiting days included
    /// (they're expensed at once), and the hand goes back to work.
    /// </summary>
    public sealed class RenewContract : TimedCommand
    {
        public long HandId { get; }

        public RenewContract(long at, long handId) : base(at) { HandId = handId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Hand h = sim.State.People?.Hand(HandId);
            if (h == null) return CommandResult.Reject(RejectReason.NoHand);
            if (h.Contract != ContractState.Lapsed) return CommandResult.Reject(RejectReason.NotLapsed);
            if (!sim.State.Ledger.CanAfford(h.Wage)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            long t = sim.State.Clock.SimTime, S = sim.Clock.SecondsPerSeason;
            long start = t / S * S;
            FarmSystem.Accrue(sim);
            Prepayment p = Accrual.Open(sim, Account.HiredLabour, Account.PrepaidWages, h.Wage, start, start + S, "hand:" + h.Id);
            Accrual.ExpenseTo(sim, p, t, sim.Period); // back pay: the days waited are an expense now
            h.PrepaymentId = p.Id;
            h.Contract = ContractState.Active;
            h.LapsedLiveSeconds = 0;
            FarmSystem.Refresh(sim);
            sim.Emit(new ContractRenewed(t, h.Id, h.Wage, h.Wage));
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Lets a hand go (spec 006 §3.6): the unearned wage is refunded, less severance after the probation season (never more than
    /// the refund). The hand leaves with their orders.
    /// </summary>
    public sealed class DismissHand : TimedCommand
    {
        public long HandId { get; }

        public DismissHand(long at, long handId) : base(at) { HandId = handId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Hand h = sim.State.People?.Hand(HandId);
            if (h == null) return CommandResult.Reject(RejectReason.NoHand);
            long t = sim.State.Clock.SimTime;
            FarmSystem.Accrue(sim);
            long refund = h.PrepaymentId != 0 && sim.State.Finance.FindPrepayment(h.PrepaymentId) != null ? Accrual.Close(sim, h.PrepaymentId) : 0;
            long severance = 0;
            if (h.SeasonsWorked > 0)
            {
                severance = Money.MulRate(h.Wage, sim.Config.Game.People.Hand.SeveranceShare);
                if (severance > refund) severance = refund;
                if (severance > 0)
                    sim.State.Ledger.Post(t, sim.Period, "ledger.severance", "hand:" + h.Id,
                        new Posting(Account.HiredLabour, severance), new Posting(Account.Cash, -severance));
            }
            PeopleSystem.Leave(sim, h);
            sim.Emit(new HandDismissed(t, h.Id, refund, severance));
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// What a standing order says (spec 006 §3.8): KEEP crop ON tile [WITH seed fertiliser] THEN sell-rule, or PICK slot THEN
    /// sell-rule. Ids, not indices, so the command log reads like the player's words.
    /// </summary>
    public sealed class OrderSpec
    {
        public OrderTargetKind Kind { get; }
        public long Target { get; }
        /// <summary>The crop id for <see cref="CropId"/> that leaves the choice to the hand (ADR 0048 §1).</summary>
        public const string HandChooses = "auto";

        public string CropId { get; }
        public SeedSource Seed { get; }
        public string FertiliserId { get; }
        public SellRule Sell { get; }
        public Fixed MinPrice { get; }

        /// <summary>This season only: the order clears once its harvest (or pick) is in and sold or stored (owner, 2026-10-19).</summary>
        public bool Once { get; private set; }

        /// <summary>The same order, done once instead of every season.</summary>
        public OrderSpec ThisSeasonOnly()
        {
            var o = (OrderSpec)MemberwiseClone();
            o.Once = true;
            return o;
        }

        private OrderSpec(OrderTargetKind kind, long target, string cropId, SeedSource seed, string fertiliserId, SellRule sell, Fixed minPrice)
        {
            Kind = kind;
            Target = target;
            CropId = cropId;
            Seed = seed;
            FertiliserId = fertiliserId;
            Sell = sell;
            MinPrice = minPrice;
        }

        public static OrderSpec KeepCrop(int tileId, string cropId, SeedSource seed, string fertiliserId, SellRule sell, Fixed minPrice = default(Fixed))
        {
            return new OrderSpec(OrderTargetKind.Field, tileId, cropId, seed, fertiliserId, sell, minPrice);
        }

        public static OrderSpec PickTree(long slotId, SellRule sell, Fixed minPrice = default(Fixed))
        {
            return new OrderSpec(OrderTargetKind.Tree, slotId, null, SeedSource.Certified, null, sell, minPrice);
        }

        /// <summary>LOOK AFTER the shed, KEEP straw and feed for <paramref name="days"/> game days (spec 012 §3.11; 2–4).</summary>
        public static OrderSpec LookAfterAnimals(int days)
        {
            return new OrderSpec(OrderTargetKind.Animals, 0, null, SeedSource.Certified, null, SellRule.Store, Fixed.FromInt(days));
        }
    }

    /// <summary>Gives a hand a standing order, replacing any order on that target (spec 006 §3.8).</summary>
    public sealed class SetStandingOrder : TimedCommand
    {
        public long HandId { get; }
        public OrderSpec Order { get; }

        public SetStandingOrder(long at, long handId, OrderSpec order) : base(at)
        {
            HandId = handId;
            Order = order;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            PeopleState s = sim.State.People;
            Hand h = s?.Hand(HandId);
            if (h == null) return CommandResult.Reject(RejectReason.NoHand);
            GameDefs defs = sim.Config.Game;
            int crop = 0, fert = 0;
            if (Order.Kind == OrderTargetKind.Field)
            {
                if (Order.Target < 0 || Order.Target >= sim.State.Land.Tiles.Count) return CommandResult.Reject(RejectReason.UnknownTarget);
                Tile tile = sim.State.Land.Tiles[(int)Order.Target];
                if (tile.Terrain != Terrain.Farm || !sim.State.Land.Parcels[tile.ParcelId].Owned) return CommandResult.Reject(RejectReason.UnknownTarget);
                crop = Order.CropId == OrderSpec.HandChooses ? -1 : defs.Farm.IndexOfCrop(Order.CropId);
                if (crop < 0 && Order.CropId != OrderSpec.HandChooses) return CommandResult.Reject(RejectReason.UnknownCrop);
                if (Order.Seed == SeedSource.Saved && (crop < 0 || !defs.Farm.Crops[crop].CanSaveSeed)) return CommandResult.Reject(RejectReason.SeedCannotBeSaved);
                fert = defs.Farm.IndexOfFertiliser(Order.FertiliserId);
                if (fert < 0) return CommandResult.Reject(RejectReason.UnknownFertiliser);
            }
            else if (Order.Kind == OrderTargetKind.Animals)
            {
                if (Livestock.LivestockRules.Shed(sim) == null) return CommandResult.Reject(RejectReason.NoShed);
                if (Order.Target != 0 || Order.MinPrice < Fixed.FromInt(2) || Order.MinPrice > Fixed.FromInt(4)) return CommandResult.Reject(RejectReason.InvalidArgument);
            }
            else if (sim.State.Land.Slots.Find(x => x.Id == Order.Target) == null) return CommandResult.Reject(RejectReason.UnknownTarget);
            if (Order.Sell == SellRule.Offer && Order.MinPrice <= Fixed.Zero) return CommandResult.Reject(RejectReason.InvalidArgument);
            StandingOrder existing = s.OrderFor(Order.Kind, Order.Target);
            if ((existing == null || existing.HandId != h.Id) && s.Orders.FindAll(o => o.HandId == h.Id).Count >= defs.People.Hand.MaxOrders)
                return CommandResult.Reject(RejectReason.TooManyOrders);

            if (existing != null) s.Orders.Remove(existing);
            var order = new StandingOrder
            {
                HandId = h.Id, Kind = Order.Kind, Target = Order.Target, Crop = crop, Seed = Order.Seed, Fertiliser = fert,
                Sell = Order.Sell, MinPrice = Order.MinPrice, Once = Order.Once,
            };
            int i = 0;
            while (i < s.Orders.Count && s.Orders[i].CompareKey(order) < 0) i++;
            s.Orders.Insert(i, order);
            sim.Emit(new StandingOrderSet(sim.State.Clock.SimTime, h.Id, Order.Kind, Order.Target));
            Orders.ActNow(sim, h, order); // the hand starts at once (ADR 0040)
            return CommandResult.Ok;
        }
    }

    /// <summary>Removes a hand's order on a target (spec 006 §3.8).</summary>
    public sealed class ClearStandingOrder : TimedCommand
    {
        public long HandId { get; }
        public OrderTargetKind Kind { get; }
        public long Target { get; }

        public ClearStandingOrder(long at, long handId, OrderTargetKind kind, long target) : base(at)
        {
            HandId = handId;
            Kind = kind;
            Target = target;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            PeopleState s = sim.State.People;
            if (s?.Hand(HandId) == null) return CommandResult.Reject(RejectReason.NoHand);
            StandingOrder o = s.OrderFor(Kind, Target);
            if (o == null || o.HandId != HandId) return CommandResult.Reject(RejectReason.NoOrder);
            s.Orders.Remove(o);
            sim.Emit(new StandingOrderCleared(sim.State.Clock.SimTime, HandId, Kind, Target));
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Sends a farm hand (place 0) or a business's worker on a course (spec 015 §3.5): courseCost now, from your cash or the business's;
    /// away courseDays (no work, wage still paid), then +courseGain skill in their role.
    /// </summary>
    public sealed class SendOnCourse : TimedCommand
    {
        public long Place { get; }
        public long WorkerId { get; }

        public SendOnCourse(long at, long place, long workerId) : base(at) { Place = place; WorkerId = workerId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            IStaff w = WorkRules.Find(sim, Place, WorkerId);
            if (w == null) return CommandResult.Reject(Place == 0 ? RejectReason.NoHand : RejectReason.NoWorker);
            if (w.CourseUntil != 0) return CommandResult.Reject(RejectReason.OnCourse);
            long cost = WorkRules.CourseCost(sim);
            if (!WorkRules.CanPay(sim, w, cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);
            FarmSystem.Accrue(sim);
            WorkRules.Pay(sim, w, cost, "ledger.course");
            WorkRules.SetCourse(w, sim.State.Clock.SimTime + sim.Config.Game.People.Work.CourseDays * ClockDef.SecondsPerDay);
            FarmSystem.Refresh(sim);
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Long days on or off for a farm hand (spec 015 §3.4): 2 hours more a day at 1.5× their hourly wage, today's paid now and each
    /// day's upfront; fatigue builds while they last. A business's long days are its open hours (<see cref="SetBusiness"/>).
    /// </summary>
    public sealed class SetLongDays : TimedCommand
    {
        public long HandId { get; }
        public bool On { get; }

        public SetLongDays(long at, long handId, bool on) : base(at) { HandId = handId; On = on; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Hand h = WorkRules.Find(sim, 0, HandId) as Hand;
            if (h == null) return CommandResult.Reject(RejectReason.NoHand);
            if (h.LongDays == On) return CommandResult.Ok;
            long pay = On ? WorkRules.OvertimeDay(sim, h) : 0;
            if (!sim.State.Ledger.CanAfford(pay)) return CommandResult.Reject(RejectReason.InsufficientFunds);
            FarmSystem.Accrue(sim);
            WorkRules.Pay(sim, h, pay, "ledger.overtime");
            h.LongDays = On;
            FarmSystem.Refresh(sim);
            return CommandResult.Ok;
        }
    }
}
