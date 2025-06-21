// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Rendering;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// A <see cref="UIBlock"/> with an adjustable, rounded-corner, rounded-edge cube mesh.
    /// </summary>
    [AddComponentMenu("Nova/UIBlock 3D")]
    [HelpURL("https://novaui.io/manual/UIBlock3D.html")]
    public sealed class UIBlock3D : UIBlock, IUIBlock3D
    {
        #region Public
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
        /// The <see cref="Length"/> configuration used to calculate a corner radius, applies to all eight corners of the body's front and back faces (XY planes).
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
        /// The <see cref="Length"/> configuration used to calculate an edge radius, applies to all eight edges of the body's front and back faces (XY planes).
        /// </summary>
        /// <remarks>
        /// <see cref="EdgeRadius">EdgeRadius</see>.<see cref="Length.Percent">Percent</see> is relative to half 
        /// the minimum dimension (X, Y, or Z) of <see cref="UIBlock.CalculatedSize">CalculatedSize</see>. Mathematically speaking:<br/>
        /// <c>float unclampedEdgeRadius = EdgeRadius.Percent * 0.5f * Mathf.Min(CalculatedSize.X.Value, CalculatedSize.Y.Value, CalculatedSize.Z.Value)</c>.<br/><br/>
        /// When rendering, EdgeRadius will not exceed the calculated value of <see cref="CornerRadius"/>. Mathematically speaking:<br/>
        /// <c> float calculatedEdgeRadius = Mathf.Min(unclampedEdgeRadius, calculatedCornerRadius)</c>.
        /// </remarks>
        public ref Length EdgeRadius
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref RenderingDataStore.Instance.Access(this).EdgeRadius.ToPublic();
        }
        #endregion

        #region Internal
        [SerializeField]
        private UIBlock3DData visuals = UIBlock3DData.Default;

        ref Internal.UIBlock3DData IRenderBlock<Internal.UIBlock3DData>.RenderData
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref UnsafeUtility.As<UIBlock3DData, Internal.UIBlock3DData>(ref visuals);
        }


        internal UIBlock3DData.Calculated CalculatedVisuals => RenderingDataStore.Instance.Access(this).Reinterpret<Internal.UIBlock3DData, UIBlock3DData>().Calc(CalculatedSize.Value);

        /// <summary>
        /// Initialize values for 3D block
        /// </summary>
        private UIBlock3D() : base()
        {
            Layout = Layout.ThreeD;
            visibility = BaseRenderInfo.Default(BlockType.UIBlock3D);
            surface = Surface.DefaultLit;
        }
        #endregion
    }
}

