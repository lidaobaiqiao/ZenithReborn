using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;

namespace ZenithReborn;

// 信息页的视觉组件与短时动画。这里不保存剑魂进度，不修改战斗和寄宿状态。
public partial class SoulUI
{
    private static readonly Color InfoPanel=new(19,25,35),InfoQuiet=new(141,155,176),InfoWhite=new(234,239,246);
    private static readonly Dictionary<string,(float Value,double Time)> infoMotion=[];
    private static double detailStarted;
    private static int environmentFocus=1;
    private static void ResetInformation()
    {infoMotion.Clear();traitSelection="";traitListScroll=traitDetailScroll=environmentGridScroll=environmentDetailScroll=0;environmentFocus=1;detailStarted=0;activeTraitsOnly=true;environmentHelp=false;memoryTab=growthScroll=growthContentHeight=0;}
    private static float DetailReveal=>.2f+.8f*SoulInfoLayout.Ease((float)(UiTime-detailStarted)/.2f);
    private static float InfoHover(string key,bool target)
    {
        double now=UiTime;var previous=infoMotion.GetValueOrDefault(key,(0f,now));
        float next=SoulInfoLayout.Approach(previous.Item1,target?1:0,(float)(now-previous.Item2));
        infoMotion[key]=(next,now);return next;
    }
    private static bool InfoVisible(Rectangle r)=>r.Intersects(ContentBounds());
    private static bool InfoHovered(Rectangle r)=>r.Contains(Main.mouseX,Main.mouseY)&&ContentBounds().Contains(Main.mouseX,Main.mouseY);
    private static bool InfoPressed(Rectangle r)
    {
        if(!InfoHovered(r)||!Main.mouseLeft||!Main.mouseLeftRelease)return false;
        Main.mouseLeftRelease=false;return true;
    }
    private static int InfoTitle(Rectangle b,string title,string caption,SoulGlyph icon,Color color)
    {
        Glyph(icon,new(b.X+23,b.Y+24),24,color);
        Text(title,new(b.X+46,b.Y+7),InfoWhite,1.2f);
        float labelWidth=FontAssets.MouseText.Value.MeasureString(title).X*1.2f;
        if(b.Width>labelWidth+360)Text(caption,new(b.X+labelWidth+68,b.Y+17),InfoQuiet,.66f);
        Line(new(b.X+12,b.Y+49),new(b.Right-12,b.Y+49),color*.18f);
        return b.Y+62;
    }
    // 列表与详情各自裁剪和滚动，切换条目不会把另一侧推出视口。
    private static void InfoClip(Rectangle bounds,Action draw)
    {
        if(bounds.Width<=0||bounds.Height<=0)return;
        var device=Main.graphics.GraphicsDevice;Rectangle previous=device.ScissorRectangle;
        Vector2 top=Vector2.Transform(bounds.Location.ToVector2(),Main.UIScaleMatrix);
        Vector2 end=Vector2.Transform(new Vector2(bounds.Right,bounds.Bottom),Main.UIScaleMatrix);
        Main.spriteBatch.End();device.ScissorRectangle=Rectangle.Intersect(previous,new((int)top.X,(int)top.Y,(int)Math.Ceiling(end.X-top.X),(int)Math.Ceiling(end.Y-top.Y)));
        Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,Main.DefaultSamplerState,DepthStencilState.None,clipState,null,Main.UIScaleMatrix);
        try{draw();}
        finally
        {
            Main.spriteBatch.End();device.ScissorRectangle=previous;
            Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,Main.DefaultSamplerState,DepthStencilState.None,clipState,null,Main.UIScaleMatrix);
        }
    }
    private static int InfoScroll(Rectangle bounds,int offset,int height)
    {
        if(InfoHovered(bounds)&&PlayerInput.ScrollWheelDelta!=0)offset-=PlayerInput.ScrollWheelDelta/120*54;
        return Math.Clamp(offset,0,Math.Max(0,height-bounds.Height));
    }
    private static void InfoScrollMark(Rectangle bounds,int offset,int height,Color tint)
    {
        if(height<=bounds.Height)return;
        int track=Math.Max(1,bounds.Height-12),size=Math.Min(track,Math.Max(18,track*bounds.Height/height));
        int y=bounds.Y+6+(track-size)*offset/Math.Max(1,height-bounds.Height);
        Box(new(bounds.Right-3,y,2,size),tint*.5f);
    }
    private static void InfoSurface(Rectangle r,Color accent,float hover=0)
    {
        if(!InfoVisible(r))return;
        RoundedBox(new(r.X,r.Y+3,r.Width,r.Height),Color.Black*.20f,11);
        RoundedBox(r,Color.Lerp(InfoPanel,new Color(29,39,52),hover)*.98f,11);
        Line(new(r.X+12,r.Y),new(r.Right-12,r.Y),accent*(.12f+hover*.35f));
        if(hover>.01f)Line(new(r.X+14,r.Bottom),new(r.Right-14,r.Bottom),accent*(hover*.27f));
    }
    private static void InfoItem(int type,Vector2 at,float size,float alpha=1)
    {
        if(type<=0)return;Main.instance.LoadItem(type);var texture=TextureAssets.Item[type].Value;
        var frame=Main.itemAnimations[type]?.GetFrame(texture)??texture.Frame();
        float scale=Math.Min(1.6f,size/Math.Max(frame.Width,frame.Height));
        Main.spriteBatch.Draw(texture,at,frame,Color.White*alpha,0,frame.Size()*.5f,scale,SpriteEffects.None,0);
    }
    private static int InfoLines(string value,int x,int y,int width,Color color,float scale=.65f,int maxLines=int.MaxValue,bool draw=true)
    {
        string line="";int lines=0,lineHeight=(int)Math.Ceiling(FontAssets.MouseText.Value.LineSpacing*scale)+3;
        foreach(char c in value)
        {
            if(c=='\r')continue;
            if(c=='\n'||line.Length>0&&FontAssets.MouseText.Value.MeasureString(line+c).X*scale>Math.Max(12,width))
            {
                if(++lines>=maxLines){if(draw)Text(Fit(line+"…",width,scale),new(x,y),color,scale);return y+lineHeight;}
                if(draw)Text(line,new(x,y),color,scale);y+=lineHeight;line="";
            }
            if(c!='\n')line+=c;
        }
        if(line.Length>0){if(draw)Text(line,new(x,y),color,scale);y+=lineHeight;}
        return y;
    }
    private static void InfoBadge(string label,Vector2 at,Color color,bool dot=true)
    {
        if(dot){Box(new((int)at.X,(int)at.Y+5,4,4),color);at.X+=11;}
        Text(label,at,color,.54f);
    }
    private static int InfoHeading(string title,string note,int x,int y,int width,SoulGlyph glyph,Color color)
    {
        if(InfoVisible(new(x,y,width,42)))
        {
            Glyph(glyph,new(x+12,y+13),17,color);
            Text(title,new(x+30,y+1),InfoWhite,.88f);
            float titleWidth=FontAssets.MouseText.Value.MeasureString(title).X*.88f;
            if(width>titleWidth+140)Text(note,new(x+40+titleWidth,y+6),InfoQuiet,.62f);
            Line(new(x,y+32),new(x+width,y+32),color*.12f);
        }
        return y+46;
    }
    private static int InfoBlock(string title,string body,int x,int y,int width,Color color,SoulGlyph glyph=SoulGlyph.Core,float reveal=1)
    {
        int textX=x+18,textWidth=Math.Max(12,width-36);
        int bottom=InfoLines(body,textX,y+50,textWidth,Ink,.75f,draw:false)+18;
        var r=new Rectangle(x,y,width,Math.Max(78,bottom-y));
        if(InfoVisible(r))
        {
            InfoSurface(r,color);Glyph(glyph,new(x+25,y+23),18,color*reveal);
            Text(Fit(title,width-66,.86f),new(x+45,y+11),color*reveal,.86f);
            InfoLines(body,textX,y+50,textWidth,Ink*reveal,.75f);
        }
        return bottom+14;
    }
    private static bool InfoAction(Rectangle r,string label,Color accent,bool enabled=true,SoulGlyph glyph=SoulGlyph.Arrow)
    {
        if(!InfoVisible(r))return false;
        bool hovered=enabled&&InfoHovered(r);float motion=InfoHover("action/"+label,hovered);
        RoundedBox(r,enabled?Color.Lerp(new Color(28,37,49),new Color(41,56,65),motion):new Color(23,28,36),8);
        Line(new(r.X+10,r.Bottom-1),new(r.Right-10,r.Bottom-1),accent*(enabled?.3f+motion*.4f:.08f));
        if(r.Width>100)Glyph(glyph,new(r.Right-22,r.Center.Y),14,enabled?accent:InfoQuiet);
        float scale=Math.Min(.66f,Math.Max(8,r.Width-48)/Math.Max(1,FontAssets.MouseText.Value.MeasureString(label).X));
        Text(label,new(r.X+14,r.Center.Y-10),enabled?InfoWhite:InfoQuiet,scale);
        return enabled&&InfoPressed(r);
    }
    private static void DrawInformationNavigation()
    {
        EnsureEffects();
        foreach(int i in NavigationPages)
        {
            var r=TabBounds(i);bool selectedPage=page==i||page==RecoveryPage&&i==0,hover=r.Contains(Main.mouseX,Main.mouseY);
            Color color=SoulUIPages.Color(i);float motion=InfoHover("nav/"+i,hover||selectedPage);
            if(selectedPage||hover)RoundedBox(r,new Color(24,34,46)*(.55f+.35f*motion),8);
            if(r.Width>=76)
            {
                Glyph(PageGlyph(i),new(r.X+18,r.Center.Y),17,color*(.55f+motion*.45f));
                float scale=Math.Min(.62f,(r.Width-43)/Math.Max(1,FontAssets.MouseText.Value.MeasureString(SoulUIPages.Name(i)).X));
                Text(SoulUIPages.Name(i),new(r.X+34,r.Center.Y-9),selectedPage?InfoWhite:Muted,scale);
            }
            else Glyph(PageGlyph(i),r.Center.ToVector2(),19,color*(.5f+motion*.5f));
            if(selectedPage)Line(new(r.X+12,r.Bottom-1),new(r.Right-12,r.Bottom-1),color*.8f,1.6f);
            if(hover){Main.HoverItem=new Item();Main.hoverItemName=SoulUIPages.Name(i);}
        }
    }
}
