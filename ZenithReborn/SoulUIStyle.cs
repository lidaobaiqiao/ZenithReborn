using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace ZenithReborn;

public partial class SoulUI
{
    private static Texture2D veilTexture;
    private static readonly Color Champagne=new(206,187,143);

    // Scanline corners keep a constant UI-unit radius at any aspect ratio.
    // These are translucent surfaces, not a screen blur or a render-target pass.
    private static void RoundedBox(Rectangle r,Color color,int radius=12)
    {
        if(r.Width<=0 || r.Height<=0)return;
        radius=Math.Min(radius,Math.Min(r.Width,r.Height)/2);
        if(radius<=0){Box(r,color);return;}
        Box(new(r.X,r.Y+radius,r.Width,r.Height-2*radius),color);
        for(int row=0;row<radius;row++)
        {
            int inset=(int)Math.Ceiling(radius-Math.Sqrt(radius*radius-Math.Pow(radius-row-.5,2)));
            Box(new(r.X+inset,r.Y+row,r.Width-2*inset,1),color);
            Box(new(r.X+inset,r.Bottom-1-row,r.Width-2*inset,1),color);
        }
    }
    private static void EnsureVeil()
    {
        if(veilTexture!=null)return;
        const int size=128;Color[] pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float d=Vector2.Distance(new(x,y),new(63.5f))/63.5f;
            float alpha=1-Ease((d-.77f)/.23f);
            pixels[y*size+x]=Color.White*alpha;
        }
        veilTexture=new Texture2D(Main.graphics.GraphicsDevice,size,size);
        veilTexture.SetData(pixels);
    }
    private static void DrawChrome(Rectangle panel,Point mouse)
    {
        // Keep the input rectangle intact, but visually separate the light
        // floating chrome from the soft circular body. No full-panel blue fill.
        var header=new Rectangle(panel.X+8,panel.Y+4,panel.Width-16,60);
        RoundedBox(new(header.X+2,header.Y+3,header.Width,header.Height),Color.Black*.18f,13);
        RoundedBox(header,new Color(13,15,19)*.95f,13);
        float hover=(WindowDragging || HeaderDragBounds().Contains(mouse)) ? .28f : .12f;
        Line(new(header.X+14,header.Y+1),new(header.Right-14,header.Y+1),Champagne*hover);
        Box(new(header.X+1,header.Y+18,2,22),Champagne*.5f);
        // Three understated marks afford dragging without a framed title bar.
        for(int i=0;i<3;i++)Box(new(header.Center.X-9+i*7,header.Y+7,3,1),Champagne*.35f);
        if(page!=0)
            RoundedBox(new(panel.X+8,panel.Y+65,panel.Width-16,43),new Color(13,15,19)*.95f,11);
        int footerWidth=Math.Min(520,panel.Width-24);
        var footer=new Rectangle(panel.X+8,panel.Bottom-48,footerWidth,36);
        RoundedBox(footer,new Color(13,15,19)*.91f,10);
        Line(new(footer.X+12,footer.Y),new(footer.Right-12,footer.Y),Champagne*.12f);
    }
    private static void GroupCaption(string value,Vector2 at,Color color)
    {
        int width=(int)Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(value).X;
        RoundedBox(new((int)at.X-5,(int)at.Y-3,(int)(width*.62f)+12,23),new Color(12,14,18)*.88f,6);
        Text(value,at,color,.62f);
    }
    private static void DrawPageSurface(Rectangle body)
    {
        if(DomainMode)
        {
            if(page!=0)RoundedBox(body,new Color(11,17,26)*.93f,14);
            else if(SideInspector)RoundedBox(new(body.Right-294,body.Y,294,body.Height),new Color(11,17,26)*.90f,14);
            return;
        }
        EnsureVeil();
        if(page==0)
        {
            float radius=RingRadius+112;
            float fade=.65f+.35f*Ease((float)(UiTime-openedAt)/.5f);
            Main.spriteBatch.Draw(veilTexture,RingCenter,null,new Color(9,11,15)*(.97f*fade),0,
                new(64),new Vector2(radius*1.12f/64,radius/64),SpriteEffects.None,0);
            // Scrolling the details into view gives text its own quiet surface.
            int detailTop=body.Y+RingCanvasHeight+6-scroll;
            if(detailTop<body.Bottom)
                RoundedBox(new(body.X+2,detailTop,body.Width-4,body.Bottom-detailTop+12),new Color(12,14,18)*.91f,14);
        }
        else
        {
            RoundedBox(new(body.X-2,body.Y,body.Width+4,body.Height),new Color(12,14,18)*.93f,14);
            Line(new(body.X+16,body.Y),new(body.Right-16,body.Y),Champagne*.16f);
        }
    }
}
