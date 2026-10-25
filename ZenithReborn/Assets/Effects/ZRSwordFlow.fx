// 项目自行编写的剑流带面材质。坐标直接沿实际剑迹展开，不使用极坐标月牙。
// 慢色场与动态形状分开计算；可选美术纹理只替换各自输入，不改轨迹代码。
float4x4 transformMatrix;
float uTime;
float uOpacity;
float uDissolve;
float uSeed;
float3 uBodyColor;
float3 uEdgeColor;
float3 uLightDirection;
float uUseColorField;
float uUseMotionMask;
float4 uMaskFrame;
float uDispersion;   // 色散：不同波长折射率略有差异，边缘出彩边
float uSsrStrength;  // 屏幕空间反射强度：>0 时反射近处场景
float uUseZoneMask;       // 1 = 启用四区遮罩
float uMapAspectScale;    // 贴图宽长比修正倍率：1 = 按真实几何自动算；美术图本该更扁时调小
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
    // ---- 贴图采样比例修正 ----
    // 带面是长条：沿迹几百像素，横向只有约 30 像素；而材质贴图是 1024×1024 方形。
    // 过去所有贴图都按 (s, depth) 直接采样，等于把正方形图纵向压进 30 像素，
    // 1024 行挤成一条，出来必然是横条纹和不成形的色块。
    // 这里用屏幕导数测出两个方向每像素对应的 UV 变化量，反推出真实"宽:长"，
    // 再把纵向采样范围压回这个比例，两个方向的贴图像素密度就一致了。
    float uvStepX=max(length(float2(ddx(input.UV.x),ddy(input.UV.x))),1e-7);  // ≈ 1 / 沿迹长度(px)
    float uvStepY=max(length(float2(ddx(input.UV.y),ddy(input.UV.y))),1e-7);  // ≈ 1 / 横向宽度(px)
    float aspectScale=(uMapAspectScale>0.0)?uMapAspectScale:1.0;
    float mapAspect=clamp((uvStepX/max(uvStepY,1e-7))*aspectScale,0.002,1.0);
    // 贴图纵向采样坐标：depth 本来是 0~1，乘上宽长比才落在真实比例上。
    float mapDepth=depth*mapAspect;
    float slowTime=uTime*0.065;
    float maskTime=uTime*0.70;

    // 底色缓慢流动，局部亮度变化不会改变形状的透明度。
    float field=Field(float2(s*1.6-slowTime,depth*2.0+uSeed));
    // 有笔刷图就用笔刷替换程序色场：带手工笔触的表面细节
    // 美术色场：大块云纹，替换程序色场的低频部分
    if(uUseColorField>0.5)
    {
        float art=tex2D(colorSampler,float2(s*0.9-slowTime*0.25,mapDepth*0.8)).r;
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
        float patch=tex2D(uSurfaceTexSampler,float2(s*1.1,mapDepth*0.9)).r;
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
    baseColor=lerp(baseColor,artworkColor,uUseColorField);

    // 连续的流线横向弯曲，沿尾部缓慢奔流；不是一张固定形状整体平移。
    float bend=sin(s*8.0-maskTime*0.8)*0.028+sin(s*15.0+maskTime*0.4)*0.009;
    // 有噪声图就用它把边界打散，破坏数学正弦的规律感
    if(uUseNoise>0.5)
    {
        float warp=tex2D(noiseSampler,float2(s*3.0-maskTime*0.18,mapDepth*2.2)).r-0.5;
        bend+=warp*0.055*uUseNoise;
    }
    float streak=Field(float2(s*2.4-maskTime*0.8,(depth+bend)*18.0));
    float broad=Field(float2(s*5.0-maskTime*0.35,depth*4.0+uSeed));
    float innerBoundary=0.80+bend+broad*0.15+(streak-0.5)*0.045;
    float sharpBoundary=smoothstep(-0.045,0.012,h);
    // 内侧过渡宽度也随低频噪声起伏，避免整条边界宽窄完全一致而显得生硬。
    // 与高光共用同一张图的不同坐标；没有噪声图时退化为原来的固定宽度。
    float edgeNoise=uUseNoise>0.5?(tex2D(noiseSampler,float2(s*0.6,depth*1.4)).r-0.5):0.0;
    float softBoundary=1-smoothstep(innerBoundary-0.09-0.03*edgeNoise,
        innerBoundary+0.035+0.03*edgeNoise,h);
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

    // 低粗糙度控制反光宽度，折射率同时参与折射方向与菲涅耳反射。
    float highlightWidth=0.008+layerRough*0.14;
    // 刃线分三层：窄核心负责"锐"，中层光晕往外化，最外层弥散融进主体。
    // 单层硬线会和带面割裂，看起来像贴上去的一条白边。
    float razorCore=(1.0-smoothstep(0.004,0.018,abs(h)))*sharpBoundary;
    float razorHalo=(1.0-smoothstep(0.016,0.085,abs(h)))*sharpBoundary;
    float razorMist=(1.0-smoothstep(0.045,0.200,abs(h)))*sharpBoundary;
    float razor=razorCore;
    // 中间白边同样拆三层，和主体的过渡才不会一刀切
    float innerRimCore=1-smoothstep(0.004,0.016,abs(h-innerBoundary+0.025));
    float innerRimHalo=1-smoothstep(0.014,0.055,abs(h-innerBoundary+0.025));
    float innerRimMist=1-smoothstep(0.040,0.140,abs(h-innerBoundary+0.025));
    float innerRim=innerRimCore;
    float reflectionAt=0.18+s*0.20+sin(uTime*0.38)*0.16;
    float reflection=1-smoothstep(0.004,highlightWidth,abs(depth-reflectionAt));
    reflection*=0.55+0.45*pow(saturate(1-s),0.45);
    float fineReflection=(1-smoothstep(0.004,0.014,abs(depth-0.57+s*0.12)))*0.22;
    float filaments=smoothstep(0.42,0.78,streak)*(1-depth)*0.13;
    float3 neutralHighlight=lerp(uEdgeColor,float3(1.15,1.18,1.22),0.55);
    // 用宽度坐标的屏幕梯度确定局部表面朝向，曲线转弯时折射方向也随之旋转。
    float2 gradient=float2(ddx(h),ddy(h));
    float2 crossAxis=gradient/max(length(gradient),0.00001);
    float2 tangent=float2(-crossAxis.y,crossAxis.x);
    float2 alongGradient=float2(ddx(s),ddy(s));
    tangent*=dot(tangent,alongGradient)<0?-1:1;
    // 稳定的双轴弧面：沿宽度弯曲，沿剑迹保持倾角；不使用滚动噪声扭曲背景。
    // 只有横向曲率会顺着竖直树干挪图，沿迹倾角让树干也出现可辨的横向偏折。
    float slope=(depth-0.46)*1.7;
    float alongSlope=0.65+0.25*cos((s-0.35)*3.14159265);
    float3 normal=normalize(float3(crossAxis*slope+tangent*alongSlope,1));
    // 法线贴图：把美术的微观凹凸加到表面法线上（只改方向，不改强度上限）
    if(uUseNormal>0.5)
    {
        float3 nrm=tex2D(uNormalTexSampler,float2(s*2.2,mapDepth*1.6)).rgb*2.0-1.0;
        normal=normalize(normal+float3(nrm.xy*0.55,0));
    }
    // 色散：红绿蓝用略微不同的折射率，法线越斜（越靠带面边缘）分色越明显
    float disp=uDispersion*0.055;
    float3 tr=refract(float3(0,0,-1),normal,1.0/(layerIor*(1.0-disp)));
    float3 tg=refract(float3(0,0,-1),normal,1.0/layerIor);
    float3 tb=refract(float3(0,0,-1),normal,1.0/(layerIor*(1.0+disp)));
    float2 ur=tr.xy/max(abs(tr.z),0.1)*uThickness*uSceneTexel;
    float2 ug=tg.xy/max(abs(tg.z),0.1)*uThickness*uSceneTexel;
    float2 ub=tb.xy/max(abs(tb.z),0.1)*uThickness*uSceneTexel;
    float2 sceneUV=clamp(input.SceneUV+ug,uSceneTexel*0.5,1-uSceneTexel*0.5);
    float3 scene=float3(
        tex2D(sceneSampler,clamp(input.SceneUV+ur,uSceneTexel*0.5,1-uSceneTexel*0.5)).r,
        tex2D(sceneSampler,sceneUV).g,
        tex2D(sceneSampler,clamp(input.SceneUV+ub,uSceneTexel*0.5,1-uSceneTexel*0.5)).b);
    float2 blurAxis=crossAxis*uSceneTexel*layerRough*2.0;
    scene=lerp(scene,(tex2D(sceneSampler,sceneUV+blurAxis).rgb+tex2D(sceneSampler,sceneUV-blurAxis).rgb)*0.5,layerRough);
    float f0=pow((layerIor-1)/(layerIor+1),2);
    float fresnel=f0+(1-f0)*pow(1-saturate(normal.z),5);
    // 屏幕空间反射：带面是平面，用反射向量的屏幕分量取一次近处场景即可，
    // 比环境图更有"映出周围"的感觉（树、敌人、其他剑）
    float3 reflectDir=reflect(float3(0,0,-1),normal);
    float2 ssrUv=clamp(input.SceneUV+reflectDir.xy*uSceneTexel*34.0,
        uSceneTexel*0.5,1-uSceneTexel*0.5);
    float3 nearScene=tex2D(sceneSampler,ssrUv).rgb;
    // 环境图：远方冷色/暖色，补上 SSR 采不到的方向
    if(uUseEnvironment>0.5)
    {
        float2 envUv=float2(atan2(reflectDir.y,reflectDir.x)/6.2831853+0.5,depth*0.6);
        float3 env=tex2D(uEnvironmentTexSampler,envUv).rgb;
        nearScene=lerp(nearScene,env,0.45*uUseEnvironment);
    }
    // 主体本身保持中性透光。剑色只进入连续高光，不再给整片带面铺底色或灰白反射图。
    // 高光法线增加小倒角，折射仍使用79原法线，避免同时改变背景的扭曲方式。
    float bevel=-1.4*pow(1-saturate(depth/0.15),3);
    float3 highlightNormal=normalize(float3(crossAxis*(slope+bevel)+tangent*alongSlope,1));
    float3 view=float3(0,0,1);
    float3 light=normalize(uLightDirection);
    float3 halfway=normalize(view+light);
    float nv=max(0.025,dot(highlightNormal,view));
    float nl=saturate(dot(highlightNormal,light));
    float nh=saturate(dot(highlightNormal,halfway));
    float vh=saturate(dot(view,halfway));
    float lightF=f0+(1-f0)*pow(1-vh,5);
    float rough=clamp(layerRough,0.025,1);
    float alpha=max(0.035,rough*rough),a2=alpha*alpha;
    float denominator=nh*nh*(a2-1)+1;
    float distribution=a2/max(0.00001,3.14159265*denominator*denominator);
    float k=(rough+1)*(rough+1)*0.125;
    float visibility=nv/(nv*(1-k)+k)*nl/(nl*(1-k)+k);
    float specular=distribution*visibility*lightF/max(0.001,4*nv*max(nl,0.001))*nl;
    // 有限面积光源的柔和包络与窄核共用同一受光方向，形成一块连续的有色高光。
    float softLobe=pow(nh,12)*0.22;
    float lightCore=1-exp(-specular*2.2);
    // 高光分布图：方向和位置由法线、光照这些数学量决定，浓淡交给低频噪声图。
    // U 取小系数把云斑沿剑迹拉长以贴合长条刀光，时间项让它缓慢流动。
    // 与边界扰动共用同一张无缝噪声，只是采样坐标不同；没有噪声图时不调制。
    float highlightMap=uUseNoise>0.5?
        lerp(0.35,1.0,tex2D(noiseSampler,float2(s*0.6-uTime*0.05,depth*1.4)).r):1.0;
    float highlight=(softLobe+lightCore*0.65)*highlightMap;
    float3 highlightTint=saturate(layerColor);
    float3 reflected=highlightTint*highlight;
    reflected+=lerp(highlightTint,float3(1,1,1),0.62)*lightCore*0.40;
    // 保留79的薄亮刃，光晕只从刃核向内衰减；撤掉横穿主体的两条坐标白线。
    reflected+=neutralHighlight*(razorCore*0.70+razorHalo*0.10+razorMist*0.018);
    reflected+=lerp(uBodyColor,uEdgeColor,0.32)*rim*uDissolve*0.10;
    float throughput=exp(-(1-layerTrans)*0.015)*(1-fresnel);
    float3 color=pow(saturate(reflected+pow(max(scene,0),2.2)*throughput*uHasScene),1/2.2);
    // 主体恢复可见覆盖度，透射通过真实背景采样体现，不再靠大幅降低透明度代替。
    float glassAlpha=0.66+field*0.12+(1-depth)*0.06+razor*0.18+reflection*0.12+innerRim*0.08;
    float opacity=shape*alive*uOpacity*input.Color.a*saturate(glassAlpha);
    // 在此统一预乘；透明处不留下独立白光，绘制使用AlphaBlend。
    float4 shaded=float4(color*opacity,opacity);
    // 保存表面法线而非已算好的偏移，最终合成中的IOR直接决定背景弯折。
    float4 refractionMask=float4((normal.xy*0.5+0.5)*opacity,uRoughness*opacity,opacity);
    // RGB保存高光的线性光能，A只记录菲涅耳损失；高光以外仍透射完整偏折背景。
    float4 surfaceMask=float4(reflected*opacity,fresnel*opacity);
    return lerp(lerp(shaded,refractionMask,uOutputRefractionMask),surfaceMask,uOutputSurface);
}

technique SwordFlow
{
    pass Surface
    {
        VertexShader=compile vs_3_0 Transform();
        PixelShader=compile ps_3_0 Shade();
    }
}
