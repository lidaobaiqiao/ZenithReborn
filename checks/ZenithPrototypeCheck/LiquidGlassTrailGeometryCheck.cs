using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ZenithPrototypeCheck;

// 隔离验证：旋转和等比缩放网格后，连续 UV 梯度应随坐标作逆尺度变换。
internal static class LiquidGlassTrailGeometryCheck
{
    internal readonly record struct Result(float Scale,float Rotation,int VertexCount,float MaxAbsoluteError,
        int DegenerateTriangles,int UnrepresentedVertices,bool Passed);

    internal static Result[] CheckSimilarity(VertexPositionColorTexture[] original,int count,
        LiquidGlassTrailMesh.Vertex[] baseline)
    {
        var cases=new[]{(Scale:.75f,Rotation:.43f),(Scale:1.5f,Rotation:-.61f)};
        var results=new Result[cases.Length];
        for(int c=0;c<cases.Length;c++)
        {
            var item=cases[c];var rotation=Matrix.CreateRotationZ(item.Rotation);
            var input=new VertexPositionColorTexture[count];
            for(int i=0;i<count;i++)
            {
                var vertex=original[i];
                vertex.Position=Vector3.Transform(vertex.Position,rotation)*item.Scale+new Vector3(150,80,0);
                input[i]=vertex;
            }
            var transformed=LiquidGlassTrailMesh.Prepare(input,count);
            float maxError=0;
            for(int i=0;i<count;i++)
            {
                Vector2 expectedU=Vector2.TransformNormal(baseline[i].GradU,rotation)/item.Scale;
                Vector2 expectedV=Vector2.TransformNormal(baseline[i].GradV,rotation)/item.Scale;
                maxError=Math.Max(maxError,Vector2.Distance(expectedU,transformed.Vertices[i].GradU));
                maxError=Math.Max(maxError,Vector2.Distance(expectedV,transformed.Vertices[i].GradV));
            }
            // 退化网格会放大浮点误差；记录实际误差，并只把该固定输入的有限结果纳入合同。
            bool passed=float.IsFinite(maxError)&&maxError<.001f;
            results[c]=new Result(item.Scale,item.Rotation,count,maxError,transformed.Info.DegenerateTriangles,
                transformed.Info.UnrepresentedVertices,passed);
        }
        return results;
    }
}
