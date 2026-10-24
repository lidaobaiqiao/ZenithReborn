using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using ZenithReborn;

namespace ZenithMaterialLab;

internal sealed class LabPreview : IDisposable
{
    internal RenderTarget2D Target {get;private set;}
    private readonly SpriteBatch batch=new(Main.graphics.GraphicsDevice);
    private static readonly Dictionary<int,VertexPositionColorTexture[]> meshes=new();
    internal static VertexPositionColorTexture[] Mesh(int shape)
    {
        if(!meshes.TryGetValue(shape,out var mesh))meshes[shape]=mesh=BuildMesh(shape);
        return mesh;
    }
    private static VertexPositionColorTexture[] BuildMesh(int shape)
    {
        if(shape==2)
        {
            var centers=new Vector2[SoulMaterialTrail.HistoryLength];var rotations=new float[centers.Length];
            for(int i=0;i<centers.Length;i++)
            {float angle=-.65f+i/(float)(centers.Length-1)*3.7f;centers[i]=new Vector2(480,270)+new Vector2(MathF.Cos(angle)*285,MathF.Sin(angle)*140);rotations[i]=angle+MathHelper.PiOver4;}
            var v=new VertexPositionColorTexture[SoulMaterialTrail.MaxVertices];int count=SoulMaterialTrail.Build(centers,rotations,centers.Length,62,Vector2.Zero,v,0);return v.Take(count).ToArray();
        }
        // 细分网格让高度贴图真正改变轮廓；不是仅改变像素明暗。
        var vertices=new List<VertexPositionColorTexture>();int columns=48,rows=32;
        var area=shape==1?new Rectangle(275,65,410,410):new Rectangle(140,110,680,320);
        VertexPositionColorTexture V(int x,int y)=>new(new Vector3(area.X+area.Width*x/(float)columns,area.Y+area.Height*y/(float)rows,0),Color.White,new Vector2(x/(float)columns,y/(float)rows));
        for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
        {vertices.Add(V(x,y));vertices.Add(V(x+1,y));vertices.Add(V(x,y+1));vertices.Add(V(x+1,y));vertices.Add(V(x+1,y+1));vertices.Add(V(x,y+1));}
        return vertices.ToArray();
    }
    internal Color[] Render(SurfaceMaterialDefinition material,SurfaceMaterialRenderer.Inputs inputs,Texture2D background,
        int shape,float clock,bool read=false,int mode=0,float dissolve=0,Color? weaponColor=null)
    {
        var device=Main.graphics.GraphicsDevice;var saved=device.GetRenderTargets();var viewport=device.Viewport;
        if(saved.Any(binding=>binding.RenderTarget is not RenderTarget2D t||t.RenderTargetUsage!=RenderTargetUsage.PreserveContents))return null;
        var usage=device.PresentationParameters.RenderTargetUsage;device.PresentationParameters.RenderTargetUsage=RenderTargetUsage.PreserveContents;
        var textures=Enumerable.Range(0,16).Select(i=>device.Textures[i]).ToArray();var samplers=Enumerable.Range(0,16).Select(i=>device.SamplerStates[i]).ToArray();
        var blend=device.BlendState;var depth=device.DepthStencilState;var raster=device.RasterizerState;
        var scissor=device.ScissorRectangle;var buffers=device.GetVertexBuffers();var indices=device.Indices;
        try
        {
            Target??=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
            for(int slot=0;slot<16;slot++)if(ReferenceEquals(device.Textures[slot],Target))device.Textures[slot]=null;
            device.SetRenderTarget(Target);device.Clear(Color.Transparent);
            if(mode==0){batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque);batch.Draw(background,new Rectangle(0,0,960,540),Color.White);batch.End();}
            var vertices=Mesh(shape);
            SurfaceMaterialRenderer.Draw(vertices,vertices.Length,material,inputs,background,weaponColor??Color.White,1,clock,960,540,true,dissolve,mode,shape);
            if(!read)return null;
            var pixels=new Color[960*540];Target.GetData(pixels);return pixels;
        }
        finally
        {
            device.SetRenderTargets(saved);device.Viewport=viewport;device.PresentationParameters.RenderTargetUsage=usage;
            device.BlendState=blend;device.DepthStencilState=depth;device.RasterizerState=raster;device.ScissorRectangle=scissor;device.SetVertexBuffers(buffers);device.Indices=indices;
            for(int i=0;i<16;i++){device.Textures[i]=textures[i];device.SamplerStates[i]=samplers[i];}
        }
    }
    internal void Save(string path){Directory.CreateDirectory(Path.GetDirectoryName(path));using var file=File.Create(path);Target.SaveAsPng(file,960,540);}
    public void Dispose(){Target?.Dispose();batch.Dispose();}
}
