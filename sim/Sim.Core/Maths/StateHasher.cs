namespace Sim.Core.Maths
{
    /// <summary>
    /// 64-bit FNV-1a over a canonical byte stream (spec 001 §3.1). Values are written little-endian,
    /// so the hash is the same on every platform. Determinism tests compare whole states with it.
    /// </summary>
    public sealed class StateHasher : IStateWriter
    {
        private const ulong OffsetBasis = 0xCBF29CE484222325UL;
        private const ulong Prime = 0x100000001B3UL;

        private ulong _hash = OffsetBasis;

        public ulong Value { get { return _hash; } }

        public bool Hashing { get { return true; } }

        public void WriteRef(ContentTable table, int index) { WriteInt(index); }

        public void WriteByte(byte b)
        {
            unchecked { _hash = (_hash ^ b) * Prime; }
        }

        public void WriteULong(ulong v)
        {
            for (int i = 0; i < 8; i++) WriteByte((byte)(v >> (8 * i)));
        }

        public void WriteLong(long v) { WriteULong(unchecked((ulong)v)); }

        public void WriteInt(int v)
        {
            uint u = unchecked((uint)v);
            for (int i = 0; i < 4; i++) WriteByte((byte)(u >> (8 * i)));
        }

        public void WriteBool(bool v) { WriteByte(v ? (byte)1 : (byte)0); }

        public void WriteFixed(Fixed v) { WriteLong(v.Raw); }

        /// <summary>Length, then each UTF-16 code unit little-endian. Null is written as length −1.</summary>
        public void WriteString(string s)
        {
            if (s == null) { WriteInt(-1); return; }
            WriteInt(s.Length);
            foreach (char c in s) { WriteByte((byte)c); WriteByte((byte)(c >> 8)); }
        }
    }
}
