// 固定颜色只用于确认隔离测试中的像素着色器执行和读回路径。
float4 Solid(float2 uv:TEXCOORD0):COLOR0
{
    return float4(1,0,1,1);
}
technique Sentinel
{
    pass SolidColor
    {
        PixelShader=compile ps_3_0 Solid();
    }
}
