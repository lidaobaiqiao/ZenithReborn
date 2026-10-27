using Microsoft.Xna.Framework;
using Terraria;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class InformationUiCheck
{
    internal static void Run(Action<bool,string> check)
    {
        int width=Main.screenWidth,height=Main.screenHeight;bool domain=SoulUI.DomainMode;
        try
        {
            foreach(int w in new[]{224,400,600,760,960,1440})
            {
                int cols=SoulInfoLayout.Columns(w);var area=new Rectangle(20,100,w,2000);
                var cells=Enumerable.Range(0,21).Select(i=>SoulInfoLayout.Tile(area,i,cols)).ToArray();
                check(cells.All(r=>r.Width>=180&&r.Left>=area.Left&&r.Right<=area.Right),"信息卡在窄屏与宽屏保持实际可读宽度");
                check(!cells.Where((r,i)=>cells.Skip(i+1).Any(r.Intersects)).Any(),"多列卡片具有独立命中框，不互相覆盖");
                check(cells.All(r=>r.Height==130),"入场和悬停不改变卡片命中区域");
            }
            SoulUI.DomainMode=true;
            foreach(int w in new[]{400,640,800,1280,1920})
            {
                Main.screenWidth=w;Main.screenHeight=720;
                foreach(int page in new[]{SoulUI.TraitsPage,2,SoulUI.EnvironmentPage})
                {
                    SoulUI.SelectPage(page);var b=SoulUI.ContentBounds();
                    check(SoulUI.PanelBounds().Contains(b)&&b.Width>=w*.85f,"信息页使用完整工作区，不被旧剑库边栏挤占");
                    check(SoulUI.NavigationPages.All(i=>SoulUI.PanelBounds().Contains(SoulUI.TabBounds(i))),"四个任务入口在信息页保持可见");
                }
            }
            check(SoulUI.NavigationPages.SequenceEqual(new[]{0,1,2,5})&&!SoulUI.NavigationPages.Contains(SoulUI.RecoveryPage),"主导航只有四项，旧物回收不常驻");
            check(SoulUIPages.Resolve(3,false)==0&&SoulUIPages.Resolve(4,false)==0&&SoulUIPages.Resolve(4,true)==4,"旧图鉴页面已删除，回收只在有旧物时可进入");
            foreach(int w in new[]{224,400,640,960,1440})
            {
                var area=new Rectangle(20,120,w,560);var panes=SoulInfoLayout.MasterDetail(area);
                check(area.Contains(panes.List)&&area.Contains(panes.Detail)&&!panes.List.Intersects(panes.Detail),"列表和详情各自固定在工作区内，互不覆盖");
                check(w<850?panes.Detail.Top>panes.List.Bottom:panes.Detail.Left>panes.List.Right,"窄屏上下排列，宽屏左右分栏");
            }
            float value=0;
            for(int i=0;i<30;i++){float next=SoulInfoLayout.Approach(value,1,1/60f);check(next>=value&&next<=1,"悬停动画单调收敛，不闪烁或越界");value=next;}
            check(value>.99f&&SoulInfoLayout.Ease(0)==0&&SoulInfoLayout.Ease(1)==1,"短动画在半秒内收敛，端点固定");
        }
        finally{Main.screenWidth=width;Main.screenHeight=height;SoulUI.DomainMode=domain;SoulUI.SelectPage(0);}
    }
}
