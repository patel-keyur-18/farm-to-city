using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Finance;
using Sim.Core.People;
using Sim.Core.Progress;
using Sim.Core.Saves;

namespace Tools.Balancer
{
    /// <summary>
    /// The Phase 2 playtest town (US-237, ADR 0101 §8): <see cref="VillageBot"/> (credit, homes first) plays a new game to the close after its
    /// third household, and the town is written as a save the owner imports on the phone. Stamped with the wall time it is written, from
    /// the build's commit, so importing it straight away loses minutes.
    /// </summary>
    public static class Playtest
    {
        public const int ThirdHousehold = 3;

        public static int Save(SimConfig config, ulong seed, string path, string commit)
        {
            Simulation sim = Simulation.NewGame(config, new NewGame(seed, Now(), 8 * 3600));
            if (sim.State.Guidance != null) sim.Execute(new SkipFirstSession(sim.State.Clock.SimTime));
            var bot = new VillageBot(false, "HMS");
            bot.Start(sim);
            int reportsAtThird = -1;
            long end = 40 * sim.Clock.SecondsPerSeason;
            for (long t = sim.State.Clock.SimTime + 600; t < end; t += 3600)
            {
                sim.AdvanceTo(t);
                bot.Play(sim, t);
                sim.DrainEvents();
                if (reportsAtThird < 0 && EraRules.Households(sim) >= ThirdHousehold) reportsAtThird = sim.State.Finance.Reports.Count;
                if (reportsAtThird >= 0 && sim.State.Finance.Reports.Count > reportsAtThird) break;
            }
            if (reportsAtThird < 0 || sim.State.Finance.Reports.Count <= reportsAtThird) { Console.Error.WriteLine("The bot never reached its third household and the close after it."); return 1; }

            long now = Now();
            sim.State.Clock.Reanchor(now); // the town is as it was at this moment: nothing has passed since
            byte[] bytes = SaveCodec.Write(sim.State, config, now);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllBytes(path, bytes);
            Describe(sim, seed, path, bytes.Length, commit);
            return 0;
        }

        private static long Now() { return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); }

        private static string Coins(long money) { return (money / 100).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("en-IN")); }

        /// <summary>The town's state for the owner, before the file goes to the phone (US-237.T2).</summary>
        public static void Describe(Simulation sim, ulong seed, string path, int bytes, string commit)
        {
            var d = sim.Config.Game;
            var s = sim.State;
            long day = s.Clock.SimTime / 86400;
            Console.WriteLine("The playtest town, seed " + seed + ", from commit " + commit + ", " + bytes / 1024 + " KB: " + path);
            Console.WriteLine("  Game day " + day + " (" + (sim.Period / 4) + " years, season " + (sim.Period % 4) + " of the year), " + s.Finance.Reports.Count + " seasons closed. Era: " + d.Onboarding.Eras[s.Era.Current].Id + ".");
            NetWorthSnapshot worth = Reporting.NetWorth(sim);
            Console.WriteLine("  Cash " + Coins(s.Ledger.Cash) + " · savings " + Coins(worth.Deposit) + " · net worth " + Coins(worth.Total) + " · owed " + Coins(Hub.Owed(worth)) + " (debt to assets " + Hub.DebtPercent(worth) + "%).");
            if (s.Credit != null)
            {
                foreach (Loan l in s.Credit.Loans.Where(x => x.IsOpen))
                    Console.WriteLine("    Loan: " + d.Credit.Lenders[l.Lender].Id + (l.BusinessId != 0 ? " (a business's)" : "") + ", owes " + Coins(CreditRules.OwedNow(sim, l)) + ", " + l.Remaining + " seasons left, next payment " + Coins(CreditRules.NextPayment(sim, l, out _, out _)));
                Console.WriteLine("    Credit score " + s.Credit.Score);
            }
            Console.WriteLine("  People: " + (s.People == null ? 0 : s.People.Hands.Count) + " hand(s), " + (s.People == null ? 0 : s.People.Workers.Count) + " worker(s), reputation " + (s.People == null ? 0 : s.People.Reputation) + "; "
                + (s.Citizens == null ? 0 : s.Citizens.Households.Count) + " household(s) in your houses (the Village needs " + Town.Households + "), "
                + (s.Citizens == null ? 0 : s.Citizens.Constructions.Count) + " house(s) being built.");
            var crops = s.Farm.Crops.GroupBy(c => d.Farm.Crops[c.Def].Id).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + " ×" + g.Count());
            Console.WriteLine("  Fields: " + s.Farm.Crops.Count + " sown (" + string.Join(", ", crops) + "); " + s.Land.Parcels.Count(p => p.Owned) + " parcels owned; " + s.Farm.Trees.Count + " trees.");
            Console.WriteLine("  Cattle: " + (s.Livestock == null ? 0 : s.Livestock.Animals.Count) + " animal(s). Businesses: " + (s.Businesses == null ? 0 : s.Businesses.Businesses.Count) + ". The school: " + (SchoolRules.IsOpen(sim) ? "open" : "not open") + ".");
            Console.WriteLine("  The Village's three: households " + EraRules.Households(sim) + " of " + Town.Households + ", a business's profitable seasons " + EraRules.BusinessSeasons(sim) + " of 2, the school " + (SchoolRules.IsOpen(sim) ? "open" : "not open") + ".");
            var milestones = MilestoneRules.State(sim)?.Reached.Select(m => m.Id + "@" + m.Period) ?? Enumerable.Empty<string>();
            Console.WriteLine("  Milestones reached: " + (milestones.Any() ? string.Join(", ", milestones) : "none") + ".");
        }
    }
}
