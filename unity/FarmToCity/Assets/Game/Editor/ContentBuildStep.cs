using System.IO;
using UnityEditor.Build;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Before every player build, copies the repository's <c>content/*.json</c> into
    /// <c>Assets/StreamingAssets/content/</c> (git-ignored), so the build carries the current balancing data. In
    /// Development builds also get the golden replays and their frozen content in <c>StreamingAssets/golden/</c>.
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

            // Development builds only (ADR 0038): the golden replays and their frozen content, for the on-device determinism check
            // in the developer panel (ADR 0035).
            string goldenTarget = Path.Combine(Application.dataPath, "StreamingAssets", "golden");
            if (Directory.Exists(goldenTarget)) Directory.Delete(goldenTarget, true);
            if ((report.summary.options & BuildOptions.Development) != 0)
            {
                string golden = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "tests", "Sim.Core.Tests", "golden"));
                foreach (string sub in new[] { "", "content", "content-005" })
                {
                    string dir = Path.Combine(goldenTarget, sub);
                    Directory.CreateDirectory(dir);
                    foreach (string file in Directory.GetFiles(Path.Combine(golden, sub), "*.json")) File.Copy(file, Path.Combine(dir, Path.GetFileName(file)));
                }
            }

        }
    }
}
