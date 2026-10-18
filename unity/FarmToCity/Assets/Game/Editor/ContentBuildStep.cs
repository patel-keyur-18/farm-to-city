using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Before every player build, copies the repository's <c>content/*.json</c> into
    /// <c>Assets/StreamingAssets/content/</c> (git-ignored), so the build carries the current balancing data.
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
            Debug.Log("ContentBuildStep: copied " + files.Length + " content files to " + target);
        }
    }
}
