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
            if (goodId.StartsWith("person_", System.StringComparison.Ordinal)) return SpriteBank.Rendered(goodId + "_front") ?? Basket;
            if (goodId.StartsWith("seed_", System.StringComparison.Ordinal)) return SpriteBank.Rendered("icon_seed");
            if (goodId.StartsWith("sapling_", System.StringComparison.Ordinal)) return SpriteBank.Rendered("icon_sapling");
            if (goodId == "fertiliser_chemical") return SpriteBank.Rendered("icon_fertiliser");
            return SpriteBank.Rendered("icon_good_" + goodId) ?? SpriteBank.Rendered("icon_" + goodId) ?? Sack;
        }
    }
}
