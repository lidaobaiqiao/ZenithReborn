// 固定分支只用于检查浮点参数是否提交到像素着色器。
float uProbeMode;
float4 TestMode(float2 uv:TEXCOORD0):COLOR0
{
    if(uProbeMode<0.5)return float4(1,0,0,1);
    if(uProbeMode>5.5)return float4(0,1,0,1);
    return float4(0,0,1,1);
}
technique ParameterProbe
{
    pass Test
    {
        PixelShader=compile ps_3_0 TestMode();
    }
}
