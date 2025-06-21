// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Rendering;
using System;
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// Clips the rendered bounds of a <see cref="UIBlock"/> hierarchy to a rounded-corner rectangle or custom texture.
    /// Supports applying a color <see cref="Tint">tint</see> or fade to the <see cref="UIBlock"/> hierarchy as well.
    /// </summary>
    /// <remarks>
    /// Clipping can be performed via a <see cref="ClipMask.Mask">mask</see> texture or using the rectangular bounds of the
    /// <see cref="UIBlock"/> on this gameobject. If the attached <see cref="UIBlock"/> is a <see cref="UIBlock2D"/>, the <see cref="ClipMask"/>
    /// defaults to clipping to the rounded-corner rectangle bounds matching the <see cref="UIBlock2D"/>'s <see cref="UIBlock2D.CornerRadius">Corner Radius</see>.
    /// </remarks>
    [AddComponentMenu("Nova/Clip Mask")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [HelpURL("https://novaui.io/manual/ClipMasks.html")]
    [RequireComponent(typeof(UIBlock))]
    public sealed class ClipMask : MonoBehaviour
    {
        #region Public
        /// <summary>
        /// The tint color applied to this <see cref="UIBlock"/> and its descendants.<br/>
        /// The tint is applied multiplicatively: <c>finalRenderedColor *= Tint</c>.<br/>
        /// Adjust <c>Tint.a</c> (alpha) to apply a fade. Set <c>Tint</c> to <c>Color.white</c> if no tint is desired.
        /// </summary>
        public Color Tint
        {
            get => info.Color;
            set
            {
                if (info.Color == value)
                {
                    return;
                }

                info.Color = value;
                RegisterOrUpdate();
            }
        }

        /// <summary>
        /// Enables or disables clipping. Can be used to make the <see cref="ClipMask"/> exclusively apply a <see cref="Tint">Tint</see>.
        /// </summary>
        /// <seealso cref="Tint"/>
        public bool Clip
        {
            get => info.Clip;
            set
            {
                if (info.Clip == value)
                {
                    return;
                }

                info.Clip = value;
                RegisterOrUpdate();
            }
        }

        /// <summary>
        /// The texture to use as a mask when <c><see cref="Clip">Clip</see> == true</c>. Clip shape defaults to a rectangle or a rounded-corner rectangle,
        /// depending on the <see cref="UIBlock"/> attached to <c>this.gameObject</c>, when <c>Mask == null</c>.
        /// </summary>
        /// <seealso cref="ClipMask"/>
        public Texture Mask
        {
            get => maskTexture;
            set
            {
                if (maskTexture == value)
                {
                    return;
                }
                maskTexture = value;
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
        private ClipMaskInfo info = ClipMaskInfo.Default;
        [SerializeField]
        private Texture maskTexture = null;

        internal void RegisterOrUpdate()
        {
            if (!UIBlock.Activated || !enabled)
            {
                return;
            }

            info.HasMask = maskTexture != null;

            RenderingDataStore.Instance.VisualModifierTracker.AddOrUpdate(UIBlock.ID, UnsafeUtility.As<ClipMaskInfo, Internal.ClipMaskInfo>(ref info), maskTexture);
        }

        private void Unregister(DataStoreID id)
        {
            if (RenderingDataStore.Instance == null)
            {
                return;
            }

            RenderingDataStore.Instance.VisualModifierTracker.Remove(id);
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

