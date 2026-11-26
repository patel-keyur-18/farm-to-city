using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;

namespace Sim.View.Views
{
    /// <summary>
    /// One business run by you (spec 030 §3.1, §7.3, US-266): the reason a second can't be run yourself, and the card an older town gets at load
    /// when it runs two or more.
    /// </summary>
    public static class RunOne
    {
        /// <summary>A card button's id: this prefix and the business's id.</summary>
        public const string KeepPrefix = "keep:";

        /// <summary>The reason you can't run another business yourself, from the string <paramref name="key"/> naming the one you run, or null.</summary>
        public static string Blocked(ViewContext ctx, string key)
        {
            Business running = BusinessRules.RunByYouNow(ctx.Sim);
            if (running == null) return null;
            return ctx.Strings.Get(key, "name", BusinessWords.InSentence(ctx, BusinessRules.TypeOf(ctx.Sim, running)));
        }

        /// <summary>True when the town runs more than one business itself: it must choose which to keep (older saves only).</summary>
        public static bool MustChoose(Simulation sim) { return BusinessRules.RunByYou(sim).Count > 1; }

        /// <summary>The card at load: a row for each business you run, with its profit this season and its hours, and a button to keep running it. No other button.</summary>
        public static CardVm Card(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var c = new CardVm { Title = s.Get("runone.title"), Text = s.Get("runone.text"), Hint = s.Get("runone.hint") };
            foreach (Business b in BusinessRules.RunByYou(sim))
            {
                BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
                long profit = BusinessBooks.NetProfit(BusinessBooks.Report(b, sim.Period));
                c.Choices.Add(new CardChoice(KeepPrefix + b.Id, t.Id, BusinessWords.Name(ctx, t),
                    s.Get("runone.detail", "profit", ctx.Fmt.Coins(profit), "hours", t.OpenSpan(b.OpenHour, b.CloseHour).ToString()), s.Get("runone.keep")));
            }
            return c;
        }

        /// <summary>The command a <see cref="KeepPrefix"/> button sends, or null for any other id.</summary>
        public static Command Answer(Simulation sim, string buttonId)
        {
            if (buttonId == null || !buttonId.StartsWith(KeepPrefix, System.StringComparison.Ordinal)) return null;
            return long.TryParse(buttonId.Substring(KeepPrefix.Length), out long id) ? new KeepRunningYourself(sim.State.Clock.SimTime, id) : null;
        }

        /// <summary>"You run the flour mill. The tea stall waits for a hire…": said after the answer.</summary>
        public static string Kept(ViewContext ctx, long keptId, IReadOnlyList<long> stoppedIds)
        {
            Simulation sim = ctx.Sim;
            string Name(long id) { Business b = sim.State.Businesses?.Find(id); return b == null ? "" : BusinessWords.InSentence(ctx, BusinessRules.TypeOf(sim, b)); }
            var others = new List<string>();
            foreach (long id in stoppedIds) others.Add(Name(id));
            string joined = string.Join(" and the ", others);
            return ctx.Strings.Plural("runone.kept", stoppedIds.Count, "kept", Name(keptId), "others", joined);
        }
    }
}
