namespace Sim.Core.Maths
{
    /// <summary>A content list a state field points into (spec 007 §3.1: saves store ids, so content can be reordered).</summary>
    public enum ContentTable
    {
        Weather = 0,
        Soil = 1,
        Drainage = 2,
        Parcel = 3,
        Building = 4,
        Good = 5,
        Crop = 6,
        GardenCrop = 7,
        Fertiliser = 8,
        Tree = 9,
        DietPlan = 10,
        WorkHours = 11,
        Lender = 12,
        Trait = 13,
        Name = 14,
        Step = 15,
        Chapter = 16,
        Era = 17,
        Animal = 18,
    }

    /// <summary>
    /// The canonical write path of the state (ADR 0041): the hash and the save both go through each class's
    /// <c>WriteTo</c>, so a field that is hashed can't be left out of a save.
    /// </summary>
    public interface IStateWriter
    {
        /// <summary>True for the state hash; false for a save, which also writes the few fields the hash leaves out.</summary>
        bool Hashing { get; }

        void WriteByte(byte b);
        void WriteULong(ulong v);
        void WriteLong(long v);
        void WriteInt(int v);
        void WriteBool(bool v);
        void WriteFixed(Fixed v);
        void WriteString(string s);

        /// <summary>An index into a content list (negative = none). The hash takes the index; a save remaps it by id on load.</summary>
        void WriteRef(ContentTable table, int index);
    }
}
