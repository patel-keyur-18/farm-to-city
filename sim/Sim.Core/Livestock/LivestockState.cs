using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Livestock
{
    public enum AnimalStage
    {
        Milking = 0,
        Dry = 1,
    }

    /// <summary>A buffalo or a cow in the shed (spec 012 §4).</summary>
    public sealed class Animal
    {
        public long Id { get; internal set; }

        /// <summary>Index into <c>LivestockDefs.Animals</c>.</summary>
        public int Kind { get; internal set; }
        public string Name { get; internal set; }
        public long BoughtAt { get; internal set; }

        /// <summary>Money: what she cost (her book value, 1750).</summary>
        public long Cost { get; internal set; }

        public AnimalStage Stage { get; internal set; }

        /// <summary>Game days (6:00 passes) since she calved, or since she went dry.</summary>
        public int StageDay { get; internal set; }

        public Fixed Health { get; internal set; } = Fixed.One;

        /// <summary>This morning's fed score (§3.7), which sets the milk of the day ahead.</summary>
        public Fixed FedScore { get; internal set; } = Fixed.One;

        /// <summary>Hungry mornings in a row.</summary>
        public int HungryDays { get; internal set; }

        /// <summary>The season (ledger period) she was last vaccinated in.</summary>
        public long VaccinatedPeriod { get; internal set; }

        /// <summary>Days of illness left: no milk while above 0 (§3.10).</summary>
        public int SickDays { get; internal set; }

        /// <summary>The bank loan secured on her, or 0 (§3.12).</summary>
        public long LoanId { get; internal set; }

        /// <summary>Sim time of her last milking (the last 6:00, or when she was bought).</summary>
        public long Since { get; internal set; }

        /// <summary>Money she has earned: milk and calves (for her payback, §7).</summary>
        public long Earned { get; internal set; }

        /// <summary>Millilitres at her last milking.</summary>
        public long LastMilkMl { get; internal set; }

        internal Animal Clone() { return (Animal)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteRef(ContentTable.Animal, Kind);
            h.WriteString(Name);
            h.WriteLong(BoughtAt);
            h.WriteLong(Cost);
            h.WriteInt((int)Stage);
            h.WriteInt(StageDay);
            h.WriteFixed(Health);
            h.WriteFixed(FedScore);
            h.WriteInt(HungryDays);
            h.WriteLong(VaccinatedPeriod);
            h.WriteInt(SickDays);
            h.WriteLong(LoanId);
            h.WriteLong(Since);
            h.WriteLong(Earned);
            h.WriteLong(LastMilkMl);
        }

        internal static Animal ReadFrom(SaveReader r)
        {
            return new Animal
            {
                Id = r.ReadLong(), Kind = r.ReadRef(ContentTable.Animal), Name = r.ReadString(), BoughtAt = r.ReadLong(), Cost = r.ReadLong(),
                Stage = (AnimalStage)r.ReadInt(), StageDay = r.ReadInt(), Health = r.ReadFixed(), FedScore = r.ReadFixed(), HungryDays = r.ReadInt(),
                VaccinatedPeriod = r.ReadLong(), SickDays = r.ReadInt(), LoanId = r.ReadLong(), Since = r.ReadLong(), Earned = r.ReadLong(),
                LastMilkMl = r.ReadLong(),
            };
        }
    }

    /// <summary>The morning's feeding, for the shed sheet and the away card (§3.7). Grams and money.</summary>
    public sealed class FeedingReport
    {
        public long GreenNeed { get; internal set; }
        public long GreenCommons { get; internal set; }
        public long GreenOwn { get; internal set; }
        public long GreenBought { get; internal set; }
        public long GreenBoughtAmount { get; internal set; }
        public long GreenSold { get; internal set; }
        public long GreenSoldAmount { get; internal set; }
        public long StrawNeed { get; internal set; }
        public long StrawGiven { get; internal set; }
        public long FeedNeed { get; internal set; }
        public long FeedGiven { get; internal set; }

        internal FeedingReport Clone() { return (FeedingReport)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            foreach (long v in new[] { GreenNeed, GreenCommons, GreenOwn, GreenBought, GreenBoughtAmount, GreenSold, GreenSoldAmount, StrawNeed, StrawGiven, FeedNeed, FeedGiven })
                h.WriteLong(v);
        }

        internal static FeedingReport ReadFrom(SaveReader r)
        {
            return new FeedingReport
            {
                GreenNeed = r.ReadLong(), GreenCommons = r.ReadLong(), GreenOwn = r.ReadLong(), GreenBought = r.ReadLong(), GreenBoughtAmount = r.ReadLong(),
                GreenSold = r.ReadLong(), GreenSoldAmount = r.ReadLong(), StrawNeed = r.ReadLong(), StrawGiven = r.ReadLong(), FeedNeed = r.ReadLong(),
                FeedGiven = r.ReadLong(),
            };
        }
    }

    /// <summary>Cattle (spec 012): the animals, the shed's construction, and the morning's results.</summary>
    public sealed class LivestockState
    {
        /// <summary>In id order.</summary>
        public List<Animal> Animals { get; internal set; } = new List<Animal>();
        public long NextAnimalId { get; internal set; } = 1;

        /// <summary>When the shed under construction is ready, or 0 when none is being built.</summary>
        public long ShedReadyAt { get; internal set; }
        public long ShedCost { get; internal set; }

        /// <summary>Buy the green fodder shortfall from neighbours (§3.7; on by default).</summary>
        public bool BuyGreenFodder { get; internal set; } = true;

        /// <summary>A low warning was given for straw or feed and the place hasn't been refilled above it since (§3.7).</summary>
        public bool StrawLow { get; internal set; }
        public bool FeedLow { get; internal set; }

        /// <summary>The Care ratio accrued since the last milking (§3.9): Σ ratio × seconds, and the seconds.</summary>
        public Fixed CareSum { get; internal set; }
        public long CareSeconds { get; internal set; }

        /// <summary>Money of milk the household kept since the last daily pass: part of its food allowance (§3.5).</summary>
        public long HomeMilkCredit { get; internal set; }

        /// <summary>The last milking: millilitres in all, what the co-op paid, and what the household kept.</summary>
        public long LastMilkMl { get; internal set; }
        public long LastMilkAmount { get; internal set; }
        public long LastHomeMl { get; internal set; }

        public FeedingReport LastFeeding { get; internal set; } = new FeedingReport();

        internal LivestockState Clone()
        {
            var c = (LivestockState)MemberwiseClone();
            c.Animals = Animals.ConvertAll(a => a.Clone());
            c.LastFeeding = LastFeeding.Clone();
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Animals.Count);
            foreach (Animal a in Animals) a.WriteTo(h);
            h.WriteLong(NextAnimalId);
            h.WriteLong(ShedReadyAt);
            h.WriteLong(ShedCost);
            h.WriteBool(BuyGreenFodder);
            h.WriteBool(StrawLow);
            h.WriteBool(FeedLow);
            h.WriteFixed(CareSum);
            h.WriteLong(CareSeconds);
            h.WriteLong(HomeMilkCredit);
            h.WriteLong(LastMilkMl);
            h.WriteLong(LastMilkAmount);
            h.WriteLong(LastHomeMl);
            LastFeeding.WriteTo(h);
        }

        internal static LivestockState ReadFrom(SaveReader r)
        {
            var s = new LivestockState();
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Animals.Add(Animal.ReadFrom(r));
            s.NextAnimalId = r.ReadLong();
            s.ShedReadyAt = r.ReadLong();
            s.ShedCost = r.ReadLong();
            s.BuyGreenFodder = r.ReadBool();
            s.StrawLow = r.ReadBool();
            s.FeedLow = r.ReadBool();
            s.CareSum = r.ReadFixed();
            s.CareSeconds = r.ReadLong();
            s.HomeMilkCredit = r.ReadLong();
            s.LastMilkMl = r.ReadLong();
            s.LastMilkAmount = r.ReadLong();
            s.LastHomeMl = r.ReadLong();
            s.LastFeeding = FeedingReport.ReadFrom(r);
            return s;
        }
    }
}
