using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Equipment;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>
    /// The tempo on the Equipment sheet and the mandi sheet (US-245, spec 028 §3.6, ADR 0162, Preview 18): its legs, the ride a load gets, letting the idle legs out and an honest
    /// "does it pay?". Every number has a *Why?* (the <c>tempo</c> source, id = the row).
    /// </summary>
    public static class TempoSheet
    {
        public const string Why = "tempo";
        public const int Leg = 1, Ride = 2, LetOutPay = 3, Worth = 4;

        /// <summary>"Carries 1,000 kg a leg · 4 h a leg · 2 legs a day".</summary>
        internal static string Does(ViewContext ctx, MachineDef d)
        {
            return ctx.Strings.Get("equipment.doesTempo", "kg", ctx.Fmt.Kg(d.Trip.LoadKg * 1000), "hours", ctx.Fmt.Integer(d.Trip.Hours), "legs", ctx.Fmt.Integer(d.Trip.LegsPerDay));
        }

        /// <summary>The tempo's state in a word: the legs it has left today.</summary>
        internal static string State(ViewContext ctx, Machine m)
        {
            Simulation sim = ctx.Sim;
            int left = TempoRules.LegsLeft(sim, m);
            return ctx.Strings.Plural("equipment.tempoReady", left, "of", ctx.Fmt.Integer(TempoRules.Trip(sim, m).LegsPerDay));
        }

        /// <summary>The mandi sheet's "Carried by": your tractor-trolley, the tempo with its legs, or the hired trolley.</summary>
        public static string RideText(ViewContext ctx, MandiRide ride)
        {
            var s = ctx.Strings;
            return ride.Ride == Sim.Core.Equipment.Ride.Own ? s.Get("mandi.rideOwn") : ride.Ride == Sim.Core.Equipment.Ride.Tempo ? s.Plural("mandi.rideTempo", ride.Legs) : s.Get("mandi.rideHired");
        }

        /// <summary>The block under the tempo: letting its idle legs out, this season's legs, and whether it pays.</summary>
        internal static void LetOut(ViewContext ctx, SheetVm vm, Machine m, MachineDef d)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            TripDef trip = d.Trip;
            vm.Lines.Add(LineVm.Header(s.Get("equipment.hireOutHeader")));
            long id = m.Id;
            if (m.Leased)
            {
                vm.Lines.Add(new LineVm(s.Get("equipment.hireOut"), s.Get("equipment.hireOutLeased")) { Note = s.Get("equipment.tempoLeasedNote") });
            }
            else
            {
                bool on = m.HireOut, broken = EquipmentRules.Broken(sim, m), noHand = !EquipmentRules.HasDriver(sim);
                string state = !on ? s.Get("equipment.hireOutOff") : broken ? s.Get("equipment.hireOutResting") : noHand ? s.Get("equipment.hireOutNoHand") : s.Get("equipment.tempoOn");
                vm.Lines.Add(new LineVm(s.Get("equipment.tempoLetOut"), state, Why, LetOutPay)
                {
                    Icon = "tempo", Note = s.Get("equipment.tempoLetOutNote", "pay", ctx.Fmt.Coins(TempoRules.NeighbourPay(sim, trip)), "cost", ctx.Fmt.Coins(TempoRules.LegCost(sim, trip))),
                    NoteTone = on && (broken || noHand) ? Tone.Warn : Tone.Plain,
                    Action = ActionVm.Checked(ctx, "hireOut:" + id, s.Get(on ? "equipment.hireOutStop" : "equipment.hireOutStart"), null, ActionKind.Secondary, at => new SetHireOut(at, id, !on)),
                });
            }
            vm.Lines.Add(new LineVm(s.Get("equipment.tempoSeason"), s.Get("equipment.tempoSeasonValue", "own", ctx.Fmt.Integer(m.OwnLegs), "others", ctx.Fmt.Integer(m.LetOutLegs)), Why, Leg));
            long best = TempoRules.BestPerSeason(sim, m), letting = TempoRules.LetOutPerSeason(sim, m), standing = TempoRules.StandingPerSeason(sim, m);
            string pays = best <= 0 ? s.Get("equipment.tempoNever") : s.Get("equipment.tempoPaysBack", "seasons", ctx.Fmt.Integer((m.Cost + best - 1) / best));
            vm.Lines.Add(new LineVm(s.Get("equipment.tempoPays"), pays, Why, Worth)
            {
                Note = s.Get("equipment.tempoPaysNote", "saves", ctx.Fmt.Coins(TempoRules.SavesPerLeg(sim, trip)), "best", Signed(ctx, best), "letting", Signed(ctx, letting), "standing", ctx.Fmt.Coins(standing)),
            });
        }

        private static string Signed(ViewContext ctx, long amount) { return (amount < 0 ? "−" : "") + ctx.Fmt.Coins(System.Math.Abs(amount)); }
    }
}
