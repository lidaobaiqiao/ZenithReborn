using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ZenithReborn;

// Pure geometry, shared by the renderer and headless regression checks.
public static class SoulRibbonMesh
{
    public const int HistoryLength = 24;
    private const int Subdivisions = 4;
    public const int MaxVertices = (HistoryLength-1)*Subdivisions*18;

    public static int Build(Vector2[] centers,float[] rotations,int count,float halfLength,
        Color tint,float visibility,Vector2 screenPosition,VertexPositionColor[] vertices)
    {
        count=Math.Min(count,Math.Min(HistoryLength,Math.Min(centers.Length,rotations.Length)));
        if(count<2 || !float.IsFinite(halfLength) || !float.IsFinite(visibility) || visibility<=0) return 0;
        if(vertices.Length<(count-1)*Subdivisions*18) throw new ArgumentException("Ribbon vertex buffer too small");
        for(int i=0;i<count;i++)
        {
            if(!float.IsFinite(centers[i].X) || !float.IsFinite(centers[i].Y) || !float.IsFinite(rotations[i])) return 0;
            if(i>0 && Vector2.DistanceSquared(centers[i],centers[i-1])>90000) return 0;
        }
        halfLength=Math.Clamp(halfLength,6,70);
        visibility=Math.Clamp(visibility,0,1);
        int written=0;
        Section previous=Sample(centers[0],rotations[0],0,halfLength,tint,visibility,screenPosition);
        for(int segment=0;segment<count-1;segment++)
        {
            for(int step=1;step<=Subdivisions;step++)
            {
                float f=step/(float)Subdivisions;
                Vector2 center=Vector2.CatmullRom(centers[Math.Max(0,segment-1)],centers[segment],
                    centers[segment+1],centers[Math.Min(count-1,segment+2)],f);
                float rotation=rotations[segment]+MathHelper.WrapAngle(rotations[segment+1]-rotations[segment])*f;
                float progress=(segment+f)/(count-1);
                Section current=Sample(center,rotation,progress,halfLength,tint,visibility,screenPosition);
                for(int band=0;band<3;band++)
                {
                    VertexPositionColor a=previous.Vertex(band),b=previous.Vertex(band+1);
                    VertexPositionColor c=current.Vertex(band),d=current.Vertex(band+1);
                    vertices[written++]=a;vertices[written++]=b;vertices[written++]=c;
                    vertices[written++]=b;vertices[written++]=d;vertices[written++]=c;
                }
                previous=current;
            }
        }
        return written;
    }

    private static Section Sample(Vector2 center,float rotation,float progress,float halfLength,
        Color tint,float visibility,Vector2 screenPosition)
    {
        Vector2 axis=new(MathF.Cos(rotation-MathHelper.PiOver4),MathF.Sin(rotation-MathHelper.PiOver4));
        float remaining=1-progress;
        float taper=MathF.Pow(remaining,.55f);
        float opacity=MathF.Pow(remaining,1.6f)*visibility;
        Vector2 tip=center-screenPosition+axis*halfLength;
        Vector2 inner=tip-axis*(halfLength*1.65f*taper);
        // The blade tip traces the bright edge; the inside and very outside
        // fade to transparent. No bloom, expanding ring or flash is drawn.
        return new Section(
            new VertexPositionColor(new Vector3(inner,0),Color.Transparent),
            new VertexPositionColor(new Vector3(Vector2.Lerp(inner,tip,.55f),0),tint*(.18f*opacity)),
            new VertexPositionColor(new Vector3(tip,0),Color.Lerp(tint,Color.White,.12f)*(.65f*opacity)),
            new VertexPositionColor(new Vector3(tip+axis*(1.5f*taper),0),Color.Transparent));
    }

    private readonly record struct Section(VertexPositionColor Inner,VertexPositionColor Middle,
        VertexPositionColor Edge,VertexPositionColor Outer)
    {
        public VertexPositionColor Vertex(int row)=>row switch {0=>Inner,1=>Middle,2=>Edge,_=>Outer};
    }
}
