Shader "Example/stencil"
{
	properties
	{
		[IntRange] _StencilID ("Stencil ID", Range (0,255)) = 0
		
	}
	SubShader
	{
		Tags
		{
			"RendererType" = "Opaque"
			"Queue"= "Geometry"
			"renderPipeline" = "Universalpipeline"
			}

		Pass
		{
			Blend zero One
			ZWrite Off
			

			Stencil
			{
				Ref[_StencilID]
				Comp Always
				Pass Replace
				Fail Keep
}
		}
		}
		}