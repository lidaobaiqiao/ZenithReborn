using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace ZenithReborn;

// 结束时仅保留真实剑迹的姿态数据；没有弹幕、碰撞、场景复制或额外光带。
[Autoload(Side=ModSide.Client)]
public sealed class SoulTrailEnding : ModSystem
{
    public const int NormalFrames=36;
    public const int RetiredFrames=24;
    private const int Capacity=64;
    private static readonly List<Ending> endings=new();
    public static int Created {get;private set;}
    public static int ActiveCount=>Main.gameMenu?0:endings.Count(e=>Age(e)<e.Frames);
    public static bool HasActive=>ActiveCount>0;

    public sealed class Pose
    {
        private readonly Vector2[] centers;
        private readonly float[] rotations;
        private readonly float halfLength;
        public Pose(Vector2[] sourceCenters,float[] sourceRotations,int count,float length)
        {
            count=Math.Min(count,Math.Min(SoulMaterialTrail.HistoryLength,Math.Min(sourceCenters.Length,sourceRotations.Length)));
            centers=sourceCenters.Take(count).ToArray();rotations=sourceRotations.Take(count).ToArray();halfLength=length;
        }
        public int Build(Vector2 camera,VertexPositionColorTexture[] vertices,float clock)
            =>SoulMaterialTrail.Build(centers,rotations,centers.Length,halfLength,camera,vertices,clock);
    }
    private sealed record Ending(Pose Pose,int ItemType,Color Tint,float Opacity,ulong Born,int Frames);
    private static int Age(Ending ending)=>(int)Math.Min(int.MaxValue,Main.GameUpdateCount-ending.Born);
    public static float Dissolve(int age,int frames)=>MathHelper.SmoothStep(0,1,Math.Clamp(age/(float)frames,0,1));
    public static float Opacity(int age,int frames)
        =>1-MathHelper.SmoothStep(0,1,Math.Clamp((age/(float)frames-.94f)/.06f,0,1));

    public static bool Capture(Vector2[] centers,float[] rotations,int count,float halfLength,int itemType,Color tint,float opacity,bool retired)
    {
        if(Main.dedServ||Main.gameMenu||Main.graphics?.GraphicsDevice==null||count<2||opacity<=0)return false;
        endings.RemoveAll(e=>Age(e)>=e.Frames);
        if(endings.Count>=Capacity)endings.RemoveAt(0);
        endings.Add(new(new Pose(centers,rotations,count,halfLength),itemType,tint,opacity,Main.GameUpdateCount,retired?RetiredFrames:NormalFrames));
        Created++;return true;
    }
    public static void DrawAll(VertexPositionColorTexture[] vertices)
    {
        if(Main.gameMenu)return;
        endings.RemoveAll(e=>Age(e)>=e.Frames);
        foreach(var ending in endings)
        {
            int age=Age(ending);float opacity=ending.Opacity*Opacity(age,ending.Frames);
            int count=ending.Pose.Build(Main.screenPosition,vertices,Main.GlobalTimeWrappedHourly);
            SoulMaterialTrail.Draw(vertices,count,ending.Tint,opacity,Main.GlobalTimeWrappedHourly,
                dissolve:Dissolve(age,ending.Frames),itemType:ending.ItemType);
        }
    }
    public override void OnWorldUnload(){endings.Clear();Created=0;}
    public override void Unload(){endings.Clear();Created=0;}
}
