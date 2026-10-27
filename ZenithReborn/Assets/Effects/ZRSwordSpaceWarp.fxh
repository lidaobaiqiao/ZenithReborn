// 独立的环境扭曲计算。只读取当前刀光覆盖，不新增纹理或改动带面法线。
float uGlassPartSpaceWarp;
float uSpaceWarpStrength;
float uSpaceWarpRadius;
float uSpaceWarpPhase;
float2 uSpaceWarpOrigin;
float4 uSpaceWarpPixelToWorld;

float2 SwordSpaceOffset(sampler coverageSampler,float2 uv,float2 texel,float centerCoverage)
{
    if(uGlassPartSpaceWarp<0.5||uSpaceWarpStrength<=0)return float2(0,0);
    float2 pressure=float2(0,0);
    // 两圈八向采样让扭曲跨过带面边界连续衰减，不复制一条可见的颜色光带。
    [unroll]
    for(int i=0;i<8;i++)
    {
        float angle=i*0.785398163;
        float2 direction=float2(cos(angle),sin(angle));
        float2 nearUV=clamp(uv+direction*texel*uSpaceWarpRadius*0.45,texel*0.5,1-texel*0.5);
        float2 farUV=clamp(uv+direction*texel*uSpaceWarpRadius,texel*0.5,1-texel*0.5);
        float density=tex2D(coverageSampler,nearUV).a*0.70+tex2D(coverageSampler,farUV).a*0.30;
        pressure-=direction*density*0.5;
    }
    // 背景相位随世界坐标连续，轻微回旋表现掠过后的空间扰动。
    float2 pixel=uv/texel;
    float2 world=float2(dot(pixel,uSpaceWarpPixelToWorld.xy),dot(pixel,uSpaceWarpPixelToWorld.zw))+uSpaceWarpOrigin;
    float phase=dot(world,float2(0.013,0.021))-uSpaceWarpPhase;
    float2 curl=float2(-pressure.y,pressure.x)*sin(phase)*0.28;
    float2 pixels=(pressure+curl)*uSpaceWarpStrength*(1-centerCoverage);
    // 限制最大像素位移；覆盖内部仍使用原有折射，外侧只扭曲环境而不染色。
    pixels*=min(1,uSpaceWarpStrength/max(length(pixels),0.0001));
    return pixels*texel;
}
