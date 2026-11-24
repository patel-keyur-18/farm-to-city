using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Game.Editor
{
    /// <summary>
    /// A command-line build for the owner's device builds: <c>Unity -batchmode -quit -projectPath unity/FarmToCity -executeMethod
    /// Game.Editor.BuildCli.IosDevelopment</c> writes the Xcode project to <c>Builds/iOS</c>. A Development build, so the developer
    /// panel exists (ADR 0038). Delete <c>Builds/iOS</c> first: Unity's post-build step crashes on macOS 27 when it finds an Xcode
    /// project already there (it asks Xcode through the scripting bridge).
    /// From the open Editor use <c>StartIosDevelopment</c> or <c>StartIosRelease</c> (<c>unity command eval</c>): they return at once and build on the Editor's next update. Calling
    /// <c>IosDevelopment</c> inside an eval blocks the Editor past the pipeline's 5 s limit, which logs two errors into the build's own count (the build still succeeds).
    /// </summary>
    public static class BuildCli
    {
        public static void IosDevelopment() { Build(BuildOptions.Development); }

        public static void IosRelease() { Build(BuildOptions.None); }

        public static void StartIosDevelopment() { EditorApplication.delayCall += () => Build(BuildOptions.Development); }

        public static void StartIosRelease() { EditorApplication.delayCall += () => Build(BuildOptions.None); }

        private static void Build(BuildOptions options)
        {
            var player = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Bootstrap.unity" },
                locationPathName = "Builds/iOS",
                target = BuildTarget.iOS,
                options = options,
            };
            BuildReport report = BuildPipeline.BuildPlayer(player);
            UnityEngine.Debug.Log("Build " + report.summary.result + ": " + report.summary.totalErrors + " errors, " + report.summary.totalWarnings + " warnings");
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
