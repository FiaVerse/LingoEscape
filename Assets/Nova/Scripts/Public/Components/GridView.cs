// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// A callback to configure a <see cref="GridSlice"/>, <see cref="GridSlice2D"/> or <see cref="GridSlice3D"/> before it's inserted into the grid view.
    /// </summary>
    /// <param name="sliceIndex">The virtual index of the slice along the <paramref name="primaryAxis"/></param>
    /// <param name="gridView">The <see cref="GridView"/> requesting the grid slice.</param>
    /// <param name="gridSlice">The grid slice of type <typeparamref name="T"/> to configure. This slice will then be inserted into the grid.</param>
    /// <typeparam name="T">
    /// The type of grid slice the provider will configure:
    /// <list type="bullet">
    /// <item><description><see cref="GridSlice"/></description></item>
    /// <item><description><see cref="GridSlice2D"/></description></item>
    /// <item><description><see cref="GridSlice3D"/></description></item>
    /// </list>
    /// </typeparam>
    /// <seealso cref="GridView.SetSliceProvider(GridSliceProviderCallback{GridSlice})"/>
    /// <seealso cref="GridView.SetSliceProvider(GridSliceProviderCallback{GridSlice2D})"/>
    /// <seealso cref="GridView.SetSliceProvider(GridSliceProviderCallback{GridSlice3D})"/>
    public delegate void GridSliceProviderCallback<T>(int sliceIndex, GridView gridView, ref T gridSlice);

    /// <summary>
    /// A <see cref="ListView"/> whose list items can be arranged in a 2D layout.
    /// </summary>
    /// <remarks>
    /// Percent-based <see cref="Layout"/> properties on <see cref="UIBlock"/>s parented directly to a 
    /// <see cref="GridView"/> are calculated relative to their "virtual" parent <see cref="GridSlice"/>, 
    /// not the <see cref="GridView"/> itself.
    /// </remarks>
    [AddComponentMenu("Nova/Grid View")]
    [HelpURL("https://novaui.io/manual/GridView.html")]
    public sealed class GridView : ListView, ISerializationCallbackReceiver
    {
        #region Public
        /// <summary>
        /// The lowest slice index currently loaded into the grid view.
        /// </summary>
        public int MinLoadedSliceIndex => Grid.GetRowIndex(MinLoadedIndex);

        /// <summary>
        /// The highest slice index currently loaded into the grid view.
        /// </summary>
        public int MaxLoadedSliceIndex => Grid.GetRowIndex(MaxLoadedIndex);

        /// <summary>
        /// The number of elements to position along the <see cref="CrossAxis"/>.
        /// </summary>
        /// <seealso cref="CrossAxis"/>
        public int CrossAxisItemCount
        {
            get
            {
                return crossAxisItemCount;
            }
            set
            {
                int newItemCount = Mathf.Max(1, value);
                if (newItemCount == crossAxisItemCount)
                {
                    return;
                }

                crossAxisItemCount = newItemCount;

                if (!UIBlock.Activated)
                {
                    return;
                }

                RebalanceGrid();
            }
        }

        /// <summary>
        /// The scrolling axis to position elements in the grid. Assigned implicitly from the <see cref="AutoLayout.Axis"/> configured on the <see cref="ListView.UIBlock"/>.
        /// </summary>
        /// <seealso cref="CrossAxis"/>
        public Axis PrimaryAxis
        {
            get
            {
                return UIBlock.GetAutoLayoutReadOnly().Axis;
            }
            set
            {
                UIBlock.AutoLayout.Axis = value;
            }
        }

        /// <summary>
        /// The non-scrolling axis along which <see cref="GridSlice"/>'s will position items.
        /// </summary>
        /// <example>If the <see cref="PrimaryAxis"/> is set to Y, the <see cref="CrossAxis"/> is commonly set to X.</example>
        /// <seealso cref="CrossAxisItemCount"/>
        /// <seealso cref="PrimaryAxis"/>
        public Axis CrossAxis
        {
            get => crossAxis;
            set
            {
                if (crossAxis == value)
                {
                    return;
                }

                crossAxis = value;

                ReadOnlyList<VirtualBlock> virtualBlocks = VirtualBlocks;

                for (int i = 0; i < virtualBlocks.Count; ++i)
                {
                    VirtualUIBlock virtualBlock = virtualBlocks[i] as VirtualUIBlock;
                    virtualBlock.AutoLayout.Axis = crossAxis;
                }
            }
        }

        /// <summary>
        /// Assigns the given <see cref="GridSliceProviderCallback{T}"/> as the sole handler for any grid slice requests. As a <see cref="GridView"/> is scrolled, new list items will 
        /// be pulled into view, and <see cref="CrossAxisItemCount"/> list items will be visually parented to the returned <see cref="GridSlice"/>.
        /// </summary>
        /// <param name="provider"></param>
        /// <remarks>
        /// A <see cref="GridView"/> can only have a single <see cref="GridSliceProviderCallback{T}"/> at a given time, so assigning a value here will remove any existing slice providers.
        /// </remarks>
        /// <seealso cref="SetSliceProvider(GridSliceProviderCallback{GridSlice2D})"/>
        /// <seealso cref="SetSliceProvider(GridSliceProviderCallback{GridSlice3D})"/>
        public void SetSliceProvider(GridSliceProviderCallback<GridSlice> provider) => SetSliceProvider<GridSlice>(provider);

        /// <summary>
        /// Assigns the given <see cref="GridSliceProviderCallback{T}"/> as the sole handler for any grid slice requests. As a <see cref="GridView"/> is scrolled, new list items will 
        /// be pulled into view, and <see cref="CrossAxisItemCount"/> list items will be visually parented to the returned <see cref="GridSlice2D"/>.
        /// </summary>
        /// <param name="provider"></param>
        /// <remarks>
        /// A <see cref="GridView"/> can only have a single <see cref="GridSliceProviderCallback{T}"/> at a given time, so assigning a value here will remove any existing slice providers.
        /// </remarks>
        /// <seealso cref="SetSliceProvider(GridSliceProviderCallback{GridSlice})"/>
        /// <seealso cref="SetSliceProvider(GridSliceProviderCallback{GridSlice3D})"/>
        public void SetSliceProvider(GridSliceProviderCallback<GridSlice2D> provider) => SetSliceProvider<GridSlice2D>(provider);

        /// <summary>
        /// Assigns the given <see cref="GridSliceProviderCallback{T}"/> as the sole handler for any grid slice requests. As a <see cref="GridView"/> is scrolled, new list items will 
        /// be pulled into view, and <see cref="CrossAxisItemCount"/> list items will be visually parented to the returned <see cref="GridSlice3D"/>.
        /// </summary>
        /// <param name="provider"></param>
        /// <remarks>
        /// A <see cref="GridView"/> can only have a single <see cref="GridSliceProviderCallback{T}"/> at a given time, so assigning a value here will remove any existing slice providers.
        /// </remarks>
        /// <seealso cref="SetSliceProvider(GridSliceProviderCallback{GridSlice})"/>
        /// <seealso cref="SetSliceProvider(GridSliceProviderCallback{GridSlice2D})"/>
        public void SetSliceProvider(GridSliceProviderCallback<GridSlice3D> provider) => SetSliceProvider<GridSlice3D>(provider);

        /// <summary>
        /// Clears any <see cref="GridSliceProviderCallback{T}"/> callback previously assigned via 
        /// <list type="bullet">
        /// <item><description><see cref="SetSliceProvider(GridSliceProviderCallback{GridSlice})"/></description></item>
        /// <item><description><see cref="SetSliceProvider(GridSliceProviderCallback{GridSlice2D})"/></description></item>
        /// <item><description><see cref="SetSliceProvider(GridSliceProviderCallback{GridSlice3D})"/></description></item>
        /// </list>
        /// </summary>
        /// <seealso cref="SetSliceProvider(GridSliceProviderCallback{GridSlice})"/>
        /// <seealso cref="SetSliceProvider(GridSliceProviderCallback{GridSlice2D})"/>
        /// <seealso cref="SetSliceProvider(GridSliceProviderCallback{GridSlice3D})"/>
        public void ClearSliceProvider()
        {
            gridSliceProvider = null;
            gridSliceType = typeof(GridSlice);
            virtualBlockType = typeof(VirtualUIBlock2D);
        }

        /// <summary>
        /// Wraps the GridView's underlying data source in a <see cref="GridList{T}"/> to be indexable by a <see cref="GridIndex"/>,
        /// where <see cref="GridIndex.Row"/> is the index into the <see cref="PrimaryAxis"/> and <see cref="GridIndex.Column"/> is the index
        /// into the <see cref="CrossAxis"/>.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="GridList{T}"/> to create. Must match the type parameter used when calling <see cref="ListView.SetDataSource{T}(IList{T})"/></typeparam>
        /// <returns>The GridView's underlying data source wrapped as a <see cref="GridList{T}"/></returns>
        /// <exception cref="ArgumentException">Thrown when <typeparamref name="T"/> doesn't match the type parameter used when calling <see cref="ListView.SetDataSource{T}(IList{T})"/></exception>
        /// <exception cref="ArgumentException">Thrown when <see cref="ListView.SetDataSource{T}(IList{T})"/> hasn't been called or was set to <c><see langword="null"/></c></exception>
        /// <seealso cref="ListView.SetDataSource{T}(IList{T})"/>
        public GridList<T> GetDataSourceAsGrid<T>()
        {
            IList<T> datatSource = GetDataSource<T>();

            if (datatSource == null)
            {
                if (DataWrapper != null)
                {
                    throw new ArgumentException($"Requesting a {nameof(GridList<T>)} of type {typeof(T).Name}, but the {nameof(GridView)}'s data source is tracking elements of type {DataWrapper.GetElementType().Name}. The provided Type argument, {nameof(T)}, must match the underlying data source element type.");
                }

                throw new ArgumentException($"The {nameof(GridView)} has no underlying data source to wrap as a {typeof(GridList<T>)}. You must first call {nameof(SetDataSource)} before a {nameof(GridList<T>)} can be made from the {nameof(GridView)}.");
            }

            return GridList<T>.CreateWithColumns(datatSource, CrossAxisItemCount);
        }

        /// <summary>
        /// Retrieves the <see cref="ItemView"/> representing the object in the data source at the provided <see cref="GridIndex"/> if it's paged into the <see cref="GridView"/>.
        /// </summary>
        /// <param name="index"> 
        /// The <see cref="GridIndex"/> of the object in the data source represented by the requested 
        /// <see cref="ItemView"/>, where <see cref="GridIndex.Row"/> is the  index into the <see cref="PrimaryAxis"/> 
        /// and <see cref="GridIndex.Column"/> is the index into the <see cref="CrossAxis"/>
        /// </param>
        /// <param name="gridItem"> The item in the GridView representing the data object in the data source at the provided <see cref="GridIndex"/>.</param>
        /// <returns>Returns <see langword="true"/> if the requested <see cref="ItemView"/> is found (paged into view), otherwise returns <see langword="false"/>.</returns>
        public bool TryGetGridItem(GridIndex index, out ItemView gridItem)
        {
            int listIndex = Grid.ToIndex(index);

            return TryGetItemView(listIndex, out gridItem);
        }

        /// <summary>
        /// Invokes the configured grid slice provider, set via <see cref="SetSliceProvider"/>, for the grid<br/> 
        /// slice at the given index, <paramref name="sliceIndex"/>. Provides a way for the caller to reconfigure the visuals/layout of the requested grid slice.
        /// </summary>
        /// <remarks>
        /// If the grid slice provider has been cleared or was never set or if <paramref name="sliceIndex"/> is outside the range defined by [<see cref="MinLoadedSliceIndex"/>, <see cref="MaxLoadedSliceIndex"/>], this call won't do anything.
        /// </remarks>
        /// <param name="sliceIndex">The virtual index of the grid slice to update.</param>
        public void UpdateGridSlice(int sliceIndex)
        {
            if (gridSliceProvider == null || sliceIndex < MinLoadedSliceIndex || sliceIndex > MaxLoadedSliceIndex)
            {
                return;
            }

            int childIndex = sliceIndex - MinLoadedSliceIndex;

            VirtualUIBlock uiBlock = VirtualBlocks[childIndex] as VirtualUIBlock;

            ApplySliceToNode(uiBlock, gridSliceProvider, sliceIndex, PrimaryAxis, CrossAxis);
        }
        #endregion

        #region Internal
        [SerializeField, NotKeyable]
        [Tooltip("The non-scrolling axis to position elements in the grid.\n\nE.g.\nIf the Primary Axis is set to Y, the Cross Axis is commonly set to X.")]
        private Axis crossAxis = Axis.X;
        [SerializeField, NotKeyable]
        [Min(1), Tooltip("The number of elements to position along the Cross Axis.\n\nE.g.\nIf the Cross Axis is set to X, this is the number of columns in the grid.")]
        private int crossAxisItemCount = 1;

        [NonSerialized, HideInInspector]
        private MulticastDelegate gridSliceProvider;

        [NonSerialized, HideInInspector]
        private Type gridSliceType = typeof(GridSlice);
        [NonSerialized, HideInInspector]
        private Type virtualBlockType = typeof(VirtualUIBlock2D);
        [NonSerialized, HideInInspector]
        private VirtualBlockQueue pooledBlocks = new VirtualBlockQueue();

        [NonSerialized, HideInInspector]
        private VirtualBlockQueue processedBlocks = new VirtualBlockQueue();
        [NonSerialized, HideInInspector]
        private int removedFromFront = 0;
        [NonSerialized, HideInInspector]
        private int removedFromBack = 0;

        private protected override int SecondaryAxisItemCount => CrossAxisItemCount;

        private GridList Grid => GridList.CreateWithInfiniteRows(DataWrapper.Count, CrossAxisItemCount);

        private protected override ReadOnlyList<DataStoreID> GetChildIDs()
        {
            return VirtualBlockCount == 0 ? base.GetChildIDs() : VirtualBlockModule.IDs;
        }

        private protected override bool TryGetPrimaryAxisItemFromSourceIndex(int index, out IUIBlock item)
        {
            item = null;

            if (!TryGetItemView(index, out ItemView listItem))
            {
                return false;
            }

            item = HierarchyDataStore.Instance.GetHierarchyParent(listItem.UIBlock.ID) as IUIBlock;

            return item != null;
        }

        private protected override void PageOutItem(bool firstSibling)
        {
            if ((firstSibling && VirtualBlocks.Count <= removedFromFront) ||
                (!firstSibling && VirtualBlocks.Count <= removedFromBack))
            {
                base.PageOutItem(firstSibling);

                return;
            }

            VirtualUIBlock blockToRemove = (firstSibling ? VirtualBlocks[removedFromFront++] : VirtualBlocks[VirtualBlockCount - (++removedFromBack)]) as VirtualUIBlock;

            if (firstSibling)
            {
                for (int i = 0; i < crossAxisItemCount && blockToRemove.BlockCount - i > 0; ++i)
                {
                    if (lowestPagedInIndex > highestPagedInIndex)
                    {
                        break;
                    }

                    RemoveFromFront();
                }
            }
            else
            {
                for (int i = 0; i < crossAxisItemCount && blockToRemove.BlockCount - i > 0; ++i)
                {
                    if (highestPagedInIndex < lowestPagedInIndex)
                    {
                        break;
                    }

                    RemoveFromBack();
                }
            }

            ScrollProcessor.ItemRemovedFromView(blockToRemove);

            processedBlocks.Enqueue(blockToRemove);
        }

        private protected override IUIBlockBase PageInItem(bool firstSibling)
        {
            if (!firstSibling && (highestPagedInIndex + 1) % SecondaryAxisItemCount != 0)
            {
                // Don't add new virtual block if it's not yet needed
                IUIBlockBase gridItem = PageInNextItem(rebuildLayout: false);

                if (gridItem != null && TryGetPrimaryAxisItemFromSourceIndex(highestPagedInIndex, out IUIBlock virtualParent))
                {
                    // Ensure new layout changes driven by recently added child
                    virtualParent.CalculateLayout();
                }

                // No new primary axis item was added
                return null;
            }

            VirtualUIBlock vBlock = GetVirtualBlock(firstSibling);

            int addedCount = 0;

            if (firstSibling)
            {
                for (int i = 0; i < crossAxisItemCount; ++i)
                {
                    IUIBlockBase added = PageInPreviousItem(rebuildLayout: false);

                    addedCount += (added as MonoBehaviour) != null ? 1 : 0;

                    if (lowestPagedInIndex == 0)
                    {
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < crossAxisItemCount; ++i)
                {
                    IUIBlockBase added = PageInNextItem(rebuildLayout: false);

                    addedCount += (added as MonoBehaviour) != null ? 1 : 0;

                    if (highestPagedInIndex == DataWrapper.Count - 1)
                    {
                        break;
                    }
                }
            }

            if (addedCount == 0)
            {
                processedBlocks.Enqueue(vBlock);

                return null;
            }

            // The calculated layout properties will be out of sync, so we need to recalculate
            vBlock.CalculateLayout();

            ScrollProcessor.ItemAddedToView(vBlock.ID);

            return vBlock;
        }

        private protected override void FinalizeItems()
        {
            while (processedBlocks.TryDequeue(out VirtualUIBlock blockToRemove))
            {
                blockToRemove.Visible = false;
                RemoveVirtualBlock(blockToRemove);
                blockToRemove.Dispose();
                pooledBlocks.Enqueue(blockToRemove);
            }

            removedFromFront = 0;
            removedFromBack = 0;
        }

        private void SetSliceProvider<TSlice>(MulticastDelegate provider) where TSlice : struct
        {
            if (provider == null)
            {
                ClearSliceProvider();
                return;
            }

            gridSliceType = typeof(TSlice);
            virtualBlockType = SliceTypeToBlockType<TSlice>();
            gridSliceProvider = provider;
        }

        private protected override void TearDown()
        {
            pooledBlocks.Dispose();
        }

        private protected override void OnDestroy()
        {
            base.OnDestroy();

            if (virtualBlockSerializer != null)
            {
                Destroy(virtualBlockSerializer);
                virtualBlockSerializer = null;
            }
        }

        private protected override void HandleOnEnabled()
        {
            VirtualBlockModule.HandleOwnerEnabled();

            if (NovaApplication.InPlayer(this))
            {
                RebalanceGrid(forceChildRegistration: true);
            }
        }

        private protected override void HandleOnDisabled()
        {
            VirtualBlockModule.HandleOwnerDisabled();
        }

        private static void ApplySliceToNode(VirtualUIBlock uiBlock, GridSlice slice)
        {
            uiBlock.Visible = false;
            uiBlock.Layout = slice.Layout;
            uiBlock.AutoLayout = slice.AutoLayout;
        }

        private static void ApplySliceToNode(VirtualUIBlock uiBlock, GridSlice2D slice)
        {
            VirtualUIBlock2D twoDBlock = uiBlock as VirtualUIBlock2D;

            twoDBlock.Visible = true;
            twoDBlock.Color = slice.Color;
            twoDBlock.Gradient = slice.Gradient;
            twoDBlock.Surface = slice.Surface;
            twoDBlock.CornerRadius = slice.CornerRadius;
            twoDBlock.Border = slice.Border;
            twoDBlock.Shadow = slice.Shadow;
            twoDBlock.Layout = slice.Layout;
            twoDBlock.AutoLayout = slice.AutoLayout;
        }

        private static void ApplySliceToNode(VirtualUIBlock uiBlock, GridSlice3D slice)
        {
            VirtualUIBlock3D threeDBlock = uiBlock as VirtualUIBlock3D;

            threeDBlock.Visible = true;
            threeDBlock.Color = slice.Color;
            threeDBlock.Surface = slice.Surface;
            threeDBlock.CornerRadius = slice.CornerRadius;
            threeDBlock.EdgeRadius = slice.EdgeRadius;
            threeDBlock.Layout = slice.Layout;
            threeDBlock.AutoLayout = slice.AutoLayout;
        }

        private static Type SliceTypeToBlockType<TSlice>() where TSlice : struct
        {
            if (typeof(TSlice) == typeof(GridSlice3D))
            {
                return typeof(VirtualUIBlock3D);
            }

            return typeof(VirtualUIBlock2D);
        }

        private VirtualUIBlock GetVirtualBlock(bool firstSibling)
        {
            int index = firstSibling ? lowestPagedInIndex - 1 : highestPagedInIndex + 1;
            int sliceIndex = Grid.GetRowIndex(index);

            if (processedBlocks.TryDequeue(virtualBlockType, out VirtualUIBlock vBlock))
            {
                int siblingIndex = vBlock.SiblingPriority;
                int requestedIndex = firstSibling ? 0 : VirtualBlockCount - processedBlocks.Count(virtualBlockType) - 1;

                if (firstSibling)
                {
                    removedFromBack = Mathf.Max(removedFromBack - 1, 0);
                }
                else
                {
                    removedFromFront = Mathf.Max(removedFromFront - 1, 0);
                }

                if (siblingIndex != requestedIndex)
                {
                    MoveVirtualBlock(vBlock, requestedIndex);
                }
            }
            else
            {
                vBlock = pooledBlocks.GetOrCreate(virtualBlockType);

                vBlock.Init();
                AddVirtualBlock(vBlock, crossAxisItemCount, insertAtFirstPosition: firstSibling);
                vBlock.CopyToDataStore();
            }

            ApplySliceToNode(vBlock, gridSliceProvider, sliceIndex, UIBlock.GetAutoLayoutReadOnly().Axis, crossAxis);

            return vBlock;
        }

        private void ApplySliceToNode(VirtualUIBlock vBlock, MulticastDelegate provider, int sliceIndex, Axis primaryAxis, Axis crossAxis)
        {
            if (gridSliceType == typeof(GridSlice2D))
            {
                GridSlice2D twoDSlice = new GridSlice2D(primaryAxis, crossAxis);

                if (provider != null)
                {
                    try
                    {
                        GridSliceProviderCallback<GridSlice2D> sliceProvider = (GridSliceProviderCallback<GridSlice2D>)provider;

                        sliceProvider.Invoke(sliceIndex, this, ref twoDSlice);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                ApplySliceToNode(vBlock, twoDSlice);
            }
            else if (gridSliceType == typeof(GridSlice3D))
            {
                GridSlice3D threeDSlice = new GridSlice3D(primaryAxis, crossAxis);

                if (provider != null)
                {
                    try
                    {
                        GridSliceProviderCallback<GridSlice3D> sliceProvider = (GridSliceProviderCallback<GridSlice3D>)provider;
                        sliceProvider.Invoke(sliceIndex, this, ref threeDSlice);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                ApplySliceToNode(vBlock, threeDSlice);
            }
            else
            {
                GridSlice twoDSlice = new GridSlice(primaryAxis, crossAxis);

                if (provider != null)
                {
                    try
                    {
                        GridSliceProviderCallback<GridSlice> sliceProvider = (GridSliceProviderCallback<GridSlice>)provider;
                        sliceProvider.Invoke(sliceIndex, this, ref twoDSlice);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                ApplySliceToNode(vBlock, twoDSlice);
            }
        }

        internal void RebalanceGrid(bool forceChildRegistration = false)
        {
            if (MinLoadedIndex < 0)
            {
                // Not bound or empty
                return;
            }

            if (forceChildRegistration)
            {
                for (int i = 0; i < transform.childCount; ++i)
                {
                    Transform child = transform.GetChild(i);

                    if (!child.gameObject.activeSelf)
                    {
                        continue;
                    }

                    if (!child.TryGetComponent(out UIBlock uiBlock))
                    {
                        continue;    
                    }
                    
                    ((IGameObjectActiveReceiver)uiBlock).HandleOnEnable();
                } 
            }

            while (MinLoadedIndex % crossAxisItemCount != 0)
            {
                PageInPreviousItem(rebuildLayout: false);
            }
            
            int expected = UIBlock.BlockCount * crossAxisItemCount;

            int remainder = DataSourceItemCount % crossAxisItemCount;

            if (remainder != 0 && MaxLoadedIndex == DataSourceItemCount - 1)
            {
                expected -= crossAxisItemCount - remainder;
            }

            if (expected == UIBlock.ChildCount)
            {
                // already balanced
                return;
            }

            if (expected > UIBlock.ChildCount)
            {
                int toRemove = UIBlock.BlockCount - Mathf.FloorToInt(UIBlock.ChildCount / (float)crossAxisItemCount);

                for (int i = 0; i < toRemove && VirtualBlockCount > removedFromBack; ++i)
                {
                    VirtualUIBlock blockToRemove = VirtualBlocks[VirtualBlockCount - (++removedFromBack)] as VirtualUIBlock;

                    ScrollProcessor.ItemRemovedFromView(blockToRemove);

                    processedBlocks.Enqueue(blockToRemove);
                }

                toRemove = UIBlock.ChildCount - ((UIBlock.BlockCount - toRemove) * crossAxisItemCount);

                for (int i = 0; i < toRemove; ++i)
                {
                    RemoveFromBack();
                }
            }
            else
            {
                bool add = UIBlock.ChildCount % crossAxisItemCount != 0;
                int toAdd = add ? crossAxisItemCount - (UIBlock.ChildCount % crossAxisItemCount) : 0;

                for (int i = 0; i < toAdd && highestPagedInIndex < DataSourceItemCount - 1; ++i)
                {
                    PageInNextItem(rebuildLayout: false);
                }

                toAdd = Mathf.CeilToInt(UIBlock.ChildCount / (float)crossAxisItemCount) - UIBlock.BlockCount;

                for (int i = 0; i < toAdd; ++i)
                {
                    VirtualUIBlock vBlock = GetVirtualBlock(firstSibling: true);

                    // The calculated layout properties will be out of sync, so we need to recalculate
                    vBlock.CalculateLayout();

                    ScrollProcessor.ItemAddedToView(vBlock.ID);
                }
            }

            View.FinalizeItemsForView();

            RedistributeChildren();

            Relayout();
        }

        #region Virtual Block Serialization 
        [SerializeField, HideInInspector]
        private VirtualBlockSerializer virtualBlockSerializer = null;
        [NonSerialized, HideInInspector]
        private VirtualBlockModule virtualBlockModule = VirtualBlockModule.Empty;

        private VirtualBlockModule VirtualBlockModule
        {
            get
            {
                if (virtualBlockSerializer == null && virtualBlockModule.IsValid)
                {
                    virtualBlockSerializer = ScriptableObject.CreateInstance<VirtualBlockSerializer>();
                    virtualBlockSerializer.Module = virtualBlockModule;
                }

                if (virtualBlockSerializer == null)
                {
                    return VirtualBlockModule.Empty;
                }

                return virtualBlockSerializer.Module;
            }
        }

        private ReadOnlyList<VirtualBlock> VirtualBlocks => VirtualBlockModule.Blocks;
        private int VirtualBlockCount => VirtualBlocks.Count;

        private void AddVirtualBlock(VirtualBlock virtualBlock, int childrenPerBlock, bool insertAtFirstPosition)
        {
            if (!VirtualBlockModule.IsValid)
            {
                virtualBlockSerializer = ScriptableObject.CreateInstance<VirtualBlockSerializer>();
                virtualBlockModule = new VirtualBlockModule(UIBlock);
                virtualBlockSerializer.Module = virtualBlockModule;
            }

            VirtualBlockModule.AddVirtualBlock(virtualBlock, childrenPerBlock, insertAtFirstPosition);
        }

        private void MoveVirtualBlock(VirtualBlock virtualBlock, int newIndex) => VirtualBlockModule.MoveVirtualBlock(virtualBlock, newIndex);
        private void RemoveVirtualBlock(VirtualBlock virtualBlock) => VirtualBlockModule.RemoveVirtualBlock(virtualBlock);

        private void RedistributeChildren() => VirtualBlockModule.RedistributeChildren(CrossAxisItemCount);

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            ClearSerializedUIBlock();

            if (!NovaApplication.IsPlaying ||
                virtualBlockSerializer == null ||
                virtualBlockSerializer.Module == virtualBlockModule ||
                !virtualBlockSerializer.Module.IsValid ||
                (SerializedRoot != null && virtualBlockSerializer.Module.Owner.ID == SerializedRoot.ID))
            {
                return;
            }

            virtualBlockModule = VirtualBlockModule.Clone(virtualBlockSerializer.Module.Owner.ID, SerializedRoot);
            virtualBlockSerializer = null;
        }
        #endregion

        #endregion
    }
}
