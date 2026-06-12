using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-shot Phase 1 migration: creates the URP pipeline/renderer assets, assigns them in
/// Graphics + every Quality level, and converts legacy-shader materials to URP/Lit.
/// Run headless:
///   Unity.exe -batchmode -quit -projectPath &lt;repo&gt; -executeMethod URPMigrationTool.Migrate
/// Idempotent: re-running overwrites the same assets and skips already-converted materials.
/// </summary>
public static class URPMigrationTool
{
    private const string SettingsFolder = "Assets/Settings";
    private const string PipelineAssetPath = SettingsFolder + "/URP-Asset.asset";
    private const string RendererDataPath = SettingsFolder + "/URP-Renderer.asset";

    public static void Migrate()
    {
        if (!AssetDatabase.IsValidFolder(SettingsFolder))
            AssetDatabase.CreateFolder("Assets", "Settings");

        UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererDataPath);
        }

        UniversalRenderPipelineAsset pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
        if (pipelineAsset == null)
        {
            pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
        }

        GraphicsSettings.defaultRenderPipeline = pipelineAsset;

        int originalQualityLevel = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipelineAsset;
        }
        QualitySettings.SetQualityLevel(originalQualityLevel, false);

        int converted = ConvertLegacyMaterials();

        AssetDatabase.SaveAssets();
        Debug.Log($"[URPMigrationTool] URP assets assigned; {converted} material(s) converted to URP/Lit.");
    }

    private static int ConvertLegacyMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("[URPMigrationTool] URP/Lit shader not found — is the URP package installed?");
            return 0;
        }

        int converted = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == null)
                continue;

            string shaderName = material.shader.name;
            bool isLegacy = shaderName == "Standard" ||
                            shaderName.StartsWith("Legacy Shaders/") ||
                            shaderName == "Hidden/InternalErrorShader";
            if (!isLegacy)
                continue;

            Color color = material.HasProperty("_Color") ? material.color : Color.white;
            Texture mainTexture = material.HasProperty("_MainTex") ? material.mainTexture : null;

            material.shader = urpLit;
            material.SetColor("_BaseColor", color);
            if (mainTexture != null)
                material.SetTexture("_BaseMap", mainTexture);

            EditorUtility.SetDirty(material);
            converted++;
            Debug.Log($"[URPMigrationTool] Converted '{path}' ({shaderName} -> URP/Lit).");
        }

        return converted;
    }
}
