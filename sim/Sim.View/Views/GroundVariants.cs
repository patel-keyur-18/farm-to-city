namespace Sim.View.Views
{
    /// <summary>
    /// Which variant of a ground kind an acre shows (spec 032 US-273, ADR 0184): the big kinds are rendered in three variants and one is picked from the
    /// acre's place by a fixed hash, so the same acre always shows the same one and a field of acres doesn't repeat like wallpaper. Presentation only:
    /// no sim or save state, nothing random.
    /// </summary>
    public static class GroundVariants
    {
        public const int Count = 3;

        /// <summary>The variant, 0 to <see cref="Count"/> − 1, of the acre at (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public static int Pick(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h % Count);
            }
        }
    }
}
