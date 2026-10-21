using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace Game.Editor
{
    /// <summary>
    /// iOS build settings for saves and notifications (spec 007 §3.4, §3.5, ADR 0041): the app's Documents folder shows in the Files
    /// app (export and import, no plugin needed), and the notifications package never asks for permission at launch: the game asks
    /// first, and iOS's dialog follows only a "Yes". Development builds also get frame-timing stats (ADR 0047 §4).
    /// </summary>
    public sealed class IosBuildStep : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder { get { return 10; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            Unity.Notifications.NotificationSettings.iOSSettings.RequestAuthorizationOnAppLaunch = false;
            // The developer panel's frame-time check reads FrameTimingManager (ADR 0047 §4); a Release build needs none of it.
            PlayerSettings.enableFrameTimingStats = (report.summary.options & BuildOptions.Development) != 0;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
#if UNITY_IOS
            if (report.summary.platform != BuildTarget.iOS) return;
            string path = Path.Combine(report.summary.outputPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(path);
            plist.root.SetBoolean("UIFileSharingEnabled", true);
            plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);
            plist.WriteToFile(path);
#endif
        }
    }
}
