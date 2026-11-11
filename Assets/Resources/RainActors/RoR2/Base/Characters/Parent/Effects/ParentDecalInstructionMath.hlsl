#ifndef RAIN_PARENT_DECAL_INSTRUCTION_MATH
#define RAIN_PARENT_DECAL_INSTRUCTION_MATH

// Decalicious/Deferred Decal, original Parent death material CLOUD variants.
// These are the original two-pass payloads, not URP's G-buffer layout.
// Caller owns depth/G-buffer copies, material samplers, render state and ordering.
float3 RainParentDecalReconstruct(float3 interpolatedRay, float farPlane,
    float rawDepth, float2 zBufferParams, float4x4 cameraToWorld, float4x4 worldToDecal)
{
    float linear01 = rcp(zBufferParams.x * rawDepth + zBufferParams.y);
    float3 viewPosition = interpolatedRay * (farPlane / interpolatedRay.z) * linear01;
    return mul(worldToDecal, mul(cameraToWorld, float4(viewPosition, 1))).xyz;
}

bool RainParentDecalCommonAccept(float3 localPosition, float objectMask, float limitTo,
    float maskRed, float maskMultiplier, float encodedLayer, float decalLayer)
{
    // Boundaries are inclusive. Layer is exact equality, unlike the object-mask tolerance.
    // The original renderer writes NaN for unrestricted LimitTo. An ordinary
    // negated comparison can be folded to <= by the new shader compiler.
    // Preserve the original unordered acceptance explicitly using IEEE bits.
    float objectDifference = limitTo - objectMask;
    bool unorderedObject = (asuint(objectDifference) & 0x7fffffffu) > 0x7f800000u;
    return (unorderedObject || abs(objectDifference) <= .1) && !any(.5 - abs(localPosition) < 0)
        && !(maskRed * maskMultiplier - .0005 < 0) && encodedLayer == decalLayer * (1.0 / 3.0);
}

float2 RainParentDecalCloudUV(float2 decalUV, float4 st, float2 scroll, float timeX)
{
    // The compiled program uses _Time.x (seconds / 20), not _Time.y.
    return decalUV * st.xy + st.zw + timeX * scroll;
}

bool RainParentDecalNormalAccept(float3 receiverNormal, float3 decalUp, float angleLimit)
{
    return dot(receiverNormal, decalUp) - angleLimit >= 0;
}

float RainParentDecalRemapCoordinate(float3 receiverNormal, float3 decalUp, float angleLimit,
    float maskRed, float maskMultiplier, float cloudOneAlpha, float cloudTwoAlpha, float alphaBoost)
{
    // Surface pass does not discard a negative angle: the ramp sampler handles this coordinate.
    return (dot(receiverNormal, decalUp) - angleLimit) * maskRed * maskMultiplier
        * cloudOneAlpha * cloudTwoAlpha * 4 * alphaBoost;
}

void RainParentDecalColorPayload(float4 ramp, float4 tint, float3 ambient, bool hdr,
    out float4 albedo, out float4 emission)
{
    float4 color = ramp * tint;
    albedo = float4(color.rgb * color.a, color.a);
    emission = float4((hdr ? color.rgb * ambient : exp2(-color.rgb * ambient)) * color.a, color.a);
}

float4 RainParentDecalSpecularPayload(float4 receiverSpecular, float maskWeight,
    float specularStrength, float specularExponent, int rampInfo, float smoothness)
{
    // Exponent is stored directly here, unlike Opaque Cloud Remap's /20 packing.
    return lerp(receiverSpecular, float4(specularStrength, specularExponent, rampInfo * .0625, smoothness), maskWeight);
}

float3 RainParentDecalNormalPayload(float3 receiverNormal, float4 packedNormal,
    float normalMultiplier, float3 decalRight, float3 decalUp, bool fixedBasis, float weight)
{
    float2 xy = float2(packedNormal.r * packedNormal.a, packedNormal.g) * 2 - 1;
    float3 normalTS = float3(xy * normalMultiplier, sqrt(1 - min(dot(xy, xy), 1)));
    float3 tangent, bitangent, normal;
    if (fixedBasis)
    {
        tangent = decalRight;
        bitangent = cross(decalUp, decalRight);
        normal = decalUp;
    }
    else
    {
        bitangent = cross(receiverNormal, decalRight);
        tangent = cross(receiverNormal, bitangent);
        // Original checks nonzero, not sign. Do not normalize this basis.
        if (dot(decalRight, tangent) != 0) tangent = -tangent;
        normal = receiverNormal;
    }
    float3 projected = normalTS.x * tangent + normalTS.y * bitangent + normalTS.z * normal;
    return normalize(projected * weight + receiverNormal * (1 - weight)) * .5 + .5;
}
#endif
