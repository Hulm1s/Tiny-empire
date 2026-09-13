using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Headless build entry points.
    ///
    /// Everything about the Web build is configured here in code rather than left in the
    /// project settings window, so a build produced on any machine - or in CI later - is
    /// identical, and so the settings that actually matter for iPhone Safari are written down
    /// with the reasons attached.
    /// </summary>
    public static class TycoonBuild
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Farm.unity";
        private const string WebTemplate = "PROJECT:MobilePWA";

        /// <summary>
        /// Set while the build calls into other batch-mode entry points, so they configure
        /// themselves without calling EditorApplication.Exit and killing the build.
        /// </summary>
        internal static bool SuppressExit;

        /// <summary>Output folder: the repo's docs/ directory, which GitHub Pages serves directly.</summary>
        private static string OutputPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs"));

        [MenuItem("Tycoon/Build Web %#b")]
        public static void BuildWeb()
        {
            int exitCode = RunWebBuild();
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// Called with -executeMethod to confirm the editor opens, scripts compile and the Web
        /// module is actually installed, without paying for a full build.
        /// </summary>
        public static void Validate()
        {
            bool supported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            Debug.Log($"[TycoonBuild] Unity {Application.unityVersion}");
            Debug.Log($"[TycoonBuild] Web build support installed: {supported}");
            Debug.Log($"[TycoonBuild] Output path: {OutputPath}");
            Debug.Log($"[TycoonBuild] Scenes: {string.Join(", ", CollectScenes())}");
            Debug.Log("[TycoonBuild] VALIDATE_OK");

            if (Application.isBatchMode) EditorApplication.Exit(supported ? 0 : 2);
        }

        private static int RunWebBuild()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError("[TycoonBuild] Web build support is not installed for this editor.");
                return 2;
            }

            string[] scenes = CollectScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError($"[TycoonBuild] No scenes to build. Expected '{MainScenePath}'.");
                return 3;
            }

            ApplyPlayerSettings();

            Directory.CreateDirectory(OutputPath);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[TycoonBuild] Result: {summary.result}");
            Debug.Log($"[TycoonBuild] Size: {summary.totalSize / (1024f * 1024f):F1} MB");
            Debug.Log($"[TycoonBuild] Duration: {summary.totalTime.TotalSeconds:F0}s");

            if (summary.result != BuildResult.Succeeded)
            {
                foreach (var step in report.steps)
                    foreach (var message in step.messages)
                        if (message.type == LogType.Error || message.type == LogType.Exception)
                            Debug.LogError($"[TycoonBuild] {step.name}: {message.content}");

                return 1;
            }

            Debug.Log("[TycoonBuild] BUILD_OK");
            return 0;
        }

        private static string[] CollectScenes()
        {
            // Prefer whatever is configured in Build Settings, but fall back to the known main
            // scene so a fresh clone builds without anyone opening the editor first.
            var configured = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path) && File.Exists(s.path))
                .Select(s => s.path)
                .ToArray();

            if (configured.Length > 0) return configured;

            return File.Exists(MainScenePath) ? new[] { MainScenePath } : Array.Empty<string>();
        }

        private static void ApplyPlayerSettings()
        {
            // Render pipeline first: without this the Web build draws no lit geometry at all.
            SuppressExit = true;
            try { WebPlatformSetup.Apply(); }
            finally { SuppressExit = false; }

            PlayerSettings.companyName = "Home Made";
            PlayerSettings.productName = "Tiny Empire";

            // The Build/ filenames never change between builds, so both Unity's own data cache
            // and the service worker key on the product version instead. Stamping it per build
            // is what stops a browser mixing a new .wasm with a stale .data - which shows up as
            // a black screen and shader errors, and cost an hour to track down once already.
            PlayerSettings.bundleVersion = System.DateTime.UtcNow.ToString("yyyy.MM.dd.HHmm");

            // Portrait only: the whole HUD layout assumes a one-handed phone grip.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.runInBackground = false;

            if (TemplateExists()) PlayerSettings.WebGL.template = WebTemplate;
            else Debug.LogWarning($"[TycoonBuild] Template '{WebTemplate}' not found, using default.");

            // GitHub Pages is a plain static host and cannot send a Content-Encoding header.
            // Gzip plus the decompression fallback is the combination that loads correctly
            // there; Brotli without headers produces a silent blank page.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            // Keeps the build in the browser cache between visits, so only the first load is slow.
            PlayerSettings.WebGL.dataCaching = true;

            // Full exception support roughly doubles code size and costs frame time. Explicit
            // throws are all the game itself needs.
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;

            PlayerSettings.SetManagedStrippingLevel(
                UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.High);

            PlayerSettings.SetIl2CppCompilerConfiguration(
                UnityEditor.Build.NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Master);

            EnsureSceneRegistered();
        }

        private static bool TemplateExists()
        {
            string name = WebTemplate.Substring("PROJECT:".Length);
            return Directory.Exists(Path.Combine(Application.dataPath, "WebGLTemplates", name));
        }

        private static void EnsureSceneRegistered()
        {
            if (!File.Exists(MainScenePath)) return;
            if (EditorBuildSettings.scenes.Any(s => s.path == MainScenePath && s.enabled)) return;

            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
            {
                new EditorBuildSettingsScene(MainScenePath, true)
            };
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
