// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Rendering;
using Nova.Internal.Utilities;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// A <see cref="UIBlock"/> with an adjustable, rounded-corner rectangle mesh.
    /// </summary>
    /// <remarks>
    /// Supports a wide range of stylistic features including:
    /// <list type="bullet">
    /// <item><description>Rendering images</description></item>
    /// <item><description><see cref="Nova.Border"/></description></item>
    /// <item><description><see cref="RadialGradient"/></description></item>
    /// <item><description><see cref="Nova.Shadow"/></description></item>
    /// </list>
    /// </remarks>
    [ExecuteAlways]
    [HelpURL("https://novaui.io/manual/UIBlock2D.html")]
    [AddComponentMenu("Nova/UIBlock 2D")]
    public sealed class UIBlock2D : UIBlock, IUIBlock2D
    {
        #region Public

        /// <summary>
        /// A gradient effect, visually blended with the body <see cref="Color">Color</see> and image (i.e. <see cref="Sprite">Sprite</see>, <see cref="Texture">Texture</see>, or <see cref="RenderTexture">RenderTexture</see>), if it exists.
        /// </summary>
        /// <remarks>
        /// <see cref="Gradient">Gradient</see>.<see cref="RadialGradient.Center">Center</see>.<see cref="Length.Percent">Percent</see> and 
        /// <see cref="Gradient">Gradient</see>.<see cref="RadialGradient.Radius">Radius</see>.<see cref="Length.Percent">Percent</see> are relative to
        /// <see cref="UIBlock.CalculatedSize">CalculatedSize</see>. Mathematically speaking:<br/>
        /// <c>Vector2 calculatedGradientCenter = Gradient.Center.Percent * CalculatedSize.XY.Value <br/>
        /// Vector2 calculatedGradientRadii = Gradient.Radius.Percent * CalculatedSize.XY.Value</c>
        /// </remarks>
        /// <seealso cref="RadialGradient"/>
        public ref RadialGradient Gradient
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref RenderingDataStore.Instance.Access(this).Gradient.Reinterpret<Internal.RadialGradient, RadialGradient>();
        }

        /// <summary>
        /// A visual border drawn around the perimeter of the body.
        /// </summary>
        /// <remarks>
        /// <see cref="Border">Border</see>.<see cref="Border.Width">Width</see>.<see cref="Length.Percent">Percent</see> is relative to half 
        /// the minimum dimension (X or Y) of <see cref="UIBlock.CalculatedSize">CalculatedSize</see>. Mathematically speaking:<br/>
        /// <c>float calculatedBorderWidth = Border.Width.Percent * 0.5f * Mathf.Min(CalculatedSize.X.Value, CalculatedSize.Y.Value)</c>
        /// </remarks>
        /// <seealso cref="Nova.Border"/>
        public ref Border Border
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref RenderingDataStore.Instance.Access(this).Border.Reinterpret<Internal.Border, Border>();
        }

        /// <summary>
        /// The radial fill/cutout configuration.
        /// </summary>
        /// <remarks>
        /// <see cref="RadialFill">RadialFill</see>.<see cref="RadialFill.Center">Center</see>.<see cref="Length.Percent">Percent</see> is 
        /// relative to <see cref="UIBlock.CalculatedSize">CalculatedSize</see>. Mathematically speaking:<br/>
        /// <c>Vector2 calculatedRadialFillCenter = RadialFill.Center.Percent * CalculatedSize.XY.Value</c>
        /// </remarks>
        public ref RadialFill RadialFill
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref RenderingDataStore.Instance.Access(this).RadialFill.Reinterpret<Internal.RadialFill, RadialFill>();
        }

        /// <summary>
        /// A drop shadow, inner shadow, or glow effect.
        /// </summary>
        /// <remarks>
        /// <see cref="Shadow">Shadow</see>.<see cref="Shadow.Width">Width</see>.<see cref="Length.Percent">Percent</see> and 
        /// <see cref="Shadow">Shadow</see>.<see cref="Shadow.Blur">Blur</see>.<see cref="Length.Percent">Percent</see> are 
        /// relative to half the minimum dimension (X or Y) of <see cref="UIBlock.CalculatedSize">CalculatedSize</see>. 
        /// Mathematically speaking:<br/>
        /// <c>float calculatedShadowWidth = Shadow.Width.Percent * 0.5f * Mathf.Min(CalculatedSize.X.Value, CalculatedSize.Y.Value) <br/>
        /// float calculatedShadowBlur = Shadow.Blur.Percent * 0.5f * Mathf.Min(CalculatedSize.X.Value, CalculatedSize.Y.Value)</c> <br/><br/>
        /// <see cref="Shadow">Shadow</see>.<see cref="Shadow.Offset">Offset</see>.<see cref="Length.Percent">Percent</see> is relative to 
        /// <see cref="UIBlock.CalculatedSize">CalculatedSize</see>. Mathematically speaking:<br/>
        ///  <c>Vector2 calculatedShadowOffset = Shadow.Offset.Percent * CalculatedSize.XY.Value</c>
        /// </remarks>
        /// <seealso cref="Nova.Shadow"/>
        public ref Shadow Shadow
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref RenderingDataStore.Instance.Access(this).Shadow.Reinterpret<Internal.Shadow, Shadow>();

        }

        /// <summary>
        /// The position and scale adjustments applied to the attached image (i.e. <see cref="Sprite">Sprite</see>, <see cref="Texture">Texture</see>, or <see cref="RenderTexture">RenderTexture</see>), if it exists.
        /// </summary>
        /// <seealso cref="Nova.ImageAdjustment"/>
        /// <seealso cref="Texture"/>
        /// <seealso cref="Sprite"/>
        /// <seealso cref="SetImage(RenderTexture)"/>
        /// <seealso cref="SetImage(Sprite)"/>
        /// <seealso cref="SetImage(Texture2D)"/>
        public ref ImageAdjustment ImageAdjustment
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref RenderingDataStore.Instance.Access(this).Image.Adjustment.Reinterpret<Internal.ImageAdjustment, ImageAdjustment>();
        }

        /// <summary>
        /// Configures the render order of this UIBlock within a <see cref="SortGroup"/>.
        /// </summary>
        /// <remarks><see cref="UIBlock"/>s with a higher ZIndex are rendered on top of <see cref="UIBlock"/>s with a lower ZIndex.</remarks>
        public short ZIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => visibility.ZIndex;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                visibility.ZIndex = value;
                RenderingDataStore.Instance.CopyBaseInfoToStore(this);
            }
        }

        /// <summary>
        /// Specifies whether the body <see cref="UIBlock.Color">Color</see>, <see cref="Gradient">Gradient</see>, and image (i.e. <see cref="Sprite">Sprite</see>, <see cref="Texture">Texture</see>, or <see cref="RenderTexture">RenderTexture</see>) should render.
        /// </summary>
        public bool BodyEnabled
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => RenderingDataStore.Instance.Access(this).FillEnabled;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                RenderingDataStore.Instance.Access(this).FillEnabled = value;
            }
        }

        /// <summary>
        /// Apply local anti-aliasing to edges around the body and <see cref="Border"/>.
        /// </summary>
        /// <remarks>
        /// Drastically improves visual quality in most user interfaces. In certain situations, however, such as rendering a transparent image or<br/>
        /// if an image has edge softening already baked into its texture, enabling this property may result in some undesired visual artifacts.
        /// </remarks>
        public bool SoftenEdges
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => RenderingDataStore.Instance.Access(this).SoftenEdges;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                RenderingDataStore.Instance.Access(this).SoftenEdges = value;
            }
        }

        /// <summary>
        /// The primary body content color.
        /// </summary>
        public override Color Color
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => RenderingDataStore.Instance.Access(this).Color;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                RenderingDataStore.Instance.Access(this).Color = value;
            }
        }

        /// <summary>
        /// The <see cref="Length"/> configuration used to calculate a corner radius, applies to all four corners of the body, <see cref="Border"/>, and <see cref="Shadow"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="CornerRadius">CornerRadius</see>.<see cref="Length.Percent">Percent</see> is relative to half 
        /// the minimum dimension (X or Y) of <see cref="UIBlock.CalculatedSize">CalculatedSize</see>. Mathematically speaking:<br/>
        /// <c>float calculatedCornerRadius = CornerRadius.Percent * 0.5f * Mathf.Min(CalculatedSize.X.Value, CalculatedSize.Y.Value)</c>
        /// </remarks>
        public ref Length CornerRadius
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref RenderingDataStore.Instance.Access(this).CornerRadius.ToPublic();
        }

        /// <summary>
        /// Configure how to store (and attempt to batch) the attached image's (i.e. <see cref="Sprite">Sprite</see>, <see cref="Texture">Texture</see>, or <see cref="RenderTexture">RenderTexture</see>) underlying texture.
        /// </summary>
        /// <seealso cref="NovaSettings.PackedImagesEnabled"/>
        public ImagePackMode ImagePackMode
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (ImagePackMode)RenderingDataStore.Instance.Access(this).Image.Mode;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                ref Internal.ImageData data = ref RenderingDataStore.Instance.Access(this).Image;
                data.Mode = (Internal.ImagePackMode)value;
                TrackImage(ref data);
                visuals.Image.Mode = value;
            }
        }

        #region Image Getters
        /// <summary>
        /// Retrieve the image texture previously assigned through <see cref="SetImage(Texture2D)"/> or in the Editor.
        /// </summary>
        /// <remarks>
        /// Will be null if the image is actually a <see cref="UnityEngine.RenderTexture"/> or <see cref="UnityEngine.Sprite"/>.
        /// </remarks>
        /// <seealso cref="RenderTexture"/>
        /// <seealso cref="Sprite"/>
        public Texture2D Texture
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => texture as Texture2D;
        }

        /// <summary>
        /// Retrieve the image texture previously assigned through <see cref="SetImage(RenderTexture)"/> or in the Editor.
        /// </summary>
        /// <remarks>
        /// Will be null if the image is actually a <see cref="Texture2D"/> or <see cref="UnityEngine.Sprite"/>.
        /// </remarks>
        /// <seealso cref="Texture"/>
        /// <seealso cref="Sprite"/>
        public RenderTexture RenderTexture
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => texture as RenderTexture;
        }

        /// <summary>
        /// Retrieve the sprite previously assigned through <see cref="SetImage(Sprite)"/> or in the Editor.
        /// </summary>
        /// <remarks>
        /// Will be null if the image is actually a <see cref="Texture2D"/> or <see cref="UnityEngine.RenderTexture"/>.
        /// </remarks>
        /// <seealso cref="Texture"/>
        /// <seealso cref="RenderTexture"/>
        public Sprite Sprite
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => sprite;
        }
        #endregion

        #region Image Setters
        /// <summary>
        /// Clear the current image assignment.
        /// </summary>
        /// <remarks>
        /// Clears anything previously assigned via <see cref="SetImage(Texture2D)"/>, <see cref="SetImage(Sprite)"/>, <see cref="SetImage(RenderTexture)"/> or in the Editor.
        /// </remarks>
        public void ClearImage()
        {
            sprite = null;
            texture = null;

            if (!Self.IsRegistered)
            {
                return;
            }

            TrackImage();
        }

        /// <summary>
        /// Render the provided <paramref name="texture"/> in the body of this <see cref="UIBlock2D"/>. Replaces any existing image assignment.
        /// </summary>
        /// <param name="texture">The texture to render in the body of this <see cref="UIBlock2D"/>.</param>
        public void SetImage(Texture2D texture) => SetTexture(texture);

        /// <summary>
        /// Render the provided <paramref name="renderTexture"/> in the body of this <see cref="UIBlock2D"/>. Replaces any existing image assignment.
        /// </summary>
        /// <param name="renderTexture">The texture to render in the body of this <see cref="UIBlock2D"/>.</param>
        public void SetImage(RenderTexture renderTexture) => SetTexture(renderTexture);

        /// <summary>
        /// Render the provided <paramref name="sprite"/> in the body of this <see cref="UIBlock2D"/>. Replaces any existing image assignment.
        /// </summary>
        /// <remarks>
        /// Sliced sprites and <see cref="SpritePackingMode.Tight"/> packed sprites are not supported.
        /// </remarks>
        /// <param name="sprite">The sprite to render in the body of this <see cref="UIBlock2D"/>.</param>
        public void SetImage(Sprite sprite)
        {
            if (sprite != null && sprite.packed && sprite.packingMode == SpritePackingMode.Tight)
            {
                Debug.LogError("Tight-packed sprite atlas not supported");
                return;
            }

            texture = null;
            this.sprite = sprite;

            if (!Self.IsRegistered)
            {
                return;
            }
            TrackImage();
        }
        #endregion
        #endregion

        #region Internal
        [SerializeField]
        private UIBlock2DData visuals = UIBlock2DData.Default;
        [SerializeField]
        private Texture texture = null;
        [SerializeField]
        private Sprite sprite = null;

        ref Internal.UIBlock2DData IRenderBlock<Internal.UIBlock2DData>.RenderData
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref visuals.Reinterpret<UIBlock2DData, Internal.UIBlock2DData>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetTexture(Texture tex)
        {
            sprite = null;
            texture = tex;

            if (!Self.IsRegistered)
            {
                return;
            }

            TrackImage();
        }

        private protected override void Register()
        {
            // Track using serialized struct (instead of Access) and do this *before* registration since
            // the data stored in the image data store is used by registration
            TrackImage(ref visuals.Image.Reinterpret<ImageData, Internal.ImageData>());
            base.Register();
        }

        private void TrackImage()
        {
            ref Internal.ImageData data = ref RenderingDataStore.Instance.Access(this).Image;
            TrackImage(ref data);
            visuals.Image.ImageID = data.ImageID;
        }

        private void TrackImage(ref Internal.ImageData data)
        {
            if (sprite != null)
            {
                RenderingDataStore.Instance.ImageTracker.Track(Sprite, data.Mode, ref data.ImageID);
            }
            else if (texture != null)
            {
                RenderingDataStore.Instance.ImageTracker.Track(texture, data.Mode, ref data.ImageID);
            }
            else if (data.ImageID.IsValid)
            {
                // Make sure to "track" null, as that will unregister
                RenderingDataStore.Instance.ImageTracker.Track(texture, data.Mode, ref data.ImageID);
            }
            visuals.Image.ImageID = data.ImageID;
        }

        private protected override void Unregister()
        {
            base.Unregister();

            // Untrack image *after* unregistering, since the data stored in the image data store
            // is used during unregistration
            if (visuals.Image.ImageID.IsValid && RenderingDataStore.Instance != null)
            {
                ref Internal.ImageData imageData = ref visuals.Image.Reinterpret<ImageData, Internal.ImageData>();
                RenderingDataStore.Instance.ImageTracker.Untrack(ref imageData.ImageID);
            }
        }

        internal override void EditorOnly_MarkDirty()
        {
            if (Self.IsRegistered)
            {
                TrackImage(ref visuals.Image.Reinterpret<ImageData, Internal.ImageData>());
            }

            base.EditorOnly_MarkDirty();
        }

        [Obfuscation]
        private protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            TrackImage();
        }

        internal UIBlock2DData.Calculated CalculatedVisuals => RenderingDataStore.Instance.Access(this).Reinterpret<Internal.UIBlock2DData, UIBlock2DData>().Calc(CalculatedSize.XY.Value);

        private UIBlock2D() : base()
        {
            visibility = BaseRenderInfo.Default(BlockType.UIBlock2D);
        }

        #endregion
    }
}
