using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Sim.Core.Commands;
using Sim.Core.Maths;

namespace Sim.Core.Saves
{
    public enum SaveError
    {
        None = 0,

        /// <summary>A checksum mismatch, a short or overlong file, or bytes that don't parse.</summary>
        CorruptSave,

        /// <summary>Written by a build with a newer schema than this one knows.</summary>
        SaveFromNewerVersion,

        /// <summary>Older than <c>minSchema</c> (3, frozen from the release candidate, ADR 0047): refused and kept (ADR 0041).</summary>
        SaveTooOld,

        /// <summary>The save names a content id (or a content section) this build's content lacks; a migration must map it.</summary>
        UnknownContentId,
    }

    /// <summary>The save header: readable without decompressing (the import list shows it; spec 007 §3.4).</summary>
    public sealed class SaveHeader
    {
        public int FormatVersion { get; internal set; }
        public int Schema { get; internal set; }
        public string SimVersion { get; internal set; }

        /// <summary>FNV-1a of the content files the save was written with: a save's log replays only on matching content.</summary>
        public ulong ContentFingerprint { get; internal set; }
        public long SavedAtWallUtc { get; internal set; }
        public long SimTime { get; internal set; }

        /// <summary>Money (1/100 coin), or 0 in a sim without a ledger.</summary>
        public long Cash { get; internal set; }
    }

    public sealed class LoadResult
    {
        public SaveError Error { get; internal set; }

        /// <summary>What went wrong, for the developer and bug reports (never shown raw to the player).</summary>
        public string Detail { get; internal set; }
        public SaveHeader Header { get; internal set; }
        public GameState State { get; internal set; }
        public bool Ok { get { return Error == SaveError.None; } }

        /// <summary>The save held the Phase 1 8 × 6 map and was moved into the 16 × 12 map (spec 010 §3.4): reset the camera.</summary>
        public bool MigratedMap { get; internal set; }

        /// <summary>Set when the farm lane took the save's parcel <c>s5</c>, for the one-time notice; else null.</summary>
        public RoadTake RoadTake { get; internal set; }
    }

    /// <summary>Thrown while reading when the save names content the running build doesn't have.</summary>
    public sealed class SaveContentException : Exception
    {
        public SaveContentException(string message) : base(message) { }
    }

    /// <summary>
    /// The save file (spec 007 §3.1): a header, then a gzip payload holding the content id tables, the state (written through
    /// each class's <c>WriteTo</c>, the same path as the hash), the replay base and the command log. A checksum (FNV-1a 64)
    /// covers the header and the compressed payload, so any damaged byte fails the load instead of loading a wrong town.
    /// </summary>
    public static class SaveCodec
    {
        public const int FormatVersion = 1;

        /// <summary>
        /// Raised whenever the state's fields change (spec 007 §3.1). From schema 3 on <c>minSchema</c> stays 3: a new schema ships a
        /// migration step in the readers (branch on <see cref="SaveReader.Schema"/>) and a frozen fixture save (ADR 0047 §3, AC-14).
        /// </summary>
        public const int SchemaVersion = 8; // 8: the inherited cattle acre (ADR 0064); 7: cattle, the shed and the livestock accounts (spec 012); 6: the trader's truck, daily prices and supermarket orders (spec 011); 5: the 16 × 12 village map, tile ids remapped (spec 010, ADR 0052); 4: labourers hired for a crop, a neighbour's tile and until (ADR 0048); 3: the guidance and era state (spec 009, ADR 0045); 2: the weather run's start (ADR 0043); below 3 refused and kept (ADR 0041 §2)

        public const int DefaultMaxLogEntries = 100_000;

        private static readonly byte[] Magic = { (byte)'F', (byte)'T', (byte)'C', (byte)'1' };

        /// <summary>
        /// The save bytes of <paramref name="state"/>. When the command log has reached <paramref name="maxLogEntries"/>, the
        /// current state becomes the replay base and the log restarts first (spec 007 §3.2): history only, the town is untouched.
        /// <paramref name="schema"/> is for tests of newer saves.
        /// </summary>
        public static byte[] Write(GameState state, SimConfig config, long savedAtWallUtc, ulong contentFingerprint = 0,
            int maxLogEntries = DefaultMaxLogEntries, int schema = SchemaVersion)
        {
            if (state.CommandLog.Entries.Count >= maxLogEntries)
                state.CommandLog.Checkpoint(Encode(state, config, savedAtWallUtc, contentFingerprint, schema, false));
            return Encode(state, config, savedAtWallUtc, contentFingerprint, schema, true);
        }

        public static LoadResult Read(byte[] bytes, SimConfig config, int minSchema = 1)
        {
            var result = new LoadResult();
            int payloadAt;
            try
            {
                result.Header = ParseHeader(bytes, out payloadAt);
            }
            catch (Exception e) when (e is SaveFormatException || e is EndOfStreamException || e is ArgumentException || e is DecoderFallbackException)
            {
                return Fail(result, SaveError.CorruptSave, e.Message);
            }
            if (result.Header.Schema > SchemaVersion) return Fail(result, SaveError.SaveFromNewerVersion, "schema " + result.Header.Schema);
            if (result.Header.Schema < minSchema) return Fail(result, SaveError.SaveTooOld, "schema " + result.Header.Schema);

            try
            {
                using (var payload = new MemoryStream())
                {
                    using (var gz = new GZipStream(new MemoryStream(bytes, payloadAt, bytes.Length - payloadAt), CompressionMode.Decompress))
                        gz.CopyTo(payload);
                    payload.Position = 0;
                    SaveReader r = ReadTables(payload, result.Header.Schema, config);
                    GameState state = GameState.ReadFrom(r, config);
                    state.CommandLog.Base = r.ReadBytes();
                    int n = r.ReadCount();
                    for (int i = 0; i < n; i++)
                    {
                        long t = r.ReadLong();
                        state.CommandLog.Add(t, CommandCodec.Read(r));
                    }
                    if (payload.Position != payload.Length) throw new SaveFormatException("bytes after the command log");
                    if (r.OldMap)
                    {
                        result.MigratedMap = true;
                        result.RoadTake = MapMigration.TakeFarmLane(Simulation.FromState(config, state));
                    }
                    bool cattleAcre = result.Header.Schema < CattleAcreMigration.Schema && CattleAcreMigration.Apply(Simulation.FromState(config, state));
                    // The old history can't replay on the new map or under the truck's rules: the migrated town becomes the
                    // replay base (spec 007 AC-6). Cattle's places change where compost is kept, so a schema-6 history does too (spec 012),
                    // and the cattle acre changes the map (ADR 0064).
                    if (r.OldMap || cattleAcre || (result.Header.Schema < 6 && config.Game?.Market.Truck != null) || (result.Header.Schema < 7 && config.Game?.Livestock != null))
                        state.CommandLog.Checkpoint(Encode(state, config, result.Header.SavedAtWallUtc, result.Header.ContentFingerprint, SchemaVersion, false));
                    result.State = state;
                }
            }
            catch (SaveContentException e)
            {
                return Fail(result, SaveError.UnknownContentId, e.Message);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                // The checksum passed, so this is a bug or a deliberate forgery: either way the town isn't loaded wrong.
                return Fail(result, SaveError.CorruptSave, e.GetType().Name + ": " + e.Message);
            }
            return result;
        }

        /// <summary>The header of a save whose checksum is good, or null.</summary>
        public static SaveHeader ReadHeader(byte[] bytes)
        {
            try { return ParseHeader(bytes, out _); }
            catch (Exception e) when (e is SaveFormatException || e is EndOfStreamException || e is ArgumentException || e is DecoderFallbackException)
            {
                return null;
            }
        }

        /// <summary>
        /// Replays a loaded save's history: from its replay base (or the new game) through its command log. A bug report
        /// reproduces with it; it must end at the loaded state's hash (spec 007 AC-6).
        /// </summary>
        public static Simulation Replay(LoadResult loaded, SimConfig config)
        {
            var commands = new List<Command>();
            foreach (CommandLogEntry e in loaded.State.CommandLog.Entries) commands.Add(e.Command);
            if (loaded.State.CommandLog.Base == null) return Simulation.Replay(config, commands);
            LoadResult b = Read(loaded.State.CommandLog.Base, config);
            if (!b.Ok) throw new InvalidOperationException("The replay base doesn't load: " + b.Error + " " + b.Detail);
            Simulation sim = Simulation.FromState(config, b.State);
            for (int i = 0; i < commands.Count; i++)
            {
                CommandResult r = sim.Execute(commands[i]);
                if (!r.Accepted) throw new InvalidOperationException("Replay diverged: command " + i + " (" + commands[i].GetType().Name + ") was " + r + ".");
            }
            return sim;
        }

        // ---- Writing ----

        private static byte[] Encode(GameState state, SimConfig config, long savedAtWallUtc, ulong fingerprint, int schema, bool withHistory)
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Fastest, leaveOpen: true))
                using (var buffered = new BufferedStream(gz, 1 << 16))
                {
                    var w = new SaveWriter(buffered);
                    w.WriteInt(ContentTables.Count);
                    for (int t = 0; t < ContentTables.Count; t++)
                    {
                        string[] ids = ContentTables.Ids(config, (ContentTable)t);
                        w.WriteInt(ids.Length);
                        foreach (string id in ids) w.WriteString(id);
                    }
                    state.WriteTo(w);
                    w.WriteBytes(withHistory ? state.CommandLog.Base : null);
                    IReadOnlyList<CommandLogEntry> log = withHistory ? state.CommandLog.Entries : (IReadOnlyList<CommandLogEntry>)new CommandLogEntry[0];
                    w.WriteInt(log.Count);
                    foreach (CommandLogEntry e in log)
                    {
                        w.WriteLong(e.SimTime);
                        CommandCodec.Write(w, e.Command);
                    }
                    w.Flush();
                }
                payload = ms.ToArray();
            }

            using (var file = new MemoryStream())
            {
                var w = new SaveWriter(file);
                foreach (byte b in Magic) w.WriteByte(b);
                w.WriteInt(FormatVersion);
                w.WriteInt(schema);
                w.WriteString(SimInfo.Version);
                w.WriteULong(fingerprint);
                w.WriteLong(savedAtWallUtc);
                w.WriteLong(state.Clock.SimTime);
                w.WriteLong(state.Ledger == null ? 0 : state.Ledger.Cash);
                w.WriteLong(payload.Length);
                w.Flush();
                w.WriteULong(Checksum(file.GetBuffer(), (int)file.Length, payload));
                w.Flush();
                file.Write(payload, 0, payload.Length);
                return file.ToArray();
            }
        }

        // ---- Reading ----

        private static SaveHeader ParseHeader(byte[] bytes, out int payloadAt)
        {
            if (bytes == null) throw new SaveFormatException("no bytes");
            using (var ms = new MemoryStream(bytes, false))
            {
                var r = new SaveReader(ms, 0, null, null);
                for (int i = 0; i < Magic.Length; i++)
                    if (r.ReadByte() != Magic[i]) throw new SaveFormatException("not a Farm to City save");
                var h = new SaveHeader { FormatVersion = r.ReadInt(), Schema = r.ReadInt(), SimVersion = r.ReadString() };
                h.ContentFingerprint = r.ReadULong();
                h.SavedAtWallUtc = r.ReadLong();
                h.SimTime = r.ReadLong();
                h.Cash = r.ReadLong();
                long length = r.ReadLong();
                int checksumAt = (int)ms.Position;
                ulong checksum = r.ReadULong();
                payloadAt = (int)ms.Position;
                if (length != bytes.Length - payloadAt) throw new SaveFormatException("the payload is " + (bytes.Length - payloadAt) + " bytes, not " + length);
                var payload = new byte[length];
                Array.Copy(bytes, payloadAt, payload, 0, length);
                if (Checksum(bytes, checksumAt, payload) != checksum) throw new SaveFormatException("checksum mismatch");
                if (h.FormatVersion != FormatVersion) throw new SaveFormatException("format " + h.FormatVersion);
                return h;
            }
        }

        private static SaveReader ReadTables(Stream payload, int schema, SimConfig config)
        {
            var bootstrap = new SaveReader(payload, schema, null, null);
            int tables = bootstrap.ReadCount();
            if (tables > ContentTables.Count) throw new SaveFormatException(tables + " content tables");
            var remap = new int[ContentTables.Count][];
            var counts = new int[ContentTables.Count];
            for (int t = 0; t < ContentTables.Count; t++)
            {
                string[] current = ContentTables.Ids(config, (ContentTable)t);
                counts[t] = current.Length;
                int n = t < tables ? bootstrap.ReadCount() : 0;
                remap[t] = new int[n];
                for (int i = 0; i < n; i++)
                {
                    string id = bootstrap.ReadString();
                    int at = Array.IndexOf(current, id);
                    if (at < 0) throw new SaveContentException(((ContentTable)t).ToString().ToLowerInvariant() + " '" + id + "'");
                    remap[t][i] = at;
                }
            }
            return new SaveReader(payload, schema, remap, counts);
        }

        private static ulong Checksum(byte[] header, int headerLength, byte[] payload)
        {
            var h = new StateHasher();
            for (int i = 0; i < headerLength; i++) h.WriteByte(header[i]);
            foreach (byte b in payload) h.WriteByte(b);
            return h.Value;
        }

        private static LoadResult Fail(LoadResult r, SaveError error, string detail)
        {
            r.Error = error;
            r.Detail = detail;
            r.State = null;
            return r;
        }
    }
}
