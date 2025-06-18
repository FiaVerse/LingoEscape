// CluePlacer.cs (with extensive debugging)
// Automatically places a prefab on a random wall from the MRUK scene scan.

using UnityEngine;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;

public class CluePlacer : MonoBehaviour
{
    [Header("Prefab Settings")]
    [Tooltip("The HiddenClue prefab that will be placed on a wall.")]
    [SerializeField]
    private GameObject m_hiddenCluePrefab;

    [Header("Placement Settings")]
    [Tooltip("Small offset from the wall surface to prevent visual flickering (Z-fighting).")]
    [SerializeField]
    private float m_surfaceOffset = 0.01f;

    void OnEnable()
    {
        // --- DEBUG LOG ---
        Debug.Log("CluePlacer.OnEnable(): Script is active. Attempting to subscribe to SceneLoadedEvent.");
        
        if (MRUK.Instance != null)
        {
            MRUK.Instance.SceneLoadedEvent.AddListener(PlaceClueOnWall);
        }
        else
        {
            // --- DEBUG LOG ---
            Debug.LogError("CluePlacer.OnEnable(): MRUK.Instance is NULL. The script cannot subscribe to the scene loaded event. Ensure an MRUK object is active in the scene.", this);
        }
    }

    void OnDisable()
    {
        if (MRUK.Instance != null)
        {
            MRUK.Instance.SceneLoadedEvent.RemoveListener(PlaceClueOnWall);
        }
    }

    private void PlaceClueOnWall()
    {
        // --- DEBUG LOG ---
        Debug.Log("--- CluePlacer.PlaceClueOnWall(): Triggered by SceneLoadedEvent ---");

        if (m_hiddenCluePrefab == null)
        {
            // --- DEBUG LOG ---
            Debug.LogError("CluePlacer FAILURE: The 'Hidden Clue Prefab' has not been assigned in the Inspector!", this);
            return;
        }

        // --- DEBUG LOG ---
        Debug.Log("CluePlacer STEP 1: Attempting to get the current room...");
        var room = MRUK.Instance.GetCurrentRoom();
        if (room == null)
        {
            // --- DEBUG LOG ---
            Debug.LogError("CluePlacer FAILURE: MRUK.Instance.GetCurrentRoom() returned null. This can happen if the headset is not inside a scanned room's bounds.", this);
            return;
        }
        
        // --- DEBUG LOG ---
        Debug.Log($"CluePlacer STEP 2: Successfully found room '{room.name}'. Searching for wall anchors among {room.Anchors.Count} total anchors...");
        
        List<MRUKAnchor> wallAnchors = new List<MRUKAnchor>();
        foreach (var anchor in room.Anchors)
        {
            if (anchor.HasLabel(OVRSceneManager.Classification.WallFace))
            {
                // --- DEBUG LOG ---
                Debug.Log($"  - Found a WALL_FACE: '{anchor.name}'");
                wallAnchors.Add(anchor);
            }
        }
        
        // --- DEBUG LOG ---
        Debug.Log($"CluePlacer STEP 3: Search complete. Found {wallAnchors.Count} wall(s).");

        if (wallAnchors.Count == 0)
        {
            // --- DEBUG LOG ---
            Debug.LogWarning("CluePlacer FAILURE: No anchors with the 'WALL_FACE' label were found in the current room. Ensure walls were scanned and labeled in Space Setup.", this);
            return;
        }

        // --- DEBUG LOG ---
        Debug.Log("CluePlacer STEP 4: Selecting a random wall for placement...");
        MRUKAnchor targetWall = wallAnchors[Random.Range(0, wallAnchors.Count)];

        Transform wallTransform = targetWall.transform;
        
        // --- DEBUG LOG ---
        Debug.Log($"CluePlacer STEP 5: Selected wall is '{targetWall.name}'. Calculating position and rotation...");

        Vector3 position = wallTransform.position + (wallTransform.forward * m_surfaceOffset);
        Quaternion rotation = Quaternion.LookRotation(-wallTransform.forward, wallTransform.up);

        GameObject clueInstance = Instantiate(m_hiddenCluePrefab, position, rotation);
        clueInstance.transform.SetParent(wallTransform);

        // --- DEBUG LOG ---
        Debug.Log($"CluePlacer SUCCESS: Placed hidden clue on wall '{targetWall.name}'!", clueInstance);
    }
}