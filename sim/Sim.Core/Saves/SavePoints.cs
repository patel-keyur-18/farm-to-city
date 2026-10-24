using System;
using System.Collections.Generic;
using Sim.Core.Definitions;

namespace Sim.Core.Saves
{
    /// <summary>
    /// The files a town lives in (spec 007 §3.3). Like a Java NIO <c>FileSystem</c> narrowed to what saving needs, so every rule
    /// is tested in memory; <c>Game.Services</c> implements it on the app's persistent data folder.
    /// </summary>
    public interface IFileStore
    {
        /// <summary>The file's bytes, or null when it doesn't exist.</summary>
        byte[] Read(string name);

        /// <summary>Writes the whole file and flushes it to disk before returning.</summary>
        void Write(string name, byte[] bytes);

        /// <summary>Replaces <paramref name="to"/> with <paramref name="from"/> in one step (an atomic rename).</summary>
        void Rename(string from, string to);

        void Delete(string name);
        bool Exists(string name);
        IReadOnlyList<string> List();
    }

    /// <summary>What <see cref="SavePoints.LoadNewestValid"/> found.</summary>
    public sealed class LoadOutcome
    {
        /// <summary>The town, or null: a fresh install (no error) or an unreadable town (see <see cref="Error"/>).</summary>
        public GameState State { get; internal set; }
        public string File { get; internal set; }

        /// <summary>True when the latest save was damaged and a backup loaded: the player sees a notice naming it.</summary>
        public bool Restored { get; internal set; }
        public long SavedAtWallUtc { get; internal set; }
        public SaveError Error { get; internal set; }
        public string Detail { get; internal set; }

        /// <summary>The name an unreadable town was kept under (ADR 0041), or null.</summary>
        public string KeptAs { get; internal set; }

        /// <summary>The town was moved into the 16 × 12 map (spec 010 §3.4): the remembered camera no longer fits.</summary>
        public bool MigratedMap { get; internal set; }

        /// <summary>The farm lane took the town's parcel <c>s5</c>, for the one-time card; else null.</summary>
        public RoadTake RoadTake { get; internal set; }

        /// <summary>The loaded file's schema; below <see cref="SaveCodec.SchemaVersion"/> it was migrated as it loaded.</summary>
        public int Schema { get; internal set; }
    }

    /// <summary>
    /// Safety save points (spec 007 §3.3, ADR 0023): an atomic write of <see cref="Latest"/>, hourly and daily backups, and
    /// recovery from the newest valid file. Never an undo: nothing loads an older save by choice.
    /// </summary>
    public sealed class SavePoints
    {
        public const string Latest = "town.ftc";
        public const string Temp = "town.tmp";

        private readonly IFileStore _store;
        private readonly SaveDef _def;

        public SavePoints(IFileStore store, SaveDef def)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _def = def ?? throw new ArgumentNullException(nameof(def));
        }

        public static string Hourly(int i) { return "town.hourly." + i + ".ftc"; }
        public static string Daily(int i) { return "town.daily." + i + ".ftc"; }

        /// <summary>
        /// Saves the town: writes <see cref="Temp"/>, reads it back and checks it, copies the current latest into the backups
        /// when they are due, then renames the temp file over the latest. A crash at any step leaves the previous latest whole.
        /// Returns the bytes written.
        /// </summary>
        public byte[] Save(GameState state, SimConfig config, long nowWallUtc, ulong contentFingerprint = 0)
        {
            byte[] bytes = SaveCodec.Write(state, config, nowWallUtc, contentFingerprint, _def.MaxLogEntries);
            if (_store.Exists(Temp)) _store.Delete(Temp);
            _store.Write(Temp, bytes);
            byte[] back = _store.Read(Temp);
            if (back == null || SaveCodec.ReadHeader(back) == null) throw new InvalidOperationException("The save didn't read back intact.");

            byte[] current = _store.Read(Latest);
            SaveHeader currentHeader = current == null ? null : SaveCodec.ReadHeader(current);
            if (currentHeader != null)
            {
                Rotate(current, currentHeader.SavedAtWallUtc, Hourly, _def.HourlyBackups, 3600);
                Rotate(current, currentHeader.SavedAtWallUtc, Daily, _def.DailyBackups, 86400);
            }
            _store.Rename(Temp, Latest);
            return bytes;
        }

        /// <summary>The save being replaced becomes the newest backup when that is missing or at least <paramref name="spacing"/> seconds older.</summary>
        private void Rotate(byte[] current, long currentSavedAt, Func<int, string> name, int count, long spacing)
        {
            if (count == 0) return;
            byte[] newest = _store.Read(name(1));
            SaveHeader h = newest == null ? null : SaveCodec.ReadHeader(newest);
            if (h != null && currentSavedAt - h.SavedAtWallUtc < spacing) return;
            if (_store.Exists(name(count))) _store.Delete(name(count));
            for (int i = count - 1; i >= 1; i--)
                if (_store.Exists(name(i))) _store.Rename(name(i), name(i + 1));
            _store.Write(name(1), current);
        }

        /// <summary>
        /// Loads the latest save, or the newest valid backup when it is damaged (spec 007 §3.3). A town this build can't read
        /// (too old, newer, or naming content it lacks) is kept as <c>town.unreadable-&lt;date&gt;.ftc</c> with its backups deleted, and
        /// a new town starts (ADR 0041). Null state and no error: there was no town yet. <paramref name="localNow"/> (the device's
        /// local time as Unix seconds) names the kept file, like an export.
        /// </summary>
        public LoadOutcome LoadNewestValid(SimConfig config, long localNow = 0)
        {
            var candidates = new List<KeyValuePair<long, string>>();
            for (int i = 1; i <= _def.HourlyBackups; i++) AddBackup(candidates, Hourly(i));
            for (int i = 1; i <= _def.DailyBackups; i++) AddBackup(candidates, Daily(i));
            candidates.Sort((a, b) => b.Key != a.Key ? b.Key.CompareTo(a.Key) : string.CompareOrdinal(a.Value, b.Value)); // newest first
            if (_store.Exists(Latest)) candidates.Insert(0, new KeyValuePair<long, string>(0, Latest));

            LoadOutcome failed = null;
            foreach (KeyValuePair<long, string> c in candidates)
            {
                LoadResult r = SaveCodec.Read(_store.Read(c.Value), config, _def.MinSchema);
                if (r.Ok)
                    return new LoadOutcome { State = r.State, File = c.Value, Restored = c.Value != Latest, SavedAtWallUtc = r.Header.SavedAtWallUtc, MigratedMap = r.MigratedMap, RoadTake = r.RoadTake, Schema = r.Header.Schema };
                if (failed == null) failed = new LoadOutcome { File = c.Value, Error = r.Error, Detail = r.Detail };
                if (r.Error != SaveError.CorruptSave) { failed = new LoadOutcome { File = c.Value, Error = r.Error, Detail = r.Detail }; break; }
            }
            if (failed == null) return new LoadOutcome();

            // Keep the unreadable town under a name of its own; its backups are older copies of the same town.
            string kept = "town.unreadable-" + DateTimeOffset.FromUnixTimeSeconds(localNow).UtcDateTime.ToString("yyyyMMdd-HHmm", System.Globalization.CultureInfo.InvariantCulture) + ".ftc";
            _store.Rename(failed.File, kept);
            DeleteTown();
            failed.KeptAs = kept;
            return failed;
        }

        /// <summary>Deletes the town and its backups (a new game or an import replaces the town, ADR 0023).</summary>
        public void DeleteTown()
        {
            foreach (string f in new List<string>(_store.List()))
                if (f == Latest || f == Temp || f.StartsWith("town.hourly.", StringComparison.Ordinal) || f.StartsWith("town.daily.", StringComparison.Ordinal))
                    _store.Delete(f);
        }

        private void AddBackup(List<KeyValuePair<long, string>> list, string name)
        {
            byte[] b = _store.Read(name);
            if (b == null) return;
            SaveHeader h = SaveCodec.ReadHeader(b);
            list.Add(new KeyValuePair<long, string>(h == null ? long.MinValue : h.SavedAtWallUtc, name));
        }
    }

    /// <summary>
    /// When to save (spec 007 §3.3): every <c>autosaveMinutes</c> of play, and <c>commandDebounceSeconds</c> after the last player
    /// command, so a burst of taps is one save. Live Syncs are not commands. Backgrounding and Save now save at once (the caller).
    /// </summary>
    public sealed class AutosavePolicy
    {
        private readonly SaveDef _def;
        private long _lastSave;
        private long _lastCommand;
        private bool _pending;

        public AutosavePolicy(SaveDef def) { _def = def ?? throw new ArgumentNullException(nameof(def)); }

        public void OnCommand(long nowWallUtc)
        {
            _pending = true;
            _lastCommand = nowWallUtc;
        }

        public bool Due(long nowWallUtc)
        {
            return (_pending && nowWallUtc - _lastCommand >= _def.CommandDebounceSeconds) || nowWallUtc - _lastSave >= _def.AutosaveMinutes * 60L;
        }

        public void Saved(long nowWallUtc)
        {
            _lastSave = nowWallUtc;
            _pending = false;
        }
    }
}
