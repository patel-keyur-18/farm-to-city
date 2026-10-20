using System.Runtime.InteropServices;

namespace Game.Services
{
    /// <summary>
    /// iOS's Reduce Motion setting and a light haptic tap (spec 008 §3.13, ADR 0043 §9), from <c>Plugins/iOS/FarmToCityNative.mm</c>.
    /// Elsewhere (the Editor, Android for now) motion is full and there is no tap.
    /// </summary>
    public static class NativeFeel
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern bool ftc_reduce_motion();
        [DllImport("__Internal")] private static extern void ftc_haptic_light();

        public static bool SystemReducesMotion { get { return ftc_reduce_motion(); } }

        public static void LightTap() { ftc_haptic_light(); }
#else
        public static bool SystemReducesMotion { get { return false; } }

        public static void LightTap() { }
#endif
    }
}
