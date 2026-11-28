namespace Sim.View.Views
{
    /// <summary>
    /// How big a repeated tree is drawn (spec 032 US-275, ADR 0186): an orchard, a grove and the like repeat one picture, so each is drawn up to
    /// 8% bigger or smaller, picked from its place by a fixed hash. Presentation only: no sim or save state, nothing random.
    /// </summary>
    public static class TreeSizes
    {
        /// <summary>The factor, 0.92 to 1.08, of tree <paramref name="n"/> at the place <paramref name="at"/>.</summary>
        public static float Factor(int at, int n)
        {
            unchecked
            {
                uint h = (uint)(at * 73856093) ^ (uint)(n * 19349663);
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (92 + (int)(h % 17)) / 100f;
            }
        }
    }
}
