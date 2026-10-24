using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithMaterialLab;

internal static class LabPipelineCheck
{
    internal sealed record Result(bool Passed,bool StateRestored,bool Dissolved,bool AnimatedMask,bool SlowColor,
        double NativeMeanError,int NativeComparedPixels);
    internal static Result Run(string output,LabResources resources,LabPreview preview)
    {
        var device=Main.graphics.GraphicsDevice;var saved=device.GetRenderTargets();var viewport=device.Viewport;
        var usage=device.PresentationParameters.RenderTargetUsage;
        var textures=Enumerable.Range(0,16).Select(i=>device.Textures[i]).ToArray();var samplers=Enumerable.Range(0,16).Select(i=>device.SamplerStates[i]).ToArray();
        var blend=device.BlendState;var depth=device.DepthStencilState;var raster=device.RasterizerState;
        try
        {
            device.PresentationParameters.RenderTargetUsage=RenderTargetUsage.PreserveContents;
            var m=LabChecks.Preset("glass");resources.Reload(m,1);
            using var marker=new Texture2D(device,1,1);marker.SetData(new[]{Color.HotPink});
            device.Textures[15]=marker;device.SamplerStates[15]=SamplerState.PointClamp;device.BlendState=BlendState.Additive;
            var direct=preview.Render(m,resources.Inputs,resources.Background,1,.25f,true);
            bool stateRestored=ReferenceEquals(device.Textures[15],marker)&&ReferenceEquals(device.SamplerStates[15],SamplerState.PointClamp)&&ReferenceEquals(device.BlendState,BlendState.Additive);
            preview.Save(Path.Combine(output,"pipeline-direct.png"));
            var data=new List<Texture2D>();Color[] normal=null;
            try
            {
                for(int mode=1;mode<=4;mode++)
                {
                    var pixels=preview.Render(m,resources.Inputs,resources.Background,1,.25f,true,mode);
                    if(mode==1)normal=pixels;
                    var texture=new Texture2D(device,960,540);texture.SetData(pixels);data.Add(texture);
                    preview.Save(Path.Combine(output,$"pipeline-data-{mode}.png"));
                }
                using var shader=ModContent.Request<Effect>("ZenithReborn/Assets/Effects/ZRGlassScreen",AssetRequestMode.ImmediateLoad).Value.Clone();
                shader.Parameters["uGlassTexel"].SetValue(new Vector2(1f/960,1f/540));
                shader.Parameters["uGlassTransmission"].SetValue(m.Transmission);shader.Parameters["uGlassThickness"].SetValue(m.Thickness);shader.Parameters["uGlassIor"].SetValue(m.Ior);
                shader.Parameters["uMaskReady"].SetValue(1f);shader.Parameters["uHasMaterialProperties"].SetValue(1f);
                shader.Parameters["uMaterialProperties"].SetValue(data[2]);shader.Parameters["uMaterialTransmission"].SetValue(data[3]);
                using var target=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
                using var batch=new SpriteBatch(device);device.SetRenderTarget(target);device.Clear(Color.Transparent);
                device.Textures[1]=data[0];device.Textures[2]=data[1];device.SamplerStates[1]=device.SamplerStates[2]=SamplerState.LinearClamp;
                batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,shader);
                batch.Draw(resources.Background,new Rectangle(0,0,960,540),Color.White);batch.End();
                var native=new Color[960*540];target.GetData(native);
                using(var file=File.Create(Path.Combine(output,"pipeline-native.png")))target.SaveAsPng(file,960,540);
                long difference=0;int compared=0;
                for(int i=0;i<native.Length;i++)if(normal[i].A>250)
                {difference+=Math.Abs(native[i].R-direct[i].R)+Math.Abs(native[i].G-direct[i].G)+Math.Abs(native[i].B-direct[i].B);compared++;}
                double error=difference/(double)Math.Max(1,compared*3);
                device.SetRenderTargets(saved);device.Viewport=viewport;
                var ended=preview.Render(m,resources.Inputs,resources.Background,1,.25f,true,dissolve:1);
                var background=new Color[960*540];resources.Background.GetData(background);
                bool dissolved=ended.SequenceEqual(background);
                // 在固定几何和固定底色下，仅切换真实蒙版图集帧。
                string atlas=Path.Combine(resources.DirectoryPath,".lab-check-mask.png");
                using(var texture=new Texture2D(device,256,128))
                {
                    var pixels=new Color[256*128];for(int y=0;y<128;y++)for(int x=0;x<256;x++)
                        pixels[y*256+x]=Color.White*((x<128?x<64:x>192)?1:0);
                    texture.SetData(pixels);using var file=File.Create(atlas);texture.SaveAsPng(file,256,128);
                }
                m.Assets.MotionMask=".lab-check-mask.png";m.Assets.Frames=2;m.Assets.Columns=2;resources.Reload(m,1);
                var maskA=preview.Render(m,resources.Inputs,resources.Background,1,.05f,true);
                var maskB=preview.Render(m,resources.Inputs,resources.Background,1,.55f,true);
                bool animated=!maskA.SequenceEqual(maskB);
                m.Assets.MotionMask="";m.Assets.Frames=m.Assets.Columns=1;m.Assets.ColorField="color.png";resources.Reload(m,1);
                var colorA=preview.Render(m,resources.Inputs,resources.Background,1,0,true);
                var colorB=preview.Render(m,resources.Inputs,resources.Background,1,9,true);
                bool slowColor=!colorA.SequenceEqual(colorB);
                return new(stateRestored&&dissolved&&animated&&slowColor&&compared>10000&&error<5,
                    stateRestored,dissolved,animated,slowColor,error,compared);
            }
            finally{foreach(var texture in data)texture.Dispose();}
        }
        finally
        {
            device.SetRenderTargets(saved);device.Viewport=viewport;device.PresentationParameters.RenderTargetUsage=usage;
            device.BlendState=blend;device.DepthStencilState=depth;device.RasterizerState=raster;
            for(int i=0;i<16;i++){device.Textures[i]=textures[i];device.SamplerStates[i]=samplers[i];}
        }
    }
}
