// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Rendering;
using Nova.Internal.Utilities;
using System;
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// Configures the rendered sorting order of a <see cref="Nova.UIBlock"/> hierarchy.
    /// </summary>
    /// <seealso cref="UIBlock2D.ZIndex"/>
    /// <seealso cref="TextBlock.ZIndex"/>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("Nova/Sort Group")]
    [RequireComponent(typeof(UIBlock))]
    [HelpURL("https://novaui.io/manual/RenderOrder.html")]
    public sealed class SortGroup : MonoBehaviour
    {
        #region Public
        /// <summary>
        /// The sorting order of this <see cref="UIBlock"/> hierarchy. Hierarchies with a higher sorting order render on top of those with a lower sorting order.
        /// </summary>
        /// <remarks>
        /// The default value for hierarchies <i>without</i> a <see cref="SortGroup"/> component is <c>0</c>.
        /// </remarks>
        /// <seealso cref="UIBlock2D.ZIndex"/>
        /// <seealso cref="TextBlock.ZIndex"/>
        /// <seealso cref="RenderQueue"/>
        public int SortingOrder
        {
            get => info.SortingOrder;
            set
            {
                if (info.SortingOrder == value)
                {
                    return;
                }
                info.SortingOrder = value;
                RegisterOrUpdate();
            }
        }

        /// <summary>
        /// The render queue value to set on all (transparent) materials used to render this <see cref="UIBlock"/> hierarchy.
        /// </summary>
        /// <remarks>
        /// The default value for hierarchies <i>without</i> a <see cref="SortGroup"/> component is <c>3000</c>.
        /// </remarks>
        /// <seealso cref="UIBlock2D.ZIndex"/>
        /// <seealso cref="TextBlock.ZIndex"/>
        /// <seealso cref="SortingOrder"/>
        public int RenderQueue
        {
            get => info.RenderQueue;
            set
            {
                value = Mathf.Clamp(value, 0, Constants.MaxRenderQueue);
                if (info.RenderQueue == value)
                {
                    return;
                }

                info.RenderQueue = value;
                RegisterOrUpdate();
            }
        }

        /// <summary>
        /// Whether or not the content in the sort group should render over geometry rendered in the 
        /// opaque render queue. This is useful for rendering in screen space.
        /// </summary>
        public bool RenderOverOpaqueGeometry
        {
            get => info.RenderOverOpaqueGeometry;
            set
            {
                if (info.RenderOverOpaqueGeometry == value)
                {
                    return;
                }

                info.RenderOverOpaqueGeometry = value;
                RegisterOrUpdate();
            }
        }
        
        /// <summary>
        /// The <see cref="Nova.UIBlock"/> on <c>this.gameObject</c>.
        /// </summary>
        public UIBlock UIBlock
        {
            get
            {
                if (uiBlock == null)
                {
                    uiBlock = GetComponent<UIBlock>();
                }

                return uiBlock;
            }
        }
        #endregion

        #region Internal
        [NonSerialized]
        private UIBlock uiBlock = null;
        [SerializeField]
        private SortGroupInfo info = SortGroupInfo.Default;

        internal void RegisterOrUpdate()
        {
            if (!UIBlock.Activated || !enabled)
            {
                return;
            }

            RenderingDataStore.Instance.AddOrUpdateSortGroup(UIBlock.ID, UnsafeUtility.As<SortGroupInfo, Internal.SortGroupInfo>(ref info));
        }

        private void Unregister(DataStoreID id)
        {
            if (RenderingDataStore.Instance == null)
            {
                return;
            }

            RenderingDataStore.Instance.RemoveSortGroup(id);
        }

        private void OnEnable()
        {
            UIBlock.EnsureRegistration();

            RegisterOrUpdate();
        }

        private void OnDisable()
        {
            Unregister(UIBlock.ID);
            
            if (NovaApplication.IsEditor)
            {
                DataStoreID id = UIBlock.ID;
                NovaApplication.EditorDelayCall += () =>
                {
                    if (this == null)
                    {
                        Unregister(id);
                    }
                };
            }
        }

        /// <summary>
        /// This is an undocumented event from Unity. It is called
        /// whenever an Animator component updates a serialized field
        /// on this object.
        /// </summary>
        [Obfuscation]
        private void OnDidApplyAnimationProperties()
        {
            RegisterOrUpdate();
        }
        #endregion
    }
}

