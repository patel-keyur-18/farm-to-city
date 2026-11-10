using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>The three kinds of shop (spec 017 §3.1): customers of one kind choose between the shops of that kind.</summary>
    public enum ShopKind
    {
        None = 0,
        Veg = 1,
        Chai = 2,
        Sweets = 3,
    }

    /// <summary>What people want of a kind of shop a real day (spec 017 §3.2), and how much less they buy as prices rise.</summary>
    public sealed class ShopKindDef
    {
        /// <summary>Kg (vegetables, sweets) or cups (chai) a person a day of life.</summary>
        public Fixed PerPerson { get; internal set; }

        /// <summary>The share of people who buy it (chai: adults, about 70%).</summary>
        public Fixed AdultShare { get; internal set; }
        public Fixed Elasticity { get; internal set; }

        /// <summary>Chai's reference price a cup (year-0 coins) and the share of cups sold with a snack.</summary>
        public Fixed Reference { get; internal set; }
        public Fixed SnackShare { get; internal set; }

        /// <summary>The sweets' reference prices a kg (year-0 coins), by good.</summary>
        public IReadOnlyList<KeyValuePair<int, Fixed>> References { get; internal set; } = new KeyValuePair<int, Fixed>[0];
    }

    /// <summary>
    /// A shop the player doesn't own (spec 017 §3.7): the village grocer and tea stall on their places, the town's halwai off the map.
    /// Fixed terms in Phase 2; it never runs out.
    /// </summary>
    public sealed class RivalDef
    {
        public string Id { get; internal set; }
        public ShopKind Kind { get; internal set; }

        /// <summary>Its tile on the map, or −1 for one off the map at <see cref="Metres"/> from the square.</summary>
        public int Tile { get; internal set; } = -1;
        public int Metres { get; internal set; }
        public Fixed PriceFactor { get; internal set; }

        /// <summary>1 standard, 2 good (§3.5).</summary>
        public int Quality { get; internal set; }
        public Fixed Stars { get; internal set; }
        public int OpenHour { get; internal set; }
        public int CloseHour { get; internal set; }

        /// <summary>Customers it serves a real hour (cups or grams), or 0 for no limit.</summary>
        public long CapacityPerHour { get; internal set; }

        /// <summary>Cuts and restores its price as its share moves (ADR 0095 §3): the village grocer and tea stall.</summary>
        public bool Reacts { get; internal set; }
    }

    /// <summary>Shops and customer choice (spec 017, <c>content/shops.json</c>).</summary>
    public sealed class ShopDefs
    {
        /// <summary>The village's old families (ADR 0061), not simulated one by one.</summary>
        public int VillagePeople { get; internal set; }

        /// <summary>The hours customers come (§3.2): a day's demand is shared over them.</summary>
        public int OpenHour { get; internal set; }
        public int CloseHour { get; internal set; }
        public int MetresPerAcre { get; internal set; }

        /// <summary>The square's tile (the banyan): the village's centre, where the pitches are.</summary>
        public int SquareTile { get; internal set; }
        public int Pitches { get; internal set; }
        public long PitchRent { get; internal set; }
        public Fixed RetailMarkup { get; internal set; }

        /// <summary>A shop's price choices as a share of the reference (the first is the default).</summary>
        public IReadOnlyList<Fixed> PricePresets { get; internal set; }

        // The attraction's weights (§3.3).
        public Fixed WPrice { get; internal set; }
        public Fixed WQuality { get; internal set; }
        public Fixed WStars { get; internal set; }
        public Fixed WDistance { get; internal set; }
        public Fixed WWait { get; internal set; }
        public Fixed AttractionFloor { get; internal set; }
        public Fixed StartStars { get; internal set; }

        /// <summary>
        /// Stars (§3.5, ADR 0092): a day's score is served share × <see cref="ScoreServed"/> + quality (standard or good) + value
        /// (full at or below the reference, gone at <see cref="ValueGoneAbove"/> above it), scaled so the best day is 5; stars move
        /// <see cref="StarsStep"/> of the way to it a day. The first <see cref="ForgivenShutDays"/> shut days in a row leave them alone.
        /// </summary>
        public Fixed ScoreServed { get; internal set; }
        public Fixed ScoreStandard { get; internal set; }
        public Fixed ScoreGood { get; internal set; }
        public Fixed ScoreValue { get; internal set; }
        public Fixed ValueGoneAbove { get; internal set; }
        public Fixed StarsStep { get; internal set; }
        public int ForgivenShutDays { get; internal set; }

        /// <summary>By <see cref="ShopKind"/> (index 0 unused).</summary>
        public IReadOnlyList<ShopKindDef> Kinds { get; internal set; }

        /// <summary>Each vegetable's and fruit's share of a person's vegetables (§3.2), by good; they add up to 1.</summary>
        public IReadOnlyList<KeyValuePair<int, Fixed>> VegShares { get; internal set; }

        public IReadOnlyList<RivalDef> Rivals { get; internal set; }

        /// <summary>The next village's chakki (§3.6): its distance from the square, quality and stars.</summary>
        public int ChakkiMetres { get; internal set; }
        public int ChakkiQuality { get; internal set; }
        public Fixed ChakkiStars { get; internal set; }

        // Newcomers (spec 017 §3.7, ADR 0095 §1–2): a gap under NewcomerGap served, or one of your shops above the reference, opens one
        // with NewcomerChance; under NewcomerExitShare for NewcomerExitSeasons running closes it.
        public Fixed NewcomerGap { get; internal set; }
        public Fixed NewcomerChance { get; internal set; }
        public Fixed NewcomerExitShare { get; internal set; }
        public int NewcomerExitSeasons { get; internal set; }
        public Fixed NewcomerStars { get; internal set; }
        public int NewcomerQuality { get; internal set; }

        /// <summary>The acre whose verge the cart stands on (drawn there), and its distance from the square that customers count.</summary>
        public int CartTile { get; internal set; }
        public int CartMetres { get; internal set; }

        // The grocer's and tea stall's reaction (ADR 0095 §3): a share down by more than ReactChange cuts RivalStep of the reference,
        // never under RivalFloor; up by more than ReactChange goes back a step, never above its own price.
        public Fixed RivalStep { get; internal set; }
        public Fixed RivalFloor { get; internal set; }
        public Fixed ReactChange { get; internal set; }

        public ShopKindDef Kind(ShopKind k) { return Kinds[(int)k]; }
    }
}
