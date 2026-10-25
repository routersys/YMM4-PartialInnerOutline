Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);

cbuffer Constants : register(b0)
{
    float stepPixels : packoffset(c0.x);
    int sampleCount : packoffset(c0.y);
    int isAngular : packoffset(c0.z);
    int isInverted : packoffset(c0.w);
    float rotationCos : packoffset(c1.x);
    float rotationSin : packoffset(c1.y);
    float pad0 : packoffset(c1.z);
    float pad1 : packoffset(c1.w);
};

float Coverage(float2 uv)
{
    float alpha = SourceTexture.SampleLevel(SourceSampler, uv, 0).a;
    return isInverted != 0 ? 1.0f - alpha : alpha;
}

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0) : SV_TARGET
{
    float best = Coverage(uv0.xy);
    float2 reach = stepPixels * uv0.zw;
    float2 ray = float2(0.0f, 1.0f);

    for (int index = 0; index < sampleCount && best < 1.0f; index++)
    {
        float2 offset = isAngular != 0 ? ray / max(abs(ray.x), abs(ray.y)) : ray;
        best = max(best, Coverage(uv0.xy + offset * reach));
        ray = float2(ray.x * rotationCos - ray.y * rotationSin, ray.y * rotationCos + ray.x * rotationSin);
    }

    return float4(best, best, best, best);
}
