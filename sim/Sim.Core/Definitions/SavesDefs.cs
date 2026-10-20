namespace Sim.Core.Definitions
{
    /// <summary>Save points and autosave (spec 007 §4, <c>content/saves.json</c>).</summary>
    public sealed class SaveDef
    {
        /// <summary>The oldest schema this build loads; until the playtest build it rises with the schema (ADR 0041).</summary>
        public int MinSchema { get; }
        public int HourlyBackups { get; }
        public int DailyBackups { get; }
        public int AutosaveMinutes { get; }
        public int CommandDebounceSeconds { get; }
        public int MaxLogEntries { get; }

        public SaveDef(int minSchema, int hourlyBackups, int dailyBackups, int autosaveMinutes, int commandDebounceSeconds, int maxLogEntries)
        {
            MinSchema = minSchema;
            HourlyBackups = hourlyBackups;
            DailyBackups = dailyBackups;
            AutosaveMinutes = autosaveMinutes;
            CommandDebounceSeconds = commandDebounceSeconds;
            MaxLogEntries = maxLogEntries;
        }
    }

    /// <summary>Local notifications (spec 007 §3.5, ADR 0041). Quiet hours are minutes after local midnight.</summary>
    public sealed class NotifyDef
    {
        public int MaxPending { get; }
        public int GroupWindowMinutes { get; }
        public int InfoGroupMinutes { get; }
        public int QuietFromMinute { get; }
        public int QuietToMinute { get; }

        public NotifyDef(int maxPending, int groupWindowMinutes, int infoGroupMinutes, int quietFromMinute, int quietToMinute)
        {
            MaxPending = maxPending;
            GroupWindowMinutes = groupWindowMinutes;
            InfoGroupMinutes = infoGroupMinutes;
            QuietFromMinute = quietFromMinute;
            QuietToMinute = quietToMinute;
        }
    }

    /// <summary>The local play log (spec 007 §3.6).</summary>
    public sealed class PlayLogDef
    {
        public int MaxBytes { get; }

        public PlayLogDef(int maxBytes) { MaxBytes = maxBytes; }
    }

    /// <summary>Everything in <c>saves.json</c>. Not part of <see cref="GameDefs"/>: none of it changes the town.</summary>
    public sealed class SavesDefs
    {
        public SaveDef Save { get; }
        public NotifyDef Notify { get; }
        public PlayLogDef PlayLog { get; }

        public SavesDefs(SaveDef save, NotifyDef notify, PlayLogDef playLog)
        {
            Save = save;
            Notify = notify;
            PlayLog = playLog;
        }
    }
}
