// 独立真实轨迹网格试验：只供检查模组使用，未接入生产材质。
// UV 梯度由检查端在世界像素空间按共享顶点汇总，不能用单位方向替代尺度。
float4x4 uTransform;
// 只含世界到屏幕的旋转与等比缩放；平移和投影不应用于折射方向。
float4x4 uWorldToScreen;
float2 uSceneSize;
float uMaterialLength;
float uTrailDistance;
float uSurfaceWidth;
float uHeightUnit;
// 虚拟折射路径以世界像素表达，与高度场的长度单位解耦。
float uThicknessWorld;
float uTime;
float uLight;
float2 uLightDirection;
float uMode;
float uSmooth;

texture uScene;
sampler sceneSampler=sampler_state
{
    Texture=<uScene>;
    MinFilter=Linear;MagFilter=Linear;MipFilter=None;
    AddressU=Clamp;AddressV=Clamp;
};

struct VertexInput
{
    float4 Position:POSITION0;
    float4 Color:COLOR0;
    float2 UV:TEXCOORD0;
    float2 GradU:TEXCOORD1;
    float2 GradV:TEXCOORD2;
};
struct PixelInput
{
    float4 Position:POSITION0;
    float4 Color:COLOR0;
    float2 UV:TEXCOORD0;
    float2 GradU:TEXCOORD1;
    float2 GradV:TEXCOORD2;
    float2 SceneUV:TEXCOORD3;
};
PixelInput Transform(VertexInput input)
{
    PixelInput output;
    float4 clip=mul(input.Position,uTransform);
    output.Position=clip;
    output.Color=input.Color;
    output.UV=input.UV;
    output.GradU=input.GradU;
    output.GradV=input.GradV;
    output.SceneUV=clip.xy/clip.w*float2(.5,-.5)+.5;
    return output;
}

// 返回高度和该高度在表面坐标两轴上的解析偏导，避免每像素重复采样高度场。
float3 Strip(float2 p,float angle,float2 origin,float bend,float width,float height,float length,float phase,float flow)
{
    float c=cos(angle),s=sin(angle);
    float2 q=p-origin;
    float2 alongAxis=float2(c,s),acrossAxis=float2(-s,c);
    float along=dot(q,alongAxis);
    float across=dot(q,acrossAxis);
    // 使用曲线同一沿向位置的横向偏移近似距离，不声称精确最近点。
    float center=bend*sin(along*2.3+phase);
    float localWidth=width*(1.0+0.12*sin(along*1.7+phase*0.8));
    float d=(across-center)/localWidth;
    float crossSection=saturate(1.0-d*d);
    float alongSection=saturate(1.0-(along/length)*(along/length));
    // 长条沿向以两个低频曲率分量缓慢变形；基面保持原先的低幅曲率。
    float baseAmplitude=0.70+0.30*sin(along*4.4+phase*0.65);
    float flowAmplitude=0.60+0.25*sin(along*2.4+phase*0.65)+0.15*sin(along*4.1-phase*0.43);
    float amplitude=height*lerp(baseAmplitude,flowAmplitude,flow);
    float cross2=crossSection*crossSection,along2=alongSection*alongSection;
    float cross3=cross2*crossSection,along3=along2*alongSection;
    float centerPrime=bend*2.3*cos(along*2.3+phase);
    float widthPrime=width*0.12*1.7*cos(along*1.7+phase*0.8);
    float baseAmplitudePrime=0.30*4.4*cos(along*4.4+phase*0.65);
    float flowAmplitudePrime=0.25*2.4*cos(along*2.4+phase*0.65)+0.15*4.1*cos(along*4.1-phase*0.43);
    float amplitudePrime=height*lerp(baseAmplitudePrime,flowAmplitudePrime,flow);
    float dAlong=(-centerPrime-d*widthPrime)/localWidth;
    float crossPrime=-6.0*d*cross2;
    float alongPrime=-6.0*along*along2/(length*length);
    float gradientAlong=amplitudePrime*cross3*along3+amplitude*cross3*alongPrime+
        amplitude*along3*crossPrime*dAlong;
    float gradientAcross=amplitude*along3*crossPrime/localWidth;
    return float3(amplitude*cross3*along3,gradientAlong*alongAxis+gradientAcross*acrossAxis);
}
// 两个可见高度合并；平滑权重和高度差的变化一并进入解析偏导。
float3 Merge(float3 a,float3 b)
{
    float k=.012;
    float3 top=a.x>=b.x?a:b;
    float3 lower=a.x>=b.x?b:a;
    // 高度精确相等时取两侧梯度的中值；严格最大值在此处本身不可微。
    if(a.x==b.x)top.yz=(a.yz+b.yz)*.5;
    float overlapPosition=saturate(lower.x/k);
    float overlap=overlapPosition*overlapPosition*(3.0-2.0*overlapPosition);
    float blend=saturate(1.0-abs(a.x-b.x)/k);
    float2 overlapGradient=(6.0/k)*overlapPosition*(1.0-overlapPosition)*lower.yz;
    float2 blendGradient=abs(a.x-b.x)<k?(-sign(a.x-b.x)/k)*(a.yz-b.yz):float2(0,0);
    float lift=uSmooth*overlap*k*.25*blend*blend;
    float2 liftGradient=uSmooth*k*.25*(overlapGradient*blend*blend+overlap*2.0*blend*blendGradient);
    return float3(top.x+lift,top.yz+liftGradient);
}
struct SurfaceStrips {float3 a;float3 b;float3 c;float3 base;};
SurfaceStrips Strips(float2 p)
{
    SurfaceStrips h;
    // 三条长度、位置和波动各异的长凸起相互交错，只改变高度而不直接绘制亮纹。
    h.a=Strip(p,-.09,float2(-.80,-.105),.075,.140,.060,1.10,uTime*.50,1);
    h.b=Strip(p, .10,float2( .10, .080),.070,.145,.055,.95,uTime*.39+1.4,1);
    h.c=Strip(p,-.07,float2(1.00,-.025),.085,.135,.050,1.05,uTime*.43+2.6,1);
    // 长尺度低幅隆起让凸包之间仍有连续曲面；它只提供高度，不直接提供亮度。
    h.base=Strip(p,.035,float2(0,0),.07,.55,.012,2.35,uTime*.19+0.7,0);
    return h;
}
float4 Shade(PixelInput input):COLOR0
{
    // 两轴均换成同一世界长度单位，再以实际中段为原点；UV 会随带面一起移动。
    float2 p=float2((input.UV.x*uMaterialLength-uTrailDistance*.5)/uHeightUnit,
                    (input.UV.y-.5)*uSurfaceWidth/uHeightUnit);
    SurfaceStrips strips=Strips(p);
    float3 surface=Merge(Merge(strips.a,strips.b),Merge(strips.c,strips.base));
    float height=surface.x,dhx=surface.y,dhy=surface.z;
    // H_world = H(p)*uHeightUnit；链式法则中的 uHeightUnit 恰好与坐标换算相消。
    float2 slope=dhx*uMaterialLength*input.GradU+dhy*uSurfaceWidth*input.GradV;
    float clipped=step(6.0,max(abs(slope.x),abs(slope.y)));
    // 极端退化处阻止无界斜率，不能据此宣称尾部坐标映射完全准确。
    slope=clamp(slope,-6.0,6.0);
    float3 normal=normalize(float3(-slope,1));
    float3 view=float3(0,0,1);
    float3 reflection=reflect(-view,normal);
    // 夹具明确提供虚拟面光方向；uLight 仅用于检查光源位置改变。
    float3 light=normalize(float3(uLightDirection.x+uLight,uLightDirection.y,1));
    float3 right=normalize(cross(float3(0,1,0),light));
    float3 up=normalize(cross(light,right));
    float facing=dot(reflection,light);
    float2 projected=float2(dot(reflection,right),dot(reflection,up))/max(facing,.01);
    float2 edge=abs(projected)/float2(.30,.23);
    float panel=(1.0-smoothstep(.75,1.0,max(edge.x,edge.y)))*step(0.0,facing);
    float fresnel=.04+.96*pow(1.0-saturate(dot(normal,view)),5.0);
    float3 reflected=panel*fresnel*float3(3.2,3.45,3.8);
    float3 ray=refract(-view,normal,1.0/1.5);
    // 折射方向与法线先在世界空间求得，再转换为屏幕像素方向；不能混入相机平移。
    float2 screenRay=mul(float4(ray.xy,0,0),uWorldToScreen).xy;
    float2 offset=(screenRay/max(-ray.z,.1))*uThicknessWorld/uSceneSize;
    float2 texel=.5/uSceneSize;
    float3 background=tex2D(sceneSampler,clamp(input.SceneUV+offset,texel,1.0-texel)).rgb;
    float3 original=tex2D(sceneSampler,clamp(input.SceneUV,texel,1.0-texel)).rgb;
    float3 combined=pow(saturate(pow(max(background,0.0),2.2)*(1.0-fresnel)+reflected),1.0/2.2);
    float3 black=pow(saturate(reflected),1.0/2.2);
    float3 owner=height<.00001?float3(0,0,0):
        (strips.base.x>=max(strips.a.x,max(strips.b.x,strips.c.x))?float3(.8,.8,.2):
        (strips.a.x>=strips.b.x&&strips.a.x>=strips.c.x?float3(1,.2,.2):
        (strips.b.x>=strips.c.x?float3(.2,1,.2):float3(.2,.4,1))));
    // 仅比较相邻最高的两个正高度，标出真正参与可见表面选择的交界。
    float firstPair=max(strips.a.x,strips.b.x),secondPair=max(strips.c.x,strips.base.x);
    float highest=max(firstPair,secondPair);
    float second=max(min(firstPair,secondPair),max(min(strips.a.x,strips.b.x),min(strips.c.x,strips.base.x)));
    float seam=second>.00001?1.0-smoothstep(0.0,.012,highest-second):0.0;
    float coverage=input.Color.a;
    if(uMode<.5)return float4((height/.1).xxx,coverage);
    if(uMode<1.5)return float4(normal*.5+.5,coverage);
    if(uMode<2.5)return float4(black,coverage);
    if(uMode<3.5)return float4(combined,coverage);
    if(uMode<4.5)return float4(owner,coverage);
    if(uMode<5.5)return float4(seam.xxx,coverage);
    if(uMode<6.5)return float4(original,coverage);
    if(uMode<8.5)
    {
        float3 transmitted=uMode<7.5?original:background;
        return float4(pow(saturate(pow(max(transmitted,0.0),2.2)*(1.0-fresnel)),1.0/2.2),coverage);
    }
    // 单独标出法线坡度被限制的像素，避免把退化近似误称为完整物理计算。
    if(uMode<9.5)return float4(clipped,0,0,coverage);
    // 只供隔离检查：编码世界折射方向转换后的屏幕 XY，零方向对应颜色中点。
    float2 screenDirection=screenRay/max(-ray.z,.1);
    return float4(saturate(screenDirection*.5+.5),0,coverage);
}
technique LiquidGlassTrail
{
    pass Surface
    {
        VertexShader=compile vs_3_0 Transform();
        PixelShader=compile ps_3_0 Shade();
    }
}
