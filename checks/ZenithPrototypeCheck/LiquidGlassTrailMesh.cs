using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ZenithPrototypeCheck;

// 隔离夹具专用：保留真实刀光网格和 UV，并为顶点补充带尺度的世界坐标梯度。
internal static class LiquidGlassTrailMesh
{
    [StructLayout(LayoutKind.Sequential,Pack=1)]
    internal struct Vertex : IVertexType
    {
        public Vector3 Position;
        public Color Color;
        public Vector2 UV,GradU,GradV;
        public static readonly VertexDeclaration Declaration=new(
            new VertexElement(0,VertexElementFormat.Vector3,VertexElementUsage.Position,0),
            new VertexElement(12,VertexElementFormat.Color,VertexElementUsage.Color,0),
            new VertexElement(16,VertexElementFormat.Vector2,VertexElementUsage.TextureCoordinate,0),
            new VertexElement(24,VertexElementFormat.Vector2,VertexElementUsage.TextureCoordinate,1),
            new VertexElement(32,VertexElementFormat.Vector2,VertexElementUsage.TextureCoordinate,2));
        VertexDeclaration IVertexType.VertexDeclaration=>Declaration;
    }
    private readonly record struct Key(Vector3 Position,Vector2 UV,Color Color);
    private struct Sum { public Vector2 U,V;public float Weight; }
    internal readonly record struct Diagnostics(int Triangles,int DegenerateTriangles,int UnrepresentedVertices,
        float LargestGradU,float LargestGradV,float AverageEdgeUvResidual,float MaxEdgeUvResidual,int EdgeResidualSamples,
        float WorstEdgeCenterX,float WorstEdgeCenterY,float WorstEdgeCenterU,float WorstEdgeCenterV,string WorstResidualComponent);

    internal static (Vertex[] Vertices,Diagnostics Info) Prepare(VertexPositionColorTexture[] source,int count)
    {
        if(count<0||count>source.Length||count%3!=0)throw new ArgumentException("顶点数必须构成完整的三角形列表");
        var indices=new Dictionary<Key,int>();var sums=new List<Sum>();var corners=new int[count];
        for(int i=0;i<count;i++)
        {
            var v=source[i];var key=new Key(v.Position,v.TextureCoordinate,v.Color);
            if(!indices.TryGetValue(key,out int index)){index=sums.Count;indices.Add(key,index);sums.Add(default);}
            corners[i]=index;
        }
        int degenerate=0;
        for(int i=0;i<count;i+=3)
        {
            Vector2 pos(int j)=>new(source[j].Position.X,source[j].Position.Y);
            Vector2 a=pos(i),e1=pos(i+1)-a,e2=pos(i+2)-a;
            Vector2 uv1=source[i+1].TextureCoordinate-source[i].TextureCoordinate;
            Vector2 uv2=source[i+2].TextureCoordinate-source[i].TextureCoordinate;
            float area=e1.X*e2.Y-e1.Y*e2.X;
            if(!float.IsFinite(area)||Math.Abs(area)<0.0001f){degenerate++;continue;}
            var u=new Vector2(uv1.X*e2.Y-uv2.X*e1.Y,e1.X*uv2.X-e2.X*uv1.X)/area;
            var v=new Vector2(uv1.Y*e2.Y-uv2.Y*e1.Y,e1.X*uv2.Y-e2.X*uv1.Y)/area;
            if(!float.IsFinite(u.X)||!float.IsFinite(u.Y)||!float.IsFinite(v.X)||!float.IsFinite(v.Y))
            {degenerate++;continue;}
            float weight=Math.Abs(area);
            for(int j=0;j<3;j++)
            {
                int index=corners[i+j];var s=sums[index];s.U+=u*weight;s.V+=v*weight;s.Weight+=weight;sums[index]=s;
            }
        }
        var result=new Vertex[count];int absent=0;float maxU=0,maxV=0;
        for(int i=0;i<count;i++)
        {
            var s=sums[corners[i]];Vector2 u=Vector2.Zero,v=Vector2.Zero;
            if(s.Weight>0){u=s.U/s.Weight;v=s.V/s.Weight;}
            else absent++;
            maxU=Math.Max(maxU,u.Length());maxV=Math.Max(maxV,v.Length());
            var z=source[i];result[i]=new Vertex{Position=z.Position,Color=z.Color,UV=z.TextureCoordinate,GradU=u,GradV=v};
        }
        // 平滑梯度只是近似雅可比；将每片中心梯度与原始边的 UV 变化对照，保留误差而不假称逐片精确。
        double totalResidual=0;float maxResidual=0;int edgeSamples=0;
        Vector2 worstPosition=Vector2.Zero,worstUv=Vector2.Zero;string worstComponent="无";
        for(int i=0;i<count;i+=3)
        {
            Vector2 p0=new(source[i].Position.X,source[i].Position.Y);
            Vector2 p1=new(source[i+1].Position.X,source[i+1].Position.Y);
            Vector2 p2=new(source[i+2].Position.X,source[i+2].Position.Y);
            Vector2 e1=p1-p0,e2=p2-p0;
            if(Math.Abs(e1.X*e2.Y-e1.Y*e2.X)<0.0001f)continue;
            Vector2 gu=(result[i].GradU+result[i+1].GradU+result[i+2].GradU)/3;
            Vector2 gv=(result[i].GradV+result[i+1].GradV+result[i+2].GradV)/3;
            for(int edge=1;edge<=2;edge++)
            {
                Vector2 delta=edge==1?e1:e2,uv=source[i+edge].TextureCoordinate-source[i].TextureCoordinate;
                float ru=Math.Abs(Vector2.Dot(gu,delta)-uv.X),rv=Math.Abs(Vector2.Dot(gv,delta)-uv.Y);
                totalResidual+=ru+rv;edgeSamples+=2;
                if(Math.Max(ru,rv)>maxResidual)
                {
                    maxResidual=Math.Max(ru,rv);worstPosition=p0+delta*.5f;
                    worstUv=(source[i].TextureCoordinate+source[i+edge].TextureCoordinate)*.5f;
                    worstComponent=ru>=rv?"纵向":"横向";
                }
            }
        }
        return (result,new Diagnostics(count/3,degenerate,absent,maxU,maxV,
            edgeSamples>0?(float)(totalResidual/edgeSamples):0,maxResidual,edgeSamples,
            worstPosition.X,worstPosition.Y,worstUv.X,worstUv.Y,worstComponent));
    }
}
