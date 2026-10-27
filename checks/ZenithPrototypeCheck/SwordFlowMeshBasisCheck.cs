using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 连续朝向的数据合同：保持几何/UV/覆盖度，消除共享顶点的方向缝，并保留真实UV接缝。
internal static class SwordFlowMeshBasisCheck
{
    internal static void Run(Action<bool,string> check)
    {
        VertexPositionColorTexture V(float x,float y,float u,float v)=>new(new Vector3(x,y,0),new Color(.4f,1,1,.7f),new Vector2(u,v));
        var a=V(0,0,0,0);var b=V(0,50,0,1);var c=V(100,0,.5f,0);
        var d=V(100,80,.5f,1);var e=V(200,40,1,0);var f=V(230,100,1,1);
        VertexPositionColorTexture[] source=[a,b,c,b,d,c,c,d,e,d,f,e];
        var actual=SwordFlowMeshBasis.Prepare(source,source.Length,Matrix.Identity).Take(source.Length).ToArray();
        check(actual.Zip(source).All(pair=>pair.First.Position==pair.Second.Position&&pair.First.Color==pair.Second.Color&&pair.First.UV==pair.Second.TextureCoordinate),
            "连续朝向不移动顶点、不更改UV或逐顶点覆盖度");
        check(actual.GroupBy(v=>(v.Position,v.UV,v.Color)).All(group=>group.All(v=>v.AcrossGradient==group.First().AcrossGradient&&v.AlongGradient==group.First().AlongGradient)),
            "共享顶点的全部三角形副本使用完全一致的朝向");
        check(actual.All(v=>float.IsFinite(v.AcrossGradient.X)&&float.IsFinite(v.AlongGradient.Y)&&
            Math.Abs(v.AcrossGradient.Length()-1)<.00001f&&Math.Abs(v.AlongGradient.Length()-1)<.00001f),
            "连续朝向单位化且不产生非法数值");
        VertexPositionColorTexture[] plane=[V(0,0,0,0),V(100,0,1,0),V(0,100,0,1)];
        float angle=.4f;var rotation=Matrix.CreateRotationZ(angle);
        var rotated=SwordFlowMeshBasis.Prepare(plane,3,rotation).Take(3).ToArray();
        check(rotated.All(v=>Vector2.Distance(v.AcrossGradient,Vector2.TransformNormal(Vector2.UnitY,rotation))<.00001f),
            "观察矩阵旋转正确传递到屏幕空间朝向");
        VertexPositionColorTexture[] seam=[..plane,V(0,0,0,1),V(100,0,1,1),V(0,100,0,0)];
        var separate=SwordFlowMeshBasis.Prepare(seam,6,Matrix.Identity).Take(6).ToArray();
        check(Vector2.Dot(separate[0].AcrossGradient,separate[3].AcrossGradient)<-.99f,
            "空间重合但UV不同的表面不会被错误焊接");
        var degenerate=SwordFlowMeshBasis.Prepare([a,a,a],3,Matrix.Identity);
        check(degenerate.Take(3).All(v=>float.IsFinite(v.AcrossGradient.X)&&float.IsFinite(v.AlongGradient.Y)),
            "退化尾尖采用稳定中性朝向而不产生除零");
        SwordFlowMeshBasis.Clear();
    }
}
