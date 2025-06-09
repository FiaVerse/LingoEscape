using Meta.XR.MRUtilityKit;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using static UnityEngine.GraphicsBuffer;


public class FindEnemySpawnLocation : FindSpawnPositions
{
    private Transform cameraTransform;
    public UnityEvent onObjectsSpawned = new UnityEvent();

    [SerializeField] private GameObject spawnLocationIndicator;
    [SerializeField] private int spawnTimer;

    private TextMeshProUGUI text;
    
    private void Start()
    {
        cameraTransform = GameObject.FindWithTag("MainCamera").transform;
    }

    public void SpawnCoffinPoint()
    {
        var room = MRUK.Instance.GetCurrentRoom();
        var prefabBounds = Meta.XR.MRUtilityKit.Utilities.GetPrefabBounds(SpawnObject);
        float minRadius = 0.0f;
        const float clearanceDistance = 0.01f;
        float baseOffset = -prefabBounds?.min.y ?? 0.0f;
        float centerOffset = prefabBounds?.center.y ?? 0.0f;
        Bounds adjustedBounds = new();

        if (prefabBounds.HasValue)
        {
            minRadius = Mathf.Min(-prefabBounds.Value.min.x, -prefabBounds.Value.min.z, prefabBounds.Value.max.x, prefabBounds.Value.max.z);
            if (minRadius < 0f)
            {
                minRadius = 0f;
            }
            var min = prefabBounds.Value.min;
            var max = prefabBounds.Value.max;
            min.y += clearanceDistance;
            if (max.y < min.y)
            {
                max.y = min.y;
            }
            adjustedBounds.SetMinMax(min, max);
            if (OverrideBounds > 0)
            {
                Vector3 center = new Vector3(0f, clearanceDistance, 0f);
                Vector3 extents = new Vector3((OverrideBounds * 2f), clearanceDistance, (OverrideBounds * 2f)); // assuming user intends to input X distance from other colliders
                adjustedBounds = new Bounds(center, extents);
            }
        }

        for (int i = 0; i < SpawnAmount; ++i)
        {
            for (int j = 0; j < MaxIterations; ++j)
            {
                Vector3 spawnPosition = Vector3.zero;
                Vector3 spawnNormal = Vector3.zero;
                if (SpawnLocations == SpawnLocation.Floating)
                {
                    var randomPos = room.GenerateRandomPositionInRoom(minRadius, true);
                    
                    if (!randomPos.HasValue)
                    {
                        break;
                    }

                    spawnPosition = randomPos.Value;
                }
                else
                {
                    MRUK.SurfaceType surfaceType = 0;
                    switch (SpawnLocations)
                    {
                        case SpawnLocation.AnySurface:
                            surfaceType |= MRUK.SurfaceType.FACING_UP;
                            surfaceType |= MRUK.SurfaceType.VERTICAL;
                            surfaceType |= MRUK.SurfaceType.FACING_DOWN;
                            break;
                        case SpawnLocation.VerticalSurfaces:
                            surfaceType |= MRUK.SurfaceType.VERTICAL;
                            break;
                        case SpawnLocation.OnTopOfSurfaces:
                            surfaceType |= MRUK.SurfaceType.FACING_UP;
                            break;
                        case SpawnLocation.HangingDown:
                            surfaceType |= MRUK.SurfaceType.FACING_DOWN;
                            break;
                    }
                    if (room.GenerateRandomPositionOnSurface(surfaceType, minRadius, LabelFilter.FromEnum(Labels), out var pos, out var normal))
                    {
                        spawnPosition = pos + normal * baseOffset;
                        spawnNormal = normal;
                        // In some cases, surfaces may protrude through walls and end up outside the room
                        // check to make sure the center of the prefab will spawn inside the room
                        if (!room.IsPositionInRoom(spawnPosition + normal * centerOffset))
                        {
                            continue;
                        }
                    }
                }

                Quaternion spawnRotation = Quaternion.FromToRotation(Vector3.up, spawnNormal);
                if (CheckOverlaps && prefabBounds.HasValue)
                {
                    if (Physics.CheckBox(spawnPosition + spawnRotation * adjustedBounds.center, adjustedBounds.extents, spawnRotation, LayerMask, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                }

                if (SpawnObject.gameObject.scene.path == null)
                {
                    GameObject spawnPoint = Instantiate(spawnLocationIndicator);
                    spawnPoint.GetComponent<AudioSource>().Play();
                    text = spawnPoint.GetComponentInChildren<TextMeshProUGUI>();
                    spawnPoint.transform.position = spawnPosition;
                    spawnPoint.transform.rotation = spawnRotation;

                    StartCoroutine(SpawnCountDown(spawnTimer, spawnPosition, spawnRotation));
                }
                else
                {
                    SpawnObject.transform.position = spawnPosition;
                    SpawnObject.transform.rotation = spawnRotation;
                    return; // ignore SpawnAmount once we have a successful move of existing object in the scene
                }
                break;
            }
        }
    }

    private IEnumerator SpawnCountDown(int seconds, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        int counter = seconds; 
        while (counter > 0)
        {
            text.text = counter.ToString();
            yield return new WaitForSeconds(1);
            counter--;
        }
        text.text = " ";
        
        GameObject spawnedObject = Instantiate(SpawnObject,transform);
        spawnedObject.transform.position = spawnPosition + new Vector3(0, cameraTransform.position.y, 0);
        spawnedObject.transform.rotation = spawnRotation;
        onObjectsSpawned?.Invoke();
    }

}
