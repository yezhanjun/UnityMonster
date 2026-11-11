#ifndef RAIN_PARENT_OPAQUE_INSTRUCTION_MATH
#define RAIN_PARENT_OPAQUE_INSTRUCTION_MATH

// Original Opaque Cloud Remap BASE surface and Internal-DeferredShading math.
// Caller owns texture sampling, transforms, clip, render targets and light attenuation.
// This include alone is not a complete production shader.
struct RainParentOpaquePayload
{
    float4 albedo;
    float4 specularRamp;
    float4 normal;
    float4 emission;
};

float4 RainParentOpaqueVertexColor(float4 vertexColor, float influence)
{
    return 1 + influence * (vertexColor - 1);
}

float RainParentOpaqueMask(float mainAlpha, float cloudOneAlpha, float cloudTwoAlpha,
    float4 influencedVertexColor, float tintAlpha, float alphaBoost, float externalAlpha)
{
    // The repeated influenced vertex alpha is present in both original surface and shadow programs.
    return mainAlpha * influencedVertexColor.a * cloudOneAlpha * cloudTwoAlpha * alphaBoost
        * influencedVertexColor.a * tintAlpha * 4 * externalAlpha;
}

float3 RainParentOpaqueNormalTS(float4 packed, float strength)
{
    float2 xy = float2(packed.r * packed.a, packed.g) * 2 - 1;
    float z = sqrt(1 - min(dot(xy, xy), 1));
    // Original interpolates the complete unpacked normal from (0,0,1), including z.
    return float3(xy * strength, 1 + (z - 1) * strength);
}

RainParentOpaquePayload RainParentOpaquePack(float3 remapColor, float4 tint, float4 influencedVertexColor,
    float3 normalWS, float specularStrength, float specularExponent, int rampInfo, float3 emission, bool hdr)
{
    RainParentOpaquePayload output;
    output.albedo = float4(remapColor * tint.rgb * influencedVertexColor.rgb, 1);
    output.specularRamp = float4(specularStrength, specularExponent * .05, (float)rampInfo * .0625, 0);
    output.normal = float4(normalWS * .5 + .5, 0);
    output.emission = float4(hdr ? emission : exp2(-emission), 1);
    return output;
}

// x: specular ramp coordinate; y: diffuse ramp coordinate; z: palette row.
float3 RainParentOpaqueLightCoordinates(float3 normalWS, float3 viewDirectionWS,
    float3 directionToLight, float encodedExponent, float encodedRamp, bool localLight)
{
    float noL = dot(directionToLight, normalWS);
    float specular = pow(saturate(dot(reflect(-directionToLight, normalWS), viewDirectionWS)), encodedExponent * 20);
    // Original point/spot programs use a different diffuse coordinate from directional lights.
    return float3(specular, localLight ? noL * .5 + .5 : noL, 1 - encodedRamp);
}

float3 RainParentOpaqueDirectColor(float3 albedo, float3 lightColor, float specularStrength,
    float3 diffuseRamp, float specularRampRed)
{
    return albedo * lightColor * (2 * diffuseRamp) + lightColor * (2 * specularRampRed * specularStrength);
}

float RainParentOpaquePointAttenuationCoordinate(float squaredDistance, float inverseSquaredRange)
{
    float distanceTerm = 1 - squaredDistance * inverseSquaredRange;
    // Original samples _LightTextureB0 at this coordinate; caller must supply that actual LUT.
    return 1 - distanceTerm * distanceTerm * distanceTerm;
}
#endif
