using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 头部必须覆盖刀身，剑尖锚点及旧尾仍遵守原有几何合同。
internal static class SwordTrailHeadJoinCheck
{
    internal static void Run(Action<bool,string> check)
    {
        foreach(float length in new[]{16f,32f,68f})
        {
            var centers=Enumerable.Range(0,13).Select(i=>new Vector2(700-i*18,230)).ToArray();
            var rotations=Enumerable.Repeat(MathHelper.PiOver4,13).ToArray();
            var vertices=new VertexPositionColorTexture[SoulMaterialTrail.MaxLayeredVertices];
            int count=SoulMaterialTrail.BuildLayered(centers,rotations,13,length,Vector2.Zero,vertices,
                new[]{new SoulMaterialTrail.TrailLayer(0,13,1,true)},.3f);
            var a=vertices[0];var b=vertices[1];
            float t=(1-a.TextureCoordinate.Y)/(b.TextureCoordinate.Y-a.TextureCoordinate.Y);
            var basePoint=Vector3.Lerp(a.Position,b.Position,t);
            check(Vector3.Distance(basePoint,new Vector3(centers[0]-Vector2.UnitX*length,0))<.001f,
                "头部深度一准确覆盖当前刀身末端，长度"+length);
            check(vertices[18].Position==new Vector3(centers[0]+Vector2.UnitX*length,0)&&vertices[0].Color.G==0,
                "接合只扩展刀身侧，真实剑尖保持不动且头部权重完整，长度"+length);
            check(vertices.Take(count).Where(v=>v.Color.R>=51).All(v=>v.Color.G==255),
                "头部之外不继续施加接合权重，长度"+length);
        }
    }
}
