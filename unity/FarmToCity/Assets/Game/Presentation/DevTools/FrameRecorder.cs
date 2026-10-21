#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Presentation.DevTools
{
    /// <summary>
    /// The frame-time check of the release candidate (ADR 0047 §4, ADR 0035's method): after a 3-second wait, 20 seconds of
    /// <see cref="FrameTimingManager"/> samples, then main thread avg / p95 / max, GPU avg / p95, frames over 25 ms and fps.
    /// The build step turns on frame-timing stats in Development builds; without them the CPU and GPU times read 0.
    /// </summary>
    public sealed class FrameRecorder : MonoBehaviour
    {
        private const float Wait = 3f, Span = 20f;
        private readonly List<double> _main = new List<double>(), _gpu = new List<double>(), _interval = new List<double>();
        private readonly FrameTiming[] _one = new FrameTiming[1];
        private float _start;
        private Action<string> _done;

        public static void Measure(Action<string> done)
        {
            var go = new GameObject("FrameRecorder");
            FrameRecorder r = go.AddComponent<FrameRecorder>();
            r._done = done;
            r._start = Time.realtimeSinceStartup + Wait;
        }

        /// <summary>Seconds left, for the countdown; negative while waiting to start.</summary>
        public static float Remaining { get; private set; }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            Remaining = _start + Span - now;
            FrameTimingManager.CaptureFrameTimings();
            if (now < _start) return;
            if (FrameTimingManager.GetLatestTimings(1, _one) == 1)
            {
                _main.Add(_one[0].cpuMainThreadFrameTime);
                _gpu.Add(_one[0].gpuFrameTime);
            }
            _interval.Add(Time.unscaledDeltaTime * 1000.0);
            if (now < _start + Span) return;
            Remaining = 0;
            string report = Report();
            Debug.Log("Frame time: " + report);
            Destroy(gameObject);
            _done(report);
        }

        private string Report()
        {
            if (_interval.Count == 0) return "no frames";
            double dropped = 100.0 * _interval.Count(x => x > 25) / _interval.Count;
            bool pass = P95(_main) <= 8.3 && P95(_gpu) <= 8.3 && dropped < 1.0 && _main.Any(x => x > 0);
            return "main " + Ms(_main.DefaultIfEmpty().Average()) + " / p95 " + Ms(P95(_main)) + " / max " + Ms(_main.DefaultIfEmpty().Max())
                + " · GPU " + Ms(_gpu.DefaultIfEmpty().Average()) + " / p95 " + Ms(P95(_gpu))
                + "\nover 25 ms " + dropped.ToString("0.00") + "% · " + (_interval.Count / Span).ToString("0.0") + " fps · " + _interval.Count + " frames"
                + "\n" + (pass ? "PASS" : "FAIL") + " (p95 ≤ 8.3 ms, under 1% over 25 ms" + (_main.Any(x => x > 0) ? ")" : "; no timings: frame-timing stats off)");
        }

        private static double P95(List<double> xs)
        {
            if (xs.Count == 0) return 0;
            var s = xs.OrderBy(x => x).ToList();
            return s[Math.Min(s.Count - 1, (int)(0.95 * s.Count))];
        }

        private static string Ms(double ms) { return ms.ToString("0.00") + " ms"; }
    }
}
#endif
