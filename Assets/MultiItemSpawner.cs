// FindFirstSurfaceSpawn.cs
//
// Extends Meta’s FindSpawnPositions so that exactly ONE prefab is
// instantiated on the first valid surface found, then it stops.

using Meta.XR.MRUtilityKit;
using Meta.XR.Util;
using UnityEngine;

namespace Meta.XR.MRUtilityKit
{
    
    public class MultiItemSpawner : FindSpawnPositions
    {
        // Internal flag – once we spawn, we’re done
        private bool _hasSpawned;

        // ────────────────────────────────────────────────────────────
        // Override the entry points so we honour the _hasSpawned flag
        // ────────────────────────────────────────────────────────────
        public new void StartSpawn()
        {
            if (_hasSpawned) return;               // already done
            base.StartSpawn();                     // let parent iterate rooms
        }

        public new void StartSpawn(MRUKRoom room)
        {
            if (_hasSpawned) return;               // already done
            TrySpawnOnce(room);                    // our custom single-shot
        }

        // ────────────────────────────────────────────────────────────
        // Core logic copied from FindSpawnPositions.StartSpawn(room)
        // but breaks immediately after first successful Instantiate.
        // ────────────────────────────────────────────────────────────
        private void TrySpawnOnce(MRUKRoom room)
        {
            // Grab prefab bounds + offsets exactly like parent does
            var prefabBounds = Utilities.GetPrefabBounds(SpawnObject);
            float baseOffset   = -prefabBounds?.min.y ?? 0f;
            float centerOffset =  prefabBounds?.center.y ?? 0f;
            float minRadius    = 0f;
            const float clearanceDistance = 0.01f;
            Bounds adjustedBounds = new();

            if (prefabBounds.HasValue)
            {
                minRadius = Mathf.Min(-prefabBounds.Value.min.x,
                                      -prefabBounds.Value.min.z,
                                       prefabBounds.Value.max.x,
                                       prefabBounds.Value.max.z);
                if (minRadius < 0f) minRadius = 0f;

                Vector3 min = prefabBounds.Value.min;
                Vector3 max = prefabBounds.Value.max;
                min.y += clearanceDistance;
                if (max.y < min.y) max.y = min.y;
                adjustedBounds.SetMinMax(min, max);
                if (OverrideBounds > 0)
                {
                    Vector3 center = new(0f, clearanceDistance, 0f);
                    Vector3 size   = new(OverrideBounds * 2f, clearanceDistance * 2f, OverrideBounds * 2f);
                    adjustedBounds = new Bounds(center, size);
                }
            }

            // Only need one attempt loop; keep MaxIterations for safety
            for (int j = 0; j < MaxIterations && !_hasSpawned; ++j)
            {
                Vector3 spawnPos = Vector3.zero;
                Vector3 normal   = Vector3.up;

                // Use same surface-type logic as parent
                if (SpawnLocations == SpawnLocation.Floating)
                {
                    var rnd = room.GenerateRandomPositionInRoom(minRadius, true);
                    if (!rnd.HasValue) break;
                    spawnPos = rnd.Value;
                }
                else
                {
                    MRUK.SurfaceType sType = 0;
                    switch (SpawnLocations)
                    {
                        case SpawnLocation.AnySurface:
                            sType |= MRUK.SurfaceType.FACING_UP |
                                     MRUK.SurfaceType.VERTICAL   |
                                     MRUK.SurfaceType.FACING_DOWN;
                            break;
                        case SpawnLocation.VerticalSurfaces: sType |= MRUK.SurfaceType.VERTICAL;    break;
                        case SpawnLocation.OnTopOfSurfaces:  sType |= MRUK.SurfaceType.FACING_UP;   break;
                        case SpawnLocation.HangingDown:      sType |= MRUK.SurfaceType.FACING_DOWN; break;
                    }

                    if (!room.GenerateRandomPositionOnSurface(sType, minRadius,
                            new LabelFilter(Labels), out var pos, out var nrm))
                        continue;

                    spawnPos = pos + nrm * baseOffset;
                    normal   = nrm;

                    // Same in-room / clearance checks as parent
                    Vector3 center = spawnPos + nrm * centerOffset;
                    if (!room.IsPositionInRoom(center)     ||
                         room.IsPositionInSceneVolume(center) ||
                         room.Raycast(new Ray(pos, nrm), SurfaceClearanceDistance, out _))
                        continue;
                }

                Quaternion rot = Quaternion.FromToRotation(Vector3.up, normal);

                if (CheckOverlaps && prefabBounds.HasValue &&
                    Physics.CheckBox(spawnPos + rot * adjustedBounds.center,
                                     adjustedBounds.extents, rot, LayerMask,
                                     QueryTriggerInteraction.Ignore))
                    continue;

                // ─────────── SUCCESS: instantiate & mark done ───────────
                if (SpawnObject.gameObject.scene.path == null)
                {
                    Instantiate(SpawnObject, spawnPos, rot, transform);
                }
                else
                {
                    SpawnObject.transform.SetPositionAndRotation(spawnPos, rot);
                }

                _hasSpawned = true;   // block further spawns
            }
        }
    }
}
