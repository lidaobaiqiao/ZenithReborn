using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace ZenithReborn;

// 剑阵场景只表现当前真实剑位与候选武器，节点的可点击位置不随入场或呼吸动画移动。
public partial class SoulUI
{
    private static void ArmoryItem(Item item,Vector2 at,float size,Color tint,float rotation=0)
    {
        if(item==null||item.IsAir)return;
        Main.instance.LoadItem(item.type);var texture=TextureAssets.Item[item.type].Value;
        var frame=Main.itemAnimations[item.type]?.GetFrame(texture)??texture.Frame();
        float scale=size/Math.Max(frame.Width,frame.Height);
        Main.spriteBatch.Draw(texture,at,frame,tint,rotation,frame.Size()*.5f,scale,SpriteEffects.None,0);
    }
    private static void ArmoryDiamond(Vector2 at,float size,Color tint,float width=1)
    {
        Vector2[] p=[at+new Vector2(0,-size),at+new Vector2(size,0),at+new Vector2(0,size),at-new Vector2(size,0)];
        for(int i=0;i<4;i++)Line(p[i],p[(i+1)%4],tint,width);
    }
    private static void DrawArmoryScene(SoulItem soul,SoulArmoryLayout layout)
    {
        Rectangle area=layout.Stage;int supports=soul.VisibleSupportCount,height=layout.SceneHeight(supports);
        armoryScroll=InfoScroll(area,armoryScroll,height);
        InfoClip(area,()=>
        {
            float time=(float)(UiTime%3600),opening=Ease((float)(UiTime-openedAt)/.7f);
            Vector2 c=layout.Center(supports,armoryScroll);float radius=layout.Radius(supports);
            Color hue=CandidateItem!=null?new(243,203,132):ArmorySlotColor(selected);
            Text("御 剑 配 阵",new(area.X+16,area.Y+8-armoryScroll),InfoWhite,.94f);
            Text($"主攻 {soul.Active.Count()} / {soul.Capacity+soul.BonusCapacity+(soul.LegendaryOn?1:0)}",new(area.Right-126,area.Y+14-armoryScroll),Accent,.65f);
            Text("全收集奖励",new(area.X+18,area.Y+34-armoryScroll),Champagne,.51f);
            if(soul.LegendaryOn||!soul.LegendaryHost.IsAir)Text("传奇",new(area.Right-58,area.Y+34-armoryScroll),Champagne,.51f);
            // 多层低亮刻度形成仪盘深度；亮度集中在正在阅读的剑与节点上。
            Glow(c,radius*1.48f,hue,.16f);
            ArmoryDiamond(c,radius*.71f,hue*.12f);
            Arc(c,radius-29,0,MathHelper.TwoPi,hue*.10f);
            Arc(c,radius+13,-MathHelper.PiOver2,MathHelper.TwoPi*opening,Accent*.22f);
            for(int i=0;i<10;i++)
                if(Available(soul,i)&&!soul.ItemAt(i).IsAir)
                    Arc(c,radius+13,MathHelper.ToRadians(-105+i*36),MathHelper.ToRadians(30),Accent*.42f*opening,1.6f);
            for(int i=0;i<60;i++)
            {
                Vector2 direction=MathHelper.ToRadians(i*6-90).ToRotationVector2();
                Line(c+direction*(radius+20),c+direction*(radius+(i%5==0?27:23)),Accent*(i%5==0?.28f:.11f));
            }
            for(int i=0;i<3;i++)
            {
                float angle=time*.12f+i*MathHelper.TwoPi/3;
                Arc(c,radius-24,angle,.38f,hue*.4f,1.6f);
                Glow(c+(angle+.38f).ToRotationVector2()*(radius-24),4,hue,.7f);
            }
            Rectangle chosen=layout.Slot(selected,supports,armoryScroll);
            // 中央羁绊核心：实时显示主导羁绊，颜色随阶段，满羁绊棱彩。
            DrawBondCore(soul,c,chosen,time,opening);
            foreach(int id in soul.VisibleSlotIds)DrawArmoryNode(soul,id,layout.Slot(id,supports,armoryScroll),area,time,opening);
            int supportY=layout.SupportTop(supports,armoryScroll);
            Text($"共鸣支援   {soul.Supports.Count()} / {soul.SupportCapacity}",new(area.X+16,supportY-29),new Color(184,163,233),.66f);
            if(area.Width>430)Text("提供特性 · 不独立出剑",new(area.Right-162,supportY-27),InfoQuiet,.54f);
            InfoScrollMark(area,armoryScroll,height,Accent);
        });
    }
    private static void DrawArmoryNode(SoulItem soul,int id,Rectangle r,Rectangle clip,float time,float opening)
    {
        if(!r.Intersects(clip))return;
        var item=soul.ItemAt(id);bool unlocked=Available(soul,id),focus=selected==id;
        bool hover=clip.Contains(Main.mouseX,Main.mouseY)&&r.Contains(Main.mouseX,Main.mouseY);
        float motion=InfoHover("armory/"+id,hover||focus),appear=Ease(((float)(UiTime-openedAt)-Math.Min(id,16)*.018f)/.4f);
        Color hue=ArmorySlotColor(id),ink=unlocked?hue:new Color(92,109,129);Vector2 at=r.Center.ToVector2();
        RoundedBox(r,Color.Lerp(new Color(14,23,34),new Color(31,50,62),motion),10);
        if(focus||hover)Glow(at,r.Width*1.15f,hue,.27f*motion);
        if(id<10)Arc(at,r.Width*.58f,-MathHelper.PiOver2,MathHelper.TwoPi,ink*(focus?.8f:.27f),focus?1.7f:1);
        else Line(new(r.X+8,r.Bottom),new(r.Right-8,r.Bottom),ink*(focus?.9f:.35f),focus?2:1);
        if(focus)
        {
            ArmoryDiamond(at,r.Width*.78f,hue*.45f);
            Glow(at+new Vector2(0,-r.Height*.65f),4,hue,.85f);
        }
        if(!item.IsAir)ArmoryItem(item,at-new Vector2(0,2),r.Width-14,(unlocked?Color.White:new Color(133,142,156))*appear);
        else if(unlocked)Glyph(SoulGlyph.Spark,at-new Vector2(0,3),18,ink*.65f);
        else Glyph(SoulGlyph.Lock,at-new Vector2(0,3),15,ink*.6f);
        CenterText(ArmorySlotCode(id),new(at.X,r.Bottom-5),ink,.5f);
        if(hover)
        {
            HoveredSlotRequirement=SlotName(id)+" · "+(item.IsAir?unlocked?"空位 · 点击选中":"未解锁 · 点击查看条件":item.Name);
            Main.HoverItem=new Item();Main.hoverItemName="";
            if(Main.mouseLeft&&Main.mouseLeftRelease)
            {
                Main.mouseLeftRelease=false;SelectSlot(id);
                if(!Main.mouseItem.IsAir)
                {bool ok=soul.TrySwapHost(id,ref Main.mouseItem,out string error);if(ok){Sync(soul);TriggerCoreFlow(id,UiTime);}DomainNote(ok,ok?"武器已归位":error);}
            }
        }
    }
    private static void DrawArmoryAttunement(SoulArmoryLayout layout)
    {
        float age=(float)(UiTime-attunedAt);if(age<0||age>1.05f||editing==null)return;
        Vector2 end=layout.Slot(attunedSlot,editing.VisibleSupportCount,armoryScroll).Center.ToVector2();
        if(!layout.Stage.Contains(end.ToPoint()))return;
        if(age<.55f)
        {
            float progress=Ease(age/.55f);Vector2 start=layout.Compact?layout.Center(editing.VisibleSupportCount,armoryScroll):attunedFrom;
            Vector2 Position(float p)=>Vector2.Lerp(start,end,p)+new Vector2(0,-MathF.Sin(p*MathHelper.Pi)*74);
            Vector2 previous=Position(Math.Max(0,progress-.25f));
            for(int i=1;i<=18;i++)
            {float p=MathHelper.Lerp(Math.Max(0,progress-.25f),progress,i/18f);Vector2 next=Position(p);Line(previous,next,Accent*(i/18f)*.75f,1+i/18f*2);previous=next;}
            ArmoryItem(attunedItem,Position(progress),36,Color.White);
        }
        else
        {
            float p=(age-.55f)/.5f;Arc(end,28+p*42,0,MathHelper.TwoPi,Accent*(1-p)*.75f,2*(1-p)+.5f);
            for(int i=0;i<8;i++)Glow(end+(i*MathHelper.PiOver4).ToRotationVector2()*(22+p*48),4,Champagne,(1-p)*.9f);
        }
    }
}
