using System.IO;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Import rules for everything in Art/Characters, applied automatically.
    ///
    /// The point is that dropping a re-exported FBX into the folder is the whole
    /// workflow - no clicking through the import inspector, and no way to forget a
    /// setting and get a character that will not animate. See the README beside the
    /// models.
    /// </summary>
    public class CharacterImportSettings : AssetPostprocessor
    {
        public const string Folder = "Assets/_Project/Art/Characters/";

        /// <summary>The one file that owns the skeleton and every animation clip.</summary>
        public const string RigAsset = Folder + "Villager_Rig.fbx";

        private const string MaterialFolder = "Assets/_Project/Materials/Characters";

        private bool InFolder => assetPath.Replace('\\', '/').StartsWith(Folder);

        private void OnPreprocessModel()
        {
            if (!InFolder) return;

            var importer = (ModelImporter)assetImporter;

            importer.globalScale = 1f;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.isReadable = false;

            // Flat shading is the whole look. Calculating normals at a one degree
            // threshold guarantees it regardless of what smoothing the exporter wrote.
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.normalSmoothingAngle = 1f;
            importer.importTangents = ModelImporterTangents.None;

            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;

            // Generic, not Humanoid: these proportions are stylised and the humanoid
            // retargeter would fight them. Every role shares one skeleton anyway, so
            // there is nothing to retarget between.
            importer.animationType = ModelImporterAnimationType.Generic;

            bool isRigSource = assetPath.Replace('\\', '/') == RigAsset;
            if (isRigSource)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            }
            else
            {
                // Role files carry a mesh and nothing else. Pointing them at the rig's
                // avatar is what lets one animator controller drive all of them.
                importer.importAnimation = false;

                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(RigAsset);
                if (avatar != null)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = avatar;
                }
                else
                {
                    // The rig has not imported yet - first pass over a fresh clone.
                    // CharacterLibrary reimports the roles once it exists.
                    importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                }
            }
        }

        /// <summary>
        /// Names the clips and makes them loop.
        ///
        /// Blender writes takes as "Rig|Walk", and nothing that looks up a clip by name
        /// would find it. Looping matters just as much: an idle or a walk cycle that
        /// plays once and stops is the kind of thing you only notice in game, standing
        /// there watching a worker freeze mid-stride.
        /// </summary>
        private void OnPreprocessAnimation()
        {
            if (!InFolder) return;
            if (Path.GetFileName(assetPath) != "Villager_Rig.fbx") return;

            var importer = (ModelImporter)assetImporter;
            var defaults = importer.defaultClipAnimations;
            var clips = new ModelImporterClipAnimation[defaults.Length];

            for (int i = 0; i < defaults.Length; i++)
            {
                var clip = defaults[i];
                int bar = clip.name.LastIndexOf('|');
                if (bar >= 0) clip.name = clip.name.Substring(bar + 1);
                clip.loopTime = true;
                clips[i] = clip;
            }

            importer.clipAnimations = clips;
        }

        /// <summary>
        /// Builds the character's materials as real URP assets rather than letting the
        /// FBX import decide.
        ///
        /// This project has been bitten before: a material that is not explicitly a URP
        /// material renders solid magenta in the Web build. Creating them here also means
        /// recolouring a role is editing a material asset, not re-exporting from Blender.
        /// </summary>
        private Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            if (!InFolder || material == null) return null;

            Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/{material.name}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var created = new Material(shader) { name = material.name };

            Color colour = material.HasProperty("_Color") ? material.color : Color.white;
            if (created.HasProperty("_BaseColor")) created.SetColor("_BaseColor", colour);
            if (created.HasProperty("_Color")) created.SetColor("_Color", colour);
            if (created.HasProperty("_Smoothness")) created.SetFloat("_Smoothness", 0.08f);

            AssetDatabase.CreateAsset(created, path);
            return created;
        }
    }
}
