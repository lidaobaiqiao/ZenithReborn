using Microsoft.Xna.Framework;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class OrePierceCheck
{
    internal static void Run(Action<bool,string> check)
    {
        EnchantmentVisuals(check);
        var ends=new OreWeaponEndHistory();
        var small=new OreWeaponEnd(new(100,90),new(100,90),Vector2.UnitX,Color.Cyan,1,1,false);
        var giant=small with {BladeAt=new(100,700),Anchor=new(100,400),Giant=true};
        check(!ends.Emit(small with {Anchor=new(float.NaN,0)}) && !ends.Emit(small with {Scale=0}),"Invalid endings cannot enter graphics history");
        ends.Emit(small);ends.Emit(giant);
        for(int i=0;i<20;i++)ends.Update();
        check(ends.Ends.Count==1 && ends.Ends[0].Giant && ends.Ends[0].Anchor.Y==400 && ends.Ends[0].BladeAt.Y==700,
            "Giant aftermath survives small-blade lifetime and retains contact separate from final blade pose");
        for(int i=0;i<16;i++)ends.Update();
        check(ends.Ends.Count==0,"Independent aftermath expires without any live weapon");
        for(int i=0;i<80;i++)ends.Emit(small with {BladeAt=new(i,0)});
        check(ends.Ends.Count==OreWeaponEndHistory.Capacity && ends.Ends[0].BladeAt.X==16,"Dense ending events have a fixed memory budget");
        ends.Clear();check(ends.Ends.Count==0,"World cleanup also clears weapon endings");
    }
    private static void EnchantmentVisuals(Action<bool,string> check)
    {
        int[] types=[Terraria.ModLoader.ModContent.ProjectileType<SoulBlade>(),
            Terraria.ModLoader.ModContent.ProjectileType<OreFlyingSword>(),
            Terraria.ModLoader.ModContent.ProjectileType<OreSkyfall>(),
            Terraria.ModLoader.ModContent.ProjectileType<MartialBlade>(),
            Terraria.ModLoader.ModContent.ProjectileType<MartialGiant>()];
        var player=Terraria.Main.player[0];byte oldEnchant=player.meleeEnchant;
        var oldNpc=Terraria.Main.npc[199];
        try
        {
            player.meleeEnchant=3;
            foreach(int type in types)
            {
                var projectile=new Terraria.Projectile();projectile.SetDefaults(type);projectile.owner=0;
                check(projectile.noEnchantmentVisuals&&!projectile.noEnchantments,"剑体关闭附魔粒子但保留减益能力："+type);
                var target=new Terraria.NPC();target.SetDefaults(Terraria.ID.NPCID.Zombie);Terraria.Main.npc[199]=target;
                projectile.StatusNPC(199);
                check(target.HasBuff(Terraria.ID.BuffID.OnFire),"关闭剑体粒子后原版火焰附魔仍能命中施加："+type);
            }
        }
        finally{player.meleeEnchant=oldEnchant;Terraria.Main.npc[199]=oldNpc;}
    }
}
