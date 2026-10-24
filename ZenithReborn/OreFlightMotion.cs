using Microsoft.Xna.Framework;

namespace ZenithReborn;

// 飞剑的实际运动曲线，AI 与受控视觉回放共用同一套。
// 后拉6帧蓄势，再用7帧从0.28倍加速到2.5倍标称速度；TravelFrames 按曲线累加算出行程帧数。
public static class OreFlightMotion
{
    public const int PullbackFrames=6,AccelerationFrames=7;
    public const float PullbackDistance=22,InitialSpeed=.28f,PeakSpeed=2.5f;
    // 按飞行年龄返回当前速度倍率（平滑加速）
    public static float SpeedMultiplier(int flightAge)=>MathHelper.SmoothStep(InitialSpeed,PeakSpeed,
        Math.Clamp(flightAge/(float)AccelerationFrames,0,1));
    // 后拉位移：就绪前6帧内平滑后撤22像素
    public static float Pullback(int age,int readyAge)=>PullbackDistance*MathHelper.SmoothStep(0,1,
        Math.Clamp((age-readyAge+PullbackFrames)/(float)PullbackFrames,0,1));
    // 按变速曲线累加，算出飞完全程需要多少帧（用于寿命与预判）
    public static int TravelFrames(float distance,float nominalSpeed)
    {
        if(!float.IsFinite(distance)||!float.IsFinite(nominalSpeed)||distance<0||nominalSpeed<=0)return 240;
        float travelled=0;int frames=0;
        while(travelled<=distance && frames<240)travelled+=nominalSpeed*SpeedMultiplier(frames++);
        return frames;
    }
}
