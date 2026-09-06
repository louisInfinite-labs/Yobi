using UnityEditor;
using UnityEngine;

namespace Yobi.EditorTools
{
    // One menu item that reruns every "Tools > Yobi > Setup ..." tool in the order that keeps
    // their cross-dependencies (SettingsModalUISetup finds "RoomButtonDock" by name, so
    // RoomUIPanelSetup - which creates it - must run first) intact. Exists so nobody has to
    // remember which specific tool a given code change touched before Build/Build And Run: this
    // one always leaves the scene fully in sync with every UI setup script, whichever of them
    // actually changed.
    internal static class SetupAllUISetup
    {
        [MenuItem("Tools/Yobi/Setup All UI")]
        private static void SetupAllUIMenuItem()
        {
            RunAll();
        }

        // Also called by ConfigFilePostBuild's sibling UISetupPreBuild (IPreprocessBuildWithReport)
        // before every build, GUI or batchmode alike - a build triggered without remembering to
        // run this menu item first is exactly what left two stale "Live Status" sections stacked
        // in the scene (one from an already-superseded version of RoomUIPanelSetup) rendering side
        // by side after a code change nobody re-synced the scene against.
        internal static void RunAll()
        {
            RoomUIPanelSetup.SetupRoomUIPanel();
            SettingsModalUISetup.SetupSettingsModal();
            RoomBackgroundUISetup.SetupRoomBackground();
            CreatorSearchUISetup.SetupCreatorSearchUI();
            MainSearchBarUISetup.SetupMainSearchBar();

            Debug.Log("[SetupAllUISetup] Ran every Yobi UI setup tool and saved the scene.");
        }
    }
}
