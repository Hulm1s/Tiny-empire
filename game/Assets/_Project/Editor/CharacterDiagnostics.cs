using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Dumps what the animation setup actually contains.
    ///
    /// A clip that animates nothing, and a clip whose curves bind to bone paths the mesh
    /// does not have, look identical in game: the character stands there. This prints the
    /// difference.
    /// </summary>
    public static class CharacterDiagnostics
    {
        [MenuItem("Tycoon/Diagnose Character Animation")]
        public static void Run()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/_Project/Animation/Villager.controller");

            if (controller == null)
            {
                Debug.LogError("[Diag] No controller asset.");
            }
            else
            {
                var machine = controller.layers[0].stateMachine;
                Debug.Log($"[Diag] controller default state = " +
                          $"'{machine.defaultState?.name ?? "NONE"}'");

                foreach (var child in machine.states)
                {
                    var motion = child.state.motion;
                    Debug.Log($"[Diag] state '{child.state.name}' motion=" +
                              $"'{(motion != null ? motion.name : "NULL")}' " +
                              $"writeDefaults={child.state.writeDefaultValues}");
                }

                foreach (var t in machine.anyStateTransitions)
                {
                    var conditions = new List<string>();
                    foreach (var c in t.conditions)
                        conditions.Add($"{c.parameter} {c.mode} {c.threshold}");
                    Debug.Log($"[Diag] anyState -> '{t.destinationState?.name}' " +
                              $"when [{string.Join(", ", conditions)}]");
                }
            }

            // Which bones each clip actually drives.
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(CharacterImportSettings.RigAsset))
            {
                if (!(asset is AnimationClip clip)) continue;
                if (clip.name.StartsWith("__preview__")) continue;

                var bones = new HashSet<string>();
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    string path = binding.path;
                    int slash = path.LastIndexOf('/');
                    bones.Add(slash >= 0 ? path.Substring(slash + 1) : path);
                }

                bool legs = bones.Contains("Thigh.L") || bones.Contains("Thigh_L");
                Debug.Log($"[Diag] clip '{clip.name}' len={clip.length:0.00}s loop={clip.isLooping} " +
                          $"curves={AnimationUtility.GetCurveBindings(clip).Length} " +
                          $"bones={bones.Count} drivesLegs={legs}");
                Debug.Log($"[Diag]   bones: {string.Join(", ", bones)}");
            }

            // And whether a role mesh has those bones under the same paths.
            string rolePath = CharacterImportSettings.Folder + "Villager_Farmer.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(rolePath);
            if (model != null)
            {
                var names = new List<string>();
                foreach (var t in model.GetComponentsInChildren<Transform>())
                    names.Add(t.name);
                Debug.Log($"[Diag] Farmer hierarchy ({names.Count}): {string.Join(", ", names)}");
            }

            Debug.Log("DIAG_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
