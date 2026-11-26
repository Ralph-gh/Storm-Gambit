using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WindowsCrossplayBuild
{
    [MenuItem("Tools/Storm Gambit/Build Windows x64 Player")]
    public static void Build()
    {
        const string outputDirectory = "Builds/Windows-x64";
        string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
            .Select(scene => scene.path).ToArray();
        Directory.CreateDirectory(outputDirectory);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            target = BuildTarget.StandaloneWindows64,
            subtarget = (int)StandaloneBuildSubtarget.Player,
            locationPathName = outputDirectory + "/StormGambit.exe",
            options = BuildOptions.StrictMode
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"Windows build {report.summary.result} ({report.summary.totalErrors} errors).");

        Debug.Log($"Windows player built at {report.summary.outputPath}");
    }
}
