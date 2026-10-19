using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class RibbonCheck
{
    internal static void Run(Action<bool,string> check)
    {
        const int length=SoulRibbonMesh.HistoryLength;
        var centers=new Vector2[length];var rotations=new float[length];
        for(int i=0;i<length;i++)
        {
            float x=800-i*26;
            centers[i]=new Vector2(x,160+.00065f*(x-800)*(x-800));
            rotations[i]=-MathHelper.PiOver4-i*.045f;
        }
        var vertices=new VertexPositionColor[SoulRibbonMesh.MaxVertices];
        int count=SoulRibbonMesh.Build(centers,rotations,length,32,new Color(150,230,75),1,Vector2.Zero,vertices);
        check(count>0 && count%3==0 && count<=vertices.Length,"Ribbon emits bounded triangle geometry");
        check(vertices.Take(count).All(v=>float.IsFinite(v.Position.X) && float.IsFinite(v.Position.Y) &&
            v.Position.X>100 && v.Position.X<880 && v.Position.Y>80 && v.Position.Y<480),
            "Ribbon remains near the weapon path rather than stretching across the screen");
        check(vertices[0].Color.A==0 && vertices[7].Color.A>120,"Ribbon fades across blade width with a clear outer edge");
        check(vertices[count-1].Color.A==0 && vertices[count-2].Color.A==0,"Ribbon tail ends transparent");
        var shifted=new VertexPositionColor[vertices.Length];
        int shiftedCount=SoulRibbonMesh.Build(centers,rotations,length,32,Color.White,1,new Vector2(150,90),shifted);
        check(shiftedCount==count && Vector2.Distance(new Vector2(shifted[7].Position.X,shifted[7].Position.Y),
            new Vector2(vertices[7].Position.X-150,vertices[7].Position.Y-90))<.001f,
            "Camera movement translates the mesh without changing dimensions");
        check(SoulRibbonMesh.Build(centers,rotations,1,32,Color.White,1,Vector2.Zero,shifted)==0,
            "No trail before two samples exist");
        check(SoulRibbonMesh.Build(centers,rotations,length,32,Color.White,0,Vector2.Zero,shifted)==0,
            "Hidden sword emits no ribbon");
        Vector2 saved=centers[3];centers[3]+=new Vector2(2000,0);
        check(SoulRibbonMesh.Build(centers,rotations,length,32,Color.White,1,Vector2.Zero,shifted)==0,
            "Teleport discontinuity cannot draw a screen-wide ribbon");
        centers[3]=saved;
        rotations[3]=float.NaN;
        check(SoulRibbonMesh.Build(centers,rotations,length,32,Color.White,1,Vector2.Zero,shifted)==0,
            "Invalid rotation cannot reach the GPU");
        rotations[3]=-MathHelper.PiOver4-3*.045f;

        string report=Environment.GetEnvironmentVariable("ZENITH_CHECK_REPORT");
        string preview=Path.Combine(Path.GetDirectoryName(report),"soul-ribbon-preview-mesh.json");
        File.WriteAllText(preview,JsonSerializer.Serialize(vertices.Take(count).Select(v=>
            new[]{v.Position.X,v.Position.Y,v.Color.R,v.Color.G,v.Color.B,v.Color.A})));
    }
}
