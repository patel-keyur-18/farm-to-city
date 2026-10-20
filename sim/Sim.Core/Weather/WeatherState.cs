using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Weather
{
    /// <summary>One spell as the forecast shows it.</summary>
    public readonly struct ForecastEntry
    {
        public long SpellIndex { get; }

        /// <summary>0 = day 1 (spells 1–4 ahead by default), 1 = day 2, 2 = day 3.</summary>
        public int Bucket { get; }

        /// <summary>Index into <c>WeatherSet.Types</c>.</summary>
        public int Weather { get; }

        public ForecastEntry(long spellIndex, int bucket, int weather)
        {
            SpellIndex = spellIndex;
            Bucket = bucket;
            Weather = weather;
        }
    }

    /// <summary>The weather part of the game state (spec 001 §4).</summary>
    public sealed class WeatherState
    {
        private readonly List<int> _generated = new List<int>();
        private readonly List<ForecastEntry> _forecastShown = new List<ForecastEntry>();

        /// <summary>The spell index of <c>Generated[0]</c>: the current spell.</summary>
        public long FirstSpell { get; internal set; }

        /// <summary>Actual weather (type indices) from the current spell onwards, at least forecastSpells ahead.</summary>
        public IReadOnlyList<int> Generated { get { return _generated; } }

        /// <summary>The forecast as last shown, for the next forecastSpells spells; used to detect ForecastRevised.</summary>
        public IReadOnlyList<ForecastEntry> ForecastShown { get { return _forecastShown; } }

        /// <summary>The first hour of the actual run the current spell belongs to (spec 001 §3.6): the forecast's roll key.</summary>
        public long CurrentRunStart { get; internal set; }

        public long LastGeneratedSpell { get { return FirstSpell + _generated.Count - 1; } }

        /// <summary>The actual weather of a spell still in the buffer.</summary>
        public int WeatherOf(long spell) { return _generated[(int)(spell - FirstSpell)]; }

        internal void Append(int weather) { _generated.Add(weather); }

        /// <summary>Tests only: replaces an already generated spell's weather (for exact time-weighting checks).</summary>
        internal void SetSpell(long spell, int weather) { _generated[(int)(spell - FirstSpell)] = weather; }

        /// <summary>Drops spells before <paramref name="spell"/>.</summary>
        internal void DropBefore(long spell)
        {
            int n = (int)(spell - FirstSpell);
            if (n <= 0) return;
            _generated.RemoveRange(0, n);
            FirstSpell = spell;
        }

        internal void SetForecast(List<ForecastEntry> entries)
        {
            _forecastShown.Clear();
            _forecastShown.AddRange(entries);
        }

        internal WeatherState Clone()
        {
            var c = new WeatherState { FirstSpell = FirstSpell, CurrentRunStart = CurrentRunStart };
            c._generated.AddRange(_generated);
            c._forecastShown.AddRange(_forecastShown);
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(FirstSpell);
            h.WriteLong(CurrentRunStart);
            h.WriteInt(_generated.Count);
            foreach (int w in _generated) h.WriteRef(ContentTable.Weather, w);
            h.WriteInt(_forecastShown.Count);
            foreach (ForecastEntry e in _forecastShown)
            {
                h.WriteLong(e.SpellIndex);
                h.WriteInt(e.Bucket);
                h.WriteRef(ContentTable.Weather, e.Weather);
            }
        }

        internal static WeatherState ReadFrom(SaveReader r)
        {
            var s = new WeatherState { FirstSpell = r.ReadLong(), CurrentRunStart = r.ReadLong() };
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s._generated.Add(r.ReadRef(ContentTable.Weather));
            n = r.ReadCount();
            for (int i = 0; i < n; i++)
            {
                long spell = r.ReadLong();
                int bucket = r.ReadInt();
                s._forecastShown.Add(new ForecastEntry(spell, bucket, r.ReadRef(ContentTable.Weather)));
            }
            return s;
        }
    }
}
