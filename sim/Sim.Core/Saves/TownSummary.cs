using System.Globalization;
using System.Text;

namespace Sim.Core.Saves
{
    /// <summary>
    /// What the other town's row on Settings → Your towns shows (spec 031 §3): era, day, net worth and when it was last saved. Written
    /// beside a town's save as <c>summary.json</c> at each save; it is not part of the save and is never hashed.
    /// </summary>
    public sealed class TownSummary
    {
        public const string FileName = "summary.json";

        /// <summary>The era's content id ("hamlet"), or null when only the save's header is known.</summary>
        public string Era { get; }

        /// <summary>The game day, counted from 1.</summary>
        public int Day { get; }

        /// <summary>Net worth in 1/100 coin, or <see cref="Unknown"/> when only the save's header was read.</summary>
        public long NetWorth { get; }
        public const long Unknown = long.MinValue;
        public long SavedWallUtc { get; }

        public TownSummary(string era, int day, long netWorth, long savedWallUtc)
        {
            Era = era;
            Day = day;
            NetWorth = netWorth;
            SavedWallUtc = savedWallUtc;
        }

        /// <summary>The summary of a town as it stands now.</summary>
        public static TownSummary Of(Simulation sim, long savedWallUtc)
        {
            string era = null;
            if (sim.State.Era != null && sim.Config.Game != null && sim.Config.Game.Onboarding != null && sim.State.Era.Current < sim.Config.Game.Onboarding.Eras.Count)
                era = sim.Config.Game.Onboarding.Eras[sim.State.Era.Current].Id;
            return new TownSummary(era, (int)(sim.State.Clock.SimTime / 86400) + 1, sim.State.Ledger == null ? 0 : Finance.Reporting.NetWorth(sim).Total, savedWallUtc);
        }

        public string ToJson()
        {
            var sb = new StringBuilder("{");
            if (Era != null) sb.Append("\"era\":\"").Append(Era).Append("\",");
            sb.Append("\"day\":").Append(Day.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"netWorth\":").Append(NetWorth.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"saved\":").Append(SavedWallUtc.ToString(CultureInfo.InvariantCulture)).Append('}');
            return sb.ToString();
        }

        /// <summary>Reads what <see cref="ToJson"/> wrote; null when the text isn't one (the row then falls back to the save's header).</summary>
        public static TownSummary Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            if (!Number(json, "day", out long day) || !Number(json, "netWorth", out long worth) || !Number(json, "saved", out long saved)) return null;
            return new TownSummary(Word(json, "era"), (int)day, worth, saved);
        }

        private static bool Number(string json, string key, out long value)
        {
            value = 0;
            int at = json.IndexOf("\"" + key + "\":", System.StringComparison.Ordinal);
            if (at < 0) return false;
            int i = at + key.Length + 3, j = i;
            while (j < json.Length && (json[j] == '-' || char.IsDigit(json[j]))) j++;
            return j > i && long.TryParse(json.Substring(i, j - i), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static string Word(string json, string key)
        {
            int at = json.IndexOf("\"" + key + "\":\"", System.StringComparison.Ordinal);
            if (at < 0) return null;
            int i = at + key.Length + 4, end = json.IndexOf('"', i);
            return end < 0 ? null : json.Substring(i, end - i);
        }
    }
}
