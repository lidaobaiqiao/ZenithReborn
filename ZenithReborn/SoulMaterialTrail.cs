using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ZenithReborn;

// 网格位置来自实际剑身历史，普通二维坐标交给项目自有着色器。
// 轨迹、缓慢底色和动态形状各自独立，美术输入可以逐步替换。
public static class SoulMaterialTrail
{
    public const int HistoryLength=32;
    private const int Steps=3;
    private const int Rows=4;
    public const int MaxVertices=(HistoryLength-1)*Steps*Rows*6;
    public const float MaterialSpan=1.6f;
    public const float MaterialThickness=.34f;

    public static void Draw(VertexPositionColorTexture[] vertices,int count,Color tint,float opacity,
        float clock,int? width=null,int? height=null,bool unscaled=false,float dissolve=0,int itemType=0)
        =>SwordFlowMaterial.Draw(vertices,count,tint,opacity,clock,width,height,unscaled,dissolve,itemType);

    // 只控制外观：先随剑体出现，再在返回末段熄灭，不延长攻击或碰撞。
    public static float Dissolve(float phase,bool retired,int retireAge)
        =>Math.Clamp(retired?retireAge/8f:(phase-60)/24f,0,1);

    public static float Visibility(float phase,bool retired,int retireAge)
    {
        float formation=MathHelper.SmoothStep(0,1,Math.Clamp(phase/12f,0,1));
        return formation*MathF.Pow(1-Dissolve(phase,retired,retireAge),.65f);
    }

    public static int Build(Vector2[] centers,float[] rotations,int count,float halfLength,
        Vector2 screenPosition,VertexPositionColorTexture[] vertices,float clock=0)
    {
        count=Math.Min(count,Math.Min(HistoryLength,Math.Min(centers.Length,rotations.Length)));
        if(count<2 || !float.IsFinite(halfLength) || halfLength<=0 || !float.IsFinite(clock))return 0;
        if(vertices.Length<(count-1)*Steps*Rows*6)throw new ArgumentException("拖尾顶点缓存不足");
        for(int i=0;i<count;i++)
        {
            if(!float.IsFinite(centers[i].X) || !float.IsFinite(centers[i].Y) || !float.IsFinite(rotations[i]))return 0;
            if(i>0 && Vector2.DistanceSquared(centers[i],centers[i-1])>90000)return 0;
        }
        halfLength=Math.Clamp(halfLength,6,70);
        // 以实际剑尖走过的距离分配材质，避免快慢段出现不同的拉伸密度。
        Span<float> distances=stackalloc float[HistoryLength];distances[0]=0;
        Vector2 Tip(int i)
        {
            float angle=rotations[i]-MathHelper.PiOver4;
            return centers[i]+new Vector2(MathF.Cos(angle),MathF.Sin(angle))*halfLength;
        }
        for(int i=1;i<count;i++)distances[i]=distances[i-1]+Vector2.Distance(Tip(i-1),Tip(i));
        float distance=distances[count-1];
        if(distance<.5f)return 0;
        float growth=MathHelper.SmoothStep(0,1,Math.Clamp(distance/(halfLength*2.5f),0,1));
        float materialLength=Math.Max(distance,halfLength*6);
        int written=0;
        Section previous=Sample(centers[0],rotations[0],0,0,halfLength,screenPosition,growth,clock);
        for(int segment=0;segment<count-1;segment++)
        for(int step=1;step<=Steps;step++)
        {
            float t=step/(float)Steps;
            Vector2 center=Vector2.CatmullRom(centers[Math.Max(0,segment-1)],centers[segment],
                centers[segment+1],centers[Math.Min(count-1,segment+2)],t);
            float rotation=rotations[segment]+MathHelper.WrapAngle(rotations[segment+1]-rotations[segment])*t;
            float traveled=MathHelper.Lerp(distances[segment],distances[segment+1],t);
            Section current=Sample(center,rotation,traveled/distance,traveled/materialLength,
                halfLength,screenPosition,growth,clock);
            for(int row=0;row<Rows;row++)
            {
                var a=previous.Vertex(row);var b=previous.Vertex(row+1);
                var c=current.Vertex(row);var d=current.Vertex(row+1);
                vertices[written++]=a;vertices[written++]=b;vertices[written++]=c;
                vertices[written++]=b;vertices[written++]=d;vertices[written++]=c;
            }
            previous=current;
        }
        return written;
    }

    private static Section Sample(Vector2 center,float rotation,float age,float materialAge,float halfLength,
        Vector2 screen,float growth,float clock)
    {
        float angle=rotation-MathHelper.PiOver4;
        Vector2 axis=new(MathF.Cos(angle),MathF.Sin(angle));
        Vector2 tip=center-screen+axis*halfLength;
        // 剑尖就是亮刃所在边；内侧沿该帧的剑身方向展开，尾部逐渐收束。
        float taper=MathF.Pow(Math.Clamp(1-age,0,1),.65f);
        float shoulder=MathHelper.Lerp(.44f,1,MathHelper.SmoothStep(0,1,Math.Clamp(age/.24f,0,1)));
        // 薄玻璃的轮廓只做轻微呼吸，反光承担主要动效，避免整片材质像液体一样扭动。
        float wave=1+.025f*MathF.Sin(age*9-clock*1.3f)*MathF.Sin(age*MathHelper.Pi);
        float width=halfLength*1.52f*taper*shoulder*growth*wave;
        return new Section(tip,axis,width,materialAge,age);
    }

    private readonly record struct Section(Vector2 Tip,Vector2 Axis,float Width,float MaterialAge,float Age)
    {
        public VertexPositionColorTexture Vertex(int row)
        {
            // 覆盖暗侧羽化与亮侧外缘，并保留精确落在真实剑尖的顶点。
            float v=row switch {0=>-.46f,1=>.08f,2=>.55f,3=>1f,_=>1.12f};
            Vector2 position=Tip-Axis*Width*(1-v);
            Vector2 uv=new(MaterialAge,1-v);
            // 红通道保存完整轨迹年龄，供结束蒙版沿旧尾收向剑尖；透明度仍用原曲线。
            Color visibility=new Color(Math.Clamp(Age,0,1),1f,1f,MathF.Pow(Math.Clamp(1-Age,0,1),.36f));
            return new VertexPositionColorTexture(new Vector3(position,0),visibility,uv);
        }
    }
}
