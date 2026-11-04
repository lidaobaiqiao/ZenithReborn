using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ZenithReborn;

// 网格位置来自实际剑身历史，普通二维坐标交给项目自有着色器。
// 轨迹、缓慢底色和动态形状各自独立，美术输入可以逐步替换。
public static class SoulMaterialTrail
{
    public const int HistoryLength=60;   // 与原版 TrailCacheLength 一致
    private const int Steps=3;
    private const int Rows=4;
    public const int MaxVertices=(HistoryLength-1)*Steps*Rows*6;
    public const int MaxLayeredVertices=MaxVertices*3;

    // 一条独立短带面的历史窗口；Start 对应剑影，Count 随剑影年龄缩短。
    public readonly record struct TrailLayer(int Start,int Count,float Opacity,bool FullyFormed=false);

    public static void Draw(VertexPositionColorTexture[] vertices,int count,Color tint,float opacity,
        float clock,int? width=null,int? height=null,bool unscaled=false,float dissolve=0,int itemType=0)
        =>SwordFlowMaterial.Draw(vertices,count,tint,opacity,clock,width,height,unscaled,dissolve,itemType);

    public static int Build(Vector2[] centers,float[] rotations,int count,float halfLength,
        Vector2 screenPosition,VertexPositionColorTexture[] vertices,float clock=0)
        =>Math.Max(0,BuildStrip(centers,rotations,count,0,count,halfLength,screenPosition,vertices,0,clock,false));

    // 主剑与旧剑影共用同一份三角形列表，中间不连接；最后仍然只需要一次材质绘制。
    // 调用方按旧到新提供窗口，短带面的头部直接保持完整宽度，不在重叠处重复渐入形成锯齿。
    public static int BuildLayered(Vector2[] centers,float[] rotations,int count,float halfLength,
        Vector2 screenPosition,VertexPositionColorTexture[] vertices,ReadOnlySpan<TrailLayer> layers,float clock=0)
    {
        int written=0;
        foreach(var layer in layers)
        {
            if(layer.Opacity<=0)continue;
            if(!float.IsFinite(layer.Opacity))return 0;
            int offset=written;
            int strip=BuildStrip(centers,rotations,count,layer.Start,layer.Count,halfLength,
                screenPosition,vertices,offset,clock,layer.FullyFormed||layer.Start!=0);
            if(strip<0)return 0;
            if(strip==0)continue;
            if(layer.Opacity<1)
            {
                // 每条短带面保留自己的完整头到尾衰减，再乘所属剑影的亮度。
                for(int i=offset;i<offset+strip;i++)
                {
                    Color color=vertices[i].Color;
                    vertices[i].Color=new Color(color.R,color.G,color.B,(byte)Math.Round(color.A*Math.Clamp(layer.Opacity,0,1)));
                }
            }
            written+=strip;
        }
        return written;
    }

    private static int BuildStrip(Vector2[] centers,float[] rotations,int available,int start,int requested,
        float halfLength,Vector2 screenPosition,VertexPositionColorTexture[] vertices,int offset,float clock,bool fullyFormed)
    {
        available=Math.Min(available,Math.Min(centers.Length,rotations.Length));
        if(start<0 || start>=available || requested<2 || !float.IsFinite(halfLength) || halfLength<=0 || !float.IsFinite(clock))return -1;
        int count=Math.Min(requested,Math.Min(HistoryLength,available-start));
        if(count<2)return 0;
        if(vertices.Length<offset+(count-1)*Steps*Rows*6)throw new ArgumentException("拖尾顶点缓存不足");
        for(int i=0;i<count;i++)
        {
            int index=start+i;
            if(!float.IsFinite(centers[index].X) || !float.IsFinite(centers[index].Y) || !float.IsFinite(rotations[index]))return -1;
            if(i>0 && Vector2.DistanceSquared(centers[index],centers[index-1])>90000)return -1;
        }
        halfLength=Math.Clamp(halfLength,6,70);
        // 以实际剑尖走过的距离分配材质，避免快慢段出现不同的拉伸密度。
        Span<float> distances=stackalloc float[HistoryLength];distances[0]=0;
        Vector2 Tip(int i)
        {
            float angle=rotations[start+i]-MathHelper.PiOver4;
            return centers[start+i]+new Vector2(MathF.Cos(angle),MathF.Sin(angle))*halfLength;
        }
        for(int i=1;i<count;i++)distances[i]=distances[i-1]+Vector2.Distance(Tip(i-1),Tip(i));
        float distance=distances[count-1];
        if(distance<.5f)return 0;
        float growth=fullyFormed?1:MathHelper.SmoothStep(0,1,Math.Clamp(distance/(halfLength*2.5f),0,1));
        float materialLength=Math.Max(distance,halfLength*6);
        int written=offset;
        Section previous=Sample(centers[start],rotations[start],0,0,halfLength,screenPosition,growth,clock);
        for(int segment=0;segment<count-1;segment++)
        for(int step=1;step<=Steps;step++)
        {
            float t=step/(float)Steps;
            Vector2 center=Vector2.CatmullRom(centers[start+Math.Max(0,segment-1)],centers[start+segment],
                centers[start+segment+1],centers[start+Math.Min(count-1,segment+2)],t);
            float rotation=rotations[start+segment]+MathHelper.WrapAngle(rotations[start+segment+1]-rotations[start+segment])*t;
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
        return written-offset;
    }

    private static Section Sample(Vector2 center,float rotation,float age,float materialAge,float halfLength,
        Vector2 screen,float growth,float clock)
    {
        float angle=rotation-MathHelper.PiOver4;
        Vector2 axis=new(MathF.Cos(angle),MathF.Sin(angle));
        Vector2 tip=center-screen+axis*halfLength;
        // 剑尖就是亮刃所在边；内侧沿该帧的剑身方向展开，尾部逐渐收束。
        float taper=MathF.Pow(Math.Clamp(1-age,0,1),.65f);
        // 内缘从刀刃靠护手处连续收向旧尾，避免收肩缺口和伸出护手的扇面。
        float headBlend=1-MathHelper.SmoothStep(0,1,Math.Clamp(age/.24f,0,1));
        float shoulder=MathHelper.Lerp(1f,1.20f,headBlend);
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
