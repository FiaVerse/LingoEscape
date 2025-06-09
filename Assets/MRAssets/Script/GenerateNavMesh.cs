using Meta.XR.MRUtilityKit;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(NavMeshSurface))]

public class GenerateNavMesh : MonoBehaviour
{
    public UnityEvent onNavMeshInitialized = new UnityEvent();
    private NavMeshSurface navMeshSurface;
    private float minimumNavMeshSurfaceArea = 0;

    private void Awake()
    {
        navMeshSurface = gameObject.GetComponent<NavMeshSurface>();
    }
    public void InitializeBounds()
    {
        navMeshSurface.BuildNavMesh();
        if (navMeshSurface.navMeshData.sourceBounds.extents.x * navMeshSurface.navMeshData.sourceBounds.extents.z > minimumNavMeshSurfaceArea)
        {
            onNavMeshInitialized?.Invoke();
        }
        else
        {
            Debug.LogWarning("ResizeNavMesh failed to generate a nav mesh, this may be because the room is too small" +
                " or the AgentType settings are to strict");
        }
    }
}
