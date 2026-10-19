using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace ZenithReborn;

// Draw mesh ribbons once between tile and entity passes. The projectile sprite
// batch is never interrupted, and sword sprites are drawn on top afterwards.
public class SoulVisuals : ModSystem
{
    private BasicEffect ribbonEffect;
    private readonly VertexPositionColor[] vertices=new VertexPositionColor[SoulRibbonMesh.MaxVertices];

    public static Color Tint(int itemType)=>WeaponFamilies.Of(itemType) switch {
        BladeFamily.Swift=>new Color(125,225,255),
        BladeFamily.Solid=>new Color(245,205,90),
        BladeFamily.Frost=>new Color(115,195,255),
        BladeFamily.Nature=>new Color(150,230,75),
        BladeFamily.Arcane=>new Color(190,145,255),
        _=>new Color(135,215,255)
    };
    public override void PostDrawTiles()
    {
        if(Main.dedServ || Main.gameMenu) return;
        // Allocate GPU resources only on the drawing thread, never on a server.
        GraphicsDevice device=Main.graphics.GraphicsDevice;
        BlendState oldBlend=device.BlendState;
        DepthStencilState oldDepth=device.DepthStencilState;
        RasterizerState oldRasterizer=device.RasterizerState;
        bool drew=false;
        try
        {
            foreach(Projectile projectile in Main.ActiveProjectiles)
            {
                if(projectile.ModProjectile is not SoulBlade blade) continue;
                int count=blade.BuildRibbon(vertices);
                if(count==0) continue;
                ribbonEffect??=new BasicEffect(device){VertexColorEnabled=true,TextureEnabled=false,LightingEnabled=false};
                ribbonEffect.World=Main.GameViewMatrix.TransformationMatrix;
                ribbonEffect.View=Matrix.Identity;
                ribbonEffect.Projection=Matrix.CreateOrthographicOffCenter(0,Main.screenWidth,Main.screenHeight,0,-1,1);
                device.BlendState=BlendState.AlphaBlend;
                device.DepthStencilState=DepthStencilState.None;
                device.RasterizerState=RasterizerState.CullNone;
                drew=true;
                foreach(EffectPass pass in ribbonEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleList,vertices,0,count/3);
                }
            }
        }
        finally
        {
            if(drew) Main.pixelShader.CurrentTechnique.Passes[0].Apply();
            device.BlendState=oldBlend;
            device.DepthStencilState=oldDepth;
            device.RasterizerState=oldRasterizer;
        }
    }
    public override void Unload()
    {
        BasicEffect old=ribbonEffect;ribbonEffect=null;
        if(old!=null) Main.QueueMainThreadAction(old.Dispose);
    }
}
