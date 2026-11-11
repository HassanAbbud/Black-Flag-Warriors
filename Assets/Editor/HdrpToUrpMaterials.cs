// Place this file in any folder named "Editor" inside your Assets folder
// (e.g. Assets/Editor/HdrpToUrpMaterials.cs).
// Usage: in the Project window, select the folder containing the pink materials,
// then run Tools > Convert HDRP Materials to URP (Selected Folder).
// Back up or commit your project first.

using UnityEditor;
using UnityEngine;

public static class HdrpToUrpMaterials
{
    [MenuItem("Tools/Convert HDRP Materials to URP (Selected Folder)")]
    static void Convert()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("URP Lit shader not found. Is URP installed and active?");
            return;
        }

        string folder = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogError("Select a folder in the Project window first.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folder });
        int converted = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == urpLit) continue;

            // Read the old HDRP values from the saved material data
            // (works even though the HDRP shader is missing).
            var so = new SerializedObject(mat);
            Texture baseMap = GetTex(so, "_BaseColorMap") ?? GetTex(so, "_MainTex");
            Texture normal = GetTex(so, "_NormalMap") ?? GetTex(so, "_BumpMap");
            Texture mask = GetTex(so, "_MaskMap");
            Texture emissive = GetTex(so, "_EmissiveColorMap") ?? GetTex(so, "_EmissionMap");
            Color baseColor = GetColor(so, "_BaseColor", GetColor(so, "_Color", Color.white));
            Color emissiveColor = GetColor(so, "_EmissiveColor", Color.black);

            Undo.RecordObject(mat, "Convert HDRP material to URP");
            mat.shader = urpLit;

            mat.SetTexture("_BaseMap", baseMap);
            mat.SetColor("_BaseColor", baseColor);

            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }

            // HDRP mask map: R = metallic, G = AO, B = detail mask, A = smoothness.
            // URP reads metallic from R and smoothness from A, and AO from G,
            // so the same texture works for both slots.
            if (mask != null)
            {
                mat.SetTexture("_MetallicGlossMap", mask);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Smoothness", 1f);
                mat.SetTexture("_OcclusionMap", mask);
                mat.EnableKeyword("_OCCLUSIONMAP");
            }

            if (emissive != null || emissiveColor.maxColorComponent > 0.001f)
            {
                // HDRP emission is in physical units and is often very bright;
                // clamp it to a sane HDR range for URP.
                float max = emissiveColor.maxColorComponent;
                if (max > 4f) emissiveColor *= 4f / max;
                if (emissive != null && emissiveColor.maxColorComponent < 0.001f)
                    emissiveColor = Color.white;

                mat.SetTexture("_EmissionMap", emissive);
                mat.SetColor("_EmissionColor", emissiveColor);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }

            EditorUtility.SetDirty(mat);
            converted++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Converted {converted} material(s) in {folder} to URP/Lit.");
    }

    static Texture GetTex(SerializedObject so, string name)
    {
        SerializedProperty texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
        if (texEnvs == null) return null;
        for (int i = 0; i < texEnvs.arraySize; i++)
        {
            SerializedProperty entry = texEnvs.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == name)
                return entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
        }
        return null;
    }

    static Color GetColor(SerializedObject so, string name, Color fallback)
    {
        SerializedProperty colors = so.FindProperty("m_SavedProperties.m_Colors");
        if (colors == null) return fallback;
        for (int i = 0; i < colors.arraySize; i++)
        {
            SerializedProperty entry = colors.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == name)
                return entry.FindPropertyRelative("second").colorValue;
        }
        return fallback;
    }
}