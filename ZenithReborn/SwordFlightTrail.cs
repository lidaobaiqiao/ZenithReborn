using Microsoft.Xna.Framework;

namespace ZenithReborn;

// Small visual-only history. Never connect a spawn, teleport or invalid sample
// to a distant previous position; no world coordinates reach a trail rectangle.
public sealed class SwordFlightTrail
{
    public const int Capacity=8;
    private readonly Vector2[] points=new Vector2[Capacity];
    private readonly float[] rotations=new float[Capacity];
    public int Count { get; private set; }
    public Vector2 Point(int index)=>points[index];
    public float Rotation(int index)=>rotations[index];
    public void Clear()=>Count=0;
    public void Record(Vector2 center,float rotation)
    {
        if(!float.IsFinite(center.X) || !float.IsFinite(center.Y) || !float.IsFinite(rotation)) { Clear();return; }
        if(Count>0 && Vector2.DistanceSquared(center,points[0])>96*96) Clear();
        for(int i=Math.Min(Count,Capacity-1);i>0;i--)
        { points[i]=points[i-1];rotations[i]=rotations[i-1]; }
        points[0]=center;rotations[0]=rotation;Count=Math.Min(Capacity,Count+1);
    }
    public static float Strength(int index,int count) => count<2?0:MathF.Pow(Math.Clamp(1-index/(float)(count-1),0,1),1.5f);
    public static float Formation(int age) => MathHelper.SmoothStep(0,1,Math.Clamp(age/20f,0,1));
}
