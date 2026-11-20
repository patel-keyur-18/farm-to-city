using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Town
{
    /// <summary>How a petition stands (spec 026 §3.2): open (nobody answered), promised, or one of the five endings.</summary>
    public enum PetitionStatus : byte { Open, Promised, Done, Kept, Declined, Ignored, Broken }

    /// <summary>A petition raised by your households (spec 026 §3.2): a topic, the side of the town most of them live on, how many of them, and where it stands.</summary>
    public sealed class TownPetition
    {
        public int Id { get; internal set; }

        /// <summary>One of <see cref="Definitions.PetitionDef"/>'s topics.</summary>
        public int Topic { get; internal set; }

        /// <summary>The compass side of the town square (0 north, 1 east, 2 south, 3 west) where most of them live.</summary>
        public int Side { get; internal set; }

        /// <summary>The families sharing the need when it was raised, and at the last close.</summary>
        public int Households { get; internal set; }

        public long RaisedAt { get; internal set; }
        public long ClosedAt { get; internal set; }

        /// <summary>The closes counted so far when it was raised; and for a promise, the close that settles it.</summary>
        public int RaisedClose { get; internal set; }
        public int PromisedBy { get; internal set; }

        public PetitionStatus Status { get; internal set; }

        public bool Ended { get { return Status >= PetitionStatus.Done; } }

        internal TownPetition Clone() { return (TownPetition)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Id);
            h.WriteInt(Topic);
            h.WriteInt(Side);
            h.WriteInt(Households);
            h.WriteLong(RaisedAt);
            h.WriteLong(ClosedAt);
            h.WriteInt(RaisedClose);
            h.WriteInt(PromisedBy);
            h.WriteInt((int)Status);
        }

        internal static TownPetition ReadFrom(SaveReader r)
        {
            return new TownPetition
            {
                Id = r.ReadInt(), Topic = r.ReadInt(), Side = r.ReadInt(), Households = r.ReadInt(), RaisedAt = r.ReadLong(), ClosedAt = r.ReadLong(),
                RaisedClose = r.ReadInt(), PromisedBy = r.ReadInt(), Status = (PetitionStatus)r.ReadInt(),
            };
        }
    }
}
