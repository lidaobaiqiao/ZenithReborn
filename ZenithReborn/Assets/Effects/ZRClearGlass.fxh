// 默认剑流的光学计算：颜色先转线性光能，吸收与表面覆盖独立。
// 光程是屏幕薄层近似，不伪称拥有三维场景深度或光线追踪。
float3 GlassLinear(float3 color)
{
    return pow(saturate(color),2.2);
}

float3 GlassF0(float ior,float metallic,float3 color)
{
    float dielectric=pow((ior-1)/(ior+1),2);
    return lerp(dielectric.xxx,GlassLinear(color),saturate(metallic));
}

float3 GlassThroughput(float3 color,float density,float transmission,float thickness,float facing,float3 fresnel)
{
    // Beer-Lambert 吸收随路径增长；密度为零仍保留表面反射，不能把整个带面淡出。
    float path=max(thickness,0)/18.0/max(facing,.25);
    float extinction=-log(max(1-saturate(density),.0001));
    float3 absorption=exp(-(0.2+(1-GlassLinear(color))*.8)*extinction*path);
    return absorption*exp(-(1-transmission)*.015*path)*(1-fresnel);
}
