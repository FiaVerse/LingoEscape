// FlashlightPlacer.cs
// Automatically places a flashlight prefab on a suitable surface (like a table or floor)
// after the MRUK scene has finished loading.

using UnityEngine;
using Meta.XR.MRUtilityKit;
using System.Collections.Generic; // Required for LabelFilter

public class FlashlightPlacer : MonoBehaviour
{
    [Header("Prefab Settings")]
    [Tooltip("The flashlight prefab that will be placed in the scene.")]
    [SerializeField]
    private GameObject m_flashlightPrefab;

    [Header("Placement Settings")]
    [Tooltip("How high above the target surface the flashlight should spawn.")]
    [SerializeField]
    private float m_surfaceOffset = 0.02f;

    void OnEnable()
    {
        if (MRUK.Instance != null)
        {
            MRUK.Instance.SceneLoadedEvent.AddListener(PlaceFlashlightInRoom);
        }
        else
        {
            Debug.LogError("FlashlightPlacer: MRUK.Instance is NULL. Cannot subscribe to SceneLoadedEvent.", this);
        }
    }

    void OnDisable()
    {
        if (MRUK.Instance != null)
        {
            MRUK.Instance.SceneLoadedEvent.RemoveListener(PlaceFlashlightInRoom);
        }
    }

    private void PlaceFlashlightInRoom()
    {
        if (m_flashlightPrefab == null)
        {
            Debug.LogError("FlashlightPlacer: Flashlight Prefab is not assigned in the Inspector!", this);
            return;
        }

        MRUKRoom currentRoom = MRUK.Instance.GetCurrentRoom();
        if (currentRoom == null)
        {
            Debug.LogError("FlashlightPlacer: No current room found. Cannot place flashlight.", this);
            return;
        }

        Vector3 spawnPosition;
        bool positionFound = false;

        // --- Primary Placement: Try to find a table ---
        // CORRECTED: Use the SceneLabels enum directly from your MRUKAnchor script.
        LabelFilter tableFilter = new LabelFilter(MRUKAnchor.SceneLabels.TABLE);
        if (currentRoom.GenerateRandomPositionOnSurface(MRUK.SurfaceType.FACING_UP, 0.1f, tableFilter, out spawnPosition, out _))
        {
            positionFound = true;
            Debug.Log("FlashlightPlacer: Found a table. Spawning flashlight there.");
        }
        // --- Fallback Placement: If no table, try to find the floor ---
        else
        {
            // CORRECTED: Use the SceneLabels enum directly from your MRUKAnchor script.
            LabelFilter floorFilter = new LabelFilter(MRUKAnchor.SceneLabels.FLOOR);
            if (currentRoom.GenerateRandomPositionOnSurface(MRUK.SurfaceType.FACING_UP, 0.2f, floorFilter, out spawnPosition, out _))
            {
                positionFound = true;
                Debug.Log("FlashlightPlacer: No table found. Spawning flashlight on the floor instead.");
            }
        }
        
        if (positionFound)
        {
            // Adjust the spawn position slightly upwards from the surface.
            Vector3 finalPosition = spawnPosition + Vector3.up * m_surfaceOffset;

            // Instantiate the flashlight with an upright rotation.
            Instantiate(m_flashlightPrefab, finalPosition, Quaternion.identity);
            Debug.Log($"FlashlightPlacer: Successfully spawned flashlight prefab at {finalPosition}");
        }
        else
        {
            // --- Final Fallback: If no surfaces are found, spawn in front of the player ---
            Debug.LogWarning("FlashlightPlacer: Could not find any suitable surfaces (table or floor). Spawning in front of the player as a fallback.");
            Transform head = Camera.main.transform;
            Vector3 fallbackPosition = head.position + (head.forward * 1.0f) - (head.up * 0.2f);
            Instantiate(m_flashlightPrefab, fallbackPosition, Quaternion.identity);
        }
    }
}
