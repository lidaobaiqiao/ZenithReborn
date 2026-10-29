// 项目自行编写的剑流带面材质。坐标直接沿实际剑迹展开，不使用极坐标月牙。
// 慢色场与动态形状分开计算；可选美术纹理只替换各自输入，不改轨迹代码。
float4x4 transformMatrix;
float uTime;
float uOpacity;
float uBodyOpacity; // 主体密度：不参与覆盖度，也不压低玻璃反射与亮刃
float uDissolve;
float uSeed;
float3 uBodyColor;
float3 uEdgeColor;
float3 uShadeColor;
float3 uMidColor;
float3 uHighlightColor;
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
float uStripeStrength;    // 内部色流强度，受主体密度限制；不承担整片表面发光
float4 uDetailA[4];     // x=强度 y=混合(0相加1相乘) z=频率U w=滚动U
float4 uDetailB[4];     // x=频率V y=滚动V z=阈值(只贴刃侧) w=保留
float4 uDetailColor[4]; // 该层染色
float uUseBrush;        // 1 = 用笔刷贴图替换程序色场
float uUseNoise;        // 1 = 用噪声贴图打散边界

// ---- 逐项测试开关：P01~P25，每项独立。1 = 生效，0 = 从画面摘除。----
// 全部为 1 时与生产形态完全一致（每一项都只是原有表达式乘 1）。
// 正式预设全部开启；隔离夹具可以只保留形状、剑色和覆盖度。
float uPartShape;         // P01 主体形状（关掉整条带面消失）
float uPartBodyTint;      // P14 剑身色铺底
float uPartGlassBase;     // P16 玻璃基底不透明度 0.66
float uPartBend;          // P02 边界正弦弯曲
float uPartThreads;       // P03 尾部拉丝细缕
float uPartDepthGradient; // P04 横向明暗梯度（刃侧亮、腹部暗）
float uPartProcField;     // P05 程序色场低频噪声
float uPartDissolve;      // P06 消融/回收（0 = 整段常亮不收尾）
float uPartRim;           // P07 消融前沿发光
float uPartCurvature;     // P08 双轴曲面法线
float uPartBevel;         // P09 刃口倒角
float uPartRazorCore;     // P10 刃核白线
float uPartRazorHalo;     // P11 刃线中层光晕
float uPartRazorMist;     // P12 刃线外层弥散
float uPartInnerRim;      // P13 内缘线
float uPartBandReflection;// P15 沿迹带状反射
float uPartGgxCore;       // P17 GGX 高光核
float uPartSoftLobe;      // P18 宽高光包络
float uPartWhiteShift;    // P19 高光亮色过渡（保留旧参数名以兼容逐项测试）
float uPartHighlightNoise;// P20 噪声调制高光
float uPartDispersion;    // P21 色散
float uPartSceneTransmission; // P22 背景透射
float uPartSsr;           // P23 屏幕空间反射
float uPartStripe;        // P24 虚拟光面强度
float uPartEmission;      // P25 虚拟光源亮度
float uPartRidges;        // P26 液态玻璃：最低基面上的圆滑条带起伏（只驱动折射方向）
float uRidgeStrength;     // 条带峰值坡度倍率：1 = 约 45° 起伏
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
float uMetallic;
float uTransmission;
float uHasScene;
float uOutputRefractionMask;
float uOutputSurface;
float uMaterialOutput;

#include "ZRClearGlass.fxh"
#include "ZRSwordHighlights.fxh"
#include "ZRLiquidGlass.fxh"
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
    float2 AcrossGradient:TEXCOORD1;
    float2 AlongGradient:TEXCOORD2;
};
struct PixelInput
{
    float4 Position:POSITION0;
    float4 Color:COLOR0;
    float2 UV:TEXCOORD0;
    float2 SceneUV:TEXCOORD1;
    float2 AcrossGradient:TEXCOORD2;
    float2 AlongGradient:TEXCOORD3;
};
PixelInput Transform(VertexInput input)
{
    PixelInput output;
    output.Position=mul(input.Position,transformMatrix);
    output.Color=input.Color;
    output.UV=input.UV;
    output.AcrossGradient=input.AcrossGradient;
    output.AlongGradient=input.AlongGradient;
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

// 条带几何定义见 ZRLiquidGlass.fxh（与 ZRMaterialSurface.fx 共用同一份）。

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
    // 美术色场：大块云纹，替换程序色场的低频部分
    if(uUseColorField>0.5)
    {
        float art=tex2D(colorSampler,float2(s*0.9-slowTime*0.25,mapDepth*0.8)).r;
        field=lerp(field,art,uUseColorField*0.55);
    }

    // 全局参数承接：细节层只改颜色与质感，不动折射/透射的物理量
    float3 layerColor=uBodyColor*uPartBodyTint;
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
    // 明确指定零粗糙度时，分区磨损也不能把它重新抬高；其余分区着色继续保留。
    if(uRoughness<=0)layerRough=0;

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
    // 色场必须写回 layerColor：后面的高光染色与最终颜色都从它取值。
    // 采样放在开关内部：关掉时连贴图都不读，避免"先采样再乘 0"的假隔离。
    if(uUseColorField>0.5)
    {
        float3 artworkColor=tex2D(colorSampler,float2(s-slowTime,depth)).rgb;
        layerColor=lerp(layerColor,artworkColor,uUseColorField);
    }

    // 连续的流线横向弯曲，沿尾部缓慢奔流；不是一张固定形状整体平移。
    float bend=(sin(s*8.0-maskTime*0.8)*0.028+sin(s*15.0+maskTime*0.4)*0.009)*uPartBend;
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
    // 三元运算符在 HLSL 里可能两支都求值，所以这里改成显式分支，关掉时零采样。
    float edgeNoise=0.0;
    if(uUseNoise>0.5)
        edgeNoise=tex2D(noiseSampler,float2(s*0.6,depth*1.4)).r-0.5;
    float softBoundary=1-smoothstep(innerBoundary-0.09-0.03*edgeNoise,
        innerBoundary+0.035+0.03*edgeNoise,h);
    float shape=sharpBoundary*softBoundary;
    // 旧尾暗侧拉成不同浓度的细缕；仍是一张动态蒙版，不叠第二层光带。
    float trailing=smoothstep(0.18,0.80,s)*smoothstep(0.25,0.75,depth);
    float threads=smoothstep(0.20,0.54,streak+broad*0.12);
    shape*=lerp(1.0,threads,trailing*0.55*uPartThreads);
    // 遮罩采样同样放进开关内部：关掉时零贴图读取。
    if(uUseMotionMask>0.5)
    {
        float2 maskUV=uMaskFrame.xy+float2(s,depth)*uMaskFrame.zw;
        float artworkMask=tex2D(maskSampler,maskUV).a*step(0,h)*(1-step(1.001,h));
        shape=lerp(shape,artworkMask,uUseMotionMask);
    }

    // 红通道是归一化真实轨迹年龄；与笔刷平铺坐标分离，短轨迹也能完整收尽。
    float grain=Field(float2(s*7.0-maskTime*0.22,depth*9.0+uSeed*2.0));
    float resistance=0.12+(1-input.Color.r)*0.76+(1-depth)*0.10+(grain-0.5)*0.09;
    float wear=uDissolve*1.12;
    float endingAlive=1-smoothstep(resistance-0.055,resistance+0.055,wear);
    // 活动期保留原先的少量旧尾断纹；结束期才用连续推进的回收前沿替换。
    float originalResistance=0.24+grain*0.60+(1-depth)*0.24;
    float alive=uDissolve>0?endingAlive:smoothstep(s*s*0.16-0.05,s*s*0.16+0.10,originalResistance);
    // 消融开关关掉时整段保持满覆盖，便于单独观察形状与明暗。
    alive=lerp(1.0,alive,uPartDissolve);
    float rim=4*endingAlive*(1-endingAlive)*step(0.0001,uDissolve);

    // 刃线分三层：窄核心负责"锐"，中层光晕往外化，最外层弥散融进主体。
    // 单层硬线会和带面割裂，看起来像贴上去的一条白边。
    float razorCore=(1.0-smoothstep(0.004,0.018,abs(h)))*sharpBoundary;
    float razorHalo=(1.0-smoothstep(0.016,0.085,abs(h)))*sharpBoundary;
    float razorMist=(1.0-smoothstep(0.045,0.200,abs(h)))*sharpBoundary;
    float razor=razorCore*uPartRazorCore;
    // 中间白边同样拆三层，和主体的过渡才不会一刀切
    float innerRimCore=1-smoothstep(0.004,0.016,abs(h-innerBoundary+0.025));
    float innerRimHalo=1-smoothstep(0.014,0.055,abs(h-innerBoundary+0.025));
    float innerRimMist=1-smoothstep(0.040,0.140,abs(h-innerBoundary+0.025));
    float innerRim=innerRimCore;
    float3 edgeHighlight=pow(saturate(uEdgeColor),2.2);
    // 连续朝向在共享顶点处汇总后插值；不能直接用逐三角形的ddx/ddy制造硬面。
    float2 gradient=input.AcrossGradient;
    float2 crossAxis=gradient/max(length(gradient),0.00001);
    float2 tangent=float2(-crossAxis.y,crossAxis.x);
    float2 alongGradient=input.AlongGradient;
    tangent*=dot(tangent,alongGradient)<0?-1:1;
    // 稳定的双轴弧面：沿宽度弯曲，沿剑迹保持倾角；不使用滚动噪声扭曲背景。
    // 只有横向曲率会顺着竖直树干挪图，沿迹倾角让树干也出现可辨的横向偏折。
    float bevel=-1.4*pow(1-saturate(depth/0.15),3)*uPartBevel;
    float slope=(depth-0.46)*1.7+bevel;
    // 两个方向均连续弯曲，使局部环境反射由曲面朝向决定，而不是固定UV遮罩。
    float alongSlope=0.65+0.25*cos((s-0.35)*3.14159265)+4.0*(s-.5);
    float3 normal=normalize(float3((crossAxis*slope+tangent*alongSlope)*uPartCurvature,1));
    // ---- P26 液态玻璃：在最低基面之上叠加圆滑条带起伏 ----
    // 基面就是上面那个双轴弧面（平整玻璃）。条带把局部顶起来，法线随之倾斜，
    // 折射方向也跟着变——背景是被曲面弯折的，不是贴图伪造的。
    // 只驱动折射方向：菲涅耳、高光和屏幕反射仍走基面法线，便于单独验证折射形态。
    float3 refrNormal=normal;
    if(uPartRidges>0.5)
    {
        float2 ridge=ZRGlassRidges(s,depth,uTime)*uRidgeStrength;
        // 基面部分受 P08 控制（关掉即平整玻璃），条带部分不受 P08 影响；
        // 两者在同一个法线里相加，所以平整基面上照样能长出液态起伏。
        refrNormal=normalize(float3(crossAxis*(slope*uPartCurvature+ridge.y)
            +tangent*(alongSlope*uPartCurvature+ridge.x),1));
    }
    // 法线贴图：把美术的微观凹凸加到表面法线上（只改方向，不改强度上限）
    if(uUseNormal>0.5)
    {
        float3 nrm=tex2D(uNormalTexSampler,float2(s*2.2,mapDepth*1.6)).rgb*2.0-1.0;
        normal=normalize(normal+float3(nrm.xy*0.55,0));
    }
    // 高度图：用相邻采样的梯度再加一层更细的起伏。与法线贴图分工——
    // 法线是美术画的凹凸朝向，高度是真实起伏的斜率，两者可叠加。
    // 此前 uUseHeight 声明后从未被读取，这张图一直是死资产。
    if(uUseHeight>0.5)
    {
        float2 hUv=float2(s*2.2,mapDepth*1.6);
        float texel=1.0/512.0;
        float hx=tex2D(uHeightTexSampler,hUv+float2(texel,0)).r
               -tex2D(uHeightTexSampler,hUv-float2(texel,0)).r;
        float hy=tex2D(uHeightTexSampler,hUv+float2(0,texel)).r
               -tex2D(uHeightTexSampler,hUv-float2(0,texel)).r;
        normal=normalize(normal+float3(-hx,-hy,0)*1.5);
    }
    // 几何覆盖先独立输出，不通过颜色层的lerp回算，避免体色运算污染法线缓冲的量化结果。
    float glassAlpha=0.66*uPartGlassBase+field*0.12*uPartProcField+(1-depth)*0.06*uPartDepthGradient
        +razor*0.18+innerRim*uPartInnerRim*0.08;
    float opacity=shape*uPartShape*alive*uOpacity*input.Color.a*saturate(glassAlpha);
    if(uOutputRefractionMask>0.5)
        return float4((refrNormal.xy*0.5+0.5)*opacity,uRoughness*opacity,opacity);

    // 色散：红绿蓝用略微不同的折射率，法线越斜（越靠带面边缘）分色越明显
    float disp=uDispersion*0.055*uPartDispersion;
    float3 tr=refract(float3(0,0,-1),refrNormal,1.0/(layerIor*(1.0-disp)));
    float3 tg=refract(float3(0,0,-1),refrNormal,1.0/layerIor);
    float3 tb=refract(float3(0,0,-1),refrNormal,1.0/(layerIor*(1.0+disp)));
    float2 ur=tr.xy/max(abs(tr.z),0.1)*uThickness*uSceneTexel;
    float2 ug=tg.xy/max(abs(tg.z),0.1)*uThickness*uSceneTexel;
    float2 ub=tb.xy/max(abs(tb.z),0.1)*uThickness*uSceneTexel;
    float2 sceneUV=clamp(input.SceneUV+ug,uSceneTexel*0.5,1-uSceneTexel*0.5);
    // 场景采样只在真的要用时发生：背景透射或屏幕空间反射至少开一项，否则一次也不读。
    float3 reflectDir=reflect(float3(0,0,-1),normal);
    float2 ssrUv=clamp(input.SceneUV+reflectDir.xy*uSceneTexel*34.0,
        uSceneTexel*0.5,1-uSceneTexel*0.5);
    float3 scene=float3(0,0,0);
    float3 nearScene=float3(0,0,0);
    // 没有真实背景时不能把白色占位图当成反射；原生屏幕路径在最终合成中另采真实当前帧。
    if(uHasScene>0.5&&(uPartSceneTransmission>0.5||uPartSsr>0.5))
    {
        scene=float3(
            tex2D(sceneSampler,clamp(input.SceneUV+ur,uSceneTexel*0.5,1-uSceneTexel*0.5)).r,
            tex2D(sceneSampler,sceneUV).g,
            tex2D(sceneSampler,clamp(input.SceneUV+ub,uSceneTexel*0.5,1-uSceneTexel*0.5)).b);
        // 玻璃即使宏观光滑也有微小散射；粗糙度为 0 时折射成了硬拷贝，
        // 背景边缘锐利得像透镜或塑料片，弯折处还会出现锯齿。
        // 这里给一个最小软化量，半径固定、权重随粗糙度上升，不额外增加采样次数。
        float refractionBlur=saturate(0.15+layerRough*0.85);
        float2 blurAxis=crossAxis*uSceneTexel*(1.5+layerRough*3.0);
        scene=lerp(scene,(tex2D(sceneSampler,sceneUV+blurAxis).rgb+tex2D(sceneSampler,sceneUV-blurAxis).rgb)*0.5,refractionBlur);
        if(uPartSsr>0.5)nearScene=tex2D(sceneSampler,ssrUv).rgb;
    }
    // 金属度直接混合介质反射率与剑色反射率，不用提高屏幕反射强度冒充。
    float3 f0=GlassF0(layerIor,uMetallic,layerColor);
    float3 fresnelColor=f0+(1-f0)*pow(1-saturate(normal.z),5);
    float fresnel=dot(fresnelColor,float3(0.2126,0.7152,0.0722));
    float bodyOpacity=saturate(uBodyOpacity);
    float3 throughput=GlassThroughput(layerColor,bodyOpacity,layerTrans,uThickness,normal.z,fresnelColor);
    // 属性与透射输出不需要后续照明采样；尽早返回，避免额外两次提交重复计算全部高光。
    if(uMaterialOutput>2.5&&uMaterialOutput<3.5)
        return float4(float3((layerIor-1)/1.5,uThickness/128,fresnel*uPartSsr*.5)*opacity,opacity);
    if(uMaterialOutput>3.5)return float4(throughput*uPartSceneTransmission*opacity,opacity);
    // 不再把按带面宽度采样的地平线贴图当作环境反射：它会凭空在整个剑迹上画出横线。
    // 原生滤镜读取当前帧的屏幕反射；视野以外的真实环境没有数据，不伪装为真实反射。
    // 主体本身保持中性透光。剑色只进入连续高光，不再给整片带面铺底色或灰白反射图。
    // 折射、菲涅耳和高光共用同一个连续表面法线。
    float3 highlightNormal=normal;
    float3 view=float3(0,0,1);
    // 虚拟环境中的小面光源位于较低仰角，使弯曲薄片能够接到有限面积的倒影。
    // 沿用日月方位变化；这是美术环境光近似，不声称读取了三维世界中的灯具。
    float3 light=normalize(float3(uLightDirection.x*2.6,uLightDirection.y,uLightDirection.z*.35));
    float3 halfway=normalize(view+light);
    float nv=max(0.025,dot(highlightNormal,view));
    float nl=saturate(dot(highlightNormal,light));
    float nh=saturate(dot(highlightNormal,halfway));
    float vh=saturate(dot(view,halfway));
    float3 lightF=f0+(1-f0)*pow(1-vh,5);
    float rough=saturate(layerRough);
    // 只在微表面分布的分母计算中留数值保护，不再把材质粗糙度钳到非零档位。
    float alpha=max(0.0001,rough*rough),a2=alpha*alpha;
    float denominator=nh*nh*(a2-1)+1;
    float distribution=a2/max(0.000000000001,3.14159265*denominator*denominator);
    float k=(rough+1)*(rough+1)*0.125;
    float visibility=nv/(nv*(1-k)+k)*nl/(nl*(1-k)+k);
    float3 specular=distribution*visibility*lightF/max(0.001,4*nv*max(nl,0.001))*nl;
    // 用反射方向与虚拟矩形光源相交形成局部光面；没有命中时允许光面消失。
    float2 reflectionFace=SwordReflectionFace(highlightNormal,light,rough);
    float softLobe=reflectionFace.y*.12;
    float3 lightCore=1-exp(-specular*2.2);
    // 高光分布由连续法线和光照决定，低频噪声只轻微调制，不能刷成明暗云斑。
    // U 取小系数把云斑沿剑迹拉长以贴合长条刀光，时间项让它缓慢流动。
    // 与边界扰动共用同一张无缝噪声，只是采样坐标不同；没有噪声图时不调制。
    float highlightMap=1.0;
    if(uUseNoise>0.5&&uPartHighlightNoise>0.5)
        highlightMap=lerp(0.88,1.0,tex2D(noiseSampler,float2(s*0.6-uTime*0.05,depth*1.4)).r);
    // 12 是虚拟光源辐亮度；菲涅耳损失参与光面能量，不将透明度乘到高光上。
    float coreEnergy=max(dot(lightCore,float3(0.2126,0.7152,0.0722)),reflectionFace.x*fresnel*12);
    float shadePosition=saturate(nl*0.75+coreEnergy*0.25);
    // P19沿剑色的亮部过渡，不再额外叠白色；三个锚点来自同一个剑色调色模块。
    shadePosition=lerp(shadePosition,1,coreEnergy*uPartGgxCore*uPartWhiteShift*0.40);
    float3 ramp=lerp(pow(saturate(uShadeColor),2.2),pow(saturate(uMidColor),2.2),smoothstep(0.05,0.55,shadePosition));
    ramp=lerp(ramp,pow(saturate(uHighlightColor),2.2),smoothstep(0.55,0.95,shadePosition));
    // 剑色作为虚拟光源的艺术配色，不改变玻璃介电反射率或给主体铺色。
    float sourceIntensity=1+uEmissionStrength*uPartEmission*.3+uStripeStrength*uPartStripe*.15;
    float3 reflected=ramp*(softLobe*uPartSoftLobe+coreEnergy*uPartGgxCore*0.65)*highlightMap*sourceIntensity;
    reflected+=ramp*reflectionFace.y*(1-reflectionFace.x)*.035*uPartBandReflection;
    // 薄亮刃与面高光使用同一亮色色阶，保持窄边形状并向内柔和衰减。
    float edgeResponse=reflectionFace.y;
    reflected+=edgeHighlight*(razorCore*uPartRazorCore*0.70+razorHalo*uPartRazorHalo*0.10
        +razorMist*uPartRazorMist*0.018)*edgeResponse;
    reflected+=GlassLinear(lerp(uBodyColor,uEdgeColor,0.32))*rim*uDissolve*uPartRim*0.10;
    // 反射：nearScene（屏幕空间反射叠环境图）此前算完即丢弃，从未进入画面。
    // 按菲涅耳加权——正对视角弱、掠射角强，这才是玻璃的读法。
    reflected+=GlassLinear(nearScene)*fresnelColor*uSsrStrength*uPartSsr;
    // 透明主体只吸收与透射背景；表面反光来自虚拟光源和环境，不使用条纹贴图发光。
    float3 coatingLight=reflected;
    float3 color=pow(saturate(coatingLight+pow(max(scene,0),2.2)*throughput*uHasScene*uPartSceneTransmission),1/2.2);
    // 所有缓冲按同一覆盖度预乘，重叠时不能用反射率当Alpha使高光近似累加。
    if(uOutputSurface>0.5)return float4(coatingLight*opacity,opacity);
    // 生命周期为零时颜色与Alpha一起退出，主体透明度不参与这次退场。
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
