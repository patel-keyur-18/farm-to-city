using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>How a Codex card's number is shown (spec 018 §3.3): money, a whole number, a percent, a factor, fertility to one decimal.</summary>
    public enum CodexNumber { Coins, Count, Percent, Factor, Decimal1 }

    /// <summary>
    /// A Concept Codex card (spec 018 §3.3, ADR 0095 §7): its group, the moment that finds it (a trigger kind the view layer knows, with
    /// its threshold where it has one) and how each of the player's numbers it quotes is shown. Its words are in the string table
    /// (<c>codex.&lt;id&gt;.title</c>, <c>.text</c>, <c>.numbers</c>, <c>.world</c>).
    /// </summary>
    public sealed class CodexCardDef
    {
        public string Id { get; internal set; }

        /// <summary>Index into <see cref="CodexDefs.Groups"/>.</summary>
        public int Group { get; internal set; }

        /// <summary>The trigger's kind, one of <see cref="CodexDefs.TriggerKinds"/>; the card's id is the usual one.</summary>
        public string Trigger { get; internal set; }

        /// <summary>The trigger's threshold, where it has one (the price index, fertility, a business's seasons), else zero.</summary>
        public Fixed Threshold { get; internal set; }

        public IReadOnlyList<CodexNumber> Numbers { get; internal set; }
    }

    /// <summary><c>codex.json</c>: the Codex's groups in order (each with its icon) and its cards in order.</summary>
    public sealed class CodexDefs
    {
        /// <summary>The moments the view layer can find a card at (spec 018 §3.3's table).</summary>
        public static readonly string[] TriggerKinds =
        {
            "diminishingReturns", "soilFertility", "opportunityCost",
            "profitNotCash", "interest", "creditScore", "leverage", "liquidity", "inflation", "workingCapital",
            "grossMargin", "fixedVariable", "breakEven", "forecastActual", "unitEconomics",
            "depreciation", "appreciation", "yield",
            "hiringUncertainty", "marketWage", "retention", "trainingRoi", "employerReputation",
            "publicGoods", "longTermInvestment",
            "publicMoney", "budgetPriority", "tiedGrants",
        };

        /// <summary>Kinds that take a threshold.</summary>
        public static readonly string[] WithThreshold = { "soilFertility", "inflation", "forecastActual" };

        public IReadOnlyList<string> Groups { get; internal set; }
        public IReadOnlyList<string> GroupIcons { get; internal set; }
        public IReadOnlyList<CodexCardDef> Cards { get; internal set; }

        public int IndexOf(string id)
        {
            for (int i = 0; i < Cards.Count; i++) if (Cards[i].Id == id) return i;
            return -1;
        }
    }
}
