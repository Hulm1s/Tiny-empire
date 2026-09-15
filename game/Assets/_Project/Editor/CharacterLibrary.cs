using System.IO;
using Tycoon.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Puts villager models into a level.
    ///
    /// One animator controller and one avatar serve every role, so a new role is a
    /// mesh and a name - nothing else. See Art/Characters/README.md.
    /// </summary>
    public static class CharacterLibrary
    {
        public const string Owner = "Owner";
        public const string Farmer = "Farmer";
        public const string Cashier = "Cashier";
        public const string Customer = "Customer";

        private const string ControllerPath = "Assets/_Project/Animation/Villager.controller";

        /// <summary>
        /// Material slot order on every villager mesh, shared by all roles.
        /// Headwear occupies 4 and 5 on the roles that have it.
        /// </summary>
        public const int ShoeSlot = 0, TrouserSlot = 1, ShirtSlot = 2, SkinSlot = 3;

        /// <summary>
        /// Where a carried stack sits. Workers and the player hold goods in front of
        /// them at chest height, matching the carry animation - the stack used to
        /// float above everyone's head, which read as a bug once the models had hands.
        /// </summary>
        public static readonly Vector3 HandAnchor = new Vector3(0f, 0.86f, 0.42f);

        public static GameObject Model(string role)
        {
            string path = $"{CharacterImportSettings.Folder}Villager_{role}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) Debug.LogError($"[CharacterLibrary] Missing model: {path}");
            return model;
        }

        /// <summary>
        /// Adds a villager under <paramref name="parent"/> and returns its root.
        ///
        /// The model is instantiated as a prefab instance, so re-exporting the FBX
        /// updates every character already placed in the scene.
        /// </summary>
        public static Transform Spawn(string role, Transform parent, string name = null)
        {
            var model = Model(role);
            if (model == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = name ?? role;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // Not "?? AddComponent": a missing Unity component compares equal to null through
            // the overloaded operator but is not actually null, so ?? hands back the dead one.
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = EnsureController();
            animator.applyRootMotion = false;
            // Always animate. Culling depends on the skinned renderer's bounds being right,
            // and when they are not the character simply freezes - a saving worth far less
            // than a farm full of statues. There are about a dozen villagers at most.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            instance.AddComponent<CharacterVisual>();

            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = false;
                // Nothing on a villager needs per-vertex skinning quality at this size.
                renderer.quality = SkinQuality.Bone2;
                // Recompute bounds from the pose; the bind-pose bounds are wrong once a
                // character raises its arms, and wrong bounds make it vanish at the edges.
                renderer.updateWhenOffscreen = true;
            }

            return instance.transform;
        }

        /// <summary>
        /// Recolours one material slot on a spawned villager, for per-character variety.
        /// Slots are Shoe, Trouser, Shirt, Skin, then any headwear.
        /// </summary>
        public static void Tint(Transform villager, int slot, Color colour)
        {
            var renderer = villager.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer == null || slot >= renderer.sharedMaterials.Length) return;

            var materials = renderer.sharedMaterials;
            var copy = new Material(materials[slot]);
            if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", colour);
            if (copy.HasProperty("_Color")) copy.SetColor("_Color", colour);
            materials[slot] = copy;
            renderer.sharedMaterials = materials;
        }

        /// <summary>
        /// Builds the shared animator: four states, chosen by one integer.
        ///
        /// An integer rather than speed floats and bools, because the states are
        /// genuinely discrete and CharacterVisual already decides which one applies.
        /// Any-state transitions mean a character can switch from whatever it was doing
        /// without a web of pairwise transitions to maintain.
        /// </summary>
        public static AnimatorController EnsureController()
        {
            // Rebuilt every time rather than created once and trusted.
            //
            // The first version returned early whenever the asset already existed. It had
            // been built before the importer learned to strip the "Rig|" prefix off clip
            // names, so every state was wired to a clip that could not be found - and being
            // cached, it stayed that way. States with no motion animate nothing, which looks
            // exactly like a broken rig.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            bool hasState = false;
            foreach (var parameter in controller.parameters)
                if (parameter.name == "State") hasState = true;
            if (!hasState) controller.AddParameter("State", AnimatorControllerParameterType.Int);

            var machine = controller.layers[0].stateMachine;

            foreach (var transition in machine.anyStateTransitions)
                machine.RemoveAnyStateTransition(transition);
            foreach (var child in machine.states)
                machine.RemoveState(child.state);

            string[] clips = { "Idle", "Walk", "CarryIdle", "CarryWalk" };
            int wired = 0;

            for (int i = 0; i < clips.Length; i++)
            {
                var clip = FindClip(clips[i]);
                if (clip != null) wired++;

                var state = machine.AddState(clips[i]);
                state.motion = clip;
                state.writeDefaultValues = false;

                if (i == 0) machine.defaultState = state;

                var transition = machine.AddAnyStateTransition(state);
                transition.AddCondition(AnimatorConditionMode.Equals, i, "State");
                transition.hasExitTime = false;
                transition.duration = 0.12f;
                // Without this a state re-enters itself every time the parameter is set.
                transition.canTransitionToSelf = false;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            if (wired < clips.Length)
                Debug.LogError($"[CharacterLibrary] Only {wired}/{clips.Length} states got a " +
                               $"clip. Characters will stand still. Re-export the rig from Blender.");
            else
                Debug.Log($"[CharacterLibrary] Animator rebuilt: {wired} states wired.");

            return controller;
        }

        private static AnimationClip FindClip(string clipName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(CharacterImportSettings.RigAsset))
            {
                if (asset is AnimationClip clip && clip.name == clipName) return clip;
            }

            Debug.LogWarning($"[CharacterLibrary] Clip '{clipName}' not found in " +
                             $"{CharacterImportSettings.RigAsset}. Re-export from Blender?");
            return null;
        }

        /// <summary>
        /// Forces the role models to pick up the rig's avatar.
        ///
        /// On a fresh clone the roles can import before the rig does, leaving them with
        /// no avatar and therefore no animation. Cheap to just reimport them once at the
        /// start of a scene build rather than leave it to import order.
        /// </summary>
        [MenuItem("Tycoon/Reimport Character Models")]
        public static void ReimportRoles()
        {
            AssetDatabase.ImportAsset(CharacterImportSettings.RigAsset,
                ImportAssetOptions.ForceUpdate);

            foreach (string role in new[] { Owner, Farmer, Cashier, Customer })
            {
                string path = $"{CharacterImportSettings.Folder}Villager_{role}.fbx";
                if (File.Exists(path))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            EnsureController();
            Debug.Log("[CharacterLibrary] Characters reimported and controller rebuilt.");
        }

        /// <summary>
        /// Prints what actually imported. A role with no avatar imports without error
        /// and then simply never animates, which is invisible until you are in game
        /// wondering why nobody moves.
        /// </summary>
        [MenuItem("Tycoon/Verify Character Models")]
        public static void Verify()
        {
            ReimportRoles();

            var rigAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(CharacterImportSettings.RigAsset);
            Debug.Log($"[Verify] rig avatar={(rigAvatar != null ? rigAvatar.name : "MISSING")} " +
                      $"valid={(rigAvatar != null && rigAvatar.isValid)}");

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(CharacterImportSettings.RigAsset))
                if (asset is AnimationClip clip)
                    Debug.Log($"[Verify] clip '{clip.name}' length={clip.length:0.00}s loop={clip.isLooping}");

            foreach (string role in new[] { Owner, Farmer, Cashier, Customer })
            {
                string path = $"{CharacterImportSettings.Folder}Villager_{role}.fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var skin = model != null ? model.GetComponentInChildren<SkinnedMeshRenderer>() : null;

                Debug.Log($"[Verify] {role}: avatarSetup={importer?.avatarSetup} " +
                          $"source={(importer?.sourceAvatar != null ? "set" : "NONE")} " +
                          $"tris={(skin != null ? skin.sharedMesh.triangles.Length / 3 : -1)} " +
                          $"materials={(skin != null ? skin.sharedMaterials.Length : -1)} " +
                          $"bones={(skin != null ? skin.bones.Length : -1)}");
            }

            var controller = EnsureController();
            Debug.Log($"[Verify] controller states=" +
                      $"{controller.layers[0].stateMachine.states.Length}");

            Debug.Log("VERIFY_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
