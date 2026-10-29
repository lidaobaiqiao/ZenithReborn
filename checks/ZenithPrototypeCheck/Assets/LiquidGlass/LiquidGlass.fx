// 独立研究原型：屏幕画布对应宽 2、高 1 的等尺度表面坐标，不接入生产刀光。
// 条带只提供高度，最终梯度形成同一法线，再计算面光反射与背景折射。
float uTime;
float uMode;
float uCount;
float uSmooth;
float uLight;
float2 uResolution;

sampler sceneSampler : register(s0);

float Strip(float2 p, float angle, float2 origin, float bend, float width, float height, float length, float phase)
{
    float c=cos(angle), s=sin(angle);
    float2 q=p-origin;
    float along=dot(q,float2(c,s));
    float across=dot(q,float2(-s,c));
    // 同一沿迹位置的横向偏移近似距离，不求曲线最近点；条带两端平滑沉回基面。
    // 中心线、高度与宽度均随时间连续变化。
    float center=bend*sin(along*2.3+phase);
    float localWidth=width*(1.0+0.12*sin(along*1.7+phase*0.8));
    float d=(across-center)/localWidth;
    float crossSection=saturate(1.0-d*d);
    // 纵向同样用有限支撑凸包，中心至两端都有连续坡度。
    float alongSection=saturate(1.0-(along/length)*(along/length));
    float endFade=alongSection*alongSection*alongSection;
    float amplitude=height*(0.70+0.30*sin(along*4.4+phase*0.65));
    return amplitude*crossSection*crossSection*crossSection*endFade;
}

float Merge(float a,float b)
{
    float top=max(a,b);
    // 两条曲面同时高出基面、且高度接近时才平滑；接近基面时连续衰减到零。
    // 两条曲面一次合并最多抬高 k/4；三条依次合并的总抬高不超过 k/2。
    float k=0.012;
    float overlap=smoothstep(0.0,k,min(a,b));
    float t=saturate(1.0-abs(a-b)/k);
    return top+uSmooth*overlap*k*0.25*t*t;
}

float4 Shade(float2 uv:TEXCOORD0):COLOR0
{
    float2 p=float2((uv.x-0.5)*2.0,uv.y-0.5);
    float time=uTime;
    float a=Strip(p,-0.12,float2(-0.13,-0.065),0.080,0.160,0.045,0.30,time*0.50);
    float b=Strip(p, 0.57,float2( 0.10, 0.025),0.064,0.128,0.0378,0.225,time*0.39+1.4);
    float c=Strip(p,-0.48,float2( 0.29, 0.12),0.050,0.110,0.0315,0.18,time*0.43+2.6);
    b*=uCount>=1.5?1.0:0.0;
    c*=uCount>=2.5?1.0:0.0;
    float height=Merge(Merge(a,b),c);
    // ddx、ddy 是每像素变化；按画布实际尺寸换回同一表面坐标单位。
    float2 gradient=float2(ddx(height)*uResolution.x*0.5,ddy(height)*uResolution.y);
    float3 normal=normalize(float3(-gradient,1.0));
    float3 view=float3(0,0,1);
    float3 reflection=reflect(-view,normal);
    float3 light=normalize(float3(-0.5+uLight,-0.7,1.0));
    float3 right=normalize(cross(float3(0,1,0),light));
    float3 up=normalize(cross(light,right));
    float facing=dot(reflection,light);
    float2 projected=float2(dot(reflection,right),dot(reflection,up))/max(facing,0.01);
    // 有限矩形光源的角度近似；不是精确面积光积分，也不采样笔刷。
    float2 edge=abs(projected)/float2(0.30,0.23);
    float panel=(1.0-smoothstep(0.75,1.0,max(edge.x,edge.y)))*step(0.0,facing);
    float fresnel=0.04+0.96*pow(1.0-saturate(dot(normal,view)),5.0);
    float3 reflected=panel*fresnel*float3(3.2,3.45,3.8);
    // 单界面折射近似，偏移量为实验参数；并未模拟玻璃背面与遮挡。
    float3 ray=refract(-view,normal,1.0/1.5);
    // 表面坐标的横向范围是纵向两倍，换成 UV 偏移时需同步换算。
    float2 offset=ray.xy/max(-ray.z,0.1)*float2(0.0175,0.035);
    float3 background=tex2D(sceneSampler,clamp(uv+offset,0.001,0.999)).rgb;
    float3 combined=pow(saturate(pow(max(background,0.0),2.2)*(1.0-fresnel)+reflected),1.0/2.2);
    float3 black=pow(saturate(reflected),1.0/2.2);
    float winner=(a>=b && a>=c)?1.0:(b>=c?2.0:3.0);
    float3 ownership=height<0.00001?float3(0,0,0):(winner<1.5?float3(1,0.2,0.2):(winner<2.5?float3(0.2,1,0.2):float3(0.2,0.4,1)));
    float seam=(a>0.00001 && b>0.00001)?1.0-smoothstep(0.0,0.012,abs(a-b)):0.0;
    if(uMode<0.5)return float4((height/0.1).xxx,1);
    if(uMode<1.5)return float4(normal*0.5+0.5,1);
    if(uMode<2.5)return float4(black,1);
    if(uMode<3.5)return float4(combined,1);
    if(uMode<4.5)return float4(ownership,1);
    if(uMode<5.5)return float4(seam.xxx,1);
    float3 original=tex2D(sceneSampler,uv).rgb;
    if(uMode<6.5)return float4(original,1);
    // 这两种诊断只输出透射，以区分法线折射和表面反射的贡献。
    float3 transmitted=uMode<7.5?original:background;
    return float4(pow(saturate(pow(max(transmitted,0.0),2.2)*(1.0-fresnel)),1.0/2.2),1);
}

technique LiquidGlass
{
    pass Surface
    {
        PixelShader=compile ps_3_0 Shade();
    }
}
