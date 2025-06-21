// Copyright (c) Supernova Technologies LLC
using System;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// Defines the behavior of an element when it's selected.
    /// </summary>
    public enum SelectBehavior
    {
        /// <summary>
        /// Treat Select events as click events.
        /// </summary>
        Click,

        /// <summary>
        /// Push the selected element onto the selection stack and convert
        /// subsequent navigation move input into corresponding directed input
        /// events until the element is deselected.
        /// </summary>
        FireEvents,

        /// <summary>
        /// Push the selected element onto the selection stack and scope
        /// subsequent navigation events to this element's descendant hierarchy
        /// until it's deselected.
        /// </summary>
        ScopeNavigation
    }

    /// <summary>
    /// Defines a <see cref="NavLink"/> for each axis-aligned direction.
    /// </summary>
    [Serializable]
    public struct NavNode : IEquatable<NavNode>
    {
        /// <summary>
        /// A <see cref="NavNode"/> where all X/Y <see cref="NavLink"/>s are set to <see cref="NavLink.Auto"/> and Z <see cref="NavLink"/>s are set to <see cref="NavLink.Empty"/>.
        /// </summary>
        public static readonly NavNode TwoD = new NavNode()
        {
            Left = NavLink.Auto,
            Right = NavLink.Auto,
            Down = NavLink.Auto,
            Up = NavLink.Auto,
            Forward = NavLink.Empty,
            Back = NavLink.Empty
        };

        /// <summary>
        /// The <see cref="NavLink"/> to use when navigating to the left of this <see cref="NavNode"/>.
        /// </summary>
        [SerializeField]
        public NavLink Left;
        /// <summary>
        /// The <see cref="NavLink"/> to use when navigating to the right of this <see cref="NavNode"/>.
        /// </summary>
        [SerializeField]
        public NavLink Right;
        /// <summary>
        /// The <see cref="NavLink"/> to use when navigating downward from this <see cref="NavNode"/>.
        /// </summary>
        [SerializeField]
        public NavLink Down;
        /// <summary>
        /// The <see cref="NavLink"/> to use when navigating upward from this <see cref="NavNode"/>.
        /// </summary>
        [SerializeField]
        public NavLink Up;
        /// <summary>
        /// The <see cref="NavLink"/> to use when navigating forward from this <see cref="NavNode"/>.
        /// </summary>
        [SerializeField]
        public NavLink Forward;
        /// <summary>
        /// The <see cref="NavLink"/> to use when navigating backward from this <see cref="NavNode"/>.
        /// </summary>
        [SerializeField]
        public NavLink Back;

        /// <summary>
        /// Converts the provided <paramref name="direction"/> into the nearest axis-aligned 
        /// direction (e.g. Left, Down, Forward, etc.) and returns the <see cref="NavLink"/>
        /// corresponding to that axis-aligned direction.
        /// </summary>
        /// <param name="direction">The approximate direction vector of the desired <see cref="NavLink"/> to retrieve.</param>
        /// <returns>The <see cref="NavLink"/> in the given <paramref name="direction"/>.</returns>
        public NavLink GetLink(Vector3 direction)
        {
            NavLink link = default;

            if (direction == Vector3.left)
            {
                link = Left;
            }
            else if (direction == Vector3.down)
            {
                link = Down;
            }
            else if (direction == Vector3.back)
            {
                link = Back;
            }
            else if (direction == Vector3.right)
            {
                link = Right;
            }
            else if (direction == Vector3.up)
            {
                link = Up;
            }
            else if (direction == Vector3.forward)
            {
                link = Forward;
            }
            else
            {
                float angleInZ = Mathf.Atan(direction.y / direction.z) * Mathf.Rad2Deg;

                if (angleInZ <= 45 && angleInZ >= -45)
                {
                    link = Forward;
                }
                else if (angleInZ >= 135 || angleInZ <= -135)
                {
                    link = Back;
                }
                else
                {
                    switch (Mathf.Atan(direction.y / direction.x) * Mathf.Rad2Deg)
                    {
                        case float angle when angle < 45 && angle >= -45:
                            link = Right;
                            break;
                        case float angle when angle < 135 && angle >= 45:
                            link = Up;
                            break;
                        case float angle when angle < -135 || angle >= 135:
                            link = Left;
                            break;
                        case float angle when angle < -45 && angle >= -135:
                            link = Down;
                            break;
                    }
                }
            }

            return link;
        }

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="lhs">Left hand side.</param>
        /// <param name="rhs">Right hand side.</param>
        /// <returns><c>true</c> if all fields of <paramref name="lhs"/> are equal to their respective fields of <paramref name="rhs"/>.</returns>
        public static bool operator ==(NavNode lhs, NavNode rhs)
        {
            return lhs.Left == rhs.Left &&
                   lhs.Right == rhs.Right &&
                   lhs.Down == rhs.Down &&
                   lhs.Up == rhs.Up &&
                   lhs.Forward == rhs.Forward &&
                   lhs.Back == rhs.Back;
        }

        /// <summary>
        /// Inequality operator.
        /// </summary>
        /// <param name="lhs">Left hand side.</param>
        /// <param name="rhs">Right hand side.</param>
        /// <returns><c>true</c> if any fields of <paramref name="lhs"/> are <b>not</b> equal to their respective fields of <paramref name="rhs"/>.</returns>
        public static bool operator !=(NavNode lhs, NavNode rhs)
        {
            return lhs.Left != rhs.Left ||
                   lhs.Right != rhs.Right ||
                   lhs.Down != rhs.Down ||
                   lhs.Up != rhs.Up ||
                   lhs.Forward != rhs.Forward ||
                   lhs.Back != rhs.Back;
        }

        /// <summary>
        /// Equality compare.
        /// </summary>
        /// <param name="other">The <see cref="NavNode"/> to compare.</param>
        /// <returns><c>true</c> if <c>this == <paramref name="other"/></c>.</returns>
        public bool Equals(NavNode other)
        {
            return this == other;
        }

        /// <summary>
        /// Equality compare.
        /// </summary>
        /// <param name="other">The <see cref="NavNode"/> to compare.</param>
        /// <returns><c>true</c> if <c>this == <paramref name="other"/></c>.</returns>
        public override bool Equals(object other)
        {
            return other is NavNode node && Equals(node);
        }

        /// <summary>The hashcode for this <see cref="NavNode"/></summary>
        public override int GetHashCode()
        {
            int hash = 13;

            hash = (hash * 7) + Left.GetHashCode();
            hash = (hash * 7) + Right.GetHashCode();
            hash = (hash * 7) + Down.GetHashCode();
            hash = (hash * 7) + Up.GetHashCode();
            hash = (hash * 7) + Forward.GetHashCode();
            hash = (hash * 7) + Back.GetHashCode();

            return hash;
        }
    }

    internal static class NavNodeExtensions
    {
        public static bool TryGetNavigation(ref this NavNode node, Vector3 direction, out IUIBlock toUIBlock)
        {
            toUIBlock = null;

            NavLink link = node.GetLink(direction);

            if (link.TryGetTarget(out UIBlock target))
            {
                toUIBlock = target;
            }

            return link.Type == NavLinkType.Manual;
        }
    }
}
