using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Forces the render pipeline into a configuration that actually works in a mobile browser.
    ///
    /// This exists because of a concrete failure: with the stock template settings the Web
    /// build rendered nothing but world-space text, and the browser console was full of
    /// "glDrawElements: Mismatch between texture format and sampler type". URP's HDR path
    /// allocates float colour buffers that WebGL2 cannot sample the way the shaders expect, so
    /// every lit draw call was being dropped.
    ///
    /// Applied automatically from <see cref="TycoonBuild"/> so nobody has to remember it, and
    /// applied to every pipeline asset so it does not matter which quality level a platform
    /// happens to pick.
    /// </summary>
    public static class WebPlatformSetup
    {
        private static readonly string[] PipelineAssets =
        {
            "Assets/Settings/Mobile_RPAsset.asset",
            "Assets/Settings/PC_RPAsset.asset"
        };

        [MenuItem("Tycoon/Apply Web-Safe Render Settings")]
        public static void Apply()
        {
            foreach (string path in PipelineAssets)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null)
                {
                    Debug.LogWarning($"[WebPlatformSetup] Missing pipeline asset: {path}");
                    continue;
                }

                var so = new SerializedObject(asset);

                // The actual fix: no float colour buffer.
                SetBool(so, "m_SupportsHDR", false);

                // Neither is used by this game's rendering, and both cost an extra full-screen
                // pass plus a sampler that WebGL is picky about.
                SetBool(so, "m_RequireDepthTexture", false);
                SetBool(so, "m_RequireOpaqueTexture", false);

                // MSAA is stored as a sample count; 1 means off. Cheap win on a phone GPU,
                // and the flat-shaded look does not need it.
                SetInt(so, "m_MSAA", 1);

                // One hard-shadow cascade: enough to ground the buildings, nothing more.
                SetBool(so, "m_SoftShadowsSupported", false);
                SetInt(so, "m_ShadowCascadeCount", 1);
                SetFloat(so, "m_ShadowDistance", 40f);
                SetInt(so, "m_MainLightShadowmapResolution", 1024);

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                Debug.Log($"[WebPlatformSetup] Configured {path}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[WebPlatformSetup] SETUP_OK");

            if (Application.isBatchMode && !TycoonBuild.SuppressExit) EditorApplication.Exit(0);
        }

        private static void SetBool(SerializedObject so, string field, bool value)
        {
            var property = so.FindProperty(field);
            if (property == null) { Warn(field); return; }
            property.boolValue = value;
        }

        private static void SetInt(SerializedObject so, string field, int value)
        {
            var property = so.FindProperty(field);
            if (property == null) { Warn(field); return; }
            property.intValue = value;
        }

        private static void SetFloat(SerializedObject so, string field, float value)
        {
            var property = so.FindProperty(field);
            if (property == null) { Warn(field); return; }
            property.floatValue = value;
        }

        private static void Warn(string field) =>
            Debug.LogWarning($"[WebPlatformSetup] Field '{field}' not found; URP may have renamed it.");
    }
}
