using Microsoft.Xna.Framework;
using Terraria;

namespace ZenithReborn;

// 配阵主页的全部几何使用界面单位；装饰动画不参与布局和命中计算。
public readonly record struct SoulArmoryLayout(Rectangle Workspace,Rectangle Library,Rectangle Stage,Rectangle Detail,bool Compact)
{
    public static SoulArmoryLayout Create(int width,int height)
    {
        int margin=width<600?12:24;
        var work=new Rectangle(margin,116,Math.Max(1,width-margin*2),Math.Max(1,height-164));
        bool compact=width<1180||height<620;
        if(compact)
        {
            var pane=new Rectangle(work.X,work.Y+44,work.Width,Math.Max(1,work.Height-44));
            return new(work,pane,pane,pane,true);
        }
        int left=Math.Clamp(width/5,244,292),right=Math.Clamp(width/4,292,340);
        return new(work,new(work.X,work.Y,left,work.Height),
            new(work.X+left+18,work.Y,Math.Max(1,work.Width-left-right-36),work.Height),
            new(work.Right-right,work.Y,right,work.Height),false);
    }
    public Rectangle Mode(int index)=>new(Workspace.X+index*(Workspace.Width/3),Workspace.Y,Workspace.Width/3-6,32);
    public int SupportColumns=>Math.Max(1,Math.Min(6,(Stage.Width-28)/56));
    public int SceneHeight(int supports)=>Math.Max(Stage.Height,510+Math.Max(0,(supports+SupportColumns-1)/SupportColumns-2)*58);
    public float Radius(int supports)=>Math.Clamp(Math.Min((Stage.Width-108)*.5f,
        Math.Min((SceneHeight(supports)-230)*.43f,(SceneHeight(supports)-((supports+SupportColumns-1)/SupportColumns)*58-172)*.5f)),102,198);
    public Vector2 Center(int supports,int scroll)=>new(Stage.Center.X,Stage.Y+94+Radius(supports)-scroll);
    public int SupportTop(int supports,int scroll)=>Stage.Y+SceneHeight(supports)-((supports+SupportColumns-1)/SupportColumns)*58-26-scroll;
    public Rectangle Slot(int id,int supports,int scroll)
    {
        Vector2 center=Center(supports,scroll);int size=Stage.Width<420?40:48;
        if(id<10)
        {
            Vector2 at=center+MathHelper.ToRadians(-90+id*36).ToRotationVector2()*Radius(supports);
            return new((int)at.X-size/2,(int)at.Y-size/2,size,size);
        }
        if(id==SoulItem.LegendarySlotId)return new(Stage.Right-62,Stage.Y+53-scroll,44,44);
        if(id is 15 or 16)return Stage.Width<420?
            new(Stage.X+12+(id-15)*48,Stage.Y+53-scroll,40,40):
            new(Stage.X+18+(id-15)*58,Stage.Y+53-scroll,44,44);
        int ordinal=SoulItem.SupportOrdinal(id),columns=SupportColumns;
        int start=Stage.Center.X-columns*56/2;
        return new(start+ordinal%columns*56,SupportTop(supports,scroll)+ordinal/columns*58,44,44);
    }
    public Rectangle LibraryList=>new(Library.X+12,Library.Y+(Library.Height<240?46:106),Math.Max(1,Library.Width-24),
        Math.Max(1,Library.Height-(Library.Height<240?64:142)));
    public Rectangle LibraryRow(int ordinal,int scroll)=>new(LibraryList.X,LibraryList.Y+ordinal*72-scroll,LibraryList.Width-4,66);
    public Rectangle Confirm=>Detail.Height<260?new(Detail.X+12,Detail.Bottom-38,Math.Max(1,Detail.Width/2-18),30):
        new(Detail.X+16,Detail.Bottom-100,Math.Max(1,Detail.Width-32),40);
    public Rectangle Return=>Detail.Height<260?new(Detail.Center.X+6,Detail.Bottom-38,Math.Max(1,Detail.Width/2-18),30):
        new(Detail.X+16,Detail.Bottom-52,Math.Max(1,Detail.Width-32),32);
    public Rectangle DetailBody=>new(Detail.X+16,Detail.Y+12,Math.Max(1,Detail.Width-32),Math.Max(1,Detail.Height-(Detail.Height<260?62:130)));
}
