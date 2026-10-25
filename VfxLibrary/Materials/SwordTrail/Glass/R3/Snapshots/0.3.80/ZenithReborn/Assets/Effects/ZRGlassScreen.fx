// 由原版屏幕滤镜提供当前帧完整背景；只在主飞剑蒙版覆盖的像素上折射。
sampler screenSampler:register(s0);
sampler normalSampler:register(s1);
sampler surfaceSampler:register(s2);
float2 uGlassTexel;
float uGlassTransmission;
float uGlassThickness;
float uGlassIor;
float uMaskReady;
float uHasMaterialProperties;
texture uMaterialProperties;
texture uMaterialTransmission;
sampler propertiesSampler:register(s3)=sampler_state{Texture=<uMaterialProperties>;MinFilter=Linear;MagFilter=Linear;AddressU=Clamp;AddressV=Clamp;};
sampler transmissionSampler:register(s4)=sampler_state{Texture=<uMaterialTransmission>;MinFilter=Linear;MagFilter=Linear;AddressU=Clamp;AddressV=Clamp;};

float4 GlassScreen(float2 uv:TEXCOORD0):COLOR0
{
    float4 original=tex2D(screenSampler,uv);
    float4 mask=tex2D(normalSampler,uv);
    float4 properties=tex2D(propertiesSampler,uv)*uHasMaterialProperties;
    float advanced=step(.001,properties.a)*uHasMaterialProperties;
    float3 decoded=properties.rgb/max(properties.a,.001);
    float flowMaterial=advanced*(1-step(.5,decoded.b));
    float ior=lerp(uGlassIor,1+decoded.r*1.5,advanced);
    float thickness=lerp(uGlassThickness,decoded.g*128,advanced);
    // 覆盖度定义玻璃是否存在；主体成熟后替换背景，羽化与消散区域才混回原图。
    float coverage=smoothstep(0.01,0.48,mask.a)*uMaskReady;
    // 剑流维持成熟带面的完整背景替换，不能因新增属性缓冲而混回完整树干。
    coverage=lerp(coverage,mask.a*uMaskReady,advanced*(1-flowMaterial));
    float2 normalXY=mask.rg/max(mask.a,0.001)*2-1;
    float3 normal=normalize(float3(normalXY,sqrt(saturate(1-dot(normalXY,normalXY)))));
    float3 ray=refract(float3(0,0,-1),normal,1/max(ior,1));
    float2 direction=ray.xy/max(abs(ray.z),0.1);
    float roughness=mask.b/max(mask.a,0.001);
    float2 offset=direction*thickness*uGlassTexel;
    float2 sampleUV=clamp(uv+offset,uGlassTexel*0.5,1-uGlassTexel*0.5);
    float4 transmitted=tex2D(screenSampler,sampleUV);
    float2 spread=uGlassTexel*roughness*2;
    transmitted=lerp(transmitted,(tex2D(screenSampler,sampleUV+spread)+tex2D(screenSampler,sampleUV-spread))*0.5,roughness);
    // 透射与反光一次合成。剑迹自身已经形成／侵蚀，不再乘全屏滤镜的慢渐入系数。
    float4 coating=tex2D(surfaceSampler,uv)/max(mask.a,0.001);
    // 透射控制光能吸收，不把未偏折的完整物体再次叠回玻璃内部。
    float transmittance=exp(-(1-uGlassTransmission)*0.35);
    float3 glass=transmitted.rgb*transmittance*(1-saturate(coating.a))+coating.rgb;
    float3 throughput=tex2D(transmissionSampler,uv).rgb/max(mask.a,.001);
    float3 reflected=coating.rgb;
    // 短距离屏幕反射近似：直接读取当前帧，和环境图按强度混合，不能把白色占位图当反射。
    float3 reflectionDirection=reflect(float3(0,0,-1),normal);
    float2 reflectionUV=clamp(uv+reflectionDirection.xy*uGlassTexel*40,uGlassTexel*.5,1-uGlassTexel*.5);
    float3 screenReflection=pow(max(tex2D(screenSampler,reflectionUV).rgb,0),2.2);
    float f0=pow((ior-1)/(ior+1),2),fresnel=f0+(1-f0)*pow(1-saturate(normal.z),5);
    reflected=lerp(reflected,screenReflection*fresnel,saturate(decoded.b*5)*flowMaterial*.25);
    float3 advancedGlass=pow(saturate(reflected+pow(max(transmitted.rgb,0),2.2)*throughput),1/2.2);
    glass=lerp(glass,advancedGlass,advanced);
    // 去掉从背景亮点凭空扩散的泛光；玻璃只在其实际覆盖区域反射与折射。
    float3 combined=lerp(original.rgb,glass,coverage);
    return float4(combined,original.a);
}
technique Glass
{
    pass GlassScreen
    {
        PixelShader=compile ps_3_0 GlassScreen();
    }
}
