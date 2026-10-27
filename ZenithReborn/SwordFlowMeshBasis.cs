using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ZenithReborn;

// 只负责带面顶点的连续UV朝向；不改几何、覆盖度、颜色、轨迹或材质强度。
public static class SwordFlowMeshBasis
{
    [StructLayout(LayoutKind.Sequential,Pack=1)]
    public struct Vertex : IVertexType
    {
        public Vector3 Position;
        public Color Color;
        public Vector2 UV,AcrossGradient,AlongGradient;
        public static readonly VertexDeclaration Declaration=new(
            new VertexElement(0,VertexElementFormat.Vector3,VertexElementUsage.Position,0),
            new VertexElement(12,VertexElementFormat.Color,VertexElementUsage.Color,0),
            new VertexElement(16,VertexElementFormat.Vector2,VertexElementUsage.TextureCoordinate,0),
            new VertexElement(24,VertexElementFormat.Vector2,VertexElementUsage.TextureCoordinate,1),
            new VertexElement(32,VertexElementFormat.Vector2,VertexElementUsage.TextureCoordinate,2));
        VertexDeclaration IVertexType.VertexDeclaration=>Declaration;
    }
    // UV与颜色都属于顶点身份，不能把重叠但不相连的剑影或不同材质接缝错误焊接。
    private readonly record struct Key(Vector3 Position,Vector2 UV,Color Color);
    private struct Sum { public Vector2 Across,Along; }
    private static readonly Dictionary<Key,int> indices=new();
    private static readonly List<Sum> sums=new();
    private static int[] corners=[];
    private static Vertex[] work=[];

    public static Vertex[] Prepare(VertexPositionColorTexture[] source,int count,Matrix view)
    {
        if(count<0||count>source.Length||count%3!=0)throw new ArgumentException("带面顶点数必须是有效三角形列表");
        if(work.Length<count){work=new Vertex[count];corners=new int[count];}
        indices.Clear();sums.Clear();
        for(int i=0;i<count;i++)
        {
            var v=source[i];var key=new Key(v.Position,v.TextureCoordinate,v.Color);
            if(!indices.TryGetValue(key,out int index))
            {index=sums.Count;indices.Add(key,index);sums.Add(default);}
            corners[i]=index;
        }
        for(int i=0;i<count;i+=3)
        {
            Vector2 Position(int j)=>Vector2.Transform(new Vector2(source[j].Position.X,source[j].Position.Y),view);
            var a=Position(i);var e1=Position(i+1)-a;var e2=Position(i+2)-a;
            var uv1=source[i+1].TextureCoordinate-source[i].TextureCoordinate;
            var uv2=source[i+2].TextureCoordinate-source[i].TextureCoordinate;
            float area=e1.X*e2.Y-e1.Y*e2.X;
            if(!float.IsFinite(area)||Math.Abs(area)<.000001f)continue;
            // 先在实际屏幕坐标求两个UV梯度，兼容缩放、旋转和翻转的观察矩阵。
            var along=new Vector2(uv1.X*e2.Y-uv2.X*e1.Y,e1.X*uv2.X-e2.X*uv1.X)/area;
            var across=new Vector2(uv1.Y*e2.Y-uv2.Y*e1.Y,e1.X*uv2.Y-e2.X*uv1.Y)/area;
            along=Unit(along,Vector2.UnitX);across=Unit(across,Vector2.UnitY);
            float weight=Math.Abs(area);
            for(int j=0;j<3;j++)
            {
                int index=corners[i+j];var sum=sums[index];
                sum.Along+=along*weight;sum.Across+=across*weight;sums[index]=sum;
            }
        }
        for(int i=0;i<count;i++)
        {
            var sourceVertex=source[i];var sum=sums[corners[i]];
            work[i]=new(){Position=sourceVertex.Position,Color=sourceVertex.Color,UV=sourceVertex.TextureCoordinate,
                AcrossGradient=Unit(sum.Across,Vector2.UnitY),AlongGradient=Unit(sum.Along,Vector2.UnitX)};
        }
        return work;
    }
    private static Vector2 Unit(Vector2 value,Vector2 fallback)
        =>float.IsFinite(value.X)&&float.IsFinite(value.Y)&&value.LengthSquared()>.00000001f?Vector2.Normalize(value):fallback;
    public static void Clear(){indices.Clear();sums.Clear();corners=[];work=[];}
}
