using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.People;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Helpers for the spec 006 tests.</summary>
    public static class PeopleKit
    {
        public const long S = FarmTest.S;
        public const int HomeTile = 84; // an inherited farm acre (parcel home_a)
        public const int QuartersTile = 119; // the labour quarters beside the yard: hands' huts go here (ADR 0040)
        public const int YardTile = 87;
        public const long Coin = 100;

        /// <summary>Rich content, no inflation, crops in any season: year-0 prices and exact examples.</summary>
        public static GameDefs Game(System.Func<string, string, string> edit = null)
        {
            return NoInflation((f, j) =>
            {
                if (f == "start.json") j = j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000");
                if (f == "crops.json")
                    foreach (string s in new[] { "[\"winter\"]", "[\"summer\", \"monsoon\"]", "[\"spring\", \"winter\"]", "[\"summer\", \"winter\"]" })
                        j = j.Replace("\"sowSeasons\": " + s, "\"sowSeasons\": [\"spring\", \"summer\", \"monsoon\", \"winter\"]");
                return edit == null ? j : edit(f, j);
            });
        }

        public static Simulation Start(GameDefs game = null, ulong seed = 42, long localSeconds = 0)
        {
            return NewFarm(seed, localSeconds, game ?? Game());
        }

        public static PeopleState P(Simulation sim) { return sim.State.People; }

        public static void Ok(Simulation sim, Command c)
        {
            CommandResult r = sim.Execute(c);
            Assert.That(r.Accepted, Is.True, c.GetType().Name + ": " + r);
        }

        /// <summary>Builds a hand's hut on the tile and waits until it's finished.</summary>
        public static void BuildHut(Simulation sim, int tile = QuartersTile)
        {
            long t = sim.State.Clock.SimTime;
            Ok(sim, new BuildHandHut(t, tile));
            sim.AdvanceTo(t + 24 * Hour);
        }

        /// <summary>Advances live until an applicant is waiting.</summary>
        public static Applicant WaitForApplicant(Simulation sim)
        {
            for (int i = 0; i < 24 * 8 && P(sim).Applicants.Count == 0; i++) sim.AdvanceTo(sim.State.Clock.SimTime + Hour);
            Assert.That(P(sim).Applicants, Is.Not.Empty, "no applicant came");
            return P(sim).Applicants[0];
        }

        /// <summary>
        /// A hand under contract: builds a hut, then offers to applicants until one accepts (a seeded game, so the same every run).
        /// </summary>
        public static Hand HireHand(Simulation sim, int tile = QuartersTile)
        {
            if (PeopleRules.EmptyHut(sim) == null) BuildHut(sim, tile);
            for (int tries = 0; tries < 20; tries++)
            {
                Applicant a = WaitForApplicant(sim);
                Ok(sim, new OfferContract(sim.State.Clock.SimTime, a.Id));
                if (P(sim).Hands.Count > 0 && P(sim).Hands.Last().HiredAt == sim.State.Clock.SimTime) return P(sim).Hands.Last();
            }
            Assert.Fail("nobody accepted");
            return null;
        }

        /// <summary>Sets cash to <paramref name="amount"/> with a balanced test entry against opening equity.</summary>
        public static void CashTo(Simulation sim, long amount)
        {
            Ok(sim, new SetCash(sim.State.Clock.SimTime, amount));
        }

        private sealed class SetCash : TimedCommand
        {
            private readonly long _amount;

            public SetCash(long at, long amount) : base(at) { _amount = amount; }

            protected override CommandResult ApplyAt(Simulation sim)
            {
                long d = _amount - sim.State.Ledger.Cash;
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.opening", "test",
                    new Finance.Posting(Finance.Account.Cash, d), new Finance.Posting(Finance.Account.OpeningEquity, -d));
                return CommandResult.Ok;
            }
        }

        public static T Last<T>(Simulation sim) where T : SimEvent { return sim.PendingEvents.OfType<T>().LastOrDefault(); }
    }
}
