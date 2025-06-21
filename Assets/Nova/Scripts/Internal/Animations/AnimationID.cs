// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using System;
using System.Runtime.CompilerServices;

namespace Nova.Internal.Animations
{
    internal readonly struct AnimationID : IEquatable<AnimationID>
    {
        public static readonly AnimationID Comparer = default;

        public readonly UID<AnimationID> ID;
        public readonly bool IsGroupID;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator UID<AnimationID>(AnimationID id)
        {
            return id.ID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator AnimationID(UID<AnimationID> id)
        {
            return ForIndividual(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AnimationID(UID<AnimationID> id, bool group)
        {
            ID = id;
            IsGroupID = group;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AnimationID ForIndividual(UID<AnimationID> id)
        {
            return new AnimationID(id, group: false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AnimationID CreateGroupID()
        {
            return new AnimationID(UID<AnimationID>.Create(), group: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(AnimationID other)
        {
            return ID.Equals(other.ID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + ID.GetHashCode();
            hash = (hash * 7) + IsGroupID.GetHashCode();
            return hash;
        }
    }
}