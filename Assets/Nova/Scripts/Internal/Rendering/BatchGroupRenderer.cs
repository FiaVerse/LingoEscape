// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal enum ComputeBufferUpdateType
    {
        All,
        UIBlock2D,
        UIBlock3D,
        Text,
        DropShadow,
    }

    internal unsafe class BatchGroupRenderer : IDisposable
    {
        #region Pool
        private static List<DrawInstancedData> instancePool = new List<DrawInstancedData>(Constants.SomeElementsInitialCapacity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DrawInstancedData GetFreeInstance()
        {
            if (!instancePool.TryPopBack(out DrawInstancedData proceduralDrawCall))
            {
                proceduralDrawCall = new DrawInstancedData();
            }
            return proceduralDrawCall;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void FreeInstances(List<DrawInstancedData> proceduralDrawCalls)
        {
            for (int i = 0; i < proceduralDrawCalls.Count; ++i)
            {
                proceduralDrawCalls[i].Clear();
            }

            instancePool.AddRange(proceduralDrawCalls);
            proceduralDrawCalls.Clear();
        }
        #endregion

        private DataStoreID rootID = default;

        private List<DrawInstancedData> instances = new List<DrawInstancedData>(Constants.SomeElementsInitialCapacity);

        public ManagedVisualModifierShaderData OverrideVisualModifierData = new ManagedVisualModifierShaderData();

        private ShaderBuffer<ShaderIndex> shaderIndicesBuffer = null;
        private ShaderBuffer<SubQuadVert> subquadBuffer = null;
        private DrawCallSummary drawCallSummary;

        private NovaList<DrawCallDescriptorID, MaterialCacheIndex> materials;
        private Matrix4x4 worldFromLocal = default;
        private Matrix4x4 localFromWorld = default;

        #region Pass Through
        private RenderEngine Engine
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => RenderEngine.Instance;
        }

        private RenderingDataStore DataStore
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => RenderingDataStore.Instance;
        }

        private ref BatchGroupDataStore BatchGroupBuffers
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Engine.BatchGroupDataStore;
        }
        #endregion

        #region Lazy Loaded
        private IUIBlock _rootUINode = null;
        private IUIBlock RootUINode
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_rootUINode == null)
                {
                    if (!HierarchyDataStore.Instance.Elements.TryGetValue(rootID, out IHierarchyBlock rootNode))
                    {
                        Debug.LogError("Root node not found in Hierarchy Data Store.");
                        return null;
                    }

                    if (!(rootNode is IUIBlock uinode))
                    {
                        Debug.LogError("Root was not a UINode.");
                        return null;
                    }

                    _rootUINode = uinode;
                }
                return _rootUINode;
            }
        }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Render(Camera camera)
        {
            NovaList<DrawCallID, CameraSorting.ProcessedDrawCall> drawCallBounds = RenderEngine.Instance.GetDrawCallBounds(rootID);
            for (int i = 0; i < instances.Count; ++i)
            {
                DrawInstancedData drawCall = instances[i];
                drawCall.RenderInstanced(camera, ref drawCallBounds.ElementAt(drawCall.DrawCallID));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearDrawCalls()
        {
            FreeInstances(instances);
        }

        public void EditorOnly_HandleTexturesReprocessed()
        {
            for (int i = 0; i < instances.Count; ++i)
            {
                DrawInstancedData instance = instances[i];

                if (instance.Descriptor.HasVisualModifier)
                {
                    Texture texture = DataStore.VisualModifierTracker.ClipMaskTextures[instance.Descriptor.VisualModifierID];
                    if (texture != null)
                    {
                        instance.Mpb.SetTexture(ShaderPropertyIDs.ClipMaskTexture, texture);
                    }
                }

                if ((instance.Descriptor.MaterialModifiers & MaterialModifier.DynamicImage) != 0)
                {
                    if (RenderingDataStore.Instance.ImageTracker.TryGet(instance.Descriptor.UIBlock2D.TextureID, out Texture texture))
                    {
                        instance.Mpb.SetTexture(ShaderPropertyIDs.DynamicTexture, texture);

                    }
                    else
                    {
                        Debug.LogError("Texture tracker did not have matching texture");
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateRootMatrices()
        {
            DataStoreIndex rootDataStoreIndex = HierarchyDataStore.Instance.HierarchyLookup[rootID];
            worldFromLocal = LayoutDataStore.Instance.LocalToWorldMatrices.ElementAt(rootDataStoreIndex);
            localFromWorld = LayoutDataStore.Instance.WorldToLocalMatrices.ElementAt(rootDataStoreIndex);

            for (int i = 0; i < instances.Count; ++i)
            {
                DrawInstancedData instance = instances[i];
                instance.Mpb.SetMatrix(ShaderPropertyIDs.WorldFromLocalTransform, worldFromLocal);
                instance.Mpb.SetMatrix(ShaderPropertyIDs.LocalFromWorldTransform, localFromWorld);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateComputeBuffer(ShaderBuffer computeBuffer, int shaderPropertyID, ComputeBufferUpdateType updateType)
        {
            for (int i = 0; i < instances.Count; ++i)
            {
                DrawInstancedData instance = instances[i];
                bool setBuffer = false;
                switch (updateType)
                {
                    case ComputeBufferUpdateType.All:
                        setBuffer = true;
                        break;
                    case ComputeBufferUpdateType.UIBlock2D:
                        setBuffer = instance.VisualType == VisualType.UIBlock2D;
                        break;
                    case ComputeBufferUpdateType.Text:
                        setBuffer = (instance.VisualType & VisualType.TEXT_MASK) != 0;
                        break;
                    case ComputeBufferUpdateType.UIBlock3D:
                        setBuffer = instance.VisualType == VisualType.UIBlock3D;
                        break;
                    case ComputeBufferUpdateType.DropShadow:
                        setBuffer = instance.VisualType == VisualType.DropShadow;
                        break;
                }

                if (setBuffer)
                {
                    instance.Mpb.SetBuffer(shaderPropertyID, computeBuffer);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateDrawCalls()
        {
            if (RootUINode == null)
            {
                Debug.LogError($"RootNode was null for {rootID}");
                return;
            }

            if (!RootUINode.ActiveInHierarchy)
            {
                return;
            }

            drawCallSummary = BatchGroupBuffers.DrawCallSummaries[rootID];
            if (drawCallSummary.DrawCallCount == 0)
            {
                return;
            }

            drawCallSummary.SetIndexBuffer(ref shaderIndicesBuffer);

            DataStoreIndex rootDataStoreIndex = HierarchyDataStore.Instance.HierarchyLookup[rootID];
            worldFromLocal = LayoutDataStore.Instance.LocalToWorldMatrices.ElementAt(rootDataStoreIndex);
            localFromWorld = LayoutDataStore.Instance.WorldToLocalMatrices.ElementAt(rootDataStoreIndex);

            NovaList<SubQuadVert> subQuadBuffers = BatchGroupBuffers.SubQuadBuffers[rootID];
            ShaderBufferUtils.SetBufferRef(ref subquadBuffer, ref subQuadBuffers);

            materials = BatchGroupBuffers.MaterialAssignments[rootID];

            for (int i = 0; i < drawCallSummary.DrawCalls.Length; ++i)
            {
                try
                {
                    ProcessDrawCall(i);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Processing draw call failed with: {ex}");
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessDrawCall(int drawCallIndex)
        {
            ref DrawCall drawCall = ref drawCallSummary.DrawCalls.ElementAt(drawCallIndex);
            ref DrawCallDescriptor descriptor = ref drawCallSummary.DrawCallDescriptors.ElementAt(drawCall.DescriptorID);

            DrawInstancedData instance = GetFreeInstance();
            ShaderIndexBounds indexBounds = drawCallSummary.GetIndexBounds(drawCall.ID);

            instance.DrawCallID = drawCall.ID;
            instance.Descriptor = descriptor;
            instance.InstanceCount = descriptor.DrawCallType == VisualType.UIBlock2D ? indexBounds.InstanceCount / 4 : indexBounds.InstanceCount;
            instance.Mpb.SetInt(ShaderPropertyIDs.FirstIndex, indexBounds.InstanceStart);
            instance.Mpb.SetInt(ShaderPropertyIDs.LastIndex, indexBounds.InstanceStart + indexBounds.InstanceCount - 1);
            instance.Material = MaterialCache.Get(materials[drawCall.DescriptorID]);

            if (descriptor.DrawCallType != VisualType.UIBlock2D)
            {
                instance.Mpb.SetBuffer(ShaderPropertyIDs.DataIndices, shaderIndicesBuffer);
            }

            instance.Mpb.SetMatrix(ShaderPropertyIDs.WorldFromLocalTransform, worldFromLocal);
            instance.Mpb.SetMatrix(ShaderPropertyIDs.LocalFromWorldTransform, localFromWorld);
            instance.Mpb.SetBuffer(ShaderPropertyIDs.TransformsAndLighting, DataStore.Common.TransformAndLightingData);
            instance.Mpb.SetFloat(ShaderPropertyIDs.EdgeSoftenWidth, NovaSettings.EdgeSoftenWidth);

            SetClipMaskData(instance, ref descriptor);

            switch (descriptor.DrawCallType)
            {
                case VisualType.UIBlock2D:
                    DoUIBlock2DDrawCall(ref instance, ref descriptor);
                    break;
                case VisualType.DropShadow:
                    DoDropShadowDrawCall(ref instance);
                    break;
                case VisualType.TextBlock:
                case VisualType.TextSubmesh:
                    DoTextDrawCall(ref instance);
                    break;
                case VisualType.UIBlock3D:
                    DoUIBlock3DDrawCall(ref instance);
                    break;
            }

            instances.Add(instance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetClipMaskData(DrawInstancedData instance, ref DrawCallDescriptor descriptor)
        {
            ManagedVisualModifierShaderData visualModifierShaderData = null;
            if (descriptor.HasVisualModifier)
            {
                // Has a visual modifier, so use that
                visualModifierShaderData = DataStore.VisualModifierTracker.ManagedShaderData[descriptor.VisualModifierID];
            }
            else
            {
                if (OverrideVisualModifierData.Count == 0)
                {
                    // No override
                    return;
                }

                // Use the override
                visualModifierShaderData = OverrideVisualModifierData;
            }

            instance.Mpb.SetInt(ShaderPropertyIDs.VisualModifierCount, visualModifierShaderData.Count);


            instance.Mpb.SetMatrixArray(ShaderPropertyIDs.VisualModifersFromRoot, visualModifierShaderData.VisualModifiersFromRoot);
            instance.Mpb.SetVectorArray(ShaderPropertyIDs.ClipRectInfos, visualModifierShaderData.ClipRectInfos);
            instance.Mpb.SetVectorArray(ShaderPropertyIDs.GlobalColorModifiers, visualModifierShaderData.VisualModifierColors);

            // Set the clip mask texture if there is one
            if (visualModifierShaderData.ClipMaskIndex != -1)
            {
                Texture texture = DataStore.VisualModifierTracker.ClipMaskTextures[visualModifierShaderData.ModifierIDs[visualModifierShaderData.ClipMaskIndex]];
                if (texture == null)
                {
                    return;
                }

                instance.Mpb.SetInt(ShaderPropertyIDs.ClipMaskIndex, visualModifierShaderData.ClipMaskIndex);
                instance.Mpb.SetTexture(ShaderPropertyIDs.ClipMaskTexture, texture);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoUIBlock3DDrawCall(ref DrawInstancedData instance)
        {
            instance.Mpb.SetBuffer(ShaderPropertyIDs.ShaderData, DataStore.UIBlock3DData.ShaderData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoTextDrawCall(ref DrawInstancedData instance)
        {
            instance.Mpb.SetBuffer(ShaderPropertyIDs.ShaderData, DataStore.TextBlockData.PerCharShaderData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoDropShadowDrawCall(ref DrawInstancedData instance)
        {
            instance.Mpb.SetBuffer(ShaderPropertyIDs.ShaderData, DataStore.UIBlock2DData.ShadowQuadShaderData);
            instance.Mpb.SetBuffer(ShaderPropertyIDs.PerBlockData, DataStore.UIBlock2DData.Shadow);
            instance.Mpb.SetFloat(ShaderPropertyIDs.EdgeSoftenWidth, NovaSettings.EdgeSoftenWidth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoUIBlock2DDrawCall(ref DrawInstancedData instance, ref DrawCallDescriptor descriptor)
        {
            if (subquadBuffer != null)
            {
                // If there is a draw call with a single UIBlock2D of size zero, it will get plumbed through
                // all of the way. Not ideal, but not the end of the world. However, in that case subquadBuffer will
                // be null
                instance.Mpb.SetBuffer(ShaderPropertyIDs.SubQuadVerts, subquadBuffer);
            }
            instance.Mpb.SetBuffer(ShaderPropertyIDs.ShaderData, DataStore.UIBlock2DData.ShaderData);
            instance.Mpb.SetFloat(ShaderPropertyIDs.EdgeSoftenWidth, NovaSettings.EdgeSoftenWidth);

            if ((descriptor.MaterialModifiers & MaterialModifier.StaticImage) != 0)
            {
                RenderingDataStore.Instance.ImageTracker.Get(descriptor.UIBlock2D.TexturePackID, instance);
            }
            else if ((descriptor.MaterialModifiers & MaterialModifier.DynamicImage) != 0)
            {
                if (RenderingDataStore.Instance.ImageTracker.TryGet(descriptor.UIBlock2D.TextureID, out Texture texture))
                {
                    instance.Mpb.SetTexture(ShaderPropertyIDs.DynamicTexture, texture);
                }
                else
                {
                    Debug.LogError("Texture tracker did not have matching texture");
                }
            }
        }

        #region Init and Cleanup
        public BatchGroupRenderer(DataStoreID rootID)
        {
            this.rootID = rootID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            FreeInstances(instances);
            shaderIndicesBuffer.SafeDispose();
            subquadBuffer.SafeDispose();
        }
        #endregion
    }
}

