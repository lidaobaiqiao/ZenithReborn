// 由原版屏幕滤镜提供当前帧完整背景；只在主飞剑蒙版覆盖的像素上折射。
sampler screenSampler:register(s0);
sampler normalSampler:register(s1);
sampler surfaceSampler:register(s2);
float2 uGlassTexel;
float uGlassTransmission;
float uGlassThickness;
float uGlassIor;
float uGlassSsrStrength;
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
    float ior=lerp(uGlassIor,1+decoded.r*1.5,advanced);
    float thickness=lerp(uGlassThickness,decoded.g*128,advanced);
    // 覆盖度定义玻璃是否存在；主体成熟后替换背景，羽化与消散区域才混回原图。
    float coverage=smoothstep(0.01,0.48,mask.a)*uMaskReady;
    coverage=lerp(coverage,mask.a*uMaskReady,advanced);
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
    float3 advancedGlass=pow(saturate(coating.rgb+pow(max(transmitted.rgb,0),2.2)*throughput),1/2.2);
    glass=lerp(glass,advancedGlass,advanced);
    // 简化环境反射：同帧画面沿表面反射方向取样，再按介电菲涅耳加权。
    // 只能反映屏幕内可见内容，不重建屏幕外物体。
    float3 reflectedRay=reflect(float3(0,0,-1),normal);
    float2 reflectionUV=clamp(uv+reflectedRay.xy*uGlassTexel*34.0,uGlassTexel*0.5,1-uGlassTexel*0.5);
    float3 reflected=tex2D(screenSampler,reflectionUV).rgb;
    float f0=pow((ior-1)/(ior+1),2);
    float fresnel=f0+(1-f0)*pow(1-saturate(normal.z),5);
    glass+=reflected*saturate(fresnel*2.5*uGlassSsrStrength);
    // 多剑相交时等比例限制峰值，避免亮色分别截断后变白。
    glass/=max(1.0,max(glass.r,max(glass.g,glass.b)));

    // 泛光只扩散剑迹自身亮色，避免明亮背景给各色剑迹刷上白光。
    float3 bloom=float3(0,0,0);
    float nearMask=0;
    float2 bloomStep=uGlassTexel*5.0;
    [unroll]
    for(int i=0;i<8;i++)
    {
        float angle=i*0.7853982;
        float2 dir=float2(cos(angle),sin(angle));
        float2 suv=clamp(uv+dir*bloomStep,uGlassTexel,1-uGlassTexel);
        bloom+=max(tex2D(surfaceSampler,suv).rgb-0.45,0);
        nearMask+=tex2D(normalSampler,suv).a;   // 同一批位置上的剑迹遮罩
    }
    bloom*=0.125;
    nearMask*=0.125;
    // 关键：泛光必须被剑迹遮罩限制。只按屏幕亮度算的话，
    // 白天野外亮→泛光强，夜晚洞穴暗→泛光弱，同一个特效在不同存档亮度就不一样了。
    float3 combined=lerp(original.rgb,glass,coverage)+bloom*0.35*saturate(nearMask*3.0);
    return float4(combined,original.a);
}
technique Glass
{
    pass GlassScreen
    {
        PixelShader=compile ps_3_0 GlassScreen();
    }
}
