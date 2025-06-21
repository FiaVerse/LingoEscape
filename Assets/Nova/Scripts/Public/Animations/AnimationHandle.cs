// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Animations;
using System;

namespace Nova
{
    /// <summary>
    /// A unique identifier of a scheduled <see cref="IAnimation"/>, <see cref="IAnimationWithEvents"/>, or any sequence/combination of the two
    /// </summary>
    /// <remarks>
    /// <list type="table">
    /// <item><term>Chain</term> <description><see cref="AnimationHandleExtensions.Chain{T}(AnimationHandle, T, float, int)"/></description></item>
    /// <item><term>Include</term> <description><see cref="AnimationHandleExtensions.Include{T}(AnimationHandle, T)"/></description></item>
    /// <item><term>Pause</term> <description><see cref="AnimationHandleExtensions.Pause(AnimationHandle)"/></description></item>
    /// <item><term>Resume</term> <description><see cref="AnimationHandleExtensions.Resume(AnimationHandle)"/></description></item>
    /// <item><term>Cancel</term> <description><see cref="AnimationHandleExtensions.Cancel(AnimationHandle)"/></description></item>
    /// <item><term>Complete</term> <description><see cref="AnimationHandleExtensions.Complete(AnimationHandle)"/></description></item>
    /// </list>
    /// </remarks>
    public readonly struct AnimationHandle : IEquatable<AnimationHandle>
    {
        /// <summary>
        /// A constant value used to indicate a single animation iteration
        /// </summary>
        public const int Once = 1;

        /// <summary>
        /// A constant value used to indicate an indefinitely looping animation
        /// </summary>
        public const int Infinite = -1;

        internal readonly AnimationID ID;

        private AnimationHandle(AnimationID id)
        {
            ID = id;
        }

        internal static AnimationHandle Create(AnimationID id)
        {
            return new AnimationHandle(id);
        }

        /// <summary>
        /// Equality operator
        /// </summary>
        /// <param name="lhs">Left hand side</param>
        /// <param name="rhs">Right hand side</param>
        /// <returns><see langword="true"/> if lhs and rhs are equal</returns>
        public static bool operator ==(AnimationHandle lhs, AnimationHandle rhs)
        {
            return lhs.ID.Equals(rhs.ID);
        }

        /// <summary>
        /// Inequality operator
        /// </summary>
        /// <param name="lhs">Left hand side</param>
        /// <param name="rhs">Right hand side</param>
        /// <returns><see langword="true"/> if lhs and rhs are <b>not</b> equal</returns>
        public static bool operator !=(AnimationHandle lhs, AnimationHandle rhs)
        {
            return !lhs.ID.Equals(rhs.ID);
        }

        /// <summary>
        /// Equality compare
        /// </summary>
        /// <param name="other">The other <see cref="AnimationHandle"/> to compare</param>
        /// <returns><see langword="true"/> if <c>this == other</c></returns>
        public override bool Equals(object pther)
        {
            if (pther is AnimationHandle handle)
            {
                return ID.Equals(handle.ID);
            }
            return false;
        }

        /// <summary>
        /// Get the hashcode for this <see cref="AnimationHandle"/>
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }

        /// <summary>
        /// Equality compare
        /// </summary>
        /// <param name="other">The other <see cref="AnimationHandle"/> to compare</param>
        /// <returns><see langword="true"/> if <c>this == other</c></returns>
        public bool Equals(AnimationHandle other)
        {
            return ID.Equals(other.ID);
        }
    }
}
