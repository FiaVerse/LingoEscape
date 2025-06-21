// Copyright (c) Supernova Technologies LLC
using Nova;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor
{
    internal class ItemViewPostProcessor : AssetPostprocessor
    {
        private static HashSet<string> prefabsToReimport = new HashSet<string>();
        private const string ItemViewPrefabReloadEditorPref = "Nova.ItemViewPrefabReload";
        private const string Separator = "%";

        [InitializeOnLoadMethod]
        private static void Init()
        {
            if (!EditorPrefs.HasKey(ItemViewPrefabReloadEditorPref))
            {
                return;
            }

            AssemblyReloadEvents.afterAssemblyReload += ReimportBustedPrefabs;
        }

        private void OnPostprocessPrefab(GameObject gameObject)
        {
            if (gameObject == null || !EditorApplication.isCompiling || prefabsToReimport.Contains(assetPath))
            {
                return;
            }

            ItemView[] itemViews = gameObject.GetComponentsInChildren<ItemView>(includeInactive: true);

            if (itemViews.Any(x => PrefabUtility.GetNearestPrefabInstanceRoot(x) == null && x.Visuals == null))
            {
                prefabsToReimport.Add(assetPath);
            }
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (prefabsToReimport.Count == 0)
            {
                return;
            }

            // This static method typically will only be called once per
            // asset post process, but in theory it can be called multiple times,
            // so we remove the subscription just to ensure a single subscribe.
            AssemblyReloadEvents.beforeAssemblyReload -= StashBustedPrefabPaths;
            AssemblyReloadEvents.beforeAssemblyReload += StashBustedPrefabPaths;
        }

        private static void StashBustedPrefabPaths()
        {
            string combined = string.Join(Separator, prefabsToReimport);

            EditorPrefs.SetString(ItemViewPrefabReloadEditorPref, combined);

            AssemblyReloadEvents.beforeAssemblyReload -= StashBustedPrefabPaths;

            prefabsToReimport.Clear();
        }

        private static void ReimportBustedPrefabs()
        {
            string combined = EditorPrefs.GetString(ItemViewPrefabReloadEditorPref, string.Empty);

            if (!string.IsNullOrWhiteSpace(combined))
            {
                string[] prefabs = combined.Split(Separator.ToCharArray());

                foreach (string prefabPath in prefabs)
                {
                    AssetDatabase.ImportAsset(prefabPath);
                }
            }

            EditorPrefs.DeleteKey(ItemViewPrefabReloadEditorPref);

            AssemblyReloadEvents.afterAssemblyReload -= ReimportBustedPrefabs;
        }
    }
}

