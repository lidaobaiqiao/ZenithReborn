using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace ZenithReborn;

// 仅用于绘制的状态，不写入剑魂存档，也不参与战斗或容量计算。
public partial class SoulUI
{
    private static Texture2D glowTexture,shardTexture;
    private static int[] lastSlotType=Enumerable.Repeat(-1,17).ToArray();
    private static double[] slotChangedAt=new double[17];
    private static float[] slotLit=new float[17];
    private static double openedAt,pageStarted,lastDrawAt;
    private static double UiTime => System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
    private static float Ease(float t) { t=MathHelper.Clamp(t,0,1);return t*t*(3-2*t); }
    private static float PageFade => Ease((float)(UiTime-pageStarted)/.24f);
    // 短视口滚动完整剑环，不把剑位缩到难以点击；正常视口显示完整圆环。
    private static int BaseRingCanvasHeight=>Math.Clamp(ContentBounds().Height,402,DomainMode?580:498);
    private static int RingCanvasHeight => BaseRingCanvasHeight+((Math.Max(0,(editing?.VisibleSupportCount??5)-5)+
        Math.Max(1,Math.Min(5,(RingViewport.Width-12)/58))-1)/Math.Max(1,Math.Min(5,(RingViewport.Width-12)/58)))*58+
        ((editing?.VisibleSupportCount??5)>5?32:0);
    public static float RingRadius => Math.Max(104,Math.Min((BaseRingCanvasHeight-188)*.5f,(RingViewport.Width-80)*.5f-62));
    public static Vector2 RingCenter => new(RingViewport.Center.X,ContentBounds().Y+BaseRingCanvasHeight*.5f-scroll);
    public static Vector2 OrbitPoint(float degrees,float radius) => RingCenter+MathHelper.ToRadians(degrees).ToRotationVector2()*radius;

    private static void EnsureEffects()
    {
        if(glowTexture!=null)return;
        var device=Main.graphics.GraphicsDevice;
        const int n=64;
        Color[] pixels=new Color[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            float d=Vector2.Distance(new(x,y),new(31.5f,31.5f))/31.5f;
            pixels[y*n+x]=Color.White*MathF.Pow(Math.Max(0,1-d),2.4f);
        }
        glowTexture=new Texture2D(device,n,n);glowTexture.SetData(pixels);
        // 一次生成按切面着色的碎晶轮廓，用于剑环导航装饰。
        const int w=36,h=52;
        pixels=new Color[w*h];
        Vector2[] outline=[new(13,1),new(29,18),new(23,31),new(15,51),new(7,30),new(11,20)];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            bool inside=false;
            for(int i=0,j=outline.Length-1;i<outline.Length;j=i++)
                if((outline[i].Y>y)!=(outline[j].Y>y) && x<(outline[j].X-outline[i].X)*(y-outline[i].Y)/(outline[j].Y-outline[i].Y)+outline[i].X) inside=!inside;
            if(inside)
            {
                float face=x<15?.95f:x<20?.58f:.78f;
                if(Math.Abs(x-(13+y*.05f))<1.4f)face=1;
                pixels[y*w+x]=Color.White*face;
            }
        }
        shardTexture=new Texture2D(device,w,h);shardTexture.SetData(pixels);
    }
    private static void Glow(Vector2 at,float radius,Color color,float amount)
    {
        Main.spriteBatch.Draw(glowTexture,at,null,color*amount,0,new(32),radius/32,SpriteEffects.None,0);
    }
    private static void Line(Vector2 a,Vector2 b,Color color,float width=1)
    {
        Vector2 v=b-a;
        // 本机原版像素纹理未必为一乘一，画线时显式指定单个像素。
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,a,new Rectangle(0,0,1,1),color,
            v.ToRotation(),new(0,.5f),new Vector2(v.Length(),width),SpriteEffects.None,0);
    }
    private static void Arc(Vector2 center,float radius,float start,float length,Color color,float width=1)
    {
        int segments=Math.Max(4,(int)(Math.Abs(length)*radius/7));
        Vector2 prev=center+start.ToRotationVector2()*radius;
        for(int i=1;i<=segments;i++)
        {
            float angle=start+length*i/segments;
            Vector2 next=center+angle.ToRotationVector2()*radius;
            Line(prev,next,color,width);prev=next;
        }
    }
    private static void CenterText(string value,Vector2 at,Color color,float scale)
    {
        Text(value,at-new Vector2(FontAssets.MouseText.Value.MeasureString(value).X*scale*.5f,0),color,scale);
    }
    private static string Fit(string text,float width,float scale)
    {
        if(FontAssets.MouseText.Value.MeasureString(text).X*scale<=width)return text;
        while(text.Length>0 && FontAssets.MouseText.Value.MeasureString(text+"…").X*scale>width)text=text[..^1];
        return text+"…";
    }
    private static void DrawNavigation(bool orbital)
    {
        if(!orbital){DrawInformationNavigation();return;}
        EnsureEffects();
        float time=(float)(UiTime%3600);Point mouse=new(Main.mouseX,Main.mouseY);
        foreach(int i in NavigationPages)
        {
            Rectangle r=TabBounds(i);
            bool hover=r.Contains(mouse) && (!orbital || ContentBounds().Contains(mouse));
            bool active=page==i;Color c=SoulUIPages.Color(i);
            Vector2 at=orbital?r.Center.ToVector2():new(r.X+20,r.Center.Y);
            float pulse=.8f+.2f*MathF.Sin(time*2.4f+i);
            Glow(at,active||hover?37:26,c,(active?.34f:hover?.25f:.10f)*pulse);
            float tilt=MathF.Sin(time*.9f+i*1.7f)*.12f+(active?.20f+(1-PageFade)*.8f:0);
            Main.spriteBatch.Draw(shardTexture,at,null,c*(active||hover?1:.58f),tilt,
                new(18,26),orbital?.7f:.58f,SpriteEffects.None,0);
            // 光片缓慢掠过晶体；选中的碎片用流动光环提示。
            if(active||hover)
            {
                Arc(at,orbital?25:21,time*.5f,MathHelper.Pi*1.4f,c*.6f);
                Vector2 spark=at+(time*.9f+i).ToRotationVector2()*(orbital?28:23);
                Glow(spark,5,c,.7f);
            }
            if(orbital)
            {
                string[] shortNames=["寄宿","特性","成长","环境"];
                CenterText(shortNames[SoulUIPages.Index(i)],at+new Vector2(i<=1?39:-39,-8),active?c:Muted,.57f);
            }
            if(hover)
            {
                Main.HoverItem=new Terraria.Item();Main.hoverItemName="天顶碎片 · "+SoulUIPages.Name(i);
                if(orbital)CenterText(SoulUIPages.Name(i),new(RingCenter.X,RingCenter.Y+63),c,.62f);
            }
        }
    }
    private static void DrawSoulRing(SoulItem soul,Rectangle body)
    {
        EnsureEffects();
        double now=UiTime;
        float dt=(float)Math.Clamp(now-lastDrawAt,0,.05);lastDrawAt=now;
        float time=(float)(now%3600),opening=Ease((float)(now-openedAt)/.65f);
        Vector2 center=RingCenter;float radius=RingRadius;
        Glow(center,radius+60,Accent,.05f*opening);
        Arc(center,radius*(.88f+.12f*opening),0,MathHelper.TwoPi,Accent*.23f*opening);
        Arc(center,radius-17,0,MathHelper.TwoPi,Accent*.07f*opening);
        for(int a=0;a<3;a++)
        {
            float angle=time*.13f+a*MathHelper.TwoPi/3;
            Arc(center,radius+9,angle,.58f,Accent*.38f*opening,1.4f);
            Glow(center+(angle+.58f).ToRotationVector2()*(radius+9),5,Accent,.55f*opening);
        }
        int supportCount=editing?.VisibleSupportCount??5;
        Arc(center,radius+62,MathHelper.ToRadians(supportCount<=6?4:-10),
            MathHelper.ToRadians(Math.Min(11,supportCount)*20-12),new Color(185,171,234)*.24f*opening);
        Arc(center,radius+62,MathHelper.ToRadians(-151),MathHelper.ToRadians(40),Champagne*.28f*opening);
        Point mouse=new(Main.mouseX,Main.mouseY);
        foreach(int i in soul.VisibleSlotIds)
        {
            if(i==SoulItem.LegendarySlotId)
            {
                DrawLegendarySlot(soul,i,SlotBounds(i),body,mouse,time,opening);
                continue;
            }
            if(i>=lastSlotType.Length)
            {
                int old=lastSlotType.Length;Array.Resize(ref lastSlotType,i+1);Array.Fill(lastSlotType,-1,old,i+1-old);
                Array.Resize(ref slotChangedAt,i+1);Array.Resize(ref slotLit,i+1);
            }
            Rectangle r=SlotBounds(i);Vector2 at=r.Center.ToVector2();var item=soul.ItemAt(i);bool unlocked=Available(soul,i);
            Color family=i<10?Accent:SoulItem.SupportOrdinal(i)>=0?new(185,171,234):Champagne;
            Color color=unlocked?family:new Color(78,89,108);
            bool lit=!item.IsAir&&unlocked;
            int signature=HashCode.Combine(item.type,item.prefix,unlocked);
            if(lastSlotType[i]!=signature)
            {
                // 首次显示逐步显现；后续更换或重放同一把剑均产生一次独立扩散环。
                slotChangedAt[i]=lastSlotType[i]==-1?now-.15:now;
                lastSlotType[i]=signature;
            }
            slotLit[i]=MathHelper.Lerp(slotLit[i],lit?1:0,1-MathF.Exp(-dt*9));
            float appear=Ease(((float)(now-openedAt)-i*.018f)/.38f);
            float pulse=.78f+.22f*MathF.Sin(time*2+i*.71f);
            bool hover=body.Contains(mouse)&&r.Contains(mouse);
            if(lit)color=Color.Lerp(color,SoulVisuals.Tint(item.type),.42f);
            Line(center+(at-center)*.75f,at,color*(lit?.16f:.035f)*appear);
            Glow(at,r.Width*(.67f+slotLit[i]*.24f),color,(.07f+slotLit[i]*.42f*pulse+(hover?.12f:0))*appear);
            if(lit)Glow(at,r.Width*.95f,new Color(color.R,color.G,color.B,0),(.28f+.16f*pulse)*appear);
            // 局部暗节点保持武器在世界背景前的可读性。
            Glow(at,r.Width*.68f,new Color(6,12,24),.95f*appear);
            float nodeRadius=r.Width*.5f;
            Arc(at,nodeRadius,.14f,MathHelper.TwoPi-.28f,color*(hover?.9f:.22f+slotLit[i]*.36f)*appear,hover?1.7f:1);
            if(selected==i)Arc(at,nodeRadius+3,time*.65f,MathHelper.Pi*1.5f,Color.Gold*.65f*appear);
            if(!item.IsAir)
            {
                Main.instance.LoadItem(item.type);
                var texture=TextureAssets.Item[item.type].Value;
                var frame=Main.itemAnimations[item.type]?.GetFrame(texture)??texture.Frame();
                float scale=Math.Min(1,(r.Width-14f)/Math.Max(frame.Width,frame.Height));
                Main.spriteBatch.Draw(texture,at,frame,(unlocked?Color.White:Color.Gray)*appear,
                    0,frame.Size()*.5f,scale,SpriteEffects.None,0);
                if(lit)
                {
                    float age=(float)(now-slotChangedAt[i]);
                    if(age<.75f)Arc(at,nodeRadius+age*30,0,MathHelper.TwoPi,color*(1-age/.75f)*.8f,2);
                    float phase=time*.7f+i*2.1f;
                    Vector2 mote=at+phase.ToRotationVector2()*(nodeRadius+4);
                    Glow(mote,4,color,.65f*pulse*appear);
                }
            }
            else CenterText(unlocked?"+":"·",at-new Vector2(0,9),color*(.55f+.15f*pulse)*appear,.75f);
            string number=i<10?$"{i+1:00}":SoulItem.SupportOrdinal(i)>=0?$"S{SoulItem.SupportOrdinal(i)+1}":$"R{i-14}";
            CenterText(number,new(at.X,at.Y+nodeRadius-1),color*appear,.45f);
            if(hover)
            {
                if(!DomainMode)selected=i;
                HoveredSlotRequirement=SoulProgress.SlotHint(soul,i);
                // 中央区域负责详情；原版模组武器提示留给背包，避免遮住整个剑环。
                Main.HoverItem=new Terraria.Item();Main.hoverItemName="";
            }
        }
        float textWidth=Math.Min(202,2*radius-70);
        CenterText(soul.Awakened?"众 剑 归 位":"天 顶 剑 环",new(center.X,center.Y-62),Ink,.78f);
        CenterText($"主攻 {soul.Active.Count()}/{soul.Capacity+soul.BonusCapacity+(soul.LegendaryOn?1:0)}  ·  支援 {soul.Supports.Count()}/{soul.SupportCapacity}",
            new(center.X,center.Y-35),Accent,textWidth<170?.52f:.62f);
        Line(center+new Vector2(-textWidth*.45f,-8),center+new Vector2(textWidth*.45f,-8),Accent*.22f);
        var chosen=soul.ItemAt(selected);
        CenterText(Fit(chosen.IsAir?SlotName(selected):chosen.Name,textWidth,.72f),new(center.X,center.Y+2),Color.Gold,.72f);
        CenterText(chosen.IsAir?Available(soul,selected)?"拿起武器 · 点击寄宿":"尚未解锁 · 成长页查看":
            Available(soul,selected)?!soul.Attached?"预览寄宿 · 附着后启用":SoulItem.SupportOrdinal(selected)>=0?"支援特性 · 不参与出剑":"独立出剑 · 特性生效":"效果暂停 · 空手可取回",
            new(center.X,center.Y+27),Muted,textWidth<170?.51f:.6f);
        if(!soul.Attached)CenterText("尚未获得祝福「附着」",new(center.X,center.Y+46),Color.LightSalmon,.52f);
        else if(!chosen.IsAir)CenterText($"面板伤害 {chosen.damage} · 暴击 {chosen.crit}%",new(center.X,center.Y+46),Muted,.51f);
        if(!DomainMode)DrawNavigation(true);
        // 分组标注放在画布空角，避开所有环形命中区域。
        GroupCaption("全收集奖励 · 真实剑位",new(body.X+8,body.Y+16-scroll),Champagne);
        GroupCaption("支援剑位 · 仅特性",new(body.Right-148,body.Y+RingCanvasHeight-30-scroll),new(185,171,234));
    }
    private static void DrawLegendarySlot(SoulItem soul,int index,Rectangle r,Rectangle body,Point mouse,float time,float opening)
    {
        Vector2 at=r.Center.ToVector2();
        Item item=soul.ItemAt(index);
        bool on=soul.LegendaryOn;
        bool hover=body.Contains(mouse)&&r.Contains(mouse);
        Color gold=new(246,210,120);
        Color color=on?gold:new Color(112,98,72);
        float pulse=.8f+.2f*MathF.Sin(time*2.4f+1.3f);
        float appear=opening;
        // 沿用剑环节点风格，用金色光环、旋转弧线和星点标识难度专属剑位。
        Glow(at,r.Width*1.55f,gold,(on?.20f:.06f)*pulse*appear);
        for(int k=0;k<2;k++)
        {
            float a=time*(k==0?.32f:-.23f)+k*MathHelper.Pi;
            Arc(at,r.Width*.62f,a,.85f,gold*(on?.55f:.14f)*appear,1.7f);
        }
        for(int k=0;k<4;k++)
        {
            float a=time*.24f+k*MathHelper.PiOver2;
            Glow(at+a.ToRotationVector2()*(r.Width*.57f),3.5f,gold,(on?.55f:.10f)*pulse*appear);
        }
        Glow(at,r.Width*.70f,new Color(10,8,4),.95f*appear);
        Arc(at,r.Width*.5f,.12f,MathHelper.TwoPi-.24f,color*(hover?.95f:.48f)*appear,hover?2f:1.2f);
        if(!item.IsAir)
        {
            Main.instance.LoadItem(item.type);
            var texture=TextureAssets.Item[item.type].Value;
            var frame=Main.itemAnimations[item.type]?.GetFrame(texture)??texture.Frame();
            float scale=Math.Min(1f,(r.Width-16f)/Math.Max(frame.Width,frame.Height));
            Main.spriteBatch.Draw(texture,at,frame,(on?Color.White:Color.Gray)*appear,
                0,frame.Size()*.5f,scale,SpriteEffects.None,0);
        }
        else CenterText(on?"+":"·",at-new Vector2(0,9),color*(.6f+.15f*pulse)*appear,.8f);
        CenterText("L",new(at.X,at.Y+r.Width*.5f-1),color*appear,.44f);
        if(hover)
        {
            if(!DomainMode)selected=index;
            HoveredSlotRequirement=SoulProgress.SlotHint(soul,index);
            if(!item.IsAir){Main.HoverItem=item.Clone();Main.hoverItemName=item.Name;}
            else Main.hoverItemName=HoveredSlotRequirement;
        }
    }
}
