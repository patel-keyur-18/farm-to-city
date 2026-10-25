using Sim.Core.Commands;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Livestock;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Progress;
using Sim.Core.Saves;
using Sim.Core.Scheduling;
using Sim.Core.Time;
using Sim.Core.Weather;

namespace Sim.Core
{
    /// <summary>
    /// Everything the simulation persists (spec 001 §4). The UI only reads it; it changes only through commands.
    /// </summary>
    public sealed class GameState
    {
        /// <summary>Bumped whenever the canonical write order changes, so stale golden hashes fail loudly.</summary>
        public const int HashFormatVersion = 2;

        /// <summary>The world seed, set at new game.</summary>
        public ulong Seed { get; internal set; }

        public ClockState Clock { get; internal set; } = new ClockState();

        public Scheduler Scheduler { get; internal set; } = new Scheduler();

        public JobTable Jobs { get; internal set; } = new JobTable();

        public WeatherState Weather { get; internal set; } = new WeatherState();

        /// <summary>Land and soil (spec 002); null in a time-and-weather-only sim.</summary>
        public LandState Land { get; internal set; }

        /// <summary>The founder's ledger (spec 005); null in a time-and-weather-only sim.</summary>
        public Ledger Ledger { get; internal set; }

        /// <summary>Prepayments and season reports (spec 005); null in a time-and-weather-only sim.</summary>
        public FinanceState Finance { get; internal set; }

        /// <summary>Prices, the trader and the store (spec 004); null in a time-and-weather-only sim.</summary>
        public MarketState Market { get; internal set; }

        /// <summary>Crops, the garden, trees and the founder (spec 003); null in a time-and-weather-only sim.</summary>
        public FarmState Farm { get; internal set; }

        /// <summary>The credit score and loans (spec 005); null when the content has no lenders (ADR 0038).</summary>
        public CreditState Credit { get; internal set; }

        /// <summary>Day labourers, hands, huts and orders (spec 006); null when the content has no people.</summary>
        public PeopleState People { get; internal set; }

        /// <summary>The first session and the letter (spec 009); null when the content has no onboarding.</summary>
        public GuidanceState Guidance { get; internal set; }

        /// <summary>The era and the next era's progress (spec 009); null when the content has no onboarding.</summary>
        public EraState Era { get; internal set; }

        /// <summary>Cattle (spec 012); null when the content has no cattle shed.</summary>
        public LivestockState Livestock { get; internal set; }

        /// <summary>Citizens and households (spec 014); null when the content has no family house.</summary>
        public CitizensState Citizens { get; internal set; }

        /// <summary>The founder's businesses (spec 016); null when the content has none.</summary>
        public Business.BusinessesState Businesses { get; internal set; }

        /// <summary>Accepted commands, for replay. Not hashed: it's input, not state.</summary>
        public CommandLog CommandLog { get; internal set; } = new CommandLog();

        /// <summary>
        /// Writes the canonical byte stream: a fixed field order, entities in id order.
        /// Each later spec appends its own section at the end. The command log is never hashed.
        /// </summary>
        public void WriteTo(IStateWriter h)
        {
            h.WriteInt(HashFormatVersion);
            h.WriteULong(Seed);
            if (!h.Hashing) h.WriteByte(Sections()); // a save says which optional sections follow
            Clock.WriteTo(h);
            Scheduler.WriteTo(h);
            Jobs.WriteTo(h);
            Weather.WriteTo(h);
            // Spec 002 and 005 sections, present only when the sim has game content, so a
            // time-and-weather-only state (golden 001) hashes as before.
            if (Land != null) Land.WriteTo(h);
            if (Ledger != null) Ledger.WriteTo(h);
            if (Finance != null) Finance.WriteTo(h);
            if (Market != null) Market.WriteTo(h);
            if (Farm != null) Farm.WriteTo(h);
            if (Credit != null) Credit.WriteTo(h);
            if (People != null) People.WriteTo(h);
            if (Guidance != null)
            {
                Guidance.WriteTo(h, _stepCount);
                Era.WriteTo(h);
            }
            // Spec 012, after everything else: content without a cattle shed (the golden replays' frozen content) hashes as before.
            if (Livestock != null) Livestock.WriteTo(h);
            // Spec 014, last: content without a family house (the golden replays' frozen content) hashes as before.
            if (Citizens != null && !(h is SaveWriter sw && sw.Schema < 9)) Citizens.WriteTo(h);
            // Spec 016, last: content without businesses hashes as before.
            if (Businesses != null && !(h is SaveWriter bw && bw.Schema < 10)) Businesses.WriteTo(h);
        }

        /// <summary>The first session's step count, so a finished session saves as "none" (set with the onboarding state).</summary>
        internal int _stepCount;

        private byte Sections()
        {
            return (byte)((Land != null ? 1 : 0) | (Ledger != null ? 2 : 0) | (Finance != null ? 4 : 0) | (Market != null ? 8 : 0)
                | (Farm != null ? 16 : 0) | (Credit != null ? 32 : 0) | (People != null ? 64 : 0) | (Guidance != null ? 128 : 0));
        }

        /// <summary>
        /// Reads what <see cref="WriteTo"/> wrote to a save (spec 007 §3.1). The optional sections must be the ones the content
        /// asks for: a save with people can't load into a build without them, nor the other way round.
        /// </summary>
        internal static GameState ReadFrom(SaveReader r, SimConfig config)
        {
            if (r.ReadInt() != HashFormatVersion) throw new SaveFormatException("state format");
            var s = new GameState { Seed = r.ReadULong() };
            Definitions.GameDefs g = config.Game;
            int expected = g == null ? 0 : 31 | (g.Credit != null ? 32 : 0) | (g.People != null ? 64 : 0) | (g.Onboarding != null ? 128 : 0);
            int sections = r.ReadByte();
            if (sections != expected) throw new SaveContentException("the save's sections (" + sections + ") don't match the content's (" + expected + ")");
            s.Clock = ClockState.ReadFrom(r);
            s.Scheduler = Scheduler.ReadFrom(r);
            s.Jobs = JobTable.ReadFrom(r);
            s.Weather = WeatherState.ReadFrom(r);
            if (g == null) return s;
            s.Land = LandState.ReadFrom(r, g.Map);
            s.Ledger = Ledger.ReadFrom(r);
            s.Finance = FinanceState.ReadFrom(r);
            s.Market = MarketState.ReadFrom(r, g.Market.Market.UnmetWindowHours, g.Market.Truck, s.Clock.SimTime);
            s.Farm = FarmState.ReadFrom(r, s.Land.Tiles.Count);
            if (g.Credit != null) s.Credit = CreditState.ReadFrom(r);
            if (g.People != null) s.People = PeopleState.ReadFrom(r, s.Clock.SimTime);
            if (g.Onboarding != null)
            {
                s._stepCount = g.Onboarding.Steps.Count;
                s.Guidance = GuidanceState.ReadFrom(r, s._stepCount);
                s.Era = EraState.ReadFrom(r);
            }
            if (g.Livestock != null) s.Livestock = r.Schema >= 7 ? LivestockState.ReadFrom(r) : new LivestockState { LastFeeding = new FeedingReport() }; // schema 6: no cattle yet
            if (g.Population != null && r.Schema >= 9) s.Citizens = CitizensState.ReadFrom(r); // older towns: CitizenRules.Migrate after loading
            if (g.Businesses != null) s.Businesses = r.Schema >= 10 ? Business.BusinessesState.ReadFrom(r) : new Business.BusinessesState(); // schema 9: none yet
            return s;
        }

        /// <summary>The 64-bit FNV-1a hash of the canonical state.</summary>
        public ulong ComputeHash()
        {
            var h = new StateHasher();
            WriteTo(h);
            return h.Value;
        }

        /// <summary>A deep copy (in-memory round-trip; saving to disk is spec 007).</summary>
        public GameState Clone(bool withCommandLog = true)
        {
            return new GameState
            {
                Seed = Seed,
                Clock = Clock.Clone(),
                Scheduler = Scheduler.Clone(),
                Jobs = Jobs.Clone(),
                Weather = Weather.Clone(),
                Land = Land?.Clone(),
                Ledger = Ledger?.Clone(),
                Finance = Finance?.Clone(),
                Market = Market?.Clone(),
                Farm = Farm?.Clone(),
                Credit = Credit?.Clone(),
                People = People?.Clone(),
                Guidance = Guidance?.Clone(),
                Era = Era?.Clone(),
                Livestock = Livestock?.Clone(),
                Citizens = Citizens?.Clone(),
                Businesses = Businesses?.Clone(),
                _stepCount = _stepCount,
                CommandLog = withCommandLog ? CommandLog.Clone() : new CommandLog(),
            };
        }
    }
}
