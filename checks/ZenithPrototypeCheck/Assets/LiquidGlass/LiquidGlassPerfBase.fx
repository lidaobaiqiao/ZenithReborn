// 性能检查的同网格基线：保留相同投影、采样、覆盖与混合，不执行液态高度和光学计算。
float4x4 uTransform;
texture uScene;
sampler sceneSampler=sampler_state
{
    Texture=<uScene>;
    MinFilter=Linear;MagFilter=Linear;MipFilter=None;
    AddressU=Clamp;AddressV=Clamp;
};
struct VertexInput
{
    float4 Position:POSITION0;
    float4 Color:COLOR0;
    float2 UV:TEXCOORD0;
    float2 GradU:TEXCOORD1;
    float2 GradV:TEXCOORD2;
};
struct PixelInput
{
    float4 Position:POSITION0;
    float4 Color:COLOR0;
    float2 SceneUV:TEXCOORD0;
};
PixelInput Transform(VertexInput input)
{
    PixelInput output;
    float4 clip=mul(input.Position,uTransform);
    output.Position=clip;
    output.Color=input.Color;
    output.SceneUV=clip.xy/clip.w*float2(.5,-.5)+.5;
    return output;
}
float4 Shade(PixelInput input):COLOR0
{
    return float4(tex2D(sceneSampler,input.SceneUV).rgb,input.Color.a);
}
technique Baseline
{
    pass Surface
    {
        VertexShader=compile vs_3_0 Transform();
        PixelShader=compile ps_3_0 Shade();
    }
}
