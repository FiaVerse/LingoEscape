// Copyright (c) Supernova Technologies LLC
//#define LOG_EVERYTHING
using Nova.Compat;
using Nova.Extensions;
using Nova.Internal.Layouts;
using Nova.Internal.Rendering;
using Nova.Internal.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// A <see cref="UIBlock"/> for rendering text
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(TMP.TextMeshProTextBlock))]
    [AddComponentMenu("Nova/TextBlock")]
    [HelpURL("https://novaui.io/manual/TextBlock.html")]
    public sealed class TextBlock : UIBlock, ITextBlock
    {
        #region Public
        /// <summary>
        /// A shorthand for setting TMP.text
        /// </summary>
        public string Text
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => TMP.text;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                TMP.text = value;
            }
        }

        /// <summary>
        /// The TextMeshPro component attached to <c>this.gameObject</c>, used to generate the underlying text mesh.
        /// </summary>
        public TextMeshPro TMP
        {
            get
            {
                if (_tmp == null)
                {
                    _tmp = GetComponent<TextMeshPro>();
                }
                return _tmp;
            }
        }

        /// <summary>
        /// The primary color of the text. Writes directly to <see cref="TMP"/>.Color.
        /// </summary>
        public override Color Color
        {
            get => TMP.color;
            set
            {
                TMP.color = value;
            }
        }

        /// <summary>
        /// Configures the render order of this UIBlock within a <see cref="SortGroup"/>.
        /// </summary>
        /// <remarks><see cref="UIBlock"/>s with a higher ZIndex are rendered on top of <see cref="UIBlock"/>s with a lower ZIndex.</remarks>
        public short ZIndex
        {
            get => visibility.ZIndex;
            set
            {
                visibility.ZIndex = value;
                RenderingDataStore.Instance.CopyBaseInfoToStore(this);
            }
        }

        /// <summary>
        /// The offset that is applied whenever text is being hugged (i.e. <see cref="UIBlock.AutoSize">AutoSize</see> is set to <see cref="AutoSize.Shrink">Shrink</see> on <c>x</c> or <c>y</c>).
        /// If text is not being hugged, this will be <see cref="Vector2.zero"/>.
        /// </summary>
        public Vector2 VisualOffset
        {
            get
            {
                if (!Self.IsRegistered || !ShrinkingAnyAxis)
                {
                    return Vector2.zero;
                }

                ref Internal.TextBlockData data = ref RenderingDataStore.Instance.Access(this, -1);
                var shrinkConfig = AutoSize.XY;
                bool2 shrink = new bool2(shrinkConfig.X == Nova.AutoSize.Shrink, shrinkConfig.Y == Nova.AutoSize.Shrink);
                return data.GetPositionalOffset(shrink);
            }
        }
        #endregion

        #region Internal
        [NonSerialized, HideInInspector]
        private TextMargin cachedMargin = TextMargin.Invalid;

        ref Internal.TextBlockData IRenderBlock<Internal.TextBlockData>.RenderData
        {
            // Nothing is actually stored on the block itself (since nothing needs to be serialized)
            get => throw new NotImplementedException("Shouldn't call this on text blocks");
        }

        /// <summary>
        /// bool2 mask indicate text is set to shrink on X or Y
        /// </summary>
        internal bool2 RawTextShrinkMask
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (AutoSize.XY == Nova.AutoSize.Shrink).ToBool2();
        }

        private bool ShrinkingAnyAxis
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => math.any(RawTextShrinkMask);
        }

        [NonSerialized]
        private bool _haveReceivedLayoutUpdate = false;
        private bool HaveReceivedLayoutUpdate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _haveReceivedLayoutUpdate;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                _haveReceivedLayoutUpdate = value;
            }
        }

        /// <summary>
        /// Returns whether or not the margin has changed
        /// </summary>
        /// <param name="newMargin"></param>
        /// <returns></returns>
        private bool TrySetMargin(TextMargin newMargin)
        {

            if (cachedMargin == newMargin)
            {
                return false;
            }

            cachedMargin = newMargin;
            TMP.margin = newMargin.Value;

            return true;
        }

        /// <summary>
        /// Updates the TMP margin and rebuilds the text mesh when necessary
        /// </summary>
        void ITextBlock.UpdateMeshSize(ref TextMargin newMargin)
        {
            HaveReceivedLayoutUpdate = true;

            if (!TrySetMargin(newMargin))
            {
                // Margin didn't change, don't need to rebuild
                return;
            }

            // Margin changed, so force an update and then unregister so we don't
            // get a double update
            TMP.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);
            UnregisterFromRebuild();
        }

        /// <summary>
        /// Annoyingly, UnRegisterTextElementForRebuild also removes the TMP component from the update loop, which
        /// is needed to check for scale changes
        /// </summary>
        private void UnregisterFromRebuild()
        {
            TMP_UpdateManager.UnRegisterTextElementForRebuild(TMP);
            TMP.isTextObjectScaleStatic = TMP.isTextObjectScaleStatic;
        }


        private void UpdateTextMeshData(TMP_TextInfo textInfo)
        {
            if (!Self.IsRegistered)
            {
                return;
            }


#pragma warning disable CS0162 // Unreachable code detected
            if (NovaApplication.ConstIsEditor && TMP.textInfo.characterCount == 0 && !string.IsNullOrWhiteSpace(TMP.text))
            {
                // Check for the text mesh being empty when the string is not
                this.LogHelpfulWarnings();
            }
#pragma warning restore CS0162 // Unreachable code detected

            ref Internal.TextBlockData renderData = ref RenderingDataStore.Instance.Access(this, textInfo.meshInfo.Length);

            // We can't use meshInfo[i].vertexCount because it doesn't include quads used for highlighting or 
            // other rich text functionality
            // We can't use textInfo.characterCount, because that includes spaces that don't have mesh information
            // associated with them.
            int newQuadCount = 0;
            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                if (i != 0)
                {
                    // Only clear the unused vertices for submeshes, since the first (i.e. primary) mesh
                    // may contain highlights, which are silently stored in the "unused" vertices at the
                    // end of the vertex buffer.
                    textInfo.meshInfo[i].ClearUnusedVertices();
                }
                newQuadCount += textInfo.meshInfo[i].vertices.Length;
            }

            newQuadCount /= 4;

            if (newQuadCount == 0 && renderData.QuadCount == 0)
            {
                return;
            }
            renderData.QuadCount = newQuadCount;
            renderData.LossyYScale = transform.lossyScale.y;

            float2 min = Internal.Utilities.Math.float2_PositiveInfinity;
            float2 max = Internal.Utilities.Math.float2_NegativeInfinity;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                var charInfo = textInfo.characterInfo[i];
                min = math.min(min, new float2(charInfo.origin, charInfo.descender));
                max = math.max(max, new float2(charInfo.xAdvance, charInfo.ascender));
            }

            if (textInfo.characterCount == 0)
            {
                min = float2.zero;
                max = float2.zero;
            }

            renderData.TextBounds = new AABB(min, max);


            int submeshCount = textInfo.meshInfo.Length - 1;
            if (submeshCount > 0)
            {
                (TMP as TMP.TextMeshProTextBlock).DisableSubmeshRenderers();
            }

            for (int i = 0; i < renderData.MeshData.Length; ++i)
            {
                ref Internal.TextBlockMeshData meshData = ref renderData.MeshData.ElementAt(i);
                int newMaterialID = textInfo.meshInfo[i].material.GetInstanceID();
                if (meshData.MaterialID != newMaterialID)
                {
                    RenderingDataStore.Instance.TextMaterials[newMaterialID] = textInfo.meshInfo[i].material;
                }

                TMP_MeshInfo meshInfo = textInfo.meshInfo[i];
                meshData.UpdateInfo(meshInfo);
                unsafe
                {
                    // Unfortunately, this needs to be here due to the core/uncore split
                    TMPUtils.CopyUVs(meshData.UVs0.Ptr, meshData.UVs1.Ptr, ref meshInfo);
                }
            }

            LayoutDataStore.Instance.UpdateShrinkSizeOverride(this, renderData.TextBounds.GetSize().xy);
        }

        private protected override void Register()
        {
            cachedMargin = TextMargin.Invalid;
            ZeroOutRectTransform();
            TMP.OnPreRenderText += UpdateTextMeshHandler;
            TMP.RegisterDirtyVerticesCallback(TMPVertsDirtiedHandler);

            SubscribeToTextChanged(this);

            MeshRenderer.hideFlags = HideFlags.HideInInspector;
            MeshRenderer.enabled = false;

            base.Register();
        }

        private protected override void Unregister()
        {
            TMP.OnPreRenderText -= UpdateTextMeshHandler;
            TMP.UnregisterDirtyVerticesCallback(TMPVertsDirtiedHandler);
            UnsubscribeFromTextChanged(this);
            base.Unregister();
        }

        /// <summary>
        /// Internal event handler for when TMP calls SetVerticesDirty()
        /// </summary>
        private void HandleTMPVertsDirtied()
        {

            if (!HaveReceivedLayoutUpdate)
            {
                // If the layout engine hasn't run yet, we don't want tmp to update the mesh
                // as it doesn't have the correct margin yet
                UnregisterFromRebuild();
            }
            else if (Activated && !TMP.enabled)
            {
                ref Internal.TextBlockData renderData = ref RenderingDataStore.Instance.Access(this, TMP.textInfo.meshInfo.Length);
                renderData.QuadCount = 0;
                renderData.TextBounds = default;
            }
        }

        private static readonly Vector2 half2 = .5f * Vector2.one;
        private void ZeroOutRectTransform()
        {
            RectTransform.anchorMin = half2;
            RectTransform.anchorMax = half2;
            RectTransform.sizeDelta = Vector2.zero;
        }

        internal override void EditorOnly_MarkDirty()
        {
            // We must update known values after base.MarkDirty, otherwise any layout properties
            // we set will get overwritten
            base.EditorOnly_MarkDirty();

            if (!Activated || !HaveReceivedLayoutUpdate)
            {
                // the editor will call mark dirty when a property changes,
                // but if this is disabled, we don't want to try to handle
                // any property changes
                return;
            }

            TrySetMargin(TextMargin.GetMargin(CalculatedSize.XY.Value, SizeMinMax.ToInternal().Max.xy, RawTextShrinkMask));
        }


        #region Cached
        [NonSerialized, HideInInspector]
        private TextMeshPro _tmp = null;

        [NonSerialized, HideInInspector]
        private RectTransform _rectTransform = null;
        private RectTransform RectTransform
        {
            get
            {
                if (_rectTransform == null)
                {
                    _rectTransform = GetComponent<RectTransform>();
                }
                return _rectTransform;
            }
        }

        [NonSerialized, HideInInspector]
        private MeshRenderer _meshRenderer = null;
        private MeshRenderer MeshRenderer
        {
            get
            {
                if (_meshRenderer == null)
                {
                    _meshRenderer = GetComponent<MeshRenderer>();
                }
                return _meshRenderer;
            }
        }

        /// <summary>
        /// Cached dirty vert event handler
        /// </summary>
        [NonSerialized]
        private UnityEngine.Events.UnityAction _tmpVertsDirtiedHandler = null;
        private UnityEngine.Events.UnityAction TMPVertsDirtiedHandler
        {
            get
            {
                if (_tmpVertsDirtiedHandler == null)
                {
                    _tmpVertsDirtiedHandler = HandleTMPVertsDirtied;
                }

                return _tmpVertsDirtiedHandler;
            }
        }

        /// <summary>
        /// Cached PreRender event handler
        /// </summary>
        private Action<TMP_TextInfo> _updateTextMeshHandler = null;
        private Action<TMP_TextInfo> UpdateTextMeshHandler
        {
            get
            {
                if (_updateTextMeshHandler == null)
                {
                    _updateTextMeshHandler = UpdateTextMeshData;
                }

                return _updateTextMeshHandler;
            }
        }
        #endregion

        #region Static
        private static HashSet<TextBlock> subscribedBlocks = new HashSet<TextBlock>();
        private static void SubscribeToTextChanged(TextBlock textBlock)
        {
            if (!subscribedBlocks.Add(textBlock))
            {
                return;
            }

            if (subscribedBlocks.Count == 1)
            {
                TMPro_EventManager.TEXT_CHANGED_EVENT.Add(HandleTextChanged);
            }
        }

        private static void UnsubscribeFromTextChanged(TextBlock textBlock)
        {
            if (!subscribedBlocks.Remove(textBlock))
            {
                return;
            }

            if (subscribedBlocks.Count == 0)
            {
                TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(HandleTextChanged);
            }
        }

        /// <summary>
        /// Since we won't get the PreRender event if the text gets set to empty,
        /// we need to also subscribe to the text changed event
        /// </summary>
        /// <param name="obj"></param>
        private static void HandleTextChanged(UnityEngine.Object obj)
        {
            if (!(obj is TextMeshPro textMeshPro))
            {
                return;
            }



            TextBlock textBlock = textMeshPro.gameObject.GetComponent<TextBlock>();
            if (textBlock == null)
            {
                return;
            }

            if (textBlock.TMP.textInfo.characterCount > 0)
            {
                // We need to handle the situation where text stops rendering for one reason or another.
                // E.g., ellipses enabled with a zero height will cause the text to disappear, but we won't get
                // an event that the mesh data changed
                return;
            }

            textBlock.UpdateTextMeshData(textBlock.TMP.textInfo);
        }
        #endregion

        private TextBlock() : base()
        {
            visibility = BaseRenderInfo.Default(BlockType.Text);
        }
        #endregion
    }
}
