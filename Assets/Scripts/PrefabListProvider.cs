using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PrefabListProvider : MonoBehaviour
{
    public string GetPrefabListForPrompt()
    {
        GameObject[] loadedPrefabs = Resources.LoadAll<GameObject>("Prefabs");

        List<string> prefabPaths = loadedPrefabs
            .Select(prefab => $"Prefabs/{prefab.name}")
            .ToList();

        string prefabBlock = "Prefabs: " + string.Join(", ", prefabPaths);
        return prefabBlock;
    }
}