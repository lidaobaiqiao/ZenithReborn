using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 内缘必须平顺收束，不能为了接满剑柄制造突出的裙边；只检查轮廓约束，不代表美术认可。
internal static class SwordTrailInnerContourCheck
{
    internal static void Run(Action<bool,string> check)
    {
        foreach(float halfLength in new[]{16f,32f,68f})
        foreach(float speed in new[]{4f,18f,70f})
        {
            var centers=Enumerable.Range(0,13).Select(i=>new Vector2(1500-i*speed,230)).ToArray();
            var rotations=Enumerable.Repeat(MathHelper.PiOver4,13).ToArray();
            var vertices=new VertexPositionColorTexture[SoulMaterialTrail.MaxVertices];
            int count=SoulMaterialTrail.BuildLayered(centers,rotations,13,halfLength,Vector2.Zero,vertices,
                new[]{new SoulMaterialTrail.TrailLayer(0,13,1,true)},.3f);
            var sections=new List<float>();
            // 每个四行截面的深度跨度固定为0.54，反算其横向宽度即可检查是否存在鼓包。
            for(int i=0;i<count;i+=24)
                sections.Add(Math.Abs(vertices[i].Position.X-vertices[i+1].Position.X)/.54f);
            sections.Add(0);
            check(sections.Zip(sections.Skip(1)).All(p=>p.First+.001f>=p.Second),
                "头部到旧尾连续变窄，不形成先缩后鼓的多余扇面");
            check(sections[0]>halfLength*1.8f&&sections[0]<halfLength*1.85f,
                "头部保留刀身连接长度，停止撑到整把武器长度之外");
            check(vertices.Take(count).All(v=>v.Color.G==255),
                "内缘曲线由几何统一负责，不再用接合权重覆盖材质边界");
        }
    }
}
