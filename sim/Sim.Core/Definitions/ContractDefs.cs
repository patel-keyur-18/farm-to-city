using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>Forward contracts (spec 027 §3.7, <c>content/contracts.json</c>), from the Township's content, or null.</summary>
    public sealed class ContractsDef
    {
        /// <summary>The staples the trader buys by the kilo and offers a contract for, by good id.</summary>
        public IReadOnlyList<string> Crops { get; internal set; }

        /// <summary>How many of your crops get an offer at a close (the biggest last-season harvests).</summary>
        public int TopCrops { get; internal set; }

        /// <summary>The share of last season's harvest an offer covers.</summary>
        public Fixed ShareOfHarvest { get; internal set; }

        /// <summary>A crop under this harvest gets no offer; an offer is rounded to this many kilos.</summary>
        public int MinKg { get; internal set; }
        public int RoundKg { get; internal set; }

        /// <summary>The offered price is the expected price less this share: never above it.</summary>
        public Fixed Discount { get; internal set; }

        /// <summary>At most this many contracts for one delivery season.</summary>
        public int MaxOpen { get; internal set; }

        /// <summary>How many settled contracts the Store sheet keeps.</summary>
        public int KeepHistory { get; internal set; }

        public bool Has(string good)
        {
            foreach (string c in Crops) if (c == good) return true;
            return false;
        }
    }
}
