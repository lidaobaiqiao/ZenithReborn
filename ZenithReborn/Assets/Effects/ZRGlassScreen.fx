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
    // 默认剑流主体近乎中性通透；只吸收少量光能，颜色来自独立高光而非整片着色。
    float transmittance=exp(-(1-uGlassTransmission)*0.015);
    float3 glass=pow(saturate(pow(max(transmitted.rgb,0),2.2)*transmittance*(1-saturate(coating.a))+max(coating.rgb,0)),1/2.2);
    float3 throughput=tex2D(transmissionSampler,uv).rgb/max(mask.a,.001);
    float3 advancedGlass=pow(saturate(coating.rgb+pow(max(transmitted.rgb,0),2.2)*throughput),1/2.2);
    glass=lerp(glass,advancedGlass,advanced);
    // 高光泛光：把屏幕上过亮的部分（>0.72）向四周扩散一圈再叠回去。
    // 泰拉没有真阴影和体积光，这是最接近的替代——光会"污染"周围环境，
    // 读起来才有体积感，而不是一张贴在屏幕上的白纸。阈值保证只对剑迹和强光生效。
    float3 bloom=float3(0,0,0);
    float nearMask=0;
    float2 bloomStep=uGlassTexel*5.0;
    [unroll]
    for(int i=0;i<8;i++)
    {
        float angle=i*0.7853982;
        float2 dir=float2(cos(angle),sin(angle));
        float2 suv=clamp(uv+dir*bloomStep,uGlassTexel,1-uGlassTexel);
        bloom+=max(tex2D(screenSampler,suv).rgb-0.72,0);
        nearMask+=tex2D(normalSampler,suv).a;   // 同一批位置上的剑迹遮罩
    }
    bloom*=0.125;
    nearMask*=0.125;
    // 关键：泛光必须被剑迹遮罩限制。只按屏幕亮度算的话，
    // 白天野外亮→泛光强，夜晚洞穴暗→泛光弱，同一个特效在不同存档亮度就不一样了。
    float3 combined=lerp(original.rgb,glass,coverage)+bloom*0.65*saturate(nearMask*3.0);
    return float4(combined,original.a);
}
technique Glass
{
    pass GlassScreen
    {
        PixelShader=compile ps_3_0 GlassScreen();
    }
}
