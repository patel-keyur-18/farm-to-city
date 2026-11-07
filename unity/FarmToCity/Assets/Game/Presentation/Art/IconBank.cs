using UnityEngine;

namespace Game.Presentation.Art
{
    /// <summary>The HUD, sheet and produce icons: Blender renders on the map rig, framed square (ADR 0041 §8).</summary>
    public static class IconBank
    {
        public static Sprite Weather(string id) { return SpriteBank.Rendered("icon_weather_" + id) ?? SpriteBank.Rendered("icon_weather_sun"); }
        public static Sprite Coin { get { return SpriteBank.Rendered("icon_coin"); } }
        public static Sprite Sack { get { return SpriteBank.Rendered("icon_sack"); } }
        public static Sprite Basket { get { return SpriteBank.Rendered("icon_basket"); } }
        public static Sprite Close { get { return SpriteBank.Rendered("icon_close"); } }
        public static Sprite Gear { get { return SpriteBank.Rendered("icon_gear"); } }
        public static Sprite Letter { get { return SpriteBank.Rendered("icon_letter") ?? Basket; } }
        public static Sprite Bell { get { return SpriteBank.Rendered("icon_bell") ?? Basket; } }
        public static Sprite Cart { get { return SpriteBank.Rendered("icon_cart") ?? Basket; } }
        public static Sprite Ledger { get { return SpriteBank.Rendered("icon_menu") ?? Basket; } }
        public static Sprite Orders { get { return SpriteBank.Rendered("icon_orders") ?? Basket; } }

        /// <summary>A skill star (ADR 0039 §7): 0 empty, 1 faded (possible), 2 solid (sure).</summary>
        public static Sprite Star(int kind) { return SpriteBank.Rendered("icon_star_" + kind); }

        /// <summary>A good's icon: produce by its id, seeds and saplings by kind, the inputs by name.</summary>
        public static Sprite Good(string goodId)
        {
            if (goodId.StartsWith("weather_", System.StringComparison.Ordinal)) return Weather(goodId.Substring(8));
            if (goodId == "coin") return Coin;
            if (goodId == "basket") return Basket;
            if (goodId == "sack") return Sack;
            if (goodId == "gear") return Gear;
            if (goodId == "sign") return SpriteBank.Sign;
            if (goodId == "hand_hut" || goodId == "hand_hut_building" || goodId == "granary") return SpriteBank.Rendered(goodId) ?? Basket; // granary: your godown in town (ADR 0087)
            if (goodId == "family_house" || goodId == "family_house_building" || goodId == "flour_mill" || goodId == "flour_mill_building" || goodId == "bakery" || goodId == "bakery_building" || goodId == "dairy" || goodId == "dairy_building") return SpriteBank.Rendered(goodId + "_s") ?? Basket;
            if (goodId == "veg_stall" || goodId == "tea_stall" || goodId == "sweet_shop") return SpriteBank.Rendered(goodId + "_s") ?? Basket; // shops (spec 017)
            if (goodId.EndsWith("_building", System.StringComparison.Ordinal) && (goodId.StartsWith("veg_stall", System.StringComparison.Ordinal) || goodId.StartsWith("tea_stall", System.StringComparison.Ordinal)
                || goodId.StartsWith("sweet_shop", System.StringComparison.Ordinal))) return SpriteBank.Rendered(goodId.Substring(0, goodId.Length - 9) + "_s") ?? Basket;
            if (goodId == "school" || goodId == "school_building") return SpriteBank.Rendered(goodId == "school" ? "village_school_e" : "village_school_building_e") ?? Basket; // spec 018
            if (goodId == "pitch") return SpriteBank.Rendered("pitch_empty_1") ?? Basket;
            if (goodId == "star") return Star(2) ?? Basket; // a shop's stars (spec 017 §3.5)
            if (goodId == "field") return SpriteBank.SoilTile;
            if (goodId == "place_halwai") return SpriteBank.Rendered("sweet_shop_s") ?? Basket; // the town's halwai, off the map (spec 017)
            if (goodId.StartsWith("place_", System.StringComparison.Ordinal)) return SpriteBank.Rendered(goodId) ?? Basket; // the village's grocer and tea stall
            if (goodId.StartsWith("portrait_", System.StringComparison.Ordinal)) return SpriteBank.Rendered(goodId) ?? Basket;
            if (goodId.StartsWith("person_", System.StringComparison.Ordinal)) return SpriteBank.Rendered(goodId + "_front") ?? Basket;
            if (goodId.StartsWith("seed_", System.StringComparison.Ordinal)) return SpriteBank.Rendered("icon_seed");
            if (goodId.StartsWith("sapling_", System.StringComparison.Ordinal)) return SpriteBank.Rendered("icon_sapling");
            if (goodId == "fertiliser_chemical") return SpriteBank.Rendered("icon_fertiliser");
            if (goodId == "tractor" || goodId == "plough_team") return SpriteBank.Rendered(goodId) ?? Sack; // equipment (spec 013 §7)
            return SpriteBank.Rendered("icon_good_" + goodId) ?? SpriteBank.Rendered("icon_" + goodId) ?? Sack;
        }
    }
}
