using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.History;
using Sim.Core.Saves;

namespace Sim.Core.Tests
{
    /// <summary>US-157 (spec 029 §3.1 to §3.3, ADR 0165, 0166): the town's Chronicle writes its lines from events, once or once a season, keeps 400, and starts empty in an older save.</summary>
    public partial class ChronicleTests
    {
        private const long Day = 86_400;

        private static Simulation Start() { return TownshipTests.Start(); }

        private static List<ChronicleEntry> Of(Simulation sim, ChronicleKind k) { return sim.State.Chronicle.Entries.FindAll(e => e.Kind == k); }

        /// <summary>The town as a schema older than 24 would hold it: the Chronicle (schema 24) written nowhere yet, so a write at an older schema is the same town.</summary>
        internal static void Forget(ChronicleState c)
        {
            c.Entries.Clear();
            c.Done = 0;
            System.Array.Clear(c.SeasonGrams, 0, c.SeasonGrams.Length);
            System.Array.Clear(c.BestGrams, 0, c.BestGrams.Length);
            c.Almanac = new AlmanacState(c.SeasonGrams.Length);
        }

        private static long Season(Simulation sim) { return sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason; }

        // AC-1: a new town opens with its founding line; each once-only kind is written once, at its event, with its numbers.
        [Test]
        public void Once_Only()
        {
            Simulation sim = Start();
            Assert.That(Of(sim, ChronicleKind.Founding).Count, Is.EqualTo(1), "the first line of a new town");
            Assert.That(Of(sim, ChronicleKind.Founding)[0].A, Is.GreaterThan(0), "with the acres it began with");
            long t = sim.State.Clock.SimTime;
            sim.Emit(new SchoolOpened(t));
            int n = sim.State.Chronicle.Entries.Count;
            sim.Emit(new SchoolOpened(t + 5));
            Assert.That(sim.State.Chronicle.Entries.Count, Is.EqualTo(n), "a second school writes nothing");
            sim.Emit(new CitizenBorn(t, 7, 3, true, 1, 2));
            sim.Emit(new CitizenBorn(t + 9, 8, 4, false, 1, 2));
            ChronicleEntry child = Of(sim, ChronicleKind.FirstChild).Single();
            Assert.That((child.A, child.B, child.At), Is.EqualTo((3L, 7L, t)), "the first child: their name, their id, when");
            sim.Emit(new CitizenLiterate(t, 11, 5, LiterateHow.School));
            sim.Emit(new CitizenLiterate(t, 12, 6, LiterateHow.AdultClass));
            sim.Emit(new CitizenLiterate(t, 13, 7, LiterateHow.School));
            sim.Emit(new CitizenLiterate(t, 14, 8, LiterateHow.AdultClass));
            Assert.That((Of(sim, ChronicleKind.FirstLiterateChild).Single().B, Of(sim, ChronicleKind.FirstAdultReader).Single().B), Is.EqualTo((11L, 12L)), "the first of each");
            sim.Emit(new EraUnlocked(t, "village"));
            sim.Emit(new EraUnlocked(t, "village"));
            sim.Emit(new EraUnlocked(t, "homestead")); // not an era the Chronicle keeps
            Assert.That(Of(sim, ChronicleKind.Village).Count, Is.EqualTo(1));
            Assert.That(Of(sim, ChronicleKind.Village)[0].Id, Is.EqualTo("village"));
            sim.Emit(new ClinicBuilt(t)); sim.Emit(new RoadBuilt(t)); sim.Emit(new FestivalHeld(t, 2, 0, 0, t, 100));
            Assert.That(new[] { ChronicleKind.ClinicBuilt, ChronicleKind.RoadBuilt, ChronicleKind.FirstFestival }.All(k => Of(sim, k).Count == 1), Is.True);
            Assert.That(Of(sim, ChronicleKind.FirstFestival)[0].A, Is.EqualTo(2));
        }

        // AC-2: a repeating kind writes at most one line a season, the biggest kept; the next season writes again.
        [Test]
        public void Repeating()
        {
            Simulation sim = Start();
            long t = sim.State.Clock.SimTime;
            sim.Emit(new FireRepaired(t, 1, false, 5_000));
            sim.Emit(new FireRepaired(t + 60, 2, false, 3_000));
            Assert.That(Of(sim, ChronicleKind.FireSurvived).Select(e => e.A), Is.EqualTo(new long[] { 5_000 }), "the smaller second one is dropped");
            sim.Emit(new FireRepaired(t + 120, 3, false, 9_000));
            Assert.That(Of(sim, ChronicleKind.FireSurvived).Select(e => e.A), Is.EqualTo(new long[] { 9_000 }), "a bigger one takes its place");
            sim.Emit(new FireRepaired(t + 2 * sim.Clock.SecondsPerSeason, 4, false, 1_000));
            Assert.That(Of(sim, ChronicleKind.FireSurvived).Select(e => e.A), Is.EqualTo(new long[] { 9_000, 1_000 }), "a new season, a new line");
            sim.Emit(new LoanCleared(t, 5, "coop_bank", 40_000));
            sim.Emit(new EpidemicEnded(t, 30));
            Assert.That(Of(sim, ChronicleKind.LoanCleared).Single().Id, Is.EqualTo("coop_bank"));
            Assert.That(Of(sim, ChronicleKind.EpidemicSurvived).Single().A, Is.EqualTo((Sim.Core.Progress.EraRules.Households(sim) * 30 + 99) / 100), "the families it made ill, rounded up");
        }

        // AC-3: a record harvest needs 1,000 kg and 5% over the best earlier season; the first season past the floor sets the mark; one line a season.
        [Test]
        public void Record_Harvest()
        {
            Simulation sim = Start();
            string wheat = sim.Config.Game.Farm.Crops[0].Id, rice = sim.Config.Game.Farm.Crops[1].Id;
            long t = sim.State.Clock.SimTime;
            void Season(long kgWheat, long kgRice)
            {
                if (kgWheat > 0) sim.Emit(new CropHarvested(t, 1, wheat, kgWheat * 1000, null));
                if (kgRice > 0) sim.Emit(new CropHarvested(t, 2, rice, kgRice * 1000, null));
                sim.Emit(new SeasonClosed(t, 0, null));
                t += sim.Clock.SecondsPerSeason;
            }
            Season(1_200, 0);
            Assert.That(Of(sim, ChronicleKind.RecordHarvest), Is.Empty, "the first season past the floor sets its mark");
            Season(1_240, 0);
            Assert.That(Of(sim, ChronicleKind.RecordHarvest), Is.Empty, "3% over is not enough");
            Season(900, 0);
            Season(1_350, 0);
            ChronicleEntry r = Of(sim, ChronicleKind.RecordHarvest).Single();
            Assert.That((r.Id, r.A, r.B), Is.EqualTo((wheat, 1_350_000L, 1_240_000L)), "9% over the 1,240 kg best (an under-floor season changes nothing)");
            Season(1_500, 1_000);
            Assert.That(Of(sim, ChronicleKind.RecordHarvest).Last().Id, Is.EqualTo(wheat), "rice's first season past the floor only sets its mark");
            Season(1_700, 1_200);
            ChronicleEntry last = Of(sim, ChronicleKind.RecordHarvest).Last();
            Assert.That((last.Id, last.A, last.B), Is.EqualTo((rice, 1_200_000L, 1_000_000L)), "one line a season: rice's 20% rise beat wheat's 13%");
            Assert.That(Of(sim, ChronicleKind.RecordHarvest).Count, Is.EqualTo(3));
        }

        // AC-4: 400 lines kept; the oldest repeating line goes first and the pinned ones never.
        [Test]
        public void Cap()
        {
            Simulation sim = Start();
            sim.Config.Game.Chronicle.Keep = 100;
            long t = sim.State.Clock.SimTime;
            int pinned = sim.State.Chronicle.Entries.Count(e => ChronicleDefs.Pinned(e.Kind));
            sim.Emit(new SchoolOpened(t));
            for (int i = 0; i < 250; i++) sim.Emit(new BusinessClosed(t + i * sim.Clock.SecondsPerSeason, 100 + i, "veg_stall", 1_000 + i, 0));
            Assert.That(sim.State.Chronicle.Entries.Count, Is.EqualTo(100));
            Assert.That(sim.State.Chronicle.Entries.Count(e => ChronicleDefs.Pinned(e.Kind)), Is.EqualTo(pinned + 1), "the founding and the firsts stay");
            Assert.That(Of(sim, ChronicleKind.BusinessClosed).Last().B, Is.EqualTo(100 + 249), "the newest stays");
            Assert.That(Of(sim, ChronicleKind.BusinessClosed).First().B, Is.GreaterThan(100), "the oldest went");
        }

        // The Chronicle's lines and marks round-trip through a save; the hash follows the lines.
        [Test]
        public void Saves_Round_Trip()
        {
            Simulation sim = Start();
            long t = sim.State.Clock.SimTime;
            sim.Emit(new CropHarvested(t, 1, sim.Config.Game.Farm.Crops[0].Id, 1_500_000, null));
            sim.Emit(new FireRepaired(t, 1, true, 7_000));
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, config, 1_700_000_000), config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Assert.That(r.State.Chronicle.Entries.Select(e => (e.Kind, e.A, e.Id)), Is.EqualTo(sim.State.Chronicle.Entries.Select(e => (e.Kind, e.A, e.Id))));
            Assert.That(r.State.Chronicle.SeasonGrams[0], Is.EqualTo(1_500_000));
        }
    }
}
