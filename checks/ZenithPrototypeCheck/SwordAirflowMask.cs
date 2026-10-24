using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace ZenithPrototypeCheck;

// H3 只制作动态 Alpha，不负责着色。复用原版贴图、SpriteBatch 与既有图集导出器。
// 这是按轮廓与流向编排的气流近似，不把几条经验曲线冒充完整流体模拟。
internal sealed class SwordAirflowMask : IDisposable
{
    private readonly int Width;
    private const int Height=256;
    private readonly Texture2D texture;
    private readonly Color[] pixels;
    private readonly float[] distance;
    private readonly float[] upper,lower;
    private readonly bool[] solid;
    private readonly float loop,length,viscosity;
    private readonly int rear,nose;

    public SwordAirflowMask(GraphicsDevice device,Texture2D blade,Vector2 pivot,float loop,float scale,int width=448,float viscosity=.55f)
    {
        this.loop=loop;this.viscosity=viscosity;Width=width;pixels=new Color[Width*Height];distance=new float[Width*Height];
        upper=new float[Width];lower=new float[Width];solid=new bool[Width*Height];
        var native=new Color[blade.Width*blade.Height];blade.GetData(native);
        Array.Fill(upper,float.PositiveInfinity);Array.Fill(lower,float.NegativeInfinity);
        var boundary=new List<Vector2>();int left=Width-1,right=0;
        // 在最终画布上采样真实轮廓，避免放大整张矩形纹理后得到菱形。
        for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
        {
            Vector2 local=(new Vector2(x+.5f,y+.5f)-pivot).RotatedBy(-MathHelper.PiOver4)/scale+blade.Size()*.5f;
            int sx=(int)MathF.Floor(local.X),sy=(int)MathF.Floor(local.Y);
            if(sx<0||sy<0||sx>=blade.Width||sy>=blade.Height||native[sy*blade.Width+sx].A<=100)continue;
            solid[y*Width+x]=true;upper[x]=Math.Min(upper[x],y);lower[x]=Math.Max(lower[x],y);
            left=Math.Min(left,x);right=Math.Max(right,x);
        }
        // 气流轮廓允许约两像素的平滑，消除斜向像素台阶；主体仍由这把剑的剪影决定。
        var coverage=solid.Select(value=>value?1f:0f).ToArray();
        for(int pass=0;pass<2;pass++)
        {
            var next=new float[coverage.Length];
            for(int y=1;y<Height-1;y++)for(int x=1;x<Width-1;x++)
            {
                int i=y*Width+x;
                next[i]=(coverage[i]*4+(coverage[i-1]+coverage[i+1]+coverage[i-Width]+coverage[i+Width])*2+
                    coverage[i-Width-1]+coverage[i-Width+1]+coverage[i+Width-1]+coverage[i+Width+1])/16;
            }
            coverage=next;
        }
        Array.Fill(upper,float.PositiveInfinity);Array.Fill(lower,float.NegativeInfinity);left=Width-1;right=0;
        for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
        {
            int i=y*Width+x;solid[i]=coverage[i]>.4f;
            if(!solid[i])continue;
            upper[x]=Math.Min(upper[x],y);lower[x]=Math.Max(lower[x],y);left=Math.Min(left,x);right=Math.Max(right,x);
        }
        rear=left;nose=right;length=Math.Max(8,nose-pivot.X);
        for(int y=1;y<Height-1;y++)for(int x=1;x<Width-1;x++)
        {
            int i=y*Width+x;
            if(solid[i]&&(!solid[i-1]||!solid[i+1]||!solid[i-Width]||!solid[i+Width]))boundary.Add(new(x,y));
        }
        // 距离场只算一次；空白区保持空白，所有输出均使用预乘白色。
        for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
        {
            int i=y*Width+x;float nearest=10000;
            if(x>=rear-18&&x<=nose+18&&y>55&&y<201)
                foreach(var point in boundary)nearest=Math.Min(nearest,Vector2.DistanceSquared(new(x,y),point));
            distance[i]=MathF.Sqrt(nearest)*(solid[i]?-1:1);
        }
        // 平滑像素阶梯仅用于气流轨迹；剑本体的像素轮廓不作修改。
        var rawUpper=(float[])upper.Clone();var rawLower=(float[])lower.Clone();
        for(int x=rear;x<=nose;x++)
        {
            float top=0,bottom=0;int count=0;
            for(int j=Math.Max(rear,x-2);j<=Math.Min(nose,x+2);j++)
                if(float.IsFinite(rawUpper[j])){top+=rawUpper[j];bottom+=rawLower[j];count++;}
            if(count>0){upper[x]=top/count;lower[x]=bottom/count;}
        }
        texture=new Texture2D(device,Width,Height);
    }

    private static float Bell(float value,float width)=>MathF.Exp(-value*value/(width*width));
    private static float Join(float a,float b)=>1-(1-a)*(1-b);
    public void Draw(float time)
    {
        float phase=(time/loop%1)*MathHelper.TwoPi;
        float wakeLength=Math.Min(rear-14,length*6f);
        for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
        {
            int i=y*Width+x;float a=0;
            if(x>=rear-4&&x<=nose+10)
            {
                float downstream=Math.Clamp((nose-x)/(float)Math.Max(1,nose-rear),0,1);
                float width=1.4f+length*.027f*MathF.Sqrt(downstream);
                // 贴刃薄气沿真实轮廓分布；气流波峰向剑尾行进，内部浓度低。
                // 粘性用较长的行进波与沿程相位滞后来表现，不改变飞剑速度。
                float travel=downstream*MathHelper.TwoPi*(2-viscosity*.6f)-phase;
                float shift=.8f*MathF.Sin(travel);
                a=.66f*Bell(distance[i]-1.2f-shift,width)*(.76f+.24f*MathF.Sin(travel+.7f));
                if(x>=rear&&x<=nose&&float.IsFinite(upper[x]))
                {
                    float edge=1+length*.025f*MathF.Sqrt(downstream);
                    float ripple=length*.018f*MathF.Sin(travel);
                    float top=upper[x]-edge-ripple,bottom=lower[x]+edge+ripple;
                    float rim=.42f*(.68f+.32f*MathF.Sin(travel+1.2f));
                    a=Join(a,rim*Math.Max(Bell(y-top,1.5f+edge*.5f),Bell(y-bottom,1.5f+edge*.5f)));
                }
                // 剑尖是连在真实尖端上的低浓度压缩气层，不额外叠 V 形或向前喷出的光球。
                float noseWeight=Bell(x-nose,length*.1f);
                a=Join(a,.42f*noseWeight*Bell(distance[i]-1.2f,2f)*(.82f+.18f*MathF.Sin(phase*2)));
            }
            if(x<rear+5&&x>rear-wakeLength)
            {
                float back=rear-x,u=Math.Clamp(back/wakeLength,0,1);
                float spread=3.5f+length*.09f*MathF.Pow(u,.333f);
                float center=128+length*.038f*MathF.Sin(u*MathHelper.TwoPi*(2-viscosity)-phase)*u;
                float fade=MathF.Pow(1-u,.9f)*Math.Clamp((back+5)/12,0,1);
                float swirl=.74f+.26f*MathF.Sin(u*MathHelper.TwoPi*(2-viscosity)-phase);
                float wake=.74f*fade*Bell(y-center,spread)*swirl;
                a=Join(a,wake);
            }
            byte alpha=(byte)Math.Clamp(a*255,0,255);
            pixels[i]=new(alpha,alpha,alpha,alpha);
        }
        texture.SetData(pixels);
        Main.spriteBatch.Draw(texture,Vector2.Zero,Color.White);
    }
    public void Dispose()=>texture.Dispose();
}
