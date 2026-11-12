using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.People
{
    /// <summary>What happened to someone at the close (spec 019 §3.1, ADR 0101 §4).</summary>
    public enum StaffOutcome
    {
        Stayed = 0,

        /// <summary>The new season's wage couldn't be paid: the contract lapsed and they wait out the grace days.</summary>
        WageUnpaid = 1,

        /// <summary>Took an offer elsewhere, or was refused a raise and left.</summary>
        LeftForElsewhere = 2,

        /// <summary>Left because the wage went unpaid.</summary>
        LeftUnpaid = 3,

        /// <summary>His working years done: not renewed.</summary>
        Retired = 4,
    }

    /// <summary>One person on a season's <em>Your people</em> rows: what they were paid against what they'd ask, their skill, and how the close left them.</summary>
    public sealed class PeopleRow
    {
        /// <summary>0 for a farm hand, else the business's id.</summary>
        public long Place { get; }
        public int Name { get; }
        public int Skill { get; }

        /// <summary>The season's pay, the family house's rent included.</summary>
        public long Paid { get; }

        /// <summary>What they would ask for their skill today.</summary>
        public long Asked { get; }
        public StaffOutcome Outcome { get; }

        public PeopleRow(long place, int name, int skill, long paid, long asked, StaffOutcome outcome)
        {
            Place = place; Name = name; Skill = skill; Paid = paid; Asked = asked; Outcome = outcome;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Place); h.WriteRef(ContentTable.Name, Name); h.WriteInt(Skill); h.WriteLong(Paid); h.WriteLong(Asked); h.WriteInt((int)Outcome);
        }

        internal static PeopleRow ReadFrom(SaveReader r)
        {
            long place = r.ReadLong();
            int name = r.ReadRef(ContentTable.Name);
            return new PeopleRow(place, name, r.ReadInt(), r.ReadLong(), r.ReadLong(), (StaffOutcome)r.ReadInt());
        }
    }

    /// <summary>
    /// Your people at a close: seen before the contracts are settled (who will go and why), and again after (what they are paid for the new season).
    /// </summary>
    internal sealed class PeopleSeen
    {
        internal sealed class Who
        {
            public bool Hand;
            public long Id;
            public long Place;
            public int Name;
            public StaffOutcome Exit; // the outcome if they're gone after the close
        }

        internal readonly List<Who> People = new List<Who>();
        internal int ReputationBefore;

        internal static PeopleSeen Before(Simulation sim)
        {
            var seen = new PeopleSeen();
            PeopleState s = sim.State.People;
            if (s == null) return seen;
            seen.ReputationBefore = s.Reputation;
            foreach (Hand h in s.Hands)
            {
                StaffOutcome exit = h.Contract == ContractState.Lapsed ? StaffOutcome.LeftUnpaid
                    : HrRules.Leaves(sim, h) ? StaffOutcome.LeftForElsewhere
                    : CitizenRules.Retiring(sim, h) ? StaffOutcome.Retired : StaffOutcome.LeftUnpaid;
                seen.People.Add(new Who { Hand = true, Id = h.Id, Name = h.Name, Exit = exit });
            }
            foreach (Worker w in s.Workers)
                seen.People.Add(new Who { Id = w.Id, Place = w.Place, Name = w.Name, Exit = HrRules.Leaves(sim, w) ? StaffOutcome.LeftForElsewhere : StaffOutcome.LeftUnpaid });
            return seen;
        }

        /// <summary>The rows once the contracts are settled, in the order the people were seen (hands, then workers).</summary>
        internal List<PeopleRow> After(Simulation sim)
        {
            var rows = new List<PeopleRow>();
            PeopleState s = sim.State.People;
            foreach (Who w in People)
            {
                IStaff staff = null;
                if (s != null)
                {
                    if (w.Hand) { foreach (Hand h in s.Hands) if (h.Id == w.Id) staff = h; }
                    else foreach (Worker k in s.Workers) if (k.Id == w.Id) staff = k;
                }
                if (staff == null) { rows.Add(new PeopleRow(w.Place, w.Name, 0, 0, 0, w.Exit)); continue; }
                StaffOutcome outcome = staff is Hand hand && hand.Contract == ContractState.Lapsed ? StaffOutcome.WageUnpaid : StaffOutcome.Stayed;
                rows.Add(new PeopleRow(w.Place, w.Name, (int)staff.Skill.RoundToLong(), HrRules.Paid(staff), HrRules.Asked(sim, staff), outcome));
            }
            return rows;
        }
    }
}
