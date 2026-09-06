using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Yobi.Editor
{
    public static class BuildScript
    {
        public static void BuildMacStandalone()
        {
            var scenes = new[] { "Assets/Scenes/SampleScene.unity" };
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Build/macOS/Yobi.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
