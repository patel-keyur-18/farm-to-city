using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Before every player build, copies the repository's <c>content/*.json</c> into
    /// <c>Assets/StreamingAssets/content/</c> (git-ignored), so the build carries the current balancing data. In
    /// Phase 0 it also copies the golden replays and their frozen content to <c>StreamingAssets/golden/</c>.
    /// Like a Maven resources step: <c>content/</c> stays the only source.
    /// </summary>
    public sealed class ContentBuildStep : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "content"));
            string target = Path.Combine(Application.dataPath, "StreamingAssets", "content");
            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.CreateDirectory(target);
            string[] files = Directory.GetFiles(source, "*.json");
            if (files.Length == 0) throw new BuildFailedException("No content JSON found in " + source);
            foreach (string file in files) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            // The string tables (content/strings/*.json) go to StreamingAssets/content/strings/.
            string stringsSource = Path.Combine(source, "strings");
            if (Directory.Exists(stringsSource))
            {
                Directory.CreateDirectory(Path.Combine(target, "strings"));
                foreach (string file in Directory.GetFiles(stringsSource, "*.json")) File.Copy(file, Path.Combine(target, "strings", Path.GetFileName(file)));
            }
            Debug.Log("ContentBuildStep: copied " + files.Length + " content files to " + target);

            // Phase 0 (US-023): the golden replays and their frozen content, for the on-device determinism check.
            string golden = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "tests", "Sim.Core.Tests", "golden"));
            string goldenTarget = Path.Combine(Application.dataPath, "StreamingAssets", "golden");
            if (Directory.Exists(goldenTarget)) Directory.Delete(goldenTarget, true);
            Directory.CreateDirectory(Path.Combine(goldenTarget, "content"));
            foreach (string file in Directory.GetFiles(golden, "*.json")) File.Copy(file, Path.Combine(goldenTarget, Path.GetFileName(file)));
            foreach (string file in Directory.GetFiles(Path.Combine(golden, "content"), "*.json"))
                File.Copy(file, Path.Combine(goldenTarget, "content", Path.GetFileName(file)));

            // Phase 0 (US-021): FrameTimingManager needs frame timing stats in the player.
            UnityEditor.PlayerSettings.enableFrameTimingStats = true;
        }
    }
}
