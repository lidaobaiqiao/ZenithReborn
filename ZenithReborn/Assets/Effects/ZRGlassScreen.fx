// 由原版屏幕滤镜提供当前帧完整背景；只在主飞剑蒙版覆盖的像素上折射。
sampler screenSampler:register(s0);
sampler normalSampler:register(s1);
sampler surfaceSampler:register(s2);
float2 uGlassTexel;
float uGlassTransmission;
float uGlassBodyOpacity;
float uGlassSsrStrength;
float uGlassThickness;
float uGlassIor;
float uMaskReady;
float uHasMaterialProperties;
// 玻璃滤镜构成项 B7-1~B7-6，逐项测试用；全部为 1 时与生产形态一致
float uGlassPartCoverage;
float uGlassPartRefraction;
float uGlassPartBlur;
float uGlassPartTransmittance;
float uGlassPartCoating;
float uGlassPartBloom;
texture uMaterialProperties;
texture uMaterialTransmission;
sampler propertiesSampler:register(s3)=sampler_state{Texture=<uMaterialProperties>;MinFilter=Linear;MagFilter=Linear;AddressU=Clamp;AddressV=Clamp;};
sampler transmissionSampler:register(s4)=sampler_state{Texture=<uMaterialTransmission>;MinFilter=Linear;MagFilter=Linear;AddressU=Clamp;AddressV=Clamp;};

#include "ZRSwordSpaceWarp.fxh"

float4 GlassScreen(float2 uv:TEXCOORD0):COLOR0
{
    float4 mask=tex2D(normalSampler,uv);
    float baseCoverage=smoothstep(0.01,0.48,mask.a);
    float2 spaceOffset=SwordSpaceOffset(normalSampler,uv,uGlassTexel,baseCoverage)*uMaskReady;
    float4 original=tex2D(screenSampler,clamp(uv+spaceOffset,uGlassTexel*0.5,1-uGlassTexel*0.5));
    float4 properties=tex2D(propertiesSampler,uv)*uHasMaterialProperties;
    float advanced=step(.001,properties.a)*uHasMaterialProperties;
    float3 decoded=properties.rgb/max(properties.a,.001);
    float ior=lerp(uGlassIor,1+decoded.r*1.5,advanced);
    float thickness=lerp(uGlassThickness,decoded.g*128,advanced);
    // 覆盖度定义玻璃是否存在；主体成熟后替换背景，羽化与消散区域才混回原图。
    float coverage=smoothstep(0.01,0.48,mask.a)*uMaskReady*uGlassPartCoverage;
    coverage=lerp(coverage,mask.a*uMaskReady*uGlassPartCoverage,advanced);
    float2 normalXY=mask.rg/max(mask.a,0.001)*2-1;
    float3 normal=normalize(float3(normalXY,sqrt(saturate(1-dot(normalXY,normalXY)))));
    float3 ray=refract(float3(0,0,-1),normal,1/max(ior,1));
    float2 direction=ray.xy/max(abs(ray.z),0.1);
    float roughness=mask.b/max(mask.a,0.001);
    float2 offset=direction*thickness*uGlassTexel*uGlassPartRefraction;
    float2 sampleUV=clamp(uv+offset,uGlassTexel*0.5,1-uGlassTexel*0.5);
    float4 transmitted=tex2D(screenSampler,sampleUV);
    float2 spread=uGlassTexel*roughness*2;
    transmitted=lerp(transmitted,(tex2D(screenSampler,sampleUV+spread)+tex2D(screenSampler,sampleUV-spread))*0.5,
        roughness*uGlassPartBlur);
    // 透射与反光一次合成。剑迹自身已经形成／侵蚀，不再乘全屏滤镜的慢渐入系数。
    float4 coating=tex2D(surfaceSampler,uv)/max(mask.a,0.001)*uGlassPartCoating;
    // 透射控制光能吸收，不把未偏折的完整物体再次叠回玻璃内部。
    // 默认剑流主体近乎中性通透；只吸收少量光能，颜色来自独立高光而非整片着色。
    float transmittance=exp(-(1-uGlassTransmission)*0.015*uGlassPartTransmittance);
    // 主体密度控制背景透过比例；覆盖度、反射和亮刃不乘这个数。
    float bodyOpacity=saturate(uGlassBodyOpacity);
    float3 reflectedRay=reflect(float3(0,0,-1),normal);
    float2 reflectionUV=clamp(uv+reflectedRay.xy*uGlassTexel*34.0,uGlassTexel*0.5,1-uGlassTexel*0.5);
    float3 sceneReflection=pow(max(tex2D(screenSampler,reflectionUV).rgb,0),2.2)*saturate(coating.a)*uGlassSsrStrength;
    float3 glass=pow(saturate(pow(max(transmitted.rgb,0),2.2)*transmittance*(1-saturate(coating.a))*(1-bodyOpacity)
        +max(coating.rgb,0)+sceneReflection),1/2.2);
    float3 throughput=tex2D(transmissionSampler,uv).rgb/max(mask.a,.001);
    float3 advancedGlass=pow(saturate(coating.rgb+pow(max(transmitted.rgb,0),2.2)*throughput),1/2.2);
    glass=lerp(glass,advancedGlass,advanced);
    // 泛光只扩散材质自身的亮部，不把白天背景当作白色笔刷叠到透明玻璃上。
    // 按最大通道提取亮部并保留各通道比例，避免逐通道截断洗掉剑色。
    float3 bloom=float3(0,0,0);
    float nearMask=0;
    float2 bloomStep=uGlassTexel*5.0;
    [unroll]
    for(int i=0;i<8;i++)
    {
        float angle=i*0.7853982;
        float2 dir=float2(cos(angle),sin(angle));
        float2 suv=clamp(uv+dir*bloomStep,uGlassTexel,1-uGlassTexel);
        float sampleMask=tex2D(normalSampler,suv).a;
        float3 radiance=max(tex2D(surfaceSampler,suv).rgb/max(sampleMask,0.001),0);
        float peak=max(radiance.r,max(radiance.g,radiance.b));
        bloom+=radiance*(max(peak-0.72,0)/max(peak,0.001))*sampleMask;
        nearMask+=sampleMask;
    }
    bloom*=0.125;
    nearMask*=0.125;
    // 同一批邻域遮罩限制扩散范围，生命周期结束后亮部与泛光一起消失。
    float3 combined=lerp(original.rgb,glass,coverage)+bloom*0.65*saturate(nearMask*3.0)*uGlassPartBloom;
    return float4(combined,original.a);
}
technique Glass
{
    pass GlassScreen
    {
        PixelShader=compile ps_3_0 GlassScreen();
    }
}
