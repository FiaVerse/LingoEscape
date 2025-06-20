// LightSwitchPlacer.cs
// Automatically finds a suitable wall and places a light switch prefab on it at a realistic height.

using UnityEngine;
using Meta.XR.MRUtilityKit;
using System.Collections.Generic; // Required for using Lists

public class LightSwitchPlacer : MonoBehaviour
{
    [Header("Prefab Settings")]
    [Tooltip("The Light Switch prefab to be placed. It should include visuals and a TextMeshPro label.")]
    [SerializeField]
    private GameObject m_lightSwitchPrefab;

    [Header("Placement Settings")]
    [Tooltip("The desired height of the light switch from the floor (in meters).")]
    [SerializeField]
    private float m_switchHeightFromFloor = 1.2f;

    [Tooltip("Small offset from the wall surface to prevent visual flickering (Z-fighting).")]
    [SerializeField]
    private float m_surfaceOffset = 0.01f;

    void OnEnable()
    {
        if (MRUK.Instance != null)
        {
            MRUK.Instance.SceneLoadedEvent.AddListener(PlaceLightSwitch);
        }
        else
        {
            Debug.LogError("LightSwitchPlacer: MRUK.Instance is NULL.", this);
        }
    }

    void OnDisable()
    {
        if (MRUK.Instance != null)
        {
            MRUK.Instance.SceneLoadedEvent.RemoveListener(PlaceLightSwitch);
        }
    }

    private void PlaceLightSwitch()
    {
        if (m_lightSwitchPrefab == null)
        {
            Debug.LogError("LightSwitchPlacer: Light Switch Prefab is not assigned in the Inspector!", this);
            return;
        }

        MRUKRoom currentRoom = MRUK.Instance.GetCurrentRoom();
        if (currentRoom == null)
        {
            Debug.LogError("LightSwitchPlacer: No current room found.", this);
            return;
        }

        List<MRUKAnchor> wallAnchors = new List<MRUKAnchor>();
        foreach (var anchor in currentRoom.Anchors)
        {
            // CORRECTED: Use the HasAnyLabel method, which accepts the SceneLabels enum.
            if (anchor.HasAnyLabel(MRUKAnchor.SceneLabels.WALL_FACE))
            {
                wallAnchors.Add(anchor);
            }
        }
        
        if (wallAnchors.Count == 0)
        {
            Debug.LogWarning("LightSwitchPlacer: No walls were found in the scene scan.", this);
            return;
        }
        MRUKAnchor wall = wallAnchors[Random.Range(0, wallAnchors.Count)];

        Vector3 roomCenter = Vector3.zero;
        MRUKAnchor floorAnchor = null;
        foreach (var anchor in currentRoom.Anchors)
        {
            // CORRECTED: Use the HasAnyLabel method, which accepts the SceneLabels enum.
            if (anchor.HasAnyLabel(MRUKAnchor.SceneLabels.FLOOR))
            {
                floorAnchor = anchor;
                break;
            }
        }

        if (floorAnchor != null)
        {
            roomCenter = floorAnchor.transform.position;
        }
        else
        {
            roomCenter = Camera.main.transform.position;
            Debug.LogWarning("LightSwitchPlacer: No floor anchor found. Using camera position for placement.");
        }
        
        Vector3 placementTargetHeight = new Vector3(roomCenter.x, roomCenter.y + m_switchHeightFromFloor, roomCenter.z);
        
        wall.GetClosestSurfacePosition(placementTargetHeight, out Vector3 closestPointOnWall, out Vector3 wallNormal);

        Vector3 finalPosition = closestPointOnWall + wallNormal * m_surfaceOffset;
        Quaternion finalRotation = Quaternion.LookRotation(-wallNormal, Vector3.up);

        GameObject switchInstance = Instantiate(m_lightSwitchPrefab, finalPosition, finalRotation);
        switchInstance.transform.SetParent(wall.transform);

        Debug.Log($"LightSwitchPlacer: Successfully placed light switch on wall '{wall.name}'.", switchInstance);
    }
}
