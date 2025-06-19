// MultiPrefabRoomSpawnerByLabel.cs  (anchors become parents)

using System;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using Meta.XR.Util;
using UnityEngine;

public class MultiPrefabRoomSpawnerByLabel : MonoBehaviour
{
    /* ── GLOBAL FILTER ────────────────────────────────────────── */
    public MRUK.RoomFilter SpawnOnStart = MRUK.RoomFilter.CurrentRoomOnly;

    /* ── GROUP CONFIG ─────────────────────────────────────────── */
    [Serializable]
    public class LabelGroup
    {
        public MRUKAnchor.SceneLabels Labels = MRUKAnchor.SceneLabels.TABLE;

        public enum SpawnLocation { Floating, AnySurface, VerticalSurfaces, OnTopOfSurfaces, HangingDown }
        public SpawnLocation Surface = SpawnLocation.OnTopOfSurfaces;

        [Min(1)] public int  SpawnAmount   = 1;
        [Min(1)] public int  MaxIterations = 1000;

        public bool  CheckOverlaps            = true;
        public float SurfaceClearanceDistance = 0.1f;
        public float OverrideBounds           = -1;
        public LayerMask LayerMask            = -1;

        public List<GameObject> Prefabs = new();
    }

    public List<LabelGroup> Groups = new();

    /* ── INITIALISE ───────────────────────────────────────────── */
    private void Start()
    {
        if (MRUK.Instance == null) { Debug.LogError("MRUK not present"); return; }

        MRUK.Instance.RegisterSceneLoadedCallback(() =>
        {
            switch (SpawnOnStart)
            {
                case MRUK.RoomFilter.AllRooms:
                    foreach (var room in MRUK.Instance.Rooms) SpawnInRoom(room);
                    break;
                case MRUK.RoomFilter.CurrentRoomOnly:
                    SpawnInRoom(MRUK.Instance.GetCurrentRoom());
                    break;
            }
        });
    }

    /* ── PER-ROOM LOOP ────────────────────────────────────────── */
    private void SpawnInRoom(MRUKRoom room)
    {
        if (room == null) return;

        foreach (var g in Groups)
        {
            foreach (var prefab in g.Prefabs)
            {
                if (prefab == null || prefab.scene.IsValid()) continue;
                SpawnPrefab(room, g, prefab);
            }
        }
    }

    /* ── SPAWN ONE PREFAB ─────────────────────────────────────── */
    private void SpawnPrefab(MRUKRoom room, LabelGroup g, GameObject prefab)
    {
        var b       = Utilities.GetPrefabBounds(prefab);
        float off   = -b?.min.y ?? 0f;
        float rad   = g.OverrideBounds > 0 ? g.OverrideBounds
                     : b.HasValue ? Mathf.Max(b.Value.extents.x, b.Value.extents.z)
                     : 0f;

        for (int n = 0; n < g.SpawnAmount; ++n)
        {
            bool placed = false;

            for (int attempt = 0; attempt < g.MaxIterations; ++attempt)
            {
                if (!TryPickPoint(room, g, rad, off,
                                  out var pos, out var normal, out var anchor))
                    continue;

                if (g.CheckOverlaps && b.HasValue &&
                    Physics.CheckBox(pos + Quaternion.FromToRotation(Vector3.up, normal) * b.Value.center,
                                     b.Value.extents,
                                     Quaternion.FromToRotation(Vector3.up, normal),
                                     g.LayerMask,
                                     QueryTriggerInteraction.Ignore))
                    continue;

                /* — parent to the anchor transform — */
                Instantiate(prefab,
                            pos,
                            Quaternion.FromToRotation(Vector3.up, normal),
                            anchor.transform);

                placed = true;
                break;
            }

            if (!placed)
            {
                Debug.LogWarning($"{name}: couldn’t place {prefab.name} in {room.name}");
                break;
            }
        }
    }

    /* ── PICK RANDOM SURFACE POINT & RETURN ANCHOR ────────────── */
    private static bool TryPickPoint(
        MRUKRoom room, LabelGroup g, float minRadius, float baseOffset,
        out Vector3 worldPos, out Vector3 normal, out MRUKAnchor anchor)
    {
        worldPos = normal = Vector3.zero;
        anchor   = null;

        /* Floating */
        if (g.Surface == LabelGroup.SpawnLocation.Floating)
        {
            var rnd = room.GenerateRandomPositionInRoom(minRadius, true);
            if (!rnd.HasValue) return false;
            worldPos = rnd.Value;
            return true;
        }

        /* Surface-type bitmask */
        MRUK.SurfaceType st = g.Surface switch
        {
            LabelGroup.SpawnLocation.AnySurface       => MRUK.SurfaceType.FACING_UP |
                                                         MRUK.SurfaceType.VERTICAL  |
                                                         MRUK.SurfaceType.FACING_DOWN,
            LabelGroup.SpawnLocation.VerticalSurfaces => MRUK.SurfaceType.VERTICAL,
            LabelGroup.SpawnLocation.OnTopOfSurfaces  => MRUK.SurfaceType.FACING_UP,
            LabelGroup.SpawnLocation.HangingDown      => MRUK.SurfaceType.FACING_DOWN,
            _                                         => 0
        };

        if (!room.GenerateRandomPositionOnSurface(st, minRadius, new LabelFilter(g.Labels),
                                                  out var p, out var n)) return false;

        if (room.Raycast(new Ray(p, n), g.SurfaceClearanceDistance, out _))
            return false;

        /* — resolve owning anchor — */
        foreach (var a in room.Anchors)
        {
            if (a.VolumeBounds.HasValue &&
                a.VolumeBounds.Value.Contains(a.transform.InverseTransformPoint(p)))
            { anchor = a; break; }

            if (a.PlaneRect.HasValue &&
                a.PlaneRect.Value.Contains((Vector2)a.transform.InverseTransformPoint(p)))
            { anchor = a; break; }
        }

        if (anchor == null) return false; // shouldn’t happen but safe-guard

        worldPos = p + n * baseOffset;
        normal   = n;
        return true;
    }
}
