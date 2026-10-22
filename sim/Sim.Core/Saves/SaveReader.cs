using System;
using System.IO;
using System.Text;
using Sim.Core.Maths;

namespace Sim.Core.Saves
{
    /// <summary>Thrown inside the reader for bytes that can't be a save; the codec turns it into <see cref="SaveError.CorruptSave"/>.</summary>
    public sealed class SaveFormatException : Exception
    {
        public SaveFormatException(string message) : base(message) { }
    }

    /// <summary>
    /// Reads what <see cref="SaveWriter"/> wrote, in the same order. Content references are remapped by id to the running
    /// build's content (spec 007 §3.1), so content can be reordered or extended between builds.
    /// </summary>
    public sealed class SaveReader
    {
        private readonly BinaryReader _r;
        private readonly int[][] _remap;
        private readonly int[] _counts;

        /// <summary>The schema the save was written at: a migration reads a field only from the schema that added it.</summary>
        public int Schema { get; }

        internal SaveReader(Stream stream, int schema, int[][] remap, int[] counts)
        {
            _r = new BinaryReader(stream, new UTF8Encoding(false, true), leaveOpen: true);
            Schema = schema;
            _remap = remap;
            _counts = counts;
        }

        /// <summary>Set by the land reader when a schema-4 save holds the 8 × 6 map (spec 010 §3.4): tile ids are remapped.</summary>
        internal bool OldMap { get; set; }

        /// <summary>A tile id, remapped to the 16 × 12 map for an old save; −1 (no tile) stays −1.</summary>
        internal int ReadTile()
        {
            int v = ReadInt();
            return OldMap && v >= 0 ? MapMigration.Remap(v) : v;
        }

        public byte ReadByte() { return _r.ReadByte(); }
        public ulong ReadULong() { return _r.ReadUInt64(); }
        public long ReadLong() { return _r.ReadInt64(); }
        public int ReadInt() { return _r.ReadInt32(); }

        public bool ReadBool()
        {
            byte b = _r.ReadByte();
            if (b > 1) throw new SaveFormatException("a bool of " + b);
            return b == 1;
        }

        public Fixed ReadFixed() { return Fixed.FromRaw(_r.ReadInt64()); }

        public string ReadString()
        {
            int n = _r.ReadInt32();
            if (n == -1) return null;
            return Encoding.UTF8.GetString(ReadExactly(n));
        }

        public byte[] ReadBytes()
        {
            int n = _r.ReadInt32();
            return n == -1 ? null : ReadExactly(n);
        }

        /// <summary>A list or array length: never negative, and never more than the bytes left could hold.</summary>
        public int ReadCount()
        {
            int n = _r.ReadInt32();
            if (n < 0 || n > _r.BaseStream.Length - _r.BaseStream.Position) throw new SaveFormatException("a count of " + n);
            return n;
        }

        /// <summary>An index into a content table, remapped to the running content (negative = none, kept as is).</summary>
        public int ReadRef(ContentTable table)
        {
            int saved = _r.ReadInt32();
            if (saved < 0) return saved;
            int[] map = _remap[(int)table];
            if (saved >= map.Length) throw new SaveFormatException(table + " index " + saved + " beyond the saved table");
            return map[saved];
        }

        /// <summary>The number of entries the save's table had (per-good and per-crop arrays are written at that length).</summary>
        public int SavedCount(ContentTable table) { return _remap[(int)table].Length; }

        /// <summary>The number of entries in the running content's table.</summary>
        public int Count(ContentTable table) { return _counts[(int)table]; }

        /// <summary>Where saved entry <paramref name="saved"/> of a content-aligned array goes in the running content.</summary>
        public int Slot(ContentTable table, int saved) { return _remap[(int)table][saved]; }

        private byte[] ReadExactly(int n)
        {
            if (n < 0 || n > _r.BaseStream.Length - _r.BaseStream.Position) throw new SaveFormatException("a length of " + n);
            byte[] b = _r.ReadBytes(n);
            if (b.Length != n) throw new EndOfStreamException();
            return b;
        }
    }
}
