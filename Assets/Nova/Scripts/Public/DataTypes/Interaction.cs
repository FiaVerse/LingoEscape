// Copyright (c) Supernova Technologies LLC
//#define DEBUG_GESTURES
using Nova.Internal;
using Nova.Internal.Collections;
using Nova.Internal.Input;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

using InputRouter = Nova.Internal.InputState<Nova.UIBlockHit>.InputRouter;
using Navigator = Nova.Internal.Navigator<Nova.UIBlock>;

namespace Nova
{
    /// <summary>
    /// A way to indicate the physical stability of an input device as it's used to perform various interactions.
    /// </summary>
    /// <remarks>
    /// Some examples:
    /// <list type="table">
    /// <item>
    /// <term>High Accuracy</term>
    /// <description>
    /// <list type="bullet">
    /// <item><description>Mouse</description></item>
    /// <item><description>Finger (touchscreen)</description></item>
    /// <item><description>Stylus (touchscreen)</description></item>
    /// <item><description>XR Controller Ray</description></item>
    /// </list>
    /// </description>
    /// </item>
    /// <item><term>Low Accuracy</term>
    /// <description>
    /// <list type="bullet">
    /// <item><description>XR Hands</description></item>
    /// <item><description>XR Finger Tips</description></item>
    /// </list>
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    public enum InputAccuracy
    {
        /// <summary>
        /// High accuracy, physically stable.
        /// </summary>
        High = 0,
        /// <summary>
        /// Lower accuracy, less physically stable.
        /// </summary>
        Low = 1
    }

    internal static class SphereExtensions
    {
        public static StructuredSphere ToInternal(ref this Sphere sphere)
        {
            return new StructuredSphere() { Position = sphere.Center, Radius = sphere.Radius };
        }
    }

    internal static class InteractionExtensions
    {
        public static Internal.Interaction ToInternal(this Interaction.Update update)
        {
            return new Internal.Interaction(update.Ray, update.ControlID, update.UserData);
        }

        public static Interaction.Update ToPublic(ref this Internal.Interaction update)
        {
            return new Interaction.Update(update.Ray, update.ID, update.UserData);
        }
    }


    /// <summary>
    /// The details of a <see cref="Ray"/>-><see cref="Nova.UIBlock"/> or <see cref="Sphere"/>-><see cref="Nova.UIBlock"/> intersection.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><see cref="Interaction.Raycast(Ray, out UIBlockHit, float, int)"/></description></item>
    /// <item><description><see cref="Interaction.RaycastAll(Ray, List{UIBlockHit}, float, int)"/></description></item>
    /// <item><description><see cref="Interaction.SphereCollide(Sphere, out UIBlockHit, int)"/></description></item>
    /// <item><description><see cref="Interaction.SphereCollideAll(Sphere, List{UIBlockHit}, int)"/></description></item>
    /// </list>
    /// </remarks>
    [System.Serializable]
    public struct UIBlockHit : System.IEquatable<UIBlockHit>, IBlockHit
    {
        IUIBlock IBlockHit.UIBlock { get => UIBlock; set => UIBlock = value as UIBlock; }
        Vector3 IBlockHit.Position { get => Position; set => Position = value; }
        Vector3 IBlockHit.Normal { get => Normal; set => Normal = value; }

        /// <summary>
        /// The UI Block that was hit.
        /// </summary>
        public UIBlock UIBlock;

        /// <summary>
        /// The hit position on the UI Block in world space.
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// The hit normal in world space.
        /// </summary>
        public Vector3 Normal;

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="lhs">Left hand side.</param>
        /// <param name="rhs">Right hand side.</param>
        /// <returns>
        /// <see langword="true"/> if <c><paramref name="lhs"/>.UIBlock == <paramref name="rhs"/>.UIBlock &amp;&amp; <paramref name="lhs"/>.Position == <paramref name="rhs"/>.Position &amp;&amp; <paramref name="lhs"/>.Normal == <paramref name="rhs"/>.Normal.</c>
        /// </returns>
        public static bool operator ==(UIBlockHit lhs, UIBlockHit rhs)
        {
            return lhs.UIBlock == rhs.UIBlock && lhs.Position == rhs.Position && lhs.Normal == rhs.Normal;
        }

        /// <summary>
        /// Inequality operator.
        /// </summary>
        /// <param name="lhs">Left hand side.</param>
        /// <param name="rhs">Right hand side.</param>
        /// <returns>
        /// <see langword="true"/> if <c><paramref name="lhs"/>.UIBlock != <paramref name="rhs"/>.UIBlock || <paramref name="lhs"/>.Position != <paramref name="rhs"/>.Position || <paramref name="lhs"/>.Normal != <paramref name="rhs"/>.Normal</c>.
        /// </returns>
        public static bool operator !=(UIBlockHit lhs, UIBlockHit rhs)
        {
            return lhs.UIBlock != rhs.UIBlock || lhs.Position != rhs.Position || lhs.Normal != rhs.Normal;
        }

        /// <summary>
        /// The hashcode for this <see cref="UIBlockHit"/>.
        /// </summary>
        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Position.GetHashCode();
            hash = (hash * 7) + Normal.GetHashCode();

            if (UIBlock != null)
            {
                hash = (hash * 7) + UIBlock.GetHashCode();
            }

            return hash;
        }

        /// <summary>
        /// Equality compare.
        /// </summary>
        /// <param name="other">The other <see cref="UIBlockHit"/> to compare.</param>
        /// <returns>
        /// <see langword="true"/> if <c>this == <paramref name="other"/></c>.
        /// </returns>
        public override bool Equals(object other)
        {
            if (other is UIBlockHit hit)
            {
                return this == hit;
            }

            return false;
        }

        /// <summary>
        /// Equality compare.
        /// </summary>
        /// <param name="other">The other <see cref="UIBlockHit"/> to compare.</param>
        /// <returns>
        /// <see langword="true"/> if <c>this == <paramref name="other"/></c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(UIBlockHit other)
        {
            return this == other;
        }
    }

    /// <summary>
    /// The static access point to provide input events and leverage the Nova input system.
    /// </summary>
    public static class Interaction
    {
        /// <summary>
        /// Represents a single update of an interaction.
        /// </summary>
        /// <seealso cref="Interaction.Point(Update, bool, bool, float, int, InputAccuracy)"/>
        /// <seealso cref="Interaction.Scroll(Update, Vector3, float, int, InputAccuracy)"/>
        /// <seealso cref="Interaction.Cancel(Update)"/>
        /// <seealso cref="IGestureEvent"/>
        public struct Update : System.IEquatable<Update>
        {
            /// <summary>
            /// The <see cref="ControlID"/> of <see cref="Uninitialized"/>.
            /// </summary>
            public const uint UninitializedControlID = uint.MaxValue;

            /// <summary>
            /// Represents an uninitialized interaction.
            /// </summary>
            public static readonly Update Uninitialized = new Update(UninitializedControlID);

            /// <summary>
            /// The unique identifier of the input control.
            /// </summary>
            /// <remarks>Likely represents a single finger on a touchscreen, a mouse button, a joystick, etc.</remarks>
            public uint ControlID;

            /// <summary>
            /// The pointer ray in world space.
            /// </summary>
            public Ray Ray;

            /// <summary>
            /// Any additional data to pass along to the receiver of the current interaction.
            /// </summary>
            public object UserData;

            /// <summary>
            /// Create a new <see cref="Update"/>.
            /// </summary>
            /// <param name="ray">The world space ray indicating where the input control is "pointing".</param>
            /// <param name="controlID">The unique identifier of the input control generating this <see cref="Update"/>.</param>
            /// <param name="userData">Any additional data to pass along to the receiver of the current interaction.</param>
            public Update(Ray ray, uint controlID = 0, object userData = null)
            {
                Ray = ray;
                ControlID = controlID;
                UserData = userData;
            }

            /// <summary>
            /// Create an interaction without a <see cref="Ray"/> or <see cref="UserData"/>.
            /// </summary>
            /// <remarks>Useful for when the caller just wants a wrapped <see cref="ControlID"/>.</remarks>
            /// <param name="controlID">The unique identifier of the input control generating this <see cref="Update"/>.
            /// </param>
            public Update(uint controlID)
            {
                ControlID = controlID;
                Ray = new Ray(Math.float3_NaN, Math.float3_NaN);
                UserData = null;
            }

            /// <summary>
            /// Equality operator.
            /// </summary>
            /// <param name="lhs">Left hand side.</param>
            /// <param name="rhs">Right hand side.</param>
            /// <returns><see langword="true"/> if all fields of <paramref name="lhs"/> are equal to all field of <paramref name="rhs"/>.</returns>
            public static bool operator ==(Update lhs, Update rhs)
            {
                return lhs.ControlID == rhs.ControlID &&
                       lhs.Ray.origin == rhs.Ray.origin &&
                       lhs.Ray.direction == rhs.Ray.direction &&
                       lhs.UserData == rhs.UserData;
            }

            /// <summary>
            /// Inequality operator
            /// </summary>
            /// <param name="lhs">Left hand side</param>
            /// <param name="rhs">Right hand side</param>
            /// <returns><see langword="true"/> if any field of <paramref name="lhs"/> is <b>not</b> equal to its corresponding field of <paramref name="rhs"/>.</returns>
            public static bool operator !=(Update lhs, Update rhs)
            {
                return !(lhs == rhs);
            }

            /// <summary>
            /// The hashcode for this <see cref="Update"/>.
            /// </summary>
            public override int GetHashCode()
            {
                int hash = 13;
                hash = (hash * 7) + ControlID.GetHashCode();
                hash = (hash * 7) + Ray.GetHashCode();

                if (UserData != null)
                {
                    hash = (hash * 7) + UserData.GetHashCode();
                }

                return hash;
            }

            /// <summary>
            /// Equality compare.
            /// </summary>
            /// <param name="other">The other <see cref="Update"/> to compare.</param>
            /// <returns>
            /// <see langword="true"/> if <c>this == <paramref name="other"/></c>.
            /// </returns>
            public override bool Equals(object other)
            {
                if (other is Update ray)
                {
                    return this == ray;
                }

                return false;
            }

            /// <summary>
            /// Equality compare.
            /// </summary>
            /// <param name="other">The other <see cref="Update"/> to compare.</param>
            /// <returns>
            /// <see langword="true"/> if <c>this == <paramref name="other"/></c>.
            /// </returns>
            public bool Equals(Update other)
            {
                return this == other;
            }

            /// <summary>
            /// The string representation of this <see cref="Update"/>.
            /// </summary>
            public override string ToString()
            {
                return UserData != null ? $"ControlID = {ControlID}, Ray = {Ray}, UseData = {UserData}" : $"ControlID = {ControlID}, Ray = {Ray}";
            }
        }

        /// <summary>
        /// A layer mask to select all GameObject layers, equivalent to <c>Physics.AllLayers</c>.
        /// </summary>
        public const int AllLayers = Constants.PhysicsAllLayers;
        private const float SphereCancelGestureThreshold = 10;

        [System.NonSerialized]
        private static InputRouter Router = new InputRouter();
        [System.NonSerialized]
        private static List<HitTestResult> hitTestCache = new List<HitTestResult>();
        [System.NonSerialized]
        private static List<UIBlockHit> blockHitCache = new List<UIBlockHit>();

        internal static void Init()
        {
            if (!Compat.NovaApplication.IsPlaying)
            {
                return;
            }

            Router.Init();
        }

        /// <summary>
        /// Retrieves the latest valid <see cref="UIBlockHit"/> to receive an interaction event from the provided <paramref name="controlID"/>.
        /// </summary>
        /// <param name="controlID">The unique identifier of the input control.</param>
        /// <param name="receiverHit">The latest valid <see cref="UIBlockHit"/> created with the same <paramref name="controlID"/>.</param>
        /// <returns>If a receiver is found and the interaction is valid, returns <see langword="true"/>. If the receiver was not found or the interaction has been canceled, returns <see langword="false"/>.</returns>
        public static bool TryGetActiveReceiver(uint controlID, out UIBlockHit receiverHit)
        {
            // first check for point event receivers
            if (Router.TryGetLatestReceiver<bool>(controlID, out receiverHit))
            {
                return true;
            }

            // might have received a scroll event instead
            return Router.TryGetLatestReceiver<UniqueValue<Vector3>>(controlID, out receiverHit);
        }

        /// <summary>
        /// Performs a sphere collision against all active <see cref="UIBlock"/>s in the scene and populates the provided list with <see cref="UIBlockHit"/>s for all <see cref="UIBlock"/>s colliding with the provided <paramref name="sphere"/>.
        /// </summary>
        /// <remarks>Performed by the Nova Input System, independent of the <see cref="Physics"/> and <see cref="Physics2D"/> systems.</remarks>
        /// <param name="sphere">The sphere, in world space, to cast</param>
        /// <param name="hitsToPopulate">The list to populate with all <see cref="UIBlockHit"/> collisions, sorted by top-most-rendered (at index 0).</param>
        /// <param name="layerMask">The gameobject layers to include, defaults to "All Layers".</param>
        /// <exception cref="System.ArgumentNullException">if <c><paramref name="hitsToPopulate"/> == null</c>.</exception>
        public static void SphereCollideAll(Sphere sphere, List<UIBlockHit> hitsToPopulate, int layerMask = AllLayers)
        {
            if (hitsToPopulate == null)
            {
                throw new System.ArgumentNullException($"Provided list of {nameof(hitsToPopulate)} is null.");
            }

            HitTestAll(sphere.ToInternal(), hitTestCache, hitsToPopulate, layerMask);
        }

        /// <summary>
        /// Performs a sphere collision test against all active <see cref="UIBlock"/>s in the scene and retrieves a <see cref="UIBlockHit"/> for the top-most-rendered <see cref="UIBlock"/> colliding with the provided <paramref name="sphere"/>.
        /// </summary>
        /// <remarks>Performed by the Nova Input System, independent of the <see cref="Physics"/> and <see cref="Physics2D"/> systems.</remarks>
        /// <param name="sphere">The sphere, in world space, to cast</param>
        /// <param name="blockHit">A <see cref="UIBlockHit"/> for the top-most-rendered <see cref="UIBlock"/> colliding with the provided <paramref name="sphere"/>.</param>
        /// <param name="layerMask">The gameobject layers to include, defaults to "All Layers".</param>
        /// <returns><see langword="true"/> if <i>any</i> <see cref="UIBlock"/> (on the <paramref name="layerMask"/>) collides with the provided <paramref name="sphere"/>.</returns>
        public static bool SphereCollide(Sphere sphere, out UIBlockHit blockHit, int layerMask = AllLayers)
        {
            if (InputEngine.Instance.HitTest(sphere.ToInternal(), out HitTestResult result, layerMask))
            {
                blockHit.UIBlock = result.HitBlock as UIBlock;
                blockHit.Position = result.HitPoint;
                blockHit.Normal = result.Normal;
                return result.HitBlock != null;
            }

            blockHit.UIBlock = null;
            blockHit.Position = Math.Vector3_NaN;
            blockHit.Normal = Math.Vector3_NaN;
            return false;
        }

        /// <summary>
        /// Performs a raycast against all active <see cref="UIBlock"/>s in the scene and populates the provided list with <see cref="UIBlockHit"/>s for all <see cref="UIBlock"/>s intersecting with the provided <paramref name="ray"/>.
        /// </summary>
        /// <remarks>Performed by the Nova Input System, independent of the <see cref="Physics"/> and <see cref="Physics2D"/> systems.</remarks>
        /// <param name="ray">The ray, in world space, to cast</param>
        /// <param name="hitsToPopulate">The list to populate with all <see cref="UIBlockHit"/> collisions, sorted by top-most-rendered (at index 0).</param>
        /// <param name="maxDistance">The max distance from the ray origin (in world space) to consider an intersection point a "hit".</param>
        /// <param name="layerMask">The gameobject layers to include, defaults to "All Layers".</param>
        /// <exception cref="System.ArgumentNullException">if <c><paramref name="hitsToPopulate"/> == null</c>.</exception>
        public static void RaycastAll(Ray ray, List<UIBlockHit> hitsToPopulate, float maxDistance = float.PositiveInfinity, int layerMask = AllLayers)
        {
            HitTestAll(ray, hitTestCache, hitsToPopulate, maxDistance, layerMask);
        }

        /// <summary>
        /// Performs a raycast against all active <see cref="UIBlock"/>s in the scene and retrieves a <see cref="UIBlockHit"/> for the top-most-rendered <see cref="UIBlock"/> colliding with the provided <paramref name="ray"/>.
        /// </summary>
        /// <remarks>Performed by the Nova Input System, independent of the <see cref="Physics"/> and <see cref="Physics2D"/> systems.</remarks>
        /// <param name="ray">The ray, in world space, to cast</param>
        /// <param name="blockHit">A <see cref="UIBlockHit"/> for the top-most-rendered <see cref="UIBlock"/> colliding with the provided <paramref name="ray"/>.</param>
        /// <param name="maxDistance">The max distance from the ray origin (in world space) to consider an intersection point a "hit".</param>
        /// <param name="layerMask">The gameobject layers to include, defaults to "All Layers".</param>
        /// <returns><see langword="true"/> if <i>any</i> <see cref="UIBlock"/> (on the <paramref name="layerMask"/> and within range) collides with the provided <paramref name="ray"/>.</returns>
        public static bool Raycast(Ray ray, out UIBlockHit blockHit, float maxDistance = float.PositiveInfinity, int layerMask = AllLayers)
        {
            if (InputEngine.Instance.HitTest(ray, out HitTestResult result, maxDistance, layerMask))
            {
                blockHit.UIBlock = result.HitBlock as UIBlock;
                blockHit.Position = result.HitPoint;
                blockHit.Normal = result.Normal;
                return result.HitBlock != null;
            }

            blockHit.UIBlock = null;
            blockHit.Position = Math.Vector3_NaN;
            blockHit.Normal = Math.Vector3_NaN;
            return false;
        }

        /// <summary>
        /// Performs a sphere collision test with the provided <see cref="Sphere"/>, 
        /// creates an <see cref="Update"/> from the results, and routes the <see cref="Update"/>
        /// to the collided <see cref="UIBlock"/> or the <see cref="UIBlock"/> currently capturing 
        /// the active interaction.
        /// </summary>
        /// <remarks>
        /// Point updates are only delivered to <see cref="UIBlock"/>s with an attached 
        /// <see cref="Interactable"/> or <see cref="Scroller"/> component.<br/><br/>
        /// To get the most reliable behavior between potentially conflicting press, drag,
        /// and scroll gestures, the entry point of the overlapping target <see cref="UIBlock"/>s
        /// <b>must</b> all be coplanar. If the entry points aren't coplanar, say a scrollable <see cref="ListView"/>'s 
        /// front face is positioned behind a draggable list item's front face, attempts to scroll the <see cref="ListView"/>
        /// will likely fail, and the list item will be dragged instead.
        /// </remarks>
        /// <param name="sphere">The sphere, in world space, to cast</param>
        /// <param name="controlID">The unique identifier of the input control generating this <see cref="Update"/>.</param>
        /// <param name="userData">Any additional data to pass along to the receiver of the current interaction. See <see cref="Update.UserData"/>.</param>
        /// <param name="layerMask">The gameobject layers to include, defaults to "All Layers".</param>
        /// <param name="accuracy">The accuracy of the input source, defaults to <see cref="InputAccuracy.Low"/>.</param>
        /// <seealso cref="Point(Update, bool, bool, float, int, InputAccuracy)"/>
        /// <seealso cref="Raycast(Ray, out UIBlockHit, float, int)"/>
        /// <seealso cref="RaycastAll(Ray, List{UIBlockHit}, float, int)"/>
        /// <seealso cref="SphereCollide(Sphere, out UIBlockHit, int)"/>
        /// <seealso cref="SphereCollideAll(Sphere, List{UIBlockHit}, int)"/>
        /// <seealso cref="Cancel(Update)"/>
        public static void Point(Sphere sphere, uint controlID, object userData = null, int layerMask = AllLayers, InputAccuracy accuracy = InputAccuracy.Low)
        {
            if (controlID < 0 || controlID >= InputRouter.MaxControls)
            {
                throw new System.ArgumentException($"Invalid Control ID [{controlID}]. Expected within range [0, {InputRouter.MaxControls}]");
            }

            StructuredSphere collider = sphere.ToInternal();
            HitTestAll(collider, hitTestCache, blockHitCache, layerMask);

            bool hitSomething = blockHitCache.Count > 0;

            bool foundSomething = false;
            bool hitPrevious = false;
            UIBlockHit previousHit = default;
            bool wasCapturing = false;

            if (hitSomething)
            {
                wasCapturing = Router.TryGetCurrentCapturingInput<bool>(controlID, out previousHit);
                bool2 result = FilterHitsForInputType<bool>(blockHitCache, target: previousHit.UIBlock);

                foundSomething = result.x;
                hitPrevious = result.y;

                if (foundSomething && !hitPrevious && wasCapturing)
                {
                    // if we hit something, but we didn't hit the previous object capturing input,
                    // we want to maintain the current gesture if the collider interesects the
                    // previously gesture plane. If it doesn't, then we want to send a pointer up.
                    Plane plane = new Plane(previousHit.Normal, previousHit.Position);
                    Vector3 hitPoint = plane.ClosestPointOnPlane(collider.Position);
                    hitPrevious = Vector3.Distance(hitPoint, collider.Position) <= collider.Radius;
                }
            }

            bool cancel = false;
            if (!foundSomething && (wasCapturing || Router.TryGetCurrentCapturingInput<bool>(controlID, out previousHit)))
            {
                Plane plane = new Plane(previousHit.Normal, previousHit.Position);
                Vector3 hitPoint = plane.ClosestPointOnPlane(collider.Position);

                if (plane.GetSide(collider.Position)) // in front of the hit plane
                {
                    blockHitCache.Add(new UIBlockHit() { UIBlock = previousHit.UIBlock, Position = hitPoint, Normal = previousHit.Normal });
                    foundSomething = true;
                    hitPrevious = true;
                }
                else  // behind the hit plane
                {
                    float distance = Vector3.Distance(collider.Position, hitPoint);

                    if (distance <= collider.Radius)
                    {
                        UIBlock root = previousHit.UIBlock.Root;

                        Bounds uiBlockBounds = new Bounds(root.HierarchyCenter, root.HierarchySize + (Vector3.one * 2 * Math.Epsilon));
                        Vector3 hitPointLocalSpace = Math.ApproximatelyZeroToZero(root.transform.InverseTransformPoint(hitPoint));

                        if (uiBlockBounds.Contains(hitPointLocalSpace))
                        {
                            blockHitCache.Add(new UIBlockHit() { UIBlock = previousHit.UIBlock, Position = hitPoint, Normal = previousHit.Normal });
                            hitSomething = true;
                        }
                    }
                    else if (distance > collider.Radius * SphereCancelGestureThreshold)
                    {
                        // only cancel if we go way past. 
                        cancel = true;
                    }
                }
            }

            // if the hit cache is empty, we have no relative input direction
            Vector3 sourceDirection = blockHitCache.Count > 0 ? (blockHitCache[0].Position - sphere.Center).normalized : Vector3.zero;

            Ray ray = new Ray(sphere.Center - sourceDirection * sphere.Radius, sourceDirection);
            Update update = new Update(ray, controlID, userData);

            if (cancel)
            {
                Router.Cancel(update.ToInternal());
            }
            else
            {
                // In the collision case, we only want to consider a "pointer down" if we collide with something which will actually capture the event.
                // Otherwise we won't try to start a gesture until we get a "pointer up" event, as opposed to a "pointer further down".
                bool pointerDown = hitSomething && (foundSomething == hitPrevious) && blockHitCache.Count > 0;

                UpdateInternal(ref update, blockHitCache, input: pointerDown, InteractionType.Gesturable, noisy: accuracy == InputAccuracy.Low);
            }
        }

        /// <summary>
        /// Performs a raycast with the provided <see cref="Update.Ray"/> and routes the <paramref name="update"/> update to the collided <see cref="UIBlock"/> or the <see cref="UIBlock"/> currently capturing the active interaction.
        /// </summary>
        /// <remarks>Point updates are only delivered to <see cref="UIBlock"/>s with an attached <see cref="Interactable"/> or <see cref="Scroller"/> component.</remarks>
        /// <param name="update">The data tied to this interaction update. <see cref="Update.Ray"/> is in world space.</param>
        /// <param name="pointerDown">A flag to indicate the "pressed" state of the control represented by <see cref="Update.ControlID"/>.</param>
        /// <param name="allowDrag">A flag to indicate whether or not this call to <see cref="Point(Update, bool, bool, float, int, InputAccuracy)"/> can trigger or update an <see cref="Gesture.OnDrag"/> event.</param>
        /// <param name="maxDistance">The max distance from the ray origin (in world space) to consider an intersection point a "hit".</param>
        /// <param name="layerMask">The gameobject layers to include, defaults to "All Layers".</param>
        /// <param name="accuracy">The accuracy of the input source, defaults to <see cref="InputAccuracy.High"/>.</param>
        /// <seealso cref="Point(Sphere, uint, object, int, InputAccuracy)"/>
        /// <seealso cref="Raycast(Ray, out UIBlockHit, float, int)"/>
        /// <seealso cref="RaycastAll(Ray, List{UIBlockHit}, float, int)"/>
        /// <seealso cref="SphereCollide(Sphere, out UIBlockHit, int)"/>
        /// <seealso cref="SphereCollideAll(Sphere, List{UIBlockHit}, int)"/>
        /// <seealso cref="Cancel(Update)"/>
        public static void Point(Update update, bool pointerDown, bool allowDrag = true, float maxDistance = float.PositiveInfinity, int layerMask = AllLayers, InputAccuracy accuracy = InputAccuracy.High)
        {
            // If the pointer is up, we only want hits for what's actually intersected by the ray.
            // If the pointer is down, we can try to maintain the active gesture target.
            InteractionType hitTestInteraction = pointerDown ? InteractionType.Default : InteractionType.RequiresHit;
            GetHits<bool>(update, maxDistance, layerMask, blockHitCache, hitTestInteraction);

            // For updating the input router, we want that "real" interaction type
            InteractionType gestureInteraction = allowDrag ? InteractionType.Gesturable : InteractionType.Default;
            UpdateInternal(ref update, blockHitCache, pointerDown, gestureInteraction, noisy: accuracy == InputAccuracy.Low);
        }

        /// <summary>
        /// Performs a raycast with the provided <see cref="Update.Ray"/> and routes the <paramref name="scroll"/> update to the collided <see cref="UIBlock"/>.
        /// </summary>
        /// <remarks>Scroll updates are only delivered to <see cref="UIBlock"/>s with an attached <see cref="Scroller"/> component.</remarks>
        /// <param name="update">The data tied to this interaction update. <see cref="Update.Ray"/> is in world space.</param>
        /// <param name="scroll">A normalized scroll vector</param>
        /// <param name="maxDistance">The max distance from the ray origin (in world space) to consider an intersection point a "hit".</param>
        /// <param name="layerMask">The gameobject layers to include, defaults to "All Layers".</param>
        /// <param name="accuracy">The accuracy of the input source, defaults to <see cref="InputAccuracy.High"/>.</param>
        public static void Scroll(Update update, Vector3 scroll, float maxDistance = float.PositiveInfinity, int layerMask = AllLayers, InputAccuracy accuracy = InputAccuracy.High)
        {
            GetHits<UniqueValue<Vector3>>(update, maxDistance, layerMask, blockHitCache, InteractionType.RequiresHit);
            UpdateInternal(ref update, blockHitCache, new UniqueValue<Vector3>(ref scroll), InteractionType.RequiresHit, noisy: accuracy == InputAccuracy.Low);
        }

        /// <summary>
        /// Cancel the most recent/current gesture triggered by the input control mapped to <paramref name="update"/>.<see cref="Update.ControlID">ControlID</see>.
        /// </summary>
        /// <param name="update">The data tied to this interaction update. <see cref="Update.Ray"/> is in world space.</param>
        public static void Cancel(Update update)
        {
            Router.Cancel(update.ToInternal());
        }

        /// <summary>
        /// Cancel the recent/current gesture triggered by the input control mapped to <paramref name="controlID"/>.
        /// </summary>
        /// <param name="controlID">The unique identifier of the input control whose gesture the caller wishes to cancel.</param>
        public static void Cancel(uint controlID = 0)
        {
            Router.Cancel(new Internal.Interaction(controlID));
        }

        #region Internal API
        private static void HitTestAll(StructuredSphere sphere, List<HitTestResult> results, List<UIBlockHit> hits, int layerMask)
        {
            results.Clear();
            hits.Clear();

            InputEngine.Instance.HitTestAll(sphere, results, layerMask);

            for (int i = 0; i < results.Count; ++i)
            {
                HitTestResult result = results[i];

                hits.Add(new UIBlockHit()
                {
                    UIBlock = result.HitBlock as UIBlock,
                    Position = result.HitPoint,
                    Normal = result.Normal,
                });
            }
        }

        private static bool HitTestAll(Ray ray, List<HitTestResult> results, List<UIBlockHit> hits, float maxDistance, int layerMask)
        {
            results.Clear();
            hits.Clear();
            InputEngine.Instance.HitTestAll(ray, results, maxDistance, layerMask);

            for (int i = 0; i < results.Count; ++i)
            {
                HitTestResult result = results[i];

                hits.Add(new UIBlockHit()
                {
                    UIBlock = result.HitBlock as UIBlock,
                    Position = result.HitPoint,
                    Normal = result.Normal
                });
            }

            return hits.Count > 0;
        }

        /// <summary>
        /// Does a hit test all and then filters the results to hit blocks that capture input of type TInput
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <param name="interaction"></param>
        /// <param name="maxDistance"></param>
        /// <param name="hits"></param>
        private static void GetHits<TInput>(Update interaction, float maxDistance, int layerMask, List<UIBlockHit> hits, InteractionType interactionType) where TInput : unmanaged, System.IEquatable<TInput>
        {
            uint controlID = interaction.ControlID;
            if (controlID < 0 || controlID >= InputRouter.MaxControls)
            {
                throw new System.ArgumentException($"Invalid Control ID [{controlID}]. Expected within range [0, {InputRouter.MaxControls}]");
            }

            if (HitTestAll(interaction.Ray, hitTestCache, hits, maxDistance, layerMask))
            {
                _ = FilterHitsForInputType<TInput>(hits);
            }

            if (hits.Count > 0 || interactionType == InteractionType.RequiresHit)
            {
                // Got valid hits, or the interaction requires a hit
                return;
            }

            if (!Router.TryGetCurrentCapturingInput<TInput>(controlID, out UIBlockHit latestHit))
            {
                // We didn't get a hit, and there is nothing currently capturing input
                return;
            }

            Plane nearPlane = new Plane(latestHit.Normal, latestHit.Position);
            Vector3 hitPointOnPlane = latestHit.Position;

            if (nearPlane.Raycast(interaction.Ray, out float distance))
            {
                hitPointOnPlane = interaction.Ray.GetPoint(Math.MinAbs(distance, maxDistance));
            }

            hits.Add(new UIBlockHit() { Normal = nearPlane.normal, Position = hitPointOnPlane, UIBlock = latestHit.UIBlock });
        }

        /// <summary>
        /// Performs a hit test in the scene and updates
        /// the hit element with the provided input value
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <param name="ray"></param>
        /// <param name="value"></param>
        private static void UpdateInternal<TInput>(ref Update interaction, List<UIBlockHit> hits, TInput input, InteractionType interactionType, bool noisy) where TInput : unmanaged, System.IEquatable<TInput>
        {
            Router.Update(interaction.ToInternal(), input, sortedHits: hits.ToReadOnly(), interactionType, noisy);
        }

        private static bool2 FilterHitsForInputType<T>(List<UIBlockHit> hitsToFilter, UIBlock target = null) where T : unmanaged, System.IEquatable<T>
        {
            bool hitTarget = false;

            for (int i = hitsToFilter.Count - 1; i >= 0; --i)
            {
                UIBlockHit blockHit = hitsToFilter[i];

                if (!blockHit.UIBlock.CapturesInput<T>())
                {
                    hitsToFilter.RemoveAt(i);
                    continue;
                }

                hitTarget |= blockHit.UIBlock == target;

                if (i < hitsToFilter.Count - 1)
                {
                    IGestureRecognizer recognizer = blockHit.UIBlock.InputTarget.GestureRecognizer;

                    if (recognizer != null && recognizer.ObstructDrags)
                    {
                        hitsToFilter.RemoveRange(i + 1, hitsToFilter.Count - i - 1);
                    }
                }
            }

            return new bool2(hitsToFilter.Count > 0, hitTarget && target != null);
        }

        private static ReadOnlyList<UIBlockHit> GetFirstHitTargetForInputType<T>(List<UIBlockHit> hitsToFilter) where T : unmanaged, System.IEquatable<T>
        {
            UIBlockHit firstHit = default;

            for (int i = 0; i < hitsToFilter.Count; ++i)
            {
                UIBlockHit blockHit = hitsToFilter[i];

                if (blockHit.UIBlock.CapturesInput<T>())
                {
                    firstHit = blockHit;
                    break;
                }
            }

            hitsToFilter.Clear();

            if (firstHit.UIBlock != null)
            {
                hitsToFilter.Add(firstHit);
            }

            return hitsToFilter.ToReadOnly();
        }
        #endregion
    }
}
