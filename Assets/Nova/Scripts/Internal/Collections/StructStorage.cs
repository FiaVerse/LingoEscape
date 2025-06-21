// Copyright (c) Supernova Technologies LLC
using System;
using System.Collections.Generic;

namespace Nova.Internal.Collections
{
    internal struct UID<T> : IEquatable<UID<T>>
    {
        internal static readonly UID<T> Invalid = default;

        private static long IDCounter = 1;
        
        [NonSerialized]
        private long id;

        public bool IsValid => id != Invalid.id;

        public static UID<T> Create()
        {
            return new UID<T>()
            {
                id = IDCounter++
            };
        }

        public bool Equals(UID<T> other)
        {
            return id == other.id;
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

        public override string ToString()
        {
            return $"UID<{typeof(T).Name}>({id})";
        }
    }

    internal static class StructHandle<T>
    {
        internal interface IStructWrapper
        {
            void Remove(UID<T> instanceID);
        }

        [NonSerialized]
        private static Dictionary<Type, IStructWrapper> storedTypes = new Dictionary<Type, IStructWrapper>();
        [NonSerialized]
        private static Dictionary<UID<T>, IStructWrapper> instanceIDs = new Dictionary<UID<T>, IStructWrapper>();

        public static void Remove(UID<T> instanceID)
        {
            if (!instanceIDs.TryGetValue(instanceID, out IStructWrapper storage))
            {
                return;
            }

            storage.Remove(instanceID);
        }

        internal sealed class Storage<U> : IStructWrapper where U : struct
        {
            [NonSerialized]
            private static Dictionary<UID<T>, U> typedInstances = new Dictionary<UID<T>, U>();

            void IStructWrapper.Remove(UID<T> instanceID) => Remove(instanceID);

            public static UID<T> Add(U value)
            {
                UID<T> structID = UID<T>.Create();

                typedInstances.Add(structID, value);

                Type type = typeof(U);

                if (!storedTypes.TryGetValue(type, out IStructWrapper wrapper))
                {
                    wrapper = new Storage<U>();
                    storedTypes.Add(type, wrapper);
                }

                instanceIDs.Add(structID, wrapper);

                return structID;
            }

            public static void Remove(UID<T> structID)
            {
                typedInstances.Remove(structID);
                instanceIDs.Remove(structID);
            }

            public static U Get(UID<T> structID)
            {
                if (typedInstances.TryGetValue(structID, out U value))
                {
                    return value;
                }

                return default;
            }

            public static bool TryGetValue(UID<T> structID, out U value)
            {
                return typedInstances.TryGetValue(structID, out value);
            }

            public static void Set(UID<T> structID, U value)
            {
                if (!typedInstances.TryGetValue(structID, out U _))
                {
                    return;
                }

                typedInstances[structID] = value;
            }
        }
    }
}
