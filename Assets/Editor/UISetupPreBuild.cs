using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Yobi.EditorTools
{
    // Runs every "Tools > Yobi > Setup ..." tool automatically before every player build - GUI
    // Build/Build And Run and BuildScript's batchmode path alike, mirroring ConfigFilePostBuild's
    // symmetric after-every-build hook. Without this, a code change to any Setup*UISetup.cs
    // script (or to RoomReminderListBehaviour's serialized fields, as happened switching the
    // watchlist header from two stacked sections to one with a switchModeButton) only reaches a
    // build if someone remembers to run Setup All UI by hand first - forgetting leaves the scene's
    // stale baked GameObjects built against old script assumptions, which is exactly what produced
    // two "Live Status" headers stacked on top of each other in one build.
    internal sealed class UISetupPreBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[UISetupPreBuild] Running all Yobi UI setup tools before build.");
            SetupAllUISetup.RunAll();
        }
    }
}
