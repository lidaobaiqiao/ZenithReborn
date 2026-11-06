using System.Reflection;
using Terraria;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 在独立剑魂上覆盖六组封印及关键边界；只运行本次修改对应的界面渲染。
internal static class BossUnlockUiRenderCheck
{
    internal delegate void Frame(string name,SoulCurse curse,int width=1440,int height=900,float age=0,int offset=0);
    internal static object Run(SoulItem original,Player player,Frame render,out bool passed)
    {
        Item[] inventory=player.inventory;player.inventory=inventory.Select(i=>i.Clone()).ToArray();
        var soul=(SoulItem)player.inventory[0].ModItem;bool unchanged=true,scaledBoundsFit=true;
        try
        {
            SoulUI.Open(soul);soul.Attached=true;soul.LegacyReleased=0;
            void Capture(string name,SoulCurse curse,int width=1440,int height=900,float age=0,int offset=0)
            {
                using var before=new MemoryStream();soul.NetSend(new BinaryWriter(before));
                render(name,curse,width,height,age,offset);
                scaledBoundsFit&=SoulUI.PanelBounds().Right*Main.UIScale<=width+1&&SoulUI.PanelBounds().Bottom*Main.UIScale<=height+1;
                using var after=new MemoryStream();soul.NetSend(new BinaryWriter(after));
                unchanged&=before.ToArray().SequenceEqual(after.ToArray());
            }
            foreach(SoulCurse curse in Enum.GetValues<SoulCurse>())
            {
                soul.Seals=0;Capture("boss-"+curse+"-locked",curse);
                soul.Seals=SoulCurses.All;Capture("boss-"+curse+"-blessed",curse);
            }
            soul.Seals=SoulCurses.Brain;Capture("boss-switching-brain-first",SoulCurse.Switching);
            soul.Seals=SoulCurses.Cultist;Capture("boss-switching-cultist-first",SoulCurse.Switching);
            soul.Seals=SoulCurses.Retinazer;Capture("boss-twins-half",SoulCurse.Solitude);
            soul.Seals=SoulCurses.Bee;Capture("boss-barrier-weakened",SoulCurse.Thinblade);
            soul.Seals=0;soul.LegacyReleased=1<<(int)SoulCurse.Switching;
            Capture("boss-legacy-release",SoulCurse.Switching);soul.LegacyReleased=0;
            soul.Seals=SoulCurses.All;soul.Attached=false;
            Capture("boss-detached",SoulCurse.Switching);soul.Attached=true;
            soul.Seals=SoulCurses.Brain;
            for(int i=0;i<8;i++)Capture("animation/boss-switching-"+i.ToString("D2"),SoulCurse.Switching,age:i*.13f);
            Capture("boss-narrow-top",SoulCurse.Switching,640,720);
            Capture("boss-narrow-end",SoulCurse.Switching,640,720,offset:10000);
            Capture("boss-small",SoulCurse.Bluntness,960,720,offset:10000);
            Main.UIScale=1.25f;Capture("boss-ui-scale125",SoulCurse.Switching,1600,1000);Main.UIScale=1;
            soul.Seals=SoulCurses.Brain;Capture("boss-scroll-end",SoulCurse.Bluntness,offset:10000);
            int scroll=(int)typeof(SoulUI).GetField("traitDetailScroll",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            bool scrollClamped=scroll>0&&scroll<10000;
            passed=unchanged&&scrollClamped&&scaledBoundsFit;
            return new{passed,readOnlySnapshotUnchanged=unchanged,scrollClamped,scaledBoundsFit,allSixGroups=true,targets=15,
                animatedSamples=8,twinsPartial=true,reverseOrder=true,legacy=true,detached=true,narrow=true,uiScale125=true};
        }
        finally{Main.UIScale=1;player.inventory=inventory;SoulUI.Open(original);}
    }
}
