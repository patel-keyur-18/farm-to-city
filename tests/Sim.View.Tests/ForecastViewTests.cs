using System;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Weather;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 008 AC-16 (ADR 0043 §2): the forecast shown as runs, on the HUD and the forecast sheet.</summary>
    public class ForecastViewTests
    {
        private const long Hour = 3600;

        /// <summary>Thursday 20 Oct 2033, 08:00 on the phone: the game's clock matches it (new games start at 08:00).</summary>
        private static readonly DateTime Thursday = new DateTime(2033, 10, 20, 8, 0, 0);

        private static ViewContext Ctx(Simulation sim)
        {
            ViewContext ctx = ViewKit2.Context(sim);
            ctx.LocalNow = Thursday;
            return ctx;
        }

        [Test]
        public void Runs()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                Simulation sim = ViewKit.NewGame(seed: seed);
                ViewContext ctx = Ctx(sim);
                var runs = ForecastView.Runs(ctx);
                var shown = sim.Forecast();

                // The runs cover the current hour and the 72 forecast hours exactly, in order, with no gaps.
                Assert.That(runs.First().FromHour, Is.EqualTo(sim.State.Clock.SimTime / Hour));
                Assert.That(runs.Last().ToHour, Is.EqualTo(shown.Last().SpellIndex + 1));
                for (int i = 1; i < runs.Count; i++)
                {
                    Assert.That(runs[i].FromHour, Is.EqualTo(runs[i - 1].ToHour));
                    Assert.That(runs[i].WeatherId != runs[i - 1].WeatherId || runs[i].Likely != runs[i - 1].Likely, Is.True, "merged");
                }

                // Every hour's run shows the forecast for that hour; days 2–3 are marked likely.
                foreach (ForecastEntry e in shown)
                {
                    ForecastRunVm r = runs.Single(x => x.FromHour <= e.SpellIndex && e.SpellIndex < x.ToHour);
                    Assert.That(r.WeatherId, Is.EqualTo(sim.Weather.Types[e.Weather].Id));
                    Assert.That(r.Likely, Is.EqualTo(e.Bucket > 0));
                }
                Assert.That(runs[0].WeatherId, Is.EqualTo(sim.CurrentWeather.Id));

                // The HUD line: the current run and the next.
                string hud = ForecastView.HudLine(ctx);
                Assert.That(hud, Does.StartWith(ctx.Strings.Get(sim.CurrentWeather.NameKey)));
                if (runs[0].ToHour - sim.State.Clock.SimTime / Hour > 24) Assert.That(hud, Does.Contain("all day"));
                else Assert.That(hud, Does.Contain(" until ").And.Contain(" · then "));
            }
        }

        [Test]
        public void TimeLabels_TodayTomorrowWeekday()
        {
            Simulation sim = ViewKit.NewGame(seed: 3);
            ViewContext ctx = Ctx(sim);
            long now = sim.State.Clock.SimTime / Hour; // 08:00
            Assert.That(ForecastView.At(ctx, now + 6), Is.EqualTo("14:00"));
            Assert.That(ForecastView.At(ctx, now + 16), Is.EqualTo("00:00 tomorrow"));
            Assert.That(ForecastView.At(ctx, now + 30), Is.EqualTo("14:00 tomorrow"));
            Assert.That(ForecastView.At(ctx, now + 54), Is.EqualTo("Sat 14:00"));
        }

        [Test]
        public void Sheet_OneRowPerRun_LikelyOnDays2And3()
        {
            Simulation sim = ViewKit.NewGame(seed: 9);
            ViewContext ctx = Ctx(sim);
            SheetVm sheet = ForecastView.Sheet(ctx);
            var runs = ForecastView.Runs(ctx);
            Assert.That(sheet.Lines.Count(l => !l.IsHeader), Is.EqualTo(runs.Count));
            Assert.That(sheet.Lines.Where(l => !l.IsHeader).All(l => l.Icon.StartsWith("weather_")), Is.True);
            Assert.That(sheet.Lines.Where(l => !l.IsHeader).Select(l => l.Value).Where(v => v.Contains("likely")).Count(), Is.EqualTo(runs.Count(r => r.Likely)));
            Assert.That(sheet.Lines.Where(l => l.Value != null && l.Value.Contains("likely")).All(l => l.WhyKey == "forecast"), Is.True);
        }
    }
}
