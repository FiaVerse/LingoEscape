using Meta.XR.MRUtilityKit;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;
using static Meta.XR.MRUtilityKit.MRUK;

public class MonsterController : MonoBehaviour
{

    [SerializeField] private Animator monstrAnimator;
    [SerializeField] private NavMeshAgent navMeshAgent; // the ai agent that will controll the monstor
    [SerializeField] private Rigidbody monstorRigidbody;
    [SerializeField] private float minDistanceToEdge = 0.01f; //Limit the generated point to not being close to a surface's edges and corners
    [SerializeField] private Vector3 rayOffSet = new Vector3(0, 0.1f, 0); // offset of the ray
    [SerializeField] private GameObject onDeadParticleEffect;
    public bool isGrabbed = false;

    private RaycastHit hit;// store the hit info
    private MRUKAnchor anchorHit; //store the hit anchor
    private SurfaceType hitSurfaceType;// stores the surface type of the hit anchor


    private float delayTimer; // interval between random point discovery
    private float timer; // used to calculate current timer value


    public void ReleaseMonstor()
    {
        monstrAnimator.enabled = true;
        navMeshAgent.enabled = true;
        monstorRigidbody.isKinematic = false;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 rayOrigin = transform.position + rayOffSet;
        Vector3 rayDirection = -this.transform.up;
        Ray ray = new Ray(rayOrigin, rayDirection);

        var room = MRUK.Instance?.GetCurrentRoom();
        room?.Raycast(ray, Mathf.Infinity, out hit, out anchorHit);

        timer += Time.deltaTime;

        Vector3 newPos = RandomNavPoint();
        Vector3 closestPos;

        if (timer >= delayTimer && navMeshAgent.enabled)
        {
            bool test = room.IsPositionInRoom(newPos, false); // occasionally NavMesh will generate areas outside the room, so we must test the value from RandomNavPoint

            if (!test)
            {
                anchorHit.GetClosestSurfacePosition(newPos, out closestPos);
                navMeshAgent.SetDestination(closestPos);
            }
            else
            {
                navMeshAgent.SetDestination(newPos);
            }

            float newDelay = UnityEngine.Random.Range(3f, 5.0f);
            delayTimer = newDelay;
            timer = 0;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.name != "Oppy")
            return;
        Instantiate(onDeadParticleEffect,transform.position, Quaternion.identity);
        gameObject.SetActive(false);

    }


    public Vector3 RandomNavPoint()
    {
        if (hit.normal.y > 0)
            hitSurfaceType = SurfaceType.FACING_UP;
        else if (hit.normal.y < 0)
            hitSurfaceType = SurfaceType.FACING_DOWN;
        else
            hitSurfaceType = SurfaceType.VERTICAL;

        GenerateRandomPositionOnAnchor(hitSurfaceType, minDistanceToEdge, out var pos, out var normal);

        return pos;
    }

    public void OnMonstorGrabbed()
    {
        isGrabbed = true;
        navMeshAgent.enabled = false;
        monstorRigidbody.isKinematic = true;
    }

    public void OnMonstorReleased()
    {
        isGrabbed = false;
        navMeshAgent.enabled = true;
    }


    public bool GenerateRandomPositionOnAnchor(MRUK.SurfaceType surfaceTypes, float minDistanceToEdge, out Vector3 position, out Vector3 normal)
    {
        List<Surface> surfaces = new();
        float totalUsableSurfaceArea = 0f;
        float minWidth = 2f * minDistanceToEdge;

        // define these as the negative early exit conditions
        position = Vector3.zero;
        normal = Vector3.zero;
        if (anchorHit == null)
            return false;

        if (anchorHit.HasPlane)
        {
            bool skipPlane = false;

            if (anchorHit.transform.forward.y >= Meta.XR.MRUtilityKit.Utilities.InvSqrt2)
            {
                if ((surfaceTypes & MRUK.SurfaceType.FACING_UP) == 0)
                {
                    skipPlane = true;
                }
            }
            else if (anchorHit.transform.forward.y <= -Meta.XR.MRUtilityKit.Utilities.InvSqrt2)
            {
                if ((surfaceTypes & MRUK.SurfaceType.FACING_DOWN) == 0)
                {
                    skipPlane = true;
                }
            }
            else if ((surfaceTypes & MRUK.SurfaceType.VERTICAL) == 0)
            {
                skipPlane = true;
            }
            if (!skipPlane)
            {
                var size = anchorHit.PlaneRect.Value.size;
                if (size.x > minWidth && size.y > minWidth)
                {
                    var usableArea = (size.x - minWidth) * (size.y - minWidth);
                    totalUsableSurfaceArea += usableArea;
                    surfaces.Add(new()
                    {
                        Anchor = anchorHit,
                        UsableArea = usableArea,
                        IsPlane = true,
                        Bounds = anchorHit.PlaneRect.Value,
                        Transform = anchorHit.transform.localToWorldMatrix
                    });
                }
            }
        }
        if (anchorHit.HasVolume)
        {
            for (int i = 0; i < 6; ++i)
            {
                Rect bounds;
                Matrix4x4 faceTransform;
                if (i == 0)
                {
                    if ((surfaceTypes & MRUK.SurfaceType.FACING_UP) == 0)
                    {
                        continue;
                    }
                }
                else if (i == 1)
                {
                    if ((surfaceTypes & MRUK.SurfaceType.FACING_DOWN) == 0)
                    {
                        continue;
                    }
                }
                else if ((surfaceTypes & MRUK.SurfaceType.VERTICAL) == 0)
                {
                    continue;
                }
                switch (i)
                {
                    case 0:
                        // +Z face
                        bounds = new()
                        {
                            xMin = anchorHit.VolumeBounds.Value.min.x,
                            xMax = anchorHit.VolumeBounds.Value.max.x,
                            yMin = anchorHit.VolumeBounds.Value.min.y,
                            yMax = anchorHit.VolumeBounds.Value.max.y
                        };
                        faceTransform = Matrix4x4.TRS(new Vector3(0f, 0f, anchorHit.VolumeBounds.Value.max.z), Quaternion.identity, Vector3.one);
                        break;
                    case 1:
                        // -Z face
                        bounds = new()
                        {
                            xMin = -anchorHit.VolumeBounds.Value.max.x,
                            xMax = -anchorHit.VolumeBounds.Value.min.x,
                            yMin = anchorHit.VolumeBounds.Value.min.y,
                            yMax = anchorHit.VolumeBounds.Value.max.y
                        };
                        faceTransform = Matrix4x4.TRS(new Vector3(0f, 0f, anchorHit.VolumeBounds.Value.min.z), Quaternion.Euler(0f, 180f, 0f), Vector3.one);
                        break;
                    case 2:
                        // +X face
                        bounds = new()
                        {
                            xMin = -anchorHit.VolumeBounds.Value.max.z,
                            xMax = -anchorHit.VolumeBounds.Value.min.z,
                            yMin = anchorHit.VolumeBounds.Value.min.y,
                            yMax = anchorHit.VolumeBounds.Value.max.y
                        };
                        faceTransform = Matrix4x4.TRS(new Vector3(anchorHit.VolumeBounds.Value.max.x, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), Vector3.one);
                        break;
                    case 3:
                        // -X face
                        bounds = new()
                        {
                            xMin = anchorHit.VolumeBounds.Value.min.z,
                            xMax = anchorHit.VolumeBounds.Value.max.z,
                            yMin = anchorHit.VolumeBounds.Value.min.y,
                            yMax = anchorHit.VolumeBounds.Value.max.y
                        };
                        faceTransform = Matrix4x4.TRS(new Vector3(anchorHit.VolumeBounds.Value.min.x, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), Vector3.one);
                        break;
                    case 4:
                        // +Y face
                        bounds = new()
                        {
                            xMin = anchorHit.VolumeBounds.Value.min.x,
                            xMax = anchorHit.VolumeBounds.Value.max.x,
                            yMin = -anchorHit.VolumeBounds.Value.max.z,
                            yMax = -anchorHit.VolumeBounds.Value.min.z
                        };
                        faceTransform = Matrix4x4.TRS(new Vector3(0f, anchorHit.VolumeBounds.Value.max.y, 0f), Quaternion.Euler(-90f, 0f, 0f), Vector3.one);
                        break;
                    case 5:
                        // -Y face
                        bounds = new()
                        {
                            xMin = anchorHit.VolumeBounds.Value.min.x,
                            xMax = anchorHit.VolumeBounds.Value.max.x,
                            yMin = anchorHit.VolumeBounds.Value.min.z,
                            yMax = anchorHit.VolumeBounds.Value.max.z
                        };
                        faceTransform = Matrix4x4.TRS(new Vector3(0f, anchorHit.VolumeBounds.Value.min.y, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
                        break;
                    default:
                        throw new SwitchExpressionException();
                }

                var size = bounds.size;
                if (size.x > minWidth && size.y > minWidth)
                {
                    var usableArea = (size.x - minWidth) * (size.y - minWidth);
                    totalUsableSurfaceArea += usableArea;
                    surfaces.Add(new()
                    {
                        Anchor = anchorHit,
                        UsableArea = usableArea,
                        IsPlane = false,
                        Bounds = bounds,
                        Transform = anchorHit.transform.localToWorldMatrix * faceTransform
                    });
                }
            }
        }

        if (surfaces.Count == 0)
            return false;

        const int maxIterations = 1000;

        for (int i = 0; i < maxIterations; ++i)
        {
            // Pick a random surface weighted by surface area (surfaces with a larger
            // area have more chance of being chosen)
            var rand = UnityEngine.Random.Range(0, totalUsableSurfaceArea);
            int index = 0;
            for (; index < surfaces.Count - 1; ++index)
            {
                rand -= surfaces[index].UsableArea;
                if (rand <= 0.0f)
                {
                    break;
                }
            }

            var surface = surfaces[index];
            var bounds = surface.Bounds;
            Vector2 pos = new Vector2(
                UnityEngine.Random.Range(bounds.xMin + minDistanceToEdge, bounds.xMax - minDistanceToEdge),
                UnityEngine.Random.Range(bounds.yMin + minDistanceToEdge, bounds.yMax - minDistanceToEdge)
            );
            if (surface.IsPlane && !surface.Anchor.IsPositionInBoundary(pos))
            {
                continue;
            }
            position = surface.Transform.MultiplyPoint3x4(new Vector3(pos.x, pos.y, 0f));
            normal = surface.Transform.MultiplyVector(Vector3.forward);
            return true;
        }

        return false;
    }

    struct Surface
    {
        public MRUKAnchor Anchor;
        public float UsableArea;
        public bool IsPlane;
        public Rect Bounds;
        public Matrix4x4 Transform;
    }
}
