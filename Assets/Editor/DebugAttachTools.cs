using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

// Helps keep Unity editor in a debugger-friendly state for VS Code attach.
[InitializeOnLoad]
public static class DebugAttachTools
{
    static DebugAttachTools()
    {
        try
        {
            if (CompilationPipeline.codeOptimization != CodeOptimization.Debug)
            {
                CompilationPipeline.codeOptimization = CodeOptimization.Debug;
                Debug.Log("[DebugAttachTools] Code Optimization set to Debug for editor attach.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[DebugAttachTools] Unable to set code optimization mode: {ex.Message}");
        }
    }

    [MenuItem("Tools/Debug/Set Code Optimization/Debug")]
    private static void SetDebugMode()
    {
        CompilationPipeline.codeOptimization = CodeOptimization.Debug;
        Debug.Log("[DebugAttachTools] Code Optimization -> Debug");
    }

    [MenuItem("Tools/Debug/Set Code Optimization/Release")]
    private static void SetReleaseMode()
    {
        CompilationPipeline.codeOptimization = CodeOptimization.Release;
        Debug.Log("[DebugAttachTools] Code Optimization -> Release");
    }

    [MenuItem("Tools/Debug/Log Attach Diagnostics")]
    private static void LogAttachDiagnostics()
    {
        Debug.Log($"[DebugAttachTools] codeOptimization={CompilationPipeline.codeOptimization}, isPlaying={EditorApplication.isPlaying}, isPaused={EditorApplication.isPaused}");
    }
}
