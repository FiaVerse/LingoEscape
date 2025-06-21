// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Rendering;
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Nova.Editor
{
    /// <summary>
    /// Ensures that changes made to textures get handled by Nova
    /// </summary>
    internal class TexturePostProcessor : AssetPostprocessor, IActiveBuildTargetChanged
    {
        public int callbackOrder => 0;

        public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
        {
            if (!Internal.NovaSettings.Initialized || !Internal.NovaSettings.PackedImagesEnabled)
            {
                return;
            }

            RenderingDataStore.Instance.ImageTracker.EditorOnly_HandleBuildTargetChanged();
        }

        [System.Reflection.Obfuscation]
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (RenderEngine.Instance == null)
            {
                return;
            }

            bool textureReprocessed = false;
            bool textureDescriptionChanged = false;
            for (int i = 0; i < importedAssets.Length; i++)
            {
                string path = importedAssets[i];

                Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (type == null || !type.IsSubclassOf(typeof(Texture)))
                {
                    // Not a texture
                    continue;
                }

                var tex = AssetDatabase.LoadAssetAtPath<Texture>(importedAssets[i]);
                textureReprocessed = true;
                if (RenderingDataStore.Instance.ImageTracker.EditorOnly_TryUpdateTextureDescriptor(tex))
                {
                    textureDescriptionChanged = true;
                }

                if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
                {
                    continue;
                }

                // Texture has sprites, so reprocess those
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path);

                if (RenderingDataStore.Instance.ImageTracker.EditorOnly_TryUpdateSprites(tex, sprites))
                {
                    textureDescriptionChanged = true;
                }
            }

            if (textureDescriptionChanged)
            {
                // At least one texture description changed, so dirty everything
                RenderEngine.Instance.DirtyEverything();
                NovaApplication.QueueEditorPlayerLoop();
            }
            else if (textureReprocessed)
            {
                // No descriptions changed, but texture contents may have changed
                RenderEngine.Instance.EditorOnly_TexturesHaveBeenReprocessed = true;
                NovaApplication.QueueEditorPlayerLoop();
            }
        }
    }
}

