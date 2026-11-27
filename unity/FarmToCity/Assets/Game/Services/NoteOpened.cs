using System;

namespace Game.Services
{
    /// <summary>A notification tap that opened the app: counted once, and the town it was from (0 when unknown, such as a note from an older build).</summary>
    public readonly struct NoteOpened
    {
        public bool Fresh { get; }
        public int Town { get; }

        public NoteOpened(bool fresh, int town)
        {
            Fresh = fresh;
            Town = town;
        }

        /// <summary>A note's data is "2|1800000000:3": the town before the bar, then a mark unique to the schedule. One from an older build has no bar: town 0.</summary>
        public static int TownOfData(string data)
        {
            int bar = data == null ? -1 : data.IndexOf('|');
            return bar > 0 && int.TryParse(data.Substring(0, bar), out int town) && town > 0 && town <= SaveService.Slots ? town : 0;
        }

        /// <summary>
        /// The town a tap asks the app to switch to (spec 031 §3.1): the one the note was from when that isn't the open town and still holds a town, else 0
        /// (an old note, a tap on the open town's own note, or a slot that is empty now).
        /// </summary>
        public int Elsewhere(int openSlot, Func<int, bool> slotHasTown)
        {
            return Fresh && Town > 0 && Town != openSlot && Town <= SaveService.Slots && slotHasTown(Town) ? Town : 0;
        }
    }
}
