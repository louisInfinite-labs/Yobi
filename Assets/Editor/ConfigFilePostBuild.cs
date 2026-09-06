using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Yobi.Editor
{
    // config.local.json (API keys) is gitignored and only ever lives at the project root - a
    // built player looks for it next to its own executable (LocalFileChannelConfigProvider),
    // which nothing else puts there. [PostProcessBuild] fires after every player build regardless
    // of how it was triggered - the Build Profiles window's Build/Build And Run buttons, or
    // BuildScript's batchmode path alike - so this is the one place that needs to know about the
    // copy, instead of every build path remembering to do it (or someone doing it by hand).
    internal static class ConfigFilePostBuild
    {
        private const string ConfigFileName = "config.local.json";

        [PostProcessBuild]
        private static void CopyConfigFileNextToBuild(BuildTarget target, string pathToBuiltProject)
        {
            // Fully qualified: Yobi.Editor sits under the Yobi namespace alongside Yobi.Application
            // (the Clean Architecture layer), which would otherwise shadow UnityEngine.Application.
            var projectRootConfigPath = Path.Combine(UnityEngine.Application.dataPath, "..", ConfigFileName);
            if (!File.Exists(projectRootConfigPath))
            {
                Debug.LogWarning($"[ConfigFilePostBuild] {ConfigFileName} not found at project root ({projectRootConfigPath}) - skipping copy. The build will fail to load API keys until this file exists.");
                return;
            }

            // Never overwrite a good copy with an empty one - a prior version of this same-path
            // guard below missed a case-insensitive match once and File.Copy truncated the actual
            // project-root file to 0 bytes (wiping the real API keys) before throwing. Refusing to
            // propagate an empty source anywhere is a second, independent line of defense against
            // that same class of failure recurring in some other guise.
            if (new FileInfo(projectRootConfigPath).Length == 0)
            {
                Debug.LogError($"[ConfigFilePostBuild] {ConfigFileName} at {projectRootConfigPath} is empty - refusing to copy it over a build output. Restore your API keys into it.");
                return;
            }

            // pathToBuiltProject is the .app on macOS and the .exe on Windows - either way its
            // containing folder is where the file needs to sit alongside the executable.
            var buildOutputFolder = Path.GetDirectoryName(pathToBuiltProject);
            if (string.IsNullOrEmpty(buildOutputFolder))
            {
                return;
            }

            var destinationPath = Path.Combine(buildOutputFolder, ConfigFileName);

            // Building straight into the project root (Build Profiles remembers the last output
            // folder picked, and the project root is a common first choice) puts the .app right
            // next to config.local.json already - source and destination are then the same file,
            // and File.Copy(overwrite: true) onto a file's own path throws (surfaces as an
            // IOException "Sharing violation", not an "already exists" error) - and, worse, a
            // partial write before that throw is what actually truncated config.local.json to 0
            // bytes here once already. OrdinalIgnoreCase because both macOS (APFS/HFS+) and
            // Windows default to case-insensitive filesystems, where two paths differing only in
            // case can still be the same file.
            if (string.Equals(Path.GetFullPath(projectRootConfigPath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[ConfigFilePostBuild] Build output folder is the project root - {ConfigFileName} is already there.");
                return;
            }

            File.Copy(projectRootConfigPath, destinationPath, overwrite: true);
            Debug.Log($"[ConfigFilePostBuild] Copied {ConfigFileName} to {destinationPath}");
        }
    }
}
