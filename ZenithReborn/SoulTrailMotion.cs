using Microsoft.Xna.Framework;

namespace ZenithReborn;

// 将未结束的剑影历史保持在主人移动后的参考系中。
public static class SoulTrailMotion
{
    public static bool IsOrdinaryShift(Vector2 shift)
        =>float.IsFinite(shift.X)&&float.IsFinite(shift.Y)&&shift.LengthSquared()<=90000f;

    public static void Translate(Vector2[] centers,Vector2[] points,int count,Vector2 shift)
    {
        if(shift==Vector2.Zero)return;
        int length=Math.Min(count,Math.Min(centers.Length,points.Length));
        for(int i=0;i<length;i++)
        {
            centers[i]+=shift;
            points[i]+=shift;
        }
    }
}
