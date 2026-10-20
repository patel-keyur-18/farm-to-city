using System.IO;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Where the content JSON comes from. In the Editor it's read straight from the repository's <c>content/</c>
    /// folder, so there is one source of truth. Player builds read the copy that the pre-build step
    /// (<c>ContentBuildStep</c>) puts in StreamingAssets, which is a plain folder inside the iOS app bundle.
    /// </summary>
    public static class ContentSource
    {
        public static string Folder
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "content"));
#else
                // ponytail: File IO works on iOS; Android's StreamingAssets live in the APK and would need UnityWebRequest.
                return Path.Combine(Application.streamingAssetsPath, "content");
#endif
            }
        }

        public static string Read(string file) { return File.ReadAllText(Path.Combine(Folder, file)); }

        /// <summary>
        /// FNV-1a over the content files (names and text, in name order), recorded in each save: a save's command log replays
        /// only on the content it was written with (spec 007 §3.1).
        /// </summary>
        public static ulong Fingerprint()
        {
            var files = new System.Collections.Generic.List<string>(Directory.GetFiles(Folder, "*.json", SearchOption.AllDirectories));
            files.Sort(System.StringComparer.Ordinal);
            var h = new Sim.Core.Maths.StateHasher();
            foreach (string f in files)
            {
                h.WriteString(Path.GetRelativePath(Folder, f).Replace('\\', '/'));
                h.WriteString(File.ReadAllText(f));
            }
            return h.Value;
        }
    }
}
