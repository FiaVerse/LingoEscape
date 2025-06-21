// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Rendering;
using Nova.Internal.Utilities;
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nova.Editor.Builds
{
    internal class NovaShaderPreprocessor : IPreprocessShaders
    {
        private const string NovaShaderPrefix = "Hidden/Nova/Nova";

        public int callbackOrder => 0;
        private List<ShaderKeyword> shaderKeywordsToRemove = new List<ShaderKeyword>();
        private static List<string> unityKeywordsToRemove = new List<string>()
        {
            "FOG_LINEAR",
            "FOG_EXP",
            "FOG_EXP2",
            "DYNAMICLIGHTMAP_ON",
            "LIGHTMAP_ON",
            "LIGHTMAP_SHADOW_MIXING",
            "DIRLIGHTMAP_COMBINED",
        };

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (!shader.name.Contains(NovaShaderPrefix))
            {
                // Not a nova shader
                return;
            }

            if (!IncludedInBuild(shader.name))
            {
                data.Clear();
                return;
            }

            UpdateShaderKeywords(shader);
            for (int i = data.Count - 1; i >= 0; --i)
            {
                for (int j = 0; j < shaderKeywordsToRemove.Count; ++j)
                {
                    if (data[i].shaderKeywordSet.IsEnabled(shaderKeywordsToRemove[j]))
                    {
                        data.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private void UpdateShaderKeywords(Shader shader)
        {
            shaderKeywordsToRemove.Clear();
            for (int i = 0; i < unityKeywordsToRemove.Count; ++i)
            {
                shaderKeywordsToRemove.Add(new ShaderKeyword(shader, unityKeywordsToRemove[i]));
            }
        }

        private bool IncludedInBuild(string shaderName)
        {
            if (!TryGetBuildFlag(shaderName, out Internal.LightingModel lightingModel) ||
                !TryGetVisualType(shaderName, out VisualType visualType) ||
                // Need to do this check here to ensure that the settings initialization
                // happens
                !Internal.NovaSettings.Initialized)
            {
                return false;
            }

            return ShaderUtils.IsIncluded(visualType, lightingModel);
        }

        private bool TryGetVisualType(string shaderName, out VisualType visualType)
        {
            if (shaderName.Contains("UIBlock2D"))
            {
                visualType = VisualType.UIBlock2D;
                return true;
            }
            else if (shaderName.Contains("DropShadow"))
            {
                visualType = VisualType.DropShadow;
                return true;
            }
            else if (shaderName.Contains("UIBlock3D"))
            {
                visualType = VisualType.UIBlock3D;
                return true;
            }
            else if (shaderName.Contains("TextBlock"))
            {
                visualType = VisualType.TextBlock;
                return true;
            }
            else
            {
                Debug.LogWarning($"Failed to get visual type from shader name: {shaderName}");
                visualType = VisualType.Invalid;
                return false;
            }
        }

        private bool TryGetBuildFlag(string shaderName, out Internal.LightingModel lightingModel)
        {
            if (shaderName.Contains("Unlit"))
            {
                lightingModel = Internal.LightingModel.Unlit;
                return true;
            }
            if (shaderName.Contains("Lambert"))
            {
                lightingModel = Internal.LightingModel.Lambert;
                return true;
            }
            else if (shaderName.Contains("BlinnPhong"))
            {
                lightingModel = Internal.LightingModel.BlinnPhong;
                return true;
            }
            else if (shaderName.Contains("StandardSpecular"))
            {
                lightingModel = Internal.LightingModel.StandardSpecular;
                return true;
            }
            else if (shaderName.Contains("Standard"))
            {
                lightingModel = Internal.LightingModel.Standard;
                return true;
            }
            else
            {
                Debug.LogWarning($"Failed to get lighting model from shader name: {shaderName}");
                lightingModel = default;
                return false;
            }
        }
    }

}
