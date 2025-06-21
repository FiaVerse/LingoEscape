// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using Unity.Burst;

namespace Nova.Internal.Hierarchy
{
    internal interface IDependencyDiffable<T> where T : IDependencyDiffable<T>
    {
        /// <summary>
        /// Object will diff its own values with the given value, update only the
        /// values that differ, and return the dependency type based on what changed
        /// </summary>
        /// <param name="valueToSet"></param>
        /// <returns></returns>
        HierarchyDependency ApplyDiff(ref T valueToSet);

        /// <summary>
        /// Objects will diff their own values with the given value and
        /// return the dependency based on how the two instances compare
        /// </summary>
        /// <param name="valueToSet"></param>
        /// <returns></returns>
        HierarchyDependency DependencyDiff(ref T valueToCompare);
    }

    [BurstCompile]
    internal struct HierarchyDependency
    {
        public static readonly HierarchyDependency None = 0;

        // Is dirty
        public static readonly HierarchyDependency Self = 1 << 0;
        
        // Dirties parent
        public static readonly HierarchyDependency Parent = 1 << 1;

        // Dirties parent and children
        public static readonly HierarchyDependency ParentAndChildren = 1 << 2;

        public static readonly HierarchyDependency MaxDependencies = ParentAndChildren;

        private byte dependency;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator HierarchyDependency(byte dependency)
        {
            return new HierarchyDependency() { dependency = dependency };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator byte(HierarchyDependency dependent)
        {
            return dependent.dependency;
        }

        public readonly bool IsDirty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => dependency != None.dependency;
        }

        public readonly bool HasDirectDependencies
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => dependency == Parent.dependency || dependency == ParentAndChildren.dependency;
        }

        public override string ToString()
        {
            string type = dependency == None ? "None" :
                          dependency == Self ? "Self" :
                          dependency == Parent ? "Parent" :
                          dependency == ParentAndChildren ? "ParentAndChildren" :
                          "Unknown";

            return $"Dependency.{type}";
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HierarchyDependency Max(HierarchyDependency a, HierarchyDependency b)
        {
            return a.dependency > b.dependency ? a : b;
        }
    }
}
