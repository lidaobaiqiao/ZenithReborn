using System.Text.Json;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 比较同一固定目标和武器下的真实玩家与生产代理；记录未能验证的情况。
internal static class LivingMeleeMatrix
{
    private static object Trial(int type,bool useProxy)
    {
        foreach(var p in Main.projectile)p.active=false;
        foreach(var n in Main.npc)n.active=false;
        Main.rand=new UnifiedRandom(318);Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;Main.gameMenu=false;
        Main.GameViewMatrix??=new Terraria.Graphics.SpriteViewMatrix(null);
        Main.screenPosition=Vector2.Zero;Main.screenWidth=1920;Main.screenHeight=1080;Main.mouseX=1180;Main.mouseY=820;
        var owner=new Player{whoAmI=0,active=true,direction=1,position=new Vector2(1000,800)};Main.player[0]=owner;owner.ResetEffects();
        var target=new NPC();target.SetDefaults(NPCID.Zombie);target.whoAmI=20;target.active=true;
        target.life=target.lifeMax=1000000;target.HideStrikeDamage=true;target.defense=0;target.noTileCollide=true;target.knockBackResist=0;
        target.Center=owner.Center+new Vector2(180,0);Main.npc[20]=target;
        LivingArmament summoned=null;Player actor=owner;
        if(useProxy)
        {
            var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;soul.LegacyReleased=63;
            soul.Hosts[0]=new Item(type);soul.Hosts[1]=new Item(ItemID.EnchantedSword);
            owner.inventory[0]=soul.Item;owner.selectedItem=0;soul.RefreshStats();
            owner.GetModPlayer<LivingArmamentPlayer>().PostUpdate();
            var p=Main.projectile.First(q=>q.active&&q.ModProjectile is LivingArmament&&(int)q.ai[0]==0);
            p.Center=target.Center-new Vector2(48,0);summoned=(LivingArmament)p.ModProjectile;
        }
        else
        {
            owner.inventory[0]=new Item(type);owner.selectedItem=0;owner.Center=target.Center-new Vector2(48,0);
            owner.lastVisualizedSelectedItem=owner.HeldItem.Clone();owner.controlUseItem=true;owner.releaseUseItem=true;
        }
        var types=new SortedSet<int>();string error="";int hits=0;
        var field=typeof(LivingArmament).GetField("proxy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        try
        {
            for(int t=0;t<90;t++)
            {
                if(useProxy)
                {
                    var instance=field.GetValue(summoned);
                    if(instance!=null)actor=(Player)instance.GetType().GetProperty("Player").GetValue(instance);
                }
                if(!useProxy || !ReferenceEquals(actor,owner))
                {
                    Main.dedServ=false;
                    try{actor.ItemCheck_ApplyUseStyle(0,actor.HeldItem,new Rectangle(0,0,32,32));}
                    finally{Main.dedServ=true;}
                }
                int previous=target.life;target.immune[0]=0;
                if(useProxy)summoned.AI();else{owner.releaseUseItem=true;owner.ItemCheck();}
                if(target.life<previous)hits++;
                foreach(var p in Main.projectile)
                    if(p.active && p.ModProjectile is not LivingArmament){types.Add(p.type);}
            }
        }
        catch(Exception e){error=e.ToString();}
        return new{hits,types=types.ToArray(),error};
    }
    public static void Run()
    {
        // 专用服务器默认没有活动光照引擎；仅在隔离矩阵中初始化，保留原版发光路径。
        Lighting.Initialize();
        var rows=new List<object>();
        // 默认沿用原版范围；显式开启后还会统计当前隔离档实际加载的模组武器。
        bool includeMods=Environment.GetEnvironmentVariable("ZENITH_MELEE_INCLUDE_MODS")=="1";
        foreach(var item in ContentSamples.ItemsByType.Values.Where(i=>i.type>0&&(includeMods||i.type<ItemID.Count)&&SoulItem.CanHost(i)).OrderBy(i=>i.type))
        {
            string identity=ItemLoader.GetItem(item.type)?.FullName??ItemID.Search.GetName(item.type)??item.type.ToString();
            rows.Add(new{id=item.type,name=identity,modded=item.type>=ItemID.Count,channel=item.channel,shoot=item.shoot,style=item.shoot>0&&ContentSamples.ProjectilesByType.TryGetValue(item.shoot,out var sample)?sample.aiStyle:-1,normal=Trial(item.type,false),proxy=Trial(item.type,true)});
        }
        string path=Environment.GetEnvironmentVariable("ZENITH_MELEE_MATRIX_OUTPUT");
        System.IO.File.WriteAllText(path,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
    }
}
