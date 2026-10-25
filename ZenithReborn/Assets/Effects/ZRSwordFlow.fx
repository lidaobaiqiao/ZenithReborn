// 项目自行编写的剑流带面材质。坐标直接沿实际剑迹展开，不使用极坐标月牙。
// 慢色场与动态形状分开计算；可选美术纹理只替换各自输入，不改轨迹代码。
float4x4 transformMatrix;
float uTime;
float uOpacity;
float uDissolve;
float uSeed;
float3 uBodyColor;
float3 uEdgeColor;
float uUseColorField;
float uColorFieldIsPalette;
float3 uLightDirection;
float uEnvironmentRotation;
float2 uHeightTexel;
float uMaterialOutputMode;
float uUseMotionMask;
float4 uMaskFrame;
float uDispersion;   // 色散：不同波长折射率略有差异，边缘出彩边
float uSsrStrength;  // 屏幕空间反射强度：>0 时反射近处场景
float uUseZoneMask;       // 1 = 启用四区遮罩
texture uZoneMaskTex;
sampler zoneSampler=sampler_state
{
    Texture=<uZoneMaskTex>;
    MinFilter=Linear;MagFilter=Linear;MipFilter=Linear;
    AddressU=Wrap;AddressV=Clamp;
};
float uEmissionStrength;  // 带面自发光
float uStripeStrength;    // 沿迹条纹强度：连续带面上形成分段间隔感：玻璃只靠透射/反光会读作灰膜，必须自己发光
float4 uDetailA[4];     // x=强度 y=混合(0相加1相乘) z=频率U w=滚动U
float4 uDetailB[4];     // x=频率V y=滚动V z=阈值(只贴刃侧) w=保留
float4 uDetailColor[4]; // 该层染色
float uUseBrush;        // 1 = 用笔刷贴图替换程序色场
float uUseNoise;        // 1 = 用噪声贴图打散边界
texture uBrushTex;
sampler brushSampler=sampler_state
{
    Texture=<uBrushTex>;
    MinFilter=Linear;MagFilter=Linear;MipFilter=Linear;
    AddressU=Wrap;AddressV=Wrap;
};
texture uNoiseTex;
sampler noiseSampler=sampler_state
{
    Texture=<uNoiseTex>;
    MinFilter=Linear;MagFilter=Linear;MipFilter=Linear;
    AddressU=Wrap;AddressV=Wrap;
};

// 美术出的 PBR 贴图，各自带开关；缺图时开关为 0，走程序化
float uUseNormal; float uUseSurface;
float uUseHeight; float uUseEmission; float uUseEnvironment;
#define ZR_MAP_SAMPLER(name) sampler name##Sampler=sampler_state {     Texture=<name>;     MinFilter=Linear;MagFilter=Linear;MipFilter=Linear;     AddressU=Wrap;AddressV=Wrap; };
texture uNormalTex;      ZR_MAP_SAMPLER(uNormalTex)
texture uSurfaceTex;     ZR_MAP_SAMPLER(uSurfaceTex)
texture uHeightTex;      ZR_MAP_SAMPLER(uHeightTex)
texture uEmissionTex;    ZR_MAP_SAMPLER(uEmissionTex)
texture uEnvironmentTex; ZR_MAP_SAMPLER(uEnvironmentTex)
float uIor;
float uRoughness;
float uTransmission;
float uHasScene;
float uOutputRefractionMask;
float uOutputSurface;
float uThickness;
float2 uSceneTexel;
float4 uSceneUvTransform;
texture uScene;
sampler sceneSampler=sampler_state
{
    Texture=<uScene>;
    MinFilter=Linear;MagFilter=Linear;MipFilter=None;
    AddressU=Clamp;AddressV=Clamp;
};

texture uColorField;
sampler colorSampler=sampler_state
{
    Texture=<uColorField>;
    MinFilter=Linear;MagFilter=Linear;MipFilter=Linear;
    AddressU=Wrap;AddressV=Clamp;
};
texture uMotionMask;
sampler maskSampler=sampler_state
{
    Texture=<uMotionMask>;
    MinFilter=Linear;MagFilter=Linear;MipFilter=Linear;
    AddressU=Clamp;AddressV=Clamp;
};

struct VertexInput
{
    float4 Position:POSITION0;
    float4 Color:COLOR0;
    float2 UV:TEXCOORD0;
};
struct PixelInput
{
    float4 Position:POSITION0;
    float4 Color:COLOR0;
    float2 UV:TEXCOORD0;
    float2 SceneUV:TEXCOORD1;
};
PixelInput Transform(VertexInput input)
{
    PixelInput output;
    output.Position=mul(input.Position,transformMatrix);
    output.Color=input.Color;
    output.UV=input.UV;
    output.SceneUV=(output.Position.xy/output.Position.w*float2(0.5,-0.5)+0.5)*uSceneUvTransform.zw+uSceneUvTransform.xy;
    return output;
}

// 常规格点值噪声：只用数学运算，不采样第三方噪声或笔刷。
float GridValue(float2 cell)
{
    float phase=cell.x*47.17+cell.y*113.81+uSeed*19.37;
    return frac(sin(phase)*17381.73);
}
float Field(float2 position)
{
    float2 cell=floor(position);
    float2 fraction=frac(position);
    float2 blend=fraction*fraction*(3.0-2.0*fraction);
    float bottom=lerp(GridValue(cell),GridValue(cell+float2(1,0)),blend.x);
    float top=lerp(GridValue(cell+float2(0,1)),GridValue(cell+float2(1,1)),blend.x);
    return lerp(bottom,top,blend.y);
}

float4 Shade(PixelInput input):COLOR0
{
    // s=头到尾的路程坐标；h=亮刃到暗侧的宽度坐标。边界外留有羽化区域。
    float s=saturate(input.UV.x);
    float h=input.UV.y;
    float depth=saturate(h);
    float slowTime=uTime*0.065;
    float maskTime=uTime*0.70;

    // 底色缓慢流动，局部亮度变化不会改变形状的透明度。
    float field=Field(float2(s*1.6-slowTime,depth*2.0+uSeed));
    // 有笔刷图就用笔刷替换程序色场：带手工笔触的表面细节
    // 美术色场：大块云纹，替换程序色场的低频部分
    if(uUseColorField>0.5)
    {
        float art=tex2D(colorSampler,float2(s*0.9-slowTime*0.25,depth*0.8)).r;
        field=lerp(field,art,uUseColorField*0.55);
    }

    float brushSample=0.5;
    if(uUseBrush>0.5)
    {
        // depth 只有 0~1（弧宽约30px），直接乘 1.3 会把 512px 的图压成一片灰。
        // 改为只取笔刷图的一小条横向带（0.22），条纹间距才落在肉眼能分辨的尺度上。
        brushSample=tex2D(brushSampler,float2(s*0.8-slowTime*0.35,depth*0.22+0.30)).r;
        field=lerp(field,brushSample,uUseBrush*0.90);
    }

    // 全局参数承接：细节层只改颜色与质感，不动折射/透射的物理量
    float3 layerColor=uBodyColor;
    float layerRough=uRoughness;
    // 表面斑驳：让粗糙度在不同位置不一样
    if(uUseSurface>0.5)
    {
        float patch=tex2D(uSurfaceTexSampler,float2(s*1.1,depth*0.9)).r;
        layerRough=layerRough*(0.55+patch*1.10);
    }
    float layerIor=uIor;
    float layerTrans=uTransmission;

    // 四区遮罩：R刃口 G脊线 B腹部 A磨损。
    // 之前整条带面是同一套材质，刃口和腹部看起来一样；分区后各自有分工。
    if(uUseZoneMask>0.5)
    {
        float4 zone=tex2D(zoneSampler,float2(s,depth));
        // 刃口：提亮、压低粗糙度，读起来像磨过的金属刃
        layerColor=lerp(layerColor,uEdgeColor,zone.r*0.45);
        layerRough=lerp(layerRough,0.04,zone.r*0.60);
        // 脊线：略提亮，保持结构感
        layerColor=lerp(layerColor,uBodyColor*1.25,zone.g*0.35);
        // 腹部：更透更暗，玻璃的纵深
        layerTrans=lerp(layerTrans,0.80,zone.b*0.50);
        layerColor=lerp(layerColor,uBodyColor*0.70,zone.b*0.25);
        // 磨损：粗糙度上升、压暗，缺口处不再反光
        layerRough=lerp(layerRough,0.50,zone.a*0.70);
        layerColor=lerp(layerColor,uBodyColor*0.55,zone.a*0.40);
    }

    float3 baseColor=layerColor*(0.20+field*0.55+0.18*pow(1-depth,0.85));

    // 质感细节层：整片刀身上叠加（拉丝／磨砂／微光斑），不是按位置分区。
    // 每层是程序化噪声：换频率/滚动就能从拉丝变磨砂，不需要额外贴图。
    [unroll]
    for(int di=0;di<4;di++)
    {
        float weight=uDetailA[di].x;
        if(weight<=0.001)continue;
        float2 detailUv=float2(s*uDetailA[di].z-uTime*uDetailA[di].w,
                               depth*uDetailB[di].x-uTime*uDetailB[di].y);
        float n=Field(detailUv+uSeed*7.31+di*13.17);
        // 阈值 >0 时只贴在刃侧：depth 越小越靠剃刀线
        float gate=lerp(1.0,smoothstep(0.55,0.05,depth),uDetailB[di].z);
        float3 tint=uDetailColor[di].rgb;
        float3 contribution=tint*n*weight*gate;
        // blend 0=相加(提亮/发光) 1=相乘(压暗/磨砂)，中间值连续过渡
        float3 added=layerColor+contribution;
        float3 multiplied=layerColor*(1.0-weight*0.55+weight*n*1.15);
        layerColor=lerp(added,multiplied,saturate(uDetailA[di].y));
    }
    baseColor=layerColor*(0.30+field*0.28+0.20*pow(1-depth,0.85));
    float3 artworkColor=tex2D(colorSampler,float2(s-slowTime,depth)).rgb;
    // 默认色场只调制亮度，不能把所有剑染成同一颜色；逐剑显式调色板仍保留原接口。
    layerColor=lerp(layerColor,artworkColor,uUseColorField*uColorFieldIsPalette);

    // 连续的流线横向弯曲，沿尾部缓慢奔流；不是一张固定形状整体平移。
    float bend=sin(s*8.0-maskTime*0.8)*0.028+sin(s*15.0+maskTime*0.4)*0.009;
    // 有噪声图就用它把边界打散，破坏数学正弦的规律感
    if(uUseNoise>0.5)
    {
        float warp=tex2D(noiseSampler,float2(s*3.0-maskTime*0.18,depth*2.2)).r-0.5;
        bend+=warp*0.055*uUseNoise;
    }
    float streak=Field(float2(s*2.4-maskTime*0.8,(depth+bend)*18.0));
    float broad=Field(float2(s*5.0-maskTime*0.35,depth*4.0+uSeed));
    float innerBoundary=0.80+bend+broad*0.15+(streak-0.5)*0.045;
    float sharpBoundary=smoothstep(-0.045,0.012,h);
    float softBoundary=1-smoothstep(innerBoundary-0.09,innerBoundary+0.035,h);
    float shape=sharpBoundary*softBoundary;
    // 旧尾暗侧拉成不同浓度的细缕；仍是一张动态蒙版，不叠第二层光带。
    float trailing=smoothstep(0.18,0.80,s)*smoothstep(0.25,0.75,depth);
    float threads=smoothstep(0.20,0.54,streak+broad*0.12);
    shape*=lerp(1.0,threads,trailing*0.55);
    float2 maskUV=uMaskFrame.xy+float2(s,depth)*uMaskFrame.zw;
    float artworkMask=tex2D(maskSampler,maskUV).a*step(0,h)*(1-step(1.001,h));
    shape=lerp(shape,artworkMask,uUseMotionMask);

    // 红通道是归一化真实轨迹年龄；与笔刷平铺坐标分离，短轨迹也能完整收尽。
    float grain=Field(float2(s*7.0-maskTime*0.22,depth*9.0+uSeed*2.0));
    float resistance=0.12+(1-input.Color.r)*0.76+(1-depth)*0.10+(grain-0.5)*0.09;
    float wear=uDissolve*1.12;
    float endingAlive=1-smoothstep(resistance-0.055,resistance+0.055,wear);
    // 活动期保留原先的少量旧尾断纹；结束期才用连续推进的回收前沿替换。
    float originalResistance=0.24+grain*0.60+(1-depth)*0.24;
    float alive=uDissolve>0?endingAlive:smoothstep(s*s*0.16-0.05,s*s*0.16+0.10,originalResistance);
    float rim=4*endingAlive*(1-endingAlive)*step(0.0001,uDissolve);

    // 曲面法线沿真实轨迹的屏幕基底展开；边缘采用小倒角，不再画三条坐标白线。
    float2 gradient=float2(ddx(h),ddy(h));
    float2 crossAxis=gradient/max(length(gradient),0.00001);
    float2 tangent=float2(-crossAxis.y,crossAxis.x);
    float2 alongGradient=float2(ddx(s),ddy(s));
    tangent*=dot(tangent,alongGradient)<0?-1:1;
    float bevel=-2.1*pow(1-saturate(depth/0.18),3)+2.2*pow(saturate((depth-0.73)/0.20),3);
    float slope=(depth-0.46)*1.7+bevel;
    float alongSlope=0.65+0.25*cos((s-0.35)*3.14159265);
    float2 materialUV=float2(s*1.5-slowTime*0.15,depth);
    float2 micro=float2(0,0);
    if(uUseNormal>0.5)
    {
        // 法线与高度来自同一张标量源图；切线方向必须随剑迹旋转，不能直接当屏幕方向。
        float3 mapNormal=tex2D(uNormalTexSampler,materialUV).rgb*2-1;
        micro+=mapNormal.xy*0.38;
    }
    float heightValue=0.5;
    if(uUseHeight>0.5)
    {
        heightValue=tex2D(uHeightTexSampler,materialUV).r;
        float hu=tex2D(uHeightTexSampler,materialUV+float2(uHeightTexel.x,0)).r;
        float hv=tex2D(uHeightTexSampler,materialUV+float2(0,uHeightTexel.y)).r;
        micro+=clamp(float2((hu-heightValue)/uHeightTexel.x,(hv-heightValue)/uHeightTexel.y)*0.022,-0.16,0.16);
    }
    float3 normal=normalize(float3(crossAxis*(slope+micro.y)+tangent*(alongSlope+micro.x),1));
    float3 view=float3(0,0,1);
    float3 light=normalize(uLightDirection);
    float3 halfway=normalize(view+light);
    float nv=max(0.025,dot(normal,view)),nl=saturate(dot(normal,light));
    float nh=saturate(dot(normal,halfway)),vh=saturate(dot(view,halfway));
    float f0=pow((layerIor-1)/(layerIor+1),2);
    float fresnel=f0+(1-f0)*pow(1-nv,5);
    float lightF=f0+(1-f0)*pow(1-vh,5);
    // 有限像素下给微表面分布一个最小面积，避免锐高光落在亚像素上闪烁。
    float rough=clamp(layerRough,0.025,1);
    float alpha=max(0.025,rough*rough),a2=alpha*alpha;
    float denominator=nh*nh*(a2-1)+1;
    float distribution=a2/max(0.00001,3.14159265*denominator*denominator);
    float k=(rough+1)*(rough+1)*0.125;
    float visibility=nv/(nv*(1-k)+k)*nl/(nl*(1-k)+k);
    float specular=distribution*visibility*lightF/max(0.001,4*nv*max(nl,0.001))*nl;
    float3 reflectedDirection=reflect(-view,normal);
    float2 envUV=float2(atan2(reflectedDirection.y,reflectedDirection.x)/6.2831853+0.5+uEnvironmentRotation,
                        acos(clamp(reflectedDirection.z,-1,1))/3.14159265);
    float2 envSpread=float2(rough*0.12,rough*0.08);
    float3 env=tex2D(uEnvironmentTexSampler,envUV).rgb;
    env=lerp(env,(tex2D(uEnvironmentTexSampler,envUV+envSpread).rgb+tex2D(uEnvironmentTexSampler,envUV-envSpread).rgb)*0.5,rough);
    env=lerp(float3(0.18,0.22,0.28),pow(max(env,0),2.2)*1.8,uUseEnvironment);
    float3 reflectionTint=lerp(float3(1,1,1),saturate(layerColor),0.22);
    float3 reflected=(env*fresnel+float3(1.0,0.98,0.93)*specular*2.2)*reflectionTint;
    // 少量内部流丝维持能量色；不再让整片自发光压过玻璃的背景对比。
    float filaments=smoothstep(0.42,0.78,streak)*(1-depth);
    float emission=tex2D(uEmissionTexSampler,materialUV).r*uUseEmission;
    reflected+=layerColor*(0.012+filaments*0.018+emission*0.025)*uEmissionStrength;
    reflected+=lerp(uBodyColor,uEdgeColor,0.32)*rim*uDissolve*0.06;
    reflected=reflected/(1+reflected);
    float localThickness=uThickness*clamp(1+(heightValue-0.5)*2,0.85,1.15);
    float3 absorption=exp(-(1-saturate(layerColor))*0.26*(1-layerTrans)*localThickness/72);
    float3 throughput=absorption*exp(-(1-layerTrans)*0.35)*(1-fresnel);
    // 覆盖度维持实体带面；通透来自被偏折的背景，不是把整个效果变透明。
    float glassAlpha=0.66+field*0.12+(1-depth)*0.05+pow(1-depth,6)*0.10;
    float opacity=shape*alive*uOpacity*input.Color.a*saturate(glassAlpha);
    float4 normalOutput=float4((normal.xy*0.5+0.5)*opacity,rough*opacity,opacity);
    if(uOutputRefractionMask>0.5)return normalOutput;
    if(uOutputSurface>0.5)return float4(reflected*opacity,fresnel*opacity);
    // 蓝通道低区间标识剑流，并携带屏幕反射强度；高区间属于通用表面材质。
    if(uMaterialOutputMode>2.5&&uMaterialOutputMode<3.5)
        return float4(float3((layerIor-1)/1.5,localThickness/128,uSsrStrength*0.20)*opacity,opacity);
    if(uMaterialOutputMode>3.5)return float4(throughput*opacity,opacity);
    // 安全备用与离线预览也使用同一法线、反射及透射数据。
    float dispersion=uDispersion*0.022;
    float3 rayR=refract(-view,normal,1/(layerIor*(1-dispersion)));
    float3 rayG=refract(-view,normal,1/layerIor);
    float3 rayB=refract(-view,normal,1/(layerIor*(1+dispersion)));
    float2 offsetR=rayR.xy/max(abs(rayR.z),0.1)*localThickness*uSceneTexel;
    float2 offsetG=rayG.xy/max(abs(rayG.z),0.1)*localThickness*uSceneTexel;
    float2 offsetB=rayB.xy/max(abs(rayB.z),0.1)*localThickness*uSceneTexel;
    float2 low=uSceneTexel*0.5,high=1-low;
    float3 scene=float3(tex2D(sceneSampler,clamp(input.SceneUV+offsetR,low,high)).r,
                        tex2D(sceneSampler,clamp(input.SceneUV+offsetG,low,high)).g,
                        tex2D(sceneSampler,clamp(input.SceneUV+offsetB,low,high)).b);
    float3 color=pow(saturate(reflected+pow(max(scene,0),2.2)*throughput*uHasScene),1/2.2);
    return float4(color*opacity,opacity);
}

technique SwordFlow
{
    pass Surface
    {
        VertexShader=compile vs_3_0 Transform();
        PixelShader=compile ps_3_0 Shade();
    }
}
