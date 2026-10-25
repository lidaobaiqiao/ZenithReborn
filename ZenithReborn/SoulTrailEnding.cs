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
    private const int Capacity=256;
    private static readonly List<Handle> endings=new();
    public static int Created {get;private set;}
    public static int ActiveCount=>Main.gameMenu?0:endings.Count(e=>Age(e)<e.Frames);
    public static bool HasActive=>ActiveCount>0;

    public sealed class Pose
    {
        private readonly Vector2[] centers;
        private readonly float[] rotations;
        private readonly float halfLength;
        private readonly bool unit;
        public Pose(Vector2[] sourceCenters,float[] sourceRotations,int count,float length)
        {
            count=Math.Min(count,Math.Min(SoulMaterialTrail.HistoryLength,Math.Min(sourceCenters.Length,sourceRotations.Length)));
            centers=sourceCenters.Take(count).ToArray();rotations=sourceRotations.Take(count).ToArray();halfLength=length;
        }
        public int Build(Vector2 camera,VertexPositionColorTexture[] vertices,float clock)
        {
            if(!unit)return SoulMaterialTrail.Build(centers,rotations,centers.Length,halfLength,camera,vertices,clock);
            Span<SoulMaterialTrail.TrailLayer> layers=stackalloc SoulMaterialTrail.TrailLayer[1];
            layers[0]=new(0,centers.Length,1,true);
            return SoulMaterialTrail.BuildLayered(centers,rotations,centers.Length,halfLength,camera,vertices,layers,clock);
        }
        internal Pose(Vector2[] sourceCenters,float[] sourceRotations,int start,int count,float length)
        {
            count=Math.Min(count,Math.Min(sourceCenters.Length,sourceRotations.Length)-start);
            centers=sourceCenters.AsSpan(start,count).ToArray();rotations=sourceRotations.AsSpan(start,count).ToArray();
            halfLength=length;unit=true;
        }
        internal void Follow(Vector2[] sourceCenters,float[] sourceRotations,int start)
        {
            int count=Math.Min(centers.Length,Math.Min(sourceCenters.Length,sourceRotations.Length)-start);
            sourceCenters.AsSpan(start,count).CopyTo(centers);sourceRotations.AsSpan(start,count).CopyTo(rotations);
        }
    }
    public sealed record Handle(Pose Pose,int ItemType,Color Tint,float Opacity,ulong Born,int Frames)
    {
        // 剑影仍可见时短尾跟随同一个历史头点；剑影熄灭后才停留在最后位置收尽。
        public void Follow(Vector2[] centers,float[] rotations,int start)
        {if(Age(this)<Frames)Pose.Follow(centers,rotations,start);}
    }
    private static int Age(Handle ending)=>(int)Math.Min(int.MaxValue,Main.GameUpdateCount-ending.Born);
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
    public static Handle CaptureUnit(Vector2[] centers,float[] rotations,int available,SoulTrailUnits.Unit unit,
        float halfLength,int itemType,Color tint,float opacity,bool retired)
    {
        if(Main.dedServ||Main.gameMenu||Main.graphics?.GraphicsDevice==null||unit.Samples<2||unit.Start+unit.Samples>available||opacity<=0)return null;
        endings.RemoveAll(e=>Age(e)>=e.Frames);
        if(endings.Count>=Capacity)endings.RemoveAt(0);
        // 旧剑影的短尾也更早收尽，避免几个不同时刻的冻结片段在归鞘处重新积成一团。
        int frames=retired?Math.Max(8,RetiredFrames-unit.Index*4):Math.Max(9,NormalFrames/(unit.Index+1));
        var handle=new Handle(new Pose(centers,rotations,unit.Start,unit.Samples,halfLength),itemType,tint,opacity,
            Main.GameUpdateCount,frames);
        endings.Add(handle);Created++;return handle;
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
