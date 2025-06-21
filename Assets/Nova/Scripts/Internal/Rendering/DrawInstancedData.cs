// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal class DrawInstancedData : ITexturePackSubscriber
    {
        public Material Material = null;
        public MaterialPropertyBlock Mpb = new MaterialPropertyBlock();
        public DrawCallDescriptor Descriptor;
        public DrawCallID DrawCallID;
        public int InstanceCount;

        public VisualType VisualType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Descriptor.DrawCallType;
        }

        private Mesh Mesh
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                switch (VisualType)
                {
                    case VisualType.UIBlock3D:
                        return RenderEngine.Instance.MeshProvider.RoundedCubeMesh;
                    case VisualType.DropShadow:
                    case VisualType.UIBlock2D:
                    case VisualType.TextBlock:
                    case VisualType.TextSubmesh:
                    default:
                        return RenderEngine.Instance.MeshProvider.SingleSidedQuadMesh;
                }
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Mpb.Clear();

            if ((Descriptor.MaterialModifiers & MaterialModifier.StaticImage) != 0 &&
                RenderingDataStore.Instance != null &&
                Descriptor.UIBlock2D.TexturePackID.IsValid)
            {
                RenderingDataStore.Instance.ImageTracker.Unsubscribe(Descriptor.UIBlock2D.TexturePackID, this);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RenderInstanced(Camera camera, ref CameraSorting.ProcessedDrawCall bounds)
        {
            if (bounds.SkipRendering || InstanceCount == 0)
            {
                return;
            }

            Mpb.SetInt(ShaderPropertyIDs.ViewingFromBehind, bounds.ViewingFromBehind ? 1 : 0);

            Graphics.DrawMeshInstancedProcedural(
                Mesh,
                0,
                Material,
                bounds.AdjustedBounds,
                InstanceCount,
                Mpb,
                Descriptor.Surface.ShadowCastingMode,
                receiveShadows: Descriptor.Surface.ReceiveShadows,
                layer: Descriptor.GameObjectLayer,
                camera: camera
                );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void HandleTextureArrayRecreated(Texture2DArray textureArray)
        {
            Mpb.SetTexture(ShaderPropertyIDs.StaticTexture, textureArray);
        }
    }
}

