using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.Core.History
{
    /// <summary>
    /// Writes the town's Chronicle (spec 029 §3, ADR 0165, 0166): every moment comes from an event or a state change the simulation already
    /// has, observed where it is emitted (so one drained between passes is never missed) and dated with its own time (so a moment while the
    /// app was closed keeps it). Nothing here changes any other state: a town with a Chronicle plays exactly as one without.
    /// </summary>
    public static class ChronicleRules
    {
        public static ChronicleState State(Simulation sim) { return sim.State.Chronicle; }

        public static ChronicleDefs Defs(Simulation sim) { return sim.Config.Game?.Chronicle; }

        public static bool On(Simulation sim) { return sim.State.Chronicle != null && Defs(sim) != null; }

        // ---- writing ----

        /// <summary>
        /// Adds a line. A once-only kind is written once; a repeating kind at most once a season, the bigger one (by <paramref name="a"/>) kept;
        /// when the Chronicle is full the oldest line that is not pinned goes.
        /// </summary>
        internal static void Add(Simulation sim, ChronicleKind kind, long at, long a = 0, long b = 0, string id = "")
        {
            if (!On(sim)) return;
            ChronicleState s = State(sim);
            if (ChronicleDefs.Once(kind))
            {
                long bit = 1L << (int)kind;
                if ((s.Done & bit) != 0) return;
                s.Done |= bit;
            }
            else if (ChronicleDefs.Repeats(kind))
            {
                long season = at / sim.Clock.SecondsPerSeason;
                for (int i = s.Entries.Count - 1; i >= 0; i--)
                {
                    ChronicleEntry x = s.Entries[i];
                    if (x.At / sim.Clock.SecondsPerSeason < season) break;
                    if (x.Kind != kind) continue;
                    if (a <= x.A) return; // a bigger one of the kind is already this season's
                    x.At = at; x.A = a; x.B = b; x.Id = id;
                    sim.Emit(new ChronicleWritten(at, kind));
                    return;
                }
            }
            s.Entries.Add(new ChronicleEntry { At = at, Kind = kind, A = a, B = b, Id = id });
            int keep = Defs(sim).Keep;
            while (s.Entries.Count > keep)
            {
                int drop = s.Entries.FindIndex(x => !ChronicleDefs.Pinned(x.Kind));
                if (drop < 0) break; // every line is pinned: keep them all
                s.Entries.RemoveAt(drop);
            }
            sim.Emit(new ChronicleWritten(at, kind));
        }

        // ---- the start ----

        /// <summary>A new town's first line: the acres it began with (§3.2).</summary>
        internal static void Founding(Simulation sim)
        {
            if (!On(sim) || sim.State.Land == null) return;
            long acres = 0;
            foreach (Land.Parcel p in sim.State.Land.Parcels)
                if (p.Owned) acres += sim.Config.Game.Map.Parcels[p.Id].Tiles.Count;
            Add(sim, ChronicleKind.Founding, sim.State.Clock.SimTime, acres);
        }

        /// <summary>
        /// A town loaded from an older save (§3.1): "The Chronicle begins" now, and only what state can still say with a date (the eras it has
        /// reached and the school, clinic and road it has), dated now. Nothing else is invented.
        /// </summary>
        internal static void Begin(Simulation sim)
        {
            ChronicleState s = State(sim);
            if (s == null || !s.NeedsBegin) return;
            s.NeedsBegin = false;
            long now = sim.State.Clock.SimTime;
            Add(sim, ChronicleKind.Begins, now);
            if (sim.State.Era != null)
            {
                IReadOnlyList<EraDef> eras = sim.Config.Game.Onboarding.Eras;
                for (int i = 1; i <= sim.State.Era.Current && i < eras.Count; i++) AddEra(sim, eras[i].Id, now);
            }
            if (People.SchoolRules.State(sim)?.Status == SchoolStatus.Open) Add(sim, ChronicleKind.SchoolOpened, now);
            if (Town.ClinicRules.State(sim)?.Status == Town.ClinicStatus.Open) Add(sim, ChronicleKind.ClinicBuilt, now);
            if (Town.RoadRules.State(sim)?.Status == Town.RoadStatus.Built) Add(sim, ChronicleKind.RoadBuilt, now);
        }

        private static void AddEra(Simulation sim, string eraId, long at)
        {
            ChronicleKind kind;
            switch (eraId)
            {
                case "hamlet": kind = ChronicleKind.Hamlet; break;
                case "village": kind = ChronicleKind.Village; break;
                case "township": kind = ChronicleKind.Township; break;
                default: return;
            }
            Add(sim, kind, at, Progress.EraRules.Households(sim), 0, eraId);
        }

        // ---- the moments ----

        /// <summary>The event hook (called from <c>Simulation.Emit</c>): turns the events the Chronicle keeps into lines.</summary>
        internal static void Observe(Simulation sim, SimEvent e)
        {
            if (!On(sim)) return;
            AlmanacRules.Observe(sim, e);
            switch (e)
            {
                case EraUnlocked x:
                    AddEra(sim, x.EraId, x.SimTime);
                    break;
                case CitizenBorn x:
                    Add(sim, ChronicleKind.FirstChild, x.SimTime, x.Name, x.CitizenId);
                    break;
                case CitizenLiterate x:
                    Add(sim, x.How == LiterateHow.School ? ChronicleKind.FirstLiterateChild : ChronicleKind.FirstAdultReader, x.SimTime, x.Name, x.CitizenId);
                    break;
                case FestivalHeld x:
                    Add(sim, ChronicleKind.FirstFestival, x.SimTime, x.Kind);
                    break;
                case SchoolOpened x:
                    Add(sim, ChronicleKind.SchoolOpened, x.SimTime);
                    break;
                case ClinicBuilt x:
                    Add(sim, ChronicleKind.ClinicBuilt, x.SimTime);
                    break;
                case RoadBuilt x:
                    Add(sim, ChronicleKind.RoadBuilt, x.SimTime);
                    break;
                case OrchardBearing x:
                    Add(sim, ChronicleKind.FirstOrchard, x.SimTime, 0, 0, x.Tree);
                    break;
                case BusinessOpened x:
                {
                    Business.Business b = sim.State.Businesses?.Find(x.BusinessId);
                    if (b != null) Add(sim, ChronicleKind.BusinessFounded, x.SimTime, 0, b.Id, Business.BusinessRules.TypeOf(sim, b).Id); // the first of a season is kept
                    break;
                }
                case BusinessSold x:
                {
                    Business.SaleRecord rec = sim.State.Township?.Exit.Sales.Find(r => r.Id == x.SaleId);
                    Add(sim, ChronicleKind.BusinessSold, x.SimTime, x.Price, x.BusinessId, rec == null ? "" : rec.TypeId);
                    break;
                }
                case BusinessClosed x:
                    Add(sim, ChronicleKind.BusinessClosed, x.SimTime, x.ToYou, x.BusinessId, x.TypeId);
                    break;
                case FireRepaired x:
                    Add(sim, ChronicleKind.FireSurvived, x.SimTime, x.Cost);
                    break;
                case EpidemicEnded x:
                    Add(sim, ChronicleKind.EpidemicSurvived, x.SimTime, (Progress.EraRules.Households(sim) * x.FactorPercent + 99) / 100); // rounded up: a town with a household and an epidemic had at least one family ill
                    break;
                case LoanCleared x:
                    Add(sim, ChronicleKind.LoanCleared, x.SimTime, x.Amount, x.LoanId, x.LenderId);
                    break;
                case PetitionEnded x:
                    if (x.Status == Town.PetitionStatus.Kept) Add(sim, ChronicleKind.PetitionKept, x.SimTime, 0, x.Topic);
                    break;
                case CropHarvested x:
                    Harvested(sim, x);
                    break;
                case SeasonClosed x:
                    RecordHarvest(sim, x.SimTime);
                    AlmanacRules.Closed(sim, x);
                    break;
            }
        }

        /// <summary>The first settler (§3.2): the first citizen of the town who is not the founder's own household, as they arrive.</summary>
        internal static void Arrived(Simulation sim, Citizen c)
        {
            Add(sim, ChronicleKind.FirstSettler, sim.State.Clock.SimTime, c.Name, c.Id);
        }

        /// <summary>The first profit (§3.2): the first business season report above nothing.</summary>
        internal static void Profit(Simulation sim, Business.Business b)
        {
            Add(sim, ChronicleKind.FirstProfit, sim.State.Clock.SimTime, b.Id, 0, Business.BusinessRules.TypeOf(sim, b).Id);
        }

        // ---- a record harvest (§3.3) ----

        private static void Harvested(Simulation sim, CropHarvested e)
        {
            IReadOnlyList<CropDef> crops = sim.Config.Game.Farm.Crops;
            for (int i = 0; i < crops.Count && i < State(sim).SeasonGrams.Length; i++)
                if (crops[i].Id == e.CropId) { State(sim).SeasonGrams[i] += e.Grams; return; }
        }

        /// <summary>
        /// At each close: a crop whose season total is at least the floor and at least <c>recordRise</c> above its best earlier season is a
        /// record; one a season, the crop with the biggest rise. A crop's first season past the floor sets its mark and is not a record.
        /// The line keeps this season's grams and the old best.
        /// </summary>
        private static void RecordHarvest(Simulation sim, long at)
        {
            ChronicleState s = State(sim);
            ChronicleDefs d = Defs(sim);
            int best = -1;
            long bestRise = 0, bestGrams = 0, bestOld = 0;
            for (int i = 0; i < s.SeasonGrams.Length; i++)
            {
                long g = s.SeasonGrams[i], old = s.BestGrams[i];
                if (g >= d.RecordMinGrams)
                {
                    long rise = old > 0 ? (g - old) * 1000 / old : 0;
                    if (old > 0 && g >= old + Money.MulRate(old, d.RecordRise) && rise > bestRise) { best = i; bestRise = rise; bestGrams = g; bestOld = old; }
                    if (g > old) s.BestGrams[i] = g;
                }
                s.SeasonGrams[i] = 0;
            }
            if (best >= 0) Add(sim, ChronicleKind.RecordHarvest, at, bestGrams, bestOld, sim.Config.Game.Farm.Crops[best].Id);
        }
    }
}
