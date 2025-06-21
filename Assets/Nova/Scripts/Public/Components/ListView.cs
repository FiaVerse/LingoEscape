// Copyright (c) Supernova Technologies LLC
//#define DEBUG_LOADING
using Nova.Compat;
using Nova.Internal;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.DataBinding;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;

using Math = Nova.Internal.Utilities.Math;

namespace Nova
{
    internal interface IListView : IScrollableView, IGameObjectActiveReceiver { }

    /// <summary>
    /// Creates a virtualized, scrollable list of <see cref="ItemView"/> prefabs from a user-provided data source.
    /// </summary>
    [AddComponentMenu("Nova/List View")]
    [RequireComponent(typeof(UIBlock))]
    [HelpURL("https://novaui.io/manual/ListView.html")]
    public class ListView : MonoBehaviour, IListView, ISerializationCallbackReceiver
    {
        #region Public
        /// <summary>
        /// Invoked when the list is scrolled beyond the last list element. Provides the distance between the edge of content and edge of the viewport.
        /// </summary>
        /// <remarks>This event will fire <i>every</i> frame the <see cref="ListView"/> is scrolled while in this state. This provides the most flexibility to the event subscribers, but it does mean this event can be a bit noisy.</remarks>
        public event Action<float> OnScrolledPastEnd = null;

        /// <summary>
        /// Invoked when the list is scrolled beyond the first list element. Provides the distance between the edge of content and edge of the viewport.
        /// </summary>
        /// <remarks>This event will fire <i>every</i> frame the <see cref="ListView"/> is scrolled while in this state. This provides the most flexibility to the event subscribers, but it does mean this event can be a bit noisy.</remarks>
        public event Action<float> OnScrolledPastStart = null;

        /// <summary>
        /// The distance a list item must be out of view before it's removed and added back into the list item pool.<br/>Can be used as a spatial buffer in the event the rendered size of a list item extends beyond its <see cref="UIBlock.LayoutSize"/>.
        /// </summary>
        public float OutOfViewDistance
        {
            get => outOfViewDistance;
            set => outOfViewDistance = value;
        }

        /// <summary>
        /// The size of the underlying data source. Will return <c>0</c> if the underlying data source is <c>null</c>.
        /// </summary>
        public int DataSourceItemCount => DataWrapper == null || DataWrapper.IsEmpty ? 0 : DataWrapper.Count;

        /// <summary>
        /// The highest index into the data source mapped to a list item that's currently loaded into view.
        /// </summary>
        public int MaxLoadedIndex => DataSourceItemCount == 0 ? -1 : Math.Clamp(highestPagedInIndex, 0, DataSourceItemCount - 1);

        /// <summary>
        /// The lowest index into the data source mapped to a list item that's currently loaded into view.
        /// </summary>
        public int MinLoadedIndex => DataSourceItemCount == 0 ? -1 : Math.Clamp(lowestPagedInIndex, 0, DataSourceItemCount - 1);

        /// <summary>
        /// The parent <see cref="Nova.UIBlock"/> of all the list items. Attached to <c>this.gameObject</c>. 
        /// </summary>
        public UIBlock UIBlock
        {
            get
            {
                if (!NovaApplication.IsPlaying)
                {
                    // We only want to cache and serialize
                    // _uiBlock in play mode, so just return
                    // here in edit mode.
                    return GetComponent<UIBlock>();
                }

                if (_uiBlock == null)
                {
                    _uiBlock = GetComponent<UIBlock>();
                }

                return _uiBlock;
            }
        }

        /// <summary>
        /// Retrieve the underlying data source, previously assigned via <see cref="SetDataSource{T}(IList{T})"/>.
        /// </summary>
        /// <remarks></remarks>
        /// <typeparam name="T">The list item type of the data source</typeparam>
        /// <returns>Returns <c>null</c> if the data source was never set or if the data source is not an instance of <see cref="IList{T}"/>. Otherwise returns the underlying data source.</returns>
        public IList<T> GetDataSource<T>()
        {
            return DataWrapper == null ? null : DataWrapper.GetSource<T>();
        }

        /// <summary>
        /// Set the underlying data source. This instructs the list to start creating a 1-to-1 mapping of objects in the data source to <see cref="ListView"/> list items.
        /// </summary>
        /// <remarks>
        /// Any desired <see cref="Data.OnBind{TData}"/> event handlers must be added via <see cref="AddDataBinder{TData, TVisuals}(UIEventHandler{Data.OnBind{TData}, TVisuals, int})"/><br/> <b>before</b> this call to <see cref="SetDataSource{T}(IList{T})"/>.
        /// Otherwise the <see cref="ListView"/> won't know how to bind objects in the <paramref name="dataSource"/> to any of the list item prefab types.
        /// </remarks>
        /// <typeparam name="T">The type of list element stored in <paramref name="dataSource"/></typeparam>
        /// <param name="dataSource">The list of data objects to bind to this <see cref="ListView"/></param>
        /// 
        /// <example><code>
        /// using System;
        /// using System.Collections.Generic;
        /// using UnityEngine;
        /// 
        /// // In Editor, assign and configure on an <see cref="ItemView"/> at the root of a toggle list-item prefab
        /// [Serializable]
        /// public class ToggleVisuals : <see cref="ItemVisuals"/>
        /// {
        ///     // The visual to display a label string
        ///     public <see cref="TextBlock"/> Label;
        ///
        ///     // The visual to display toggle on/off state
        ///     public <see cref="UIBlock2D"/> IsOnIndicator;
        /// }
        /// 
        /// // The underlying data stored per toggle in a list of toggles
        /// [Serializable]
        /// public class ToggleData
        /// {
        ///     public string Label;
        ///     public bool IsOn;
        /// }
        /// 
        /// public class ListViewBinder : MonoBehaviour
        /// {
        ///      // Serialize and assign in the Editor
        ///      public <see cref="ListView"/> ListView = null;
        ///
        ///      // Serialize and assign in the Editor
        ///      public List&lt;ToggleData&gt; Toggles = null;
        ///      
        ///      // Color to display when a toggle is "on"
        ///      public Color ToggledOnColor = Color.blue;
        ///      // Color to display when a toggle is "off"
        ///      public Color ToggledOffColor = Color.grey;
        /// 
        ///      public void OnEnable()
        ///      {
        ///          // <see cref="ListView.AddDataBinder{TData, TVisuals}(UIEventHandler{Data.OnBind{TData}, TVisuals, int})"/>
        ///          ListView.AddDataBinder&lt;ToggleData, ToggleVisuals&gt;(BindToggle);
        ///      }
        ///
        ///      // <see cref="Data.OnBind{TData}"/>
        ///      public void BindToggle(Data.OnBind&lt;ToggleData&gt; evt, ToggleVisuals fields, int index)
        ///      {
        ///          // Get the ToggleData off the Data.OnBind event
        ///          ToggleData toggleData = evt.UserData;
        ///
        ///          // Assign the ToggleVisuals.Label text to the underlying ToggleData.Label string
        ///          fields.Label.Text = toggleData.Label;
        ///
        ///          // Assign the ToggleVisuals.IsOnIndicator color to ToggledOnColor or ToggledOffColor, depending on the underlying ToggleData.IsOn bool
        ///          fields.IsOnIndicator.Color = toggleData.IsOn ? ToggledOnColor : ToggledOffColor;
        ///      }
        ///      
        ///      public void OnDisable()
        ///      {
        ///          // <see cref="ListView.RemoveDataBinder{TData, TVisuals}(UIEventHandler{Data.OnBind{TData}, TVisuals, int})"/>
        ///          ListView.RemoveDataBinder&lt;ToggleData, ToggleVisuals&gt;(BindToggle);
        ///      }
        /// }
        /// </code></example>
        public void SetDataSource<T>(IList<T> dataSource)
        {
            settingDataSource = true;

            prefabPool.Init(UIBlock.transform, listItemPrefabs);

            Clear(finalize: false);

            if (DataWrapper != null)
            {
                DataWrapper.Dispose();
            }

            DataWrapper = ListWrapper<T>.Wrap(dataSource);

            if (dataSource != null && UIBlock.Activated)
            {
                AutoLayout layout = UIBlock.GetAutoLayoutReadOnly();

                if (layout.AutoSpace)
                {
                    Debug.LogWarning($"{nameof(AutoLayout)}.{nameof(AutoLayout.AutoSpace)} is not supported on {GetType().Name}s and will likely lead to issues when populating the {GetType().Name} with items if enabled.", this);
                }

                bool crossAxisEnabled = layout.Cross.Enabled && layout.Cross.Axis != layout.Axis;

                if (crossAxisEnabled)
                {
                    Debug.LogWarning($"{nameof(AutoLayout)}.{nameof(AutoLayout.Cross)} is not supported on {GetType().Name}s and will likely lead to issues populating the {GetType().Name} with items and while scrolling if enabled.", this);
                }

                InitializeView();
            }

            View.FinalizeItemsForView();

            settingDataSource = false;
        }

        /// <summary>
        /// Scrolls the list content by the provided <paramref name="delta"/> along the <see cref="UIBlock"/>'s <see cref="AutoLayout"/>.<see cref="AutoLayout.Axis">Axis</see>
        /// </summary>
        /// <remarks>If <c><see cref="UIBlock"/>.gameObject.activeInHierarchy == <see langword="false"/></c>, this call won't do anything.</remarks>
        /// <param name="delta">Value is in <c><see cref="UIBlock"/>.transform</c> local space</param>
        public void Scroll(float delta)
        {
            if (!UIBlock.Activated || DataWrapper == null || DataWrapper.IsEmpty)
            {
                return;
            }

            AutoLayout layout = UIBlock.GetAutoLayoutReadOnly();

            if (!layout.Axis.TryGetIndex(out int axis))
            {
                return;
            }

            // Somewhat arbitrarily double it so we can scroll by a full page
            float clip = View.ClipSize[axis] * 2;

            if (Mathf.Abs(delta) >= clip)
            {
                delta = Math.Sign(delta) * clip;
            }

            viewportVirtualizer.Scroll(delta);
        }

        /// <summary>
        /// Jumps the <see cref="ListView"/> to the item at the given index.
        /// </summary>
        /// <remarks>
        /// The final location of the specified item after the jump will be at to the edge of the view 
        /// in the direction which the <see cref="ListView"/> had to scroll to get to the item.<br/>
        /// E.g. If the <see cref="ListView"/> had to scroll <i>down</i> to get to the item in a vertically scrollable list,
        /// the item will be at the <i>bottom</i> of the view.
        /// </remarks>
        /// <param name="index">The index into the data source, set via <see cref="SetDataSource{T}(IList{T})"/>, of the object to bind into view and jump to</param>
        /// <exception cref="IndexOutOfRangeException">if <c><paramref name="index"/> &lt; 0 || <paramref name="index"/> &gt;= <see cref="DataSourceItemCount"/></c></exception>
        /// <exception cref="InvalidOperationException">if <c><see cref="UIBlock">UIBlock</see>.<see cref="UIBlock.AutoLayout">AutoLayout</see>.<see cref="AutoLayout.Axis">Axis</see> == <see cref="Axis"/>.<see cref="Axis.None">None</see></c></exception>
        /// <seealso cref="JumpToIndexPage(int)"/>
        /// <seealso cref="Scroller.ScrollToIndex(int, bool)"/>
        public void JumpToIndex(int index) => Scroll(JumpToIndexPage(index));

        /// <summary>
        /// Jumps the <see cref="ListView"/> to the virtualized page of the item at the given index
        /// </summary>
        /// <remarks>If <c><see cref="UIBlock"/>.gameObject.activeInHierarchy == <see langword="false"/></c>, this call won't do anything.</remarks>
        /// <example>
        /// <code>
        /// 
        /// // Bind element at index 100 and its surrounding elements to the ListView.
        /// float distanceOutOfView = listView.JumpToIndexPage(100); 
        /// 
        /// // move the element at index 100 into view
        /// listView.Scroll(distanceOutOfView);
        /// 
        /// </code>
        /// </example>
        /// 
        /// <param name="index">The index into the data source, set via <see cref="SetDataSource{T}(IList{T})"/>, of the object to bind into view and jump to</param>
        /// 
        /// <returns>
        /// The signed distance the list must be scrolled to make the jumped-to item maximally visible within the viewport.<br/><br/>
        /// Calling <see cref="JumpToIndexPage(int)"/> only guarantees to bind the element at the provided <paramref name="index"/> to<br/>
        /// the <see cref="ListView"/>, but it might be slightly out of view without a subsequent call to <see cref="Scroll(float)"/>.
        /// </returns>
        /// <exception cref="IndexOutOfRangeException">if <c><paramref name="index"/> &lt; 0 || <paramref name="index"/> &gt;= <see cref="DataSourceItemCount"/></c></exception>
        /// <exception cref="InvalidOperationException">if <c><see cref="UIBlock">UIBlock</see>.<see cref="UIBlock.AutoLayout">AutoLayout</see>.<see cref="AutoLayout.Axis">Axis</see> == <see cref="Axis"/>.<see cref="Axis.None">None</see></c></exception>
        /// <seealso cref="JumpToIndex(int)"/>
        /// <seealso cref="Scroller.ScrollToIndex(int, bool)"/>
        public float JumpToIndexPage(int index) => JumpToIndexPageInternal(index, syncToVirtualizer: true);

        /// <summary>
        /// Attempts to retrieve the <see cref="ItemView"/> in view representing the object in the data source at the provided index, <paramref name="sourceIndex"/>.
        /// </summary>
        /// <param name="sourceIndex">The index of the object in the data source</param>
        /// <param name="listItem">The list item visual reprensenting the object at index <paramref name="sourceIndex"/></param>
        /// <returns><see langword="true"/> if a list item representing the object at index <paramref name="sourceIndex"/> is currently loaded into view. Otherwise returns <see langword="false"/>.</returns>
        /// <seealso cref="TryGetSourceIndex(ItemView, out int)"/>
        public bool TryGetItemView(int sourceIndex, out ItemView listItem)
        {
            return TryGetListItemInternal(sourceIndex, out _, out listItem);
        }

        /// <summary>
        /// Attempts to retrieve the index into the data source, <paramref name="sourceIndex"/>, represented by the provided in-view <paramref name="listItem"/>.
        /// </summary>
        /// <param name="listItem">The list item visual reprensenting the object at index <paramref name="sourceIndex"/></param>
        /// <param name="sourceIndex">The index of the object in the data source</param>
        /// <returns><see langword="true"/> if <paramref name="listItem"/> is loaded into view. Otherwise returns <see langword="false"/>.</returns>
        /// <seealso cref="TryGetItemView(int, out ItemView)"/>
        public bool TryGetSourceIndex(ItemView listItem, out int sourceIndex)
        {
            if (listItem == null)
            {
                sourceIndex = -1;
                return false;
            }

            return prefabPool.TryGetKey(listItem.UIBlock.ID, out sourceIndex);
        }

        /// <summary>
        /// Transfers ownership of the <see cref="ItemView"/> representing the object at data source index <paramref name="sourceIndex"/> from the <see cref="ListView"/> to the caller, allowing the caller to manipulate the <paramref name="listItem"/> arbitrarily without fighting the <see cref="ListView"/> binding/prefab pooling system.
        /// </summary>
        /// <remarks>
        /// If the detach succeeds, the <paramref name="listItem"/>'s parent will be set to <paramref name="newParent"/>.<br/><br/>
        /// Because the now detached item is no longer a child of the <see cref="ListView"/>, the gesture handlers registered
        /// on the <see cref="ListView"/> will not fire for the detached item anymore. To continue receiving the same events while the item is detached,
        /// subscribe to them on the detached <see cref="ItemView.UIBlock"/> directly (or its new parent) via <see cref="UIBlock.AddGestureHandler{TGesture}(UIEventHandler{TGesture}, bool)"/> 
        /// and then use <see cref="UIBlock.RemoveGestureHandler{TGesture}(UIEventHandler{TGesture})"/> before <see cref="TryReattach(ItemView)"/> to avoid having multiple gesture handlers simultaneously. 
        /// </remarks>
        /// <param name="sourceIndex">The index of the object in the data source</param>
        /// <param name="listItem">The list item visual reprensenting the object at index <paramref name="sourceIndex"/></param>
        /// <param name="newParent">The transform to set as the new parent of <paramref name="listItem"/></param>
        /// <returns><see langword="true"/> if <paramref name="listItem"/> is loaded into view and gets detached succesfully. Otherwise returns <see langword="false"/>.</returns>
        /// <seealso cref="TryDetach(ItemView, Transform)"/>
        /// <seealso cref="TryReattach(ItemView)"/>
        public bool TryDetach(int sourceIndex, out ItemView listItem, Transform newParent = null)
        {
            if (!TryGetItemView(sourceIndex, out listItem))
            {
                return false;
            }

            return TryDetach(listItem, newParent);
        }

        /// <summary>
        /// Transfers ownership of the detached <paramref name="listItem"/> from the <see cref="ListView"/> to the caller, allowing the caller to manipulate the <paramref name="listItem"/> arbitrarily without fighting the <see cref="ListView"/> binding/prefab pooling system.
        /// </summary>
        /// <remarks>
        /// If the detach succeeds, the <paramref name="listItem"/>'s parent will be set to <paramref name="newParent"/>.<br/><br/>
        /// Because the now detached item is no longer a child of the <see cref="ListView"/>, the gesture handlers registered
        /// on the <see cref="ListView"/> will not fire for the detached item anymore. To continue receiving the same events while the item is detached,
        /// subscribe to them on the detached <see cref="ItemView.UIBlock"/> directly (or its new parent) via <see cref="UIBlock.AddGestureHandler{TGesture}(UIEventHandler{TGesture}, bool)"/> 
        /// and then use <see cref="UIBlock.RemoveGestureHandler{TGesture}(UIEventHandler{TGesture})"/> before <see cref="TryReattach(ItemView)"/> to avoid having multiple gesture handlers simultaneously. 
        /// </remarks>
        /// <param name="listItem">The list item visual reprensenting the item</param>
        /// <param name="newParent">The transform to set as the new parent of <paramref name="listItem"/></param>
        /// <returns><see langword="true"/> if <paramref name="listItem"/> is loaded into view and gets detached succesfully. Otherwise returns <see langword="false"/>.</returns>
        /// <seealso cref="TryDetach(int, out ItemView, Transform)"/>
        /// <seealso cref="TryReattach(ItemView)"/>
        public bool TryDetach(ItemView listItem, Transform newParent = null)
        {
            if (listItem == null ||
                !prefabPool.TryGetKey(listItem.UIBlock.ID, out int index) ||
                !prefabPool.TryDetachInstance(listItem, newParent))
            {
                return false;
            }

            pagedInItems.Remove(index);
            detachedItemIDs.Add(index);
            viewportVirtualizer.ItemRemovedFromView(listItem.UIBlock);
            return true;
        }

        /// <summary>
        /// If a list item was previously detached from the <see cref="ListView"/> (via <see cref="TryDetach(ItemView, Transform)"/>),
        /// this is a way to transfer ownership of <paramref name="listItem"/> back to the <see cref="ListView"/>.
        /// </summary>
        /// <remarks>
        /// The returned object is simply returned to the list item prefab pool for reuse, <i>not</i> inserted into view directly.
        /// </remarks>
        /// <param name="listItem">The list item to re-attach, previously detached via <see cref="TryDetach(ItemView, Transform)"/></param>
        /// <returns><see langword="true"/> if <paramref name="listItem"/> was previously detached from this <see cref="ListView"/> and can be attached/pooled. Otherwise returns <see langword="false"/>.</returns>
        /// <seealso cref="TryDetach(int, out ItemView, Transform)"/>
        /// <seealso cref="TryDetach(ItemView, Transform)"/>
        public bool TryReattach(ItemView listItem)
        {
            return prefabPool.TryReattachInstance(listItem);
        }

        /// <summary>
        /// Reprocesses the object at index <paramref name="sourceIndex"/> in the data source, invoking any 
        /// necessary <see cref="PrefabProviderCallback{int}"/> callbacks, added via<br/> 
        /// <see cref="AddPrefabProvider{TData}(PrefabProviderCallback{int})"/>, and triggering a <see cref="Data.OnBind{TData}"/> event 
        /// to refresh the list item content. 
        /// </summary>
        /// <remarks>Unlike <see cref="Refresh"/>, <see cref="Rebind(int)"/> does not load new items if the rebind causes new content
        /// to become visible. If your call to <see cref="Rebind(int)"/> modifies the size of the target item, potentially requiring new items
        /// to be loaded, call <see cref="Relayout"/> after <see cref="Rebind(int)"/> or use <see cref="Refresh"/> instead.</remarks>
        /// <param name="sourceIndex">The index of the object in the data source to rebind to the <see cref="ListView"/></param>
        public void Rebind(int sourceIndex)
        {
            if (!UIBlock.Activated || DataWrapper == null || DataWrapper.IsEmpty)
            {
                return;
            }

            if (sourceIndex < lowestPagedInIndex || sourceIndex > highestPagedInIndex)
            {
                return;
            }

            if (!detachedItemIDs.Contains(sourceIndex))
            {
                bool found = TryGetListItemInternal(sourceIndex, out DataStoreID id, out ItemView item);
                if (!found && !id.IsValid)
                {
                    // The index was not found
                    return;
                }

                if (found && prefabPool.IsCorrectPrefabType(sourceIndex, item, DataWrapper.GetDataType(sourceIndex), out Type userDataType))
                {
                    PrefabPool<int>.GetDataBinder(userDataType).InvokeBind(item, DataWrapper, sourceIndex);
                    return;
                }

                // this is a user-invoked code path, and the objects might be pulled in from the pool
                // after this method runs, so we need to ensure this is in the expected pooled state
                RemoveListItem(sourceIndex, finalize: true);
            }

            if (TryAddItem(sourceIndex, out ItemView addedItem))
            {
                int siblingIndex = -1;

                int currentIndex = addedItem.transform.GetSiblingIndex();

                for (int adjacentSiblingIndex = sourceIndex - 1; adjacentSiblingIndex >= lowestPagedInIndex; --adjacentSiblingIndex)
                {
                    if (TryGetItemView(adjacentSiblingIndex, out ItemView sibling))
                    {
                        siblingIndex = sibling.transform.GetSiblingIndex() + 1;
                        break;
                    }
                }

                if (siblingIndex < 0)
                {
                    for (int adjacentSiblingIndex = sourceIndex + 1; adjacentSiblingIndex <= highestPagedInIndex; ++adjacentSiblingIndex)
                    {
                        if (TryGetItemView(adjacentSiblingIndex, out ItemView sibling))
                        {
                            siblingIndex = sibling.transform.GetSiblingIndex();
                            break;
                        }
                    }
                }

                siblingIndex = siblingIndex < 0 ? 0 : currentIndex < siblingIndex ? siblingIndex - 1 : siblingIndex;

                if (currentIndex != siblingIndex)
                {
                    addedItem.transform.SetSiblingIndex(siblingIndex);
                }
            }
        }

        /// <summary>
        /// Synchronizes the items in view with the content of the underlying data source, previously assigned via <see cref="SetDataSource{T}(IList{T})"/>.<br/>
        /// Calls <see cref="Rebind(int)"/> for in-view elements, handles changes to the number of items in the data source, and will call <see cref="Relayout"/> to ensure the view is filled. 
        /// </summary>
        public void Refresh()
        {
            if (!UIBlock.Activated)
            {
                return;
            }

            if (DataWrapper == null || DataWrapper.IsEmpty)
            {
                if (pagedInItems.Count > 0)
                {
                    // if the list is empty, but we have items
                    // in view, just clear the items in view
                    Clear(finalize: true);
                }

                return;
            }

            bool removedSomething = false;

            // if the list size changed to smaller value than the location in the list
            // we were previously at, remove the items that are now outside the data source count bounds
            while (highestPagedInIndex >= DataSourceItemCount && highestPagedInIndex >= lowestPagedInIndex)
            {
                if (highestPagedInIndex % SecondaryAxisItemCount == 0)
                {
                    PageOutItem(fromFront: false);
                }
                else
                {
                    RemoveFromBack();
                }

                removedSomething = true;
            }

            bool populateFromScratch = highestPagedInIndex < lowestPagedInIndex;

            if (populateFromScratch)
            {
                lowestPagedInIndex = removedSomething ? DataSourceItemCount : 0;
                highestPagedInIndex = lowestPagedInIndex - 1;

                // We cleared the view. If we previously had items paged in
                // that are now beyond the highest index in the data source,
                // we want to remain at the end of the list. If the list was
                // previously empty, we want to remain at the start of the list.
                PopulateView(append: !removedSomething);
                JumpToIndex(removedSomething ? highestPagedInIndex : lowestPagedInIndex);
            }
            else
            {
                // Rebind the items that are still in view
                for (int i = MinLoadedIndex; i <= MaxLoadedIndex; ++i)
                {
                    Rebind(i);

                    if (!TryGetListItemInternal(i, out DataStoreID id, out ItemView item))
                    {
                        // We removed something, tried to re-add it on rebind,
                        // the rebind failed (which would have thrown an exception),
                        // can't really do much to completely recover so just continue
                        continue;
                    }

                    item.UIBlock.SetAsLastSibling();
                }
            }

            if (removedSomething)
            {
                View.FinalizeItemsForView();
            }

            Relayout();
        }

        /// <summary>
        /// The <see cref="ListView"/> will pull more content into view and page content out of view as it's scrolled, but it doesn't automatically check for external<br/>
        /// events causing its child list items to change in size. This utility provides a way to manually re-fill the list view or page out extraneous list items.
        /// </summary>
        public void Relayout()
        {
            UIBlock.CalculateLayout();

            AutoLayout autoLayout = UIBlock.GetAutoLayoutReadOnly();

            if (!autoLayout.Axis.TryGetIndex(out int axis))
            {
                return;
            }

            float scroll = 0;

            if (View.EstimatedTotalContentSize <= View.ScrollRange[axis] && DataSourceItemCount > 0)
            {
                int index = TryGetItemView(0, out _) ? DataSourceItemCount - 1 : 0;

                // If all the content fits within the viewport, just
                // jump to the first page and adjust the offset from there.
                scroll = JumpToIndexPageInternal(index, syncToVirtualizer: false);
            }
            else
            {
                // Handle case where viewport is filled along primary
                // axis but cross axis is not fully populated
                while ((highestPagedInIndex + 1) % SecondaryAxisItemCount != 0 &&
                       highestPagedInIndex < DataSourceItemCount - 1)
                {
                    PageInNextItem(rebuildLayout: false);
                }

                bool atPositiveEnd = !View.HasContentInDirection(1);
                bool atNegativeEnd = !View.HasContentInDirection(-1);

                float contentCenter = UIBlock.ContentCenter[axis];
                float contentSize = UIBlock.ContentSize[axis];
                float viewportSize = UIBlock.PaddedSize[axis];
                float viewportCenter = UIBlock.CalculatedPadding.Offset[axis];

                float contentExtent = contentSize * 0.5f;
                float contentMin = contentCenter - contentExtent;
                float contentMax = contentCenter + contentExtent;
                float viewportExtent = viewportSize * 0.5f;
                float viewportMin = viewportCenter - viewportExtent;
                float viewportMax = viewportCenter + viewportExtent;

                if (atPositiveEnd && contentMax < viewportMax)
                {
                    scroll = viewportMax - contentMax;
                }
                else if (atNegativeEnd && contentMin > viewportMin)
                {
                    scroll = viewportMin - contentMin;
                }
            }

            Scroll(scroll);
        }

        /// <summary>
        /// Register a <see cref="PrefabProviderCallback{TKey}"/> for a particular data type in the data source, <typeparamref name="TData"/>.
        /// </summary>
        /// <remarks>Allows the caller to manually provide the prefab to use for a given object in the data source. Useful for when the<br/> caller wants to
        /// bind multiple list item prefab types to a single data type in the data source, <typeparamref name="TData"/>, or if the caller<br/> 
        /// wants to bypass the serialized set of list item prefabs.
        /// </remarks>
        /// <typeparam name="TData">The the list element type of the data source (or a derived type)</typeparam>
        /// <param name="prefabProvider">The callback that will be invoked before loading a list item for data source element type <typeparamref name="TData"/></param>
        /// <exception cref="ArgumentNullException">If <paramref name="prefabProvider"/> is <c>null</c>.</exception>
        public void AddPrefabProvider<TData>(PrefabProviderCallback<int> prefabProvider)
        {
            if (prefabProvider == null)
            {
                throw new ArgumentNullException(nameof(prefabProvider));
            }

            prefabPool.AddListItemPrefabProvider<TData>(prefabProvider);
        }

        /// <summary>
        /// Unregister a <see cref="PrefabProviderCallback{TKey}"/> that was previously registered via <see cref="AddPrefabProvider{TData}(PrefabProviderCallback{int})"/>
        /// </summary>
        /// <typeparam name="TData">The the list element type of the data source (or a derived type)</typeparam>
        public void RemovePrefabProvider<TData>() => prefabPool.RemoveListItemPrefabProvider<TData>();

        /// <summary>
        /// Subscribe to an indexed <see cref="Data.OnUnbind{TData}"/> event on this <see cref="ListView"/>'s set of list items
        /// </summary>
        /// <remarks><typeparamref name="TData"/> should match or be derived from the underlying data source element type, the type parameter passed into <see cref="SetDataSource{T}(IList{T})"/>.</remarks>
        /// <typeparam name="TData">The data type to unbind from the corresponding <typeparamref name="TVisuals"/> type</typeparam>
        /// <typeparam name="TVisuals">The type of <see cref="ItemVisuals"/>, configured on a list item prefab, to target.</typeparam>
        /// <param name="eventHandler">The callback invoked when the event fires</param>
        /// <exception cref="ArgumentNullException">If <paramref name="eventHandler"/> is <c>null</c>.</exception>
        public void AddDataUnbinder<TData, TVisuals>(UIEventHandler<Data.OnUnbind<TData>, TVisuals, int> eventHandler) where TVisuals : ItemVisuals
        {
            if (eventHandler == null)
            {
                throw new ArgumentNullException(nameof(eventHandler));
            }

            prefabPool.AddPrefabToDataTypeMapping<TVisuals, TData>();

            AddCallback(eventHandler);
        }

        /// <summary>
        /// Unsubscribe from an indexed <see cref="Data.OnUnbind{TData}"/> event previously subscribed to via <see cref="ListView.AddDataUnbinder{TData, TVisuals}(UIEventHandler{Data.OnUnbind{TData}, TVisuals, int})"/>
        /// </summary>
        /// <remarks><typeparamref name="TData"/> should match or be derived from the underlying data source element type, the type parameter passed into <see cref="SetDataSource{T}(IList{T})"/>.</remarks>
        /// <typeparam name="TData">The data type to bind to the corresponding <typeparamref name="TVisuals"/> type</typeparam>
        /// <typeparam name="TVisuals">The type of <see cref="ItemVisuals"/>, configured on a list item prefab, to target.</typeparam>
        /// <param name="eventHandler">The callback to remove from the subscription list</param>
        /// <exception cref="ArgumentNullException">If <paramref name="eventHandler"/> is <c>null</c>.</exception>
        public void RemoveDataUnbinder<TData, TVisuals>(UIEventHandler<Data.OnUnbind<TData>, TVisuals, int> eventHandler) where TVisuals : ItemVisuals
        {
            if (eventHandler == null)
            {
                throw new ArgumentNullException(nameof(eventHandler));
            }

            prefabPool.RemovePrefabToDataTypeMapping<TVisuals, TData>();

            RemoveCallback(eventHandler);
        }

        /// <summary>
        /// Subscribe to an indexed <see cref="Data.OnBind{TData}"/> event on this <see cref="ListView"/>'s set of list items
        /// </summary>
        /// <remarks><typeparamref name="TData"/> should match or be derived from the underlying data source element type, the type parameter passed into <see cref="SetDataSource{T}(IList{T})"/>.</remarks>
        /// <typeparam name="TData">The data type to bind to the corresponding <typeparamref name="TVisuals"/> type</typeparam>
        /// <typeparam name="TVisuals">The type of <see cref="ItemVisuals"/>, configured on a list item prefab, to target.</typeparam>
        /// <param name="eventHandler">The callback invoked when the event fires</param>
        /// <exception cref="ArgumentNullException">If <paramref name="eventHandler"/> is <c>null</c>.</exception>
        public void AddDataBinder<TData, TVisuals>(UIEventHandler<Data.OnBind<TData>, TVisuals, int> eventHandler) where TVisuals : ItemVisuals
        {
            if (eventHandler == null)
            {
                throw new ArgumentNullException(nameof(eventHandler));
            }

            prefabPool.AddPrefabToDataTypeMapping<TVisuals, TData>();

            AddCallback(eventHandler);
        }

        /// <summary>
        /// Unsubscribe from an indexed <see cref="Data.OnBind{TData}"/> event previously subscribed to via <see cref="ListView.AddDataBinder{TData, TVisuals}(UIEventHandler{Data.OnBind{TData}, TVisuals, int})"/>
        /// </summary>
        /// <remarks><typeparamref name="TData"/> should match or be derived from the underlying data source element type, the type parameter passed into <see cref="SetDataSource{T}(IList{T})"/>.</remarks>
        /// <typeparam name="TData">The data type to bind to the corresponding <typeparamref name="TVisuals"/> type</typeparam>
        /// <typeparam name="TVisuals">The type of <see cref="ItemVisuals"/>, configured on a list item prefab, to target.</typeparam>
        /// <param name="eventHandler">The callback to remove from the subscription list</param>
        /// <exception cref="ArgumentNullException">If <paramref name="eventHandler"/> is <c>null</c>.</exception>
        public void RemoveDataBinder<TData, TVisuals>(UIEventHandler<Data.OnBind<TData>, TVisuals, int> eventHandler) where TVisuals : ItemVisuals
        {
            if (eventHandler == null)
            {
                throw new ArgumentNullException(nameof(eventHandler));
            }

            prefabPool.RemovePrefabToDataTypeMapping<TVisuals, TData>();

            RemoveCallback(eventHandler);
        }

        /// <summary>
        /// Subscribe to an indexed <see cref="IGestureEvent"/> event on this <see cref="ListView"/>'s set of list items
        /// </summary>
        /// <typeparam name="TEvent">The type of gesture event to handle for list items of type <typeparamref name="TVisuals"/></typeparam>
        /// <typeparam name="TVisuals">The type of <see cref="ItemVisuals"/>, configured on a list item prefab, to target.</typeparam>
        /// <param name="eventHandler">The callback invoked when the event fires</param>
        /// <exception cref="ArgumentNullException">If <paramref name="eventHandler"/> is <c>null</c>.</exception>
        /// <seealso cref="Gesture.OnClick"/>
        /// <seealso cref="Gesture.OnPress"/>
        /// <seealso cref="Gesture.OnRelease"/>
        /// <seealso cref="Gesture.OnHover"/>
        /// <seealso cref="Gesture.OnUnhover"/>
        /// <seealso cref="Gesture.OnScroll"/>
        /// <seealso cref="Gesture.OnMove"/>
        /// <seealso cref="Gesture.OnDrag"/>
        /// <seealso cref="Gesture.OnCancel"/>
        public void AddGestureHandler<TEvent, TVisuals>(UIEventHandler<TEvent, TVisuals, int> eventHandler) where TEvent : struct, IGestureEvent where TVisuals : ItemVisuals
        {
            if (eventHandler == null)
            {
                throw new ArgumentNullException(nameof(eventHandler));
            }

            AddCallback(eventHandler);
        }

        /// <summary>
        /// Unsubscribe from a gesture event previously subscribed to via <see cref="ListView.AddGestureHandler{TEvent, TVisuals}(UIEventHandler{TEvent, TVisuals, int})"/>
        /// </summary>
        /// <typeparam name="TEvent">The type of gesture event to handle for list items of type <typeparamref name="TVisuals"/></typeparam>
        /// <typeparam name="TVisuals">The type of <see cref="ItemVisuals"/>, configured on a list item prefab, to target.</typeparam>
        /// <param name="eventHandler">The callback to remove from the subscription list</param>
        /// <exception cref="ArgumentNullException">If <paramref name="eventHandler"/> is <c>null</c>.</exception>
        /// <seealso cref="Gesture.OnClick"/>
        /// <seealso cref="Gesture.OnPress"/>
        /// <seealso cref="Gesture.OnRelease"/>
        /// <seealso cref="Gesture.OnHover"/>
        /// <seealso cref="Gesture.OnUnhover"/>
        /// <seealso cref="Gesture.OnScroll"/>
        /// <seealso cref="Gesture.OnMove"/>
        /// <seealso cref="Gesture.OnDrag"/>
        /// <seealso cref="Gesture.OnCancel"/>
        public void RemoveGestureHandler<TEvent, TVisuals>(UIEventHandler<TEvent, TVisuals, int> eventHandler) where TEvent : struct, IGestureEvent where TVisuals : ItemVisuals
        {
            if (eventHandler == null)
            {
                throw new ArgumentNullException(nameof(eventHandler));
            }

            RemoveCallback(eventHandler);
        }
        #endregion

        #region Internal
        [SerializeField]
        [Tooltip("The prefabs to use when binding items in the data source.")]
        private List<ItemView> listItemPrefabs = null;

        [SerializeField]
        [Tooltip("The distance a list item must be out of view before it's removed and added back into the list item pool.\nCan be used as a spatial buffer in the event the rendered size of a list item extends beyond its layout size.")]
        private float outOfViewDistance;

        private protected virtual int SecondaryAxisItemCount => 1;
        int IScrollableView.ItemsPerRow => SecondaryAxisItemCount;

        [NonSerialized, HideInInspector]
        private protected int lowestPagedInIndex = 0;
        [NonSerialized, HideInInspector]
        private protected int highestPagedInIndex = -1;
        [SerializeField, HideInInspector]
        private UIBlock _uiBlock = null;
        private protected UIBlock SerializedRoot => _uiBlock;

        float IScrollableView.EstimatedTotalContentSize
        {
            get
            {
                if (UIBlock.GetAutoLayoutReadOnly().Axis.TryGetIndex(out int axis) &&
                    highestPagedInIndex - lowestPagedInIndex + 1 == DataSourceItemCount)
                {
                    // if everything's in view, we don't need to estimate
                    return viewportVirtualizer.ScrolledContentSize[axis];
                }

                return GetEstimatedSize(DataSourceItemCount);
            }
        }

        float IScrollableView.EstimatedScrollableRange
        {
            get
            {
                float size = View.EstimatedTotalContentSize;

                if (UIBlock.GetAutoLayoutReadOnly().Axis.TryGetIndex(out int axis))
                {
                    size -= View.ScrollRange[axis];
                }

                return Mathf.Max(size, 0);
            }
        }

        [NonSerialized, HideInInspector]
        private VirtualizedView viewportVirtualizer = new VirtualizedView();

        private protected VirtualizedView ScrollProcessor => viewportVirtualizer;

        [NonSerialized, HideInInspector]
        private SimpleMovingAverage estimatedRowSize = default;

        [NonSerialized, HideInInspector]
        PrefabPool<int> prefabPool = new PrefabPool<int>();

        [NonSerialized, HideInInspector]
        private bool settingDataSource = false;

        [NonSerialized, HideInInspector]
        private Dictionary<MulticastDelegate, MulticastDelegate> remappedEvents = new Dictionary<MulticastDelegate, MulticastDelegate>();

        [NonSerialized, HideInInspector]
        private protected Dictionary<int, (ItemView Item, Type UserDataType, DataStoreID ID)> pagedInItems = new Dictionary<int, (ItemView Item, Type UserDataType, DataStoreID ID)>();

        [NonSerialized, HideInInspector]
        private HashSet<int> detachedItemIDs = new HashSet<int>();

        [field: NonSerialized, HideInInspector]
        private protected ListWrapper DataWrapper { get; private set; }

        [NonSerialized]
        private UIBlockActivator activator = null;
        [NonSerialized]
        private bool initialized = false;
        [NonSerialized]
        private bool haveInitializedView = false;

        Vector3 IScrollableView.ScrollRange => UIBlock.PaddedSize;
        Vector3 IScrollableView.ClipSize
        {
            get
            {
                Vector3 extraSpace = Vector3.zero;

                if (UIBlock.GetAutoLayoutReadOnly().Axis.TryGetIndex(out int index))
                {
                    extraSpace[index] = 2 * OutOfViewDistance;
                }

                return UIBlock.CalculatedSize.Value + extraSpace;
            }
        }

        private protected IScrollableView View => this;

        float IScrollableView.EstimatedPosition
        {
            get
            {
                AutoLayout layout = UIBlock.GetAutoLayoutReadOnly();

                if (!layout.Axis.TryGetIndex(out int scrollAxis))
                {
                    return 0;
                }

                float direction = layout.ContentDirection;

                int outgoingCount = Mathf.Clamp(lowestPagedInIndex, 0, DataSourceItemCount - 1);
                int incomingCount = DataSourceItemCount - Mathf.Clamp(highestPagedInIndex, 0, DataSourceItemCount - 1) - 1;
                float outgoingSpacing = lowestPagedInIndex > 0 ? UIBlock.CalculatedSpacing.Value : 0;
                float incomingSpacing = highestPagedInIndex < DataSourceItemCount - 1 ? UIBlock.CalculatedSpacing.Value : 0;
                float outgoingSize = -direction * (GetEstimatedSize(outgoingCount) + outgoingSpacing);
                float incomingSize = direction * (GetEstimatedSize(incomingCount) + incomingSpacing);
                float contentPosition = viewportVirtualizer.ScrolledContentPosition[scrollAxis];

                return contentPosition - UIBlock.CalculatedPadding.Offset[scrollAxis] + 0.5f * (outgoingSize + incomingSize);
            }
        }

        float IScrollableView.EstimatedOffset => GetEstimatedOffset();

        Vector3 IScrollableView.ContentInViewSize => viewportVirtualizer.ScrolledContentSize;
        Vector3 IScrollableView.ContentInViewPosition => viewportVirtualizer.ScrolledContentPosition;
        int IScrollableView.ItemCount => DataSourceItemCount;
        ReadOnlyList<DataStoreID> IScrollableView.ChildIDs => GetChildIDs();

        private protected virtual ReadOnlyList<DataStoreID> GetChildIDs()
        {
            return ((ICoreBlock)UIBlock).ChildIDs.ToReadOnly();
        }

        private void InitializeView()
        {
            ref AutoLayout layout = ref UIBlock.AutoLayout;

            layout.Offset = 0;

            viewportVirtualizer.Init(View, UIBlock, UIBlock.ID);

            if (DataWrapper == null)
            {
                return;
            }

            lowestPagedInIndex = 0;
            highestPagedInIndex = -1;

            if (!haveInitializedView && Internal.NovaSettings.Config.ShouldLog(Internal.LogFlags.ListViewUntrackedItemsUnderRoot) && UIBlock.ChildCount != 0)
            {
                string nameToUse = name;
                nameToUse = string.IsNullOrWhiteSpace(nameToUse) ? "GameObject" : nameToUse;

                Debug.LogWarning($"{nameToUse}'s {nameof(ListView)} has {nameof(Nova.UIBlock)} children before being initialized. Manually adding {nameof(Nova.UIBlock)} children to a {nameof(ListView)} is unsupported and will cause issues, as a {nameof(ListView)} must have full control over its children. {Constants.LogDisableMessage}", this);
            }

            haveInitializedView = true;

            PopulateView(append: true);

            viewportVirtualizer.SyncToRoot();

            if (layout.Alignment != 0)
            {
                JumpToIndex(0);
            }
        }

        private void PopulateView(bool append)
        {
            UIBlock root = UIBlock;

            root.CalculateLayout();

            ref AutoLayout layout = ref root.AutoLayout;

            if (!layout.Enabled)
            {
                Debug.LogError($"Auto Layout is disabled on the {GetType().FullName}, {root.name}.", root);
                return;
            }

            int axis = layout.Axis.Index();

            bool shrink = UIBlock.AutoSize[axis] == AutoSize.Shrink;

            float size;

            if (shrink)
            {
                MinMax range = UIBlock.SizeMinMax[axis];

                size = range.Max + 2 * OutOfViewDistance;
            }
            else
            {
                size = View.ClipSize[axis];
            }

            float addedSize = 0;

            while (addedSize <= size)
            {
                IUIBlockBase block = View.TryPageInItems(nextItem: append, size - addedSize);

                if (block == null)
                {
                    break;
                }

                float blockSize = block.LayoutSize[axis];

                size = Mathf.Max(View.ClipSize[axis], size);
                addedSize += blockSize + UIBlock.CalculatedSpacing.Value;
            }
        }

        private void Clear(bool finalize = true, bool teardown = false)
        {
            // if size changes, this currently doesn't handle that completely -- might just work for simple cases
            while (pagedInItems.Count > 0)
            {
                PageOutItem(fromFront: false);
            }

            if (finalize)
            {
                View.FinalizeItemsForView();
            }

            lowestPagedInIndex = 0;
            highestPagedInIndex = -1;

            UIBlock.AutoLayout.Offset = 0;

            if (teardown)
            {
                viewportVirtualizer.Dispose();
                prefabPool.DestroyPooledPrefabs();
                TearDown();
            }
        }

        private float JumpToIndexPageInternal(int index, bool syncToVirtualizer)
        {
            if (!UIBlock.Activated || DataWrapper == null || DataWrapper.IsEmpty)
            {
                return 0;
            }

            if (index < 0 || index >= DataWrapper.Count)
            {
                throw new IndexOutOfRangeException($"Expected: [0, {DataWrapper.Count}). Actual: {index}.");
            }

            AutoLayout layout = UIBlock.GetAutoLayoutReadOnly();

            if (!layout.Axis.TryGetIndex(out int axis))
            {
                throw new InvalidOperationException($"{UIBlock.name}'s Auto Layout axis is unassigned.");
            }

            bool inView = TryGetItemView(index, out _);
            bool pageInHigherOrderItems = index < lowestPagedInIndex;

            if (!inView)
            {
                Clear(finalize: false);

                int paddedItems = index % SecondaryAxisItemCount;

                if (pageInHigherOrderItems)
                {
                    lowestPagedInIndex = Mathf.Max(index - paddedItems + SecondaryAxisItemCount, 0);
                    highestPagedInIndex = lowestPagedInIndex - 1;
                }
                else
                {
                    highestPagedInIndex = Mathf.Min(index - paddedItems + SecondaryAxisItemCount - 1, DataWrapper.Count - 1);
                    lowestPagedInIndex = highestPagedInIndex + 1;
                }

                PopulateView(append: pageInHigherOrderItems);
                View.TryPageInItems(nextItem: false, Mathf.Max(0, UIBlock.CalculatedSize.Value[axis] - UIBlock.ContentSize[axis]));
            }

            float offset = 0;

            if (TryGetPrimaryAxisItemFromSourceIndex(index, out IUIBlock listItem))
            {
                float size = listItem.CalculatedSize[axis].Value;

                if (size == 0)
                {
                    // Likely not initialized, so force a calculation.
                    listItem.CalculateLayout();
                    size = listItem.CalculatedSize[axis].Value;
                }

                if (!inView && pageInHigherOrderItems != layout.PositioningInverted)
                {
                    UIBlock.AutoLayout.Offset -= size + UIBlock.CalculatedSpacing.Value;
                }

                View.FinalizeItemsForView();
                UIBlock.CalculateLayout();

                offset = LayoutUtils.GetMinDistanceToParentEdge(listItem, axis, layout.Alignment);
            }

            if (syncToVirtualizer)
            {
                viewportVirtualizer.SyncToRoot();
            }

            return Math.ApproximatelyZeroToZero(offset);
        }

        private bool TryGetListItemInternal(int key, out DataStoreID dataStoreID, out ItemView itemView)
        {
            if (!pagedInItems.TryGetValue(key, out var vals))
            {
                dataStoreID = DataStoreID.Invalid;
                itemView = null;
                return false;
            }

            dataStoreID = vals.ID;
            itemView = vals.Item;
            return itemView != null;
        }

        private protected virtual void TearDown() { }

        private float GetEstimatedOffset()
        {
            AutoLayout layout = UIBlock.GetAutoLayoutReadOnly();

            if (!layout.Axis.TryGetIndex(out int scrollAxis))
            {
                return 0;
            }

            float estimatedPosition = View.EstimatedPosition;
            return LayoutUtils.LocalPositionToLayoutOffset(estimatedPosition, View.EstimatedTotalContentSize, View.ScrollRange[scrollAxis], UIBlock.CalculatedPadding.Offset[scrollAxis], layout.Alignment);
        }

        private float GetEstimatedSize(int count)
        {
            AutoLayout layout = UIBlock.GetAutoLayoutReadOnly();

            if (!layout.Axis.TryGetIndex(out _) || count <= 0)
            {
                return 0;
            }

            float rows = Mathf.Ceil((float)count / SecondaryAxisItemCount);

            return (rows * estimatedRowSize.Value) + ((rows - 1) * UIBlock.CalculatedSpacing.Value);
        }

        private static void EventCallback<TEvent, TTarget>(ListView view, TEvent evt, TTarget target, UIEventHandler<TEvent, TTarget, int> eventHandler) where TEvent : struct, IEvent where TTarget : ItemVisuals
        {
            try
            {
                bool foundItem = false;
                UIBlock listObject = evt.Target;

                if (!view.prefabPool.TryGetKey(listObject.ID, out int index))
                {
                    if (listObject.transform == view.transform)
                    {
                        // Technically this could happen if there's an ItemView on a ListView,
                        // but in that case we don't expect to fire an indexed event,
                        // so we can just ignore it.
                        return;
                    }

                    if (!listObject.transform.IsChildOf(view.transform))
                    {
                        goto NotFound;
                    }

                    while (listObject != view.UIBlock.gameObject)
                    {
                        listObject = listObject.Parent;

                        if (view.prefabPool.TryGetKey(listObject.ID, out index))
                        {
                            foundItem = true;
                            break;
                        }
                    }

                    NotFound:
                    if (!foundItem)
                    {
                        return;
                    }
                }

                eventHandler?.Invoke(evt, target, index);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private protected virtual void FinalizeItems() { }

        void IScrollableView.FinalizeItemsForView()
        {
            prefabPool.FinalizePoolForCurrentFrame();

            FinalizeItems();
        }

        bool IScrollableView.TryPageOutItem(bool fromFront)
        {
            bool removeFromBack = !fromFront;
            fromFront &= lowestPagedInIndex >= 0;

            if (!fromFront && !removeFromBack)
            {
                return false;
            }

            PageOutItem(fromFront: fromFront);

            return true;
        }

        bool IScrollableView.TryGetItemAtIndex(int sourceIndex, out IUIBlock item)
        {
            item = null;

            if (TryGetListItemInternal(sourceIndex, out _, out ItemView view))
            {
                item = view.UIBlock;
                return true;
            }

            return false;
        }

        bool IScrollableView.TryGetIndexOfItem(IUIBlock listItem, out int sourceIndex)
        {
            if (listItem == null)
            {
                sourceIndex = -1;
                return false;
            }

            return prefabPool.TryGetKey(listItem.UniqueID, out sourceIndex);
        }

        IUIBlockBase IScrollableView.TryPageInItems(bool nextItem, float emptySpace)
        {
            if (DataWrapper == null)
            {
                return null;
            }

            if (!nextItem && lowestPagedInIndex == 0 && OnScrolledPastStart != null)
            {
                try
                {
                    OnScrolledPastStart.Invoke(emptySpace);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            else if (nextItem && highestPagedInIndex == DataWrapper.Count - 1 && OnScrolledPastEnd != null)
            {
                try
                {
                    OnScrolledPastEnd.Invoke(emptySpace);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            bool addToFront = !nextItem && lowestPagedInIndex > 0;
            bool addToBack = nextItem && highestPagedInIndex < DataWrapper.Count - 1;

            if (!addToFront && !addToBack)
            {
                return null;
            }

            IUIBlockBase listItem = PageInItem(firstSibling: addToFront);

            // listItem can be null if we don't find a prefab match or the user returns null in a handler
            if (listItem != null)
            {
                float itemSize = listItem.CalculatedSize[UIBlock.GetAutoLayoutReadOnly().Axis.Index()].Value;

                if (estimatedRowSize.Weight == 0)
                {
                    estimatedRowSize = new SimpleMovingAverage(itemSize, 0.1f);
                }
                else
                {
                    estimatedRowSize.AddSample(itemSize);
                }
            }

            return listItem;
        }

        bool IScrollableView.TryGetPrimaryAxisItemFromSourceIndex(int index, out IUIBlock item) => TryGetPrimaryAxisItemFromSourceIndex(index, out item);


        private protected virtual bool TryGetPrimaryAxisItemFromSourceIndex(int index, out IUIBlock item)
        {
            item = null;
            if (!TryGetItemView(index, out ItemView listItem))
            {
                return false;
            }

            item = listItem.UIBlock;

            return true;
        }

        private protected virtual void PageOutItem(bool fromFront)
        {
            if (fromFront)
            {
                RemoveFromFront();
            }
            else
            {
                RemoveFromBack();
            }
        }

        private protected virtual IUIBlockBase PageInItem(bool firstSibling)
        {
            return firstSibling ? PageInPreviousItem(rebuildLayout: true) : PageInNextItem(rebuildLayout: true);
        }

        private protected IUIBlockBase PageInPreviousItem(bool rebuildLayout)
        {
            lowestPagedInIndex--;

            if (!TryAddItem(lowestPagedInIndex, out ItemView item))
            {
                // failed to add a new element at the requested index probably means
                // prefab (potentially of a specific type) could not be found
                lowestPagedInIndex++;

                return null;
            }

            item.UIBlock.SetAsFirstSibling();

            if (rebuildLayout)
            {
                item.UIBlock.CalculateLayout();
            }

            return item.UIBlock;
        }

        private protected IUIBlockBase PageInNextItem(bool rebuildLayout)
        {
            highestPagedInIndex++;
            if (!TryAddItem(highestPagedInIndex, out ItemView item))
            {
                // failed to add a new element at the requested index probably means
                // prefab (potentially of a specific type) could not be found
                highestPagedInIndex--;
                return null;
            }

            item.UIBlock.SetAsLastSibling();

            if (rebuildLayout)
            {
                item.UIBlock.CalculateLayout();
            }

            return item.UIBlock;
        }

        private protected void RemoveFromFront()
        {
            RemoveListItem(lowestPagedInIndex);
            lowestPagedInIndex = Mathf.Min(DataWrapper.Count - 1, lowestPagedInIndex + 1);
        }

        private protected void RemoveFromBack()
        {
            RemoveListItem(highestPagedInIndex);
            highestPagedInIndex = Mathf.Max(-1, highestPagedInIndex - 1);
        }

        private bool TryAddItem(int index, out ItemView item)
        {
            if (index < 0 || index >= DataWrapper.Count)
            {
                throw new IndexOutOfRangeException($"Index out of range. Expected [0, {DataWrapper.Count}) but received {index}.");
            }

            Type dataType = DataWrapper.GetDataType(index);
            PrefabRetrieval result = prefabPool.GetPrefabInstance(index, dataType, out item, out Type userDataType);

            if (item == null)
            {
                if (result == PrefabRetrieval.TypeMatchFailed)
                {
                    string message = $"Unable to find a corresponding prefab type for data object of type {dataType.Name}.";
                    string secondHalf = settingDataSource ? $" Make sure any desired databind handlers are registered before calling {nameof(SetDataSource)}." : string.Empty;
                    Debug.LogError($"{message}{secondHalf}", this);
                }

                return false;
            }

            if (item.transform.parent != UIBlock.transform)
            {
                item.transform.SetParent(UIBlock.transform, false);
            }

            detachedItemIDs.Remove(index);
            pagedInItems[index] = (item, userDataType, item.UIBlock.ID);

            PrefabPool<int>.GetDataBinder(userDataType).InvokeBind(item, DataWrapper, index);

            viewportVirtualizer.ItemAddedToView(item.UIBlock.ID);


            return true;
        }

        [NonSerialized, HideInInspector]
        private bool haveLoggedDestroyWarning = false;
        private void RemoveListItem(int index, bool finalize = false)
        {
            bool found = TryGetListItemInternal(index, out DataStoreID dataStoreID, out ItemView item);

            if (!found && !dataStoreID.IsValid)
            {
                // The item was not found, but it wasn't in the dictionary
                if (!detachedItemIDs.Remove(index))
                {
                    Debug.LogError($"Not tracking list item for index {index}. Current range [{MinLoadedIndex}, {MaxLoadedIndex}]. Use ListView.{nameof(TryDetach)}() to manually remove an item from the List View.");
                }

                return;
            }

            Type userDataType = null;
            if (pagedInItems.TryGetValue(index, out var value))
            {
                userDataType = value.UserDataType;
                pagedInItems.Remove(index);
            }

            viewportVirtualizer.ItemRemovedFromView(dataStoreID);

            if (found)
            {
                // Valid item, so unbind and return to pool
                Type unbinderDataType = index < DataSourceItemCount ? prefabPool.GetUserDataType(item, DataWrapper.GetDataType(index)) : userDataType;
                PrefabPool<int>.GetDataBinder(unbinderDataType).InvokeUnbind(item, DataWrapper, index);
                prefabPool.ReturnPrefabInstance(item, index);
            }
            else
            {
                // Item was destroyed, so notify the prefab pool to stop tracking
                prefabPool.Remove(dataStoreID);

                if (!haveLoggedDestroyWarning && Internal.NovaSettings.Config.ShouldLog(Internal.LogFlags.ListViewItemDestroyed))
                {
                    haveLoggedDestroyWarning = true;
                    Debug.LogWarning($"ListView item was destroyed without being detached first. This is unsupported and may cause issues. {Constants.LogDisableMessage}", this);
                }
            }


            if (finalize)
            {
                prefabPool.FinalizePoolForCurrentFrame();
            }
        }

        bool IScrollableView.HasContentInDirection(float direction)
        {
            if (DataWrapper == null || DataWrapper.IsEmpty)
            {
                return false;
            }

            bool nextItem = UIBlock.GetAutoLayoutReadOnly().ContentDirection == direction;
            return nextItem ? highestPagedInIndex < DataWrapper.Count - 1 : lowestPagedInIndex > 0;
        }

        private void EditorOnly_AssemblyReload_TearDown()
        {
            if (this == null)
            {
                return;
            }

            Clear(teardown: true);

            NovaApplication.EditorBeforeAssemblyReload -= EditorOnly_AssemblyReload_TearDown;
        }

        void IGameObjectActiveReceiver.HandleOnEnable()
        {
            if (!NovaApplication.InPlayer(this))
            {
                return;
            }

            if (NovaApplication.IsEditor)
            {
                NovaApplication.EditorBeforeAssemblyReload -= EditorOnly_AssemblyReload_TearDown;
                NovaApplication.EditorBeforeAssemblyReload += EditorOnly_AssemblyReload_TearDown;
            }

            viewportVirtualizer.Init(View, UIBlock, UIBlock.ID);

            EnsureInitialized();

            HandleOnEnabled();
        }

        void IGameObjectActiveReceiver.HandleOnDisable()
        {
            if (!NovaApplication.InPlayer(this))
            {
                return;
            }

            viewportVirtualizer.Dispose();

            HandleOnDisabled();
        }

        private protected virtual void HandleOnEnabled() { }
        private protected virtual void HandleOnDisabled() { }
        private protected virtual void Init() { }

        internal int GetDataIndex(int index) => lowestPagedInIndex + index;
        private protected virtual void OnDestroy()
        {
            if (activator != null)
            {
                activator.Unregister(this);
            }

            TearDown();
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            if (TryGetComponent(out activator))
            {
                activator.Register(this);
            }

            Init();

            initialized = true;
        }

        private void AddCallback<TEvent, TVisuals>(UIEventHandler<TEvent, TVisuals, int> eventHandler) where TEvent : struct, IEvent where TVisuals : ItemVisuals
        {
            if (eventHandler == null)
            {
                return;
            }

            if (remappedEvents.TryGetValue(eventHandler, out var wrapper))
            {
                Debug.LogError($"Provided event handler is already registered with the {GetType().Name}, {name}.", this);
                return;
            }

            UIEventHandler<TEvent, TVisuals> eventWrapper = (evt, target) => EventCallback(this, evt, target, eventHandler);

            remappedEvents[eventHandler] = eventWrapper;

            UIBlock.AddEventHandler(eventWrapper);
        }

        private void RemoveCallback<TEvent, TVisuals>(UIEventHandler<TEvent, TVisuals, int> eventHandler) where TEvent : struct, IEvent where TVisuals : ItemVisuals
        {
            if (eventHandler == null)
            {
                return;
            }

            if (!remappedEvents.TryGetValue(eventHandler, out var eventWrapper))
            {
                return;
            }

            remappedEvents.Remove(eventHandler);

            if (!(eventWrapper is UIEventHandler<TEvent, TVisuals> typedHandler))
            {
                Debug.Log($"Event handler type mismatch. Provided handler is of type {eventHandler.GetType()}, but the caller specified the handler would be of type {typeof(UIEventHandler<TEvent, TVisuals, int>)}.");
                return;
            }

            UIBlock.RemoveEventHandler(typedHandler);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize() => ClearSerializedUIBlock();

        private protected void ClearSerializedUIBlock()
        {
            if (!NovaApplication.IsPlaying && _uiBlock != null)
            {
                // Ensure this is null in edit mode, 
                // even if something was copy/pasted
                // from play mode.
                _uiBlock = null;
            }
        }

        /// <summary>
        /// Prevent users from inheriting
        /// </summary>
        internal ListView() { }
        #endregion
    }
}
