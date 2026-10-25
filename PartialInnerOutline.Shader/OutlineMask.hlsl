Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);

cbuffer Constants : register(b0)
{
    float featherLow : packoffset(c0.x);
    float featherInverseWidth : packoffset(c0.y);
    float opacity : packoffset(c0.z);
    float bandOffset : packoffset(c0.w);
    float2 bandDirection : packoffset(c1.x);
    float bandEdgeNear : packoffset(c1.z);
    float bandEdgeFar : packoffset(c1.w);
    float4 sourceBounds : packoffset(c2);
};

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0) : SV_TARGET
{
    bool isInside = posScene.x >= sourceBounds.x && posScene.y >= sourceBounds.y && posScene.x < sourceBounds.z && posScene.y < sourceBounds.w;
    float coverage = isInside ? SourceTexture.SampleLevel(SourceSampler, uv0.xy, 0).a : 1.0f;
    float ramp = saturate((coverage - featherLow) * featherInverseWidth);
    float feather = featherInverseWidth > 0.0f ? ramp * ramp * (3.0f - 2.0f * ramp) : step(featherLow, coverage);

    float bandDistance = abs(dot(posScene.xy, bandDirection) + bandOffset);
    float band = bandEdgeFar > bandEdgeNear ? 1.0f - smoothstep(bandEdgeNear, bandEdgeFar, bandDistance) : step(bandDistance, bandEdgeNear);

    float mask = feather * band * opacity;
    return float4(mask, mask, mask, mask);
}
