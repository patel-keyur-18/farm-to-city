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
    }
}
