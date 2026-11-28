using System;

namespace Sim.View.Views
{
    /// <summary>
    /// The river's flow (spec 032 US-273, ADR 0184): the water acres are a loop of 12 rendered frames played at 10 a second, one loop 1.2 s; reduced motion
    /// holds the first frame. Presentation only: the clock is the app's, not the game's, so a paused or fast-forwarded game never changes it.
    /// </summary>
    public static class WaterFlow
    {
        public const int Frames = 12;
        public const int Fps = 10;

        /// <summary>The frame to show after <paramref name="seconds"/> of app time.</summary>
        public static int Frame(float seconds, bool reducedMotion)
        {
            if (reducedMotion) return 0;
            int tick = (int)Math.Floor(seconds * Fps);
            return ((tick % Frames) + Frames) % Frames;
        }
    }
}
