// 自有二维材质核心。采用通用微表面公式，不包含其他模组或渲染器的源码。
// 受光与环境贴图可用；次表面、体积和薄膜明确为薄层实时近似。
float4x4 transformMatrix;
float4 uBase;
float4 uOptics;
float4 uSurface;
float4 uCoat;
float4 uSheen;
float4 uFilm;
float4 uEmission;
float4 uAbsorption;
float4 uSubsurface;
float4 uVolume;
float4 uShape;
float4 uLight;
float3 uLightColor;
float3 uAmbient;
float3 uVolumeColor;
float4 uMaps;
float4 uMoreMaps;
float4 uMaskFrame;
float2 uSceneTexel;
float uTime;
float uSsrStrength;
float uDissolve;
float uMode;
float uUseBrush;
float uUseNoise;
float uUseLayerMask;
// 液态玻璃：在基面法线之上叠加圆滑条带起伏，只驱动折射方向。
// 0 时完全退回原来的平整玻璃，便于逐项对照。
float uRidgeStrength;
#include "ZRLiquidGlass.fxh"
float4 uDetailA[4];
float4 uDetailB[4];
float4 uDetailColor[4];
float4 uDetailMaterial[4];
texture uScene;
texture uColorMap;
texture uMaskMap;
texture uNormalMap;
texture uSurfaceMap;
texture uHeightMap;
texture uEmissionMap;
texture uEnvironment;
texture uBrushTex;
texture uNoiseTex;
texture uLayerMask;
sampler sceneSampler=sampler_state{Texture=<uScene>;MinFilter=Linear;MagFilter=Linear;AddressU=Clamp;AddressV=Clamp;};
sampler colorSampler=sampler_state{Texture=<uColorMap>;MinFilter=Linear;MagFilter=Linear;AddressU=Wrap;AddressV=Clamp;};
sampler maskSampler=sampler_state{Texture=<uMaskMap>;MinFilter=Linear;MagFilter=Linear;AddressU=Clamp;AddressV=Clamp;};
sampler normalSampler=sampler_state{Texture=<uNormalMap>;MinFilter=Linear;MagFilter=Linear;AddressU=Wrap;AddressV=Clamp;};
sampler surfaceSampler=sampler_state{Texture=<uSurfaceMap>;MinFilter=Linear;MagFilter=Linear;AddressU=Wrap;AddressV=Clamp;};
sampler heightSampler=sampler_state{Texture=<uHeightMap>;MinFilter=Linear;MagFilter=Linear;AddressU=Wrap;AddressV=Clamp;};
sampler emissionSampler=sampler_state{Texture=<uEmissionMap>;MinFilter=Linear;MagFilter=Linear;AddressU=Wrap;AddressV=Clamp;};
sampler environmentSampler=sampler_state{Texture=<uEnvironment>;MinFilter=Linear;MagFilter=Linear;MipFilter=Linear;AddressU=Wrap;AddressV=Clamp;};
sampler brushSampler=sampler_state{Texture=<uBrushTex>;MinFilter=Linear;MagFilter=Linear;AddressU=Wrap;AddressV=Wrap;};
sampler noiseSampler=sampler_state{Texture=<uNoiseTex>;MinFilter=Linear;MagFilter=Linear;AddressU=Wrap;AddressV=Wrap;};
sampler layerMaskSampler=sampler_state{Texture=<uLayerMask>;MinFilter=Linear;MagFilter=Linear;AddressU=Clamp;AddressV=Clamp;};
struct VertexInput{float4 Position:POSITION0;float4 Color:COLOR0;float2 UV:TEXCOORD0;float2 Along:TEXCOORD1;float2 Across:TEXCOORD2;};
struct PixelInput{float4 Position:POSITION0;float4 Color:COLOR0;float2 UV:TEXCOORD0;float2 SceneUV:TEXCOORD1;float2 Along:TEXCOORD2;float2 Across:TEXCOORD3;};
PixelInput Transform(VertexInput input)
{
    PixelInput o;o.Position=mul(input.Position,transformMatrix);o.Color=input.Color;o.UV=input.UV;
    o.SceneUV=o.Position.xy/o.Position.w*float2(.5,-.5)+.5;o.Along=input.Along;o.Across=input.Across;return o;
}
float3 Fresnel(float3 f0,float angle){return f0+(1-f0)*pow(1-saturate(angle),5);}
float3 Environment(float3 direction,float roughness)
{
    float2 uv=float2(atan2(direction.z,direction.x)/6.2831853+.5,acos(clamp(-direction.y,-1,1))/3.14159265);
    // 无环境贴图时只使用环境色；有贴图时按方向取样并随粗糙度模糊。
    float2 spread=float2(roughness*.09,roughness*.06);
    float3 sample=tex2D(environmentSampler,uv).rgb;
    sample=lerp(sample,(tex2D(environmentSampler,uv+spread).rgb+tex2D(environmentSampler,uv-spread).rgb)*.5,roughness);
    return lerp(uAmbient,pow(max(sample,0),2.2),uMoreMaps.z)*uLight.w;
}
float Distribution(float3 n,float3 h,float3 t,float3 b,float roughness,float anisotropy)
{
    float a=max(.02,roughness*roughness),aspect=sqrt(1-.9*abs(anisotropy));
    float ax=a/aspect,ay=a*aspect;
    if(anisotropy<0){float temp=ax;ax=ay;ay=temp;}
    float3 projected=float3(dot(t,h)/ax,dot(b,h)/ay,max(.001,dot(n,h)));
    float denominator=dot(projected,projected);
    return 1/max(.00001,3.14159265*ax*ay*denominator*denominator);
}
float Visibility(float nv,float nl,float roughness)
{
    float k=(roughness+1)*(roughness+1)*.125;
    return nv/(nv*(1-k)+k)*nl/(nl*(1-k)+k)/max(.001,4*nv*nl);
}
float4 Shade(PixelInput input):COLOR0
{
    float2 uv=input.UV;float h=saturate(uv.y),s=saturate(uv.x);
    float3 along=normalize(float3(input.Along,0)),across=normalize(float3(input.Across,0));
    float shape=1;float3 n=float3(0,0,1);
    if(uShape.x>1.5)
    {
        float edge=.88+.025*sin(s*9-uTime*.5);
        shape=smoothstep(-.035,.015,uv.y)*(1-smoothstep(edge-.08,edge+.03,uv.y));
        n=normalize(float3(input.Across*(h-.46)*uShape.y+input.Along*uShape.z,1));
    }
    else if(uShape.x>.5)
    {
        float2 xy=(uv-.5)*2.05;float radius=dot(xy,xy);
        shape=1-smoothstep(.96,1,radius);n=normalize(float3(xy,sqrt(saturate(1-radius))));
    }
    float2 atlasUV=uMaskFrame.xy+saturate(uv)*uMaskFrame.zw;
    shape*=lerp(1,tex2D(maskSampler,atlasUV).a,uMaps.y);
    float grain=.5+.22*sin(s*19+h*13+uTime*.4)+.15*cos(h*27-s*11);
    float erosion=uDissolve*(1.4+s*.24+h*.18);
    float alive=1-smoothstep(grain-.1,grain+.12,erosion);
    // 剑迹按真实年龄推进，实验室其他形状仍保留其原来的侵蚀语义。
    if(uShape.x>1.5&&uDissolve>0)
    {
        float resistance=.12+(1-input.Color.r)*.76+(1-h)*.10+(grain-.5)*.09;
        alive=1-smoothstep(resistance-.055,resistance+.055,uDissolve*1.12);
    }
    float endingRim=4*alive*(1-alive)*uDissolve;
    float coverage=saturate(shape*alive*input.Color.a*uBase.a);
    float3 texNormal=tex2D(normalSampler,uv).xyz*2-1;
    float3 tangentValue=along-n*dot(n,along);
    tangentValue+=float3(0,1,0)*step(length(tangentValue),.0001);
    float3 t=normalize(tangentValue);float3 b=normalize(cross(n,t));
    b*=dot(b,across)<0?-1:1;
    n=normalize(n+uMaps.z*uSurface.z*(t*texNormal.x+b*texNormal.y+n*(texNormal.z-1)));
    float height=tex2D(heightSampler,uv).r;
    n=normalize(n-float3(ddx(height),ddy(height),0)*uMoreMaps.x*uSurface.w*4);
    n=normalize(float3(n.xy,max(.01,n.z)));
    tangentValue=along-n*dot(n,along);tangentValue+=float3(0,1,0)*step(length(tangentValue),.0001);
    t=normalize(tangentValue);b=normalize(cross(n,t));
    float rotation=uSurface.y*0.0174532925;
    float3 rotated=t*cos(rotation)+b*sin(rotation);b=normalize(cross(n,rotated));t=rotated;
    float3 orm=tex2D(surfaceSampler,uv).rgb;
    float metal=uOptics.x*lerp(1,orm.r,uMaps.w);
    float rough=max(.02,uOptics.y*lerp(1,orm.g,uMaps.w));
    float occlusion=lerp(1,orm.b,uMaps.w);
    float3 base=pow(max(uBase.rgb*lerp(float3(1,1,1),tex2D(colorSampler,uv-float2(uTime*.025,0)).rgb,uMaps.x),0),2.2);
    // 区域遮罩与整体动态形状分工：只调本片表面属性，不多画一层轮廓。
    float4 regions=tex2D(layerMaskSampler,saturate(uv));
    float3 detailEmission=0;
    [unroll]
    for(int index=0;index<4;index++)
    {
        float4 detail=uDetailA[index],motion=uDetailB[index];
        float2 detailUV=uv*float2(detail.z,motion.x)-uTime*float2(detail.w,motion.y);
        float noise=tex2D(noiseSampler,detailUV*.37).r;
        detailUV+=(noise-.5)*uUseNoise*.07;
        float field=.5+.25*sin(detailUV.x*6.2831853)+.25*cos(detailUV.y*6.2831853+index*1.7);
        field=lerp(field,tex2D(brushSampler,detailUV).r,uUseBrush);
        float gate=lerp(lerp(1,1-smoothstep(.05,.55,h),motion.z),regions[index],uUseLayerMask);
        float weight=saturate(detail.x*gate);
        float3 added=base+uDetailColor[index].rgb*field*weight;
        float3 multiplied=base*lerp(float3(1,1,1),uDetailColor[index].rgb*(.45+field),weight);
        base=lerp(added,multiplied,saturate(detail.y));
        rough=clamp(rough+uDetailMaterial[index].x*weight,.02,1);
        metal=saturate(metal+uDetailMaterial[index].y*weight);
        detailEmission+=uDetailColor[index].rgb*field*weight*uDetailMaterial[index].z;
    }
    float3 v=float3(0,0,1),l=normalize(uLight.xyz),halfway=normalize(v+l);
    float nv=clamp(dot(n,v),.001,1),nl=saturate(dot(n,l)),vh=saturate(dot(v,halfway));
    float dielectric=pow((uOptics.z-1)/(uOptics.z+1),2)*uSurface.x;
    float3 f0=lerp(dielectric.xxx,base,metal);
    // 三波段干涉色，近似薄膜色移；不是完整光谱积分。
    float3 film=.5+.5*cos(6.2831853*(2*uFilm.z*uFilm.y*nv/float3(650,510,475)));
    f0=lerp(f0,saturate(f0*(.4+film*2.4)),uFilm.x);
    float3 f=Fresnel(f0,vh),fv=Fresnel(f0,nv);
    float d=Distribution(n,halfway,t,b,rough,uFilm.w);
    float visibility=Visibility(nv,max(.001,nl),rough);
    float diffuseWeight=(1-metal)*(1-uOptics.w);
    float3 direct=((1-f)*base*diffuseWeight/3.14159265+f*d*visibility)*uLightColor*nl;
    float3 env=Environment(reflect(-v,n),rough)*fv*occlusion;
    float3 lighting=direct+env+base*uAmbient*diffuseWeight*occlusion;
    // 次表面采用背光包裹，薄层体积采用解析吸收／散射；没有场景深度或三维光程。
    float wrap=pow(saturate((dot(n,l)+uSubsurface.w)/(1+uSubsurface.w)),2);
    lighting+=uSubsurface.rgb*uCoat.w*wrap*(1-metal)*uLightColor*.6;
    float sheen=pow(saturate(1-nv),1+uSheen.w*4);
    lighting+=uSheen.rgb*sheen*uCoat.z*(uLightColor*.25+Environment(reflect(-v,n),uSheen.w));
    float coatF0=pow((uCoat.y-1)/(uCoat.y+1),2);
    float coatF=Fresnel(coatF0.xxx,nv).x;
    float coatD=Distribution(n,halfway,t,b,uCoat.x,0);
    lighting=lighting*(1-uEmission.w*coatF)+uEmission.w*(coatD*Visibility(nv,max(nl,.001),uCoat.x)*coatF*nl*uLightColor+Environment(reflect(-v,n),uCoat.x)*coatF);
    float density=uVolume.x*uShape.w/32;
    float volumeAlpha=1-exp(-density);
    float g=uVolume.y,cosTheta=dot(-v,l);
    float phase=(1-g*g)/max(.05,pow(max(.001,1+g*g-2*g*cosTheta),1.5));
    lighting+=uVolumeColor*volumeAlpha*(phase*uLightColor*.15+uVolume.z);
    float3 emission=uEmission.rgb*lerp(float3(1,1,1),tex2D(emissionSampler,uv).rgb,uMoreMaps.y);
    lighting+=emission+detailEmission;
    lighting+=lerp(base,float3(1,1,1),.35)*endingRim*.35;
    // 反射层以压缩线性颜色存储，正式合成与预览使用相同的解码约定。
    float3 reflected=lighting/(1+lighting);
    float3 absorption=exp(-(1-uAbsorption.rgb)*uAbsorption.a*uShape.w/32);
    float3 throughput=absorption*(1-fv)*(1-metal)*uOptics.w*(1-volumeAlpha)*(1-uEmission.w*coatF);
    // 液态玻璃：圆滑条带把基面顶起来，折射方向随曲面倾斜。
    // 只改折射用的法线，高光与环境反射仍走基面法线 n，
    // 于是平整玻璃的反射保持不变，可以单独判断折射形态对不对。
    float3 refrN=n;
    if(uRidgeStrength>0.0)
    {
        float2 ridge=ZRGlassRidges(s,h,uTime)*uRidgeStrength;
        refrN=normalize(float3(n.xy+input.Across*ridge.y+input.Along*ridge.x,max(.01,n.z)));
    }
    float4 normalOutput=float4((refrN.xy*.5+.5)*coverage,rough*coverage,coverage);
    if(uMode>.5&&uMode<1.5)return normalOutput;
    if(uMode>1.5&&uMode<2.5)return float4(reflected*coverage,coverage);
    // 与默认剑流共用属性约定：第三通道保存反射率乘该材质的屏幕反射强度，按0～2编码。
    if(uMode>2.5&&uMode<3.5)return float4(float3((uOptics.z-1)/1.5,uShape.w/128,dot(fv,float3(.2126,.7152,.0722))*.5)*coverage,coverage);
    if(uMode>3.5)return float4(throughput*coverage,coverage);
    float3 ray=refract(-v,refrN,1/uOptics.z);
    float2 sampleUV=clamp(input.SceneUV+ray.xy/max(abs(ray.z),.1)*uShape.w*uSceneTexel,uSceneTexel*.5,1-uSceneTexel*.5);
    // 玻璃即使宏观光滑也有微小散射；粗糙度为 0 时折射成了硬拷贝，
    // 弯折处会出现锯齿。给一个最小软化量，不额外增加采样次数。
    float refractionBlur=saturate(.15+rough*.85);
    float2 blur=uSceneTexel*(1.5+rough*3.0);
    float3 scene=tex2D(sceneSampler,sampleUV).rgb;
    scene=lerp(scene,(tex2D(sceneSampler,sampleUV+blur).rgb+tex2D(sceneSampler,sampleUV-blur).rgb)*.5,refractionBlur);
    float3 color=pow(saturate(reflected+pow(max(scene,0),2.2)*throughput),1/2.2);
    return float4(color*coverage,coverage);
}
technique MaterialSurface
{
    pass Surface
    {
        VertexShader=compile vs_3_0 Transform();
        PixelShader=compile ps_3_0 Shade();
    }
}
