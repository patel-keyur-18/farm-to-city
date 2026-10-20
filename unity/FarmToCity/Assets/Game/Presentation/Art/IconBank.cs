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

        /// <summary>A skill star (ADR 0039 §7): 0 empty, 1 faded (possible), 2 solid (sure).</summary>
        public static Sprite Star(int kind) { return SpriteBank.Rendered("icon_star_" + kind); }

        /// <summary>A good's icon: produce by its id, seeds and saplings by kind, the inputs by name.</summary>
        public static Sprite Good(string goodId)
        {
            if (goodId.StartsWith("seed_", System.StringComparison.Ordinal)) return SpriteBank.Rendered("icon_seed");
            if (goodId.StartsWith("sapling_", System.StringComparison.Ordinal)) return SpriteBank.Rendered("icon_sapling");
            if (goodId == "fertiliser_chemical") return SpriteBank.Rendered("icon_fertiliser");
            return SpriteBank.Rendered("icon_good_" + goodId) ?? SpriteBank.Rendered("icon_" + goodId) ?? Sack;
        }
    }
}
