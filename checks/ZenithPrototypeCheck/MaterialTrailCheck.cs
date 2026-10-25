using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class MaterialTrailCheck
{
    internal static void Run(Action<bool,string> check)
    {
        SwordColorNoiseCheck.Run(check);
        CheckUnits(check);
        var centers=new Vector2[SoulMaterialTrail.HistoryLength];
        var rotations=new float[centers.Length];
        for(int i=0;i<centers.Length;i++)
        {
            centers[i]=new Vector2(800-i*16,220+MathF.Sin(i*.17f)*70);
            rotations[i]=MathHelper.PiOver4+.22f*MathF.Cos(i*.17f);
        }
        var vertices=new VertexPositionColorTexture[SoulMaterialTrail.MaxVertices];
        var shifted=new VertexPositionColorTexture[vertices.Length];
        int count=SoulMaterialTrail.Build(centers,rotations,centers.Length,32,Vector2.Zero,vertices);
        check(count>0 && count%3==0 && count<=vertices.Length,"Material trail bounded triangle geometry");
        // 历史长度可由其他开发调整，边界应跟随实际输入轨迹，而非假定永远32帧。
        check(vertices.Take(count).All(v=>float.IsFinite(v.Position.X) && float.IsFinite(v.TextureCoordinate.X)
            && v.Position.X>centers.Min(p=>p.X)-64 && v.Position.X<centers.Max(p=>p.X)+64
            && v.Position.Y>centers.Min(p=>p.Y)-64 && v.Position.Y<centers.Max(p=>p.Y)+64),
            "Material trail follows the actual noncircular path");
        int moved=SoulMaterialTrail.Build(centers,rotations,centers.Length,32,new Vector2(150,90),shifted);
        check(count==moved && Enumerable.Range(0,count).All(i=>Vector2.Distance(
            new Vector2(shifted[i].Position.X,shifted[i].Position.Y),
            new Vector2(vertices[i].Position.X-150,vertices[i].Position.Y-90))<.001f &&
            shifted[i].TextureCoordinate==vertices[i].TextureCoordinate),"Camera translates geometry without sliding material coordinates");
        // 自有着色器直接读取路程与深度，不再反算第三方材质的极角和径向包络。
        check(vertices[0].TextureCoordinate.X==0 && Math.Abs(vertices[count-1].TextureCoordinate.X-1)<.001f &&
            vertices.Take(count).All(v=>v.TextureCoordinate.X>=0 && v.TextureCoordinate.X<=1),
            "Own shader consumes ordinary distance coordinates without crescent mapping");
        check(vertices[0].Color.A==255 && vertices[count-1].Color.A==0,
            "Own shader receives per-pose lifetime alpha including transparent oldest sample");
        check(vertices[0].Color.R==0&&vertices[count-1].Color.R==255,
            "Ending coordinate spans the full real path independently of material tiling");
        var pose=new SoulTrailEnding.Pose(centers,rotations,centers.Length,32);
        var copiedCenters=centers.ToArray();var copiedRotations=rotations.ToArray();
        Array.Fill(centers,Vector2.Zero);Array.Fill(rotations,0);
        int frozen=pose.Build(Vector2.Zero,shifted,0);
        check(frozen==count&&Enumerable.Range(0,count).All(i=>vertices[i].Position==shifted[i].Position),
            "Ending retains the actual path after the parent history is overwritten");
        centers=copiedCenters;rotations=copiedRotations;
        int translated=pose.Build(new Vector2(50,20),shifted,0);
        check(translated==count&&Enumerable.Range(0,count).All(i=>Vector3.Distance(vertices[i].Position-new Vector3(50,20,0),shifted[i].Position)<.001f),
            "Frozen ending follows camera movement without dragging the world");
        check(SoulTrailEnding.Opacity(10,20)==1&&SoulTrailEnding.Opacity(20,20)==0&&SoulTrailEnding.Dissolve(20,20)==1,
            "Ending erodes at full body density before its final edge fade");
        check(Enumerable.Range(0,20).All(i=>SoulTrailEnding.Dissolve(i,20)<=SoulTrailEnding.Dissolve(i+1,20)),
            "Ending progress is monotonic and completes independently of sword speed");
        check(Vector3.Distance(vertices[count-1].Position,vertices[count-2].Position)<.001f,
            "Oldest trail tapers to zero width");
        check(SoulMaterialTrail.Build(centers,rotations,1,32,Vector2.Zero,vertices)==0,"No material before path exists");
        int animatedCount=SoulMaterialTrail.Build(centers,rotations,centers.Length,32,Vector2.Zero,shifted,.7f);
        check(animatedCount==count && Enumerable.Range(0,count).Any(i=>
            Vector3.Distance(vertices[i].Position,shifted[i].Position)>.1f),"Inner mask boundary moves at fixed sword poses");
        check(Enumerable.Range(0,count).Where(i=>i%24==18).All(i=>vertices[i].Position==shifted[i].Position),
            "Animated inner boundary does not move real sword-tip edge");
        check(SoulMaterialTrail.Visibility(0,false,0)==0 && SoulMaterialTrail.Visibility(6,false,0)<SoulMaterialTrail.Visibility(12,false,0),
            "Trail forms gradually with blade visibility");
        check(SoulMaterialTrail.Dissolve(60,false,0)==0 && SoulMaterialTrail.Dissolve(72,false,0)>.4f &&
            SoulMaterialTrail.Visibility(84,false,0)==0 && SoulMaterialTrail.Visibility(30,true,8)==0,
            "Normal return and early retirement finish without stale material");
        var stationary=Enumerable.Repeat(centers[0],centers.Length).ToArray();
        var fixedRotations=Enumerable.Repeat(rotations[0],rotations.Length).ToArray();
        check(SoulMaterialTrail.Build(stationary,fixedRotations,stationary.Length,32,Vector2.Zero,shifted)==0,
            "Stationary poses cannot expand a full material sheet");
        check(SoulMaterialTrail.Build(centers,rotations,centers.Length,32,Vector2.Zero,shifted,float.NaN)==0,
            "Invalid animation time cannot reach GPU geometry");
        centers[4]+=new Vector2(2000,0);
        check(SoulMaterialTrail.Build(centers,rotations,centers.Length,32,Vector2.Zero,vertices)==0,"Material trail rejects teleport discontinuity");
        centers[4]-=new Vector2(2000,0);rotations[4]=float.NaN;
        check(SoulMaterialTrail.Build(centers,rotations,centers.Length,32,Vector2.Zero,vertices)==0,"Material trail rejects invalid rotations");
    }
    private static void CheckUnits(Action<bool,string> check)
    {
        var units=Enumerable.Range(0,SoulTrailUnits.Count).Select(i=>SoulTrailUnits.At(i,60,55)).ToArray();
        check(units.Select(u=>u.Start).SequenceEqual(new[]{0,15,30,45}),"剑影保持原版三个历史采样位置");
        check(units.Select(u=>u.Samples).SequenceEqual(new[]{13,11,9,7})&&
            units.Take(3).All(u=>u.Start+u.Samples-1<units[u.Index+1].Start),"四个剑尾窗口递减且留有严格空隙");
        check(units[0].Opacity==1&&units[1].Opacity==.75f&&units[2].Opacity==.5f&&units[3].Opacity==.125f,
            "短尾与剑影共享亮度及最后一把的渐入窗口");
        var centers=Enumerable.Range(0,60).Select(i=>new Vector2(1000-i*16,200)).ToArray();
        var rotations=Enumerable.Repeat(MathHelper.PiOver4,60).ToArray();
        var vertices=new VertexPositionColorTexture[SoulMaterialTrail.MaxLayeredVertices];
        var layers=units.Select(u=>u.Layer).ToArray();
        int count=SoulMaterialTrail.BuildLayered(centers,rotations,60,32,Vector2.Zero,vertices,layers);
        check(count==(12+10+8+6)*3*4*6,"每个独立窗口都有自己的三角形且不连接窗口");
        int offset=0;bool anchored=true;var ranges=new List<(float Low,float High)>();
        foreach(var unit in units)
        {
            int size=(unit.Samples-1)*3*4*6;
            anchored&=Vector3.Distance(vertices[offset+18].Position,new Vector3(centers[unit.Start]+new Vector2(32,0),0))<.001f;
            // 直接比较完整网格的空间范围，包括亮刃外扩；不假设顶点都落在剑尖连线上。
            ranges.Add((vertices.Skip(offset).Take(size).Min(v=>v.Position.X),vertices.Skip(offset).Take(size).Max(v=>v.Position.X)));
            offset+=size;
        }
        check(anchored&&Enumerable.Range(0,3).All(i=>ranges[i].Low>ranges[i+1].High),
            "四个短尾头点与本体剑影一致且完整网格之间存在空间留白");
        check(SoulTrailUnits.At(3,45,55).Samples==0&&SoulTrailUnits.At(2,31,55).Samples==1,
            "历史不足时不伪造旧剑影或越界读取");
    }
}
