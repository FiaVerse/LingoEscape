// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Utilities;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    /// <summary>
    /// We have two modes of rendering, regular and fallback (fallback being
    /// when compute buffers are not supported in vertex shaders like on webgl and 
    /// mali gpus). In the fallback mode, we use textures as the data buffers.
    /// This type abstracts that away
    /// </summary>
    internal unsafe class ShaderBuffer<T> : ShaderBuffer where T : unmanaged
    {
        public int Count { get; private set; } = -1;
        private bool usingRGBATexture = false;

        public ShaderBuffer(int count)
        {
            Count = count;

            if (SystemSettings.UseFallbackRendering)
            {
                CreateTexture();
            }
            else
            {
                computeBuffer = new ComputeBuffer(count, sizeof(T), ComputeBufferType.Structured);
            }
        }

        private void CreateTexture()
        {
            int dataSize = sizeof(T);
            int float4Size = sizeof(float4);
            if (dataSize >= float4Size)
            {
                usingRGBATexture = true;

                int sizeInBytes = Count * dataSize;
                int numPixels = sizeInBytes / float4Size;

                int textureWidth = math.min(numPixels, Constants.MaxTextureDimension);
                int textureHeight = numPixels / Constants.MaxTextureDimension + 1;

                texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBAFloat, false, true);

                if (!UnityVersionUtils.Is2022OrNewer && !backingBuffer.IsCreated)
                {
                    // Workaround for a unity bug with GetPixelData, forcing us to use SetPixelData
                    int backingBufferSize = 4 * textureWidth * textureHeight;
                    backingBuffer = new NativeArray<float>(backingBufferSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                }
            }
            else
            {
                usingRGBATexture = false;


                int textureWidth = math.min(Count, Constants.MaxTextureDimension);
                int textureHeight = Count / Constants.MaxTextureDimension + 1;

                texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RFloat, false, true);
                if (!UnityVersionUtils.Is2022OrNewer && !backingBuffer.IsCreated)
                {
                    // Workaround for a unity bug with GetPixelData, forcing us to use SetPixelData
                    backingBuffer = new NativeArray<float>(textureWidth * textureHeight, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                }
            }

            // Don't interpolate
            texture.filterMode = FilterMode.Point;
        }

        /// <summary>
        /// For some reason, unity destroys textures when switching scenes?
        /// </summary>
        private void EnsureTexture()
        {
            if (texture == null)
            {
                CreateTexture();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="apply">If using fallback, setting this to false avoids calling apply on the texture</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref NativeArray<T> data, int dataStartIndex = 0, int shaderBufferStartIndex = 0, bool apply = true)
        {
            if (SystemSettings.UseFallbackRendering)
            {
                // When switching scenes in editor, the texture gets destroyed for some reason. So this
                // just makes sure it's created.
                EnsureTexture();

                if (usingRGBATexture)
                {
                    NativeArray<float4> dataAsF4 = data.Reinterpret<float4>(sizeof(T));

                    if (backingBuffer.IsCreated)
                    {
                        // Copy to backing buffer
                        NativeArray<float4> backingBufferAsF4 = backingBuffer.Reinterpret<float4>(sizeof(float));
                        NativeArray<float4>.Copy(dataAsF4, backingBufferAsF4, dataAsF4.Length);
                    }
                    else
                    {
                        // Copy directly to texture
                        NativeArray<float4> textureData = texture.GetPixelData<float4>(0);
                        NativeArray<float4>.Copy(dataAsF4, textureData, dataAsF4.Length);
                    }

                    if (apply)
                    {
                        ApplyChanges();
                    }

                }
                else
                {
                    NativeArray<float> dataAsF = data.Reinterpret<float>(sizeof(T));

                    if (backingBuffer.IsCreated)
                    {
                        // Copy to backing buffer
                        NativeArray<float>.Copy(dataAsF, dataStartIndex, backingBuffer, shaderBufferStartIndex, dataAsF.Length);
                    }
                    else
                    {
                        // Copy directly to texture
                        NativeArray<float> textureData = texture.GetPixelData<float>(0);
                        NativeArray<float>.Copy(dataAsF, dataStartIndex, textureData, shaderBufferStartIndex, dataAsF.Length);
                    }

                    if (apply)
                    {
                        ApplyChanges();
                    }
                }
            }
            else
            {
                computeBuffer.SetData(data, dataStartIndex, shaderBufferStartIndex, data.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyChanges()
        {
            if (texture != null)
            {
                if (backingBuffer.IsCreated)
                {
                    // In some version of unity, GetPixelData has a bug so we need to use a backing buffer
                    // and then copy using SetPixelData
                    if (usingRGBATexture)
                    {
                        NativeArray<float4> backingBufferAsF4 = backingBuffer.Reinterpret<float4>(sizeof(float));
                        texture.SetPixelData(backingBufferAsF4, 0);
                    }
                    else
                    {
                        texture.SetPixelData(backingBuffer, 0);
                    }
                }

                texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            }
        }
    }

    internal abstract class ShaderBuffer : IDisposable
    {
        protected ComputeBuffer computeBuffer = null;
        protected Texture2D texture = null;
        protected NativeArray<float> backingBuffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBuffer(MaterialPropertyBlock mpb, int shaderPropertyID)
        {
            if (SystemSettings.UseFallbackRendering)
            {
                mpb.SetTexture(shaderPropertyID, texture);
            }
            else
            {
                mpb.SetBuffer(shaderPropertyID, computeBuffer);
            }
        }

        public virtual void Dispose()
        {
            if (computeBuffer != null)
            {
                computeBuffer.Dispose();
                computeBuffer = null;
            }

            DestroyUtils.SafeDestroy(texture);
            texture = null;

            if (backingBuffer.IsCreated)
            {
                backingBuffer.Dispose();
            }
        }
    }

    internal static class ShaderBufferUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBuffer(this MaterialPropertyBlock mpb, int shaderPropertyID, ShaderBuffer shaderBuffer)
        {
            shaderBuffer.SetBuffer(mpb, shaderPropertyID);
        }

        public static void SafeDispose(this ShaderBuffer shaderBuffer)
        {
            if (shaderBuffer != null)
            {
                shaderBuffer.Dispose();
            }
        }

        /// <summary>
        /// Returns true if destroyed the old buffer and created a new one
        /// </summary>
        public static bool EnsureSizeAndCreated<T>(ref ShaderBuffer<T> shaderBuffer, int count) where T : unmanaged
        {
            if (count == 0 || (shaderBuffer != null && shaderBuffer.Count >= count))
            {
                return false;
            }

            bool toRet = false;
            if (shaderBuffer != null)
            {
                toRet = true;
                shaderBuffer.Dispose();
            }

            shaderBuffer = new ShaderBuffer<T>(count);
            return toRet;
        }

        public static bool SetBufferRef<T>(ref ShaderBuffer<T> shaderBuffer, ref NovaList<T> data) where T : unmanaged
        {
            bool toRet = EnsureSizeAndCreated<T>(ref shaderBuffer, data.Length);

            if (data.Length == 0 || shaderBuffer == null)
            {
                return toRet;
            }

            using (var dataAsArray = data.AsArray())
            {
                NativeArray<T> arr = dataAsArray.Array;
                shaderBuffer.SetData(ref arr);
            }
            return toRet;
        }
    }
}

