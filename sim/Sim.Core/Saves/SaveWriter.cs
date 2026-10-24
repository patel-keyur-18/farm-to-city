using System.IO;
using System.Text;
using Sim.Core.Maths;

namespace Sim.Core.Saves
{
    /// <summary>
    /// Writes the state for a save (spec 007 §3.1): little-endian integers, length-prefixed UTF-8 strings, <c>Fixed</c> as its
    /// raw <c>long</c>, content references as indices into the content tables the save also stores. Like a hand-written
    /// <c>Externalizable</c> in Java: no reflection, so IL2CPP keeps it intact.
    /// </summary>
    public sealed class SaveWriter : IStateWriter
    {
        private readonly BinaryWriter _w;

        public SaveWriter(Stream stream) { _w = new BinaryWriter(stream, new UTF8Encoding(false), leaveOpen: true); }

        public bool Hashing { get { return false; } }

        /// <summary>The schema being written: the current one, or an older one for migration tests (what it lacked is left out).</summary>
        public int Schema { get; internal set; } = SaveCodec.SchemaVersion;

        public void WriteByte(byte b) { _w.Write(b); }
        public void WriteULong(ulong v) { _w.Write(v); }
        public void WriteLong(long v) { _w.Write(v); }
        public void WriteInt(int v) { _w.Write(v); }
        public void WriteBool(bool v) { _w.Write(v ? (byte)1 : (byte)0); }
        public void WriteFixed(Fixed v) { _w.Write(v.Raw); }

        /// <summary>Byte length, then UTF-8. Null is length −1.</summary>
        public void WriteString(string s)
        {
            if (s == null) { _w.Write(-1); return; }
            byte[] b = Encoding.UTF8.GetBytes(s);
            _w.Write(b.Length);
            _w.Write(b);
        }

        public void WriteRef(ContentTable table, int index) { _w.Write(index); }

        public void WriteBytes(byte[] bytes)
        {
            if (bytes == null) { _w.Write(-1); return; }
            _w.Write(bytes.Length);
            _w.Write(bytes);
        }

        public void Flush() { _w.Flush(); }
    }
}
