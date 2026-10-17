using Sim.Core.Events;
using Sim.Core.Scheduling;

namespace Sim.Core.Time
{
    /// <summary>The first system in every pass: announces hour, day, season and year boundaries.</summary>
    internal sealed class TimeSystem : ISimSystem
    {
        public SystemOrder Order { get { return SystemOrder.Time; } }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            long t = sim.State.Clock.SimTime;
            CalendarDate c = Calendar.From(t, sim.Clock);
            switch (pass)
            {
                case ItemClass.Hourly: sim.Emit(new HourStarted(t)); break;
                case ItemClass.Daily: sim.Emit(new DayStarted(t, c.Day, c.DayOfSeason, c.Season, c.Year)); break;
                case ItemClass.Seasonal: sim.Emit(new SeasonStarted(t, c.Season, c.Year)); break;
                case ItemClass.Yearly: sim.Emit(new YearStarted(t, c.Year)); break;
            }
        }
    }
}
