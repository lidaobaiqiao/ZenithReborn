// 液态玻璃：最低基面上的圆滑条带起伏。
// 两个材质入口（ZRSwordFlow.fx 与 ZRMaterialSurface.fx）共用同一份几何定义，
// 避免两处各写一套导致同一条剑迹在不同绘制路径下形状不一致。
//
// 层级：基面（平整玻璃）→ 条带隆起（本文件）→ 最终曲面。
// 目的不是画高光，而是把带面从平面顶成有局部起伏的曲面；
// 法线随曲面倾斜，折射方向跟着变，背景因此被真实弯折，而不是靠贴图伪造。
// 条带是几何基元，不应被辨认成独立线条或珠子；多条交错后参与成像的是整体曲面。

// 一条圆滑隆起的斜率。p = (中心横坐标, 半宽, 峰值坡度, 相位)，横坐标与半宽都以横向参数为单位。
// 中心线沿迹摆动、宽度沿迹呼吸、高度两端渐隐，
// 于是同一条带自己就在变宽变窄、弯曲、从无到有再消退，不依赖额外动画。
// 返回 (沿迹斜率, 横向斜率)，横向峰值约等于 p.z。
float2 ZRRidgeSlope(float s,float depth,float4 p)
{
    float ph=p.w;
    // 中心线：低频为主，叠一点高频，避免能看穿的规律感
    float C=p.x+0.16*sin(s*4.1+ph)+0.05*sin(s*9.3+ph*2.3);
    float Cp=0.656*cos(s*4.1+ph)+0.465*cos(s*9.3+ph*2.3);
    // 宽度沿迹呼吸
    float W=max(p.y*(0.75+0.30*sin(s*2.9+ph*1.7)),0.02);
    float Wp=p.y*0.87*cos(s*2.9+ph*1.7);
    // 包络：两端渐隐，中段满高
    float e1=saturate(s/0.15);
    float e2=saturate((1.0-s)/0.15);
    float env=e1*e2;
    float e1p=(s<0.15)?6.6667:0.0;
    float e2p=(s>0.85)?-6.6667:0.0;
    float envp=e1p*e2+e1*e2p;
    // 横向剖面 (1-t^2)^2：中心平缓、两侧圆滑下降、边缘切线水平，
    // 所以隆起与基面之间没有折痕，读起来是同一片玻璃被顶起来，而不是贴上去一条东西。
    float d=depth-C;
    float t=saturate(abs(d)/W);
    float omt=1.0-t*t;
    float P=omt*omt;
    // dP/dt 归一化到 ±1，使 p.z 直接就是峰值坡度
    float dPn=-4.0*t*omt/1.5396;
    float sgn=d>=0.0?1.0:-1.0;
    float across=p.z*env*dPn*sgn;
    // 沿迹斜率：中心线弯曲与宽度变化都会让同一处倾斜，量级约为横向的三成；
    // 再加包络自身的升降，使隆起在头尾也向基面倒下去。
    float along=p.z*env*dPn*0.30*(-sgn*Cp/W-t*Wp/W)+p.z*envp*P*0.45;
    return float2(along,across);
}

// 四条带交错：中心错开，宽窄与高低各不相同，相位以不同速度漂移。
// 宽度 0.17~0.20、中心间距约 0.26：宽度再大就会互相叠平成一整条缓坡（看不出隆起），
// 再窄则斜率在几像素内冲到峰值，折射会把背景切成碎块。
// 四条之间保留部分重叠，使相邻隆起在根部相连，读起来是同一片玻璃被顶起几道棱。
float2 ZRGlassRidges(float s,float depth,float time)
{
    float2 g=float2(0,0);
    g+=ZRRidgeSlope(s,depth,float4(0.16,0.170,1.05,time*0.09));
    g+=ZRRidgeSlope(s,depth,float4(0.42,0.200,0.80,2.7+time*0.06));
    g+=ZRRidgeSlope(s,depth,float4(0.68,0.165,0.95,4.4+time*0.11));
    g+=ZRRidgeSlope(s,depth,float4(0.92,0.185,0.65,6.1+time*0.08));
    return g;
}
