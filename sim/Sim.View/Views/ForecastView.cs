using System;
using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Weather;

namespace Sim.View.Views
{
    /// <summary>One run of the forecast as shown: hours [FromHour, ToHour) with the same weather and the same certainty.</summary>
    public sealed class ForecastRunVm
    {
        public string WeatherId { get; internal set; }
        public string WeatherText { get; internal set; }
        public long FromHour { get; internal set; }
        public long ToHour { get; internal set; }

        /// <summary>Days 2 and 3 (spec 001 §3.6): the forecast may be wrong.</summary>
        public bool Likely { get; internal set; }
    }

    /// <summary>The forecast shown as runs (spec 001 §3.6, spec 008 §3.3, ADR 0043 §2): the HUD line and the forecast sheet.</summary>
    public static class ForecastView
    {
        private const long Hour = 3600;

        /// <summary>The current run (the actual weather) and then the forecast's runs, merged by weather and certainty.</summary>
        public static List<ForecastRunVm> Runs(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            long now = ctx.Now / Hour;
            var runs = new List<ForecastRunVm> { Run(ctx, sim.CurrentWeather.Id, now, false) };
            foreach (ForecastEntry e in sim.Forecast())
            {
                string id = sim.Weather.Types[e.Weather].Id;
                ForecastRunVm last = runs[runs.Count - 1];
                if (last.WeatherId == id && last.Likely == e.Bucket > 0) last.ToHour = e.SpellIndex + 1;
                else runs.Add(Run(ctx, id, e.SpellIndex, e.Bucket > 0));
            }
            return runs;
        }

        private static ForecastRunVm Run(ViewContext ctx, string id, long from, bool likely)
        {
            return new ForecastRunVm
            {
                WeatherId = id, WeatherText = ctx.Strings.Get("weather." + id), FromHour = from, ToHour = from + 1, Likely = likely,
            };
        }

        /// <summary>"Sun until 14:00 · then heavy rain about 1 h", or "Sun all day" when the run passes the certain day.</summary>
        public static string HudLine(ViewContext ctx)
        {
            List<ForecastRunVm> runs = Runs(ctx);
            ForecastRunVm now = runs[0];
            if (now.ToHour - ctx.Now / Hour > 24 || runs.Count == 1) return ctx.Strings.Get("forecast.allDay", "weather", now.WeatherText);
            ForecastRunVm next = runs[1];
            return ctx.Strings.Get("forecast.until", "weather", now.WeatherText, "time", At(ctx, now.ToHour))
                + " · " + ctx.Strings.Get("forecast.then", "weather", next.WeatherText.ToLowerInvariant(), "time", ctx.Fmt.Duration((next.ToHour - next.FromHour) * Hour));
        }

        /// <summary>
        /// The time a sim hour starts, as the player reads it (ADR 0043 §2): "14:00" today, "14:00 tomorrow", then "Sat 14:00". The
        /// game's clock matches the phone's, so the day is the phone's now plus the hours ahead.
        /// </summary>
        public static string At(ViewContext ctx, long hour) { return AtTime(ctx, hour * Hour); }

        /// <summary>As <see cref="At"/>, for any sim time (to the minute).</summary>
        public static string AtTime(ViewContext ctx, long simTime)
        {
            DateTime local = ctx.LocalNow.AddSeconds(simTime - ctx.Now);
            string time = local.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            int days = (local.Date - ctx.LocalNow.Date).Days;
            if (days <= 0) return time;
            if (days == 1) return ctx.Strings.Get("time.tomorrow", "time", time);
            return ctx.Strings.Get("time.weekday", "day", ctx.Strings.Get("day." + (int)local.DayOfWeek), "time", time);
        }

        /// <summary>The forecast sheet: one row per run over 3 days, days 2 and 3 marked likely with their accuracy's Why?.</summary>
        public static SheetVm Sheet(ViewContext ctx)
        {
            var vm = new SheetVm { Icon = "weather_" + ctx.Sim.CurrentWeather.Id, Title = ctx.Strings.Get("forecast.title"), Subtitle = ctx.Strings.Get("forecast.subtitle") };
            List<ForecastRunVm> runs = Runs(ctx);
            for (int i = 0; i < runs.Count; i++)
            {
                ForecastRunVm r = runs[i];
                string from = i == 0 ? ctx.Strings.Get("forecast.now") : At(ctx, r.FromHour);
                string label = ctx.Strings.Get("forecast.span", "from", from, "to", At(ctx, r.ToHour));
                string value = r.Likely ? ctx.Strings.Get("forecast.likely", "weather", r.WeatherText) : r.WeatherText;
                vm.Lines.Add(new LineVm(label, value, r.Likely ? "forecast" : null) { Icon = "weather_" + r.WeatherId, WhyId = r.FromHour });
            }
            return vm;
        }
    }
}
