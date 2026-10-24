using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Saves;

namespace Game.Services
{
    /// <summary>
    /// <see cref="IFileStore"/> on a real folder (spec 007 §3.3). Writes flush to disk before returning; a rename over an existing
    /// file is one atomic step (rename(2) on iOS), so the latest save is always whole.
    /// </summary>
    public sealed class FileStore : IFileStore
    {
        private readonly string _dir;

        public FileStore(string dir)
        {
            _dir = dir;
            Directory.CreateDirectory(dir);
        }

        private string P(string name) { return Path.Combine(_dir, name); }

        public byte[] Read(string name) { return File.Exists(P(name)) ? File.ReadAllBytes(P(name)) : null; }

        public void Write(string name, byte[] bytes)
        {
            using (var fs = new FileStream(P(name), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }
        }

        public void Rename(string from, string to)
        {
            if (File.Exists(P(to))) File.Replace(P(from), P(to), null);
            else File.Move(P(from), P(to));
        }

        public void Delete(string name) { File.Delete(P(name)); }
        public bool Exists(string name) { return File.Exists(P(name)); }

        public IReadOnlyList<string> List()
        {
            var names = new List<string>();
            foreach (string f in Directory.GetFiles(_dir)) names.Add(Path.GetFileName(f));
            names.Sort(StringComparer.Ordinal);
            return names;
        }
    }

    /// <summary>
    /// Saving for the app (spec 007 §3.3, §3.4): the town and its backups live in a private folder (iOS Library), never shown in
    /// the Files app; exports and imports go through the Documents folder, which the Files app shows. The rules themselves are
    /// <see cref="SavePoints"/> and <see cref="AutosavePolicy"/> in the sim, tested with <c>dotnet test</c>.
    /// </summary>
    public sealed class SaveService
    {
        public const string Extension = ".ftc";

        private readonly FileStore _store;
        private readonly SavePoints _points;
        private readonly AutosavePolicy _policy;

        public string Documents { get; }
        public long LastSavedWallUtc { get; private set; }

        public SaveService(string savesFolder, string documentsFolder, SaveDef def)
        {
            _store = new FileStore(savesFolder);
            _points = new SavePoints(_store, def);
            _policy = new AutosavePolicy(def);
            Documents = documentsFolder;
            Directory.CreateDirectory(documentsFolder);
        }

        /// <summary>Loads the town (or a backup). An unreadable town is kept, and a copy is put in Documents so it can be exported.</summary>
        public LoadOutcome Load(SimConfig config, long nowWallUtc, int utcOffsetMinutes)
        {
            LoadOutcome o = _points.LoadNewestValid(config, nowWallUtc + utcOffsetMinutes * 60L);
            if (o.KeptAs != null) File.WriteAllBytes(Path.Combine(Documents, o.KeptAs), _store.Read(o.KeptAs));
            // A town from an older build is upgraded as it loads; its file as it was goes to Documents first (the Files app shows
            // it), once per schema, so a week of play is never lost to an upgrade (owner, Sprint 12).
            if (o.State != null && o.Schema < SaveCodec.SchemaVersion)
            {
                string copy = Path.Combine(Documents, "FarmToCity-before-update-schema" + o.Schema + Extension);
                if (!File.Exists(copy)) File.WriteAllBytes(copy, _store.Read(o.File));
            }
            if (o.State != null) LastSavedWallUtc = o.SavedAtWallUtc;
            _policy.Saved(nowWallUtc);
            return o;
        }

        public int Save(GameState state, SimConfig config, long nowWallUtc, ulong contentFingerprint)
        {
            byte[] bytes = _points.Save(state, config, nowWallUtc, contentFingerprint);
            LastSavedWallUtc = nowWallUtc;
            _policy.Saved(nowWallUtc);
            return bytes.Length;
        }

        public void OnCommand(long nowWallUtc) { _policy.OnCommand(nowWallUtc); }
        public bool Due(long nowWallUtc) { return _policy.Due(nowWallUtc); }

        /// <summary>A copy of the latest save in Documents, named by the local date and time; the caller saves first.</summary>
        public string Export(long nowWallUtc, int utcOffsetMinutes)
        {
            string name = "FarmToCity-" + Local(nowWallUtc, utcOffsetMinutes).ToString("yyyy-MM-dd-HHmm", CultureInfo.InvariantCulture) + Extension;
            File.WriteAllBytes(Path.Combine(Documents, name), _store.Read(SavePoints.Latest));
            return name;
        }

        public string ExportText(string prefix, string extension, string text, long nowWallUtc, int utcOffsetMinutes)
        {
            string name = prefix + Local(nowWallUtc, utcOffsetMinutes).ToString("yyyy-MM-dd-HHmm", CultureInfo.InvariantCulture) + extension;
            File.WriteAllText(Path.Combine(Documents, name), text);
            return name;
        }

        /// <summary>The town files in Documents, by name.</summary>
        public List<string> TownFiles()
        {
            var names = new List<string>();
            foreach (string f in Directory.GetFiles(Documents, "*" + Extension)) names.Add(Path.GetFileName(f));
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        public byte[] ReadDocument(string name) { return File.ReadAllBytes(Path.Combine(Documents, name)); }

        /// <summary>A new game or an import replaces the town: its save and backups go (ADR 0023).</summary>
        public void DeleteTown()
        {
            _points.DeleteTown();
            LastSavedWallUtc = 0;
        }

        private static DateTime Local(long wallUtc, int offsetMinutes) { return DateTimeOffset.FromUnixTimeSeconds(wallUtc + offsetMinutes * 60L).UtcDateTime; }
    }
}
