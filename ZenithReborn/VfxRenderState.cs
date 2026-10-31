using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace ZenithReborn;

// 统一保护特效绘制前后的显卡状态，供各自独立的绘制入口调用。
public static class VfxRenderState
{
    public static void DrawWithRestoredState(Action draw)
    {
        var device=Main.graphics.GraphicsDevice;
        var blend=device.BlendState;var depth=device.DepthStencilState;var raster=device.RasterizerState;
        var scissor=device.ScissorRectangle;
        var samplers=Enumerable.Range(0,3).Select(i=>device.SamplerStates[i]).ToArray();
        var textures=Enumerable.Range(0,3).Select(i=>device.Textures[i]).ToArray();
        var vertices=device.GetVertexBuffers();var indices=device.Indices;
        try
        {
            device.BlendState=BlendState.Additive;
            device.DepthStencilState=DepthStencilState.None;
            draw();
        }
        finally
        {
            Main.pixelShader.CurrentTechnique.Passes[0].Apply();
            device.BlendState=blend;device.DepthStencilState=depth;device.RasterizerState=raster;
            device.ScissorRectangle=scissor;
            for(int i=0;i<3;i++){device.SamplerStates[i]=samplers[i];device.Textures[i]=textures[i];}
            device.SetVertexBuffers(vertices);device.Indices=indices;
        }
    }
}
