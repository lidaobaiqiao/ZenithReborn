using Microsoft.Xna.Framework;

namespace ZenithReborn;

// Actual projectile motion, shared by AI and controlled visual replays.
public static class OreFlightMotion
{
    public const int PullbackFrames=6,AccelerationFrames=7;
    public const float PullbackDistance=22,InitialSpeed=.28f,PeakSpeed=2.5f;
    public static float SpeedMultiplier(int flightAge)=>MathHelper.SmoothStep(InitialSpeed,PeakSpeed,
        Math.Clamp(flightAge/(float)AccelerationFrames,0,1));
    public static float Pullback(int age,int readyAge)=>PullbackDistance*MathHelper.SmoothStep(0,1,
        Math.Clamp((age-readyAge+PullbackFrames)/(float)PullbackFrames,0,1));
    public static int TravelFrames(float distance,float nominalSpeed)
    {
        if(!float.IsFinite(distance)||!float.IsFinite(nominalSpeed)||distance<0||nominalSpeed<=0)return 240;
        float travelled=0;int frames=0;
        while(travelled<=distance && frames<240)travelled+=nominalSpeed*SpeedMultiplier(frames++);
        return frames;
    }
}
