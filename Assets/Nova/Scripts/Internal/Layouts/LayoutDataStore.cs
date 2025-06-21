// Copyright (c) Supernova Technologies LLC
//#define AGGRESSIVE_INDEX_GETTERS

using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Nova.Internal.Layouts
{
    internal struct SizeOverride
    {
        public float3 Size;
        public bool3 Mask;
    }

    internal struct Axes
    {
        public Axis Primary;
        public Axis Cross;
    }

    internal unsafe partial class LayoutDataStore : TransformDataStore<LayoutDataStore, ILayoutBlock>
    {
        public struct UpdateScope : System.IDisposable
        {

            public UpdateScope(object _)
            {
                Instance.BeginUpdate();
            }

            public void Dispose()
            {
                Instance.EndUpdate();
            }
        }

        public NativeList<Length3> LengthConfigs;
        public NativeList<Length3.MinMax> LengthMinMaxes;
        public NativeList<Length3.Calculated> CalculatedLengths;

        public NativeList<bool> UseRotations;
        public NativeList<float3> Alignments;
        public NativeList<AutoSize3> AutoSizes;

        public NativeList<AutoLayout> AutoLayouts;
        public NativeList<Length2.Calculated> CalculatedSpacing;

        public NovaHashMap<DataStoreIndex, Axes> FormerAutoLayoutAxes;
        public NovaHashMap<DataStoreID, SizeOverride> ShrinkSizeOverrides;
        public NativeList<float3> ParentSizes;

        public NativeList<float3> DirectContentSizes;
        public NativeList<float3> DirectContentOffsets;
        public NativeList<float3> TotalContentSizes;
        public NativeList<float3> TotalContentOffsets;

        public NativeList<float3> TransformLocalPositions;
        public NativeList<quaternion> TransformLocalRotations;
        public NativeList<float3> TransformLocalScales;
        public NativeList<bool> UsingTransformPositions;

        public NativeList<AspectRatio> AspectRatios;

        public NativeList<SpatialPartitionMask> SpatialPartitions;

        public NativeBitList ReceivedFullEngineUpdate;

        private static LayoutPointer EmptyProperties = default;
        [FixedAddressValueType]
        private static AutoLayout EmptyAuto;
        [FixedAddressValueType]
        private static Layout EmptyLayout;

        private static CalculatedLayout EmptyCalculations = default;
        private static Length2.Calculated EmptySpacing = default;

        private Length3* lengthPropertiesPtr;
        private Length3.MinMax* rangePropertiesPtr;
        private Length3.Calculated* calcPropertiesPtr;
        private Length2.Calculated* calcSpacingPtr;

        private bool* useRotationsPtr;
        private float3* alignmentsPtr;
        private AutoSize3* autosizesPtr;
        private AutoLayout* autoLayoutsPtr;
        private AspectRatio* aspectRatiosPtr;

        protected override bool TryGetIndex(DataStoreID id, out DataStoreIndex index) => HierarchyDataStore.Instance.IDToIndexMap.TryGetIndex(id, out index);
        protected override DataStoreID GetID(DataStoreIndex index) => HierarchyDataStore.Instance.IDToIndexMap.ToID(index);
        public ref NativeList<HierarchyDependency> DirtyDependencies => ref HierarchyDataStore.Instance.BatchGroupTracker.DirtyDependencies;

        [FixedAddressValueType]
        private static int accessedLayoutCount = 0;
        public NativeReference<UnsafeAtomicCounter32> NumLayoutsAccessed;
        private NativeReference<bool> elementsDirtiedInPreUpdate;
        private NativeReference<int> numAccessedBeforePreEngineUpdate;

        public NovaHashMap<DataStoreIndex, DataStoreID> AncestorBuffer;
        public NativeList<LayoutPointer> AccessedLayouts;
        public NativeList<DataStoreIndex> DirtyIndices;
        public NativeReference<bool> NeedsSecondPass;

        public override bool IsDirty => elementsDirtiedInPreUpdate.Value || LayoutPropertiesNeedUpdate;
        public bool LayoutPropertiesNeedUpdate => DirtyIndices.Length > 0;
        public int KnownDirtyCount => DirtyIndices.Length;

        public bool LayoutsNeedSecondPass => NeedsSecondPass.Value;

        [FixedAddressValueType]
        private static LayoutCore.Register registerRunner;
        private static BurstedMethod<BurstMethod> register;
        [FixedAddressValueType]
        private static LayoutCore.Unregister unregisterRunner;
        private static BurstedMethod<BurstMethod> unregister;
        [FixedAddressValueType]
        private LayoutCore.ReleaseHandles releaseHandlesRunner;
        private static BurstedMethod<BurstMethod> releaseHandles;
        [FixedAddressValueType]
        private LayoutCore.CopyToPointer copyToPointerRunner;
        private static BurstedMethod<BurstMethod> copyToPointer;

        private LayoutCore.DiffAndDirty diffAndDirtyLayoutsRunner;
        private LayoutCore.FilterCleanElements filterCleanElementsRunner;
        private LayoutCore.DirtyDependencies dirtyLayoutDependenciesRunner;

        private bool engineUpdateRunning = false;

        [FixedAddressValueType]
        private static int modifiedTransformCount = 0;
        public NativeReference<UnsafeAtomicCounter32> NumTransformsModified;

        // If true, then one or more UIBlock.transform.localPosition was applied to
        // UIBlock.Layout.Position, so we should call Undo.RecordObject(UIBlock).
        // before calling CopyFromDataStore. Currently the only place this is read
        // is in the UIBlock editors, so we don't call Undo.RecordObject per object in the
        // Selection because the editor can batch it via Undo.RecordObjects(targets).
        public bool TransformsWereModified => modifiedTransformCount > 0;

        public PreviewSizeManager Previews { get; } = new PreviewSizeManager();

        public JobHandle PreProcessDirtyState(JobHandle dependency)
        {
            return PreUpdate(dependency);
        }

        public JobHandle PreUpdate(JobHandle dependency)
        {
            BeginUpdate();

            JobHandle diffAndDirty = diffAndDirtyLayoutsRunner.NovaScheduleByRef(Elements.Count, EngineBase.EqualWorkBatchSize, dependency);
            JobHandle filter = filterCleanElementsRunner.NovaScheduleByRef(diffAndDirty);
            return dirtyLayoutDependenciesRunner.NovaScheduleByRef(filter);
        }

        public void BeginUpdate()
        {
            engineUpdateRunning = true;
            NeedsSecondPass.Value = false;
        }

        public void EndUpdate()
        {
            engineUpdateRunning = false;
            NeedsSecondPass.Value = false;
        }

        /// <summary>
        /// Called by the layout engine before it schedules some jobs which may dirty more layout elements.
        /// This tells the data store to cache the currently tracked dirty count so it can be compared
        /// to later when setting the official dirty state for the given frame.
        /// </summary>
        public void CacheDirtyCount()
        {
            modifiedTransformCount = 0;
            numAccessedBeforePreEngineUpdate.Value = accessedLayoutCount;
        }

        public override void ClearDirtyState()
        {
            NumLayoutsAccessed.Ref().Reset();
            DirtyIndices.Clear();
            FormerAutoLayoutAxes.Clear();

            ReceivedFullEngineUpdate.SetAll(true);

            releaseHandles.Method.Invoke(UnsafeUtility.AddressOf(ref releaseHandlesRunner));

            unsafe
            {
                UnsafeUtility.MemSet(UsingTransformPositions.GetRawPtr(), 0, sizeof(bool) * UsingTransformPositions.Length);
                UnsafeUtility.MemSet(AccessedLayouts.GetRawPtr(), 0, sizeof(LayoutPointer) * AccessedLayouts.Length);
            }

            numAccessedBeforePreEngineUpdate.Value = 0;
            elementsDirtiedInPreUpdate.Value = false;

            if (NovaApplication.IsEditor)
            {
                Previews.EditorOnly_ClearDirtyState();
            }

            EndUpdate();
        }

        public void UpdateShrinkSizeOverride(ILayoutBlock val, float2 sizeOverride)
        {
            // whether we're mid engine update or mid user update (pre engine update), read 
            // from the Access points -- they have the latest user provided and calculated values
            ref Layout layout = ref UnsafeUtility.AsRef<Layout>(Access(val).Layout);
            bool2 overrides = layout.AutoSize.Shrink.xy;
            float2 previousSize = AccessCalc(val).Size.Value.xy;

            bool3 overrideMask = new bool3(overrides, false);
            bool overrideAny = math.any(overrides);
            bool dirty = math.any(previousSize != sizeOverride);

            if (dirty)
            {
                if (engineUpdateRunning)
                {
                    // mark data store dirty during mid engine update as needed
                    NeedsSecondPass.Value |= true;
                }

                // mark dirty dependencies since pre-update diff/dirty might have already run
                DirtyDependencies.ElementAt(val.Index) = HierarchyDependency.ParentAndChildren;
            }

            if (overrideAny)
            {
                layout.Size.XY.Raw = math.select(layout.Size.XY.Raw, sizeOverride, overrideMask.xy);
                layout.Size.X.Type = overrides.x ? LengthType.Value : layout.Size.X.Type;
                layout.Size.Y.Type = overrides.y ? LengthType.Value : layout.Size.Y.Type;

                ShrinkSizeOverrides[val.UniqueID] = new SizeOverride()
                {
                    Size = new float3(sizeOverride, 0),
                    Mask = overrideMask
                };
            }
            else
            {
                ShrinkSizeOverrides.Remove(val.UniqueID);
            }
        }

        protected override void CopyToStoreImpl(ILayoutBlock val)
        {
            if (engineUpdateRunning)
            {
                // mark dirty if paused mid-update and user is modifying from the editor
                NeedsSecondPass.Value |= true;
            }

            DataStoreIndex index = val.Index;
            if (!index.IsValid)
            {
                return;
            }

            ref LayoutPointer pointer = ref AccessedLayouts.ElementAt(index);

            EnsurePointerAccessed(val, ref pointer, out _);
        }

        protected override void CopyFromStoreImpl(ILayoutBlock val)
        {
            DataStoreIndex index = val.Index;

            if (!index.IsValid)
            {
                return;
            }

            _ = Access(val);
        }

        protected override void CloneImpl(ILayoutBlock source, ILayoutBlock destination)
        {
            DataStoreIndex index = source.Index;

            ref LayoutPointer sourcePtr = ref Access(source);

            UnsafeUtility.MemCpy(UnsafeUtility.AddressOf(ref destination.SerializedLayout), sourcePtr.Layout, sizeof(Layout));
            UnsafeUtility.MemCpy(UnsafeUtility.AddressOf(ref destination.SerializedAutoLayout), sourcePtr.AutoLayout, sizeof(AutoLayout));

            if (source as UnityEngine.MonoBehaviour != null && destination as UnityEngine.MonoBehaviour != null)
            {
                destination.PreviewSize = source.PreviewSize;
            }
        }

        public float3 GetContentSize(ILayoutBlock val)
        {
            DataStoreIndex index = val.Index;

            return DirectContentSizes[index];
        }

        public float3 GetContentCenter(ILayoutBlock val)
        {
            DataStoreIndex index = val.Index;

            return DirectContentOffsets[index];
        }

        public float3 GetHierarchySize(ILayoutBlock val)
        {
            DataStoreIndex index = val.Index;

            return TotalContentSizes[index];
        }

        public float3 GetHierarchyCenter(ILayoutBlock val)
        {
            DataStoreIndex index = val.Index;
            return TotalContentOffsets[index];
        }

        public float3 GetCalculatedTransformLocalPosition(ILayoutBlock val, bool excludeVirtualParentOffset = false)
        {
            DataStoreIndex index = val.Index;
            IHierarchyBlock parent = HierarchyDataStore.Instance.GetHierarchyParent(val.UniqueID);

            if (!index.IsValid)
            {
                return val.IsVirtual ? Math.float3_Zero : (float3)val.Transform.localPosition;
            }
            if (excludeVirtualParentOffset)
            {
                return TransformLocalPositions[index];
            }

            DataStoreIndex parentIndex = parent != null ? parent.Index : DataStoreIndex.Invalid;

            return parentIndex.IsValid && TransformProxies[parentIndex].IsVirtual ? TransformLocalPositions[index] + TransformLocalPositions[parentIndex] : TransformLocalPositions[index];
        }

        protected override void Add(ILayoutBlock val)
        {
            int transformIndex = base.AddNonNativeOnly(val);
            bool isVirtual = val.IsVirtual;

            registerRunner.TransformIndex = transformIndex;
            registerRunner.TransformIsVirtual = isVirtual;

            registerRunner.Layout = val.SerializedLayout;
            registerRunner.AutoLayout = val.SerializedAutoLayout;

            UnityEngine.Transform transform = val.Transform;

            registerRunner.TransformPosition = isVirtual ? UnityEngine.Vector3.zero : transform.localPosition;
            registerRunner.TransformRotation = isVirtual ? UnityEngine.Quaternion.identity : transform.localRotation;
            registerRunner.TransformScale = isVirtual ? UnityEngine.Vector3.one : transform.localScale;

            unsafe
            {
                register.Method.Invoke(UnsafeUtility.AddressOf(ref registerRunner));
            }

            autoLayoutsPtr = registerRunner.AutoLayoutsPtr;
            calcSpacingPtr = registerRunner.CalcSpacingPtr;

            lengthPropertiesPtr = registerRunner.LengthPropertiesPtr;
            rangePropertiesPtr = registerRunner.RangePropertiesPtr;
            calcPropertiesPtr = registerRunner.CalcPropertiesPtr;

            useRotationsPtr = registerRunner.UseRotationsPtr;
            alignmentsPtr = registerRunner.AlignmentsPtr;
            autosizesPtr = registerRunner.AutosizesPtr;
            aspectRatiosPtr = registerRunner.AspectRatiosPtr;

            TransformTracker.Add(val);
        }

        protected override void RemoveAtSwapBack(DataStoreID idToRemove, DataStoreIndex indexToRemove)
        {
            if (NovaApplication.IsEditor) // this check is redundant for Remove but avoids GetTransform
            {
                TransformTracker.Remove(idToRemove, GetTransform(indexToRemove));
            }

            unregisterRunner.IndexToRemove = indexToRemove;
            unregisterRunner.IDToRemove = idToRemove;

            unsafe
            {
                unregister.Method.Invoke(UnsafeUtility.AddressOf(ref unregisterRunner));
            }
        }

        public bool HasReceivedFullEngineUpdate(ILayoutBlock val)
        {
            DataStoreIndex index = val.Index;

            if (!index.IsValid)
            {
                return false;
            }

            return ReceivedFullEngineUpdate[index];
        }

        public void GetLayout(ILayoutBlock node, out LayoutAccess.Properties layout)
        {
            layout = LayoutAccess.GetUnsafe(node.Index, lengthPropertiesPtr);

            layout.WrapMinMaxes(rangePropertiesPtr);
            layout.WrapCalculated(calcPropertiesPtr);
            layout.WrapAutoSizes(autosizesPtr);
            layout.WrapAlignments(alignmentsPtr);
            layout.WrapUseRotations(useRotationsPtr);
            layout.WrapAspectRatios(aspectRatiosPtr);
        }

        public void SetLayoutDirty(ILayoutBlock val)
        {
            // passthrough marks dirty
            _ = Access(val);
        }

        public bool GetLayoutDirty(ILayoutBlock val)
        {
            DataStoreIndex layoutIndex = val.Index;

            if (!layoutIndex.IsValid)
            {
                return true;
            }

            return AccessedLayouts.ElementAt(layoutIndex).ID == val.UniqueID;
        }

        public ref LayoutPointer Access(ILayoutBlock val)
        {
            DataStoreIndex index = val.Index;

            if (!index.IsValid)
            {
                return ref EmptyProperties;
            }

            ref LayoutPointer pointer = ref AccessedLayouts.ElementAt(index);
            EnsurePointerAccessed(val, ref pointer, out bool wasAccessed);

            if (!wasAccessed)
            {
                copyToPointerRunner.IndexToCopy = index;
                copyToPointer.Method.Invoke(UnsafeUtility.AddressOf(ref copyToPointerRunner));
            }

            return ref pointer;
        }

        private void EnsurePointerAccessed(ILayoutBlock val, ref LayoutPointer pointer, out bool wasAccessed)
        {
            wasAccessed = true;
            DataStoreID id = val.UniqueID;

            if (pointer.ID == id)
            {
                return;
            }

            if (pointer.ID.IsValid)
            {
            }

            _ = UnsafeUtility.PinGCObjectAndGetAddress(val, out ulong gcHandle);

            pointer.ID = id;
            pointer.Layout = (Layout*)UnsafeUtility.AddressOf(ref val.SerializedLayout);
            pointer.AutoLayout = (AutoLayout*)UnsafeUtility.AddressOf(ref val.SerializedAutoLayout);
            pointer.GCHandle = gcHandle;

            wasAccessed = false;
            accessedLayoutCount++;
        }

        public ref CalculatedLayout AccessCalc(ILayoutBlock val)
        {
            if (!val.Index.IsValid)
            {
                return ref EmptyCalculations;
            }

            DataStoreIndex layoutIndex = val.Index;
            unsafe
            {
                return ref UnsafeUtility.AsRef<CalculatedLayout>(calcPropertiesPtr + (LayoutAccess.Length3SliceSize * layoutIndex));
            }
        }

        public ref Length2.Calculated AccessCalcSpacing(ILayoutBlock val)
        {
            if (!val.Index.IsValid)
            {
                return ref EmptySpacing;
            }

            DataStoreIndex layoutIndex = val.Index;
            unsafe
            {
                return ref UnsafeUtility.AsRef<Length2.Calculated>(calcSpacingPtr + layoutIndex);
            }
        }

        public ref AutoLayout AccessAutoLayoutReadOnly(ILayoutBlock val)
        {
            if (!val.Index.IsValid)
            {
                return ref UnsafeUtility.AsRef<AutoLayout>(EmptyProperties.AutoLayout);
            }

            DataStoreIndex layoutIndex = val.Index;
            ref LayoutPointer wrapper = ref AccessedLayouts.ElementAt(layoutIndex);

            if (wrapper.ID == val.UniqueID)
            {
                return ref UnsafeUtility.AsRef<AutoLayout>(wrapper.AutoLayout);
            }

            unsafe
            {
                return ref UnsafeUtility.AsRef<AutoLayout>(autoLayoutsPtr + layoutIndex);
            }
        }

        public override void Init()
        {
            base.Init();

            unsafe
            {
                register = new BurstedMethod<BurstMethod>(LayoutCore.Register.Run);
                unregister = new BurstedMethod<BurstMethod>(LayoutCore.Unregister.Run);
                releaseHandles = new BurstedMethod<BurstMethod>(LayoutCore.ReleaseHandles.Run);
                copyToPointer = new BurstedMethod<BurstMethod>(LayoutCore.CopyToPointer.Run);
            }

            unsafe
            {
                EmptyProperties.AutoLayout = (AutoLayout*)UnsafeUtility.AddressOf(ref EmptyAuto);
                EmptyProperties.Layout = (Layout*)UnsafeUtility.AddressOf(ref EmptyLayout);
            }

            elementsDirtiedInPreUpdate = new NativeReference<bool>(Allocator.Persistent);
            numAccessedBeforePreEngineUpdate = new NativeReference<int>(Allocator.Persistent);

            AutoLayouts = new NativeList<AutoLayout>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            CalculatedSpacing = new NativeList<Length2.Calculated>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            FormerAutoLayoutAxes = new NovaHashMap<DataStoreIndex, Axes>(Constants.SomeElementsInitialCapacity, Allocator.Persistent);

            ShrinkSizeOverrides = new NovaHashMap<DataStoreID, SizeOverride>(Constants.SomeElementsInitialCapacity, Allocator.Persistent);

            autoLayoutsPtr = AutoLayouts.GetRawPtr();

            LengthConfigs = new NativeList<Length3>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            LengthMinMaxes = new NativeList<Length3.MinMax>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            CalculatedLengths = new NativeList<Length3.Calculated>(Constants.AllElementsInitialCapacity, Allocator.Persistent);

            UseRotations = new NativeList<bool>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            Alignments = new NativeList<float3>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            AutoSizes = new NativeList<AutoSize3>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            AspectRatios = new NativeList<AspectRatio>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            ParentSizes = new NativeList<float3>(Constants.AllElementsInitialCapacity, Allocator.Persistent);

            lengthPropertiesPtr = LengthConfigs.GetRawPtr();
            rangePropertiesPtr = (Length3.MinMax*)LengthConfigs.GetRawPtr();
            calcPropertiesPtr = (Length3.Calculated*)LengthConfigs.GetRawPtr();

            useRotationsPtr = UseRotations.GetRawPtr();
            alignmentsPtr = Alignments.GetRawPtr();
            autosizesPtr = AutoSizes.GetRawPtr();

            DirectContentSizes = new NativeList<float3>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            DirectContentOffsets = new NativeList<float3>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            TotalContentSizes = new NativeList<float3>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            TotalContentOffsets = new NativeList<float3>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            SpatialPartitions = new NativeList<SpatialPartitionMask>(Constants.AllElementsInitialCapacity, Allocator.Persistent);

            TransformLocalPositions = new NativeList<float3>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            TransformLocalRotations = new NativeList<quaternion>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            TransformLocalScales = new NativeList<float3>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            UsingTransformPositions = new NativeList<bool>(Constants.AllElementsInitialCapacity, Allocator.Persistent);

            AncestorBuffer = new NovaHashMap<DataStoreIndex, DataStoreID>(Constants.SomeElementsInitialCapacity / 2, Allocator.Persistent);
            AccessedLayouts = new NativeList<LayoutPointer>(Constants.AllElementsInitialCapacity, Allocator.Persistent);

            DirtyIndices = new NativeList<DataStoreIndex>(Constants.AllElementsInitialCapacity / 2, Allocator.Persistent);
            NumLayoutsAccessed = new NativeReference<UnsafeAtomicCounter32>(new UnsafeAtomicCounter32(UnsafeUtility.AddressOf(ref accessedLayoutCount)), Allocator.Persistent);

            ReceivedFullEngineUpdate = new NativeBitList(Constants.AllElementsInitialCapacity, Allocator.Persistent);

            NeedsSecondPass = new NativeReference<bool>(Allocator.Persistent);

            diffAndDirtyLayoutsRunner = new LayoutCore.DiffAndDirty()
            {
                LengthConfigs = LengthConfigs,
                LengthRanges = LengthMinMaxes,

                UseRotations = UseRotations,
                Alignments = Alignments,
                Autosizes = AutoSizes,
                AspectRatios = AspectRatios,

                AutoLayouts = AutoLayouts,
                FormerAutoLayoutAxes = FormerAutoLayoutAxes,
                TransformRotations = TransformLocalRotations,
                UsingTransformPosition = UsingTransformPositions,

                DirtyDependencies = DirtyDependencies,

                Hierarchy = HierarchyDataStore.Instance.Hierarchy,
                HierarchyLookup = HierarchyDataStore.Instance.HierarchyLookup,

                DirtyLayouts = AccessedLayouts,
            };

            NumTransformsModified = new NativeReference<UnsafeAtomicCounter32>(new UnsafeAtomicCounter32(UnsafeUtility.AddressOf(ref modifiedTransformCount)), Allocator.Persistent);
            Previews.Init(ref diffAndDirtyLayoutsRunner);

            dirtyLayoutDependenciesRunner = new LayoutCore.DirtyDependencies()
            {
                AutoSizes = AutoSizes,
                AutoLayouts = AutoLayouts,

                DirtyIndices = DirtyIndices,
                DirtyDependencyStates = DirtyDependencies,

                Hierarchy = HierarchyDataStore.Instance.Hierarchy,
                HierarchyLookup = HierarchyDataStore.Instance.HierarchyLookup,
            };

            filterCleanElementsRunner = new LayoutCore.FilterCleanElements()
            {
                DirtyIndices = DirtyIndices,
                DirtyDependencies = DirtyDependencies,
                ActiveDirtyCount = NumLayoutsAccessed,
                DirtyCountBeforeAnyUpdate = numAccessedBeforePreEngineUpdate,
                DirtyCountChanged = elementsDirtiedInPreUpdate,
            };

            registerRunner = new LayoutCore.Register()
            {
                LengthConfigs = LengthConfigs,
                LengthMinMaxes = LengthMinMaxes,
                CalculatedLengths = CalculatedLengths,

                UseRotations = UseRotations,
                Alignments = Alignments,
                AutoSizes = AutoSizes,

                AutoLayouts = AutoLayouts,
                CalculatedSpacing = CalculatedSpacing,
                FormerAutoLayoutAxes = FormerAutoLayoutAxes,
                ParentSizes = ParentSizes,

                DirectContentSizes = DirectContentSizes,
                DirectContentOffsets = DirectContentOffsets,
                TotalContentSizes = TotalContentSizes,
                TotalContentOffsets = TotalContentOffsets,

                TransformLocalPositions = TransformLocalPositions,
                TransformLocalRotations = TransformLocalRotations,
                TransformLocalScales = TransformLocalScales,
                UsingTransformPositions = UsingTransformPositions,

                AspectRatios = AspectRatios,

                SpatialPartitions = SpatialPartitions,

                AccessedLayouts = AccessedLayouts,
                ReceivedFullEngineUpdate = ReceivedFullEngineUpdate,

                LocalToWorldMatrices = LocalToWorldMatrices,
                WorldToLocalMatrices = WorldToLocalMatrices,
                TransformProxies = TransformProxies,
                PhysicalToSharedIndexMap = PhysicalToSharedIndexMap,
                VirtualToSharedIndexMap = VirtualToSharedIndexMap,
            };

            unregisterRunner = new LayoutCore.Unregister()
            {
                LengthConfigs = LengthConfigs,
                LengthMinMaxes = LengthMinMaxes,
                CalculatedLengths = CalculatedLengths,

                UseRotations = UseRotations,
                Alignments = Alignments,
                AutoSizes = AutoSizes,

                AutoLayouts = AutoLayouts,
                CalculatedSpacing = CalculatedSpacing,
                FormerAutoLayoutAxes = FormerAutoLayoutAxes,
                ParentSizes = ParentSizes,

                ShrinkSizeOverrides = ShrinkSizeOverrides,

                DirectContentSizes = DirectContentSizes,
                DirectContentOffsets = DirectContentOffsets,
                TotalContentSizes = TotalContentSizes,
                TotalContentOffsets = TotalContentOffsets,

                TransformLocalPositions = TransformLocalPositions,
                TransformLocalRotations = TransformLocalRotations,
                TransformLocalScales = TransformLocalScales,
                UsingTransformPositions = UsingTransformPositions,

                LocalToWorldMatrices = LocalToWorldMatrices,
                WorldToLocalMatrices = WorldToLocalMatrices,
                TransformProxies = TransformProxies,
                PhysicalToSharedIndexMap = PhysicalToSharedIndexMap,
                VirtualToSharedIndexMap = VirtualToSharedIndexMap,
                PhysicalTransforms = PhysicalTransforms,

                AccessedLayouts = AccessedLayouts,
                AccessedLayoutCount = NumLayoutsAccessed,

                AspectRatios = AspectRatios,

                SpatialPartitions = SpatialPartitions,

                ReceivedFullEngineUpdate = ReceivedFullEngineUpdate,
            };

            releaseHandlesRunner = new LayoutCore.ReleaseHandles()
            {
                Handles = AccessedLayouts,
            };

            copyToPointerRunner = new LayoutCore.CopyToPointer()
            {
                AccessedLayouts = AccessedLayouts,

                Lengths = LengthConfigs,
                Ranges = LengthMinMaxes,
                UseRotations = UseRotations,
                Alignments = Alignments,
                AutoSizes = AutoSizes,
                AspectRatios = AspectRatios,

                AutoLayouts = AutoLayouts,
            };
        }

        public override void Dispose()
        {
            base.Dispose();

            AutoLayouts.Dispose();
            CalculatedSpacing.Dispose();
            FormerAutoLayoutAxes.Dispose();

            ShrinkSizeOverrides.Dispose();

            LengthConfigs.Dispose();
            LengthMinMaxes.Dispose();
            CalculatedLengths.Dispose();

            UseRotations.Dispose();
            Alignments.Dispose();
            AutoSizes.Dispose();
            AspectRatios.Dispose();
            ParentSizes.Dispose();

            DirectContentSizes.Dispose();
            DirectContentOffsets.Dispose();
            TotalContentSizes.Dispose();
            TotalContentOffsets.Dispose();
            SpatialPartitions.Dispose();

            TransformLocalPositions.Dispose();
            TransformLocalRotations.Dispose();
            TransformLocalScales.Dispose();
            UsingTransformPositions.Dispose();

            AncestorBuffer.Dispose();
            AccessedLayouts.Dispose();

            DirtyIndices.Dispose();
            NumLayoutsAccessed.Dispose();

            elementsDirtiedInPreUpdate.Dispose();
            numAccessedBeforePreEngineUpdate.Dispose();

            ReceivedFullEngineUpdate.Dispose();

            NeedsSecondPass.Dispose();

            NumTransformsModified.Dispose();
            Previews.Dispose();
        }
    }

    internal static class LengthListExtensions
    {
        public unsafe static void Add(ref this NativeList<Length3> lengths, LengthBounds bounds)
        {
            lengths.AddRange(UnsafeUtility.AddressOf(ref bounds), 2);
        }

        public static unsafe void Add(ref this NativeList<Length3.MinMax> ranges, LengthBounds.MinMax minMax)
        {
            ranges.AddRange(UnsafeUtility.AddressOf(ref minMax), 2);
        }

        public static unsafe void AddEmpty(ref this NativeList<Length3.Calculated> ranges, int count)
        {
            ranges.Resize(ranges.Length + count, NativeArrayOptions.ClearMemory);
        }
    }
}
