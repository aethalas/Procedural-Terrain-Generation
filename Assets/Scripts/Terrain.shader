Shader "Custom/Terrain"
{
 Properties
 {
  testTexture("Texture", 2D) = "white"{}
  testScale("Scale", Float) = 1
 }

 SubShader
 {
  Tags { "RenderType"="Opaque" }
  LOD 200

  CGPROGRAM
  // Physically based Standard lighting model, and enable shadows on all light types
  #pragma surface surf Standard fullforwardshadows

  // Use shader model 3.0 target, to get nicer looking lighting
  #pragma target 3.0

  const static int maxLayerCount = 8;
  const static float epsilon = 1E-4;

  int layerCount;
  float3 baseColours[maxLayerCount];
  float baseStartHeights[maxLayerCount];
  float baseBlends[maxLayerCount];
  float baseColourStrength[maxLayerCount];
  float baseTextureScales[maxLayerCount];
  float minHeight;
  float maxHeight;
  sampler2D testTexture;
  float testScale;

  UNITY_DECLARE_TEX2DARRAY(baseTextures);

  struct Input
  {
   float3 worldPos;
   float3 worldNormal;
  };

  float InverseLerp(float a, float b, float value)
  {
   return saturate((value-a)/(b-a));
  }

  float3 triplanar(float3 worldPos, float scale, float3 blendAxis, int textureIndex)
  {
   float3 scaledWorldPos = worldPos / scale;
   float3 xProjection = UNITY_SAMPLE_TEX2DARRAY(baseTextures, float3(scaledWorldPos.y, scaledWorldPos.z, textureIndex)) * blendAxis.x;
   float3 yProjection = UNITY_SAMPLE_TEX2DARRAY(baseTextures, float3(scaledWorldPos.x, scaledWorldPos.z, textureIndex)) * blendAxis.y;
   float3 zProjection = UNITY_SAMPLE_TEX2DARRAY(baseTextures, float3(scaledWorldPos.x, scaledWorldPos.y, textureIndex)) * blendAxis.z;
   return xProjection + yProjection + zProjection;
  }

  void surf (Input IN, inout SurfaceOutputStandard o)
  {
   float heightPercent = InverseLerp(minHeight, maxHeight, IN.worldPos.y);
   float3 blendAxis = abs(IN.worldNormal);
   blendAxis /= blendAxis.x + blendAxis.y + blendAxis.z;

   for (int i = 0; i < layerCount; i++)
   {
   	float drawStrength = InverseLerp(-baseBlends[i] / 2, baseBlends[i] / 2 - epsilon, heightPercent - baseStartHeights[i]);
	float3 baseColour = baseColours[i] * baseColourStrength[i];
	float3 textureColour = triplanar(IN.worldPos, baseTextureScales[i], blendAxis, i) * (1-baseColourStrength[i]);
	o.Albedo = o.Albedo * (1-drawStrength) + (baseColour + textureColour) * drawStrength;
   }
  }
  ENDCG
 }
 FallBack "Diffuse"
}
