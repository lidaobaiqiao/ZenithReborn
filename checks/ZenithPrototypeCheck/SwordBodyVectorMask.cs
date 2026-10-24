using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace ZenithPrototypeCheck;

// 蒙版制作入口：保留用户认可的 R2；H3 复用同一导出管线生成剑形气流。
internal sealed class SwordBodyVectorMask : IDisposable
{
    // R2 基准：448×256，枢轴305,128；103画布像素对应钛金剑1.85倍显示长度。
    private const float BaselinePivotX=305f,BaselinePivotY=128f;
    private const float BaselineBladeCanvasPx=103f;
    private const float BaselineBladeWorldPx=103f/1.3122883f;
    private const float AtlasPerWorldPx=BaselineBladeCanvasPx/BaselineBladeWorldPx;

    private readonly Texture2D profile;
    private readonly SwordAirflowMask airflow;
    private readonly float bodyWidth,tailWidth,loop,waveStart,waveEnd,waveHeight,frontWidth,backWidth,topPhase,bottomPhase,leadingExtension;
    private readonly Vector2 tip,pivot;
    // 剑形光环模式：蒙版 = 原版剑轮廓放大一圈，只保留边缘那一环
    private readonly bool haloMode;
    private readonly Texture2D bladeTexture;
    private readonly float bloomScale,breath;
    private readonly float edgeAlphaValue;   // 光环边缘不透明度
    // 湍流模式：包络（放大剑形）裁剪剪切层，三区各自生成涡
    private readonly bool turbulenceMode;
    private static Texture2D sharedBlob;     // 64×64 径向高斯斑（所有剑共用一张）
    private static readonly Dictionary<Texture2D,Texture2D> softBlades=new(); // 原贴图 → 柔化轮廓
    // 擦除混合：目标色 ×(1-源Alpha)，用来把放大轮廓的中心挖空
    private static readonly BlendState CutCenter=new()
    {
        ColorSourceBlend=Blend.Zero,ColorDestinationBlend=Blend.InverseSourceAlpha,
        AlphaSourceBlend=Blend.Zero,AlphaDestinationBlend=Blend.InverseSourceAlpha
    };
    // 裁剪到包络：目标色 × 源Alpha，用来让剪切层不超出这把剑的扰动域
    private static readonly BlendState ClipToEnvelope=new()
    {
        ColorSourceBlend=Blend.Zero,ColorDestinationBlend=Blend.SourceAlpha,
        AlphaSourceBlend=Blend.Zero,AlphaDestinationBlend=Blend.SourceAlpha
    };
    private readonly Texture2D softBlade;    // 柔化轮廓（旧方案，保留兜底）
    private readonly Texture2D bladeField;   // 距离场：每把剑自己的真实轮廓 + 贴刃光环
    public Vector2 Tip=>tip;
    public Vector2 Pivot=>pivot;
    public float BladeCanvasPx=>tip.X-pivot.X;
    public SwordBodyVectorMask(GraphicsDevice device,string path,Texture2D blade=null)
    {
        using var json=JsonDocument.Parse(File.ReadAllText(path));var root=json.RootElement;
        if(!root.TryGetProperty("family",out var family)||family.GetString()!="R2")throw new InvalidOperationException("Only the user-preferred R2 recipe family is active. Later hook studies are withdrawn.");
        bool autoFit=root.TryGetProperty("autoFit",out var fit)&&fit.GetBoolean();
        if(autoFit&&blade==null)throw new InvalidOperationException("autoFit recipe requires the vanilla blade texture.");
        loop=root.GetProperty("loopSeconds").GetSingle();leadingExtension=root.GetProperty("leadingExtensionPixels").GetSingle();
        var wave=root.GetProperty("wave");
        Vector2? givenTip=root.TryGetProperty("tip",out var t)?new Vector2(t[0].GetSingle(),t[1].GetSingle()):null;
        Vector2? givenPivot=root.TryGetProperty("pivot",out var pv)?new Vector2(pv[0].GetSingle(),pv[1].GetSingle()):null;
        float? givenBody=root.TryGetProperty("bodyHalfWidth",out var bw)?bw.GetSingle():(float?)null;
        float? givenTail=root.TryGetProperty("tailHalfWidth",out var tw)?tw.GetSingle():(float?)null;
        float? givenWaveStart=wave.TryGetProperty("startX",out var ws)?ws.GetSingle():(float?)null;
        float? givenWaveEnd=wave.TryGetProperty("endX",out var we)?we.GetSingle():(float?)null;
        waveHeight=wave.GetProperty("height").GetSingle();frontWidth=wave.GetProperty("leadingWidth").GetSingle();backWidth=wave.GetProperty("trailingWidth").GetSingle();
        topPhase=wave.GetProperty("topPhase").GetSingle();bottomPhase=wave.GetProperty("bottomPhase").GetSingle();
        pivot=givenPivot??new Vector2(BaselinePivotX,BaselinePivotY);
        tip=givenTip??new Vector2(BaselinePivotX+BaselineBladeCanvasPx,BaselinePivotY);
        bodyWidth=givenBody??20f;tailWidth=givenTail??8f;
        waveStart=givenWaveStart??tip.X-18f;waveEnd=givenWaveEnd??48f;
        if(autoFit)
        {
            // 沿生产绘制相同的对角轴实测原版剑尖与中段宽度，再映射到基准画布。
            var pixels=new Color[blade.Width*blade.Height];blade.GetData(pixels);
            float cx=(blade.Width-1)*.5f,cy=(blade.Height-1)*.5f,reach=1,mid=0;
            for(int i=0;i<pixels.Length;i++)
            {
                if(pixels[i].A<=32)continue;
                float dx=i%blade.Width-cx,dy=i/blade.Width-cy;
                reach=Math.Max(reach,(dx-dy)*.70710678f);
            }
            for(int i=0;i<pixels.Length;i++)
            {
                if(pixels[i].A<=32)continue;
                float dx=i%blade.Width-cx,dy=i/blade.Width-cy;
                float along=(dx-dy)*.70710678f,across=(dx+dy)*.70710678f;
                if(along>=reach*.55f&&along<=reach*.82f)mid=Math.Max(mid,Math.Abs(across));
            }
            float bladeCanvas=reach*1.85f*AtlasPerWorldPx;
            pivot=new(Math.Min(BaselinePivotX,442f-leadingExtension-bladeCanvas),BaselinePivotY);
            tip=new(pivot.X+bladeCanvas,pivot.Y);
            bodyWidth=MathF.Round(mid*1.85f*AtlasPerWorldPx,1);
            tailWidth=MathF.Round(bodyWidth*.4f,1);
            waveStart=tip.X-18f;waveEnd=pivot.X-257f;
        }
        float center=root.GetProperty("centerAlpha").GetSingle(),edge=root.GetProperty("edgeAlpha").GetSingle();
        edgeAlphaValue=edge;
        // 剑形光环模式：形状直接用原版剑的轮廓，放大一圈后挖掉中心，只留边缘环
        haloMode=root.TryGetProperty("shape",out var shape)&&shape.GetString()=="bladeHalo";
        // 湍流模式：放大剑形作为"扰动域包络"，域内按剑尖/剑身/剑尾三区生成气流
        turbulenceMode=root.TryGetProperty("shape",out var shape2)&&shape2.GetString()=="turbulence";
        if(haloMode||turbulenceMode)
        {
            bladeTexture=blade??throw new InvalidOperationException("剑形模式需要原版剑贴图。");
            bloomScale=root.TryGetProperty("bloomScale",out var bloom)?bloom.GetSingle():1.18f;
            breath=root.TryGetProperty("breath",out var b)?b.GetSingle():.03f;
        }
        if(root.TryGetProperty("shape",out var airShape)&&airShape.GetString()=="airflow")
            {
                int canvasWidth=root.GetProperty("canvas")[0].GetInt32();
                float shift=canvasWidth-448;pivot+=new Vector2(shift,0);tip+=new Vector2(shift,0);
                airflow=new SwordAirflowMask(device,blade,pivot,loop,AtlasPerWorldPx*1.85f,canvasWidth,root.TryGetProperty("viscosity",out var sticky)?sticky.GetSingle():.55f);
            }
        sharedBlob=sharedBlob??=CreateBlob(device);
        if(turbulenceMode)
        {
            softBlade=CreateSoftBlade(device,bladeTexture);
            bladeField=CreateBladeField(device,bladeTexture);
        }
        const int samples=256;var colors=new Color[samples];
        for(int i=0;i<samples;i++)
        {
            float v=i/(float)(samples-1),shoulder=MathF.Exp(-MathF.Pow((v-.74f)/.19f,2));
            colors[i]=Color.White*((center+(edge-center)*shoulder)*(1-Smooth(.87f,1,v)));
        }
        profile=new Texture2D(device,1,samples);profile.SetData(colors);
    }
    // 剑形光环：把原版剑贴图放大一圈画出来，再用原尺寸把中心挖空。
    // 剩下的就是一圈严格顺着剑的轮廓走的光环；每帧放大倍数呼吸、外缘轻微蠕动，
    // 十六帧连起来就是边缘在流动。几何形状完全来自剑本身，不发明任何随机形状。
    private void DrawHalo(float time)
    {
        float q=time/loop;q-=MathF.Floor(q);
        float wave=MathF.Sin(q*MathHelper.TwoPi);
        float outer=bloomScale+breath*wave;
        // 贴图像素 → 画布像素：与 R2 基准同一换算（1.85倍游戏尺寸 × 画布倍率）
        float baseScale=AtlasPerWorldPx*1.85f;
        Vector2 at=new(pivot.X,pivot.Y);
        Vector2 origin=bladeTexture.Size()*.5f;
        // 外缘蠕动：两个方向错相，幅度不到1像素，只让轮廓"活"起来
        Vector2 wobble=new(MathF.Sin(q*MathHelper.TwoPi*2f)*.8f,MathF.Cos(q*MathHelper.TwoPi*2f)*.8f);
        var batch=Main.spriteBatch;
        // 第一步：放大轮廓（这一层就是光环的外边界）
        batch.Draw(bladeTexture,at+wobble,null,Color.White*edgeAlphaValue,MathHelper.PiOver4,origin,
            baseScale*outer,SpriteEffects.None,0);
        // 第二步：用原尺寸贴图把中心挖空，只留下放大出来的一圈
        batch.End();
        batch.Begin(SpriteSortMode.Deferred,CutCenter,SamplerState.LinearClamp,
            DepthStencilState.None,RasterizerState.CullNone);
        batch.Draw(bladeTexture,at,null,Color.White,MathHelper.PiOver4,origin,
            baseScale,SpriteEffects.None,0);
        batch.End();
        // 恢复外层期望的 AlphaBlend 状态
        batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,
            DepthStencilState.None,RasterizerState.CullNone);
    }
    // 把剑贴图的 Alpha 做三遍盒式模糊，得到柔和的"气团轮廓"。
    // 直接放大像素画会露出马赛克方块，柔化后才是空气该有的边缘。
    private static Texture2D CreateSoftBlade(GraphicsDevice device,Texture2D source)
    {
        if(softBlades.TryGetValue(source,out var cached))return cached;
        int w=source.Width,h=source.Height;
        var pixels=new Color[w*h];source.GetData(pixels);
        var values=new float[w*h];
        for(int i=0;i<values.Length;i++)values[i]=pixels[i].A/255f;
        for(int pass=0;pass<1;pass++)
        {
            var tmp=new float[values.Length];
            for(int y=0;y<h;y++)
                for(int x=0;x<w;x++)
                {
                    float sum=0;int n=0;
                    for(int dy=-1;dy<=1;dy++)
                        for(int dx=-1;dx<=1;dx++)
                        {
                            int sx=x+dx,sy=y+dy;
                            if(sx<0||sy<0||sx>=w||sy>=h)continue;
                            sum+=values[sy*w+sx];n++;
                        }
                    tmp[y*w+x]=sum/n;
                }
            values=tmp;
        }
        var output=new Color[values.Length];
        for(int i=0;i<values.Length;i++)
            output[i]=new Color((byte)255,(byte)255,(byte)255,(byte)Math.Clamp(values[i]*255f,0,255));
        var texture=new Texture2D(device,w,h);texture.SetData(output);
        string diag=Environment.GetEnvironmentVariable("ZENITH_SWORDBODY_PREVIEW");
        if(!string.IsNullOrEmpty(diag))
        {
            int opaque=0,partial=0;
            foreach(var v in values){if(v>.85f)opaque++;else if(v>.1f)partial++;}
            File.WriteAllText(Path.Combine(diag,"softblade-diag.txt"),
                $"source={w}x{h} opaque={opaque}/{values.Length} partial={partial}/{values.Length}");
            using(var png=File.Create(Path.Combine(diag,"softblade.png"))) texture.SaveAsPng(png,w,h);
        }
        softBlades[source]=texture;
        return texture;
    }
    // 从原版贴图算"距刃口距离场"：每把剑按自己的真实轮廓生成，
    // 放大时被双线性插值，所以既保留真实剑形（含真实剑尖），又不会像素化。
    // 亮度曲线取"离刃口约15%处最亮、向外向内都衰减"——这就是贴着刃口的一层气。
    private static Texture2D CreateBladeField(GraphicsDevice device,Texture2D source)
    {
        int w=source.Width,h=source.Height,n=w*h;
        var pixels=new Color[n];source.GetData(pixels);
        var solid=new bool[n];
        for(int i=0;i<n;i++)solid[i]=pixels[i].A>100;
        var dist=new float[n];
        float maxDist=1f;
        for(int y=0;y<h;y++)
            for(int x=0;x<w;x++)
            {
                int i=y*w+x;
                if(!solid[i]){dist[i]=0f;continue;}
                float best=999f;
                for(int dy=-w;dy<=w;dy++)
                    for(int dx=-w;dx<=w;dx++)
                    {
                        int sx=x+dx,sy=y+dy;
                        bool outside=sx<0||sy<0||sx>=w||sy>=h;
                        if(outside||!solid[sy*w+sx])
                        {
                            float d=MathF.Sqrt(dx*dx+dy*dy);
                            if(d<best)best=d;
                        }
                    }
                dist[i]=best;
                if(best>maxDist)maxDist=best;
            }
        var output=new Color[n];
        for(int i=0;i<n;i++)
        {
            // 只有剑身内部才有值；外部必须严格为 0，否则整块方形都会发光（会变成菱形）
            if(!solid[i]){output[i]=new Color((byte)255,(byte)255,(byte)255,(byte)0);continue;}
            float v=dist[i]/maxDist;
            // 光环带：v≈0.15 最亮，中心(v→1)迅速变淡
            float a=MathF.Exp(-MathF.Pow((v-.15f)/.34f,2f));
            output[i]=new Color((byte)255,(byte)255,(byte)255,(byte)Math.Clamp(a*255f,0,255));
        }
        var texture=new Texture2D(device,w,h);texture.SetData(output);
        string diag=Environment.GetEnvironmentVariable("ZENITH_SWORDBODY_PREVIEW");
        if(!string.IsNullOrEmpty(diag))
        {
            int solidCount=0;foreach(var b in solid)if(b)solidCount++;
            File.WriteAllText(Path.Combine(diag,"field-diag.txt"),$"{w}x{h} solid={solidCount}/{n} maxDist={maxDist:F2}");
            using(var png=File.Create(Path.Combine(diag,"field.png")))texture.SaveAsPng(png,w,h);
        }
        return texture;
    }
    // 生成 64×64 径向高斯斑：画单个涡用，中心最亮、边缘平滑归零
    private static Texture2D CreateBlob(GraphicsDevice device)
    {
        const int n=128;var pixels=new Color[n*n];
        for(int y=0;y<n;y++)
            for(int x=0;x<n;x++)
            {
                float dx=(x+.5f)/n*2f-1f,dy=(y+.5f)/n*2f-1f;
                float r2=dx*dx+dy*dy;
                float a=r2>=1f?0f:MathF.Exp(-r2*2.1f)*(1f-r2)*(1f-r2);
                pixels[y*n+x]=new Color((byte)255,(byte)255,(byte)255,(byte)Math.Clamp(a*255f,0,255));
            }
        var tex=new Texture2D(device,n,n);tex.SetData(pixels);return tex;
    }
    // 画一个涡斑：radius 是半径，alpha 是强度
    private void DrawBlob(Vector2 at,float radius,float alpha)
    {
        if(alpha<=.001f||radius<=.05f)return;
        Main.spriteBatch.Draw(sharedBlob,at,null,Color.White*alpha,0,
            sharedBlob.Size()*.5f,radius*2f/sharedBlob.Width,SpriteEffects.None,0);
    }

    // 湍流模式：一把剑刺进一团空气。
    // 放大剑形是这把剑的"扰动域包络"，剪切层被它裁剪；剑尖涡与尾部尾流按各自物理规律自由延伸。
    //   剑尖：前缘涡（LEV），向前喷出、边飞边散
    //   剑身：Blasius 边界层 δ∝√x，沿刃缘逐渐变厚，叠加 Kelvin-Helmholtz 行进波
    //   剑尾：卡门涡街，左右交替的涡沿尾流后移、逐渐扩散，宽度按 x^(1/3) 增长
    private void DrawTurbulence(float time)
    {
        float q=time/loop;q-=MathF.Floor(q);
        float L=MathF.Max(1f,tip.X-pivot.X);
        float W=MathF.Max(2.5f,bodyWidth);
        var batch=Main.spriteBatch;
        float baseScale=AtlasPerWorldPx*1.85f;   // 贴图像素 → 画布像素
        Vector2 at=new(pivot.X,pivot.Y);          // 贴图中心（画布坐标）

        // 一、气团主体：每把剑自己的真实轮廓距离场。
        // 形状取自原版贴图（真实剑尖、真实护手），贴刃一圈最亮、剑身内部渐淡；
        // 放大由双线性插值完成，所以平滑、不会出现像素马赛克。
        Texture2D field=bladeField??softBlade??bladeTexture;
        Vector2 fieldOrigin=field.Size()*.5f;
        batch.Draw(field,at,null,Color.White*.55f,MathHelper.PiOver4,fieldOrigin,
            baseScale*bloomScale,SpriteEffects.None,0);
        // 外扩一层更淡的气，让刃口外侧有扩散感
        batch.Draw(field,at,null,Color.White*.26f,MathHelper.PiOver4,fieldOrigin,
            baseScale*bloomScale*1.12f,SpriteEffects.None,0);

        // 四、剑身剪切层：沿上下刃缘的行进波，厚度按 √x 增长（Blasius）
        for(float x=pivot.X;x<=tip.X;x+=3.5f)
        {
            float t=Math.Clamp((x-pivot.X)/L,0f,1f);
            float s=1f-t;
            float half=W;   // 实测剑身半宽即刃缘位置
            float delta=L*.028f+L*.06f*MathF.Sqrt(Math.Clamp(s,0f,1f));
            float kh=L*.035f*MathF.Sin(s*16f-q*MathHelper.TwoPi*2f);
            float a=(.55f-.16f*s)*(.72f+.28f*MathF.Sin(s*11f+q*MathHelper.TwoPi));
            // 涡斑半径必须小于它到剑身的距离，否则会糊进剑身、把中心填白
            DrawBlob(new(x,128f-half-delta+kh),delta*1.8f,a);
            DrawBlob(new(x,128f+half+delta-kh),delta*1.8f,a);
        }

        // 五、剑尖前缘涡：从剑尖向前喷出
        for(int i=0;i<3;i++)
        {
            float life=(q+i/3f)%1f;
            float forward=L*.06f+life*L*.26f;
            float r=L*.04f+life*L*.06f;
            float a=(1f-life)*.9f;
            DrawBlob(new(tip.X+forward,128f+MathF.Sin(life*MathHelper.TwoPi+i*1.7f)*L*.03f),r*2.2f,a);
        }

        // 六、剑尾尾流：逐列扫描画成连续锥形，宽度按 k·(x/L)^(1/3) 扩散；
        //     沿 x 的周期性明暗模拟卡门涡街的涡核交替。连续带没有离散斑的硬边。
        float span=MathF.Min(pivot.X-24f,L*3.0f);
        var pixel=TextureAssets.MagicPixel.Value;
        for(float x=pivot.X-3f;x>MathF.Max(24f,pivot.X-span);x-=1f)
        {
            float back=pivot.X-x;
            float u=Math.Clamp(back/span,0f,1f);
            float b=L*.14f+L*.28f*MathF.Pow(u,.34f);
            float swirl=.5f+.5f*MathF.Sin(u*MathHelper.TwoPi*2.2f-q*MathHelper.TwoPi);
            float a=(.34f*(1f-u*.55f))*(.55f+.45f*swirl);
            int top=(int)(128f-b),height=Math.Max(1,(int)(b*2f));
            batch.Draw(pixel,new Rectangle((int)x,top,1,height),Color.White*a);
        }
    }
    public float Crest(float time,float phase)
    {float q=time/loop+phase;q-=MathF.Floor(q);return MathHelper.Lerp(waveStart,waveEnd,q);}
    public void Draw(float time)
    {
        if(airflow!=null){airflow.Draw(time);return;}
        if(turbulenceMode){DrawTurbulence(time);return;}
        if(haloMode){DrawHalo(time);return;}
        float top=Crest(time,topPhase),bottom=Crest(time,bottomPhase);
        float px=pivot.X;
        for(int x=1;x<(int)(tip.X+leadingExtension);x++)
        {
            float blade=Smooth(px-55,px+5,x),end=1-Smooth(tip.X-16,tip.X+leadingExtension,x),tail=Smooth(px-271,px-105,x);
            float half=MathHelper.Lerp(tailWidth*tail,bodyWidth,blade)*end;
            float support=Smooth(px-267,px-195,x)*(1-Smooth(px+45,px+100,x));
            float ripple=1.4f*MathF.Sin(x*.11f+time/loop*MathHelper.TwoPi*5)*support;
            float upper=half+waveHeight*Pulse(x,top)*support+ripple;
            float lower=half+waveHeight*.8f*Pulse(x,bottom)*support-ripple;
            float center=tip.Y+(1-blade)*1.7f*MathF.Sin((time/loop-x/180f)*MathHelper.TwoPi)*tail;
            float opacity=Smooth(px-271,px-135,x)*Smooth(0,9,half);
            if(opacity<=.001f || upper+lower<.2f)continue;
            Main.spriteBatch.Draw(profile,new Vector2(x,center),null,Color.White*opacity,MathHelper.Pi,
                new Vector2(1,0),new Vector2(1,upper/256f),SpriteEffects.None,0);
            Main.spriteBatch.Draw(profile,new Vector2(x,center),null,Color.White*opacity,0,
                Vector2.Zero,new Vector2(1,lower/256f),SpriteEffects.None,0);
        }
    }
    private float Pulse(float x,float at)
    {float distance=x-at,width=distance>0?frontWidth:backWidth;return MathF.Exp(-distance*distance/(width*width));}
    private static float Smooth(float from,float to,float value)=>MathHelper.SmoothStep(0,1,Math.Clamp((value-from)/(to-from),0,1));
    public void Dispose(){profile.Dispose();airflow?.Dispose();bladeField?.Dispose();}
}
