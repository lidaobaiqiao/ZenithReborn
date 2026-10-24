using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class MaterialTrailCheck
{
    internal static void Run(Action<bool,string> check)
    {
        SwordColorNoiseCheck.Run(check);
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
        check(vertices.Take(count).All(v=>float.IsFinite(v.Position.X) && float.IsFinite(v.TextureCoordinate.X)
            && v.Position.X>250 && v.Position.X<850 && v.Position.Y>90 && v.Position.Y<350),
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
}
